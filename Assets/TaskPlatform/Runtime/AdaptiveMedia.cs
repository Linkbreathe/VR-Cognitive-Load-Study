using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace EvidenceVR
{
    [Serializable] public sealed class AdaptiveMediaItem
    {
        public string id, work, source, category, emotion, audioFile, videoFile, audioSha256, videoSha256, rightsRecord;
        public string split, notes;
        public float duration, gain = 1;
        public bool ready, volumeReviewed, synthetic;
    }
    [Serializable] public sealed class AdaptiveMediaManifest
    {
        public string version;
        public AdaptiveMediaItem[] items;
        public bool HasValidLayout()
        {
            if(items==null || items.Any(m=>m==null || string.IsNullOrWhiteSpace(m.id)))return false;
            int training=items.Count(m=>m.split=="train");
            return training>=6 && training%2==0 && items.Length==training+4 && items.Select(m=>m.id).Distinct().Count()==items.Length &&
                items.Count(m=>m.split=="test_a")==2 && items.Count(m=>m.split=="test_b")==2;
        }
    }
    [Serializable] public sealed class AdaptiveExposure
    {
        public string mediaId, modality, stage;
        public int block, level, previousExposures;
        public double onset, duration;
    }
    public sealed class AdaptiveMediaScheduler
    {
        readonly AdaptiveTrainingConfig config;
        readonly AdaptiveMediaManifest manifest;
        readonly Dictionary<string,int> exposureCounts=new Dictionary<string,int>();
        public AdaptiveMediaScheduler(AdaptiveTrainingConfig c,AdaptiveMediaManifest m){config=c;manifest=m;}
        public List<AdaptiveExposure> PlanPractice(double blockDuration)
        {
            // Practice uses only the participant's AV-assigned training clips; held-out clips stay unseen.
            var pool=manifest.items.Where(m=>m.split=="train").OrderBy(m=>m.id,StringComparer.Ordinal)
                .Where((m,i)=>i%2==(1+config.counterbalanceGroup)%2).ToList();
            var rng=new StableRandom(unchecked(config.seed+7919));
            for(int i=pool.Count-1;i>0;i--){int j=rng.Next(i+1);var item=pool[i];pool[i]=pool[j];pool[j]=item;}
            var result=new List<AdaptiveExposure>();
            for(int i=0;i<config.practiceMediaOnsets.Length;i++)
            {
                var item=i<pool.Count?pool[i]:null;
                double onset=config.practiceMediaOnsets[i]+(rng.Next(2001)/1000.0-1)*config.practiceOnsetJitterSeconds;
                double duration=item?.duration??12;
                if(onset+duration>blockDuration)continue;
                result.Add(new AdaptiveExposure{mediaId=item?.id??"unavailable",modality="audiovisual",stage="practice",block=0,level=2,
                    previousExposures=item!=null && exposureCounts.ContainsKey(item.id)?exposureCounts[item.id]:0,onset=onset,duration=duration});
            }
            return result;
        }
        public List<AdaptiveExposure> Plan(int block,int level,string stage)
        {
            var rng=new StableRandom(unchecked(config.seed+block*104729));
            var plan=new List<AdaptiveExposure>();
            string split=stage=="training"||stage=="calibration"?"train":((stage=="pretest")==(config.counterbalanceGroup/2==0)?"test_a":"test_b");
            var pool=manifest.items.Where(m=>m.split==split).OrderBy(m=>m.id,StringComparer.Ordinal).ToList();
            // Assign each pair to one modality per participant; exchange allocation between groups.
            if(level>0)pool=pool.Where((m,i)=>i%2==(level-1+config.counterbalanceGroup)%2).ToList();
            int count=stage=="pretest"||stage=="posttest"?1:3;
            for(int i=pool.Count-1;i>0;i--){int j=rng.Next(i+1);var v=pool[i];pool[i]=pool[j];pool[j]=v;}
            pool=pool.OrderBy(m=>exposureCounts.ContainsKey(m.id)?exposureCounts[m.id]:0).ToList();
            for(int i=0;i<count;i++)
            {
                var item=level==0?null:(i<pool.Count?pool[i]:null);
                int previous=item!=null && exposureCounts.ContainsKey(item.id)?exposureCounts[item.id]:0;
                if(item!=null && previous>0 && !config.allowMediaRepeats)item=null;
                plan.Add(new AdaptiveExposure{block=block,level=level,stage=stage,mediaId=level==0?"silent_slot":item?.id??"unavailable",
                    modality=level==0?"silent":level==1?"audio":"audiovisual",previousExposures=previous,
                    onset=config.mediaOnsets[count==1?1:i]+(rng.Next(2001)/1000.0-1)*config.onsetJitterSeconds,duration=item?.duration??12});
            }
            return plan;
        }
        public void Presented(string id){if(id=="silent_slot"||id=="unavailable")return;exposureCounts[id]=(exposureCounts.ContainsKey(id)?exposureCounts[id]:0)+1;}
    }

    public sealed class AdaptiveMedia : MonoBehaviour
    {
        TaskPlatformController owner;
        TaskEngine engine;
        AdaptiveTrainingConfig config;
        AdaptiveMediaManifest manifest;
        AdaptiveMediaScheduler scheduler;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,string> videos=new Dictionary<string,string>();
        List<AdaptiveExposure> plan=new List<AdaptiveExposure>();
        AdaptiveExposure active;
        VideoPlayer player;
        AudioSource source;
        Renderer screen;
        Material neutral,videoMaterial;
        RenderTexture texture;
        double blockStart,activeEnd;
        int next;
        bool firstFrame,started;
        public bool Ready {get;private set;}
        public bool BlockFailed {get;private set;}
        public bool IsPlaying=>active!=null;
        public bool IsVideoVisible=>active!=null && active.level==2 && started && firstFrame && screen!=null && screen.enabled && player!=null && player.isPlaying;
        public string ActiveId=>active?.mediaId??"none";
        public Transform Screen=>screen?.transform;
        public double PlannedOnset(AdaptiveExposure e)=>blockStart+e.onset;
        public string ManifestJson {get;private set;}
        public event Action<AdaptiveExposure,string,double,string> OnMedia;
        public void Bind(TaskPlatformController controller,AdaptiveTrainingConfig c)
        {
            owner=controller;engine=owner.Engine;config=c;
            if(screen==null)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="Adaptive conversation window";Destroy(go.GetComponent<Collider>());
                var wall=GameObject.Find("NeutralRoom/FrontWall");
                if(wall==null)throw new InvalidOperationException("Stage 2 requires the front wall reference.");
                go.transform.SetParent(wall.transform.parent,false);screen=go.GetComponent<Renderer>();
                go.transform.position=wall.transform.position-wall.transform.forward*(wall.transform.lossyScale.z*.5f+.025f);
                go.transform.rotation=wall.transform.rotation;
                go.transform.localScale=new Vector3(c.videoWidth,c.videoHeight,1);
                screen.enabled=false;
                neutral=new Material(Shader.Find("Universal Render Pipeline/Unlit"));neutral.color=LabPalette.Wall;screen.sharedMaterial=neutral;
                videoMaterial=new Material(neutral);texture=new RenderTexture(1280,720,0);texture.Create();videoMaterial.mainTexture=texture;videoMaterial.color=Color.white;
                source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.dopplerLevel=0;source.minDistance=3.5f;source.maxDistance=10;source.loop=false;
                player=go.AddComponent<VideoPlayer>();player.playOnAwake=false;player.isLooping=false;player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=texture;
                player.audioOutputMode=VideoAudioOutputMode.AudioSource;player.controlledAudioTrackCount=1;player.SetTargetAudioSource(0,source);
                player.sendFrameReadyEvents=true;player.frameReady+=OnFrame;player.errorReceived+=(p,error)=>Stop("failure",error);
                player.loopPointReached+=p=>{Stop("ended","media_end");PrepareNext();};
            }
            engine.OnEvent+=OnEvent;
            StartCoroutine(Load());
        }
        IEnumerator Load()
        {
            string path=Path.Combine(Application.persistentDataPath,config.mediaManifest);
            if(File.Exists(path))ManifestJson=File.ReadAllText(path);
            else {var asset=Resources.Load<TextAsset>("adaptive-media-manifest");ManifestJson=asset!=null?asset.text:"{\"items\":[]}";}
            manifest=JsonUtility.FromJson<AdaptiveMediaManifest>(ManifestJson);
            scheduler=new AdaptiveMediaScheduler(config,manifest);
            Ready=manifest.HasValidLayout() &&
                manifest.items.All(m=>m.ready && m.volumeReviewed && (config.sessionType!="participant"||!m.synthetic) && m.gain>=0 && m.gain<=1 &&
                !string.IsNullOrEmpty(m.source) && !string.IsNullOrEmpty(m.rightsRecord) && m.duration>=10 && m.duration<=12 && !string.IsNullOrEmpty(m.audioSha256) && !string.IsNullOrEmpty(m.videoSha256));
            foreach(var item in manifest.items.Where(m=>m.ready))
            {
                string audio=Path.Combine(Application.persistentDataPath,"AdaptiveMedia",Path.GetFileName(item.audioFile));
                string video=Path.Combine(Application.persistentDataPath,"AdaptiveMedia",Path.GetFileName(item.videoFile));
                if(!File.Exists(audio)||!File.Exists(video)){Ready=false;continue;}
                if(Hash(audio)!=item.audioSha256 || Hash(video)!=item.videoSha256){Ready=false;continue;}
                using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(audio).AbsoluteUri,AudioType.WAV))
                {
                    yield return request.SendWebRequest();
                    if(request.result==UnityWebRequest.Result.Success){clips[item.id]=DownloadHandlerAudioClip.GetContent(request);videos[item.id]=video;}
                    else Ready=false;
                }
            }
            File.WriteAllText(Path.Combine(owner.DataPath,"adaptive-media-manifest.json"),ManifestJson);
            engine.Note("adaptive_media_ready","complete="+Ready+";loaded_pairs="+clips.Count,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            if(engine.Phase==TaskPhase.Running)PrepareNext();
        }
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
        void OnEvent(TaskEvent e)
        {
            if(e.type=="block_start")
            {
                Stop("interrupted","block_boundary");next=0;BlockFailed=false;engine.AdaptiveEvaluationBlocked=false;
                blockStart=e.planned+(!engine.Practice && engine.Block==1?9:0);
                plan=scheduler==null?new List<AdaptiveExposure>():engine.Practice?
                    scheduler.PlanPractice((owner.SessionConfig.hardPracticeTrials+owner.SessionConfig.warmupTrials)*owner.SessionConfig.trialSeconds):
                    scheduler.Plan(engine.Block,engine.Adaptive.Level,engine.Adaptive.Stage);
                foreach(var slot in plan)OnMedia?.Invoke(slot,"planned",blockStart+slot.onset,"");
                PrepareNext();
            }
            if(e.type=="adaptive_level_changed")
            {
                var replacement=scheduler==null?new List<AdaptiveExposure>():scheduler.Plan(engine.Block,engine.Adaptive.Level,engine.Adaptive.Stage);
                for(int i=next;i<plan.Count;i++)
                {
                    var old=plan[i];OnMedia?.Invoke(old,"cancelled",e.actual,"level_change_replan");
                    var slot=replacement[i];slot.onset=old.onset;
                    plan[i]=slot;OnMedia?.Invoke(slot,"planned",e.actual,"level_change;original_slot_time_retained");
                }
                PrepareNext();
            }
            if(e.type=="block_complete"){Stop("interrupted","block_complete");plan.Clear();next=0;}
            if(e.type=="pause" || e.type=="session_aborted" || e.type=="session_complete")Stop("interrupted",e.type);
            if(e.type=="pause")
            {
                BlockFailed=true;engine.AdaptiveEvaluationBlocked=true;
                for(int i=next;i<plan.Count;i++)OnMedia?.Invoke(plan[i],"skipped",e.actual,"remaining_slot_cancelled_by_pause");
                next=plan.Count;
            }
            if(e.type=="resume")
            {next=plan.Count;engine.Note("adaptive_media_cancelled","remaining slots skipped after pause;block invalid",e.actual,e.frame);}
        }
        void PrepareNext()
        {
            if(next<plan.Count && plan[next].level==2 && videos.TryGetValue(plan[next].mediaId,out var path))
            {player.url=path;player.Prepare();}
        }
        public void Tick(double now)
        {
            if(engine==null||engine.Phase!=TaskPhase.Running)return;
            if(active!=null && now>=activeEnd){Stop(started?"ended":"failure",started?"fixed_slot_end":"no_video_frame");PrepareNext();}
            if(next>=plan.Count || now<blockStart+plan[next].onset)return;
            active=plan[next++];activeEnd=blockStart+active.onset+active.duration;started=false;firstFrame=false;
            if(active.level==0){OnMedia?.Invoke(active,"silent_slot",now,"");active=null;return;}
            if(now>=activeEnd || !clips.ContainsKey(active.mediaId) || (active.level==2&&!player.isPrepared))
            {Stop("failure","missing_or_not_prepared_or_missed_slot");PrepareNext();return;}
            var item=manifest.items.First(m=>m.id==active.mediaId);source.volume=config.mediaVolume*item.gain;
            OnMedia?.Invoke(active,"play_requested",now,"fixed_source;software_timestamp");
            if(active.level==1){source.clip=clips[active.mediaId];source.Play();started=true;scheduler.Presented(active.mediaId);OnMedia?.Invoke(active,"started",now,"audio_play_call");}
            else {source.clip=null;player.Play();}
        }
        void OnFrame(VideoPlayer p,long frame)
        {
            if(active==null||active.level!=2||firstFrame)return;
            firstFrame=started=true;screen.sharedMaterial=videoMaterial;screen.enabled=true;scheduler.Presented(active.mediaId);
            OnMedia?.Invoke(active,"started",Time.realtimeSinceStartupAsDouble,"first_decoded_frame;not_photon_onset");
        }
        void Stop(string status,string detail)
        {
            if(active!=null)
            {
                if(status!="ended")
                {
                    BlockFailed=true;engine.AdaptiveEvaluationBlocked=true;
                    engine.ResetRolling(Time.realtimeSinceStartupAsDouble,Time.frameCount,"media_"+status);
                }
                OnMedia?.Invoke(active,status,Time.realtimeSinceStartupAsDouble,detail);
            }
            active=null;if(source!=null)source.Stop();if(player!=null)player.Stop();if(screen!=null){screen.sharedMaterial=neutral;screen.enabled=false;}
        }
        void OnDestroy()
        {
            if(engine!=null)engine.OnEvent-=OnEvent;Stop("interrupted","destroy");
            foreach(var clip in clips.Values)Destroy(clip);
            if(screen!=null)Destroy(screen.gameObject);if(texture!=null){texture.Release();Destroy(texture);}Destroy(neutral);Destroy(videoMaterial);
        }
    }
}

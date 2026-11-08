using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
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
        public int trainingClipCount;
        public AdaptiveMediaItem[] items;
        public bool HasExpectedTrainingClips()
        {
            // Older catalogs predate the explicit count and contain 100 comparison clips.
            int expected=trainingClipCount>0?trainingClipCount:100;
            return items!=null && items.Count(m=>m!=null && m.split=="training20" && Math.Abs(m.duration-20)<.01)==expected;
        }
        public bool HasValidLayout()
        {
            if(items==null || items.Any(m=>m==null || string.IsNullOrWhiteSpace(m.id)))return false;
            int training=items.Count(m=>m.split=="train");
            return training>=6 && training%2==0 && items.Length==training+4+items.Count(m=>m.split=="training20") && items.Select(m=>m.id).Distinct().Count()==items.Length &&
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
                .Where((m,i)=>config.IsPersonalized || i%2==(1+config.counterbalanceGroup)%2).ToList();
            var rng=new StableRandom(unchecked(config.seed+7919));
            for(int i=pool.Count-1;i>0;i--){int j=rng.Next(i+1);var item=pool[i];pool[i]=pool[j];pool[j]=item;}
            var result=new List<AdaptiveExposure>();
            for(int i=0;i<config.practiceMediaOnsets.Length;i++)
            {
                var item=i<pool.Count?pool[i]:null;
                double onset=config.practiceMediaOnsets[i]+(rng.Next(2001)/1000.0-1)*config.practiceOnsetJitterSeconds;
                double duration=item?.duration??12;
                if(onset+duration>blockDuration)continue;
                result.Add(new AdaptiveExposure{mediaId=item?.id??"unavailable",modality="audiovisual",stage="practice",block=0,level=config.IsPersonalized?1:2,
                    previousExposures=item!=null && exposureCounts.ContainsKey(item.id)?exposureCounts[item.id]:0,onset=onset,duration=duration});
            }
            return result;
        }
        public List<AdaptiveExposure> Plan(int block,int level,string stage)
        {
            if(config.IsPersonalized)return PlanPersonal(block,level,stage);
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
        List<AdaptiveExposure> PlanPersonal(int block,int level,string stage)
        {
            bool longer=config.Probe && LayerABStudy.IsTraining(stage);
            var pool=manifest.items.Where(m=>m.split==(longer?"training20":"train")).OrderBy(m=>m.id,StringComparer.Ordinal).ToList();
            var rng=new StableRandom(unchecked(config.seed+block*104729));
            for(int i=pool.Count-1;i>0;i--){int j=rng.Next(i+1);var temp=pool[i];pool[i]=pool[j];pool[j]=temp;}
            pool=pool.OrderBy(m=>exposureCounts.ContainsKey(m.id)?exposureCounts[m.id]:0).ToList();
            var onsets=stage=="calibration"?new[]{9.0,31.0}:longer?new[]{4.0,31.0,58.0,85.0}:new[]{9.0,36.0,63.0,90.0};
            var result=new List<AdaptiveExposure>();
            for(int i=0;i<onsets.Length;i++)
            {
                var item=level==1 && i<pool.Count?pool[i]:null;
                int previous=item!=null && exposureCounts.ContainsKey(item.id)?exposureCounts[item.id]:0;
                if(previous>0 && !config.allowMediaRepeats)item=null;
                result.Add(new AdaptiveExposure{block=block,level=level,stage=stage,
                    mediaId=level==0?"silent_slot":item?.id??"unavailable",modality=level==0?"silent":"audiovisual",
                    previousExposures=previous,onset=onsets[i]+(rng.Next(2001)/1000.0-1)*config.onsetJitterSeconds,duration=item?.duration??12});
            }
            return result;
        }
        public void Presented(string id){if(id=="silent_slot"||id=="unavailable")return;exposureCounts[id]=(exposureCounts.ContainsKey(id)?exposureCounts[id]:0)+1;}
    }

    public sealed partial class AdaptiveMedia : MonoBehaviour
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
        bool forestSelected, forestRequested, forestVisible;
        double forestLastFrameAt;
        AudioSource source;
        Renderer screen;
        Material neutral,videoMaterial;
        RenderTexture texture;
        double blockStart,activeEnd;
        int next;
        bool firstFrame,started;
        public bool Ready {get;private set;}
        public bool EngineeringReady {get;private set;}
        public bool BlockFailed {get;private set;}
        public bool IsPlaying=>active!=null;
        public bool IsAudioPlaying=>active!=null && source!=null && source.isPlaying && !source.mute && source.volume>0;
        public bool IsVideoVisible=>active!=null && active.modality=="audiovisual" && started && firstFrame && screen!=null && screen.enabled && player!=null && player.isPlaying;
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
                go.transform.position=wall.transform.position-wall.transform.forward*(wall.transform.lossyScale.z*.5f+(c.Probe ? .40f : .025f));
                go.transform.rotation=wall.transform.rotation;
                go.transform.localScale=new Vector3(c.videoWidth,c.videoHeight,1);
                screen.enabled=false;
                neutral=new Material(Shader.Find("Universal Render Pipeline/Unlit"));neutral.color=LabPalette.Wall;screen.sharedMaterial=neutral;
                videoMaterial=new Material(neutral);texture=new RenderTexture(1280,720,0);texture.Create();videoMaterial.mainTexture=texture;videoMaterial.color=Color.white;
                source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.dopplerLevel=0;source.minDistance=3.5f;source.maxDistance=10;source.loop=false;
                player=go.AddComponent<VideoPlayer>();player.playOnAwake=false;player.isLooping=false;player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=texture;
                player.audioOutputMode=VideoAudioOutputMode.AudioSource;player.controlledAudioTrackCount=1;player.SetTargetAudioSource(0,source);
                player.sendFrameReadyEvents=true;player.frameReady+=OnFrame;
                player.errorReceived+=(p,error)=>{if(forestSelected)ForestFailure(error);else if(ContinuousStage)ContinuousFailure(error);else Stop("failure",error);};
                player.loopPointReached+=p=>{if(ContinuousStage){ContinuousFailure("playlist_exhausted");}else if(!forestSelected){Stop("ended","media_end");PrepareNext();}};
            }
            engine.OnEvent+=OnEvent;
            StartCoroutine(Load());
        }
        IEnumerator Load()
        {
            string path=Path.Combine(Application.persistentDataPath,config.mediaManifest);
            if(File.Exists(path))ManifestJson=File.ReadAllText(path);
            else {var asset=Resources.Load<TextAsset>(config.Probe?"layer-ab-144-media":"adaptive-media-manifest");ManifestJson=asset!=null?asset.text:"{\"items\":[]}";}
            manifest=JsonUtility.FromJson<AdaptiveMediaManifest>(ManifestJson);
            scheduler=new AdaptiveMediaScheduler(config,manifest);
            Ready=manifest.HasValidLayout() &&
                manifest.items.All(m=>m.ready && m.volumeReviewed && (config.sessionType!="participant"||!m.synthetic) && m.gain>=0 && m.gain<=1 &&
                !string.IsNullOrEmpty(m.source) && !string.IsNullOrEmpty(m.rightsRecord) && (m.split=="training20"?Math.Abs(m.duration-20)<.01:m.duration>=10 && m.duration<=12) && !string.IsNullOrEmpty(m.audioSha256) && !string.IsNullOrEmpty(m.videoSha256));
            foreach(var item in manifest.items.Where(m=>m.ready))
            {
                string audio=Path.Combine(Application.persistentDataPath,"AdaptiveMedia",Path.GetFileName(item.audioFile));
                string video=Path.Combine(Application.persistentDataPath,"AdaptiveMedia",Path.GetFileName(item.videoFile));
                if(!File.Exists(audio)||!File.Exists(video)){Ready=false;continue;}
                if(Hash(audio)!=item.audioSha256 || Hash(video)!=item.videoSha256){Ready=false;continue;}
                if(config.Probe && item.split=="training20"){videos[item.id]=video;continue;}
                using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(audio).AbsoluteUri,AudioType.WAV))
                {
                    yield return request.SendWebRequest();
                    if(request.result==UnityWebRequest.Result.Success){clips[item.id]=DownloadHandlerAudioClip.GetContent(request);videos[item.id]=video;}
                    else Ready=false;
                }
            }
            File.WriteAllText(Path.Combine(owner.DataPath,"adaptive-media-manifest.json"),ManifestJson);
            bool bankReady=!config.Probe || manifest.HasExpectedTrainingClips();
            Ready &= bankReady;
            EngineeringReady=bankReady && manifest.HasValidLayout() && clips.Count==manifest.items.Count(m=>!config.Probe || m.split!="training20") && videos.Count==manifest.items.Length;
            if(config.CompactComparison)LoadContinuousBank();
            engine.Note("adaptive_media_ready","complete="+Ready+";engineering_files_ready="+EngineeringReady+";loaded_pairs="+clips.Count,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            if(engine.Phase==TaskPhase.Running)PrepareNext();
        }
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
        void OnEvent(TaskEvent e)
        {
            if(config.CompactComparison && HandleContinuousEvent(e))return;
            if(e.type=="protocol_stage")
            {
                if(e.detail=="load_rest_ready")PrepareForest();
                else if(e.detail=="load_rest")StartForest(e.actual);
                else if(forestSelected)StopForest("stage_end");
            }
            if(forestSelected && (e.type=="pause" || e.type=="session_aborted" || e.type=="session_complete"))
                StopForest(e.type);
            if(e.type=="block_start")
            {
                Stop("interrupted","block_boundary");next=0;BlockFailed=false;engine.AdaptiveEvaluationBlocked=false;
                blockStart=e.planned+(!config.IsPersonalized && !engine.Practice && engine.Block==1?9:0);
                plan=scheduler==null?new List<AdaptiveExposure>():engine.Practice?
                    scheduler.PlanPractice((owner.SessionConfig.hardPracticeTrials+owner.SessionConfig.warmupTrials)*owner.SessionConfig.trialSeconds):
                    scheduler.Plan(engine.Block,engine.Adaptive.Level,engine.Adaptive.Stage);
                foreach(var slot in plan)OnMedia?.Invoke(slot,"planned",blockStart+slot.onset,"");
                PrepareNext();
            }
            if(e.type=="adaptive_level_changed")
            {
                if(config.Probe && engine.Adaptive.Level==0 && active!=null)Stop("ended","automatic_off");
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
        readonly Dictionary<Light,float> restLights=new Dictionary<Light,float>();
        const float RelaxLightMultiplier=.40f;
        Color restSky,restEquator,restGround;
        bool roomDimmed;
        void RestoreRoomLights()
        {
            if(!roomDimmed)return;
            roomDimmed=false;
            RenderSettings.ambientSkyColor=restSky;
            RenderSettings.ambientEquatorColor=restEquator;
            RenderSettings.ambientGroundColor=restGround;
            foreach(var entry in restLights)if(entry.Key!=null)entry.Key.intensity=entry.Value;
            restLights.Clear();
        }
        void PrepareForest()
        {
            if(forestSelected)return;
            forestSelected=true;
            player.Stop();
            player.audioOutputMode=VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount=1;
            player.SetTargetAudioSource(0,source);
            player.source=VideoSource.VideoClip;
            player.clip=Resources.Load<VideoClip>("CalibrationForest");
            if(player.clip!=null)player.Prepare();
        }
        void StartForest(double now)
        {
            PrepareForest();
            forestRequested=true;
            forestLastFrameAt=now;
            if(player.clip==null){ForestFailure("missing_clip");return;}
            roomDimmed=true;
            restSky=RenderSettings.ambientSkyColor;
            restEquator=RenderSettings.ambientEquatorColor;
            restGround=RenderSettings.ambientGroundColor;
            RenderSettings.ambientSkyColor=restSky*RelaxLightMultiplier;
            RenderSettings.ambientEquatorColor=restEquator*RelaxLightMultiplier;
            RenderSettings.ambientGroundColor=restGround*RelaxLightMultiplier;
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                restLights[light]=light.intensity;
                light.intensity*=RelaxLightMultiplier;
            }
            source.volume=.23f;
            player.Play();
        }
        void ForestFailure(string detail)
        {
            bool interrupt=forestRequested;
            StopForest("failure="+detail);
            if(interrupt)engine.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"forest_video_unavailable");
        }
        void StopForest(string reason)
        {
            RestoreRoomLights();
            forestSelected=forestRequested=forestVisible=false;
            long frame=player.frame;
            player.Stop();
            screen.enabled=false;
            screen.sharedMaterial=neutral;
            player.clip=null;
            player.source=VideoSource.Url;
            player.audioOutputMode=VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount=1;
            player.SetTargetAudioSource(0,source);
            engine.Note("calibration_forest_stopped",reason+";last_frame="+frame,Time.realtimeSinceStartupAsDouble,Time.frameCount);
        }
        void PrepareNext()
        {
            if(next<plan.Count && plan[next].modality=="audiovisual" && videos.TryGetValue(plan[next].mediaId,out var path))
            {player.url=path;player.Prepare();}
        }
        public void Tick(double now)
        {
            if(ContinuousStage){TickContinuous(now);return;}
            if(forestRequested && now-forestLastFrameAt>3)ForestFailure("no_decoded_frames");
            if(engine==null||engine.Phase!=TaskPhase.Running)return;
            if(active!=null && now>=activeEnd){Stop(started?"ended":"failure",started?"fixed_slot_end":"no_video_frame");PrepareNext();}
            if(next>=plan.Count || now<blockStart+plan[next].onset)return;
            active=plan[next++];activeEnd=blockStart+active.onset+active.duration;started=false;firstFrame=false;
            if(active.level==0){OnMedia?.Invoke(active,"silent_slot",now,"");active=null;return;}
            if(now>=activeEnd || (active.modality=="audio"?!clips.ContainsKey(active.mediaId):!videos.ContainsKey(active.mediaId)) || (active.modality=="audiovisual"&&!player.isPrepared))
            {Stop("failure","missing_or_not_prepared_or_missed_slot");PrepareNext();return;}
            var item=manifest.items.First(m=>m.id==active.mediaId);source.volume=config.mediaVolume*item.gain;
            OnMedia?.Invoke(active,"play_requested",now,"fixed_source;software_timestamp");
            if(active.modality=="audio"){source.clip=clips[active.mediaId];source.Play();started=true;scheduler.Presented(active.mediaId);OnMedia?.Invoke(active,"started",now,"audio_play_call");}
            else {source.clip=null;player.Play();}
        }
        void OnFrame(VideoPlayer p,long frame)
        {
            if(ContinuousStage)ContinuousFrame(p,frame);
            if(forestSelected)
            {
                if(!forestRequested)return;
                forestLastFrameAt=Time.realtimeSinceStartupAsDouble;
                if(forestVisible)return;
                forestVisible=true;
                screen.sharedMaterial=videoMaterial;
                screen.enabled=true;
                engine.Note("calibration_forest_started","VermontForestTrail-RWxQn923pa8;source_start_s=45;light_multiplier="+RelaxLightMultiplier.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+";audio_gain=0.23;frame="+frame,forestLastFrameAt,Time.frameCount);
                return;
            }
            if(active==null||active.modality!="audiovisual"||firstFrame)return;
            firstFrame=started=true;screen.sharedMaterial=videoMaterial;screen.enabled=true;scheduler.Presented(active.mediaId);
            OnMedia?.Invoke(active,"started",Time.realtimeSinceStartupAsDouble,"first_decoded_frame;not_photon_onset");
        }
        void Stop(string status,string detail)
        {
            if(active!=null)
            {
                if(status!="ended")
                {
                    BlockFailed=true;
                    if(engine!=null){engine.AdaptiveEvaluationBlocked=true;engine.ResetRolling(Time.realtimeSinceStartupAsDouble,Time.frameCount,"media_"+status);}
                }
                OnMedia?.Invoke(active,status,Time.realtimeSinceStartupAsDouble,detail);
            }
            active=null;if(source!=null)source.Stop();if(player!=null)player.Stop();if(screen!=null){screen.sharedMaterial=neutral;screen.enabled=false;}
        }
        void OnDestroy()
        {
            RestoreRoomLights();
            if(engine!=null)engine.OnEvent-=OnEvent;Stop("interrupted","destroy");
            foreach(var clip in clips.Values)Destroy(clip);
            if(screen!=null)Destroy(screen.gameObject);if(texture!=null){texture.Release();Destroy(texture);}Destroy(neutral);Destroy(videoMaterial);
        }
    }
}

namespace EvidenceVR
{
    [Serializable] public sealed class ContinuousSegment
    {
        public string mediaId, sourceVideoHash;
        public double start, duration;
    }
    [Serializable] public sealed class ContinuousPlaylist
    {
        public string id, file, sha256;
        public double duration;
        public ContinuousSegment[] segments;
    }
    [Serializable] public sealed class ContinuousMediaBank
    {
        public string revision;
        public ContinuousPlaylist[] playlists;
    }

    // A single encoded timeline joins the source clips without decoder reloads at cuts.
    public sealed partial class AdaptiveMedia
    {
        public const string ContinuousRevision="continuous-clips-resume-v1";
        ContinuousMediaBank continuousBank;
        ContinuousPlaylist continuousPlaylist;
        string continuousStage;
        bool continuousRequested;
        int continuousSegment=-1, continuousExposure;
        double continuousRequestedAt, continuousFrameAt;
        bool ContinuousStage=>config!=null && config.CompactComparison && engine!=null && LayerABStudy.IsTraining(engine.Adaptive.Stage);

        void LoadContinuousBank()
        {
            try
            {
                string path=Path.Combine(Application.persistentDataPath,"continuous-comparison-media.json");
                string json=File.ReadAllText(path);
                continuousBank=JsonUtility.FromJson<ContinuousMediaBank>(json);
                if(continuousBank.revision!=ContinuousRevision || continuousBank.playlists?.Length!=2)throw new InvalidDataException("Invalid continuous playlist bank");
                foreach(var list in continuousBank.playlists)
                {
                    var file=Path.Combine(Application.persistentDataPath,"AdaptiveMedia",Path.GetFileName(list.file));
                    if(list.duration<300 || list.segments==null || list.segments.Length==0 || Hash(file)!=list.sha256)
                        throw new InvalidDataException("Missing, short or changed continuous playlist: "+list.id);
                    double end=0;
                    foreach(var segment in list.segments)
                    {
                        if(Math.Abs(segment.start-end)>.001 || segment.duration<=0 || !manifest.items.Any(m=>m.id==segment.mediaId && m.videoSha256==segment.sourceVideoHash))
                            throw new InvalidDataException("Invalid continuous segment provenance");
                        end+=segment.duration;
                    }
                    if(Math.Abs(end-list.duration)>.001)throw new InvalidDataException("Playlist duration mismatch");
                }
                File.WriteAllText(Path.Combine(owner.DataPath,"continuous-comparison-media.json"),json);
                if(ContinuousStage)BeginContinuousStage(engine.Adaptive.Stage);
            }
            catch(Exception error)
            {
                continuousBank=null;Ready=EngineeringReady=false;
                engine.Note("continuous_media_unavailable",error.Message,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            }
        }

        void BeginContinuousStage(string stage)
        {
            EndContinuous("ended","stage_transition");
            player.Stop();continuousStage=stage;continuousSegment=-1;continuousExposure=0;
            if(continuousBank==null)return;
            int index=(config.seed&1)^(stage=="layer_b"?1:0);
            continuousPlaylist=continuousBank.playlists[index];
            player.url=Path.Combine(Application.persistentDataPath,"AdaptiveMedia",Path.GetFileName(continuousPlaylist.file));
            player.isLooping=false;player.Prepare();
            engine.Note("continuous_media_assignment",ContinuousRevision+";stage="+stage+";playlist="+continuousPlaylist.id+";sha256="+continuousPlaylist.sha256,
                Time.realtimeSinceStartupAsDouble,Time.frameCount);
        }

        bool HandleContinuousEvent(TaskEvent e)
        {
            if(e.type=="protocol_stage")
            {
                if(LayerABStudy.IsTraining(e.detail))BeginContinuousStage(e.detail);
                else if(continuousStage!=null)
                {EndContinuous("ended","stage_end");player.Stop();continuousStage=null;continuousPlaylist=null;}
                return false;
            }
            if(!ContinuousStage)return false;
            if(e.type=="block_start")
            {
                // Statistical segment boundaries must not interrupt audiovisual exposure.
                if(continuousStage!=engine.Adaptive.Stage)BeginContinuousStage(engine.Adaptive.Stage);
                return true;
            }
            if(e.type=="block_complete")return true;
            if(e.type=="adaptive_level_changed")
            {
                if(engine.Adaptive.Level==0)EndContinuous("ended","automatic_off;cursor_retained");
                return true;
            }
            if(e.type=="pause" || e.type=="session_aborted" || e.type=="session_complete")
            {EndContinuous("interrupted",e.type);return true;}
            if(e.type=="resume")return true;
            return false;
        }

        void EndContinuous(string status,string detail)
        {
            if(player==null)return;
            player.Pause();
            if(active!=null)OnMedia?.Invoke(active,status,Time.realtimeSinceStartupAsDouble,
                detail+";playlist_time="+player.time.ToString("R",CultureInfo.InvariantCulture));
            active=null;continuousRequested=false;started=firstFrame=false;
            if(screen!=null){screen.enabled=false;screen.sharedMaterial=neutral;}
        }

        void ContinuousFailure(string reason)
        {
            EndContinuous("failure",reason);BlockFailed=true;engine.AdaptiveEvaluationBlocked=true;
            engine.Note("continuous_media_failure",reason,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            if(engine.Phase==TaskPhase.Running)engine.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"continuous_media_failure");
        }

        void TickContinuous(double now)
        {
            if(engine.Phase!=TaskPhase.Running || BlockFailed)return;
            if(engine.Adaptive.Level==0){if(active!=null)EndContinuous("ended","automatic_off");return;}
            if(continuousPlaylist==null){ContinuousFailure("playlist_not_available");return;}
            if(!continuousRequested)
            {
                continuousRequested=true;continuousRequestedAt=now;
                continuousFrameAt=now;
                blockStart=now;
                active=new AdaptiveExposure{mediaId=continuousPlaylist.id+"-exposure-"+(++continuousExposure),
                    modality="audiovisual",stage=engine.Adaptive.Stage,block=engine.Block,level=1,onset=0,
                    duration=continuousPlaylist.duration-player.time};
                source.volume=config.mediaVolume;source.clip=null;started=firstFrame=false;
                OnMedia?.Invoke(active,"play_requested",now,"continuous_playlist;cursor="+player.time.ToString("R",CultureInfo.InvariantCulture));
            }
            if(!player.isPlaying && player.isPrepared && !firstFrame)player.Play();
            if(now-(firstFrame?continuousFrameAt:continuousRequestedAt)>3)ContinuousFailure("no_decoded_frames");
        }

        void ContinuousFrame(VideoPlayer p,long frame)
        {
            if(!continuousRequested || active==null)return;
            continuousFrameAt=Time.realtimeSinceStartupAsDouble;
            int index=Array.FindLastIndex(continuousPlaylist.segments,s=>p.time>=s.start);
            if(index==continuousSegment || index<0)return;
            continuousSegment=index;
            var segment=continuousPlaylist.segments[index];
            engine.Note("adaptive_media_segment_changed",ContinuousRevision+";playlist="+continuousPlaylist.id+
                ";mediaId="+segment.mediaId+";segment="+index+";playlist_time="+p.time.ToString("R",CultureInfo.InvariantCulture)+
                ";frame="+frame+";exposure="+active.mediaId+";not_controller_on=true",continuousFrameAt,Time.frameCount);
        }
    }
}

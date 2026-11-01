using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace EvidenceVR
{
    public sealed class EnvironmentalDistractions : MonoBehaviour
    {
        public string[] streetFiles;
        public AudioClip[] doorClips, conversationClips;
        public GameObject[] windowRoots;
        public Renderer[] windowScreens;
        public AudioSource[] doorSources, conversationSources;
        public VideoPlayer player;
        public TextAsset mediaManifest;
        public bool MediaReady { get; private set; }
        public DistractionEvent Active => active;
        public int StartedCount { get; private set; }
        public int EndedCount { get; private set; }
        public bool Visible => windowRoots!=null && windowRoots.Length>0 && windowRoots[0].activeSelf;
        public IReadOnlyList<DistractionEvent> Plan => plan;
        TaskEngine engine;
        TaskConfig config;
        List<DistractionEvent> plan=new List<DistractionEvent>();
        DistractionEvent active;
        RenderTexture texture;
        AudioSource activeSource;
        string[] localVideos;
        int next,preparedIndex=-1;
        bool started,initializing,callbacksBound;
        double began,requestElapsed,lastBlockElapsed;

        public void Bind(TaskEngine nextEngine,TaskConfig nextConfig,string dataPath)
        {
            Cancel("session_reset");
            if(engine!=null)engine.OnEvent-=OnTaskEvent;
            engine=nextEngine; config=nextConfig; engine.OnEvent+=OnTaskEvent;
            StartedCount=EndedCount=0; next=0; lastBlockElapsed=0; plan.Clear();
            if(mediaManifest!=null)File.WriteAllText(Path.Combine(dataPath,"distraction-media.json"),mediaManifest.text);
            foreach(var source in doorSources)source.volume=config.distractionMasterVolume*config.doorVolume;
            foreach(var source in conversationSources)source.volume=config.distractionMasterVolume*config.conversationVolume;
            if(!callbacksBound)
            {
                player.prepareCompleted+=OnPrepared; player.frameReady+=OnFrame;
                player.errorReceived+=OnVideoError; player.loopPointReached+=OnVideoEnded;
                callbacksBound=true;
            }
            if(texture==null)
            {
                texture=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32); texture.name="StreetWindowVideo"; texture.Create();
                player.targetTexture=texture;
                foreach(var screen in windowScreens)
                {
                    var properties=new MaterialPropertyBlock(); properties.SetTexture("_BaseMap",texture); screen.SetPropertyBlock(properties);
                }
            }
            HideWindows();
            engine.Note("distraction_configuration",DistractionPlan.Version+";seed="+config.distractionSeed+";formal_only=true;per_type="+config.distractionsPerTypePerBlock+";native_3d_panning=true;acoustic_onsets_not_measured",Time.realtimeSinceStartupAsDouble,Time.frameCount);
            if(config.distractionsEnabled && !MediaReady && !initializing)StartCoroutine(InitializeMedia());
        }
        IEnumerator InitializeMedia()
        {
            initializing=true; localVideos=new string[streetFiles.Length];
            for(int i=0;i<streetFiles.Length;i++)
            {
                string source=Application.streamingAssetsPath+"/Distractions/Streets/"+streetFiles[i];
                if(Application.platform==RuntimePlatform.Android)
                {
                    string folder=Path.Combine(Application.persistentDataPath,"DistractionMedia-v1"); Directory.CreateDirectory(folder);
                    string destination=Path.Combine(folder,streetFiles[i]);
                    // Copy from the packaged APK locally; playback never needs an internet connection.
                    using(var request=UnityWebRequest.Get(source))
                    {
                        request.downloadHandler=new DownloadHandlerFile(destination);
                        yield return request.SendWebRequest();
                        if(request.result!=UnityWebRequest.Result.Success)
                        { Note("distraction_media_error","asset="+streetFiles[i]+";local_copy_failed"); initializing=false; yield break; }
                    }
                    localVideos[i]=destination;
                }
                else localVideos[i]=source;
            }
            MediaReady=true;initializing=false;
            Note("distraction_media_ready","videos="+streetFiles.Length+";doors="+doorClips.Length+";conversations="+conversationClips.Length);
        }
        void OnTaskEvent(TaskEvent item)
        {
            if(item.type=="rules" && !engine.Practice)
            {
                BuildPlan(); PrepareUpcoming();
            }
            else if(item.type=="block_start")
            {
                Cancel("block_start"); BuildPlan();
                foreach(var scheduled in plan)
                    engine.Note("distraction_planned",Describe(scheduled),item.actual,item.frame,item.actual+scheduled.start);
                PrepareUpcoming();
            }
            else if(item.type=="pause" && active!=null)
            {
                HideWindows(); activeSource?.Pause(); if(player.isPlaying)player.Pause();
                Note("distraction_paused",Describe(active));
            }
            else if(item.type=="resume" && active!=null)
            {
                Note("distraction_resumed",Describe(active));
                if(active.kind==DistractionKind.Street)
                { if(started)ShowWindows(); if(player.isPrepared)player.Play(); }
                else activeSource?.UnPause();
            }
            else if(item.type=="block_complete" || item.type=="session_complete" || item.type=="session_aborted")Cancel(item.type);
        }
        void BuildPlan()
        {
            next=0; plan=DistractionPlan.Create(config,engine.Practice ? -1 : engine.Block,streetFiles.Length,Lengths(doorClips),Lengths(conversationClips));
        }
        static float[] Lengths(AudioClip[] clips)
        { var values=new float[clips.Length];for(int i=0;i<clips.Length;i++)values[i]=clips[i].length;return values; }
        public void Tick(double now)
        {
            if(engine==null || engine.Phase!=TaskPhase.Running || engine.Practice || !config.distractionsEnabled)return;
            double elapsed=engine.BlockElapsed(now);
            lastBlockElapsed=elapsed;
            if(active!=null)
            {
                if(started && elapsed-began>=active.duration)End("duration_complete",now);
                else if(!started && elapsed-requestElapsed>1.5)End("video_start_timeout",now);
            }
            if(next<plan.Count && elapsed>=plan[next].start)
            {
                var item=plan[next++];
                if(active!=null || elapsed-item.start>1.5)
                    Note("distraction_skipped",Describe(item)+";reason="+(active!=null ? "overlap_guard" : "late_schedule"));
                else Begin(item,now);
            }
            PrepareUpcoming();
        }
        void PrepareUpcoming()
        {
            if(!MediaReady || active!=null)return;
            for(int i=next;i<plan.Count;i++)if(plan[i].kind==DistractionKind.Street)
            { Prepare(plan[i].mediaIndex); return; }
        }
        void Prepare(int index)
        {
            if(preparedIndex==index)return;
            player.Stop(); preparedIndex=index; player.url=localVideos[index]; player.Prepare();
        }
        void Begin(DistractionEvent item,double now)
        {
            active=item;started=false;requestElapsed=engine.BlockElapsed(now); activeSource=null;
            engine.Note("distraction_requested",Describe(item),now,Time.frameCount,now-requestElapsed+item.start);
            if(item.kind==DistractionKind.Street)
            {
                if(!MediaReady){End("local_media_not_ready",now);return;}
                Prepare(item.mediaIndex);
                if(player.isPrepared)player.Play();
            }
            else
            {
                var clips=item.kind==DistractionKind.Door ? doorClips : conversationClips;
                activeSource=(item.kind==DistractionKind.Door ? doorSources : conversationSources)[item.side];
                activeSource.clip=clips[item.mediaIndex]; activeSource.Play(); MarkStarted(now);
            }
        }
        void OnPrepared(VideoPlayer video)
        {
            if(preparedIndex<0 || preparedIndex>=streetFiles.Length)return;
            Note("distraction_video_prepared","asset="+streetFiles[preparedIndex]);
            if(active!=null && active.kind==DistractionKind.Street && engine.Phase==TaskPhase.Running)video.Play();
        }
        void OnFrame(VideoPlayer video,long frame)
        {
            if(active==null || active.kind!=DistractionKind.Street || started || engine.Phase!=TaskPhase.Running)return;
            if(engine.BlockElapsed(Time.realtimeSinceStartupAsDouble)-requestElapsed>1.5)
            {End("late_first_frame",Time.realtimeSinceStartupAsDouble);return;}
            ShowWindows();MarkStarted(Time.realtimeSinceStartupAsDouble);
        }
        void OnVideoError(VideoPlayer video,string message)
        {
            Note("distraction_media_error","video_decoder;asset="+(preparedIndex>=0 ? streetFiles[preparedIndex] : "none")+";message="+message);
            if(active!=null && active.kind==DistractionKind.Street)End("video_error",Time.realtimeSinceStartupAsDouble);
        }
        void OnVideoEnded(VideoPlayer video)
        { if(active!=null && active.kind==DistractionKind.Street)End("clip_finished",Time.realtimeSinceStartupAsDouble); }
        void MarkStarted(double now)
        {
            started=true; began=engine.BlockElapsed(now); StartedCount++;
            engine.Note("distraction_started",Describe(active)+";software_playback_only",now,Time.frameCount,now-began+active.start);
        }
        void End(string reason,double now)
        {
            if(active==null)return;
            var item=active; active=null;
            double elapsed=engine.Phase==TaskPhase.Running || engine.Phase==TaskPhase.Paused ? engine.BlockElapsed(now) : lastBlockElapsed;
            engine.Note(started ? "distraction_ended" : "distraction_skipped",Describe(item)+";reason="+reason+";exposure_s="+F(started ? Math.Max(0,elapsed-began) : 0),now,Time.frameCount);
            if(started)EndedCount++;
            started=false; activeSource?.Stop();activeSource=null;HideWindows();
            if(item.kind==DistractionKind.Street){if(player!=null)player.Stop();preparedIndex=-1;}
        }
        public void Cancel(string reason)
        {
            if(engine!=null)End(reason,Time.realtimeSinceStartupAsDouble);
            HideWindows(); if(player!=null)player.Stop();preparedIndex=-1;
            if(doorSources!=null)foreach(var source in doorSources)if(source!=null)source.Stop();
            if(conversationSources!=null)foreach(var source in conversationSources)if(source!=null)source.Stop();
        }
        void ShowWindows(){foreach(var root in windowRoots)if(root!=null)root.SetActive(true);}
        void HideWindows(){if(windowRoots!=null)foreach(var root in windowRoots)if(root!=null)root.SetActive(false);}
        void Note(string type,string detail){engine?.Note(type,detail,Time.realtimeSinceStartupAsDouble,Time.frameCount);}
        string Describe(DistractionEvent item)
        {
            string asset=item.kind==DistractionKind.Street ? streetFiles[item.mediaIndex] : (item.kind==DistractionKind.Door ? doorClips : conversationClips)[item.mediaIndex].name;
            float volume=item.kind==DistractionKind.Door ? config.doorVolume : item.kind==DistractionKind.Conversation ? config.conversationVolume : 0;
            return "index="+item.index+";kind="+item.kind+";asset="+asset+";side="+(item.kind==DistractionKind.Street ? "both" : item.side==0 ? "left" : "right")+";planned_block_s="+F(item.start)+";duration_s="+F(item.duration)+";gain="+F(volume*config.distractionMasterVolume);
        }
        static string F(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
        void OnDestroy()
        {
            Cancel("component_destroyed"); if(engine!=null)engine.OnEvent-=OnTaskEvent;
            if(callbacksBound && player!=null){player.prepareCompleted-=OnPrepared;player.frameReady-=OnFrame;player.errorReceived-=OnVideoError;player.loopPointReached-=OnVideoEnded;}
            if(texture!=null){texture.Release();Destroy(texture);}
        }
    }
}

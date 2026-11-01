using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;

public static class DistractionAudioProbe
{
    public static async Task<string> Run()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var d=c.distractions;
        if(!Application.isPlaying || c.Engine.Phase!=TaskPhase.Preparation)throw new Exception("Use fresh Play Mode preparation.");
        bool enabled=c.enabled;var original=c.Engine;var config=original.Config;config.distractionMasterVolume=.5f;
        var engine=new TaskEngine(config);const string folder="Evidence/DistractionAudio-v1.7";Directory.CreateDirectory(folder);
        var begin=typeof(EnvironmentalDistractions).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic);
        int cases=0;
        try
        {
            c.enabled=false;double clock=Time.realtimeSinceStartupAsDouble-108;int frame=0;
            engine.Advance(clock,frame++);engine.Advance(clock,frame++);
            while(engine.Block<2){if(engine.Phase==TaskPhase.Instructions)engine.Advance(clock,frame++);else{clock+=config.trialSeconds;engine.Tick(clock,frame++);}}
            d.Bind(engine,config,folder);engine.Advance(Time.realtimeSinceStartupAsDouble,frame++);
            ((List<DistractionEvent>)d.Plan).Clear();
            if(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x=>x.enabled)!=1)throw new Exception("Invalid listener state.");
            foreach(bool speech in new[]{false,true})for(int family=0;family<3;family++)for(int side=0;side<2;side++)
            {
                var clips=speech ? d.conversationClips : d.doorClips;
                int index=Array.FindIndex(clips,x=>(speech ? EnvironmentalDistractions.SpeechFamily(x.name) : EnvironmentalDistractions.EnvironmentFamily(x.name))==family);
                double now=Time.realtimeSinceStartupAsDouble;
                var item=new DistractionEvent{index=cases++,kind=speech ? DistractionKind.Speech : DistractionKind.Environment,side=side,mediaIndex=index,start=engine.BlockElapsed(now),duration=clips[index].length};
                begin.Invoke(d,new object[]{item,now});var source=d.ActiveAudioSource;var start=source.transform.position;
                await Task.Delay(180);d.Tick(Time.realtimeSinceStartupAsDouble);
                if(!source.isPlaying || source.time<=0 || source.spatialBlend!=1 || source.clip.channels!=1)throw new Exception("Spatial playback did not advance.");
                float expected=.5f*(speech ? config.conversationVolume*(family==2 ? .45f : 1) : config.doorVolume);
                if(Math.Abs(source.volume-expected)>.001)throw new Exception("Gain mismatch.");
                if(!speech && family>0 && Vector3.Distance(start,source.transform.position)<.01f)throw new Exception("Source path did not advance.");
                if(!speech && family==2 && Vector3.Distance(source.transform.position,d.listener.position)>=Vector3.Distance(start,d.listener.position))throw new Exception("Footsteps did not approach.");
                if(!speech && family==1 && source.transform.position.z<3)throw new Exception("Traffic not outside front wall.");
                if(speech && family==2 && Vector3.Distance(source.transform.position,d.listener.position)>.5f)throw new Exception("Near-ear placement failed.");
                engine.Pause(Time.realtimeSinceStartupAsDouble,frame++);float pausedAt=source.time;var frozen=source.transform.position;
                await Task.Delay(180);d.Tick(Time.realtimeSinceStartupAsDouble);
                if(source.isPlaying || Math.Abs(source.time-pausedAt)>.03 || source.transform.position!=frozen)throw new Exception("Audio or motion pause failed.");
                engine.Resume(Time.realtimeSinceStartupAsDouble,frame++);await Task.Delay(100);d.Tick(Time.realtimeSinceStartupAsDouble);
                if(!source.isPlaying)throw new Exception("Resume failed.");
                d.Cancel("audio_probe_case");if(source.isPlaying)throw new Exception("Cancellation failed.");
            }
            begin.Invoke(d,new object[]{new DistractionEvent{kind=DistractionKind.Speech,mediaIndex=0,side=0,duration=10},Time.realtimeSinceStartupAsDouble});
            engine.Abort(Time.realtimeSinceStartupAsDouble,frame++,"probe_abort");
            if(d.Active!=null || d.conversationSources.Any(x=>x.isPlaying))throw new Exception("Abort left audio active.");
            string report="PASS: "+cases+" live audio cases (door, traffic, approaching footsteps, conversation, news, near-ear soft voice; both sides). Mono 3D playback, gains, movement, approach, near-ear distance, pause/frozen motion, resume, cancellation and abort verified. Spatializer="+AudioSettings.GetSpatializerPluginName()+". Native spatial panning and attenuation; not a measured HRTF or physical listening test.";
            File.WriteAllText("Evidence/distraction-audio-v1.7.txt",report);Debug.Log(report);return report;
        }
        finally{d.Cancel("probe_end");d.Bind(original,original.Config,c.DataPath);c.enabled=enabled;}
    }
}

using System;
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
        bool enabled=c.enabled; var original=c.Engine;var config=original.Config;config.distractionMasterVolume=.5f;
        var engine=new TaskEngine(config);const string folder="Evidence/DistractionAudio-v1.4";Directory.CreateDirectory(folder);
        var begin=typeof(EnvironmentalDistractions).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic);
        try
        {
            c.enabled=false;double now=Time.realtimeSinceStartupAsDouble;engine.Advance(now,0);engine.Advance(now,1);d.Bind(engine,config,folder);
            if(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x=>x.enabled)!=1 || AudioListener.volume<=0)throw new Exception("Invalid listener state.");
            foreach(var kind in new[]{DistractionKind.Door,DistractionKind.Conversation})for(int side=0;side<2;side++)
            {
                now=Time.realtimeSinceStartupAsDouble;
                var item=new DistractionEvent { index=side,kind=kind,side=side,mediaIndex=0,start=engine.BlockElapsed(now),duration=10 };
                begin.Invoke(d,new object[]{item,now});
                var source=(kind==DistractionKind.Door ? d.doorSources : d.conversationSources)[side];
                await Task.Delay(100);
                if(!source.isPlaying || source.time<=0)throw new Exception("Audio did not advance.");
                float expected=.5f*(kind==DistractionKind.Door ? config.doorVolume : config.conversationVolume);
                if(Math.Abs(source.volume-expected)>.001 || source.spatialBlend!=1 || source.clip.channels!=1)throw new Exception("Spatial gain settings mismatch.");
                engine.Pause(Time.realtimeSinceStartupAsDouble,2);float pausedAt=source.time;
                await Task.Delay(200);
                if(source.isPlaying || Math.Abs(source.time-pausedAt)>.03)throw new Exception("Audio pause failed.");
                engine.Resume(Time.realtimeSinceStartupAsDouble,3);await Task.Delay(100);
                if(!source.isPlaying)throw new Exception("Audio resume failed.");
                d.Cancel("audio_probe_case");if(source.isPlaying)throw new Exception("Audio cancellation failed.");
            }
            begin.Invoke(d,new object[]{new DistractionEvent{kind=DistractionKind.Conversation,mediaIndex=0,side=0,duration=10},Time.realtimeSinceStartupAsDouble});
            engine.Abort(Time.realtimeSinceStartupAsDouble,4,"probe_abort");
            if(d.Active!=null || d.conversationSources.Any(x=>x.isPlaying))throw new Exception("Abort left audio active.");
            var report="PASS: door and conversation sources on both sides played and advanced; mono 3D settings, master gain multiplication, pause/unpause, cancellation and abort verified. One enabled listener. Spatializer="+AudioSettings.GetSpatializerPluginName()+". Native 3D panning is verified; perceived front/back localization and intelligibility require Quest 3 listening.\n";
            File.WriteAllText("Evidence/distraction-audio-v1.4.txt",report);Debug.Log(report);return report;
        }
        finally{d.Cancel("probe_end");d.Bind(original,original.Config,c.DataPath);c.enabled=enabled;}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class DistractionPlaybackProbe
{
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var d=c.distractions;
        if(c.Engine.Phase!=TaskPhase.Preparation)throw new Exception("Use a fresh preparation session.");
        const string folder="Evidence/DistractionPlayback-v1.4";Directory.CreateDirectory(folder);
        var config=c.Engine.Config; config.distractionSeed=9471;
        var engine=new TaskEngine(config);var original=c.Engine;bool enabled=c.enabled;
        bool board=c.spatialView.instructions.enabled;
        var events=new List<TaskEvent>();bool paused=false,captured=false;double visualStarted=-1;
        var writer=new StreamWriter(folder+"/events.jsonl");
        try
        {
            c.enabled=false;c.spatialView.instructions.enabled=false;
            // Advance only the isolated practice engine; the formal block then uses the live clock.
            double clock=Time.realtimeSinceStartupAsDouble-108;int frame=0;
            engine.Advance(clock,frame++);engine.Advance(clock,frame++);
            while(engine.Block<2)
            {
                if(engine.Phase==TaskPhase.Instructions)engine.Advance(clock,frame++);
                else {clock+=config.trialSeconds;engine.Tick(clock,frame++);}
            }
            d.Bind(engine,config,folder);
            engine.OnEvent+=item=>{events.Add(item);writer.WriteLine(JsonUtility.ToJson(item));writer.Flush();};
            double origin=Time.realtimeSinceStartupAsDouble;engine.Advance(origin,Time.frameCount);
            while(Time.realtimeSinceStartupAsDouble-origin<155 && d.EndedCount<6)
            {
                double now=Time.realtimeSinceStartupAsDouble;engine.Tick(now,Time.frameCount);d.Tick(now);
                if(d.Active!=null && d.Active.kind==DistractionKind.Street && d.Visible)
                {
                    if(visualStarted<0)visualStarted=now;
                    if(!captured && now-visualStarted>.5)
                    {Capture(c,d,0,folder+"/left-window.png");Capture(c,d,1,folder+"/right-window.png");captured=true;}
                    if(!paused && now-visualStarted>1)
                    {
                        double frozen=engine.BlockElapsed(now);engine.Pause(now,Time.frameCount);
                        if(d.Visible || d.player.isPlaying)throw new Exception("Video did not pause and hide.");
                        await Task.Delay(700);
                        if(Math.Abs(engine.BlockElapsed(Time.realtimeSinceStartupAsDouble)-frozen)>.001)throw new Exception("Distraction clock did not freeze.");
                        engine.Resume(Time.realtimeSinceStartupAsDouble,Time.frameCount);paused=true;
                    }
                }
                foreach(var source in d.doorSources.Concat(d.conversationSources))
                    if(source.isPlaying && source.clip==null)throw new Exception("Playing without a clip.");
                await Task.Delay(30);
            }
            var starts=events.Where(x=>x.type=="distraction_started").ToArray();
            if(starts.Length!=6 || d.EndedCount!=6 || !captured || !paused)throw new Exception("Incomplete playback: starts="+starts.Length+";ends="+d.EndedCount);
            if(events.Any(x=>x.type=="distraction_skipped" || x.type=="distraction_media_error"))throw new Exception("Media playback failure.");
            foreach(var kind in new[]{"Street","Door","Conversation"})if(starts.Count(x=>x.detail.Contains("kind="+kind+";"))!=2)throw new Exception("Unbalanced actual playback.");
            if(d.Visible || d.doorSources.Concat(d.conversationSources).Any(x=>x.isPlaying))throw new Exception("Media remained active after end.");
            var report="PASS: six live scheduled distractions (two street windows, two door sounds, two conversations); all completed without skips or decoder errors; video pause/hide/resume and frozen task clock verified; left/right captures saved; sources stopped at end. Elapsed="+(Time.realtimeSinceStartupAsDouble-origin).ToString("F2")+" seconds.\nIsolated synthetic engine and local Editor media only; not physical headset audibility or standalone performance.\n";
            File.WriteAllText("Evidence/distraction-playback-v1.4.txt",report);Debug.Log(report);return report;
        }
        finally
        {
            d.Cancel("probe_end");writer.Dispose();d.Bind(original,original.Config,c.DataPath);c.enabled=enabled;c.spatialView.instructions.enabled=board;
        }
    }
    static void Capture(TaskPlatformController c,EnvironmentalDistractions d,int side,string file)
    {
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        var data=camera.GetComponent<UniversalAdditionalCameraData>();bool xr=data.allowXRRendering;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            data.allowXRRendering=false;camera.transform.SetPositionAndRotation(new Vector3(0,1.65f,0),Quaternion.LookRotation(d.windowRoots[side].transform.position-new Vector3(0,1.65f,0)));
            camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());
        }
        finally
        {camera.targetTexture=previous;data.allowXRRendering=xr;camera.transform.SetPositionAndRotation(position,rotation);RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
}

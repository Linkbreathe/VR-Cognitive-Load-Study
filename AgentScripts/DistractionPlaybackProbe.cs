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
    public static async Task<string> Preview()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var d=c.distractions;
        if(!Application.isPlaying || c.Engine.Phase!=TaskPhase.Preparation)throw new Exception("Fresh Play Mode preparation required.");
        var property=typeof(TaskPlatformController).GetProperty("Engine");
        var render=typeof(TaskPlatformController).GetMethod("Render",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Directory.CreateDirectory("Evidence/DistractionPlayback-v1.7");
        var original=c.Engine;bool enabled=c.enabled;
        try
        {
            c.enabled=false;
            var synthetic=new TaskEngine(original.Config);double clock=0;int frame=0;
            synthetic.Advance(clock,frame++);synthetic.Advance(clock,frame++);
            while(synthetic.Block<2){if(synthetic.Phase==TaskPhase.Instructions)synthetic.Advance(clock,frame++);else{clock+=4.5;synthetic.Tick(clock,frame++);}}
            synthetic.Advance(clock,frame++);synthetic.Tick(clock+=4.5,frame++);synthetic.Tick(clock+=4.5,frame++);
            property.SetValue(c,synthetic);render.Invoke(c,null);c.spatialView.Refresh(c,clock+.1);Canvas.ForceUpdateCanvases();
            d.player.url=Application.streamingAssetsPath+"/Distractions/Streets/"+d.streetFiles[0];d.player.Prepare();
            double started=Time.realtimeSinceStartupAsDouble;
            while(!d.player.isPrepared && Time.realtimeSinceStartupAsDouble-started<8)await Task.Delay(30);
            if(!d.player.isPrepared)throw new Exception("Preview video not prepared.");
            d.player.Play();await Task.Delay(600);d.player.Pause();d.windowRoots[0].SetActive(true);
            for(int pos=0;pos<5;pos++)Capture(c,d,pos,"Evidence/DistractionPlayback-v1.7/task-"+EnvironmentalDistractions.FrontWindowName(pos)+".png");
            return "Five synthetic front-wall previews with active sphere and formal HUD saved.";
        }
        finally{d.Cancel("visual_probe_end");property.SetValue(c,original);c.enabled=enabled;render.Invoke(c,null);}
    }
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var d=c.distractions;
        if(c.Engine.Phase!=TaskPhase.Preparation)throw new Exception("Use a fresh preparation session.");
        const string folder="Evidence/DistractionPlayback-v1.7";Directory.CreateDirectory(folder);
        var config=c.Engine.Config; config.distractionSeed=9471;
        var engine=new TaskEngine(config);Directory.CreateDirectory("Evidence/DistractionPlayback-v1.7");
        var original=c.Engine;bool enabled=c.enabled;
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
            while(Time.realtimeSinceStartupAsDouble-origin<155 && d.EndedCount<9)
            {
                double now=Time.realtimeSinceStartupAsDouble;engine.Tick(now,Time.frameCount);d.Tick(now);
                if(d.Active!=null && d.Active.kind==DistractionKind.Street && d.Visible)
                {
                    if(visualStarted<0)visualStarted=now;
                    if(!captured && now-visualStarted>.5)
                    {for(int pos=0;pos<5;pos++)Capture(c,d,pos,folder+"/"+EnvironmentalDistractions.FrontWindowName(pos)+".png");captured=true;}
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
            if(starts.Length!=9 || d.EndedCount!=9 || !captured || !paused)throw new Exception("Incomplete playback: starts="+starts.Length+";ends="+d.EndedCount);
            if(events.Any(x=>x.type=="distraction_skipped" || x.type=="distraction_media_error"))throw new Exception("Media playback failure.");
            foreach(var end in events.Where(x=>x.type=="distraction_ended" && x.detail.Contains("kind=Street;")))
            {
                double exposure=double.Parse(end.detail.Split(new[]{"exposure_s="},StringSplitOptions.None)[1],System.Globalization.CultureInfo.InvariantCulture);
                if(exposure<10 || exposure>10.10 || !end.detail.Contains("reason=duration_complete"))throw new Exception("Ten-second video exposure failed: "+end.detail);
            }
            if(events.Count(x=>x.type=="distraction_audio_path")!=6)throw new Exception("Spatial path logs missing.");
            foreach(var kind in new[]{"Street","Environment","Speech"})if(starts.Count(x=>x.detail.Contains("kind="+kind+";"))!=3)throw new Exception("Unbalanced actual playback.");
            if(d.Visible || d.doorSources.Concat(d.conversationSources).Any(x=>x.isPlaying))throw new Exception("Media remained active after end.");
            var report="PASS: nine live scheduled distractions (three front windows, three environmental sounds, three speech clips); all completed without skips or decoder errors; video pause/hide/resume and frozen task clock verified; all five front-position captures saved; sources stopped at end. Elapsed="+(Time.realtimeSinceStartupAsDouble-origin).ToString("F2")+" seconds.\nIsolated synthetic engine and local Editor media only; not physical headset audibility or standalone performance.\n";
            File.WriteAllText("Evidence/distraction-playback-v1.7.txt",report);Debug.Log(report);return report;
        }
        finally
        {
            d.Cancel("probe_end");writer.Dispose();d.Bind(original,original.Config,c.DataPath);c.enabled=enabled;c.spatialView.instructions.enabled=board;
        }
    }
    static void Capture(TaskPlatformController c,EnvironmentalDistractions d,int side,string file)
    {
        var window=d.windowRoots[0].transform;var windowPosition=window.localPosition;window.localPosition=EnvironmentalDistractions.FrontWindowPosition(side);
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        var data=camera.GetComponent<UniversalAdditionalCameraData>();bool xr=data.allowXRRendering;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            data.allowXRRendering=false;camera.transform.SetPositionAndRotation(new Vector3(0,1.2f,0),Quaternion.identity);
            camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());
        }
        finally
        {window.localPosition=windowPosition;camera.targetTexture=previous;data.allowXRRendering=xr;camera.transform.SetPositionAndRotation(position,rotation);RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
}

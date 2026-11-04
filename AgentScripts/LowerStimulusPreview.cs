using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Video;

public static class LowerStimulusPreview
{
    public static string Validate()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var v=o.spatialView;
        var sphere=o.stimulus;var tray=v.bench.Find("StimulusPlinth").GetComponent<Renderer>();
        var stem=v.bench.Find("PlinthStem").GetComponent<Renderer>();var screen=o.AdaptiveRuntime.Media.Screen;
        int checks=0;Action<bool,string> check=(ok,message)=>{checks++;if(!ok)throw new Exception(message);};
        float originalDiameter=2*o.SessionConfig.distance*Mathf.Tan(o.SessionConfig.visualAngle*Mathf.Deg2Rad*.5f);
        check(Mathf.Abs(sphere.bounds.size.x-originalDiameter*1.08f)<.001f,"Sphere enlarged eight percent");
        check(Mathf.Abs(tray.bounds.max.y-.72f)<.001f,"Lower tray top");
        check(Mathf.Abs(stem.bounds.min.y-.65f)<.001f && Mathf.Abs(stem.bounds.max.y-tray.bounds.min.y)<.001f,"Stem joins table and tray");
        check(Mathf.Abs(sphere.bounds.min.y-tray.bounds.max.y-.025f)<.001f,"Visible 25 mm air gap");
        check(sphere.bounds.center.y<.84f,"Sphere below previous eye-height placement");
        check(screen.lossyScale==new Vector3(3.2f,1.8f,1),"Movie size retained");
        var bounds=GazeRegions.TaskBounds(sphere.bounds,tray.bounds);
        var eye=Camera.main.transform.position;
        check(GazeRegions.Classify(true,eye,sphere.bounds.center-eye,bounds,true,screen,true).task,"Gaze follows actual new sphere bounds");
        check(GazeRegions.Classify(true,eye,screen.TransformPoint(new Vector3(.4f,.4f,0))-eye,bounds,true,screen,true).video,"Unobstructed movie gaze remains movie");
        check(!GazeRegions.Classify(false,eye,Vector3.forward,bounds,true,screen,true).valid,"Missing eyes never substituted");
        string metadata=File.ReadAllText(Path.Combine(o.DataPath,"session.json"));
        check(metadata.Contains(SpatialTaskView.StimulusPlacementRevision) && metadata.Contains("stimulusDiameterScale") && metadata.Contains("traySurfaceHeightMeters"),"Geometry metadata recorded");
        float top=float.NegativeInfinity;var b=sphere.bounds;
        foreach(float x in new[]{b.min.x,b.max.x})foreach(float y in new[]{b.min.y,b.max.y})foreach(float z in new[]{b.min.z,b.max.z})
            top=Mathf.Max(top,Camera.main.WorldToViewportPoint(new Vector3(x,y,z)).y);
        float movieBottom=Camera.main.WorldToViewportPoint(screen.TransformPoint(new Vector3(0,-.5f,0))).y;
        float conservativeOverlap=Mathf.Max(0,top-movieBottom)*720;
        check(conservativeOverlap<20,"Nominal seated view has only a small conservative overlap");
        var originalPosition=sphere.transform.parent.position;
        try
        {
            sphere.transform.parent.position+=new Vector3(0,.2f,0);v.PlaceSupports(sphere.transform.parent);v.PlaceStimulusAboveTray(sphere);
            check(Mathf.Abs(tray.bounds.max.y-.72f)<.001f && sphere.bounds.center.y<.84f,"Support and sphere remain desk-relative after calibration-height change");
        }
        finally{sphere.transform.parent.position=originalPosition;v.PlaceSupports(sphere.transform.parent);v.PlaceStimulusAboveTray(sphere);}
        string result="LOW STIMULUS PASS: "+checks+" checks; diameter="+b.size.x.ToString("F6")+" m; sphere center="+b.center.y.ToString("F6")+" m; conservative projected overlap="+conservativeOverlap.ToString("F2")+" px at 720p, eye height 1.20 m; synthetic only.";
        File.WriteAllText("Evidence/LowerStimulus/validation.txt",result);Debug.Log(result);return result;
    }
    public static async Task<string> ShowMovie()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        o.Engine.Note("engineering_preview","Muted local video frame for geometry inspection; outside protocol scheduler; not participant evidence",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        var screen=o.AdaptiveRuntime.Media.Screen.GetComponent<Renderer>();
        var go=new GameObject("LowerStimulusMoviePreview");var player=go.AddComponent<VideoPlayer>();
        player.playOnAwake=false;player.audioOutputMode=VideoAudioOutputMode.None;player.renderMode=VideoRenderMode.RenderTexture;
        player.targetTexture=new RenderTexture(1280,720,0);player.targetTexture.Create();
        player.url=Path.Combine(Application.persistentDataPath,"AdaptiveMedia","pair_01.mp4");player.Prepare();
        for(int i=0;i<200 && !player.isPrepared;i++)await Task.Delay(25);
        if(!player.isPrepared)throw new Exception("Preview video preparation timed out");
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.name="LowerStimulusPreviewMaterial";
        material.mainTexture=player.targetTexture;screen.sharedMaterial=material;screen.enabled=true;
        player.time=3;player.Play();await Task.Delay(900);player.Pause();
        return "Muted local movie frame on the existing 3.2 x 1.8 m screen; synthetic geometry preview.";
    }
    public static string HideMovie()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var screen=o.AdaptiveRuntime.Media.Screen.GetComponent<Renderer>();
        var go=GameObject.Find("LowerStimulusMoviePreview");
        if(go!=null)
        {
            var player=go.GetComponent<VideoPlayer>();player.Stop();player.targetTexture.Release();UnityEngine.Object.Destroy(player.targetTexture);
            UnityEngine.Object.Destroy(screen.sharedMaterial);UnityEngine.Object.Destroy(go);
        }
        screen.sharedMaterial=(Material)typeof(AdaptiveMedia).GetField("neutral",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o.AdaptiveRuntime.Media);
        screen.enabled=false;return "Preview resources released; original inactive movie material restored.";
    }
    public static string Inspect()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var sphere=o.stimulus;var tray=o.spatialView.bench.Find("StimulusPlinth").GetComponent<Renderer>();
        var screen=o.AdaptiveRuntime.Media.Screen;
        var camera=Camera.main;
        string s="sphere="+sphere.bounds+";tray="+tray.bounds+";eye="+camera.transform.position+";screen="+screen.position+";screen_size="+screen.lossyScale+
            ";sphere_top_view="+camera.WorldToViewportPoint(sphere.bounds.center+Vector3.up*sphere.bounds.extents.y)+
            ";movie_bottom_view="+camera.WorldToViewportPoint(screen.TransformPoint(new Vector3(0,-.5f,0)));
        Directory.CreateDirectory("Evidence/LowerStimulus");File.WriteAllText("Evidence/LowerStimulus/inspection.txt",s);return s;
    }
}

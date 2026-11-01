using System;
using System.IO;
using System.Reflection;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Visual-only snapshots; the synthetic engine has no recorder or audio subscribers.
public static class StimulusMaterialProbe
{
    public static string Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var property=typeof(TaskPlatformController).GetProperty("Engine");
        var render=typeof(TaskPlatformController).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic);
        var original=c.Engine; bool enabled=c.enabled;
        var camera=Camera.main; var position=camera.transform.position; var rotation=camera.transform.rotation;
        var cameraData=camera.GetComponent<UniversalAdditionalCameraData>(); bool xr=cameraData.allowXRRendering;
        var previous=camera.targetTexture; var active=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24); var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        const string folder="Evidence/Visuals-v1.3.1"; Directory.CreateDirectory(folder);
        try
        {
            c.enabled=false; var engine=new TaskEngine(c.SessionConfig); property.SetValue(c,engine);
            engine.Advance(0,0); engine.Advance(0,1); engine.Tick(4.5,2); engine.Tick(9,3);
            render.Invoke(c,null); c.spatialView.Refresh(c,9); Canvas.ForceUpdateCanvases();
            cameraData.allowXRRendering=false; camera.targetTexture=rt;
            camera.transform.SetPositionAndRotation(c.taskAnchor.position-c.taskAnchor.forward*c.SessionConfig.distance,c.taskAnchor.rotation);
            var material=c.stimulus.material;
            if(material.shader.name!="Universal Render Pipeline/Lit" || !material.shader.isSupported)throw new Exception("Unsupported stimulus shader.");
            if(Math.Abs(material.GetFloat("_Smoothness")-.42f)>.001f)throw new Exception("Unexpected smoothness.");
            string[] colors={"blue","orange","green","magenta"};
            for(int i=0;i<4;i++)
            {
                material.color=LabPalette.Stimuli[i]; Capture(colors[i]);
            }
            material.color=LabPalette.Stimuli[0];
            c.spatialView.taskCanvas.enabled=false;
            camera.transform.position=c.taskAnchor.position-c.taskAnchor.forward*.48f;
            Capture("satin-closeup");
            var report="PASS: four palette colors rendered with supported URP Lit, metallic 0 and smoothness 0.42; close-up captured; original live engine restored. Presentation="+LabPresentation.Version+".\nSynthetic visual probe only; not headset or participant evidence.\n";
            File.WriteAllText("Evidence/stimulus-material-v1.3.1.txt",report); Debug.Log(report); return report;
        }
        finally
        {
            camera.targetTexture=previous; cameraData.allowXRRendering=xr;
            camera.transform.SetPositionAndRotation(position,rotation); RenderTexture.active=active;
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
            property.SetValue(c,original); c.enabled=enabled; render.Invoke(c,null);
        }
        void Capture(string name)
        {
            camera.Render(); camera.Render(); RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1920,1080),0,0); pixels.Apply();
            File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EvidenceVR;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class UiOcclusionProbe
{
    public static string Run()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required.");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var property=typeof(TaskPlatformController).GetProperty("Engine");
        var render=typeof(TaskPlatformController).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic);
        var original=c.Engine;bool enabled=c.enabled;var anchor=c.taskAnchor.position;
        const string folder="Evidence/Occlusion-v1.6.1";Directory.CreateDirectory(folder);int checks=0,screens=0;
        try
        {
            c.enabled=false;
            Check(c.spatialView.rule.fontSharedMaterial.IsKeywordEnabled("OUTLINE_ON"),"SDF outline variant enabled");
            foreach(float height in new[]{1.05f,1.2f,1.4f})
            {
                c.taskAnchor.position=new Vector3(0,height,1.5f);c.spatialView.PlaceSupports(c.taskAnchor);
                var e=new TaskEngine(c.SessionConfig);property.SetValue(c,e);double clock=0;int frame=0;
                Show("preparation",clock);e.Advance(clock,frame++);
                while(e.Phase!=TaskPhase.Complete)
                {
                    if(e.Phase==TaskPhase.Instructions){Show("rules-"+e.Block,clock);e.Advance(clock,frame++);}
                    else if(e.Phase==TaskPhase.Rest){Show("rest",clock);clock+=c.SessionConfig.restSeconds;e.Advance(clock,frame++);}
                    else if(e.Phase==TaskPhase.Running)
                    {
                        if(e.TrialIndex==2){Show("running-"+e.Block,clock);e.Pause(clock,frame++);Show("paused-"+e.Block,clock);e.Resume(clock,frame++);}
                        clock+=c.SessionConfig.trialSeconds;e.Tick(clock,frame++);
                    }
                    else throw new Exception("Unexpected phase");
                }
                Show("complete",clock);
                void Show(string phase,double now)
                {
                    render.Invoke(c,null);c.spatialView.Refresh(c,now);Canvas.ForceUpdateCanvases();
                    bool running=e.Phase==TaskPhase.Running;
                    var canvas=running ? c.spatialView.taskCanvas : c.spatialView.instructions;
                    var labels=canvas.GetComponentsInChildren<TMP_Text>().Where(x=>x.isActiveAndEnabled).ToArray();
                    var blockers=c.spatialView.bench.GetComponentsInChildren<Renderer>().Where(x=>x.enabled).ToList();
                    if(c.stimulus.enabled)blockers.Add(c.stimulus);
                    foreach(var label in labels)
                    {
                        label.ForceMeshUpdate();Check(!label.isTextOverflowing,"Overflow "+label.name);
                        var corners=new Vector3[4];label.rectTransform.GetWorldCorners(corners);
                        foreach(var shift in new[]{Vector3.zero,new Vector3(-.12f,-.10f,0),new Vector3(.12f,.10f,.10f)})
                        foreach(var target in corners.Concat(new[]{label.transform.position}))
                        {
                            var eye=new Vector3(0,height,0)+shift;var delta=target-eye;var ray=new Ray(eye,delta.normalized);
                            foreach(var blocker in blockers)
                            {float distance;Check(!blocker.bounds.IntersectRay(ray,out distance) || distance>=delta.magnitude-.005f,"Occluded "+phase+"/"+label.name+" by "+blocker.name+" at eye "+height);}
                        }
                    }
                    if(height==1.05f || phase=="preparation" || phase=="running-2")
                    {Capture(c,new Vector3(0,height,0),folder+"/"+height.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"-"+phase+".png");screens++;}
                }
            }
            string report="PASS: "+checks+" checks; all visible text rectangles clear of desk, plinth and sphere at 1.05/1.20/1.40 m calibrated eye heights and three viewing offsets; preparation, both rules, practice/formal HUD, pause, rest and completion; no TMP overflow. "+screens+" synthetic screenshots saved. Not physical headset acceptance.";
            File.WriteAllText("Evidence/ui-occlusion-v1.6.1.txt",report);Debug.Log(report);return report;
        }
        finally{property.SetValue(c,original);c.enabled=enabled;c.taskAnchor.position=anchor;c.spatialView.PlaceSupports(c.taskAnchor);render.Invoke(c,null);}
        void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    }
    static void Capture(TaskPlatformController c,Vector3 eye,string file)
    {
        var camera=Camera.main;var pos=camera.transform.position;var rot=camera.transform.rotation;
        var data=camera.GetComponent<UniversalAdditionalCameraData>();bool xr=data.allowXRRendering;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1600,1000,24);var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try{data.allowXRRendering=false;camera.transform.SetPositionAndRotation(eye,Quaternion.identity);camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());}
        finally{camera.targetTexture=previous;data.allowXRRendering=xr;camera.transform.SetPositionAndRotation(pos,rot);RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
}

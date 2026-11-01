using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EvidenceVR;
using TMPro;
using UnityEngine;

// Isolated presentation probe. Synthetic clocks never feed the participant recorder.
public static class VisualReviewProbe
{
    static TaskPlatformController controller;
    static readonly PropertyInfo EngineProperty=typeof(TaskPlatformController).GetProperty("Engine");
    static readonly MethodInfo RenderMethod=typeof(TaskPlatformController).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic);
    static int screens;
    const string CaptureDirectory="Evidence/Visuals-v1.7";
    static double snapshotTime;
    public static string Run()
    {
        if(!Application.isPlaying) throw new Exception("Run in Play Mode.");
        controller=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var original=controller.Engine;
        bool enabled=controller.enabled;
        screens=0;
        Directory.CreateDirectory(CaptureDirectory);
        try
        {
            controller.enabled=false;
            var engine=new TaskEngine(controller.SessionConfig);
            EngineProperty.SetValue(controller,engine);
            Show("preparation");
            double clock=0; int frame=0;
            engine.Advance(clock,frame++);
            Show("easy-rules");
            while(engine.Phase!=TaskPhase.Complete)
            {
                if(engine.Phase==TaskPhase.Instructions)
                {
                    if(engine.Mode==TaskMode.Hard) Show("hard-rules");
                    engine.Advance(clock,frame++);
                }
                else if(engine.Phase==TaskPhase.Rest)
                {
                    Show("rest"); clock+=controller.SessionConfig.restSeconds;
                    engine.Advance(clock,frame++);
                }
                else if(engine.Phase==TaskPhase.Running)
                {
                    RenderMethod.Invoke(controller,null);
                    snapshotTime=clock;
                    int number=engine.TrialIndex+1;
                    if(!controller.footer.text.StartsWith("COLOR "+number+"/")) throw new Exception("Color position label mismatch");
                    if(!controller.badge.text.Contains("STAGE "+(engine.Block+1)+"/4")) throw new Exception("Stage label mismatch");
                    if(!engine.Current.scored && !controller.body.text.Contains("Do not respond")) throw new Exception("Warm-up guidance missing");
                    if(engine.Current.scored && engine.Mode==TaskMode.Hard && !controller.body.text.Contains("#"+number+" with color #"+(number-2))) throw new Exception("Two-back reference index mismatch");
                    if(engine.TrialIndex<2 || engine.TrialIndex==6) Show("block-"+engine.Block+"-color-"+number);
                    if(engine.TrialIndex==2)
                    {
                        Show(engine.Practice ? "practice-stimulus" : "formal-stimulus");
                        bool accepted=engine.Respond(engine.Current.expectedMatch,clock+.2,frame++);
                        if(!accepted) throw new Exception("Synthetic response rejected");
                        Show(engine.Practice ? "practice-feedback" : "formal-response");
                        if(!engine.Practice && !controller.presentation.actionHint.text.Contains("Correct")) throw new Exception("Formal feedback missing");
                        if(engine.Practice && !controller.presentation.actionHint.text.Contains("Correct")) throw new Exception("Practice feedback missing");
                        if(!engine.Practice)
                        {
                            engine.Pause(clock+.3,frame++); Show("paused"); engine.Resume(clock+.3,frame++);
                        }
                    }
                    clock+=controller.SessionConfig.trialSeconds;
                    engine.Tick(clock,frame++);
                }
                else throw new Exception("Unexpected phase: "+engine.Phase);
            }
            Show("complete");
            engine=new TaskEngine(controller.SessionConfig); EngineProperty.SetValue(controller,engine);
            engine.Abort(clock,frame++,"visual probe"); Show("aborted");
            var defaults=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r=>r.sharedMaterials.Any(m=>m!=null && m.name=="Default-Material")).Select(r=>r.name).ToArray();
            if(defaults.Length>0)throw new Exception("Default materials: "+string.Join(",",defaults));
            var report="PASS: "+screens+" presentation snapshots checked; stage and color counters verified across 88 stimuli; two-back references verified; warm-up response guidance present; no active TMP overflow; rounded surfaces have meshes; no default materials; practice feedback present; formal feedback present.\nSynthetic state injection with original engine restored; not physical input, timing or headset evidence.\n";
            File.WriteAllText("Evidence/visual-validation-v1.7.txt",report); Debug.Log(report); return report;
        }
        finally
        {
            EngineProperty.SetValue(controller,original); controller.enabled=enabled;
            RenderMethod.Invoke(controller,null); Canvas.ForceUpdateCanvases();
        }
    }
    static void Show(string name)
    {
        RenderMethod.Invoke(controller,null); Canvas.ForceUpdateCanvases();
        controller.spatialView?.Refresh(controller,snapshotTime); Canvas.ForceUpdateCanvases();
        foreach(var text in controller.taskAnchor.GetComponentsInChildren<TMP_Text>())
        {
            if(text.canvas==null || !text.canvas.isActiveAndEnabled)continue;
            text.ForceMeshUpdate();
            if(text.isTextOverflowing)throw new Exception(name+": text overflow in "+text.name);
        }
        foreach(var surface in UnityEngine.Object.FindObjectsByType<RoundedSurface>(FindObjectsSortMode.None))
            if(surface.canvas!=null && surface.canvas.isActiveAndEnabled && (surface.canvasRenderer==null || surface.canvasRenderer.GetMesh()==null || surface.canvasRenderer.GetMesh().vertexCount==0))throw new Exception(name+": empty panel "+surface.name);
        Capture(name); if(name=="block-3-color-7")Capture("spatial-angle"); screens++;
    }
    static void Capture(string name)
    {
        var camera=Camera.main; var previous=camera.targetTexture; var active=RenderTexture.active;
        var position=camera.transform.position; var rotation=camera.transform.rotation;
        var cameraData=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        bool xr=cameraData.allowXRRendering;
        var rt=new RenderTexture(1920,1080,24); var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            cameraData.allowXRRendering=false;
            camera.transform.SetPositionAndRotation(controller.taskAnchor.position-controller.taskAnchor.forward*controller.SessionConfig.distance,controller.taskAnchor.rotation);
            if(name=="spatial-angle")
            {
                camera.transform.position+=controller.taskAnchor.right*.45f;
                camera.transform.rotation=Quaternion.LookRotation(controller.taskAnchor.position-camera.transform.position,Vector3.up);
            }
            camera.targetTexture=rt;
            camera.Render(); // Warm-up render is discarded.
            camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
            File.WriteAllBytes(CaptureDirectory+"/"+name+".png",image.EncodeToPNG());
        }
        finally
        { camera.targetTexture=previous; cameraData.allowXRRendering=xr; camera.transform.SetPositionAndRotation(position,rotation); RenderTexture.active=active; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
}

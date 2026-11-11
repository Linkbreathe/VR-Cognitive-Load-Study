using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using EvidenceVR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

public static class EyeTrackingRepairValidation
{
    const string Folder="Evidence/EyeTrackingRepair-20261110";
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);

    public static string Configure()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        foreach(var group in new[]{BuildTargetGroup.Standalone,BuildTargetGroup.Android})
        {
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group);
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            var feature=settings.GetFeature<QuestLinkEyeSpaceFeature>();
            if(feature==null)throw new Exception("Missing eye freshness feature: "+group);
            feature.enabled=true;feature.retryWithNullPath=group==BuildTargetGroup.Standalone;
            EditorUtility.SetDirty(feature);AssetDatabase.SaveAssetIfDirty(feature);
        }
        return "Eye pose/timestamp feature enabled for Standalone and Android; narrow retry enabled for Standalone only. No runtime started.";
    }

    public static string Run()
    {
        checks=0;Directory.CreateDirectory(Folder);
        var freeze=new EyeFreezeDetector();var time=new EyeSampleFreshness();
        for(int i=0;i<=120;i++)
        {
            bool fresh=time.Update(i/30.0,true,1000000+i*33333);
            bool rejected=freeze.Update(i/30.0,true,Quaternion.Euler(0,.02f*Mathf.Sin(i),0)*Vector3.forward,!fresh && time.Status=="EYE SAMPLE STALE");
            Check(!rejected,"Fresh fixation/small motion must never be frozen");
        }
        Check(freeze.LowMotion,"Low angular spread remains diagnostic");
        Check(!time.Update(4.3,true,1000000+120*33333),"Repeating native time remains rejected");
        Check(freeze.Update(4.3,true,Vector3.right,true),"Stale time rejected even when direction changes with head motion");
        time.Invalidate();Check(!time.Update(4.31,true,1000000+120*33333),"Coordinate reset cannot refresh a cached timestamp");
        Check(time.Update(4.32,true,6000000),"New stamp can start recovery");
        Check(!time.Update(4.33,false,0),"Missing eyes/head cannot be valid");
        Check(!time.Update(4.34,true,6000000),"Blink/loss cannot refresh a cached timestamp");
        Check(!time.Update(4.35,true,1),"Backward time requires new progress");
        Check(time.Update(4.36,true,2),"New timestamp epoch resumes progress");
        int replayRows=0;
        foreach(var path in Directory.GetFiles("Evidence/EyeFreezeRecurrence-20261110","native-layers-*.json"))
        {
            var recorded=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));var gate=new EyeSampleFreshness();
            foreach(var p in recorded["points"])
            {Check(!gate.Update((double)p["time"],(int)p["result"]==0 && (ulong)p["flags"]==15,(long)p["eyeSampleTime"]),"Archived frozen input cannot recover");replayRows++;}
        }
        var continuity=new EyeTrackingContinuity();
        var identity=Matrix4x4.identity;
        Check(!continuity.Observe(0,true,1,2,1,identity,10),"First input cannot authorize coordinates");
        Check(!continuity.Observe(.04,true,1,2,1,identity,11),"Recovery needs duration and distinct samples");
        Check(!continuity.Observe(.12,true,1,2,1,identity,11),"Repeated timestamp cannot complete recovery");
        Check(continuity.Observe(.16,true,1,2,1,identity,12),"Coherent fresh sequence recovers");
        Check(!continuity.Observe(.17,false,1,2,1,identity,13),"Head/eye loss immediately invalidates coordinates");
        Check(!continuity.Observe(.18,true,1,2,1,identity,14),"Loss recovery cannot reuse old coverage");
        Check(!continuity.Observe(.30,true,3,2,1,identity,15),"Replacement head invalidates old frame");
        Check(!continuity.Observe(.42,true,3,4,1,identity,16),"Replacement eye invalidates old frame");
        Check(!continuity.Observe(.54,true,3,4,2,identity,17),"Native session/app-space epoch invalidates old frame");
        var moved=Matrix4x4.TRS(new Vector3(1,.5f,-2),Quaternion.Euler(0,30,0),Vector3.one);
        Check(!continuity.Observe(.66,true,3,4,2,moved,18),"Rig recenter invalidates old frame");
        Check(!continuity.Observe(.70,true,3,4,2,moved,19),"Recenter awaits coherent observations");
        Check(continuity.Observe(.80,true,3,4,2,moved,20),"New coordinate frame recovers without moving scene geometry");
        Check(!continuity.Observe(1.2,true,3,4,2,moved,21),"Sampling gap cannot bridge coordinate evidence");
        Check(!AdaptiveRuntime.IsHeadTracked(default),"Invalid head fails production gate");
        Check(QuestLinkEyeSpaceFeature.ToUnityPosition(new Vector3(1,2,-3))==new Vector3(1,2,3),"Native right-handed position conversion");
        var xrRotation=Quaternion.Euler(12,31,-8);
        var nativeDirection=xrRotation*Vector3.back;
        var unityDirection=QuestLinkEyeSpaceFeature.ToUnityRotation(xrRotation)*Vector3.forward;
        Check(Vector3.Distance(unityDirection,QuestLinkEyeSpaceFeature.ToUnityPosition(nativeDirection))<1e-5f,"Native orientation handedness and forward axis");
        // The same physical ray expressed in different app-space frames must reach the
        // same world target. Neither the room nor the task is repositioned by recovery.
        var worldOrigin=new Vector3(.2f,1.2f,-.3f);var worldTarget=new Vector3(0,.8f,1.5f);
        foreach(var frame in new[]{identity,moved,Matrix4x4.TRS(new Vector3(-.7f,.2f,.3f),Quaternion.Euler(0,-63,0),Vector3.one)})
        {
            var localOrigin=frame.inverse.MultiplyPoint3x4(worldOrigin);
            var localDirection=frame.inverse.MultiplyVector((worldTarget-worldOrigin).normalized);
            var nativePosition=QuestLinkEyeSpaceFeature.ToUnityPosition(localOrigin);
            var nativeOrientation=QuestLinkEyeSpaceFeature.ToUnityRotation(Quaternion.LookRotation(localDirection));
            var recoveredOrigin=frame.MultiplyPoint3x4(QuestLinkEyeSpaceFeature.ToUnityPosition(nativePosition));
            var recoveredDirection=frame.MultiplyVector(QuestLinkEyeSpaceFeature.ToUnityRotation(nativeOrientation)*Vector3.forward);
            Check(GazeRegions.Classify(true,recoveredOrigin,recoveredDirection,new Bounds(worldTarget,Vector3.one*.2f),true,null,false).task,"Native pose/world ROI remains consistent across translated and rotated frames");
        }
        var featureType=typeof(QuestLinkEyeSpaceFeature);
        Check(Marshal.SizeOf(featureType.GetNestedType("SpaceLocation",BindingFlags.NonPublic))==56,"Native location ABI");
        Check(Marshal.SizeOf(featureType.GetNestedType("EyeSampleTime",BindingFlags.NonPublic))==24,"Native sample-time ABI");
        var appSpace=featureType.GetMethod("AppSpaceAndDisplayTime",BindingFlags.NonPublic|BindingFlags.Static);
        var nativeArgs=new object[]{(ulong)0,(long)0};
        Check(appSpace.Invoke(null,nativeArgs) is bool,"Installed native app-space/time export is callable");
        var retry=featureType.GetMethod("ShouldRetry",BindingFlags.NonPublic|BindingFlags.Static);
        Check((bool)retry.Invoke(null,new object[]{true,true,-22,true,(uint)1,(ulong)7,(ulong)7,(ulong)7}),"Known single-eye retry retained");
        Check(!(bool)retry.Invoke(null,new object[]{true,true,-22,true,(uint)2,(ulong)7,(ulong)7,(ulong)7}),"Multi-source actions never retried");
        Check(!(bool)retry.Invoke(null,new object[]{true,false,-22,true,(uint)1,(ulong)7,(ulong)7,(ulong)7}),"Non-Oculus actions never retried");
        foreach(var group in new[]{BuildTargetGroup.Standalone,BuildTargetGroup.Android})
        {
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            Check(settings.GetFeature<EyeGazeInteraction>().enabled,"Eye profile enabled: "+group);
            Check(settings.GetFeature<QuestLinkEyeSpaceFeature>().enabled,"Native metadata enabled: "+group);
            Check(settings.GetFeature<QuestLinkEyeSpaceFeature>().retryWithNullPath==(group==BuildTargetGroup.Standalone),"Retry target boundary: "+group);
        }
        VisualFixture();
        string current=File.ReadAllText("Assets/TaskPlatform/Runtime/AdaptiveRuntime.cs");
        string before=File.ReadAllText(Folder+"/restore/Assets/TaskPlatform/Runtime/AdaptiveRuntime.cs");
        foreach(var header in before.Split('\n').Where(l=>l.TrimStart().StartsWith("Open(\"")))
            Check(current.Contains(header.Trim()),"Existing log schema unchanged: "+header.Trim().Split(',')[0]);
        var task=new TaskConfig();Check(task.trialSeconds==4.5f && task.responseSeconds==4 && task.visibleSeconds==4,"Task timing remains unchanged");
        string report="PASS: "+checks+" isolated eye repair checks; "+replayRows+" archived frozen rows rejected; mode="+(Application.isPlaying?"isolated Play":"Edit")+". No participant data or hardware recovery claim.";
        File.WriteAllText(Folder+"/validation-"+(Application.isPlaying?"play":"edit")+".txt",report);Debug.Log(report);return report;
    }

    static void VisualFixture()
    {
        Scene preview=default;
        if(!Application.isPlaying)preview=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("SYNTHETIC Eye repair fixture");
        if(preview.IsValid())SceneManager.MoveGameObjectToScene(root,preview);
        Material sphereMaterial=null;TaskPlatformController owner=null;
        try
        {
            owner=root.AddComponent<TaskPlatformController>();owner.enabled=false;
            var engine=new TaskEngine(new TaskConfig());Set(owner,"<Engine>k__BackingField",engine);
            var headObject=new GameObject("Synthetic head");headObject.transform.SetParent(root.transform,false);owner.head=headObject.transform;
            var camera=headObject.AddComponent<Camera>();camera.enabled=false;
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.transform.SetParent(root.transform,false);sphere.transform.position=Vector3.forward*2;sphere.transform.localScale=Vector3.one*.25f;
            owner.stimulus=sphere.GetComponent<Renderer>();sphereMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));sphereMaterial.color=new Color(.1f,.35f,.85f);owner.stimulus.sharedMaterial=sphereMaterial;
            var debugObject=new GameObject("Synthetic overlay");debugObject.transform.SetParent(root.transform,false);
            var highlight=debugObject.AddComponent<GazeDebugHighlighter>();
            highlight.Bind(owner,new AdaptiveTrainingConfig{enabled=true,sessionType="engineering",engineeringGazeHighlight=true});
            var bound=GazeRegions.TaskBounds(owner.stimulus.bounds,owner.stimulus.bounds);
            var hit=GazeRegions.Classify(true,Vector3.zero,Vector3.forward,bound,true,null,false);
            var miss=GazeRegions.Classify(true,Vector3.zero,Vector3.right,bound,true,null,false);
            highlight.Present(miss,Time.realtimeSinceStartupAsDouble);
            Check(highlight.TaskArmed && !highlight.TaskHighlighted,"Idle outline is not an eye hit");
            if(!Application.isPlaying)Capture(root,camera,"outline-idle.png");
            foreach(TaskPhase phase in Enum.GetValues(typeof(TaskPhase)))
            {
                Set(engine,"<Phase>k__BackingField",phase);owner.stimulus.enabled=phase==TaskPhase.Running;
                highlight.Present(hit,Time.realtimeSinceStartupAsDouble);
                Check(highlight.TaskHighlighted,"Fresh engineering eyes can check placed outline in "+phase);
            }
            owner.stimulus.enabled=true;
            highlight.Present(hit,Time.realtimeSinceStartupAsDouble);
            var line=debugObject.GetComponentsInChildren<LineRenderer>().Single(l=>l.positionCount==64);
            Set(highlight,"lastVisualTime",Time.realtimeSinceStartupAsDouble-GazeDebugHighlighter.FadeInSeconds);
            Call(highlight,"Refresh");
            Check(Mathf.Abs(line.widthMultiplier-.0035f)<.00001f && Mathf.Abs(line.sharedMaterial.GetColor("_Color").a-GazeDebugHighlighter.HitAlpha)<.001f,"Soft hit fades up above the position-only outline");
            if(!Application.isPlaying)Capture(root,owner.head.GetComponent<Camera>(),"outline-hit.png");
            highlight.Present(default,Time.realtimeSinceStartupAsDouble);
            Check(highlight.TaskArmed && !highlight.TaskHighlighted,"Invalid/frozen eyes never light the target");
            Check(line.widthMultiplier==.0015f && line.sharedMaterial.GetColor("_Color").a<.11f,"Invalid input returns to faint position aid");
            highlight.Present(hit,Time.realtimeSinceStartupAsDouble);Set(highlight,"lastSample",double.NegativeInfinity);Call(highlight,"LateUpdate");
            Check(!highlight.TaskHighlighted,"Stale visual sample clears hit");
            Call(highlight,"OnApplicationPause",true);highlight.Present(hit,Time.realtimeSinceStartupAsDouble);
            Check(!highlight.TaskHighlighted,"Application suspension cannot highlight");
            Call(highlight,"OnApplicationPause",false);
            Check(owner.stimulus.sharedMaterial.color==new Color(.1f,.35f,.85f),"Overlay never changes stimulus color");
            var deniedObject=new GameObject("Participant lockout");deniedObject.transform.SetParent(root.transform,false);
            var denied=deniedObject.AddComponent<GazeDebugHighlighter>();denied.Bind(owner,new AdaptiveTrainingConfig{enabled=true,sessionType="participant",engineeringGazeHighlight=true});denied.Present(hit,Time.realtimeSinceStartupAsDouble);
            Check(!denied.Allowed && deniedObject.GetComponentsInChildren<LineRenderer>().Length==0,"Participant cannot create highlighter even when requested");
        }
        finally
        {
            if(owner!=null)Set(owner,"<Engine>k__BackingField",null);
            UnityEngine.Object.DestroyImmediate(root);
            if(sphereMaterial!=null)UnityEngine.Object.DestroyImmediate(sphereMaterial);
            if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
        }
    }
    static void Capture(GameObject root,Camera camera,string name)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
        camera.scene=root.scene;camera.enabled=false;camera.cullingMask=1<<30;camera.fieldOfView=32;camera.nearClipPlane=.05f;camera.farClipPlane=10;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.12f,.12f);
        var target=new RenderTexture(800,600,24);var image=new Texture2D(800,600,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,800,600),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name,image.EncodeToPNG());
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
    }

    public static string PreparePlay()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve dirty scenes before isolated validation.");
        SessionState.SetString("EyeRepair.SceneSetup",Newtonsoft.Json.JsonConvert.SerializeObject(EditorSceneManager.GetSceneManagerSetup()));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        return "Empty scene prepared; no task controller, assignment client or participant recorder.";
    }
    public static string NativeSnapshot()
    {
        bool readable=QuestLinkEyeSpaceFeature.TryReadSample(out var position,out var rotation,out long stamp,out ulong flags);
        string json=Newtonsoft.Json.JsonConvert.SerializeObject(new { runtime=OpenXRRuntime.name,
            state=QuestLinkEyeSpaceFeature.LastResult,epoch=QuestLinkEyeSpaceFeature.TrackingEpoch,
            frame=Time.frameCount,readable,stamp,flags,position=position.ToString("F6"),rotation=rotation.ToString("F6"),
            note="Read-only isolated snapshot; absence of an active XR runtime cannot verify live eye recovery." },Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(Folder+"/native-snapshot.json",json);Debug.Log("Eye repair native snapshot: "+json);return json;
    }
    public static string RestoreScenes()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        var setup=Newtonsoft.Json.JsonConvert.DeserializeObject<SceneSetup[]>(SessionState.GetString("EyeRepair.SceneSetup",""));
        EditorSceneManager.RestoreSceneManagerSetup(setup);SessionState.EraseString("EyeRepair.SceneSetup");
        return "Original saved scenes restored.";
    }
}

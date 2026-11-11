using System;
using System.Collections;
using System.IO;
using System.Reflection;
using EvidenceVR;
using UnityEditor;
using UnityEngine;

// Isolated synthetic geometry only; never writes participant records or feeds eye acquisition.
public static class SeatedHeightValidation
{
    const string Folder="Evidence/SeatedHeight-20261110";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    public static string Run()
    {
        checks=0;
        var rig=new GameObject("SyntheticSeatedRig");
        var anchor=new GameObject("SyntheticTaskAnchor");
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var root=new GameObject("SyntheticSeatedController");
        var head=new GameObject("SyntheticHead");head.transform.SetParent(rig.transform,false);
        sphere.transform.SetParent(anchor.transform,false);
        var owner=root.AddComponent<TaskPlatformController>();owner.enabled=false;
        owner.head=head.transform;owner.taskAnchor=anchor.transform;owner.stimulus=sphere.GetComponent<Renderer>();
        var config=JsonUtility.FromJson<TaskConfig>(File.ReadAllText("Assets/TaskPlatform/Config/default-task-config.json"));
        var engine=new TaskEngine(config);
        typeof(TaskPlatformController).GetField("config",Private).SetValue(owner,config);
        typeof(TaskPlatformController).GetField("<Engine>k__BackingField",Private).SetValue(owner,engine);
        try
        {
            foreach(bool floor in new[]{true,false})foreach(float height in new[]{0f,.80f,.95f,1.2f,1.65f})
            {
                head.transform.localPosition=new Vector3(.03f,height,-.02f);
                rig.transform.position=new Vector3(0,TaskPlatformController.TrackingRigHeight(floor,height),0);
                Check(Mathf.Abs(head.transform.position.y-1.2f)<.00001f,"Normalized eye height");
                Vector3 target=new Vector3(0,.829901f,1.5f);
                Vector3 localEye=head.transform.localPosition+Vector3.right*.01f;
                Vector3 eye=rig.transform.TransformPoint(localEye);
                Vector3 localDirection=rig.transform.InverseTransformDirection((target-eye).normalized);
                var sample=GazeRegions.Classify(true,rig.transform.TransformPoint(localEye),rig.transform.TransformDirection(localDirection),new Bounds(target,Vector3.one*.17f),true,null,false);
                Check(sample.valid && sample.task,"Normalized eye ray hits unchanged sphere bounds");
                Check(Mathf.Abs((eye-head.transform.position).y)<.00001f,"Camera and eye use the same height offset");
                Check(!GazeRegions.Classify(false,eye,target-eye,new Bounds(target,Vector3.one*.17f),true,null,false).valid,"Invalid eyes remain invalid");
                var tracked=head.transform.localPosition;tracked.y+=.1f;head.transform.localPosition=tracked;
                Check(Mathf.Abs(head.transform.position.y-1.3f)<.00001f,"Head motion remains tracked after calibration");
            }
            foreach(float height in new[]{.95f,1.65f})
            {
                head.transform.localPosition=new Vector3(0,height,0);
                var routine=(IEnumerator)typeof(TaskPlatformController).GetMethod("CalibrateSeatedPose",Private).Invoke(owner,null);
                while(routine.MoveNext()){}
                Check(owner.Calibrated && Mathf.Abs(head.transform.position.y-1.2f)<.00001f,"Preparation coroutine applies seated height");
                Check(Mathf.Abs(anchor.transform.position.y-1.2f)<.00001f,"Task anchor uses normalized head");
            }
            engine.Advance(1,1);
            Check(engine.Phase!=TaskPhase.Preparation,"Fixture leaves preparation");
            var before=rig.transform.position;head.transform.localPosition=Vector3.up*.8f;
            owner.Recenter();Check(rig.transform.position==before,"Recenter cannot move rig during an active task");
            string recordRoot=Path.Combine(Folder,"synthetic-metadata",Guid.NewGuid().ToString("N"));
            using(var recorder=new SessionRecorder(recordRoot,config,participant:"SYNTHETIC_SEATED_HEIGHT",kind:"engineering"))
            {
                var metadata=File.ReadAllText(Path.Combine(recorder.DirectoryPath,"session.json"));
                Check(metadata.Contains(TaskPlatformController.SeatedHeightRevision),"Metadata records height revision");
                Check(metadata.Contains("seatedEyeHeightMeters"),"Metadata records target height");
                recorder.Complete("synthetic_validation_only");
            }
            string report="PASS: "+checks+" synthetic checks; playMode="+EditorApplication.isPlaying+"; "+TaskPlatformController.SeatedHeightRevision+"; no participant data or real-eye accuracy claim.";
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,EditorApplication.isPlaying?"playmode-validation.txt":"editmode-validation.txt"),report);
            Debug.Log(report);return report;
        }
        finally
        {
            // The fixture does not own a recorder; detach its engine before controller destruction.
            typeof(TaskPlatformController).GetField("<Engine>k__BackingField",Private).SetValue(owner,null);
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(anchor);UnityEngine.Object.DestroyImmediate(rig);
        }
    }
    public static string PreparePlay()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        SessionState.SetBool("SeatedHeightControllerWasEnabled",owner.enabled);
        owner.enabled=false;
        return "Scene task controller temporarily disabled to prevent operator claims during synthetic Play Mode validation.";
    }
    public static string Restore()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        owner.enabled=SessionState.GetBool("SeatedHeightControllerWasEnabled",true);
        return "Restored scene task controller enabled="+owner.enabled;
    }
}

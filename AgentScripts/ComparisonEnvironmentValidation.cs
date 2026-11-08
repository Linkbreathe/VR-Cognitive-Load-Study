using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ComparisonEnvironmentValidation
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static async Task<object> Connect()
    {
        Check(Application.isPlaying,"Play Mode required");
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(owner!=null && owner.TestClient!=null && owner.Engine==null,"Expected idle operator setup");
        owner.TestClient.StopAllCoroutines();owner.TestClient.Bind(owner,"http://127.0.0.1:18766");
        double end=Time.realtimeSinceStartupAsDouble+25;
        while(Time.realtimeSinceStartupAsDouble<end && (owner.AdaptiveRuntime==null || !owner.AdaptiveRuntime.CanStart))await Task.Delay(100);
        var rt=owner.AdaptiveRuntime;
        Check(rt!=null,"Isolated session was not claimed: "+owner.TestClient.Status);
        var c=owner.Engine.Adaptive.Config;
        Check(c.DesktopComparison,"Desktop assignment missing");
        Check(rt.CanStart,"Desktop preparation blocked: "+rt.LastServiceError+";clock="+rt.StudyClockReady+";media="+rt.Media.EngineeringReady);
        Check(rt.GazeTrackingStatus=="SIMULATED GAZE 75% / 70%","Source label missing");
        Check(owner.head.GetComponent<Camera>().stereoTargetEye==StereoTargetEyeMask.None,"Desktop camera is not monocular");
        Check(!owner.head.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled,"Desktop pose still follows XR");
        var clone=JsonUtility.FromJson<AdaptiveTrainingConfig>(JsonUtility.ToJson(c));
        clone.comparisonEnvironment="quest_pro";clone.Validate();
        Check(!clone.ComparisonTrackingReady(false,false),"VR permits missing headset/eyes");
        Check(!clone.ComparisonTrackingReady(true,false),"VR permits missing eyes");
        Check(!clone.ComparisonTrackingReady(false,true),"VR permits missing headset");
        Check(clone.ComparisonTrackingReady(true,true),"VR rejects complete tracking");
        var result=new {evidence="Unity Editor engineering rehearsal; no worn devices or EEG model verification",session=c.testSessionId,
            desktopCanStart=rt.CanStart,rt.GazeTrackingStatus,rt.HeadsetTracked,rt.TestAssignmentConfirmed,rt.StudyClockReady,
            camera=owner.head.GetComponent<Camera>().stereoTargetEye.ToString(),headPosition=owner.head.position.ToString(),vrGateCases=4};
        File.WriteAllText("Evidence/ComparisonEnvironments-20261107/unity-preparation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        Debug.Log("Comparison environments verified: isolated Desktop assignment ready without tracked eyes; fixed camera; VR requires headset AND eyes.");
        return result;
    }
    public static async Task<object> KeyboardChecks()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(owner.Engine.Adaptive.Config.DesktopComparison && owner.AdaptiveRuntime.CanStart,"Desktop not ready");
        var keyboard=InputSystem.AddDevice<Keyboard>("ComparisonValidationKeyboard");
        try
        {
            foreach(var key in new[]{Key.C,Key.A,Key.B})
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Task.Delay(200);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(200);
            }
            Check(owner.Calibrated && owner.MatchChecked && owner.NonmatchChecked,"Keyboard preparation failed");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));await Task.Delay(200);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(200);
            Check(owner.Engine.Phase!=TaskPhase.Preparation,"Enter did not advance");
            var result=new {owner.Calibrated,owner.MatchChecked,owner.NonmatchChecked,phase=owner.Engine.Phase.ToString(),stage=owner.Engine.Adaptive.Stage};
            File.WriteAllText("Evidence/ComparisonEnvironments-20261107/unity-keyboard.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            Debug.Log("Desktop comparison keyboard verified: C seating, A/B checks, Enter advances.");
            return result;
        }
        finally{InputSystem.RemoveDevice(keyboard);}
    }
    public static object RestoreView()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        owner.GetType().GetMethod("ConfigureComparisonView",Private).Invoke(owner,new object[]{false});
        Check(owner.head.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled,"Pose driver did not restore");
        Debug.Log("Comparison environment restore verified: XR pose driver restored after Desktop mode.");
        return new{poseRestored=true,camera=owner.head.GetComponent<Camera>().stereoTargetEye.ToString()};
    }
}

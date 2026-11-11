using System;
using System.IO;
using EvidenceVR;
using UnityEngine;

public static class GazeDebugInheritanceValidation
{
    const string Folder="Evidence/GazeDebugInheritance-20261110";
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static string Model()
    {
        checks=0;
        var missing=TestSessionClient.CaptureGazeDebugPreferences(null);
        Check(!missing.highlight && !missing.ray,"Missing local preferences fail closed");
        foreach(string kind in new[]{"engineering","participant","editor-test","synthetic"})
        foreach(bool enabled in new[]{false,true})
        foreach(bool highlight in new[]{false,true})
        foreach(bool ray in new[]{false,true})
        {
            var config=new AdaptiveTrainingConfig{sessionType=kind,enabled=enabled,engineeringGazeHighlight=highlight,engineeringGazeRay=ray};
            var preferences=TestSessionClient.CaptureGazeDebugPreferences(config);
            bool allowed=kind=="engineering" && enabled;
            Check(preferences.highlight==(allowed && highlight) && preferences.ray==(allowed && ray),"Local permission matrix");
            Check(preferences.revision==TestSessionClient.GazeDebugInheritanceRevision,"Claim revision");
            Check(config.engineeringGazeHighlight==highlight && config.engineeringGazeRay==ray,"Local settings unchanged");
        }
        string report="PASS: "+checks+" local preference permission/revision/immutability checks. Engineering fixture; no eye data generated.";
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/unity-model.txt",report);Debug.Log(report);return report;
    }
    public static string Live()
    {
        checks=0;
        Check(Application.isPlaying,"Play Mode required");
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(owner!=null && owner.Engine!=null,"Console assignment received");
        var config=owner.Engine.Adaptive.Config;
        Check(config.participantCode=="ENGINEERING_GAZE_INHERITANCE" && config.controllerUrl=="http://127.0.0.1:8766","Isolated engineering assignment only");
        Check(owner.Engine.Phase==TaskPhase.Preparation,"No trial started");
        Check(config.engineeringGazeHighlight && config.engineeringGazeRay,"Both flags in frozen task config");
        var runtime=owner.AdaptiveRuntime;
        Check(runtime.ServiceConnected,"Telemetry accepted by service");
        Check(runtime.GazeDebug.Allowed && runtime.GazeRay.Allowed && runtime.GazeRay.RayEnabled,"Both runtime permissions enabled");
        Check(runtime.GazeDebug.GetComponentsInChildren<LineRenderer>().Length==3,"Two outline renderers and one eye ray allocated");
        Check(!runtime.GazeDebug.TaskHighlighted && !runtime.GazeDebug.VideoHighlighted,"Preparation outlines remain hidden");
        var disk=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(Path.Combine(owner.DataPath,"adaptive-training-config.json")));
        Check(disk.engineeringGazeHighlight && disk.engineeringGazeRay,"Frozen archive config inherits flags");
        var participantObject=new GameObject("Isolated participant gaze lockout fixture");
        try
        {
            var denied=new AdaptiveTrainingConfig{enabled=true,sessionType="participant",engineeringGazeHighlight=true,engineeringGazeRay=true};
            var outline=participantObject.AddComponent<GazeDebugHighlighter>();outline.Bind(owner,denied);
            var ray=participantObject.AddComponent<EngineeringGazeRay>();ray.Bind(denied);ray.SetEnabled(true);
            Check(!outline.Allowed && !ray.Allowed && !ray.RayEnabled,"Participant lockout including live enable attempt");
            Check(participantObject.GetComponentsInChildren<LineRenderer>().Length==0,"Participant cannot allocate either overlay");
        }
        finally{UnityEngine.Object.DestroyImmediate(participantObject);}
        string report="PASS: "+checks+" live engineering assignment/metadata/render allocation/participant lockout checks;phase="+owner.Engine.Phase+
            ";tracking="+runtime.GazeTrackingStatus+";rayVisible="+runtime.GazeRay.Visible+";data="+owner.DataPath+
            ". No synthetic rays, scored trials or physical display acceptance.";
        File.WriteAllText(Folder+"/unity-live.txt",report);Debug.Log(report);return report;
    }
    public static string Abort()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(owner?.Engine?.Adaptive?.Config.participantCode!="ENGINEERING_GAZE_INHERITANCE")throw new Exception("Unexpected session");
        owner.Engine.Abort(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_gaze_inheritance_validation_complete");
        return "Isolated engineering session aborted; awaiting core archive.";
    }
}

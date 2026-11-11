using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using EvidenceVR;
using UnityEngine;

public static class EyeSampleFreshnessValidation
{
    [Serializable] sealed class Point {public double time;public long eyeSampleTime;public ulong flags;public int result;}
    [Serializable] sealed class Probe {public Point[] points;}
    static int checks;
    static void Check(bool result,string name){checks++;if(!result)throw new Exception(name);}
    public static string Run()
    {
        checks=0;var gate=new EyeSampleFreshness();
        Check(!gate.Update(0,true,123),"First cached pose cannot establish freshness");
        Check(!gate.Update(.1,true,123),"Repeated first pose stays waiting");
        Check(gate.Update(.11,true,124),"Advancing native timestamp establishes progress");
        Check(gate.Update(.2,true,124),"Short repeat within sampling tolerance");
        Check(!gate.Update(.37,true,124) && gate.Status=="EYE SAMPLE STALE","Cached tracked pose rejected after 250 ms");
        Check(!gate.Update(.4,false,0),"Invalid location cannot authorize eyes");
        Check(!gate.Update(.41,true,124),"Brief tracking loss cannot refresh the old timestamp");
        Check(gate.Update(.42,true,125),"New native timestamp can recover");
        Check(!gate.Update(.43,true,0),"Unavailable timestamp is not live evidence");
        Check(!gate.Update(.44,true,125),"Same cached pose cannot recover after unavailable timestamp");
        Check(gate.Update(.45,true,126),"Subsequent actual update recovers");
        Check(!gate.Update(.46,true,1),"Clock discontinuity requires new progress");
        Check(gate.Update(.47,true,2),"New clock epoch can recover");
        var type=typeof(QuestLinkEyeSpaceFeature);
        Check(Marshal.SizeOf(type.GetNestedType("EyeSampleTime",BindingFlags.NonPublic))==24,"Native sample time ABI");
        Check(Marshal.SizeOf(type.GetNestedType("SpaceLocation",BindingFlags.NonPublic))==56,"Native location ABI");
        int replayRows=0;
        foreach(string file in Directory.GetFiles("Evidence/EyeFreezeRecurrence-20261110","native-layers-*.json"))
        {
            var probe=Newtonsoft.Json.JsonConvert.DeserializeObject<Probe>(File.ReadAllText(file));
            var replay=new EyeSampleFreshness();
            Check(probe.points.Length>0,"Recorded input exists");
            foreach(var point in probe.points)
            {
                Check(!replay.Update(point.time,point.result==0 && (point.flags&15)==15,point.eyeSampleTime),"Recorded frozen input must never be fresh");
                replayRows++;
            }
        }
        string report="PASS: "+checks+" freshness/ABI/replay checks; "+replayRows+" recorded frozen samples rejected. Synthetic and recorded-data verification only; no worn-device recovery claim.";
        Directory.CreateDirectory("Evidence/EyeInputRecovery-20261110");
        File.WriteAllText("Evidence/EyeInputRecovery-20261110/freshness-validation-"+(Application.isPlaying?"play":"edit")+".txt",report);
        Debug.Log(report);return report;
    }
    public static string Settings()
    {
        var settings=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(UnityEditor.BuildTargetGroup.Standalone);
        var eye=settings.GetFeature<UnityEngine.XR.OpenXR.Features.Interactions.EyeGazeInteraction>();
        var compatibility=settings.GetFeature<QuestLinkEyeSpaceFeature>();
        string report="Eye profile="+(eye!=null && eye.enabled)+"; compatibility="+(compatibility!=null && compatibility.enabled)+"; retry="+(compatibility!=null && compatibility.retryWithNullPath)+"; preview="+UnityEditor.EditorUserSettings.GetConfigValue("EvidenceVR.QuestLinkPreview");
        File.WriteAllText("Evidence/EyeInputRecovery-20261110/settings.txt",report);
        return report;
    }
    public static string PrepareIsolatedPlay()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        var setup=UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scenes before this check.");
        UnityEditor.SessionState.SetString("EyeFreshness.SceneSetup",Newtonsoft.Json.JsonConvert.SerializeObject(setup));
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
        return "Empty test scene opened; no participant controller or assignment client.";
    }
    public static string RestoreScenes()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first.");
        var setup=Newtonsoft.Json.JsonConvert.DeserializeObject<UnityEditor.SceneManagement.SceneSetup[]>(UnityEditor.SessionState.GetString("EyeFreshness.SceneSetup",""));
        UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
        UnityEditor.SessionState.EraseString("EyeFreshness.SceneSetup");
        return "Original saved scene setup restored.";
    }
}

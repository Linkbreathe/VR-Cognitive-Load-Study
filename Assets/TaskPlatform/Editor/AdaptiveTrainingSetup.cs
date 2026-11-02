using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace EvidenceVR.Editor
{
    public static class AdaptiveTrainingSetup
    {
        [MenuItem("Evidence VR/Stage 2/Create Editor Engineering Configuration")]
        public static string EnableEditor()
        {
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            if(File.Exists(path))throw new InvalidOperationException("An adaptive override already exists. Edit it explicitly or disable it first.");
            var c=new AdaptiveTrainingConfig{enabled=true,sessionType="engineering",participantCode="engineering"};
            File.WriteAllText(path,JsonUtility.ToJson(c,true));Debug.Log("Stage 2 engineering override: "+path);return path;
        }
        [MenuItem("Evidence VR/Stage 2/Disable Editor Adaptive Mode")]
        public static string DisableEditor()
        {
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            if(File.Exists(path))File.Move(path,path+".disabled-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            return "Stage 1 will run on the next Play session.";
        }
        [MenuItem("Evidence VR/Stage 2/Engineering Gaze Highlight/Enable")]
        public static string EnableGazeHighlight()=>SetGazeHighlight(true);
        [MenuItem("Evidence VR/Stage 2/Engineering Gaze Highlight/Disable")]
        public static string DisableGazeHighlight()=>SetGazeHighlight(false);
        static string SetGazeHighlight(bool enabled)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before changing the frozen debug configuration.");
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            if(!File.Exists(path))throw new InvalidOperationException("Create an Editor engineering configuration first.");
            var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path));c.UpgradeLegacyPresentation();
            if(enabled && (!c.enabled || c.sessionType!="engineering"))throw new InvalidOperationException("Gaze highlighting requires an enabled engineering session. Participant sessions never highlight.");
            c.engineeringGazeHighlight=enabled;c.Validate();File.WriteAllText(path,JsonUtility.ToJson(c,true));
            return "Engineering gaze highlight "+(enabled?"enabled":"disabled")+" for the next Play session. Valid tracked eyes are required.";
        }
        [MenuItem("Evidence VR/Stage 2/Configure Build Support")]
        public static string Configure()
        {
            PlayerSettings.bundleVersion="2.0.0-stage2-dev7";
            PlayerSettings.Android.bundleVersionCode=Math.Max(19,PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.Android.forceInternetPermission=true;
            PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed;
            foreach(var group in new[]{BuildTargetGroup.Standalone,BuildTargetGroup.Android})
            {
                var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                var eye=settings?.GetFeature<EyeGazeInteraction>();
                if(eye!=null){eye.enabled=true;EditorUtility.SetDirty(settings);EditorUtility.SetDirty(eye);}
            }
            AssetDatabase.SaveAssets();
            return "Stage 2 dev build version, local HTTP bridge and optional OpenXR eye-gaze profile configured. Hardware capability is not implied.";
        }
        [MenuItem("Evidence VR/Stage 2/Build Quest Development Candidate")]
        public static string Build()
        {
            Configure();Directory.CreateDirectory("Builds/Quest");Directory.CreateDirectory("Evidence/Stage2");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{TaskPlatformBuilder.ScenePath},target=BuildTarget.Android,
                locationPathName="Builds/Quest/EvidenceVR-Stage2-dev7.apk",options=BuildOptions.None});
            string result="Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nBytes: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime;
            File.WriteAllText("Evidence/Stage2/android-build.txt",result);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception(result);
            return result;
        }
    }
}

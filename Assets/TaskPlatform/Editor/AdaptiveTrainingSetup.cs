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
        public static string EnablePersonalized()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before changing the protocol.");
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            var c=AdaptiveTrainingConfig.Personalized();
            string template=Path.Combine(Application.dataPath,"TaskPlatform/Config/adaptive-training-config.json");
            if(File.Exists(template))
            {
                var defaults=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(template));
                c.calibrationFormUrl=defaults.calibrationFormUrl;
                c.trainingFormUrl=defaults.trainingFormUrl;
            }
            if(File.Exists(path))
            {
                var old=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path));
                c.controllerUrl=old.controllerUrl;
                if(!string.IsNullOrEmpty(old.calibrationFormUrl))c.calibrationFormUrl=old.calibrationFormUrl;
                if(!string.IsNullOrEmpty(old.trainingFormUrl))c.trainingFormUrl=old.trainingFormUrl;
                File.Copy(path,path+".pre-dev11-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            }
            c.Validate();File.WriteAllText(path,JsonUtility.ToJson(c,true));
            return "Dev11 engineering protocol enabled. Personal model admission remains gated by the Python policy. "+path;
        }
        public static string EnableEditor()
        {
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            if(File.Exists(path))throw new InvalidOperationException("An adaptive override already exists. Edit it explicitly or disable it first.");
            var c=new AdaptiveTrainingConfig{enabled=true,sessionType="engineering",participantCode="engineering"};
            File.WriteAllText(path,JsonUtility.ToJson(c,true));Debug.Log("Stage 2 engineering override: "+path);return path;
        }
        public static string DisableEditor()
        {
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            if(File.Exists(path))File.Move(path,path+".disabled-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            return "Stage 1 will run on the next Play session.";
        }
        public static string EnableGazeHighlight()=>SetGazeHighlight(true);
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
        public static string EnableEyeRay()=>SetEyeRay(true);
        public static string DisableEyeRay()=>SetEyeRay(false);
        static string SetEyeRay(bool enabled)
        {
            if(EditorApplication.isPlaying)
            {
                var runtime=UnityEngine.Object.FindFirstObjectByType<AdaptiveRuntime>();
                if(runtime==null)throw new InvalidOperationException("Start a Stage 2 engineering session first.");
                runtime.SetEngineeringGazeRay(enabled);
                return "Live engineering eye ray "+(enabled?"enabled":"disabled")+". "+runtime.GazeTrackingStatus;
            }
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            if(!File.Exists(path))throw new InvalidOperationException("Create an Editor engineering configuration first.");
            var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path));c.UpgradeLegacyPresentation();
            if(!EngineeringGazeRay.IsAllowed(c))throw new InvalidOperationException("Eye-ray debugging requires an enabled engineering session.");
            c.engineeringGazeRay=enabled;c.Validate();File.WriteAllText(path,JsonUtility.ToJson(c,true));
            return "Engineering eye ray "+(enabled?"enabled":"disabled")+" for the next session.";
        }
        public static string Configure()
        {
            PlayerSettings.bundleVersion="2.0.0-stage2-dev12";
            PlayerSettings.Android.bundleVersionCode=Math.Max(24,PlayerSettings.Android.bundleVersionCode);
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
        public static string Build()
        {
            Configure();Directory.CreateDirectory("Builds/Quest");Directory.CreateDirectory("Evidence/Stage2");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{TaskPlatformBuilder.ScenePath},target=BuildTarget.Android,
                locationPathName="Builds/Quest/EvidenceVR-Stage2-dev12.apk",options=BuildOptions.None});
            string result="Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nBytes: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime;
            File.WriteAllText("Evidence/Stage2/android-build.txt",result);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception(result);
            return result;
        }
    }
}

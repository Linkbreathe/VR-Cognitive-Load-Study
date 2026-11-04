using System.IO;
using EvidenceVR;
using UnityEditor;
using UnityEngine;

public static class ShortCalibrationProgressBuild
{
    [System.Serializable] public sealed class Saved {public string version;public int code;}
    const string Backup="Evidence/ShortCalibrationProgress/player-settings-before.json";
    public static string Prepare()
    {
        Directory.CreateDirectory("Evidence/ShortCalibrationProgress");Directory.CreateDirectory("Builds/Stage3Dev2");
        if(PlayerSettings.bundleVersion!="3.0.0-stage3-dev2")
            File.WriteAllText(Backup,JsonUtility.ToJson(new Saved{version=PlayerSettings.bundleVersion,code=PlayerSettings.Android.bundleVersionCode},true));
        PlayerSettings.bundleVersion="3.0.0-stage3-dev2";PlayerSettings.Android.bundleVersionCode=26;
        foreach(string mode in new[]{"off","observe","enforce"})
        {
            var c=AdaptiveTrainingConfig.Personalized();c.evidenceGuardMode=mode;c.participantCode="stage3-engineering";
            var template=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText("Assets/TaskPlatform/Config/adaptive-training-config.json"));
            c.calibrationFormUrl=template.calibrationFormUrl;c.trainingFormUrl=template.trainingFormUrl;
            c.Validate();File.WriteAllText("Builds/Stage3Dev2/adaptive-training-config-"+mode+".json",JsonUtility.ToJson(c,true));
        }
        AssetDatabase.SaveAssets();
        return "Stage 3 dev2 / code 26; four calibration blocks; optional two-block supplement; completed-trial progress.";
    }
    public static string Restore()
    {
        var saved=JsonUtility.FromJson<Saved>(File.ReadAllText(Backup));
        PlayerSettings.bundleVersion=saved.version;PlayerSettings.Android.bundleVersionCode=saved.code;
        AssetDatabase.SaveAssets();return "Restored project PlayerSettings to "+saved.version+" / "+saved.code;
    }
}

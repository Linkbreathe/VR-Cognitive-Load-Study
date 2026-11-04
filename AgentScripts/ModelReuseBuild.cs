using System.IO;
using UnityEditor;
using UnityEngine;
using EvidenceVR;

public static class ModelReuseBuild
{
    [System.Serializable] sealed class Settings {public string version;public int code;}
    const string Backup="Evidence/ModelReuse/player-settings-before.json";
    public static string Prepare()
    {
        Directory.CreateDirectory("Evidence/ModelReuse");Directory.CreateDirectory("Builds/Stage3Dev6");
        if(PlayerSettings.bundleVersion!="3.0.0-stage3-dev6")
            File.WriteAllText(Backup,JsonUtility.ToJson(new Settings{version=PlayerSettings.bundleVersion,code=PlayerSettings.Android.bundleVersionCode},true));
        PlayerSettings.bundleVersion="3.0.0-stage3-dev6";PlayerSettings.Android.bundleVersionCode=30;
        var config=AdaptiveTrainingConfig.Personalized();config.operatorManaged=true;
        config.Validate();File.WriteAllText("Builds/Stage3Dev6/adaptive-training-config.json",JsonUtility.ToJson(config,true));
        AssetDatabase.SaveAssets();return "Operator test/model reuse candidate: 3.0.0-stage3-dev6 / code 30";
    }
    public static string Restore()
    {
        var saved=JsonUtility.FromJson<Settings>(File.ReadAllText(Backup));
        PlayerSettings.bundleVersion=saved.version;PlayerSettings.Android.bundleVersionCode=saved.code;
        AssetDatabase.SaveAssets();return "Restored PlayerSettings: "+saved.version+" / "+saved.code;
    }
}

using System.IO;
using EvidenceVR;
using UnityEditor;
using UnityEngine;

public static class EvidenceGuardBuild
{
    [System.Serializable] public sealed class Saved {public string version;public int code;}
    const string Backup="Evidence/Stage3/player-settings-before.json";
    public static string Prepare()
    {
        Directory.CreateDirectory("Evidence/Stage3");Directory.CreateDirectory("Builds/Stage3");
        if(PlayerSettings.bundleVersion!="3.0.0-stage3-dev1")
            File.WriteAllText(Backup,JsonUtility.ToJson(new Saved{version=PlayerSettings.bundleVersion,code=PlayerSettings.Android.bundleVersionCode},true));
        PlayerSettings.bundleVersion="3.0.0-stage3-dev1";PlayerSettings.Android.bundleVersionCode=25;
        foreach(string mode in new[]{"off","observe","enforce"})
        {
            var c=AdaptiveTrainingConfig.Personalized();c.evidenceGuardMode=mode;c.participantCode="stage3-engineering";
            c.Validate();File.WriteAllText("Builds/Stage3/adaptive-training-config-"+mode+".json",JsonUtility.ToJson(c,true));
        }
        AssetDatabase.SaveAssets();
        return "Stage 3 build configured: version 3.0.0-stage3-dev1, code 25; source default guard mode remains off.";
    }
    public static string Restore()
    {
        var saved=JsonUtility.FromJson<Saved>(File.ReadAllText(Backup));
        PlayerSettings.bundleVersion=saved.version;PlayerSettings.Android.bundleVersionCode=saved.code;
        AssetDatabase.SaveAssets();return "Restored project PlayerSettings to "+saved.version+" / "+saved.code;
    }
}

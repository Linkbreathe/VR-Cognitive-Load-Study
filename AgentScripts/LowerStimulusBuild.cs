using System.IO;
using UnityEditor;
using UnityEngine;

public static class LowerStimulusBuild
{
    [System.Serializable] public sealed class Saved {public string version;public int code;}
    const string Backup="Evidence/LowerStimulus/player-settings-before.json";
    public static string Prepare()
    {
        Directory.CreateDirectory("Builds/Stage3Dev4");
        if(PlayerSettings.bundleVersion!="3.0.0-stage3-dev4")
            File.WriteAllText(Backup,JsonUtility.ToJson(new Saved{version=PlayerSettings.bundleVersion,code=PlayerSettings.Android.bundleVersionCode},true));
        PlayerSettings.bundleVersion="3.0.0-stage3-dev4";PlayerSettings.Android.bundleVersionCode=28;
        AssetDatabase.SaveAssets();return "Lower stimulus candidate: 3.0.0-stage3-dev4 / code 28; protocol and guard unchanged.";
    }
    public static string Restore()
    {
        var saved=JsonUtility.FromJson<Saved>(File.ReadAllText(Backup));
        PlayerSettings.bundleVersion=saved.version;PlayerSettings.Android.bundleVersionCode=saved.code;
        AssetDatabase.SaveAssets();return "Restored project PlayerSettings: "+saved.version+" / "+saved.code;
    }
}

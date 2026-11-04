using System.IO;
using UnityEditor;
using UnityEngine;

public static class SignTypographyBuild
{
    [System.Serializable] public sealed class Saved {public string version;public int code;}
    const string Backup="Evidence/SignTypography/player-settings-before.json";
    public static string Prepare()
    {
        Directory.CreateDirectory("Builds/Stage3Dev5");
        if(PlayerSettings.bundleVersion!="3.0.0-stage3-dev5")
            File.WriteAllText(Backup,JsonUtility.ToJson(new Saved{version=PlayerSettings.bundleVersion,code=PlayerSettings.Android.bundleVersionCode},true));
        PlayerSettings.bundleVersion="3.0.0-stage3-dev5";PlayerSettings.Android.bundleVersionCode=29;
        AssetDatabase.SaveAssets();return "Sign typography candidate: 3.0.0-stage3-dev5 / code 29; protocol and guard unchanged.";
    }
    public static string Restore()
    {
        var saved=JsonUtility.FromJson<Saved>(File.ReadAllText(Backup));
        PlayerSettings.bundleVersion=saved.version;PlayerSettings.Android.bundleVersionCode=saved.code;
        AssetDatabase.SaveAssets();return "Restored project PlayerSettings: "+saved.version+" / "+saved.code;
    }
}

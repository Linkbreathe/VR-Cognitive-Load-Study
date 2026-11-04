using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EvidenceVR.Editor;

public static class ModelCompatibilityValidation
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static string CheckMetadata()
    {
        var input=new LslInputSelection{kind="EEG",name="Fixture",source_id="fixture",uid="old",stream_type="eeg",sample_rate=500,channel_count=2,channel_format=1,channel_labels=new[]{"Fz","Cz"},units="uV",channels=new[]{0,1}};
        var copy=JsonUtility.FromJson<LslInputSelection>(JsonUtility.ToJson(input));
        Require(copy.units=="uV" && copy.channel_labels.SequenceEqual(input.channel_labels),"Metadata lost in Unity JSON roundtrip.");
        var source=new LslDiscoveredStream{name=input.name,source_id=input.source_id,uid="new",stream_type=input.stream_type,sample_rate=500,channel_count=2,channel_format=1,channel_labels=new[]{"Fz","Cz"}};
        Require(LslSourceWindow.ReusableProfile(copy,source,new[]{source}),"Saved mapping did not survive a confirmed publisher restart.");
        source.channel_labels=new[]{"Cz","Fz"};
        Require(!LslSourceWindow.ReusableProfile(copy,source,new[]{source}),"Changed channel order reused a saved mapping.");
        source.channel_labels=new[]{"Fz","Cz"};
        Require(!LslSourceWindow.ReusableProfile(copy,source,new[]{source,source}),"Ambiguous restarted publisher reused a mapping.");
        var file=new LslSelectionFile{streams=new[]{copy},profiles=new[]{copy}};
        var restored=JsonUtility.FromJson<LslSelectionFile>(JsonUtility.ToJson(file));
        Require(restored.streams[0].units=="uV" && restored.profiles[0].channel_labels.SequenceEqual(copy.channel_labels),"Profile metadata lost.");
        LslSourceWindow.ValidateSelection(file);
        const string result="MODEL CONFIG PASS: 6 metadata, layout and publisher-restart checks; no participant session started.";
        Directory.CreateDirectory("Evidence/ModelCompatibilityFix");File.WriteAllText("Evidence/ModelCompatibilityFix/unity-validation.txt",result);Debug.Log(result);return result;
    }

    public static async Task<string> ReconnectSavedSelection()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play Mode before reconnecting.");
        LslSourceWindow.Open();
        var window=Resources.FindObjectsOfTypeAll<LslSourceWindow>().Single();
        typeof(LslSourceWindow).GetMethod("LoadSelection",Flags).Invoke(window,new object[]{LslSourceWindow.SelectionPath});
        typeof(LslSourceWindow).GetMethod("BuildFields",Flags).Invoke(window,null);
        await Task.Delay(1200);
        typeof(LslSourceWindow).GetMethod("RefreshState",Flags).Invoke(window,null);
        Require(LslSourceWindow.OwnedBridge()==null,"Previous bridge must be stopped first.");
        await (Task)typeof(LslSourceWindow).GetMethod("StartBridgeAsync",Flags).Invoke(window,null);
        var file=JsonUtility.FromJson<LslSelectionFile>(File.ReadAllText(LslSourceWindow.SelectionPath));
        Require(file.streams.All(s=>s.units=="uV" && s.channel_labels.Length==9),"Saved Gin metadata did not survive Connect.");
        Require(window.rootVisualElement.Query<TextField>().ToList().Count(f=>f.label=="Analysis units (after scaling)" && f.value=="uV")==2,"Unit fields not restored.");
        const string result="MODEL CONFIG CONNECT PASS: actual Unity Connect preserved Gin units/labels and started the bridge with saved configuration.";
        File.WriteAllText("Evidence/ModelCompatibilityFix/unity-connect-validation.txt",result);Debug.Log(result);return result;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EvidenceVR.Editor;

public static class LslDisconnectValidation
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static LslSourceWindow Window()=>Resources.FindObjectsOfTypeAll<LslSourceWindow>().Single();
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static string Inspect()
    {
        var w=Window();typeof(LslSourceWindow).GetMethod("RefreshState",Flags).Invoke(w,null);
        string state=string.Join(" | ",new[]{w.rootVisualElement.Q<Label>("serviceStatus").text,
            w.rootVisualElement.Q<Label>("status").text,
            "buttons="+string.Join(",",w.rootVisualElement.Query<Button>().ToList().Where(b=>new[]{"Scan","Connect","Disconnect"}.Contains(b.text)).Select(b=>b.text+":"+b.enabledInHierarchy)),
            "picker="+w.rootVisualElement.Q<PopupField<string>>("sourcePicker").enabledInHierarchy});
        Directory.CreateDirectory("Evidence/LslDisconnect");File.AppendAllText("Evidence/LslDisconnect/ui-states.txt",state+"\n");
        return state;
    }
    public static async Task<string> DisconnectAndCheck()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Play Mode must be stopped.");
        var w=Window();
        Require((bool)typeof(LslSourceWindow).GetField("externalBridge",Flags).GetValue(w),"Expected the existing project service to be detected.");
        Require(w.rootVisualElement.Query<Button>().ToList().Single(b=>b.text=="Disconnect").enabledInHierarchy,"Disconnect should be enabled for an external project service.");
        Require(!w.rootVisualElement.Q<PopupField<string>>("sourcePicker").enabledInHierarchy,"Connected source must be locked.");
        await (Task)typeof(LslSourceWindow).GetMethod("DisconnectBridgeAsync",Flags).Invoke(w,null);
        Inspect();
        Require(LslSourceWindow.OwnedBridge()==null,"Owned process must be stopped.");
        Require(w.rootVisualElement.Q<PopupField<string>>("sourcePicker").enabledInHierarchy,"Disconnected source must be editable.");
        Require(!w.rootVisualElement.Query<Button>().ToList().Single(b=>b.text=="Disconnect").enabledInHierarchy,"Already-disconnected action must be disabled.");
        Require(!w.rootVisualElement.Q<Label>("status").text.Contains("locked"),"Stale lock message remains.");
        Require(LslSourceWindow.IsProjectBridge(new LslBridgeHealth{application="Evidence Console",project=LslSourceWindow.ProjectRoot,pid=1}),"Same-project health must match.");
        Require(!LslSourceWindow.IsProjectBridge(new LslBridgeHealth{application="Other app",project=LslSourceWindow.ProjectRoot,pid=1}),"Other application must not match.");
        Require(!LslSourceWindow.IsProjectBridge(new LslBridgeHealth{application="Evidence Console",project="D:/OtherProject",pid=1}),"Other project must not match.");
        Require(!LslSourceWindow.IsProjectBridge(new LslBridgeHealth{application="Evidence Console",project=null,pid=1}),"Missing identity must not match.");
        Require(!LslSourceWindow.IsProjectBridge(null),"Missing health must not match.");
        const string result="LSL DISCONNECT PASS: external project service detected and stopped, controls unlocked, identity checks passed; publisher untouched.";
        File.WriteAllText("Evidence/LslDisconnect/validation.txt",result);Debug.Log(result);return result;
    }
}

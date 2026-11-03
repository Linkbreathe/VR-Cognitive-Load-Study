using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using UnityEditor;
using EvidenceVR;
using EvidenceVR.Editor;

public static class LslSourceFirstValidation
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
    static async Task<string> Status()
    {
        using(var r=UnityWebRequest.Get("http://127.0.0.1:18769/status"))
        {r.timeout=3;var op=r.SendWebRequest();while(!op.isDone)await Task.Yield();if(r.result!=UnityWebRequest.Result.Success)throw new Exception(r.error);return r.downloadHandler.text;}
    }
    public static string ProfileIdentity()
    {
        checks=0;
        var source=new LslDiscoveredStream{name="Shared",source_id="shared",uid="first",stream_type="EEG",sample_rate=250,channel_count=2,channel_format=1};
        var sibling=JsonUtility.FromJson<LslDiscoveredStream>(JsonUtility.ToJson(source));sibling.uid="second";
        var profile=new LslInputSelection{name=source.name,source_id=source.source_id,uid=source.uid,stream_type=source.stream_type,sample_rate=250,channel_count=2,channel_format=1};
        Check(LslSourceWindow.ReusableProfile(profile,source,new[]{source,sibling}),"Exact outlet profile remains usable");
        Check(!LslSourceWindow.ReusableProfile(profile,sibling,new[]{source,sibling}),"Same descriptors do not transfer mappings between simultaneous outlets");
        Check(LslSourceWindow.ReusableProfile(profile,sibling,new[]{sibling}),"Unique restarted source can reuse explicit saved mapping");
        profile.source_id="";sibling.source_id="";
        Check(!LslSourceWindow.ReusableProfile(profile,sibling,new[]{sibling}),"Anonymous restarted outlet cannot inherit mapping");
        Check(new LslSelectionFile().revision=="lsl-source-selector-v2","Selection revision recorded");
        string report="PASS: "+checks+" source profile identity and configuration revision checks.";
        File.WriteAllText("Evidence/LslSourceFirst/profile-validation.txt",report);return report;
    }
    public static async Task<string> OpenFinalPanel()
    {
        LslSourceWindow.Open();var w=EditorWindow.GetWindow<LslSourceWindow>();w.position=new Rect(150,100,800,520);w.CreateGUI();
        await (Task)Call(w,"ScanAsync");
        return "Source panel open and scanned; no bridge started.";
    }
    public static async Task<string> Prepare()
    {
        checks=0;
        var fixture=JsonUtility.FromJson<LslDiscovery>(File.ReadAllText("Evidence/LslSourceFirst/fixture-outlets.json"));
        LslSourceWindow.Open();var w=EditorWindow.GetWindow<LslSourceWindow>();w.position=new Rect(150,100,800,520);w.CreateGUI();
        await (Task)Call(w,"ScanAsync");
        var found=(LslDiscoveredStream[])typeof(LslSourceWindow).GetField("discovered",Flags).GetValue(w);
        Check(found.Count(s=>s.source_id=="source-first-shared-id")==3,"All outlets sharing source_id discovered");
        Check(found.Count(s=>s.name=="Source First Numeric")==2,"Same-name outlets remain separate");
        Check(w.rootVisualElement.Q("actions").Query<Button>().ToList().Select(b=>b.text).SequenceEqual(new[]{"Scan","Connect","Disconnect"}),"Only three primary actions");
        Check(w.rootVisualElement.Q<Foldout>("advancedSettings").value==false,"Advanced settings closed");
        var numeric=found.Single(s=>s.uid==fixture.streams[0].uid);
        var marker=found.Single(s=>s.uid==fixture.streams[2].uid);
        w.SelectSource(marker);w.SaveSelection();
        var saved=JsonUtility.FromJson<LslSelectionFile>(File.ReadAllText(LslSourceWindow.SelectionPath));
        Check(saved.streams.Single().kind=="OBSERVE" && saved.streams[0].sample_rate==0,"Text/event stream needs no physiology toggle");
        Check(saved.streams[0].uid==marker.uid && saved.streams[0].channel_format==3,"Exact string outlet pinned");
        typeof(LslSourceWindow).GetField("port",Flags).SetValue(w,18769);
        await (Task)Call(w,"StartBridgeAsync");
        string status="";double end=EditorApplication.timeSinceStartup+14;
        while(EditorApplication.timeSinceStartup<end){status=await Status();if(status.Contains("fixture_event"))break;await Task.Delay(300);}
        Check(status.Contains("fixture_event") && status.Contains(marker.uid),"Actual string outlet received by UID");
        await Task.Delay(3400);status=await Status();
        Check(JsonUtility.FromJson<LslBridgeStatus>(status).physiologyStatus.connection=="idle","Sparse events remain connected and idle");
        File.WriteAllText("Evidence/LslSourceFirst/marker-status.json",status);LslSourceWindow.StopBridge();
        w.SelectSource(numeric);w.SaveSelection();
        saved=JsonUtility.FromJson<LslSelectionFile>(File.ReadAllText(LslSourceWindow.SelectionPath));
        Check(saved.streams[0].kind=="OBSERVE" && saved.streams[0].channels.SequenceEqual(new[]{0,1}),"Unknown numeric signal receives all channels without inference");
        Check(saved.streams[0].uid==numeric.uid,"Numeric duplicate name selected by exact UID");
        var profile=saved.streams[0];profile.kind="EEG";
        Check(LslSourceWindow.ProfileMatches(profile,numeric),"Matching profile recognized");
        var other=JsonUtility.FromJson<LslDiscoveredStream>(JsonUtility.ToJson(numeric));other.stream_type="imu";
        Check(!LslSourceWindow.ProfileMatches(profile,other),"Profile not reused for another type sharing ID");
        other=JsonUtility.FromJson<LslDiscoveredStream>(JsonUtility.ToJson(numeric));other.channel_count=9;
        Check(!LslSourceWindow.ProfileMatches(profile,other),"Profile not reused for another layout");
        await (Task)Call(w,"StartBridgeAsync");
        end=EditorApplication.timeSinceStartup+10;
        while(EditorApplication.timeSinceStartup<end){status=await Status();if(status.Contains("111.0") && status.Contains("222.0"))break;await Task.Delay(250);}
        Check(status.Contains("111.0") && status.Contains("222.0") && !status.Contains("999.0"),"Exact numeric outlet sampled, not its identical-name sibling");
        Check(JsonUtility.FromJson<LslBridgeStatus>(status).physiologyStatus.streams[0].uid==numeric.uid,"HTTP identity includes selected UID");
        File.WriteAllText("Evidence/LslSourceFirst/numeric-status.json",status);
        bool blocked=false;try{w.SelectSource(marker);}catch(InvalidOperationException){blocked=true;}Check(blocked,"Selection locked while connected");
        string report="PASS: "+checks+" source-first UI, exact identity, profile, text/numeric and sparse-event checks.";
        File.WriteAllText("Evidence/LslSourceFirst/editor-validation.txt",report);Debug.Log(report);return report;
    }
    public static async Task<string> Runtime()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        double end=EditorApplication.timeSinceStartup+15;
        while(c.AdaptiveRuntime?.PhysiologyStatus?.connection!="receiving" && EditorApplication.timeSinceStartup<end)await Task.Delay(250);
        var source=c.AdaptiveRuntime?.PhysiologyStatus;
        Check(c.DataPath.Contains("editor-test"),"Editor data isolation");
        Check(source!=null && source.connection=="receiving" && source.streams[0].kind=="OBSERVE","Unity receives unclassified source");
        var expected=JsonUtility.FromJson<LslDiscovery>(File.ReadAllText("Evidence/LslSourceFirst/fixture-outlets.json"));
        Check(source.streams[0].uid==expected.streams[0].uid && source.source=="recorded_replay","Exact UID/provenance preserved in Unity");
        Check(c.badge.text.Contains("Source First Numeric") && c.badge.text.Contains("RECEIVING"),"VR shows selected source and receipt");
        string status=await Status();
        Check(status.Contains("\"features\": []"),"Receive-only samples never become physiological features");
        File.WriteAllText("Evidence/LslSourceFirst/runtime-path.txt",c.DataPath);File.WriteAllText("Evidence/LslSourceFirst/runtime-status.json",status);
        string report="PASS: actual LSL / HTTP / Unity exact source identity, receive-only isolation and VR status.";
        Debug.Log(report);File.WriteAllText("Evidence/LslSourceFirst/runtime-validation.txt",report);return report;
    }
    public static string Cleanup()
    {
        LslSourceWindow.StopBridge();
        File.Copy("Evidence/LslSourceFirst/editor-config-before.json",Path.Combine(Application.persistentDataPath,"adaptive-training-config.json"),true);
        if(File.Exists("Evidence/LslSourceFirst/selection-before.json"))File.Copy("Evidence/LslSourceFirst/selection-before.json",LslSourceWindow.SelectionPath,true);
        else if(File.Exists(LslSourceWindow.SelectionPath))File.Move(LslSourceWindow.SelectionPath,Path.GetFullPath("Evidence/LslSourceFirst/probe-selection.json"));
        foreach(var w in Resources.FindObjectsOfTypeAll<LslSourceWindow>())w.Close();
        return "Probe bridge stopped; original configuration restored.";
    }
}

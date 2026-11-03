using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using EvidenceVR.Editor;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

public static class LslSourceValidation
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Flags).Invoke(target,args);
    public static async Task<string> Prepare()
    {
        checks=0;
        var status=new PhysiologyDisplayStatus{source="recorded_replay",connection="receiving",sourceName="My amplifier",streams=new[]{new PhysiologySourceIdentity{name="My amplifier",sourceId="id-a",kind="EEG",origin="recorded_xdf_replay"}}};
        string compact=ExperimentStatusView.CompactStatus("ACTIVE","behavioral_fallback",status,true);
        Check(compact.Contains("My amplifier") && compact.Contains("RECEIVING") && !compact.Contains("REPLAY"),"Source name replaces replay banner");
        Check(ExperimentStatusView.PhysiologyText(status,true).Contains("recorded_xdf_replay"),"Provenance retained in events");
        foreach(string state in new[]{"waiting","partial","disconnected"})
        {status.connection=state;Check(!ExperimentStatusView.CompactStatus("OFF","behavioral_fallback",status,true).Contains("RECEIVING"),"No false receiving for "+state);}
        Check(ExperimentStatusView.CompactStatus("ACTIVE","behavioral_fallback",status,false).Contains("SERVICE OFFLINE"),"Disconnected service remains explicit");
        Check(ExperimentStatusView.CompactStatus("SHADOW","physiological",status,true).Contains("AUTO OFF"),"Shadow remains advice-only");
        Check(ExperimentStatusView.SourceLabel(new PhysiologyDisplayStatus{source="recorded_replay"})=="SOURCE UNKNOWN","Legacy service does not invent source");
        Check(ExperimentStatusView.SourceLabel(new PhysiologyDisplayStatus{sourceName=new string('a',80)}).Length==44,"Long source bounded");
        Check(!ExperimentStatusView.SourceLabel(new PhysiologyDisplayStatus{sourceName="a\nb"}).Contains("\n"),"Control characters normalized");
        var input=new LslInputSelection{source_id="a",channels=new[]{0},data_origin="recorded_xdf_replay"};
        LslSourceWindow.ValidateSelection(new LslSelectionFile{streams=new[]{input}});Check(input.synthetic,"Replay provenance cannot be lost");
        foreach(int[] channels in new[]{Array.Empty<int>(),new[]{-1},new[]{0,0}})
        {input.channels=channels;bool rejected=false;try{LslSourceWindow.ValidateSelection(new LslSelectionFile{streams=new[]{input}});}catch(InvalidOperationException){rejected=true;}Check(rejected,"Reject invalid mapping");}
        var args=new[]{"path with spaces", "embedded\"quote", "trailing\\", "slash\\\"quote", "$() & ` literal"};
        var process=new System.Diagnostics.Process{StartInfo=new System.Diagnostics.ProcessStartInfo{FileName=Path.Combine(LslSourceWindow.ProjectRoot,"Tools/AdaptiveTraining/.venv/Scripts/python.exe"),Arguments="-c "+LslSourceWindow.QuoteArgument("import sys; assert sys.argv[1:] == "+"[\"path with spaces\", 'embedded\\\"quote', 'trailing\\\\', 'slash\\\\\\\"quote', '$() & ` literal']")+" "+string.Join(" ",args.Select(LslSourceWindow.QuoteArgument)),UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true}};
        process.Start();await Task.Run(()=>process.WaitForExit());Check(process.ExitCode==0,"Arguments round-trip: "+process.StandardError.ReadToEnd());process.Dispose();
        var window=EditorWindow.GetWindow<LslSourceWindow>();window.position=new Rect(150,100,800,850);window.CreateGUI();
        Check(window.rootVisualElement.Q<Button>()!=null,"Panel imported and controls exist");
        await (Task)Call(window,"ScanAsync");
        var found=(LslDiscoveredStream[])typeof(LslSourceWindow).GetField("discovered",Flags).GetValue(window);
        Check(found.Any(s=>s.source_id=="source-panel-eeg-fixture" && s.recorded),"Real LSL discovery reads replay provenance");
        Check(found.Any(s=>s.source_id=="source-panel-features-fixture" && s.channel_labels.Contains("fixture_heart_rate")),"Feature outlet and labels discovered");
        Call(window,"LoadSelection",Path.GetFullPath("Evidence/LslSourceSelection/fixture-selection.json"));Call(window,"BuildFields");
        typeof(LslSourceWindow).GetField("port",Flags).SetValue(window,18769);
        await (Task)Call(window,"StartBridgeAsync");
        Check(LslSourceWindow.OwnedBridge()!=null,"Panel owns its bridge");
        bool locked=false;try{window.SaveSelection();}catch(InvalidOperationException){locked=true;}Check(locked,"Cannot switch source while owned bridge runs");
        string report="PASS: "+checks+" source label, provenance, mapping, argument, panel, real LSL discovery and bridge lifecycle checks.";
        File.WriteAllText("Evidence/LslSourceSelection/editor-validation.txt",report);Debug.Log(report);return report;
    }
    public static async Task<string> Runtime()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(owner==null)throw new Exception("Missing task controller");
        double until=EditorApplication.timeSinceStartup+20;
        while((owner.AdaptiveRuntime?.PhysiologyStatus?.connection!="receiving") && EditorApplication.timeSinceStartup<until)await Task.Delay(250);
        var runtime=owner.AdaptiveRuntime;
        Check(runtime!=null && runtime.ServiceConnected,"Actual HTTP bridge replies");
        var source=runtime.PhysiologyStatus;
        Check(source.connection=="receiving" && source.sourceName.Contains("Source Panel EEG Fixture") && source.sourceName.Contains("Source Panel Features Fixture"),"Both selected LSL outlets are received");
        Check(source.source=="recorded_replay" && source.streams.Length==2,"Replay provenance and IDs survive HTTP");
        owner.StatusView.Refresh();
        var hud=(TMP_Text)typeof(ExperimentStatusView).GetField("hud",Flags).GetValue(owner.StatusView);
        var eyes=(TMP_Text)typeof(ExperimentStatusView).GetField("eyeHud",Flags).GetValue(owner.StatusView);
        Check(hud.text.Contains("LSL:") && hud.text.Contains("RECEIVING"),"Source HUD stays visible with eye debug");
        Check(eyes.enabled && eyes.text.Contains("ENGINEERING EYE RAY"),"Independent eye status line");
        Check(!hud.richText && !owner.badge.richText,"Stream names cannot inject TMP markup");
        Check(owner.DataPath.Contains("editor-test"),"Engineering session isolation");
        File.WriteAllText("Evidence/LslSourceSelection/runtime-path.txt",owner.DataPath);
        File.WriteAllText("Evidence/LslSourceSelection/runtime-status.json",JsonUtility.ToJson(source,true));
        string report="PASS: real LSL -> Python -> HTTP -> Unity source identity and receipt; separate eye/source labels; editor-test data only.";
        Debug.Log(report+" "+hud.text);File.WriteAllText("Evidence/LslSourceSelection/runtime-validation.txt",report);return report;
    }
    public static async Task<string> ShowRunning()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(owner==null || !owner.DataPath.Contains("editor-test"))throw new Exception("Editor-only probe required");
        owner.Engine.Note("engineering_probe","programmatic practice start; no participant responses",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        owner.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        owner.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        await Task.Delay(250);
        return "Engineering practice started for source/eye HUD layout capture only.";
    }
    public static string Cleanup()
    {
        LslSourceWindow.StopBridge();
        File.Copy("Evidence/LslSourceSelection/editor-config-before.json",Path.Combine(Application.persistentDataPath,"adaptive-training-config.json"),true);
        if(File.Exists("Evidence/LslSourceSelection/selection-before.json"))File.Copy("Evidence/LslSourceSelection/selection-before.json",LslSourceWindow.SelectionPath,true);
        else if(File.Exists(LslSourceWindow.SelectionPath))File.Move(LslSourceWindow.SelectionPath,Path.GetFullPath("Evidence/LslSourceSelection/selection-used-in-probe.json"));
        foreach(var window in Resources.FindObjectsOfTypeAll<LslSourceWindow>())window.Close();
        return "Owned bridge stopped and original Editor/source configurations restored.";
    }
    public static async Task<string> OpenFinalPanel()
    {
        if(LslSourceWindow.OwnedBridge()!=null)throw new Exception("Unexpected owned bridge left running");
        LslSourceWindow.Open();
        var window=EditorWindow.GetWindow<LslSourceWindow>();window.position=new Rect(150,100,800,850);
        await (Task)Call(window,"ScanAsync");
        return "Final panel open with current LSL inventory. No source selected or service started by this check.";
    }
}

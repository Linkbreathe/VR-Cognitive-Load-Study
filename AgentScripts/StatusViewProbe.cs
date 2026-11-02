using System;
using System.IO;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;

public static class StatusViewProbe
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static async Task<string> Preparation(string file)
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        await Task.Delay(500);c.StatusView.Refresh();
        Check(c.Engine.Phase==TaskPhase.Preparation,"Preparation remains idle");
        Check(c.StatusView.DisplayText.Contains(c.Engine.Adaptive==null?"STAGE 1":"STAGE 2"),"Mode comes from the running engine");
        c.badge.ForceMeshUpdate();Check(!c.badge.isTextOverflowing,"Preparation status fits its bounds");
        Check(File.ReadAllText(Path.Combine(c.DataPath,"session.json")).Contains(ExperimentStatusView.Revision),"Visual revision saved in metadata");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/Stage2Status/"+file+".png"));await Task.Delay(150);
        return c.StatusView.DisplayText+"\nSession: "+c.DataPath;
    }
    public static async Task<string> Practice(string file)
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        double now=Time.realtimeSinceStartupAsDouble;
        c.Engine.Note("engineering_status_probe","Programmatic navigation for UI verification; not participant data",now,Time.frameCount);
        c.Engine.Advance(now,Time.frameCount);c.Engine.Advance(now,Time.frameCount);
        await Task.Delay(250);
        var hud=c.spatialView.taskCanvas.transform.Find("ExperimentModeStatus").GetComponent<TMPro.TMP_Text>();
        hud.ForceMeshUpdate();Check(!hud.isTextOverflowing,"Running status fits its bounds");
        Check(c.spatialView.taskCanvas.enabled && c.Engine.Practice,"Status visible during actual practice");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/Stage2Status/"+file+".png"));await Task.Delay(150);
        c.Engine.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_status_probe");
        await Task.Delay(150);Check(c.badge.text.Contains("Paused"),"Paused page identifies its phase");
        return "PASS: running/paused status and metadata; "+c.StatusView.DisplayText;
    }
    public static string ConnectedReplay()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(c.AdaptiveRuntime.ServiceConnected,"Actual bridge responds");
        Check(c.AdaptiveRuntime.PhysiologyStatus.source=="recorded_replay","Actual bridge identifies recorded source");
        Check(c.AdaptiveRuntime.PhysiologyStatus.connection=="receiving","Actual LSL samples are current");
        c.StatusView.Refresh();
        Check(c.StatusView.DisplayText.Contains("RECORDED REPLAY") && c.StatusView.DisplayText.Contains("Online"),"Replay and connectivity visible");
        File.WriteAllText("Evidence/Stage2Status/replay-connected.txt",c.StatusView.DisplayText+"\n"+c.DataPath);
        return c.StatusView.DisplayText;
    }
    public static string DisconnectedReplay()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(!c.AdaptiveRuntime.ServiceConnected,"No service response in three seconds");c.StatusView.Refresh();
        Check(c.StatusView.DisplayText.Contains("SERVICE OFFLINE") && c.StatusView.DisplayText.Contains("Last source: RECORDED REPLAY"),"Stale source is explicitly historical");
        File.WriteAllText("Evidence/Stage2Status/replay-disconnected.txt",c.StatusView.DisplayText);
        return c.StatusView.DisplayText;
    }
    public static async Task<string> ReceivingScreenshot()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        c.Engine.Resume(Time.realtimeSinceStartupAsDouble,Time.frameCount);await Task.Delay(250);
        Check(c.StatusView.DisplayText.Contains("Receiving"),"Current sample receipt displayed");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/Stage2Status/active-replay-receiving.png"));await Task.Delay(150);
        c.Engine.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_status_probe");
        return c.StatusView.DisplayText;
    }
    public static string Labels()
    {
        Check(ExperimentStatusView.ControllerText("SHADOW","behavioral_fallback",true).Contains("Auto OFF (advice only)"),"Shadow never implies execution");
        Check(ExperimentStatusView.ControllerText("ACTIVE","physiological",false).Contains("Offline / HOLD"),"Active configuration does not imply service availability");
        Check(ExperimentStatusView.PhysiologyText(null,true).Contains("UNKNOWN"),"Older bridge without provenance cannot claim live input");
        Check(ExperimentStatusView.PhysiologyText(new PhysiologyDisplayStatus{source="live",connection="receiving"},true).Contains("not validated"),"Live transport is not physiological validation");
        Check(ExperimentStatusView.PhysiologyText(new PhysiologyDisplayStatus{source="mixed",connection="partial"},true).Contains("includes simulation"),"Mixed sources retain simulation warning");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        c.badge.text="STAGE 2 - ADAPTIVE TRAINING | Preparation\n"+ExperimentStatusView.ControllerText("SHADOW","physiological",true)+"\n"+
            ExperimentStatusView.PhysiologyText(new PhysiologyDisplayStatus{source="mixed",connection="partial"},true);
        c.badge.ForceMeshUpdate();Check(!c.badge.isTextOverflowing,"Longest source/control combination fits preparation label");
        c.StatusView.Refresh();
        File.WriteAllText("Evidence/Stage2Status/label-checks.txt","PASS: SHADOW, ACTIVE offline, unknown bridge, live-unvalidated, mixed-simulation and longest-label layout checks. Synthetic label inputs only.");
        return "PASS: six label provenance and layout checks.";
    }
}

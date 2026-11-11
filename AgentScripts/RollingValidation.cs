using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;

public static class RollingValidation
{
    const string Folder="Evidence/RollingAdaptation";
    static int checks;
    static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
    static void Trial(TaskEngine e,ref double now,ref int frame)
    {
        if(e.Phase==TaskPhase.Preparation||e.Phase==TaskPhase.Instructions)e.Advance(now,frame++);
        if(e.Phase!=TaskPhase.Running)return;
        double start=e.CurrentResult.plannedOnset;
        if(e.Current.scored)e.Respond(e.Current.expectedMatch,start+.5,frame++);
        e.Tick(start+4,frame++);now=start+4.5;e.Tick(now,frame++);
    }
    public static string Model()
    {
        checks=0;Directory.CreateDirectory(Folder);
        Check(SpatialTaskView.FormatCountdown(4)=="4","integer onset");
        Check(SpatialTaskView.FormatCountdown(3.126)=="4","ceiling seconds");
        Check(SpatialTaskView.FormatCountdown(-1)=="0","clamped zero");
        var restore="Evidence/RestorePoints/rolling-window-20261102/editor-config.json";
        var migration=File.Exists(restore)?JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(restore)):new AdaptiveTrainingConfig{version="2.0.0-stage2-dev4",mode="OFF",seed=20261102,enabled=true};
        string originalMode=migration.mode;int originalSeed=migration.seed;bool originalEnabled=migration.enabled;string originalUrl=migration.controllerUrl;
        Check(migration.UpgradeLegacyPresentation(),"Actual saved config migrates");migration.Validate();
        Check(migration.mode==originalMode && migration.seed==originalSeed && migration.enabled==originalEnabled && migration.controllerUrl==originalUrl,"Actual user choices preserved");
        foreach(string mode in new[]{"OFF","SHADOW","ACTIVE"})
        {
            var e=new TaskEngine(new TaskConfig(),new AdaptiveTrainingConfig{mode=mode,includeTransferTests=false,seed=0});
            double now=0;int frame=0;bool allow=false;var windows=new List<RollingEvaluation>();int deferred=0,changes=0;
            e.CanApplyAdaptiveChange=()=>allow;
            e.OnEvent+=ev=>
            {
                if(ev.type=="rolling_evaluation")
                {
                    var r=e.LatestRolling;windows.Add(r);
                    if(mode=="ACTIVE")e.Adaptive.Pending=new AdaptiveDecision{action="UP",reason="SYNTHETIC_GATE_FIXTURE",level=r.level,block=r.block,epoch=r.epoch,evaluationId=r.evaluationId,lastTrial=r.lastTrial};
                }
                if(ev.type=="rolling_execution_deferred")deferred++;
                if(ev.type=="adaptive_level_changed")changes++;
            };
            while(windows.Count<8 && !e.Adaptive.Finished)Trial(e,ref now,ref frame);
            Check(windows.Count>=8,"Repeated rolling windows");
            for(int i=1;i<windows.Count;i++)
            {
                Check(windows[i].lastTrial-windows[i-1].lastTrial==2,"Stride two");
                Check(Math.Abs(windows[i].end-windows[i-1].end-9)<1e-7,"Nine-second cadence");
                Check(windows[i].lastTrial-windows[i].firstTrial==9,"Ten contiguous trials");
            }
            Check(windows.Select(w=>w.block).Distinct().Count()>1,"Rolling windows cross 108-second boundary");
            Check(e.Adaptive.Level==1 && changes==0,"No changes while media gate closed / auto off");
            if(mode=="ACTIVE")
            {
                Check(deferred>0,"Media deferral logged");allow=true;Trial(e,ref now,ref frame);
                Check(e.Adaptive.Level==2 && e.RollingHistory.Count==0,"Safe next-trial execution and reset");
                Check(e.CurrentResult.transition,"Transition trial retained");
                e.Adaptive.Pending=new AdaptiveDecision{action="UP",level=2};e.Adaptive.ApplyRollingDecision();
                Check(e.Adaptive.TrainingEndRequested,"Candidate end queued");
                int epoch=e.RollingEpoch;e.Pause(now+.2,frame++);e.Resume(now+1,frame++);
                Check(e.RollingEpoch>epoch && e.Adaptive.Pending==null && !e.Current.scored,"Pause resets and rewarms");
                Check(!e.Adaptive.TrainingEndRequested,"Pause revokes uncompleted attainment");
            }
            foreach(var b in e.Adaptive.Blocks)Check(Math.Abs(b.end-b.start-108)<1e-7,"Statistics remain 108 seconds");
        }
        string report="PASS: "+checks+" focused rolling/countdown checks: 10/2 windows, cross-statistics history, media deferral, OFF/SHADOW isolation, reset/rewarm and 108-second summaries.";
        File.WriteAllText(Folder+"/focused-model.txt",report);Debug.Log(report);return report;
    }
    public static string Setup()
    {
        Check(!Application.isPlaying,"Stop before setup");Directory.CreateDirectory(Folder);
        string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
        File.Copy(path,Folder+"/editor-config-before-live.json",true);
        var config=new AdaptiveTrainingConfig{enabled=true,mode="ACTIVE",includeTransferTests=false,seed=0,controllerUrl="http://127.0.0.1:18765",participantCode="synthetic_rolling_live",sessionType="engineering"};
        File.WriteAllText(path,JsonUtility.ToJson(config,true));return path;
    }
    public static async Task<string> Live()
    {
        checks=0;var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var e=c.Engine;
        string path=Folder+"/live-result.txt";
        try
        {
            await Task.Delay(1800);Check(e.Adaptive.Config.mode=="ACTIVE","Active test mode");
            Check(e.Phase==TaskPhase.Preparation,"Fresh session");c.enabled=false;
            // Only setup/baselines are accelerated. Training uses the real player loop and HTTP service.
            double now=Time.realtimeSinceStartupAsDouble-288;int frame=Time.frameCount;
            while(e.Adaptive.Stage!="training")Trial(e,ref now,ref frame);
            typeof(TaskPlatformController).GetField("lastTime",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,Time.realtimeSinceStartupAsDouble);
            c.enabled=true;
            e.Note("synthetic_rolling_live_probe","Accelerated programmatic practice/baselines; real-time training, actual films, real HTTP, programmatic responses; no participant physiology",Time.realtimeSinceStartupAsDouble,Time.frameCount);
            int changes=0,failures=0,interruptions=0,started=0,ended=0;bool captured=false;double began=Time.realtimeSinceStartupAsDouble,changeTime=-1;
            e.OnEvent+=ev=>{if(ev.type=="adaptive_level_changed"){changes++;changeTime=ev.actual;Check(!c.AdaptiveRuntime.Media.IsPlaying,"Never change during media playback");Debug.Log("Rolling live verified: "+ev.detail+";elapsed="+(ev.actual-began).ToString("F2"));}};
            c.AdaptiveRuntime.Media.OnMedia+=(exposure,status,time,detail)=>{if(status=="failure")failures++;if(status=="interrupted")interruptions++;if(status=="started")started++;if(status=="ended")ended++;};
            while(Time.realtimeSinceStartupAsDouble-began<210 && !e.Adaptive.TrainingEndRequested)
            {
                await Task.Delay(20);double real=Time.realtimeSinceStartupAsDouble;
                Check(e.Phase==TaskPhase.Running,"No unexpected pause or termination");
                if(e.Current.scored && !e.CurrentResult.responded && real-e.CurrentResult.actualOnset>=.5)e.Respond(e.Current.expectedMatch,real,Time.frameCount);
                if(!captured && real-began>2)
                {
                    captured=true;c.spatialView.timer.ForceMeshUpdate();
                    Check(System.Text.RegularExpressions.Regex.IsMatch(c.spatialView.timer.text,@"^[0-4]$"),"Actual integer HUD");
                    Check(!c.spatialView.timer.isTextOverflowing,"Countdown fits existing rectangle");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/countdown.png"));
                }
            }
            Check(changes==1 && e.Adaptive.Level==2,"Real HTTP applied Level 1 to Level 2");
            Check(changeTime-began<108,"Upgrade before old statistics boundary");
            Check(e.Adaptive.Blocks.Any(b=>b.stage=="training" && b.mixedLevel && b.level==-1),"Mixed-level 108-second summary explicitly labeled");
            Check(started>=3 && ended>=3 && failures==0 && interruptions==0,"Real media retained, no failures or interrupted clips");
            Check(e.Adaptive.TrainingEndRequested,"Level 2 mastery queued for statistics boundary");
            e.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_probe_complete");
            string report="PASS: "+checks+" live checks; real-time 10/2 HTTP control, Level 1->2 after "+(changeTime-began).ToString("F2")+" s; Level 2 candidate completion queued; "+started+" media starts / "+ended+" ends / 0 failures; mixed statistics, integer countdown fits. Accelerated setup and programmatic answers, no physical/sensor validation. Session: "+c.DataPath;
            File.WriteAllText(path,report);Debug.Log(report);return report;
        }
        catch(Exception ex){e.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_probe_failure");File.WriteAllText(path,"FAIL: "+ex+"\nSession: "+c.DataPath);throw;}
    }
    public static string StartLive(){RunDetached();return "Live probe started; poll Evidence/RollingAdaptation/live-result.txt.";}
    static async void RunDetached(){try{await Live();}catch(Exception ex){Debug.LogException(ex);}}
    public static async Task<string> Smoke()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();await Task.Delay(800);
        Check(Application.version=="2.0.0-stage2-dev5" && c.Engine.Adaptive.Config.version==Application.version,"Final version stamping");
        Check(c.Engine.Adaptive.Config.mode=="OFF","User mode preserved");
        var e=c.Engine;e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        await Task.Delay(1100);c.spatialView.timer.ForceMeshUpdate();
        Check(System.Text.RegularExpressions.Regex.IsMatch(c.spatialView.timer.text,@"^[0-4]$") && !c.spatialView.timer.isTextOverflowing,"Final countdown");
        string metadata=File.ReadAllText(Path.Combine(c.DataPath,"session.json"));
        Check(metadata.Contains(SpatialTaskView.CountdownRevision) && metadata.Contains("adaptive-lab-v2.0-rolling-countdown"),"Saved display revisions");
        e.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_final_smoke");
        string result="PASS: final dev5 runtime/config stamps, preserved OFF mode, actual integer HUD and session display metadata. "+c.DataPath;
        File.WriteAllText(Folder+"/final-smoke.txt",result);Debug.Log(result);return result;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using EvidenceVR;
using UnityEngine;

public static class ShortCalibrationProgressValidation
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Step(TaskEngine engine,int count,ref double now)
    {
        for(int i=0;i<count;i++){now=engine.CurrentResult.plannedOnset+4.5;engine.Tick(now,i+1);}
    }
    static TaskEngine Stage(string stage,int completed=0)
    {
        var engine=new TaskEngine(new TaskConfig(),AdaptiveTrainingConfig.Personalized());
        typeof(TaskEngine).GetMethod("PersonalStage",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(engine,new object[]{stage,0,0d,0,TaskPhase.Instructions});
        typeof(TaskEngine).GetProperty("TrainingOrdinal").SetValue(engine,stage=="bioadaptive"?2:1);
        engine.Advance(0,0);double now=0;Step(engine,completed,ref now);return engine;
    }
    public static string Inspect()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(owner==null)throw new Exception("Open the existing task scene first.");
        var view=owner.spatialView;
        var card=(RectTransform)view.timer.transform.parent;
        string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
        string result="Countdown="+card.name+";parent="+card.parent.name+";position="+card.anchoredPosition+";size="+card.sizeDelta+
            ";number="+view.timer.name+";caption="+view.timerLabel.name+";font="+view.timer.fontSize+";scale="+card.lossyScale+
            ";children="+string.Join(",",view.taskCanvas.GetComponentsInChildren<RectTransform>(true).Select(x=>x.name))+
            ";override="+path+";config="+(File.Exists(path)?File.ReadAllText(path):"absent");
        Directory.CreateDirectory("Evidence/ShortCalibrationProgress");File.WriteAllText("Evidence/ShortCalibrationProgress/before-ui.txt",result);
        return result;
    }
    public static string Run()
    {
        checks=0;Check(Application.isPlaying,"Use isolated engines on the running engineering UI");
        Check(AdaptiveTrainingConfig.Personalized().calibrationBlocks==4,"Default four blocks");
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var runtime=o.AdaptiveRuntime;
        Check(runtime!=null && o.Engine.IsPersonalized,"Personalized engineering preview required");
        var original=o.Engine;var property=typeof(TaskPlatformController).GetProperty("Engine");
        var runtimeField=typeof(AdaptiveRuntime).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic);
        try
        {
            foreach(string stage in new[]{"practice","calibration","random","bioadaptive"})
            {
                int total=stage=="practice"?14:stage=="calibration"?12:96;
                var e=Stage(stage);Check(e.PersonalProgressText=="0/"+total,"Starts at zero: "+stage);
                double now=0;Step(e,2,ref now);
                Check(e.PersonalProgressText=="2/"+total,"Two completed slots: "+stage);
                if(e.Current.scored)e.Respond(!e.Current.expectedMatch,now+.1,3);
                Check(e.PersonalProgressText=="2/"+total,"Answer does not prematurely increment progress");
                property.SetValue(o,e);runtimeField.SetValue(runtime,e);
                o.spatialView.Refresh(o,now+.5);runtime.Render();
                Check(runtime.PersonalProgressValue.text=="2/"+total,"Actual UI value");
                Check(runtime.PersonalProgressValue.color==LabPalette.Text,"Progress has no correctness feedback");
                Check(o.spatialView.timer.text=="3.50","Existing countdown stays independent");
                runtime.PersonalProgressValue.ForceMeshUpdate();runtime.PersonalProgressLabel.ForceMeshUpdate();
                Check(!runtime.PersonalProgressValue.isTextOverflowing && !runtime.PersonalProgressLabel.isTextOverflowing,"Progress fits");
                var left=(RectTransform)o.spatialView.timer.transform.parent;
                var right=(RectTransform)runtime.PersonalProgressValue.transform.parent;
                Check(left.anchoredPosition.x<0 && right.anchoredPosition.x==-left.anchoredPosition.x && left.anchoredPosition.y==right.anchoredPosition.y,"Mirrored left/right placement");
                e.Pause(now+1,4);Check(e.PersonalProgressText=="2/"+total,"Pause does not complete a slot");
                runtime.Render();Check(!right.gameObject.activeSelf,"Paused progress hidden with task HUD");
                e.Resume(now+10,5);Check(e.PersonalProgressText=="3/"+total,"Interrupted slot consumes exactly one budget position");
                o.spatialView.Refresh(o,now+10);runtime.Render();Check(right.gameObject.activeSelf,"Progress restored after resume");
            }
            {
                var e=Stage("calibration");double now=0;
                for(int block=0;block<4;block++)
                {
                    Check(e.PersonalProgressCaption=="Block "+(block+1)+"/4","Block label");
                    Check(e.PersonalProgressText=="0/12","Reset at calibration block start");
                    Step(e,11,ref now);Check(e.PersonalProgressText=="11/12","Last active calibration trial");Step(e,1,ref now);
                }
                Check(now==216 && e.Adaptive.Stage=="model_wait","Short calibration lasts 216 seconds");
                Check(e.Results.Count==48 && e.Results.GroupBy(t=>t.level).All(g=>g.Count()==24),"Equal L0/L1 budgets");
                e.ReceivePersonalModel("personal_model_not_validated",false,1,now,0);Check(e.SupplementUsed,"Supplement offered");e.Advance(now,0);
                Check(e.PersonalProgressText=="0/12" && e.PersonalProgressCaption=="Extra block 1/2","Supplement has local progress");
                Step(e,12,ref now);Check(e.PersonalProgressText=="0/12" && e.PersonalProgressCaption=="Extra block 2/2","Supplement second block");
                Step(e,12,ref now);Check(now==324 && e.Results.Count==72,"Supplement total 324 seconds / 72 slots");
                Check(e.Results.Select(t=>t.phaseTrial).Distinct().Count()==72,"Supplement IDs do not overlap");
                e.ReceivePersonalModel("personal_model_not_validated",false,2,now,0);
                Check(!e.PersonalModelAdmitted && e.Adaptive.Stage=="questionnaire_calibration","Second failure does not falsely admit a model");
            }
            {
                var e=Stage("practice",2);e.Advance(9.1,3);double now=9;Step(e,1,ref now);
                Check(e.Results.Count==3 && e.Adaptive.Stage=="calibration" && e.PersonalProgressCompleted==0,"Skipped practice never shows 14 completed");
            }
            foreach(string stage in new[]{"random","bioadaptive"})
            {
                var e=Stage(stage,24);Check(e.PersonalProgressText=="24/96","Training count does not reset at statistics block");
                double now=108;Step(e,71,ref now);Check(e.PersonalProgressText=="95/96","Training last active trial");
                property.SetValue(o,e);runtimeField.SetValue(runtime,e);o.spatialView.Refresh(o,now+.5);runtime.Render();
                runtime.PersonalProgressValue.ForceMeshUpdate();Check(!runtime.PersonalProgressValue.isTextOverflowing,"Two-digit numerator and denominator fit");
                Step(e,1,ref now);runtime.Render();Check(!runtime.PersonalProgressValue.transform.parent.gameObject.activeSelf,"No stale progress on questionnaire");
            }
            string metadata=File.ReadAllText(Path.Combine(o.DataPath,"session.json"));
            Check(metadata.Contains(TaskEngine.PersonalProgressRevision) && metadata.Contains(TaskEngine.PersonalPresentationRevision),"Visual revisions in session metadata");
            string result="SHORT CALIBRATION / PROGRESS PASS: "+checks+" synthetic checks; 216 s initial / 324 s with supplement; no physical acceptance.";
            File.WriteAllText("Evidence/ShortCalibrationProgress/unity-validation.txt",result);Debug.Log(result);return result;
        }
        finally{property.SetValue(o,original);runtimeField.SetValue(runtime,original);o.spatialView.Refresh(o,Time.realtimeSinceStartupAsDouble);runtime.Render();}
    }
    static string Preview(string stage,int completed)
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        o.Engine.Note("engineering_preview","static short-calibration/progress UI; synthetic data only",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        o.enabled=false;
        var e=Stage(stage,completed);
        typeof(TaskPlatformController).GetProperty("Engine").SetValue(o,e);
        typeof(AdaptiveRuntime).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o.AdaptiveRuntime,e);
        double now=completed*4.5+.5;
        o.stimulus.enabled=true;o.stimulus.material.color=LabPalette.Stimuli[e.Current.color];
        o.spatialView.Refresh(o,now);o.AdaptiveRuntime.Render();o.StatusView.Refresh();Canvas.ForceUpdateCanvases();
        return "Static synthetic preview: "+stage+";left="+o.spatialView.timer.text+";right="+o.AdaptiveRuntime.PersonalProgressValue.text+";label="+o.AdaptiveRuntime.PersonalProgressLabel.text;
    }
    public static string PreviewCalibration()=>Preview("calibration",2);
    public static string PreviewPractice()=>Preview("practice",2);
    public static string PreviewTraining()=>Preview("random",24);
}

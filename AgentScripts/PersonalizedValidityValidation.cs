using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;

public static class PersonalizedValidityValidation
{
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static TaskEngine Stage(string stage)
    {
        var e=new TaskEngine(new TaskConfig{formalFeedbackEnabled=true},AdaptiveTrainingConfig.Personalized());
        typeof(TaskEngine).GetMethod("PersonalStage",BindingFlags.NonPublic|BindingFlags.Instance)
            .Invoke(e,new object[]{stage,0,0d,0,TaskPhase.Instructions});
        e.Advance(0,0);e.Tick(4.5,1);e.Tick(9,2);return e;
    }
    public static string Run()
    {
        checks=0;
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(owner!=null && owner.spatialView!=null,"existing scene controller and view");
        var property=typeof(TaskPlatformController).GetProperty("Engine");var original=owner.Engine;
        var view=owner.spatialView;
        var texts=new[]{view.stage,view.rule,view.feedback,view.timer,view.timerLabel};
        var contents=texts.Select(t=>t.text).ToArray();var colors=texts.Select(t=>t.color).ToArray();
        bool instructions=view.instructions.enabled, task=view.taskCanvas.enabled;
        try
        {
            foreach(string stage in new[]{"practice","calibration","random","bioadaptive"})
            foreach(string answer in new[]{"correct","incorrect","omission"})
            {
                var e=Stage(stage);property.SetValue(owner,e);
                double now=10;
                if(answer=="omission"){now=13.01;e.Tick(now,3);}
                else Check(e.Respond(answer=="correct"?e.Current.expectedMatch:!e.Current.expectedMatch,now,3),"response accepted");
                view.Refresh(owner,now);
                Check(e.Config.formalFeedbackEnabled==false,"personalized feedback frozen off regardless of base config");
                if(stage=="practice")
                {
                    Check(e.ShowsCorrectnessFeedback,"practice feedback retained");
                    Check(view.timerLabel.text==(answer=="omission"?"No response":answer=="correct"?"Correct":"Incorrect"),"practice wording");
                    Check(view.timer.color==(answer=="correct"?SpatialTaskView.CorrectColor:SpatialTaskView.IncorrectColor),"practice colors");
                }
                else
                {
                    Check(!e.ShowsCorrectnessFeedback,"calibration/training feedback disabled");
                    Check(view.timer.color==LabPalette.Text,"neutral countdown even after response or omission");
                    Check(view.timerLabel.text=="" || view.timerLabel.text=="Next","no answer wording");
                }
                Check(e.CurrentResult.correct==(answer=="correct"),"scoring unaffected by visibility");
            }
            foreach(string stage in new[]{"practice","calibration","random","bioadaptive"})
            {
                var e=Stage(stage);e.ReportPersonalMountMovement(10,1,"synthetic_operator_fixture");
                Check(!e.PersonalMountIntact && !e.PersonalModelAdmitted && e.Phase==TaskPhase.Paused,"mount invalidates and pauses");
                e.Resume(20,2);e.Advance(21,3);e.ReceivePersonalModel("personal_model_validated",true,2,22,4);
                Check(e.Phase==TaskPhase.Paused && !e.PersonalModelAdmitted,"resume and stale model cannot restore mount qualification");
            }
            Check(new TaskEngine(new TaskConfig{formalFeedbackEnabled=true}).ShowsCorrectnessFeedback,"Stage 1 approved feedback retained");
            Check(Stage("calibration").PersonalMountIntact,"fresh session permits fresh calibration");
        }
        finally
        {
            property.SetValue(owner,original);
            for(int i=0;i<texts.Length;i++){texts[i].text=contents[i];texts[i].color=colors[i];}
            view.instructions.enabled=instructions;view.taskCanvas.enabled=task;
        }
        string result="PASS: "+checks+" feedback, scoring and mount-integrity checks; synthetic engines on actual scene UI; no physical acceptance.";
        Directory.CreateDirectory("Evidence/PersonalizedValidity");File.WriteAllText("Evidence/PersonalizedValidity/unity-validity-validation.txt",result);
        Debug.Log(result);return result;
    }
    public static string InspectLive()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        string result="DEV10 LIVE: stage="+o.Engine.Adaptive.Stage+";feedback="+o.Engine.ShowsCorrectnessFeedback+
            ";mount="+o.Engine.PersonalMountIntact+";form="+o.Engine.Adaptive.Config.questionnaireAdministration+
            ";timer="+o.spatialView.timer.text+";caption="+o.spatialView.timerLabel.text+";data="+o.DataPath;
        Debug.Log(result);return result;
    }
    public static async Task<string> AnswerCalibrationIncorrectly()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(o.Engine.Phase!=TaskPhase.Preparation)throw new Exception("Expected a fresh engineering preview session");
        o.Engine.Note("engineering_preview","direct calibration entry for UI check; no resting/calibration evidence",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        typeof(TaskEngine).GetMethod("PersonalStage",BindingFlags.NonPublic|BindingFlags.Instance)
            .Invoke(o.Engine,new object[]{"calibration",0,Time.realtimeSinceStartupAsDouble,Time.frameCount,TaskPhase.Instructions});
        o.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        // The test script's in-memory compile happened before trial onset, not during the trial.
        typeof(TaskPlatformController).GetField("lastTime",BindingFlags.NonPublic|BindingFlags.Instance)
            .SetValue(o,Time.realtimeSinceStartupAsDouble);
        double deadline=Time.realtimeSinceStartupAsDouble+16;
        while(o.Engine.Current==null || !o.Engine.Current.scored)
        {
            if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("Live trial loop did not advance");
            await Task.Delay(50);
        }
        await Task.Delay(300);
        Check(o.Engine.Respond(!o.Engine.Current.expectedMatch,Time.realtimeSinceStartupAsDouble,Time.frameCount),"live response accepted");
        await Task.Delay(100);
        Check(o.spatialView.timer.color==LabPalette.Text && o.spatialView.timerLabel.text=="","live incorrect response has no correctness feedback");
        return InspectLive();
    }
    public static string PreviewQuestions()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        o.Engine.Note("engineering_preview","synthetic oral-questionnaire UI preview; no participant or sensor claims",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        typeof(TaskEngine).GetMethod("PersonalStage",BindingFlags.NonPublic|BindingFlags.Instance)
            .Invoke(o.Engine,new object[]{"questionnaire_calibration",0,Time.realtimeSinceStartupAsDouble,Time.frameCount,TaskPhase.Rest});
        return InspectLive();
    }
}

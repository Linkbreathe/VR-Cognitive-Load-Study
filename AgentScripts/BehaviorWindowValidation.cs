using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EvidenceVR;
using UnityEngine;

// Isolated synthetic TaskEngine instances; no participant or acquisition claims.
public static class BehaviorWindowValidation
{
    [Serializable] public class Trace { public string mode; public int seed=20261102; public int[] requests; public bool[] correct; public bool[] omitted; }
    [Serializable] public class Traces { public Trace[] traces; }
    [Serializable] public class Rows { public TrialResult[] trials; }
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static TaskEngine Start(int seed=20261102)
    {
        var c=AdaptiveTrainingConfig.Personalized();c.studyDesign=LayerABStudy.Revision;
        c.questionnaireRevision=LayerABStudy.QuestionnaireRevision;c.evidenceGuardMode="observe";
        c.participantCode="SYNTHETIC_BEHAVIOR_WINDOW";c.counterbalanceGroup=0;c.seed=seed;c.Validate();
        var e=new TaskEngine(new TaskConfig(),c);double now=0;
        e.Advance(now,0);e.Advance(now,0);e.Advance(now,0);
        now=e.CurrentResult.plannedOnset+4.5;e.Tick(now,1);e.Advance(now,1);
        for(int i=0;i<48;i++){now=e.CurrentResult.plannedOnset+4.5;e.Tick(now,i);}
        e.ReceivePersonalModel("personal_model_validated",true,1,now,1);e.Advance(now,1);
        Check(e.Adaptive.Stage=="layer_a","Synthetic A condition");return e;
    }
    public static string Export()
    {
        var e=Start();var rows=new List<TrialResult>();
        for(int i=0;i<96;i++){var t=e.CurrentResult;rows.Add(t);e.Tick(t.plannedOnset+4.5,i);}
        File.WriteAllText("Evidence/BehaviorWindowRevision/unity-sequence.json",JsonUtility.ToJson(new Rows{trials=rows.ToArray()},true));
        return "Exported exact Unity synthetic sequence";
    }
    public static string Run()
    {
        Check(Application.isPlaying,"Synthetic validation requires Play Mode");checks=0;
        var traces=Newtonsoft.Json.JsonConvert.DeserializeObject<Traces>(File.ReadAllText("Evidence/BehaviorWindowRevision/traces.json"));
        var report=new List<string>();
        foreach(var trace in traces.traces)
        {
            var e=Start(trace.seed);var events=new List<TaskEvent>();e.OnEvent+=events.Add;
            double origin=e.CurrentResult.plannedOnset;
            for(int i=0;i<96;i++)
            {
                var t=e.CurrentResult;
                Check(Math.Abs(t.plannedOnset-(origin+i*4.5))<1e-7,"Unchanged 4.5-second onsets");
                if(t.scored && !trace.omitted[i])e.Respond(trace.correct[i]?e.Current.expectedMatch:!e.Current.expectedMatch,t.plannedOnset+1,i);
                e.Tick(t.plannedOnset+4,i);Check(!e.StimulusVisible,"Four-second visibility");
                int before=e.Adaptive.Level;e.Tick(t.plannedOnset+4.5,i);
                if(trace.requests.Contains(i+1))
                {
                    Check(e.Adaptive.Level==before,"Request boundary starts next trial before async reply");
                    var d=new AdaptiveDecision{action=before==0?"ON":"OFF",epoch=e.RollingEpoch,
                        level=before,evaluationId=e.PersonalDecisionId,lastTrial=i+1,requestId="synthetic-"+(i+1)};
                    e.Adaptive.Pending=d;
                    // A delayed response cannot execute during the already-started next trial.
                    if(i+1<96){e.Tick(t.plannedOnset+4.6,i);Check(e.Adaptive.Level==before,"No mid-trial execution");}
                }
            }
            var changes=events.Where(x=>x.type=="adaptive_level_changed").ToArray();
            Check(changes.Length==trace.requests.Length,"Every scheduled witness switch executed");
            for(int k=0;k<changes.Length;k++)
                Check(Math.Abs(changes[k].actual-(origin+(trace.requests[k]+1)*4.5))<1e-7,"Executes one boundary after request");
            Check(events.Count(x=>x.type=="personal_decision_due")==19,"Nineteen requests retained");
            report.Add(trace.mode+" seed="+trace.seed+": "+changes.Length+" changes; requests="+string.Join(",",trace.requests));
        }
        // Existing active-media deferral remains enforced without stretching trials.
        var deferred=Start();double start=deferred.CurrentResult.plannedOnset;
        for(int i=0;i<25;i++)deferred.Tick(start+(i+1)*4.5,i);
        deferred.CanApplyAdaptiveChange=()=>false;
        deferred.Adaptive.Pending=new AdaptiveDecision{action="ON",epoch=deferred.RollingEpoch,level=0,
            evaluationId=deferred.PersonalDecisionId,lastTrial=25,requestId="synthetic-media"};
        deferred.Tick(start+26*4.5,26);Check(deferred.Adaptive.Level==0,"Active media defers action");
        deferred.CanApplyAdaptiveChange=()=>true;deferred.Tick(start+27*4.5,27);
        Check(deferred.Adaptive.Level==1,"Deferred action executes at safe boundary");
        var terminal=Start();double terminalStart=terminal.CurrentResult.plannedOnset;
        for(int i=0;i<95;i++)terminal.Tick(terminalStart+(i+1)*4.5,i);
        terminal.Adaptive.Pending=new AdaptiveDecision{action="ON",epoch=terminal.RollingEpoch,level=0,
            evaluationId=terminal.PersonalDecisionId,lastTrial=95,requestId="synthetic-terminal"};
        terminal.Tick(terminalStart+96*4.5,96);
        Check(terminal.Adaptive.Stage=="questionnaire_1","Trial 96 ends the training phase");
        Check(terminal.Adaptive.Level==0,"Trial 95 reply does not execute after the last trial");
        string result="BEHAVIOR WINDOW PASS: "+checks+" checks\n"+string.Join("\n",report);
        File.WriteAllText("Evidence/BehaviorWindowRevision/unity-validation.txt",result);Debug.Log(result);return result;
    }
}

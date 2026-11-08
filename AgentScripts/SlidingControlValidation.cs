using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EvidenceVR;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class SlidingControlValidation
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static TaskEngine Start()
    {
        var c=AdaptiveTrainingConfig.Personalized();c.studyDesign=LayerABStudy.Revision;
        c.slidingRevision=LayerABStudy.SlidingRevision;c.questionnaireRevision=LayerABStudy.QuestionnaireRevision;
        c.evidenceGuardMode="observe";c.counterbalanceGroup=0;c.seed=20261102;c.Validate();
        var e=new TaskEngine(new TaskConfig(),c);double now=0;
        e.Advance(now,0);e.Advance(now,0);e.Advance(now,0);
        now=e.CurrentResult.plannedOnset+4.5;e.Tick(now,1);e.Advance(now,1);
        for(int i=0;i<48;i++){now=e.CurrentResult.plannedOnset+4.5;e.Tick(now,i);}
        e.ReceivePersonalModel("personal_model_validated",true,1,now,1);e.Advance(now,1);return e;
    }
    public static string Export()
    {
        var e=Start();var rows=new List<TrialResult>();
        for(int i=0;i<96;i++){var r=e.CurrentResult;rows.Add(r);e.Tick(r.plannedOnset+4,i);e.Tick(r.plannedOnset+4.5,i);}
        Directory.CreateDirectory("Evidence/SlidingControl");
        File.WriteAllText("Evidence/SlidingControl/sequence.json",Newtonsoft.Json.JsonConvert.SerializeObject(new { trials=rows }));
        return "Exported 96 Unity stimuli for sliding-capacity replay";
    }
    public static string Run()
    {
        checks=0;Check(Application.isPlaying,"Run synthetic checks in Play Mode");
        var trace=JObject.Parse(File.ReadAllText("Evidence/SlidingControl/capacity.json"));
        var seq=JObject.Parse(File.ReadAllText("Evidence/SlidingControl/sequence.json"))["trials"];
        var wanted=trace["requests"].Values<int>().ToArray();var changes=new List<int>();var due=new List<TaskEvent>();
        var e=Start();double origin=e.CurrentResult.plannedOnset;
        e.OnEvent+=ev=>{if(ev.type=="personal_decision_due")due.Add(ev);};
        for(int i=0;i<96;i++){
            var t=e.CurrentResult;var start=t.plannedOnset;int count=i+1,before=e.Adaptive.Level;
            Check(Math.Abs(start-origin-i*4.5)<1e-8,"Stimulus interval changed");
            Check(t.expectedMatch==(bool)seq[i]["expectedMatch"],"Wrong sequence");
            if(t.scored && !(bool)trace["omitted"][i])e.Respond((bool)trace["correct"][i]?t.expectedMatch:!t.expectedMatch,start+1,i);
            e.Tick(start+4,i);Check(!e.StimulusVisible,"Response/visibility end");
            if(count%5==0 && count<96){
                Check(Math.Abs(due.Last().actual-(start+4))<1e-8,"Check must start at response close");
                Check(e.ProtocolCompletedTrials==i && t.closeTime==start+4,"Current outcome closed, trial not yet complete");
                Check(LayerABStudy.FreshSlidingReply(LayerABStudy.SlidingRevision,start+4.1,start+4.5,i,count),"On-time reply");
                Check(!LayerABStudy.FreshSlidingReply(LayerABStudy.SlidingRevision,start+4.5,start+4.5,i,count),"At deadline rejected");
                Check(!LayerABStudy.FreshSlidingReply(null,start+4.1,start+4.5,i,count),"Old server rejected");
                Check(!LayerABStudy.FreshSlidingReply(LayerABStudy.SlidingRevision,start+4.1,start+4.5,i+1,count),"Wrong trial rejected");
                if(wanted.Contains(count))
                    e.Adaptive.Pending=new AdaptiveDecision{action=before==0?"ON":"OFF",level=before,epoch=e.RollingEpoch,
                        evaluationId=e.PersonalDecisionId,lastTrial=count,requestId="synthetic-"+count,slidingRevision=LayerABStudy.SlidingRevision};
                e.Tick(start+4.1,i);Check(e.Adaptive.Level==before,"Never switch mid-trial");
            }
            e.Tick(start+4.5,i);
            if(count<96 && e.Adaptive.Level!=before)changes.Add(count);
            if(count<96)Check(e.CurrentResult.level==e.Adaptive.Level,"Following stimulus starts in delivered level");
        }
        Check(due.Count==19 && changes.SequenceEqual(wanted),"Request/execution counts differ");
        var result="SLIDING PASS: "+checks+" checks; 19 response-close requests; 15 after warmup/history; delivered after trials "+string.Join(",",changes);
        File.WriteAllText("Evidence/SlidingControl/unity-validation.txt",result);Debug.Log(result);return result;
    }
}

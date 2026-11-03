using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEngine;

public static class PersonalizedProtocolValidation
{
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static void RunTrials(TaskEngine e,ref double now,int count)
    {
        for(int i=0;i<count;i++)
        {
            Check(e.Phase==TaskPhase.Running,"must be running "+e.Adaptive.Stage+" at "+i);
            var start=e.CurrentResult.plannedOnset;
            Check(e.CurrentResult.mode==2 && e.CurrentResult.level>=0 && e.CurrentResult.level<=1,"two levels and 2-back");
            if(e.Current.scored)Check(e.Respond(e.Current.expectedMatch,start+1,1),"response accepted");
            e.Tick(start+3.99,2);Check(e.StimulusVisible,"full four-second visibility");
            e.Tick(start+4,3);Check(!e.StimulusVisible,"four-second offset");
            now=start+4.5;e.Tick(now,4);
        }
    }
    static TaskEngine Begin(int group,int calibrationBlocks,ref double now,List<TaskEvent> events=null)
    {
        var c=AdaptiveTrainingConfig.Personalized();c.counterbalanceGroup=group;c.calibrationBlocks=calibrationBlocks;
        var e=new TaskEngine(new TaskConfig(),c);if(events!=null)e.OnEvent+=events.Add;
        Check(e.Phase==TaskPhase.Preparation && e.Adaptive.Stage=="preparation","preparation retained");
        e.Advance(now,0);
        Check(e.Phase==TaskPhase.Instructions && e.Adaptive.Stage=="practice" && e.Current==null,"direct practice instructions without resting baseline");
        e.Advance(now,0);return e;
    }
    public static string Run()
    {
        checks=0;
        for(int group=0;group<4;group++)foreach(int blocks in new[]{6,8})
        {
            double now=0;var events=new List<TaskEvent>();var e=Begin(group,blocks,ref now,events);
            RunTrials(e,ref now,14);Check(now==63,"practice maximum 63 seconds; no baseline delay");
            e.Advance(now,0);double start=now;RunTrials(e,ref now,blocks*12);
            Check(e.Adaptive.Stage=="model_wait" && now-start==blocks*54,"calibration budget");
            var calibration=e.Results.Where(t=>t.trainingStage=="calibration").ToList();
            for(int b=0;b<blocks;b++)
            {
                var rows=calibration.Skip(b*12).Take(12).ToList();
                Check(rows.All(t=>t.level==(group%2+b)%2),"alternating conditions");
                Check(rows.Count==12,"twelve stimuli per calibration block");
                if(b>0)Check(rows.Take(2).All(t=>t.transition),"two transition rows");
            }
            for(int i=2;i<calibration.Count;i++)Check(calibration[i].expectedMatch==(calibration[i].color==calibration[i-2].color),"continuous 2-back memory");
            e.ReceivePersonalModel("personal_model_validated",true,1,now,0);e.Advance(now,0);
            Check(e.Adaptive.Stage==(group/2==0?"random":"bioadaptive"),"counterbalanced order");
            for(int run=1;run<=2;run++)
            {
                e.Advance(now,0);start=now;RunTrials(e,ref now,96);
                Check(now-start==432 && e.Adaptive.Stage=="questionnaire_"+run,"96 trials then external form");
                e.Advance(now,0);
            }
            Check(e.Phase==TaskPhase.Complete,"study completes after external forms");
            Check(events.Count(x=>x.type=="personal_decision_due")==38,"19 decisions per 96-trial training");
            Check(e.Adaptive.Blocks.Count(x=>x.stage=="random")==4 && e.Adaptive.Blocks.Count(x=>x.stage=="bioadaptive")==4,"four independent statistics blocks");
            Check(now==(blocks==8?1359:1251),"nominal total timing without baseline");
            Check(!events.Any(x=>x.type=="protocol_stage" && x.detail.StartsWith("rest_")),"no resting baseline stages anywhere in full session");
        }
        {
            double now=0;var e=Begin(1,8,ref now);
            e.Advance(now+.1,0);RunTrials(e,ref now,1);Check(e.Results.Count==1,"skip practice at boundary");
            e.Advance(now,0);RunTrials(e,ref now,96);e.ReceivePersonalModel("personal_model_not_validated",false,1,now,0);
            Check(e.SupplementUsed && e.Adaptive.Stage=="calibration","one supplemental pair offered");
            e.Advance(now,0);double start=now;RunTrials(e,ref now,24);
            Check(now-start==108 && e.Results.Count(x=>x.trainingStage=="calibration")==120,"supplement count and time");
            Check(e.Results.Where(x=>x.trainingStage=="calibration").Select(x=>x.phaseTrial).Distinct().Count()==120,"unique calibration trial ids");
            e.ReceivePersonalModel("personal_model_not_validated",false,2,now,0);
            Check(!e.PersonalModelAdmitted && e.Adaptive.Stage=="questionnaire_calibration","failed model explicitly retained");
        }
        foreach(int pauseIndex in new[]{0,11,23,95})
        {
            double now=0;var e=Begin(0,8,ref now);RunTrials(e,ref now,14);e.Advance(now,0);
            RunTrials(e,ref now,pauseIndex);e.Pause(now+1,0);e.Resume(now+11,0);now+=11;
            if(e.Phase==TaskPhase.Running)RunTrials(e,ref now,95-pauseIndex);
            var rows=e.Results.Where(x=>x.trainingStage=="calibration").ToList();
            Check(rows.Count==96 && rows.Select(x=>x.phaseTrial).Distinct().Count()==96,"pause preserves exact stimulus budget");
            Check(rows.GroupBy(x=>x.block).All(g=>g.Count()==12),"pause at boundary preserves twelve-trial blocks");
            Check(rows.Count(x=>x.interrupted)==1 && e.Adaptive.Stage=="model_wait","pause explicitly excluded");
        }
        {
            double now=0;var e=Begin(0,8,ref now);RunTrials(e,ref now,14);e.Advance(now,0);RunTrials(e,ref now,96);
            e.ReceivePersonalModel("personal_model_validated",true,1,now,0);e.Advance(now,0);e.Advance(now,0);RunTrials(e,ref now,20);
            e.Adaptive.Pending=new AdaptiveDecision{action="ON",level=0,epoch=e.RollingEpoch,evaluationId=e.PersonalDecisionId,lastTrial=20};
            e.CanApplyAdaptiveChange=()=>false;RunTrials(e,ref now,1);Check(e.Adaptive.Level==0 && e.Adaptive.Pending!=null,"active movie defers execution");
            e.CanApplyAdaptiveChange=()=>true;RunTrials(e,ref now,1);Check(e.Adaptive.Level==1 && e.CurrentResult.transition,"ON executes only at safe boundary and marks transition");
            e.Adaptive.Pending=new AdaptiveDecision{action="OFF",level=1,epoch=e.RollingEpoch-1,evaluationId=e.PersonalDecisionId,lastTrial=22};
            RunTrials(e,ref now,1);Check(e.Adaptive.Level==1 && e.Adaptive.Pending==null,"stale epoch rejected");
        }
        {
            var c=AdaptiveTrainingConfig.Personalized();var manifest=JsonUtility.FromJson<AdaptiveMediaManifest>(File.ReadAllText("Assets/Resources/adaptive-media-manifest.json"));
            var scheduler=new AdaptiveMediaScheduler(c,manifest);
            foreach(string stage in new[]{"calibration","random","bioadaptive"})foreach(int level in new[]{0,1})
            {
                var plan=scheduler.Plan(3,level,stage);double previousEnd=0;
                foreach(var p in plan){Check(p.modality==(level==0?"silent":"audiovisual"),"no audio-only level");Check(p.onset>=previousEnd,"no media overlap");previousEnd=p.onset+p.duration;}
                Check(previousEnd<=(stage=="calibration"?54:108),"media fits block");
            }
        }
        string result="PASS: "+checks+" assertions; synthetic clocks and responses only; no physical acceptance.";
        Directory.CreateDirectory("Evidence/NoRestProtocol");File.WriteAllText("Evidence/NoRestProtocol/unity-protocol-validation.txt",result);
        Debug.Log(result);return result;
    }
}

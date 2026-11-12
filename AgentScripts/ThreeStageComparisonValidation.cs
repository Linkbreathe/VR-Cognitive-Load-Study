using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EvidenceVR;
using UnityEngine;

public static class ThreeStageComparisonValidation
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static AdaptiveTrainingConfig Config(string stage)
    {
        var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText("Assets/TaskPlatform/Config/adaptive-training-config.json"));
        c.enabled=true;c.mode="ACTIVE";c.operatorManaged=true;c.testSessionMode="comparison_rehearsal";c.sessionType="engineering";
        c.studyDesign="layer-ab-probe-v2";c.slidingRevision=LayerABStudy.SlidingRevision;c.questionnaireRevision=LayerABStudy.QuestionnaireRevision;
        c.evidenceGuardMode="observe";c.mediaManifest="layer-ab-144-media.json";c.onsetJitterSeconds=0;c.abInterConditionRestSeconds=180;
        c.evaluationPolicy="behavior5-continuous-stride2-v3";c.trainingTrials=34;c.rollingWindowTrials=5;c.rollingStrideTrials=2;c.videoWidth=3.84f;c.videoHeight=2.16f;
        c.comparisonStage=stage;c.comparisonRevision="comparison-three-stages34-v2";c.comparisonEnvironment="desktop_fixed_gaze";
        c.testModelId="";c.testModelHash="";c.testSessionId="";c.Validate();return c;
    }
    public static object Run()
    {
        var results=new List<object>();
        foreach(string stage in new[]{"fixed_exposure","layer_a","layer_b"})
        {
            var c=Config(stage);var engine=new TaskEngine(new TaskConfig(),c);
            Check(LayerABStudy.Stage(c,1)==stage,"Selected stage must start independently");
            Check(LayerABStudy.GuardMode(c,stage)==(stage=="layer_b"?"enforce":"observe"),"A/B review assignment");
            Call(engine,"AdvancePersonal",0d,0);
            Check(engine.Adaptive.Stage==stage,"Preparation routes to the selected stage");
            Call(engine,"AdvancePersonal",0d,0);
            var checks=new List<int>();var levels=new List<int>();var receipts=new List<string>();
            engine.OnEvent+=e=>{if(e.type=="personal_decision_due")checks.Add(engine.CurrentResult.phaseTrial+1);if(e.type=="personal_execution")receipts.Add(e.detail);};
            for(int i=0;i<34;i++)
            {
                var t=engine.CurrentResult;Check(t.phaseTrial==i && engine.PersonalProgressTotal==34,"Exactly 34 positions");
                Check(t.scored==(i>=2),"Two observe-only warmups in the budget");
                levels.Add(t.level);engine.Tick(t.plannedOnset+4,i);
                if(i==7 || i==9 || i==11)
                {
                    string action=i==7?"ON":i==9?"HOLD":"OFF";
                    engine.CanApplyAdaptiveChange=()=>action=="ON";
                    engine.Adaptive.Pending=new AdaptiveDecision{action=action,epoch=engine.RollingEpoch,level=t.level,lastTrial=i+1,
                        evaluationId=engine.PersonalDecisionId,requestId="synthetic-"+i,guardRequestId="synthetic-"+i,
                        guardRevision=EvidenceGuardContract.Revision,guardMode="enforce",guardVerdict="ALLOW"};
                }
                engine.Tick(t.plannedOnset+4.5,i);
            }
            Check(engine.ProtocolCompletedTrials==34 && engine.Phase==TaskPhase.Rest,"Stop at questionnaire after 34");
            if(stage=="fixed_exposure")
            {Check(levels.SequenceEqual(Enumerable.Repeat(0,18).Concat(Enumerable.Repeat(1,16))),"2 warmups, 16 scored OFF, 16 scored ON");Check(checks.Count==0 && receipts.Count==0,"No adaptive decisions in fixed exposure");}
            else {Check(checks.SequenceEqual(Enumerable.Range(1,16).Select(i=>i*2)),"Checks 2 through 32");Check(receipts.Count==3,"ON/HOLD/OFF executed");}
            Check(engine.Results.Count(t=>t.scored)==32,"32 scored results");
            Check(engine.ABRestRemaining(153)==0,"No paired inter-condition rest");
            Call(engine,"AdvancePersonal",153d,35);Check(engine.Phase==TaskPhase.Complete,"Questionnaire ends this independent assignment");
            results.Add(new {stage,trials=34,levels,checks,receipts,complete=engine.Phase.ToString()});
        }
        Directory.CreateDirectory("Evidence/Comparison34-20261112");
        File.WriteAllText("Evidence/Comparison34-20261112/timing.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
        Debug.Log("PASS: three independent 34-trial stages; 2 warmups plus 32 scored; fixed scored 16/16; A/B checks 2..32; neutral warmups; standalone questionnaire completion.");return results;
    }
    public static object VerifyAssignments()
    {
        var results=new List<object>();
        foreach(string stage in new[]{"fixed_exposure","layer_a","layer_b"})
        {
            string path="Evidence/Comparison34-20261112/browser/"+stage+"-config.json";
            var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path));c.Validate();
            Check(c.trainingTrials==34 && c.IndependentTrialBudget==34 && c.FixedOnTrialIndex==18,"New assignment contract");
            File.WriteAllText(path.Replace("-config.json","-unity-config.json"),JsonUtility.ToJson(c,true));
            c.comparisonRevision="comparison-three-stages30-v1";c.trainingTrials=30;c.Validate();
            Check(c.IndependentTrialBudget==30 && c.FixedOnTrialIndex==15,"Historical contract preserved");
            results.Add(new {stage,current=34,historical=30});
        }
        Debug.Log("PASS: three actual backend configurations accepted; historical 30-trial contracts retained.");
        return results;
    }
}

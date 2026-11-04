using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EvidenceVR;
using UnityEngine;

public static class EvidenceGuardValidation
{
    static int checks;
    static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
    public static string Run()
    {
        checks=0;Check(Application.isPlaying,"Engineering validation requires Play Mode");
        foreach(string mode in new[]{"off","observe","enforce"})
        foreach(string action in new[]{"ON","OFF","HOLD","REST"})
        foreach(bool authorized in new[]{false,true})
        {
            var config=AdaptiveTrainingConfig.Personalized();config.evidenceGuardMode=mode;config.Validate();
            int initial=action=="OFF"?1:0;
            var engine=new TaskEngine(new TaskConfig(),config);
            var events=new List<TaskEvent>();engine.OnEvent+=events.Add;
            typeof(TaskEngine).GetMethod("PersonalStage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(engine,new object[]{"bioadaptive",initial,0d,0,TaskPhase.Instructions});
            engine.Advance(0,0);for(int i=1;i<=25;i++)engine.Tick(i*4.5,i);
            var d=new AdaptiveDecision{sessionId="synthetic",requestId="guard-test",action=action,basis="behavioral_fallback",
                configVersion=config.version,level=initial,epoch=engine.RollingEpoch,evaluationId=engine.PersonalDecisionId,lastTrial=25};
            if(authorized){d.guardMode="enforce";d.guardRevision=EvidenceGuardContract.Revision;d.guardVerdict="ALLOW";d.guardRequestId=d.requestId;}
            bool permitted=!(mode=="enforce" && action=="ON" && !authorized);
            Check(EvidenceGuardContract.Allows(config,d)==permitted,"Wire authorization "+mode+action+authorized);
            engine.Adaptive.Pending=d;engine.CanApplyAdaptiveChange=()=>true;engine.Tick(26*4.5,26);
            Check(engine.Adaptive.Level==(action=="ON" && permitted?1:action=="OFF"?0:initial),"Selective execution "+mode+action+authorized);
            Check(action!="REST" || engine.Phase==TaskPhase.Paused,"REST remains independent");
            Check(permitted || events.Any(e=>e.type=="evidence_guard_rejected"),"Missing authorization audited");
        }
        var enforced=AdaptiveTrainingConfig.Personalized();enforced.evidenceGuardMode="enforce";
        foreach(string revision in new[]{"", "old-revision"})
            Check(!EvidenceGuardContract.Allows(enforced,new AdaptiveDecision{action="ON",requestId="a",guardRequestId="a",guardVerdict="ALLOW",guardMode="enforce",guardRevision=revision}),"Reject missing/old plugin");
        Check(!EvidenceGuardContract.Allows(enforced,new AdaptiveDecision{action="ON",requestId="a",guardRequestId="b",guardVerdict="ALLOW",guardMode="enforce",guardRevision=EvidenceGuardContract.Revision}),"Reject wrong request");
        var go=new GameObject("Synthetic guard transport validation");
        try
        {
            var runtime=go.AddComponent<AdaptiveRuntime>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(AdaptiveRuntime).GetField("config",flags).SetValue(runtime,enforced);
            typeof(AdaptiveRuntime).GetField("engine",flags).SetValue(runtime,new TaskEngine(new TaskConfig(),enforced));
            var sampleMethod=typeof(AdaptiveRuntime).GetMethod("RecordGuardGaze",flags);
            var eye=new GazeRegionSample{valid=true,task=true};
            sampleMethod.Invoke(runtime,new object[]{0d,eye,true,false});
            sampleMethod.Invoke(runtime,new object[]{.02d,eye,true,false});
            sampleMethod.Invoke(runtime,new object[]{1d,new GazeRegionSample(),false,false});
            var samples=(List<GuardGazeSample>)typeof(AdaptiveRuntime).GetField("guardGaze",flags).GetValue(runtime);
            Check(samples.Count==2,"No inferred coverage before first or after last observation");
            Check(Math.Abs(samples[0].end-.02)<1e-9,"Duration weights preserve irregular sample spacing");
            Check(Math.Abs(samples[1].end-.12)<1e-9,"Missing interval is capped at 100 ms");
            Check(samples.All(s=>s.target=="task" && s.taskVisible && !s.movieVisible),"Exclusive production ROI transport");
            string wire=JsonUtility.ToJson(samples[0]);
            Check(wire.Contains("\"start\"") && wire.Contains("\"end\"") && wire.Contains("\"target\":\"task\""),"Wire interval fields");
            runtime.Shutdown();
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
        using(var recorder=new SessionRecorder("Evidence/Stage3/Metadata",new TaskConfig(),kind:"synthetic",evidenceGuardMode:"enforce"))
        {
            string metadata=File.ReadAllText(Path.Combine(recorder.DirectoryPath,"session.json"));
            Check(metadata.Contains("\"evidenceGuardMode\": \"enforce\"") && metadata.Contains(EvidenceGuardContract.Revision),"Frozen guard metadata");
            recorder.Complete("synthetic_validation_complete");
        }
        string result="EVIDENCE GUARD PASS: "+checks+" synthetic execution checks; no participant, gaze-device or physiological acceptance.";
        Directory.CreateDirectory("Evidence/Stage3");File.WriteAllText("Evidence/Stage3/unity-validation.txt",result);Debug.Log(result);return result;
    }
}

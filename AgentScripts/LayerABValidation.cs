using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using EvidenceVR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class LayerABValidation
{
    public static string InstallBridgeFixture()
    {
        var c=Config();c.controllerUrl="http://127.0.0.1:8879";c.participantCode="SYNTHETIC_AB_BRIDGE";
        Directory.CreateDirectory("Evidence/LayerAB");string path="Evidence/LayerAB/integration-config.json";
        File.WriteAllText(path,JsonUtility.ToJson(c,true));return EvidenceVR.Editor.LayerABSetup.Install(path);
    }
    public static string VerifyBridge()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(owner?.Engine?.Adaptive?.Config.IsLayerAB!=true)throw new Exception("Expected isolated A/B fixture");
        if(!owner.AdaptiveRuntime.StudyClockReady || !owner.AdaptiveRuntime.ServiceConnected)throw new Exception("Clock/service handshake is not ready");
        owner.Engine.Note("synthetic_timing_probe","engineering_only;no_physical_input",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        string result="LAYER AB BRIDGE PASS: actual Unity HTTP clock handshake; event queued; session="+Path.GetFileName(owner.DataPath)+";path="+owner.DataPath;
        File.WriteAllText("Evidence/LayerAB/bridge-validation.txt",result);Debug.Log(result);return result;
    }
    static int checks;
    static void Check(bool condition,string why){checks++;if(!condition)throw new Exception(why);}
    static void Step(TaskEngine e,int count,ref double now)
    {for(int i=0;i<count;i++){now=e.CurrentResult.plannedOnset+4.5;e.Tick(now,i+1);}}
    static AdaptiveTrainingConfig Config(int group=0)
    {
        var c=AdaptiveTrainingConfig.Personalized();c.studyDesign=LayerABStudy.Revision;c.questionnaireRevision=LayerABStudy.QuestionnaireRevision;
        c.evidenceGuardMode="observe";c.counterbalanceGroup=group;c.participantCode="SYNTHETIC_AB";c.Validate();return c;
    }
    public static string Run()
    {
        Check(Application.isPlaying,"Run synthetic validation in Play Mode");checks=0;
        var sample=new StudyClockSample{unitySent=10,unityReceived=10.12,serverReceived=110.01,serverSent=110.11};
        Check(sample.Calculate(),"Clock calculation");Check(Math.Abs(sample.offset-100)<1e-9,"Offset excludes processing");
        Check(Math.Abs(sample.uncertainty-.01)<1e-9,"Network delay bound");
        for(int group=0;group<4;group++)
        {
            var c=Config(group);var e=new TaskEngine(new TaskConfig(),c);var events=new List<TaskEvent>();e.OnEvent+=events.Add;
            double now=0;e.Advance(now,0);e.Advance(now,0);e.Advance(now,0);Step(e,1,ref now);
            Check(e.Adaptive.Stage=="calibration","Practice skip still reaches calibration");e.Advance(now,1);Step(e,48,ref now);
            Check(e.Adaptive.Stage=="model_wait","Calibration budget");e.ReceivePersonalModel("personal_model_validated",true,1,now,1);
            Check(e.Adaptive.Stage==LayerABStudy.Stage(c,1),"First counterbalanced adaptive condition; no calibration questionnaire");
            for(int ordinal=1;ordinal<=2;ordinal++)
            {
                Check(e.TrainingOrdinal==ordinal,"Ordinal");Check(e.Adaptive.Level==0,"Fresh initial level");
                Check(e.Adaptive.Stage==LayerABStudy.Stage(c,ordinal),"Correct condition");
                Check(e.Adaptive.Pending==null,"No pending action leaks across conditions");e.Advance(now,1);
                int start=e.Results.Count;
                for(int i=0;i<96;i++)
                {
                    var t=e.CurrentResult;Check(t.phaseTrial==i,"Unique phase trial");
                    var onset=events.Last(x=>x.type=="stimulus_on");Check(onset.phaseTrial==i,"Onset marker phase-trial identity");
                    if(t.scored){Check(e.Respond(e.Current.expectedMatch,t.actualOnset+.37,i),"Response accepted");Check(Math.Abs(t.reactionTime-.37)<1e-8,"Original RT preserved");}
                    now=t.plannedOnset+4;e.Tick(now,i);Check(!e.StimulusVisible,"Four-second stimulus duration");
                    now=t.plannedOnset+4.5;e.Tick(now,i);
                }
                Check(e.Results.Count-start==96,"Exactly 96 trials");Check(e.Adaptive.Stage=="questionnaire_"+ordinal,"Correct scheduled questionnaire");
                e.Advance(now,1);Check(e.Phase==TaskPhase.Rest,"Cannot acknowledge unsaved questionnaire");
                e.ReceiveABQuestionnaire(e.Adaptive.Stage,true,now);
                if(ordinal==1){e.Advance(now+179,1);Check(e.Phase==TaskPhase.Rest,"Rest cannot be skipped");now+=180;}
                e.Advance(now,1);
            }
            Check(e.Phase==TaskPhase.Complete,"Complete only after final TLX/preference receipt");
            var on=new AdaptiveDecision{action="ON",requestId="r"};
            Check(EvidenceGuardContract.Allows(c,on,"layer_a"),"A does not gate base action");
            Check(!EvidenceGuardContract.Allows(c,on,"layer_b"),"B rejects missing authorization");
            on.guardMode="enforce";on.guardRevision=EvidenceGuardContract.Revision;on.guardVerdict="ALLOW";on.guardRequestId="r";
            Check(EvidenceGuardContract.Allows(c,on,"layer_b"),"B accepts valid authorization");
        }
        InputEdges();
        string result="LAYER AB PASS: "+checks+" synthetic checks; timing, four allocation groups, short questionnaire receipts and raw input edges. No participant/device validation.";
        Directory.CreateDirectory("Evidence/LayerAB");File.WriteAllText("Evidence/LayerAB/unity-validation.txt",result);Debug.Log(result);return result;
    }
    static void InputEdges()
    {
        var e=new TaskEngine(new TaskConfig(),Config());double now=Time.realtimeSinceStartupAsDouble;
        typeof(TaskEngine).GetMethod("PersonalStage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,new object[]{"layer_a",0,now-10,1,TaskPhase.Instructions});
        e.Advance(now-9.2,1);double at=0;Step(e,2,ref at);
        var device=InputSystem.AddDevice<Keyboard>();var action=new InputAction("SyntheticResponse",InputActionType.Button,"<Keyboard>/f11");
        var events=new List<TaskEvent>();e.OnEvent+=events.Add;
        try
        {
            using(var recorder=new SessionRecorder("Evidence/LayerAB/InputAudit",new TaskConfig(),"SYNTHETIC_AB","synthetic",studyDesign:LayerABStudy.Revision))
            using(var audit=new InputTimingAudit(e,action))
            {
                e.OnEvent+=recorder.Event;action.Enable();
                InputSystem.QueueStateEvent(device,new KeyboardState(Key.F11),now-.1);
                InputSystem.QueueStateEvent(device,new KeyboardState(),now-.05);InputSystem.Update();
                var edges=events.Where(x=>x.type=="input_edge").Select(x=>JsonUtility.FromJson<InputTimingAudit.Edge>(x.detail)).ToArray();
                Check(edges.Length==2,"Press and release both retained even in one frame");
                Check(Math.Abs(edges[0].inputTime-(now-.1))<1e-5,"Input event timestamp preserved");
                Check(Math.Abs(edges[1].heldSeconds-.05)<1e-5,"Hold duration preserved");
                Check(e.Respond(true,now,Time.frameCount),"Existing scoring uses frame time");audit.LinkAcceptedResponse(action,Time.frameCount);
                Check(Math.Abs(e.CurrentResult.reactionTime-.2)<1e-5,"Original frame RT");
                Check(Math.Abs(e.CurrentResult.inputReactionTime-.1)<1e-5,"Input event RT separately linked");
                Check(e.CurrentResult.inputEventId==edges[0].id,"Accepted response joins raw edge ID");
                recorder.Trial(e.CurrentResult);recorder.Complete("synthetic_complete");
            }
        }
        finally{action.Dispose();InputSystem.RemoveDevice(device);}
    }
}

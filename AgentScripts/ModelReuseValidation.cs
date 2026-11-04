using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEngine;

public static class ModelReuseValidation
{
    static int checks;
    static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
    static AdaptiveTrainingConfig Config(string mode)
    {
        var c=AdaptiveTrainingConfig.Personalized();c.operatorManaged=true;c.participantCode="VALIDATION_ONLY";
        c.testSessionMode=mode;c.testSessionId="validation-only";
        if(mode=="bioadaptive"){c.testModelId="fixture";c.testModelHash=new string('a',64);c.allowCandidateModel=true;}
        return c;
    }
    static void Trials(TaskEngine e,int count,ref double time)
    {
        for(int i=0;i<count;i++)
        {
            Check(e.Phase==TaskPhase.Running,"Running trial");
            double onset=e.CurrentResult.plannedOnset;
            if(e.Current.scored)e.Respond(e.Current.expectedMatch,onset+1,1);
            e.Tick(onset+3.99,2);Check(e.StimulusVisible,"Four-second visibility retained");
            e.Tick(onset+4,3);Check(!e.StimulusVisible,"Four-second offset retained");
            time=onset+4.5;e.Tick(time,4);
        }
    }
    public static string Run()
    {
        checks=0;
        var c=Config("bioadaptive");var e=new TaskEngine(new TaskConfig(),c);var events=new List<TaskEvent>();e.OnEvent+=events.Add;
        e.Advance(0,0);Check(e.Phase==TaskPhase.Preparation,"Reuse cannot start before service confirms model");
        e.ReceivePersonalModel("engineering_candidate_loaded",false,0,0,0,true);
        Check(!e.PersonalModelAdmitted && e.PersonalModelTestUsable,"Candidate usable without false admission");
        e.Advance(0,0);Check(e.Adaptive.Stage=="bioadaptive" && e.Phase==TaskPhase.Instructions,"Direct Bioadaptive instructions");
        e.Advance(0,0);double time=0;Trials(e,96,ref time);
        Check(e.Phase==TaskPhase.Complete && time==432,"Direct test ends after 96 slots / 432 s");
        Check(e.Results.Count==96 && e.Results.All(t=>t.trainingStage=="bioadaptive"),"No calibration or Random rows");
        Check(events.Count(x=>x.type=="personal_decision_due")==19,"Five-trial requests retained");
        Check(!events.Any(x=>x.type=="external_questionnaire_acknowledged"),"No questionnaire in direct engineering test");
        foreach(bool admitted in new[]{true,false})
        {
            e=new TaskEngine(new TaskConfig(),Config("calibration"));e.Advance(0,0);
            Check(e.Adaptive.Stage=="calibration","Calibration-only skips practice");e.Advance(0,0);time=0;Trials(e,48,ref time);
            Check(e.Adaptive.Stage=="model_wait","Waits for model saving");
            e.ReceivePersonalModel(admitted?"personal_model_validated":"personal_model_not_validated",admitted,1,time,0);
            if(!admitted){Check(e.SupplementUsed,"Existing supplemental pair retained");e.Advance(time,0);Trials(e,24,ref time);e.ReceivePersonalModel("personal_model_not_validated",false,2,time,0);}
            Check(e.Phase==TaskPhase.Complete,"Calibration-only ends after result");
        }
        c=Config("full");e=new TaskEngine(new TaskConfig(),c);e.Advance(0,0);
        Check(e.Adaptive.Stage=="practice","Full mode retains original entry");
        c=Config("bioadaptive");c.sessionType="participant";
        bool rejected=false;try{c.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Participant mode rejects engineering reuse");
        Directory.CreateDirectory("Evidence/ModelReuse");string message="MODEL REUSE ENGINE PASS: "+checks+" assertions; isolated synthetic task engines only";
        File.WriteAllText("Evidence/ModelReuse/engine-validation.txt",message);Debug.Log(message);return message;
    }
    public static string ConfigureIntegration()
    {
        var path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
        Directory.CreateDirectory("Evidence/ModelReuse");
        var backup="Evidence/ModelReuse/editor-override-before.json";
        if(!File.Exists(backup))File.WriteAllText(backup,File.Exists(path)?File.ReadAllText(path):"absent");
        var c=AdaptiveTrainingConfig.Personalized();c.operatorManaged=true;c.controllerUrl="http://127.0.0.1:8876";
        File.WriteAllText(path,JsonUtility.ToJson(c,true));return path;
    }
    public static string RestoreIntegration()
    {
        var path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");var backup=File.ReadAllText("Evidence/ModelReuse/editor-override-before.json");
        if(backup=="absent"){if(File.Exists(path))File.Delete(path);}else File.WriteAllText(path,backup);
        return "Restored pre-test Editor override";
    }
    public static string Inspect()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        return JsonUtility.ToJson(new State{data=o.DataPath,phase=o.Engine?.Phase.ToString(),stage=o.Engine?.Adaptive?.Stage,
            model=o.Engine?.PersonalModelStatus,canStart=o.AdaptiveRuntime?.CanStart??false,confirmed=o.AdaptiveRuntime?.TestAssignmentConfirmed??false,
            archive=o.TestClient?.Status,frame=Time.frameCount,heading=o.heading.text,body=o.body.text});
    }
    [Serializable] sealed class State {public string data,phase,stage,model,archive,heading,body;public bool canStart,confirmed;public int frame;}
    public static string AdvanceFixture()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(!o.Engine.IsOperatorTest || o.Engine.Adaptive.Config.participantCode!="TEST_FIXTURE")throw new Exception("Fixture identity required");
        o.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        return Inspect();
    }
    public static string StopFixture()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(!o.Engine.IsOperatorTest || o.Engine.Adaptive.Config.participantCode!="TEST_FIXTURE")throw new Exception("Fixture identity required");
        o.Engine.Abort(Time.realtimeSinceStartupAsDouble,Time.frameCount,"synthetic_integration_complete");return Inspect();
    }
}

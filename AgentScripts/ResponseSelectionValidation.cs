using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EvidenceVR;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ResponseSelectionValidation
{
    const string Folder="Evidence/ResponseAndHighlight-20261110";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);
    static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);
    static AdaptiveTrainingConfig Config(bool compact=true)
    {
        var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(Directory.GetFiles("Tools/LayerAB/Examples","*config.json")[0]));
        if(compact){c.evaluationPolicy="behavior5-continuous-stride2-v3";c.trainingTrials=60;c.rollingWindowTrials=5;c.rollingStrideTrials=2;}
        c.sessionType="engineering";c.questionnaireRevision=LayerABStudy.QuestionnaireRevision;c.Validate();return c;
    }
    static TaskEngine Engine(string stage,bool compact=true)
    {
        var e=new TaskEngine(new TaskConfig(),Config(compact));
        Call(e,"PersonalStage",stage,0,0d,0,TaskPhase.Instructions);Call(e,"BeginPersonalTrials",0d,0);
        return e;
    }
    static void Scored(TaskEngine e)
    {while(!e.Current.scored){double t=e.CurrentResult.plannedOnset;e.Tick(t+4,0);e.Tick(t+4.5,0);}}
    public static string Run()
    {
        checks=0;Directory.CreateDirectory(Folder);
        foreach(bool compact in new[]{true,false})
        foreach(string stage in new[]{"layer_a","layer_b"})
        {
            var e=Engine(stage,compact);var events=new List<TaskEvent>();e.OnEvent+=events.Add;
            Check(e.AllowsResponseRevision,"Comparison uses revisable selections");
            if(!e.Current.scored)Check(!e.Respond(true,.1,1),"Warmup ignores input");
            Scored(e);var row=e.CurrentResult;double t=row.plannedOnset;bool expected=e.Current.expectedMatch;
            Check(!e.Respond(true,row.actualOnset-.001,1),"Pre-onset rejected");
            Check(e.Respond(expected,t+.4,2),"First choice accepted");
            Check(!row.correct,"No final score before deadline");
            Check(e.Respond(!expected,t+1.2,3),"Opposite choice accepted");
            Check(e.Respond(!expected,t+1.4,4),"Same choice re-press accepted");
            Check(e.Respond(expected,t+3.999,5),"Last pre-deadline choice accepted");
            using(var action=new InputAction("Match"))
            using(var audit=new InputTimingAudit(e,action))
            {
                var latest=(Dictionary<string,InputTimingAudit.Edge>)typeof(InputTimingAudit).GetField("latest",Private).GetValue(audit);
                latest[action.name]=new InputTimingAudit.Edge{id=42,frame=5,inputTime=t+3.998};audit.LinkAcceptedResponse(action,5);
                Check(row.inputEventId==42 && Math.Abs(row.inputReactionTime-3.998)<1e-6,"Final choice has final input edge/RT");
            }
            Check(!e.Respond(!expected,t+4,6),"Exact deadline rejected even before tick");
            e.Tick(t+4,6);
            Check(row.correct && row.responseMatch==expected && !row.timeout,"Final choice scored correctly");
            Check(Math.Abs(row.reactionTime-3.999)<1e-6,"RT is final choice RT");
            Check(events.Count(x=>x.type=="response_selection")==4,"Every accepted selection audited");
            Check(events.Count(x=>x.type=="response_finalized")==1,"One finalization");
            Check(!e.Respond(!expected,t+4.2,7),"Closed response remains final");
            e.Tick(t+4.5,8);Check(e.Results.Contains(row),"Final result emitted at unchanged boundary");
            t=e.CurrentResult.plannedOnset;e.Tick(t+4,9);Check(e.CurrentResult.timeout && !e.CurrentResult.correct,"Omission preserved");
            e.Tick(t+4.5,10);t=e.CurrentResult.plannedOnset;
            e.Respond(e.Current.expectedMatch,t+.2,11);e.Respond(!e.Current.expectedMatch,t+.5,12);e.Tick(t+4,13);
            Check(!e.CurrentResult.correct,"Correct then wrong scores last wrong choice");
            e.Tick(t+4.5,14);t=e.CurrentResult.plannedOnset;e.Pause(t+.1,15);
            Check(!e.Respond(true,t+.2,16),"Paused input rejected");
            using(var recorder=new SessionRecorder(Folder+"/records",new TaskConfig(),"synthetic","engineering",comparison:true,revisableComparisonResponses:true))
            {
                recorder.Trial(row);foreach(var item in events)recorder.Event(item);recorder.Complete("synthetic_validation");
                var meta=File.ReadAllText(recorder.DirectoryPath+"/session.json");
                Check(meta.Contains(TaskEngine.ComparisonResponseRevision) && meta.Contains(SpatialTaskView.SelectionFeedbackRevision) && meta.Contains(TrialOnsetCue.SelectionCueVersion),"Metadata versions policies");
                var lines=File.ReadAllLines(recorder.DirectoryPath+"/trials.csv");var header=lines[0].Split(',');var cells=lines[1].Split(',');
                Check(cells[Array.IndexOf(header,"input_event_id")]=="42","CSV retains final edge ID");
            }
        }
        var practice=Engine("practice");Scored(practice);double onset=practice.CurrentResult.plannedOnset;
        Check(!practice.AllowsResponseRevision && practice.Respond(true,onset+.1,1) && !practice.Respond(false,onset+.2,2),"Practice stays first answer with correctness feedback");
        var calibration=Engine("calibration");Scored(calibration);onset=calibration.CurrentResult.plannedOnset;
        Check(!calibration.AllowsResponseRevision && calibration.Respond(true,onset+.1,1) && !calibration.Respond(false,onset+.2,2),"Calibration stays first answer");
        var legacy=new TaskEngine(new TaskConfig());legacy.Advance(0,0);legacy.Advance(0,0);Scored(legacy);onset=legacy.CurrentResult.plannedOnset;
        Check(legacy.Respond(true,onset+.1,1) && !legacy.Respond(false,onset+.2,2),"Stage 1 unchanged");
        Check(SpatialTaskView.FormatCountdown(3.99)=="4" && SpatialTaskView.FormatCountdown(.01)=="1" && SpatialTaskView.FormatCountdown(0)=="0","Integer timer unchanged");
        VisualAndSound();
        string report="PASS: "+checks+" response, audit, HUD, cue and highlight checks; "+(Application.isPlaying?"isolated Play Mode":"Edit Mode")+". Synthetic only; no participant/device validation.";
        File.WriteAllText(Folder+"/"+(Application.isPlaying?"play":"edit")+".txt",report);Debug.Log(report);return report;
    }
    static TMP_Text Text(Transform parent)
    {var go=new GameObject("Synthetic label",typeof(RectTransform));go.transform.SetParent(parent,false);return go.AddComponent<TextMeshProUGUI>();}
    static void VisualAndSound()
    {
        var root=new GameObject("Synthetic response fixture");root.SetActive(false);
        var owner=root.AddComponent<TaskPlatformController>();owner.enabled=false;
        var e=Engine("layer_b");Scored(e);Set(owner,"<Engine>k__BackingField",e);Set(owner,"config",e.Config);
        var view=root.AddComponent<SpatialTaskView>();owner.spatialView=view;
        view.instructions=new GameObject("Instructions",typeof(Canvas)).GetComponent<Canvas>();view.instructions.transform.SetParent(root.transform);
        view.taskCanvas=new GameObject("HUD",typeof(Canvas)).GetComponent<Canvas>();view.taskCanvas.transform.SetParent(root.transform);
        view.stage=Text(view.taskCanvas.transform);view.rule=Text(view.taskCanvas.transform);view.feedback=Text(view.taskCanvas.transform);view.timer=Text(view.taskCanvas.transform);view.timerLabel=Text(view.taskCanvas.transform);
        var cue=root.AddComponent<TrialOnsetCue>();cue.Bind(e,new TaskConfig{onsetCueEnabled=false});
        var head=new GameObject("Synthetic camera",typeof(Camera));head.transform.SetParent(root.transform);head.transform.position=new Vector3(0,0,-2);owner.head=head.transform;
        var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.transform.SetParent(root.transform);ball.transform.localScale=Vector3.one*.3f;owner.stimulus=ball.GetComponent<Renderer>();
        var highlight=root.AddComponent<GazeDebugHighlighter>();highlight.Bind(owner,new AdaptiveTrainingConfig{enabled=true,sessionType="engineering",engineeringGazeHighlight=true});
        root.SetActive(true);
        try
        {
            double t=e.CurrentResult.plannedOnset;e.Respond(!e.Current.expectedMatch,t+.5,1);view.Refresh(owner,t+.5);
            Check(view.timer.color==SpatialTaskView.CorrectColor && view.timerLabel.text=="","Incorrect choice gets same green acknowledgement without correctness");
            view.Refresh(owner,t+.81);Check(view.timer.color==LabPalette.Text,"Green flash expires");
            e.Respond(e.Current.expectedMatch,t+1,2);view.Refresh(owner,t+1.1);Check(view.timer.color==SpatialTaskView.CorrectColor,"Revision retriggers green");
            Check(cue.SelectionPlayRequests==2 && cue.PlayRequests==0,"One selection tone per accepted input independent of onset toggle");
            e.Tick(t+4,3);view.Refresh(owner,t+4);Check(view.timer.text=="0" && view.timer.color==LabPalette.Text,"Deadline neutral zero");
            Check(!e.Respond(true,t+4.1,4) && cue.SelectionPlayRequests==2,"Rejected input has no tone");
            e.Pause(t+4.2,5);Check(!root.GetComponents<AudioSource>().Any(a=>a.isPlaying),"Pause stops sounds");
            var sample=new GazeRegionSample{valid=true,task=true};double now=Time.realtimeSinceStartupAsDouble;highlight.Present(sample,now);
            Set(highlight,"lastVisualTime",now-.12);Call(highlight,"Refresh");
            var line=root.GetComponentsInChildren<LineRenderer>().Single(x=>x.positionCount==64);
            Check(highlight.TaskHighlighted && line.widthMultiplier<=.0035f && line.sharedMaterial.GetColor("_Color").a<=.381f,"Thin low-alpha hit");
            Check(line.sharedMaterial.GetFloat("_SoftEdge")==1,"Feathered shader enabled");
            view.taskCanvas.enabled=false;view.instructions.enabled=false;head.GetComponent<Camera>().fieldOfView=24;Capture(head.GetComponent<Camera>(),root,"soft-highlight.png");
            highlight.Present(default,Time.realtimeSinceStartupAsDouble);Check(highlight.TaskArmed && !highlight.TaskHighlighted && line.sharedMaterial.GetColor("_Color").a<=.101f,"Invalid input immediately returns to persistent position outline");
            var deny=root.AddComponent<GazeDebugHighlighter>();deny.Bind(owner,new AdaptiveTrainingConfig{enabled=true,sessionType="participant",engineeringGazeHighlight=true});Check(!deny.Allowed,"Participant lockout");
            var shader=Resources.Load<Shader>("GazeDebugOutline");Check(!ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"),"Shader compiles");
        }
        finally{Set(owner,"<Engine>k__BackingField",null);UnityEngine.Object.DestroyImmediate(root);}
    }
    static void Capture(Camera camera,GameObject root,string name)
    {
        foreach(var tr in root.GetComponentsInChildren<Transform>())tr.gameObject.layer=30;
        camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.12f,.12f);
        var rt=new RenderTexture(800,600,24);var tex=new Texture2D(800,600,TextureFormat.RGB24,false);var before=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,800,600),0,0);tex.Apply();File.WriteAllBytes(Folder+"/"+name,tex.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=before;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
    public static string PreparePlay()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Dirty scene must be preserved");
        SessionState.SetString("ResponseSelection.Scenes",Newtonsoft.Json.JsonConvert.SerializeObject(EditorSceneManager.GetSceneManagerSetup()));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);return "Isolated empty scene; no acquisition session.";
    }
    public static string RestoreScenes()
    {
        EditorSceneManager.RestoreSceneManagerSetup(Newtonsoft.Json.JsonConvert.DeserializeObject<SceneSetup[]>(SessionState.GetString("ResponseSelection.Scenes","")));
        SessionState.EraseString("ResponseSelection.Scenes");return "Original scenes restored";
    }
}




using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ThreeStagePlaybackValidation
{
    static double Now=>Time.realtimeSinceStartupAsDouble;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value,args);
    public static string Prepare()
    {
        Check(!Application.isPlaying,"Stop Play Mode before isolated setup");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)Check(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty,"Unsaved scene must be retained");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        SessionState.SetString("ThreeStageValidationOriginalScene",scene.path);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        return "Isolated empty scene; no production controller or acquisition client. Original scene retained for restoration.";
    }
    public static string Restore()
    {
        Check(!Application.isPlaying,"Exit Play Mode first");
        string path=SessionState.GetString("ThreeStageValidationOriginalScene","");Check(!string.IsNullOrEmpty(path),"Original scene path missing");
        EditorSceneManager.OpenScene(path,OpenSceneMode.Single);return path;
    }
    public static async Task<object> Run()
    {
        Check(Application.isPlaying,"Play Mode required");
        var ownerObject=new GameObject("Inactive fixture owner");ownerObject.SetActive(false);
        var mediaObject=new GameObject("Isolated decoded media fixture");var room=new GameObject("NeutralRoom");
        var cameraObject=new GameObject("Fixture camera");
        var rows=new List<object>();var events=new List<object>();
        try
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="FrontWall";wall.transform.SetParent(room.transform,false);wall.transform.position=new Vector3(0,1.5f,4);wall.transform.localScale=new Vector3(6,3,.2f);
            var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(0,1.5f,0);cameraObject.AddComponent<AudioListener>();
            var owner=ownerObject.AddComponent<TaskPlatformController>();owner.enabled=false;
            string output=Path.GetFullPath("Evidence/Comparison34-20261112/playback");Directory.CreateDirectory(output);
            var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText("Evidence/Comparison34-20261112/browser/fixed_exposure-config.json"));c.Validate();
            var engine=new TaskEngine(new TaskConfig(),c);
            typeof(TaskPlatformController).GetProperty("Engine").SetValue(owner,engine);typeof(TaskPlatformController).GetProperty("DataPath").SetValue(owner,output);
            engine.OnEvent+=e=>events.Add(new {e.type,e.actual,e.detail});
            Call(engine,"AdvancePersonal",Now,Time.frameCount);
            var media=mediaObject.AddComponent<AdaptiveMedia>();media.Bind(owner,c);
            double deadline=Now+40;while(!media.EngineeringReady && Now<deadline)await Task.Delay(50);
            Check(media.EngineeringReady,"Media bank validation");
            Call(engine,"AdvancePersonal",Now,Time.frameCount);
            double origin=engine.CurrentResult.plannedOnset,onRequested=-1,firstDecoded=-1;int offLeaks=0,onGaps=0;bool wasVisible=false;
            while(engine.Phase==TaskPhase.Running && Now-origin<163)
            {
                engine.Tick(Now,Time.frameCount);media.Tick(Now);
                int trial=engine.TrialIndex+1;
                if(engine.Adaptive.Level==0 && media.IsVideoVisible)offLeaks++;
                if(engine.Adaptive.Level==1 && onRequested<0)onRequested=Now;
                if(engine.Adaptive.Level==1 && media.IsVideoVisible && firstDecoded<0){firstDecoded=Now;Debug.Log("Fixed exposure decoder: first frame at trial "+trial+" after "+(firstDecoded-onRequested).ToString("F3")+" s.");}
                if(engine.Adaptive.Level==1 && wasVisible && !media.IsVideoVisible && engine.Phase==TaskPhase.Running)onGaps++;
                if(engine.Adaptive.Level==1 && media.IsVideoVisible)wasVisible=true;
                rows.Add(new {time=Now-origin,trial,level=engine.Adaptive.Level,visible=media.IsVideoVisible,audio=media.IsAudioPlaying});
                if(engine.Phase==TaskPhase.Paused)throw new Exception("Unexpected playback pause");
                await Task.Delay(40);
            }
            Check(engine.ProtocolCompletedTrials==34 && engine.Phase==TaskPhase.Rest,"34-trial completion");
            Check(offLeaks==0 && firstDecoded>=0 && onGaps==0,"No video in 2 warmups and 16 scored OFF; continuous decoded playback in 16 scored ON");
            Check(!media.IsVideoVisible && !media.IsAudioPlaying,"Stage end hides and mutes playback");
            Call(engine,"AdvancePersonal",Now,Time.frameCount);Check(engine.Phase==TaskPhase.Complete,"Standalone completion");
            var result=new {scope="Isolated Unity Editor Play Mode; synthetic timing/behavior, real local video decoder; no participant or headset evidence",trials=34,offLeaks,onGaps,firstDecodedDelay=firstDecoded-onRequested,decodedSeconds=origin+153-firstDecoded,rows,events};
            File.WriteAllText(Path.Combine(output,"validation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            Debug.Log("PASS: fixed exposure full real-time 34 trials, 2 warmups plus 16 scored OFF / 16 scored ON, no sampled decoded-video gaps, end stops video/audio.");
            return new {trials=34,offLeaks,onGaps,firstDecodedDelay=firstDecoded-onRequested};
        }
        finally{UnityEngine.Object.DestroyImmediate(mediaObject);UnityEngine.Object.DestroyImmediate(ownerObject);UnityEngine.Object.DestroyImmediate(room);UnityEngine.Object.DestroyImmediate(cameraObject);}
    }
}

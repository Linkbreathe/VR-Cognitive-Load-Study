using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class FilmPoolPlaybackValidation
{
    static double Now=>Time.realtimeSinceStartupAsDouble;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value,args);
    public static string Prepare()
    {
        Check(!Application.isPlaying,"Stop Play Mode before isolated setup");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)Check(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty,"Unsaved scene must be retained");
        SessionState.SetString("FilmPoolOriginalScene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);return "Isolated scene prepared.";
    }
    public static string Restore()
    {
        Check(!Application.isPlaying,"Exit Play Mode first");
        string path=SessionState.GetString("FilmPoolOriginalScene","");Check(!string.IsNullOrEmpty(path),"Original scene missing");
        EditorSceneManager.OpenScene(path,OpenSceneMode.Single);return path;
    }
    public static async Task<object> Run()
    {
        Check(Application.isPlaying,"Play Mode required");
        var room=new GameObject("NeutralRoom");var cameraObject=new GameObject("Film pool fixture camera");
        var results=new List<object>();var selected=new HashSet<string>();
        string root=Path.GetFullPath("Evidence/FilmLibrary-20261112/playback/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="FrontWall";wall.transform.SetParent(room.transform,false);wall.transform.position=new Vector3(0,1.5f,4);wall.transform.localScale=new Vector3(6,3,.2f);
            cameraObject.AddComponent<Camera>().transform.position=new Vector3(0,1.5f,0);cameraObject.AddComponent<AudioListener>();
            for(int i=0;i<6;i++)
            {
                var ownerObject=new GameObject("Inactive isolated owner");ownerObject.SetActive(false);var mediaObject=new GameObject("Film pool player");
                try
                {
                    string folder=Path.Combine(root,"session-"+i);Directory.CreateDirectory(folder);
                    var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText("Evidence/Comparison34-20261112/browser/fixed_exposure-config.json"));
                    c.participantCode="SYNTHETIC_FILM_POOL";c.testStorageFolder=root;c.testSessionId="fixture-"+i;c.seed=42;
                    c.comparisonStage=new[]{"fixed_exposure","layer_a","layer_b"}[i%3];c.Validate();
                    var engine=new TaskEngine(new TaskConfig(),c);var events=new List<TaskEvent>();engine.OnEvent+=e=>events.Add(e);
                    Call(engine,"PersonalStage",c.comparisonStage,1,Now,0,TaskPhase.Instructions);
                    Call(engine,"BeginPersonalTrials",Now,0);
                    // This decoder fixture requests ON directly; the separate full Fixed run verifies its real OFF/ON schedule.
                    Call(engine.Adaptive,"SetPersonalLevel",1);
                    var owner=ownerObject.AddComponent<TaskPlatformController>();owner.enabled=false;
                    typeof(TaskPlatformController).GetProperty("Engine").SetValue(owner,engine);typeof(TaskPlatformController).GetProperty("DataPath").SetValue(owner,folder);
                    var media=mediaObject.AddComponent<AdaptiveMedia>();media.Bind(owner,c);
                    double deadline=Now+40;while(!media.EngineeringReady && Now<deadline)await Task.Delay(50);
                    Check(media.EngineeringReady,"Installed bank validation");
                    var reservation=JsonUtility.FromJson<ContinuousMediaReservation>(File.ReadAllText(Path.Combine(folder,"continuous-media-selection.json")));
                    Check(selected.Add(reservation.playlistId),"Six actual stage assignments must be distinct");
                    var player=media.Screen.GetComponent<VideoPlayer>();deadline=Now+8;
                    while(!media.IsVideoVisible && Now<deadline){media.Tick(Now);await Task.Delay(25);}
                    Check(media.IsVideoVisible && media.IsAudioPlaying,"First decoded video and playing audio");
                    // Seek only in this isolated fixture to exercise a real encoded cut promptly.
                    player.time=18.5;deadline=Now+10;
                    while(player.time<21 && Now<deadline){media.Tick(Now);await Task.Delay(25);}
                    Check(player.time>=21 && media.IsVideoVisible && media.IsAudioPlaying,"Decoded cut crossed");
                    Check(events.Count(e=>e.type=="adaptive_media_segment_changed")>=2,"Source boundary provenance logged");
                    Call(engine.Adaptive,"SetPersonalLevel",0);Call(media,"OnEvent",new TaskEvent{type="adaptive_level_changed",actual=Now});
                    Check(!media.IsVideoVisible && !media.IsAudioPlaying,"OFF hides and pauses");
                    double cursor=player.time;await Task.Delay(350);Check(Math.Abs(cursor-player.time)<.1,"OFF retains cursor");
                    Call(engine.Adaptive,"SetPersonalLevel",1);deadline=Now+6;
                    while((!media.IsVideoVisible || player.time<cursor+.4) && Now<deadline){media.Tick(Now);await Task.Delay(25);}
                    Check(media.IsVideoVisible && player.time>=cursor+.4,"ON resumes same content");
                    Call(media,"OnEvent",new TaskEvent{type="session_complete",actual=Now});
                    Check(!media.IsVideoVisible && !media.IsAudioPlaying,"Completion stops playback");
                    Check(events.Any(e=>e.type=="continuous_media_playlist" && e.detail.Contains("sourceStartSeconds")),"Core event archive includes source recipe");
                    results.Add(new {stage=c.comparisonStage,reservation.playlistId,cursor,events});
                    Debug.Log("PASS film pool decoder "+reservation.playlistId+": distinct allocation, boundary, OFF, resume, completion.");
                }
                finally{UnityEngine.Object.DestroyImmediate(mediaObject);UnityEngine.Object.DestroyImmediate(ownerObject);}
            }
            File.WriteAllText(Path.Combine(root,"validation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{scope="Isolated Editor; synthetic control, real decoder; no participant/device evidence",results},Newtonsoft.Json.Formatting.Indented));
            return new{playlists=selected.Count,output=root};
        }
        finally{UnityEngine.Object.DestroyImmediate(room);UnityEngine.Object.DestroyImmediate(cameraObject);}
    }
}

using System;
using System.IO;
using EvidenceVR;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class GazeConsoleValidation
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static bool Near(float a,float b)=>Mathf.Abs(a-b)<.0001f;
    public static string Run()
    {
        checks=0;
        Check(!Application.isPlaying,"Use isolated Editor validation, outside task sessions");
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var go=new GameObject("Synthetic gaze reference plane");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
            var screen=go.transform;screen.position=new Vector3(0,2,4);screen.localScale=new Vector3(3.2f,1.8f,1);
            var origin=new Vector3(0,1,0);
            Check(GazeConsoleProjection.Project(origin,screen.position-origin,screen,out var p)&&Near(p.x,0)&&Near(p.y,0),"Center projection");
            Check(GazeConsoleProjection.Project(origin,new Vector3(1,3,4)-origin,screen,out p)&&Near(p.x,1)&&Near(p.y,1),"Coordinates remain meters, right/up positive");
            Check(!GazeConsoleProjection.Project(origin,Vector3.right,screen,out p),"Parallel ray rejected");
            Check(!GazeConsoleProjection.Project(origin,Vector3.back,screen,out p),"Backward ray rejected");
            Check(!GazeConsoleProjection.Project(origin,Vector3.zero,screen,out p),"Zero direction rejected");
            Check(!GazeConsoleProjection.Project(origin,new Vector3(float.NaN,0,1),screen,out p),"Nonfinite ray rejected");
            Check(!GazeConsoleProjection.Project(origin,Vector3.forward,null,out p),"Missing plane rejected");
            var sphere=new Bounds(new Vector3(0,1,2),Vector3.one*.2f);
            var tray=new Bounds(new Vector3(0,.8f,2),new Vector3(.6f,.1f,.4f));
            var task=GazeRegions.TaskBounds(sphere,tray);
            var tracked=GazeRegions.Classify(true,origin,screen.position-origin,task,false,screen,true);
            var sample=GazeConsoleProjection.Sample(1,2,tracked,screen,false,true);
            Check(sample.valid&&sample.projected&&sample.target=="movie","Existing movie classification retained");
            var invalid=GazeConsoleProjection.Sample(2,3,new GazeRegionSample(),screen,true,true);
            Check(!invalid.valid&&!invalid.projected&&invalid.target=="invalid","Invalid eyes do not become center gaze");
            var rect=GazeConsoleProjection.ProjectBounds(sphere,origin,screen);
            Check(rect.available&&rect.x<0&&rect.x+rect.width>0&&rect.width>.2f,"Perspective bounds enlarge foreground object");
            var layout=GazeConsoleProjection.Layout(screen,origin,sphere,tray,task);
            Check(layout.available&&Near(layout.movie.width,3.2f)&&Near(layout.movie.height,1.8f),"Physical screen dimensions");
            Check(layout.minY<=layout.task.y&&layout.maxY>=layout.task.y+layout.task.height,"Task remains inside layout");
            screen.rotation=Quaternion.Euler(0,35,0);
            var target=screen.position+screen.right*.7f+screen.up*.3f;
            Check(GazeConsoleProjection.Project(origin,target-origin,screen,out p)&&Near(p.x,.7f)&&Near(p.y,.3f),"Rotated screen basis");
            var overview=new ConsoleGazeOverview{now=3,phase="Running",layout=layout,samples=new[]{sample,invalid}};
            var json=JsonUtility.ToJson(overview);
            var restored=JsonUtility.FromJson<ConsoleGazeOverview>(json);
            Check(restored.source=="tracked_eyes"&&restored.revision==GazeConsoleProjection.Revision,"Versioned transport identity");
            Check(restored.samples.Length==2&&!restored.samples[1].projected&&restored.samples[0].target=="movie","JSON preserves invalidity and classification");
            Directory.CreateDirectory("Evidence/GazeConsole");
            File.WriteAllText("Evidence/GazeConsole/synthetic-projection.json",json);
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        string result="GAZE CONSOLE PASS: "+checks+" isolated Editor assertions; synthetic geometry only; no task session or hardware acceptance.";
        File.WriteAllText("Evidence/GazeConsole/unity-validation.txt",result);
        Debug.Log(result);return result;
    }
}

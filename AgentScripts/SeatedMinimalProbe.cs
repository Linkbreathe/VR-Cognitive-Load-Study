using System;
using System.IO;
using System.Reflection;
using EvidenceVR;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class SeatedMinimalProbe
{
    public static string Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var view=c.spatialView;
        var engineProperty=typeof(TaskPlatformController).GetProperty("Engine");
        var render=typeof(TaskPlatformController).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic);
        var original=c.Engine;bool enabled=c.enabled;var anchorPosition=c.taskAnchor.position;
        const string folder="Evidence/Visuals-v1.6.1";Directory.CreateDirectory(folder);int checks=0;
        try
        {
            c.enabled=false;
            foreach(float eyeHeight in new[]{1.05f,1.2f,1.65f})
            {
                var position=c.taskAnchor.position;position.y=eyeHeight;c.taskAnchor.position=position;view.PlaceSupports(c.taskAnchor);
                Check(Mathf.Abs(view.bench.Find("WorkSurface").GetComponent<Renderer>().bounds.max.y-.65f)<.001f,"Fixed tabletop");
                Check(Mathf.Abs(view.leftLeg.GetComponent<Renderer>().bounds.min.y)<.001f && Mathf.Abs(view.rightLeg.GetComponent<Renderer>().bounds.min.y)<.001f,"Grounded supports");
            }
            var seated=c.taskAnchor.position;seated.y=1.2f;c.taskAnchor.position=seated;view.PlaceSupports(c.taskAnchor);
            var e=new TaskEngine(c.SessionConfig);engineProperty.SetValue(c,e);
            e.Advance(0,0);e.Advance(0,1);e.Tick(4.5,2);e.Tick(9,3);
            Show(9,"practice-neutral");Check(view.timer.color==LabPalette.Text,"Neutral timer");
            e.Respond(e.Current.expectedMatch,9.2,4);Show(9.2,"practice-correct");
            Check(view.timer.color==SpatialTaskView.CorrectColor && view.timerLabel.text=="Correct","Green correct feedback");
            e.Tick(12.999,5);Show(12.999,"visible-before-deadline");Check(c.stimulus.enabled,"Visible until four seconds after early answer");
            e.Tick(13,6);Show(13,"closed-gap");Check(!c.stimulus.enabled && view.timer.text=="0","Hidden at four-second deadline");
            e.Tick(13.5,7);Show(13.5,"next-trial");Check(view.timer.color==LabPalette.Text && view.timerLabel.text=="","Feedback reset");
            e.Respond(!e.Current.expectedMatch,13.6,8);Show(13.6,"practice-incorrect");
            Check(view.timer.color==SpatialTaskView.IncorrectColor && view.timerLabel.text=="Incorrect","Red incorrect feedback");
            e.Pause(13.7,9);Show(20,"paused");Check(!c.stimulus.enabled && !view.taskCanvas.enabled,"Paused view hidden");
            e.Resume(23.7,10);e.Tick(27.499,11);Show(27.499,"resumed-visible");Check(c.stimulus.enabled,"Visibility freezes through pause");
            e.Tick(27.5,12);Show(27.5,"resumed-deadline");Check(!c.stimulus.enabled,"Pause-adjusted offset");
            double clock=27.5;int frame=13;
            while(e.Block<2)
            {if(e.Phase==TaskPhase.Instructions)e.Advance(clock,frame++);else{clock+=4.5;e.Tick(clock,frame++);}}
            e.Advance(clock,frame++);e.Tick(clock+=4.5,frame++);e.Tick(clock+=4.5,frame++);
            e.Respond(!e.Current.expectedMatch,clock+.1,frame++);Show(clock+.1,"formal-neutral-after-error");
            Check(view.timer.color==LabPalette.Text && view.timerLabel.text=="","No formal error feedback");
            e.Tick(clock+=4.5,frame++);e.Respond(e.Current.expectedMatch,clock+.1,frame++);Show(clock+.1,"formal-neutral-after-correct");
            Check(view.timer.color==LabPalette.Text && view.timerLabel.text=="","No formal correct feedback");
            int textCount=0;foreach(var text in view.taskCanvas.GetComponentsInChildren<TMP_Text>())if(text.isActiveAndEnabled)textCount++;
            Check(textCount==4,"Only four active running text elements");
            Check(!view.taskCanvas.transform.Find("StageSign").GetComponent<RoundedSurface>().enabled && !view.taskCanvas.transform.Find("CountdownCard").GetComponent<RoundedSurface>().enabled,"No large running panels");
            var report="PASS: "+checks+" checks; 0.65 m tabletop at three calibrated eye heights, grounded legs, sphere visible through 3.999 s including early response, hidden at 4 s, pause-adjusted offset, green/red practice feedback, neutral reset and no formal correctness feedback. Four active HUD text elements; screenshots inspected separately.\nIsolated synthetic engine; not participant or headset evidence.\n";
            File.WriteAllText("Evidence/seated-minimal-v1.6.1.txt",report);Debug.Log(report);return report;
            void Show(double time,string name)
            {
                render.Invoke(c,null);view.Refresh(c,time);Canvas.ForceUpdateCanvases();
                foreach(var text in view.taskCanvas.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Text overflow: "+text.name);}
                Capture(c,folder+"/"+name+".png");
            }
            void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        }
        finally
        {
            engineProperty.SetValue(c,original);c.enabled=enabled;c.taskAnchor.position=anchorPosition;view.PlaceSupports(c.taskAnchor);render.Invoke(c,null);
        }
    }
    static void Capture(TaskPlatformController c,string file)
    {
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        var data=camera.GetComponent<UniversalAdditionalCameraData>();bool xr=data.allowXRRendering;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            data.allowXRRendering=false;camera.transform.SetPositionAndRotation(c.taskAnchor.position-c.taskAnchor.forward*c.SessionConfig.distance,c.taskAnchor.rotation);
            camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());
        }
        finally{camera.targetTexture=previous;data.allowXRRendering=xr;camera.transform.SetPositionAndRotation(position,rotation);RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
}

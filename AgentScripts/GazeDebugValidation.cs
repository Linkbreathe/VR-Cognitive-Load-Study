using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using TMPro;

public static class GazeDebugValidation
{
    const string Folder="Evidence/GazeDebug";
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    public static string Model()
    {
        checks=0;Directory.CreateDirectory(Folder);
        var go=new GameObject("Synthetic gaze geometry");var t=go.transform;t.position=new Vector3(0,0,3);t.localScale=new Vector3(3.2f,1.8f,1);
        try
        {
            Vector3 origin=Vector3.zero;
            var sphere=new Bounds(new Vector3(0,0,1.5f),Vector3.one*.16f);
            var tray=new Bounds(new Vector3(0,-.19f,1.5f),new Vector3(.30f,.05f,.30f));
            var ball=GazeRegions.TaskBounds(sphere,tray);
            Func<Vector3,bool,bool,GazeRegionSample> resolve=(direction,visible,video)=>GazeRegions.Classify(true,origin,direction,ball,visible,t,video);
            var s=resolve(Vector3.forward,true,true);Check(s.valid && s.task && !s.video,"Task takes priority over screen");
            Check(resolve(new Vector3(.8f,0,3),true,true).video,"Screen intersection inside rectangle");
            Check(resolve(new Vector3(2,0,3),true,true).Target=="other","Outside rectangle");
            Check(resolve(new Vector3(0,1,3),false,true).Target=="other","Outside screen height");
            Check(resolve(Vector3.back,true,true).Target=="other","No hits behind eye");
            Check(resolve(Vector3.forward,false,false).Target=="other","Hidden targets excluded");
            Check(resolve(Vector3.forward,false,true).video,"Hidden ball does not mask visible movie");
            Check(resolve(new Vector3(.8f,0,3),true,false).Target=="other","Inactive movie excluded");
            Check(!resolve(Vector3.zero,true,true).valid,"Zero direction invalid");
            Check(!resolve(new Vector3(float.NaN,0,1),true,true).valid,"Nonfinite direction invalid");
            Check(!GazeRegions.Classify(false,origin,Vector3.forward,ball,true,t,true).valid,"Tracking lost invalid");
            Check(!GazeRegions.Classify(true,new Vector3(float.PositiveInfinity,0,0),Vector3.forward,ball,true,t,true).valid,"Nonfinite position invalid");
            Check(!GazeRegions.ValidRotation(new Quaternion(0,0,0,0)),"Zero quaternion invalid");
            Check(GazeRegions.ValidRotation(Quaternion.identity),"Identity quaternion valid");
            Check(resolve(tray.center,true,false).task,"Tray center counts as task focus");
            Check(resolve(new Vector3(0,-.1f,1.5f),true,false).task,"Gap between sphere and tray counts");
            Check(resolve(new Vector3(.18f,-.19f,1.5f),true,false).task,"Margin around tray counts");
            Check(!resolve(new Vector3(.30f,-.19f,1.5f),true,false).task,"Outside area excluded");
            Check(!resolve(tray.center,false,false).task,"Inactive task area excluded");
            Check(!GazeRegions.Classify(true,ball.center,Vector3.forward,ball,true,null,false).task,"Origin inside task area excluded");
            Check(!GazeRegions.Classify(true,Vector3.zero,Vector3.back,ball,true,null,false).task,"Task area behind eye excluded");
            Check(!GazeRegions.Classify(true,Vector3.zero,Vector3.forward,new Bounds(Vector3.forward,new Vector3(float.NaN,1,1)),true,null,false).task,"Invalid task bounds excluded");
            for(int i=0;i<=1000;i++)
            {
                float angle=i/100f;
                var r=resolve(Quaternion.Euler(0,angle,0)*Vector3.forward,true,true);
                float boundary=Mathf.Atan2(.19f,1.31f)*Mathf.Rad2Deg;
                if(Mathf.Abs(angle-boundary)>.001f)Check(r.task==(angle<boundary),"Sphere/tray area horizontal boundary");
                Check(!(r.task && r.video),"Regions do not double count");
            }
            foreach(string kind in new[]{"engineering","participant","editor-test","synthetic"})foreach(bool requested in new[]{false,true})foreach(bool enabled in new[]{false,true})
                Check(GazeDebugHighlighter.IsAllowed(new AdaptiveTrainingConfig{sessionType=kind,engineeringGazeHighlight=requested,enabled=enabled})==
                      (kind=="engineering" && requested && enabled),"Engineering permission matrix");
            var shader=Resources.Load<Shader>("GazeDebugOutline");Check(shader!=null && shader.isSupported,"Outline shader available");
            var messages=UnityEditor.ShaderUtil.GetShaderMessages(shader);Check(!messages.Any(m=>m.severity.ToString()=="Error"),"No shader compiler errors");
            string report="PASS: "+checks+" synthetic geometry/gating/shader checks. Active sphere/tray area, margin and gap inclusion, invalid eye data, no overlap, participant lockout. No real eye device validation.";
            File.WriteAllText(Folder+"/model.txt",report);Debug.Log(report);return report;
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    public static string Setup()
    {
        Check(!Application.isPlaying,"Stop before setup");Directory.CreateDirectory(Folder);
        string target=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
        File.Copy(target,Folder+"/editor-config-before-test.json",true);
        return EvidenceVR.Editor.AdaptiveTrainingSetup.EnableGazeHighlight();
    }
    public static string Restore()
    {
        Check(!Application.isPlaying,"Stop before restore");
        File.Copy(Folder+"/editor-config-before-test.json",Path.Combine(Application.persistentDataPath,"adaptive-training-config.json"),true);
        return "User configuration restored byte-for-byte; debug defaults off.";
    }
    public static async Task<string> Live()
    {
        checks=0;var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var e=c.Engine;
        GameObject fixtureObject=null,deniedObject=null,disabledObject=null;TMP_Text label=null;
        try
        {
            await Task.Delay(1400);
            Check(e.Adaptive.Config.engineeringGazeHighlight && c.AdaptiveRuntime.GazeDebug.Allowed,"Engineering switch active");
            Check(!c.AdaptiveRuntime.GazeDebug.TaskHighlighted && !c.AdaptiveRuntime.GazeDebug.VideoHighlighted,"Preparation not highlighted");
            fixtureObject=new GameObject("Synthetic gaze visual fixture");var fixture=fixtureObject.AddComponent<GazeDebugHighlighter>();fixture.Bind(c,e.Adaptive.Config);
            deniedObject=new GameObject("Participant lockout fixture");var denied=deniedObject.AddComponent<GazeDebugHighlighter>();
            denied.Bind(c,new AdaptiveTrainingConfig{enabled=true,sessionType="participant",engineeringGazeHighlight=true});
            disabledObject=new GameObject("Disabled engineering fixture");var disabled=disabledObject.AddComponent<GazeDebugHighlighter>();
            disabled.Bind(c,new AdaptiveTrainingConfig{enabled=true,sessionType="engineering",engineeringGazeHighlight=false});
            Check(!denied.Allowed && denied.GetComponentsInChildren<LineRenderer>().Length==0,"Participant creates no outline renderers");
            Check(!disabled.Allowed && disabled.GetComponentsInChildren<LineRenderer>().Length==0,"Disabled default creates no outline renderers");
            label=UnityEngine.Object.Instantiate(c.spatialView.stage,c.spatialView.taskCanvas.transform);
            label.name="Synthetic gaze validation label";label.rectTransform.anchoredPosition=new Vector2(0,650);label.rectTransform.sizeDelta=new Vector2(1260,40);
            label.fontSize=20;label.text="SYNTHETIC GAZE VISUAL CHECK - NOT TRACKED EYES";label.textWrappingMode=TextWrappingModes.NoWrap;
            e.Note("synthetic_gaze_visual_fixture","Separate outline fixture only; production gaze CSV remains real input / invalid. Synthetic rays are not injected into recorder or controller.",Time.realtimeSinceStartupAsDouble,Time.frameCount);
            e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
            await Task.Delay(250);
            Func<bool,GazeRegionSample> sample=video=>
            {
                var target=video?c.AdaptiveRuntime.Media.Screen.position+c.AdaptiveRuntime.Media.Screen.right*.9f:c.stimulus.transform.position;
                var tray=c.spatialView.bench.Find("StimulusPlinth").GetComponent<Renderer>();
                return GazeRegions.Classify(true,c.head.position,target-c.head.position,GazeRegions.TaskBounds(c.stimulus.bounds,tray.bounds),e.StimulusVisible,
                    c.AdaptiveRuntime.Media.Screen,c.AdaptiveRuntime.Media.IsVideoVisible);
            };
            var ball=sample(false);Color original=c.stimulus.sharedMaterial.color;
            fixture.Present(ball,Time.realtimeSinceStartupAsDouble);
            Check(fixture.TaskHighlighted && !fixture.VideoHighlighted,"Synthetic ball ray lights only outline");
            Check(c.stimulus.sharedMaterial.color==original,"Stimulus color unchanged");
            denied.Present(ball,Time.realtimeSinceStartupAsDouble);disabled.Present(ball,Time.realtimeSinceStartupAsDouble);
            Check(!denied.TaskHighlighted && !denied.VideoHighlighted && !disabled.TaskHighlighted,"Denied overlays remain dark with valid ray");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/sphere-outline.png"));await Task.Delay(45);
            fixture.Present(default,Time.realtimeSinceStartupAsDouble);Check(!fixture.TaskHighlighted && !fixture.VideoHighlighted,"Invalid ray clears immediately");
            fixture.Present(ball,Time.realtimeSinceStartupAsDouble);await Task.Delay(180);Check(!fixture.TaskHighlighted,"Stale sample clears after 100 ms");
            double deadline=Time.realtimeSinceStartupAsDouble+16;
            while(!c.AdaptiveRuntime.Media.IsVideoVisible && Time.realtimeSinceStartupAsDouble<deadline)
            {
                await Task.Delay(20);
                if(e.Current.scored && !e.CurrentResult.responded && Time.realtimeSinceStartupAsDouble-e.CurrentResult.actualOnset>.6)
                    e.Respond(e.Current.expectedMatch,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            }
            Check(c.AdaptiveRuntime.Media.IsVideoVisible,"Actual practice movie decoded");
            var movie=sample(true);Check(movie.video && !movie.task,"Synthetic movie ray classified");
            var renderer=c.AdaptiveRuntime.Media.Screen.GetComponent<Renderer>();Color movieColor=renderer.sharedMaterial.color;
            fixture.Present(movie,Time.realtimeSinceStartupAsDouble);
            Check(fixture.VideoHighlighted && !fixture.TaskHighlighted,"Only movie border shown");
            Check(renderer.sharedMaterial.color==movieColor,"Movie tint unchanged");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/movie-border.png"));await Task.Delay(45);
            fixture.Present(default,Time.realtimeSinceStartupAsDouble);Check(!fixture.VideoHighlighted,"Invalid gaze removes movie border");
            Check(!c.AdaptiveRuntime.GazeDebug.TaskHighlighted && !c.AdaptiveRuntime.GazeDebug.VideoHighlighted,"Production overlay has no valid eyes, remains dark");
            fixture.Present(movie,Time.realtimeSinceStartupAsDouble);e.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_gaze_check");
            await Task.Delay(40);Check(!fixture.TaskHighlighted && !fixture.VideoHighlighted,"Pause clears overlay");
            Check(!c.AdaptiveRuntime.Media.IsVideoVisible,"Paused media no longer a visible target");
            fixture.Present(movie,Time.realtimeSinceStartupAsDouble);Check(!fixture.VideoHighlighted,"Valid ray cannot light paused task");
            string metadata=File.ReadAllText(Path.Combine(c.DataPath,"session.json"));
            Check(metadata.Contains(GazeRegions.Revision) && metadata.Contains(GazeDebugHighlighter.Revision) && metadata.Contains("\"gazeDebugEnabled\": true"),"Debug and ROI revisions saved");
            string result="PASS: "+checks+" live Editor checks. Synthetic geometry exercises separate outlines; participant/off gates, stale/invalid/pause removal, actual film decode, unchanged colors and metadata verified. Production eye input remained invalid. Session: "+c.DataPath;
            File.WriteAllText(Folder+"/live.txt",result);Debug.Log(result);return result;
        }
        catch(Exception ex){e.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_gaze_check_failed");File.WriteAllText(Folder+"/live.txt","FAIL: "+ex+"\nSession: "+c.DataPath);throw;}
        finally
        {
            if(fixtureObject!=null)UnityEngine.Object.Destroy(fixtureObject);
            if(deniedObject!=null)UnityEngine.Object.Destroy(deniedObject);
            if(disabledObject!=null)UnityEngine.Object.Destroy(disabledObject);
            if(label!=null)UnityEngine.Object.Destroy(label.gameObject);
        }
    }
}

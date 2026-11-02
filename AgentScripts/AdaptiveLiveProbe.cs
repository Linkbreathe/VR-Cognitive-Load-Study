using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;

public static class AdaptiveLiveProbe
{
    static void Check(bool value,string why){if(!value)throw new Exception(why);}
    public static string Setup()
    {
        Check(!Application.isPlaying,"Stop Play Mode before installing a test override.");
        string directory=Path.GetFullPath("Evidence/Stage2/OverrideBackup");Directory.CreateDirectory(directory);
        string target=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
        File.WriteAllText(Path.Combine(directory,"target.txt"),target);
        if(File.Exists(target))File.Copy(target,Path.Combine(directory,"original.json"),false);
        var c=new AdaptiveTrainingConfig{enabled=true,mode="SHADOW",sessionType="engineering",participantCode="synthetic_live_probe",includeTransferTests=false,mediaManifest="stage2-synthetic-media.json"};
        File.WriteAllText(target,JsonUtility.ToJson(c,true));
        return Application.persistentDataPath;
    }
    public static string Restore()
    {
        Check(!Application.isPlaying,"Stop Play Mode before restoring config.");
        string directory=Path.GetFullPath("Evidence/Stage2/OverrideBackup");
        string target=File.ReadAllText(Path.Combine(directory,"target.txt"));
        string expected=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
        Check(target==expected,"Unexpected override path");
        string original=Path.Combine(directory,"original.json");
        if(File.Exists(original))File.Copy(original,target,true);else if(File.Exists(target))File.Delete(target);
        return "Original runtime mode restored.";
    }
    public static string ActiveSetup()
    {
        var result=Setup();var path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
        var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path));c.mode="ACTIVE";
        File.WriteAllText(path,JsonUtility.ToJson(c,true));return result;
    }
    public static async Task<string> ActiveBoundary()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var e=c.Engine;
        Check(e.Adaptive.Config.mode=="ACTIVE","Active fixture config");
        c.enabled=false;await Task.Delay(800);
        double now=Time.realtimeSinceStartupAsDouble;int frame=Time.frameCount;int applied=0;
        e.OnEvent+=item=>{if(item.type=="adaptive_execution" && item.detail.StartsWith("action=UP"))applied++;};
        e.Note("synthetic_controller_probe","Model clock advanced programmatically; HTTP boundary requests are real. No participant data or exposure efficacy.",now,frame);
        while(e.Phase!=TaskPhase.Complete && now<10000)
        {
            if(e.Phase==TaskPhase.Preparation||e.Phase==TaskPhase.Instructions)e.Advance(now,frame++);
            if(e.Phase!=TaskPhase.Running)continue;
            double start=e.CurrentResult.plannedOnset;
            if(e.Current.scored)e.Respond(e.Current.expectedMatch,start+.5,frame++);
            e.Tick(start+4,frame++);
            if(e.IsEvaluationClosing)
            {
                await Task.Delay(150);
                if(e.Adaptive.Stage=="training")Check(e.Adaptive.Pending!=null,"Real HTTP decision must arrive before simulated boundary");
            }
            now=start+4.5;e.Tick(now,frame++);
        }
        Check(e.Phase==TaskPhase.Complete && e.Adaptive.Outcome=="candidate_mastery","Active controller completes bounded protocol");
        // Final UP becomes a protocol completion REST in the execution summary, so count the level-one upgrade separately.
        Check(applied>=1 && e.Adaptive.TrainingCount==4,"Two valid passes at each level");
        Check(!c.spatialView.timer.transform.parent.GetComponent<RectTransform>().anchoredPosition.Equals(new Vector2(220,0)),"Countdown moved away from video");
        c.AdaptiveRuntime.RecordRating();c.AdaptiveRuntime.RecordRating();
        c.enabled=true;
        string report="PASS: real local Python HTTP policy -> Unity ACTIVE boundary execution; 2 baseline + 4 training blocks, two consecutive passes per level, final candidate mastery, separately recorded requests/results and end ratings. Simulated model clock/answers, no real physiology. Session: "+c.DataPath;
        File.WriteAllText("Evidence/Stage2/active-controller-probe.txt",report);Debug.Log(report);return report;
    }
    public static async Task<string> Run()
    {
        Check(Application.isPlaying,"Run in Play Mode.");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(c.Engine.Adaptive!=null,"Adaptive mode not active");
        await Task.Delay(1000);
        Check(c.AdaptiveRuntime.Media.Ready,"Synthetic paired media not ready");
        var e=c.Engine;double now=Time.realtimeSinceStartupAsDouble;
        e.Note("synthetic_live_probe","Programmatic input and test-pattern media only; not participant or headset evidence",now,Time.frameCount);
        e.Advance(now,Time.frameCount);e.Advance(now,Time.frameCount);
        int frameBefore=Time.frameCount;double began=now;bool answered=false;
        while(Time.realtimeSinceStartupAsDouble-began<14)
        {
            await Task.Delay(25);now=Time.realtimeSinceStartupAsDouble;
            Check(e.Phase==TaskPhase.Running,"Unexpected pause or runtime failure");
            if(e.Current.scored && !answered && now-e.CurrentResult.actualOnset>3.1)
                answered=e.Respond(e.Current.expectedMatch,now,Time.frameCount);
        }
        Check(Time.frameCount>frameBefore+100,"Player loop must advance");Check(answered,"Late valid input accepted");
        Check(c.spatialView.stage.text.Contains("2-back"),"Adaptive HUD");
        Check(Math.Abs(c.AdaptiveRuntime.Media.Screen.position.x)<.001f,"Centered front-wall geometry");
        string report="PASS: live Editor frames advanced; fixed 2-back HUD, paired media preload, late response, four-second task and centered front-wall screen. Session: "+c.DataPath;
        File.WriteAllText("Evidence/Stage2/live-task-probe.txt",report);Debug.Log(report);return report;
    }
    public static async Task<string> FilmsAndRecordedReplay(bool filmsOnly=false)
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        await Task.Delay(1500);
        var media=c.AdaptiveRuntime.Media;
        var loaded=(System.Collections.IDictionary)typeof(AdaptiveMedia).GetField("clips",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(media);
        Check(loaded.Count==10,"All ten downloaded film pairs loaded with matching hashes");
        Check(!media.Ready,"Headset listening review remains explicitly pending");
        var e=c.Engine;double now=Time.realtimeSinceStartupAsDouble;int frame=Time.frameCount;
        e.Advance(now,frame);e.Advance(now,frame);c.enabled=false;
        while(e.Adaptive.Stage!="training" && now<10000)
        {
            if(e.Phase==TaskPhase.Instructions)e.Advance(now,frame);
            if(e.Phase==TaskPhase.Running && e.Current.scored)e.Respond(e.Current.expectedMatch,e.CurrentResult.plannedOnset+.5,frame);
            now+=4.5;e.Tick(now,frame++);
        }
        Check(e.Adaptive.Stage=="training","Training reached with programmatic responses");
        var origin=typeof(AdaptiveMedia).GetField("blockStart",BindingFlags.NonPublic|BindingFlags.Instance);
        var planField=typeof(AdaptiveMedia).GetField("plan",BindingFlags.NonPublic|BindingFlags.Instance);
        var plan=(System.Collections.Generic.List<AdaptiveExposure>)planField.GetValue(media);
        double real=Time.realtimeSinceStartupAsDouble;origin.SetValue(media,real-plan[0].onset);media.Tick(real+.01);
        await Task.Delay(250);Check(media.Screen.GetComponent<AudioSource>().isPlaying,"Film audio-only playback");
        e.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount);Check(!media.Screen.GetComponent<AudioSource>().isPlaying,"Film audio stops on pause");
        e.Resume(now,frame);Check(c.AdaptiveRuntime.RequestLevel(2),"Manual engineering level request");
        while(e.Adaptive.Level!=2 && now<10000)
        {
            if(e.Current.scored)e.Respond(e.Current.expectedMatch,e.CurrentResult.plannedOnset+.5,frame);
            now+=4.5;e.Tick(now,frame++);
        }
        var player=media.Screen.GetComponent<UnityEngine.Video.VideoPlayer>();
        real=Time.realtimeSinceStartupAsDouble;
        while(!player.isPrepared && Time.realtimeSinceStartupAsDouble-real<8)await Task.Delay(50);
        Check(player.isPrepared,"Downloaded film video prepared");
        plan=(System.Collections.Generic.List<AdaptiveExposure>)planField.GetValue(media);
        real=Time.realtimeSinceStartupAsDouble;origin.SetValue(media,real-plan[0].onset);media.Tick(real+.01);
        await Task.Delay(1200);Check(player.isPlaying && player.frame>0,"Downloaded film actually decoded");
        typeof(TaskPlatformController).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);c.AdaptiveRuntime.Render();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/Stage2/film-runtime.png"));
        while(Time.realtimeSinceStartupAsDouble-real<plan[0].duration+.3){media.Tick(Time.realtimeSinceStartupAsDouble);await Task.Delay(30);}
        Check(!player.isPlaying && media.ActiveId=="none","Downloaded film obeys fixed exposure end");
        if(filmsOnly)
        {
            e.Abort(Time.realtimeSinceStartupAsDouble,Time.frameCount,"film_software_probe_complete");
            string filmResult="PASS: all ten downloaded matched-gain pairs loaded, actual audio/pause, AV decode, running layout and fixed exposure end. Isolated Editor fixture with programmatic task advancement. "+c.DataPath;
            File.WriteAllText("Evidence/Stage2/film-playback-probe.txt",filmResult);return filmResult;
        }
        c.enabled=true;c.BeginSession();await Task.Delay(800);
        e=c.Engine;now=Time.realtimeSinceStartupAsDouble;e.Advance(now,Time.frameCount);e.Advance(now,Time.frameCount);
        real=now;
        while(Time.realtimeSinceStartupAsDouble-real<65)
        {
            await Task.Delay(40);now=Time.realtimeSinceStartupAsDouble;
            if(e.Phase==TaskPhase.Instructions)e.Advance(now,Time.frameCount);
            if(e.Phase==TaskPhase.Running && e.Current.scored)e.Respond(e.Current.expectedMatch,now,Time.frameCount);
        }
        string features=File.ReadAllText(Path.Combine(c.DataPath,"physio_features.csv"));
        Check(features.Contains("eeg_ch0_theta_relative") && features.Contains("ecg_heart_rate"),"Recorded EEG and ECG reach Unity through live HTTP bridge");
        Check(features.Contains("synthetic"),"Recorded feature provenance retained");
        e.Abort(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_probe_complete");
        string result="PASS: ten actual film pairs loaded; audio playback/pause, AV decode and bounded stop. P007 recorded EEG and ECG mirrored into a separate real-time Editor session. Programmatic task answers; no participant or physiological-control validation. Session: "+c.DataPath;
        File.WriteAllText("Evidence/Stage2/film-and-p007-unity-probe.txt",result);return result;
    }
    public static async Task<string> Visual()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();c.BeginSession();await Task.Delay(800);
        double now=Time.realtimeSinceStartupAsDouble;c.Engine.Advance(now,Time.frameCount);c.Engine.Advance(now,Time.frameCount);
        await Task.Delay(700);ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/Stage2/layout-final.png"));await Task.Delay(150);
        c.Engine.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount,"synthetic_visual_check");
        return "Final Stage 2 layout captured with countdown left and neutral media surface right.";
    }
    public static string FinishRecordedReplay()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        string path=c.DataPath;
        c.Engine.Abort(Time.realtimeSinceStartupAsDouble,Time.frameCount,"engineering_replay_probe_complete");
        c.AdaptiveRuntime.Shutdown();
        string features=File.ReadAllText(Path.Combine(path,"physio_features.csv"));
        Check(features.Contains("eeg_ch0_theta_relative") && features.Contains("ecg_heart_rate") && features.Contains("ecg_rmssd_ms_exploratory"),"Recorded EEG, ECG and exploratory HRV reach Unity");
        Check(features.Contains("synthetic"),"Replay provenance mirrored");
        string result="PASS: ten downloaded film pairs loaded; audio playback/pause, AV decode and fixed stop. P007 recorded EEG/ECG/HRV mirrored into a separate real-time Editor session. Programmatic answers and recorded simulation only. Session: "+path;
        File.WriteAllText("Evidence/Stage2/film-and-p007-unity-probe.txt",result);return result;
    }
    public static async Task<string> Media()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var e=c.Engine;
        // Advance only the model clock to a training boundary; this is an isolated playback fixture, not timing validation.
        c.enabled=false;
        double now=e.CurrentResult?.plannedOnset??0;int frame=Time.frameCount;
        while(e.Adaptive.Stage!="training" && now<5000)
        {
            if(e.Phase==TaskPhase.Paused)e.Resume(now,frame);
            if(e.Phase==TaskPhase.Instructions)e.Advance(now,frame);
            if(e.Phase==TaskPhase.Running && e.Current.scored)e.Respond(e.Current.expectedMatch,e.CurrentResult.actualOnset+.5,frame);
            now+=4.5;e.Tick(now,frame++);
        }
        Check(e.Adaptive.Stage=="training","Synthetic baseline leads to training");
        var media=c.AdaptiveRuntime.Media;
        var origin=typeof(AdaptiveMedia).GetField("blockStart",BindingFlags.Instance|BindingFlags.NonPublic);
        var planField=typeof(AdaptiveMedia).GetField("plan",BindingFlags.Instance|BindingFlags.NonPublic);
        var slots=(System.Collections.Generic.List<AdaptiveExposure>)planField.GetValue(media);
        double real=Time.realtimeSinceStartupAsDouble;origin.SetValue(media,real-slots[0].onset);
        media.Tick(real+.02);await Task.Delay(100);
        var audio=media.Screen.GetComponent<AudioSource>();
        Check(audio.isPlaying && audio.clip!=null,"Level one plays audio-only");
        e.Pause(Time.realtimeSinceStartupAsDouble,Time.frameCount);await Task.Delay(100);
        Check(!audio.isPlaying,"Pause immediately stops distraction audio");
        string report="PASS: synthetic model advancement, Level 1 AudioSource playback, pause interruption and paired preload. Actual task timing not measured in this fixture. "+c.DataPath;
        File.WriteAllText("Evidence/Stage2/live-media-probe.txt",report);Debug.Log(report);return report;
    }
    public static async Task<string> Video()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var e=c.Engine;
        c.enabled=false;
        if(e.Phase==TaskPhase.Paused)e.Resume(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        Check(c.AdaptiveRuntime.RequestLevel(2),"Manual level request queued");
        double now=e.CurrentResult.plannedOnset;int frame=Time.frameCount;
        while(e.Adaptive.Level!=2 && now<5000)
        {
            if(e.Current.scored)e.Respond(e.Current.expectedMatch,e.CurrentResult.actualOnset+.5,frame);
            now+=4.5;e.Tick(now,frame++);
        }
        Check(e.Adaptive.Level==2,"Manual request applied at boundary");
        var media=c.AdaptiveRuntime.Media;var player=media.Screen.GetComponent<UnityEngine.Video.VideoPlayer>();
        double wait=Time.realtimeSinceStartupAsDouble;
        while(!player.isPrepared && Time.realtimeSinceStartupAsDouble-wait<5)await Task.Delay(50);
        Check(player.isPrepared,"Paired AV prepared asynchronously");
        var plan=(System.Collections.Generic.List<AdaptiveExposure>)typeof(AdaptiveMedia).GetField("plan",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(media);
        double start=Time.realtimeSinceStartupAsDouble;
        typeof(AdaptiveMedia).GetField("blockStart",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(media,start-plan[0].onset);
        media.Tick(start+.01);await Task.Delay(500);
        Check(player.isPlaying && player.frame>=0,"Video actually decodes");
        Check(media.Screen.GetComponent<Renderer>().sharedMaterial.mainTexture==player.targetTexture,"Video visible on right-side screen");
        Check(player.audioOutputMode==UnityEngine.Video.VideoAudioOutputMode.AudioSource && player.GetTargetAudioSource(0)==media.Screen.GetComponent<AudioSource>(),"Paired original audio routed to fixed source");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/Stage2/av-fixture.png"));
        while(Time.realtimeSinceStartupAsDouble-start<11.2){media.Tick(Time.realtimeSinceStartupAsDouble);await Task.Delay(30);}
        Check(!player.isPlaying && media.ActiveId=="none","Fixed exposure end stops video and audio");
        string report="PASS: manual boundary Level 2, async preparation, decoded video frame, render texture, paired fixed audio source and fixed 11-second stop. Synthetic isolated media fixture, not actual device timing. "+c.DataPath;
        File.WriteAllText("Evidence/Stage2/live-video-probe.txt",report);Debug.Log(report);return report;
    }
}

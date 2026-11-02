using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using TMPro;

public static class WallPracticeValidation
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    public static string Model()
    {
        checks=0;var manifest=JsonUtility.FromJson<AdaptiveMediaManifest>(File.ReadAllText("Assets/Resources/adaptive-media-manifest.json"));
        for(int seed=0;seed<1000;seed++)for(int group=0;group<4;group++)
        {
            var c=new AdaptiveTrainingConfig{seed=seed,counterbalanceGroup=group};c.Validate();
            var a=new AdaptiveMediaScheduler(c,manifest);var b=new AdaptiveMediaScheduler(c,manifest);
            var practice=a.PlanPractice(63);var repeat=b.PlanPractice(63);
            Check(practice.Count==3 && practice.Select(x=>x.mediaId).Distinct().Count()==3,"Three distinct practice clips");
            var av=manifest.items.Where(x=>x.split=="train").OrderBy(x=>x.id,StringComparer.Ordinal)
                .Where((x,i)=>i%2==(1+group)%2).Select(x=>x.id).ToArray();
            for(int i=0;i<practice.Count;i++)
            {
                var p=practice[i];Check(av.Contains(p.mediaId),"Only AV-assigned training content");
                Check(JsonUtility.ToJson(p)==JsonUtility.ToJson(repeat[i]),"Seed reproducibility");
                Check(p.stage=="practice" && p.level==2 && p.block==0,"Practice exposure attribution");
                Check(Math.Abs(p.onset-c.practiceMediaOnsets[i])<=.500001 && p.onset+p.duration<=63,"Bounded practice slots");
                if(i>0)Check(practice[i-1].onset+practice[i-1].duration<p.onset,"No overlap");
                a.Presented(p.mediaId);
            }
            int assigned=manifest.items.Count(x=>x.split=="train")/2;
            Check(a.Plan(4,2,"training").All(x=>x.previousExposures==(assigned==3?1:0)),"Least-exposed content follows practice");
            Check(a.Plan(4,1,"training").All(x=>x.previousExposures==0),"Audio assignment untouched");
            Check(a.Plan(5,2,"pretest").All(x=>x.previousExposures==0),"Pretest content unseen");
            Check(a.Plan(6,2,"posttest").All(x=>x.previousExposures==0),"Posttest content unseen");
            Check(a.PlanPractice(25).Count==1,"Short custom practice only schedules fitting slots");
        }
        var legacy=new AdaptiveTrainingConfig{version="2.0.0-stage2-dev1",mode="SHADOW",seed=41,videoWidth=.64f,videoHeight=.36f,videoAngle=20};
        Check(legacy.UpgradeLegacyPresentation(),"Legacy config upgraded");legacy.Validate();
        Check(legacy.mode=="SHADOW" && legacy.seed==41,"Migration preserves control and randomization");
        Check(!legacy.UpgradeLegacyPresentation(),"Migration is idempotent");
        string report="PASS: "+checks+" assertions; 1,000 seeds x four groups, practice allocation, timing, history, held-out separation and config migration. Synthetic only.";
        Directory.CreateDirectory("Evidence/WallPractice");File.WriteAllText("Evidence/WallPractice/model.txt",report);Debug.Log(report);return report;
    }
    public static async Task<string> Live()
    {
        checks=0;Directory.CreateDirectory("Evidence/WallPractice");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var e=c.Engine;var media=c.AdaptiveRuntime.Media;
        await Task.Delay(1500);
        Check(e.Adaptive!=null && e.Phase==TaskPhase.Preparation,"Stage 2 preparation");
        var wall=GameObject.Find("NeutralRoom/FrontWall").transform;var screen=media.Screen;
        Check(Math.Abs(screen.position.x-wall.position.x)<.001 && Math.Abs(screen.position.y-wall.position.y)<.001,"Wall center in both axes");
        Check(Vector3.Distance(screen.position,wall.position-wall.forward*(wall.lossyScale.z*.5f+.025f))<.001,"Screen lies just ahead of wall");
        Check(Vector3.Distance(screen.lossyScale,new Vector3(3.2f,1.8f,1))<.001,"Large 16:9 display");
        Check(!screen.GetComponent<Renderer>().enabled,"No idle rectangle");
        c.badge.ForceMeshUpdate();Check(!c.badge.isTextOverflowing,"Preparation badge fits");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/WallPractice/preparation.png"));await Task.Delay(100);
        e.Note("engineering_wall_practice_probe","Actual media and real-time task; programmatic answers; not participant or physical-device evidence",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        int starts=0,ends=0,failures=0;bool captured=false;
        media.OnMedia+=(exposure,status,time,detail)=>{if(exposure.stage=="practice"){if(status=="started")starts++;if(status=="ended")ends++;if(status=="failure")failures++;}};
        e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        double began=Time.realtimeSinceStartupAsDouble;
        while(e.Phase==TaskPhase.Running && e.Practice && Time.realtimeSinceStartupAsDouble-began<68)
        {
            await Task.Delay(25);double now=Time.realtimeSinceStartupAsDouble;
            if(e.Phase!=TaskPhase.Running || !e.Practice)break;
            if(e.Current.scored && !e.CurrentResult.responded && now-e.CurrentResult.actualOnset>.6)e.Respond(e.Current.expectedMatch,now,Time.frameCount);
            if(starts>=1 && !captured && now-began>12)
            {
                captured=true;var hud=c.spatialView.taskCanvas.transform.Find("ExperimentModeStatus").GetComponent<TMP_Text>();
                hud.ForceMeshUpdate();Check(hud.textInfo.lineCount==1 && !hud.isTextOverflowing,"Compact single-line status");
                Check(c.spatialView.rule.text=="","No duplicate rule row");
                Check(screen.GetComponent<Renderer>().enabled,"Decoded film visible");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/WallPractice/practice-film.png"));
            }
        }
        Check(e.Phase==TaskPhase.Instructions && !e.Practice,"Practice ends normally");
        Check(starts==3 && ends==3 && failures==0,"Three real decoded clips complete");
        Check(!screen.GetComponent<Renderer>().enabled,"Wall restored after practice");
        e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);await Task.Delay(1500);
        Check(e.Adaptive.Stage=="baseline" && media.ActiveId=="none" && !screen.GetComponent<Renderer>().enabled,"Baseline stays silent and screen-free");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/WallPractice/baseline.png"));await Task.Delay(100);
        foreach(string mode in new[]{"OFF","SHADOW","ACTIVE"})foreach(string source in new[]{"recorded_replay","live","mixed","synthetic","none","unknown"})
            foreach(string state in new[]{"receiving","waiting","partial","disconnected"})foreach(bool connected in new[]{false,true})
            {
                c.badge.text=ExperimentStatusView.CompactStatus(mode,"physiological",new PhysiologyDisplayStatus{source=source,connection=state},connected)+" | Paused";
                c.badge.ForceMeshUpdate();Check(!c.badge.isTextOverflowing,"Compact source/control label fits");
            }
        c.StatusView.Refresh();
        string report="PASS: "+checks+" checks; actual 63-second practice, three decoded films completed, centered 3.2 x 1.8 m wall screen, idle renderer hidden, compact UI and silent baseline. Programmatic input / Editor only. Session: "+c.DataPath;
        File.WriteAllText("Evidence/WallPractice/live.txt",report);Debug.Log(report);return report;
    }
    public static async Task<string> Stage1()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var e=c.Engine;
        await Task.Delay(500);
        Check(e.Adaptive==null,"Disabled override preserves Stage 1");
        e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        await Task.Delay(200);
        var hud=c.spatialView.taskCanvas.transform.Find("ExperimentModeStatus").GetComponent<TMP_Text>();
        hud.ForceMeshUpdate();Check(!hud.isTextOverflowing && hud.text.Contains("STAGE 1"),"Stage 1 compact status fits");
        Check(c.spatialView.stage.text.Contains("0-back") && c.spatialView.stage.text.Contains("Observe only"),"Stage 1 rule and warmup preserved");
        Check(c.spatialView.rule.text=="","Single phase row");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/WallPractice/stage1.png"));await Task.Delay(100);
        string report="PASS: Stage 1 actual engine identity, compact running HUD, 0-back and observe-only warmup reminder. Editor/programmatic input only.";
        File.WriteAllText("Evidence/WallPractice/stage1.txt",report);Debug.Log(report);return report;
    }
}

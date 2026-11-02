using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Video;

public static class ExpandedMediaValidation
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static string Model()
    {
        checks=0;var m=JsonUtility.FromJson<AdaptiveMediaManifest>(File.ReadAllText("Assets/Resources/adaptive-media-manifest.json"));
        Check(m.HasValidLayout() && m.items.Length==22,"Expanded 18/2/2 layout");
        var legacy=new AdaptiveMediaManifest{items=m.items.Take(10).ToArray()};Check(legacy.HasValidLayout(),"Legacy ten-pair library remains supported");
        Check(!new AdaptiveMediaManifest{items=m.items.Take(21).ToArray()}.HasValidLayout(),"Reject odd training pool");
        Check(!new AdaptiveMediaManifest{items=m.items.Concat(new[]{m.items[0]}).ToArray()}.HasValidLayout(),"Reject duplicate IDs");
        for(int seed=0;seed<1000;seed++)for(int group=0;group<4;group++)
        {
            var config=new AdaptiveTrainingConfig{seed=seed,counterbalanceGroup=group};var scheduler=new AdaptiveMediaScheduler(config,m);
            var practice=scheduler.PlanPractice(63);var reference=new AdaptiveMediaScheduler(config,m).PlanPractice(63);
            Check(practice.Count==3 && practice.Select(p=>p.mediaId).Distinct().Count()==3,"Three distinct practice clips");
            Check(practice.Zip(reference,(a,b)=>JsonUtility.ToJson(a)==JsonUtility.ToJson(b)).All(x=>x),"Practice determinism");
            var allVideo=new HashSet<string>();var allAudio=new HashSet<string>();
            foreach(var p in practice){allVideo.Add(p.mediaId);scheduler.Presented(p.mediaId);}
            for(int block=4;block<6;block++)
            {
                var plan=scheduler.Plan(block,2,"training");
                foreach(var p in plan)
                {
                    Check(p.previousExposures==0 && allVideo.Add(p.mediaId),"Six fresh AV clips after practice");scheduler.Presented(p.mediaId);
                }
            }
            Check(allVideo.Count==9,"Nine clips assigned to AV");
            Check(scheduler.Plan(6,2,"training").All(p=>p.previousExposures==1),"Familiarity retained after pool exhaustion");
            for(int block=7;block<10;block++)foreach(var p in scheduler.Plan(block,1,"training"))
            {
                Check(p.previousExposures==0 && allAudio.Add(p.mediaId),"Nine unique audio clips");scheduler.Presented(p.mediaId);
            }
            Check(!allVideo.Overlaps(allAudio) && allAudio.Count==9,"Modality content stays separated");
            var train=allVideo.Concat(allAudio).ToHashSet();
            foreach(string stage in new[]{"pretest","posttest"})foreach(int level in new[]{1,2})
                Check(!train.Contains(scheduler.Plan(10,level,stage)[0].mediaId),"Transfer content stays held out");
            config.allowMediaRepeats=false;Check(scheduler.Plan(11,2,"training").All(p=>p.mediaId=="unavailable"),"No-repeat exhaustion explicit");
        }
        string report="PASS: "+checks+" assertions, 1,000 seeds x four groups; expanded/legacy layouts, nine clips per modality, six fresh AV clips after practice, held-out separation and no-repeat exhaustion. Synthetic only.";
        File.WriteAllText("Evidence/MediaExpansion/model.txt",report);Debug.Log(report);return report;
    }
    public static async Task<string> Live()
    {
        checks=0;var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();var media=c.AdaptiveRuntime.Media;var e=c.Engine;
        await Task.Delay(1800);
        var loaded=(System.Collections.IDictionary)typeof(AdaptiveMedia).GetField("clips",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(media);
        Check(loaded.Count==22,"All 22 hash-checked pairs loaded");Check(!media.Ready,"Participant listening gate remains closed");
        var manifest=JsonUtility.FromJson<AdaptiveMediaManifest>(media.ManifestJson);
        var host=new GameObject("Engineering expanded-media decoder probe");var player=host.AddComponent<VideoPlayer>();
        var texture=new RenderTexture(320,180,0);texture.Create();player.playOnAwake=false;player.audioOutputMode=VideoAudioOutputMode.None;
        player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=texture;player.sendFrameReadyEvents=true;
        bool frame=false;string failure=null;player.frameReady+=(p,index)=>frame=true;player.errorReceived+=(p,error)=>failure=error;
        var decoded=new List<string>();
        try
        {
            foreach(var item in manifest.items.Skip(10))
            {
                frame=false;failure=null;player.url=Path.Combine(Application.persistentDataPath,"AdaptiveMedia",item.videoFile);player.Prepare();
                double deadline=Time.realtimeSinceStartupAsDouble+10;
                while(!player.isPrepared && failure==null && Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(20);
                Check(player.isPrepared && failure==null,"New clip prepares: "+item.id);player.Play();
                while(!frame && failure==null && Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(20);
                Check(frame && failure==null,"New clip decodes: "+item.id);decoded.Add(item.id);player.Stop();
            }
        }
        finally{UnityEngine.Object.Destroy(host);texture.Release();UnityEngine.Object.Destroy(texture);}
        e.Note("engineering_expanded_media_probe","22 loaded; all 12 additions decoded offscreen without audio; real-time practice uses standard wall/audio playback and programmatic answers",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        await Task.Delay(300);
        int starts=0,ends=0,failures=0;var played=new List<string>();
        media.OnMedia+=(slot,status,time,detail)=>{if(slot.stage=="practice"){if(status=="started"){starts++;played.Add(slot.mediaId);}if(status=="ended")ends++;if(status=="failure")failures++;}};
        e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        double began=Time.realtimeSinceStartupAsDouble;bool captured=false;
        while(e.Phase==TaskPhase.Running && e.Practice && Time.realtimeSinceStartupAsDouble-began<68)
        {
            await Task.Delay(25);double now=Time.realtimeSinceStartupAsDouble;
            if(e.Phase!=TaskPhase.Running || !e.Practice)break;
            if(e.Current.scored && !e.CurrentResult.responded && now-e.CurrentResult.actualOnset>.6)e.Respond(e.Current.expectedMatch,now,Time.frameCount);
            if(starts>0 && !captured && now-began>12){captured=true;ScreenCapture.CaptureScreenshot(Path.GetFullPath("Evidence/MediaExpansion/practice.png"));}
        }
        Check(e.Phase==TaskPhase.Instructions && !e.Practice,"Practice finishes normally");
        Check(starts==3 && ends==3 && failures==0,"Three full expanded-pool practice exposures complete");
        Check(played.Any(id=>int.Parse(id.Substring(5))>10),"Actual scheduler selected added content");
        Check(!media.Screen.GetComponent<Renderer>().enabled,"No idle rectangle after practice");
        string report="PASS: "+checks+" live checks; all 22 pairs loaded; all 12 new clips prepared/decoded offscreen (audio not exercised by decoder probe); three full real-time practice AV exposures completed: "+string.Join(", ",played)+". Editor/programmatic input only. Session: "+c.DataPath;
        File.WriteAllText("Evidence/MediaExpansion/live.txt",report);Debug.Log(report);return report;
    }
}

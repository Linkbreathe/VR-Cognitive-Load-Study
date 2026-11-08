using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Video;

public static class PairedOffSlotPlayback
{
    [Serializable] public class Slot { public string scenario,condition; public int level,trial; public float durationSeconds; }
    [Serializable] public class Plan { public Slot[] slots; }
    [Serializable] public class Receipt {
        public string scenario,condition,mediaId;
        public int level,decodedFrames;
        public long firstFrame=-1,lastFrame=-1;
        public bool screenVisible,hiddenAtEnd;
        public List<string> events=new List<string>();
    }
    [Serializable] public class Output { public List<Receipt> receipts=new List<Receipt>(); }
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static double Now => Time.realtimeSinceStartupAsDouble;
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play mode required");
        var dir=Path.GetFullPath("Evidence/PairedOffMock");
        var plan=Newtonsoft.Json.JsonConvert.DeserializeObject<Plan>(File.ReadAllText(Path.Combine(dir,"next-slot-input.json")));
        var original=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var go=new GameObject("Engineering paired OFF slot probe");
        var previousBackground=Application.runInBackground;
        try {
            Application.runInBackground=true;
            var owner=go.AddComponent<TaskPlatformController>();
            owner.enabled=false;
            var config=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(Path.Combine(Application.persistentDataPath,"adaptive-training-config.json")));
            config.sessionType="engineering";
            var engine=new TaskEngine(JsonUtility.FromJson<TaskConfig>(original.defaultConfig.text),config);
            typeof(TaskPlatformController).GetProperty("Engine").SetValue(owner,engine);
            typeof(TaskPlatformController).GetProperty("DataPath").SetValue(owner,Application.temporaryCachePath);
            var media=go.AddComponent<AdaptiveMedia>();
            media.Bind(owner,config);
            var limit=Now+30;
            while(!media.EngineeringReady && Now<limit)await Task.Delay(75);
            if(!media.EngineeringReady)throw new Exception("Media files failed readiness");
            typeof(TaskEngine).GetProperty("Phase").SetValue(engine,TaskPhase.Running);
            var player=media.Screen.GetComponent<VideoPlayer>();
            var output=new Output();
            foreach(var s in plan.slots) {
                var rec=new Receipt {scenario=s.scenario,condition=s.condition,level=s.level,mediaId=s.level==1?"pair_01":"silent_slot"};
                var exposure=new AdaptiveExposure {stage=s.condition,level=s.level,block=1,mediaId=rec.mediaId,
                    modality=s.level==1?"audiovisual":"silent",onset=0,duration=s.durationSeconds};
                var type=typeof(AdaptiveMedia);
                type.GetField("plan",Private).SetValue(media,new List<AdaptiveExposure>{exposure});
                type.GetField("next",Private).SetValue(media,0);
                type.GetMethod("PrepareNext",Private).Invoke(media,null);
                limit=Now+15;
                while(s.level==1 && !player.isPrepared && Now<limit)await Task.Delay(25);
                Action<AdaptiveExposure,string,double,string> log=(e,status,t,why)=>rec.events.Add(status+" "+why);
                VideoPlayer.FrameReadyEventHandler frame=(p,f)=>{rec.decodedFrames++;if(rec.firstFrame<0)rec.firstFrame=f;rec.lastFrame=f;};
                media.OnMedia+=log;
                player.frameReady+=frame;
                double start=Now;
                type.GetField("blockStart",Private).SetValue(media,start);
                media.Tick(start);
                while(media.IsPlaying && Now-start<15) {
                    await Task.Delay(20);
                    if(media.IsVideoVisible && !rec.screenVisible && player.frame>=12) {
                        rec.screenVisible=true;
                        var tex=player.targetTexture;
                        var prev=RenderTexture.active;
                        RenderTexture.active=tex;
                        var image=new Texture2D(tex.width,tex.height,TextureFormat.RGB24,false);
                        image.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);image.Apply();
                        File.WriteAllBytes(Path.Combine(dir,"next-slot-frame.png"),image.EncodeToPNG());
                        UnityEngine.Object.Destroy(image);RenderTexture.active=prev;
                    }
                    media.Tick(Now);
                }
                media.OnMedia-=log;player.frameReady-=frame;
                rec.hiddenAtEnd=!media.Screen.GetComponent<Renderer>().enabled;
                if(s.level==0 ? rec.events.Count!=1 || !rec.events[0].StartsWith("silent_slot") || rec.decodedFrames!=0 :
                    !rec.events.Exists(x=>x.StartsWith("started first_decoded_frame")) || !rec.events.Exists(x=>x.StartsWith("ended")) ||
                    !rec.screenVisible || rec.lastFrame<270) throw new Exception("Slot verification: "+Newtonsoft.Json.JsonConvert.SerializeObject(rec));
                output.receipts.Add(rec);
            }
            File.WriteAllText(Path.Combine(dir,"next-slot-unity.json"),Newtonsoft.Json.JsonConvert.SerializeObject(output,Newtonsoft.Json.Formatting.Indented));
            return output.receipts.Count;
        } finally {
            Application.runInBackground=previousBackground;
            UnityEngine.Object.Destroy(go);
        }
    }
}

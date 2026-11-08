using EvidenceVR;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public static class ProbeStudyValidation
{
    static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    static double Now=>Time.realtimeSinceStartupAsDouble;
    static AdaptiveTrainingConfig Config(string path)=>JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path));
    public static async Task<object> Run()
    {
        Check(Application.isPlaying,"Play Mode required");
        var configs=Directory.GetFiles("Tools/LayerAB/Examples","*config.json").Select(Config).ToArray();
        foreach(var c in configs)c.Validate();
        Check(configs.Length==6 && configs.Count(c=>LayerABStudy.Stage(c,1)=="layer_a")==3,"allocation order");
        var c0=configs[0];
        var engine=new TaskEngine(new TaskConfig(),c0);
        double now=0;
        engine.Advance(now,0);engine.Advance(now,0);engine.Advance(now,0);
        now=engine.CurrentResult.plannedOnset+4.5;engine.Tick(now,1);engine.Advance(now,1);
        for(int i=0;i<c0.calibrationBlocks*12;i++){now=engine.CurrentResult.plannedOnset+4.5;engine.Tick(now,i);}
        engine.ReceivePersonalModel("personal_model_validated",true,1,now,1);engine.Advance(now,1);
        var checks=0;var swaps=0;
        engine.OnEvent+=ev=>{if(ev.type=="personal_decision_due")checks++;if(ev.type=="adaptive_level_changed")swaps++;};
        for(int i=0;i<144;i++){
            var t=engine.CurrentResult;
            Check(t.phaseTrial==i && !t.transition && engine.PersonalProgressTotal==144,"ordinal or transition");
            engine.Tick(t.plannedOnset+4,i);
            if(i==14 || i==19){
                var action=i==14?"ON":"OFF";
                engine.CanApplyAdaptiveChange=()=>action!="OFF";
                engine.Adaptive.Pending=new AdaptiveDecision{action=action,epoch=engine.RollingEpoch,level=t.level,lastTrial=i+1,
                    evaluationId=engine.PersonalDecisionId,requestId="mock",guardRequestId="mock",guardRevision=EvidenceGuardContract.Revision,
                    guardMode="enforce",guardVerdict="ALLOW"};
            }
            engine.Tick(t.plannedOnset+4.5,i);
            if(i==19)Check(engine.Adaptive.Level==0,"busy-media OFF was deferred");
        }
        Check(engine.Adaptive.Stage=="questionnaire_1" && checks==28 && swaps==2,"phase duration or request count");
        var obj=new GameObject("Engineering probe media verification");
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        try{
            var original=UnityEngine.Object.FindObjectsByType<TaskPlatformController>(FindObjectsSortMode.None).First();
            var owner=obj.AddComponent<TaskPlatformController>();owner.enabled=false;
            var fixture=new TaskEngine(new TaskConfig(),c0);
            typeof(TaskPlatformController).GetProperty("Engine").SetValue(owner,fixture);
            typeof(TaskPlatformController).GetProperty("DataPath").SetValue(owner,Application.temporaryCachePath);
            var media=obj.AddComponent<AdaptiveMedia>();
            media.Bind(owner,c0);
            double deadline=Now+60;
            while(!media.EngineeringReady && Now<deadline)await Task.Delay(60);
            Check(media.EngineeringReady,"manifest files and checksums");
            var bank=JsonUtility.FromJson<AdaptiveMediaManifest>(media.ManifestJson);
            var schedule=new AdaptiveMediaScheduler(c0,bank).Plan(1,1,"layer_b");
            Check(schedule.Select(e=>e.onset).SequenceEqual(new double[]{4,31,58,85}) && schedule.All(e=>e.duration==20),"27-second slots");
            var slot=schedule[0];slot.onset=0;
            typeof(TaskEngine).GetProperty("Phase").SetValue(fixture,TaskPhase.Running);
            fixture.Adaptive.GetType().GetMethod("SetPersonalStage",flags).Invoke(fixture.Adaptive,new object[]{"layer_b",1});
            typeof(AdaptiveMedia).GetField("plan",flags).SetValue(media,new List<AdaptiveExposure>{slot});
            typeof(AdaptiveMedia).GetMethod("PrepareNext",flags).Invoke(media,null);
            var player=media.Screen.GetComponent<VideoPlayer>();
            deadline=Now+12;while(!player.isPrepared && Now<deadline)await Task.Delay(40);
            var events=new List<string>();
            media.OnMedia+=(e,status,t,why)=>events.Add(status+"/"+why);
            typeof(AdaptiveMedia).GetField("blockStart",flags).SetValue(media,Now);
            media.Tick(Now);deadline=Now+8;
            while(!media.IsVideoVisible && Now<deadline)await Task.Delay(30);
            Check(media.IsVideoVisible,"decoded frame");
            fixture.Adaptive.GetType().GetMethod("SetPersonalLevel",flags).Invoke(fixture.Adaptive,new object[]{0});
            typeof(AdaptiveMedia).GetMethod("OnEvent",flags).Invoke(media,new object[]{new TaskEvent{type="adaptive_level_changed",actual=Now}});
            Check(!media.IsPlaying && !media.IsVideoVisible && events.Contains("ended/automatic_off"),"immediate playback cut");
            var result=new {trials=144,checks,swaps,firstOrderA=3,firstOrderB=3,selectedClip=slot.mediaId,mediaEvents=events,
                mediaCount=bank.items.Count(e=>e.split=="training20")};
            Directory.CreateDirectory("Evidence/ProbeStudy");
            File.WriteAllText("Evidence/ProbeStudy/unity.json",Newtonsoft.Json.JsonConvert.SerializeObject(result));
            return result;
        }finally{UnityEngine.Object.Destroy(obj);}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Video;

public static class ContinuousComparisonValidation
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static double Now=>Time.realtimeSinceStartupAsDouble;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
    static AdaptiveTrainingConfig Config()
    {
        var c=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(Directory.GetFiles("Tools/LayerAB/Examples","*config.json")[0]));
        c.evaluationPolicy="behavior5-continuous-stride2-v3";c.trainingTrials=60;c.rollingWindowTrials=5;c.rollingStrideTrials=2;
        c.sessionType="engineering";c.Validate();return c;
    }
    public static object Timing()
    {
        var result=new List<object>();
        foreach(var stage in new[]{"layer_a","layer_b"})
        {
            var c=Config();var engine=new TaskEngine(new TaskConfig(),c);
            Call(engine,"PersonalStage",stage,0,0d,0,TaskPhase.Instructions);
            Call(engine,"BeginPersonalTrials",0d,0);
            var checks=new List<int>();var receipts=new List<string>();
            engine.OnEvent+=e=>{if(e.type=="personal_decision_due")checks.Add(engine.CurrentResult.phaseTrial+1);if(e.type=="personal_execution")receipts.Add(e.detail);};
            for(int i=0;i<60;i++)
            {
                var t=engine.CurrentResult;Check(t.phaseTrial==i && engine.PersonalProgressTotal==60,"trial budget");
                engine.Tick(t.plannedOnset+4,i);
                if(i==7 || i==9 || i==11)
                {
                    string action=i==7?"ON":i==9?"HOLD":"OFF";
                    engine.CanApplyAdaptiveChange=()=>action=="ON";
                    engine.Adaptive.Pending=new AdaptiveDecision{action=action,epoch=engine.RollingEpoch,level=t.level,lastTrial=i+1,
                        evaluationId=engine.PersonalDecisionId,requestId="fixture-"+i,guardRequestId="fixture-"+i,
                        guardRevision=EvidenceGuardContract.Revision,guardMode="enforce",guardVerdict="ALLOW"};
                }
                engine.Tick(t.plannedOnset+4.5,i);
                if(i==9)Check(engine.Adaptive.Pending==null && engine.Adaptive.Level==1,"HOLD must acknowledge during active video");
                if(i==11)Check(engine.Adaptive.Level==0,"OFF must not wait for clip end");
            }
            Check(checks.SequenceEqual(Enumerable.Range(1,29).Select(i=>i*2)),"checkpoints 2..58");
            Check(receipts.Count==3,"ON/HOLD/OFF acknowledgements");
            result.Add(new {stage,trials=60,checks,fullBehaviorPositions=checks.Count(i=>i>=8),receipts});
        }
        Directory.CreateDirectory("Evidence/ContinuousComparison60-20261107");
        File.WriteAllText("Evidence/ContinuousComparison60-20261107/unity-timing.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        Debug.Log("Compact comparison timing verified: 60 trials, 29 checks, 26 full behavior positions per arm; HOLD acknowledged while playing.");
        return result;
    }
    public static async Task<object> Playback()
    {
        Check(Application.isPlaying,"Play Mode required");
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(owner!=null,"Scene owner missing");
        var oldEngine=owner.Engine;var oldPath=owner.DataPath;bool oldEnabled=owner.enabled;
        var go=new GameObject("Continuous comparison engineering verification");
        var events=new List<string>();var samples=new List<object>();var segments=new List<string>();
        try
        {
            owner.enabled=false;if(owner.TestClient!=null)owner.TestClient.enabled=false;
            var c=Config();var engine=new TaskEngine(new TaskConfig(),c);
            Call(engine,"PersonalStage","layer_b",1,Now,0,TaskPhase.Instructions);
            Call(engine,"BeginPersonalTrials",Now,0);
            typeof(TaskPlatformController).GetProperty("Engine").SetValue(owner,engine);
            string folder=Path.GetFullPath("Evidence/ContinuousComparison60-20261107/playback");Directory.CreateDirectory(folder);
            typeof(TaskPlatformController).GetProperty("DataPath").SetValue(owner,folder);
            var media=go.AddComponent<AdaptiveMedia>();media.Bind(owner,c);
            media.OnMedia+=(e,status,t,why)=>events.Add(status+";"+why);
            engine.OnEvent+=e=>{if(e.type=="adaptive_media_segment_changed")segments.Add(e.detail);};
            double deadline=Now+40;
            while(!media.EngineeringReady && Now<deadline)await Task.Delay(50);
            Check(media.EngineeringReady,"Continuous media files not ready");
            var player=media.Screen.GetComponent<VideoPlayer>();
            deadline=Now+12;
            while(!media.IsVideoVisible && Now<deadline){media.Tick(Now);await Task.Delay(25);}
            Check(media.IsVideoVisible,"No first decoded frame");
            double start=Now;bool held=false,boundary=false;int hidden=0,audioMissing=0;
            while(Now-start<27)
            {
                media.Tick(Now);double elapsed=Now-start;
                if(!media.IsVideoVisible)hidden++;if(!media.IsAudioPlaying)audioMissing++;
                if(!held && elapsed>=18)
                {
                    engine.CanApplyAdaptiveChange=()=>false;
                    engine.Adaptive.Pending=new AdaptiveDecision{action="HOLD",epoch=engine.RollingEpoch,level=1,lastTrial=0,evaluationId=engine.PersonalDecisionId,
                        requestId="deferred-off",guardRequestId="deferred-off",guardVerdict="DEFER",guardMode="enforce"};
                    Call(engine,"ApplyPersonalDecision",Now,Time.frameCount);
                    Check(engine.Adaptive.Pending==null && media.IsVideoVisible,"Deferred OFF interrupted playback");
                    held=true;Debug.Log("Continuous playback: model OFF deferred at source 18s; HOLD leaves audiovisual playback active.");
                }
                if(!boundary && elapsed>=21)
                {
                    Call(media,"OnEvent",new TaskEvent{type="block_complete",actual=Now});
                    Call(media,"OnEvent",new TaskEvent{type="block_start",actual=Now});
                    boundary=true;
                }
                samples.Add(new {elapsed,cursor=player.time,frame=player.frame,visible=media.IsVideoVisible,audio=media.IsAudioPlaying});
                await Task.Delay(50);
            }
            Check(hidden==0 && audioMissing==0,"Video/audio gap during HOLD or clip transition");
            Check(segments.Count>=2 && player.time>=26,"Clip boundary not crossed");
            Call(engine.Adaptive,"SetPersonalLevel",0);Call(media,"OnEvent",new TaskEvent{type="adaptive_level_changed",actual=Now});
            Check(!media.IsVideoVisible && !media.IsAudioPlaying,"OFF did not hide/mute");
            double cursor=player.time;await Task.Delay(1000);Check(Math.Abs(player.time-cursor)<.1,"OFF cursor advanced");
            Call(engine.Adaptive,"SetPersonalLevel",1);Call(media,"OnEvent",new TaskEvent{type="adaptive_level_changed",actual=Now});
            deadline=Now+5;while((!media.IsVideoVisible || player.time<cursor+.5) && Now<deadline){media.Tick(Now);await Task.Delay(30);}
            Check(media.IsVideoVisible && player.time>=cursor+.5,"ON failed to resume saved position");
            Call(media,"OnEvent",new TaskEvent{type="session_complete",actual=Now});
            Check(!media.IsVideoVisible && !media.IsAudioPlaying,"Session end did not stop exposure");
            var result=new {evidence="Unity Editor; no worn device or physical photon/audio measurement",held,hidden,audioMissing,cursorRetained=cursor,segments,events,samples};
            File.WriteAllText(Path.Combine(folder,"verification.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            Debug.Log("Continuous comparison playback verified: crossed 20s clip boundary during HOLD without sampled video/audio gap; OFF stopped; ON resumed; session end stopped.");
            return new {held,hidden,audioMissing,cursor,segmentCount=segments.Count};
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            typeof(TaskPlatformController).GetProperty("Engine").SetValue(owner,oldEngine);
            typeof(TaskPlatformController).GetProperty("DataPath").SetValue(owner,oldPath);
            owner.enabled=oldEnabled;if(owner.TestClient!=null)owner.TestClient.enabled=true;
        }
    }
}

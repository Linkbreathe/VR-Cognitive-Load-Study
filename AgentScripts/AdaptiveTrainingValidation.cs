using System;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEngine;

public static class AdaptiveTrainingValidation
{
    static int assertions;
    static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    public static string Run()
    {
        assertions=0;
        var manifest=JsonUtility.FromJson<AdaptiveMediaManifest>(File.ReadAllText("Assets/Resources/adaptive-media-manifest.json"));
        for(int seed=0;seed<1000;seed++)
        {
            var c=new AdaptiveTrainingConfig{seed=seed,counterbalanceGroup=seed%4};
            var a=new AdaptiveMediaScheduler(c,manifest);var b=new AdaptiveMediaScheduler(c,manifest);
            foreach(int level in new[]{0,1,2})
            {
                var first=a.Plan(4,level,"training");var second=b.Plan(4,level,"training");
                Check(first.Count==3,"three training slots");
                Check(level==0 || first.Select(x=>x.mediaId).Distinct().Count()==3,"distinct media within evaluation");
                for(int i=0;i<3;i++)
                {
                    Check(JsonUtility.ToJson(first[i])==JsonUtility.ToJson(second[i]),"deterministic media");
                    Check(first[i].onset>=0 && first[i].onset+first[i].duration<=108,"slots fit evaluation");
                    if(i>0)Check(first[i-1].onset+first[i-1].duration<first[i].onset,"non-overlap");
                }
            }
            var audio=a.Plan(5,1,"training");var video=a.Plan(5,2,"training");
            Check(!audio.Select(x=>x.mediaId).Intersect(video.Select(x=>x.mediaId)).Any(),"modality content separation");
            foreach(var item in audio)a.Presented(item.mediaId);
            Check(a.Plan(6,1,"training").All(x=>x.previousExposures==1),"exposure history");
            c.allowMediaRepeats=false;
            Check(a.Plan(7,1,"training").All(x=>x.mediaId=="unavailable"),"no-repeat exhaustion explicit");
            Check(a.Plan(8,1,"pretest")[0].mediaId!=a.Plan(9,1,"posttest")[0].mediaId,"new posttest content");
        }
        for(int seed=0;seed<30;seed++)RunSession(seed);
        PauseTest(false);PauseTest(true);
        string report="PASS: "+assertions+" Stage 2 model assertions; 1,000 schedule seeds; 30 complete simulated sessions; uninterrupted cross-level 2-back, 24 scored trials per block, fixed 4.5-second cycles, transition flags, caps, memory rewarm and media history. Synthetic only.";
        Directory.CreateDirectory("Evidence/Stage2");File.WriteAllText("Evidence/Stage2/model-validation.txt",report);Debug.Log(report);return report;
    }
    static void RunSession(int seed)
    {
        var config=new AdaptiveTrainingConfig{enabled=true,mode="ACTIVE",seed=seed};
        var e=new TaskEngine(new TaskConfig(),config);
        e.OnEvent+=item=>
        {
            if(item.type=="response_close" && e.IsEvaluationClosing && e.Adaptive.Stage=="training")
            {
                int atLevel=e.Adaptive.Blocks.Count(b=>b.stage=="training" && b.level==e.Adaptive.Level);
                e.Adaptive.Pending=new AdaptiveDecision{block=e.Block,level=e.Adaptive.Level,action=atLevel>=1?"UP":"HOLD",reason="synthetic_policy"};
            }
        };
        double now=0;int frame=0;
        while(e.Phase!=TaskPhase.Complete && now<2000)
        {
            if(e.Phase==TaskPhase.Preparation||e.Phase==TaskPhase.Instructions)e.Advance(now,frame);
            e.Tick(now,frame);
            if(e.Phase==TaskPhase.Running && e.Current.scored && !e.CurrentResult.responded && now-e.CurrentResult.actualOnset>=.5)
                e.Respond(e.Current.expectedMatch,now,frame);
            now+=.25;frame++;
        }
        Check(e.Phase==TaskPhase.Complete,"bounded complete session");
        Check(e.Adaptive.Outcome=="candidate_mastery","success requires level two passes");
        Check(e.Adaptive.Blocks.Count==10,"two baseline, two pretest, four training, two posttest");
        Check(e.Adaptive.Blocks.All(b=>b.valid && b.balancedAccuracy==1),"valid perfect summaries");
        var rows=e.Results.Where(t=>!t.practice).ToList();
        Check(rows.Count(t=>!t.scored)==2,"only two initial memory warmups across evaluations");
        foreach(var block in rows.GroupBy(t=>t.block))Check(block.Count(t=>t.scored)==24,"24 scored per block");
        for(int i=2;i<rows.Count;i++)
        {
            Check(rows[i].expectedMatch==(rows[i].color==rows[i-2].color),"cross-boundary 2-back oracle");
            Check(Math.Abs(rows[i].plannedOnset-rows[i-1].plannedOnset-4.5)<1e-7,"cycle retained");
            if(rows[i].level!=rows[i-1].level)Check(rows[i].transition && rows[i+1].transition,"two transition trials");
        }
        var cap=new AdaptiveTrainingState(new AdaptiveTrainingConfig{includeTransferTests=false});
        for(int i=0;i<5;i++)cap.CompleteBlock(new TrainingBlock{block=i,stage=i<2?"baseline":"training",level=i<2?0:1,valid=true,balancedAccuracy=.9,omissions=0});
        Check(cap.Finished && cap.Outcome=="level_cap","OFF mode also has bounded training");
    }
    static void PauseTest(bool practice)
    {
        var e=new TaskEngine(new TaskConfig(),new AdaptiveTrainingConfig());double now=0;int frame=0;
        while(((!practice && e.Block<1) || e.Current==null || !e.Current.scored) && now<100)
        {if(e.Phase==TaskPhase.Preparation||e.Phase==TaskPhase.Instructions)e.Advance(now,frame);e.Tick(now,frame++);now+=.25;}
        e.Pause(now,frame);e.Resume(now+10,frame+1);
        Check(!e.Current.scored,"first resume warmup");
        e.Tick(now+14.5,frame+2);Check(!e.Current.scored,"second resume warmup");
        e.Tick(now+19,frame+3);Check(e.Current.scored,"scoring resumes after memory warmups");
        Check(e.Results.Any(t=>t.interrupted),"interruption is preserved");
    }
}

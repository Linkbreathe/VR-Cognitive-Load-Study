using System;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEngine;

public static class DistractionValidation
{
    public static string Run()
    {
        var d=UnityEngine.Object.FindFirstObjectByType<EnvironmentalDistractions>();
        var doors=d.doorClips.Select(x=>x.length).ToArray();var voices=d.conversationClips.Select(x=>x.length).ToArray();
        if(d.streetFiles.Length!=8 || doors.Length!=8 || voices.Length!=8)throw new Exception("Incomplete media catalog.");
        if(doors.Any(x=>x<.99 || x>2.01) || voices.Any(x=>x<5 || x>10))throw new Exception("Audio duration outside limits.");
        if(d.doorClips.Concat(d.conversationClips).Any(x=>x.channels!=1))throw new Exception("Non-mono spatial audio.");
        int checks=0;
        for(int seed=1;seed<=1000;seed++)
        {
            var c=new TaskConfig { distractionSeed=seed };
            var easy=DistractionPlan.Create(c,2,8,doors,voices);var hard=DistractionPlan.Create(c,3,8,doors,voices);
            var repeat=DistractionPlan.Create(c,2,8,doors,voices);
            Assert(easy.Count==6 && hard.Count==6,"Event counts");
            Assert(DistractionPlan.Create(c,0,8,doors,voices).Count==0 && DistractionPlan.Create(c,1,8,doors,voices).Count==0,"Practice exclusion");
            foreach(DistractionKind kind in Enum.GetValues(typeof(DistractionKind)))Assert(easy.Count(x=>x.kind==kind)==2 && hard.Count(x=>x.kind==kind)==2,"Balanced types");
            for(int i=0;i<6;i++)
            {
                var a=easy[i];var b=hard[i];var r=repeat[i];
                Assert(JsonUtility.ToJson(a)==JsonUtility.ToJson(r),"Repeatability");
                Assert(a.start==b.start && a.kind==b.kind && a.side!=b.side,"Matched schedules and mirrored sides");
                Assert(a.start>c.warmupTrials*c.trialSeconds && a.start+a.duration<32*c.trialSeconds,"Within scored block");
                Assert(a.kind!=DistractionKind.Street || a.duration>=5 && a.duration<=8,"Video duration");
                if(i>0)Assert(easy[i-1].start+easy[i-1].duration+1.5<a.start && hard[i-1].start+hard[i-1].duration+1.5<b.start,"No overlap with decoder grace");
            }
            Assert(easy.Concat(hard).Where(x=>x.kind==DistractionKind.Street).Select(x=>x.mediaIndex).Distinct().Count()==4,"No street repeats per session");
            c.distractionsEnabled=false;Assert(DistractionPlan.Create(c,2,8,doors,voices).Count==0,"Disabled");
            c.distractionsEnabled=true;c.scoredTrials=2;Assert(DistractionPlan.Create(c,2,8,doors,voices).Count==0,"Short block safety");
        }
        var engine=new TaskEngine(new TaskConfig());engine.Advance(0,0);engine.Advance(0,1);
        engine.Pause(2,2);Assert(engine.BlockElapsed(50)==2,"Pause clock");engine.Resume(52,3);Assert(engine.BlockElapsed(53)==3,"Resume clock");
        Assert(d.doorSources.All(x=>x.transform.position.z<0 && x.spatialBlend==1 && !x.playOnAwake),"Rear sources");
        Assert(d.conversationSources.All(x=>x.transform.position.z<-3 && x.spatialBlend==1),"Outside sources");
        var report="PASS: "+checks+" checks across 1,000 seeds; balanced formal-only schedules, no overlaps, deterministic replay, four distinct streets per session, pause clocks, 24 local media assets, mono audio and requested duration ranges.\n";
        Directory.CreateDirectory("Evidence");File.WriteAllText("Evidence/distraction-validation-v1.4.txt",report);Debug.Log(report);return report;
        void Assert(bool ok,string description){checks++;if(!ok)throw new Exception(description);}
    }
}

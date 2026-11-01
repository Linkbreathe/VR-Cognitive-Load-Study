using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEditor;
using UnityEngine;

namespace EvidenceVR.Editor
{
    public static class TaskPlatformValidation
    {
        static int assertions;
        static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception("Validation failed: " + message); }
        [MenuItem("Evidence VR/Run Automated Validation")]
        public static string Run()
        {
            assertions = 0;
            var c = new TaskConfig();
            foreach (TaskMode mode in new[] { TaskMode.Easy, TaskMode.Hard })
            for (int seed = 0; seed < 1000; seed++)
            {
                var sequence = SequenceGenerator.Generate(c, mode, 30, seed);
                var replay = SequenceGenerator.Generate(c, mode, 30, seed);
                Check(sequence.Count == 32, "stimulus count");
                Check(sequence.Count(t => t.scored && t.expectedMatch) == 10, "exact target ratio");
                Check(sequence.Count(t => !t.scored) == 2, "warmup count");
                for (int i = 0; i < sequence.Count; i++)
                {
                    Check(sequence[i].color == replay[i].color, "deterministic replay");
                    Check(sequence[i].color >= 0 && sequence[i].color < 4, "color bounds");
                    if (i >= 2) Check(sequence[i].expectedMatch == (sequence[i].color == (mode == TaskMode.Easy ? 0 : sequence[i-2].color)), "independent answer oracle");
                }
            }
            BoundaryTests();
            FourSecondBoundaryTests();
            Directory.CreateDirectory("Evidence");
            string root = Path.GetFullPath("Evidence/SyntheticSessions");
            for (int run = 0; run < 5; run++)
            {
                var config = new TaskConfig { seed = 7000 + run, hardFirst = run % 2 != 0 };
                var engine = new TaskEngine(config);
                using (var recorder = new SessionRecorder(root, config, "synthetic_" + run, "automated"))
                {
                    engine.OnEvent += recorder.Event; engine.OnTrial += recorder.Trial;
                    engine.Note("synthetic_test", "Simulated clock and responses; NOT headset or live timing evidence", 0, 0);
                    double now = 0; int frame = 0; int previousBlock = -1, previousTrial = -1;
                    var modes = new List<TaskMode>();
                    while (engine.Phase != TaskPhase.Complete && now < 1000)
                    {
                        if (engine.Phase == TaskPhase.Preparation || engine.Phase == TaskPhase.Instructions || engine.Phase == TaskPhase.Rest)
                        {
                            if (engine.Phase == TaskPhase.Instructions && engine.Block >= 2) modes.Add(engine.Mode);
                            engine.Advance(now,frame);
                        }
                        engine.Tick(now,frame);
                        if (engine.Phase == TaskPhase.Running && (engine.Block != previousBlock || engine.TrialIndex != previousTrial))
                        {
                            previousBlock = engine.Block; previousTrial = engine.TrialIndex;
                            if (engine.Current.scored && engine.TrialIndex % 7 != 0)
                            {
                                double responseAt = now + .35;
                                engine.Respond(engine.TrialIndex % 5 == 0 ? !engine.Current.expectedMatch : engine.Current.expectedMatch, responseAt,frame + 25);
                            }
                        }
                        now += .025; frame++;
                    }
                    Check(engine.Phase == TaskPhase.Complete, "full synthetic session finishes");
                    Check(engine.Results.Count == 88, "88 stimuli including both practices and four warmup pairs");
                    Check(engine.Results.Count(t => !t.practice && t.scored) == 60, "60 formal scored rows");
                    Check(modes.Count == 2 && modes[0] == (config.hardFirst ? TaskMode.Hard : TaskMode.Easy) && modes[0] != modes[1], "counterbalanced order");
                    foreach (var group in engine.Results.GroupBy(t => t.block))
                    {
                        Check(group.All(t => t.actualOnset >= t.plannedOnset - 1e-8), "onsets never early");
                        Check(group.Count(t => !t.scored) == 2, "fresh warmups every block");
                    }
                    recorder.Complete("synthetic_complete");
                    Check(File.ReadAllLines(Path.Combine(recorder.DirectoryPath,"trials.csv")).Length == 65, "formal CSV includes warmups and omissions");
                    Check(File.ReadAllText(Path.Combine(recorder.DirectoryPath,"session.json")).Contains("synthetic_complete"), "closed file reopens");
                }
            }
            string report = "PASS\nAssertions: " + assertions + "\nSequence sets: 2000 (1000 seeds per mode)\nFull synthetic sessions: 5 (88 stimuli each)\nBoundary checks: early/late/duplicate response, timeout, pause/resume, rest gate, config freeze, difficulty guard, abort persistence\nNOT VERIFIED HERE: physical headset, controller hardware, photon onset, device performance, ten-minute soak, five real sessions.\n";
            File.WriteAllText("Evidence/automated-validation.txt", report); Debug.Log(report); return report;
        }
        static TaskEngine Running(TaskConfig c, int formalBlock = 0)
        {
            var e = new TaskEngine(c); e.Advance(0,0); e.Advance(0,0); return e;
        }
        static void BoundaryTests()
        {
            var c = new TaskConfig { trialSeconds=3, responseSeconds=2.5f, visibleSeconds=1 }; var e = Running(c);
            Check(!e.Respond(true,0,0), "warmup rejects answers");
            e.Tick(3,216); e.Tick(6,432);
            bool answer = e.Current.expectedMatch;
            Check(e.Respond(answer,6.2,446), "first response accepted");
            Check(!e.Respond(!answer,6.3,454), "duplicate rejected");
            Check(e.TrialIndex == 2, "early answer does not advance");
            e.Tick(7,504); Check(!e.StimulusVisible,"sphere hidden at 1 s");
            e.Tick(9,648); Check(e.Results[2].correct, "stored first answer");
            Check(!e.Respond(true,11.5,828), "deadline exclusive");
            e.Tick(12,864); Check(e.Results[3].timeout, "omission persists");
            Check(!e.RequestDifficulty(TaskMode.Hard,12.1,870), "difficulty cannot change active block");
            e.Pause(12.2,878); e.Tick(100,7200); Check(e.TrialIndex==4,"pause freezes index");
            e.Resume(100,7200); e.Tick(100.7,7250); Check(e.StimulusVisible, "remaining visible time retained");
            e.Tick(100.81,7258); Check(!e.StimulusVisible,"offset shifted by pause");
            e.Tick(102.8,7402); Check(e.Results[4].interrupted,"interrupted trial flagged");
            c.scoredTrials = 100; Check(e.Config.scoredTrials == 30,"config cloned at session start");
            var copy = e.Config; copy.responseSeconds = 99; Check(e.Config.responseSeconds == 2.5,"config getter immutable copy");
            e.Abort(103,7416,"test"); Check(e.Phase==TaskPhase.Aborted && e.Results.Last().aborted,"abort saves active trial");
            var invalid = new TaskConfig { responseSeconds = 5 }; bool rejected=false;
            try { invalid.Validate(); } catch (ArgumentException) { rejected=true; }
            Check(rejected,"invalid timing rejected");
            var stalled = Running(new TaskConfig()); TaskEvent pauseEvent = null;
            stalled.OnEvent += item => { if(item.type=="pause") pauseEvent=item; };
            stalled.Pause(5,360,"frame_stall",.2);
            Check(pauseEvent.actual == 5 && pauseEvent.frame == 360,"stall log retains actual handling time");
            stalled.Resume(6,432); stalled.Tick(6.7,482);
            Check(stalled.StimulusVisible && stalled.TrialIndex==0,"stall freezes elapsed task time without skipping");
            // Reach rest without answers and enforce duration before rules can appear.
            var r = Running(new TaskConfig()); double t=0;
            while(r.Phase != TaskPhase.Rest && t<500) { t+=3; r.Tick(t, (int)(t*72)); if(r.Phase==TaskPhase.Instructions) r.Advance(t,(int)(t*72)); }
            Check(r.Phase==TaskPhase.Rest,"rest reached"); r.Advance(t+24,1); Check(r.Phase==TaskPhase.Rest,"rest cannot skip"); r.Advance(t+25,2); Check(r.Phase==TaskPhase.Instructions,"rest gate opens");
            Check(r.RequestDifficulty(TaskMode.Easy,t+25,2),"boundary request accepted"); r.Advance(t+25,2); Check(r.TrialIndex==0 && !r.Current.scored,"new difficulty resets memory");
        }
        static void FourSecondBoundaryTests()
        {
            var config=new TaskConfig(); var e=Running(config); int starts=0;
            e.OnEvent+=item=>{if(item.type=="trial_countdown_start")starts++;};
            Check(config.responseSeconds==4 && config.trialSeconds==4.5f,"current response window and onset interval");
            Check(e.ResponseRemaining(0)==4,"countdown starts at four");
            Check(!e.Respond(true,3.9,1),"warmup remains observe-only for whole countdown");
            e.Tick(4.5,2); e.Tick(9,3);
            Check(e.ResponseRemaining(9)==4,"scored trial starts a fresh countdown");
            e.Tick(10,4); Check(e.StimulusVisible,"sphere remains visible after one second");
            Check(e.Respond(e.Current.expectedMatch,12.5,5),"response after old 2.5-second deadline accepted");
            e.Tick(12.999,6); Check(e.StimulusVisible,"sphere remains visible until four seconds after answering");
            Check(e.TrialIndex==2 && e.ResponseRemaining(12.5)==.5,"early response preserves scheduled interval");
            Check(!e.Respond(true,12.6,6),"duplicate in extended window rejected");
            e.Tick(13,7); Check(e.ResponseRemaining(13)==0 && !e.StimulusVisible && e.CurrentResult.offsetTime==13,"countdown and sphere close together at four seconds");
            e.Tick(13.49,8); Check(e.TrialIndex==2,"half-second transition gap retained");
            e.Tick(13.5,9); Check(e.TrialIndex==3,"next onset at 4.5 seconds");
            Check(!e.Respond(true,17.5,10),"four-second deadline is exclusive");
            e.Tick(18,11); Check(e.Results[3].timeout,"extended-window omission recorded");
            e.Pause(18.75,12); e.Tick(100,13);
            Check(e.ResponseRemaining(100)==3.25 && e.TrialIndex==4,"pause freezes countdown and trial");
            e.Resume(100,14); Check(e.ResponseRemaining(100)==3.25,"resume retains remaining response time");
            Check(!e.Respond(true,103.25,15),"resumed deadline remains exclusive");
            Check(starts==4,"one countdown event per new stimulus, none on resume");
            var badVolume=new TaskConfig { onsetCueVolume=-.1f }; bool rejected=false;
            try{badVolume.Validate();}catch(ArgumentException){rejected=true;}
            Check(rejected,"negative onset cue gain rejected");
        }
    }
}

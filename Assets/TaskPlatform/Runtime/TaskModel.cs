using System;
using System.Collections.Generic;

namespace EvidenceVR
{
    public enum TaskMode { Easy = 0, Hard = 2 }
    public enum TaskPhase { Preparation, Instructions, Running, Rest, Paused, Complete, Aborted }

    [Serializable]
    public sealed class TaskConfig
    {
        public string version = "stage1-v1.0";
        public int seed = 20261101;
        public int colorCount = 4;
        public int targetColor = 0;
        public int scoredTrials = 30;
        public int warmupTrials = 2;
        public int easyPracticeTrials = 8;
        public int hardPracticeTrials = 12;
        public float matchRatio = 1f / 3f;
        public float trialSeconds = 3;
        public float visibleSeconds = 1;
        public float responseSeconds = 2.5f;
        public float restSeconds = 25;
        public float distance = 1.5f;
        public float visualAngle = 6;
        public bool hardFirst;
        public string matchAction = "RightPrimary";
        public string nonmatchAction = "RightSecondary";
        public string advanceAction = "LeftPrimary";
        public string pauseAction = "LeftSecondary";
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(version) || colorCount < 2 || colorCount > 4 || targetColor < 0 || targetColor >= colorCount ||
                scoredTrials < 2 || scoredTrials > 1000 || warmupTrials < 2 || warmupTrials > 100 || easyPracticeTrials < 2 || hardPracticeTrials < 2 ||
                !(matchRatio > 0 && matchRatio < 1) || !(visibleSeconds > 0 && visibleSeconds <= responseSeconds && responseSeconds < trialSeconds) ||
                !(restSeconds >= 0 && restSeconds <= 600) || !(distance >= 0.5f && distance <= 5) || !(visualAngle >= 1 && visualAngle <= 20) ||
                string.IsNullOrWhiteSpace(matchAction) || string.IsNullOrWhiteSpace(nonmatchAction) || matchAction == nonmatchAction ||
                matchAction == advanceAction || matchAction == pauseAction || nonmatchAction == advanceAction || nonmatchAction == pauseAction || advanceAction == pauseAction)
                throw new ArgumentException("Invalid task configuration. Check counts, color, timing, geometry and bindings.");
        }
    }

    [Serializable]
    public sealed class TrialSpec
    {
        public int index, color;
        public bool scored, expectedMatch;
    }

    // Explicit xorshift32 and Fisher-Yates: reproducible across Mono and IL2CPP.
    public sealed class StableRandom
    {
        uint state;
        public StableRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x6D2B79F5; }
        public int Next(int max) { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (int)(state % (uint)max); }
    }

    public static class SequenceGenerator
    {
        public static List<TrialSpec> Generate(TaskConfig c, TaskMode mode, int count, int seed)
        {
            c.Validate();
            var rng = new StableRandom(seed);
            var matches = new bool[count];
            int matchCount = (int)Math.Round(count * (double)c.matchRatio, MidpointRounding.AwayFromZero);
            for (int i = 0; i < matchCount; i++) matches[i] = true;
            for (int i = count - 1; i > 0; i--) { int j = rng.Next(i + 1); bool b = matches[i]; matches[i] = matches[j]; matches[j] = b; }
            var result = new List<TrialSpec>();
            for (int i = 0; i < count + c.warmupTrials; i++)
            {
                bool scored = i >= c.warmupTrials;
                int reference = mode == TaskMode.Easy ? c.targetColor : (i >= 2 ? result[i - 2].color : -1);
                int color;
                if (!scored) color = rng.Next(c.colorCount);
                else if (matches[i - c.warmupTrials]) color = reference;
                else { color = rng.Next(c.colorCount - 1); if (color >= reference) color++; }
                result.Add(new TrialSpec { index = i, color = color, scored = scored, expectedMatch = reference >= 0 && color == reference });
            }
            return result;
        }
    }

    [Serializable]
    public sealed class TrialResult
    {
        public int block, trial, color, mode, seed;
        public bool practice, scored, expectedMatch, responded, responseMatch, correct, timeout, interrupted, aborted;
        public double plannedOnset, actualOnset, reactionTime = -1, responseTime = -1, offsetTime = -1, closeTime = -1;
        public int onsetFrame, responseFrame = -1;
    }

    [Serializable]
    public sealed class TaskEvent
    {
        public string type, detail;
        public double actual, planned, deviation;
        public int frame, block, trial;
    }

    public sealed class TaskEngine
    {
        readonly TaskConfig config;
        public TaskConfig Config => UnityEngine.JsonUtility.FromJson<TaskConfig>(UnityEngine.JsonUtility.ToJson(config));
        public TaskPhase Phase { get; private set; } = TaskPhase.Preparation;
        public TaskMode Mode { get; private set; }
        public bool Practice { get; private set; }
        public int Block { get; private set; } = -1;
        public int TrialIndex { get; private set; } = -1;
        public bool StimulusVisible { get; private set; }
        public TrialSpec Current => sequence != null && TrialIndex >= 0 && TrialIndex < sequence.Count ? sequence[TrialIndex] : null;
        public TrialResult CurrentResult { get; private set; }
        public readonly List<TrialResult> Results = new List<TrialResult>();
        public event Action<TaskEvent> OnEvent;
        public event Action<TrialResult> OnTrial;
        List<TrialSpec> sequence;
        double origin, pauseStart, pausedTotal, restEnd;
        TaskPhase resumePhase;
        bool closed;
        TaskMode? requested;
        public TaskEngine(TaskConfig c) { c.Validate(); config = UnityEngine.JsonUtility.FromJson<TaskConfig>(UnityEngine.JsonUtility.ToJson(c)); }
        void Emit(string type, double now, int frame, double planned, string detail = "") => OnEvent?.Invoke(new TaskEvent { type = type, actual = now, planned = planned, deviation = now - planned, frame = frame, block = Block, trial = TrialIndex, detail = detail });
        public void Note(string type, string detail, double now, int frame) => Emit(type, now, frame, now, detail);
        public double RestRemaining(double now) => Math.Max(0, restEnd - now);
        public void Advance(double now, int frame)
        {
            if (Phase == TaskPhase.Preparation) { Block = 0; SetInstructions(now, frame); }
            else if (Phase == TaskPhase.Instructions) BeginBlock(now, frame);
            else if (Phase == TaskPhase.Rest && now >= restEnd) SetInstructions(now, frame);
        }
        void SetInstructions(double now, int frame)
        {
            Practice = Block < 2;
            Mode = Block == 0 ? TaskMode.Easy : Block == 1 ? TaskMode.Hard : ((Block == 2) != config.hardFirst ? TaskMode.Easy : TaskMode.Hard);
            if (requested.HasValue) { Mode = requested.Value; requested = null; }
            Phase = TaskPhase.Instructions; TrialIndex = -1; StimulusVisible = false;
            Emit("rules", now, frame, now, Mode.ToString());
        }
        void BeginBlock(double now, int frame)
        {
            int count = Practice ? (Mode == TaskMode.Easy ? config.easyPracticeTrials : config.hardPracticeTrials) : config.scoredTrials;
            int seed = unchecked(config.seed + Block * 104729);
            sequence = SequenceGenerator.Generate(config, Mode, count, seed);
            Phase = TaskPhase.Running; TrialIndex = 0; origin = now; pausedTotal = 0;
            Emit("block_start", now, frame, now, $"mode={Mode};practice={Practice};seed={seed}");
            BeginTrial(now, frame);
        }
        double Planned => origin + TrialIndex * config.trialSeconds + pausedTotal;
        void BeginTrial(double now, int frame)
        {
            closed = false; StimulusVisible = true;
            CurrentResult = new TrialResult { block = Block, trial = TrialIndex, color = Current.color, mode = (int)Mode, seed = unchecked(config.seed + Block * 104729), practice = Practice, scored = Current.scored, expectedMatch = Current.expectedMatch, plannedOnset = Planned, actualOnset = now, onsetFrame = frame };
            Emit("stimulus_on", now, frame, Planned, "color=" + Current.color);
        }
        public void Tick(double now, int frame)
        {
            if (Phase != TaskPhase.Running) return;
            double elapsed = now - Planned;
            if (StimulusVisible && elapsed >= config.visibleSeconds) { StimulusVisible = false; CurrentResult.offsetTime = now; Emit("stimulus_off", now, frame, Planned + config.visibleSeconds); }
            if (!closed && elapsed >= config.responseSeconds) Close(now, frame);
            if (elapsed >= config.trialSeconds)
            {
                FinishTrial();
                TrialIndex++;
                if (TrialIndex >= sequence.Count)
                {
                    Emit("block_complete", now, frame, origin + sequence.Count * config.trialSeconds + pausedTotal);
                    CurrentResult = null; Block++; StimulusVisible = false;
                    if (Block >= 4) { Phase = TaskPhase.Complete; Emit("session_complete", now, frame, now); }
                    else if (Block == 3) { Phase = TaskPhase.Rest; restEnd = now + config.restSeconds; Emit("rest_start", now, frame, now); }
                    else SetInstructions(now, frame);
                }
                else BeginTrial(now, frame);
            }
        }
        void Close(double now, int frame)
        {
            closed = true; CurrentResult.closeTime = now;
            CurrentResult.timeout = !CurrentResult.responded;
            Emit("response_close", now, frame, Planned + config.responseSeconds);
        }
        void FinishTrial() { if (CurrentResult == null) return; Results.Add(CurrentResult); OnTrial?.Invoke(CurrentResult); CurrentResult = null; }
        public bool Respond(bool match, double now, int frame)
        {
            string rejection = Phase != TaskPhase.Running ? "not_running" : !Current.scored ? "warmup" : CurrentResult.responded ? "duplicate" : now - Planned >= config.responseSeconds || now < CurrentResult.actualOnset ? "outside_window" : "";
            Emit("button", now, frame, now, (match ? "match" : "nonmatch") + (rejection == "" ? ":accepted" : ":" + rejection));
            if (rejection != "") return false;
            CurrentResult.responded = true; CurrentResult.responseMatch = match;
            CurrentResult.correct = match == Current.expectedMatch;
            CurrentResult.responseTime = now; CurrentResult.responseFrame = frame;
            CurrentResult.reactionTime = now - CurrentResult.actualOnset;
            return true;
        }
        public void Pause(double now, int frame, string reason = "operator", double? freezeAt = null)
        {
            if (Phase != TaskPhase.Running && Phase != TaskPhase.Rest && Phase != TaskPhase.Instructions) return;
            resumePhase = Phase; Phase = TaskPhase.Paused; pauseStart = freezeAt ?? now;
            if (CurrentResult != null) CurrentResult.interrupted = true;
            Emit("pause", now, frame, now, reason + (freezeAt.HasValue ? ";frozen_since=" + pauseStart.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : ""));
        }
        public void Resume(double now, int frame)
        {
            if (Phase != TaskPhase.Paused) return;
            double duration = now - pauseStart;
            if (resumePhase == TaskPhase.Running) pausedTotal += duration;
            if (resumePhase == TaskPhase.Rest) restEnd += duration;
            Phase = resumePhase; Emit("resume", now, frame, now, "pause_seconds=" + duration.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        // Stage 1 accepts external requests only between blocks; never mutates an active sequence.
        public bool RequestDifficulty(TaskMode mode, double now, int frame)
        {
            if (mode != TaskMode.Easy && mode != TaskMode.Hard) return false;
            if (Phase != TaskPhase.Instructions && Phase != TaskPhase.Rest && Phase != TaskPhase.Preparation) { Emit("difficulty_rejected", now, frame, now, "Only allowed between blocks"); return false; }
            if (Phase == TaskPhase.Instructions) { Mode = mode; Emit("rules", now, frame, now, mode.ToString()); }
            else requested = mode;
            Emit("difficulty_request", now, frame, now, mode.ToString()); return true;
        }
        public void Abort(double now, int frame, string reason)
        {
            if (Phase == TaskPhase.Complete || Phase == TaskPhase.Aborted) return;
            if (CurrentResult != null) { CurrentResult.aborted = true; CurrentResult.interrupted = true; FinishTrial(); }
            StimulusVisible = false; Phase = TaskPhase.Aborted; Emit("session_aborted", now, frame, now, reason);
        }
    }
}

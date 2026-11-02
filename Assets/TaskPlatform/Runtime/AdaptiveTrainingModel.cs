using System;
using System.Collections.Generic;
using System.Linq;

namespace EvidenceVR
{
    [Serializable]
    public sealed class AdaptiveTrainingConfig
    {
        public string version = "2.0.0-stage2-dev4";
        public string mode = "OFF";
        public string protocol = "training";
        public string controllerUrl = "http://127.0.0.1:8765";
        public string participantCode = "engineering";
        public string sessionType = "engineering";
        public string controlBasis = "behavioral_fallback";
        public int seed = 20261102, counterbalanceGroup, scoredTrials = 24;
        public int baselineBlocks = 2, maxTrainingBlocks = 8, maxConsecutiveBlocks = 3;
        public bool enabled, includeTransferTests = true, allowMediaRepeats = true;
        public float mediaVolume = .25f, onsetJitterSeconds = 1;
        public float[] mediaOnsets = { 19, 51, 83 };
        public float videoAngle = 0, videoWidth = 3.2f, videoHeight = 1.8f;
        public string videoPlacement = "front_wall_center";
        public float[] practiceMediaOnsets = {10,28,46};
        public float practiceOnsetJitterSeconds = .5f;
        public string mediaManifest = "adaptive-media-manifest.json";
        public bool UpgradeLegacyPresentation()
        {
            if(version!="2.0.0-stage2-dev1" && version!="2.0.0-stage2-dev3")return false;
            version="2.0.0-stage2-dev4";videoAngle=0;videoWidth=3.2f;videoHeight=1.8f;videoPlacement="front_wall_center";
            practiceMediaOnsets=new[]{10f,28f,46f};practiceOnsetJitterSeconds=.5f;return true;
        }
        public void Validate()
        {
            if (version != "2.0.0-stage2-dev4" || !new[]{"OFF","SHADOW","ACTIVE"}.Contains(mode) ||
                !new[]{"training","calibration"}.Contains(protocol) ||
                !new[]{"behavioral_fallback","physiological"}.Contains(controlBasis) ||
                !new[]{"engineering","participant"}.Contains(sessionType) || string.IsNullOrWhiteSpace(participantCode) ||
                scoredTrials != 24 || baselineBlocks != 2 || maxTrainingBlocks < 1 || maxTrainingBlocks > 8 ||
                maxConsecutiveBlocks < 2 || maxConsecutiveBlocks > 3 || counterbalanceGroup < 0 || counterbalanceGroup > 3 ||
                !(mediaVolume >= 0 && mediaVolume <= 1) || !(onsetJitterSeconds >= 0 && onsetJitterSeconds <= 2) ||
                videoAngle != 0 || videoWidth != 3.2f || videoHeight != 1.8f || videoPlacement!="front_wall_center" ||
                practiceMediaOnsets==null || !practiceMediaOnsets.SequenceEqual(new[]{10f,28f,46f}) || practiceOnsetJitterSeconds!=.5f ||
                mediaOnsets == null || mediaOnsets.Length != 3)
                throw new ArgumentException("Invalid adaptive configuration. This revision freezes 24 trials, four-second responses and 4.5-second cycles.");
            for (int i=0;i<3;i++)
                if (!(mediaOnsets[i]-onsetJitterSeconds >= 0 && mediaOnsets[i]+onsetJitterSeconds+12 <= 108) ||
                    (i>0 && mediaOnsets[i]-mediaOnsets[i-1] <= 12+2*onsetJitterSeconds))
                    throw new ArgumentException("Adaptive media slots must fit the block without overlap.");
            if (!Uri.TryCreate(controllerUrl, UriKind.Absolute, out var uri) || uri.Scheme != "http")
                throw new ArgumentException("Controller URL must be an explicit HTTP endpoint on the study computer.");
        }
    }

    [Serializable]
    public sealed class TrainingBlock
    {
        public int block, level, eligible, matches, nonmatches, omissions, transitionTrials;
        public string stage, sessionId, configVersion;
        public bool valid;
        public double start, end, balancedAccuracy = -1, medianCorrectRt = -1;
        public static TrainingBlock Summarize(IEnumerable<TrialResult> records, int block, int level, string stage)
        {
            var all = records.Where(t=>t.block==block && t.scored).ToList();
            var clean = all.Where(t=>!t.transition && !t.interrupted && !t.aborted).ToList();
            var m = clean.Where(t=>t.expectedMatch).ToList();
            var n = clean.Where(t=>!t.expectedMatch).ToList();
            var rt = clean.Where(t=>t.responded && t.correct && t.reactionTime>=0).Select(t=>t.reactionTime).OrderBy(t=>t).ToList();
            return new TrainingBlock { block=block,level=level,stage=stage,eligible=clean.Count,matches=m.Count,nonmatches=n.Count,
                omissions=clean.Count(t=>!t.responded),transitionTrials=all.Count(t=>t.transition),
                valid=all.Count==24 && !all.Any(t=>t.interrupted||t.aborted) && clean.Count>=20 && m.Count>=4 && n.Count>=4,
                start=all.Count>0?all[0].plannedOnset:0,end=all.Count>0?all.Last().plannedOnset+4.5:0,
                balancedAccuracy=m.Count>0 && n.Count>0 ? .5*(m.Count(t=>t.correct)/(double)m.Count+n.Count(t=>t.correct)/(double)n.Count) : -1,
                medianCorrectRt=rt.Count==0?-1:rt.Count%2==1?rt[rt.Count/2]:(rt[rt.Count/2-1]+rt[rt.Count/2])*.5 };
        }
    }
    [Serializable]
    public sealed class AdaptiveDecision
    {
        public string sessionId, requestId, action, reason, basis, configVersion;
        public int block, level;
        public bool physiologicalEligible;
    }

    public sealed class AdaptiveTrainingState
    {
        public readonly AdaptiveTrainingConfig Config;
        public readonly List<TrainingBlock> Blocks = new List<TrainingBlock>();
        public string Stage { get; private set; } = "baseline";
        public int Level { get; private set; }
        public int TrainingCount { get; private set; }
        public int ConsecutiveCount { get; private set; }
        public bool Finished { get; private set; }
        public bool Transition { get; private set; }
        public string Outcome { get; private set; } = "in_progress";
        public AdaptiveDecision Pending;
        public int? ManualLevel;
        public string LastAction { get; private set; } = "HOLD";
        public string LastReason { get; private set; } = "initial";
        int stageCount;
        public AdaptiveTrainingState(AdaptiveTrainingConfig config) { config.Validate(); Config=config; }
        public void CompleteBlock(TrainingBlock summary)
        {
            Blocks.Add(summary); stageCount++; int before=Level;
            LastAction="HOLD"; LastReason="fixed_protocol";
            if (Stage=="baseline" && stageCount>=Config.baselineBlocks)
            {
                if (!Blocks.All(b=>b.valid && b.balancedAccuracy>=.75 && b.omissions<=2))
                { Finish("baseline_unstable"); return; }
                Stage=Config.protocol=="calibration"?"calibration":Config.includeTransferTests?"pretest":"training"; stageCount=0;
                Level=Stage=="training"?1:FirstTestLevel;
            }
            else if (Stage=="pretest" || Stage=="posttest" || Stage=="calibration")
            {
                if (stageCount==1) Level=3-FirstTestLevel;
                else if(Stage=="calibration")Finish("calibration_complete");
                else if (Stage=="posttest") Finish(Outcome=="in_progress"?"completed_without_mastery":Outcome);
                else { Stage="training";stageCount=0;Level=1; }
            }
            else if (Stage=="training")
            {
                TrainingCount++;ConsecutiveCount++;
                bool usable=Pending!=null && Pending.block==summary.block && Pending.level==Level;
                if (Config.mode=="ACTIVE" && usable)
                {
                    LastAction=Pending.action;LastReason=Pending.reason;
                    if (Pending.action=="REST") { Finish("rest_requested:"+Pending.reason); Pending=null;return; }
                    if (Pending.action=="UP")
                    {
                        if (Level==2) EndTraining("candidate_mastery");
                        else Level++;
                    }
                    else if (Pending.action=="DOWN") { if(Level==0) { Finish("difficulty_at_level_zero");return; } Level--; }
                }
                else LastReason=Config.mode=="ACTIVE"?"controller_unavailable_hold":Config.mode.ToLowerInvariant();
                if (Config.mode!="ACTIVE" && ManualLevel.HasValue)
                { Level=ManualLevel.Value;LastAction="MANUAL";LastReason="operator_boundary_request"; }
                if(Stage=="training" && (TrainingCount>=Config.maxTrainingBlocks || (Level==before && ConsecutiveCount>=Config.maxConsecutiveBlocks)))
                    EndTraining(TrainingCount>=Config.maxTrainingBlocks?"training_cap":"level_cap");
            }
            Transition=Level!=before;
            if(Transition)ConsecutiveCount=0;
            ManualLevel=null;Pending=null;
        }
        int FirstTestLevel => Config.counterbalanceGroup%2==0?1:2;
        void EndTraining(string outcome)
        { Outcome=outcome;if(Config.includeTransferTests){Stage="posttest";stageCount=0;Level=FirstTestLevel;}else Finish(outcome); }
        void Finish(string outcome) { Finished=true;Outcome=outcome;LastAction="REST";LastReason=outcome; }
    }
}

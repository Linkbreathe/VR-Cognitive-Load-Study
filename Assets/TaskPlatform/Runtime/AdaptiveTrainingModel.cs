using System;
using System.Collections.Generic;
using System.Linq;

namespace EvidenceVR
{
    [Serializable]
    public sealed class AdaptiveTrainingConfig
    {
        public string version = "2.0.0-stage2-dev5";
        public string evaluationPolicy = "rolling10-stride2-v1";
        public int rollingWindowTrials=10, rollingStrideTrials=2;
        public string mode = "OFF";
        public string protocol = "training";
        public string controllerUrl = "http://127.0.0.1:8765";
        public string participantCode = "engineering";
        public string sessionType = "engineering";
        public string controlBasis = "behavioral_fallback";
        public int seed = 20261102, counterbalanceGroup, scoredTrials = 24;
        public int baselineBlocks = 2, maxTrainingBlocks = 8, maxConsecutiveBlocks = 3;
        public bool enabled, includeTransferTests = true, allowMediaRepeats = true;
        public bool engineeringGazeHighlight;
        public bool engineeringGazeRay;
        public float mediaVolume = .25f, onsetJitterSeconds = 1;
        public float[] mediaOnsets = { 19, 51, 83 };
        public float videoAngle = 0, videoWidth = 3.2f, videoHeight = 1.8f;
        public string videoPlacement = "front_wall_center";
        public float[] practiceMediaOnsets = {10,28,46};
        public float practiceOnsetJitterSeconds = .5f;
        public string mediaManifest = "adaptive-media-manifest.json";
        public const string PersonalVersion = "2.0.0-stage2-dev11";
        public bool IsPersonalized => version==PersonalVersion && protocol=="personalized-rf-v2";
        public int calibrationBlocks=8, trainingTrials=96;
        public string assignmentId="engineering-unallocated";
        public string calibrationFormUrl="", trainingFormUrl="";
        public string questionnaireAdministration=TaskEngine.PersonalQuestionnaireAdministration;
        public static AdaptiveTrainingConfig Personalized() => new AdaptiveTrainingConfig {
            version=PersonalVersion, protocol="personalized-rf-v2", enabled=true, mode="ACTIVE",
            evaluationPolicy="physio5-behavior20-stride5-v2", controlBasis="physiological",
            rollingWindowTrials=20, rollingStrideTrials=5, includeTransferTests=false };
        public bool UpgradeLegacyPresentation()
        {
            if(version!="2.0.0-stage2-dev1" && version!="2.0.0-stage2-dev3" && version!="2.0.0-stage2-dev4")return false;
            version="2.0.0-stage2-dev5";evaluationPolicy="rolling10-stride2-v1";rollingWindowTrials=10;rollingStrideTrials=2;
            videoAngle=0;videoWidth=3.2f;videoHeight=1.8f;videoPlacement="front_wall_center";
            practiceMediaOnsets=new[]{10f,28f,46f};practiceOnsetJitterSeconds=.5f;return true;
        }
        public void Validate()
        {
            if(IsPersonalized)
            {
                if(!new[]{"OFF","SHADOW","ACTIVE"}.Contains(mode) || !new[]{"engineering","participant"}.Contains(sessionType) ||
                    questionnaireAdministration!=TaskEngine.PersonalQuestionnaireAdministration ||
                    string.IsNullOrWhiteSpace(participantCode) || (calibrationBlocks!=6 && calibrationBlocks!=8) || trainingTrials!=96 ||
                    counterbalanceGroup<0 || counterbalanceGroup>3 || evaluationPolicy!="physio5-behavior20-stride5-v2" ||
                    videoWidth!=3.2f || videoHeight!=1.8f || videoPlacement!="front_wall_center" ||
                    !(mediaVolume>=0 && mediaVolume<=1) || !(onsetJitterSeconds>=0 && onsetJitterSeconds<=1) ||
                    !Uri.TryCreate(controllerUrl,UriKind.Absolute,out var endpoint) || endpoint.Scheme!="http" ||
                    (sessionType=="participant" && (string.IsNullOrWhiteSpace(assignmentId) || assignmentId=="engineering-unallocated" || mode!="ACTIVE")))
                    throw new ArgumentException("Invalid dev11 personalized protocol configuration or participant allocation.");
                return;
            }
            if (version != "2.0.0-stage2-dev5" || evaluationPolicy!="rolling10-stride2-v1" || rollingWindowTrials!=10 || rollingStrideTrials!=2 || !new[]{"OFF","SHADOW","ACTIVE"}.Contains(mode) ||
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
        public bool valid, mixedLevel;
        public int[] levels;
        public double start, end, balancedAccuracy = -1, medianCorrectRt = -1;
        public static TrainingBlock Summarize(IEnumerable<TrialResult> records, int block, int level, string stage)
        {
            var all = records.Where(t=>t.block==block && t.scored).ToList();
            var clean = all.Where(t=>!t.transition && !t.interrupted && !t.aborted).ToList();
            var m = clean.Where(t=>t.expectedMatch).ToList();
            var n = clean.Where(t=>!t.expectedMatch).ToList();
            var rt = clean.Where(t=>t.responded && t.correct && t.reactionTime>=0).Select(t=>t.reactionTime).OrderBy(t=>t).ToList();
            var levels=all.Select(t=>t.level).Distinct().OrderBy(x=>x).ToArray();
            return new TrainingBlock { block=block,level=levels.Length>1?-1:levels.Length==1?levels[0]:level,levels=levels,mixedLevel=levels.Length>1,stage=stage,eligible=clean.Count,matches=m.Count,nonmatches=n.Count,
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
        public int evaluationId, epoch, lastTrial;
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
        string trainingEndRequested;
        public AdaptiveTrainingState(AdaptiveTrainingConfig config) { config.Validate(); Config=config; }
        internal void SetPersonalStage(string stage,int level)
        { if(!Config.IsPersonalized)throw new InvalidOperationException();Stage=stage;Level=level;Pending=null; }
        internal void SetPersonalLevel(int level)
        { if(!Config.IsPersonalized || level<0 || level>1)throw new ArgumentException();Level=level;Pending=null; }
        public bool ApplyRollingDecision()
        {
            if(Stage!="training" || Finished || Config.mode!="ACTIVE" || Pending==null)return false;
            var decision=Pending;Pending=null;int before=Level;
            LastAction=decision.action;LastReason=decision.reason;
            if(decision.action=="REST")Finish("rest_requested:"+decision.reason);
            else if(decision.action=="DOWN")
            {if(Level==0)Finish("difficulty_at_level_zero");else Level--;}
            else if(decision.action=="UP")
            {if(Level==2)trainingEndRequested="candidate_mastery";else Level++;}
            if(Level!=before){ConsecutiveCount=0;Transition=true;}
            return Level!=before || Finished;
        }
        public bool TrainingEndRequested=>trainingEndRequested!=null;
        public void InvalidatePendingAttainment(){trainingEndRequested=null;}
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
                LastReason="statistics_boundary";
                if(trainingEndRequested!=null)EndTraining(trainingEndRequested);
                if (Stage=="training" && Config.mode!="ACTIVE" && ManualLevel.HasValue)
                { Level=ManualLevel.Value;LastAction="MANUAL";LastReason="operator_boundary_request"; }
                if(!Finished && Stage=="training" && (TrainingCount>=Config.maxTrainingBlocks || (Level==before && ConsecutiveCount>=Config.maxConsecutiveBlocks)))
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

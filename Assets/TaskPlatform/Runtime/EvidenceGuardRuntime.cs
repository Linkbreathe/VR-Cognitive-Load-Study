using System;
using System.Collections.Generic;

namespace EvidenceVR
{
    // Optional transport contract; the Python package owns all evidence judgments.
    [Serializable]
    public sealed class GuardGazeSample
    {
        public long id;
        public double start,end;
        public int level;
        public bool valid,taskVisible,movieVisible;
        public string target;
    }

    public static class EvidenceGuardContract
    {
        public const string Revision="evidence-guard-v3-minimal-1";
        public static bool Allows(AdaptiveTrainingConfig config,AdaptiveDecision decision,string stage="")
        {
            if(config.IsLayerAB && string.IsNullOrEmpty(stage) && decision.action=="ON")return false;
            if(LayerABStudy.GuardMode(config,stage)!="enforce" || decision.action!="ON")return true;
            return decision.guardMode=="enforce" && decision.guardRevision==Revision &&
                decision.guardVerdict=="ALLOW" && decision.guardRequestId==decision.requestId;
        }
    }

    public sealed partial class AdaptiveRuntime
    {
        readonly List<GuardGazeSample> guardGaze=new List<GuardGazeSample>();
        GuardGazeSample previousGuardGaze;
        long guardGazeId;
        void RecordGuardGaze(double now,GazeRegionSample sample,bool taskVisible,bool movieVisible)
        {
            if(!config.IsPersonalized || config.evidenceGuardMode=="off")return;
            // Attribute elapsed time to the previous tracked-eye observation; cap gaps.
            // No extrapolation through pauses, hidden stimuli, or missing tracking.
            if(previousGuardGaze!=null)
            {
                previousGuardGaze.end=Math.Min(now,previousGuardGaze.start+.1);
                if(guardGaze.Count<4096)guardGaze.Add(previousGuardGaze);
                else engine.Note("evidence_guard_gaze_overflow","coverage_missing;upgrade_requires_valid_review",now,UnityEngine.Time.frameCount);
            }
            previousGuardGaze=new GuardGazeSample{id=++guardGazeId,start=now,end=now,
                level=engine.Practice?1:engine.Adaptive.Level,valid=sample.valid,
                target=sample.Target=="video"?"movie":sample.Target,taskVisible=taskVisible,movieVisible=movieVisible};
        }
    }
}

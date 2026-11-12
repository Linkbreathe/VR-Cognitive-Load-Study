using System;

namespace EvidenceVR
{
    public static class LayerABStudy
    {
        public const string SlidingRevision="sliding-five-close-v1";
        public const string Revision="layer-ab-v1";
        public const string QuestionnaireRevision="raw-tlx7-ueq4-experience7-preference-google-forms-v5.1";
        public static bool FreshSlidingReply(string revision,double received,double deadline,int activeTrial,int closedCount)
            =>revision==SlidingRevision && received<deadline && activeTrial==closedCount-1;
        public static bool IsTraining(string stage)=>stage=="random" || stage=="bioadaptive" || stage=="layer_a" || stage=="layer_b" || stage=="fixed_exposure";
        public static string Condition(string stage)=>stage=="layer_a"?"A":stage=="layer_b"?"B":stage=="fixed_exposure"?"Fixed":"";
        public static string GuardMode(AdaptiveTrainingConfig config,string stage)=>config.IsLayerAB?(stage=="layer_b"?"enforce":"observe"):config.evidenceGuardMode;
        public static string Stage(AdaptiveTrainingConfig config,int ordinal)
        {
            if(config.IndependentComparison)return config.comparisonStage;
            bool aFirst=config.counterbalanceGroup/2==0;
            return (ordinal==1)==aFirst?"layer_a":"layer_b";
        }
    }
}

using System;

namespace EvidenceVR
{
    public static class LayerABStudy
    {
        public const string Revision="layer-ab-v1";
        public const string QuestionnaireRevision="raw-tlx6-preference-google-forms-v2";
        public static bool IsTraining(string stage)=>stage=="random" || stage=="bioadaptive" || stage=="layer_a" || stage=="layer_b";
        public static string Condition(string stage)=>stage=="layer_a"?"A":stage=="layer_b"?"B":"";
        public static string GuardMode(AdaptiveTrainingConfig config,string stage)=>config.IsLayerAB?(stage=="layer_b"?"enforce":"observe"):config.evidenceGuardMode;
        public static string Stage(AdaptiveTrainingConfig config,int ordinal)
        {
            bool aFirst=config.counterbalanceGroup/2==0;
            return (ordinal==1)==aFirst?"layer_a":"layer_b";
        }
    }
}

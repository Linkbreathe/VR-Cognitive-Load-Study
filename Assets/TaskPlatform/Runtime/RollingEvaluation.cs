using System;
using System.Collections.Generic;
using System.Linq;

namespace EvidenceVR
{
    [Serializable]
    public sealed class RollingEvaluation
    {
        public int evaluationId, epoch, block, level, firstTrial, lastTrial, eligible, matches, nonmatches, omissions;
        public string stage="training";
        public bool valid;
        public double start, end, conditionStart, balancedAccuracy=-1, medianCorrectRt=-1;
        public static RollingEvaluation Summarize(List<TrialResult> rows,int id,int epoch,double conditionStart)
        {
            var first=rows.First();var last=rows.Last();
            var matches=rows.Where(t=>t.expectedMatch).ToList();var nonmatches=rows.Where(t=>!t.expectedMatch).ToList();
            var rt=rows.Where(t=>t.responded && t.correct && t.reactionTime>=0).Select(t=>t.reactionTime).OrderBy(t=>t).ToList();
            return new RollingEvaluation{evaluationId=id,epoch=epoch,block=last.block,level=last.level,firstTrial=first.trial,lastTrial=last.trial,
                eligible=rows.Count,matches=matches.Count,nonmatches=nonmatches.Count,omissions=rows.Count(t=>!t.responded),
                valid=rows.Count==10 && matches.Count>=3 && nonmatches.Count>=3,
                start=first.actualOnset,end=last.closeTime,conditionStart=conditionStart,
                balancedAccuracy=matches.Count>0 && nonmatches.Count>0?.5*(matches.Count(t=>t.correct)/(double)matches.Count+nonmatches.Count(t=>t.correct)/(double)nonmatches.Count):-1,
                medianCorrectRt=rt.Count==0?-1:rt.Count%2==1?rt[rt.Count/2]:(rt[rt.Count/2-1]+rt[rt.Count/2])*.5};
        }
    }
}

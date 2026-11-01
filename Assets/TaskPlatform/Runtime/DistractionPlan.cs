using System;
using System.Collections.Generic;

namespace EvidenceVR
{
    public enum DistractionKind { Street, Door, Conversation }

    [Serializable]
    public sealed class DistractionEvent
    {
        public int index, mediaIndex, side;
        public DistractionKind kind;
        public double start, duration;
    }

    public static class DistractionPlan
    {
        public const string Version="distractions-v1-balanced-slots";
        public static List<DistractionEvent> Create(TaskConfig config,int block,int videoCount,float[] doorDurations,float[] voiceDurations)
        {
            var result=new List<DistractionEvent>();
            if(!config.distractionsEnabled || block<2 || block>3)return result;
            double usable=config.scoredTrials*config.trialSeconds-8;
            int repeats=Math.Min(config.distractionsPerTypePerBlock,Math.Max(0,(int)(usable/45)));
            int count=repeats*3;
            if(count==0)return result;
            if(videoCount==0 || doorDurations.Length==0 || voiceDurations.Length==0)throw new ArgumentException("Distraction media catalog is empty.");
            var random=new StableRandom(config.distractionSeed);
            var kinds=new List<DistractionKind>();
            for(int i=0;i<repeats;i++)foreach(DistractionKind kind in Enum.GetValues(typeof(DistractionKind)))kinds.Add(kind);
            Shuffle(kinds,random);
            var bags=new[]{Bag(videoCount,random),Bag(doorDurations.Length,random),Bag(voiceDurations.Length,random)};
            var used=new int[3];
            for(int i=0;i<count;i++)
            {
                var kind=kinds[i]; int k=(int)kind;
                int pick=bags[k][(used[k]+repeats*(block-2))%bags[k].Count]; used[k]++;
                double duration=kind==DistractionKind.Street ? 5+random.Next(3001)/1000.0 : kind==DistractionKind.Door ? doorDurations[pick] : voiceDurations[pick];
                result.Add(new DistractionEvent { index=i,kind=kind,mediaIndex=pick,side=(i+block)%2,start=config.warmupTrials*config.trialSeconds+4+i*usable/count+random.Next(2001)/1000.0,duration=duration });
            }
            return result;
        }
        static List<int> Bag(int count,StableRandom random)
        {
            var bag=new List<int>(); for(int i=0;i<count;i++)bag.Add(i); Shuffle(bag,random); return bag;
        }
        static void Shuffle<T>(List<T> items,StableRandom random)
        {
            for(int i=items.Count-1;i>0;i--){int j=random.Next(i+1);T value=items[i];items[i]=items[j];items[j]=value;}
        }
    }
}

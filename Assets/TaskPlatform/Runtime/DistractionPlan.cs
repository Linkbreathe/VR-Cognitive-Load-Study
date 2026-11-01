using System;
using System.Collections.Generic;

namespace EvidenceVR
{
    public enum DistractionKind { Street, Door, Conversation }

    [Serializable]
    public sealed class DistractionEvent
    {
        public int index, mediaIndex, side, windowPosition;
        public DistractionKind kind;
        public double start, duration;
    }

    public static class DistractionPlan
    {
        public const string Version="distractions-v2-front-wall-3-or-4-trials";
        public static List<DistractionEvent> Create(TaskConfig config,int block,int videoCount,float[] doorDurations,float[] voiceDurations)
        {
            var result=new List<DistractionEvent>();
            if(!config.distractionsEnabled || block<2 || block>3)return result;
            // Reserve the longest exposure plus decoder grace; never truncate a late event.
            const double maxExposure=10, decoderGrace=1.5, firstOffset=.5;
            if(config.trialSeconds*3<maxExposure+decoderGrace)return result;
            double usable=config.scoredTrials*config.trialSeconds-firstOffset-maxExposure-decoderGrace;
            int capacity=usable<0 ? 0 : 1+(int)Math.Floor(usable/(3*config.trialSeconds));
            int repeats=Math.Min(config.distractionsPerTypePerBlock,capacity/3);
            int count=repeats*3;
            if(count==0)return result;
            if(videoCount==0 || doorDurations.Length==0 || voiceDurations.Length==0)throw new ArgumentException("Distraction media catalog is empty.");
            var random=new StableRandom(config.distractionSeed);
            var kinds=new List<DistractionKind>();
            for(int i=0;i<repeats;i++)foreach(DistractionKind kind in Enum.GetValues(typeof(DistractionKind)))kinds.Add(kind);
            Shuffle(kinds,random);
            int gapCount=count-1;
            int extraCapacity=(int)Math.Floor(usable/config.trialSeconds)-gapCount*3;
            int fourTrialGaps=Math.Min(Math.Max(0,extraCapacity),(int)Math.Round(gapCount*.4));
            var gaps=new List<int>();for(int i=0;i<gapCount;i++)gaps.Add(i<fourTrialGaps ? 4 : 3);
            Shuffle(gaps,random);
            var bags=new[]{Bag(videoCount,random),Bag(doorDurations.Length,random),Bag(voiceDurations.Length,random)};
            var positions=Bag(5,random);
            var used=new int[3];
            double onset=config.warmupTrials*config.trialSeconds+firstOffset;
            for(int i=0;i<count;i++)
            {
                var kind=kinds[i]; int k=(int)kind;
                int ordinal=used[k]+repeats*(block-2);
                int pick=bags[k][ordinal%bags[k].Count]; used[k]++;
                double duration=kind==DistractionKind.Street ? 5+random.Next(3001)/1000.0 : kind==DistractionKind.Door ? doorDurations[pick] : voiceDurations[pick];
                result.Add(new DistractionEvent { index=i,kind=kind,mediaIndex=pick,side=(i+block)%2,windowPosition=kind==DistractionKind.Street ? positions[ordinal%positions.Count] : -1,start=onset,duration=duration });
                if(i<gapCount)onset+=gaps[i]*config.trialSeconds;
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

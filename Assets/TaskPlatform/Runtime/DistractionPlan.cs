using System;
using System.Collections.Generic;

namespace EvidenceVR
{
    public enum DistractionKind { Street, Environment, Speech }

    [Serializable]
    public sealed class DistractionEvent
    {
        public int index, mediaIndex, side, windowPosition;
        public DistractionKind kind;
        public double start, duration;
    }

    public static class DistractionPlan
    {
        public const string Version="distractions-v4-five-videos-six-audio";
        public static List<DistractionEvent> Create(TaskConfig config,int block,int videoCount,float[] doorDurations,float[] voiceDurations,int[] environmentFamilies=null,int[] speechFamilies=null)
        {
            var result=new List<DistractionEvent>();
            if(!config.distractionsEnabled || block<2 || block>3)return result;
            // Reserve the longest exposure plus decoder grace; never truncate a late event.
            const double maxExposure=10, decoderGrace=1.5, firstOffset=.5;
            double usable=config.scoredTrials*config.trialSeconds-firstOffset-maxExposure-decoderGrace;
            int capacity=usable<0 ? 0 : 1+(int)Math.Floor(usable/(maxExposure+decoderGrace+.1));
            int repeats=Math.Min(config.distractionsPerTypePerBlock,capacity/3);
            int streetCount=Math.Min(config.streetEventsPerBlock,capacity-repeats*2);
            int count=repeats*2+streetCount;
            if(count==0)return result;
            if(videoCount==0 || doorDurations.Length==0 || voiceDurations.Length==0)throw new ArgumentException("Distraction media catalog is empty.");
            var random=new StableRandom(config.distractionSeed);
            var audioKinds=new List<DistractionKind>();
            for(int i=0;i<repeats;i++){audioKinds.Add(DistractionKind.Environment);audioKinds.Add(DistractionKind.Speech);}
            Shuffle(audioKinds,random);
            var kinds=new List<DistractionKind>();int videoSlot=0,audioSlot=0;
            // Distribute video evenly through the block; shuffle only the interleaved audio types.
            for(int i=0;i<count;i++)
            {
                if(videoSlot<streetCount && i==(int)Math.Round((videoSlot+.5)*count/streetCount-.5))
                {kinds.Add(DistractionKind.Street);videoSlot++;}
                else kinds.Add(audioKinds[audioSlot++]);
            }
            double gap=count>1 ? usable/(count-1) : 0;
            var bags=new[]{Bag(videoCount,random),Bag(doorDurations.Length,random),Bag(voiceDurations.Length,random)};
            var positions=Bag(5,random);
            var familyOrder=Bag(3,random);
            var used=new int[3];
            double onset=config.warmupTrials*config.trialSeconds+firstOffset;
            for(int i=0;i<count;i++)
            {
                var kind=kinds[i]; int k=(int)kind;
                int ordinal=used[k]+(kind==DistractionKind.Street ? streetCount : repeats)*(block-2);
                int pick=bags[k][ordinal%bags[k].Count]; used[k]++;
                if(kind!=DistractionKind.Street)
                {
                    var families=kind==DistractionKind.Environment ? environmentFamilies : speechFamilies;
                    if(families!=null)
                    {
                        var familyBag=bags[k].FindAll(x=>families[x]==familyOrder[ordinal%3]);
                        if(familyBag.Count==0)throw new ArgumentException("Missing audio family in catalog.");
                        pick=familyBag[(ordinal/3)%familyBag.Count];
                    }
                }
                double duration=kind==DistractionKind.Street ? 10 : kind==DistractionKind.Environment ? doorDurations[pick] : voiceDurations[pick];
                result.Add(new DistractionEvent { index=i,kind=kind,mediaIndex=pick,side=(i+block)%2,windowPosition=kind==DistractionKind.Street ? positions[ordinal%positions.Count] : -1,start=onset,duration=duration });
                onset+=gap;
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

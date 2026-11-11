using System.Collections.Generic;
using UnityEngine;

namespace EvidenceVR
{
    // Angular spread is diagnostic only. Small movements cannot establish staleness;
    // rejection uses the timestamp belonging to the same native eye pose instead.
    public sealed class EyeFreezeDetector
    {
        public const string Revision="eye-freeze-v2-native-stale-angular-diagnostic";
        public const double WindowSeconds=2.0;
        public const double MinimumCoverage=.9;
        public const double MaximumGapSeconds=.25;
        public const float ToleranceDegrees=.1f;
        struct Entry {public double time;public Vector3 direction;}
        readonly Queue<Entry> history=new Queue<Entry>();
        double lastTime=double.NegativeInfinity;
        public bool Frozen {get;private set;}
        public bool LowMotion {get;private set;}
        public float LastSpreadDegrees {get;private set;}

        public void Reset(){history.Clear();lastTime=double.NegativeInfinity;Frozen=LowMotion=false;LastSpreadDegrees=0;}

        // eyeInHead: gaze direction expressed in the head's local frame.
        public bool Update(double now,bool tracked,Vector3 eyeInHead,bool sampleTimeStale=false)
        {
            if(!tracked || !GazeRegions.Finite(eyeInHead) || eyeInHead.sqrMagnitude<1e-8f){Reset();return false;}
            eyeInHead.Normalize();
            if(now-lastTime>MaximumGapSeconds)history.Clear();
            lastTime=now;
            history.Enqueue(new Entry{time=now,direction=eyeInHead});
            while(history.Count>0 && history.Peek().time<now-WindowSeconds)history.Dequeue();
            float minimumDot=1;
            foreach(var entry in history)minimumDot=Mathf.Min(minimumDot,Vector3.Dot(entry.direction,eyeInHead));
            LastSpreadDegrees=Mathf.Acos(Mathf.Clamp(minimumDot,-1,1))*Mathf.Rad2Deg;
            bool covered=history.Count>=3 && now-history.Peek().time>=WindowSeconds*MinimumCoverage;
            LowMotion=covered && LastSpreadDegrees<ToleranceDegrees;
            Frozen=sampleTimeStale;
            return Frozen;
        }
    }
}

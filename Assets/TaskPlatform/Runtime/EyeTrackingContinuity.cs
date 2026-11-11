using UnityEngine;

namespace EvidenceVR
{
    // Coordinates are accepted only after a short run of coherent head/eye observations.
    // Never infer a new tracking origin from an old head position after a disconnect.
    public sealed class EyeTrackingContinuity
    {
        public const string Revision="eye-coordinate-continuity-v1";
        public const double RecoverySeconds=.1, MaximumGapSeconds=.25;
        bool hasFrame;
        ulong headId,eyeId;
        long epoch;
        long lastSampleTime;
        Matrix4x4 rig;
        double since,last;
        int samples;
        public int Generation {get;private set;}
        public bool Ready {get;private set;}
        public void Reset(){hasFrame=false;Ready=false;samples=0;Generation++;}
        public bool Observe(double now,bool tracked,ulong headDevice,ulong eyeDevice,long trackingEpoch,Matrix4x4 trackingToWorld,long sampleTime)
        {
            if(!tracked)
            {
                if(hasFrame)Reset();
                return false;
            }
            if(!hasFrame || headId!=headDevice || eyeId!=eyeDevice || epoch!=trackingEpoch || rig!=trackingToWorld || now<last || now-last>MaximumGapSeconds)
            {
                Reset();hasFrame=true;headId=headDevice;eyeId=eyeDevice;epoch=trackingEpoch;rig=trackingToWorld;since=now;lastSampleTime=0;
            }
            last=now;if(sampleTime>lastSampleTime){samples++;lastSampleTime=sampleTime;}
            Ready=samples>=3 && now-since>=RecoverySeconds;
            return Ready;
        }
    }
}

namespace EvidenceVR
{
    // A tracked pose can still be cached by Link. Compare native sample-time progress,
    // not its absolute value with Unity time (the clocks have different epochs).
    public sealed class EyeSampleFreshness
    {
        public const string Revision="eye-sample-freshness-v2-coordinate-recovery";
        public const double MaximumRepeatSeconds=.25;
        long lastStamp;
        double lastChange;
        bool advanced;
        public string Status {get;private set;}="EYE SAMPLE WAITING";
        // Keep the last stamp: an origin/device reset must not make a cached sample new.
        public void Invalidate(){advanced=false;Status="EYE SAMPLE WAITING";}
        public bool Update(double now,bool readable,long stamp)
        {
            if(!readable || stamp<=0)
            {
                advanced=false;
                Status="EYE SAMPLE UNAVAILABLE";
                return false;
            }
            if(lastStamp==0 || stamp<lastStamp)
            {
                lastStamp=stamp;lastChange=now;advanced=false;
                Status="EYE SAMPLE WAITING";
                return false;
            }
            if(stamp>lastStamp){lastStamp=stamp;lastChange=now;advanced=true;}
            if(now-lastChange>MaximumRepeatSeconds)
            {
                advanced=false;Status="EYE SAMPLE STALE";return false;
            }
            Status=advanced?"EYE SAMPLE FRESH":"EYE SAMPLE WAITING";
            return advanced;
        }
    }
}

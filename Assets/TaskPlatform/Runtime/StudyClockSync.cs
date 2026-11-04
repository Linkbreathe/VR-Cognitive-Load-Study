using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace EvidenceVR
{
    [Serializable] public sealed class StudyClockSample
    {
        public string id,clockId,revision="unity-perf-lsl-four-stamp-v1";
        public double unitySent,unityReceived,serverReceived,serverSent,offset,uncertainty;
        public double lslClock,lslSamplePerf,lslReadUncertainty;
        public bool lslAvailable;
        public bool Calculate()
        {
            double network=(unityReceived-unitySent)-(serverSent-serverReceived);
            if(double.IsNaN(network)||double.IsInfinity(network)||network<0||serverSent<serverReceived)return false;
            offset=((serverReceived-unitySent)+(serverSent-unityReceived))*.5;
            uncertainty=network*.5;return true;
        }
    }
    public sealed partial class AdaptiveRuntime
    {
        StudyClockSample abClock;
        bool clockBusy;
        double nextClock;
        int clockSequence;
        public bool StudyClockReady=>abClock!=null && Time.realtimeSinceStartupAsDouble-abClock.unityReceived<5 && abClock.uncertainty<=.05;
        IEnumerator SyncStudyClock()
        {
            clockBusy=true;
            using(var request=UnityWebRequest.Get(config.controllerUrl.TrimEnd('/')+"/clock"))
            {
                request.timeout=2;double sent=Time.realtimeSinceStartupAsDouble;
                yield return request.SendWebRequest();
                double received=Time.realtimeSinceStartupAsDouble;
                if(!disposed && request.result==UnityWebRequest.Result.Success)
                {
                    var sample=JsonUtility.FromJson<StudyClockSample>(request.downloadHandler.text);
                    sample.unitySent=sent;sample.unityReceived=received;sample.id=sessionId+":clock:"+(++clockSequence);
                    if(sample.Calculate())
                    {
                        Json("clock_sync_ab.jsonl",sample);
                        // Keep the lowest-delay recent sample; retain every sample for offline inspection.
                        if(abClock==null || received-abClock.unityReceived>3 || sample.uncertainty<abClock.uncertainty)abClock=sample;
                        offset=abClock.offset;uncertainty=StudyClockReady?abClock.uncertainty:999;
                    }
                }
            }
            clockBusy=false;
        }
    }
}

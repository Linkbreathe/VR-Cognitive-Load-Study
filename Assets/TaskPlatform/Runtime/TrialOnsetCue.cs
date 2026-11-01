using System;
using UnityEngine;

namespace EvidenceVR
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class TrialOnsetCue : MonoBehaviour
    {
        public const string CueVersion="soft-sine-660hz-120ms-v1";
        public int PlayRequests { get; private set; }
        public AudioSource Source => source;
        AudioSource source;
        AudioClip clip;
        TaskEngine engine;
        bool cueEnabled;
        public void Bind(TaskEngine next,TaskConfig config)
        {
            if(engine!=null) engine.OnEvent-=OnTaskEvent;
            source=GetComponent<AudioSource>(); source.Stop();
            source.playOnAwake=false; source.loop=false; source.spatialBlend=0;
            source.volume=config.onsetCueVolume;
            if(clip==null)
            {
                const int rate=48000, count=5760;
                var samples=new float[count];
                for(int i=0;i<count;i++)
                {
                    double time=(double)i/rate;
                    double envelope=Math.Min(1,time/.008)*Math.Min(1,(.12-time)/.016)*Math.Exp(-time*16);
                    samples[i]=(float)(Math.Sin(2*Math.PI*660*time)*envelope);
                }
                clip=AudioClip.Create(CueVersion,count,1,rate,false); clip.SetData(samples,0);
            }
            source.clip=clip; cueEnabled=config.onsetCueEnabled; PlayRequests=0;
            engine=next; engine.OnEvent+=OnTaskEvent;
        }
        void OnTaskEvent(TaskEvent item)
        {
            if(item.type=="pause" || item.type=="session_aborted") {source.Stop(); return;}
            if(item.type!="trial_countdown_start" || !cueEnabled) return;
            source.PlayOneShot(clip); PlayRequests++;
            engine.Note("audio_cue_requested",CueVersion+";gain="+source.volume.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+";software_request_not_measured_sound_onset",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        }
        void OnDestroy()
        {
            if(engine!=null)engine.OnEvent-=OnTaskEvent;
            if(clip!=null)Destroy(clip);
        }
    }
}

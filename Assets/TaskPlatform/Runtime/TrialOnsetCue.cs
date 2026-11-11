using System;
using UnityEngine;

namespace EvidenceVR
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class TrialOnsetCue : MonoBehaviour
    {
        public const string CueVersion="soft-sine-660hz-120ms-v1";
        public const string SelectionCueVersion="selection-sine-520hz-65ms-v1";
        public int SelectionPlayRequests {get;private set;}
        public int PlayRequests { get; private set; }
        public AudioSource Source => source;
        AudioSource source;
        AudioClip clip;
        AudioClip selectionClip;
        AudioSource selectionSource;
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
            if(selectionSource==null)selectionSource=gameObject.AddComponent<AudioSource>();
            selectionSource.Stop();selectionSource.playOnAwake=false;selectionSource.loop=false;selectionSource.spatialBlend=0;selectionSource.volume=.06f;
            if(selectionClip==null)
            {
                const int rate=48000,count=3120;var samples=new float[count];
                for(int i=0;i<count;i++)
                {double t=(double)i/rate;double envelope=Math.Pow(Math.Sin(Math.PI*i/(count-1)),2);samples[i]=(float)(Math.Sin(2*Math.PI*520*t)*envelope);}
                selectionClip=AudioClip.Create(SelectionCueVersion,count,1,rate,false);selectionClip.SetData(samples,0);
            }
            SelectionPlayRequests=0;
            engine=next; engine.OnEvent+=OnTaskEvent;
        }
        void OnTaskEvent(TaskEvent item)
        {
            if(item.type=="pause" || item.type=="session_aborted" || item.type=="session_complete") {source.Stop();selectionSource.Stop(); return;}
            if(item.type=="response_selection" && engine.AllowsResponseRevision)
            {
                // Restart a separate quiet voice so rapid revisions cannot stack into a loud chord.
                selectionSource.Stop();selectionSource.PlayOneShot(selectionClip);SelectionPlayRequests++;
                engine.Note("selection_cue_requested",SelectionCueVersion+";gain=0.06;software_request_not_measured_sound_onset",Time.realtimeSinceStartupAsDouble,Time.frameCount);
                return;
            }
            if(item.type!="trial_countdown_start" || !cueEnabled) return;
            source.PlayOneShot(clip); PlayRequests++;
            engine.Note("audio_cue_requested",CueVersion+";gain="+source.volume.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+";software_request_not_measured_sound_onset",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        }
        void OnDestroy()
        {
            if(engine!=null)engine.OnEvent-=OnTaskEvent;
            if(clip!=null)Destroy(clip);
            if(selectionClip!=null)Destroy(selectionClip);
            if(selectionSource!=null)Destroy(selectionSource);
        }
    }
}

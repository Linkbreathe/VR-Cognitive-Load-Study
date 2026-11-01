using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;

public static class ResponseWindowProbe
{
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Run in Play Mode.");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(c.Engine.Phase!=TaskPhase.Preparation)throw new Exception("Use a fresh preparation screen for this test.");
        var e=c.Engine; var starts=new List<double>(); var digits=new HashSet<string>();
        int cueEvents=0, activeAudio=0; bool lateAccepted=false;
        Action<TaskEvent> observe=item=>
        {
            if(item.type=="stimulus_on")starts.Add(item.actual);
            if(item.type=="audio_cue_requested") { cueEvents++; if(c.onsetCue.Source.isPlaying)activeAudio++; }
        };
        e.OnEvent+=observe;
        double began=Time.realtimeSinceStartupAsDouble;
        try
        {
            e.Note("runtime_probe","Programmatic responses; not physical controller or acoustic-onset evidence",began,Time.frameCount);
            e.Advance(began,Time.frameCount); e.Advance(began,Time.frameCount);
            while(Time.realtimeSinceStartupAsDouble-began<14)
            {
                await Task.Delay(35);
                double now=Time.realtimeSinceStartupAsDouble;
                if(e.Phase!=TaskPhase.Running)throw new Exception("Live probe left Running: "+e.Phase);
                digits.Add(c.spatialView.timer.text);
                if(e.TrialIndex==2 && !lateAccepted && now-e.CurrentResult.actualOnset>=3.2)
                {
                    lateAccepted=e.Respond(e.Current.expectedMatch,now,Time.frameCount);
                    if(!lateAccepted)throw new Exception("Response at approximately 3.2 seconds rejected");
                }
            }
            if(starts.Count!=4 || cueEvents!=4 || activeAudio!=4 || c.onsetCue.PlayRequests!=4)throw new Exception("Expected exactly one playing cue at each of four onsets");
            if(!lateAccepted || !e.Results.Any(r=>r.scored && r.responded && r.reactionTime>=3.2 && r.reactionTime<4))throw new Exception("Extended response was not saved");
            foreach(string digit in new[]{"4","3","2","1","NEXT"})if(!digits.Contains(digit))throw new Exception("Missing countdown state: "+digit);
            var intervals=starts.Skip(1).Select((t,i)=>t-starts[i]).ToArray();
            if(intervals.Any(t=>Math.Abs(t-4.5)>.15))throw new Exception("Live onset interval outside software probe tolerance");
            if(c.spatialView.instructions.enabled || !c.spatialView.taskCanvas.enabled)throw new Exception("Running layout did not separate the stimulus");
            if(c.onsetCue.Source.clip.length<.119f || c.onsetCue.Source.clip.length>.121f || Math.Abs(c.onsetCue.Source.volume-.12f)>.001f)throw new Exception("Unexpected cue duration/gain");
            string report="PASS: 14-second live editor probe; four stimuli, four playing onset cues; countdown 4/3/2/1/NEXT observed; a response after 3.2 seconds accepted and saved.\nOnset intervals (s): "+string.Join(", ",intervals.Select(x=>x.ToString("F4")))+"\nData: "+c.DataPath+"\nProgrammatic response, software timestamps and AudioSource state only. Headset audibility and physical input are not verified.\n";
            File.WriteAllText("Evidence/response-window-live-v1.3.txt",report); Debug.Log(report); return report;
        }
        finally { e.OnEvent-=observe; c.BeginSession(); }
    }
}

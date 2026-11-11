using System;
using System.IO;
using EvidenceVR;
using UnityEngine;

public static class UnifiedTaskGazeValidation
{
    const string Folder="Evidence/UnifiedTaskGaze-20261110";
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    public static string Run()
    {
        checks=0;
        // Read the actual task geometry without starting a recording or supplying synthetic eyes.
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var sphere=owner.stimulus.bounds;
        var tray=owner.spatialView.bench.Find("StimulusPlinth").GetComponent<Renderer>().bounds;
        var task=GazeRegions.TaskBounds(sphere,tray);
        var origin=new Vector3(task.center.x,1.2f,task.min.z-1.5f);
        Func<Vector3,bool> hit=point=>GazeRegions.Classify(true,origin,point-origin,task,true,null,false).task;
        Check(hit(sphere.center),"Sphere counts");
        Check(hit(tray.center),"Tray counts");
        Check(hit((sphere.center+tray.center)*.5f),"Gap counts");
        Check(hit(new Vector3(tray.max.x+.03f,tray.center.y,tray.min.z)),"Three-centimeter tray margin counts");
        var enclosing=sphere;enclosing.Encapsulate(tray);
        Check(Vector3.Distance(task.min,enclosing.min-Vector3.one*.04f)<.00001f,"Four-centimeter minimum padding");
        Check(Vector3.Distance(task.max,enclosing.max+Vector3.one*.04f)<.00001f,"Four-centimeter maximum padding");
        Check(!hit(new Vector3(task.max.x+1,task.center.y,task.center.z)),"Unrelated surroundings excluded");
        Check(!GazeRegions.Classify(true,origin,tray.center-origin,task,false,null,false).task,"Inactive task excluded");
        Check(!GazeRegions.Classify(false,origin,tray.center-origin,task,true,null,false).valid,"Tracking loss remains invalid");
        foreach(bool comparison in new[]{false,true})
        {
            string path=Path.Combine(Folder,"synthetic-metadata",Guid.NewGuid().ToString("N"));
            using(var recorder=new SessionRecorder(path,new TaskConfig(),participant:"SYNTHETIC_TASK_REGION",kind:"synthetic",
                presentationVersion:"synthetic-area-validation",comparison:comparison))
            {
                var json=File.ReadAllText(Path.Combine(recorder.DirectoryPath,"session.json"));
                Check(json.Contains(GazeRegions.Revision),"Practice/comparison metadata shares current area revision");
                Check(!json.Contains("sphere-bounds-v1"),"Comparison metadata cannot claim legacy sphere-only ROI");
                recorder.Complete("synthetic_validation_only");
            }
        }
        string report="PASS: "+checks+" isolated task-region checks; revision="+GazeRegions.Revision+"; playMode="+Application.isPlaying+"; geometry and metadata only, no real-eye acceptance.";
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,Application.isPlaying?"playmode-validation.txt":"editmode-validation.txt"),report);
        Debug.Log(report);return report;
    }
}

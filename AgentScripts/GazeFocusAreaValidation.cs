using System;
using System.IO;
using EvidenceVR;
using UnityEngine;

public static class GazeFocusAreaValidation
{
    public static object Scene()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(owner==null || owner.AdaptiveRuntime==null)throw new Exception("Start Stage 2 before checking scene geometry.");
        var tray=owner.spatialView.bench.Find("StimulusPlinth").GetComponent<Renderer>();
        var bounds=GazeRegions.TaskBounds(owner.stimulus.bounds,tray.bounds);
        Vector3 origin=owner.head.position;
        int checks=0;
        Action<Vector3,bool,string> check=(target,active,label)=>{
            checks++;
            var result=GazeRegions.Classify(true,origin,target-origin,bounds,active,null,false);
            if(!result.valid || result.task!=active)throw new Exception("Actual geometry check failed: "+label);
        };
        check(owner.stimulus.bounds.center,true,"sphere");
        check(tray.bounds.center,true,"tray");
        check((owner.stimulus.bounds.center+tray.bounds.center)*.5f,true,"gap");
        check(tray.bounds.center+Vector3.right*tray.bounds.extents.x,true,"tray right edge");
        check(tray.bounds.center-Vector3.right*tray.bounds.extents.x,true,"tray left edge");
        check(tray.bounds.center,false,"inactive area");
        string header;using(var stream=new FileStream(Path.Combine(owner.DataPath,"gaze_samples.csv"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(stream))header=reader.ReadLine()??"";
        if(!header.Contains(",task_region_visible,"))throw new Exception("Area visibility column missing.");
        if(!header.EndsWith(",eye_frozen,eye_freeze_spread_deg,eye_freeze_revision"))throw new Exception("Eye freeze columns missing.");
        var session=File.ReadAllText(Path.Combine(owner.DataPath,"session.json"));
        if(!session.Contains(GazeRegions.Revision))throw new Exception("Session ROI revision missing.");
        Debug.Log("Focus area validation: PASS;actual_scene_checks="+checks+";revision="+GazeRegions.Revision+";synthetic_directions_only;no_production_pose_injection");
        return new {passed=true,checks,revision=GazeRegions.Revision,worldCenter=bounds.center.ToString("F4"),worldSize=bounds.size.ToString("F4"),sphere=owner.stimulus.bounds.center.ToString("F4"),tray=tray.bounds.center.ToString("F4"),data=owner.DataPath};
    }
}

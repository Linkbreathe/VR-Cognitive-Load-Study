using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.XR;

public static class SeatedCalibrationProbe
{
    public static async Task<string> Run()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(!Application.isPlaying || c.Engine.Phase!=TaskPhase.Preparation)throw new Exception("Fresh Play Mode preparation required.");
        var events=new List<TaskEvent>();Action<TaskEvent> record=item=>events.Add(item);c.Engine.OnEvent+=record;
        var inputs=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(inputs);
        var input=inputs.FirstOrDefault(x=>x.running);
        var driver=c.head.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        bool driverEnabled=driver!=null && driver.enabled;
        var local=c.head.localPosition;var rig=c.head.parent.position;
        try
        {
            if(input==null)
            {
                if(driver!=null)driver.enabled=false;
                c.head.localPosition=new Vector3(0,1.02f,0);
                c.head.parent.position=new Vector3(0,1.2f,0);
            }
            c.Recenter();await Task.Delay(400);
            if(!c.Calibrated || !events.Any(x=>x.type=="tracking_calibration"))throw new Exception("Calibration coroutine did not complete.");
            bool floor=input!=null && input.GetTrackingOriginMode()==TrackingOriginModeFlags.Floor;
            float expected=floor ? c.head.localPosition.y : 1.2f;
            if(Math.Abs(c.head.position.y-expected)>.01f)throw new Exception("Tracking height was added twice.");
            if(Math.Abs(c.spatialView.bench.Find("WorkSurface").GetComponent<Renderer>().bounds.max.y-.65f)>.001f)throw new Exception("Tabletop moved with calibration.");
            float first=c.head.position.y;c.Recenter();await Task.Delay(400);
            if(input==null && Math.Abs(c.head.position.y-first)>.001f)throw new Exception("Repeated calibration accumulated height.");
            string report="PASS: actual calibration coroutine, origin="+(floor ? "floor" : "device_fallback")+"; head_y="+c.head.position.y+"; rig_y="+c.head.parent.position.y+"; tabletop=0.65 m; repeated calibration completed. "+(input==null ? "Simulated raw eye height 1.02 m plus old rig 1.20 m; corrected to 1.20 m. No headset connected." : "Live XR tracking present; seated comfort still requires wearer review.");
            File.WriteAllText("Evidence/seated-calibration-v1.6.txt",report);Debug.Log(report);return report;
        }
        finally
        {
            c.Engine.OnEvent-=record;
            if(input==null){c.head.localPosition=local;c.head.parent.position=rig;if(driver!=null)driver.enabled=driverEnabled;c.Recenter(false);}
        }
    }
}

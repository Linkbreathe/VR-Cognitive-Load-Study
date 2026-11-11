using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.XR;

public static class GazeLiveDiagnostics
{
    [Serializable] public class Point
    {
        public double time;public bool valid,hit,focused,rayVisible;
        public Vector3 head,headForward,eye,direction,target;
        public float eyeHeadDistance,angleToTarget;
    }
    [Serializable] public class Report
    {
        public string kind="engineering-real-eye-diagnostic",runtime;
        public bool synthetic=false;
        public List<Point> samples=new List<Point>();
    }
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required.");
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        // Observe the normal application only. Never disable its controller, enable a parallel
        // head-input lifecycle, reposition the scene, or render an untracked diagnostic camera.
        if(owner==null || !owner.enabled || owner.Engine==null || owner.AdaptiveRuntime==null)
            throw new Exception("Start a normal engineering session before this read-only probe.");
        if(!EngineeringGazeRay.IsAllowed(owner.Engine.Adaptive.Config))
            throw new Exception("This probe is restricted to engineering sessions.");
        foreach(string name in new[]{"HeadPosition","HeadRotation","HeadTracking"})
            if(!owner.inputActions.FindAction(name,true).enabled)
                throw new Exception("Head tracking actions are disabled; do not run a gaze accuracy probe.");
        var hmd=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if(!hmd.TryGetFeatureValue(CommonUsages.isTracked,out bool headTracked)||!headTracked)
            throw new Exception("No tracked HMD pose; no gaze accuracy result is available.");
        var report=new Report{runtime=UnityEngine.XR.OpenXR.OpenXRRuntime.name};
        var devices=new List<InputDevice>();
        var tray=owner.spatialView.bench.Find("StimulusPlinth").GetComponent<Renderer>();

        try
        {
            for(int i=0;i<200 && Application.isPlaying;i++)
            {
                devices.Clear();InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking,devices);
                var p=new Point{time=Time.realtimeSinceStartupAsDouble,head=owner.head.position,headForward=owner.head.forward,target=owner.stimulus.bounds.center,focused=Application.isFocused};
                foreach(var d in devices)
                {
                    if(d.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked)&&tracked &&
                       d.TryGetFeatureValue(new InputFeatureUsage<Vector3>("gazePosition"),out var pos)&&
                       d.TryGetFeatureValue(new InputFeatureUsage<Quaternion>("gazeRotation"),out var rot)&&GazeRegions.ValidRotation(rot))
                    {
                        var sample=GazeRegions.Classify(true,owner.head.parent.TransformPoint(pos),owner.head.parent.TransformDirection(rot*Vector3.forward),GazeRegions.TaskBounds(owner.stimulus.bounds,tray.bounds),true,null,false);
                        p.valid=sample.valid;p.hit=sample.task;p.eye=sample.origin;p.direction=sample.direction;
                        p.eyeHeadDistance=Vector3.Distance(p.eye,p.head);p.angleToTarget=Vector3.Angle(p.direction,p.target-p.eye);
                        break;
                    }
                }
                p.rayVisible=owner.AdaptiveRuntime.GazeRay!=null && owner.AdaptiveRuntime.GazeRay.Visible;report.samples.Add(p);
                await Task.Delay(50);
            }
            Directory.CreateDirectory("Evidence/GazeDiagnosis-20261110");
            string header=JsonUtility.ToJson(report);
            File.WriteAllText("Evidence/GazeDiagnosis-20261110/live-probe.json",header.Substring(0,header.Length-1)+",\"points\":["+string.Join(",",report.samples.ConvertAll(p=>JsonUtility.ToJson(p)))+"]}");
            return "Real eye probe complete; samples="+report.samples.Count+"; valid="+report.samples.FindAll(p=>p.valid).Count+"; taskHits="+report.samples.FindAll(p=>p.hit).Count+"; rayVisible="+report.samples.FindAll(p=>p.rayVisible).Count;
        }
        finally{Debug.Log("Read-only eye diagnostic ended; production scene and inputs were not modified.");}
    }
}

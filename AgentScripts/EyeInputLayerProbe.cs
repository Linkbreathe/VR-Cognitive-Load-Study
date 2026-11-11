using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Interactions;

// Read-only engineering probe. No session assignment, rig changes or synthetic input.
public static class EyeInputLayerProbe
{
    [Serializable] public class Sample
    {
        public double time; public int frame;
        public bool legacyTracked,inputTracked,headTracked,legacyRotationAvailable,inputAvailable;
        public Quaternion legacyEye,inputEye,rawHead,sceneHead;
        public Vector3 legacyInHead,inputInHead;
        public string status;
    }
    [Serializable] public class Report
    {
        public string revision="eye-input-layer-probe-v1",utc,runtime;
        public bool synthetic=false;
        public List<Sample> samples=new List<Sample>();
    }
    public static async Task<string> Run()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(!Application.isPlaying || owner==null || !owner.enabled || !XRSettings.isDeviceActive || !Camera.main.stereoEnabled)
            throw new InvalidOperationException("An active normal stereo VR scene is required.");
        var cfg=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(Path.Combine(Application.persistentDataPath,"adaptive-training-config.json")));
        if(!EngineeringGazeRay.IsAllowed(cfg))throw new InvalidOperationException("Engineering configuration required.");
        foreach(string action in new[]{"HeadPosition","HeadRotation","HeadTracking"})
            if(!owner.inputActions.FindAction(action,true).enabled)throw new InvalidOperationException("Head input must remain enabled.");
        var report=new Report{utc=DateTime.UtcNow.ToString("o"),runtime=UnityEngine.XR.OpenXR.OpenXRRuntime.name};
        var devices=new List<UnityEngine.XR.InputDevice>();
        Debug.Log("Eye input layer probe started: read-only, normal stereo VR and head inputs retained.");
        for(int i=0;i<300 && Application.isPlaying;i++)
        {
            var row=new Sample{time=Time.realtimeSinceStartupAsDouble,frame=Time.frameCount,sceneHead=owner.head.localRotation,status=owner.AdaptiveRuntime?.GazeTrackingStatus};
            var hmd=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            hmd.TryGetFeatureValue(CommonUsages.isTracked,out row.headTracked);
            hmd.TryGetFeatureValue(CommonUsages.deviceRotation,out row.rawHead);
            devices.Clear();InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking,devices);
            foreach(var d in devices)
            {
                d.TryGetFeatureValue(CommonUsages.isTracked,out row.legacyTracked);
                row.legacyRotationAvailable=d.TryGetFeatureValue(EyeTrackingUsages.gazeRotation,out row.legacyEye);
                if(row.legacyRotationAvailable)break;
            }
            foreach(var d in UnityEngine.InputSystem.InputSystem.devices)
                if(d is EyeGazeInteraction.EyeGazeDevice eye)
                {var pose=eye.pose.ReadValue();row.inputAvailable=true;row.inputTracked=pose.isTracked;row.inputEye=pose.rotation;break;}
            if(row.legacyRotationAvailable)row.legacyInHead=Quaternion.Inverse(row.rawHead)*(row.legacyEye*Vector3.forward);
            if(row.inputAvailable)row.inputInHead=Quaternion.Inverse(row.rawHead)*(row.inputEye*Vector3.forward);
            report.samples.Add(row);
            await Task.Delay(50);
        }
        const string dir="Evidence/EyeFreezeRecurrence-20261110";
        Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"input-layers-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+".json");
        string metadata=JsonUtility.ToJson(report);
        File.WriteAllText(path,metadata.Substring(0,metadata.Length-1)+",\"points\":["+string.Join(",",report.samples.ConvertAll(s=>JsonUtility.ToJson(s)))+"]}");
        Debug.Log("Eye input layer probe finished: "+path+";samples="+report.samples.Count);
        return path;
    }
}

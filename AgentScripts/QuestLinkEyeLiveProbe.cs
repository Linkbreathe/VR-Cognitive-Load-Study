using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

public static class QuestLinkEyeLiveProbe
{
    [Serializable] public sealed class Sample
    {
        public double time;
        public string device;
        public bool tracked,positionAvailable,rotationAvailable,valid;
        public Vector3 position;
        public Quaternion rotation;
    }
    [Serializable] public sealed class Report
    {
        public string revision="quest-link-eye-settings-recovery-v1",kind="engineering-live-eye-probe",runtime,compatibility,createdUtc;
        public int samples,validSamples;
        public bool synthetic=false;
        public List<Sample> series=new List<Sample>();
    }
    public static async Task<string> Run()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Start Play Mode first.");
        var report=new Report{runtime=OpenXRRuntime.name,createdUtc=DateTime.UtcNow.ToString("o")};
        var devices=new List<InputDevice>();
        for(int i=0;i<160 && EditorApplication.isPlaying;i++)
        {
            devices.Clear();InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking,devices);
            foreach(var device in devices)
            {
                var row=new Sample{time=Time.realtimeSinceStartupAsDouble,device=device.name};
                device.TryGetFeatureValue(CommonUsages.isTracked,out row.tracked);
                row.positionAvailable=device.TryGetFeatureValue(new InputFeatureUsage<Vector3>("gazePosition"),out row.position);
                row.rotationAvailable=device.TryGetFeatureValue(new InputFeatureUsage<Quaternion>("gazeRotation"),out row.rotation);
                row.valid=row.tracked && row.positionAvailable && row.rotationAvailable && GazeRegions.ValidRotation(row.rotation);
                report.samples++;if(row.valid)report.validSamples++;
                report.series.Add(row);
            }
            await Task.Delay(50);
        }
        report.compatibility=QuestLinkEyeSpaceFeature.LastResult;
        const string path="Evidence/QuestLinkEyeRepair-20261110/live-eye-probe.json";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path,JsonUtility.ToJson(report,true));
        string summary="Live eye probe: valid="+report.validSamples+"/"+report.samples+";runtime="+report.runtime+";compatibility="+report.compatibility+";evidence="+path;
        Debug.Log(summary);
        return summary;
    }
}

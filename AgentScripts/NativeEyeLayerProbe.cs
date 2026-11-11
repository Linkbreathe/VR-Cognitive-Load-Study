using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.XR;

// Uses the existing eye action space read-only. Owns only a temporary VIEW reference space.
public static class NativeEyeLayerProbe
{
    [StructLayout(LayoutKind.Sequential)] struct ReferenceInfo
    {public int type;public IntPtr next;public int referenceType;public Quaternion orientation;public Vector3 position;}
    [StructLayout(LayoutKind.Sequential)] struct Location
    {public int type;public IntPtr next;public ulong flags;public Quaternion orientation;public Vector3 position;}
    [StructLayout(LayoutKind.Sequential)] struct SampleTime
    {public int type;public IntPtr next;public long time;}
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int CreateReference(ulong session,ref ReferenceInfo info,out ulong space);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int Locate(ulong space,ulong baseSpace,long time,ref Location location);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int DestroySpace(ulong space);
    [DllImport("UnityOpenXR",EntryPoint="OpenXRInputProvider_GetXRSession")]
    [return:MarshalAs(UnmanagedType.U1)] static extern bool Session(out ulong session);
    [DllImport("UnityOpenXR",EntryPoint="OpenXRInputProvider_GetPredictedDisplayTime")]
    [return:MarshalAs(UnmanagedType.U1)] static extern bool DisplayTime(out long time);
    [Serializable] public class Point
    {
        public double time;public int frame,result;public long requestedTime,eyeSampleTime;public ulong flags;
        public Quaternion nativeEyeInView,legacyEye,rawHead;
        public Vector3 nativeDirectionUnity,legacyDirectionInHead;
        public bool tracked,rotationAvailable;
    }
    static T Procedure<T>(string name) where T:class
    {
        var type=typeof(QuestLinkEyeSpaceFeature);var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var get=(Delegate)type.GetField("originalGetProc",flags).GetValue(null);
        var instance=(ulong)type.GetField("diagnosticInstance",flags).GetValue(null);
        if(instance==0 || get==null)throw new Exception("No live engineering OpenXR instance.");
        object[] args={instance,name,IntPtr.Zero};int result=(int)get.DynamicInvoke(args);
        if(result!=0 || (IntPtr)args[2]==IntPtr.Zero)throw new Exception(name+" unavailable: "+result);
        return Marshal.GetDelegateForFunctionPointer((IntPtr)args[2],typeof(T)) as T;
    }
    public static async Task<string> Run()
    {
        if(!Application.isPlaying || !XRSettings.isDeviceActive || !Camera.main.stereoEnabled)throw new Exception("Normal stereo VR required.");
        var cfg=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(Path.Combine(Application.persistentDataPath,"adaptive-training-config.json")));
        if(!EngineeringGazeRay.IsAllowed(cfg))throw new Exception("Engineering only.");
        ulong eye=(ulong)typeof(QuestLinkEyeSpaceFeature).GetField("diagnosticEyeSpace",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        if(eye==0 || !Session(out ulong session))throw new Exception("No live eye action space/session.");
        var create=Procedure<CreateReference>("xrCreateReferenceSpace");var locate=Procedure<Locate>("xrLocateSpace");var destroy=Procedure<DestroySpace>("xrDestroySpace");
        var info=new ReferenceInfo{type=37,referenceType=1,orientation=Quaternion.identity};
        int created=create(session,ref info,out ulong view);if(created!=0)throw new Exception("VIEW creation failed: "+created);
        IntPtr sampleTime=Marshal.AllocHGlobal(Marshal.SizeOf<SampleTime>());
        var points=new List<Point>();var devices=new List<InputDevice>();
        string utc=DateTime.UtcNow.ToString("o");
        Debug.Log("Native eye layer probe started: direct xrLocateSpace, existing eye action, temporary VIEW space; no input changes.");
        try
        {
            for(int i=0;i<300 && Application.isPlaying;i++)
            {
                var p=new Point{time=Time.realtimeSinceStartupAsDouble,frame=Time.frameCount};
                if(!DisplayTime(out p.requestedTime))throw new Exception("No predicted display time.");
                Marshal.StructureToPtr(new SampleTime{type=1000030001},sampleTime,false);
                var location=new Location{type=42,next=sampleTime};
                p.result=locate(eye,view,p.requestedTime,ref location);p.flags=location.flags;
                p.nativeEyeInView=location.orientation;p.eyeSampleTime=Marshal.PtrToStructure<SampleTime>(sampleTime).time;
                // OpenXR right-handed pose to Unity left-handed rotation.
                var q=location.orientation;
                p.nativeDirectionUnity=new Quaternion(-q.x,-q.y,q.z,q.w)*Vector3.forward;
                InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.deviceRotation,out p.rawHead);
                devices.Clear();InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking,devices);
                foreach(var d in devices)
                {d.TryGetFeatureValue(CommonUsages.isTracked,out p.tracked);p.rotationAvailable=d.TryGetFeatureValue(new InputFeatureUsage<Quaternion>("gazeRotation"),out p.legacyEye);if(p.rotationAvailable)break;}
                p.legacyDirectionInHead=Quaternion.Inverse(p.rawHead)*(p.legacyEye*Vector3.forward);
                points.Add(p);await Task.Delay(50);
            }
        }
        finally {destroy(view);Marshal.FreeHGlobal(sampleTime);}
        const string dir="Evidence/EyeFreezeRecurrence-20261110";Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"native-layers-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+".json");
        File.WriteAllText(path,"{\"revision\":\"native-eye-layer-probe-v1\",\"synthetic\":false,\"utc\":\""+utc+"\",\"points\":["+string.Join(",",points.ConvertAll(p=>JsonUtility.ToJson(p)))+"]}");
        Debug.Log("Native eye layer probe finished: "+path+";samples="+points.Count);
        return path;
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace EvidenceVR
{
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(UiName="Eye Pose Freshness and Quest Link Compatibility",
        BuildTargetGroups=new[]{UnityEditor.BuildTargetGroup.Standalone,UnityEditor.BuildTargetGroup.Android},Company="EvidenceVR",
        Desc="Read eye pose and sample time together; track reference-space/session changes. Narrow Oculus eye-space retry only.",
        Version="2.0.0",FeatureId="com.evidencevr.quest-link-eye-space",OpenxrExtensionStrings="XR_EXT_eye_gaze_interaction")]
#endif
    public sealed class QuestLinkEyeSpaceFeature : OpenXRFeature
    {
        public bool retryWithNullPath;
        public const string Revision="eye-space-v2-atomic-pose-all-modes";
        public static string LastResult {get;private set;}="inactive";
        static bool retry,oculus,active;
        static ulong eyePath;
        // Observed handles are owned by Unity; this feature never creates a competing action.
        static ulong diagnosticInstance,diagnosticEyeSpace;
        static LocateSpace locateSpace;
        public static bool SampleTimeCheckRequired=>true;
        public static long TrackingEpoch {get;private set;}
        static int sessionState;
        [StructLayout(LayoutKind.Sequential)] struct SpaceLocation
        {public int type;public IntPtr next;public ulong flags;public Quaternion orientation;public Vector3 position;}
        [StructLayout(LayoutKind.Sequential)] struct EyeSampleTime
        {public int type;public IntPtr next;public long time;}
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int LocateSpace(ulong space,ulong baseSpace,long time,ref SpaceLocation location);
        [DllImport("UnityOpenXR",EntryPoint="OpenXRInputProvider_GetAppSpaceAndPredictedDisplayTime")]
        [return:MarshalAs(UnmanagedType.U1)] static extern bool AppSpaceAndDisplayTime(out ulong space,out long time);
        // Read metadata from the existing eye action; never replace it with a head pose,
        // create a competing input action, or modify Unity's tracking origin.
        public static bool TryReadSampleTime(out long sampleTime,out ulong locationFlags)
            =>TryReadSample(out _,out _,out sampleTime,out locationFlags);
        public static Vector3 ToUnityPosition(Vector3 value)=>new Vector3(value.x,value.y,-value.z);
        public static Quaternion ToUnityRotation(Quaternion value)=>new Quaternion(-value.x,-value.y,value.z,value.w);
        public static bool TryReadSample(out Vector3 position,out Quaternion rotation,out long sampleTime,out ulong locationFlags)
        {
            position=Vector3.zero;rotation=Quaternion.identity;sampleTime=0;locationFlags=0;
            if(!active || sessionState!=5 || diagnosticEyeSpace==0 || locateSpace==null)return false;
            if(!AppSpaceAndDisplayTime(out ulong appSpace,out long time) || appSpace==0 || time<=0)return false;
            IntPtr pointer=Marshal.AllocHGlobal(Marshal.SizeOf<EyeSampleTime>());
            try
            {
                Marshal.StructureToPtr(new EyeSampleTime{type=1000030001},pointer,false);
                var location=new SpaceLocation{type=42,next=pointer};
                int result=locateSpace(diagnosticEyeSpace,appSpace,time,ref location);
                locationFlags=location.flags;
                sampleTime=Marshal.PtrToStructure<EyeSampleTime>(pointer).time;
                position=ToUnityPosition(location.position);rotation=ToUnityRotation(location.orientation);
                return result==0 && (locationFlags&15)==15 && GazeRegions.Finite(position) && GazeRegions.ValidRotation(rotation);
            }
            finally{Marshal.FreeHGlobal(pointer);}
        }
        static readonly Dictionary<ulong,uint> actionPathCounts=new Dictionary<ulong,uint>();
        static readonly Dictionary<ulong,ulong> soleActionPaths=new Dictionary<ulong,ulong>();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int GetProc(ulong instance,[MarshalAs(UnmanagedType.LPStr)]string name,out IntPtr function);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int CreateAction(ulong actionSet,IntPtr info,out ulong action);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int CreateSpace(ulong session,ref SpaceInfo info,out ulong space);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int StringToPath(ulong instance,[MarshalAs(UnmanagedType.LPStr)]string text,out ulong path);
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Ansi)] struct ActionInfo
        {
            public int type;public IntPtr next;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string name;
            public int actionType;public uint pathCount;public IntPtr paths;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string localizedName;
        }
        [StructLayout(LayoutKind.Sequential)] struct SpaceInfo
        {
            public int type;public IntPtr next;public ulong action,subactionPath;
            public Quaternion orientation;public Vector3 position;
        }
        static GetProc originalGetProc;
        static CreateAction originalCreateAction;
        static CreateSpace originalCreateSpace;
        // Root native callback delegates for the lifetime of the Editor process.
        static readonly GetProc getProcHook=GetProcedure;
        static readonly CreateAction createActionHook=CreateActionTraced;
        static readonly CreateSpace createSpaceHook=CreateSpaceTraced;
        protected override IntPtr HookGetInstanceProcAddr(IntPtr function)
        {
            active=false;LastResult="inactive";diagnosticInstance=diagnosticEyeSpace=0;locateSpace=null;sessionState=0;TrackingEpoch++;
            active=true;retry=retryWithNullPath;oculus=false;eyePath=0;actionPathCounts.Clear();soleActionPaths.Clear();LastResult="hooked";
            originalGetProc=Marshal.GetDelegateForFunctionPointer<GetProc>(function);
            return Marshal.GetFunctionPointerForDelegate(getProcHook);
        }
        protected override bool OnInstanceCreate(ulong instance)
        {
            if(!active)return true;
            diagnosticInstance=instance;
            oculus=OpenXRRuntime.name=="Oculus";
            if(OpenXRRuntime.IsExtensionEnabled("XR_EXT_eye_gaze_interaction") &&
                originalGetProc(instance,"xrLocateSpace",out var locatePointer)==0 && locatePointer!=IntPtr.Zero)
                locateSpace=Marshal.GetDelegateForFunctionPointer<LocateSpace>(locatePointer);
            if(originalGetProc!=null && originalGetProc(instance,"xrStringToPath",out var ptr)==0)
                Marshal.GetDelegateForFunctionPointer<StringToPath>(ptr)(instance,"/user/eyes_ext",out eyePath);
            Debug.Log("Eye space probe: runtime="+OpenXRRuntime.name+";eye_path="+eyePath+";retry="+retry);
            return true;
        }
        [AOT.MonoPInvokeCallback(typeof(GetProc))]
        static int GetProcedure(ulong instance,string name,out IntPtr function)
        {
            int result=originalGetProc(instance,name,out function);
            if(result!=0 || function==IntPtr.Zero)return result;
            if(name=="xrCreateAction")
            {originalCreateAction=Marshal.GetDelegateForFunctionPointer<CreateAction>(function);function=Marshal.GetFunctionPointerForDelegate(createActionHook);}
            if(name=="xrCreateActionSpace")
            {originalCreateSpace=Marshal.GetDelegateForFunctionPointer<CreateSpace>(function);function=Marshal.GetFunctionPointerForDelegate(createSpaceHook);}
            return result;
        }
        [AOT.MonoPInvokeCallback(typeof(CreateAction))]
        static int CreateActionTraced(ulong actionSet,IntPtr pointer,out ulong action)
        {
            int result=originalCreateAction(actionSet,pointer,out action);
            if(result==0)
            {
                var info=Marshal.PtrToStructure<ActionInfo>(pointer);
                actionPathCounts[action]=info.pathCount;
                if(info.pathCount==1 && info.paths!=IntPtr.Zero)
                    soleActionPaths[action]=unchecked((ulong)Marshal.ReadInt64(info.paths));
                if(info.actionType==4 && soleActionPaths.TryGetValue(action,out var source) && source==eyePath)
                    Debug.Log("Eye space probe action: name="+info.name+";handle="+action+";subaction_count="+info.pathCount+";sole_path="+(soleActionPaths.TryGetValue(action,out var sole)?sole:0));
            }
            return result;
        }
        [AOT.MonoPInvokeCallback(typeof(CreateSpace))]
        static int CreateSpaceTraced(ulong session,ref SpaceInfo info,out ulong space)
        {
            int result=originalCreateSpace(session,ref info,out space);
            bool known=actionPathCounts.TryGetValue(info.action,out uint count);
            soleActionPaths.TryGetValue(info.action,out var sole);
            if(eyePath==0 || !known || count!=1 || sole!=eyePath || (info.subactionPath!=eyePath && info.subactionPath!=0))return result;
            LastResult="eye_space_result="+result+";known_action="+known+";declared_subactions="+count;
            // Test only the aggregate of an eye-only action; never hand or multi-source actions.
            if(ShouldRetry(retry,oculus,result,known,count,sole,eyePath,info.subactionPath))
            {
                var aggregate=info;aggregate.subactionPath=0;
                result=originalCreateSpace(session,ref aggregate,out space);
                LastResult+=";null_path_retry="+result;
            }
            Debug.Log("Eye space probe: "+LastResult);
            if(result==0){diagnosticEyeSpace=space;TrackingEpoch++;}
            return result;
        }
        protected override void OnInstanceDestroy(ulong instance)
        {diagnosticInstance=diagnosticEyeSpace=0;locateSpace=null;active=false;oculus=false;sessionState=0;TrackingEpoch++;}
        protected override void OnSessionDestroy(ulong session)
        {diagnosticEyeSpace=0;sessionState=0;TrackingEpoch++;}
        protected override void OnSessionCreate(ulong session){sessionState=0;TrackingEpoch++;}
        protected override void OnSessionStateChange(int previous,int current)
        {sessionState=current;if(previous!=current)TrackingEpoch++;}
        protected override void OnAppSpaceChange(ulong space){TrackingEpoch++;}
        internal static bool ShouldRetry(bool enabled,bool isOculus,int result,bool known,uint count,ulong sole,ulong eye,ulong requested)
            =>enabled && isOculus && result==-22 && known && count==1 && eye!=0 && sole==eye && requested==eye;
    }
}

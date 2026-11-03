using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace EvidenceVR
{
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(UiName="Quest Link Engineering Eye Compatibility",
        BuildTargetGroups=new[]{UnityEditor.BuildTargetGroup.Standalone},Company="EvidenceVR",
        Desc="Editor engineering only: retry an Oculus eye-only action space with the standard aggregate path after XR_ERROR_PATH_UNSUPPORTED.",
        Version="1.0.0",FeatureId="com.evidencevr.quest-link-eye-space")]
#endif
    public sealed class QuestLinkEyeSpaceFeature : OpenXRFeature
    {
        public bool retryWithNullPath;
        public const string Revision="quest-link-eye-space-v1";
        public static string LastResult {get;private set;}="inactive";
        static bool retry,oculus,active;
        static ulong eyePath;
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
            active=false;LastResult="inactive";
            if(!Application.isEditor)return function;
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            if(!File.Exists(path))return function;
            AdaptiveTrainingConfig config;
            try {config=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path));}
            catch(Exception e){Debug.LogWarning("Eye compatibility disabled: invalid local configuration ("+e.GetType().Name+").");return function;}
            if(!EngineeringGazeRay.IsAllowed(config))return function;
            active=true;retry=retryWithNullPath;oculus=false;eyePath=0;actionPathCounts.Clear();soleActionPaths.Clear();LastResult="hooked";
            originalGetProc=Marshal.GetDelegateForFunctionPointer<GetProc>(function);
            return Marshal.GetFunctionPointerForDelegate(getProcHook);
        }
        protected override bool OnInstanceCreate(ulong instance)
        {
            if(!active)return true;
            oculus=OpenXRRuntime.name=="Oculus";
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
            if(result==0 && oculus)
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
            if(!oculus || eyePath==0 || info.subactionPath!=eyePath)return result;
            bool known=actionPathCounts.TryGetValue(info.action,out uint count);
            LastResult="eye_space_result="+result+";known_action="+known+";declared_subactions="+count;
            // Test only the aggregate of an eye-only action; never hand or multi-source actions.
            soleActionPaths.TryGetValue(info.action,out var sole);
            if(ShouldRetry(retry,oculus,result,known,count,sole,eyePath,info.subactionPath))
            {
                var aggregate=info;aggregate.subactionPath=0;
                result=originalCreateSpace(session,ref aggregate,out space);
                LastResult+=";null_path_retry="+result;
            }
            Debug.Log("Eye space probe: "+LastResult);
            return result;
        }
        internal static bool ShouldRetry(bool enabled,bool isOculus,int result,bool known,uint count,ulong sole,ulong eye,ulong requested)
            =>enabled && isOculus && result==-22 && known && count==1 && eye!=0 && sole==eye && requested==eye;
    }
}

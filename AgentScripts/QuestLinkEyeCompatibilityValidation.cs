using System;
using System.Reflection;
using System.Runtime.InteropServices;
using EvidenceVR;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR;
public static class QuestLinkEyeCompatibilityValidation
{
    public static string Run()
    {
        var type=typeof(QuestLinkEyeSpaceFeature);
        var method=type.GetMethod("ShouldRetry",BindingFlags.Static|BindingFlags.NonPublic);
        int checks=0;
        Action<bool,object[]> check=(expected,args)=>{checks++;if((bool)method.Invoke(null,args)!=expected)throw new Exception("Retry boundary failed: "+checks);};
        check(true,new object[]{true,true,-22,true,(uint)1,(ulong)42,(ulong)42,(ulong)42});
        check(false,new object[]{false,true,-22,true,(uint)1,(ulong)42,(ulong)42,(ulong)42});
        check(false,new object[]{true,false,-22,true,(uint)1,(ulong)42,(ulong)42,(ulong)42});
        check(false,new object[]{true,true,0,true,(uint)1,(ulong)42,(ulong)42,(ulong)42});
        check(false,new object[]{true,true,-1,true,(uint)1,(ulong)42,(ulong)42,(ulong)42});
        check(false,new object[]{true,true,-22,false,(uint)1,(ulong)42,(ulong)42,(ulong)42});
        check(false,new object[]{true,true,-22,true,(uint)0,(ulong)42,(ulong)42,(ulong)42});
        check(false,new object[]{true,true,-22,true,(uint)2,(ulong)42,(ulong)42,(ulong)42});
        check(false,new object[]{true,true,-22,true,(uint)1,(ulong)43,(ulong)42,(ulong)42});
        check(false,new object[]{true,true,-22,true,(uint)1,(ulong)42,(ulong)42,(ulong)43});
        check(false,new object[]{true,true,-22,true,(uint)1,(ulong)0,(ulong)0,(ulong)0});
        var action=type.GetNestedType("ActionInfo",BindingFlags.NonPublic);
        var space=type.GetNestedType("SpaceInfo",BindingFlags.NonPublic);
        if(IntPtr.Size!=8 || Marshal.SizeOf(action)!=224 || Marshal.OffsetOf(action,"pathCount").ToInt32()!=84 || Marshal.OffsetOf(action,"paths").ToInt32()!=88 || Marshal.SizeOf(space)!=64 || Marshal.OffsetOf(space,"subactionPath").ToInt32()!=24)throw new Exception("Unexpected x64 OpenXR ABI layout.");
        if(EditorApplication.isPlaying)throw new Exception("Run validation in Edit Mode.");
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
        var feature=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone).GetFeature<QuestLinkEyeSpaceFeature>();
        if(feature==null || !feature.enabled || !feature.retryWithNullPath)throw new Exception("Engineering Editor compatibility feature not enabled.");
        var android=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<QuestLinkEyeSpaceFeature>();
        if(android!=null && android.enabled)throw new Exception("Compatibility must not be enabled in Android settings.");
        AssetDatabase.SaveAssets();
        return "PASS: "+checks+" retry boundaries; x64 native ABI; Standalone enabled; Android not enabled. Real pose and headset confirmation are separate evidence.";
    }
}

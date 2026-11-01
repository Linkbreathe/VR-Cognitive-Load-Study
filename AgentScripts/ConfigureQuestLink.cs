using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

public static class ConfigureQuestLink
{
    public static string Run()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before configuring Link.");
        var factory=typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate",BindingFlags.NonPublic|BindingFlags.Static);
        var owner=(XRGeneralSettingsPerBuildTarget)factory.Invoke(null,null);
        const BuildTargetGroup group=BuildTargetGroup.Standalone;
        if(!owner.HasManagerSettingsForBuildTarget(group)) owner.CreateDefaultManagerSettingsForBuildTarget(group);
        var general=owner.SettingsForBuildTarget(group);
        general.InitManagerOnStart=true;
        if(!XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",group))
            throw new Exception("Could not enable the Standalone OpenXR loader.");
        var packageType=typeof(UnityEditor.XR.OpenXR.Features.OpenXRFeatureSetManager).Assembly.GetType("UnityEditor.XR.OpenXR.OpenXRPackageSettings");
        var package=packageType.GetMethod("GetOrCreateInstance",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
        var xr=(OpenXRSettings)packageType.GetMethod("GetSettingsForBuildTargetGroup").Invoke(package,new object[]{group});
        UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group);
        xr.GetFeature<OculusTouchControllerProfile>().enabled=true;
        var pro=xr.GetFeature<MetaQuestTouchProControllerProfile>();
        if(pro!=null) pro.enabled=true;
        xr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;
        EditorUtility.SetDirty(general.Manager); EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(owner); EditorUtility.SetDirty(xr); AssetDatabase.SaveAssets();
        const string message="Quest Link Editor preview configured: Standalone OpenXR, automatic initialization, Touch profiles. Android target and experiment scene preserved.";
        Debug.Log(message); return message;
    }
}

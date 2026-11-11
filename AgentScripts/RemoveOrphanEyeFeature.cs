using System;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.OpenXR;

public static class RemoveOrphanEyeFeature
{
    const string AssetPath="Assets/XR/Settings/OpenXR Package Settings.asset";
    const long OrphanId=4027437313833860866,ActiveId=8630051931892971767;
    static long Id(UnityEngine.Object value)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out string guid,out long id);
        return id;
    }
    public static string Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before removing an unused settings subasset.");
        var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        var active=settings.GetFeature<QuestLinkEyeSpaceFeature>();
        if(active==null || Id(active)!=ActiveId || !active.enabled || !active.retryWithNullPath)
            throw new InvalidOperationException("The expected active compatibility feature is not ready.");
        var assets=AssetDatabase.LoadAllAssetsAtPath(AssetPath);
        var orphan=assets.SingleOrDefault(a=>a is QuestLinkEyeSpaceFeature && Id(a)==OrphanId);
        if(orphan==null)throw new InvalidOperationException("Expected orphan not found; no assets removed.");
        foreach(var asset in assets.Where(a=>a!=orphan))
        {
            var serialized=new SerializedObject(asset);
            var field=serialized.GetIterator();
            while(field.Next(true))
                if(field.propertyType==SerializedPropertyType.ObjectReference && field.objectReferenceValue==orphan)
                    throw new InvalidOperationException("Orphan is referenced by "+asset.name+":"+field.propertyPath);
        }
        // The caller also checked project text references and saved the complete asset before removal.
        UnityEngine.Object.DestroyImmediate(orphan,true);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath,ImportAssetOptions.ForceUpdate);
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
        AssetDatabase.SaveAssetIfDirty(settings);
        var remaining=AssetDatabase.LoadAllAssetsAtPath(AssetPath).OfType<QuestLinkEyeSpaceFeature>().ToArray();
        active=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone).GetFeature<QuestLinkEyeSpaceFeature>();
        if(remaining.Length!=1 || remaining[0]!=active || Id(active)!=ActiveId || !active.enabled || !active.retryWithNullPath)
            throw new InvalidOperationException("Post-removal compatibility verification failed; restore the saved asset.");
        var android=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<QuestLinkEyeSpaceFeature>();
        if(android!=null && android.enabled)throw new InvalidOperationException("Unexpected Android compatibility feature.");
        string result="PASS: removed unreferenced eye feature "+OrphanId+"; sole remaining feature "+ActiveId+" is referenced, enabled and retry-enabled after import/refresh; Android unchanged; Play Mode stopped.";
        File.WriteAllText("Evidence/QuestLinkEyeRepair-20261110/orphan-cleanup/result.txt",result);
        Debug.Log(result);
        return result;
    }
}

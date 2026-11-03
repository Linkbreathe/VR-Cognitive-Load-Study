using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvidenceVR.Editor
{
    public static class LabEnvironmentLighting
    {
        public static string Prepare()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before preparing lightmaps.");
            const string root="Assets/TaskPlatform/Environment/";
            var room=GameObject.Find("NeutralRoom");if(room==null)throw new InvalidOperationException("Laboratory scene required.");
            foreach(string path in Directory.GetFiles(root+"ThirdParty","*.fbx",SearchOption.AllDirectories))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.generateSecondaryUV=true;importer.SaveAndReimport();
            }
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(root+"Meshes/ManufacturedBox.asset");
            if(mesh.uv2.Length==0){Unwrapping.GenerateSecondaryUVSet(mesh);EditorUtility.SetDirty(mesh);}
            foreach(var r in room.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.gameObject.activeInHierarchy)continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI);
                r.receiveGI=ReceiveGI.Lightmaps;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                r.scaleInLightmap=1;EditorUtility.SetDirty(r);
            }
            var old=room.transform.Find("BakedCeilingIllumination");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var parent=new GameObject("BakedCeilingIllumination").transform;parent.SetParent(room.transform,false);
            foreach(float x in new[]{-1.45f,1.45f})foreach(float z in new[]{-.95f,1.45f})
            {
                var go=new GameObject("FixedBakedArea");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,2.84f,z);
                go.transform.localRotation=Quaternion.Euler(90,0,0);var light=go.AddComponent<Light>();
                light.type=LightType.Rectangle;light.lightmapBakeType=LightmapBakeType.Baked;light.areaSize=new Vector2(.69f,1.09f);
                light.color=LabPalette.Hex("FFF0DA");light.intensity=8.0f;light.bounceIntensity=1.5f;light.range=8;light.shadows=LightShadows.Soft;
            }
            // Fixed practical lamps illuminate only the baked room, never the dynamic stimulus.
            foreach(var position in new[]{new Vector3(2.38f,1.70f,1.65f),new Vector3(-2.55f,1.68f,-1.95f),new Vector3(2.40f,1.12f,-.64f)})
            {
                var go=new GameObject("FixedBakedPractical");go.transform.SetParent(parent,false);go.transform.localPosition=position;
                var lamp=go.AddComponent<Light>();lamp.type=LightType.Point;lamp.lightmapBakeType=LightmapBakeType.Baked;
                lamp.color=LabPalette.Hex("FFE7C5");lamp.intensity=1.8f;lamp.bounceIntensity=1.5f;lamp.range=5;lamp.shadows=LightShadows.Soft;
            }
            string settingsPath=root+"LaboratoryLighting.lighting";
            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
            if(settings==null){settings=new LightingSettings();AssetDatabase.CreateAsset(settings,settingsPath);}
            settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;
            settings.lightmapResolution=24;settings.lightmapMaxSize=1024;settings.lightmapPadding=3;
            settings.directSampleCount=32;settings.indirectSampleCount=128;settings.environmentSampleCount=32;settings.maxBounces=4;
            settings.directionalityMode=LightmapsMode.NonDirectional;settings.ao=true;settings.aoMaxDistance=.20f;
            Lightmapping.lightingSettings=settings;EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(room.scene);EditorSceneManager.SaveScene(room.scene);AssetDatabase.SaveAssets();
            return "Prepared static room-only bake; original realtime key, ambient settings, movable task furniture and stimulus excluded from lightmaps.";
        }
    }
}

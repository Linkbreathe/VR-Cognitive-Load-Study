using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace EvidenceVR.Editor
{
    public static class DistractionSceneSetup
    {
        public static void Apply(TaskPlatformController controller)
        {
            var root=GameObject.Find("EnvironmentalDistractions"); if(root==null)root=new GameObject("EnvironmentalDistractions");
            var component=root.GetComponent<EnvironmentalDistractions>(); if(component==null)component=root.AddComponent<EnvironmentalDistractions>();
            controller.distractions=component;
            component.streetFiles=Directory.GetFiles("Assets/StreamingAssets/Distractions/Streets","*.mp4").Select(Path.GetFileName).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            component.doorClips=ImportAudio("Assets/TaskPlatform/Media/Doors").Concat(ImportAudio("Assets/TaskPlatform/Media/Environment")).ToArray();
            component.conversationClips=ImportAudio("Assets/TaskPlatform/Media/Conversation").Concat(ImportAudio("Assets/TaskPlatform/Media/News")).ToArray();
            component.listener=controller.head;
            component.mediaManifest=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TaskPlatform/Media/distraction-media-manifest.json");
            if(component.streetFiles.Length!=8 || component.doorClips.Length!=16 || component.conversationClips.Length!=16 || component.mediaManifest==null)throw new Exception("Expected eight videos, sixteen environmental sounds and sixteen speech clips plus the manifest.");
            var player=root.GetComponent<VideoPlayer>();if(player==null)player=root.AddComponent<VideoPlayer>();
            player.playOnAwake=false; player.isLooping=false; player.source=VideoSource.Url;player.renderMode=VideoRenderMode.RenderTexture;
            player.audioOutputMode=VideoAudioOutputMode.None;player.waitForFirstFrame=true;player.skipOnDrop=true;player.sendFrameReadyEvents=true;
            component.player=player;
            var screenMaterial=Material("StreetWindowScreen","Universal Render Pipeline/Unlit",Color.white);
            var frameMaterial=Material("StreetWindowFrame","Universal Render Pipeline/Lit",LabPalette.Trim);
            foreach(string legacyName in new[]{"StreetWindowLeft","StreetWindowRight"})
            {var legacy=root.transform.Find(legacyName);if(legacy!=null)legacy.gameObject.SetActive(false);}
            var window=Child(root.transform,"StreetWindowFront");
            window.localPosition=EnvironmentalDistractions.FrontWindowPosition(0);window.localRotation=Quaternion.identity;window.localScale=Vector3.one*.62f;
            var screen=Primitive(window,"VideoSurface",PrimitiveType.Quad,Vector3.zero,new Vector3(2.4f,1.35f,1),screenMaterial);
            Primitive(window,"TopFrame",PrimitiveType.Cube,new Vector3(0,.71f,0),new Vector3(2.54f,.07f,.07f),frameMaterial);
            Primitive(window,"BottomFrame",PrimitiveType.Cube,new Vector3(0,-.71f,0),new Vector3(2.54f,.07f,.07f),frameMaterial);
            Primitive(window,"LeftFrame",PrimitiveType.Cube,new Vector3(-1.235f,0,0),new Vector3(.07f,1.35f,.07f),frameMaterial);
            Primitive(window,"RightFrame",PrimitiveType.Cube,new Vector3(1.235f,0,0),new Vector3(.07f,1.35f,.07f),frameMaterial);
            component.windowRoots=new[]{window.gameObject};component.windowScreens=new[]{screen};window.gameObject.SetActive(false);
            component.doorSources=new AudioSource[2];component.conversationSources=new AudioSource[2];
            for(int side=0;side<2;side++)
            {
                string suffix=side==0 ? "Left" : "Right"; float sign=side==0 ? -1 : 1;
                component.doorSources[side]=Source(root.transform,"RearDoor"+suffix,new Vector3(sign*2.4f,1.35f,-2.65f),.35f,false);
                component.conversationSources[side]=Source(root.transform,"OutsideConversation"+suffix,new Vector3(sign*1.9f,1.55f,-3.35f),.3f,true);
            }
            EditorUtility.SetDirty(component);EditorUtility.SetDirty(player);EditorUtility.SetDirty(controller);
        }
        static AudioClip[] ImportAudio(string folder)
        {
            return Directory.GetFiles(folder,"*.wav").OrderBy(x=>x,StringComparer.Ordinal).Select(path=>
            {
                path=path.Replace('\\','/');
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var settings=importer.defaultSampleSettings;
                bool changed=!importer.forceToMono || settings.loadType!=AudioClipLoadType.DecompressOnLoad || settings.compressionFormat!=AudioCompressionFormat.PCM;
                if(changed)
                {
                    importer.forceToMono=true;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
                    importer.defaultSampleSettings=settings;importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }).ToArray();
        }
        static AudioSource Source(Transform parent,string name,Vector3 position,float volume,bool outside)
        {
            var child=Child(parent,name);child.localPosition=position;
            var source=child.GetComponent<AudioSource>();if(source==null)source=child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=false;source.spatialBlend=1;source.volume=volume;source.dopplerLevel=0;
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=2.5f;source.maxDistance=12;source.spread=0;
            source.spatialize=!string.IsNullOrEmpty(AudioSettings.GetSpatializerPluginName());
            if(outside){var filter=child.GetComponent<AudioLowPassFilter>();if(filter==null)filter=child.gameObject.AddComponent<AudioLowPassFilter>();filter.cutoffFrequency=5500;filter.lowpassResonanceQ=1;EditorUtility.SetDirty(filter);}
            EditorUtility.SetDirty(source);return source;
        }
        static Transform Child(Transform parent,string name)
        {var child=parent.Find(name);if(child!=null)return child;var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Renderer Primitive(Transform parent,string name,PrimitiveType kind,Vector3 position,Vector3 scale,Material material)
        {
            var child=parent.Find(name);if(child==null){child=GameObject.CreatePrimitive(kind).transform;child.name=name;child.SetParent(parent,false);UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());}
            child.localPosition=position;child.localScale=scale;var renderer=child.GetComponent<Renderer>();renderer.sharedMaterial=material;return renderer;
        }
        static Material Material(string name,string shader,Color color)
        {
            string path="Assets/TaskPlatform/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,path);}
            material.color=color;if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.1f);EditorUtility.SetDirty(material);return material;
        }
    }
}

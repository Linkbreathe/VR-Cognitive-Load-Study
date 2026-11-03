using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LabEnvironmentValidation
{
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    public static object Inspect()
    {
        checks=0;
        var room=GameObject.Find("NeutralRoom");var design=room.transform.Find("ModernLaboratory");
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(LabEnvironmentIdentity.CurrentRevision=="warm-study-v1","environment identity");
        Check(GraphicsSettings.currentRenderPipeline!=null,"existing URP pipeline");
        Check(LightmapSettings.lightmaps.Length>0 && !Lightmapping.isRunning,"finished lightmaps");
        Check(design.GetComponentsInChildren<AudioSource>().Length==0,"no new sound");
        Check(design.GetComponentsInChildren<Animator>().Length==0 && design.GetComponentsInChildren<Animation>().Length==0,"static decor");
        Check(design.GetComponentsInChildren<Collider>().Length==0,"no new task collisions");
        var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Check(lights.Count(l=>l.lightmapBakeType!=LightmapBakeType.Baked)==1,"one original realtime light");
        Check(lights.Count(l=>l.lightmapBakeType==LightmapBakeType.Baked)==7,"seven baked fixtures");
        var key=GameObject.Find("StableSoftLight").GetComponent<Light>();
        Check(Mathf.Approximately(key.intensity,.7f) && key.color==LabPalette.KeyLight && key.shadows==LightShadows.None,"original task key unchanged");
        Check(!GameObjectUtility.AreStaticEditorFlagsSet(owner.stimulus.gameObject,StaticEditorFlags.ContributeGI),"stimulus excluded from bake");
        Check(!GameObjectUtility.AreStaticEditorFlagsSet(owner.spatialView.bench.Find("WorkSurface").gameObject,StaticEditorFlags.ContributeGI),"movable desk excluded from bake");
        Check(Mathf.Abs(owner.spatialView.bench.Find("WorkSurface").GetComponent<Renderer>().bounds.max.y-.65f)<.003f,"table height retained");
        Check(design.Find("LeftAcousticBay")==null && design.Find("SignalEquipmentCart")==null,"institutional draft removed");
        var renderers=design.GetComponentsInChildren<MeshRenderer>();
        var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
        Check(materials.All(m=>m!=null && m.shader!=null && m.shader.isSupported && m.name!="Default-Material" && !m.shader.name.Contains("Error")),"authored supported materials");
        Check(renderers.All(r=>r.lightmapIndex>=0 && r.lightmapIndex<LightmapSettings.lightmaps.Length),"all furnishings baked and reload-persistent");
        var badTextures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(n=>m.GetTexture(n))).OfType<Texture2D>().Distinct()
            .Select(t=>AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter).Where(i=>i!=null)
            .Where(i=>i.maxTextureSize>1024 || !i.mipmapEnabled || i.GetPlatformTextureSettings("Android").format!=TextureImporterFormat.ASTC_6x6).Select(i=>i.assetPath).ToArray();
        Check(badTextures.Length==0,"mobile texture limits");
        int clearRays=0;
        foreach(float ox in new[]{-.15f,0,.15f})foreach(float oy in new[]{1.1f,1.2f,1.3f})
        for(int x=0;x<=8;x++)for(int y=0;y<=4;y++)
        {
            var origin=new Vector3(ox,oy,0);var target=new Vector3(-1.6f+x*.4f,.6f+y*.45f,2.915f);
            var ray=new Ray(origin,(target-origin).normalized);float length=Vector3.Distance(origin,target);
            Check(!renderers.Any(r=>r.bounds.IntersectRay(ray,out float distance) && distance<length),"decor blocks movie sightline");clearRays++;
        }
        long triangles=renderers.Sum(r=>{var mesh=r.GetComponent<MeshFilter>().sharedMesh;return Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(long)mesh.GetIndexCount(s)/3);});
        Debug.Log("Warm study inspection passed; checks="+checks+"; clear movie rays="+clearRays+"; environment="+LabEnvironmentIdentity.CurrentRevision);
        return new {passed=true,checks,clearRays,environment=LabEnvironmentIdentity.CurrentRevision,renderers=renderers.Length,materials=materials.Length,triangles,lightmaps=LightmapSettings.lightmaps.Length,bakedLights=7,realtimeLights=1};
    }
    public static async Task<object> Live()
    {
        checks=0;
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(Application.isPlaying && owner.Engine!=null,"live session");
        int before=Time.frameCount;await Task.Delay(300);int after=Time.frameCount;
        Check(after>before,"Editor actually advances frames");
        string metadata=File.ReadAllText(Path.Combine(owner.DataPath,"session.json"));
        Check(metadata.Contains("warm-study-v1") && metadata.Contains("2.0.0-stage2-dev12"),"actual session environment/application metadata");
        Check(metadata.Contains("editor-test"),"engineering preview isolated from participants");
        Check(owner.Engine.Adaptive.Config.version=="2.0.0-stage2-dev11","unchanged configuration/service contract");
        Check(owner.Engine.Config.trialSeconds==4.5f && owner.Engine.Config.visibleSeconds==4 && owner.Engine.Config.responseSeconds==4,"timing retained");
        float gap=owner.stimulus.bounds.min.y-owner.spatialView.bench.Find("StimulusPlinth").GetComponent<Renderer>().bounds.max.y;
        Check(Mathf.Abs(gap-.0864f)<.0001f,"live sphere clearance");
        Debug.Log("Warm study live check passed; frames="+before+"->"+after+"; metadata=warm-study-v1; timing=4/4.5; editor-test only.");
        return new {passed=true,checks,before,after,gap,owner.DataPath,environment=LabEnvironmentIdentity.CurrentRevision};
    }
}

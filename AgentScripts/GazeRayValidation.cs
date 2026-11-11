using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using EvidenceVR;
public static class GazeRayValidation
{
    public static string UnboundLifecycle()
    {
        var runtimeObject=new GameObject("Unbound runtime cleanup fixture");
        var mediaObject=new GameObject("Reloaded media cleanup fixture");
        try
        {
            runtimeObject.AddComponent<AdaptiveRuntime>().Shutdown();
            var media=mediaObject.AddComponent<AdaptiveMedia>();
            var active=typeof(AdaptiveMedia).GetField("active",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            active.SetValue(media,Activator.CreateInstance(active.FieldType));
            UnityEngine.Object.DestroyImmediate(mediaObject);mediaObject=null;
            return "PASS: unbound runtime and restored-active media cleanup without an engine.";
        }
        finally{UnityEngine.Object.DestroyImmediate(runtimeObject);if(mediaObject!=null)UnityEngine.Object.DestroyImmediate(mediaObject);}
    }
    static void Lifecycle(EngineeringGazeRay ray,string name,params object[] args)
        =>typeof(EngineeringGazeRay).GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ray,args);
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static async Task<string> Run()
    {
        checks=0;
        var root=new GameObject("Isolated synthetic eye-ray fixture");
        var deniedObject=new GameObject("Participant ray exclusion fixture");
        var cameraObject=new GameObject("Synthetic ray verification camera");
        RenderTexture rt=null;Texture2D image=null;
        try
        {
            var ray=root.AddComponent<EngineeringGazeRay>();
            ray.Bind(new AdaptiveTrainingConfig{enabled=true,sessionType="engineering"});
            Check(!ray.RayEnabled && root.GetComponentsInChildren<LineRenderer>().Length==0,"Default off allocates no lines");
            var denied=deniedObject.AddComponent<EngineeringGazeRay>();
            denied.Bind(new AdaptiveTrainingConfig{enabled=true,sessionType="participant",engineeringGazeRay=true});denied.SetEnabled(true);
            Check(!denied.RayEnabled && deniedObject.GetComponentsInChildren<LineRenderer>().Length==0,"Participant cannot enable ray");
            Check(!EngineeringGazeRay.IsAllowed(new AdaptiveTrainingConfig{enabled=false}),"Disabled Stage 2 excluded");
            ray.SetEnabled(true);
            var sample=GazeRegions.Classify(true,Vector3.zero,Vector3.forward,new Bounds(Vector3.forward*2,Vector3.one*.2f),false,null,false);
            ray.Present(sample,"SYNTHETIC FIXTURE ONLY",Time.realtimeSinceStartupAsDouble,true);
            Check(ray.Visible && ray.LineVisible && ray.State==EngineeringGazeRay.DisplayState.Live,"Valid non-ROI gaze visible (live)");
            var all=root.GetComponentsInChildren<LineRenderer>();Check(all.Length==2,"One ray line plus one hit reticle");
            var reticle=all.Single(l=>l.loop);var lines=all.Where(l=>!l.loop).ToArray();Check(lines.Length==1,"One continuous line");
            Check(ray.ReticleVisible && reticle.positionCount==EngineeringGazeRay.ReticleSegments,"Reticle visible with valid eyes");
            float expectedRadius=EngineeringGazeRay.DefaultHitDistance*Mathf.Tan(EngineeringGazeRay.ReticleRadiusDegrees*Mathf.Deg2Rad);
            for(int i=0;i<reticle.positionCount;i++)
            {
                var p=reticle.GetPosition(i);var center=sample.origin+sample.direction*EngineeringGazeRay.DefaultHitDistance;
                Check(Mathf.Abs(Vector3.Distance(p,center)-expectedRadius)<.0001f && Mathf.Abs(Vector3.Dot(p-center,sample.direction))<.0001f,"Reticle ring centered on gaze hit, facing the eye");
            }
            Check(Mathf.Approximately(reticle.sharedMaterial.GetFloat("_ZTest"),(float)UnityEngine.Rendering.CompareFunction.Always),"Reticle not hidden by its hit surface");
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,true,1f);
            Check(Vector3.Distance(reticle.GetPosition(0),sample.origin+sample.direction)<expectedRadius,"Reticle follows supplied hit distance");
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,true);
            var surface=new Bounds(Vector3.forward*1.5f,Vector3.one*.2f);
            float surfaceHit=GazeRegions.HitDistance(sample,surface,null,9f);Check(surfaceHit>0 && surfaceHit<=1.4001f,"Reticle lands no farther than the sphere surface");
            Check(GazeRegions.HitDistance(default,surface,null,9f)==9f,"Invalid eyes use fallback distance");
            reticle.gameObject.layer=30;
            for(int i=0;i<lines.Length;i++)
            {
                Check(Mathf.Abs(Vector3.Distance(lines[i].GetPosition(0),sample.origin)-.65f)<.0001f,"Near-eye clearance");
                Check(Mathf.Abs(Vector3.Distance(lines[i].GetPosition(1),sample.origin)-3.85f)<.0001f,"Far endpoint");
                Check(Mathf.Abs(lines[i].widthMultiplier-.0015f)<.00001f,"Thin line");
                Check(lines[i].sharedMaterial.GetColor("_Color")==new Color(1,1,1,.22f),"Translucent white material");
                lines[i].gameObject.layer=30;
            }
            Check(root.GetComponentsInChildren<Collider>().Length==0,"No collision effects");
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
            camera.transform.position=new Vector3(1.8f,1.2f,-.4f);camera.transform.LookAt(Vector3.forward*1.9f);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.03f,.05f);
            rt=new RenderTexture(1600,1000,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();
            var before=RenderTexture.active;RenderTexture.active=rt;
            image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();RenderTexture.active=before;
            Directory.CreateDirectory("Evidence/GazeComfort");File.WriteAllBytes("Evidence/GazeComfort/synthetic-ray.png",image.EncodeToPNG());
            ray.Present(sample,"SYNTHETIC FROZEN",Time.realtimeSinceStartupAsDouble,true,EngineeringGazeRay.DefaultHitDistance,true);
            Check(ray.State==EngineeringGazeRay.DisplayState.Frozen && ray.Visible && reticle.sharedMaterial.GetColor("_Color")==EngineeringGazeRay.FrozenColor,"Frozen raw pose drawn amber");
            ray.Present(default,"INVALID",Time.realtimeSinceStartupAsDouble,true);Check(ray.State==EngineeringGazeRay.DisplayState.Held && ray.Visible && !ray.LineVisible && reticle.sharedMaterial.GetColor("_Color")==EngineeringGazeRay.HeldColor,"Invalid eyes hold last point in gray; line hidden");
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,false);Check(ray.State==EngineeringGazeRay.DisplayState.Held && ray.Visible,"Disallowed phase holds");
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,true);
            await Task.Delay(180);Lifecycle(ray,"LateUpdate");Check(ray.State==EngineeringGazeRay.DisplayState.Held && ray.Visible && !ray.LineVisible,"Stale samples hold");
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,true);
            Lifecycle(ray,"OnApplicationFocus",false);Check(ray.State==EngineeringGazeRay.DisplayState.Held && ray.Visible,"Focus loss holds");
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,true);Check(ray.State==(Application.isEditor?EngineeringGazeRay.DisplayState.Live:EngineeringGazeRay.DisplayState.Held),"Editor window focus does not block valid eyes; player focus remains required");
            foreach(bool editor in new[]{false,true})foreach(bool focused in new[]{false,true})foreach(bool paused in new[]{false,true})
                Check(EngineeringGazeRay.FocusAllowsRay(editor,focused,paused)==(!paused&&(editor||focused)),"Independent pause/focus policy");
            Lifecycle(ray,"OnApplicationPause",true);Lifecycle(ray,"OnApplicationFocus",true);
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,true);Check(ray.State==EngineeringGazeRay.DisplayState.Held,"Focus recovery cannot override application pause");
            Lifecycle(ray,"OnApplicationPause",false);
            Lifecycle(ray,"OnApplicationFocus",true);
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,true);Check(ray.State==EngineeringGazeRay.DisplayState.Live,"Focus recovery requires new sample");
            ray.SetEnabled(false);Check(!ray.Visible && !ray.RayEnabled && ray.State==EngineeringGazeRay.DisplayState.Off,"Live toggle off hides everything");
            string report="PASS: "+checks+" isolated synthetic ray checks. No hardware-validity claim; no synthetic data injected into production reader.";
            File.WriteAllText("Evidence/GazeComfort/ray-validation.txt",report);Debug.Log(report);return report;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(deniedObject);UnityEngine.Object.DestroyImmediate(cameraObject);
            if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(image!=null)UnityEngine.Object.DestroyImmediate(image);
        }
    }
}

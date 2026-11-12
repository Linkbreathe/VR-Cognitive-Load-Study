using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EvidenceVR;
using UnityEngine;
public static class BirdReticleValidation
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static object Run()
    {
        var root=new GameObject("Isolated synthetic bird fixture");var denied=new GameObject("Participant exclusion fixture");
        var cameraObject=new GameObject("Bird verification camera");RenderTexture rt=null;Texture2D image=null;
        try
        {
            var ray=root.AddComponent<EngineeringGazeRay>();ray.Bind(new AdaptiveTrainingConfig{enabled=true,sessionType="engineering"});
            Check(!ray.Visible && root.GetComponentsInChildren<Renderer>().Length==0,"Default off");
            var participant=denied.AddComponent<EngineeringGazeRay>();participant.Bind(new AdaptiveTrainingConfig{enabled=true,sessionType="participant",engineeringGazeRay=true});
            Check(!participant.RayEnabled && denied.GetComponentsInChildren<Renderer>().Length==0,"Participant exclusion");
            ray.SetEnabled(true);Check(ray.AnimationFrameCount==8,"Eight supplied frames");
            var sample=GazeRegions.Classify(true,Vector3.zero,Vector3.forward,new Bounds(Vector3.forward*2,Vector3.one*.2f),false,null,false);
            ray.Present(sample,"SYNTHETIC",Time.realtimeSinceStartupAsDouble,true);
            var bird=root.GetComponentInChildren<MeshRenderer>();var line=root.GetComponentInChildren<LineRenderer>();
            Check(ray.Visible && ray.LineVisible && bird!=null && line!=null,"One bird and one ray");
            Check(root.GetComponentsInChildren<Collider>().Length==0,"No collider changes");
            Check(Vector3.Distance(bird.transform.position,Vector3.forward*2)<.0001f,"Bird centered on eye hit");
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=8;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.17f,.2f);camera.cullingMask=1<<30;
            bird.gameObject.layer=30;line.gameObject.layer=30;
            rt=new RenderTexture(512,512,24);camera.targetTexture=rt;image=new Texture2D(512,512,TextureFormat.RGB24,false);
            double start=(double)typeof(EngineeringGazeRay).GetField("animationStarted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ray);
            Directory.CreateDirectory("Evidence/ThreeStageComparison-20261111/bird");
            for(int i=0;i<8;i++)
            {
                ray.AdvanceAnimation(start+i*.15+.025);Check(ray.AnimationFrame==i,"GIF frame timing "+i);
                camera.Render();var prior=RenderTexture.active;RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=prior;
                File.WriteAllBytes("Evidence/ThreeStageComparison-20261111/bird/frame-"+i+".png",image.EncodeToPNG());
            }
            ray.AdvanceAnimation(start+1.225);Check(ray.AnimationFrame==0,"GIF loops");
            ray.Present(sample,"FROZEN",Time.realtimeSinceStartupAsDouble,true,2,true);Check(ray.State==EngineeringGazeRay.DisplayState.Frozen && bird.sharedMaterial.GetColor("_Color")==EngineeringGazeRay.FrozenColor,"Frozen amber");
            ray.Present(default,"MISSING",Time.realtimeSinceStartupAsDouble,true);Check(ray.Visible && !ray.LineVisible && ray.State==EngineeringGazeRay.DisplayState.Held && bird.sharedMaterial.GetFloat("_Held")==1,"Held gray; ray hidden");
            ray.AdvanceAnimation(start+.625);Check(ray.AnimationFrame==4 && ray.Visible,"Held bird remains animated");
            ray.SetEnabled(false);Check(!ray.Visible && !ray.LineVisible,"Switch hides all");
            Debug.Log("PASS: bird eight-frame render, 150 ms frames, loop, tracked/frozen/held states, participant exclusion and no colliders.");
            return new {frames=8,frameSeconds=.15,loopSeconds=1.2,states="LIVE/FROZEN/HELD",scope="isolated synthetic renderer; no worn eye tracking acceptance"};
        }
        finally
        {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(denied);UnityEngine.Object.DestroyImmediate(cameraObject);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class LabEnvironmentPreview
{
    public static string RenderRoom(string prefix)
    {
        if(Application.isPlaying)throw new Exception("Room overview captures use Edit Mode; live task capture is separate.");
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        bool[] enabled=canvases.Select(x=>x.enabled).ToArray();bool sphere=c.stimulus.gameObject.activeSelf;
        var go=new GameObject("EnvironmentReviewCamera");var cam=go.AddComponent<Camera>();cam.CopyFrom(Camera.main);cam.enabled=false;cam.fieldOfView=78;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);target.antiAliasing=4;
        var oldTarget=RenderTexture.active;
        try
        {
            foreach(var canvas in canvases)canvas.enabled=false;c.stimulus.gameObject.SetActive(true);
            cam.targetTexture=target;
            Capture(cam,new Vector3(0,1.2f,0),Quaternion.Euler(0,0,0),prefix+"-front.png");
            Capture(cam,new Vector3(0,1.45f,.05f),Quaternion.Euler(6,110,0),prefix+"-workspace.png");
            Capture(cam,new Vector3(.45f,1.55f,.05f),Quaternion.Euler(4,-115,0),prefix+"-storage.png");
            return "Three inspection cameras rendered; task camera, UI and scene transforms restored.";
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];c.stimulus.gameObject.SetActive(sphere);
            RenderTexture.active=oldTarget;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);
        }
    }
    static void Capture(Camera cam,Vector3 position,Quaternion rotation,string name)
    {
        cam.transform.SetPositionAndRotation(position,rotation);cam.Render();cam.Render();
        var target=cam.targetTexture;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes("Evidence/LabEnvironment/"+name,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
    }
    public static async Task<string> BeginLivePractice()
    {
        var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(c.Engine.Phase!=TaskPhase.Preparation)throw new Exception("Start a fresh engineering session.");
        c.Engine.Note("engineering_preview","environment task-view check; direct engine start bypasses preparation inputs; not participant evidence",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        c.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);c.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        typeof(TaskPlatformController).GetField("lastTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,Time.realtimeSinceStartupAsDouble);
        await Task.Delay(200);
        return "Live practice; frame="+Time.frameCount+"; phase="+c.Engine.Phase+"; environment="+LabEnvironmentIdentity.CurrentRevision+"; data="+c.DataPath;
    }
}

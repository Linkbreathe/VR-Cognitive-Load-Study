using UnityEngine;
using UnityEngine.Rendering;

namespace EvidenceVR
{
    // This overlay never changes stimulus colors, video pixels, scoring or controller inputs.
    public sealed class GazeDebugHighlighter : MonoBehaviour
    {
        public const string Revision="engineering-gaze-outline-v5-soft-edge";
        public const float HitAlpha=.38f, ArmedAlpha=.10f, FadeInSeconds=.12f;
        public const double MaximumSampleAge=.1;
        public bool Allowed {get;private set;}
        // Highlighted = fresh valid eyes on the target (bright). Armed = the persistent faint sphere outline
        // shown at the placed sphere in every phase, with or without eyes.
        public bool TaskHighlighted=>taskLine!=null && taskLine.enabled && taskGazed;
        public bool TaskArmed=>taskLine!=null && taskLine.enabled && !taskGazed;
        public bool VideoHighlighted=>videoLine!=null && videoLine.enabled;
        public event System.Action<string> OnStateChanged;
        string lastState="none";
        TaskPlatformController owner;
        LineRenderer taskLine,videoLine;
        Material material,armedMaterial,videoMaterial;
        float taskBlend,videoBlend;
        double lastVisualTime;
        bool taskGazed,videoGazed;
        double lastSample=double.NegativeInfinity;
        bool suspended;
        public static bool IsAllowed(AdaptiveTrainingConfig config)=>config!=null && config.enabled &&
            config.sessionType=="engineering" && config.engineeringGazeHighlight;
        public void Bind(TaskPlatformController controller,AdaptiveTrainingConfig config)
        {
            owner=controller;Allowed=IsAllowed(config);
            if(!Allowed)return;
            var shader=Resources.Load<Shader>("GazeDebugOutline");
            if(shader==null)throw new System.InvalidOperationException("Gaze debug outline shader is missing.");
            material=new Material(shader);material.SetFloat("_SoftEdge",1);
            videoMaterial=new Material(shader);videoMaterial.SetFloat("_SoftEdge",1);
            armedMaterial=new Material(shader);armedMaterial.SetColor("_Color",new Color(1,1,1,ArmedAlpha));armedMaterial.SetFloat("_SoftEdge",1);
            lastVisualTime=Time.realtimeSinceStartupAsDouble;
            taskLine=CreateLine("Engineering gaze sphere outline",64,.0025f);
            videoLine=CreateLine("Engineering gaze movie border",4,.004f);videoLine.sharedMaterial=videoMaterial;
            Clear();
        }
        LineRenderer CreateLine(string name,int count,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.loop=true;
            line.positionCount=count;line.widthMultiplier=width;line.numCornerVertices=3;line.numCapVertices=0;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;
            line.enabled=false;return line;
        }
        // Engineering preflight uses the placed outline too. Only accepted eyes can brighten it.
        bool GazeAvailable=>Allowed && !suspended && owner!=null && owner.Engine!=null &&
            (Application.isEditor || Application.isFocused);
        // The placed sphere position is known in every phase, even while its renderer is hidden between stimuli.
        bool SpherePlaced=>owner!=null && owner.stimulus!=null && owner.stimulus.gameObject.activeInHierarchy;
        float lastRadius=-1;
        void SphereGeometry(out Vector3 center,out float radius)
        {
            center=owner.stimulus.transform.position;
            if(owner.stimulus.enabled && owner.stimulus.bounds.extents.x>0)lastRadius=owner.stimulus.bounds.extents.x;
            radius=lastRadius>0?lastRadius:.5f*Mathf.Abs(owner.stimulus.transform.lossyScale.x);
        }
        bool MovieShown=>owner!=null && owner.AdaptiveRuntime!=null && owner.AdaptiveRuntime.Media!=null && owner.AdaptiveRuntime.Media.IsVideoVisible;
        public void Present(GazeRegionSample sample,double now)
        {
            if(!Allowed)return;
            if(!GazeAvailable || !sample.valid){taskGazed=videoGazed=false;Refresh();return;}
            lastSample=now;
            taskGazed=sample.task && SpherePlaced;
            videoGazed=!taskGazed && sample.video && MovieShown;
            Refresh();
        }
        // Drops gaze highlighting only; the persistent faint outline stays.
        public void Clear()
        {
            lastSample=double.NegativeInfinity;taskGazed=videoGazed=false;
            Refresh();
        }
        void Refresh()
        {
            if(taskLine==null){Report();return;}
            if(!GazeAvailable || Time.realtimeSinceStartupAsDouble-lastSample>MaximumSampleAge)taskGazed=videoGazed=false;
            if(taskGazed && !SpherePlaced)taskGazed=false;
            if(videoGazed && !MovieShown)videoGazed=false;
            videoLine.enabled=videoGazed;
            taskLine.enabled=SpherePlaced;
            double now=Time.realtimeSinceStartupAsDouble;
            float step=(float)System.Math.Max(0,now-lastVisualTime)/FadeInSeconds;lastVisualTime=now;
            // Fade in only: stale/invalid/off-target eyes remove hit brightness immediately.
            taskBlend=taskGazed?Mathf.MoveTowards(taskBlend,1,step):0;
            videoBlend=videoGazed?Mathf.MoveTowards(videoBlend,1,step):0;
            material.SetColor("_Color",new Color(1,1,1,Mathf.Lerp(ArmedAlpha,HitAlpha,Mathf.SmoothStep(0,1,taskBlend))));
            videoMaterial.SetColor("_Color",new Color(1,1,1,HitAlpha*Mathf.SmoothStep(0,1,videoBlend)));
            taskLine.sharedMaterial=taskGazed?material:armedMaterial;
            taskLine.widthMultiplier=taskGazed?Mathf.Lerp(.0015f,.0035f,Mathf.SmoothStep(0,1,taskBlend)):.0015f;
            UpdateGeometry();
            Report();
        }
        void Report(){string state=TaskHighlighted?"task":VideoHighlighted?"video":"none";if(state==lastState)return;lastState=state;OnStateChanged?.Invoke(state);}
        void LateUpdate(){if(Allowed)Refresh();}
        void UpdateGeometry()
        {
            if(taskLine!=null && taskLine.enabled)
            {
                SphereGeometry(out Vector3 center,out float radius);
                Vector3 toEye=owner.head.position-center;float distance=toEye.magnitude;
                if(distance<=radius){taskLine.enabled=false;return;}
                Vector3 normal=toEye/distance;
                Vector3 right=Vector3.Cross(normal,Mathf.Abs(normal.y)>.95f?Vector3.right:Vector3.up).normalized;
                Vector3 up=Vector3.Cross(normal,right);
                // Tangent circle follows the sphere silhouette without recoloring its surface.
                center+=normal*(radius*radius/distance);
                float edge=radius*Mathf.Sqrt(1-radius*radius/(distance*distance))+.0015f;
                for(int i=0;i<taskLine.positionCount;i++)
                {float angle=2*Mathf.PI*i/taskLine.positionCount;taskLine.SetPosition(i,center+edge*(Mathf.Cos(angle)*right+Mathf.Sin(angle)*up));}
            }
            if(videoLine!=null && videoLine.enabled)
            {
                var screen=owner.AdaptiveRuntime.Media.Screen;
                float offset=Vector3.Dot(owner.head.position-screen.position,screen.forward)>=0?.003f:-.003f;
                var corners=new[]{new Vector3(-.501f,-.501f,offset),new Vector3(.501f,-.501f,offset),new Vector3(.501f,.501f,offset),new Vector3(-.501f,.501f,offset)};
                for(int i=0;i<4;i++)videoLine.SetPosition(i,screen.TransformPoint(corners[i]));
            }
        }
        void OnApplicationPause(bool paused){suspended=paused;if(paused)Clear();}
        void OnApplicationFocus(bool focused){if(!focused)Clear();}
        void OnDisable()=>Clear();
        void OnDestroy(){Clear();if(material!=null)Destroy(material);if(armedMaterial!=null)Destroy(armedMaterial);if(videoMaterial!=null)Destroy(videoMaterial);}
    }
}

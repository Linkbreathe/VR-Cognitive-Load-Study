using UnityEngine;
using UnityEngine.Rendering;

namespace EvidenceVR
{
    // This overlay never changes stimulus colors, video pixels, scoring or controller inputs.
    public sealed class GazeDebugHighlighter : MonoBehaviour
    {
        public const string Revision="engineering-gaze-outline-v1";
        public const double MaximumSampleAge=.1;
        public bool Allowed {get;private set;}
        public bool TaskHighlighted=>taskLine!=null && taskLine.enabled;
        public bool VideoHighlighted=>videoLine!=null && videoLine.enabled;
        public event System.Action<string> OnStateChanged;
        string lastState="none";
        TaskPlatformController owner;
        LineRenderer taskLine,videoLine;
        Material material;
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
            material=new Material(shader);material.SetColor("_Color",new Color(1,1,1,.55f));
            taskLine=CreateLine("Engineering gaze sphere outline",64,.0025f);
            videoLine=CreateLine("Engineering gaze movie border",4,.006f);
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
        bool Available=>Allowed && !suspended && owner!=null && owner.Engine!=null && owner.Engine.Phase==TaskPhase.Running &&
            (Application.isEditor || Application.isFocused);
        public void Present(GazeRegionSample sample,double now)
        {
            if(!Available || !sample.valid){Clear();return;}
            lastSample=now;
            taskLine.enabled=sample.task && owner.Engine.StimulusVisible && owner.stimulus.enabled;
            videoLine.enabled=!taskLine.enabled && sample.video && owner.AdaptiveRuntime!=null && owner.AdaptiveRuntime.Media.IsVideoVisible;
            UpdateGeometry();
            Report();
        }
        public void Clear()
        {
            lastSample=double.NegativeInfinity;
            if(taskLine!=null)taskLine.enabled=false;
            if(videoLine!=null)videoLine.enabled=false;
            Report();
        }
        void Report(){string state=TaskHighlighted?"task":VideoHighlighted?"video":"none";if(state==lastState)return;lastState=state;OnStateChanged?.Invoke(state);}
        void LateUpdate()
        {
            if(!Available || Time.realtimeSinceStartupAsDouble-lastSample>MaximumSampleAge){Clear();return;}
            if(taskLine!=null && (!owner.Engine.StimulusVisible || !owner.stimulus.enabled))taskLine.enabled=false;
            if(videoLine!=null && (owner.AdaptiveRuntime==null || !owner.AdaptiveRuntime.Media.IsVideoVisible))videoLine.enabled=false;
            UpdateGeometry();
            Report();
        }
        void UpdateGeometry()
        {
            if(taskLine!=null && taskLine.enabled)
            {
                Vector3 center=owner.stimulus.bounds.center,toEye=owner.head.position-center;
                float radius=owner.stimulus.bounds.extents.x,distance=toEye.magnitude;
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
        void OnDestroy(){Clear();if(material!=null)Destroy(material);}
    }
}

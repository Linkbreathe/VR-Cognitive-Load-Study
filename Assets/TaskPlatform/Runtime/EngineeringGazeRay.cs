using UnityEngine;
using UnityEngine.Rendering;

namespace EvidenceVR
{
    // Displays only the tracked-eye ray supplied by the production gaze reader.
    public sealed class EngineeringGazeRay : MonoBehaviour
    {
        public const string Revision="engineering-gaze-ray-v2-soft-white";
        public bool Allowed {get;private set;}
        public bool RayEnabled {get;private set;}
        public bool Visible=>line!=null && line.enabled;
        public string TrackingStatus {get;private set;}="WAITING";
        LineRenderer line;
        Material material;
        double lastSample=double.NegativeInfinity;
        bool suspended;
        public static bool IsAllowed(AdaptiveTrainingConfig c)=>c!=null && c.enabled && c.sessionType=="engineering";

        public void Bind(AdaptiveTrainingConfig config)
        {
            Allowed=IsAllowed(config);
            SetEnabled(config.engineeringGazeRay);
        }
        public void SetEnabled(bool value)
        {
            RayEnabled=Allowed && value;
            if(RayEnabled && line==null)
            {
                material=new Material(Resources.Load<Shader>("GazeDebugOutline"));
                material.SetColor("_Color",new Color(1,1,1,.22f));
                var go=new GameObject("Tracked eye soft white line");go.transform.SetParent(transform,false);
                line=go.AddComponent<LineRenderer>();
                line.sharedMaterial=material;line.positionCount=2;line.useWorldSpace=true;
                line.widthMultiplier=.0015f;line.shadowCastingMode=ShadowCastingMode.Off;
                line.receiveShadows=false;line.enabled=false;
                line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;
                Debug.Log("Engineering eye ray: "+Revision+";white;alpha=0.22;width_m=0.0015;start_m=0.65;end_m=3.85;roi="+GazeRegions.Revision);
            }
            Clear();
        }
        public void Present(GazeRegionSample sample,string status,double now,bool phaseAllowed)
        {
            TrackingStatus=status;
            if(!RayEnabled || suspended || !phaseAllowed || !sample.valid){Clear();return;}
            lastSample=now;
            line.SetPosition(0,sample.origin+sample.direction*.65f);
            line.SetPosition(1,sample.origin+sample.direction*3.85f);
            line.enabled=true;
        }
        public void Clear()
        {
            lastSample=double.NegativeInfinity;
            if(line!=null)line.enabled=false;
        }
        void LateUpdate(){if(suspended || Time.realtimeSinceStartupAsDouble-lastSample>.1)Clear();}
        void OnApplicationPause(bool value){suspended=value;if(value)Clear();}
        void OnApplicationFocus(bool value){suspended=!value;if(!value)Clear();}
        void OnDisable()=>Clear();
        void OnDestroy(){Clear();if(material!=null)Destroy(material);}
    }
}

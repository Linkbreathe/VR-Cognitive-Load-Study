using UnityEngine;
using UnityEngine.Rendering;

namespace EvidenceVR
{
    // Displays the tracked-eye ray supplied by the production gaze reader. Display only:
    // it never changes gaze validity, classification or logging.
    // Only a ring marks the tracked-eye hit point; no directional ray or bird is rendered.
    // While enabled the ring persists in every phase; its color states what it shows:
    // white = live tracked eyes, amber = runtime pose rejected as frozen (follows the head),
    // gray = no fresh sample, holding the last drawn point.
    public sealed class EngineeringGazeRay : MonoBehaviour
    {
        public const string Revision="engineering-gaze-reticle-v7-ring-only";
        public const float DefaultHitDistance=2f;
        public const float ReticleRadiusDegrees=.6f;
        public const int ReticleSegments=32;
        public const double FreshSampleSeconds=.1;
        public enum DisplayState {Off,Waiting,Live,Frozen,Held}
        public static readonly Color LiveColor=new Color(1,1,1,.85f),FrozenColor=new Color(1,.62f,.12f,.9f),HeldColor=new Color(.55f,.55f,.55f,.6f);
        public bool Allowed {get;private set;}
        public bool RayEnabled {get;private set;}
        public DisplayState State {get;private set;}=DisplayState.Off;
        // The legacy Eye Ray toggle now controls only this persistent ring.
        public bool Visible=>reticle!=null && reticle.enabled;
        public bool LineVisible=>false;
        public bool ReticleVisible=>Visible;
        public string TrackingStatus {get;private set;}="WAITING";
        LineRenderer reticle;
        Material reticleMaterial;
        double lastSample=double.NegativeInfinity;
        bool applicationPaused,applicationFocused=true,hasPoint;
        public static bool FocusAllowsRay(bool editor,bool focused,bool paused)=>!paused && (editor || focused);
        bool suspended=>!FocusAllowsRay(Application.isEditor,applicationFocused,applicationPaused);
        public static bool IsAllowed(AdaptiveTrainingConfig c)=>c!=null && c.enabled && c.sessionType=="engineering";

        public void Bind(AdaptiveTrainingConfig config)
        {
            Allowed=IsAllowed(config);
            SetEnabled(config.engineeringGazeRay);
        }
        public void SetEnabled(bool value)
        {
            RayEnabled=Allowed && value;
            if(RayEnabled && reticle==null)
            {
                var shader=Resources.Load<Shader>("GazeDebugOutline");
                // The reticle sits on surfaces; skip depth testing so it never z-fights or hides behind the hit surface.
                reticleMaterial=new Material(shader);
                reticleMaterial.SetColor("_Color",LiveColor);
                reticleMaterial.SetFloat("_ZTest",(float)CompareFunction.Always);
                reticle=CreateLine("Tracked eye hit reticle",ReticleSegments,.002f,reticleMaterial,true);
                Debug.Log("Engineering eye ray: "+Revision+";direction_line=false;bird=false;reticle_radius_deg="+ReticleRadiusDegrees+";states=live_white,frozen_amber,held_gray;persistent=all_phases;roi="+GazeRegions.Revision);
            }
            if(!RayEnabled){Clear();return;}
            hasPoint=false;lastSample=double.NegativeInfinity;
            reticle.enabled=false;State=DisplayState.Waiting;
        }
        LineRenderer CreateLine(string name,int count,float width,Material shared,bool loop)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var result=go.AddComponent<LineRenderer>();
            result.sharedMaterial=shared;result.positionCount=count;result.useWorldSpace=true;result.loop=loop;
            result.widthMultiplier=width;result.shadowCastingMode=ShadowCastingMode.Off;
            result.receiveShadows=false;result.enabled=false;
            result.lightProbeUsage=LightProbeUsage.Off;result.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return result;
        }
        // frozen: the sample is the runtime's raw pose that the reader rejected as frozen.
        public void Present(GazeRegionSample sample,string status,double now,bool phaseAllowed,float hitDistance=DefaultHitDistance,bool frozen=false)
        {
            TrackingStatus=status;
            if(!RayEnabled)return;
            if(suspended || !phaseAllowed || !sample.valid){Hold();return;}
            lastSample=now;hasPoint=true;
            if(!GazeRegions.Finite(hitDistance) || hitDistance<=0)hitDistance=DefaultHitDistance;
            Vector3 center=sample.origin+sample.direction*hitDistance;
            Vector3 right=Vector3.Cross(sample.direction,Mathf.Abs(sample.direction.y)>.95f?Vector3.right:Vector3.up).normalized;
            Vector3 up=Vector3.Cross(right,sample.direction);
            // Constant angular size: about 1.2 degrees across at any distance.
            float radius=hitDistance*Mathf.Tan(ReticleRadiusDegrees*Mathf.Deg2Rad);
            reticle.widthMultiplier=Mathf.Max(.0015f,radius*.3f);
            for(int i=0;i<ReticleSegments;i++)
            {float angle=2*Mathf.PI*i/ReticleSegments;reticle.SetPosition(i,center+radius*(Mathf.Cos(angle)*right+Mathf.Sin(angle)*up));}
            SetState(frozen?DisplayState.Frozen:DisplayState.Live);
        }
        // Keep the last drawn point on screen, marked gray; nothing is drawn before the first sample.
        public void Hold()
        {
            if(!RayEnabled || reticle==null)return;
            if(!hasPoint){reticle.enabled=false;State=DisplayState.Waiting;return;}
            SetState(DisplayState.Held);
        }
        void SetState(DisplayState state)
        {
            if(State==state && reticle.enabled)return;
            State=state;
            reticleMaterial.SetColor("_Color",state==DisplayState.Live?LiveColor:state==DisplayState.Frozen?FrozenColor:HeldColor);
            reticle.enabled=true;
        }
        // Hides everything; used only when the switch is turned off or the component goes away.
        public void Clear()
        {
            lastSample=double.NegativeInfinity;hasPoint=false;
            if(reticle!=null)reticle.enabled=false;
            State=RayEnabled?DisplayState.Waiting:DisplayState.Off;
        }
        void LateUpdate()
        {
            if(!RayEnabled || reticle==null)return;
            if((State==DisplayState.Live || State==DisplayState.Frozen) && (suspended || Time.realtimeSinceStartupAsDouble-lastSample>FreshSampleSeconds))Hold();
        }
        void OnApplicationPause(bool value){applicationPaused=value;if(value)Hold();}
        // Editor window focus is not headset focus. Require a fresh valid eye sample after loss.
        void OnApplicationFocus(bool value){applicationFocused=value;if(!value)Hold();}
        void OnDisable()=>Clear();
        void OnDestroy(){Clear();if(reticleMaterial!=null)Destroy(reticleMaterial);}
    }
}

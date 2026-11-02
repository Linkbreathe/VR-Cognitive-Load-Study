using UnityEngine;

namespace EvidenceVR
{
    public struct GazeRegionSample
    {
        public bool valid, task, video;
        public Vector3 origin, direction;
        public string Target=>!valid?"invalid":task?"task":video?"video":"other";
    }

    public static class GazeRegions
    {
        public const string Revision="gaze-roi-v2-visible-targets";
        public static bool Finite(float x)=>!float.IsNaN(x) && !float.IsInfinity(x);
        public static bool Finite(Vector3 v)=>Finite(v.x) && Finite(v.y) && Finite(v.z);
        public static bool ValidRotation(Quaternion q)=>Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w) && Quaternion.Dot(q,q)>1e-8f;
        public static GazeRegionSample Classify(bool tracked,Vector3 origin,Vector3 direction,
            Vector3 taskCenter,bool taskVisible,Transform screen,bool videoVisible)
        {
            var result=new GazeRegionSample();
            if(!tracked || !Finite(origin) || !Finite(direction) || direction.sqrMagnitude<1e-8f)return result;
            result.valid=true;result.origin=origin;result.direction=direction.normalized;
            result.task=taskVisible && Finite(taskCenter) && (taskCenter-origin).sqrMagnitude>1e-8f &&
                Vector3.Angle(result.direction,taskCenter-origin)<=3;
            if(!result.task && videoVisible && screen!=null)
            {
                var plane=new Plane(screen.forward,screen.position);var ray=new Ray(origin,result.direction);
                if(plane.Raycast(ray,out float distance) && distance>0)
                {var hit=screen.InverseTransformPoint(ray.GetPoint(distance));result.video=Mathf.Abs(hit.x)<=.5f && Mathf.Abs(hit.y)<=.5f;}
            }
            return result;
        }
    }
}

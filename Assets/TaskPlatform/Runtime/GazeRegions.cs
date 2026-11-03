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
        public const string Revision="gaze-roi-v4-sphere-tray-area";
        public const float TaskPaddingMeters=.04f;
        public static Bounds TaskBounds(Bounds sphere,Bounds tray)
        {
            sphere.Encapsulate(tray);
            sphere.Expand(2*TaskPaddingMeters);
            return sphere;
        }
        public static bool Finite(float x)=>!float.IsNaN(x) && !float.IsInfinity(x);
        public static bool Finite(Vector3 v)=>Finite(v.x) && Finite(v.y) && Finite(v.z);
        public static bool ValidRotation(Quaternion q)=>Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w) && Quaternion.Dot(q,q)>1e-8f;
        public static GazeRegionSample Classify(bool tracked,Vector3 origin,Vector3 direction,
            Bounds taskBounds,bool taskVisible,Transform screen,bool videoVisible)
        {
            var result=new GazeRegionSample();
            if(!tracked || !Finite(origin) || !Finite(direction) || direction.sqrMagnitude<1e-8f)return result;
            result.valid=true;result.origin=origin;result.direction=direction.normalized;
            var eyeRay=new Ray(origin,result.direction);
            result.task=taskVisible && Finite(taskBounds.center) && Finite(taskBounds.size) &&
                taskBounds.size.x>0 && taskBounds.size.y>0 && taskBounds.size.z>0 &&
                !taskBounds.Contains(origin) && taskBounds.IntersectRay(eyeRay,out float taskDistance) && taskDistance>0;
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

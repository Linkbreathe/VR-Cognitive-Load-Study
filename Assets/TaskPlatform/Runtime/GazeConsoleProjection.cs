using System;
using System.Collections.Generic;
using UnityEngine;

namespace EvidenceVR
{
    [Serializable] public sealed class ConsoleGazePoint
    {
        public long id;
        public double time;
        public bool valid, projected, taskVisible, movieVisible;
        public float x, y;
        public string target;
    }

    [Serializable] public sealed class ConsoleGazeRect
    {
        public bool available;
        public float x, y, width, height;
    }

    [Serializable] public sealed class ConsoleGazeLayout
    {
        public bool available;
        public float minX, maxX, minY, maxY;
        public ConsoleGazeRect movie, sphere, tray, task;
    }

    [Serializable] public sealed class ConsoleGazeOverview
    {
        public string revision=GazeConsoleProjection.Revision, source="tracked_eyes", status, phase;
        public string projection="front_wall_plane_meters", layoutViewpoint="head_position_for_geometry_only";
        public double now;
        public int frame;
        public ConsoleGazeLayout layout;
        public ConsoleGazePoint[] samples;
    }

    // Diagnostic projection only. Never supplies gaze classification or authorization.
    public static class GazeConsoleProjection
    {
        public const string Revision="gaze-console-front-plane-v1";

        public static bool Project(Vector3 origin, Vector3 direction, Transform screen, out Vector2 point)
        {
            point=Vector2.zero;
            if(screen==null || !GazeRegions.Finite(origin) || !GazeRegions.Finite(direction) || direction.sqrMagnitude<1e-8f)
                return false;
            var ray=new Ray(origin,direction.normalized);
            float denominator=Vector3.Dot(screen.forward,ray.direction);
            if(Mathf.Abs(denominator)<.001f)return false;
            var plane=new Plane(screen.forward,screen.position);
            if(!plane.Raycast(ray,out float distance) || distance<=0 || distance>50)return false;
            var delta=ray.GetPoint(distance)-screen.position;
            point=new Vector2(Vector3.Dot(delta,screen.right),Vector3.Dot(delta,screen.up));
            return GazeRegions.Finite(point.x) && GazeRegions.Finite(point.y);
        }

        public static ConsoleGazePoint Sample(long id,double time,GazeRegionSample sample,Transform screen,bool taskVisible,bool movieVisible)
        {
            var result=new ConsoleGazePoint{id=id,time=time,valid=sample.valid,taskVisible=taskVisible,
                movieVisible=movieVisible,target=sample.Target=="video"?"movie":sample.Target};
            // Invalid eyes never become a center point or a head-direction substitute.
            if(sample.valid && Project(sample.origin,sample.direction,screen,out var point))
            {result.projected=true;result.x=point.x;result.y=point.y;}
            return result;
        }

        public static ConsoleGazeRect ProjectBounds(Bounds bounds,Vector3 viewpoint,Transform screen)
        {
            var result=new ConsoleGazeRect();
            if(!GazeRegions.Finite(bounds.center) || !GazeRegions.Finite(bounds.size))return result;
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
            for(int corner=0;corner<8;corner++)
            {
                var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                if(!Project(viewpoint,p-viewpoint,screen,out var projected))return result;
                min=Vector2.Min(min,projected);max=Vector2.Max(max,projected);
            }
            result.available=true;result.x=min.x;result.y=min.y;result.width=max.x-min.x;result.height=max.y-min.y;
            return result;
        }

        public static ConsoleGazeLayout Layout(Transform screen,Vector3 viewpoint,Bounds sphere,Bounds tray,Bounds task)
        {
            var result=new ConsoleGazeLayout();
            if(screen==null || !GazeRegions.Finite(viewpoint))return result;
            float width=screen.TransformVector(Vector3.right).magnitude,height=screen.TransformVector(Vector3.up).magnitude;
            if(width<=0 || height<=0)return result;
            result.available=true;
            result.movie=new ConsoleGazeRect{available=true,x=-width/2,y=-height/2,width=width,height=height};
            result.sphere=ProjectBounds(sphere,viewpoint,screen);result.tray=ProjectBounds(tray,viewpoint,screen);
            result.task=ProjectBounds(task,viewpoint,screen);
            result.minX=-width*.65f;result.maxX=width*.65f;result.minY=-height*.65f;result.maxY=height*.65f;
            foreach(var box in new[]{result.sphere,result.tray,result.task})
                if(box.available)
                {result.minX=Mathf.Min(result.minX,box.x-.15f);result.maxX=Mathf.Max(result.maxX,box.x+box.width+.15f);
                 result.minY=Mathf.Min(result.minY,box.y-.2f);result.maxY=Mathf.Max(result.maxY,box.y+box.height+.2f);}
            return result;
        }
    }

    public sealed partial class AdaptiveRuntime
    {
        readonly Queue<ConsoleGazePoint> consoleGaze=new Queue<ConsoleGazePoint>();
        ConsoleGazeLayout consoleGazeLayout;
        string consoleGazePhase;
        int consoleGazeFrame;
        long consoleGazeId;
        bool consoleGazeHasFrame;
        Matrix4x4 consoleGazeScreenMatrix;
        Bounds consoleGazeTaskBounds;

        void RecordConsoleGaze(double now,GazeRegionSample sample,Bounds sphere,Bounds tray,Bounds task,bool taskVisible,bool movieVisible)
        {
            var screen=Media?.Screen;
            string phase=engine.Phase.ToString();
            var matrix=screen!=null?screen.localToWorldMatrix:Matrix4x4.identity;
            if(!consoleGazeHasFrame || phase!=consoleGazePhase || matrix!=consoleGazeScreenMatrix || task!=consoleGazeTaskBounds)
            {
                consoleGaze.Clear();consoleGazeFrame++;consoleGazeHasFrame=true;
                consoleGazePhase=phase;consoleGazeScreenMatrix=matrix;consoleGazeTaskBounds=task;
            }
            consoleGaze.Enqueue(GazeConsoleProjection.Sample(++consoleGazeId,now,sample,screen,taskVisible,movieVisible));
            while(consoleGaze.Count>360 || (consoleGaze.Count>0 && consoleGaze.Peek().time<now-10))consoleGaze.Dequeue();
            // The head position defines only the schematic object's outlines, never a gaze point.
            consoleGazeLayout=GazeConsoleProjection.Layout(screen,owner.head.position,sphere,tray,task);
        }

        ConsoleGazeOverview ConsoleGazeSnapshot(double now)
        {
            var rows=new List<ConsoleGazePoint>();
            foreach(var point in consoleGaze)if(point.time>=now-10 && point.time<=now)rows.Add(point);
            return new ConsoleGazeOverview{now=now,status=GazeTrackingStatus,phase=engine.Phase.ToString(),frame=consoleGazeFrame,
                layout=consoleGazeLayout,samples=rows.ToArray()};
        }
    }
}

using System;
using System.IO;
using EvidenceVR;
using UnityEngine;

public static class PersonalizedPreview
{
    static TaskPlatformController Owner => UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
    public static string Inspect()
    {
        var o=Owner;
        return JsonUtility.ToJson(new Snapshot {stage=o.Engine.Adaptive.Stage,phase=o.Engine.Phase.ToString(),
            data=o.DataPath,mediaReady=o.AdaptiveRuntime.Media.Ready,visibleMovie=o.AdaptiveRuntime.Media.IsVideoVisible,
            frame=Time.frameCount,heading=o.heading.text,body=o.body.text,badge=o.badge.text});
    }
    [Serializable] class Snapshot {public string stage,phase,data,heading,body,badge;public bool mediaReady,visibleMovie;public int frame;}
    public static string BeginPractice()
    {
        var o=Owner;double now=Time.realtimeSinceStartupAsDouble;
        o.Engine.Note("engineering_preview","direct engine UI preview; preparation input checks bypassed; not participant acceptance",now,Time.frameCount);
        o.Engine.Advance(now,Time.frameCount);o.Engine.Advance(now,Time.frameCount);
        return Inspect();
    }
    public static string Advance(){Owner.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);return Inspect();}
    public static string PrepareCalibration()
    {
        BeginPractice();Advance();
        return "Engineering preview: practice skip requested at the next trial boundary. "+Inspect();
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEditor;
using UnityEngine;

public static class WarmStudyPlaybackPreview
{
    public static async Task<object> Show()
    {
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(!Application.isPlaying || owner?.Engine?.Phase!=TaskPhase.Preparation || owner.Engine.Adaptive?.Config.sessionType!="engineering")
            throw new InvalidOperationException("A fresh engineering Play Mode session is required.");
        var view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        view.maximized=true;view.Focus();
        await Task.Delay(500);
        owner.Engine.Note("engineering_preview","User-requested warm study task/movie preview; direct practice start bypasses preparation inputs; normal timing and media schedule retained; no participant evidence",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        owner.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        owner.Engine.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        typeof(TaskPlatformController).GetField("lastTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,Time.realtimeSinceStartupAsDouble);
        int initialFrame=Time.frameCount;
        string folder=Path.GetFullPath("Evidence/LabEnvironment/PlaybackPreview");Directory.CreateDirectory(folder);
        await Task.Delay(650);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"task.png"));
        double deadline=Time.realtimeSinceStartupAsDouble+20;
        while(Application.isPlaying && owner!=null && !owner.AdaptiveRuntime.Media.IsVideoVisible && Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(owner.Engine.Phase!=TaskPhase.Running)throw new InvalidOperationException("Preview stopped or paused before the scheduled movie: "+owner.Engine.Phase);
            await Task.Delay(100);
        }
        if(owner==null || !owner.AdaptiveRuntime.Media.IsVideoVisible)throw new InvalidOperationException("No decoded movie frame within the first practice exposure window.");
        await Task.Delay(800);
        string media=owner.AdaptiveRuntime.Media.ActiveId;
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"task-with-movie.png"));
        await Task.Delay(300);
        string message="Warm study live playback: phase="+owner.Engine.Phase+"; frames="+initialFrame+"->"+Time.frameCount+"; visible movie="+media+"; normal practice timing; editor-test="+owner.DataPath;
        Debug.Log(message);
        return new {message,initialFrame,finalFrame=Time.frameCount,movieVisible=owner.AdaptiveRuntime.Media.IsVideoVisible,media,owner.DataPath,captures=folder};
    }
}

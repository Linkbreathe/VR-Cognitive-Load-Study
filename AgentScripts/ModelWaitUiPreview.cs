using System;
using System.IO;
using System.Reflection;
using EvidenceVR;
using UnityEditor;
using UnityEngine;

public static class ModelWaitUiPreview
{
    public static string Show()
    {
        var owner = UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if (!Application.isPlaying || owner?.Engine?.Adaptive == null ||
            owner.Engine.Adaptive.Config.sessionType != "engineering" ||
            owner.Engine.Phase != TaskPhase.Preparation)
            throw new InvalidOperationException("A fresh personalized engineering Play Mode session is required.");

        // Stop task updates and outstanding transport before staging this UI-only preview.
        // Stopping Play Mode restores normal component state; no scene/configuration is saved.
        owner.enabled = false;
        owner.AdaptiveRuntime.StopAllCoroutines();
        double now = Time.realtimeSinceStartupAsDouble;
        owner.Engine.Note("engineering_preview",
            "model_wait_ui_only; no calibration samples; no model training; task/transport suspended; stop Play Mode to restore",
            now, Time.frameCount);
        typeof(TaskEngine).GetMethod("PersonalStage", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(owner.Engine, new object[] { "model_wait", 0, now, Time.frameCount, TaskPhase.Rest });
        typeof(TaskPlatformController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(owner, null);
        owner.AdaptiveRuntime.Render();
        owner.StatusView?.Refresh();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();

        string result = "MODEL WAIT UI PREVIEW: engineering only; no actual model training.\n" +
            "Stage: " + owner.Engine.Adaptive.Stage + "\nHeading: " + owner.heading.text +
            "\nBody: " + owner.body.text + "\nHint: " + owner.presentation.actionHint.text +
            "\nSession: " + owner.DataPath + "\nRestore: stop Play Mode. No scene or configuration saved.";
        Directory.CreateDirectory("Evidence/ModelWaitPreview");
        File.WriteAllText("Evidence/ModelWaitPreview/preview.txt", result);
        Debug.Log(result);
        return result;
    }
}

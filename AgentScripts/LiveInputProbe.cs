using System;
using System.IO;
using EvidenceVR;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class LiveInputProbe
{
    public static string Run()
    {
        var controller = UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        double start = EditorApplication.timeSinceStartup;
        int stage = 0;
        int firstFrame = Time.frameCount;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            double elapsed = EditorApplication.timeSinceStartup-start;
            if (elapsed < stage*.5) return;
            Key? key = stage == 0 ? Key.C : stage == 2 ? Key.A : stage == 4 ? Key.B : stage == 6 ? Key.Enter : stage == 8 ? Key.Enter :
                stage == 23 ? Key.A : stage == 25 ? Key.B : stage == 28 ? Key.P : stage == 32 ? Key.P : (Key?)null;
            InputSystem.QueueStateEvent(Keyboard.current, key.HasValue ? new KeyboardState(key.Value) : new KeyboardState());
            stage++;
            if (stage < 80) return;
            EditorApplication.update -= tick;
            int scored = controller.Engine.Results.FindAll(t => t.scored).Count;
            File.WriteAllText("Evidence/live-playmode.txt", "InputSystem queued keyboard-state integration probe (not physical keyboard/controller validation)\nFrames advanced: " + (Time.frameCount-firstFrame) + "\nPhase: " + controller.Engine.Phase + "\nCompleted rows: " + controller.Engine.Results.Count + "\nScored rows: " + scored + "\nData: " + controller.DataPath + "\n");
            Debug.Log("Live input probe finished; Evidence/live-playmode.txt");
        };
        EditorApplication.update += tick;
        return "Started 40-second live InputSystem probe";
    }
}

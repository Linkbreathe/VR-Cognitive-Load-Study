using System;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

// Read-only configuration audit and isolated mathematical counterexamples.
// No Play Mode, input injection, scene mutation or participant recording.
public static class EyeConflictAudit
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run this audit in Edit Mode.");
        var configurations = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android }.Select(group =>
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            var eye = settings?.GetFeature<EyeGazeInteraction>();
            var compatibility = settings?.GetFeature<QuestLinkEyeSpaceFeature>();
            return new { target = group.ToString(), eyeEnabled = eye != null && eye.enabled,
                compatibilityEnabled = compatibility != null && compatibility.enabled,
                retry = compatibility != null && compatibility.retryWithNullPath,
                features = settings?.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>()
                    .Where(f => f != null && f.enabled).Select(f => f.GetType().Name).ToArray() };
        }).ToArray();
        var controller = UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var head = controller?.head;
        var driver = head?.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        var angular = new EyeFreezeDetector();
        var freshness = new EyeSampleFreshness();
        bool fresh = false, frozen = false;
        for (int i = 0; i <= 60; i++)
        {
            double t = i / 30.0;
            // Bounded tiny movement, with independently advancing timestamps.
            var direction = Quaternion.Euler(0, .02f * Mathf.Sin(i), 0) * Vector3.forward;
            frozen = angular.Update(t, true, direction);
            fresh = freshness.Update(t, true, 1000000 + i * 33333);
        }
        var tinyMotion = new { fresh, frozen, spreadDegrees = angular.LastSpreadDegrees };
        bool afterInvalid = angular.Update(2.01, false, Vector3.zero);
        bool immediatelyAfterReturn = angular.Update(2.02, true, Vector3.forward);
        var restarts = new { afterInvalid, immediatelyAfterReturn };
        var report = new {
            revision = "eye-conflict-audit-v1", checkedUtc = DateTime.UtcNow.ToString("o"),
            unityVersion = Application.unityVersion, playMode = Application.isPlaying,
            configurations, preview = EditorUserSettings.GetConfigValue("EvidenceVR.QuestLinkPreview"),
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            head = head == null ? null : new { name = head.name, parent = head.parent?.name,
                parentScale = head.parent?.lossyScale.ToString("F6"),
                drivers = head.GetComponents<UnityEngine.InputSystem.XR.TrackedPoseDriver>().Length,
                driverEnabled = driver != null && driver.enabled, updateType = driver?.updateType.ToString(),
                positionBinding = driver?.positionInput.action?.bindings.Select(b => b.effectivePath).ToArray(),
                rotationBinding = driver?.rotationInput.action?.bindings.Select(b => b.effectivePath).ToArray() },
            tinyMotion, restarts,
            interpretation = "Counterexamples establish software behavior only; they do not measure real fixation, false-positive rates, or device recovery."
        };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory("Evidence/EyeConflictAudit-20261110");
        File.WriteAllText("Evidence/EyeConflictAudit-20261110/editor-audit.json", json);
        return json;
    }
}

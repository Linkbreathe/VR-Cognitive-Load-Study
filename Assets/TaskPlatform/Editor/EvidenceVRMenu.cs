using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EvidenceVR.Editor
{
    // Menu-only facade. Existing tools remain callable by their original methods.
    public static class EvidenceVRMenu
    {
        const string Root = "Evidence VR/";
        const string Advanced = Root + "Advanced/";
        const string Highlight = Advanced + "Eye Tracking Debug/Target Highlight";
        const string EyeRay = Advanced + "Eye Tracking Debug/Eye Ray";
        const string Oculus = Advanced + "Quest Link Settings/Use Oculus for Editor Preview";
        const string SystemDefault = Advanced + "Quest Link Settings/Use System Default for Editor Preview";

        static string ConfigurationPath => Path.Combine(Application.persistentDataPath, "adaptive-training-config.json");

        static AdaptiveTrainingConfig ReadConfiguration() => File.Exists(ConfigurationPath)
            ? JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(ConfigurationPath))
            : null;

        internal static string WebConsoleUrl
        {
            get
            {
                var config = ReadConfiguration();
                string address = config?.controllerUrl ?? new AdaptiveTrainingConfig().controllerUrl;
                if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    throw new InvalidOperationException("The configured controller URL must use HTTP or HTTPS.");
                return address.TrimEnd('/') + "/tests";
            }
        }

        [MenuItem(Root + "Open Web Console", false, 0)]
        public static void OpenWebConsole() => Application.OpenURL(WebConsoleUrl);

        [MenuItem(Root + "Signal Sources", false, 1)]
        public static void SignalSources() => LslSourceWindow.Open();

        [MenuItem(Advanced + "Use Web Console Workflow", false, 20)]
        public static void UseWebConsoleWorkflow() => Debug.Log(OperatorTestSetup.Enable());

        [MenuItem(Advanced + "Use Web Console Workflow", true)]
        static bool CanChangeWorkflow() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Oculus, false, 40)]
        public static void UseOculus() => Debug.Log(QuestLinkPreviewSetup.Enable());

        [MenuItem(SystemDefault, false, 41)]
        public static void UseSystemDefault() => Debug.Log(QuestLinkPreviewSetup.Disable());

        [MenuItem(Oculus, true)]
        static bool CanUseOculus() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(SystemDefault, true)]
        static bool CanUseSystemDefault() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Highlight, false, 60)]
        public static void ToggleTargetHighlight()
        {
            bool enabled = ReadConfiguration()?.engineeringGazeHighlight == true;
            Debug.Log(enabled ? AdaptiveTrainingSetup.DisableGazeHighlight() : AdaptiveTrainingSetup.EnableGazeHighlight());
        }

        [MenuItem(Highlight, true)]
        static bool CanToggleTargetHighlight()
        {
            try
            {
                var config = ReadConfiguration();
                Menu.SetChecked(Highlight, config?.engineeringGazeHighlight == true);
                return !EditorApplication.isPlayingOrWillChangePlaymode && EngineeringGazeRay.IsAllowed(config);
            }
            catch { Menu.SetChecked(Highlight, false); return false; }
        }

        static EngineeringGazeRay LiveEyeRay() => UnityEngine.Object.FindFirstObjectByType<AdaptiveRuntime>()?.GazeRay;

        [MenuItem(EyeRay, false, 61)]
        public static void ToggleEyeRay()
        {
            bool enabled = EditorApplication.isPlaying
                ? LiveEyeRay()?.RayEnabled == true
                : ReadConfiguration()?.engineeringGazeRay == true;
            Debug.Log(enabled ? AdaptiveTrainingSetup.DisableEyeRay() : AdaptiveTrainingSetup.EnableEyeRay());
        }

        [MenuItem(EyeRay, true)]
        static bool CanToggleEyeRay()
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    var ray = LiveEyeRay();
                    Menu.SetChecked(EyeRay, ray?.RayEnabled == true);
                    return ray != null && ray.Allowed;
                }
                var config = ReadConfiguration();
                Menu.SetChecked(EyeRay, config?.engineeringGazeRay == true);
                return !EditorApplication.isPlayingOrWillChangePlaymode && EngineeringGazeRay.IsAllowed(config);
            }
            catch { Menu.SetChecked(EyeRay, false); return false; }
        }

        [MenuItem(Advanced + "Validate Setup", false, 80)]
        public static void ValidateSetup() => Debug.Log(TaskPlatformValidation.Run());

        [MenuItem(Advanced + "Validate Setup", true)]
        static bool CanValidateSetup() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Advanced + "Build Quest App", false, 100)]
        public static void BuildQuestApp() => Debug.Log(AdaptiveTrainingSetup.Build());

        [MenuItem(Advanced + "Build Quest App", true)]
        static bool CanBuildQuestApp() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

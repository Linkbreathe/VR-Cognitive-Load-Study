using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace EvidenceVR.Editor
{
    // Project-local opt-in. The Windows default OpenXR runtime is never changed.
    [InitializeOnLoad]
    public static class QuestLinkPreviewSetup
    {
        const string Preference="EvidenceVR.QuestLinkPreview";
        static QuestLinkPreviewSetup()
        {
            EditorApplication.delayCall+=Restore;
        }
        static void Restore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorUserSettings.GetConfigValue(Preference)!="enabled")return;
            try {SelectOculus();}
            catch(Exception e){Debug.LogWarning("Quest Link preview selection failed: "+e.Message+" Select Oculus OpenXR in Project Settings > XR Plug-in Management > OpenXR > Play Mode OpenXR Runtime.");}
        }
        [MenuItem("Evidence VR/Stage 2/Quest Link/Use Oculus for Editor Preview")]
        public static string Enable()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before selecting an OpenXR runtime.");
            string selected=SelectOculus();
            EditorUserSettings.SetConfigValue(Preference,"enabled");
            return "Oculus OpenXR selected for this project's Editor previews, including after restart: "+selected;
        }
        [MenuItem("Evidence VR/Stage 2/Quest Link/Use System Default for Editor Preview")]
        public static string Disable()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before selecting an OpenXR runtime.");
            var selector=Selector();
            selector.GetMethod("SetSelectedRuntime",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{""});
            EditorUserSettings.SetConfigValue(Preference,"disabled");
            return "Editor preview uses the system default runtime. Windows settings are unchanged.";
        }
        static Type Selector()=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.XR.OpenXR.OpenXRRuntimeSelector")).FirstOrDefault(t=>t!=null)
            ?? throw new InvalidOperationException("Installed OpenXR Editor runtime selector is unavailable.");
        static string SelectOculus()
        {
            // Reading RuntimeDetectors initializes Unity's own Play Mode activation callback.
            var selector=Selector();
            var detectors=(IEnumerable)selector.GetProperty("RuntimeDetectors",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            foreach(var detector in detectors)
            {
                var type=detector.GetType();
                if((string)type.GetProperty("name").GetValue(detector)!="Oculus OpenXR" || !(bool)type.GetProperty("detected").GetValue(detector))continue;
                string path=(string)type.GetProperty("jsonPath").GetValue(detector);
                if(!File.Exists(path))throw new FileNotFoundException("Detected Oculus runtime JSON is missing.",path);
                selector.GetMethod("SetSelectedRuntime",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{path});
                return path;
            }
            throw new InvalidOperationException("Meta Quest Link's Oculus OpenXR runtime was not detected.");
        }
    }
}

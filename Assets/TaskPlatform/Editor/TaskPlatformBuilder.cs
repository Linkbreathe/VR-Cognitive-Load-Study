using System;
using System.IO;
using System.Reflection;
using EvidenceVR;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace EvidenceVR.Editor
{
    public static class TaskPlatformBuilder
    {
        public const string ScenePath = "Assets/TaskPlatform/Scenes/NBackExperiment.unity";
        [MenuItem("Evidence VR/Build Experiment Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building the scene.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before creating the experiment scene.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Experiment scene already exists. Open it instead of overwriting it.");
            Directory.CreateDirectory("Assets/TaskPlatform/Scenes"); Directory.CreateDirectory("Assets/TaskPlatform/Materials"); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.7f,.7f,.7f);
            RenderSettings.fog = false;
            var room = new GameObject("NeutralRoom").transform;
            var wall = Material("RoomGrey", "Universal Render Pipeline/Lit", new Color(.43f,.43f,.43f));
            var floor = Material("FloorGrey", "Universal Render Pipeline/Lit", new Color(.31f,.31f,.31f));
            Box("Floor", room, new Vector3(0,-.06f,0), new Vector3(6,.12f,6), floor);
            Box("Ceiling", room, new Vector3(0,3,0), new Vector3(6,.12f,6), wall);
            Box("FrontWall", room, new Vector3(0,1.5f,3), new Vector3(6,3,.12f), wall);
            Box("BackWall", room, new Vector3(0,1.5f,-3), new Vector3(6,3,.12f), wall);
            Box("LeftWall", room, new Vector3(-3,1.5f,0), new Vector3(.12f,3,6), wall);
            Box("RightWall", room, new Vector3(3,1.5f,0), new Vector3(.12f,3,6), wall);
            var light = new GameObject("StableSoftLight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = .65f; light.shadows = LightShadows.None; light.transform.rotation = Quaternion.Euler(50,-25,0);
            var rig = new GameObject("SeatedTrackingSpace").transform; rig.position = new Vector3(0,1.2f,0);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.transform.SetParent(rig, false);
            camera.nearClipPlane = .05f; camera.farClipPlane = 20; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.43f,.43f,.43f);
            camera.fieldOfView = 60; camera.allowHDR = false; camera.allowMSAA = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var driver = camera.gameObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition"));
            driver.rotationInput = new InputActionProperty(new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("HeadTracking", InputActionType.Value, "<XRHMD>/trackingState"));
            var anchor = new GameObject("WorldLockedTaskArea").transform; anchor.position = new Vector3(0,1.2f,1.5f);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "ColorStimulus"; sphere.transform.SetParent(anchor,false); sphere.transform.localScale = Vector3.one * (3 * Mathf.Tan(3 * Mathf.Deg2Rad));
            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.GetComponent<Renderer>().sharedMaterial = Material("StimulusUnlit", "Universal Render Pipeline/Unlit", new Color(.10f,.36f,.95f));
            sphere.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; sphere.GetComponent<Renderer>().receiveShadows = false; sphere.GetComponent<Renderer>().enabled = false;
            var canvasObject = new GameObject("TaskInstructions", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(anchor, false); canvasObject.transform.localPosition = new Vector3(0,0,.015f); canvasObject.transform.localScale = Vector3.one * .001f;
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1100,820);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("Import TMP Essentials before building scene.");
            var badge = Text("PlatformLabel", canvasObject.transform, font, new Vector2(0,360), new Vector2(1080,45), 18);
            var heading = Text("RuleHeading", canvasObject.transform, font, new Vector2(0,275), new Vector2(1080,75), 36);
            var body = Text("Instructions", canvasObject.transform, font, new Vector2(0,0), new Vector2(1050,400), 29);
            var footer = Text("ResponseLegend", canvasObject.transform, font, new Vector2(0,-285), new Vector2(1080,95), 28);
            var controller = new GameObject("ExperimentController").AddComponent<TaskPlatformController>();
            controller.defaultConfig = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/TaskPlatform/Config/default-task-config.json");
            controller.taskAnchor = anchor; controller.head = camera.transform; controller.stimulus = sphere.GetComponent<Renderer>();
            controller.heading = heading; controller.body = body; controller.footer = footer; controller.badge = badge;
            badge.text = "EVIDENCE-AWARE VR  /  TASK PLATFORM 01"; heading.text = "N-BACK  /  PREPARATION";
            body.text = "Sit comfortably and look straight ahead.\n\nLeft trigger / C: calibrate task position\nA: match    B: non-match\n\nX / Enter: continue"; footer.text = "Seated task  /  No movement required";
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "EvidenceVR"; PlayerSettings.productName = "Evidence-Aware VR Task Platform"; PlayerSettings.bundleVersion = "1.0.0-stage1";
            PlayerSettings.runInBackground = true;
            ConfigureInput(); AssetDatabase.SaveAssets(); Selection.activeGameObject = controller.gameObject;
            Debug.Log("Created " + ScenePath);
        }
        static Material Material(string name, string shader, Color color)
        {
            var m = new Material(Shader.Find(shader)) { name = name, color = color };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",0);
            AssetDatabase.CreateAsset(m, "Assets/TaskPlatform/Materials/" + name + ".mat"); return m;
        }
        static void Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent,false); box.transform.position = position; box.transform.localScale = scale; box.GetComponent<Renderer>().sharedMaterial = material;
        }
        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent,false);
            var text = go.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.color = new Color(.95f,.95f,.95f); text.raycastTarget = false;
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            return text;
        }
        [MenuItem("Evidence VR/Configure Quest Android")]
        public static void ConfigureQuest()
        {
            var getOrCreate = typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate", BindingFlags.NonPublic | BindingFlags.Static);
            var settings = (XRGeneralSettingsPerBuildTarget)getOrCreate.Invoke(null,null);
            if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general = settings.SettingsForBuildTarget(BuildTargetGroup.Android); general.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android)) throw new Exception("Could not assign OpenXR loader");
            // The package settings owner is internal; use its factory, then the public settings API.
            var packageType = typeof(UnityEditor.XR.OpenXR.Features.OpenXRFeatureSetManager).Assembly.GetType("UnityEditor.XR.OpenXR.OpenXRPackageSettings");
            var owner = packageType.GetMethod("GetOrCreateInstance", BindingFlags.Public | BindingFlags.Static).Invoke(null,null);
            var xr = (OpenXRSettings)packageType.GetMethod("GetSettingsForBuildTargetGroup").Invoke(owner, new object[] { BuildTargetGroup.Android });
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            xr.GetFeature<MetaQuestFeature>().enabled = true;
            xr.GetFeature<OculusTouchControllerProfile>().enabled = true;
            xr.GetFeature<MetaQuestTouchProControllerProfile>().enabled = true;
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(xr); EditorUtility.SetDirty(general); EditorUtility.SetDirty(settings);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.evidencevr.taskplatform");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            mobile.supportsHDR = false; mobile.msaaSampleCount = 4; mobile.renderScale = 1; mobile.supportsCameraDepthTexture = false; mobile.supportsCameraOpaqueTexture = false;
            GraphicsSettings.defaultRenderPipeline = mobile; QualitySettings.renderPipeline = mobile; QualitySettings.vSyncCount = 0; EditorUtility.SetDirty(mobile);
            AssetDatabase.SaveAssets(); Debug.Log("Quest Android configured: ARM64, IL2CPP, OpenXR, Touch / Touch Pro.");
        }
        [MenuItem("Evidence VR/Configure Serialized Input")]
        public static void ConfigureInput()
        {
            const string path = "Assets/TaskPlatform/Config/TaskControls.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset == null)
            {
                var actions = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = new InputActionMap("Task"); actions.AddActionMap(map);
                AddButton(map,"RightPrimary","RightHand","primaryButton","a");
                AddButton(map,"RightSecondary","RightHand","secondaryButton","b");
                AddButton(map,"LeftPrimary","LeftHand","primaryButton","enter");
                AddButton(map,"LeftSecondary","LeftHand","secondaryButton","p");
                AddButton(map,"Calibrate","LeftHand","triggerPressed","c");
                map.AddAction("HeadPosition",InputActionType.Value,"<XRHMD>/centerEyePosition").expectedControlType = "Vector3";
                map.AddAction("HeadRotation",InputActionType.Value,"<XRHMD>/centerEyeRotation").expectedControlType = "Quaternion";
                map.AddAction("HeadTracking",InputActionType.Value,"<XRHMD>/trackingState").expectedControlType = "Integer";
                File.WriteAllText(path,actions.ToJson()); UnityEngine.Object.DestroyImmediate(actions); AssetDatabase.ImportAsset(path);
                asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            }
            bool repaired = false;
            foreach (var map in asset.actionMaps)
                for (int i=0; i<map.bindings.Count; i++)
                    if (map.bindings[i].path.Contains("<MetaQuestTouchProController>"))
                    { map.ChangeBinding(i).WithPath(map.bindings[i].path.Replace("<MetaQuestTouchProController>","<QuestProTouchController>")); repaired = true; }
            if (repaired) { File.WriteAllText(path,asset.ToJson()); AssetDatabase.ImportAsset(path); asset=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path); }
            var controller = UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>(); controller.inputActions = asset;
            var driver = controller.head.GetComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(Reference(asset,"HeadPosition"));
            driver.rotationInput = new InputActionProperty(Reference(asset,"HeadRotation"));
            driver.trackingStateInput = new InputActionProperty(Reference(asset,"HeadTracking"));
            EditorUtility.SetDirty(controller); EditorUtility.SetDirty(driver);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene); EditorSceneManager.SaveScene(controller.gameObject.scene); AssetDatabase.SaveAssets();
        }
        static InputActionReference Reference(InputActionAsset asset, string name)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)))
                if (obj is InputActionReference reference && reference.action.name == name) return reference;
            throw new Exception("Missing serialized action reference: " + name);
        }
        static void AddButton(InputActionMap map, string name, string hand, string control, string key)
        {
            var a = map.AddAction(name,InputActionType.Button);
            a.AddBinding("<OculusTouchController>{"+hand+"}/"+control);
            a.AddBinding("<QuestProTouchController>{"+hand+"}/"+control);
            a.AddBinding("<Keyboard>/"+key);
        }
        [MenuItem("Evidence VR/Build Quest APK")]
        public static void BuildQuest()
        {
            Directory.CreateDirectory("Builds/Quest"); Directory.CreateDirectory("Evidence");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.Android, locationPathName = "Builds/Quest/EvidenceVR-Stage1.apk", options = BuildOptions.None });
            File.WriteAllText("Evidence/android-build.txt", "Result: " + report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\nBytes: " + report.summary.totalSize + "\nTime: " + report.summary.totalTime);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed; see Evidence/android-build.txt and Editor log.");
        }
    }
}

# Meta Quest Pro target and eye permission

Date: 2026-11-02. Application: `2.0.0-stage2-dev7`, Android code 19. Training configuration/controller remain dev5 / `rolling10-stride2-v1`.

## Target and implementation

The user clarified that Meta Quest Pro is the intended deployment and genuine eye-tracking target. Earlier Quest 3 observations remain historical compatibility evidence, not Quest Pro acceptance. No physical Quest Pro test has been completed.

The project already enables Meta Quest Support, the Quest Pro (`cambria`) device declaration, Touch Pro input and the OpenXR Eye Gaze Interaction Profile on Android and desktop. The existing dev6 APK already declares `com.oculus.permission.EYE_TRACKING` and the `oculus.software.eye_tracking` feature. The installed OpenXR 1.19.0 package adds these declarations when the enabled eye-gaze feature targets Quest Pro. This is distinct from requesting permission at runtime: its automatic runtime request is inside the foveated-rendering path, which this project does not enable.

The final dev7 manifest retains those declarations. OpenXR marks the eye-tracking feature required; the list of declared Quest devices is not proof of distribution compatibility with eye-less hardware. Quest Pro is the acceptance target for this candidate.

Dev7 explicitly requests eye-tracking permission once when an Android Stage 2 session initializes, before task trials. An existing grant skips the request. Grant/denial and the initial request are recorded as `gaze_permission` events with revision `quest-eye-permission-v1`. Sampling rechecks authorization; without permission, samples remain invalid and no highlight appears. Callback subscriptions are removed at session shutdown. Stage 1 does not add a permission prompt. Desktop/Editor use the runtime's own permission route and log `desktop_runtime_managed`; this does not assert that eyes are available.

The reader uses the OpenXR tracked-eye device's `gazePosition`, `gazeRotation` and `isTracked` features. This matches the installed package's `EyeGazeInteraction.cs` / `EyeTrackingUsages` contract. No Meta XR SDK dependency or head-direction fallback was added. Gaze remains an observational channel, not an adaptive-control input.

## Operating on Quest Pro

1. Enable and calibrate eye tracking in the headset before the session.
2. Install the dev7 APK and deploy the existing external media/configuration as described in [Stage2Guide.md](Stage2Guide.md). Use an engineering session for device acceptance.
3. Accept the eye-tracking authorization request when Stage 2 initializes. If denied, enable the app permission through the headset and restart the session; do not interpret invalid gaze as looking away.
4. To inspect targeting, explicitly enable `engineeringGazeHighlight` in the device configuration before launch. The Unity Editor menu changes the Editor's local configuration, not an already installed headset's configuration.
5. Verify valid samples and correct sphere/movie classification, then check tracking loss, invalid samples and pause. Disable the engineering overlay after debugging. Participant sessions always forbid highlights, including practice.

Quest Link/PCVR is a separate runtime path: the desktop OpenXR runtime must actually expose tracked eyes. The Android permission request does not configure desktop eye forwarding. Standalone and Link acceptance must be recorded separately.

## Validation boundary

Editor C# compilation and 2,033 existing synthetic geometry/permission-matrix/shader checks passed. An isolated `editor-test` smoke session confirmed `desktop_runtime_managed`, debug disabled and unchanged OFF behavior; console readback contained the new permission event and no current errors. This does not exercise the Android system dialog. The final Android build and manifest inspection are recorded in [AcceptanceRecord.md](AcceptanceRecord.md).

Physical checks remain pending: grant/denial/revocation on Quest Pro, calibration and real valid-pose acquisition, target alignment and stereo appearance, comfort, media performance, controls and exports. Software compilation and manifest declarations do not establish real tracking accuracy or participant readiness.

Implementation checkpoint before this change: `af1d08e`. Local evidence: `Evidence/QuestProSupport/`. Pipeline references used: `scripts.md`, `build-and-compilation.md` and `editor-lifecycle-and-observability.md` from the installed Pipeline package.

Sources: [Unity XR gaze and platform permissions](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit%403.0/manual/xr-gaze-interactor.html), [Meta OpenXR Quest settings](https://developers.meta.com/vr/documentation/unity/unity-openxr-settings-quest/). Installed OpenXR source files inspected: `Runtime/Features/Interactions/EyeGazeInteraction.cs`, `Runtime/Settings/OpenXRPermissionSettings.cs` and `Editor/MetaQuest/ModifyAndroidManifestMeta.cs`.

## Live Link follow-up: 2026-11-02

Quest Pro head/controllers initially tracked over PC Link while real eyes remained invalid. The runtime rejected creation of `eyegaze/pose` with `/user/eyes_ext` (`XR_ERROR_PATH_UNSUPPORTED`), including with the Virtual Desktop compatibility layer excluded. Native tracing later confirmed the matching declared eye path; an engineering Editor-only retry of that same action using the standard aggregate path succeeded. The production reader returned tracked eyes, and the user confirmed that the cyan ray follows eye movements. Choose **Evidence VR > Stage 2 > Quest Link > Use Oculus for Editor Preview** to retain this project's Meta Link runtime selection across Editor restarts. See [live evidence and compatibility limits](EyeRayLinkReview.md). Calibration accuracy, standalone eye acquisition and participant readiness remain unverified; the last built Android candidate remains dev7.

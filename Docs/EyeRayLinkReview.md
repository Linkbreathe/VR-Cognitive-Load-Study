# Engineering eye ray and Quest Pro Link diagnosis

2026-11-10 task-area correction: `gaze-roi-v5-unified-sphere-tray-area` applies the existing sphere/tray area and 4 cm padding to A/B as well as practice, removing the comparison-only sphere restriction. Console, CSV and guard area visibility share the definition. [Current task-area review](UnifiedTaskGazeReview.md).

2026-11-10 follow-up: the referenced Standalone feature had become disabled while an orphan remained enabled. `quest-link-eye-settings-recovery-v1` repairs the referenced instance through the Oculus Editor-preview opt-in. Live Quest Pro acquisition and Console transport were reverified; no Android or full participant acceptance. See [repair, evidence and updated opt-out operation](QuestLinkEyeRepairReview.md). The historical instructions below describe the original v1 setup.

Date: 2026-11-02. Source/application revision: `2.0.0-stage2-dev8` (next Android code 20). No dev8 APK was built. Training configuration/policy remain dev5 / `rolling10-stride2-v1`.

## Operation

Use **Evidence VR > Stage 2 > Engineering Eye Ray > Enable / Disable**. In Edit Mode this updates the Editor-local configuration for the next session; in Play Mode it changes only the current engineering overlay and records a `gaze_ray_toggle` event. It does not restart the task or rewrite the frozen session configuration. The persisted setting is `engineeringGazeRay`; the release template defaults to false. This machine's engineering override was explicitly enabled for the user's requested debugging. The previous override is backed up under `Evidence/GazeRay/editor-config-before-ray.json`.

A continuous translucent white line follows the same tracked-eye sample as the gaze recorder and ROI outlines. Revision `engineering-gaze-ray-v2-soft-white` replaces the original cyan dashes with a 1.5 mm line at alpha 0.22, extending from 0.65 m to 3.85 m along the actual eye ray. It is depth-tested and has no colliders or shadows. This is a direction display, not a fixation or scene-hit estimate. From the eye viewpoint the ray is naturally foreshortened; Scene View provides a useful side view. The existing sphere/movie outlines remain independently controlled.

The user clarified that focus means looking at the sphere-and-tray area. Revision `gaze-roi-v4-sphere-tray-area` replaces the earlier 3-degree/v3 4.5-degree cone with the union of the sphere and `StimulusPlinth` world-axis-aligned renderer bounds, padded by 0.04 m on each side. The enclosing box includes the gap between them; the tabletop, stem and furniture are not added as targets. This is a fixed physical region that follows actual scene placement, not an exact mesh hit or fixation estimate. Visible sphere/tray geometry is unchanged.

The region is active during running trials while either the sphere or tray is visible. Because the tray persists, looking at the area between sphere presentations can still count as task focus. Pause, preparation and end do not count as task focus. The existing sphere outline remains gated by actual sphere visibility: gaze on the tray or gap lights that same outline when the sphere is visible, but does not reveal a hidden sphere. Rendering stays engineering-only; the shared classification applies to logged gaze in all sessions. Task-before-video priority and movie geometry are unchanged. Answer correctness, scoring and adaptation rules are not changed by focus membership.

`session.json` and gaze CSV rows carry the new ROI revision. The `gaze_task_region` event records world bounds and padding at initialization and when geometry changes. Existing CSV `task_visible` still means visible sphere; appended `task_region_visible` records the broader active area. A tray-focused row can therefore have `task_roi=true`, `task_visible=false` and `task_region_visible=true`. Missing/disabled tray falls back to padded sphere bounds and reports its absence. Do not pool task-gaze fractions across ROI revisions without accounting for this definition and visibility change. No smoothing or head-direction substitution is introduced.

The ray can appear during preparation, instructions and running trials. Pause, completion, abort, invalid input, focus/background loss, disable and a sample-delivery gap above 100 ms clear it. Only enabled engineering sessions can allocate the ray; participant sessions, including practice, cannot enable it. There is no head/controller/mouse/synthetic fallback.

While the ray switch is on, the existing top status line shows `ENGINEERING EYE RAY`, the reader status and `VISIBLE`/`HIDDEN`. States include `NO XR RUNTIME`, `NO EYE DEVICE`, `EYES NOT TRACKED`, `EYE PERMISSION MISSING`, `INVALID EYE POSE`, and `TRACKED EYES`. A tracked pose does not imply validated calibration or accuracy. A hidden ray can also reflect the task phase or loss of focus. The usual mode/source badge remains present.

## Records and unchanged task behavior

`session.json` records `gazeRayRevision`, `gazeRayRequested` and `gazeRayInitiallyEnabled`. Configuration/toggle events identify subsequent engineering display changes. Rate-limited `gaze_tracking_status` transitions record runtime/device availability. Engineering preparation diagnostics do not enter `gaze_samples.csv` or trial counters. Running trials continue using the existing shared reader, ROI revision, sampling cadence and CSV format. Participant trial semantics, stimulus timing, feedback, media schedules and adaptation are unchanged. Synthetic fixtures remain separate from the production reader and participant data.

## Resolved engineering Link acquisition: 2026-11-02 follow-up

Revision `quest-link-eye-space-v1` restores real eyes in the tested Editor/Meta Link setup. The user explicitly confirmed Meta Quest Link / Air Link, both eye switches enabled, calibration complete and wearing the headset. The native trace captured a successfully created pose action with exactly one declared subaction, equal to `/user/eyes_ext`; Oculus nevertheless returned `XR_ERROR_PATH_UNSUPPORTED` when creating its space. Retrying that same action with `XR_NULL_PATH` returned success. This aggregate path is allowed by the [OpenXR action-space contract](https://registry.khronos.org/OpenXR/specs/1.0/man/html/XrActionSpaceCreateInfo.html). Responsibility for the original runtime/package incompatibility remains unassigned.

After this retry, the unchanged production eye reader returned `Eye Tracking OpenXR`, `tracked=true`, position `(0.05450, 1.29536, -0.03709)` and rotation `(0.10012, 0.05898, -0.00911, 0.99318)`. The user then confirmed that the cyan dashed ray was visible and moved with the eyes when asked to hold the head still. This establishes genuine Link pose acquisition and user-observed eye-driven visualization in this setup. It does not measure calibration accuracy, stereo alignment, gaze ROI accuracy or participant readiness.

The `Quest Link Engineering Eye Compatibility` Standalone OpenXR feature is enabled with `retryWithNullPath`. Its hook operates only in the Editor with an enabled local engineering configuration. It forwards the original call first and retries only for Oculus, error -22, a known action declaring exactly one matching eye path and a request for that eye path. Other errors, hands, unknown actions, multiple sources, other runtimes, participant configurations and players are not rewritten. This uses the existing genuine gaze action, never a head pose or generated pose. Android settings do not enable the feature. Disable `retryWithNullPath` to trace without retry, or disable the feature for the original behavior; restart Play Mode after changing it.

Windows was also found to default to VirtualDesktopXR, which produced `XR_ERROR_FORM_FACTOR_UNAVAILABLE` while the user was using Link. **Evidence VR > Stage 2 > Quest Link > Use Oculus for Editor Preview** selects the installed Oculus runtime via Unity's runtime selector and saves a project-local user preference, restored after Editor/domain restart. **Use System Default for Editor Preview** removes that opt-in. The Windows runtime registry and other applications are unchanged. The helper uses the installed OpenXR Editor selector API through reflection; if a future package changes it, a warning points to the manual **Project Settings > XR Plug-in Management > OpenXR > Play Mode OpenXR Runtime** selector.

New `session.json` fields identify the compatibility revision and initial state; `gaze_link_compatibility` events record later state changes, including native failure and retry result. An initial `hooked` state means initialization is pending, not that the retry has succeeded. Existing gaze CSV source remains `tracked_eyes`; preparation samples remain outside trial records.

Final follow-up smoke: C# compilation passed, Oculus selection and local preference were read back, and an isolated `editor-test` initialization contained the new metadata and compatibility events. After the user was told they could remove the headset, this final smoke had no XR devices; it is startup/logging evidence, not an additional hardware pass. Unity AI Assistant reported a separate subscription error and XR fell back to the default audio output. Neither is counted as gaze validation. Play Mode was stopped for handoff; the local eye-ray setting, compatibility feature and Oculus preview preference remain enabled.

Evidence: `Evidence/EyeSpaceProbe/probe-worn.json` (before), `null-path-worn-2.json` (real tracked eyes after retry), `validation.json` (11 retry boundary checks, x64 ABI, Standalone/Android configuration), and the native trace. `AgentScripts/QuestLinkEyeCompatibilityValidation.cs` is reproducible software validation. The attempted `live-series.json` capture did not serialize its sample list and is not usable time-series evidence. The successful single readback and explicit user observation are the hardware evidence. Last built APK remains dev7; no new APK or standalone acceptance is claimed.

## Earlier live PC diagnosis (before compatibility retry)

The user confirmed wearing a calibrated Quest Pro, with eye tracking and Link eye forwarding enabled, and could see the Unity room. The readback identified Oculus OpenXR, runtime version 1.208.0, Unity 6000.3.27f2 and Unity OpenXR 1.19.0. Head and both Touch Pro controllers were tracked; `Eye Tracking OpenXR` existed, but `isTracked=false`, with zero gaze position and identity rotation.

The same live session repeatedly reported:

```text
XR_ERROR_PATH_UNSUPPORTED: Failed to create ActionSpace for 'eyegaze/pose'
with subActionPath '/user/eyes_ext'
```

Subsequent `xrLocateSpace` calls reported `XR_ERROR_HANDLE_INVALID`. Extension availability and a device entry therefore do not establish a usable eye pose. The failing stage is eye action-space creation at the Unity OpenXR / Oculus runtime boundary, before ROI classification or visualization. The exact responsible implementation/version is not yet proven.

A reversible comparison excluded the registered Virtual Desktop compatibility layer only in the Unity process via its documented `disable_environment` variable. The log confirmed zero available layers; the user re-entered Link and the same eye-space error persisted while head/controllers tracked. This layer is not necessary for the observed failure. The process environment was restored to its original unset state; no registry, global runtime or privacy settings were changed in this revision.

ADB still reported the attached device as `unauthorized`. No standalone comparison, APK install or Android eye-permission test was performed. A useful next discriminator is a Quest Pro standalone dev build after the user authorizes USB debugging; it bypasses the PC Link runtime, but is not yet evidence that standalone eyes work. Do not silently replace the gaze source with head direction or with a different eye API.

The OpenXR specification documents `XR_ERROR_PATH_UNSUPPORTED` when an action-space subaction path was not supplied at action creation: [action-space contract](https://registry.khronos.org/OpenXR/specs/1.1-khr/html/xrspec.html#XrActionSpaceCreateInfo). The installed Unity `Runtime/Input/OpenXRInput.cs` does include the eye device path when creating actions, so further native/runtime tracing is needed to assign responsibility. Meta's older [v85 eye-gaze investigation](https://developers.meta.com/vr/support/investigations/1477478657436281/) is marked fixed and concerns a different runtime; it is not proof of the cause in 1.208.0.

## Validation and limits

- C# compilation passed; existing 2,033 synthetic ROI/gating/shader checks passed.
- 72 isolated synthetic ray checks passed: default off, participant exclusion, geometry/gaps, invalid/phase/stale/focus clearing and live disable. The rendered side-view artifact was inspected (`Evidence/GazeRay/synthetic-ray.png`). It is synthetic, not hardware evidence.
- A completed `editor-test` session contained 1,506 production gaze rows, all invalid, with the new metadata revision. No synthetic rays were injected into the sampler. Readbacks, errors, validation and audit are under `Evidence/GazeRay/`.
- At this earlier checkpoint, real eye acquisition failed and stereo alignment was unverified. The follow-up above supersedes only the Link acquisition result. No participant readiness, standalone acceptance, calibration accuracy or physiological-control claim is made.

Restore checkpoint before source changes: `406e5fbe0239bb8796c7eca192b3cf3b7557c1de`. Pipeline references read: installed `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md`, and `commands/runtime.md`. No package/runtime downgrade was performed. The follow-up adds a project-owned, Editor-engineering-only compatibility feature; vendor binaries are unchanged.

Earlier UI check: dev8 compilation passed and the then-current console errors/warnings were zero. The preparation badge was rendered and inspected; the diagnostic string fits its 1,160-unit width (measured preferred width 722.6 for the captured state). The existing Pipeline auto-tick setting remains enabled. No new Android build was produced. The latest eye-acquisition result is the successful follow-up above.

A Play-mode script reload during investigation exposed pre-existing unbound-engine cleanup assumptions. Dev8 adds null guards in `AdaptiveRuntime.Shutdown` and interrupted `AdaptiveMedia.Stop`; separate unbound-runtime and restored-active-media cleanup fixtures passed. These guards change only cleanup when the engine reference is absent; normal interruption handling remains intact.

# Quest Link eye settings recovery

2026-11-10, Stage 2/3, revision `quest-link-eye-settings-recovery-v1`. Native retry behavior remains `quest-link-eye-space-v1`; gaze projection remains `gaze-console-front-plane-v1`.

## Cause and implementation

The current Standalone OpenXR settings referenced feature file ID `8630051931892971767`, with both `m_enabled` and `retryWithNullPath` false. A different, unreferenced same-type subasset (`4027437313833860866`) had both flags true. The running Editor confirmed `GetFeature<QuestLinkEyeSpaceFeature>().enabled == false`; the apparent enabled orphan did not activate the hook. The operation that originally produced this duplicate/rebinding has not been established.

Without the hook, the Oculus runtime identified Meta Quest Pro and one eye device but rejected the eye action space with `XR_ERROR_PATH_UNSUPPORTED` for `/user/eyes_ext`. P001 and Ptest had no valid gaze. This diagnoses an initialization blocker, not a failed eye sensor.

The repair enables the feature actually referenced by Standalone settings and its null-path retry. `QuestLinkPreviewSetup.EnsureEyeCompatibility()` resolves through `OpenXRSettings.GetFeature`, refreshes discovery only when missing, updates that instance and saves only its asset. The existing **Use Oculus for Editor Preview** opt-in invokes this repair when selected and when restored after an Editor/domain restart. It logs the settings revision, asset GUID and exact file ID when repairing. A second call is a no-op. It does not locate features by display name or remove unrelated subassets.

No native retry condition was broadened. The existing hook remains limited to enabled local engineering configurations in the Editor and a known Oculus eye-only action returning error -22. Android settings, gaze validity checks, ROI classification, task timing, scoring and participant records are unchanged. No APK was built.

## Verification

- Unity 6000.3.27f2 compilation completed. Existing native ABI and 11 retry boundary checks passed; two new checks reproduce a disabled referenced feature and verify recovery/idempotence. A subsequent feature refresh retained the active flags. Android compatibility remains absent/disabled.
- The user confirmed wearing the connected Quest Pro for the check. Play Mode logged `eye_space_result=-22;known_action=True;declared_subactions=1;null_path_retry=0`: the original failure was recovered successfully.
- An approximately nine-second real-input probe used the production reader's `isTracked`, `gazePosition`, `gazeRotation` and valid-rotation checks. 128/160 samples were valid; no synthetic input or head-direction substitution was used.
- An isolated engineering practice assignment, `ENGINEERING_GAZE_CHECK`, session `20261110T131424_3499ba0bfeb1`, stayed in Preparation. The live Console snapshot returned `TRACKED EYES`, source `tracked_eyes`, 92/92 valid and projected samples, and telemetry age 0.190 seconds. This verifies transport to the Gaze overview payload; no browser-render inspection was performed.
- The engineering check was explicitly stopped, marked aborted, and its core archive completed. Trial and practice CSV files contain headers only. Play Mode is stopped. Pipeline auto-tick was already enabled and remains enabled.
- Final Unity console ground truth: `compilationFailed=false`, `consoleErrors=0`. The retained buffer includes an earlier Unity AI subscription error; it is unrelated to this repair.

This establishes live Quest Pro Link acquisition and Console transport on this machine. It does not establish gaze calibration accuracy, ROI accuracy, sustained tracking reliability, standalone Android behavior or participant readiness. The probe's invalid samples remain recorded; this is not a claim of continuous 100% tracking.

## Evidence and operation

### Unreferenced component cleanup (2026-11-10)

At the researcher's request, removed only orphan subasset `4027437313833860866` using Unity's asset API after checking serialized references and project text references. The active subasset `8630051931892971767` is preserved. Reimport and feature discovery confirmed exactly one remaining eye compatibility component, referenced by Standalone settings with both enable/retry flags true. Android configuration is unchanged. No Play Mode or device run was needed for this unused-object cleanup; the earlier live acquisition evidence remains the hardware evidence. This removes the misleading duplicate but does not prevent Link/runtime disconnections.

Recovery copy: `Evidence/QuestLinkEyeRepair-20261110/orphan-cleanup/OpenXRSettings-before.asset`; verification: `orphan-cleanup/result.txt`; operation script: `AgentScripts/RemoveOrphanEyeFeature.cs`. Play Mode remains stopped.

Evidence folder: `Evidence/QuestLinkEyeRepair-20261110/` contains the pre-change recovery copies, `live-eye-probe.json`, `console-gaze-verification.json`, `check-assignment.json`, `unity-console.json` and `console-status.json`. Reproducible checks: `AgentScripts/QuestLinkEyeCompatibilityValidation.cs` and `AgentScripts/QuestLinkEyeLiveProbe.cs`. The live probe only reads real XR inputs and writes engineering evidence.

The workflow followed the installed Pipeline documentation: `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/runtime.md` and `commands/multiplayer-play-mode.md` under `Library/PackageCache/com.unity.pipeline@363b8bb9e91d/Documentation~/`.

Restart Play Mode and create a fresh assignment for the intended participant when resuming work. The setup page may retain `ENGINEERING_GAZE_CHECK` as the last-used identity; select the intended ID explicitly. A stopped session can show its last gaze distribution as stale, which is not a new tracking failure. For normal Link operation, keep **Evidence VR > Advanced > Quest Link Settings > Use Oculus for Editor Preview** enabled. To deliberately disable retry for diagnosis, first select **Use System Default for Editor Preview** to stop automatic recovery, then change the referenced feature flags and restart Play Mode. The Windows default runtime is never changed by these menu actions.

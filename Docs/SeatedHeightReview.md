# Seated height normalization and task-gaze diagnosis

2026-11-10, Stage 2/3, revision `seated-eye-height-v2-normalized`.

Subsequent same-day correction: the researcher clarified that A/B must use practice's sphere/tray area and nearby margin. `gaze-roi-v5-unified-sphere-tray-area` implements this. The sphere-only findings below describe archived data, not the current task definition. See [Shared task gaze area](UnifiedTaskGazeReview.md).

## Recorded finding

Read-only inspection of P002 editor-test session `20261110T164151_ff635d6fb7dc` found 5,501 gaze rows flagged valid, 4,875 with the sphere visible, and only one task hit. This is approximately 0.018% of valid samples, which can display as 0%. These sample counts are not duration-weighted review ratios. The session used `sphere-bounds-v1`; looking at the tray, countdown or instruction area does not count as task gaze in this comparison.

The confirmed preparation calibration recorded floor-origin local/world eye height 0.9497925 m with zero rig offset. Across visible valid samples the median eye-ray origin height was 0.9498022 m. Projecting recorded rays to the sphere center's world-Z plane gave median horizontal/vertical differences of +0.72037 m / -0.50679 m. This demonstrates recorded rays missing the task, not the reason for that mismatch. There was no instructed fixation sequence or synchronized head-pose record to establish eye calibration accuracy, intended fixation, or a coordinate-system error. The session later recorded loss of its XR runtime. The last attempted assignment had no eye device; these are distinct states and should not be merged into one diagnosis.

Source records remain unchanged. Summary: [archived-gaze-diagnosis.json](../Evidence/SeatedHeight-20261110/archived-gaze-diagnosis.json). Preparation also deliberately has no visible task target, so preparation task share alone is not an accuracy test.

## Implementation

Previously floor tracking retained the physical eye height while device-origin fallback normalized to 1.20 m. Both now use a one-time rig translation during preparation: `rigY = 1.20 - localEyeY`. The target reuses the project's existing seated device-origin height; it is not a newly validated ergonomic optimum. The existing scene has an upright, unit-scale tracking rig.

The camera and tracked-eye ray share the translated tracking parent. Head rotation and subsequent translation remain tracked; there is no per-frame height clamp or head-direction substitution. Confirmed calibration remains preparation-only. Changing seats during an active round is not corrected silently; start a fresh preparation and calibrate again. Existing mount-integrity rules remain applicable.

New session metadata records `seatedHeightRevision` and `seatedEyeHeightMeters`. Calibration events/logs retain origin mode, raw local eye height, rig offset and resulting world eye height. The fixed desk, low sphere placement, task target geometry, gaze validity tests, A/B thresholds, task timing and scoring are unchanged.

## Operation

Restart Play Mode, select the intended assignment and sit normally in the intended chair. During Preparation press the left-controller trigger or keyboard C once to calibrate. This places the virtual eyes at 1.20 m at that instant. Looking around and small natural head movements continue normally. Recalibrate in Preparation after changing chairs; this operation is not the headset's eye-tracking calibration.

Use an engineering practice session with the existing tracked-eye ray to check deliberate fixations at the sphere, tray, intervening space and nearby margin. Current A/B and practice share that area with 4 cm padding, active during Running while sphere or tray is visible. If the real tracked ray misses the intended area despite deliberate fixation, check headset fit and eye calibration, then repeat the check. Do not substitute head direction for real eyes. A successful height calculation alone does not establish real eye-ray accuracy. The earlier sphere-only operation advice is superseded by [UnifiedTaskGazeReview.md](UnifiedTaskGazeReview.md).

## Validation and boundaries

- Unity 6000.3.27f2 compilation completed. Final console ground truth: `compilationFailed=false`, `consoleErrors=0`.
- `AgentScripts/SeatedHeightValidation.cs`: 58 checks passed in Edit Mode and again in Play Mode. Synthetic floor/device height cases include 0.80, 0.95, 1.20 and 1.65 m; checks cover shared camera/eye translation, unchanged sphere intersections, invalid-eye rejection, retained head movement, the real preparation coroutine, active-task recenter refusal and isolated metadata output.
- Read-back runtime logs confirmed 0.95 m and 1.65 m fixture heads both calibrated to world Y=1.20 m. The coroutine ran without a live XR input subsystem; floor mode was checked through the shared height function, not real floor-tracked hardware.
- Existing `DistractionValidation.Run`: 96,013 checks across 1,000 seeds passed, including timing/schedule/media invariants and updated floor/device height expectations.
- Synthetic sessions exist only under `Evidence/SeatedHeight-20261110/synthetic-metadata/`. The scene task controller was temporarily disabled to prevent operator claims during Play Mode, then restored. Play Mode is stopped; pre-existing Pipeline auto-tick remains enabled. No assignment was created, no participant gaze was injected, and no APK was built.
- Worn Quest Pro checks on high/low chairs, comfort, real-eye sphere fixation and sustained tracking remain pending. This change does not claim the reported task-gaze problem is fully resolved.

Evidence and recovery copies: [SeatedHeight-20261110](../Evidence/SeatedHeight-20261110/). The installed Unity Pipeline references used were `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md` and `commands/runtime.md` under `Library/PackageCache/com.unity.pipeline@363b8bb9e91d/Documentation~/`.

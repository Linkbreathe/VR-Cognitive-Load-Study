# Eye freeze recurrence diagnosis

2026-11-10, Stage 2/3, diagnostic revision `eye-freeze-recurrence-diagnosis-v1`. Read-only investigation; no runtime, task, settings or data changes. Recovery is not verified.

## Findings

Latest local engineering archive: `%USERPROFILE%/AppData/LocalLow/EvidenceVR/Evidence-Aware VR Task Platform/AdaptiveTrainingData/editor-test/20261110T181752_736b243b028d/`.

- All 428 recorded `gaze_samples.csv` rows have `eye_frozen=True` and `valid=False`. Median sample interval is 0.041781 seconds; maximum is 0.071131 seconds. This does not support a sustained stall of the application sampling loop during these rows.
- Head forward x spans -0.608360 to 0.749737, while eye-in-head x spans only 0.433762 to 0.434095 (y: -0.527868 to -0.527149; z: 0.730210 to 0.730531). The reported eye direction is effectively fixed relative to a moving head. This is the same frozen-pose symptom as the earlier run, not proof of the same underlying cause.
- Session events enter `EYES FROZEN` at application time 21.510 seconds, frame 881, with reported spread 0.0000 degrees. The active compatibility hook reports `null_path_retry=0` (success), so the earlier disabled-feature / rejected-eye-space initialization fault is not recurring in this run.
- Unity console additionally shows a transient unfreeze at 18:18:26 UTC (19:18:26 Europe/Copenhagen), another freeze at 18:18:28 UTC, then `EYES NOT TRACKED` at 18:18:30 UTC. These later console transitions are not all present in the already closed session archive and are not counted in its 428 rows.
- The prior `20261110T181147_7c9787611d3e` archive contains 196 recorded rows, all frozen and invalid. Its later 0.4196-degree unfreeze event and `TRACKED EYES` status do not establish sustained recovery. The earlier ProjectLog description of restored tracking should be read as a transient status observation only.

## Interpretation and limits

`AdaptiveRuntime.SampleGaze` reads XR gaze position/rotation, expresses the eye direction relative to the head, and rejects frozen input. `EngineeringGazeRay` deliberately draws this rejected raw pose in amber for diagnosis; it does not substitute head-forward gaze or accept amber samples as valid. A fixed eye-in-head pose naturally produces a ring that moves with the head. The renderer uses world-space vertices.

The affected boundary is the eye pose supplied through the XR/runtime path, before reticle rendering. Existing evidence cannot distinguish headset tracking failure, stale Link eye transport, or another runtime/input-path fault. It does not prove a blocked thread or queue. Oculus service logs contain registry errors and video timeouts, but neither was established as causal to eye freezing. No service restart or headset recalibration was performed during this investigation.

Unity was already stopped when inspected and remains stopped; Editor status was ready, not compiling, with zero current console errors. No new Play Mode session, synthetic input or participant data was created.

## Recovery verification still required

Restore headset eye tracking/calibration and reconnect Link using the existing procedure in `ExperimentGuide.md`, then run a fresh engineering check. With the head held still, look between separated targets; require actual eye-in-head changes and sustained live tracking. A momentary white ring or `TRACKED EYES` immediately after invalid input is insufficient because the detector needs approximately 1.8 seconds of coverage. Verify valid samples and plausible target hits before treating tracking as restored.

Tools: Unity CLI 1.0.0-beta.17 (latest check), installed Pipeline documentation `commands/editor-lifecycle-and-observability.md`, `commands/scripts.md`, and `commands/runtime.md`. Checks were source inspection, existing session/event analysis, Unity status/console inspection and local Oculus log inspection. No behavioral code was changed, so no compilation or synthetic regression test was needed.

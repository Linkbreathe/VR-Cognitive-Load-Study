# Engineering gaze highlight review

Current follow-up (2026-11-02, dev8): at the user's request, shared task focus covers the sphere and tray bounding region, including the gap and 4 cm margin (`gaze-roi-v4-sphere-tray-area`). The engineering eye ray is a translucent continuous white line (`engineering-gaze-ray-v2-soft-white`). Sphere silhouette styling is unchanged; gaze on the tray can highlight the visible sphere. See [current operation and validation](EyeRayLinkReview.md). The dev6 definitions and results below are historical.

Target clarification, 2026-11-02: Meta Quest Pro is the intended genuine eye-tracking device. Dev7 adds the explicit Android runtime permission request; see [Quest Pro support](QuestProSupport.md). The dev6 implementation and validation record below is retained as historical evidence.

Date: 2026-11-02. Application: `2.0.0-stage2-dev6`, Android code 18. Training configuration/controller policy remain `2.0.0-stage2-dev5` / `rolling10-stride2-v1`. Debug presentation revision: `engineering-gaze-outline-v1`. Gaze processing revision: `gaze-roi-v2-visible-targets`.

## Authorized scope and operation

The user requested a faint white sphere outline and a thin white movie border for engineering debugging only. Participant experiments must never highlight. Invalid eye input must produce no highlight.

The new `engineeringGazeHighlight` configuration flag defaults to `false`. Enable it before starting an engineering session:

1. Stop Play Mode.
2. Use **Evidence VR > Stage 2 > Engineering Gaze Highlight > Enable**. An existing, enabled Stage 2 engineering configuration is required. This does not change the controller OFF/SHADOW/ACTIVE mode.
3. Enter Play Mode with a runtime/device supplying genuine tracked eye pose.
4. Use the matching **Disable** menu to switch it off for the next session.

For a device engineering session, set `engineeringGazeHighlight: true` in the complete `adaptive-training-config.json` before launching, with `enabled: true` and `sessionType: "engineering"`. The flag is frozen with the session configuration. The release template remains disabled for gaze highlighting. Participant sessions ignore a requested highlight flag and create no outline renderers, including during participant practice. Editor execution does not override this participant lockout. Stage 1 has no gaze highlight.

No valid tracked eye pose means no outline. There is no head-direction, mouse, controller-ray or recorded-data fallback. The present Quest 3 setup still has no genuine eye input; enabling this option does not add eye-tracking hardware. Synthetic visual fixtures are isolated test code, not an operator input mode.

## Display and common region classification

- The sphere receives a white, 55%-opacity silhouette line, approximately 2.5 mm wide in world space. Its original material, color, scale and visibility are unchanged.
- A currently decoded, playing movie receives a white border approximately 6 mm wide. Movie pixels, gain, dimensions and playback scheduling are unchanged. No border appears for audio-only or inactive media.
- The task region retains the existing <=3-degree direction-to-sphere-center rule; it is not an exact collider hit. Movie classification intersects the eye ray with the existing screen rectangle. Task takes priority over video to prevent double counting.
- Logging and highlighting receive the same `GazeRegionSample` produced by `GazeRegions.Classify`. Invisible targets are now excluded from both: the sphere must be visible in its response interval and the movie must actually be playing with a decoded frame. Looking through the hidden sphere's former position toward a visible movie may therefore classify as video.
- Invalid tracking, nonfinite/zero pose values, looking elsewhere, pause, end, object shutdown and player focus/background loss remove the overlay. If no new sample is presented for more than 100 ms, the overlay clears. This is a local sample-delivery timeout, not validation of hardware timestamps or sensor accuracy.
- The overlay has no colliders, casts no shadows and uses a small URP transparent shader with stereo/instancing support. It does not alter scoring, four-second responses, 4.5-second cycles, rolling decisions, feedback or formal media schedules.

This remains geometric region classification. Full scene-occlusion classification, dedicated timer/feedback regions, fixation/saccade analysis, pupil processing and cognitive-attention inference are not implemented by this update. Real eye calibration/accuracy and stereo appearance need physical validation.

## Records and comparability

`session.json` now records `gazeRoiRevision`, `gazeDebugRevision`, `gazeDebugRequested` and effective `gazeDebugEnabled`. A requested flag in participant mode remains effectively false. The frozen adaptive configuration records the flag. Events include `gaze_debug_configuration` and visual target transitions (`gaze_debug_visual`). `gaze_samples.csv` appends `task_visible`, `video_visible`, `roi_revision` and `gaze_source`; invalid samples remain invalid and trial valid-sample fractions keep their existing denominator rules.

Visibility gating changes gaze-data semantics even when the debug overlay is off, so compare/aggregate gaze records using the explicit ROI revision. The behavior/EEG/ECG controller protocol is unchanged and eye data still does not control adaptation. Engineering data remain separate from participant data. Normal participant visuals are unchanged.

## Validation and delivery

- Unity C# compilation passed. Shader import reported no compiler errors.
- 2,033 synthetic classifier, permission and shader checks passed, including visible/inactive targets, behind-eye rays, three-degree boundaries, invalid pose and participant/default-off lockout.
- Nineteen live Editor checks passed using a separate, explicitly labeled synthetic overlay fixture and an actual decoded practice movie. Sphere and movie colors were unchanged; invalid/stale/pause removal and participant/default-off renderer exclusion passed. Screenshots were inspected: `Evidence/GazeDebug/sphere-outline.png` and `movie-border.png`.
- The 302 production gaze samples from that session were all invalid with both ROI flags false. Synthetic rays were not injected into the eye recorder or controller. Session metadata and configuration restoration were checked in `Evidence/GazeDebug/log-audit.json`.
- 48,128 adaptive model regression assertions passed over 1,000 schedule seeds and 30 complete synthetic sessions. Existing rolling control/timing and media scheduling passed.
- The user's local configuration was restored byte-for-byte after testing, preserving enabled/OFF, seed and endpoint. The engineering highlight remains off. Editor was stopped; no physiology/replay service was started.
- Android dev6 build passed in 246.8 seconds with zero errors and one diagnostics-symbol warning; ARM64/code 18/APK v2 signature verified. Final Editor console errors: zero. The media archive passed integrity with all 44 content hashes unchanged and debug off.
- Android build/signature results are recorded in the current [acceptance record](AcceptanceRecord.md) and [project log](ProjectLog.md). Real eye hardware and Quest 3/Quest Pro stereo/comfort/device acceptance remain pending.

Restore point: `Evidence/RestorePoints/gaze-debug-20261102/working-source-before.zip` includes the existing uncommitted dev5 work, with original Editor configuration and Git HEAD alongside it. New test code: `AgentScripts/GazeDebugValidation.cs`. Pipeline references used: installed `Documentation~/commands/scripts.md`, `editor-lifecycle-and-observability.md`, `build-and-compilation.md` and `capture.md`. The user subsequently requested remote delivery on 2026-11-02; the dev5/dev6 source, validation scripts and documentation are included together in the remote checkpoint. Existing ignore rules keep APK/ZIP artifacts, downloaded media and local engineering evidence outside Git.

## 2026-11-02 engineering eye-ray extension

Dev8 adds an independently switchable, default-off cyan dashed true-eye ray, engineering status readout and audited live toggles. Participant sessions remain excluded. Quest Pro Link readback still fails eye action-space creation; no valid eyes have been demonstrated. See [eye-ray implementation, tests and Link diagnosis](EyeRayLinkReview.md).

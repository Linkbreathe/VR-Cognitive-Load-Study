# Eye input freshness and Link recovery

2026-11-10, Stage 2/3, `eye-sample-freshness-v1-native-time`.

## Finding

The researcher reported head-following gaze in practice and comparison and cannot wear the headset for this investigation. Both phases use `AdaptiveRuntime.SampleGaze`. Eye Ray draws that reader's eye pose in world space, without substituting head-forward direction. Target Highlight consumes the same valid classified sample; its faint persistent sphere outline is a placement aid, not fixation evidence.

Three existing `Evidence/EyeFreezeRecurrence-20261110/native-layers-*.json` probes contain 300 rows each. All 900 rows report successful native location results and flags 15 but repeat eye sample time `301391658123000`. This locates stale input upstream of rendering, without distinguishing a headset, transport, Link service or runtime implementation cause. These are existing records, not new worn tests. The application cannot reconstruct actual eye movement from a cached pose.

## Changes

- The existing engineering Editor/Oculus compatibility feature reads `XrEyeGazeSampleTimeEXT` from its existing eye action space using `xrLocateSpace`, Unity's current application space and predicted display time. It creates no alternate action and does not change the tracking origin or replace gaze coordinates.
- Native timestamp progress is required before accepting input. Repeats exceeding 0.25 seconds, absent timestamps, invalid native locations and backwards timestamps are rejected. A brief tracking interruption cannot refresh the same cached sample. Native and Unity clock epochs are never subtracted. This verifies timestamp progress, not absolute latency or calibration accuracy.
- This extra guard applies only when the existing engineering Editor/Oculus compatibility path is active. Other configurations retain the angular freeze heuristic. Participant and Android hardware acceptance are not extended.
- The shared XR reader additionally requires finite position and both position/rotation tracking-state bits. Invalid samples cannot count toward gaze statistics, bright target highlighting or tracked-eye readiness. The angular freeze detector remains active.
- Gaze rows append sample time, location flags, status and revision; session metadata records the revision. Rejected raw poses can still appear amber for diagnosis. Task rules, timing, scoring and ROI definitions are unchanged.
- Re-enabled the project-local Oculus Editor preview preference, previously disabled. The Windows runtime was already Meta Horizon Link. Eye interaction profile and the referenced compatibility feature/retry were already enabled. This does not establish runtime selection as the freeze cause.

## Link restoration completed after consent

Meta Horizon Link 208.0.0.84.535 was inspected through the UI. General settings identified Meta as the active OpenXR runtime. The initial Developer view exposed eye, passthrough, face and spatial features as enabled. The first navigation screenshot caught a transient master-switch animation and must not be interpreted as proof that it was disabled.

During diagnosis the master developer switch was cycled, resetting eye, passthrough, facial-expression and spatial-data child switches. This side effect was disclosed and restoration paused at the eye agreement. The researcher then explicitly approved restoration of the original features and the corresponding terms on 2026-11-10.

On resuming, the eye agreement was already dismissed and eye transmission was visibly enabled; the assistant did not repeat that toggle. Passthrough was restored, the approved facial-expression agreement was accepted, and spatial access was restored through its confirmation. Final UI inspection verified all six relevant switches ON: Developer runtime features, Pass-through over Link, Pass-through camera API permissions, Eye tracking over Link, Natural facial expressions over Link, and Spatial data over Link. No agreement dialog remains. This completes restoration of the settings affected by the earlier operation; it does not demonstrate recovery of actual eye movement. No service crash, headset reset or deletion was performed.

## Validation and limits

- Unity 6000.3.27f2 compilation completed with no compilation errors.
- `AgentScripts/EyeSampleFreshnessValidation.cs`: 918 checks passed in Edit Mode and again in isolated empty-scene Play Mode, covering 900 recorded frozen inputs, startup, missing data, recovery, clock discontinuity and native ABI. No synthetic data entered the production reader or participant archive.
- In the unworn isolated run, the native accessor returned false with sample time and flags zero. This tests its unavailable path only; connected live accessor verification remains pending.
- Initial fixture attempts encountered assembly reload timing and JsonUtility ephemeral nested-type deserialization failures. The fixture now uses Newtonsoft and final runs passed. Failed attempts are not acceptance evidence.
- The Play Mode PASS log was read back. Current Unity console ground truth reported zero errors and no compilation failure; historical buffers include unrelated Unity AI entitlement messages.
- Play Mode stopped, original saved scene setup restored, pre-existing auto-tick retained. No participant assignment/session or APK/Player build was created.

Evidence and pre-change source copies: `Evidence/EyeInputRecovery-20261110/`. Rollback uses the three runtime copies in `restore/` plus removal of the new freshness class/meta and recompilation. Previous project-local preview preference was disabled. Link feature restoration is complete and separate from source rollback.

The Link features have been restored and verified. Unity was gracefully closed and reopened after saving the current assets/scenes. The new Editor process (PID 38396, previously 14896) reached ready state. Post-restart checks confirm Eye Gaze Interaction enabled, compatibility and retry enabled, project-local Oculus preview enabled, Play Mode stopped and zero current console errors. No new acquisition session was started. The initial OpenProject request did not restart the process; the subsequent explicit close/open did. A future worn Quest Pro check must demonstrate advancing native timestamps, independent eye-in-head movement with a still head, and plausible target hits in engineering practice/comparison. Sustained tracking, gaze accuracy and participant readiness remain unverified.

Sources: [Meta Link development settings](https://developers.meta.com/vr/documentation/unity/unity-link/), [Khronos eye sample time contract](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrEyeGazeSampleTimeEXT.html), installed Unity OpenXR source and Pipeline `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/runtime.md`, `commands/build-and-compilation.md`.

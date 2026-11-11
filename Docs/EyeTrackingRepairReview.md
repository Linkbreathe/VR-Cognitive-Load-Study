# Eye freshness, coordinate recovery and highlighting repair

2026-11-10, Stage 2/3. Implements the requested high-priority software corrections from [the conflict audit](EyeConflictAuditReview.md). Software verification is separate from worn Quest Pro acceptance.

## Implemented behavior

- `eye-freeze-v2-native-stale-angular-diagnostic`: small angular spread is diagnostic only. Fresh timestamps may accompany fixation or small movements; the angular threshold no longer rejects them. The existing `eye_frozen` field now identifies native sample-time staleness, with its existing revision field distinguishing the corrected interpretation. Repeated, missing, invalid and backwards timestamps remain gated. The 250 ms repeat tolerance is retained.
- `eye-space-v2-atomic-pose-all-modes`: eye position, orientation, tracked flags and timestamp come from one `xrLocateSpace` call. Unity's app-space handle and predicted display time are read together. Native right-handed position/orientation is converted to Unity coordinates, then transformed by the same tracking parent as the head. No head-forward gaze substitution is provided.
- The existing OpenXR feature is available and enabled for Standalone and Android. Acquisition no longer depends on the engineering local configuration or Editor execution. The narrow retry still requires Oculus, a known single-source eye action, its eye path and `XR_ERROR_PATH_UNSUPPORTED`; retry is enabled for Standalone and disabled for Android. Participant overlays remain prohibited.
- `eye-coordinate-continuity-v1` / `eye-sample-freshness-v2-coordinate-recovery`: accepted samples require a tracked head with position/rotation flags and user presence when available. Device identity, XR session/state/app-space epoch, tracking-origin events and tracking-parent changes invalidate old evidence. Recovery needs at least three distinct progressing native sample times over at least 100 ms. Gaps over 250 ms reset the sequence. Resetting coordinates cannot make a cached timestamp new. Previous Guard intervals and bright overlays are cleared across discontinuities.
- Recovery does not guess physical head movement during a disconnect, reposition the room/task or silently recalibrate the seat. New eye poses use Unity's current app space. Head-relative angular spread remains diagnostic; absolute head/eye latency and physical calibration are not established by this change.
- `engineering-gaze-outline-v4-preflight-hit`: engineering feedback works on the placed outline in preparation and other phases using accepted eyes. Outside Running, a separate display-only sample uses the existing ROI classifier; it never enters trial CSV rows, Guard intervals or gaze statistics. During Running, feedback uses the recorded classification.
- The position-only outline is faint (alpha 0.10, width 1.5 mm); a valid task hit is bright white (alpha 0.95, width 6 mm). Sphere color/material is unchanged. Engineering status reports `TASK HIT`, `MOVIE HIT` or `POSITION ONLY`, tracking status and the ray state. Suspended/stale/invalid input cannot cause a bright hit. Participant permission checks remain enforced.
- The current frame's stimulus visibility is applied before gaze sampling, eliminating the old onset-frame renderer mismatch without changing stimulus duration or the final rendered state.

The user's symptom had two relevant software gates: the angular heuristic could reject usable input, and the bright overlay previously required running trials. Steering an amber stale ray onto the sphere still cannot authorize a hit. The persistent faint outline is a placement aid, not selection evidence.

## Preserved contracts

Four-second visibility/response, the 4.5-second cycle, integer countdown, scoring, practice/formal feedback policy, A/B controllers, media schedules and sphere/tray ROI are unchanged. No production Python/controller code changed.

Existing AdaptiveRuntime log headers are unchanged; no columns or metadata fields were added or removed. Existing revision fields identify corrected acquisition/display behavior; existing event types carry coordinate-reset diagnostics. Historical data were not rewritten. Corrected validity can change new valid fractions and the evidence supplied to the unchanged controller, so different validity revisions must not be treated as identical acquisition policies.

Current local engineering preferences enable Eye Ray and Target Highlight for the next fresh assignment. Project defaults and participant lockout remain unchanged. The previous local configuration is retained in the restore folder.

## Validation

- Unity 6000.3.27f2 compilation succeeded. CLI 1.0.0-beta.17 was confirmed current.
- [Repair fixture](../AgentScripts/EyeTrackingRepairValidation.cs): **1,085 isolated checks in Edit Mode and 1,085 on final code in empty-scene Play Mode**. Covers fresh fixation, true stale time despite direction changes, 900 archived frozen samples, missing/backwards time, head loss, device replacement, session/space changes, rig transforms, recovery duration/distinct samples, gaps, coordinate handedness, world-target agreement under translated/rotated frames, native ABI/export availability, logging headers, timing, engineering highlighting across phases, invalid/stale clearing and participant lockout.
- Existing geometry/shader/permission checks: **2,042 passed**. Unified-task-region/metadata checks: **13 passed**. Native retry/settings checks: **13 passed**. Distraction regression: **96,013 checks over 1,000 seeds passed**.
- Rendered and visually inspected [position-only outline](../Evidence/EyeTrackingRepair-20261110/outline-idle.png) and [valid-hit outline](../Evidence/EyeTrackingRepair-20261110/outline-hit.png). These are synthetic rendering evidence, not headset photographs or measured eyes.
- Play Mode frame progression and PASS logs were checked. Original saved scenes were restored; Play Mode stopped. No assignment client or participant recorder ran during isolated Play checks. Synthetic fixture outputs remain under Evidence.
- The native snapshot had no active XR runtime and returned unavailable with timestamp/flags zero. Live eye recovery, worn fixation accuracy, actual reconnect, binocular display, Android/IL2CPP deployment and participant readiness remain pending. No APK/Player build was made.
- Initial development encountered an inaccessible legacy InputDevice identifier; production identity checks now use InputDevice equality. The rendering fixture needed a real camera and explicit preview-scene assignment. These corrected failed attempts are not acceptance evidence. Historical console buffers may retain earlier messages; final ground truth has no compilation failure or errors.

Evidence: [Edit result](../Evidence/EyeTrackingRepair-20261110/validation-edit.txt), [Play result](../Evidence/EyeTrackingRepair-20261110/validation-play.txt), [native snapshot](../Evidence/EyeTrackingRepair-20261110/native-snapshot.json), and pre-change source/settings copies under `Evidence/EyeTrackingRepair-20261110/restore/`.

## Physical follow-up

Start a fresh engineering assignment with connected, worn Quest Pro. In preparation, keep the head still and look at the sphere/placed outline, then away. Require `TRACKED EYES` and a bright task hit following eye movement. Verify practice and comparison. In a separate reconnect check, require invalid/held output while disconnected, then new timestamps and a fresh recovery sequence. If task placement is wrong after a reference change, use the established preparation recenter/new-session procedure. Do not assume physical alignment from a recovered status alone.

Tool references read: installed Pipeline `commands/scripts.md`, `commands/build-and-compilation.md`, `commands/editor-lifecycle-and-observability.md`, `commands/runtime.md`; Unity CLI and UI/uGUI skills. [Khronos sample-time semantics](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrEyeGazeSampleTimeEXT.html) permit clamped/predicted/interpolated time; progress alone does not establish calibration or latency. [Reference-space changes](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrEventDataReferenceSpaceChangePending.html) can follow recentering or runtime changes; coordinate discontinuities must be handled explicitly.

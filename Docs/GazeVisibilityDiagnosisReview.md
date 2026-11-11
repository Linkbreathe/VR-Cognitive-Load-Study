# Gaze visibility diagnosis and display repair

2026-11-10, Stage 2/3. Display revision `console-gaze-expanded-view-v1`; report projection `decision-gaze-density-v2-expanded-view`; engineering ray `engineering-gaze-ray-v3-editor-focus`. Backend code revision `evidence-console-v1.30-gaze-display` is on disk; the running v1.29 backend has not been restarted.

## Findings in the requested session

The researcher identified P002 session `20261110T171424_bdd760218e4a`, not the subsequent short practice. Read-only inspection of its archived service/Unity records established:

- This 60-trial comparison already uses five eligible scored trials for current-state ON gaze review. It does not wait for ten trials. Regression coverage now explicitly verifies the five intervals passed to the gaze reviewer. Legacy 144-trial verification remains separate and unchanged.
- At checks 8 and 10, current-state BA was 7/12 (58.333%), below the 65% opening requirement. The review returned before querying gaze. At check 12 BA was 75%, but an applicable current-state model score was missing. These early returns do not mean eye acquisition stopped.
- At check 14, gaze was reached over five trial intervals (trials 10–14 in the report), approximately 20 seconds. Valid eye coverage was 99.982%, task share 0%. The recorded decision was `task_view_insufficient`.
- Console telemetry contains 4,449 unique point IDs, including 2,194 valid/projected points. During the selected decision intervals, projected positions were outside the original plot bounds. The old report consequently had empty density bins and the misleading `no_recorded_positions` status despite recorded positions and nonzero outside-view duration. Both engineering debug flags were enabled.
- The sphere highlighter only appears on a task-area hit while the sphere is visible. Zero task hits therefore explains its absence during the recorded task; enabling it does not force a permanent outline. A separate ray bug could keep a valid ray hidden after the Editor window lost focus. The historical records do not establish whether that focus condition occurred for this session.

The audit also contains ten `Invalid evidence interval or condition` faults beginning at Unity time 196.85, after the mount-integrity pause at 98.10. These are not the explanation for checks 8–14. Their underlying cause remains unverified and was not changed in this repair.

## Implemented repair

Live Console gaze plots now fit the actual finite, valid projected samples and explicitly report how many were outside the original view. Historical density reports expand each frame using only samples eligible for that interval and availability cutoff. They retain the original layout as `referenceLayout`, report `outsideSeconds`, and label expanded plots. Task/movie geometry and recorded target classifications are unchanged. No point is moved onto a target or replaced with head direction.

Decision text explains that an earlier requirement prevented gaze review while eye tracking may still be active. It explicitly identifies the recorded current-state trial window. Interval labels now say selected intervals rather than incorrectly calling every opening-review interval pre-response time. Acquisition, the ten-second overview display history, five-trial decision review and legacy ten-trial verification are distinct concepts; no decision window or threshold was changed.

Engineering eye rays now distinguish application pause from window focus. In the Editor, losing desktop-window focus clears the current line but the next valid eye sample may show it again. In a Player, application focus remains required. Pause, invalid/stale samples, disallowed phases and participant exclusion still hide the ray. A focus-return callback cannot override application pause. The faint white line geometry/material remains unchanged.

## Invalid temporary live diagnostic and restoration

The first isolated live probe disabled `TaskPlatformController` to avoid creating a participant assignment. This also skipped `inputActions.Enable()`, although the camera's TrackedPoseDriver references HeadPosition/HeadRotation/HeadTracking in that action asset. All 200 recorded head poses remained identical while eyes updated. The researcher correctly reported that the scene moved with the head. The resulting 185 valid eye samples and zero task hits are **not valid gaze-accuracy evidence**.

Retain `Evidence/GazeDiagnosis-20261110/invalid-head-pose-probe.json` and its invalidity note as diagnostic failure evidence only. The subsequent attempted check found no tracked HMD/input device, and the researcher requested an end to worn testing. Play Mode was stopped and the normal scene controller restored enabled. No diagnostic scene changes were saved and no P002 participant records were injected or rewritten.

`AgentScripts/GazeLiveDiagnostics.cs` is now read-only and requires a normal running engineering session, enabled head actions and a tracked HMD. It no longer disables the controller, enables a parallel input lifecycle, moves the rig/task, or creates a diagnostic ray. It passed compile-only validation; no corrected live run was performed after the researcher stopped worn testing. Use only after a future explicit live-check request.

## Validation and deployment boundary

- Project Python environment: 39 focused tests and 258 full regression tests passed. An initial invocation through the system Anaconda interpreter failed on unrelated NumPy/package incompatibility; no environment packages were changed. The project `.venv` is the validated interpreter.
- Node display contracts and JavaScript syntax passed. New tests verify expanded views retain source coordinates/task bounds, exclude invalid/late points, and preserve the five-trial reviewer and BA early return.
- Unity 6000.3.27f2 recompiled. `GazeRayValidation.Run` passed 26 isolated synthetic rendering/focus/pause checks in Edit Mode; no additional headset run was performed. `GazeLiveDiagnostics` compiled without execution.
- The initial Edit Mode fixture used Unity `SendMessage` for lifecycle callbacks and generated `ShouldRunBehaviour()` assertions. It was corrected to invoke the component methods directly through reflection. The final 26-check rerun produced no new console error entries after cursor 599. Earlier test assertions remain in console history; they were not erased or represented as a clean historical console.
- Headless Edge replayed the requested session with all non-GET requests blocked. Real archived coordinates appeared in the overview and decision density; task share stayed 0%. Desktop/tablet/mobile widths passed without overflow or page errors. Screenshots were visually inspected. This is recorded-data display verification, not new live gaze acceptance.
- The backend restart attempt was rejected by automatic approval review with `blocked by policy`; no more specific reason was supplied. The old service remains running. Refresh loads changed frontend assets; restarting the existing Console service manually is still required for the historical report backend to use v2 projection. No attempt was made to bypass the rejection.
- Unity remains stopped, the original controller is enabled and pre-existing Pipeline auto-tick remains enabled. Actual head-tracked eye alignment, ray visibility in the headset, sustained Link reliability and the post-pause audit fault remain unverified.

Evidence, pre-edit recovery copies, replay fixtures, screenshots and browser/test outputs: [GazeDiagnosis-20261110](../Evidence/GazeDiagnosis-20261110/). The original archived session files remain unchanged. Installed Pipeline references used: `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/runtime.md` and `commands/build-and-compilation.md`.

# Gaze overview review

Date: 2026-11-03. Stage 2/3 operator monitoring. Display revision: `console-gaze-overview-v1`; Unity telemetry revision: `gaze-console-front-plane-v1`. The service contract remains `evidence-console-v1.2-model-reuse`.

## Implemented behavior

The Physiology page now includes **Gaze overview**, with a front-view diagram of the movie screen, task sphere and tray. **Last 10 seconds** shows fading projected samples and the latest valid point; **Latest point** hides the history. The page is English, uses the existing neutral styling and remains read-only.

Unity supplies actual tracked-eye ray intersections with the front-wall movie plane. Coordinates are meters along the screen's right/up basis. This is a reference-plane projection, not a calibrated screen heatmap, fixation detector, scene camera image or measured object-surface hit. Off-view points are counted rather than clamped. Parallel, backward, nonfinite or excessively distant intersections are rejected. The head position is used only to project schematic object bounds; it never supplies a gaze point. Production task/movie classification remains the existing visibility-gated `GazeRegions` result.

Sampling uses the existing 30 Hz opportunity, now also during preparation/instructions. Non-running samples return before production trial counts, gaze CSV rows and Guard evidence. A rolling ten-second queue is capped at 360 samples and resets on phase or reference geometry changes. The existing once-per-second telemetry carries `gazeOverview`; Python retains it in telemetry and the console snapshot without recalculating eye positions. Session metadata records `gazeOverviewRevision`. Task scoring, timing, RF thresholds and Guard authorization are unchanged.

Task and Movie percentages divide classified valid time by all valid tracked time, including other targets. Valid tracking divides valid time by the observed window duration. Each sample contributes at most 0.1 seconds; missing intervals remain uncovered. The initial window grows up to ten seconds. The display reports duration, coverage and off-view samples. These diagnostic percentages do not replace trial-level or Guard metrics.

## Missing data and deployment

- Without a Unity session, show **Waiting for Unity**, a labeled neutral schematic and no points. EEG/ECG reception alone cannot supply gaze.
- Without valid tracked eyes, show **Tracking unavailable**, no latest point, and only previously valid samples if any remain in the window. Missing permission/device/runtime remains explicit in the message.
- Telemetry older than three seconds is marked stale; the latest point is hidden. Latest-point age must also be at most 1.5 seconds. Paused, completed and aborted phases hide dots and metrics.
- Older Players without compatible position telemetry show **Gaze telemetry unavailable**. Head-pose sources are rejected. Missing scene geometry produces no points.
- Refresh `http://127.0.0.1:8765/#physiology` for the web change. New sessions in the updated Editor source can send the new fields. Existing APKs require a new build before they can send position telemetry; none was built as part of this update. Genuine Quest Pro tracking still requires its existing permission/runtime setup and physical verification.

## Validation and limits

- Unity recompilation completed with no compilation errors.
- `AgentScripts/GazeConsoleValidation.cs`: 16 isolated Editor assertions passed for projection direction, units, rotation, invalidity, bounds and JSON. It uses a temporary PreviewScene; it did not start a task session.
- `AgentScripts/validate_gaze_console.py`: synthetic browser-only fixtures passed no-session, valid tracking, total/partial tracking loss, stale telemetry, off-view, pause, old telemetry, rejected head-pose source and missing-layout checks. Metrics and mode switches passed. Six widths (320, 360, 560, 736, 1080, 1440 pixels) had no horizontal overflow or page errors. Desktop/mobile screenshots were visually reviewed.
- Ten console integration tests passed, including HTTP transport of invalid gaze samples and unchanged HOLD decisions before/after the extra field. All fixtures were isolated from participant records.
- Full AdaptiveTraining Python discovery passed: 154 tests in 49.778 seconds. JavaScript syntax and Git whitespace checks passed.
- A final read-only check of the already-running Editor session `20261103T192021_a50f04b7f959` confirmed the real HTTP path carries `gaze-console-front-plane-v1`, available scene geometry and 286 recent samples. The session was in Preparation, reported `NO EYE DEVICE`, and had zero valid samples; telemetry age was 0.42 seconds. This confirms live transport of explicit invalidity, not successful eye tracking. This update did not initiate that session. [Read-only observation](../Evidence/GazeConsole/live-telemetry-readonly.json).
- No Quest Pro, gaze calibration accuracy, physical viewing alignment, runtime performance acceptance, new live experiment or APK acceptance is claimed.

Evidence: [Editor result](../Evidence/GazeConsole/unity-validation.txt), [browser results](../Evidence/GazeConsole/browser-validation.json), [synthetic desktop preview](../Evidence/GazeConsole/synthetic-gaze-preview.png), [synthetic mobile preview](../Evidence/GazeConsole/synthetic-gaze-mobile.png), [waiting state](../Evidence/GazeConsole/waiting-for-unity.png). A pre-edit file snapshot is retained at `Evidence/GazeConsole/before-gaze-console.zip`; restore selectively because other work may have changed shared files afterward.

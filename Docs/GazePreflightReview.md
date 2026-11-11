# Eye-tracking preflight

## Removal: 2026-11-10

Current display revision: `console-preflight-removed-v1`. The researcher explicitly requested removal of only the Eye-tracking preflight panel. The shared panel, summary helper, periodic update, dedicated styles and panel-specific test/browser scripts were removed. Gaze overview and its display controls remain in Signals; eye acquisition, session logs and A/B decision logic are unchanged. The implementation and verification history below describes the removed panel and is retained as historical evidence.

Validation: Node syntax and existing decision-display contracts passed. Read-only headless Edge verification of the live service returned HTTP 200, found no preflight panel on Run monitor / Signals / Guide, confirmed Gaze overview and Latest point selection, and recorded zero page errors. Evidence: `Evidence/PreflightRemoval-20261110/verification.json` and `signals.png`. No task POSTs were allowed. No service restart, participant data modification, Unity build or physical acceptance was performed for removal. Refresh open browser pages to apply.

2026-11-10 · Stage 2/3 operator Console · `gaze-preflight-v1`

## Purpose and scope

The researcher requested a pre-experiment view of connection state, valid tracking over the latest ten seconds and data update time. The shared Console now displays an Eye-tracking preflight panel above Run monitor, Signals and Guide, including during Unity Preparation. It identifies the selected participant, session and phase and links to the existing gaze overview.

This is read-only operator information. It does not change Start controls, gaze acquisition, recording, task timing, scoring, calibration admission, ON/OFF review thresholds or support-probe rules. The panel has its own HTML revision marker; no Unity visual or session-schema revision is introduced. No synthetic or head-direction substitute is accepted as real eyes.

## Display semantics

- Connection and tracking: distinguish waiting for Unity, disconnected Console, stale Unity telemetry, desktop mode, incompatible gaze telemetry, paused/ended sessions, disconnected XR runtime, absent eye device, missing permission, missing/stale eye samples and currently valid/invalid tracked eyes. These are reported software states, not a diagnosis of a specific USB port or cable fault.
- Valid tracking: use existing `gaze-console-front-plane-v1` / `tracked_eyes` samples in the selected snapshot's last ten seconds. Deduplicate sample IDs, ignore future/out-of-window samples and integrate valid time. Each sample contributes until the next sample or snapshot time, capped at 100 ms, matching the existing gaze overview. Missing intervals within the observed span count as invalid. The denominator is the available observed span, capped at ten seconds. The panel shows that span explicitly; it does not call three seconds a full ten-second check.
- Freshness: use Unity telemetry age plus time from the last eye sample to the snapshot, plus browser elapsed time. Show seconds since the latest sample and its estimated local clock time. Invalid eye samples also update this timestamp: fresh delivery does not imply valid tracking. Age continues to advance while polling fails.
- Hide the rate as an em dash when the Console is disconnected, telemetry is older than three seconds, eye samples are older than 1.5 seconds, a session is paused/ended, the input is desktop/simulated/incompatible, or no measurable window exists. A current invalid sample can coexist with a nonzero ten-second historical rate; the current tracking label remains invalid.

The Unity gaze buffer resets on phase or projection-frame/layout changes. The panel uses the shorter available window after a reset, does not combine sessions and does not claim uninterrupted ten-second coverage until available. Tracking validity does not establish calibrated gaze accuracy, scientific validity or future connection reliability. Existing decision-review thresholds and their denominators are separate.

## Operation

Start the existing Console service if needed and refresh the browser. Create the intended assignment in Study setup, run Unity, and remain in Preparation before starting trials. Select the matching session in Run monitor or Signals. Check that the panel reports Tracked eyes, the observed window reaches approximately ten seconds and Latest eye data keeps updating. Use Open gaze overview while moving the eyes to inspect the corresponding real gaze. Investigate warnings before manually starting the experiment.

No assignment means no session eye telemetry is available; the panel prompts for an assignment. A stopped or historical session cannot certify current tracking. This display does not automatically block starts or pause an experiment, and a successful check cannot prevent a later Link interruption.

## Validation and limits

- Node diagnostic tests cover complete/partial windows, invalid intervals, missing intervals, duplicate/future/old samples, connection and permission statuses, frozen/stale snapshots, offline aging, stopped sessions, actual `desktop_fixed_gaze` configuration, head-pose rejection and input immutability.
- Existing Console decision-display contracts and JavaScript syntax checks pass.
- Headless Edge uses only intercepted local assets and labeled `SYNTHETIC_PREFLIGHT` fixtures. Browser checks cover tracking/runtime/device failures, stale/ended/desktop states, partial windows, disconnected-service age progression, all three monitor tabs and 1440/768/390/320 px widths. No page errors or horizontal overflow; desktop and narrow screenshots visually inspected. No requests reach a live task or recording service.
- The local production service was not listening during verification. No service restart, assignment, trial, Unity edit/build, new worn Quest Pro check or participant check was performed. Refresh the Console after starting the normal service. Physical preflight and sustained USB Link reliability remain pending.

Sources: `Tools/AdaptiveTraining/console/{console.js,index.html,console.css}`. Checks: `Tools/AdaptiveTraining/test_gaze_preflight.cjs`, `AgentScripts/validate_gaze_preflight.py`. Evidence: `Evidence/GazePreflight-20261110/` (browser report, screenshots, test output and pre-edit copies).

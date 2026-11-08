# Console display v3: wording, readiness and decision report

2026-11-08 · Stage 2/3 · display `console-display-v3-clarity` · Console service `evidence-console-v1.21-decision-gaze` unchanged

The researcher reviewed a before/after proposal and approved implementation of three steps in order: P0 unified terminology, P1 wording and readiness fixes, P2 a conclusion-first Session report. The researcher also asked for a more refined color palette. This revision changes only the Console front end. Controller rules, thresholds, review logic, model scoring, records, exports, timing and Unity code are unchanged.

## P0 · One vocabulary

`decision-evidence.js` now holds a shared display vocabulary (`EvidenceUI.terms`, `EvidenceUI.label`). It is loaded by Study setup, Run monitor and Session report. Recorded codes are never rewritten; unknown codes fall back to readable text.

| Previous display text | Current display text |
|---|---|
| layer_a / arm / A · observe / A · hypothetical | Round A · Review observed only |
| layer_b / B · enforced / Patent layer | Round B · Review enforced |
| Patent decisions / Run monitor | Run monitor |
| Physiology / Signals | Signals |
| Experiment guide | Guide |
| Decision evidence / View decision evidence / Open session report | Session report / Open report |
| Evidence Guard / Patent evidence review / Guard review | Review |
| Personal RF / Personal trained model / Personal workload RF (forest / arithmetic) | Workload model (Personal RF on first mention) |
| Base proposal / Base controller proposal | Proposal |
| Delivered / Final command to Unity | Command |
| Unity execution / personal_execution event | Unity receipt |
| current-condition | since the last video change / for the current video state |
| eligible trials | scored trials |
| hypothetical deferrals / Deferred ON / proposed | Proposals held by review; Round A: B rules would hold · not applied |
| Post-upgrade errors / observed | Errors in the 10 trials after each ON |
| not evaluated on this branch | not reached |
| load_reference_ready, editor-test, live_sensor, L0 / L1 | Workload reference ready, Unity Editor test, Live EEG, Video OFF / ON |

All pages share one header: Study setup · Run monitor · Signals · Guide, plus Session report on a report page. The second, duplicate stage navigation bar on the monitor was removed; stage selection remains on Study setup.

## P1 · Wording and readiness

- Study setup readiness lists only the inputs the selected run option uses. Practice "Task only" and rehearsals no longer show an EEG warning or the EEG rate widget. Calibration shows Model as "Trained in this session" instead of "Not required".
- When a run option needs EEG and none is arriving, the next-step card reads "Start the EEG stream" and "Not ready to record". The previous "Ready. Select an ID…" notice is replaced by a neutral connection message. Assignment gating is unchanged: the create button behaves exactly as before.
- Readiness rows carry state icons (✓ ready, ✗ blocked, • waiting, ○ informational).
- The Run monitor EEG-rate alert sits inside the page gutter instead of an edge-to-edge band and is hidden on the Guide. Record paths and the Console revision moved into a collapsible Session details line.
- Session cards show the environment, scope and two-decimal thresholds as chips, readable training status, and one primary "Open report" button with secondary "Download JSON" and "Open folder" buttons.
- Fixed an existing defect: status notices lost their `notice` style after the first message.
- The monitor follows browser hash changes (for example, Back between Run monitor and Signals).

## P2 · Session report

- Header: participant and session start time in UTC (from the UTC session ID), with chips for environment, scope, trials per round and thresholds. Threshold edits are flagged. IDs, model hash and policy revisions are in Session details.
- The A/B summary is always visible: video on screen (with bars), executed changes, proposals held, hold episodes, holds for missing eye data, errors after each ON and decision checks. Executed ON/OFF counts are derived from recorded proposal and hold counts and are shown only when they reconcile with the recorded `executedChanges` total. Reasons for holds are listed per round.
- The checkpoint slider is replaced by a decision timeline: one lane per round, markers placed by trial (ON executed, OFF executed, held by review, Round A changes that B rules would hold) and a band for the commanded video state. Filters dim non-matching markers. Previous/next buttons, arrow keys and Home/End move through the filtered decisions.
- The decision card states the event, for example "Video ON blocked by review", with the first unmet review check as the reason. Proposal, Review and Command follow as three rows, and the blocking step is highlighted.
- "How this decision was reached" separates the proposal routes (either route can propose a change) from the review checks (every check needed). Route status comes from the recorded `behaviorTrigger` / `modelTrigger` flags, not from re-evaluating thresholds. The base OFF behavior route is labeled as either accuracy or omissions. The first unmet review check is marked neutrally. For an OFF deferral, an unmet check means the closure proceeds, and the card says so.
- Empty playback, clip-cut and EEG-window lists show plain sentences instead of `[]`.

## P3 · Run monitor, Signals and Guide

The researcher asked to finish the remaining items after P0–P2.

- Removed an earlier `.monitor-shell #evidence-console` token block. Its higher specificity had kept the old green palette on parts of the monitor after the v3 tokens were added. The unused `.workflow-nav` rules were removed with it.
- Decision graph: inputs (Behavior, Workload model) → Proposal → Review → Unity receipt, with the Gaze check placed under Review, which it feeds. Rows size to their content instead of fixed 126–142 px heights. The inspector no longer stretches to the graph height. Phone layout is unchanged in order and uses automatic row heights.
- The connection notice sits inside the page gutter as a card. It is amber only when the service is offline or a running session's telemetry is stale. Signal preview without Unity is a normal state and no longer looks like a warning.
- Signals: tighter gaze metrics, smaller waveform and feature empty states, notes at 12.5 px, and source IDs/UIDs folded into a "Source identifiers" section.
- Guide: removed the folded legacy continuous-protocol steps and the unused `questionnaire()` helper. One line points to `Docs/ExperimentGuide.md` (which keeps those steps) and to the legacy session options.
- The decision card now handles Stage 3 Guard records, which have no recorded recommendation and use nested checks. Only named probe checks become rows. An enforced review without a recommendation reports the delivered command as its outcome; an observe-mode record reports "Not recorded". "Gaze checks were not reached" appears only for probe-style reviews without gaze evidence. Previously such a record showed "Changed to undefined", an empty first-unmet row and a gaze claim contradicting the graph. A gaze panel without a recorded interval now shows a short note instead of an empty 600 × 360 plot.
- Fixed the selected gaze-period toggle (dark text on a dark background) and missing padding around the latest decision on the monitor.

P3 validation: contract tests extended with Guard-shaped enforce and observe records; all Console scripts passed syntax checks; all 206 AdaptiveTraining Python tests passed. A populated monitor was rendered from the existing unit-test fixture (`test_evidence_console.fixture`, synthetic enforce decision with tracked-eye checks) served in-process on port 8792 with a temporary output folder. The empty monitor, Signals and Guide were rendered on the isolated port 8791. 1440 px and 390 px pages had no document overflow. Screenshots: `Evidence/ConsoleDisplayV3-20261108/p3/` with SHA-256 hashes.

## Palette

The new palette uses an ink-blue accent (`#1f3a5f`) with brass highlights (`#9a7537`) on a warm porcelain background (`#f5f4f0`). Good, warning and error colors (`#2d6a4f`, `#8f6118`, `#a23e36`) stay separate from the accent. `workspace.css` was rewritten around these tokens. `console.css` keeps its existing token names and receives a final v3 block, so existing monitor components pick up the palette. Fonts use the system Segoe UI Variable family, with no network fonts, so the Console still works offline in the lab.

## Validation

- `node Tools/AdaptiveTraining/test_decision_clarity.cjs` passed. Existing contracts were updated to the new wording and still cover separate evidence windows, null/zero values, provenance, receipts and immutable records. New contracts cover route partitioning, trigger-based route status, first unmet check, the B trial 10 headline, Round A hypothetical wording, OFF deferral wording, the no-proposal case and vocabulary fallback.
- `node --check` passed for every Console script.
- The full AdaptiveTraining Python suite passed: 206 tests in 58.3 s.
- The diff, ignoring line endings, is limited to the 11 Console front-end files and the contract test.
- Served-browser check on an isolated port (8791) with a copy of the session index, rendering archived engineering session `20261107T203024_5e0116214361`:
  - The summary showed A 90.0 s / 14 changes (7 ON · 7 OFF) / 27 of 70 errors, and B 54.0 s / 4 changes (2 ON · 2 OFF) / 14 of 18 held / 4 of 20 errors. These match the report JSON.
  - B trial 10 rendered "Video ON blocked by review" with workload 0.634 against ON ≤ 0.56 as the first unmet check.
  - A trial 10 rendered "Video ON executed" with the B-rules note.
  - 1440 px and 390 px layouts had no document overflow.
  - Screenshots are in `Evidence/ConsoleDisplayV3-20261108/` (local; `Evidence/` is not tracked). `before/` holds v1.21 screenshots and CSS; `after/` holds the revision with SHA-256 hashes.
- The production service was not restarted. Static files are read from disk, so the next page load on port 8765 serves this revision. New sessions record `consoleDisplayRevision = console-display-v3-clarity` through the existing mechanism; historical metadata is unchanged.

## Not verified or out of scope

- The populated monitor was checked with the synthetic unit-test fixture, not with live Unity telemetry from a running assignment.
- No headset, EEG acquisition or participant check was performed. Physical Quest Pro acceptance remains pending.
- On phones at 400 px or narrower, the gaze display toggles still stack vertically, as before this revision.

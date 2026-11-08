# Researcher Console workspace

2026-11-07 · Stage 2/3 · `console-study-workspace-v1` · service `evidence-console-v1.19-study-workspace`.

## Purpose and scope

Make independent stage preparation, monitoring and decision inspection easier to operate. This revision changes the Web Console presentation and read-only status/report projections. It does not change the saved RF, training labels, A/B controller rules, score thresholds, task timing, gaze thresholds, media continuity or Unity input behavior.

## Delivered workflow

- **Study setup** has three independent stage cards: Practice, Calibration and Adaptive Comparison. Comparison presents participant/model, Desktop or Quest Pro, editable score thresholds, shared A/B arrangement and readiness in that order. Task rehearsal is a separate, clearly labeled choice.
- **Desktop** explicitly uses simulated 75% valid-eye coverage and 70% task share of valid eye time. **Quest Pro** requires real tracking with no fixed fallback. The existing immutable assignment remains the authority for both rounds.
- The threshold editor retains held-out forest/arithmetic counts and the model recommendation. Model selection means selected, not loaded or physically accepted. Start Session creates an assignment; Unity still completes preparation and starts trials.
- **Run monitor** shows the latest reported stage, completed trials, next scheduled A/B check and decoded playback state. Missing/stale telemetry is labeled. Personal RF retains the score and session cutoffs inside the Decision graph, with a short in-memory trend and last-score time. No separate Live model score panel is added. Repeated polling does not create new score points; changing session, stage or model resets the trend.
- **Decision evidence** uses a shared renderer in the monitor and session report. It presents the conclusion, base proposal, review recommendation, delivered command, execution receipt and an evidence table. Passed, Not met, Missing data and Not evaluated are distinct. HOLD describes the retained video state and, when known, the next scheduled checkpoint; it does not promise a change there. A's review remains explicitly observational.
- **Recent sessions** supports participant/status filters and incremental display within the existing latest-20 registry response. It retains archive details, folder access and evidence download.
- The comparison report separates A/B totals, filters checkpoints by round and decision type, and links contributing trials and ten-trial verification. Technical JSON is folded. Reports expose the recorded EEG origin; known seeded-simulator assignments explicitly identify simulated responses, gaze, playback and receipts. Older records without the environment field are not retroactively labeled Quest Pro.

## Validation

The served application was exercised with actual browser controls. Verified stage selection, Desktop/Quest Pro selection, missing-model Start inhibition, switching to/from EEG-free rehearsal, and completed-session filtering. With Wei_004, changing ON from 0.49 to 0.50 changed the displayed held-out counts from Forest 9/18 and Arithmetic 2/18 to 12/18 and 3/18; ON was restored to 0.49 and OFF remained 0.57. No participant assignment was started on the main service.

An isolated `CONSOLE_UI_CHECK` comparison rehearsal verified that stage, participant, environment, model-mode and order controls are frozen after assignment. Stop/release restored editing and retained the interrupted test record. It was never claimed by Unity and contains no participant trials.

The existing 60-trial replay (`20261107T174942_174bb5be01d2`) was displayed through an isolated report service. B trial 8 showed ON -> HOLD, BA 83.333% passing 80%, RF 0.620 failing ON <= 0.49, eyes Not evaluated, video remaining off and the next check at trial 10. B trial 16 showed ON delivery, RF 0.479, valid-eye coverage 98.5% >= 70% and task share 93.909% >= 60%, both passing. Deferred-proposal filtering and simulated provenance were checked. These are archived software replay results, not new device or participant evidence.

`python -m unittest test_evidence_console test_probe_study -q` passed all 18 tests. JavaScript syntax checks passed for all four affected scripts. Scoped Git whitespace checks passed. Browser checks found and fixed nested stage-button click handling and a report-filter interaction before its initial response had loaded.

Screenshots cover the narrow setup layout and the wide report/locked-session layout. The live monitor showed unavailable RF and unknown playback while EEG and Unity were absent, retaining ON/OFF cutoffs without fabricating readings. A new numeric trend with worn devices was not exercised in this revision.

## Evidence and remaining acceptance

- [Source restore point](../Evidence/ConsoleWorkflow-20261107/source-before.zip)
- [Verification record](../Evidence/ConsoleWorkflow-20261107/verification.json)
- [Narrow setup preview](../Evidence/ConsoleWorkflow-20261107/setup-preview.jpg) and [wide setup preview](../Evidence/ConsoleWorkflow-20261107/setup-wide.jpg)
- [B trial 8 evidence](../Evidence/ConsoleWorkflow-20261107/decision-b8.jpg)
- [Frozen isolated assignment](../Evidence/ConsoleWorkflow-20261107/locked-rehearsal.jpg)

Worn Quest Pro gaze, live Mindtooth A/B operation, physical video/audio delivery and participant benefit remain pending. Replay totals with different answer/gaze inputs do not establish a causal A/B benefit. UI readiness is not model qualification or device acceptance.

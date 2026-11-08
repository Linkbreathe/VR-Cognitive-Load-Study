# Console clarity audit

2026-11-08 · Read-only review of deployed Console v1.21

## Scope

Inspected the live Study setup, Run monitor, Experiment guide and archived comparison page, then checked their source and the immutable report for session `20261107T203024_5e0116214361`. No session was started, threshold applied, controller rule changed, participant record rewritten or device validation performed. There was no active Unity session and the EEG receiver reported no samples, so live device-dependent readiness was inspected in code, not physically exercised.

## Priority findings

### 1. High: the compact decision line mixes evidence windows

The card uses the base model score but prefers the review's current-condition BA, without identifying their different windows. B trial 16 shows RF 0.604 and BA 16.667%, with HOLD. The actual records distinguish base BA 25% from five trials; review BA 16.667% from only four current-condition trials; and no applicable current-condition model score. The base RF exists but crosses the condition boundary. This can look like a controller contradiction.

Show one concise base-proposal line and one current-condition eligibility line. State `4 / 5 eligible trials` and `No applicable current-condition RF window`. Preserve null values instead of falling back across evidence contexts. Do not change the recorded controller rule to fix its explanation.

Pointers: `Tools/AdaptiveTraining/console/decision-evidence.js:93`; `Tools/AdaptiveTraining/probe_control.py:360`.

### 2. High: the criteria table omits the gate that caused some OFF deferrals

For the same B16 decision, the table has base BA, omission, class-count and model-threshold rows, but no explicit row for the missing current-condition model window or insufficient current-condition trial count. The short reason mentions the score-window issue, yet the visible table cannot independently explain it. Passed/Not met rows also do not communicate OR versus AND branches.

Show the decisive recorded gate first, followed by the contributing checks grouped as base proposal, B review and execution. Mark branches that were not reached. B16 should clearly show that gaze was not used to justify this deferral; it was not a gaze-supported OFF interception. Historical tables should identify reconstructed explanations when the original audit omitted a row.

Pointers: `Tools/AdaptiveTraining/probe_control.py:109-160`; `Tools/AdaptiveTraining/console/decision-evidence.js`.

### 3. High: Desktop non-acquisition resembles complete tracking failure

The reviewed Desktop session correctly has no spatial map, but the region chart displays 0% valid and 100% Missing / invalid. The same panel can separately show simulated review values of 75% valid and 70% task share. These represent different sources, but the visual treatment suggests contradictory measurements or a failed device.

For fixed simulated gaze, show `Real gaze not collected in Desktop mode`; display the configured assumptions as simulated values and omit a failure-colored measurement chart when no measured coverage exists. Distinguish not collected, tracking lost, no eligible interval and coordinates unavailable. Never generate positions from ratios.

Pointers: `Tools/AdaptiveTraining/decision_gaze.py:78-85,136-146`; `Tools/AdaptiveTraining/console/decision-evidence.js:56-63`.

### 4. High: the guide still teaches a different protocol

Study setup offers independent Practice / Calibration / Adaptive Comparison. The live guide instead presents eight continuous stages, a default 21:27 duration, mandatory headset/electrode retention language, and Random / Bioadaptive allocation. Source-connection instructions also retain the old Evidence VR > Stage 2 menu path. This conflicts with the current Desktop option, A/B roles and simplified Editor menu.

Make the guide depend on the selected stage and environment. Move older continuous-protocol instructions behind an explicitly labeled legacy reference. Explain the actual sequence: assign in Console, complete Unity preparation, press Enter/X, monitor, then review records.

Pointers: `Tools/AdaptiveTraining/console/console.js:7-29,56-61`; `Tools/AdaptiveTraining/console/index.html`.

### 5. Medium: calibration choices revive the model-selection ambiguity

The calibration heading says Train a personal model, but the default option is Forest / arithmetic workload observation. A separate option says Calibrate the A/B control model. The current workload RF produced by the default path is already selectable for saved-model A/B. An operator can reasonably infer that the default model is unsuitable and a second qualification procedure is mandatory.

Name the current path by its actual output: `Train Personal RF · forest / arithmetic`. Explain the other protocol separately and place it under advanced/legacy if it is not part of the normal intended workflow. Do not delete either backend based solely on this review. The Mindtooth vendor model is currently a legacy single-period option, not a second interchangeable saved-model A/B source; make that capability boundary visible.

Pointers: `Tools/AdaptiveTraining/console/tests.js:14-19,34-37`; `Tools/AdaptiveTraining/test_session_store.py:205-234`.

### 6. Medium: configured, loaded, received and used are not consistently distinguished

The readiness list says a model is Selected and loading is checked in Unity, even though the pre-Unity RF preview can load and score it. The Quest Pro eye row is assigned a waiting status in code regardless of the received tracking status. History badges label the configured environment as real eye input without checking recorded coverage. These labels are not sufficient evidence of actual acquisition.

Use separate, concise statuses for selected configuration, model loaded, fresh signal received, and evidence used in a decision. A Quest Pro configuration badge should say configured; tracked coverage should come from observations. Confirm live readiness with a worn session before claiming it passed.

Pointers: `Tools/AdaptiveTraining/console/tests.js:109-111,140-145`; `Tools/AdaptiveTraining/console/console.js:88-124`.

### 7. Medium: the heatmap period and scale need stronger visual context

The data uses recorded playback/pre-response intervals for some OFF reviews, complete trial intervals for opening reviews, and contextual pre-response intervals for other branches. The primary display lacks a compact trial range and eligible-duration label. Colors are normalized independently within each frame, so similarly dark areas do not imply equal dwell duration between A/B or before/after. At decision and After decision currently switch the gaze/timeline portion while the top RF/BA line stays at the original decision; this scope should be explicit.

Add one line such as `Trials 13–16 · 6.2 eligible seconds · Context only`, distinguish decision inputs from later observations, and keep the legend's seconds scale visible. Use a common color scale if side-by-side comparison is introduced. Label the toggle as gaze evidence period so it does not imply the headline score changed.

Pointers: `Tools/AdaptiveTraining/decision_gaze.py:130-147`; `Tools/AdaptiveTraining/console/decision-evidence.js:33-64`.

### 8. Medium: HOLD and Unity confirmed do not convey the visible result

The main flow ends in Delivered HOLD / Unity confirmed. The useful operational statement, keeps video on or off, is inside a collapsed provenance section and derived from the command context. A matched command receipt is distinct from decoded-video playback.

Lead with the verified command outcome, for example `Keep video ON · command acknowledged`; separately show decoded playback confirmation when available. Display the next scheduled checkpoint and the next recorded result as separate facts. Do not promise that the next checkpoint will change the state.

Pointers: `Tools/AdaptiveTraining/console/decision-evidence.js:88-98`; `Tools/AdaptiveTraining/comparison_report.py`.

### 9. Medium: recommendation, draft and applied thresholds need a clearer relationship

The recommended banner and editable inputs may intentionally differ because remembered or researcher-applied values override the calibration recommendation. Applied values appear in a separate sentence, but there is no prominent pending-change label or compact model/profile provenance. This repeats a confusion already encountered by the operator.

Show `Calibration recommendation`, `Current applied values` and `Unsaved edits` only when relevant. Keep the forest/arithmetic trigger counts next to the draft values and explicitly label their calibration-window basis. Add a restore-recommendation action without changing thresholds automatically.

Pointers: `Tools/AdaptiveTraining/console/tests.js:48-83`.

## Recommended sequence

1. Correct evidence meaning: mixed windows, missing decisive gates, Desktop non-acquisition and command/playback outcomes.
2. Align the setup and guide vocabulary with the current independent-stage workflow; distinguish model sources and truthful readiness.
3. Polish period labels, shared visual scales when comparing, threshold draft states and model display names.

Keep the accepted compact composition. Add short, useful labels instead of new dashboard panels. Any implementation should retain immutable decision records and leave controller behavior unchanged unless separately requested.

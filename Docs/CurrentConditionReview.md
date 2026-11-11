# Current-condition A/B review without an opening test

Current gaze thresholds were revised on 2026-11-10 to >= 60% valid eye coverage and >= 40% task share of valid eye time; see [Gaze threshold review](GazeThresholdReview.md). Earlier validation evidence below predates this threshold revision.

> Superseded for Round B on 2026-11-08 by [the selective support and logged probe](SupportProbeReview.md). The rules below otherwise remain current; Round A is unchanged.

2026-11-08, Stage 2/3. Backend review `current-condition-no-opening-test-v1`, Console `evidence-console-v1.22-current-condition-review`, display `console-current-condition-review-v1`.

2026-11-10 follow-up: the 60-trial ON BA requirement is now 0.65. The ON rows below reflect that revision; OFF and its 0.80 deferral requirement are retained. The later [support-probe policy](SupportProbeReview.md) also supersedes the historical no-probe statements below. [Threshold revision and evidence](OnBAThresholdReview.md).

Latest 2026-11-10 follow-up: ordinary 60-trial OFF is now BA <= 0.50; the shared OFF row below reflects this change. ON remains 0.65 and model-only OFF deferral remains 0.80. [Current cutoffs and validation](OffBAThresholdReview.md).

## Decision and scope

The researcher approved removing the opening test and failure lock from the 60-trial continuous comparison. This supersedes `test6-two-errors-behavior-only-v2` for new sessions served by this backend. No replacement probe, evidence-acquisition state, timeout, error counter, recovery inference or retry budget is introduced. The 144-trial allocation keeps its existing ten-trial verification and failure lock.

The existing `behavior5-continuous-stride2-v3` configuration identifies the unchanged five-trial window and two-trial decision cadence. The new review revision is recorded in `personal_protocol.json`, individual comparison reviews and exported reports. The Console service version changes so the launcher does not identify an older running backend as compatible. Existing recordings are not rewritten. Replaying old inputs with this backend evaluates the new review; it does not reproduce the old six-trial policy.

## Complete remaining rules

| Item | Round A | Round B | Change |
| --- | --- | --- | --- |
| Task | 60 stimuli, including two unscored warmups; 4 s response and 4.5 s onset cycle. | Same. | Retained. |
| Decision timing | Response-close checks at even counts 2 through 58; no new decision at 60. | Same. | Retained. |
| Shared ON proposal | At L0, latest five eligible responses have BA >= 0.65 and fewer than two omissions, OR an eligible model score <= the applied ON cutoff. | Same proposal before review. | Retained. |
| Shared OFF proposal | At L1, latest five eligible responses have BA <= 0.50 or at least two omissions, OR an eligible model score >= the applied OFF cutoff. | Same proposal before review. | Retained. |
| Missing response class | BA is unavailable without both match and non-match trials. Two omissions in a full window can still propose OFF. | Same. | Retained. |
| Applied thresholds | Same selected model and session threshold configuration; each decision retains its actual applied values and revision. | Same. | Retained, including live edits. |
| Behavioral history | Base behavior may span ordinary level changes; existing interruption boundaries remain. | Review uses eligible trials after the latest executed change, in the current state. | Retained. |
| ON review | Record the review; deliver the base proposal. | Require five current-state responses, BA >= 0.65, fewer than two omissions, an applicable pure-current-state model score <= ON, valid eye coverage >= 60% and task share >= 40%. | Retained. These are current-state policy conditions, not a computed recovery claim. |
| Behavioral OFF review | Deliver the base proposal. | Current-state behavioral difficulty permits OFF. An exclusively old-state behavioral proposal is held. | Retained. |
| Model OFF review | Deliver the base proposal. | Require an applicable current-state model window. Otherwise hold the proposal. | Retained. |
| Model-only OFF deferral | Record review without applying it. | May hold OFF only with current-state BA >= 0.80, fewer than two omissions, the existing nonoverlapping low-to-high score/onset alignment, at least 1 s of eligible pre-response decoded playback overlap, eye coverage >= 60% and task share >= 40%. | Retained. |
| Gaze | Review only. | Supports ON or the specific model-only OFF deferral; it never independently proposes OFF. | Retained. Desktop engineering assumptions remain explicitly simulated; Quest Pro requires tracked eyes. |
| OFF notes and linked ON history | Retain both clues, the final note and the latest OFF link. | Same; a linked note is `annotation_only`, with no verification requirement. | Removed verification flags and opening-test wording from new 60-trial links. |
| Forward/reverse relations | Retain recorded claims, session bands, feature checks and relation states. | Same. | Retained; association is not causation. |
| Opening tests | No new shadow opening tests. | No six-trial error test, pass/fail/inconclusive state or test-triggered OFF. | Removed. |
| Failure locks | No new shadow failure counts. | No accumulated opening-test failures or round lock. | Removed. |
| Late opening | Ordinary base policy. | Counts 54, 56 and 58 receive the ordinary ON review. | Removed the trial-52 cap. Round completion still ends playback. |
| Execution | ON resumes audiovisual playback; OFF hides/pauses it at the next trial boundary; HOLD preserves state. | Same mechanism, using the reviewed command. | Retained. Receipts and decoded playback remain distinct evidence. |
| Faults and interruptions | Existing clock, request, model, source, mounting, pause and playback-failure handling. | Same, plus ordinary evidence review. | Retained. Missing data do not create a new post-opening timeout. |

## What this implements

The retained code checks evidence availability and current-condition applicability, combines behavior/model/gaze for ON, reviews particular OFF proposals using playback timing and gaze, and preserves evidence and execution provenance. Ordinary changes exclude old-state observations from the next current-state review without deleting their historical records.

The specialized A/B path does not construct a `recovered_l1` judgment or compute improvement across OFF. It also does not implement the full general evidence-to-judgment-to-action dependency mechanism, selective support revocation and renewed authorization described in the concept. The generic Guard has related capabilities, but this A/B path returns through `ProbeReview`. Removing or retaining a universal opening test cannot by itself establish that the general mechanism is implemented.

## Compatibility and operation

End an existing run normally, restart the Console service, refresh the browser and create a fresh assignment/Unity session. The server cannot resume an existing personalized session after restart. A prepared configuration uses the backend review active when its new session starts; the unchanged timing configuration alone does not select historical review code. Do not combine decisions from different review revisions as one unchanged study condition.

No Unity source, timing contract, scene, media, RF, physiological preprocessing or threshold definition changes are required. Old session reports retain their recorded six-trial or ten-trial verification fields. The renderer uses history-only wording for new linked notes and neutral, recorded-policy wording for historical verification; it does not relabel a historical ten-trial failure as a second six-trial failure. The ten-trial post-upgrade descriptive outcome in reports remains an observation, not a control test.

## Validation and boundaries

- Targeted compact-policy checks passed, including two errors across six trials without a five-trial OFF, ordinary model/behavior OFF, missing-score HOLD without a new timeout, history-only annotations and late ON at 54/56/58.
- An isolated full-round regression drives the real base controller, adapter, execution receipts and report export with synthetic responses, scores and Desktop gaze. Each arm has 29 checks and no new opening-test records. B opens at 8/22/34/46/58 and closes on model evidence at 16/28/40/52, demonstrating repeated reopening without a failure lock. Cached requests remain identical.
- Console display contracts and JavaScript syntax checks passed. New annotations and archived verification remain distinct.
- All 224 AdaptiveTraining tests passed in 110.446 s, including the retained 144-trial ten-trial verification and failure-lock regressions. All 14 generic Guard tests passed. Evidence: `Evidence/NoOpeningTest-20261108/` (engineering only, ignored by Git).
- The old service was idle, with no telemetry or prepared/claimed/running assignment. It was restarted with the same host, LSL and policy configuration. The health endpoint reports v1.22 and the served HTML/JavaScript contain the new display revision and history-only text. No assignment was created; the next run requires a fresh assignment and Unity session.
- No participant, worn EEG, Quest Pro, physical timing or benefit validation was performed. No Unity build was needed or performed.

Source pointers: [controller](../Tools/AdaptiveTraining/probe_control.py), [adapter](../Tools/AdaptiveTraining/evidence_guard_adapter.py), [regressions](../Tools/AdaptiveTraining/test_compact_comparison.py), [historical six-trial design](OpeningTestReview.md), [continuous playback](ContinuousComparisonReview.md).

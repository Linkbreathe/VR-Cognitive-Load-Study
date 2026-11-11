# Video-closing BA threshold of 0.50

2026-11-10, Stage 2/3. Behavior `scored5-on65-off50-v5`, review `current-condition-support-probe4-on65-off50-v3`, backend `evidence-console-v1.26-on65-off50`, display `console-on65-off50-v1`.

## Implementation

The researcher requested an OFF BA threshold around 0.50 after selecting ON >= 0.65. The current 60-trial A/B comparison now uses inclusive BA <= 0.50 instead of <= 0.60 in both the shared behavioral OFF proposal and B's current-state behavioral OFF review. A still executes the base proposal and records the review hypothetically. The model branch remains an independent OR proposal; reducing the BA cutoff does not suppress an otherwise eligible model OFF.

ON remains BA >= 0.65 with fewer than two omissions. Two omissions in a full five-trial window still trigger behavioral OFF even without both response classes. Model-only OFF deferral still requires BA >= 0.80 and the existing model/onset/playback/gaze evidence. EEG cutoffs, eye coverage >= 60%, task share >= 40%, task timing, media, execution gates and scoring are unchanged.

Four-trial support probes still fail on two mistakes/omissions or an ordinary allowed OFF. Technical interruptions remain inconclusive, and the five/seven-trial cooldown is unchanged. Consequently, a probe may turn video off while BA is above 0.50 because its error-count rule is independent. The 144-trial legacy path retains ON >= 0.80, OFF <= 0.60 and its ten-trial verification.

New decisions, review evidence and `personal_protocol.json` record the numeric cutoffs and revised identifiers. The Console says ON >= 65%, OFF <= 50% or two omissions, and model-only OFF deferral >= 80%. Historical rows are rendered with their original thresholds; no saved participant data is rewritten. The earlier [ON-only change](OnBAThresholdReview.md) remains documented as history.

## Validation and operation

Full AdaptiveTraining regression passed **251 tests in 63.858 seconds**. Node decision-display contracts and `console/tests.js` syntax checks passed. No failing checks remained.

Boundary fixtures exercise both rounds at BA 0.50 (OFF), about 0.583 (HOLD without another trigger) and about 0.417 (OFF). Additional checks retain model-only OFF and omission-triggered OFF above 0.50, preserve the 144-trial 0.60 threshold, and verify decision/review metadata. Existing support-probe regressions cover independent probe outcomes. Display checks verify 50% OFF and preservation of archived 60% records, with ON/deferral text unchanged.

Evidence and pre-edit recovery copies: `Evidence/OffBAThreshold-20261110/`. Tests use isolated engineering/synthetic fixtures. This verifies policy implementation, not participant performance, video exposure or headset behavior.

Start the updated Console service, refresh the browser and create a fresh assignment. If a run is active, finish it before restarting. Confirm backend `evidence-console-v1.26-on65-off50`. No Unity rebuild is required. Port 8765 was not listening during the change; no running session was interrupted, production data changed or new physical check performed.

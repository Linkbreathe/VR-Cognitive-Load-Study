# Stage 2 dev11: preparation directly to practice

Date: 2026-11-03. Application/configuration `2.0.0-stage2-dev11`, Android code 23. The user requested removal of the two-minute resting acquisition while retaining preparation and signal checks. Protocol family remains `personalized-rf-v2`; the model policy remains `personalized-rf-pilot-v2-window5`. Flow revision is `preparation-practice-calibration-no-rest-v1`; presentation revision is `personalized-lab-v2.3-preparation-to-practice`.

## Operation and timing

Preparation checks -> practice instructions -> up to 14 practice stimuli -> alternating calibration -> model preparation (one supplemental pair if needed) -> oral calibration questionnaire -> Training 1 -> oral questionnaire/rest -> Training 2 -> final oral questionnaire.

There is no closed-eye or open-eye timed baseline and no baseline countdown. Preparation still requires seated recentering and A/B checks. Participant mode additionally requires ready media and a recent Python reply reporting live, receiving signals. This connection gate is not an electrode-quality certificate: the operator must still inspect source identity, EEG/ECG mappings, units and contact. Existing per-window quality and model admission gates remain active. Engineering mode retains its existing ability to preview without real sensors.

Practice remains skippable at a trial boundary. Starting each task stage still logs the operator's seating/task-area/sensor/eye-calibration recheck acknowledgement. Model preparation, questionnaires, discretionary breaks, pause/resume and mount-movement invalidation are retained. After recorded headset/electrode movement, start a new preparation/practice/calibration session; no model or feature-offset correction is carried over.

| Timed activity | Default duration |
|---|---:|
| Practice: 14 stimuli | 63 s |
| Calibration: 8 x 12 stimuli | 432 s |
| Training 1: 96 stimuli | 432 s |
| Training 2: 96 stimuli | 432 s |
| Total | 1,359 s (22:39) |

The six-block pilot option totals 1,251 s (20:51). These totals exclude preparation, instructions, model computation, oral questions, discretionary rests and interruptions. Skipping practice shortens them; a supplemental calibration pair adds 108 s. The removal saves exactly 120 s relative to dev10's corresponding schedule.

## Data and compatibility

Unity starts with the `preparation` stage and records the new flow policy with `resting_baseline_seconds=0`. Its frozen session metadata records the new presentation revision. Python records the same flow revision in `personal_protocol.json` and no longer generates `rest_closed_reference.json` or `rest_open_reference.json`. The generic offline `resting_reference` utility is retained for historical data; the live personalized service no longer calls it. Raw LSL ingestion during preparation can continue for signal checks and transport buffering; it is not a labeled resting acquisition.

Rest was never a training row, a scaler-fitting source, a baseline subtraction or a production frequency-band update. Training still uses only eligible calibration windows, labeled by their own five trials. The forest, fold-specific scaling, held-out-block/permutation admission, task scoring, four-second response window, 4.5-second cycle, allocations and media schedules are unchanged. Practice-only feedback and oral headset-on questionnaires from [dev10](PersonalizedValidityReview.md) remain active.

Use the matching dev11 APK, Python service and newly generated configuration together. Version checks reject dev10 configuration packets/model artifacts; a fresh personal calibration is required. Do not pool sessions across flow revisions without retaining the revision in analysis. Stage 1 and legacy Stage 2 flows are outside this change.

## Verification and limits

The restore archive and patch preserve the pre-dev11 dirty worktree in `Evidence/NoRestProtocol/`. Updated synthetic-clock tests cover all four counterbalance groups, six/eight calibration blocks, complete flow duration, optional supplementation, practice skipping, interruptions and media-safe decisions. A Play Mode check exercises the existing controller preparation inputs and transient signal-status fixtures. The Python smoke also creates calibration windows directly without baseline data or reference files. These are engineering checks, not physical sensor or participant validation.

Final counts, build identity and validation results are recorded in [AcceptanceRecord.md](AcceptanceRecord.md) and [NoRestProtocolRelease.md](NoRestProtocolRelease.md). The dev10 production-size forest/permutation and P007 audit remain historical evidence for unchanged model logic; neither is a new dev11 physiological acceptance test. Quest Pro standalone operation, real electrodes/units/contact, timing, oral questionnaire rehearsal, model admission rate and scientific effects remain unverified.

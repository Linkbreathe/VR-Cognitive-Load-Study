# Stage 2 dev9: personalized two-level protocol

**Historical protocol.** Dev10 `personalized-rf-v2` supersedes the label alignment, correctness-feedback and questionnaire-removal behavior below. Dev11 additionally removes the initial resting acquisition. Use [NoRestProtocolReview.md](NoRestProtocolReview.md) and [ExperimentGuide.md](ExperimentGuide.md) for current operation. Do not deploy the historical ten-trial policy or load dev9 models into dev10.

Revision: 2026-11-02. Application/configuration `2.0.0-stage2-dev9`; protocol `personalized-rf-v1`; evaluation policy `physio5-behavior20-stride5-v1`. This implements the supplied revised development plan as an engineering candidate. It does not establish physiological validity, cognitive-state accuracy, an intervention effect or patent efficacy.

## Protocol and timing

The task remains seated, four-color 2-back with A = Match and B = Non-match. The sphere is visible and responses are accepted for four seconds; each stimulus cycle lasts 4.5 seconds. The existing single quiet onset cue, visual correctness feedback, sphere/tray placement and two-decimal countdown are retained. No response-dependent audio is added.

| Phase | Stimuli | Nominal time | Behavior |
|---|---:|---:|---|
| Eyes-closed resting baseline | 0 | 60 s | First 30 s excluded from the reference |
| Eyes-open resting baseline | 0 | 60 s | Fixed task-area fixation; no media |
| Practice | Up to 14 | Up to 63 s | Two observe-only stimuli, then 12 scored; X skips at the next boundary |
| Calibration | 8 x 12 | 432 s | Alternating L0/L1, uninterrupted memory across blocks |
| Personal model preparation | 0 | Measured, not promised | Asynchronous Python fit and self-test |
| Training 1 | 96 | 432 s | Random or Bioadaptive, allocated before the session |
| Training 2 | 96 | 432 s | The other strategy |

Default nominal timed total: 1,479 s (24:39). Six calibration blocks give 1,371 s (22:51). Model preparation, instructions, external questionnaires, rest, interruptions and setup are additional. One supplemental calibration pair adds 24 stimuli / 108 s. Practice can be shorter.

**The first two observe-only stimuli are included in each phase's 96-stimulus budget.** Adding them outside the budget would contradict the requested 7:12 duration. Thus each new 96-stimulus sequence starts with 94 scored opportunities. Calibration memory remains continuous across its internal 12-stimulus blocks; after a condition change the first two stimuli are marked `transition`. Each training phase has four non-overlapping 24-stimulus statistics segments, including warmups in the first segment. `eligible` and excluded counts must be reported separately.

Pause consumes the interrupted stimulus, records it, and uses the next two positions as observe-only rewarm stimuli within the original budget. A pause can therefore reduce scored opportunities and extend wall-clock duration. Pausing a resting minute restarts that minute on resume. Failed or interrupted media is logged, resets control history and cancels remaining media slots in the affected statistics block.

## Two levels and media

- L0 = no additional movie or speech.
- L1 = intermittent audiovisual films; never an audio-only condition.
- Calibration L1 slots occur at 9 and 31 s of a 54 s block, with seeded jitter up to 1 s.
- Training has common potential slots at 9, 36, 63 and 90 s of each 108 s statistics segment, with the same jitter rule. L0 slots are silent; switching replans only unstarted slots while preserving their original times.
- Practice retains the three 10/28/46 s slots with 0.5 s jitter. Its familiarity counts are separate from model data.
- The existing 22-pair library is retained. All 18 training pairs are eligible for the audiovisual pool; the four reserved pairs remain excluded from practice/calibration/training. Audio assets remain on disk for historical protocols.
- The 3.2 x 1.8 m front-wall screen is hidden when inactive. Playback starts are first-decoded-frame software timestamps, not measured light or acoustic onset.

The new slot cadence is an explicit engineering choice because the supplied plan does not prescribe exact L1 onsets. Both strategies use the same schedule and safety constraints. Actual exposure can differ and must be included in analysis. Human media/volume review remains required; the existing `volumeReviewed=false` flags are not changed to manufacture participant readiness.

## Allocation, concealment and external forms

`Tools/AdaptiveTraining/allocate_personalized.py` generates four-cell allocations: Random/Bioadaptive order x calibration starting level. Each consecutive set of four enrollment indices has one of each cell in a seeded shuffled order. Use a unique index per enrollment and preserve the allocation files; do not reassign selectively after observing results. The generator refuses to overwrite an existing participant configuration. It does not manage a shared enrollment registry; the experimenter must keep indices unique.

Participant presentation uses `Training 1` / `Training 2` and hides policy/source diagnostics. The actual strategy, allocation, model status, source identity and control reasoning remain in operator/service records. Engineering mode retains diagnostics. Engineering gaze highlights/rays remain default off and blocked in participant sessions.

Calibration and each training phase lead to an external questionnaire/rest page. No numerical subjective ratings are collected in VR for dev9. X acknowledges questionnaire completion and proceeds to a separate start/recheck screen; the acknowledgement is an operator/participant assertion, not proof that Google Forms received a submission. Check seating, task area, sensors and eye calibration after headset removal. The experimenter gives the eyes-open cue after 60 seconds; no additional audio cue was introduced.

No Google Forms URLs were supplied. `calibrationFormUrl` and `trainingFormUrl` remain empty configuration fields for the operator's record; the headset does not launch forms. Use [questionnaire setup](QuestionnaireSetup.md) to prepare the external forms. Form creation/submission and response linkage are pending.

## Signal acquisition and quality

The existing exact-outlet LSL selector and raw timestamp/provenance logs are retained. Select actual channels and convert EEG into microvolts before using the +/-100 uV rule. The policy's channel indices refer to the selected EEG columns, not the original full outlet's columns. Describe the actual electrode sites; ear channels are not Fz/Pz measurements.

Python buffers selected raw samples and extracts a 22.5 s window after each completed stimulus with a 4.5 s stride. Only one condition and one task phase may occur in an admitted window. Windows crossing interruptions, transition trials, excessive onset/end deviations, source gaps or clock uncertainty are excluded. At least 95% sampling coverage, monotonic timestamps and bounded gaps are required. The engineering clock-uncertainty limit is 50 ms; it requires hardware validation before freezing.

EEG uses a 50 Hz notch and 1-40 Hz bandpass, two-second subsegments stepped by one second, +/-100 uV rejection relative to each raw subsegment's median and flatline rejection, and at least 50% accepted subsegments. EEG sampling must exceed 100 Hz for these bands. Default features are log theta relative power (4-8 / 1-30 Hz) and log alpha relative power (8-13 / 1-30 Hz), mapped to explicitly configured channels. Welch frequency bins use lower-inclusive, upper-exclusive band edges. Filters and feature extraction use the same completed-window method offline and online; no future signal samples are used.

ECG uses a provisional polarity-independent R-peak detector, physiologic interval bounds and a median-deviation check. At least eight intervals, 80% acceptable intervals and edge coverage are required for mean heart rate. This detector is not device-validated. lnRMSSD is excluded from the model. Movie playback fraction is the union of actual started/ended intervals, not the current level name.

The closed-eye reference uses seconds 30-60; the open-eye reference uses its full minute. An IAF candidate is reported only with an explicitly qualified montage, confirmed units, a fully artifact-accepted interval and a clear interior 8-13 Hz peak. Otherwise `iaf_available=false` and fixed bands remain in use. IAF does not change production bands in this revision and resting measurements do not supply Stable/Struggling labels.

Old raw-bridge 12 s EEG / 30 s ECG configuration fields remain compatible with the LSL selector. For dev9 those legacy feature schedules are bypassed; the personalized processor uses completed five-trial windows.

## Labels: unresolved pilot design made explicit

The user selected `label_alignment="retrospective_block10"` on 2026-11-02 as the provisional pilot default. It uses the final ten valid trials within the same calibration block, excluding warmups, transition, interrupted and aborted trials. A feature window must lie within the label interval. The alternative `trailing10` uses only behavior available by the feature-window end.

**With 12-trial blocks and two excluded initial/transition trials, the strict rule allows at most one labeled feature window per block.** Eight blocks provide at most eight labeled candidates, or ten after the supplemental pair, so the two classes cannot both reach ten samples. This incompatibility in the proposed timing/label/sample gates must be resolved in the pilot; the implementation does not silently relax gates. The optional strict `trailing10` setting can exercise collection, rejection, supplementation and fallback, but cannot admit a personal model under these unchanged sample gates.

The selected provisional default, `retrospective_block10`, associates the block's final ten valid trials with the six retained physiological windows fully contained within that interval. It yields at most 48 labelable candidates over eight blocks, with only eight block-level behavioral targets. Some labels use behavior after a feature window. The shared label intervals, retrospective interpretation and heavy dependence must be disclosed; this is a provisional alternative, not an approved ground truth. The user approved this mapping for the engineering pilot; it remains methodologically unvalidated. Freeze its final definition only after methodological review/pilot evidence.

The label score averages standardized BA, negative correct-response RT and negative omissions. Normalization and quantile cutoffs are fitted on training blocks only within each validation fold. The lower third can be Struggling only with an absolute difficulty gate (candidate BA <= .70 or at least two omissions). The upper third can be Stable only with BA >= .85, no omissions and median correct RT <= 3 s. Other windows are unlabeled. An entirely good performer never receives artificial Struggling labels merely to fill a class quota. These absolute gates and quantiles are provisional policy fields, not established clinical or cognitive thresholds.

## Personal model and admission

Each attempt trains from the current session's calibration data only. Practice, resting and both training phases are excluded. The model is a 300-tree, depth-3 `RandomForestClassifier`, minimum leaf size 5, `sqrt` features and balanced-subsample class weights. A scaler is fit within each training fold and then refit on the eligible final calibration training set. No population model is loaded and formal training never refits the model.

Leave-one-block-out validation refits label normalization, the scaler and the estimator without the held-out block. Reports retain fold membership, label definitions, all out-of-fold scores, ROC-AUC, frozen-0.5 balanced accuracy and within-L0/L1 summaries. Three comparators are included: physiology only, actual movie fraction only, and current distraction condition only. The combined model must beat both environmental baselines by the configured AUC margin.

Admission also requires at least ten labeled windows of each class from at least two blocks each, usable held-out folds, a specified AUC floor, a frozen qualified policy, confirmed EEG units/electrodes and live participant-source provenance. The default AUC floor is null and `frozen=false`; no real model can be admitted by default. Same-session gate performance is preliminary admission evidence, not independent cognitive-state validation. Eighty-percent-overlapping windows are never reported as independent replications.

The frozen policy supplies separate L0/L1 score thresholds. Both default to Stable <= .35 and Struggling >= .65. There is no small-sample threshold search or reuse of validation scores to tune accuracy. Threshold instability is handled by retaining these simple pilot values until qualified. Output is an uncalibrated classifier score, not a probability of true cognitive state.

Each attempt saves training rows, policy, configuration/hash, feature order, label definitions, version information and a report including every failure reason. Admitted artifacts are loaded back with participant/session identity and hash checks; rejected estimators, if trainable, are named `unvalidated_candidate.joblib`. Never load joblib artifacts from untrusted sources. Service restart refuses an existing dev9 session rather than silently reusing stale buffers/models.

A first failed attempt offers one supplemental L0/L1 pair. A second failure preserves `personal_model_not_validated`; the configured behavior fallback can run the nominal Bioadaptive slot, but analysis labels it `behavioral_fallback_or_engineering`. Such sessions must not be analyzed as successful physiological Bioadaptive sessions. If the service is unavailable, model preparation waits and stop remains available.

## Decisions and execution

Python makes formal requests only every five completed trials, at 5, 10, ..., 95. It evaluates the last 20 behavioral trials and the latest five predictions. Random uses only its saved seed-generated 19-entry schedule; model shadow predictions cannot affect it. Bioadaptive requires at least three valid recent pure-condition predictions, enough new-condition trials, adequate actual movie exposure when L1, an admitted model and agreement with behavior. Opposing/insufficient evidence gives HOLD. The fallback is explicitly identified; no invalid model supplies physiological authority.

Unity checks session/request/version/phase/epoch/evaluation identity and request age, then executes ON/OFF/HOLD/REST at a trial boundary after active media ends. Deferred and rejected requests and actual execution are logged. A decision is not an execution receipt. Level changes and interruptions reset physiological history; mixed-level statistics are retained and flagged. The protective operator pause/stop remains independent of the model. REST is supported as an incoming control action; this revision does not invent an automatic REST threshold absent from the supplied plan.

Twenty-trial behavior windows exclude transition rows, so physiological control can resume later than the first complete five-trial window after a switch. The logs distinguish minimum dwell, insufficient recent pure windows, insufficient behavior and insufficient actual exposure.

## Operation

1. Stop Play Mode. Select **Evidence VR > Stage 2 > Enable dev9 Personalized Engineering Protocol**. The previous override is copied to a timestamped `.pre-dev9-*` backup. This creates an engineering session, not participant approval.
2. Use the project Python environment and start the service. Select the real publisher/channel mappings externally; the existing source selector can still start the service with the default, non-admitting policy. For a reviewed policy use the CLI:

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/service.py --output Evidence/PersonalizedService --lsl-config UserSettings/lsl-source-selection.json --personalized-policy Tools/AdaptiveTraining/personalized-policy.json
```

3. For participant allocation, use a managed unique enrollment index. Set the study computer address reachable from the headset:

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/allocate_personalized.py --participant P001 --index 0 --allocation-seed 20261102 --controller-url http://STUDY_COMPUTER_IP:8765 --output Evidence/Allocations
```

4. Review the output and deploy it under the exact name `adaptive-training-config.json` in the app's persistent data folder. Deploy the existing media package. The APK alone still starts Stage 1 without an enabled override. Participant startup requires reviewed media and receiving live sources. Engineering/debug imagery remains prohibited in participant sessions.
5. Run the two resting minutes, practice, calibration and personal-model preparation. Follow the supplemental-pair/failure path if necessary. Complete the external forms at the three prescribed breaks. Save both Unity and Python session folders.
6. Export descriptive comparisons, including failures and incomplete sessions:

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/analyze_personalized.py PATH_TO_UNITY_SESSION --output Evidence/Analysis/personalized-summary.json
```

The report includes four statistics segments per strategy, BA/RT/omissions, actual movie seconds/start counts, movie-overlap task performance, levels, switches, model status and order. Level durations reconstructed from trials exclude interrupted rows; raw events remain the authoritative source for exact interrupted wall-clock accounting. Missing end events are reported, not silently converted into complete exposure. Lower distraction exposure alone cannot establish improved distraction resistance or long-term learning.

## Evidence and pending checks

Implemented software paths are tested with synthetic fixtures and accelerated Editor UI checks. Exact evidence counts, build results and artifact locations are in [AcceptanceRecord.md](AcceptanceRecord.md). Restore point: `Evidence/PersonalizedProtocol/pre-dev9.zip` and `pre-dev9.patch`, preserving the dirty dev8 worktree before this update.

Pending: pilot validation of the selected label alignment and absolute gates, AUC floor and score thresholds, actual electrode/units verification, ECG R-peak reference comparison, EEG artifact thresholds, optional IAF reliability, true LSL/Unity onset synchronization, questionnaire scoring/submission/export linkage, media content/audio review, Quest Pro stereo/comfort/controller/eye calibration, full real-time device sessions, model self-test pass rate and scientific efficacy. Two English pilot Forms are published and linked; see [questionnaire record](QuestionnaireSetup.md). Historical Quest Pro Link eye acquisition is not a dev9 physiological/device acceptance pass.

Implementation reference: [scikit-learn RandomForestClassifier documentation](https://scikit-learn.org/stable/modules/generated/sklearn.ensemble.RandomForestClassifier.html). Local Unity Pipeline `commands/scripts.md`, `commands/build-and-compilation.md`, `commands/editor-lifecycle-and-observability.md`, `commands/capture.md` and `commands/runtime.md`, plus the CLI `playmode-verification-loop.md`, were used for Editor validation.

## Confirmed P007 replay mapping (2026-11-02)

The user supplied `D:/Code-Space/patent_code/P007/sub-P007.xdf` and confirmed EEG XDF columns 1-4 and ECG column 7 minus column 8. Column 0 is a sample counter and is never an EEG input. The header identifies Hyperscanner, 500 Hz and uV. Anatomical electrode locations remain unspecified, so both EEG band features average the four selected EEG columns and IAF remains unqualified.

`p007-personalized-replay-policy.json` confirms the recorded file's units/mapping, retains the selected retrospective block labels, and keeps `frozen=false`, `device_qualified=false`, IAF off and the AUC gate unset. Replay provenance independently prevents admission. No source XDF bytes are modified. To run the dev9 transport demo:

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/launch_replay_demo.py --personalized --editor --mode ACTIVE --duration 300 --repeat 6
```

The personalized launcher defaults to the supplied full `sub-P007.xdf`; the historical launcher without `--personalized` retains the earlier dev5 protocol and earlier cropped recording. Ctrl+C stops the demo processes and restores the previous Editor override. Use this only for engineering/transport checks: recorded relaxation-task physiology is not calibration evidence for a new participant's anti-distraction experiment.

The actual dev9 replay check retained 17 candidates after 32 completed calibration stimuli: 15 failed signal quality and two included excluded transition history. All 15 evaluated EEG windows failed the initial subsegment gate; ten ECG windows passed its candidate gate. No model was admitted and thresholds were not loosened. The recorded source and mapped data reached Unity through the running service, demonstrating transport and rejection behavior only. See `Evidence/PersonalizedProtocol/p007-replay-summary.json` for counts, provenance, limitations and cleanup.

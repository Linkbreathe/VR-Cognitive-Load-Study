# Stage 2 dev10: window labels, feedback isolation and continuous headset wear

Historical dev10 record. Current dev11 removes the initial resting baseline and updates the application/configuration version; see [current flow](NoRestProtocolReview.md), [release](NoRestProtocolRelease.md) and [operation](ExperimentGuide.md). Dev10 model/feedback/oral-questionnaire rules are retained unless explicitly superseded there.

Date: 2026-11-02. Application/configuration `2.0.0-stage2-dev10`, protocol `personalized-rf-v2`, policy `personalized-rf-pilot-v2-window5`, decision schedule `physio5-behavior20-stride5-v2`. This revision supersedes dev9's retrospective block-ten labels, calibration/training correctness feedback and headset-removal questionnaire procedure. Stage 1 and the legacy dev5 controller retain their approved behavior.

## Reason and scope

The user identified four threats: one behavioral target shared by six overlapping windows per calibration block; correctness feedback contributing to the physiological signal used to predict behavior-derived labels; headset removal changing electrode contact between calibration and training; and the high EEG rejection rate in recorded P007 replay. The user selected experimenter-administered oral questionnaires with the headset kept on throughout calibration and both training phases.

The four-second response window, 4.5-second cycle, central 2-back, 12-stimulus calibration blocks, two 96-stimulus training phases, media schedules, allocation and scoring are retained. Default timed task/rest duration remains 24:39, excluding instructions, model computation, oral questions and breaks. The first two observe-only stimuli remain inside each phase's budget. No twenty-trial feedback summaries were added.

## Exact five-trial labels

Each physiological window is labeled only from its own five completed trials. All five must be scored, uninterrupted, non-aborted and non-transition, contiguous and in the same calibration block and condition. Feature and label interval bounds must agree. Later responses cannot alter an earlier window's label. No minimum Match/Non-match count or balanced accuracy is used for these labels.

The provisional per-trial score is:

```
correct response: 1 + 0.1 * (1 - 2 * reaction_time_seconds / 4)
incorrect response or omission: -1
window score: mean of the five trial scores
Stable (class 0): score >= 0.8
Struggling (class 1): score <= 0.4
unlabeled buffer: 0.4 < score < 0.8
```

Correct RT must be finite and inside the response window. Its adjustment is at most +/-0.1, so correctness dominates. Five correct responses remain Stable even when slow; one error normally lands in the buffer and two errors normally yield Struggling. These are engineering score choices, not validated cognitive thresholds. Fixed score units replace per-session quantiles: a uniformly good performer is never forced into the Struggling class.

There are still at most six clean five-trial windows per 12-stimulus calibration block, or 48 across eight blocks. Their labels can now vary within a block. Adjacent windows still overlap by 80%, and eight blocks are still only eight temporal clusters. This change does not create 48 independent observations or establish a latent cognitive-state ground truth. Same-window labels describe completed performance; they are not a validated forecast of future performance.

## Validation and the permutation admission gate

The 300-tree, depth-3 forest, minimum leaf size five, per-fold preprocessing and leave-one-block-out split are retained. Entire blocks are held out. Practice, rest, Random and Bioadaptive trials remain excluded from fitting. Both training phases use the frozen calibration model.

Admission retains the minimum ten labeled windows per class from at least two blocks each. It also requires at least two blocks containing both labeled classes within each condition, complete held-out-block coverage, and the configured AUC floor separately within L0 and L1. A time-only comparator (calibration block position and position within the block) is added alongside condition-only, exposure-only and physiology-only comparisons. The combined model must beat condition, exposure and time baselines by the configured margin. Default AUC minimum remains unset.

The additional null test shifts each block's ordered label sequence circularly within that same block. Buffer labels move with the sequence; block membership, condition, trial positions, feature values and the signal-quality mask stay fixed. Labels from signal-rejected windows remain in the trajectory before the fixed quality mask is applied. Individual windows are never shuffled independently.

For each of 199 seeded non-identity transformations, all held-out-block combined models are fitted again with the same 300-tree settings. The statistic is the mean of the two condition-specific held-out AUCs. Each individual block may have a zero shift; only the all-zero transformation is excluded from null draws. Unique index transformations are sampled without replacement. Identity-equivalent label transformations remain valid null draws. An unusable null fit counts conservatively as a statistic of 1; it is not dropped. The upper-tail value is `(1 + null_statistics_at_least_observed) / (1 + draws)`, with a provisional maximum of .05. The observed statistic, every null statistic and shift vector, seeds, resolution and failure reasons are saved. Constant label trajectories yield a failed null result without unnecessary refits.

Circular-shift inference requires a defensible within-block stationarity/exchangeability assumption. Six highly overlapping windows make this a small, dependent-data diagnostic, not an independent validation. The flag `permutation_exchangeability_qualified=false` prevents policy freezing until this assumption and the score/gate choices are reviewed in a pilot. A passing null result alone cannot qualify physiology, devices or efficacy. The null is an engineering admission diagnostic, not a change to the thesis's exploratory reporting conventions.

Method references: [Winkler et al., permutation inference and exchangeability blocks](https://pmc.ncbi.nlm.nih.gov/articles/PMC4010955/). The [scikit-learn permutation API](https://scikit-learn.org/stable/modules/generated/sklearn.model_selection.permutation_test_score.html) permutes individual labels within groups; its `groups` argument alone does not implement this circular null, so the project uses an explicit implementation.

## Feedback and presentation

Only practice displays Correct, Incorrect and No response colors/words. Calibration, Random and Bioadaptive always use the neutral countdown, including after an incorrect response or omission. No result-dependent summary or audio is added. Correctness, reaction times and omissions are still recorded and scored exactly as before.

`TaskEngine.ShowsCorrectnessFeedback` is shared by the spatial HUD and legacy presentation paths. Personalized sessions freeze `formalFeedbackEnabled=false` even if an older base task configuration requests feedback. Presentation revision is `personalized-lab-v2.2-practice-feedback-oral-forms`; session events record the practice-only policy. Stage 1 retains its approved formal visual feedback.

## Oral questionnaires and mount integrity

After calibration and each training phase, the participant remains seated with headset and electrodes in place. The experimenter reads the existing questionnaire wording and scale endpoints verbatim and records the participant's spoken answer in the corresponding Google Form. Do not name Random/Bioadaptive, coach an answer, or adjust the headset/electrodes during these breaks. Rest is still allowed. Record administration as `interviewer_oral_headset_on-v1`; the configuration and questionnaire acknowledgement event retain this revision. X remains an assertion of completion, not proof of a Forms submission. The questionnaires are outside the task and model data.

If removal or electrode movement is necessary, pause and restart as a new session after checking the equipment. On an XR runtime that exposes `userPresence`, a reported absence after preparation latches mount invalidation and pauses the protocol. The operator can also press H on the connected application's keyboard to record headset/electrode movement. Headset presence does not detect electrode shifts; unsupported presence reporting does not imply the mount is unchanged. On standalone, the experimenter must use pause/stop and Left trigger to restart if an adjustment is needed.

The invalidated session cannot resume, execute a pending adjustment or accept a late successful model response. Python also latches the event, blocks subsequent requests including cached decisions, rejects new windows, and saves `mount_integrity.json`. Removing and replacing the headset does not clear this state. X stops; Left trigger / C starts a fresh session with fresh baseline and calibration. The incomplete session remains saved and analysis marks it as mount-interrupted rather than valid Bioadaptive evidence.

The user chose continuous wear instead of the alternative one-minute rewear baseline and offset correction. No automatic feature subtraction or recalibration was implemented. Such a correction would need validation that it handles a nuisance offset without erasing a real state change or hiding bad contact. Oral administration and uninterrupted-wear comfort still need a physical pilot.

## P007 EEG rejection audit

`diagnose_personalized_eeg.py` reconstructs the fifteen EEG-evaluated windows from the original saved raw chunks and contemporaneous Unity/Python clock offsets. All fifteen recorded acceptance fractions were reproduced exactly. The other two of the seventeen candidates were excluded transition/history windows and were not EEG evaluations.

Read-only source inspection confirms a nine-column, 500 Hz Hyperscanner stream whose header declares uV for channels ch0-ch7. The user-selected EEG source columns are 1-4 (zero-based; column 0 is the counter). The receiver selects these columns and applies `scale_to_units=1` once before raw retention. No evidence of an extra 1e6 conversion was found. Header units are not independent physical amplifier qualification.

The gate operates on raw two-second subsegments after subtracting each channel's median, before notch/bandpass filtering. A large DC baseline alone is therefore not the rejection reason. Any selected channel exceeding +/-100 uV or falling below .01 uV standard deviation rejects the subsegment; a window needs at least 50% accepted subsegments. The old selector field `amplitude_limit=300` belongs to the legacy bridge and does not replace the personalized +/-100 uV gate.

| Selected EEG column | Original XDF column | Amplitude failures / 315 window-subsegments | Flatline failures | Single-channel diagnostic window passes / 15 |
|---|---:|---:|---:|---:|
| 0 | 1 | 133 | 0 | 10 |
| 1 | 2 | 244 | 0 | 2 |
| 2 | 3 | 92 | 0 | 13 |
| 3 | 4 | 296 | 0 | 0 |

The 315 subsegments include repeated, overlapping samples and are not independent observations. The fourth selected channel alone fails all fifteen evaluated windows; the second also fails most. The all-channel policy consequently passes zero windows. Anatomical locations remain unknown, so this does not identify a forehead electrode, a headset-pressure cause or a specific artifact source. It does not establish the future live-device rejection rate.

Per-channel amplitude/flatline counts and peak/standard-deviation summaries are now logged for every processed window, while the existing joint acceptance gate is unchanged. No channel was dropped and no threshold was loosened. Single-channel recomputation is diagnostic only; choosing a new montage from this result would require an explicit qualified configuration and new calibration. Live preflight must check the actual amplifier units, physical electrode contact, chosen reference and preselected channels.

For a signal actually expressed in volts, failing to multiply by 1e6 makes a numeric amplitude limit of 100 too permissive; the separate .01 uV flatline test may then reject it. Multiplying already-microvolt data by 1e6 makes the amplitude gate too strict. Tests cover correct conversion, missing conversion, double conversion and large DC offsets.

## Operation and compatibility

1. Stop Play Mode and select **Evidence VR > Stage 2 > Enable dev10 Personalized Engineering Protocol**. A timestamped pre-dev10 override backup is retained. Start a fresh session.
2. Run the current Python service and use the matching v2 policy/configuration. Configure the exact live source, channels and units. The default policy is unfrozen/unqualified with no AUC minimum; ACTIVE does not override admission.
3. Run the unchanged baseline, practice and alternating calibration. Practice has feedback; calibration and training do not. Model preparation now includes 199 full null refits when there is sufficient data. The production-size synthetic check took 591.192 s (about 9:51) on this computer; actual data/hardware and a supplemental attempt can change the duration. The app waits; there is no promised fitting deadline.
4. Administer all three questionnaires orally while the participant keeps the equipment in place. Use [QuestionnaireSetup.md](QuestionnaireSetup.md). Follow the removal/restart rule above.
5. Save Unity and Python data. `analyze_personalized.py` exports the questionnaire administration and mount-interruption status as well as performance/exposure/model outcomes.

Old dev9 configurations, ten-trial policies and fitted artifacts are not silently migrated into v2. Use a new allocated configuration and retrain. The previous APK/ZIP remain historical artifacts. The Stage 1 default without an enabled override is retained.

## Validation and remaining work

Evidence is in `Evidence/PersonalizedValidity/`. The original dirty source worktree is preserved in `pre-dev10-source.zip` and `pre-dev10.patch`. Current test/build results are tracked in [AcceptanceRecord.md](AcceptanceRecord.md); release details are in [PersonalizedValidityRelease.md](PersonalizedValidityRelease.md).

The Unity Pipeline pages used for checks were the installed `commands/scripts.md`, `commands/build-and-compilation.md`, `commands/editor-lifecycle-and-observability.md`, `commands/runtime.md` and `commands/capture.md`.

Pending: live electrode/unit/contact qualification, interpretation of P007 artifacts, score/label and circular-null pilot validity, fixed AUC/score gates, real class availability, model-fit waiting tolerance, oral questionnaire scoring/submission/export rehearsal, media/volume review, genuine Quest Pro presence detection/comfort/eye/standalone acceptance, physical onset synchronization and scientific effects. Software/synthetic/replay results do not pass these checks.

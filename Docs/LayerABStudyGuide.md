# Layer A/B study and timestamp alignment

## Independent stage operation

Stage 2/3 engineering revision, 2026-11-06. Milestones 1–4 are implemented in the Console and the matching Unity source. Use **Study stages** in the [operator workflow](ExperimentGuide.md#independent-study-stages). The earlier allocated study and its protocol retain their own rules. Independent comparisons apply the versioned playback review below. Milestone 5 covers worn-device and participant qualification.

- **Practice** runs the 14-stimulus practice or ends at the skip boundary. Task only skips EEG acquisition for the assigned session. Monitor Mindtooth EEG requires a receiving EEG source.
- **Calibration** offers the existing forest/arithmetic observation, the same task as a rehearsal without EEG, and alternating 2-back calibration blocks for the response-close control RF. The last option can save a candidate when enough usable labeled windows exist. Engineering data retain their engineering status.
- **Adaptive Comparison** defaults to **A/B with a saved model** for a compatible personal workload RF or control RF. The second option runs a task rehearsal without EEG or a model. Select the allocated A/B or B/A order. The same model stays loaded for both 144-trial periods. Session data use the engineering or Editor category throughout these Console assignments. Existing participant qualification and acceptance require separate evidence.

### Playback eye criteria and audit

`pre-response-movie-gaze-v1` is frozen for engineering comparison. For the existing playback-coincident physiological OFF case, intersect three intervals: the elevated EEG window, actual decoded video, and each eligible trial from sphere onset through the first accepted response or omission deadline. Cap at sphere offset and the four-second window. Sum across these intersections. Deferring closure requires at least **1.00 second** of eligible overlap, **70% valid tracked-eye coverage** of that time and **60% task gaze** of valid eye time. The task target is the visible sphere's bounding box in this revision. The ratio criteria reuse the opening rule's engineering values, measured on a different interval. The duration floor prevents a brief isolated sample from deciding a deferral. Their physiological or behavioral validity awaits independent data.

If overlap, coverage or task gaze falls short, record the specific reason and deliver the base OFF. Behavioral OFF keeps its existing path. Post-response looks are displayed separately. Audio-only playback contributes audio evidence only. The operator sees **Video playing** or **Video not playing** from decoded playback events. Stale telemetry has its own status. The ten-trial upgrade check runs in the background.

**Open comparison report** reconstructs the records for the selected session, including after a service restart. It links decision-time values and thresholds, window IDs, review, delivery, receipt and subsequent outcomes. Expand a trial to inspect timestamped gaze before and after the response. Download the same data as JSON. Summary denominators are distinct ON and OFF requests. A deferral episode groups consecutive checkpoints sharing proposal, reason and current level. Missing execution receipts leave execution unknown for a delivered change. Post-upgrade counts use observed scored trials among the next ten positions and retain the denominator.

[Software acceptance](AcceptanceRecord.md#independent-console-stages-and-playback-gaze) · [Synthetic checkpoint evidence](../Evidence/ConsoleStagesEngineering/browser-check.json)

## Planned Console stages and gaze review

Stage 2/3 design revision, 2026-11-06. This section describes proposed work. The operating rules for the current allocation are in the [implemented probe study](ProbeStudyReview.md).

1. **Practice.** Start and finish a standalone practice session. Offer task-only practice and practice with Mindtooth monitoring.
2. **Calibration.** Offer a task rehearsal or acquire qualified Mindtooth EEG for personal calibration. Label rehearsal data separately. Save the participant ID, source, calibration revision and model identity. The [forest and arithmetic classifier](LoadReferenceCalibration.md) provides an arithmetic-class score. The engineering comparison can use this score for control in both periods. Formal comparison requires an admitted response-close control RF.
3. **Adaptive Comparison.** Start a separate comparison session for the same participant. Check model compatibility, current EEG quality, mounting and Quest Pro tracked-eye coverage. Record which physiological score drives the shared controller. Counterbalance A/B order and collect the same gaze, EEG, behavior and playback evidence in both periods. Label runs without qualified physiological or gaze input as rehearsals when qualification is required.

### Decisions during comparison

Keep the shared controller. At each existing five-trial checkpoint it may propose ON from ten-trial balanced accuracy (BA) >= 0.80 or a qualified model score <= 0.45. It may propose OFF from BA <= 0.60, at least three omissions, or a qualified score >= 0.65. A records the review and delivers the base proposal. B may enforce the review. Keep the model, thresholds, task and execution boundaries common to both arms.

| Checkpoint | Planned B decision and evidence |
| --- | --- |
| Before opening a video | Retain the current ON requirements. Use ten eligible current-condition trials, BA >= 0.80, fewer than three omissions, an eligible model score <= 0.45, valid eye coverage >= 70% and task gaze >= 60% of valid eye time. Show tracking loss separately from valid gaze directed elsewhere. |
| During playback | Align decoded playback and sphere visibility with tracked gaze on the sphere, movie or elsewhere. For each scored trial, end the decision-relevant viewing interval at the first accepted response or the omission deadline. Keep later looks separately. Inspect forward from playback onset to subsequent gaze and responses, and backward from each error or omission to preceding gaze and playback. Store the trial IDs and intervals supporting each direction. |
| Proposed OFF | Honor a qualified behavioral OFF even if eyes are unavailable. For a physiology-only OFF, the proposed additional requirement for deferral is usable gaze supporting continued task viewing alongside the existing stable-behavior and playback-coincident score-rise checks. Otherwise deliver the base OFF and identify any missing or conflicting evidence. Show pre-response movie looks associated with errors as supporting observations. Audio exposure and visual attention are separate fields. |
| After opening a video | Retain the ten-trial verification and minimum 15 seconds of actual playback. Attach the gaze timeline, observed BA, omissions, EEG window and results to the original ON request. Separate a failed performance check from an unevaluable check. Display evidence acquired after the decision only in the follow-up. |

### Console evidence and verification

For every checkpoint retain the request ID, stage, arm, policy and model versions, behavioral and physiological triggers, and the actual value, threshold, denominator, time window and status of each evaluated check. Link the base proposal, review recommendation and reason, delivered action, Unity receipt and actual playback interval. Show hypothetical interventions in A separately from enforced interventions in B. Count deferred ON and OFF proposals against their respective totals, distinguish repeated requests from distinct episodes, and show missing or conflicting evidence separately. Retain post-upgrade errors, omissions and actual exposure for each participant.

Before enabling the new OFF rule, specify and freeze the minimum usable pre-response eye coverage and the criterion for continued task viewing during playback. Use paired engineering inputs to check task viewing, pre-response movie viewing, post-response looks, missing eyes, audio-only exposure and behavioral OFF. Change only gaze in a matched pair and verify that only the intended B judgment changes. Check receipt and timeline alignment. Verify gaze targets, dropouts and synchronization on a worn Quest Pro with live Mindtooth EEG before participant acceptance. These steps and the proposed Console layout remain pending.

### Implementation milestones

Plan dated 2026-11-06. The five milestones below organize development and acceptance. Operators see three session entries (Practice, Calibration, Adaptive Comparison). The playback indicator has two values, **Video playing** and **Video not playing**, driven by observed playback events. Keep permission to play media, checkpoint reviews and the ten-trial follow-up in the decision record. An interval between clips can therefore show Video not playing while the controller permits distraction.

| Milestone | Expected result | Deliverables | Acceptance check |
| --- | --- | --- | --- |
| 1. Independent session entry and model selection | An operator can run just the intended stage and return later with the same participant ID. The operator can identify the score selected for A/B. | Three English Console entries, stage-specific start and finish flows, task-only and monitored practice, rehearsal and acquisition choices for calibration, and a comparison setup showing session ID, model identity, source, compatibility and qualification. | In an isolated engineering run, start and end each stage separately. Restart the Console, select the same participant and verify the recorded model identity. Reject a wrong-participant or incompatible model for formal comparison. Keep observational forest/arithmetic scores and rehearsal records identifiable. |
| 2. Playback and eye evidence per trial | Both arms reconstruct what the participant could see, where the eyes were directed and whether the look preceded the response. | Two-state playback indicator, synchronized trial timeline and linked event records for sphere visibility, decoded playback, tracked gaze, accepted response or omission deadline, and EEG window. Report valid eye coverage and target time with their denominators. | Use timed fixtures covering gaze on the sphere, on the movie and elsewhere, post-response looks, tracking loss, an omitted response, a gap between clips and audio without visual exposure. Compare expected intervals with exported intervals. Exclude future observations from each checkpoint. |
| 3. A/B decisions and playback gaze criteria | Identical inputs produce identical base proposals. A delivers the base action. B enforces the specified review, including gaze for a physiology-only OFF deferral. | Versioned decision table, explicit reason codes, documented pre-response coverage and task-viewing criteria, and paired replay traces covering ON, OFF and the ten-trial verification. | With all other evidence fixed, change only gaze in the physiology-only OFF case. Verify A delivers OFF in both cases, while B defers with sufficient task viewing and delivers OFF with missing gaze. Verify behavioral OFF, post-response looks, and passed, failed and unevaluable upgrade checks separately. Freeze the proposed playback criteria before running the paired acceptance set and record their engineering or pilot basis. |
| 4. Operator audit and comparison report | An operator can explain a single decision and reproduce the session totals from archived records. | Clickable checkpoint list, values and thresholds for each evaluated check, linked timeline, original proposal, review, delivered action, Unity receipt, playback and follow-up. Provide separate ON/OFF counts with proposal totals, missing evidence counts, actual exposure and post-upgrade outcomes. | Trace an allowed proposal, a hypothetical A intervention, an enforced B intervention and a missing receipt to source IDs. Reconcile the summary with the request log. Show unknown execution when a receipt is missing. Keep evidence obtained after execution in the follow-up view. |
| 5. Worn hardware rehearsal and acceptance | The operator can see which parts of the complete workflow are software-verified, physically verified or still open. | Quest Pro and Mindtooth rehearsal records, signal and clock checks, annotated session export, updated operating instructions and acceptance record. | Check actual EEG rate, units, contact, gaze on known targets, tracking loss and recovery, playback and event alignment on the worn setup. Exercise separate stage starts and a complete A/B engineering comparison. Record discrepancies and retest. Participant use requires the relevant device, signal and personal-model qualifications. |

The table records the original milestone criteria. Current software results appear in [independent stage operation](#independent-stage-operation). Worn Quest Pro and Mindtooth acceptance remains pending. Version any rule changes and repeat the affected paired tests before participant use.

## Current A/B allocation

Use `layer-ab-probe-v2` for new sessions. Generate a fresh allocation, install the 20-second media pack, restart the Python bridge and load the matching Unity configuration. Each training period has 144 stimuli. Both arms use the same OR thresholds. B reviews every proposed upgrade and verifies it after ten new trials. [Current setup, decision table, outcome definitions and pending acceptance](ProbeStudyReview.md). Earlier 96-trial counts, thresholds and mock plans below are retained for the corresponding study versions.

**Historical 96-trial allocations:** `sliding-five-close-v1` retains behavioral and physiological history across ordinary level changes. Behavior uses the response just closed, while physiology uses the latest fully acquired, compatible five-trial window (one trial behind). Recalibrate with the new revision; start the updated Python service and updated Unity source/player. Supersedes the previous switch-capacity estimates only for the new allocations. [Operation, timing and validation](SlidingControlReview.md).


Date: 2026-11-05 (work began 2026-11-04). Branch: `codex/layer-ab-timing-study`.
Study revision: `layer-ab-v1`. Questionnaire revision: `raw-tlx7-ueq4-preference-google-forms-v4`.
This is an implemented, software-tested experimental candidate. It is not participant efficacy or physical timing acceptance.

## Approved plan for OFF annotation and subsequent ON review

2026-11-05, Stage 2/3 study plan. Both arms already allow OFF through the Guard. The following annotation and its use in subsequent ON review are planned work.

1. Allow each OFF in A and B under the existing execution timing. Record the source annotation separately from authorization so an incomplete review never delays the reduction. Link the OFF proposal and its execution receipt.
2. For every OFF, check forward from actual playback to the overlapping behavioral and physiological evidence. Check backward from the specific evidence supporting OFF to preceding playback and task events. Record whether difficulty occurred predominantly before playback or during playback, with timestamps, window boundaries and supporting event IDs. Record mixed or insufficient evidence explicitly. Timing alone establishes a temporal association.
3. At a later ON proposal in the same condition, link the relevant executed OFF and its annotation. If difficulty predominantly coincided with playback, subsequent stability in L0 alone cannot establish tolerance of L1. A logs the review and executes the controller proposal. B defers an ON supported only by that L0 recovery. An independently sufficient, applicable authorization path may still support ON. First ON has no prior OFF to consult. Clear this history between conditions.
4. Before implementation, specify the predominance criterion, how to handle ambiguous evidence, and what independent evidence can qualify a later ON while the user is still in L0. Test identical inputs in A and B for both pre-playback and during-playback difficulty. Verify equal OFF timing, inspect each linked ON verdict and its evidence, and include a case where reopening is warranted.

The one-checkpoint wait to give the user a recovery opportunity belongs to a separate next-stage training-strategy comparison. Its existing mock results are documented in the [replay review](GinGuardReplayReview.md).

## Isolation and comparison

The existing `NBackExperiment` scene is reused to keep presentation, geometry and assets identical. A separate allocated configuration opts into the study; existing configurations default to `studyDesign=legacy`. The previous Random/Bioadaptive protocol remains available. No APK was built or installed in this update.

Both conditions use the same personalized adaptive controller, calibration/model, thresholds and execution rules. `layer_a` is A (guard observe, no influence on actions); `layer_b` is B (guard enforce). There is no Random condition in this study. Distinct stage identities prevent trial/prediction/request collisions. Both conditions start at L0 and reset online histories; the personal model is retained without refitting. The same guard processes both conditions for comparable telemetry; only its execution authority changes. Qualification, mount-integrity, actual-exposure, safe-boundary and signal gates remain active. An unavailable model remains explicit behavioral fallback, not admitted physiology.

The allocation has four cells: A/B versus B/A, crossed with calibration starting L0/L1. Seeds, stimuli, media selection and actual exposures remain recorded. Stimulus/media sequences differ across periods under the retained seeded generator and are not exactly replay-yoked. Counterbalance period order and inspect actual exposure; do not claim equal delivered distraction dose.

Flow: preparation and connection checks -> optional practice -> 48-trial calibration (one 24-trial supplement if needed) -> model preparation -> Training 1 (96 trials) -> short questionnaire -> 180-second rest after the experimenter confirms Google Forms submission in Unity -> Training 2 (96 trials) -> short questionnaire and final preference -> completion. There is no calibration questionnaire in this variant. Four-second response/visibility, 4.5-second cycle and five-trial decisions are retained. No initial resting-reference acquisition is restored.

## Start an engineering rehearsal

1. Select and verify Mindtooth EEG sources, channels and units using the existing source-selection workflow. The saved selection is `UserSettings/lsl-source-selection.json`. Check the saved policy and actual source provenance. Quest Pro remains the target for genuine tracked eyes.
2. Run `StartLayerABStudy.cmd --engineering`. This starts a separate service on **8766**, writes to `D:\ExperimentData\LayerAB`, and opens the post-condition Google Form and prints both Forms links. It does not start a task or replace the existing 8765 service. Omit `--engineering` only for live-source operation; that flag permits replay at launcher validation, not physiological admission. The allocation's session type still controls the recording category.
3. In Unity, while stopped, choose **Evidence VR > Layer A-B study > Load allocated configuration**. For a rehearsal choose one of `Tools/LayerAB/Examples/AB_ENGINEERING_01` through `04` configurations. They cover the four balanced cells and are engineering examples only. The previous startup configuration is backed up before loading.
4. Start LabRecorder on the study computer and select **the Mindtooth EEG outlet and `EvidenceVR_TaskEvents`**. Check its `source_id` against the launcher output. Recording the Mindtooth EEG streams alone does not include task markers. The marker outlet is published when the new service starts, before Unity enters Play.
5. Enter Play Mode. Complete seating and response-control checks. A/B additionally requires a recent software clock exchange before leaving preparation. Recordings in Editor are always `editor-test`. A signal/clock connection is not device or scientific qualification.
6. At each scheduled break, the experimenter enters the exact participant code, session ID and Training 1/2 occasion in the post-condition Google Form and records six workload ratings on page 1, then four overall impression ratings on page 2. Submit after both pages are complete. After Training 2, also submit the separate preference form. After successful submission, press X/Enter in Unity. The first confirmation starts 180 seconds of rest; press again after it expires. Final confirmation completes the task. Unity records operator acknowledgement, not a verified Google receipt. The experimenter keeps headset/electrodes in place and records any necessary interruption. Removal/movement invalidates the existing physiological session as before.
7. End recording after the final task events have reached the service. Preserve **both** Unity and Python session folders, the configuration/allocation, and the XDF. Stop Play before changing startup mode. **Evidence VR > Layer A-B study > Restore previous configuration** returns to the backed-up startup mode and retains data.

For standalone Quest, `127.0.0.1` means the headset, unless an explicit ADB reverse tunnel is configured. Use the study computer's reachable address and `--host 0.0.0.0` only for the intended LAN route. New code requires a new build; existing APKs do not implement this protocol. Google Forms requires internet access in the experimenter browser. The local questionnaire page and its GET/POST endpoints are removed; the controller and clock service remain necessary.

The launcher refuses an occupied port instead of stopping another process. Its PID/log are printed on success. After stopping Unity and saving the recording, stop that specific service process if it is no longer needed; the original console service is independent.

## New participant allocation

Use a new enrollment index and pseudonymous code; retain the allocation file. The allocator refuses to overwrite existing participant files. Do not use an engineering example as a participant allocation.

```powershell
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe Tools/AdaptiveTraining/allocate_personalized.py --layer-ab --participant P001 --index 0 --allocation-seed 20261105 --controller-url http://127.0.0.1:8766 --output D:\ExperimentData\LayerAB\Allocations
```

This command creates an allocation, not scientific/device authorization. Run a qualified participant-specific calibration; do not reuse Gin/synthetic candidate models. For an engineering allocation add `--engineering`. Operator-managed saved-model shortcuts are disabled in the A/B variant.

## Short questionnaires

Both conditions use the same six Raw NASA-TLX dimensions, rated 1–7 using horizontal single-choice scales. Performance is Perfect=1 to Failure=7; all other dimensions are Very low=1 to Very high=7. Calculate the unweighted mean from six complete exported ratings; missing values remain missing. Preserve the original dimension definitions and introduce them consistently during preparation; do not improvise explanations between conditions. Oral administration and seven-point entry are adapted procedures requiring pilot review, not claimed equivalent to the original interface. [NASA instrument resources](https://www.nasa.gov/human-systems-integration-division/nasa-task-load-index-tlx/).

The second page contains the four standard UEQ+ Attractiveness pairs: annoying/enjoyable, bad/good, unpleasant/pleasant, and unfriendly/friendly. Use seven response positions (1 = left endpoint, 7 = right endpoint). Recode each complete response as rating minus 4, then average the four items (-3 to +3; higher is a more positive overall impression). Keep this score separate from Raw TLX. No importance/KPI question is included. Both conditions receive the same two pages immediately after their training. See [instrument sources, permission evidence and form verification](LayerABGoogleForms.md).

After both conditions, preference is Training 1 / Training 2 / No preference, with an optional reason. Neither the participant-facing task nor the rating questions identify A/B or claim superiority. Keep brief verbal comfort checks and record reported discomfort, interruptions or skipped checks in session notes; this is not an extra questionnaire item or scored scale. IMI, the three adjustment-experience items, repeated SSQ and the previous calibration ratings are omitted for this variant. Existing historical Google Forms were not edited or deleted.

Responses are stored only in Google Forms. There is no local answer writer or receipt polling. See [Google Forms setup](LayerABGoogleForms.md) and `Tools/LayerAB/questionnaire-links.json` for the two new forms. Export responses separately, joining participant code + exact session ID + Training occasion; map period/preference to A/B using the saved allocation. Original legacy forms are unchanged.

Unity confirmations explicitly retain `submission_verified=false`. They timestamp operator acknowledgement, not individual answers or verified Google receipt. Google wall-clock timestamps are not LSL timestamps. Preserve task button edges, onset, RT and clock mapping for physiological alignment. If submission fails, remain at the break or abort and document the reason. Never fill missing answers with neutral scores. Old questionnaire-revision allocations require fresh allocation for a new session; do not relabel recorded data.

## What the timing files mean

| Record | Meaning |
|---|---|
| `trials.csv`: `actual_onset_s` | Unity software stimulus-on event time, before physical display presentation |
| `response_time_s`, `rt_s` | Existing first accepted response processing time and RT; scoring unchanged |
| `input_event_id`, `input_event_time_s`, `input_rt_s` | Link to the first performed input-action edge observed in the scoring frame, its event timestamp, and event time minus software onset |
| `input_events.jsonl` | Every observed task/navigation action performed/canceled edge, control path, timestamp, callback time, frame, stage/trial context, and paired hold duration when available |
| `events.jsonl` | Original stimulus, accepted/rejected response, pause, media, execution and stage events, plus input edges and condition/ordinal context |
| `clock_sync_ab.jsonl` | Four timestamps for each clock exchange, clock-process ID, offset, network-delay estimate and coincident Python/LSL clock reads |
| Python `aligned_events.jsonl` | Event time mapped to Python monotonic time, qualification status, uncertainty, optional marker timestamp and publication result |
| Python `raw_signals.jsonl` | Mapped/scaled selected signal samples, original source LSL timestamps, inlet correction, corrected Python times and LSL-to-Python offset |

Input System `CallbackContext.time` uses the Unity realtime time domain. `callbackTime-inputTime` exposes callback delay; it is not hardware switch latency. Performed/canceled events describe the configured actions: cancellation can also result from device loss/disable, and must not automatically be interpreted as a physical release. All edges remain available even when a frame-polled response is ignored, simultaneous, duplicate, warmup or outside the scoring window. Multiple same-frame presses are retained; only the first matching performed edge is linked to that frame's accepted response. Missing links and missing responses retain explicit `-1` values. The frame-based and event-based RTs are different measures; do not silently replace one with the other.

Clock transfer uses:

```
offset = ((server_receive - unity_send) + (server_send - unity_receive)) / 2
network_uncertainty = ((unity_receive - unity_send) - (server_send - server_receive)) / 2
event_python_time = event_unity_time + offset
```

Dedicated `/clock` requests exclude server processing duration from the network estimate. Each accepted sample is saved; the online mapping uses a low-delay recent sample and refuses stale/high-uncertainty physiological windows. The engineering flags require at most 50 ms estimated uncertainty and 5 seconds of clock distance. This is not a physical accuracy guarantee. Network asymmetry, device timing, drift and signal acquisition delay still need measurement.

When enabled with `--lsl-markers`, the service publishes explicitly timestamped JSON events to `EvidenceVR_TaskEvents`. It maps the **original event time**, not HTTP arrival time, to the service-host LSL clock. Missing, stale or uncertain mappings remain recorded but are not published as qualified timed markers. A successful `push_sample` is not proof LabRecorder received or saved it. Inspect the actual XDF.

LSL sources can have different raw clock domains. `raw_signals.jsonl`'s `corrected_monotonic` and exported `*_event_perf_s` share the Python service clock. For XDF, use the recorded marker stream and the importer's consistent clock correction for all streams; do not directly compare arbitrary raw source timestamps or mix corrected and uncorrected times. [LSL synchronization documentation](https://labstreaminglayer.readthedocs.io/info/time_synchronization.html).

## Export aligned data

Supply matching Unity and service session IDs and a **new** output directory. Originals are not edited.

```powershell
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe Tools/AdaptiveTraining/export_ab_timing.py --unity-session "C:\path\AdaptiveTrainingData\participant\SESSION_ID" --service-session "D:\ExperimentData\LayerAB\participant\SESSION_ID" --output "D:\ExperimentData\LayerAB\Analysis\SESSION_ID"
```

Outputs: `events_aligned.csv`, `trials_aligned.csv`, `questionnaire_acknowledgements.csv`, `timing_report.json`. Offline alignment selects the nearest recorded clock sample, retains qualification flags and original RT, and leaves absent times unavailable. The LSL-local column is the service-host LSL clock, not an arbitrary XDF importer's normalized time. Multiple service clock IDs cause a review-required failure. `analyze_personalized.py` also recognizes `layer_a/layer_b` for descriptive performance/exposure summaries.

Questionnaires, pauses and preparation are separately marked and must be excluded from task physiological windows. Preserve failed/interrupted runs and missing-questionnaire flags. Report actual exposure, valid-data fraction, fallback/model status, order, HOLD reasons and paired participant differences. Shortened questionnaires cannot establish all dimensions of user experience; Raw TLX measures workload, and lower load alone does not establish training efficacy.

## Current Google Forms revision

Current changes and checks: [Google Forms review](LayerABGoogleForms.md). The local questionnaire implementation in the historical evidence below is superseded.

## Original A/B software evidence and pending acceptance

- Python: **165 tests passed**, including eight new A/B tests, real localhost HTTP questionnaire submission/receipt, immutable retry, four-stamp timing, missing responses and aligned export.
- Unity: compilation passed; **3,906 synthetic Play Mode checks passed**, including all four allocation cells, complete two-period budgets, questionnaire/rest gates, guard authorization and actual Input System press/release timestamp capture. The PASS log was read back. Console had zero errors; existing warnings remain. Play Mode is stopped and auto-tick remains at its prior enabled setting.
- Actual Unity-to-Python preparation handshake passed in an isolated `editor-test` session; the synthetic timing probe arrived with its original timestamp and estimated mapping. No scored participant session occurred. The original startup configuration was restored byte-for-byte afterward.
- A real local liblsl outlet/inlet loopback preserved the explicit marker timestamp exactly in that test and checked a known synthetic clock offset. This is local transport evidence, not Quest/Wi-Fi/amplifier or physical-button accuracy. Details: `Evidence/LayerAB/lsl-loopback.txt` and `LslLoopback/*/result.json`.
- Short questionnaire page visually inspected in a narrow browser panel using a synthetic fixture; all six dimensions, final preference, optional reason and operator check rendered. Submission was verified through the real HTTP handler in automated tests. The preview service was stopped.

Evidence: `Evidence/LayerAB/python-regression.txt`, `unity-validation.txt`, `bridge-validation.txt`, `bridge-event.json`, synthetic input audit folders. Validation scripts: `AgentScripts/LayerABValidation.cs`, `validate_layer_ab_lsl.py`, `serve_layer_ab_fixture.py` (removed in the Google Forms revision).

Pending: Quest Pro gaze/permission/comfort, physical controller and stimulus timing, LSL/LabRecorder XDF capture on the intended network, actual Mindtooth EEG source acceptance and personal model admission, full real-time A/B rehearsal, questionnaire comprehension and pilot action/coverage rates, participant effects. No thresholds were weakened to generate differences; no efficacy or causal benefit has been established.

Restore point: `Evidence/LayerAB/pre-layer-ab.zip` contains 130 pre-existing modified/untracked project files (excluding untouched `Recordings/`), with a manifest, and `pre-layer-ab.diff` retains the pre-task tracked diff. The branch began from `main` with those uncommitted changes already present. They are now preserved in the local restore branch `codex/pre-layer-ab-20261105` at `cfae28c8aa172983380e54eecb0871969fd0f6fd`; the A/B changes are committed separately on `codex/layer-ab-timing-study`. `main` was not moved. No push or merge was performed. Stop Play and restore the startup configuration before switching back to the restore branch. Retain unrelated untracked recordings.

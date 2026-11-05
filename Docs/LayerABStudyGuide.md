# Layer A/B study and timestamp alignment

Date: 2026-11-05 (work began 2026-11-04). Branch: `codex/layer-ab-timing-study`.
Study revision: `layer-ab-v1`. Questionnaire revision: `raw-tlx7-ueq4-preference-google-forms-v4`.
This is an implemented, software-tested experimental candidate. It is not participant efficacy or physical timing acceptance.

## Isolation and comparison

The existing `NBackExperiment` scene is reused to keep presentation, geometry and assets identical. A separate allocated configuration opts into the study; existing configurations default to `studyDesign=legacy`. The previous Random/Bioadaptive protocol remains available. No APK was built or installed in this update.

Both conditions use the same personalized adaptive controller, calibration/model, thresholds and execution rules. `layer_a` is A (guard observe, no influence on actions); `layer_b` is B (guard enforce). There is no Random condition in this study. Distinct stage identities prevent trial/prediction/request collisions. Both conditions start at L0 and reset online histories; the personal model is retained without refitting. The same guard processes both conditions for comparable telemetry; only its execution authority changes. Qualification, mount-integrity, actual-exposure, safe-boundary and signal gates remain active. An unavailable model remains explicit behavioral fallback, not admitted physiology.

The allocation has four cells: A/B versus B/A, crossed with calibration starting L0/L1. Seeds, stimuli, media selection and actual exposures remain recorded. Stimulus/media sequences differ across periods under the retained seeded generator and are not exactly replay-yoked. Counterbalance period order and inspect actual exposure; do not claim equal delivered distraction dose.

Flow: preparation and connection checks -> optional practice -> 48-trial calibration (one 24-trial supplement if needed) -> model preparation -> Training 1 (96 trials) -> short questionnaire -> 180-second rest after the experimenter confirms Google Forms submission in Unity -> Training 2 (96 trials) -> short questionnaire and final preference -> completion. There is no calibration questionnaire in this variant. Four-second response/visibility, 4.5-second cycle and five-trial decisions are retained. No initial resting-reference acquisition is restored.

## Start an engineering rehearsal

1. Select and verify EEG/ECG sources, channels and units using the existing source-selection workflow. The saved selection is `UserSettings/lsl-source-selection.json`. Check the saved policy and actual source provenance. Quest Pro remains the target for genuine tracked eyes.
2. Run `StartLayerABStudy.cmd --engineering`. This starts a separate service on **8766**, writes to `D:\ExperimentData\LayerAB`, and opens the post-condition Google Form and prints both Forms links. It does not start a task or replace the existing 8765 service. Omit `--engineering` only for live-source operation; that flag permits replay at launcher validation, not physiological admission. The allocation's session type still controls the recording category.
3. In Unity, while stopped, choose **Evidence VR > Layer A-B study > Load allocated configuration**. For a rehearsal choose one of `Tools/LayerAB/Examples/AB_ENGINEERING_01` through `04` configurations. They cover the four balanced cells and are engineering examples only. The previous startup configuration is backed up before loading.
4. Start LabRecorder on the study computer and select **both physiological outlets and `EvidenceVR_TaskEvents`**. Check its `source_id` against the launcher output. Recording the EEG/ECG streams alone does not include task markers. The marker outlet is published when the new service starts, before Unity enters Play.
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

Pending: Quest Pro gaze/permission/comfort, physical controller and stimulus timing, LSL/LabRecorder XDF capture on the intended network, actual EEG/ECG source acceptance and personal model admission, full real-time A/B rehearsal, questionnaire comprehension and pilot action/coverage rates, participant effects. No thresholds were weakened to generate differences; no efficacy or causal benefit has been established.

Restore point: `Evidence/LayerAB/pre-layer-ab.zip` contains 130 pre-existing modified/untracked project files (excluding untouched `Recordings/`), with a manifest, and `pre-layer-ab.diff` retains the pre-task tracked diff. The branch began from `main` with those uncommitted changes already present. They are now preserved in the local restore branch `codex/pre-layer-ab-20261105` at `cfae28c8aa172983380e54eecb0871969fd0f6fd`; the A/B changes are committed separately on `codex/layer-ab-timing-study`. `main` was not moved. No push or merge was performed. Stop Play and restore the startup configuration before switching back to the restore branch. Retain unrelated untracked recordings.

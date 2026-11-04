# Filtered signal quality and Gin reprocessing

Date: 2026-11-03. Revision: `trial-aligned-eeg-ecg-v3-filtered-quality`.
Stage: Python preprocessing and engineering model reuse. User requested optimization after all online Gin predictions were missing.

**Operation update:** The later [unified console entry](UnifiedConsoleReview.md) supersedes the port-8877/StartGinTestConsole instructions below. Use only **StartEvidenceConsole.cmd** and **8765**. The model, recorded validation results and pending live-run boundary below are unchanged.

## Changes and rationale

The previous EEG gate tested median-centered **raw** amplitude against 100 uV even though features used 50 Hz notch and 1-40 Hz band-pass filtered samples. In the saved 24-second Gin probe, all four selected channels had their largest spectral peak at 50 Hz. Typical raw centered peaks were 267-824 uV; after the existing filters they were 14-29 uV. This supported separating removable interference from residual artifact screening, rather than increasing the amplitude limit.

V3 retains the filters, two-second Welch subsegments with one-second stride, all-selected-channel rule, 100 uV residual peak limit, 0.01 uV minimum standard deviation and at least 50% usable subsegments. Amplitude now refers to median-centered filtered samples. Flatline checks use both raw and filtered standard deviation. Additional raw checks reject repeated samples lasting 100 ms, exact extrema plateaus lasting 20 ms, and jumps over 1000 uV per sample. These are explicit pilot integrity heuristics, not calibrated knowledge of amplifier ADC rails. Raw/filtered amplitudes and rejection counts are logged separately. No input samples are overwritten, channels silently dropped, or failed windows imputed.

ECG retains 5-25 Hz filtering, 300 ms refractory distance, at least eight RR intervals, 0.3-2 s RR bounds, 80% valid intervals and two-second edge coverage. A 100 ms RMS envelope integrates the biphasic QRS before peak detection instead of taking its instantaneous absolute value. RR deviation remains 20%, but its reference is the local median of up to five intervals instead of one median over the entire window. This allows gradual rate variation and still flags isolated missing beats. Peak times, intervals, validity masks and coverage are audited. No RR correction or fabricated beats are inserted.

The implementation processes completed 22.5-second task windows. It uses forward/backward filtering within that completed window, not a sample-by-sample causal filter. Window-edge effects, noisy ECG and device-specific detection accuracy still require validation against annotated recordings. The retained physiological feature list is log relative theta, log relative alpha and mean HR, plus exposure context; no HRV claim is added.

Method references: [MNE filtering tutorial](https://mne.tools/dev/auto_tutorials/preprocessing/30_filtering_resampling.html) describes notch removal of power-line components; [WFDB processing documentation](https://wfdb.readthedocs.io/en/latest/processing.html) describes integration and refractory checks for QRS detection. V3 is a smaller custom pilot implementation, not WFDB XQRS, and those references do not validate its thresholds or this Gin montage.

## Compatibility and model

Feature revision is recorded in session protocols, extracted windows, model reports and registry manifests. Model loading checks it before deserialization and checks artifact contents afterwards. Old or missing revisions require retraining; old artifacts remain intact. Console health and launcher compatibility also check the processing revision so an old running Python process is not silently reused.

`train_gin_simulation.py` now uses the normal `train_personal` path on quality-qualified windows only. The earlier diagnostic fallback that fitted rejected windows has been removed from this script. The unchanged capture and generated response files are hash-recorded. Model `20261103T184931_8f2739fb95e8-1` is saved under `D:/ExperimentData/PatentDemo/SIM_GIN_TEST/models/`. It uses 43 windows, with 24 Stable and 19 Struggling synthetic labels; registry-reloaded predictions are identical. It remains **unvalidated**, with replay/synthetic provenance, unfrozen policy/device checks and failed/unavailable admission evidence. Generated labels do not establish a physiological relationship. No model-efficacy claim follows from the available held-out engineering metrics.

## Verification

- Identical 436-second capture, 48 candidate windows: old EEG/ECG jointly eligible **0**, new **43**. All five missing-sample/timestamp-gap windows remain rejected. Old ECG alone passed three windows; new EEG and ECG each passed all 43 temporally complete windows. Maximum measured combined extraction time was about 37 ms per 22.5-second window on this computer; this is a single diagnostic run, not a performance guarantee.
- Independent synthetic tests cover removable line noise with preserved band features, unchanged input arrays, in-band artifacts, clipping, dropouts, impulses, flatline, wrong units, nonfinite values, timestamp gaps, known beat counts during changing rate, polarity inversion, missing beats and coverage. Missing/old model revisions are rejected before joblib loading.
- Full Python regression: **154 tests passed** in 33.711 seconds. After the subsequent launcher health-revision check, the affected console tests were rerun separately; see `console-tests.txt`.
- Prior actual Unity run `20261103T183828_c4d88eae4bbc` was reprocessed **offline**, merging its recorded chunks and telemetry in original receive order through `PersonalSession.ingest/receive/process_windows/decide`. No device actions were sent. Of 23 windows, **20 now produced scores**, range **0.285985-0.761120**; three incomplete/mixed windows stayed invalid. At trial 25, five recent predictions averaged **0.507258**. Behavior was Stable, but this score lies between 0.45 and 0.65, so HOLD remained correct. Engineering-model eligibility was true; physiological eligibility/admission remained false. This is not a new Unity run.

Evidence: `Evidence/SignalQualityV3/offline-comparison.json`, `trained-model.json`, `training.log`, `python-tests.txt`, `console-tests.txt`, `offline-runtime-result.json`, `offline-runtime/`, and `quality-and-predictions.png`. Restore snapshot: `Evidence/SignalQualityV3/before.zip`, including the previously uncommitted files as they existed before this update. Original raw captures, source sessions and models remain intact.

## Operation and pending checks

The isolated Gin service at port **8877** was restarted with V3 and the newly trained model's mapping/policy. No active test assignment was created. The normal port-8765 monitor and Gin publisher were left running; 8765 still needs an orderly restart before using updated personalized processing. Do not restart an active participant session. No APK, scene, task timing, scoring, level rules or control thresholds changed.

Automatic approval review rejected the combined request to create a new test assignment and enter Unity Play Mode, returning only `blocked by policy`. The action was not retried through another route. Consequently, V3 live Unity execution, physical inputs, actual adaptive switches and Quest Pro acceptance are **pending**. Unity is left out of Play Mode; auto-tick remains enabled as before.

For a manual test, open `StartGinTestConsole.cmd`, select ID **SIM_GIN_TEST**, root **D:/ExperimentData/PatentDemo**, mode **Bioadaptive test**, model **20261103T184931_8f2739fb95e8-1**, and **Allow unvalidated candidate model**. Enter Unity Play Mode with the existing port-8877 operator bootstrap, press Start Session on the page and complete the normal preparation controls. Candidate opt-in does not bypass the revised quality, timing, history or execution gates. This remains an engineering replay test, not participant calibration.

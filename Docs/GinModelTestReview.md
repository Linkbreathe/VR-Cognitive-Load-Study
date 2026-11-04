# Gin recorded-signal model-loading test

**Historical V1 record:** Current preprocessing, model and operation are superseded by [Signal quality V3](SignalQualityV3Review.md). The original model and all results below are retained; the current `trained-model.json` launcher pointer selects the new model, while V1 metadata is also preserved in `Evidence/SignalQualityV3/before.zip` and its original source session.

Date: 2026-11-03. Revision: `gin-recorded-signal-simulation-v1`. Engineering test only.

## Source and generated behavior

The user explicitly selected `Gin_mock_p007_test`, outlet UID `660660ea-f206-4a10-9a59-40e37e119074`. Its metadata identifies original stream `02_007_5_eeg`, Hyperscanner, nine columns at 500 Hz, source file `sub-P007_ses-S002_active.xdf`, recorded replay, speed 1 and preserved original timestamps. This is not the earlier local `sub-P007.xdf`. Original XML is retained in `Evidence/GinModelTest/source.xml` and the capture folder.

Column 0 is a sample counter. The retained project montage uses EEG columns 1-4 and differential ECG column 7 minus 8. Header-declared units for columns 1-8 are uV, with scale 1. Anatomical sites and physical-device validity remain unconfirmed. Both processing roles remain recorded replay; they are not live physiology.

Behavior comes unchanged from `D:/ExperimentData/PatentDemo/Simulations/SourcePending/20261103T182001_964d733b/`: 96 slots, eight alternating 12-slot blocks, 432 simulated seconds and 48 five-trial labels (24 Stable/24 Struggling). The source clock is offset to its first captured sample for offline pairing. These generated responses and generated exposure values were not observed during the original recording and do not establish a physiological relationship.

## Strict result versus diagnostic fit

The initial 24-second probe failed strict EEG amplitude rejection on all four selected columns and ECG RR consistency. Production EEG/ECG extraction and control gates are unchanged. Each training window retains the exact strict quality result. The standard `train_personal` result is saved separately in `strict-quality-attempt/model_report.json`.

To fulfill the requested model-loading test, a separate diagnostic fit uses finite log relative theta/alpha and bounded RR-derived mean-HR descriptors, including descriptors from rejected-quality windows. This is explicitly recorded in the model report. It does not turn those windows into eligible controller observations. The fourth feature, movie fraction, is generated protocol context. Features are never replaced with fabricated numeric physiology.

The saved pipeline is the existing StandardScaler plus a 300-tree random forest, depth 3 and minimum leaf size 5. Its identity is `SIM_GIN_TEST`. Admission is false, and the reasons include synthetic responses, diagnostic fit outside training quality qualification, recorded provenance and no scientific admission evaluation. No AUC or permutation claim is made for this diagnostic artifact. Save/load checks compare predictions exactly.

## Use and records

Run `StartGinTestConsole.cmd` for the separate test service at `http://127.0.0.1:8877/tests`. The existing source monitor at port 8765 is independent. The isolated service loads the capture's explicit EEG/ECG mapping and a matching non-admitting policy. Select ID `SIM_GIN_TEST`, root `D:/ExperimentData/PatentDemo`, Bioadaptive test, its saved model and **Allow unvalidated candidate model**.

Exact model/session paths are recorded in `Evidence/GinModelTest/trained-model.json`. Raw captured samples and original timestamps are in `D:/ExperimentData/PatentDemo/SIM_GIN_TEST/captures/gin-20261103/raw-replay.npz`; the companion capture manifest records identity, SHA-256 and chunk arrival times. Training records, responses, window diagnostics, reports, mapping and policy are saved under the model's source session. The normal local registry holds the complete joblib model, report and compatibility manifest.

The Editor bootstrap for this test uses port 8877. Its previous configuration is backed up at `Evidence/GinModelTest/editor-override-before.json`. Keep the current Gin publisher running. If it restarts, re-scan and explicitly update the test mapping for the new outlet identity before use; matching a model's numeric feature contract does not authorize following an unknown publisher.

The replay preserves old source timestamps, so normal live task-window alignment may reject the incoming data. Candidate loading does not waive timing, signal quality, new-history, behavior or execution gates. HOLD is an expected possible outcome; successful loading does not demonstrate usable adaptive control or model efficacy.

## Validation status

Captured 217,904 nine-column samples over approximately 436 seconds. Of the 48 paired behavioral windows, 43 had finite diagnostic descriptors; five were excluded for missing samples/timestamp gaps. Strict quality eligible windows: zero. The diagnostic fit used 24 Stable and 19 Struggling generated labels. No failed window was relabeled as quality-qualified.

Saved model: `D:/ExperimentData/PatentDemo/SIM_GIN_TEST/models/20261103T183721_e718f9a0f3d8-1/model.joblib`. Source session: `20261103T183721_e718f9a0f3d8`. All fitted prediction outputs were identical after registry reload. Descriptor checks also verified that input samples remain unchanged and strict EEG rejection remains in force.

Unity test session `20261103T183828_c4d88eae4bbc` successfully acknowledged and loaded `engineering_candidate_loaded`, entered Bioadaptive directly and completed 26 real-time slots in approximately 117 seconds. The helper then aborted the next slot: the CSV contains 27 rows, including one aborted row. The helper is restricted to `SIM_GIN_TEST` and records generated responses/programmatic preparation advance explicitly; it does not assert physical input acceptance.

Five control requests returned HOLD. At trial 25, the twenty-trial behavior window was valid and Stable (BA 0.8333, three omissions), but there were zero valid recent model predictions. The result retained `basis=engineering_candidate_rf`, `physiologicalEligible=false`, `engineeringModelEligible=false`, and `reason=fewer_than_three_recent_pure_condition_predictions`. Incoming replay windows reached signal extraction and were rejected by quality checks; timestamp preservation did not prevent extraction in this observed run.

The session is correctly aborted, not a completed 96-slot experiment. All five core Unity files reached `complete_core_records` and were byte-identical to the local files. Evidence: `trained-model.json`, `training.log`, `probe-quality.json`, `unity-exercise-command.json`, `runtime-result.json` and `runtime-window-summary.json` in `Evidence/GinModelTest/`. A screenshot request queued behind the long-running exercise timed out without executing; this was a capture-command scheduling issue, not a task failure. The final browser configuration was inspected separately.

The Editor is left in Play Mode at the ended-test/waiting screen, with no active assignment or new trials. The browser page at port 8877 has SIM_GIN_TEST, Bioadaptive mode, its model and candidate opt-in selected; press Start Session for a fresh assignment, then perform normal preparation controls. Port 8765 and the original publisher remain intact. No APK or physical Quest Pro validation was performed.

# Workload AUC reported without a save threshold

2026-11-10, Stage 2/3; `workload-auc-report-only-v1`; backend `evidence-console-v1.29-auc-report-only`; display `console-auc-report-only-v1`.

## Requested behavior

The researcher requested removal of the workload-model AUC acceptance threshold: compute and report AUC, but do not prevent model creation because it is low. The previous mean held-out AUC >= 0.65 save requirement is superseded for the forest/arithmetic workload RF path only.

Training still performs the same three temporal folds and reports each fold's AUC and their unweighted mean. After evaluation it fits and saves the final model regardless of the mean AUC. No score is inflated, flipped or relabeled as passing. `admitted=false` remains the engineering provenance marker, not an AUC gate. Insufficient samples/windows, absent class coverage in a fold, non-finite data, clock/mapping checks and other inability-to-compute conditions can still prevent a fit. Alpha fallback, unscreened epochs, feature extraction and the video controller's BA/EEG/gaze rules are unchanged. Separate legacy control-model admission is unchanged.

Protocol and report metadata record `aucPolicyRevision=workload-auc-report-only-v1`, `aucGating=false`, and `aucMinimum=null`. The window/feature compatibility contract is unchanged because model input and scoring semantics have not changed. Earlier compatible models remain loadable with their original report provenance.

Console session summaries show the mean and fold AUCs, and state that AUC is reported only for new reports. Model selectors and selected-model details expose saved AUC values, including historical models when the report contains them. The live model output labels report-only AUC. The archived failure record is not rewritten when a separate reanalysis model is saved.

## P002 reanalysis

Replayed original session `20261110T155340_7eaf2c6e62ae` through the revised path, retaining 36 Rest and 33 Calculate windows. Fold AUCs remain 0.560606, 0.462121 and 0.674242; mean remains **0.5656566**. The final model now saves successfully and reloads through the existing registry loader. It retains the detected 8 Hz individual alpha reference; no band change was made to increase its AUC.

Saved separately as `20261110T155340_7eaf2c6e62ae-auc-report-v1` under `D:/ExperimentData/PatentDemo/P002/models/`. The model records source-session hashes and recorded-data reanalysis provenance. Original EEG and historical failure results are retained. Evidence: `Evidence/AUCReportOnly-20261110/P002-retrained/summary.json` and `published-model.json`.

## Validation and operation

The regression fixture uses identical class spectra to produce AUC 0.5, then exercises actual session fitting, registry publication, listing and reload. It verifies report-only metadata and score equality after reload; insufficient temporal coverage still fails. Existing high-AUC, alpha fallback and model reuse cases remain covered. Full regression passed **255 tests in 117.589 seconds**. JavaScript syntax, existing display contracts and changed-file whitespace checks passed. Evidence: `Evidence/AUCReportOnly-20261110/validation.json`.

Confirmed no active assignments and stopped Unity's completed screen. Restarted the Console through the existing launcher; health reports `evidence-console-v1.29-auc-report-only`, `/tests` returns HTTP 200 and the model-list endpoint exposes the saved P002 model with mean/fold AUCs and `aucGating=false`. Service log: `Evidence/EvidenceConsole/20261110T161605989083Z/service.log`. No new session was assigned; Unity remains stopped.

End the completed Unity run, restart the Console, and refresh the browser. Choose the P002 model ending `-auc-report-v1`, or run a fresh calibration. Saving a model now means that computation completed; AUC is a reported measurement rather than an admission decision. Fresh live acquisition and independent performance validation remain pending.

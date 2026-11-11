# Workload training without artifact rejection

**Subsequent revision, 2026-11-10:** the alpha-peak failure documented here was investigated separately. A labeled fixed-band fallback now permits fitting without a detected IAF; P002 produced a separate engineering model with mean held-out AUC 0.760185. The original failure is preserved. [Current alpha policy](AlphaPeakFallbackReview.md).

2026-11-10, Stage 2/3 maintenance. Revision `workload-training-no-artifact-screen-v1`; selection policy `rolling5-stride1-disjoint-fold60-unscreened-v4`; backend `evidence-console-v1.27-unscreened-training`.

## Reason and implementation

The researcher clarified that workload model training must not reject EEG based on signal quality. The previous implementation disabled artifact screening for engineering A/B inference and live preview, but left calibration training screened. P002 session `20261110T152940_293bba07749d` consequently retained zero windows in both conditions; P4 was flagged in every checked epoch.

Training now calls the unscreened measurement path and retains all five filtered one-second epochs of each computable window. Raw discontinuity/plateau/flatline checks, filtered amplitude checks and the three-clean-second requirement no longer reject training windows. The scanner uses the same spectra for fitting and held-out deployment-scale scores. Console/Unity status says that artifact screening is off and reports included seconds, without claiming clean signal or quality acceptance.

Data availability, finite numeric values, sample coverage/timestamps, 250 Hz sampling, channel mapping, declared/confirmed units, synchronization and mount interruption checks remain. Five-second windows, one-second candidates, disjoint selection, three temporal folds, twelve windows per condition, IAF extraction and mean held-out AUC >= 0.65 remain unchanged. No alpha-peak fallback or model-admission waiver was introduced. The separate observational scoring route retains its previous screening behavior.

Protocol, window logs, model report and registry contract record `artifactScreening=false`, `minCleanSeconds=0` and the training revision. Reports use `trainingSeconds`, not `cleanSeconds`. The feature formula revision remains `rest-math-iaf-rf-v3-5s`; the changed training policy is identified separately. Existing screened model records remain intact and cannot be silently reused under the new window contract; fresh calibration is required.

## Verification

Ten focused workload/short-calibration tests passed. A synthetic fixture with a flat channel and large P4 jumps is rejected by the legacy screened measurement but accepted by the actual calibration scanner with all five epochs. Missing samples and non-finite data remain rejected. The synthetic six-minute fit/save/reload test verifies a trained model, metadata and score consistency.

Isolated chronological replay of the recorded P002 calibration retained **36 rest windows / 180 seconds** and **34 arithmetic windows / 170 seconds**. It then stopped at `rest_alpha_peak_unclear`, before fold AUC evaluation. This proves removal of artifact-based window rejection, not successful model production from that recording. No model was published into the research registry. Source snapshot prefixes and original result files were preserved; the still-running Console appended only telemetry, events and clock-sync records during the replay. Earlier diagnostic attempts and the final successful replay are kept separately.

Full AdaptiveTraining regression passed **252 tests in 145.974 seconds**; changed-file whitespace checks passed. Evidence: `Evidence/UnscreenedTraining-20261110/validation.json` and `P002-replay-final/summary.json`; regression test `Tools/AdaptiveTraining/test_load_reference.py`; replay utility `AgentScripts/validate_unscreened_calibration.py`.

After confirming that all Console sessions were terminal, Unity Play was stopped. The existing launcher stopped and restarted the backend successfully. Service log: `Evidence/EvidenceConsole/20261110T154304357316Z/service.log`. No new assignment was created and Unity was left stopped. Fresh worn calibration, independent task performance and Quest Pro physical acceptance remain unverified for this change.

## Operation

End the current Unity run before restarting the Console. Refresh the browser and create a fresh workload calibration assignment. Historical failed sessions continue to show their original outcome; replay results do not overwrite them. Removing artifact rejection does not remove the alpha-peak and model-discrimination requirements.

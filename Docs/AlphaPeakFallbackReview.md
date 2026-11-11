# Alpha-peak diagnosis and fixed-band fallback

**Subsequent AUC policy, 2026-11-10:** the researcher removed the AUC save threshold. AUC is reported only; the alpha fallback described here remains active. The historical AUC requirements and rejection tests below describe this earlier revision. [Current policy](AUCReportOnlyReview.md).

2026-11-10, Stage 2/3. Revision `iaf-or-fixed-theta4-8-alpha8-12-v1`; backend `evidence-console-v1.28-alpha-fallback`; display `console-alpha-fallback-v1`.

## Diagnosis

After artifact screening was removed, P002 calibration `20261110T152940_293bba07749d` supplied 36 rest and 34 arithmetic windows. The next failure came from the existing local-peak prominence requirement, not a missing data window or random-forest error. The detector computes the median across one-second epochs after averaging Pz/P3/P4 power, searches local peaks at 7–13 Hz and requires prominence of at least 15% of the maximum over 6–14 Hz.

In fold zero's training rest data, a local 10 Hz peak has prominence 0.1238868 against the required 0.1623117. Folds one and two also fail their thresholds. All rest epochs together contain a 10 Hz local peak with prominence only 0.0354095 against 0.1619632. The posterior channels also have different local maxima; their pooled curve has a weak bump. The finding does not establish absence of physiological alpha, a particular electrode fault, or a new verified numeric scale.

This is consistent with the documented behavior of [SciPy find_peaks](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.find_peaks.html): local maxima are filtered using the requested prominence. The existing workload feature approach was adapted from [Sciaraffa et al. (2022)](https://www.frontiersin.org/journals/human-neuroscience/articles/10.3389/fnhum.2022.901387/full); the fallback below is a separate engineering policy, not claimed as that paper's validated method.

![P002 posterior spectra](../Evidence/AlphaPeakRepair-20261110/alpha-peak-diagnosis.png)

## Implemented behavior

Keep the existing peak detector and its threshold. If it detects a peak, use the existing individual-frequency feature bands. Otherwise use fixed theta [4,8) Hz and alpha [8,12) Hz. This fixed fallback was specified before evaluating its AUC; no sweep across thresholds/bands was used to maximize P002 performance.

Every temporal fold chooses its bands from its training rest subset only. Held-out rest/arithmetic windows do not determine band selection. The final model chooses from all retained rest data. Per-fold and final records include the candidates, power curve, prominence threshold, chosen mode, band boundaries, fallback reason and revision. `iafHz`/`detectedIafHz` are null for fallback models; `bandCenterHz=10` is a computational reference, not a claimed measured IAF. Features, inference and attribution use that explicit reference consistently. The Console labels fallback models as fixed bands.

Artifact screening remains off for workload training. Five-second windows, disjoint selection, thirty-six maximum/twelve minimum windows per condition, sample/clock/mapping checks, temporal folds, 100 trees, maximum depth four, balanced class weights and mean held-out AUC >= 0.65 are unchanged. A flat/uninformative fixture still fails AUC instead of being accepted merely because a fallback exists. The window contract now includes the band-selection revision, preventing silent loading of earlier models under different semantics.

## Validation and P002 model

- Twelve focused workload/short-calibration tests passed. Tests cover detectable, weak and absent peaks, explicit null IAF, fold-local selection, fallback training, save/reload and retained discrimination rejection.
- Full AdaptiveTraining regression: **254 tests passed in 58.197 seconds**. Both changed JavaScript files passed syntax checks; existing decision-display contracts passed.
- Chronological replay using the unmodified original EEG and Unity packets produced fold AUCs **0.808333, 0.777778, 0.694444**, mean **0.760185**. All folds and the final model used the fixed fallback. Snapshot hashes confirm original session contents remained unchanged.
- Created and reload-verified a separate engineering model `20261110T152940_293bba07749d-bands-v1` under `D:/ExperimentData/PatentDemo/P002/models/`. Manifest and report identify recorded-calibration reanalysis, original session/result, source hashes, no fresh acquisition and fixed-band mode. This is not synthetic data or a new participant session. The original failed session and its report were not overwritten.
- Console restarted through its existing launcher after confirming no active sessions or telemetry. Log: `Evidence/EvidenceConsole/20261110T154831123350Z/service.log`. No new task assignment was created; Unity remains stopped.

Evidence: `Evidence/AlphaPeakRepair-20261110/spectral-diagnosis.json`, `before/`, `after/summary.json`, `after/published-model.json`; utilities `AgentScripts/validate_unscreened_calibration.py` and `AgentScripts/publish_calibration_reanalysis.py`.

This is an engineering reanalysis prompted by the same recording. Its within-recording AUC is not independent validation of the revised method, physiological specificity, cross-session performance or Quest Pro acceptance. Fresh acquisition and independent task testing remain pending; `admitted=false` remains intact.

## Operation

Refresh the Console, select P002 and the saved model ending `-bands-v1` for an engineering saved-model comparison. It is labeled fixed bands. Historical calibration cards still show their original failure. New calibrations use the fallback automatically only when the unchanged peak detector has no accepted peak. Existing BA rules and EEG score thresholds are separate; the new model has freshly recomputed threshold recommendations.

# P001 forced workload candidate

2026-11-10, Stage 2/3 engineering analysis, revision `forced-offline-no-artifact-screening-v1`.

The researcher explicitly requested a model from the failed P001 workload calibration. Source session: `20261110T124607_2e2d018b9321`, recorded as `editor-test`, with live Mindtooth EEG. This analysis is separate from participant qualification and preserves the failed source report.

## Method and result

The standalone script reads the recorded raw EEG and telemetry, maps windows with recorded clock offsets, and retains the existing montage, unit configuration, timestamp-gap, finite-sample and clock-uncertainty checks. It bypasses calibration artifact screening, including the clean-epoch minimum, and keeps all ten one-second epochs in each accepted ten-second interval. Windows are selected without overlap within the original three 60-second temporal folds. Features and RF parameters follow `workload_rf.py` (100 trees, maximum depth 4, balanced classes, recorded seed 257283015).

Original quality-qualified windows remain zero for both conditions. Unscreened training windows: 17 rest and 18 arithmetic. Three temporal held-out AUC values: 0.833333, 0.944444 and 0.733333; mean 0.837037. Fold 1 (zero-based) had no clear alpha peak and explicitly used a fixed 10 Hz fallback. Other folds and the final model used a detected 9 Hz peak. The script can retain a candidate even if discrimination fails; in this run the mean exceeded 0.65. No quality or admission gate was changed in production code.

These metrics describe discrimination of the recorded conditions with artifact screening bypassed. They do not establish workload validity, artifact independence, cross-session performance, participant admission or device acceptance. Model status is `forced_unvalidated_candidate`, `admitted=false`, with the waived checks recorded in its report and manifest. The threshold profile (ON 0.46, OFF 0.52) is exploratory and was not applied to an active session.

## Artifacts and operation

Saved model ID: `20261110T124607_2e2d018b9321-forced-load`.

Folder: `D:\ExperimentData\PatentDemo\P001\models\20261110T124607_2e2d018b9321-forced-load` contains `workload_rf.joblib`, `reference.json` and `manifest.json`. The existing engineering model registry exposes the candidate under P001. Refresh Study setup and select the ID ending in `forced-load` for an explicitly intended engineering comparison. No assignment was created and no active model or thresholds were switched. The original calibration still reports failure because its recorded outcome is unchanged.

Reproduction and validation: `Evidence/P001ForcedModel-20261110/train_forced.py`, `window-audit.json` and `verification.json`. The script refuses to overwrite an existing candidate; `--verify-existing` independently retrains in memory and compares scores to the saved artifact through the existing model loader.

## Verification and pending checks

- Existing loader: model identity, signal/policy compatibility, report and forest hashes passed. Reloaded scores matched retrained scores exactly across all 35 windows and were finite in [0, 1].
- Running Console `/tests/models`: candidate returned with `available=true`. Browser rendering and a new Unity run were not performed.
- Original raw EEG, calibration windows and failure report remained unchanged. The running service continued appending telemetry, events and clock-sync records; verification checks preserved file prefixes instead of requiring those live files to remain frozen. The initial whole-file equality check detected these appends after export; corrected prefix verification passed without changing the candidate.
- Pending: clean recalibration, independent signal-unit/contact verification, cross-session and physical/device validation. This forced candidate does not satisfy those checks.

# Simulated calibration and source-bound model training

Date: 2026-11-03. Revision: `simulated-calibration-record-v1`. Engineering fixture only.

The user initially requested generated behavioral records and training with the signal described as "GIN". They subsequently identified and connected `Gin_mock_p007_test`. Its stream metadata identifies `sub-P007_ses-S002_active.xdf`, distinct from the earlier local P007 recording. The original response-only artifact below remains unchanged; source binding and derived model records are tracked separately in the [Gin model test review](GinModelTestReview.md). Do not silently substitute another source or invent physiological features.

## Generated artifact

`D:/ExperimentData/PatentDemo/Simulations/SourcePending/20261103T182001_964d733b/` contains `simulation.json`, `simulated-trials.json`, `simulated-trials.csv` and `behavior-windows.json`. Identity is `SIM_SOURCE_PENDING`, type `synthetic`; source is null and `modelTrained=false`.

Eight alternating 12-slot blocks total 96 slots/432 simulated seconds. The first two slots of every block are unscored transition/observation slots. Seed 20261103 generates two-back colors and responses, correct/error/omission outcomes and reaction times within four seconds. Preset Stable/Struggling block scenarios are independent of physiology. They are artificial behavior, not cognitive-state ground truth or synchronized real task responses.

The existing five-trial scoring function validates 48 within-block behavioral windows: 24 Stable and 24 Struggling. Feature vectors are null and all feature windows remain invalid. No physiological quality gate is bypassed. No model or reuse manifest exists for this fixture yet.

Reproduce in a fresh directory with:

```powershell
Tools/AdaptiveTraining/.venv/Scripts/python.exe Tools/AdaptiveTraining/simulate_calibration_record.py --output D:/ExperimentData/PatentDemo/Simulations/SourcePending
```

After source identification, retain its file hash, units, channel mapping and exact source intervals alongside derived features. Any resulting model must remain an explicitly unvalidated simulation candidate, with source quality failures retained and no live/participant admission. The current source mapping, active service, formal task and delivered dev6 ZIP are unchanged.

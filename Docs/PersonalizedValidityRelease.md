# Stage 2 dev10 engineering release

Historical dev10 record. Current dev11 removes the initial resting baseline and updates the application/configuration version; see [current flow](NoRestProtocolReview.md), [release](NoRestProtocolRelease.md) and [operation](ExperimentGuide.md). Dev10 model/feedback/oral-questionnaire rules are retained unless explicitly superseded there.

Date: 2026-11-02. Application/configuration `2.0.0-stage2-dev10`, Android code 22, protocol `personalized-rf-v2`. This is an engineering candidate, with no participant/device/scientific acceptance claim.

## Changes and compatibility

- Five-trial window-local score labels, an unlabeled buffer and 199 within-block circular-label null refits with held-out-block validation, condition-specific gates and a time-only comparator.
- Practice-only correctness feedback; neutral calibration and training countdowns with unchanged task scoring/timing.
- Experimenter-administered oral questionnaires with headset/electrodes kept in place. Recorded movement latches invalidation and requires a fresh session; no automatic offset correction.
- Per-channel EEG amplitude/flatline diagnostics. Original P007 rejection fractions reproduced without changing thresholds, dropping channels or modifying source recordings.

Read [PersonalizedValidityReview.md](PersonalizedValidityReview.md) for exact formulas, gate assumptions, evidence and pending checks. Old dev9 policies, configurations and model files are incompatible; generate new allocated configurations and retrain. Historical dev9 APK/ZIP are retained unchanged.

## Artifacts

- `Builds/Quest/EvidenceVR-Stage2-dev10.apk`: 95,983,490 bytes, ARM64, version code 22; APK v2 signature verified. SHA-256 `2aed3232fc410a15687c4c40f6825b601b2e78af2543c5161e9424f1c505fc63`.
- `Builds/Stage2PersonalizedDev10.zip`: deployment bundle containing the APK, unchanged 22-pair media library, dev10 engineering configuration, Python tools/policies, questionnaire URLs, documentation and compact evidence. Its final size, SHA-256 and CRC check are recorded in `Evidence/PersonalizedValidity/release-package.json`.
- The original P007 XDF, raw participant/replay samples, Python virtual environment, model artifacts and source restore archive are not distributed in the bundle. Use the original Unity checkout for Editor development; the bundle is not a replacement checkout.

## Deployment

1. Extract the complete archive, preserving directory structure. Create a Python 3.10 environment and install `Tools/AdaptiveTraining/requirements-verified.txt`.
2. Select real LSL sources and reviewed EEG/ECG mappings. Start the bundled service with the matching v2 policy. Default policy is unfrozen and device/permutation-unqualified with no AUC minimum. ACTIVE never bypasses admission. See [operation](PersonalizedValidityReview.md#operation-and-compatibility).
3. Install the APK on the intended Quest Pro. Copy `adaptive-training-config.json`, `adaptive-media-manifest.json` and the `AdaptiveMedia` directory from the media folder to `/sdcard/Android/data/com.evidencevr.taskplatform/files/`.
4. Replace `controllerUrl` with the study computer address reachable from the headset. `127.0.0.1` is only suitable for Editor and service on the same computer. The bundled enabled configuration is engineering-only; generate a uniquely allocated participant configuration before any qualified participant session.
5. Follow [ExperimentGuide.md](ExperimentGuide.md). The APK alone starts Stage 1 until an enabled adaptive override is deployed. Administer all questionnaires orally using [QuestionnaireSetup.md](QuestionnaireSetup.md); no headset/electrode adjustment between calibration and training.

The combined suite passed 94 Python tests; 15,965 protocol assertions and 79 feedback/mount/scoring checks passed. A separate full 300-tree/199-null synthetic check completed in 591.192 s and correctly retained non-admission. Allow for substantial model preparation time.

The Android build succeeded in 174.291 s with zero errors and 347 recorded warnings. Signature, package/version and ARM64 contents were inspected. No headset installation or physical acceptance was performed. See [AcceptanceRecord.md](AcceptanceRecord.md) for final test evidence and all pending checks.

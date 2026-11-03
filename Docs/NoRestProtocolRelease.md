# Stage 2 dev11 engineering release

Date: 2026-11-03. Application/configuration `2.0.0-stage2-dev11`, Android code 23, protocol family `personalized-rf-v2`. This engineering candidate removes the 120-second resting baseline and proceeds from preparation to practice and calibration. See [review](NoRestProtocolReview.md) and [operation](ExperimentGuide.md).

## Delivery

- APK: `Builds/Quest/EvidenceVR-Stage2-dev11.apk`, 95,981,506 bytes; SHA-256 `d28376148519195cf5e7a0fd23797c3d38d63911f1fcf0c068af4476bbcdd62e`.
- Deployment bundle: `Builds/Stage2PersonalizedDev11.zip`, including the matching Python service, enabled engineering configuration, unchanged media library, documentation and compact evidence.
- Final APK/build identity is recorded in `Evidence/NoRestProtocol/android-build.json`, `apk-badging.txt`, `apk-signature.txt` and `final-verification.json`. Bundle size, SHA-256 and CRC verification are recorded in `release-package.json`.

The bundle excludes original XDF/raw recordings, local environments, personal model artifacts and the source restore archive. Historical dev10 APK/ZIP and evidence are retained unchanged. The bundle is a deployment package, not a replacement Unity checkout.

## Deployment

1. Extract the bundle preserving its directory structure. Use Python 3.10 with `Tools/AdaptiveTraining/requirements-verified.txt`. Start the matching service and reviewed v2 policy after selecting the intended LSL sources/mappings. Default model admission remains disabled pending policy/device qualification.
2. Install the APK on the intended Quest Pro. Copy `adaptive-training-config.json`, `adaptive-media-manifest.json` and `AdaptiveMedia` from the media release folder to `/sdcard/Android/data/com.evidencevr.taskplatform/files/`.
3. Set `controllerUrl` to the study computer address reachable from the headset. `127.0.0.1` is appropriate only when Editor and service share a computer. The included enabled configuration is engineering-only. Generate a new uniquely allocated participant configuration for a qualified participant run.
4. Retire the old dev10 override/service/model together. The dev11 APK alone still starts Stage 1 without an enabled adaptive override. In the Editor, **Evidence VR > Stage 2 > Enable dev11 Personalized Engineering Protocol** backs up the old override before replacing it.
5. Follow [ExperimentGuide.md](ExperimentGuide.md): complete seating/A/B and signal checks, then practice and calibration. Administer questionnaires orally with headset/electrodes in place. A required removal or adjustment means a fresh session and calibration.

The default timed task schedule is 22:39 plus preparation, instructions, model fitting and oral questions/rests. Model fitting can still take several minutes. No headset installation, full physical session or scientific acceptance is implied by the build. See [AcceptanceRecord.md](AcceptanceRecord.md) for the final software evidence and pending checks.

## Software verification

Android build succeeded in 83.560 s with zero errors and 350 recorded warnings. APK: 95,981,506 bytes, ARM64, code 23/name `2.0.0-stage2-dev11`, v2 signature verified. SHA-256 `d28376148519195cf5e7a0fd23797c3d38d63911f1fcf0c068af4476bbcdd62e`. The combined Python suite passed 94 tests; 15,945 complete-flow assertions and 15 live preparation/input/gate checks passed. The no-baseline Python smoke produced calibration rows without any resting-reference files. Quest Pro/device validation remains pending.

# Stage 2 dev12 warm study release

Date: 2026-11-03. Application `2.0.0-stage2-dev12`, Android code 24; environment `warm-study-v1`. Configuration and Python service deliberately stay `2.0.0-stage2-dev11`; protocol `personalized-rf-v2`, policy `personalized-rf-pilot-v2-window5`. The environment does not change the service contract or qualify a model.

## Delivery

- APK: `Builds/Quest/EvidenceVR-Stage2-dev12.apk`.
- Deployment bundle: `Builds/Stage2PersonalizedDev12.zip`, including the existing dev11 service, enabled engineering configuration, unchanged media, documents, source credits and compact validation evidence.
- APK/build/signature identity: `Evidence/LabEnvironment/android-build.json`, `apk-badging.txt`, `apk-signature.txt` and `final-verification.json`. Bundle size/hash/CRC: `release-package.json`.

The bundle excludes original recordings, local Python environments, personal model binaries and the source restore ZIP. Older APKs/bundles are retained. This is a deployment package, not a replacement Unity checkout.

## Deployment and operation

1. Extract the bundle preserving paths. Use the existing Python 3.10 environment and `requirements-verified.txt`; start the dev11 service with the reviewed v2 policy and explicit LSL mappings. Default participant admission remains disabled pending qualification.
2. Install the dev12 APK on the intended Quest Pro. Copy the engineering configuration, media manifest and `AdaptiveMedia` from `Builds/Stage2MediaRelease` to `/sdcard/Android/data/com.evidencevr.taskplatform/files/` when not already deployed.
3. Set `controllerUrl` to the study computer address reachable from the headset. The bundled configuration is engineering-only. Without an enabled override, the APK starts Stage 1. In the Editor, **Enable dev11 Personalized Engineering Protocol** remains the correct menu because the protocol configuration did not change.
4. Start a fresh session and complete preparation/signal checks, practice and calibration. Do not resume an earlier environment's session or treat its calibration as qualification for this scene. Questionnaires remain oral, with the headset/electrodes in place. There is no timed resting acquisition.
5. Verify `applicationVersion=2.0.0-stage2-dev12` and `environmentRevision=warm-study-v1` in `session.json`; configuration/service still read dev11. Keep environment versions explicit in analysis and inspect actual Quest Pro brightness, color identification, HUD contrast, scale, comfort and full-duration performance before participant work.

See [operation](ExperimentGuide.md), [review](LabEnvironmentReview.md), [asset sources](../Assets/TaskPlatform/Environment/ThirdParty/AssetCredits.md) and [acceptance](AcceptanceRecord.md). No physical device was connected for installation during this update; no participant or scientific acceptance is implied.

## Build verification

Android build succeeded in 177.607 s with zero errors and 347 recorded warnings (345 existing Sentis shader warnings, one existing Quest Link member-hiding warning and one crash-symbol warning). APK: 100,260,882 bytes, ARM64, code 24/name `2.0.0-stage2-dev12`, APK v2 signature verified. SHA-256 `2703d6356fe470ea212379c4b97a905d5f6325c1c11a84c352fdf1d62bf63319`. BuildReport totalSize includes intermediate artifacts and is not the APK size. No headset installation was performed.

# Stage 2 dev9 engineering release

Historical dev9 release. Current dev11 source and deployment instructions are in [NoRestProtocolRelease.md](NoRestProtocolRelease.md). Dev9 APKs, models and policies do not contain the dev10 validity changes.

Release date: 2026-11-02. App/configuration `2.0.0-stage2-dev9`, Android version code 21, ARM64. This is an engineering candidate for the personalized two-level study. It is not a passed physical-device or scientific-effect validation.

## Contents

- `Builds/Quest/EvidenceVR-Stage2-dev9.apk`: APK v2 signed. SHA-256: `0205f9085e60f33cf301ed380e9b6f2d323b64e70f623f9b0190a0f169e75e65`.
- `Builds/Stage2MediaRelease/`: 22 media pairs and manifest. The bundled configuration is explicitly engineering/ACTIVE; 18 training pairs are eligible and four remain reserved. Media review is still required for participant sessions.
- `Tools/AdaptiveTraining/`: study service, personalized signal/model/controller code, allocation, analysis, replay launcher, policies, questionnaire links and tests. Python dependencies are listed in `requirements-verified.txt`.
- `Assets/TaskPlatform/Config/adaptive-training-config.json`: disabled template, with published questionnaire URLs. `allocate_personalized.py` generates enabled configurations with counterbalanced assignments.
- `Docs/`: operation, methodology, acceptance and historical records. `AgentScripts/` contains the dev9 engine validation and explicitly accelerated Editor preview helpers.
- `Evidence/PersonalizedProtocol/`: compact software/build/replay evidence. Original raw P007 samples, source XDF, Python environment and pre-change backups are not distributed in this ZIP.

## Start

1. Extract the complete archive, preserving its directories. For Unity Editor development, use the original Unity project; this archive is a deployment and Python-tool bundle, not a replacement Unity checkout.
2. Create a Python 3.10 environment and install `Tools/AdaptiveTraining/requirements-verified.txt`. Run the service from that environment. Select real LSL outlets and explicit channel/unit mappings; see [protocol operation](PersonalizedProtocolReview.md#operation).
3. Install the APK on the intended Quest Pro after connecting it through ADB. No device was connected during this release, so installation has not been performed. Put the configuration, media manifest and `AdaptiveMedia` directory in the app's persistent data folder. The package name is `com.evidencevr.taskplatform`; on Android, the normal location is `/sdcard/Android/data/com.evidencevr.taskplatform/files/`.
4. Replace `controllerUrl` with the study computer's address reachable from the headset. `127.0.0.1` in the engineering example is only for an Editor and Python service on the same computer. Start a new session after any configuration change.
5. Use [ExperimentGuide.md](ExperimentGuide.md) for the timed procedure and [QuestionnaireSetup.md](QuestionnaireSetup.md) for the external Forms. Prepare a unique allocated participant configuration before a real study; the bundled engineering example is not a participant assignment.

The APK alone starts Stage 1 until an enabled adaptive override is deployed. The default personalized policy is unfrozen and unqualified with no AUC admission threshold. ACTIVE mode does not bypass those gates. Real physiological model admission requires the documented pilot qualification; repeated model failure is retained and any behavioral fallback is explicitly labeled.

The optional local P007 replay command uses `D:/Code-Space/patent_code/P007/sub-P007.xdf` and the existing project at `D:/Code-Space/patent_code/xdf_replay`. Neither is bundled. Recorded data remain replay/simulation and cannot qualify a participant model.

## Verified and pending

81 Python tests and 15,965 pure-engine Unity assertions passed. Actual Editor presentation, AV decoding, HTTP transport and P007 acquisition/quality rejection were checked. The Android build succeeded with zero errors and 347 recorded warnings. The two pilot Forms were published and their fields inspected; no questionnaire answers were submitted.

P007 produced 17 candidate windows in the recorded-source check: 15 failed EEG quality and two included excluded transition history. No model was admitted and no quality thresholds were relaxed.

Remaining acceptance includes actual Quest Pro sessions, live physiological hardware and electrode qualification, R-peak reference comparison, timing/onset validation, human media review, frozen model/label/score gates, questionnaire scoring and submission/export rehearsal, and scientific evaluation. See [AcceptanceRecord.md](AcceptanceRecord.md).

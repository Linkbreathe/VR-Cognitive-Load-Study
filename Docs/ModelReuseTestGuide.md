# Operator-managed calibration and model reuse tests

Date: 2026-11-03. Revision: `engineering-model-reuse-v1`; console `evidence-console-v1.2-model-reuse`; Android candidate `3.0.0-stage3-dev6` / code 30. This is an explicit engineering test extension to the existing dev11 personalized contract. It does not authorize participant model reuse or establish validity after rewearing sensors.

## Start on the study computer

1. Configure the actual acquisition source using the existing LSL source selector. EEG and ECG must be mapped and receiving; an OBSERVE-only source is insufficient for the new test preparation gate. Confirm channel mapping and scaling separately from connection status.
2. Double-click the single launcher **StartEvidenceConsole.cmd**. The page is `http://127.0.0.1:8765/tests`; read-only monitoring pages remain at `/`. An identical running service is reused. Before a service upgrade, end the session, stop Unity/Quest telemetry, wait at least five seconds, then use `StartEvidenceConsole.cmd --stop` or the Unity LSL window's Disconnect before relaunching. There is no separate test/Gin/stop CMD. [Unified entry details](UnifiedConsoleReview.md).
3. In the Editor, stop Play Mode and choose **Evidence VR > Test sessions > Enable operator-managed engineering tests**, then enter Play Mode. This backs up the previous override and preserves its controller URL. Use the normal dev11 menu or restore that backup to return to the original entry flow.
4. Enter **Participant ID** (for example `TEST001`) and **Storage Folder** (default `D:\ExperimentData\PatentDemo`). Browse opens a desktop folder picker; Open Folder opens an existing configured folder. These paths belong to the Python study computer, not the headset. The folder is checked for write access and at least 1 GiB free space before each new assignment. This is a startup check, not a guarantee of capacity for an indefinitely running acquisition.
5. Choose a mode and press **Start Session**. Unity claims the assignment and the service checks its exact ID, configuration and client identity. Complete seating and A/B checks, then press X/Enter in Unity. The webpage prepares the session; it does not bypass physical preparation controls.

The page remembers the last ID and storage folder in `UserSettings/TestSessions/port-8765/test-session-settings.json`. Session assignments are retained in `test-session-index.json` in the same directory. This location survives the LSL selector creating a new bridge output directory. An explicit `--test-state-dir` can override it; retain that directory across launches. IDs accept 1-80 ASCII letters, digits, underscores or hyphens; reserved Windows device names are rejected. Use the same ID for the same tester. A fresh generated Session ID distinguishes every run. Once assigned, ID, directory, model and mode are locked until completion or stopping.

The explicit engineering launcher permits an already configured replay/synthetic source and prints its status; it does not create a publisher, map channels or qualify the signal. The normal monitoring launcher continues to reject saved replay/synthetic selections. At delivery the existing `mock_p007_test` recorded replay was preserved in OBSERVE mode and the idle service refreshed without changing its selection fingerprint. This source is insufficient for the EEG/ECG preparation gate.

## Modes

| Mode | Sequence | End behavior |
|---|---|---|
| Calibration only | Preparation -> alternating calibration blocks -> model preparation | Saves result/model, offers the existing supplemental pair on first rejection, then ends |
| Bioadaptive test | Preparation -> Bioadaptive instructions -> 96 stimulus slots | Skips practice, calibration, Random and questionnaires; ends after Bioadaptive |
| Full protocol | Existing practice -> calibration -> counterbalanced Random/Bioadaptive and oral questionnaires | Retains the original task flow; data remains engineering/editor-test |

Calibration defaults to four 12-trial blocks (216 seconds), with six/eight available and one optional 24-trial supplement. Reuse is a fresh 96-slot, 432-second phase, with its first two slots observe-only. The four-second response/visibility window, 4.5-second cycle, five-trial decisions, twenty-trial behavioral criteria, media timing, scoring and neutral formal countdown are retained.

Engineering tests require installed media with valid file hashes and successful loading. They do not set the media's human volume-review flags to true. Participant readiness continues to use the original stricter checks. No music, new distraction types or decorative animation were added.

## Stored files

```text
<Storage Folder>/
  TEST001/
    sessions/<Session ID>/
      test-session.json             Frozen operator assignment
      service/                     Python raw signals, aligned windows, predictions,
                                   decisions, training rows and model attempt reports
      unity/                       Synchronized core Unity records
        session.json
        adaptive-training-config.json
        trials.csv
        practice_trials.csv
        events.jsonl
        archive-manifest.json
    models/<Model ID>/
      model.joblib                 Complete fitted pipeline, scaler and random forest
      model_report.json
      manifest.json                Identity, source session, hashes and compatibility
```

Every model attempt keeps its training rows and report in its source session. A trainable but rejected estimator is still preserved as an unvalidated candidate. An attempt with insufficient usable labeled data can produce a report without a model. The page distinguishes these outcomes. New models have distinct IDs and never overwrite previous ones. The manifest records feature order, policy, sklearn version, configured signal channels/rate/scaling, electrode description through the policy, source session and artifact SHA-256.

Only models registered by this operator-managed workflow appear in its model list. Historical per-session joblib files without a reuse manifest are retained but not silently imported: their acquisition compatibility is not inferred. Recalibrate through the new entry point to create a reusable artifact. Move/copy complete records deliberately if changing storage roots; selecting another root does not migrate old data.

## Reuse and candidate control

Choose the same ID and root, select **Bioadaptive test**, refresh models and select one. Compatibility checks reject a changed identity, missing/changed file hash, different feature order, policy, sklearn version, channel mapping, rate or scaling. Publisher UIDs may change between sessions; within-session identity checks still apply. Only trusted, locally produced registry files are deserialized; there is no network model-upload endpoint.

An admitted model retains its original admission report. Cross-session reuse is nevertheless engineering evidence only. A rejected candidate requires the explicit **Allow unvalidated candidate model** checkbox. It runs with `basis=engineering_candidate_rf`, `engineeringModelEligible=true` only when its current control requirements are met, and `physiologicalEligible=false`. It never changes the original report to admitted. `model-reuse.json` records the source and waived admission reasons. The page and engineering HUD identify candidate-enabled testing.

Fresh sessions start with empty signal buffers, predictions, decision history and behavior. Models are never refit in reuse mode. A decision may remain HOLD while behavior is insufficient, the latest windows are unusable, conditions disagree or media exposure/dwell is inadequate. The console reports the actual basis, action, reason, latest window reason and telemetry age. Signal quality, clock, mounting, new-condition and trial-boundary/media execution checks continue to apply. Service loss pauses an active managed test; stop/restart uses a new assignment.

Default policy remains unfrozen, device-unqualified, units-unconfirmed and without a qualified AUC floor. An OBSERVE source or simply enabling candidate mode cannot manufacture a fitted model or a valid feature window. Configure actual units/mapping and collect usable calibration data; do not relabel replay as live physiology. The standalone synthetic fixture is for software tests only.

## Archive and interruption behavior

The headset/Editor keeps its original local files under `Application.persistentDataPath/AdaptiveTrainingData/<engineering-or-editor-test>/<Session ID>/`. Python synchronizes the five core files above in bounded chunks. Append retries verify matching prefixes, including partially uploaded chunks after restart. Metadata is replaced atomically. Final completion requires exact file lengths, SHA-256 checks, matching Unity identity/configuration and a closed local session.

`complete_core_records` means these five files were verified, including the complete or aborted status. It does **not** mean a session completed successfully, or that every headset diagnostic file was exported. Gaze CSVs and other optional headset diagnostics remain local and require the existing export procedure. Python signals/model records are written directly to the chosen computer directory.

Keep Unity/Quest and the service open until the core archive is complete. Disconnection leaves the archive visibly pending; the next operator-managed launch against the same endpoint retries local unfinished archives. A hard crash may leave `session.json` marked in progress, so the archive stays incomplete rather than asserting completion. Preserve those files for manual recovery. Restarting the Python service invalidates old task assignments; it permits archive recovery but never resumes task/model buffers. The local session-ID collision check prevents overwrite.

**Stop / release test** requests a stop from a connected Unity instance. An unclaimed or disconnected assignment can be released immediately and remains aborted/incomplete. Connection loss pauses a running managed task, and a released assignment cannot regain control.

## Standalone Quest setup

The APK alone retains Stage 1 startup when no enabled override exists. Install `Builds/Stage3Dev6/EvidenceVR-Stage3-dev6.apk` and copy the accompanying operator bootstrap config to the app's persistent folder. Continue using the existing media package; it is not bundled again in this release.

For an authorized USB-debug connection, using the project's Android platform tools:

```powershell
adb install -r Builds/Stage3Dev6/EvidenceVR-Stage3-dev6.apk
adb reverse tcp:8765 tcp:8765
adb push Builds/Stage3Dev6/adaptive-training-config.json /sdcard/Android/data/com.evidencevr.taskplatform/files/adaptive-training-config.json
```

The bootstrap defaults to `http://127.0.0.1:8765`, which requires the USB reverse route on a standalone headset. For a reviewed LAN route, use the study computer's reachable URL in that bootstrap and start the service with `--host 0.0.0.0`; the operator continues to use the local page on the computer. Device configuration must be deployed before launching the app. No device was installed or configured as part of this software validation.

## Verification and limits

See `Evidence/ModelReuse/` and the current [acceptance record](AcceptanceRecord.md). The test suite covers training artifact publication, prediction-preserving load, candidate authority, identity/configuration rejection, compatibility, restart, filesystem checks, local operator HTTP boundaries, idempotent archive recovery and final hashes. Isolated Unity engines exercise all 96 slots, calibration/supplement outcomes and the unchanged full protocol.

Use `Tools/AdaptiveTraining/.venv/Scripts/python.exe` with `requirements-verified.txt` and the bundled EvidenceGuard dependency. The unrelated system Anaconda interpreter has incompatible NumPy binaries/API and lacks the guard package; its failed diagnostic run is retained as `Evidence/ModelReuse/system-python-not-supported.txt` and is not the release test environment.

The live Editor/service/browser fixture used only `TEST_FIXTURE` and synthetic model/connection metadata. It confirmed preparation acknowledgement, direct Bioadaptive entry, a five-trial HOLD for insufficient history, webpage stop and byte-identical core archiving. It did not acquire genuine EEG/ECG or establish model efficacy. Earlier interrupted development fixtures remain in the evidence folder, including an intentionally uncompleted archive from the pre-fix float handshake; they are not counted as successful sessions.

Pending physical checks: Quest Pro controls, stereo readability of the new engineering labels, genuine signals and mapping, packet delay/clock quality, media loading/volume, mobile performance, device restart/USB or LAN disconnection, and real cross-session model usefulness. Quest 3 testing is compatibility evidence only. Do not pool engineering fixtures with participant data.

Implementation used Unity 6000.3.27f2 and the installed Pipeline documentation: `commands/scripts.md`, `commands/build-and-compilation.md`, `commands/editor-lifecycle-and-observability.md`, `commands/runtime.md`, and `commands/capture.md`. Restore point: `Evidence/ModelReuse/pre-model-reuse.zip` and `.patch`.

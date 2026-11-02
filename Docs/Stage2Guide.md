# Stage 2 operator guide

Application: `2.0.0-stage2-dev3`, 2026-11-02. Training configuration version is `2.0.0-stage2-dev3`. Read [implementation boundaries](Stage2Implementation.md) before participant use. Default startup remains Stage 1 until an adaptive override is explicitly enabled.

## Read the mode and status display

The compact status row reads, for example, `STAGE 2 | AUTO OFF | PHYSIO OFFLINE` or `STAGE 2 | AUTO ON: BEHAVIOR | REPLAY`. SHADOW explicitly shows AUTO OFF. ACTIVE without a recent service reply shows `AUTO ON / HOLD`. Replay waiting, partial or lost reception is shown beside REPLAY; a disconnected service shows OFFLINE. A live stream is labeled `LIVE (UNVALIDATED)`. Source receipt is not physiological eligibility. The second running row identifies practice/baseline/training and 2-back; the sphere and countdown remain the response focus. See the [front-wall/practice revision](WallPracticeReview.md).

Menu changes take effect on the next Play session. The menu-created configuration defaults to OFF. An existing enabled configuration can be edited before Play; stopping the replay service does not change its mode setting. The automatic `--editor` launcher restores its saved configuration on exit.

## Start the software

1. In Unity, use **Evidence VR > Stage 2 > Create Editor Engineering Configuration**. The command creates `adaptive-training-config.json` under `Application.persistentDataPath` and refuses to overwrite an existing override. Edit that file before entering Play Mode. Use **Disable Editor Adaptive Mode** to preserve the override under a dated disabled name and return to Stage 1.
2. Choose `protocol: "calibration"` for two silent baseline blocks and two counterbalanced fixed distraction blocks, or `"training"` for baseline/pretest/training/posttest. Choose OFF for manual control, SHADOW for recommendations only, or ACTIVE for boundary execution. Keep `sessionType: "engineering"` while testing. Editor sessions are always stored separately as `editor-test`.
3. Start the computer service from the project root:

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/service.py --output Evidence/Stage2Service
```

The local development environment uses Python 3.10.4, NumPy 1.24.3, SciPy 1.14.1, pylsl 1.18.6 and pyxdf 1.16.8; `requirements-verified.txt` freezes those tested runtime packages. To set up another computer, create a virtual environment and install `Tools/AdaptiveTraining/requirements.txt`. Record the resolved package versions when freezing the protocol.

4. Open `http://127.0.0.1:8765/` on the study computer for the monitor. It displays level/stage, current media, block metrics, gaze fractions, sensor connection/quality, last sample/feature timestamps and recent decisions. Compare timestamps with the displayed monotonic clock; old values are not current measurements.
5. In VR, calibrate seated position with left trigger / C, check A and B, then X / Enter to begin the 2-back practice. X starts the baseline after practice instructions. Training then proceeds continuously according to the configured protocol.

Default practice lasts 63 seconds and now includes three random 10–12-second movie clips, beginning around 10, 28 and 46 seconds. Keep responding to the sphere. Films appear on the front wall center (3.2 x 1.8 m); the rectangle disappears between clips. Practice uses only AV-assigned training content, counts prior exposures and excludes reserved test clips. OFF mode can play these practice films without a physiology service. Baseline starts without film distractions after practice; it is therefore a post-familiarization baseline. Pausing cancels remaining practice exposures.

**Controls:** A = match; B = nonmatch; Y / P = immediate pause. After pausing, Y resumes with two observe-only warmups; X / Enter stops and preserves the incomplete session. C restarts as a new session. Escape also stops in desktop testing. In OFF/SHADOW training, desktop keys 0/1/2 queue a level for the next boundary; callers can use `AdaptiveRuntime.RequestLevel`. ACTIVE rejects manual level requests. End ratings use A/B to decrease/increase and X to save.

The config snapshot freezes seed, group, protocol, mode, control basis, caps, slots, display geometry and gains. Supported first-revision bounds intentionally constrain counts/timing/geometry. Changing an unsupported protocol requires an explicit versioned implementation change, rather than silently modifying task comparability. `TaskPlatform/Config/adaptive-training-config.json` is the complete disabled template.

## Run the P007 recorded-data demo

From the project root, run:

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/launch_replay_demo.py --editor --mode ACTIVE
```

Then enter Unity Play Mode. The launcher installs the prepared film pairs, temporarily enables the engineering configuration, starts the existing P007 XDF scheduler and the local bridge, and displays the monitor URL. `ACTIVE` demonstrates **behavioral adaptation with recorded physiology shown alongside it**. The replay does not authorize physiological promotions. Use `--mode SHADOW` to observe recommendations without applying them. Ctrl+C stops the child processes and restores the previous Editor configuration for the next Play session. Prepared media remain installed. Run one study service at a time; an occupied port is rejected.

The default input is `D:/Code-Space/patent_code/P007/EEG-ECG-unlabeled-07-12min.xdf`. Its existing `xdf_replay/xdf_replay.py` scheduler is imported read-only. The source recording is a relaxation-task recording, not a new anti-distraction experiment. Six real-time repetitions of the five-minute recording provide about thirty minutes of engineering replay; every repetition is logged. The source manifest records hashes, selected stream, channel mapping, timing and replay identity. Use `--repeat 1 --duration 150` for a short transport check.

The selected 500 Hz stream is `02_007_5_eeg`: zero-based columns 1–4 are EEG channels, and columns 7 minus 8 form the recorded ECG derivation, in microvolts. These are provisional replay mappings from that project, not a claim about a newly connected device. The monitor prominently labels recorded replay; raw data and features carry `synthetic: true` / `recorded_xdf_replay`. Participant directories reject simulated streams. Genuine eye tracking is absent.

Outputs are in `Evidence/Stage2/ReplayDemo-TIMESTAMP/`: replay source/provenance, service records, process logs and any prior Editor configuration. Retain the corresponding Unity `editor-test` session too. The generic low-level `replay_p007.py` and `service.py --lsl-config ...` commands remain available for a Quest-connected study computer without changing the Editor configuration.

## Prepared film pairs

Ten candidate pairs have been downloaded and prepared: Titanic (3), Little Women (3), The Lord of the Rings (2), and Harry Potter (2). The **current deliverable is `Builds/Stage2MediaRelease/`**. Earlier `Stage2Media` and `Stage2MediaFinal` folders are superseded cut/normalization intermediates. See [film provenance and review](Stage2MediaCatalog.md) for sources, exact cuts and limitations.

The release contains ten H.264/AAC videos, ten mono WAVs, a hash/provenance manifest and an enabled SHADOW engineering config. All clips are 10–12 seconds, 1280 × 720 at 30 fps. WAV bytes exactly match PCM decoded from the corresponding video soundtrack. Two-pass mono normalization targets -23 LUFS, with peak limiting; per-pair runtime attenuation matches the encoded clips to -24 LUFS without boosting, and measured output is retained in `Evidence/Stage2/film-pair-audit.json`. Volume/intelligibility still require actual headset listening. `volumeReviewed` intentionally remains false, so these candidates cannot silently enter participant mode.

To reproduce from the downloaded source files:

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/prepare_media.py Tools/AdaptiveTraining/media-sources.stage2.json prepared/AdaptiveMedia
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/verify_media.py prepared --output media-audit.json
```

Preparation refuses to overwrite existing pairs. Complete short dialogue units use at most two seconds of explicitly logged final-frame/silence padding. No music or other sound is added; incidental source score/effects remain and require content review. Original source files and download metadata remain under ignored `MediaSources/Stage2/`.

Copy the release manifest and enabled config into the application's persistent root and the twenty media files into its `AdaptiveMedia/` directory. The launcher performs this for the Editor, preserving prior config/manifest. Participant mode requires all ten valid, hash-matching, reviewed, nonsynthetic pairs. Engineering mode logs failed exposures and excludes interrupted/failed blocks from attainment.

Pre/post tests use one held-out pair per modality/block because the plan reserves only four unique pairs; training/calibration use three. Some reserved excerpts share a scene with training excerpts, so they are unseen clips, not fully novel narrative contexts. Review matching/familiarity before recruitment; see [implementation choices](Stage2Implementation.md).

## Quest deployment

The candidate APK is `Builds/Quest/EvidenceVR-Stage2-dev3.apk`. The package identifier remains `com.evidencevr.taskplatform`. The application's persistent root on Quest is `/sdcard/Android/data/com.evidencevr.taskplatform/files/`.

Install the candidate, push an enabled complete adaptive config and the prepared manifest/media to that root, and restart the app. With a single connected, developer-enabled Quest, the following uses the installed SDK:

```powershell
$adb = 'D:/Tool/6000.3.27f2/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
& $adb devices
& $adb install -r Builds/Quest/EvidenceVR-Stage2-dev3.apk
& $adb shell mkdir -p /sdcard/Android/data/com.evidencevr.taskplatform/files/AdaptiveMedia
& $adb push Builds/Stage2MediaRelease/adaptive-training-config.json /sdcard/Android/data/com.evidencevr.taskplatform/files/
& $adb push Builds/Stage2MediaRelease/adaptive-media-manifest.json /sdcard/Android/data/com.evidencevr.taskplatform/files/
& $adb push Builds/Stage2MediaRelease/AdaptiveMedia/. /sdcard/Android/data/com.evidencevr.taskplatform/files/AdaptiveMedia/
& $adb reverse tcp:8765 tcp:8765
```

Export any existing device configuration before replacing it. Start `launch_replay_demo.py` without `--editor` on the computer for a recorded-data Quest demo. The supplied device config is SHADOW engineering mode; edit `mode` to ACTIVE for the explicitly behavioral fallback loop, preserving engineering provenance. These commands are provided for the pending device check and were not executed against a headset. With USB development connection, `adb reverse tcp:8765 tcp:8765` allows the default `http://127.0.0.1:8765` controller URL to reach the study computer. For a local study network, explicitly bind the service to the computer's interface with `--host` and set `controllerUrl` to that computer. The build enables Internet permission and local HTTP; it does not embed a cloud service.

The VR application runs in a standalone APK, while EEG/ECG analysis and control run on the separate computer as specified in the plan. No connected Quest was available during implementation; install/start/physical controls/video/audio/performance/export acceptance remains required. Quest 3 is the current test headset and has no genuine eye-tracking input in this setup. Quest Pro support is configured, not physically verified.

## Connect real sensors and admit a feature

Copy `lsl-config.example.json` and replace the source IDs, actual sample rates, selected channels, scale-to-units and amplitude limits using the device setup. Defaults are illustrative and must not be treated as a verified ear-EEG/ECG configuration. EEG defaults to 12 s; ECG HR/IBI to 30 s and exploratory RMSSD to a separate 60 s window. Raw samples, source timestamps, corrections and processing windows are retained on the computer.

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/service.py --output StudyData --lsl-config lsl-config.json --calibration participant-calibration.json
```

Start with `SHADOW`. Validate clock alignment and signal quality against raw recordings, then run the fixed calibration protocol. A real participant calibration record must identify the participant, `2-back-distraction` task, configuration, source ID, feature name, center/scale, adverse direction/threshold and a linked validation record. Set `validated` only after that evidence exists. `calibration.example.json` is intentionally unvalidated and cannot authorize physiological UP. The baseline controller does not validate a scientific model simply because a JSON flag is enabled.

Keep `controlBasis: "behavioral_fallback"` for behavioral-only engineering tests. Use `"physiological"` only after calibration; missing/poor/stale/cross-condition physiology then holds promotions. Eye-gaze input additionally requires a runtime/device providing the OpenXR eye-gaze profile and its device permissions. Invalid gaze remains invalid; the app never substitutes head orientation.

## Export and inspect

Unity writes `AdaptiveTrainingData/<sessionType>/<sessionId>/` beneath the persistent root. Besides the existing session/trial/event files, inspect:

| File | Meaning |
|---|---|
| `adaptive-training-config.json`, `adaptive-media-manifest.json` | Frozen runtime protocol and selected provenance/hashes |
| `training_blocks.csv` | Stage/level, eligible counts, balanced accuracy, correct RT median, omissions, transition counts and validity |
| `media_events.csv` | Scheduled/requested/started/ended/interrupted/failed slots, modality, previous exposure count and world position |
| `gaze_samples.csv`, `gaze_trials.csv` | Real gaze pose validity and task/video ROI sample fractions; no head-gaze proxy |
| `physio_features.csv` | Timestamped JSON feature packets mirrored from the computer; empty/header-only when there is no data |
| `controller_decisions.jsonl` | Input snapshots, recommendations, acceptance/rejection events and boundary execution |
| `clock_sync.jsonl`, `calibration.json` | Clock observations and latest calibration/baseline snapshot |
| `subjective_ratings.csv` | End-session perceived distraction and workload (1–7) |

The service's output directory has the same session ID and session type. It holds raw LSL samples, deduplicated Unity events, clock information, structured feature rows, recomputation windows, frozen policy/calibration and replayable controller requests. Retain **both** directories. Unity timestamps share the application monotonic clock; service files retain the mapping and uncertainty.

```powershell
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/replay.py StudyData/participant/SESSION_ID
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' Tools/AdaptiveTraining/analyze.py EXPORTED_UNITY_SESSION_FOLDER
& 'Tools/AdaptiveTraining/.venv/Scripts/python.exe' -m unittest discover -s Tools/AdaptiveTraining -p test_stage2.py -v
```

Replay operates on the **service** directory. Descriptive analysis operates on the **Unity** directory. The summary reports actual missing/invalid conditions; it does not invent absent transfer data. Changes in accuracy/RT do not establish training efficacy. Use [the calibration and acceptance worksheet](Stage2CalibrationReport.md) for the required real study records.

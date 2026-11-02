# Current acceptance record

Updated: 2026-11-02. Application: `2.0.0-stage2-dev4` (Android code 16). **Stage 2 engineering implementation and software checks are available; final Stage 2 physiological/device acceptance is not complete.** Stage 1 remains the default when the adaptive override is absent. Historical Stage 1 evidence is retained below.

## Expanded media library (dev4)

Implemented: 22 pairs / 44 media files, including 12 new nonoverlapping source cuts, with the original ten entries and bytes unchanged. The training pool is 18 clips (nine per modality); practice uses three AV clips and least-exposed-first selection can use six other AV clips before repeating them. The four reserved clips and all exposure/task timing remain unchanged. Legacy library layouts remain supported; dev4 requires matching physiology calibration. See [expanded-media review](ExpandedMediaReview.md).

Verified: all 22 pairs passed codec/hash/PCM/loudness/duration checks, and every added clip was visually inspected. 100,004 expanded-library model assertions, 51,068 adaptive assertions and 31 Python tests passed. Thirty live Editor checks passed: all 22 pairs loaded, all 12 new clips prepared and decoded offscreen, and a full 63-second practice played pair_22, pair_20 and pair_02 through the normal wall/audio system with three clean ends and no media failures. Offscreen decoding did not exercise audio; it is not a headset test. Logs retained 12 correct programmatic answers, 14 practice rows and the 4.5-second cycle. Evidence: `Evidence/MediaExpansion/`. The remote tool response timed out at 60 seconds, but the Editor probe completed; its saved result, runtime state and media/trial CSVs were independently checked.

Android build passed in 93.8 seconds with zero errors and one diagnostics-symbol warning. `Builds/Quest/EvidenceVR-Stage2-dev4.apk` is 88,757,512 bytes; ARM64, code 16 and APK v2 signature verified. The release ZIP passed integrity checks with 44 media files and dev4 config. Migration of the actual saved dev3 override preserved mode/seed. Build/signature/hash evidence is in `Evidence/MediaExpansion/`. Current local config remains enabled/OFF; the release config remains SHADOW engineering. Human content/volume review and Quest 3 physical acceptance remain pending. All `volumeReviewed` flags remain false.

## Historical front-wall films, compact UI and practice update (dev3)

Implemented: fixed centered 3.2 x 1.8 m front-wall screen, no idle rectangle, two compact running status rows, and three randomized AV-assigned training clips during default 63-second practice (10/28/46 s plus up to 0.5 s jitter). Practice is separately attributed and counted in familiarity history; held-out pre/post clips remain unused. Configuration and presentation are versioned. See [wall/practice review](WallPracticeReview.md).

Software verified: 80,003 new schedule/config assertions across 1,000 seeds x four groups, 49,068 existing adaptive assertions, 31 Python tests, and 157 real-time Editor checks. All three actual practice films started and ended without failure, then the baseline was silent; 12 programmatic scored answers were correctly logged and the 4.5-second cycle was retained. A separate Stage 1 Play Mode check verified compact status and warmup reminders. Screenshots and a CSV audit are under `Evidence/WallPractice/`. Initial harness failures and observed decoder warnings are documented in the detailed review.

Android build passed in 127.7 seconds with zero errors and one diagnostics-symbol warning. `Builds/Quest/EvidenceVR-Stage2-dev3.apk` is 88,752,896 bytes; ARM64, version code 15 and APK v2 signature were verified. The media ZIP now includes the dev3 config while preserving the film bytes and archive root structure; ZIP integrity passed. Build/signature/hash evidence is in `Evidence/WallPractice/`. Physical acceptance remains pending: centered video occlusion/readability/comfort, newly positioned sound source, Quest decoding/performance, and genuine physiological integration. Practice familiarity now differs between AV and audio-only training; review this design before participant recruitment. Prior dev1 physiology calibration is not accepted for dev3 control.

## Historical mode/status display update (dev2)

Persistent Stage 1/Stage 2, controller OFF/SHADOW/ACTIVE, automatic-control state, bridge availability, control basis and source provenance are implemented. Actual Editor checks passed for both task modes, preparation/running/pause layouts, metadata, P007 replay receiving and service dropout. Twenty-nine Python tests and six additional label/layout checks pass. Evidence is in `Evidence/Stage2Status/`; see [status review](ModeStatusReview.md). Physical-headset readability remains pending.

The dev2 APK was `Builds/Quest/EvidenceVR-Stage2-dev2.apk`. Its build succeeded with zero errors and one diagnostics-symbol warning; ARM64, version code 14 and APK v2 signature were verified. Build/signature/hash evidence is under `Evidence/Stage2Status/`. At dev2, the film package and training/controller configuration version were unchanged. The dev1 evidence below remains historical evidence for the underlying protocol.

## Stage 2 software evidence (dev1 protocol)

| Check | Result and evidence |
|---|---|
| Compilation and model | No current compiler errors. 49,068 adaptive assertions; 1,000 schedule seeds and 30 complete synthetic sessions; practice/formal pause rewarm included. `Evidence/Stage2/model-validation.txt` |
| Stage 1 regression | 194,113 task assertions and 96,013 distraction schedule assertions passed. Existing fixed-mode timing/scoring/presentation retained. |
| Python controller and quality | 27 tests passed, including calibrated/unvalidated/missing/stale/mixed/synthetic inputs, severe omissions, idempotence/restart, known-frequency EEG and known-RR ECG fixtures. `Evidence/Stage2/python-tests.txt` |
| ACTIVE boundary integration | Real local HTTP recommendations executed at Unity boundaries; two programmatic baselines plus four programmatic training evaluations reached candidate completion. Six ACTIVE and three SHADOW decision records replayed exactly. This was an accelerated engineering probe. |
| Actual film library | Ten candidate pairs (20 files), 10–12 s, H.264 720p/30 fps, identical decoded mono audio. Hash/codec/duration/loudness matching audit passed. Source, cut, padding and gain recorded. `film-pair-audit.json` and [media catalog](Stage2MediaCatalog.md) |
| Actual film runtime | Ten pairs loaded; audio-only playback, immediate pause, actual AV decoding, shared fixed source, running layout and bounded exposure end passed in Editor. `film-playback-probe.txt`, `film-runtime.png` |
| P007 recorded replay | Existing external XDF scheduler used read-only. Isolated receiver: 267 feature rows, 84 windows recomputed. Unity-connected Editor session: EEG/ECG/HRV received, 646 rows all synthetic, 156 windows recomputed. `p007-offline-recompute.json`, `unity-p007-offline-recompute.json`, `film-and-p007-unity-probe.txt` |
| Isolation and restoration | Editor data under `editor-test`; recorded LSL data rejected from participant directories. Replay labels retained in source, feature and monitor data. Launcher process/config cleanup exercised; Stage 1 startup restored. |
| Android candidate | Final build succeeded, zero errors, one diagnostics-symbol warning. ARM64, minimum SDK 29, target 36, APK v2 signature verified. `Evidence/Stage2/android-build.json`, `apk-platform.txt`, `apk-signature.txt` |

All paths abbreviated in this table are beneath `Evidence/Stage2/`. The candidate APK is `Builds/Quest/EvidenceVR-Stage2-dev1.apk`; media/config package is `Builds/Stage2MediaRelease.zip`. The film files are external to the APK and must be installed separately. `delivery-artifacts.json` records SHA-256 and sizes. The sole build warning concerns debug symbols for crash-report symbolication; it is not a passed crash-reporting test.

## Pending Stage 2 acceptance

- Quest 3 standalone installation, full operation, physical A/B/pause/stop, actual headset volume, visibility, comfort, video performance and export; no headset was connected during this implementation. Quest Pro remains an unverified target.
- Human semantic/content matching and listening review of all film pairs; every `volumeReviewed` flag remains false. This blocks participant mode.
- Approximately 6–8 real fixed-condition pilot participants, individual baseline/feature calibration and validation of EEG/ECG artifacts and ECG beats. No feature is currently admitted as real physiological control evidence.
- Genuine supported eye-tracking hardware and permissions. Quest 3 does not acquire real gaze in this setup; head orientation is not substituted.
- Five complete real-device integration sessions, real command/sensor dropout checks and pre/post unseen-clip observations. One reserved exposure per transfer block differs from three during training; shared-scene familiarity also requires review.
- A validated physiological feature actually contributing to control and a reviewed final configuration before freezing the Stage 3 Patent OFF baseline. Recorded P007 playback and software success do not establish physiological adaptation or training improvement.

Detailed records: [implementation/review](Stage2Implementation.md), [operator guide](Stage2Guide.md), [calibration worksheet](Stage2CalibrationReport.md), [project log](ProjectLog.md).

---

## Historical Stage 1 acceptance record

For the current project stage and major-update history, see [Project status and update log](ProjectLog.md).

Date: 2026-11-01. Software implementation and desktop validation are available; full physical-headset acceptance remains pending. The current presentation is spatial-lab-v1.7.1-more-video, application version 1.7.1-stage1.

## Verified software evidence

| Check | Evidence |
|---|---|
| Sequence generation and scoring | 2,000 sequence sets, 1,000 seeds per mode; counts, ratio, independent answers and replay pass |
| Core assertions | 194,113 assertions in `Evidence/automated-validation.txt`, including full four-second visibility, response boundaries and pause behavior |
| Complete synthetic runs | Five sessions per validation invocation; 88 stimuli each, 60 formal scored trials, both orders |
| Export-level recomputation | `Evidence/export-validation.txt`: 400 scored rows from five saved sessions independently checked |
| Real-time editor integration | `Evidence/live-playmode.txt`: queued InputSystem states, 40 seconds, 21,939 advancing frames, ten practice rows, pause/resume |
| Restart and session isolation | `Evidence/restart-validation.txt`: previous session saved as aborted_restart, fresh editor-test session created |
| OpenXR project configuration | `Evidence/openxr-validation.txt`; Android validation reports zero issues |
| Prior APK | v1.0 built and signature-verified; ARM64, VR launch category, Quest Pro/Quest 3 support declarations |
| Prior v1.1 APK | v1.1.0-stage1, version code 2; Android build succeeded in 191.7 seconds with zero errors; APK v2 signature verified; 41,735,754 bytes |
| Prior v1.2 APK | v1.2.0-stage1, version code 3; Android build succeeded in 111.3 seconds with zero errors; APK v2 signature verified; 41,737,330 bytes |
| Prior v1.2.1 APK | v1.2.1-stage1, version code 4; feedback wording changed to Incorrect; Android build succeeded in 53.2 seconds with zero errors; APK v2 signature verified; 41,736,466 bytes |
| Prior v1.3 APK | v1.3.0-stage1, version code 5; Android build succeeded in 121.0 seconds with zero errors; ARM64 and APK v2 signature verified; 41,831,962 bytes |
| Prior v1.3.1 APK | v1.3.1-stage1, version code 6; Android build succeeded in 55.2 seconds with zero errors; ARM64 and APK v2 signature verified; 41,874,802 bytes |
| Prior v1.4 APK | v1.4.0-stage1, version code 7; Android build succeeded in 266.1 seconds with zero errors; ARM64 and APK v2 signature verified; 70,020,954 bytes; all eight packaged videos match their catalog SHA256 values |
| Prior v1.5 APK | v1.5.0-stage1, version code 8; Android build succeeded in 116.0 seconds with zero errors; ARM64 and APK v2 signature verified; 70,022,502 bytes |
| Prior v1.6 APK | v1.6.0-stage1, version code 9; Android build succeeded in 139.7 seconds with zero errors; ARM64 and APK v2 signature verified; 70,027,606 bytes |
| Prior v1.6.1 APK | v1.6.1-stage1, version code 10; Android build succeeded in 63.5 seconds with zero errors; ARM64 and APK v2 signature verified; 70,027,754 bytes |
| Prior v1.7 APK | v1.7.0-stage1, version code 11; Android build succeeded in 110.4 seconds with zero errors; ARM64 and APK v2 signature verified; 88,634,246 bytes; all eight packaged videos match catalog SHA256 values |
| Current APK | v1.7.1-stage1, version code 12; Android build succeeded in 65.3 seconds with zero errors; ARM64 and APK v2 signature verified; 88,633,438 bytes |
| Visual refinement | See `VideoFrequencyReview.md` for current v1.7.1; media and feedback are recorded in `SpatialAudioFeedbackReview.md`; UI clearance is recorded in `UiClearanceReview.md`; v1.6 is recorded in `FrontWallCalibrationReview.md`; v1.5 is recorded in `SeatedMinimalReview.md`; prior refinements remain in `EnvironmentalDistractions.md`, `StimulusMaterialReview.md`, `SpatialTaskReview.md`, `VisualReview.md` and `SequenceGuidanceReview.md` |
| Seated minimal presentation | 88 checks: tabletop 0.65 m at three eye heights, sphere visible through 3.999 s and hidden at 4 s, practice and both formal modes green/red feedback, omission feedback, neutral warmups and reset, four active text elements and no overflow; `Evidence/seated-minimal-v1.7.txt` |
| Prior v1.5 live timing | 14-second Editor run verified persistent sphere visibility, hidden gap, green practice feedback, four cues and a saved 3.2-second response; `Evidence/response-window-live-v1.5.txt` |
| UI clearance | 39,214 checks at 1.05/1.20/1.40 m eye heights and three viewing offsets; sampled text rectangles clear of desk, plinth and sphere across all phases; no TMP overflow; SDF outline enabled; 19 screenshots saved; sampled phase views and all five video-position previews inspected; `Evidence/ui-occlusion-v1.6.1.txt` |
| Seated calibration | Actual coroutine corrected simulated 2.22 m combined height to 1.20 m, repeated calibration stable, fixed 0.65 m tabletop; `Evidence/seated-calibration-v1.6.txt`; no live headset connected during probe |
| Distraction scheduling | 96,013 checks over 1,000 seeds: eleven events per block, five videos at 24.6-second intervals and six audio events, balanced types and six audio families, ten-second video, no overlap, deterministic replay, eight distinct videos per session and five front-wall positions without consecutive repeats; `Evidence/distraction-validation-v1.7.1.txt` |
| Distraction playback | 141.27-second isolated Editor run: eleven completed exposures, no skips or decoder errors, five video exposures measured 10.009–10.021 seconds; pause/hide/resume and clock freeze verified; `Evidence/distraction-playback-v1.7.1.txt` |
| Spatial audio | 12 cases: six profiles on both sides; actual audio advance, moving traffic, approaching footsteps, near-ear distance, gain, pause/frozen motion, resume, cancellation and abort verified; `Evidence/distraction-audio-v1.7.txt` |
| Local media | 40 assets: eight silent H.264 videos, sixteen environmental recordings and sixteen synthesized speech clips; 46,603,614 prepared bytes; source and hash catalog saved in `Assets/TaskPlatform/Media/distraction-media-manifest.json` |
| Satin sphere | Four colors and a close-up rendered and visually reviewed; supported Lit material verified; sphere radius preserved with smoother mesh; `Evidence/stimulus-material-v1.3.1.txt` |
| Spatial layout and countdown | 29 states and 88 color counters checked; final front, angled and instruction views inspected after fixing plinth/sign occlusion; `Evidence/visual-validation-v1.3.txt` |
| Four-second live integration | 14-second Editor probe: four stimuli, four playing cues, countdown 4/3/2/1/NEXT; a programmatic response after 3.2 seconds accepted and saved; `Evidence/response-window-live-v1.3.txt` |
| Sequence guidance | 29 snapshots checked; stage/color counters across 88 stimuli, two-back indices, warm-up prompts and absence of formal answer feedback verified; `Evidence/visual-validation-v1.2.txt` |
| Quest Link initialization | Standalone OpenXR configuration added after diagnosing missing Editor XR settings; Oculus runtime, a running XR display subsystem and head-tracking device observed in Play Mode. See `Evidence/quest-link-runtime.json`. Visual confirmation and physical input remain separate checks. |
| Quest 3 Link visual confirmation | On 2026-11-01, the user confirmed that the experiment scene was visible inside Quest 3 while Unity Play Mode was running through Link. This confirms Editor preview, not standalone APK operation or complete task/input acceptance. |

Synthetic data under `Evidence/SyntheticSessions/` and editor probes are not participant or device-test evidence. Historical live input probes remain as provenance; core validation and export verification were refreshed for v1.7. The response-window probe is explicitly marked editor-test, uses programmatic input, and does not establish physical sound onset or headset audibility. Distraction and seated-layout probes use isolated engines and store their data under `Evidence/`.

The official Unity OpenJDK 17.0.18+8 module was installed during initial delivery after the first build found it missing. The current build has one warning: crash diagnostics lack debug symbols. Current build outcome is in `Evidence/android-build-report-v1.7.1.json`; package inspection and checksum are in `Evidence/apk-inspection-v1.7.1.txt`. SHA256: `A69F8B4C9B0668D8A1AFEB078794F381979FD5803A9A0AF76CE472961E48E81A`. Older versioned reports remain historical. The latest observed ADB connection required debugging authorization for APK installation; Quest Link preview is a separate connection. Final Editor checks found zero console errors, no compilation failure, Play Mode stopped and the scene saved.

## Required physical-device checks

| Item | Status | Record when tested |
|---|---|---|
| Standalone Quest 3 launch | Pending | Firmware, APK SHA256, date |
| Quest Pro launch | Pending | Separate device-specific evidence |
| Head tracking and world-fixed 1.5 m task placement | Pending | Look away/return, recenter, seated comfort |
| Physical A/B input, holds, simultaneous and late presses | Pending | Correspondence with event records |
| Pause, resume, restart, headset backgrounding | Pending | Trial order and interruption flags |
| Color discrimination and text readability | Pending | Review all four colors and all instructions |
| v1.3 cue audibility and comfortable gain | Pending | Quest 3 output volume and perceived onset cue level |
| v1.4 local video and spatial distraction audio | Pending | Android decoding/performance, comfortable gain, dialogue intelligibility and perceived rear localization; the Editor uses native 3D panning with no HRTF plugin |
| At least ten minutes of device operation | Pending | Refresh rate, software gaps, device performance tools |
| Five complete real sessions | Pending | Session IDs, complete status, 60 formal scored trials each |
| Export, metric recomputation, file persistence | Pending on device | Paths and summaries |
| Photon onset | Not measured | External instrumentation if required |

Do not label Stage 1 fully accepted or begin claiming physiological/adaptive validation until the required physical tests pass. The new room/panel change the visual environment; record the presentation revision and review comparability before combining versions.

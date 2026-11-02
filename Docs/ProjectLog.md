# Project status and update log

This is the entry point for project progress and major changes. Add a dated entry after each major update, newest first, and refresh the current status below. Preserve historical entries and link detailed reviews and evidence instead of duplicating them.

## Current status

Last reviewed: 2026-11-02. Application/configuration: `2.0.0-stage2-dev3` (Android code 15). Display revision: `mode-status-v2-compact`. Current work: **Stage 2 engineering implementation and software integration verified; real physiological/device acceptance pending**.

The project now contains two separate modes. `FixedBlockExperiment` preserves Stage 1 behavior and remains the default when the adaptive override is absent. `AdaptiveDistractionTraining` adds continuous fixed 2-back training, graded Silent/Audio/AV distraction, bounded behavioral adaptation, physiology acquisition/quality gates and audit/replay tools. The latest engineering APK and external film package are available locally.

The temporary physiological source is explicitly **recorded P007 XDF replay / simulation**, using the existing `D:/Code-Space/patent_code/xdf_replay/xdf_replay.py` scheduler read-only. Current automatic engineering demonstrations use `behavioral_fallback`; replay signals are displayed and recorded but cannot authorize physiological promotion. No real participant feature has been calibrated/admitted, and genuine eye tracking has not been verified.

Current Stage 2 behavior:

- Compact mode/control/source and phase/task rows replace the verbose running status. Replay, live-but-unvalidated, unknown, waiting and disconnected states remain distinct; full detail stays in logs/monitor. See [current wall/practice review](WallPracticeReview.md).

- Fixed four-color 2-back, four-second sphere visibility/response window, 4.5-second cycle and the existing single onset cue/green-red feedback. No response-dependent audio.
- Twenty-four scored trials per 108-second evaluation, continuous color history across boundaries, two initial observe-only warmups and two new warmups after a real pause. Two transition trials after a level change are retained but excluded from attainment.
- Two silent baselines, counterbalanced fixed pre/post conditions and bounded training; two valid passes for candidate advancement, maximum three consecutive blocks per level and eight training blocks. OFF, SHADOW and ACTIVE keep recommendations and actual boundary execution distinct.
- Fixed 3.2 x 1.8 m movie display/source at the front-wall center; hidden when inactive, countdown remains left. Default 63-second practice now includes three seeded AV training clips around 10/28/46 seconds, with separate practice/familiarity records. Formal training/calibration cadence remains unchanged; reserved pre/post clips are never used in practice. Baseline is silent but now follows AV familiarization; audio/AV familiarity differences require design review.
- Ten downloaded candidate film pairs (3 Titanic, 3 Little Women, 2 LOTR, 2 Harry Potter), six training/four reserved, with source cuts, hashes, matched audio/AV, runtime loudness gains and exposure logging. Human content matching and headset listening remain pending; participant readiness is blocked.
- Computer LSL bridge, EEG/ECG feature/quality windows, clock mapping, monitoring, raw retention, decision replay and descriptive summaries. Editor/synthetic/engineering records remain separate from participant directories.

The user has previously confirmed Stage 1 scene visibility on Quest 3 through Quest Link. That does not verify this Stage 2 candidate or standalone operation. No Quest was connected for this update. Quest 3 is the available test headset; Quest Pro remains a supported but physically unverified target. VLM attribution, active probes and Patent effects are outside this revision.

Start with the [Stage 2 operator guide](Stage2Guide.md). See [acceptance record](AcceptanceRecord.md), [implementation review](Stage2Implementation.md), [media catalog](Stage2MediaCatalog.md) and [calibration worksheet](Stage2CalibrationReport.md).

## 2026-11-02 — v2.0.0-stage2-dev3: Centered wall films, compact UI and practice familiarization

- **Stage/version:** Stage 2 engineering candidate; application/config `2.0.0-stage2-dev3`, Android code 15; presentation `adaptive-lab-v2.0-front-wall-practice`, status `mode-status-v2-compact`.
- **Reason:** The user requested a larger movie region centered on the wall, simpler UI and frequent randomized practice content.
- **Changes:** Moved the film surface/source to the front-wall center (3.2 x 1.8 m) and hide the surface between clips. Reduced running UI to two status/task rows plus the existing sphere/countdown; shortened preparation instructions. Added three seeded nonoverlapping AV practice clips at 10/28/46 seconds with 0.5 s jitter, using only AV-assigned training content and logging practice/Level 2 and prior exposures. Held-out pre/post clips remain unused. Next video is prepared after natural clip completion. Preserved task timing/scoring, formal cadence and controller thresholds.
- **Compatibility/data:** Versioned the new practice/presentation protocol, added legacy override migration, retained historical controller replay and required calibration to match the incoming config version. Excluded the central task ROI from film ROI double counting. Updated local/release configs while preserving the user's enabled/OFF mode, seed and endpoint. Film bytes and gains are unchanged; source placement and practice familiarity are explicit changes needing physical/design review.
- **Validation:** 80,003 new schedule/config assertions (1,000 seeds x four groups), 49,068 adaptive regression assertions and 31 Python tests passed. A full real-time Editor practice passed 157 checks with three decoded films, three clean ends, no media failures, 12 correct programmatic answers and unchanged 4.5-second cycles; baseline restored silence and hid the screen. Stage 1 compact display/warmup checks also passed. Screenshots and CSV metadata were checked. Initial harness failures were corrected and rerun; decoder timestamp-adjustment warnings and offline bridge timeouts are recorded in the detailed review.
- **Build/delivery:** Android build succeeded in 127.7 seconds, zero errors, one diagnostics-symbol warning. ARM64, version code 15 and APK v2 signature verified. APK: `Builds/Quest/EvidenceVR-Stage2-dev3.apk` (88,752,896 bytes). `Builds/Stage2MediaRelease.zip` now contains dev3 configuration; root structure and integrity verified. Evidence/hashes: `Evidence/WallPractice/`. Editor returned to stopped state; local Stage 2 enabled/OFF retained.
- **Pending:** Quest 3 wall-screen size/readability/occlusion/comfort, audio position/level and decoding/performance; human media review and genuine physiological validation. Practice now exposes AV training content before baseline, so pre-exposure baseline and equal-familiarity claims are inappropriate. No new participant data, real-gaze verification or physiological adaptation claim is made. Quest Pro remains unverified.
- **Records:** [Detailed review](WallPracticeReview.md), [operation](Stage2Guide.md), [experiment guide](ExperimentGuide.md), [acceptance](AcceptanceRecord.md), [media catalog](Stage2MediaCatalog.md). Restore point: `Evidence/RestorePoints/wall-practice-20261102/`. Read Pipeline documentation: `commands/scripts.md`, `capture.md`, `editor-lifecycle-and-observability.md`, `build-and-compilation.md` under the installed package documentation.

## 2026-11-02 — v2.0.0-stage2-dev2: Explicit mode, control and physiology status

- **Stage/version:** Stage 2 engineering candidate; application `2.0.0-stage2-dev2`, Android code 14; display `mode-status-v1`. Training configuration/controller protocol remains `2.0.0-stage2-dev1`.
- **Reason:** The user requested an immediately visible indication of the current experiment mode, whether automatic control is enabled, and whether physiological input is recorded replay.
- **Changes:** Added persistent three-line status to the instruction/preparation/pause/end panel and running HUD. It identifies the actual engine mode, controller OFF/SHADOW/ACTIVE and Auto OFF/ON, bridge Online/Offline, control basis and source provenance. ACTIVE offline explicitly displays HOLD. Replaced ambiguous session `STAGE n/4` wording with `STEP n/4`. Header spacing was adjusted without changing the task rule, timing, sphere, media schedule or policy thresholds.
- **Source integrity:** Added optional bridge `physiologyStatus` metadata, distinguishing recorded replay, simulation, live-but-unvalidated, mixed, unknown and unconfigured sources. Source/sample freshness is separate from feature/control eligibility. Stale service responses display SERVICE OFFLINE; previously observed replay is labeled Last source. Missing metadata from an older service never implies live acquisition.
- **Records:** Every new session stores `statusDisplayVersion`; status changes are recorded as `mode_status_display` events and Unity console entries. Engineering and replay checks remain isolated from participant data.
- **Validation:** Unity compiled without errors. Actual Editor Stage 1/Stage 2 preparation, running practice and pause screens passed visibility/text-bound/metadata checks. Actual P007 LSL replay and HTTP telemetry progressed from Waiting to Receiving, then to SERVICE OFFLINE after service exit. Six additional synthetic label/layout checks cover SHADOW, offline ACTIVE, old-service metadata, live-unvalidated and mixed provenance. Twenty-nine Python tests pass. Evidence and screenshots are in `Evidence/Stage2Status/`.
- **Build:** `Builds/Quest/EvidenceVR-Stage2-dev2.apk` succeeded with zero errors and one existing diagnostics-symbol warning. ARM64, version code 14 and APK v2 signature verified; 88,748,060 bytes. Build report, signature and SHA-256 are recorded in `Evidence/Stage2Status/`. The external film package is unchanged.
- **Restoration:** The user's enabled Stage 2/OFF configuration was restored byte-for-byte after test-mode changes (matching SHA-256). Editor Play Mode is stopped; no replay/bridge test process remains running. Original working files were preserved under `Evidence/RestorePoints/status-ui-20261102/`.
- **Pending:** Physical Quest 3 text readability/comfort and existing Stage 2 real-sensor/calibration/device acceptance remain pending. LIVE STREAM/Receiving is transport status, not validated physiology or evidence of training efficacy.
- **Details:** [Mode/status review](ModeStatusReview.md), [operator guide](Stage2Guide.md), [experiment guide](ExperimentGuide.md), [acceptance record](AcceptanceRecord.md).

## 2026-11-02 — v2.0.0-stage2-dev1: Graded training and recorded physiology integration

- **Stage/version:** Stage 2 engineering candidate, application `2.0.0-stage2-dev1`; media `stage2-paired-media-v1-matched`. Final physiological/device acceptance is pending.
- **Reason:** Execute the supplied Stage 2 plan, acquire the ten paired film clips online as requested, and temporarily use the user's P007 XDF/existing replay code with explicit simulation provenance.
- **Task/controller changes:** Added optional continuous 2-back evaluations, graded media state, baseline/calibration/transfer protocol, transition exclusions, pause rewarm, bounded caps, OFF/SHADOW/ACTIVE boundary control, behavioral policy and individually validated physiological promotion gates. Preserved Stage 1 as the default mode.
- **Media/presentation changes:** Prepared twenty playable film files with source/usage/cut/padding/hash records; audio-only PCM matches AV exactly. Applied recorded per-pair attenuation for -24 LUFS matching within 0.15 LU. Added fixed display/source and neutral surface, retained the 0.65 m seated desk and moved the Stage 2 countdown left. No additional music or response-dependent audio was added.
- **Data/architecture changes:** Added local Python LSL/HTTP service, raw/feature/clock/coverage records, real-gaze validity without a head-pose proxy, source/version/request checks, decision deduplication/replay, end ratings, descriptive summaries and a launcher that restores prior Editor configuration. P007 replay remains synthetic in monitor/features and is rejected from participant data.
- **Validation:** 49,068 Stage 2 model assertions over 1,000 media seeds/30 complete simulated sessions; 194,113 Stage 1 task and 96,013 distraction regression assertions; 27 Python tests. Actual film preload/audio/pause/AV/fixed-stop checks passed in Editor. Six ACTIVE and three SHADOW controller requests replayed exactly. Isolated P007 replay produced 267 feature rows with 84 exact window recomputations; Unity-connected replay produced 646 synthetic feature rows and 156 exact window recomputations. Initial replay timestamp-gap failures were diagnosed and fixed by using high-resolution `perf_counter()` consistently; these remain software results.
- **Build/delivery:** Final Android build succeeded with zero errors and one crash-diagnostics-symbol warning; ARM64 and APK v2 signature verified. APK: `Builds/Quest/EvidenceVR-Stage2-dev1.apk` (88,744,016 bytes). External film/config package: `Builds/Stage2MediaRelease.zip` (22,074,327 bytes). SHA-256 values and detailed evidence are in `Evidence/Stage2/delivery-artifacts.json` and `android-build.json`.
- **Pending:** Human film/content/headset listening review; Quest 3 standalone and comfort/performance checks; real fixed-condition pilot (approximately 6–8 people), individual physiology validation and genuine eye-tracking hardware; five complete real-device integration sessions, transfer observations and final Stage 3 baseline freeze. Replay is not current-task physiology, validated adaptation or evidence of training improvement.
- **Protocol decisions:** With only four held-out clips, each of four pre/post modality blocks receives one new clip rather than three. Shared-scene excerpts also limit narrative novelty. Review these explicitly before recruitment. Training and calibration retain three exposures per block.
- **Records:** [Implementation and review](Stage2Implementation.md), [operation and replay](Stage2Guide.md), [film provenance](Stage2MediaCatalog.md), [acceptance](AcceptanceRecord.md), [calibration/integration worksheet](Stage2CalibrationReport.md). Existing uncommitted project documentation was preserved; initial restore evidence is under `Evidence/RestorePoints/stage2-20261102/`.

## 2026-11-02 — Documentation workflow

- **Stage/version:** Stage 1, `1.7.1-stage1`; no runtime change.
- **Reason:** The user requested persistent records after every major update.
- **Changes:** Established this central status/update log and made maintenance mandatory in `AGENTS.md`. Existing detailed review records remain the historical evidence.
- **Validation:** Cross-checked the current summary against the experiment guide, acceptance record and latest review documents. Documentation only; no runtime tests required.
- **Pending:** Physical-device checks remain as listed in the acceptance record.

## 2026-11-01 — v1.7.1: More frequent videos

- **Stage:** Stage 1 implementation and software validation; physical acceptance pending.
- **Reason:** The user requested more frequent video appearances.
- **Changes:** Increased videos from three to five per formal block. Each lasts ten seconds, with 24.6-second onset spacing. Retained six audio events, interleaved without overlap. Other v1.7 behavior is unchanged.
- **Validation:** 96,013 checks across 1,000 seeds; a 141.27-second isolated Editor run completed all eleven events without skips or decoder errors. Android build succeeded with zero errors; ARM64 and APK v2 signature verified. These are software results, not physical-headset acceptance.
- **Pending:** Quest 3 comfort, media playback/performance, audio perception, physical input and standalone acceptance.
- **Details:** [Video frequency review](VideoFrequencyReview.md), [Acceptance record](AcceptanceRecord.md). Rollback checkpoint: `019e94e`.

## Earlier implementation history

These links preserve the detailed changes, rationale and checks made before this central log was established. Earlier behavior may be superseded by later revisions; use the current status and experiment guide for current operation.

| Revision | Main changes | Detailed record |
|---|---|---|
| v1.7 | Ten-second videos, expanded environmental/news/near-ear audio, spatial source paths and formal countdown feedback | [Spatial audio and feedback](SpatialAudioFeedbackReview.md) |
| v1.6.1 | Clear instruction and running prompts from desk/sphere occlusion | [UI clearance](UiClearanceReview.md) |
| v1.6 | Lower seated desk, tracking-origin calibration and front-wall video positions | [Front wall and calibration](FrontWallCalibrationReview.md) |
| v1.5 | Seated desk, full four-second sphere visibility and minimal running UI | [Seated minimal presentation](SeatedMinimalReview.md) |
| v1.4 | Local street videos, door sounds and conversation; seeded formal-only distraction scheduling | [Environmental distractions](EnvironmentalDistractions.md) |
| v1.3.1 | Refined sphere material and mesh | [Stimulus material](StimulusMaterialReview.md) |
| v1.3 | Detached sphere, four-second countdown and quiet onset cue | [Spatial task](SpatialTaskReview.md) |
| v1.2 / v1.2.1 | Clearer stage/sequence guidance and `Incorrect` wording | [Sequence guidance](SequenceGuidanceReview.md), [Acceptance record](AcceptanceRecord.md) |
| Initial Stage 1 / v1.1 | Cognitive task platform and visual refinement | [Requirements](Stage1Requirements.md), [Visual review](VisualReview.md), [Acceptance record](AcceptanceRecord.md) |

## Entry requirements

For every major update, record its date and version/revision, project stage, reason, concrete changes, validation performed, pending checks and relevant document/evidence links. Record protocol or comparability changes explicitly. Update the guide when operation changes and the acceptance record when verification changes. Preserve the distinction between synthetic/Editor evidence and participant or physical-device evidence.

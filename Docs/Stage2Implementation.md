# Stage 2 implementation and review

Current target clarification: dev7 targets Meta Quest Pro and adds the explicit Android eye-permission request; see [Quest Pro support](QuestProSupport.md).

Historical review: dev6 adds [engineering-only gaze outlines and visibility-aware gaze records](GazeDebugReview.md). Dev5 rolling control and decimal countdown supersede the block-based training policy below; see [current control review](RollingAdaptationReview.md). Current dev3 presentation, practice exposure, configuration and compact status behavior supersede the corresponding details below. See [Wall and practice revision](WallPracticeReview.md).

Date: 2026-11-02. Revision: `2.0.0-stage2-dev1`. Status: **engineering implementation, not final Stage 2 acceptance**.

The supplied Stage 2 plan authorizes a separate `AdaptiveDistractionTraining` mode. The frozen Stage 1 task configuration, scene, rules and distraction schedule remain available when the adaptive override is absent or disabled. The application now contains both modes; application version alone does not identify the protocol. Check the saved configuration and presentation revision.

## Implemented architecture

| Component | Responsibility |
|---|---|
| `TaskModel.cs` / `TaskEngine` | Existing task timing/scoring; optional continuous 2-back sequence; two observe-only warmups at training start and after an actual pause; transition labels |
| `AdaptiveTrainingModel.cs` | Configuration, balanced-accuracy/RT summaries, fixed calibration/pre/post protocol, bounded training state, safe-boundary execution |
| `TaskPlatformController.cs` | Existing input, seated calibration, stimulus and recorder integration; separate adaptive activation |
| `AdaptiveMedia.cs` | Paired audio/video loading, hash checks, participant readiness gate, reproducible scheduling, history, fixed position, asynchronous video preparation and stop/failure events |
| `AdaptiveRuntime.cs` | HTTP requests/acknowledgment checks, frozen configuration, adaptive CSV/JSONL exports, gaze validity/ROI, end ratings and minimal UI |
| `Tools/AdaptiveTraining/controller.py` | Independent, replayable `BaselineAdaptiveController`; OFF/SHADOW/ACTIVE; behavioral thresholds and individually validated physiology gate |
| `features.py`, `service.py` | Optional LSL acquisition, raw samples/clock corrections, EEG/ECG feature windows, quality, condition coverage, monitoring and recorded decisions |
| `prepare_media.py`, `replay.py`, `analyze.py` | Paired-source preparation, deterministic replay/feature recomputation and descriptive summaries |

No attribution, VLM audit or active probe was added. No physiological model from the relaxation project was reused.

## Protocol and explicit implementation choices

- The task remains 2-back, four colors, four seconds visible/respondable and 4.5 seconds onset-to-onset. The existing response scoring, green/red correctness feedback and single onset cue remain. No response-dependent audio was added.
- A new session starts with one 2-back practice block. Adaptive evaluations contain 24 scored trials and last 108 seconds, excluding the initial nine seconds of observe-only warmups. Color history continues across evaluation and level boundaries.
- Default training protocol: two Level 0 baselines, optional fixed pretest in counterbalanced Audio/AV order, training beginning at Level 1, then optional posttest. The separate `calibration` protocol runs two baselines and one fixed block at each distraction level, with no adaptive training.
- A baseline below 0.75 balanced accuracy, with more than two omissions, or otherwise invalid stops progression. These are provisional engineering thresholds, not established cognitive standards.
- The first two scored trials after a level change are retained in trial exports but excluded from attainment summaries. Invalid/interrupted blocks cannot support promotion. An actual pause stops media immediately; resume inserts two new unscored memory colors. Remaining media slots in that interrupted evaluation are cancelled.
- Maximums are three consecutive training blocks at a level and eight training evaluations total. Invalid blocks still consume these limits. Baseline/pre/post evaluations are outside the eight-block training limit; the full maximum is fourteen evaluation blocks plus practice/warmups. A cap ends training without declaring success, then runs optional posttests. REST stops the protocol; user pause/stop does not wait for Python.
- Audio and AV training evaluations contain three distinct 10–12 second clips at nominal scored-block seconds 19, 51 and 83, with seeded ±1 second jitter. Silent evaluations keep three analysis slots. Actual exposure ends at the fixed slot deadline; slow decoding does not extend it. Failed playback invalidates the evaluation for attainment.
- **Transfer-material arithmetic:** four held-out pairs cannot provide three unique clips in each of four pre/post modality blocks. This revision uses one new held-out clip at the middle slot per transfer block, reserving two for pretest and two for posttest. Training and fixed calibration blocks retain three clips. This difference must be reviewed before freezing an experimental protocol; increase the held-out library if three unique exposures per transfer condition are required.
- Counterbalance groups 0–3 cross Audio/AV order and pre/post held-out allocation. Training pairs alternate modality assignment by group. Six training pairs yield three per modality, so later same-modality blocks necessarily repeat. Real presentation starts increment history; no-repeat configuration emits explicit unavailability instead of inventing unseen content.
- The display is a fixed 0.64 × 0.36 m surface, 20 degrees right of the central axis at the task plane. The source is fixed at that display. The static surface uses the neutral wall color. Stage 2 moves the countdown to the left to avoid the video. The 0.65 m desk, sphere position/size and seated tracking calibration remain.
- Two end-of-session 1–7 ratings record perceived distraction and mental workload. Per-condition subjective reports for the calibration study must also be collected using the calibration worksheet; those are not collected during the uninterrupted task.

## Controller, data quality and timing

The behavioral baseline is the mean of two valid baseline-block balanced accuracies and the mean of their correct-response RT medians. Match and nonmatch sensitivities each contribute one half of balanced accuracy; omissions count as failures. At least twenty eligible trials and four of each response class are required.

Candidate pass: accuracy at least `max(0.75, baseline - 0.10)`, correct RT median at most `1.20 × baseline`, and at most two omissions. Two consecutive valid passes at the same level permit UP. Consecutive blocks below 0.65 accuracy, above 1.50 × baseline RT or with more than four omissions permit DOWN. One severe block below 0.45 accuracy or with at least eight omissions can trigger DOWN. At Level 0, persistent/severe difficulty yields REST. These policy parameters are saved separately from the Unity presentation configuration.

OFF does not request automatic training decisions. SHADOW records recommendations without applying them. ACTIVE applies a matching, timely request only at the evaluation boundary. Fixed baseline/pre/post transitions and caps remain active in all three modes. Manual 0/1/2 requests are accepted only during training in OFF/SHADOW and are deferred to the boundary.

Requests are sent after the final four-second response window closes, leaving the normal half-second gap for the reply. A missing, invalid, stale, wrong-session or wrong-block reply produces HOLD. There is no network wait in trial timing. Requests, replies and actual execution are recorded separately. Python caches request IDs, persists the decision inputs and can recover the cache after a service restart. Unity still applies its own caps when the service is unavailable.

`behavioral_fallback` is explicitly labeled. In `physiological` mode, UP additionally requires a real, fresh, quality-qualified feature with complete single-stage/single-level coverage in the current evaluation and an explicit participant/task/source-specific validation record. Missing sensors, unvalidated calibration, synthetic features and stale/cross-condition windows cannot establish recovery. An adverse candidate blocks promotion; physiology alone does not cause rapid downgrade/upgrade cycling. No feature has yet been validated for a real participant in this project.

EEG uses configurable 10–15 second windows (default 12 s), per-channel Welch relative theta/alpha/beta power and conservative engineering quality checks. ECG uses a 30-second HR/IBI window and a separate 60-second exploratory RMSSD window. The polarity-independent beat detector requires device-specific offline validation. Flatline, excessive median-centered amplitude, timestamp gaps, insufficient samples and nonfinite values are rejected. Median centering avoids treating the P007 amplifier DC offset as physiological amplitude. This does not replace device clipping/artifact validation. Amplitude limits/units and selected ear-electrode channels must be configured for the actual hardware.

LSL timestamps and `time_correction()` are saved with raw samples, then mapped to the computer monotonic clock. HTTP round-trip observations map Unity's clock to the computer clock and retain uncertainty. Software timestamps are not photon/acoustic onsets or hardware synchronization validation. The implementation follows [LSL clock synchronization guidance](https://labstreaminglayer.readthedocs.io/info/time_synchronization.html). Eye data uses the installed OpenXR eye-gaze interaction provider's tracked position/rotation, not head direction. Unsupported/lost gaze is explicitly invalid. Quest 3 is the available headset; Quest Pro remains an unverified eye-tracking target.

## Software evidence

- Unity compilation: no compiler errors after integration.
- `AgentScripts/AdaptiveTrainingValidation.cs`: **49,068 assertions**, 1,000 media seeds and thirty complete synthetic sessions, checking continuous 2-back, block counts, deterministic slots, no overlap, transition trials, bounded completion, rewarm and exposure history.
- Stage 1 `TaskPlatformValidation.Run`: **194,113 assertions**, 2,000 generated sequences and five complete synthetic sessions. Existing timing/scoring behavior passed regression.
- Python: **27 tests** for controller paths, quality/staleness/mixed conditions, signal fixtures, idempotence, service restart cache and deterministic replay. Test recordings are synthetic; they are not physiological validation.
- Live Editor: advancing frames, paired preload, late accepted response, 2-back HUD, audio-only playback, immediate interruption, real decoded AV frames, shared fixed audio source and fixed exposure end passed. Generated test-pattern/tone media was used exclusively in `editor-test` sessions.
- Real local HTTP requests reached Python and ACTIVE results were applied at Unity boundaries: two synthetic baselines and four synthetic training evaluations, then candidate completion. This probe accelerated the model clock and supplied programmatic correct answers; it did not validate participant responses, exposure effects, real physiology or full-session real-time stability.
- Six ACTIVE and three SHADOW requests replayed exactly from service records. No real physiological feature windows existed in those sessions.
- Final layout was inspected after moving the timer left. See `Evidence/Stage2/layout-final.png`; the earlier AV fixture demonstrates decode/rendering but predates the timer move.
- Ten downloaded film pairs passed codec, duration, hashes and byte-identical decoded soundtrack checks. The actual films loaded in Unity; audio playback/pause, video decoding and bounded stopping passed. See [media catalog](Stage2MediaCatalog.md).
- P007 recorded-XDF replay used the existing external scheduler read-only. An isolated LSL/HTTP fixture produced 267 feature rows including EEG, HR, IBI and exploratory RMSSD; 84 saved windows recomputed exactly. All features were marked synthetic, and mixed/uncovered windows remained ineligible. An initial timestamp-gap failure was traced to the coarse Python 3.10 Windows `monotonic()` mapping; acquisition now uses high-resolution `perf_counter()` consistently.
- A separate real-time Editor session received P007 EEG/ECG/HRV through the actual local service, with provenance mirrored in Unity CSV; its 646 feature rows were all simulated and 156 saved windows recomputed exactly. Task answers in this check were programmatic. See `Evidence/Stage2/film-and-p007-unity-probe.txt`; this is transport/integration evidence, not real physiological control evidence.
- Local ADB inventory found no connected headset. No live sensors were connected; the later LSL stream was explicitly recorded P007 replay.

Evidence is under `Evidence/Stage2/` and the `AdaptiveTrainingData/editor-test/` directories referenced in those probe reports. Generated fixtures are not part of the ten-film deliverable and cannot satisfy participant readiness. See [Acceptance record](AcceptanceRecord.md) for the build result and pending physical checks.

## Remaining gates

| Plan stage | Current status | Required next evidence |
|---|---|---|
| 2.1 Graded presentation | Software implemented; ten candidate pairs prepared | Human semantic/content matching and headset listening review; headset volume/visibility/performance and independent APK session |
| 2.2 Pre-calibration | Fixed protocol, metrics and worksheet available; study not performed | Real three-condition pilot, approximately 6–8 volunteers, content/level calibration and individual baseline evidence |
| 2.3 Physiology | Acquisition and recorded P007 replay verified | Real device IDs/channels/units, synchronized EEG/ECG, supported eye hardware, offline beat/feature checks and individually validated feature |
| 2.4 Controller | Behavioral software loop verified; physiological path guarded | Real calibrated physiological evidence driving actual adjustments, including dropouts and comfort stops |
| 2.5 Integration/transfer | APK engineering candidate built; software integration checked | Five complete real-device sessions, command loss/pause tests, pre/post unseen-media results and final configuration freeze |

The Stage 2 completion criterion from the plan is **not yet met**. Do not label this revision a validated physiological adaptive training system or claim learning/Patent effects. No participant study, long-term training efficacy, Quest Pro physical acceptance or real physiological validation has been performed here.

## Replay and media provenance boundary

The P007 recording is deliberately a temporary simulated input, as requested by the user. Its original experiment did not contain the Stage 2 distraction conditions. Replaying it alongside Unity cannot establish a response to the current task, calibration validity or training efficacy. The engineering `behavioral_fallback` loop may adjust levels using current task behavior; the displayed replay features do not authorize those adjustments. `physiological` mode continues to reject replay features.

The source XDF and scheduler are not modified. The launcher writes new replay identities/configuration and source hashes to isolated evidence folders, starts the existing scheduler at speed 1, and records cycle boundaries. The default film package is external to the APK and must be copied to the app's persistent root; this keeps the reviewed manifest/media independently replaceable and logged. The APK remains usable in the preserved Stage 1 mode without an adaptive override.

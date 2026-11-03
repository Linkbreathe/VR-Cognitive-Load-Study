# Experiment guide: current modes

Updated 2026-11-03 for application `2.0.0-stage2-dev12`, dev11 configuration/HTTP contract, protocol `personalized-rf-v2` and updated Python policy `personalized-rf-pilot-v3-control70-40`. Stage 1 remains the default without an enabled adaptive override. Read the [control rules](PersonalizedControlRulesReview.md), [validity revision](PersonalizedValidityReview.md), [no-rest flow](NoRestProtocolReview.md), [release instructions](LabEnvironmentRelease.md) and [acceptance record](AcceptanceRecord.md).

## Current source: illustrated controller guidance

The preparation screen uses a left-trigger illustration and large A/B keycaps. The warm border and `TRY NOW` show the first unfinished check; `READY` confirms each actual input, and the count shows how many of the three checks are complete. The order is a suggestion, not a new gate. Squeeze the left trigger (keyboard C) to place the task, press A and B once, then use X/Enter when the existing preparation and service checks permit continuation. These cards are instructions for physical buttons, not pointer-clickable UI.

The A/B meanings remain visible after checks and in a compact legend on subsequent instruction pages. The trial HUD is unchanged. Source sessions record `controlHintRevision=controller-guidance-v1`; the existing dev12 APK does not contain this source-only UI revision. [Implementation and verification](ControlHintUiReview.md).

## Run dev12 with the dev11 protocol configuration

1. Stop Play Mode. Choose **Evidence VR > Stage 2 > Enable dev11 Personalized Engineering Protocol**. The old override is backed up; configuration freezes at session start. For participants, generate a new allocation with the current tool and a unique managed enrollment index.
2. Start the updated Python service and matching `personalized-rf-pilot-v3-control70-40` policy. Select exact LSL outlets and confirm actual EEG/ECG channels, units, reference and contact. The source panel starts with a non-admitting policy; `service.py --personalized-policy` selects an explicitly reviewed policy. The dev11 configuration/HTTP contract remains supported. Old policy files/models are rejected; restart the service and begin a fresh session/calibration after updating. Existing deployment ZIPs contain their original Python policy and must not be mistaken for this update.
3. Complete seating/A/B checks and inspect source identity, EEG/ECG mapping, units and contact. Participant mode still requires ready media and a recent live/receiving service status; that connection check does not certify electrode quality. Continue directly to practice instructions and up to 14 practice stimuli. There is no timed resting baseline. X/Enter skips practice at the next boundary. Correctness feedback is shown in practice only.
4. Run eight alternating twelve-stimulus calibration blocks (six is an explicit pilot option). Calibration and both training phases have a neutral countdown and no correctness feedback. Python labels each 22.5 s window from exactly its own five trials, trains the personal model and runs held-out-block and circular-permutation checks. Model computation may take several minutes. A first failure offers one supplemental L0/L1 pair; a second retains the invalid-model status and may use an explicitly logged behavioral fallback.
5. Keep the headset and electrodes in place. The experimenter reads the calibration questionnaire aloud and records spoken answers in Forms. Then run Training 1 (96 stimuli), oral questionnaire/rest, Training 2 (96 stimuli), and the final oral questionnaire. Participant UI hides the strategy. X confirms completion, not a Forms submission. See [wording, scoring and linkage](QuestionnaireSetup.md).
6. If removal or electrode movement is necessary, pause and start a new session after checks. H on the application's keyboard records movement; supported XR presence loss also invalidates and pauses. An invalidated session cannot resume or accept late model/control results. X stops; Left trigger / C restarts from preparation, practice and fresh calibration. Unsupported presence reporting cannot certify unchanged contact. On standalone, use pause/stop and Left trigger to restart.
7. Save Unity and Python session folders. `analyze_personalized.py` retains failures, incomplete sessions, questionnaire administration and mount interruptions. Check actual exposure as well as performance.

Four-second visibility/responses, the 4.5-second cycle, onset cue, two-decimal countdown, scoring and media schedules are retained. The two observe-only stimuli are included in each phase's budget. Nominal timed duration is now 22:39 (20:51 with six calibration blocks) plus model fitting, instructions, oral questions, rest and preparation. A supplemental pair adds 108 s.

The five-trial score thresholds, circular-null assumptions and AUC floor require pilot qualification. Default policy remains unfrozen, device-unqualified, permutation-unqualified and without an AUC minimum. ACTIVE cannot override these gates. The model is frozen before training and is never refitted during Random or Bioadaptive. Continuous wear was selected instead of automatic rewear offset correction.

L0 has no added media; L1 uses complete AV films. All 18 training pairs are eligible, four remain reserved, and actual exposure is logged. Human media/volume review remains required. P007 is recorded replay only; its failed EEG windows were reproduced and audited per channel without changing thresholds or dropping channels.

## Current personalized adjustment rules

Every five completed trials, evaluate the latest twenty-trial behavior window with its existing eligibility checks. Stable requires balanced accuracy >= 0.70 and fewer than four omissions, without an RT ceiling. Struggling requires balanced accuracy <= 0.40 or more than five total omissions (at least six), not necessarily consecutive. Four/five omissions alone do not support adjustment. Correct RT remains descriptive and is null if no correct response exists; this no longer blocks difficulty detection.

With an admitted model, the mean of at least three valid recent current-condition predictions must also be <= 0.45 for L0 -> L1 or >= 0.65 for L1 -> L0. Intermediate/disagreeing or insufficient evidence gives HOLD. Quality, dwell, exposure, safe-boundary and mount gates remain active. After two failed model attempts, enabled behavioral fallback uses the revised behavior rules and stays marked as fallback. Random and five-trial labels are unchanged. [Exact rules and verification](PersonalizedControlRulesReview.md).

## Warm study presentation

The scene uses cream walls, oak surfaces, static bookshelves/plants, linen seating and fixed baked lights. Check `environmentRevision=warm-study-v1` and application `2.0.0-stage2-dev12` in `session.json`. Keep this environment consistent across conditions and record revisions when comparing sessions. The configuration/HTTP contract remains dev11; use the existing **Enable dev11 Personalized Engineering Protocol** menu with the updated Python policy. Model admission and signal preparation are unchanged. Before participant use, inspect the actual Quest Pro for contrast, color recognition, seated scale, comfort, distraction and performance. [Review](LabEnvironmentReview.md).

## Retained device/presentation rules

Meta Quest Pro remains the deployment and genuine-eye target. Engineering eye rays/outlines are default off and forbidden in participant sessions; invalid eyes never become head-direction gaze. Sphere/tray gap is 0.0864 m, tabletop 0.65 m, shared ROI `gaze-roi-v4-sphere-tray-area`. Dev11 records `personalized-lab-v2.3-preparation-to-practice` with flow `preparation-practice-calibration-no-rest-v1` and oral administration `interviewer_oral_headset_on-v1`. Historical Quest Pro Link acquisition is not full dev11 standalone, presence-sensor or physiological acceptance.

---

## Preserved Stage 1 experiment guide

For project progress and major changes, see [Project status and update log](ProjectLog.md).

Open `Assets/TaskPlatform/Scenes/NBackExperiment.unity` in Unity 6000.3.27f2 and enter Play mode. All participant-facing and project-authored content uses English. Meta Quest Pro is the intended deployment and genuine eye-tracking target. Earlier Quest 3 checks remain historical evidence; physical Quest Pro acceptance is pending. See [Quest Pro support and eye permission](QuestProSupport.md).

## Controls

| Action | Quest controller | Desktop keyboard |
|---|---|---|
| Match | Right A | A |
| Non-match | Right B | B |
| Continue / start block | Left X | Enter |
| Pause / resume | Left Y | P |
| Place task area during preparation | Left trigger | C |
| Restart while paused | Left trigger | C or R |

These are the default bindings. The displayed response prompts follow the configured action mapping. Only the first valid response is accepted. Held buttons do not generate repeat answers. Simultaneous responses and presses on the trial-transition frame are rejected conservatively. Releasing and pressing again is necessary for another response. All prompts are controller instructions, not pointer-clickable UI buttons.

Practice and formal feedback use the countdown: green `Correct`, red `Incorrect`, or red `No response`. It does not reveal the expected answer or offer a retry. The color resets for the next stimulus. Formal scored trials use the same green/red feedback by default (`formalFeedbackEnabled=true`). Warmups remain neutral and observe-only. Response controls are explained before each block.

## Session procedure

1. The experimenter checks configuration version, seed and formal block order, then records the anonymous participant code against the generated session ID externally. The app does not request names.
2. The participant sits comfortably, looks straight ahead and presses the left trigger. The task is placed at initial eye height, 1.5 m forward, then stays fixed in world space. Press A and B once each. All three preparation cards must say READY before X continues.
3. Read the 0-back rule: match BLUE; all other colors are non-matches. Press X. Observe two unscored warmups without answering, then complete eight scored practice trials. Practice includes answer feedback.
4. Read the 2-back rule and static BLUE > ORANGE > BLUE example. The third color matches two positions ago. Press X, observe two warmups, then complete 12 practice trials while continually updating color memory.
5. Confirm understanding before formal testing. If needed, pause and restart as a new session; the incomplete session remains saved. Repeating only one practice block is not implemented.
6. Read the first formal rule and press X. The block contains two warmups plus 30 scored trials, taking 144 seconds. The full instruction board gives way to the freestanding stimulus, workbench and compact rule/countdown signs. A quiet tone marks each color onset. Correct answers turn the countdown green; incorrect answers and omissions turn it red. There is no score panel or response-dependent audio.
7. Rest for at least 25 seconds. The next rule is shown. After the countdown, X opens the instructions and a second X starts the next block.
8. Complete the other formal block. The completion screen confirms that responses have been saved. Wait for the experimenter before removing the headset. X creates a fresh session without overwriting the previous one.
9. Export the files and recompute metrics with the Python tool below. Inspect omissions, interrupted trials, configuration and scheduling deviations.

Default formal order is Easy then Hard. Set `hardFirst=true` for Hard then Easy; practice order stays unchanged. The v1.3 task intervals total 396 seconds (45 + 63 practice; 144 + 144 formal), plus 25 seconds of rest. Preparation, rule reading and pauses are additional.

### Reading the sequence guidance

One session is the complete run, containing four task stages: Practice 1, Practice 2, Main Block 1 and Main Block 2. The break is between the two main blocks. Instruction screens identify the complete stage and block, such as `STAGE 3/4 / MAIN BLOCK 1/2`. The minimal running header uses `Main 1/2 | 0-back | Color 7/32`.

`Color 7/32` identifies the seventh presented color, including the two warmups. Counts restart at each block. The fuller pause screen also shows the scored response-trial count. During warmups, the minimal rule says `Observe only`.

For 0-back, always compare with the named target color (BLUE by default). For 2-back, compare with two positions earlier: #3 with #1, #4 with #2, #5 with #3, and so on. The current instruction states the exact pair, such as `Compare #7 with #5`. Remember the earlier color yourself; running trials do not display color history or reveal the answer. Same color means MATCH; a different color means NON-MATCH.

## Timing, sequence and analysis

Each trial has a fixed 4.5-second cycle: show the sphere and accept responses for 0–4 s, then hide the sphere and close the response window for 0.5 s until the next onset. The current shared countdown displays two decimals (for example 4.00, 3.27, then 0.00); the deadline is unchanged. An onset tone is requested once per color, including warmups. Early responses never hide the sphere, advance the trial or stop the countdown. Warmups reject responses and display Observe only. Pause freezes remaining visibility and response time; resume does not replay the tone. Each formal block has exactly ten match and twenty non-match scored trials.

Each block has its own memory history and seed. The generator uses a fixed xorshift32 implementation and Fisher–Yates shuffle; block seed = session seed + block index × 104729, using 32-bit arithmetic. Identical configuration and seed reproduce the colors and answers. Randomization does not forbid all coincidental runs or impose an unspecified maximum streak length.

Pause preserves the remaining trial time and marks the active trial `interrupted=1`. RT is measured from actual software onset to response and includes pause time. The summary reports both all completed trials and clean trials excluding interruptions. A player-loop stall over 250 ms automatically pauses instead of silently skipping stimuli. The pause event records its actual handling time and separately identifies the frozen interval.

Accuracy = correct / scored trials, counting omissions as incorrect. Balanced accuracy is the average of match recall and non-match recall. An omission is never a correct non-match. RT summaries use correct responded trials. Aborted incomplete trials are reported separately.

## Configuration and presentation

Current application is `1.7.1-stage1`, presentation `spatial-lab-v1.7.1-more-video`, configuration `stage1-v1.7.1-more-video`. The tabletop is fixed at 0.65 m above the virtual floor. The sphere stays visible for all four response seconds. Running UI is reduced to stage/mode/color count, a short comparison rule and the countdown; practice and formal feedback are colored on the countdown. Floor tracking uses zero rig height offset; Device fallback normalizes seated eye height to 1.20 m. Sit facing the front wall and use the left trigger to calibrate. The large instruction panel keeps at least 12 cm clearance above the tabletop; the running labels sit forward of the sphere support and use narrow outlines for video contrast. See `UiClearanceReview.md` and `FrontWallCalibrationReview.md`.

Version 1.7.1 schedules five ten-second front-wall videos, one door sound, one passing vehicle, one approaching footstep clip, one conversation, one news clip and one near-ear soft voice per formal block. Video onsets are 24.6 seconds apart (about 5.5 trials); six audio events are interleaved, giving eleven total events per default formal block. Each video uses one of five seeded front-wall positions without consecutive repeats. Practice remains distraction-free. The schedule is balanced between Easy and Hard, non-overlapping and saved with a separate resolved random seed. All media is local. See `VideoFrequencyReview.md` for the current schedule and `SpatialAudioFeedbackReview.md` for media, audio paths and feedback. Speech uses two synthesized English voices. Six added news scripts preserve dated source links; near-ear clips are quiet speech, not true whisper synthesis.

Version 1.3.1 introduced presentation `spatial-lab-v1.3.1-satin-sphere`. The user-approved sphere refinement adds a satin lit material and smoother surface tessellation. Base colors, physical radius, placement, four-second response timing and scoring are retained. Shading changes apparent surface brightness; session metadata distinguishes this revision from the earlier unlit stimulus. See `StimulusMaterialReview.md` for visual checks.

The default configuration is `Assets/TaskPlatform/Config/default-task-config.json`. Color indices: 0 BLUE, 1 ORANGE, 2 GREEN, 3 MAGENTA. Sphere diameter is `2 × distance × tan(visualAngle / 2)`; defaults yield about 0.157 m at 1.5 m (6 degrees).

The v1.1 quiet-lab presentation adds a warm neutral room, architectural seams, matte rounded panel, English typography, preparation checks, button legends and a static 2-back example. The existing unlit stimulus colors are retained. The room and panel remain identical across formal conditions, with no animation, music, post-processing or feedback. `presentationVersion` in session.json identifies this visual revision; do not silently pool it with v1.0 without considering the changed visual environment.

Version 1.2 adds persistent stage/block labels, color and response-trial counters, explicit warm-up instructions, numbered examples and current comparison indices. It records `quiet-lab-v1.2-sequence-guidance` in session metadata. These cues may affect task experience; review comparability before combining presentation versions. Sequence generation, timing, colors, geometry and scoring are unchanged.

Version 1.3 implements the user's approved spatial layout and timing revision. That revision used configuration `stage1-v1.3-response4s`, presentation is `spatial-lab-v1.3-response4s`, and application is `1.3.0-stage1`. The sphere retains its 1.5 m distance, 6-degree diameter and unlit colors; it floats above a grounded workbench plinth with separate world-space signs. Instruction, rest and completion screens still use the full board. The onset cue is a 120 ms mono 660 Hz sine with a smooth envelope, fixed pitch and default gain 0.12. `onsetCueEnabled` and `onsetCueVolume` are frozen into each session configuration. There is no music, ticking, or answer-dependent sound. Audibility depends on headset output and volume and must be checked physically.

This revision changes response time, auditory cues and visual context. Treat it as a new protocol revision; do not silently pool its data with earlier versions. In v1.3 the sphere was visible for one second; v1.5 extends visibility to all four response seconds. Existing custom configuration overrides retain their explicitly configured timing; remove or update an old override to use the new defaults.

`matchAction` and `nonmatchAction` default to RightPrimary and RightSecondary and can be swapped. Continue and pause use `advanceAction` and `pauseAction`. Actions are pre-authored in `TaskControls.inputactions` with Oculus Touch and Quest Pro Touch layouts. Every operation must use a distinct button action; changing the action asset requires rebuilding.

To override a device configuration, place a complete `task-config.json` in `Application.persistentDataPath` and restart the app. Each session deep-copies and saves its configuration. Update its version when changing parameters. Runtime file edits never alter an active sequence.

`TaskEngine.RequestDifficulty(mode, now, frame)` accepts requests only in Preparation, Instructions or Rest, and rejects Running/Paused requests with a logged event. Each new block rebuilds the sequence and warmups. No automatic adaptation or physiological processing exists in Stage 1.

## Data and export

Editor: `%USERPROFILE%/AppData/LocalLow/EvidenceVR/Evidence-Aware VR Task Platform/TaskPlatformData/editor-test/<sessionId>/`.

Quest: `/sdcard/Android/data/com.evidencevr.taskplatform/files/TaskPlatformData/participant/<sessionId>/`.

Synthetic validation data is stored separately in `Evidence/SyntheticSessions/automated/`. `sessionType` records the category as well.

| File | Contents |
|---|---|
| session.json | Session ID, type, UTC start, application/Unity/presentation versions, configuration, status |
| trials.csv | Formal records including warmups, omissions, interruptions and aborted trials |
| practice_trials.csv | Separate practice records |
| events.jsonl | Planned/actual times, deviation and frame number for stimuli, input, rules, pauses and frame gaps |
| summary.json | Metrics generated by the analysis script |

Events use `Time.realtimeSinceStartupAsDouble`. These are software events, not measured photon onset. Frame gaps are software diagnostics, not compositor dropped-frame counts. Writers flush each row; forced termination can leave an `in_progress` session, which must not be treated as complete.

`trial_countdown_start` records the window duration and whether the trial permits a response. `audio_cue_requested` records cue version, gain and software playback-request time; it is not a measured acoustic onset. The visual timer follows the same planned trial clock and pause offset as the response gate.

```powershell
adb install -r Builds/Quest/EvidenceVR-Stage1.apk
adb pull /sdcard/Android/data/com.evidencevr.taskplatform/files/TaskPlatformData ./ExportedSessions
python Tools/summarize_session.py "path/to/session-directory"
```

Enable developer mode and accept USB debugging on the headset before using ADB. Quest Link is not needed for standalone APK testing.

## Rebuild and validation

### Unity Play Mode with Quest Link

USB attachment alone does not stream Unity Play Mode to Quest. For Editor preview, start Meta Quest Link / Meta Horizon Link on the PC, then enter Settings > Quest Link > Launch Quest Link inside the headset. The PC OpenXR runtime must be Meta Link. Unity uses the Windows/Standalone XR settings in Play Mode even when the active build target is Android.

Standalone OpenXR automatic initialization and Oculus Touch controller profiles are configured for this project. `AgentScripts/ConfigureQuestLink.cs` can reapply these settings through Unity Pipeline. Once the headset is inside Link, press Play in Unity. Link runs the application on the PC; it does not measure standalone Quest performance.

For standalone testing, install and launch the APK instead. ADB status `unauthorized` means USB debugging permission must be accepted inside the headset before APK installation. This authorization is separate from the Link connection.

Reference: [Meta Link development workflow](https://developers.meta.com/vr/documentation/unity/unity-link/).

### Build and software checks

Use `Evidence VR / Apply Quiet Lab Presentation` for the scene presentation, `Evidence VR / Run Automated Validation` for core checks, and `Evidence VR / Build Quest APK` for the Android package. Core results go to `Evidence/automated-validation.txt`; independent export verification is `python Tools/verify_exports.py`.

See [AcceptanceRecord.md](AcceptanceRecord.md) for the evidence boundary and remaining headset checks. OpenXR setup follows the installed package and [Meta's configuration documentation](https://developers.meta.com/vr/documentation/unity/unity-xr-plugin/).

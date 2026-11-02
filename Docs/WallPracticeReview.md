# Front-wall films, compact UI and practice familiarization

Historical dev3 presentation review. The wall/UI/timing remain current; dev4 expands the training pool and changes content familiarity. See [expanded-library revision](ExpandedMediaReview.md).

Date: 2026-11-02. Application/configuration: `2.0.0-stage2-dev3`, Android code 15. Presentation: `adaptive-lab-v2.0-front-wall-practice`; status display: `mode-status-v2-compact`. This supersedes the dev1 right-side display and silent practice, and the dev2 three-line status layout.

## Requested changes

The user requested a larger movie area centered on the wall, less UI, and frequent random practice content for familiarization.

- The Stage 2 film surface is fixed at the front wall center: world `(0, 1.5, 2.915)` in the current room, 3.2 m wide and 1.8 m high. It stays attached to the room placement when the seated task is recentered. The renderer is disabled between films, so there is no idle rectangular placeholder. The sound source shares the wall position; its 3.5 m minimum distance retains nominal gain near the default seated position. Headset listening must be repeated for the changed source placement.
- The running display has one compact mode/control/source row and one phase/task row. The existing sphere and left countdown/feedback remain. Redundant running rule text is removed; observe-only warmups and the Stage 1 target reminder appear in the phase row. Preparation instructions are shortened. Detailed status remains in the session log and service monitor.
- Default Stage 2 practice remains 2 warmups + 12 scored 2-back trials, totaling 63 seconds. Three distinct AV-assigned training clips are shuffled with the session seed and scheduled at 10, 28 and 46 seconds, each with up to 0.5 s seeded jitter. Clips last 10–12 seconds, do not overlap, and occur about every 18 seconds, versus 32 seconds during formal training. Practice keeps the same A/B task and response feedback while films play.
- Only the three AV-assigned training clips are used in practice. Audio-assigned training clips and all four reserved pre/post clips are untouched. Every actual first decoded frame increments exposure history; later training correctly records repeat exposure. Custom shorter practice only schedules clips that fit. Pause immediately interrupts playback and cancels remaining slots; resume does not add extra exposure.
- Practice is explicitly `stage=practice`, `level=2` in trial, event, telemetry, gaze and media records. It is excluded from attainment and formal block statistics. Baseline remains silent, but now follows audiovisual familiarization and must not be described as a pre-exposure baseline.

## Interpretation and compatibility

This is a changed presentation/familiarization protocol. AV training content is now familiar from practice while audio-only content is initially unseen; counterbalancing exchanges content assignment, but does not remove that familiarity difference. Pre/post exact clips remain unseen; excerpts from the same film still limit narrative novelty. Do not pool dev1 and dev3 sessions without accounting for the revision.

The enlarged display lies behind the central task. Gaze rays inside the existing three-degree task ROI are excluded from the film ROI to prevent double counting. The film ROI is a geometric region even when its renderer is hidden; join media events to interpret actual exposure. This spatial measure cannot establish attention to the film at the overlapping center. Genuine eye tracking is still unavailable on the test Quest 3; head direction is never substituted.

Legacy dev1 overrides migrate in memory to dev3 geometry/practice fields while preserving mode, seed, participant settings and controller endpoint. Migration is logged and the session snapshot freezes the effective configuration. The current local engineering override and release template were updated explicitly; the user's existing OFF setting was retained. The service accepts historical dev1 records for replay, but physiology calibration must match the incoming configuration version exactly. Old calibration cannot authorize dev3 promotion.

The single four-second response window, 4.5-second onset interval, stimulus geometry/scoring, formal media cadence, controller thresholds and onset cue are unchanged. Stage 1 task/distraction rules remain available; its status/header layout also uses the compact display. Film readiness still requires human content and headset volume review before participant use.

## Validation

Evidence is stored in `Evidence/WallPractice/` and linked from the current acceptance record. Schedule/model checks cover 1,000 seeds across four counterbalance groups (80,003 assertions), timing, assignment, nonoverlap, history, held-out separation and configuration migration. The existing adaptive model regression passes 49,068 assertions. Python controller/quality tests pass 31 tests, including historical replay and rejection of old calibration for the new presentation.

Actual Editor playback passed 157 checks: three decoded movies completed during a full 63-second practice, followed by a silent baseline with the screen hidden. Logs contain 14 practice rows (12 correctly answered scored rows using programmatic input), all labeled practice/Level 2; planned stimulus onsets remain 4.5 seconds apart. Three first-frame events and three clean ends were recorded, with durations approximately 11, 12 and 10 seconds. These are software timings, not measured photon/acoustic onsets. Stage 1 compact HUD and observe-only reminder also passed a separate Play Mode check. Preparation, active film and baseline screenshots were visually inspected; the Stage 1 layout was checked for text fit in the running scene.

The first long-running test harness accessed a cleared trial after practice completion; its phase guard was corrected and the full test rerun successfully. A short Stage 1 probe initially tripped the existing frame-stall guard during dynamic compilation; allowing the preparation frame to settle resolved the harness issue. Neither initial probe is counted as passed. Existing Windows decoder timestamp-adjustment warnings were observed; all scheduled clips nevertheless decoded and completed. The offline bridge emitted connection timeouts as expected; this revision did not claim a new real LSL/headset check.

Android build succeeded (127.7 seconds, zero errors, one diagnostics-symbol warning); ARM64, code 15 and APK v2 signature passed. APK size is 88,752,896 bytes. Release ZIP structure/integrity and the preserved local enabled/OFF configuration were checked. Build results and hashes are recorded in the acceptance record and `Evidence/WallPractice/`. Physical Quest 3 readability, occlusion/comfort, decoder performance, sound position/level and real physiological integration remain pending. Quest Pro remains supported, not physically verified.

## Operator quick check

Start a new Play session with the enabled Stage 2 engineering override. Prepare the seated position and A/B checks, then press X / Enter to start practice. Keep answering the sphere while three movies play automatically. Press X after practice to start the silent baseline. OFF mode still permits practice movies; automatic adaptation and LSL reception are independent settings. No mode-switch button was added by this revision.

Example status: `STAGE 2 | AUTO OFF | PHYSIO OFFLINE`, or `STAGE 2 | AUTO ON: BEHAVIOR | REPLAY`. ACTIVE without the service reads `AUTO ON / HOLD`; replay waiting, partial reception, stale samples and service loss remain explicit. `LIVE (UNVALIDATED)` is a source label, not physiological validation.

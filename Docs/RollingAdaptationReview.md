# Rolling adaptation and decimal countdown review

Date: 2026-11-02. Revision: `2.0.0-stage2-dev5`, Android code 17. Policy: `rolling10-stride2-v1`. Presentation: `adaptive-lab-v2.0-rolling-countdown`; shared countdown revision: `response-countdown-v2-centiseconds`.

## Reason and scope

The user requested more timely adaptation using the most recent ten responses, updated every two trials, while retaining 108-second statistical segments, and requested a two-decimal response countdown. This revision supersedes the dev1 block-based training controller. Fixed baseline, calibration and pre/post conditions, four-second responses, 4.5-second cycles, color generation, scoring, feedback, wall geometry and media files remain as before. Stage 1 retains its protocol and receives the same decimal countdown display.

## Behavioral policy

Training uses the last ten contiguous eligible scored trials at the current level. Evaluation starts after ten eligible trials and repeats every two new eligible trials (nominally every nine seconds). Warmups and the first two scored trials after a level change do not enter the window. Both response classes must have at least three trials to support BA-based decisions. Insufficient class coverage produces HOLD, rather than changing the sequence or substituting ordinary accuracy. Overlapping windows are repeated control checks, not independent statistical evidence.

| Action | Provisional engineering rule |
|---|---|
| UP | Two adjacent eligible windows at the same level/epoch, each with BA >= max(0.75, baseline BA - 0.10), correct-response median RT <= 1.20 times baseline and at most one omission |
| DOWN | Two adjacent eligible windows, each with BA < 0.65, RT > 1.50 times baseline, or at least two omissions |
| Faster DOWN | One current ten-trial window with BA < 0.45, or at least four omissions; the omission rule remains usable without any correct RT or sufficient response-class balance |
| HOLD | All other cases, missing/stale replies, insufficient class coverage, or a failed physiological promotion gate |
| REST | Difficulty requiring DOWN at Level 0, or explicit stop/discomfort |

These thresholds are not empirically validated cognitive standards. Baseline still uses two valid, fixed 24-trial silent blocks. A baseline block below 0.75 BA, above two omissions or otherwise invalid stops progression. Two confirmations span twelve eligible trials: nominal earliest upgrade is 54 seconds from the first eligible trial, or 63 seconds including two transition trials after a level change. Poor class coverage, media deferral, response/feature quality and pauses can delay this. The nine-second update interval is not a guaranteed nine-second reaction latency: the behavior window spans about 45 seconds.

## Execution and reset rules

Requests are sent when the four-second response window closes. Replies must match session/configuration, epoch, evaluation ID, trial, block and level and arrive during the same half-second response-closed gap. ACTIVE executes a valid recommendation at the next trial onset boundary; task timing never waits for the service. OFF does not request automatic decisions. SHADOW records recommendations without applying them. Manual OFF/SHADOW requests remain queued for the 108-second statistics boundary.

If a film/audio clip is active, execution is deferred to a later trial boundary. A new rolling evaluation supersedes an older pending recommendation, preventing an old UP from surviving newer evidence. After a level change, the window and confirmation history reset and two transition trials are excluded. A real pause also revokes queued, uncompleted candidate attainment, clears pending/history and inserts two observe-only colors on resume. Interrupted or failed-media segments are excluded from control; collection resumes with a fresh window after the next statistics boundary. A same-stage/same-level statistics boundary alone does not clear valid rolling history.

Level 2 candidate attainment queues normal training completion at the next 108-second statistics boundary, preserving a complete final statistics segment. REST can end earlier and exports an explicitly invalid partial statistics row. Participant pause/stop remains immediate and does not wait for media or the controller. Existing eight-training-segment and three-consecutive-statistics-segment caps remain; a level change resets the consecutive-segment counter. A cap is not a successful attainment result.

## Media and physiological evidence

The three training slots remain at their original seeded times around 19/51/83 seconds within each 108-second segment. A mid-segment level change replaces only unstarted slots with the new modality, preserving each original onset and logging cancellation/replanning. An active clip is never cut for an automatic level change. Practice, fixed calibration and held-out pre/post schedules retain their previous behavior. No new files, gain changes or response-dependent sounds are introduced.

`behavioral_fallback` remains explicit. In `physiological` mode, UP additionally requires real, task/participant/source/configuration-validated evidence. Signal windows must be fresh, quality-qualified and wholly within the current uninterrupted condition epoch. They do not shrink to the behavioral window: EEG defaults to 12 seconds, HR/IBI to 30 seconds and exploratory RMSSD to 60 seconds. A 60-second feature can be used after sufficient continuous same-condition data even though the behavior window is shorter. Replay, missing or stale physiology cannot authorize UP. Mid-segment level-change events now update service condition coverage. Old dev4 calibration does not authorize dev5 promotion. No personal physiological feature has yet been validated.

## Data and display

- `rolling_evaluations.jsonl`: unique evaluation/epoch IDs, exact first/last trial, current level, current block, condition start, window times, class counts, BA, RT, omissions and validity. Invalid class coverage is retained.
- `controller_decisions.jsonl`: request/reply identities, accepted/rejected replies, deferred execution and actual actions. Python retains frozen policy and inputs for exact replay, including historical block-controller records.
- `training_blocks.csv`: fixed 24-trial/108-second summaries remain. Mixed-level rows explicitly use `level=-1`, `mixed_level=true` and a `levels` list. Trial rows retain their actual level. The analysis tool separately computes descriptive training metrics by actual trial level; mixed rows are never silently attributed to the final level.
- Session/configuration metadata records the new policy and countdown revision. The response countdown uses invariant two-decimal formatting, clamped at zero, including after an early answer. Feedback color/text and response deadlines are unchanged. Rendering still updates at the frame rate: decimal display is not a claim of 10 ms physical onset accuracy.
- Editor/synthetic records remain isolated from participant data. Legacy overrides migrate in memory while preserving enabled/mode/seed/endpoint; release/local configuration updates must also preserve those choices.

## Validation and remaining checks

Final checks passed: 48,128 adaptive assertions, 92 focused checks, 194,113 Stage 1 assertions, 47 Python tests, real-time Editor HTTP/media/HUD validation and 14 exact decision replays. The live upgrade occurred after 71.98 seconds because initial class coverage delayed eligibility; it was not an imposed 72-second wait. Four actual clips completed without playback failures or interruptions. Android dev5/code 17 build and signature passed. Final stamped dev5 Editor smoke also passed. Earlier live runs tested dev5 configuration/presentation before the application bundle version was stamped for the build.

Validation results and delivery details are recorded in the current [acceptance record](AcceptanceRecord.md) and [project log](ProjectLog.md), with evidence under `Evidence/RollingAdaptation/`. Restore point: `Evidence/RestorePoints/rolling-window-20261102/` (source archive, original commit and Editor override).

The live validation also exposed an existing shutdown-order problem: Unity-destroyed AudioSource/VideoPlayer references were accessed through C# null-conditionals. Stop now uses Unity-aware null checks; the second live session and shutdown had no console errors.

Quest 3 standalone behavior, decimal-text readability/comfort, media perception/performance, human content matching, real sensor validation and threshold calibration remain pending. Quest Pro remains supported but physically unverified. Shorter behavior windows and changed adaptation exposure require a new protocol version; do not pool with historical block-controller sessions without accounting for the revision.

Pipeline reference pages read from the installed `com.unity.pipeline@363b8bb9e91d/Documentation~/commands/`: `scripts.md`, `editor-lifecycle-and-observability.md`, `build-and-compilation.md`, `capture.md`.

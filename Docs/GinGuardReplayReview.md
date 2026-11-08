# Gin replay and Guard decisions

2026-11-05 · Stage 2/3 · `sliding-five-close-v1` · offline A/B evaluation

## Inputs and decision counts

Each 96-trial phase has 19 scheduled checks. The 15 checks from trial 25 through 95 can use a full scored behavioral window. A records Guard verdicts and executes the controller proposal. B enforces the verdict. Counts below refer to changes in the replayed distraction level. One ON can enable several separate movie clips.

All runs use recorded Gin EEG/ECG. The archived engineering session had eight responses among 94 scored trials and no recorded gaze or video starts. The mixed-answer fixture has 55 correct, 19 incorrect and 20 omitted responses. Its stable sections are trials 3–30 and 51–75. The fitted RF uses simulated calibration labels. Formal-RF mock runs assume admission in memory to exercise authorization. Actual participant model qualification remains pending.

| Replay inputs and mode | A switches | B switches | B ON proposals / blocked |
|---|---|---|---|
| Archived answers and original eligibility | 0 | 0 | 0 / 0 |
| Mixed answers and original eligibility | 0 | 0 | 0 / 0 |
| Mixed answers, engineering candidate basis, missing gaze | 1 at 30 | 0 | 4 / 4, candidate basis lacks an authorization path |
| Mixed answers, behavioral fallback after two assumed unsuccessful fits, missing gaze | 4 at 25, 45, 65, 90 | 0 | 7 / 7 |
| Formal-RF mock, missing gaze | 1 at 30 | 0 | 4 / 4, insufficient task-view evidence |
| Formal-RF mock, task gaze starting at trial 31 | 1 at 30 | 1 at 65 | 3 / 2, insufficient task-view evidence at 30 and 35 |
| Formal-RF mock, mixed gaze from trial 1 | 1 at 30 | 1 at 30 | 1 / 0 |

[Archived session summary](../Evidence/GinSlidingRound-Results/summary.json) · [Mixed-answer results](../Evidence/GinMixedBehaviorReplay/summary.json) · [Simulated answers](../Evidence/GinMixedBehaviorReplay/simulated-answers.csv) · [Authorization audit](../Evidence/GinGuardAuthorizationMock/review.jsonl) · [Full-gaze decisions](../Evidence/GinMixedGazeAB/decisions.csv)

## Full-gaze replay

Both conditions used the same gaze rule from trial 1. Simulated eyes targeted the task outside playback and alternated between task and movie during playback. At trial 30, balanced accuracy was 90%, the model score was 0.346, valid gaze coverage was 99.98% and task gaze accounted for all valid gaze. Both conditions authorized ON.

The first clip started during trial 31. Forward review first registered the completed clip at trial 33 and then registered one new clip every five trials through trial 93 (13 distinct clips per condition). Reverse checks triggered at trials 15, 18, 29, 30, 45 and 56 on RF state crossings. The last two checks each found two candidate media events. These are temporal matches involving simulated playback and prerecorded physiology. This run provides no causal attribution or participant outcome estimate. Neither controller proposed OFF. [Event-level timeline](../Evidence/GinMixedGazeAB/timeline.jsonl) · [Replay runner](../Tools/AdaptiveTraining/replay_gin_mixed_gaze.py)

## Next-stage training strategy explored in the OFF-deferral mocks

The [approved A/B plan](LayerABStudyGuide.md#approved-plan-for-off-annotation-and-subsequent-on-review) allows OFF in both conditions and applies the OFF annotation to later ON review. The following experiments are historical mock evidence for a separate recovery-strategy comparison.

Discussion only. The current Guard allows OFF. The proposed change would defer an automatic OFF for one five-trial interval (22.5 seconds) when the supporting difficulty evidence occurs only during video exposure and its source is ambiguous. A new decision would use fresh evidence at the next checkpoint. Manual stop would remain available. The full-gaze replay contains zero qualifying OFF proposals among its 15 opportunities. The paired mock below exercises both cases. Participant frequency, behavioral recovery and Quest Pro operation require separate evidence.

## Paired OFF timing mock (2026-11-05)

Both runs start at L1 with identical stimuli, answers, task-directed gaze and scripted RF scores. The only changed input is the placement of two ten-second AV clips. Trial 45 has six omissions in its latest twenty trials and an RF difficulty score of 0.70. Existing controller and Guard code produce OFF and ALLOW in both scenarios. The separate mock reviewer uses actual paired mock playback events, omission deadlines, qualified fixture window timestamps and Guard forward/reverse relation records. A records the mock review and proceeds. B enforces it.

| Placement of difficulty | A OFF | B OFF | Mock review evidence |
|---|---|---|---|
| Before playback | 45 | 45 | All six omissions and the high-score window ending at trial 40 precede the first clip. |
| During playback | 45 | 50 | All six omission deadlines overlap playback. Relevant high-score windows have forward and reverse matches; the fixture supplies audio confirmation. At the next checkpoint the controller still proposes OFF and fresh clip-free omissions occur at 46, 48 and 50. |

Times are simulated execution boundaries. The 22.5-second difference tests the proposed timing rule with scripted scores and persistent difficulty. Participant benefit, physiological causation and event frequency remain open. [Archived runner](../Evidence/PairedOffMock/ArchivedRunners/replay_paired_off_mock.py.txt) and [per-checkpoint results](../Evidence/PairedOffMock/results.json).

## Recovery after the five-trial wait

The recovery variant starts at L1 and uses the during-playback fixture through trial 45. Both conditions then answer trials 46–50 correctly in one second each. RF scores stay at 0.70, task gaze and clip timing follow the original fixture. The runner verifies the archived decisions through trial 45 and follows both conditions through trial 50. A executes OFF at 45 and ends at L0 (one change). B defers at 45 and stays at L1 (zero changes). At 50 its latest 20 trials still contain six earlier omissions, so the production controller again proposes OFF. The mock recheck finds zero new failures among five clip-free trials and releases the pending request without a change. This demonstrates a scripted behavioral recovery outcome within a 50-trial fixture. The response to waiting in real participants is unmeasured. [Recovery checkpoints](../Evidence/PairedOffMock/recovery.json). The archived runner used the `--recovery` option; the current study has no recovery-wait strategy.

## Next clip after recovery

Both timing cases and conditions continued with correct answers on trials 46–50. The offline runner retained the original decisions through trial 45 and exported a common ten-second mock slot one second into trial 51 (226–236 seconds, ending during trial 53). Difficulty before playback produced OFF at 45 in both conditions. Difficulty during playback produced OFF at 45 in A and zero switches through 53 in B.

The four resulting levels were passed into an isolated Unity Editor Play Mode probe using the project AdaptiveMedia component, local training manifest, front-wall renderer and VideoPlayer. The probe supplied the common slot to the component. Three L0 cases emitted silent_slot with zero decoded frames. The remaining B slot played pair_01, showed the front-wall movie, received frames 0 through 299, emitted ended/media_end and hid the screen. The replay clock and Unity playback ran as two linked steps. The full live scheduler, participant response and Quest Pro display require separate verification. [Decision checkpoints](../Evidence/PairedOffMock/next-slot-input.json) · [Unity playback receipts](../Evidence/PairedOffMock/next-slot-unity.json) · [Decoded frame](../Evidence/PairedOffMock/next-slot-frame.png).

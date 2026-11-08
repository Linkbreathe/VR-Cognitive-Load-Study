# Probe A/B study

2026-11-05, Stage 2/3. New allocations use `layer-ab-probe-v2` and control revision `behavior10-or-probe-v2`. The study compares two executions of the same adaptive controller. A logs the review and executes its original proposal. B enforces the review. Training stages adapt the distraction level (L0 silent, L1 audiovisual); the n-back task itself stays fixed within the study.

## Setup and timing

1. Prepare a fresh allocation using `Tools/AdaptiveTraining/allocate_personalized.py --layer-ab`, the participant code, zero-based enrollment index 0–5, a common allocation seed, controller URL and output folder. Each adjacent pair of indices receives opposite orders. The six [engineering allocations](../Tools/LayerAB/Examples) exercise this mapping.
2. Copy `layer-ab-144-media.json` and the `AdaptiveMedia` directory from `Builds/LayerAB144` into the Unity application's persistent data directory. Use the matching updated Unity project/player. Launch `StartLayerABStudy.cmd` and select the allocated JSON in the Layer A-B study menu. Retain a copy of the allocation with the session logs. Keep engineering data separate from participant data.
3. Complete signal checks, practice and calibration before both counterbalanced training periods, with the existing per-period oral questionnaire and rest workflow. Keep the same personal model throughout both periods. B upgrades require a qualified physiology model. The shipped policy still requires pilot threshold and device qualification before formal admission. Check the actual model report before enrollment.
4. Each training period contains 144 stimuli, including observe-only warmup, at 4.5 seconds per stimulus (10:48). At every fifth response close, from 5 through 140, the program issues a decision request. A full scored ten-trial window first becomes available at 15 in uninterrupted 2-back. The default remains 2-back and a four-second response window pending a ceiling check with pilot participants. Freeze any later difficulty revision before collecting the six paired cases.

## Decision and audit rules

| Decision | Implemented rule |
| --- | --- |
| Shared ON at L0 | Latest ten scored trials BA at least 0.80 or qualified model score at most 0.45. |
| Shared OFF at L1 | BA at most 0.60 or at least three omissions in ten scored trials, or qualified model score at least 0.65. |
| Shared history | Slide by five. Include eligible behavior across level changes. Hard interruptions delimit usable data. BA averages match and non-match accuracy and requires both classes. The model uses its latest fully acquired aligned five-trial window. |
| B authorization of ON | Current-condition ten-trial BA at least 0.80, fewer than three omissions, eligible pure-condition model at most 0.45 and sufficient task viewing (valid gaze at least 70%, task gaze at least 60%). An authorized ON begins a provisional upgrade. In particular, after playback-associated difficulty, L0 recovery authorizes only a test of L1. |
| B upgrade verification | On the second checkpoint following executed ON, require ten post-switch trials, latest applicable five-trial physiology, and at least 15 seconds of actual playback across those ten. The strict pass requires BA at least 0.80, fewer than three omissions and score at most 0.45. A pass keeps L1. A valid failure returns to L0 and locks reopening through phase end. A missed or unevaluable test returns to L0 with a retry permitted. A late ON lacking two remaining checkpoints is deferred. |
| B OFF review | Applicable behavioral difficulty allows immediate reduction. Review a model-only reduction against actual clip onset: a crossing from a previous nonoverlapping score below 0.65 into a high-score window containing a decoded movie onset can hold OFF when ten-trial BA exceeds 0.60 and omissions are below three. A single high score cannot establish a rise. There is no five-trial recovery grace period. |
| Execution | Apply accepted changes at the safe trial boundary and stop active movie and audio immediately on OFF. Record execution receipts. Evidence epochs follow actual changes; audit requests, source labels, reciprocal matches, exposure intervals and probe reasons in the Guard logs. |

For OFF annotation, a majority of error/omission timestamps inside recorded playback supports “during”; errors wholly preceding the first observed start support “before”; the remainder is unresolved. A playback-coincident physiological rise is also logged when it suppresses a model-only OFF. These are temporal classifications. The original action, review, effective action and actual execution can be inspected separately.

## Distraction and outcome measurement

The screen measures 3.84 × 2.16 m, with its center plane 0.40 m in front of the wall; the audio gain is 0.40. Six 108-second segments each have four potential starts (4, 31, 58 and 85 seconds). Each clip lasts 20 seconds with a seven-second gap. All 24 slots played to completion would occupy 480 of 648 seconds (74.1%). Use actual decoded-frame start and termination records for exposure. The initial seeded catalog had 100 unique encoded cuts from six works and 11 source videos. On 2026-11-07, eight Heidi cuts were removed; the current bank has 92 comparison cuts from five works. See [media exclusion](MediaExclusionReview.md). Some excerpts share source footage. Source links, timestamps, hashes, estimated speech and review flags are in the [catalog](../Assets/Resources/layer-ab-144-media.json). Manual dialogue and headset loudness review are pending.

Using the project Python environment, run `python Tools/AdaptiveTraining/analyze_personalized.py SESSION_FOLDER --output SUMMARY.json`. Primary outcomes are executed level changes per period and errors (including omissions) in the ten subsequent trials after each executed upgrade. The export marks partial windows and includes observed counts. Report accuracy, omissions, actual exposure and upgrade opportunities, plus TLX frustration, UEQ+ Attractiveness and final preference from the linked [questionnaires](LayerABGoogleForms.md). Retain zero-switch periods. A period with no upgrades has no post-upgrade error observation. Compare actual paired results for all six participants; direction of difference is an outcome.

## Verification

- [Python regression output](../Evidence/ProbeStudy/python-regression.txt) and [closed-loop scenario tests](../Tools/AdaptiveTraining/test_probe_study.py). The fixtures exercise observe/enforce, missing attention, single-channel agreement, score rise, invalid exposure with retry, and failed reopening with a phase lock. Model admission in the fixtures is synthetic.
- [Unity Editor receipt](../Evidence/ProbeStudy/unity.json). One 144-trial phase reached all 28 checkpoints; all six example orders were checked, catalog count was 100 and a decoded movie ended on automatic OFF.
- [Media audit](../Evidence/ProbeStudy/media-audit.json). Verified 100 distinct encoded files, 600 frames and 20-second audio tracks. Separate Quest Pro playback, gaze, on-device volume, physical timing, real personal-model admission and the pilot/participant sessions remain pending.

## Paired engineering examples

[All 28 checkpoints per arm and case](../Evidence/ProbeStudy/mock-traces.json). The fixtures supply artificial responses, gaze and RF scores and acknowledge the effective commands. The movie-coincident rise produced three switches in A and one in B (B held at trial 30). The failed reopening produced three in A and four in B (B reopened at 40, returned to L0 at 50 and stayed there). Looking away produced one in A and zero in B. When every scheduled clip stayed silent without a reported start, repeated inconclusive verifications produced one switch in A and twelve in B. This distinguishes real difficulty from a failed exposure measurement when interpreting switch counts. The [outcome-export fixture](../Evidence/ProbeStudy/outcome-check.txt) checked a full and a phase-end-censored upgrade window.

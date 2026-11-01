# Stage 1 requirements — Task Platform

English project specification derived from the supplied Stage 1 brief. The original supplied text remains in the user's attachment and the pre-refinement Git checkpoint; maintained project documentation is English.

## Approved revision: v1.4 (2026-11-01)

Current v1.6 approval (2026-11-01): lower the tabletop further to 0.65 m, correct seated tracking offsets, increase formal distractions to one every three or four trials (three of each type per default block), and use one front-wall video window with five seeded positions and no consecutive repeats. See FrontWallCalibrationReview.md.

Historical v1.5 approval (2026-11-01): lower the workbench for seated use, keep the sphere visible until the four-second response window ends, and simplify the running HUD with green/red practice feedback on the countdown. The tabletop is fixed at 0.72 m. The existing 4.5-second onset interval, distraction schedule and formal no-correctness-feedback rule remain in force. See SeatedMinimalReview.md.

The user requested eight local street videos for temporary windows on both side walls (5–8 seconds), local door sounds from rear-left/right (1–2 seconds), and quiet intelligible English two-speaker conversation outside the room (5–10 seconds). They explicitly selected formal blocks only, equal frequency in Easy and Hard, with no overlap between distraction types. The v1.4 default was two events of each type per formal block. This is an approved exception to the earlier no-extra-audio/animated-scenery baseline. The resolved random seed, asset catalog, schedule, playback and interruptions are recorded. The sphere, trial timing and scoring remain unchanged. See EnvironmentalDistractions.md.

## Approved revision: v1.3 (2026-11-01)

The user requested a freestanding sphere outside the large task panel and explicitly selected a four-second response countdown for every color, with one quiet sound at countdown start. This supersedes the original three-second/2.5-second timing and no-audio presentation below. Current timing is 4.5 seconds per trial, one second of stimulus visibility and four seconds to respond, followed by a half-second closed interval. Warmups remain observe-only. The original brief below is retained as history; current operation is documented in ExperimentGuide.md. No spatial matching rule, grabbing, correctness audio or automatic adaptation was added.

## Purpose and evidence boundary

Build a standalone, controllable, reproducible VR cognitive task platform for the Evidence-Aware Bioadaptive VR Patent Demonstrator. It will eventually provide a common environment for comparing a baseline physiological adaptive system against a system enhanced with evidence-source checks, decision review and active verification. Stage 1 does not implement either adaptive controller, EEG/ECG/eye-tracking analysis, VLMs or patent mechanisms.

Success means that presentation, responses, timing, condition switching and data are accurate and inspectable. It does not establish a cognitive-load effect or patent effectiveness.

## Presentation and interaction

Unity 6 URP, primarily standalone Meta Quest. The original target is Quest Pro; the currently available test device is Quest 3. Use a simple neutral gray room, stable soft light, no complex scenery, characters, environmental motion or music. Seat the participant. Place a world-fixed task area at initial eye height about 1.5 m ahead. Its upper section shows rules before the block and only a brief mode label while running. A stationary colored sphere has a visual diameter near 6 degrees. Lower prompts describe two fixed response buttons, default right A = match and B = non-match. Do not use gaze to answer.

Keep formal trials free of correctness feedback, sounds and brightness changes. The approved v1.1 visual refinement adds restrained architectural depth, a matte rounded panel and clearer English instructions while preserving these constraints and the original stimulus material.

## Cognitive task and defaults

- Easy: 0-back. Match a designated target color, default BLUE.
- Hard: 2-back. Compare the current color with the color two positions earlier; derive answers from the sequence rather than manually labeling them.
- Use the same sphere, four-color set, response buttons and timing in both conditions. Only the rule changes between conditions.
- Three-second trial: visible from 0–1 s; response deadline 2.5 s; next trial at 3 s. An early answer never shortens the trial.
- Two unscored warmups followed by 30 scored trials in each formal block: 32 stimuli / 96 s. Every block resets history.
- About one-third matches: default ten match and twenty non-match scored trials. Randomize their positions using a logged seed; avoid a fixed predictable pattern.
- Practice: about eight 0-back and 12 2-back scored trials, with warmups and optional answer feedback. Save practice separately.
- Formal order is configurable as Easy–Hard or Hard–Easy, separated by about 20–30 s rest, default 25 s, and clear next-rule instructions.
- Finish with a concise completion screen; provide experimenter access to raw data, accuracy, balanced accuracy, reaction time, omissions and block information.

## Configuration and extension interface

Use a unified editable configuration covering mode, color count and target, scored/warmup counts, match ratio, cycle/visibility/deadline/rest durations, sphere distance and angle, order, seed and response mapping. Save a snapshot and application/configuration versions at session start. Do not freely modify formal parameters mid-block. Changes require a new block or session and a record of the change.

Provide one difficulty-request interface. Stage 1 uses configuration or operator requests, not automatic state-based adaptation. Switch only at a safe boundary and reinitialize history; the current implementation uses block boundaries and fresh warmups.

## Implementation stages

### 1.1 — Scene and basic interaction

Deliver a Quest-capable room, fixed stimulus area, preparation/instructions/pause/completion states, seated calibration, controller input and one-response-per-trial protection. Verify headset startup, world locking, A/B recognition, pause/resume/restart, and approximately ten minutes of stable operation.

### 1.2 — N-back and parameter control

Deliver deterministic sequence generation, 0-back and 2-back scoring, fixed scheduling, warmups, practice, formal blocks, rest and the difficulty interface. Automatically check many seeds against independently calculated answers, exact counts and proportions, reproducibility, history reset and boundary/timeout/early-response/pause behavior.

### 1.3 — Logging, stability and delivery

Save session.json, trials.csv and events.jsonl, plus separately identifiable practice data. Record actual software processing time, planned time, frame number, stimuli, answers, RT, conditions, seed, versions, pauses and switches. Use Unity monotonic time and explicitly distinguish software stimulus onset from actual light emission, which would need external measurement.

Save locally on Quest and export through development tools; no online database is needed. Record frame timing relative to the device refresh rate and scheduling deviation. Deliver the Unity project, installable APK, configuration, sample data, automated reports and concise setup/operation instructions.

## Acceptance and release gate

Complete at least five full real test sessions with both formal blocks finished. Every formal stimulus, including omissions, must have a record linked to its block, mode and stimulus index. Recompute metrics from raw rows, reproduce sequences from snapshots/seeds, inspect timing/frame evidence, and reopen saved data after restarting the app. The standalone application must not depend on Unity Editor.

The end-to-end acceptance session is: enter the room, read and practice both rules, complete one formal block, rest and switch rules, complete the other, exit, export and inspect the data, then reproduce the sequence. Freeze Stage 1 / Task Platform v1.0 only after required software and physical-device acceptance. Later physiological modules must reuse the task/events/interface and preserve the evidence boundary.

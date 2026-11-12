# Thirty-four-trial independent comparison stages

2026-11-12, Stage 2/3. Protocol `comparison-three-stages34-v2`; Console `evidence-console-v1.32-three-stages34`; display `console-comparison34-warmups2-scored32-v1`; fixed threshold grouping `fixed-exposure-behavior-thresholds34-v2`.

## Approved scope and implemented schedule

The researcher resolved the conflicting initial counts in favor of **2 observe-only warmups plus 32 scored trials in each of Fixed Exposure, A and B**. Fixed has 16 scored OFF trials and 16 scored ON trials. This supersedes the new-assignment budget in [ThreeStageComparisonReview](ThreeStageComparisonReview.md); it does not rewrite historical sessions.

| Stage | Total positions 1–2 | Total positions 3–18 | Total positions 19–34 |
| --- | --- | --- | --- |
| Fixed Exposure | Observe-only, video OFF | 16 scored, video OFF | 16 scored, continuous video ON |
| A | Observe-only, initial video OFF | Existing adaptive controller | Existing adaptive controller |
| B | Observe-only, initial video OFF | Existing controller with enforced evidence review | Existing controller with enforced evidence review |

Fixed requests ON at the start of total position 19 (formal position 17). First decoded playback is logged separately. Completion stops playback. Fixed accepts no adaptive decision. A/B retain the five-response window, two-trial cadence, thresholds, model/gaze review, support probes, cooldowns, software execution receipts and no failure lock. Four-second responses, the 4.5-second cycle, feedback, sequence generation, media/audio and questionnaire flow are unchanged. No terminal controller evaluation or new recovery rule was introduced.

Each stage lasts 153 seconds of scheduled trial cycles (2 min 33 s), excluding preparation, questionnaires and interruptions. All three total 102 positions, including six warmups and 96 scored trials, or 459 seconds (7 min 39 s).

## Decision opportunities

All positions below are total, one-based positions, including warmups. Commands at response close take effect at a following trial boundary when execution conditions permit.

| Quantity | Per adaptive stage |
| --- | --- |
| Scheduled decision checks | 16: 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32 |
| Checkpoints from initial five-response availability onward | 13: 8 through 32, step 2 (formal positions 6 through 30) |
| Earliest B ON | After 8, provided behavior, current-state model and gaze all qualify |
| Last B Probe ON | After 28; subsequent checks at 30 and 32 can complete four eligible probe trials |
| Last ordinary ON | After 32; two trial cycles remain |
| New decision after 34 | None; stage completion ends exposure |

A+B have **32 scheduled checks in total**. ON and OFF share those checks; they are not 32 openings plus 32 closings. The current video state determines which direction is considered and HOLD remains possible. Fixed adds no adaptive opportunity: its OFF-to-ON transition is pre-scheduled. The 13 later checks are timing opportunities, not guaranteed eligible decisions; missing evidence, response-class coverage, state changes, interruptions and cooldowns can reduce actionable opportunities. A can use its existing model-only base trigger before a complete behavioral window; B ON still requires agreement. No expected switching frequency or participant effect is inferred from these counts.

## Reporting and historical compatibility

New assignments freeze the 34-position revision and budget. Unity and Python still recognize the historical `comparison-three-stages30-v1` allocation with its original 30 positions and ON at position 16; 60/144 allocations are unchanged. New setup refuses to create assignments against a backend that does not advertise the new revision. Existing recordings and model thresholds are not modified.

The fixed-stage estimator keeps non-overlapping five-response groups and the same behavior-anchor requirements. New OFF groups are 3–7, 8–12 and 13–17; ON groups are 19–23, 24–28 and 29–33. Positions 18 and 34 remain recorded experimental outcomes but are not padded into incomplete five-response groups. At most six groups are now available. Completion requires all 34 recorded positions. Historical reports retain their original five groups. Report background boundaries and group labels follow the recorded protocol revision. These remain exploratory, fixed-order within-session candidates; no automatic threshold application, held-out validation or causal claim is added.

## Validation and deployment

Evidence is isolated under `Evidence/Comparison34-20261112/`; participant data are not used. Pre-edit copies are in its `restore/` directory, including existing uncommitted work. Pipeline documentation consulted: `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md`, and `commands/runtime.md` from the installed package.

Passed:

- All 269 AdaptiveTraining Python regression tests (86.689 s), including 44 targeted comparison tests, the new 34-position schedule/report groups and preserved historical allocations.
- Unity compilation; deterministic TaskEngine execution of all 102 positions with 32 scored results in each stage, exact Fixed levels, A/B checks 2–32 and independent completion.
- All three actual isolated backend configurations validated and round-tripped through Unity without differences; historical 30-position configuration contracts validated.
- Full real-time 34-position Fixed Editor Play Mode run with the real local decoder: zero sampled OFF leaks and zero sampled ON gaps; first decoded frame at total position 19, 42.1 ms after the ON request. Completion hid/muted playback. This is engineering decoder evidence, not headset timing acceptance.
- Browser checks for three isolated assignments and four synthetic report fixtures at 1440 px and 390 px, including candidate/no-candidate displays, new schedule chart labels and old-v1.31-backend rejection before a preparation request; no browser JavaScript errors or page overflow. Setup and candidate-report screenshots inspected.
- JavaScript syntax and incremental whitespace checks passed. Final Unity ground-truth console errors: zero; compilation failed: false.

Original scene restored to `Assets/TaskPlatform/Scenes/NBackExperiment.unity`; Play Mode stopped. Auto-tick was already enabled and remains enabled. The isolated port-8773 service was stopped. No participant session or hardware acceptance test was performed. System Anaconda Python is not the project test environment (NumPy ABI / missing dependency errors); backend tests use `Tools/AdaptiveTraining/.venv/Scripts/python.exe`, while browser checks use the existing Playwright-enabled interpreter.

Production deployment verified: the idle port-8765 service had no prepared/claimed/running assignments and stale task telemetry before restart. Restarted with its existing LSL selection, policy and host. `/console/health` now reports `evidence-console-v1.32-three-stages34` and `comparison-three-stages34-v2`; no active assignment was created. Refresh Console and create a fresh assignment in a new Unity session. Installed Players need a rebuild. Worn Quest Pro, live Mindtooth threshold usefulness, physical audiovisual timing and participant acceptance remain unverified by these engineering tests.

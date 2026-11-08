# Compact decision evidence with recorded gaze

2026-11-08 · Stage 2/3 · Console v1.21 · `console-decision-gaze-v1`

## Scope and operation

The Run monitor and session Decision evidence page share a compact decision card. Select a checkpoint to see the proposal, patent review, delivered command, matched Unity receipt and recorded RF thresholds. At decision / After decision switches the spatial view, region distribution and expandable trial timeline together. Criteria, provenance and source records are collapsed by default. The A/B report summary is also collapsible. Container queries stack the heatmap and distribution in narrow panels.

The monitor refreshes recorded spatial context every five seconds. The archived report uses Refresh. JSON downloads retain both periods, intervals, frame identifiers, density bins, denominators and source records. The view is read-only: it does not change model scores, thresholds, gaze classification, controller rules, timing or media.

## Data and interpretation

- Read `telemetry.jsonl` incrementally and deduplicate rolling `gazeOverview.samples` by stage, projection frame and sample ID. Accept only `tracked_eyes` / `gaze-console-front-plane-v1`, finite coordinates, valid tracked eyes and a valid projection layout. No headset-direction substitute is introduced.
- Decision-time positions must have appeared in telemetry by the decision's Unity timestamp. Late positions do not retrospectively become decision-time evidence. Incomplete JSONL tails wait for the next read.
- For playback review, use the recorded `gazeIntervals`. For opening review, preserve its existing full-trial intervals. Other branches show contextual pre-response intervals and explicitly say when gaze was not evaluated. No authorization rules are recomputed in the page.
- Follow-up is a separate period, through the next recorded checkpoint (or the next two trial positions when no checkpoint is recorded yet). It does not supply evidence to the earlier decision or establish correctness/benefit.
- Each point contributes elapsed time until the next sample, capped at 100 ms and clipped to the selected intervals. Gaps are not interpolated. The terminal sample without a successor contributes no extrapolated duration.
- Bin recorded positions on a 48 by 32 grid of the front reference plane. Each frame is separate; the initially displayed frame has the greatest mapped duration. Report outside-view duration and allow frame selection. Heat colors use a local seconds-per-cell scale, not a normalized workload score or a fixation detector. Object outlines are schematic location context, not proof of playback or calibrated screen accuracy.
- Region percentages use the recorded gaze audit; their denominator includes missing/invalid gaze. Task share uses valid eye time. Spatial coverage can differ because coordinates arrived late, are outside view, are missing, or belong to another frame. The recorded rule's actual gaze values are shown separately.
- Desktop fixed gaze and seeded simulator sessions never generate spatial positions. Historic recordings without coordinates show an explicit empty state. A recorded `tracked_eyes` label establishes the software path, not physical Quest Pro acceptance.

## Validation

- 31 Python regressions passed: decision gaze, existing probe study, compact comparison, editable thresholds and Console projection. New contracts cover deduplication, partial JSONL, interval clipping, gaps, late arrival, invalid/unprojectable eyes, stage/frame separation, out-of-view coordinates, simulated-source exclusion and follow-up separation.
- JavaScript syntax checks passed for the shared card, monitor and report scripts.
- Browser checks on the production page code with a clearly separated synthetic fixture exercised both periods, criteria expansion and real-coordinate density rendering. Widths 320, 390 and 1100 pixels had no document horizontal overflow; the heatmap/distribution stacked on narrow widths.
- Production v1.21 started successfully on port 8765 (the previous service was not running). The report endpoint returned `decision-gaze-density-v1` for Wei_004's archived 20261107T203024_5e0116214361 session: 57 checkpoints, explicitly fixed simulated gaze, no fabricated spatial map. This is archived Editor evidence, not a worn-device test.
- Fixture data and its isolated server are under `Evidence/DecisionGaze-20261108`; no synthetic trials were added to participant recordings.

## Pending acceptance

A worn Quest Pro session must confirm eye permission/calibration, tracked-coordinate delivery, reference-plane alignment, adequate retained coverage, frame changes and decision/receipt synchronization. Mindtooth EEG and model validity remain separate checks. No new Player build or physical acceptance is claimed.

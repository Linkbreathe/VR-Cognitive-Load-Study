# Attribution claims, session reverse bands and linked ON review

**Current 60-trial scope (2026-11-08):** `current-condition-no-opening-test-v1` retains claims, relations, final OFF notes and their links to later ON proposals, but the linked note is historical context only. It adds no verified-reopening requirement, opening test or failure lock. The 144-trial allocation keeps its existing verification. This supersedes the 60-trial verification statements below; the historical revision evidence is retained. See [current-condition review](CurrentConditionReview.md).

2026-11-08 · Stage 2/3 · revisions `attribution-claim-v1`, `reverse-bands-session-cutoffs-v1`, `off-annotation-on-review-v2` (v1 superseded, see below), `workload-feature-reference-v1`, `evidence-relations-report-v1`, `recorded-session-replay-v1`

This revision implements the approved 2026-11-05 plan for OFF annotation and subsequent ON review (see [LayerABStudyGuide](LayerABStudyGuide.md#approved-plan-for-off-annotation-and-subsequent-on-review)), together with two researcher decisions made on 2026-11-08:
- The reverse-check bands follow the session's actual thresholds.
- An attribution-claim module is required.

The researcher accepted all five recommendations in the design proposal:
1. Adopt the predominance criteria below.
2. After difficulty during playback, permit an ON only as a verified reopening (option 2).
3. Treat mixed evidence like difficulty during playback.
4. Require a minimum crossing change of 0.05 and an EEG feature change of at least 1 calibration SD.
5. Leave the existing B OFF-deferral rule unchanged and record whether it agrees with the claim.

## What changed

**Reverse bands.** The Evidence Guard engine accepts optional per-window `reverse_bands`. For workload-control comparisons, the adapter supplies the session cutoffs in effect at that moment: stable ≤ ON, struggling ≥ OFF, with a minimum change of 0.05. Live threshold edits are honoured per window and recorded with their revision. A window without bands keeps the original fixed 0.45 / 0.65, so the earlier protocols are unchanged.

**Attribution claims** (`attribution.py`). The adapter writes a claim before ingesting any score window that crosses the bands. The claim contains:
- the candidate clip: a decoded clip overlapping [window start − 10 s, window end], with currently playing clips included and the largest overlap first, or "no candidate clip" when there is none;
- the change-onset interval, from the end of the previous comparison window to the end of this window. The engine receives the latest bound, so a dispute is recorded only when the change was certainly complete before the clip could act;
- the EEG feature change. This is the mean, over the model's eight inputs, of the change projected onto that participant's calibration workload direction, divided by the pooled within-condition SD.

The engine judges each claimed clip–signal relation with its existing rules. The old `physiological_change_confirmed` field remains false. The workload RF has no video input, so a movie-fraction counterfactual does not apply.

**Calibration statistics.** New workload models store `featureReference`: per-feature means for each class, the pooled within-condition SD and the load direction. Task score windows record their eight feature values (`loadFeatures`). Scores are computed exactly as before. Models saved earlier have no statistics, and their feature check reports "not available".

**OFF source annotation** (A and B). Each OFF proposal records a provisional annotation:
- **Behavior:** more than half of the window's errors during decoded playback → during; all errors before playback → before; otherwise mixed; no errors → none.
- **Physiology** (model-triggered OFF only): the latest upward claim in the current-state window. A supported relation → during; no clip or a disputed relation → before; a pending relation → unresolved.
- **Combined:** agreement, or a single source, gives that result; disagreement gives mixed; nothing usable gives insufficient.

The OFF is never delayed.

**Linked ON review.** Each ON proposal links the round's latest executed OFF and re-resolves its annotation with the relations that exist by then; a closed clip's relations are only computed after the clip ends. A result of during or mixed requires a verified reopening. Verified reopening is the existing ten-trial verification, and a failure locks openings for the round. As before, an OFF during an active verification also requires one; the record states which basis applied. An executed ON clears the link, and history resets between rounds.

**Report and Console.** `comparison_report.py` adds `evidenceRelations`: per-round forward/reverse summaries, played-clip spans with relation shares, reverse markers and, for each checkpoint, its score window's claim, relations and reverse result.
- The Session report shows an Evidence relations group in the summary.
- The timeline shows played clips coloured by relation state, with reverse-check markers.
- Each decision card has an Evidence relations section: reverse claim, forward relations with exposure checks and open alternatives, OFF source annotation and linked OFF.

**Offline replay** (`replay_recorded_session.py`). Feeds a recorded session's Unity packets and EEG blocks into a fresh service in their original receive order. It uses its own registry and output folder, redirects model publication, and compares every decision with the original. The replay is open loop, so it is valid only up to the first changed command. It can inject a calibration `featureReference`, but only after checking that its forest hash matches the saved model.

## Wei_004 replay (engineering data, Desktop simulated gaze)

Input: comparison session `20261107T203024_5e0116214361` (1,092 packets, 3,057 EEG blocks, 57 decisions) and calibration session `20261107T201418_0af9dfa746f5`.

| Check | Result |
|---|---|
| Current code (before this revision) reproduces the original | 57/57 delivered commands and base decisions identical |
| Calibration replay reproduces the saved model | Forest SHA-256 identical, held-out AUC 0.870, recommendation 0.48 / 0.56, 18 + 18 windows |
| New code: delivered commands, Guard verdicts and reasons | 57/57 identical; no divergence, so the open-loop replay covers the full session |
| Forward checks / relation states | 77 / 68 relations, 36 supported and 32 unresolved, all unchanged |
| Reverse checks | 0 → 25 (A 10, B 15; 11 up, 14 down; 15 with a candidate clip, 10 without) |
| Claim relations | 11 relations now also carry the reverse direction. Four claims named a clip that was still playing; their relation was judged when the clip ended. No disputes. |
| EEG feature change consistent with direction | 8 of 25 crossings (3 of 11 up, 5 of 14 down) |
| OFF annotations | 7 during playback, 4 insufficient |
| Linked ON reviews | 18: 16 by difficulty during playback, 2 by an OFF during verification. All require a verified reopening. |
| Existing OFF-deferral rule versus claims | 7 model-triggered OFFs: the existing rule found no playback-coincident rise; at decision time the claims gave none (5) or unresolved (2) |

The commands did not change because B already verified every ON. The approved option 2 therefore adds evidence, explanation and linkage without altering this session's video exposure. Option 1 (no reopening in the round) would first diverge at B26; it would need a closed-loop run.

Notable cases:
- **B32**, a model-triggered OFF, was "insufficient" when executed because its clip was still playing. At B34 it resolved to "during playback" once the relation was supported.
- **B30**'s earlier rise (window 22, no clip) precedes the current video state, so it is outside the annotation window.
- For Wei_004 the parietal alpha features rise in arithmetic, not fall. The check uses the direction learned from calibration, not a fixed textbook direction.

Evidence (local, `Evidence/` is ignored): `Evidence/AttributionReplay-20261108/` contains `baseline-head/`, `calibration-head/`, `calibration-new/`, `comparison-new/` (with `replay-summary.json`), `differences.json` and `screens/`.

## Revision v2: turn-off note answers at the turn-off (2026-11-08)

The researcher found that the turn-off note almost always read "not enough evidence". On the Wei_004 replay with v1, the score clue never gave an answer: it relied on a reverse crossing inside the current video period and on a clip relation that exists only after the clip ends. The researcher approved `off-annotation-on-review-v2`:

- **Clue 1 (mistakes)** is unchanged: did most errors in the window happen during decoded playback?
- **Clue 2 (workload score)** compares the last score that ended before the current playback began with the highest score during it, against the session OFF cutoff:
  - before below the cutoff and peak at or above it → rose while the video played;
  - before already at or above the cutoff → already high before the video;
  - peak below the cutoff → did not reach the line;
  - missing scores → no score available.
- Both clues are used for every turn-off. The note is final at the turn-off and is reused unchanged at the next turn-on review; v1's provisional result and later re-check are removed. Attribution claims and forward/reverse relations are still recorded and shown, but no longer feed the note. The agreement record now pairs the existing rise rule with the clue-2 answer.

Wei_004 replay (`Evidence/AttributionReplay-20261108/comparison-v2/`): 57/57 commands, verdicts and reasons unchanged. Turn-off notes changed from 7 during and 4 insufficient (v1) to 6 during, 4 before and 2 mixed (v2):
- A12, A46, B30 and B32 changed from insufficient to before: the score was already ≥ 0.60 before the video.
- A32 and A50 changed from during to mixed: their errors fell during the video, but the score was already high before it.
- B34–B58 still require a verified reopening. The basis is now that B32 happened during B26's verification; v1 gave difficulty during playback.

Because this participant's score hovers around the cutoffs, a before/during answer from a single comparison is sensitive; the card shows both numbers. Tests: 218 Python tests passed (new clue-2 and reuse tests). The card was served-browser checked on B32.

## Validation

- 217 AdaptiveTraining Python tests passed, including 11 new ones covering bands, crossings, the minimum change, candidates, feature direction, claims, annotation rules, linked ON resolution and basis, engine default and per-window bands, and report projection. The 14 Evidence Guard tests and the JS display contracts passed, and every Console script passed syntax checks.
- Served-browser check of the replayed report at 1440 px and 390 px: summary relation rows, coloured clip timeline, B16 and B34 decision cards. No document overflow.

## Boundaries

- Relations, claims and annotations describe temporal association and exposure, never causation. Audio exposure is the software playback state. Visual exposure requires Quest Pro tracked eyes; this replay used Desktop simulated gaze, so it had none.
- The 0.05 change, 1 SD threshold and predominance rules are engineering choices without validation.
- The replay is open loop; it reuses recorded behavior and is not a new participant run.
- No headset, worn EEG or participant check was performed, and device acceptance is unchanged. The running service needs a restart, and a fresh assignment, before it records these revisions.

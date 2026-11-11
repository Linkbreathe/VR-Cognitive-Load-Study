# Selective support invalidation and logged probe (Round B)

Latest 2026-11-10 follow-up: ordinary 60-trial behavioral OFF is now BA <= 0.50; ON remains >= 0.65. The independent two-mistake/omission probe failure rule is retained. This supersedes the 0.60 ordinary OFF statement in the preceding ON-only revision below. [Current cutoffs](OffBAThresholdReview.md).

2026-11-10 follow-up: the researcher lowered only the 60-trial ON BA requirement to 0.65, including Probe ON. OFF remains 0.60 and model-only OFF deferral remains 0.80. The current rule table below reflects this change; original validation history remains historical. [Threshold revision and evidence](OnBAThresholdReview.md).

Current gaze thresholds were revised on 2026-11-10 to >= 60% valid eye coverage and >= 40% task share of valid eye time; see [Gaze threshold review](GazeThresholdReview.md). Earlier validation evidence below predates this threshold revision.

2026-11-08, Stage 2/3. Backend review `current-condition-support-probe4-cooldown2-v1`, support policy `selective-support-probe4-cooldown2-v1`, Console `evidence-console-v1.23-support-probe`, display `console-support-probe-v1`. Builds on [the v1.22 current-condition review](CurrentConditionReview.md), which removed the six-trial test, failure lock and trial-52 cap.

## Decision and scope

The researcher requested an incremental change for the 60-trial continuous comparison. After the video is turned off because difficulty began during playback, good performance with the video off cannot directly re-authorize an ordinary opening. Direct opening is suspended. The next opening is a logged four-trial probe that re-acquires comparable evidence with the video on. A failed probe adds a capped cooldown and never locks openings for the round. A probe ended by a technical playback problem is inconclusive and adds no cooldown.

Applied only to Round B (enforced review) of 60-trial sessions. Round A records no support state and executes the base proposal as before. The 144-trial allocation keeps its ten-trial verification and failure lock. The five-trial window, two-trial cadence, thresholds, timing, media, model and Unity source are unchanged. Old recordings are not rewritten.

## Rules

| Item | Rule |
| --- | --- |
| Support flag | `cross_condition_on_support_invalidated` (recorded as `support.invalidated`), initially false at the start of each Round B. |
| Invalidation | Set at an executed Round B OFF whose final turn-off note is `during` or `mixed`. Mixed follows the approved conservative rule that treats it like difficulty during playback. Notes `before`, `none` and `insufficient` do not invalidate. Any executed OFF during a probe is a probe failure and keeps the flag set. |
| ON evidence | Unchanged: at least the required number of eligible current-OFF trials, latest five with BA >= 0.65 and fewer than two omissions, a pure current-state model score <= ON, valid eye coverage >= 60% and task share >= 40%. |
| Normal ON | Flag false: the reviewed ON is executed directly as `Normal_ON`, without a probe. |
| Probe ON | Flag true: the same evidence authorizes only `Probe_ON`. Unity receives the ordinary ON command; the controller and audit record `Probe_ON`. |
| Probe period | The first four eligible scored trials after the probe ON executes, judged at the two following checks (about 18 s). |
| Probe failure (`Probe_Failed`) | At either check: two or more mistakes or omissions among the probe trials, or an OFF allowed by the ordinary Round B OFF review (for example a pure current-state model score >= OFF that the existing deferral does not hold). The video turns off, support stays suspended and the cooldown increases. A behavioral failure recommends OFF even if the base proposal was HOLD. |
| Probe inconclusive (`Probe_Inconclusive`) | A recorded playback failure, interruption, pause or mount interruption, or four completed trials without decoded playback. The video turns off and support stays suspended, because no comparable evidence was collected. This is not a participant failure: the required trial count is unchanged and the failure count does not increase. It does not wait for a base OFF. |
| Probe pass | Four eligible trials completed, no failure and decoded playback > 0 s. The video stays on (HOLD), the flag is cleared, the cooldown resets and the state becomes `Restored_ON`. The audit records `Authorization Restored via Logged Probe`. Later checks use the ordinary rules. |
| Cooldown | `required_off_trials` is 5. Each executed `Probe_Failed` adds 2, capped at 7; `Probe_Inconclusive` keeps the current value. A pass resets it to 5. With 7, BA and omissions are still judged on the latest five eligible trials. |
| Late openings | Normal ON remains possible at 54, 56 and 58. A Probe ON can be requested through check 54, so that it completes at 58. At 56 and 58, a supported opening that still needs a probe is held (`support_probe_window_closed`). |
| Not added | No failure lock, retry budget, timeout or recovery inference. The probe is a policy evidence-acquisition step, not a claim that the participant recovered. Relations remain temporal association, not causation, and never delay an OFF. |

## Records

Each Round B review carries `support`: policy, state (`OFF`, `Normal_ON`, `Probe_ON`, `Restored_ON`), flag, required OFF trials, probe failures, inconclusive probes, the active probe, `probeEvaluation` (trials, completed, mistakes/omissions, decoded playback, playback failure), `authorization` for ON, `outcome` and `afterReview`. During a probe, `ordinaryReason` retains the ordinary review reason. The linked OFF note uses rule `selective_support_probe` and records whether that OFF invalidated support. `evidence_guard_audit.jsonl` adds `support_event` rows for `Normal_ON`, `Support_Invalidated`, `Probe_ON`, `Probe_Failed`, `Probe_Inconclusive` and `Probe_Passed`. The report summary counts normal and probe openings, passes, failures, inconclusive probes and late holds. The Console displays an ON AUTHORIZATION block, probe checks and the new reasons.

New reasons: `support_probe_on`, `support_probe_passed`, `support_probe_failed_behavior`, `support_probe_failed_model`, `support_probe_inconclusive_playback`, `support_probe_inconclusive_no_decoded_playback`, `support_cooldown_trials_missing`, `support_probe_window_closed`.

## Implementation boundary

The specialized A/B path now implements a narrow form of selective support invalidation and renewed authorization through a logged probe. Invalidation depends on the recorded turn-off note. It does not implement the general evidence-to-judgment-to-action dependency mechanism; the generic Evidence Guard is unchanged. Whether the note correctly identifies playback-related difficulty is not validated.

The model branch reuses the existing OFF review. A current-state model window first becomes available at the second probe check, and the model-only deferral requires five current-state trials. It therefore cannot hold a model OFF during the probe. When a base OFF has no pure current-state window, the ordinary review holds it. Without another failure, the probe can then pass on behavior and playback evidence, as at Wei_004 B30.

## Revision: inconclusive probes (2026-11-08)

The researcher confirmed that `mixed` invalidates support, the probe's check timing and model handling, and the Wei_004 B30 pass as implemented. They then asked to separate technical interruptions from participant failures. Playback failure or interruption and zero decoded playback were initially counted as `Probe_Failed` with cooldown. They are now `Probe_Inconclusive`, with the reasons renamed from `support_probe_failed_*` to `support_probe_inconclusive_*`. The video still turns off and support stays suspended, but no cooldown is added. No session was recorded with the earlier classification; the service was not running it.

## Display revision v2 (2026-11-08)

Console `evidence-console-v1.23.1-support-monitor`, display `console-support-probe-v2`. Display only; no control, review or record change.
- Study setup "Decision rules" describes the probe, inconclusive outcomes, cooldown, the trial-54 limit and the absence of a lock.
- The Run monitor shows a Round B "Opening authorization" tile from a read-only `guard.support` snapshot of the controller state.
- An unfinished probe row reads "… in progress" without changing the recorded status.
- Probe outcomes are explained by their reasons rather than a "first unmet" row; badges show "Probe failed" or "Probe inconclusive".
- Narrow layouts stack ON AUTHORIZATION rows, and the fifth monitor tile spans the row.

Browser check: synthetic data only, on an isolated port. A Round B with one failed and one interrupted probe and a labeled synthetic monitor session gave 30 of 30 checks passed and no horizontal overflow at 1440 px or 390 px. Evidence: `Evidence/SupportProbe-20261108/ui-check/`. In the monitor fixture, the tile was exercised with the video both off and on. Its decision card is an unrelated synthetic decision, so probe-card rendering was checked in the report.

## Validation

- Policy unit checks (13 new, 25 compact checks in total): invalidation by note class; Normal versus Probe ON; Round A history-only notes; probe pass with restored authorization; running probe; two mistakes or omissions failing at the first check; one error passing; model failure with cooldown; playback-failure and zero-decoded-playback inconclusive outcomes keeping the requirement at 5 or 7; cooldown 5 → 7 capped with no lock; cooldown hold at 6 trials and Probe ON at 8; Probe ON at 54 and holds at 56/58; Normal ON closure without invalidation.
- Full synthetic rounds through the real base controller, adapter, execution receipts, audit and report: A is unchanged and records no support state. B gives Normal ON 8, then Probe ON 22/34/46, each passing; the probe at 58 is held. With mistakes injected into two probes, B fails at 24 and 34, holds the 7-trial cooldown at 30, reopens through probes at 32 and 42, and passes at 46. No lock occurs. A Unity `adaptive_media_interrupted` event injected at 24 ends the first probe as inconclusive. B then reopens through a probe at 30 under the unchanged five-trial requirement (32 after a failure), with no failure counted. Audit `support_event` rows match controller events, and the report counts match.
- All 238 AdaptiveTraining tests passed (after display revision v2), 14 generic Guard tests passed, JavaScript display contracts passed and Console scripts passed syntax checks.
- Wei_004 recorded closed-loop simulation (`simulate_recorded_closed_loop.py`, `UserSettings/personalized-policy.json`): commands match a v1.22 control run with support disabled in both rounds. Round A is unchanged at 90.0 s. Round B: Normal ON 12; OFF 18 (note `during`, support invalidated); Probe ON 26; probe passed at 30 (0 mistakes, 17.5 s decoded playback); OFF 32 on score (note `before`, no invalidation); Normal ON 56; 72.8 s video. The difference from the original recording at 56 comes from v1.22's removal of the trial-52 cap. Recorded responses and EEG were produced under the original schedule.
- Evidence: `Evidence/SupportProbe-20261108/` (engineering only, ignored by Git).
- Deployment: The idle service on 127.0.0.1:8765 was restarted with the same host, LSL selection and policy (hashes unchanged). Before the restart it had no Unity telemetry and no prepared, claimed or running assignment among 18 records. Health now reports `evidence-console-v1.23-support-probe` (PID 52896). The served HTML/JavaScript contain `console-support-probe-v1`, the ON AUTHORIZATION block and the inconclusive wording. No assignment was created; the next run needs a fresh assignment and Unity session.
- Not performed: participant, worn EEG, Quest Pro, physical timing or benefit validation. No Unity build is needed.

Source pointers: [controller](../Tools/AdaptiveTraining/probe_control.py), [adapter](../Tools/AdaptiveTraining/evidence_guard_adapter.py), [report](../Tools/AdaptiveTraining/comparison_report.py), [regressions](../Tools/AdaptiveTraining/test_compact_comparison.py), [display](../Tools/AdaptiveTraining/console/decision-evidence.js).

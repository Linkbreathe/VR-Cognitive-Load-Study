# Six-trial test after an opening (Round B)

**Historical rule, superseded on 2026-11-08.** The researcher subsequently approved removing this test, its failure lock and the trial-52 cap from the 60-trial comparison. New backend sessions use `current-condition-no-opening-test-v1`; A no longer records a shadow test and B uses only the remaining ordinary review. No replacement test was added. The 144-trial ten-trial verification is unchanged. See [current rules and validation](CurrentConditionReview.md). The material below preserves the earlier implementation and its evidence.

2026-11-08 · Stage 2/3 · `test6-two-errors-behavior-only-v2` (supersedes `test6-two-errors-retry1-v1`) · applies to the 60-trial continuous comparison (`behavior5-continuous-stride2-v3`); 144-trial allocations keep their ten-trial verification.

## Rule (approved by the researcher)

After every executed opening in Round B, the next **six trials** with the video on form a test. Round A records the same test without applying it.
- **Fail:** two or more mistakes or omissions among the test trials, judged at once at the next decision check. Only performance decides the test (researcher decision, v2): a turn-off during the test, for example on a high score, ends the test without a failure, and the next opening is tested again.
- **Pass:** all six trials completed with fewer than two mistakes or omissions. A pass only ends the test. The same check still applies the ordinary rules, so a high score can still turn the video off.
- **First failed test in a round:** video off; a later opening and test are allowed. **Second:** the video stays off for the rest of the round.
- **Missing trials or a playback failure:** the test is inconclusive; video off, retry allowed.
- **Last opening:** at trial 52, so that the test verdict falls on or before the final check (trial 58).

This replaces, for this policy only:
- the ten-trial verification (≥ 15 s playback, BA ≥ 80 %, fewer than three omissions, score below the OFF cutoff at the final check);
- the lock after one failure;
- the HOLD returned on a pass.

The earlier rules punished a mid-test turn-off less than a failure at the final check, judged the score from one final window, and locked the round after a single failure. Mistakes are counted directly, so the test does not need both trial classes.

For a participant at about 80 % accuracy, a single test fails by chance about one time in three. That is why one retry is allowed. The parameters are engineering choices without validation.

## Closed-loop simulation (Wei_004)

v2 gives the same Wei_004 result as v1 (`closed-loop-test6-behavior/`), because no turn-off happened during a test in that session.


`simulate_recorded_closed_loop.py` reuses the recorded responses, timing and EEG in their original order. Only the video state follows the current code: level changes, media start/end and execution receipts are regenerated at each decision boundary. With the earlier rules it reproduced the original session exactly: 0 changed commands; A 90.0 s and 14 changes, B 54.0 s and 4 changes (`closed-loop-check-current-rules/`).

With the new rule (`closed-loop-test6/`):

| | Original | New rule |
|---|---|---|
| Round A commands | 14 changes, 90.0 s video | unchanged |
| Round B | ON 12, OFF 18, ON 26, OFF 32 · 54.0 s | ON 12, **OFF 16**, ON 26, OFF 32 · **45.0 s** |
| B tests | not applicable | Test 13–18 failed at check 16 (3 mistakes in 13–16; first failure, retry allowed). Test 27–32 passed (0 mistakes); the same check turned the video off on score 0.617 by the ordinary rule. |

Only B16 (HOLD → OFF) and B18 (OFF → HOLD) changed; the round was never locked.

Limitation: trials 17–18 were recorded with the video on, but are replayed with it off, so the participant's real reaction under the new schedule is unknown. Round A's B-rule records diverge from B's real trajectory after A's own changes, and are hypothetical bookkeeping only.

## Validation

- 221 AdaptiveTraining Python tests passed. The new tests cover: pass with a high final score still turning the video off by the ordinary rule; two mistakes failing at once; the second failure locking; a turn-off during the test counting as a failure; and the trial-52 limit.
- JS contracts and syntax checks passed.
- The closed-loop harness reproduced the original exactly under the earlier rules.
- No device or participant check. Evidence: `Evidence/AttributionReplay-20261108/closed-loop-*`.

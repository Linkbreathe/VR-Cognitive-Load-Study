# Behavioral transition inclusion and switch capacity

Date: 2026-11-05. Stage 2/3 engineering candidate.
Control-evidence revision: `behavior20-stride5-include-transition-v1`.

## Requested change

Every five completed trials, inspect the latest twenty consecutive behavioral trials. A transition flag alone no longer invalidates that window. Transition trials contribute their actual correctness, omissions and correct RT; they are not dropped or replaced with older trials. Unscored, interrupted or aborted trials, insufficient history and answer-class coverage still invalidate the window. Stable/Struggling thresholds are unchanged. Valid summaries include `transition_trials`, mixed `levels` and `behaviorRevision`; every decision and the Python `personal_protocol.json` record the revision, including early HOLD decisions.

The model policy revision and model feature/label contract are unchanged: this is an online behavioral eligibility change, not retraining or model admission. Existing model compatibility checks remain in force. Physiology extraction and five-trial calibration labels still reject transition-containing windows. Three fresh valid current-condition predictions, model/quality gates, actual L1 exposure, minimum dwell, mount integrity, Guard and media-safe execution are retained. Behavioral fallback retains its existing explicitly non-physiological path; this update does not add a new exposure requirement to that path.

Stop the existing session and restart the Python service before starting a fresh session to use the revision. A previously running bridge does not reload changed Python files. Do not relabel historical data or pool control revisions without retaining provenance. No service was forcibly restarted during this work.

## Decision timing investigation

Unity `FinishPersonalTrial` completes the record and emits `personal_decision_due` at 4.5 seconds. `AdaptiveRuntime.Send` yields for HTTP; the same engine tick proceeds to the next stimulus. A nonzero-latency reply therefore cannot execute before that stimulus starts. `ApplyPersonalDecision` checks a later trial boundary. Synthetic engine validation explicitly installs the reply after the request boundary and verifies no mid-trial execution, followed by execution at the next boundary.

We did not move requests into the 4.0-4.5 second interval: the existing completed trial/physiology contract uses the entire 4.5 seconds and extraction waits another 0.75 seconds for acquisition. Merely sending earlier would mean using an unfinished trial or changing the evidence contract. Waiting for HTTP would stretch the fixed cycle. Accordingly, this update preserves the four-second response/visibility and 4.5-second onset cycle, with no blocking wait, early speculative request or mid-trial switch.

This is a software timing finding, not a measurement of Quest/network latency. A different earlier-decision protocol requires separate design and validation.

## Capacity under the retained execution contract

There are 19 request boundaries (5 through 95). The first eligible behavioral request is 25: the initial two unscored trials invalidate the window at 20. There are 15 potentially behavior-eligible request boundaries (25 through 95). Actual changes are not guaranteed.

For a request at 25, the earliest asynchronous boundary execution is after trial 26. New-condition trials start at 27:

- **Behavioral fallback:** trial 30 has only four new-condition trials; the five-new-trial dwell gate permits the next request no earlier than 35. Successive executable requests are separated by at least ten trials.
- **Physiology:** trials 27-28 are physiological transitions. The first three clean overlapping five-trial windows end at 33, 34 and 35. At the immediate trial-35 request, the third window is still inside the 0.75-second acquisition wait. The next opportunity is 40. Successive executable requests are separated by at least fifteen trials.
- **Terminal boundary:** a request at 95 is still audited, but its response cannot execute at the end of 96: the engine enters the questionnaire before applying pending actions. It cannot be counted as a delivered switch.

Thus the timing upper bounds are **7 executed level changes in fallback** and **5 with physiology**, per 96-trial phase, under timely replies and otherwise favorable gates. Starting at L0, these correspond to at most four and three executed ON actions respectively. These are level changes, not counts of individual movie clips. Additional delays, media, invalid data, behavior/model disagreement or Guard refusal reduce delivered changes, possibly to zero.

## Reproducible synthetic verification

`AgentScripts/BehaviorWindowValidation.cs` exports the actual Unity stimulus sequence (seed 20261102). `Tools/AdaptiveTraining/validate_behavior_switches.py` uses binary optimization over correctness and omissions, with actual Match/Non-match positions and the unmodified 70/40 and omission thresholds, to find achievable schedules. It searches schedules from the timing upper bound downward and rejects infeasible behavior. It then evaluates every boundary using the production `PersonalSession.decide`; physiology uses real window eligibility/acquisition gates but explicitly mocked sensor features and model outputs, with sufficient synthetic exposure. No existing prediction history is rewritten.

Achieved request/execution counts:

| Mode | Requests leading to switches | Actual execution after trial |
|---|---|---|
| Behavioral fallback | 25, 35, 45, 55, 70, 80, 90 | 26, 36, 46, 56, 71, 81, 91 |
| Physiology (synthetic admitted-model fixture) | 25, 40, 55, 70, 85 | 26, 41, 56, 71, 86 |

Unity replays these witnesses on isolated `TaskEngine` objects in Play Mode, verifying nineteen requests, all delivered changes, no mid-trial execution, unchanged onset spacing, four-second visibility, active-media deferral and the non-executable final request. These are constructive software capacity witnesses, not expected human switching rates, genuine admitted participant models, evidence of causal source attribution or proof of Guard benefit. Media/Guard are favorable in the capacity fixtures; a separate check verifies media deferral.

Evidence is in `Evidence/BehaviorWindowRevision/`: `unity-sequence.json`, `traces.json`, `switch-results.json`, `python-regression.txt`, `unity-validation.txt`, and `unity-console-status.txt`.

Python: 166 tests passed. Unity: 432 synthetic checks passed; the final PASS log was read back, console errors were zero and compilation failure was false. Play Mode was stopped and the previously enabled auto-tick setting was retained. Production C# runtime was not changed; the validation script was compiled in memory. Quest Pro, actual network/acquisition timing, complete live rehearsal and participant effects remain unverified. No APK, deployment restart or remote push.

Restore point: local branch `codex/pre-behavior-transition-20261105`, commit `79e10d58882f01500b344551874ce8d5b3c52664`. Unrelated untracked recordings were left intact. Pipeline reference pages used: `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/runtime.md` from the installed package.

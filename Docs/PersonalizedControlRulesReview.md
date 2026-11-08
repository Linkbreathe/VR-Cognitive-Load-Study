# Personalized control policy v3

Date: 2026-11-03. Policy `personalized-rf-pilot-v3-control70-40`. Application remains dev12/code 24 with `warm-study-v1`; configuration/HTTP contract remains dev11 and protocol remains `personalized-rf-v2`. This is a Python control update, not a new APK or calibration-label scheme.

## Approved behavior

The user revised the thresholds, then clarified **more than five total omissions within the latest twenty trials**, replacing the proposed consecutive-omission rule.

| Evidence | Stable | Struggling | Otherwise |
|---|---|---|---|
| Latest 20 behavioral trials | Balanced accuracy >= 0.70 AND omissions < 4 | Balanced accuracy <= 0.40 OR omissions > 5 | No adjustment supported |
| Mean eligible recent model score | <= 0.45 | >= 0.65 | No adjustment supported |

Balanced accuracy remains the mean of Match recall and Non-match recall; omissions count as incorrect. Zero to three omissions can satisfy Stable. Four/five omissions alone do not support adjustment, but BA <= 0.40 still supports Struggling. Six or more omissions support Struggling regardless of adjacency. There is no consecutive-omission trigger.

The control RT ceiling is removed. Correct-response median RT remains descriptive and is JSON null when there are no correct responses. An all-wrong/all-omitted window can therefore support difficulty instead of failing the former `no_correct_rt` gate. BA and decision flags use native JSON-compatible values.

## Retained behavior and evidence boundaries

Requests still occur every five completed trials. As of 2026-11-05, `behavior20-stride5-include-transition-v1` includes transition trials in the latest twenty-trial behavior window. Scored/interruption/stage and answer-class coverage gates remain; invalid trials are not filtered out and stitched together. Physiological and calibration-label windows still exclude transitions. At least three valid recent current-condition predictions, minimum dwell, fresh evidence and ten seconds of actual L1 exposure remain required for model-based control. [Revision, timing and capacity tests](BehaviorWindowRevisionReview.md).

With an admitted model, L0 -> L1 requires both behavior and model to support Stable; L1 -> L0 requires both to support Struggling. Disagreement, buffer values or insufficient evidence give HOLD. Unity executes at safe trial boundaries after active media ends. After two failed model attempts, enabled behavioral fallback uses the same revised behavior rules and stays explicitly marked as fallback. Mount invalidation and disabled fallback remain effective. Random retains its seeded schedule.

Five-trial labels are unchanged: correctness-dominant per-trial scores, a small correct-RT adjustment, Stable >= 0.8, Struggling <= 0.4 and an unlabeled buffer. Removing RT from online decisions does not remove it from these labels. Forest settings, signal-quality and held-out-block/permutation admission gates, task timing, feedback, media schedules and questionnaires are unchanged. Default physiological/device/permutation qualification remains disabled. These thresholds are user-approved engineering choices, not empirically validated cognitive thresholds.

## Versioning and operation

Code defaults and both `personalized-policy.json` and `p007-personalized-replay-policy.json` now use the v3 policy. Fields `stable_omissions_below=4` and `struggling_omissions_above=5` replace the previous omission rules; `stable_max_rt` is removed. L0/L1 model thresholds are both 0.45/0.65. Replay mappings, units declaration and non-admitting provenance remain intact.

The full policy is frozen into Python session records and model artifacts; each decision also records `policyRevision` and its behavioral state. Old policy revisions and old-policy model artifacts are rejected. Restart the updated Python service and begin a fresh Unity session/calibration. Custom policy files must be updated explicitly. Do not change rules within an active session.

No Unity runtime source, scene, application version or APK changed. The existing dev12 APK remains compatible. Existing deployment ZIPs retain their original Python policy; deploy the updated checkout's Python source and matching policy rather than assuming an older ZIP contains this change. No remote push or headset installation was performed.

## Verification

- **111 Python tests passed in 26.239 s**, including 17 new rule regressions and a real localhost HTTP/cache/audit roundtrip. Coverage includes 70/40 and 0.45/0.65 boundaries, three/four/five/six omissions, scattered versus consecutive omissions, last-twenty scope, no-correct-RT difficulty, fallback, retained gates and old-policy/model rejection.
- **73 Play Mode checks passed across nine synthetic Python decisions**, deserialized with the production Unity `AdaptiveDecision` type and applied to isolated `TaskEngine` instances. Only fixture identity fields were rebound. Checks cover unchanged wire contract, media-safe deferral, ON/OFF/HOLD execution and audit events. The live scene controller's decision state was not replaced. These are not sensor-driven or participant trials.
- The validation helper compiled without diagnostics and its PASS log was read back. Current Unity console ground truth showed zero errors and two warnings; historical retained counts and unrelated Unity AI `NoSubscription` messages remain. Play Mode was stopped; already-enabled auto-tick was retained.
- Initial checks exposed NumPy boolean serialization in new audit fields, fixed by making summary BA a native float. The first Unity fixture wrapper did not populate from an ephemeral assembly; the helper was corrected to deserialize production decision types directly. Both were resolved before final verification.

Evidence: `Evidence/PersonalizedControlRules/python-tests.txt`, `unity-decision-fixtures.json`, `unity-boundary-validation.txt`, `unity-console.json` and `final-verification.json`. Helpers: `Tools/AdaptiveTraining/test_personalized_control.py`, `AgentScripts/validate_personalized_control.py`, `AgentScripts/PersonalizedControlValidation.cs`.

Restore: `Evidence/PersonalizedControlRules/pre-control-v3-source.zip`, with pre-change worktree status and protected-file hashes alongside it. Restore individual files only after checking for subsequent unrelated edits.

Pending: empirical threshold qualification, real signal/device quality, personal model admission and full Quest Pro participant sessions. See [acceptance](AcceptanceRecord.md), [operation](ExperimentGuide.md) and [project log](ProjectLog.md).

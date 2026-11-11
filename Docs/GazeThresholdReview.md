# Gaze review thresholds

Date: 2026-11-10. Revision: `gaze-review-coverage60-task40-v1`. Stage: 2/3 A/B evidence review.

The researcher requested inclusive valid-eye coverage >= 60% and task-view time >= 40% of valid-eye time. These replace the former 70% / 60% thresholds for ON authorization and playback-coincident model-only OFF deferral. The Evidence Guard defaults and example configuration use the same new values. Explicit custom GuardConfig values remain supported and are logged.

Coverage is valid-eye time divided by the eligible interval duration. Task share is task-view time divided by valid-eye time, not the whole interval. Missing tracking still does not pass. The pre-response decoded-playback overlap floor remains one second. Behavioral/model thresholds, timing, support-probe rules and task geometry are unchanged. A still records review without applying it; B applies review. Desktop fixed gaze remains explicitly simulated at 75% coverage / 70% task share.

New `personal_protocol.json` files and comparison review records include `gazeReviewRevision`. Review records retain actual numeric thresholds, and the Guard configuration audit records its actual defaults or overrides. Historical recordings are not rewritten. Console decision-rule text and current operating/review documents now show 60% / 40%.

## Software validation

- Project Python environment: 57 tests passed across `test_compact_comparison`, `test_probe_study`, `test_evidence_guard`, `test_attribution` and `test_short_calibration`.
- Boundary cases: ON is allowed at exactly 0.60 / 0.40 when other evidence passes; coverage 0.599, task share 0.399 or unavailable tracking holds ON. For eligible model-only OFF deferral, exactly 0.60 / 0.40 holds playback; either ratio just below its threshold allows OFF.
- Evidence Guard package: 14 tests passed. Existing A-observe/B-enforce, missing tracking, playback and support-probe regressions passed.
- Console decision-display contracts passed with `node Tools/AdaptiveTraining/test_decision_clarity.cjs`.
- The launcher environment imports the editable Evidence Guard source and reports defaults 0.60 / 0.40 without a PYTHONPATH override.
- An initial attempt with the global Anaconda interpreter encountered incompatible NumPy optional binaries and a missing websockets dependency. Validation was rerun successfully using the project's `.venv`; no global dependencies were changed.
- Repository-wide whitespace checking reports pre-existing trailing spaces in the modified TMP fallback font asset; that asset was not changed by this work.

## Operation and pending checks

End the current run, restart the Python Evidence Console/controller service, refresh the Console, then create a fresh session. Do not resume an old session across the policy change. No running service was restarted by this work.

This is a software-verified policy change. The revised thresholds have not been physically verified on Quest Pro or validated with participant data; no new device or scientific acceptance is claimed. No Unity scene, compiled Player or participant recording was changed.

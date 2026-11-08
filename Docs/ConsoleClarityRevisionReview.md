# Decision clarity and operator wording revision

2026-11-08 · Stage 2/3 · display `console-decision-clarity-v2` · backend v1.21 unchanged

## Changes

The compact decision card now separates base-proposal RF/BA/count from current-condition review RF/BA/eligible-count. Explicit null review scores remain unavailable; they are never replaced by base scores. The original recorded reason leads the explanation, with a note when the branch did not evaluate eyes. Criteria start with the recorded outcome and clearly labeled context reconstructed from saved fields; original checks remain grouped and unchanged. The interface explains that base triggers are OR conditions and table rows are not independent authorization.

Desktop fixed-gaze sessions display a compact `Real gaze not collected in Desktop mode` notice and configured simulated assumptions. They no longer draw a 100% invalid measurement chart, spatial map or measured-gaze timeline. Seeded simulations are similarly identified without inventing coordinates. Tracked-eye rendering retains the existing measured coordinates, bins and timing. Gaze-period labels show trial range, eligible duration, whether it was used by the review and the local heat scale. Follow-up does not change the original headline scores.

HOLD is expressed as keep video ON/OFF, using the matched command receipt when available. Command acknowledgment and decoded playback are explicitly distinguished. The timeline retains recorded playback overlaps.

The primary guide now has Practice, Calibration and Adaptive Comparison, with Desktop / Quest Pro reference controls and session-specific environment locking. Historical continuous-stage instructions are collapsed and labeled legacy. The current A/B questionnaire reference uses the existing seven-point protocol and only exposes configured session form links; questionnaire rules and remote forms were not changed. Source selection uses the current Evidence VR > Signal Sources menu name.

The default calibration option is `Train Personal RF · forest / arithmetic`; the other control calibration is labeled a separate legacy protocol. Saved-model A/B and the separate single-period vendor workload capability are explained. A selected model is not labeled loaded, and a configured Quest Pro is not labeled measured eye data. Fresh `TRACKED EYES` status can now make the display readiness row ready without altering preparation gates.

Thresholds distinguish calibration recommendation, draft and applied values. `Copy recommendation to draft` changes only the editable draft; a running session still requires the existing explicit Apply action. Trigger counts are labeled calibration windows. The assignment button now says `Create session assignment`, preserving its original action and Unity start sequence.

## Validation and boundaries

- JavaScript syntax checks passed for shared evidence, monitor, setup and report scripts.
- `node Tools/AdaptiveTraining/test_decision_clarity.cjs` passed: distinct windows, null/zero handling, simulated source identification, receipt wording and immutable input records.
- Browser validation on the production archived B16 decision showed base RF 0.604 / BA 25% / five trials and separate review BA 16.7% / 4 of 5 eligible trials / unavailable applicable RF. The original outcome remains HOLD. Desktop has no heatmap or red invalid chart. Recorded criteria and raw source details remain accessible.
- The production guide exposes three primary stages and Desktop reference controls; the calibration menu and historical configured-eye labels show the revised wording.
- An isolated read-only fixture under `Evidence/ConsoleClarity-20261108` confirmed that copying recommendations leaves applied 0.55/0.60 values intact and labels the draft unapplied. Its synthetic tracked-eye status produced a ready display row. The fixture rejects all POST requests; no participant session or threshold was changed.
- The isolated coordinate fixture exercised both gaze periods, retained the 4 s follow-up map and rendered two columns at 1100 px without overflow or browser errors. This was a synthetic software fixture, not device evidence.
- A 390 px browser viewport showed no document horizontal overflow on setup or the expanded report criteria. Layout retains the existing stacked narrow-screen behavior.
- SHA-256 comparisons confirmed no changes to `probe_control.py`, `layer_ab.py`, `comparison_report.py`, `decision_gaze.py` or `test_session_store.py`. No Unity code, controller threshold defaults, timing, gaze gates, acquisition processing or historical recording was changed in this revision.
- Static assets were served by the existing production process. No service restart, Unity run or worn-device test was performed. New session metadata uses the updated display revision through the existing metadata mechanism. Physical Quest Pro acceptance remains pending.

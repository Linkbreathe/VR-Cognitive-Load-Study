# Five-second personal workload windows

**Training-policy correction, 2026-11-10:** the screened-training clauses below are historical. `workload-training-no-artifact-screen-v1` retains all five epochs without artifact rejection and versions the new selection contract separately. Five-second timing and model extraction/admission rules remain. [Current training policy](UnscreenedTrainingReview.md).

2026-11-10, Stage 2/3. The researcher requested implementation of the discussed five-second workload calibration and scoring scheme. Model revision: `rest-math-iaf-rf-v3-5s`; selection policy: `rolling5-stride1-disjoint-fold60-v3`; Console backend: `evidence-console-v1.24-workload-five-second`; display: `console-workload-five-second-v1`.

## Implemented contract

- Each condition remains 180 seconds. The arithmetic task still has eighteen ten-second questions; EEG window duration does not change question timing, responses or feedback.
- Candidate EEG windows last five seconds and advance by one second, producing up to 176 candidates per condition. After each window ends, the scanner still allows one second for sample arrival, plus polling delay.
- Each window is filtered independently with the existing fifth-order, forward/backward 2–30 Hz filter. The existing raw-integrity, flatline and amplitude checks apply to one-second epochs across all eight channels. At least three of the five epochs must pass. This 60% clean-data requirement is a provisional engineering setting, compared with the previous 5/10 requirement.
- Selected training windows do not overlap or cross a 60-second fold boundary. Earliest usable selection yields at most twelve per fold and thirty-six per condition. Failed candidates can shift subsequent selection; selected starts are at least five seconds apart.
- Each condition needs at least twelve accepted windows. This preserves at least sixty seconds of original-time coverage and provides at least thirty-six accepted one-second epochs. The report separately records actual accepted epoch counts in `cleanSeconds`. Window count is not an independent-sample count.
- IAF estimation, eight per-epoch power features, stage labels, 100 trees, depth four, balanced class weights and seeded fitting are retained. Validation holds out matching 60-second portions of both conditions. IAF is fitted only from each fold's training data. Both classes must be present in each training/test fold; mean held-out AUC must reach 0.65. No new weakest-fold or permutation criterion is introduced.
- Final fitting uses all selected, screened epochs. Held-out threshold recommendations are recomputed from five-second windows on the deployment score scale.
- Engineering A/B scoring and its pre-Unity live preview use the latest five seconds, retaining their existing artifact-screening-off policy. The single-period observational route retains screened scoring. This update does not resolve the existing difference between screened training and unscreened A/B deployment.
- Candidate step is one second; the comparison controller's existing check cadence remains every two trials. The existing prediction-trial selection, behavioral windows, gaze checks, attribution intervals and support-probe rules remain unchanged. Shortening the input window does not make decisions occur twice as often or establish shorter measured end-to-end latency.

## Provenance and migration

Protocol metadata records the workload window contract. Candidate logs and model reports contain window length, candidate step, minimum clean epochs, minimum accepted windows and revision. Predictions record the actual workload start/end; A/B decisions record the five-second model revision and duration. Live previews return actual start/end and duration. New registry manifests bind the contract, and loading checks it against the report.

Historical `rest-math-iaf-rf-v2` models, including the P001 forced candidate, remain on disk with their original records. They are excluded from the new compatible-model list and explicitly refused if requested by ID. Do not relabel, overwrite or migrate their thresholds. The separate historical `rest-math-120s-v1` anchor keeps its original ten-second observation semantics.

Finish any current run before restarting the Python service. Refresh the Console and run a fresh forest/arithmetic calibration. Select the newly saved five-second model and review its newly generated thresholds before creating a new comparison assignment. Existing allocations and an already-running Python process do not acquire the new contract automatically. No participant recording, existing model or active assignment is modified by this implementation.

## Validation and limits

Initial software validation passed nine workload/short-calibration tests, including a six-minute synthetic fit/save/reload; 36 windows per condition; 12 held-out windows per class per fold; preview/A/B score agreement; five-second score timestamps; three-clean-epoch acceptance versus two-clean-epoch rejection; five-second live-buffer readiness; shifted selection without overlap or fold crossing; minimum-window and temporal-coverage failures; and refusal of historical models without modifying them.

Full AdaptiveTraining regression: **241 tests passed** in 57.058 seconds using `Tools/AdaptiveTraining/.venv/Scripts/python.exe -m unittest discover -p test_*.py` from `Tools/AdaptiveTraining`. This includes model reuse, controller, gaze review, compact comparison, support probe, attribution, signal quality and Console regressions. Node syntax checks passed for `console/console.js` and `console/tests.js`; `test_decision_clarity.cjs` passed all display contracts. Final targeted validation after the backend-version and manifest-contract test changes is recorded in `Evidence/FiveSecondWorkload-20261110/validation.json`.

All fixtures use temporary engineering data, separate from participant records. This change has not been validated with fresh worn EEG, independent 2-back data, participant outcomes, physical Quest Pro operation or a comparison of window lengths. A five-second window is a requested engineering choice, not an established optimum. No Unity asset, task presentation or Player build change is needed for this server-side window implementation. Service restart and browser rendering with a fresh five-second model have not been performed.

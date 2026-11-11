# Lower video-opening balanced-accuracy threshold

Subsequent revision, 2026-11-10: ON remains 0.65, but the researcher subsequently lowered the 60-trial OFF BA cutoff to 0.50. The 0.60 OFF descriptions and v1.25 identifiers below document the earlier ON-only revision. Use the [current OFF threshold review](OffBAThresholdReview.md) for operation.

2026-11-10, Stage 2/3, behavior revision `scored5-on65-off60-v4`, review revision `current-condition-support-probe4-on65-v2`, backend `evidence-console-v1.25-on-ba65`, display `console-on-ba65-v1`.

## Requested change and scope

The researcher selected BA >= 0.65 instead of >= 0.80 to make video opening easier in the current 60-trial A/B comparison. The same cutoff applies to the shared behavioral ON proposal in both rounds and the current-state behavioral ON requirement in B's applied review (also recorded hypothetically in A). Normal ON and support-invalidated Probe ON share this cutoff.

The base controller still combines behavior and model proposals with OR. B still requires agreement from the applicable current-OFF behavior, current-state model score and gaze before authorizing ON. Lowering BA does not bypass missing classes, missing/old-state trials, the fewer-than-two-omissions requirement, EEG eligibility/session score cutoffs, eye coverage >= 60%, task share >= 40%, cooldown or late-opening checks.

OFF remains BA <= 0.60 or at least two omissions, or the eligible model OFF proposal, subject to the existing review. Model-only OFF deferral still requires BA >= 0.80 and its existing timing/gaze evidence. The descriptive stable-state criterion while video is already on remains 0.80. Four-trial support-probe evaluation is unchanged: two mistakes/omissions fail, technical problems are inconclusive, and the cooldown remains five/seven trials. The 144-trial path keeps its 0.80 ON threshold and ten-trial verification. Stimulus timing, model fitting, scoring, media and Unity execution are unchanged.

With five trials, attainable BA values depend on the match/non-match counts; the cutoff is not a continuous accuracy dial. For example, two correct matches and one correct non-match out of three give BA = (1 + 1/3)/2 = 0.667. This now supports ON when the other requirements pass. BA 0.625 remains below the opening cutoff. These examples demonstrate rule behavior, not measured increases in video exposure for participants.

## Records and operation

New decisions record the behavior revision and numeric `behaviorThresholds`; reviews and evidence-check rows record the applied ON value. `personal_protocol.json` records `comparisonBehaviorThresholds` and the new revisions. The Console setup text shows 65% for ON, 60% for OFF and 80% for OFF deferral. Historical evidence rows retain their recorded thresholds; existing data files are not rewritten. Replaying historical inputs with the new code evaluates this revised policy rather than reproducing the former one.

Start the updated Console service (or restart after ending an existing run), refresh the Console and create a fresh assignment. Confirm backend `evidence-console-v1.25-on-ba65`. No Unity recompile or Player build is required for this server-side rule change. The normal service was not listening during this work; no active run was interrupted.

## Validation

Full AdaptiveTraining regression passed **248 tests in 65.039 seconds** using the project Python environment. Node decision-display contracts and `console/tests.js` syntax checks passed. The new fixtures initially exposed two test-setup mistakes (a wrong evidence-config argument and an unintended legacy BA value); those fixtures were corrected before the passing full run. No production failure or record modification resulted.

New regression cases cover the 0.667 opening case, 0.625 rejection, two-omission rejection despite BA above 0.65, model disagreement, both rounds, Probe ON, numeric evidence rows, metadata and unchanged legacy 144-trial behavior. Existing checks retain the 0.80 OFF-deferral requirement and support-probe outcomes. Console tests verify new 65% records while preserving historical 80% records and the distinct OFF/deferral text.

Validation output and pre-edit recovery copies are in `Evidence/OnBAThreshold-20261110/`. Fixtures are isolated engineering/synthetic data. Worn EEG/Quest Pro operation, achieved video exposure, switching frequency and participant effects remain unverified.

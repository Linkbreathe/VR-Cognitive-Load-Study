# Fixed Exposure threshold and score chart

Date: 2026-11-12. Stage 2/3. Display revision: `console-fixed-threshold-score-chart-v1`.

## Purpose and behavior

The Fixed Exposure Session report now places **Suggested thresholds and score range** near the top. It distinguishes configured reference thresholds from exploratory suggestions and from fluctuating workload scores. The header explicitly labels configured thresholds. When the existing estimator returns no candidate pair, the report states its reason and the available Stable / Struggling anchor counts. A suggested pair appears only when supplied by the existing report; the chart does not estimate or apply thresholds.

The chart plots all finite, valid recorded Fixed Exposure scores on a 0–1 axis at their recorded window end times, in seconds since the first trial. Invalid scores have crosses below the axis and break the line; a gap greater than seven seconds also breaks the line. Large colored markers identify the estimator's eligible five-response behavior groups. Clicking or focusing a point shows its score and EEG time interval; eligible groups additionally show trial range, balanced accuracy, omissions and label. Scheduled OFF / ON background bands are explicitly distinguished from actual playback eligibility checks. The five-response behavior group and the EEG time window are not presented as identical intervals.

Dashed lines show the latest configured reference pair. Solid colored suggestion lines appear only for an available candidate pair. These lines are reference values, not a threshold-edit history. Summary cards show the min–max across all valid recorded scores and, separately, eligible score ranges and medians for each exposure. These are descriptive observed ranges, not confidence intervals. Detailed eligible and excluded windows remain expandable, including exact candidate values for manual entry. Narrow screens retain legible chart text through horizontal scrolling.

The estimator revision remains `fixed-exposure-behavior-thresholds-v1`: completed uninterrupted stage, valid data in both exposures, at least two Stable and two Struggling groups, and median OFF minus median ON at least 0.05. This is exploratory within-session evidence with at most five groups, not model admission or proof of video effects. Fixed Exposure follows its schedule independently of these configured reference thresholds. Task timing, controller behavior and source records are unchanged. Future session metadata records the updated Console display revision through the existing mechanism; historical metadata is not rewritten when viewing this report.

## Read-only verification against the requested session

Session `20261112T091929_65c4a11025f7` is labeled Unity Editor test with live EEG by its report. It contains 30 completed trials and 23 valid recorded scores, with observed range 0.318–0.394 (display rounded to three decimals). Two eligible OFF windows span 0.341–0.381, median 0.361; two eligible ON windows span 0.360–0.389, median 0.375. There are two Stable and zero Struggling anchors; the final candidate window is excluded for an invalid or missing score. No suggested pair exists. The configured pair is 0.48 / 0.59. These observations are not participant-benefit or hardware-acceptance evidence.

## Validation and remaining checks

- JavaScript syntax check passed for `comparison.js`.
- The existing five `test_three_stage_comparison.FixedThresholdTests` passed using `Tools/AdaptiveTraining/.venv/Scripts/python.exe`. The initial system Anaconda invocation emitted NumPy binary compatibility errors; validation uses the project environment.
- The live localhost report rendered the values above. Clicking the Stable group for trials 8–12 showed score 0.341, BA 0.833, zero omissions and its 49.0–54.0 s EEG interval. Browser warning/error log was empty.
- Desktop chart visually inspected. At a 390 px viewport, summary cards stack, the page has no horizontal overflow and the 760 px chart scrolls within its container. Temporary viewport override restored.
- Screenshot: [verified report](../Evidence/ThresholdScoreChart-20261112/report.jpg).
- The live `/console/health` endpoint reports `evidence-console-v1.31-three-stages30` on port 8765. This supersedes the historical v1.30 deployment observation for the current process; no service restart was performed in this work.

Candidate-present and no-data chart variants are implemented but were not visually exercised in this session. Threshold utility, worn Quest Pro operation and participant acceptance remain unverified. No Unity compilation or physical checks were needed or claimed for this report-only change.

# Continuous 60-trial comparison

Current gaze thresholds were revised on 2026-11-10 to >= 60% valid eye coverage and >= 40% task share of valid eye time; see [Gaze threshold review](GazeThresholdReview.md). Earlier validation evidence below predates this threshold revision.

Current review, 2026-11-08: `current-condition-no-opening-test-v1`. The six-trial opening test, failure lock and trial-52 cap have been removed; the five-trial/two-trial controller, ON/OFF evidence checks and continuous playback below are retained. OFF notes are historical context, not a verified-reopening requirement. Review ordinary ON proposals through trial 58. See [complete current rule table and validation](CurrentConditionReview.md). Earlier verification history below is superseded for current backend sessions only; archived records and 144-trial rules are retained.

2026-11-07. Control revision `behavior5-continuous-stride2-v3`, review revision `pre-response-continuous-gaze-v2`, presentation/media revision `continuous-clips-resume-v1`, Console `evidence-console-v1.17-continuous-comparison60`.

The current PC media content is `layer-ab-144-dialogue-v2-no-heidi`. Three source slots were replaced and both streams re-encoded to remove Heidi; durations and transport behavior are unchanged. See [media exclusion and verification](MediaExclusionReview.md).

## Authorized behavior

New Console A/B assignments contain 60 stimuli per period, including two unscored warmups. Stimulus and response timing remain 4.5 and 4 seconds. Requests occur at response close on even counts 2 through 58 (29 scheduled checks). The first complete five-scored-trial behavior window occurs at count 8: 26 potential complete windows. Class coverage, acquisition, clock, condition and remaining-verification gates can reduce actionable opportunities. No post-session action is requested at count 60. The saved RF and ten-second workload feature extraction remain unchanged. The original fixed-cutoff rule was superseded on 2026-11-07: researchers can apply new thresholds during either round, with actual values retained per decision; see [live threshold edits](LiveThresholdsReview.md). Existing saved allocations retain their own policy and trial budget.

| Evidence | Rule for the latest five eligible responses |
| --- | --- |
| Behavior supports ON | BA >= 0.80, fewer than two omissions, both match and non-match classes present |
| Behavior supports OFF | BA <= 0.60, or at least two omissions |
| BA unavailable | Missing either class; report class counts and do not invent BA. Two omissions can still independently propose OFF. |
| B authorizes ON | Current-condition behavior supports ON, applicable pure-condition model <= ON cutoff, valid eye coverage >= 0.60, task share >= 0.40 |
| B defers a model-only OFF | Current-condition BA >= 0.80, fewer than two omissions, existing score-rise/onset/time alignment checks, at least one second of eligible pre-response playback overlap, valid eye coverage >= 0.60, task share >= 0.40 |

BA averages match and non-match accuracy; it is not overall accuracy. Small windows have discrete, class-count-dependent jumps. Thresholds are engineering pilot settings, not validated physiological boundaries. A retains the shared behavioral-OR-model proposal; B enforces review. Missing eye evidence cannot justify gaze-supported OFF deferral. A behaviorally justified reduction does not require eye tracking.

(Historical: superseded first by the six-trial test and then by removal of opening tests; see [CurrentConditionReview](CurrentConditionReview.md).) Post-ON verification remains a separate ten-new-trial assessment with at least fifteen seconds of decoded playback, BA >= 0.80 and fewer than three omissions. For this revision the model must remain below the OFF cutoff, rather than return below the ON cutoff. Evidence reports the ten verification trial IDs separately from the fast five-trial window. Existing phase-end lock after a valid failed probe is retained; this change does not implement a new recovery/retry policy. Inconclusive verification retains the existing return-to-OFF/retry behavior. Late upgrades without room for verification are deferred.

## Continuous clip playback

Both arms use the same media mechanism. A clip boundary is not a controller OFF or ON. While level 1 is active, source clips continue in a fixed sequence across clip and statistical block boundaries. HOLD acknowledges the decision while video/audio keep playing; it does not wait for the media to end. OFF takes effect at the next trial boundary, hides the screen, pauses audiovisual playback and retains the cursor. A subsequent ON resumes that position. Pause, session completion, abort and playback failure stop visible/audible exposure. A playback failure pauses the task instead of silently substituting a frozen picture or a missing video.

The initial local media bank joins thirty existing verified 20-second source clips into two 300-second streams (fifteen clips each). A single decoder runs across encoded cut points, avoiding per-clip preparation gaps. Source identities and hashes remain in `continuous-comparison-media.json`. Config seed parity assigns the two streams to A/B in opposite order. This is deterministic content allocation, not evidence that the two streams are perceptually equivalent. Existing source rights records and pending volume review still apply.

`continuous_media_assignment` records playlist ID/hash. `adaptive_media_segment_changed` records source clip, cursor, frame and exposure without creating a new model-review playback onset. Actual first-decoded-frame starts, OFF/end/interruption events and resumed cursors remain separate. `session.json` identifies the presentation revision; the full media manifest is copied into the session. The comparison report exports clip boundaries separately from exposure intervals. Do not treat a change at a content cut as proof of controller-induced distraction.

## Installation and operation

Generate the bank with the project Python environment:

```text
Tools/AdaptiveTraining/.venv/Scripts/python.exe Tools/AdaptiveTraining/prepare_continuous_media.py --source <existing-media-root> --output Builds/ContinuousComparison60 --ffmpeg <ffmpeg-path> --deploy <application-persistent-data-root>
```

The source root contains `layer-ab-144-media.json` and `AdaptiveMedia`. The output contains two MP4 streams and `continuous-comparison-media.json`. Install the JSON at the application's persistent data root and the MP4s under its `AdaptiveMedia` folder, alongside the original bank. The current PC installation has these files. A Quest deployment needs the updated player and these additional files. No new headset build was produced by this change.

Create a fresh Adaptive Comparison assignment in Web Console, select the saved model and cutoffs, then start from Unity. Existing recorded configurations are not rewritten. Create a new session after restarting the backend; its review revision is recorded separately from the unchanged timing configuration. Practice and calibration retain their previous media behavior. Model threshold edits apply to subsequent new decisions; behavioral thresholds are versioned policy constants.

## Verification and limitations

- Python regression: 55 targeted tests passed, covering existing probe/model/Console behavior and the new BA/class-count/omission/gaze boundaries, version validation and separate ten-trial verification. The saved-model replay uses the real forest on archived eye-open EEG plus synthetic responses/gaze/execution.
- Unity Editor timing: both conditions completed 60 simulated trials with 29 checks, 26 full-window positions and ON/HOLD/OFF receipts. HOLD was acknowledged even with the active-media gate closed; OFF did not wait for the clip end. [Timing evidence](../Evidence/ContinuousComparison60-20261107/unity-timing.json).
- Unity Editor playback: deferred OFF at approximately source second 18; crossed the 20-second clip boundary and continued through second 27 with no hidden-video or missing-audio state in the sampled observations. OFF hid/muted, the cursor stayed fixed while off, ON resumed, and session end stopped exposure. This verifies software player state, not measured photons or acoustic continuity. [Playback evidence](../Evidence/ContinuousComparison60-20261107/playback/verification.json).
- Replay: 120 trial records, 58 scheduled decisions; A delivered five ON/four OFF and B two ON/two OFF. B blocked fifteen proposals, mostly ON and late verification-ineligible openings. These are not fifteen independent successful interventions and do not establish B superiority. [Replay report](../Evidence/ContinuousComparison60-20261107/replay/report/index.html).

Worn Quest Pro eye tracking, current Mindtooth units and time alignment, perceptual continuity, loudness, real participant performance and the scientific validity of these pilot thresholds remain pending. The CSV used in replay does not declare units; the model's uV/scale-1 assumption remains explicit. Prior source backup: `Evidence/ContinuousComparison60-20261107/source-before.zip`.

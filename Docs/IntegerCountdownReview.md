# Integer response countdown

2026-11-10, Stage 1/2/3 shared presentation, revision `response-countdown-v3-integer-seconds`.

The researcher requested whole numbers instead of decimal response times. The shared response countdown now displays `4 -> 3 -> 2 -> 1`, then `0` at the existing deadline. It uses the ceiling of nonnegative remaining seconds: 3.999 displays 4, 3 displays 3, and 0.01 displays 1. This avoids displaying zero before responses close. The response window remains four seconds and the onset interval remains 4.5 seconds. The normal half-second gap, stimulus visibility, early responses, feedback rules, media and scoring are unchanged. Calibration/rest minute-and-second clocks are unchanged.

`SpatialTaskView.CountdownRevision` is recorded automatically in new `session.json` files as `countdownDisplayVersion`. Runtime initialization and legacy-upgrade event messages now identify that constant rather than claiming centisecond formatting. Historical recordings retain their original display revision. Existing rolling-validation HUD expectations were updated to the integer format; historical dev5 protocol fixtures were not rerun as current-protocol acceptance.

Verification: Unity compilation completed; compiled formatter outputs and unchanged timing defaults were read back to `Evidence/IntegerCountdown-20261110/formatting.json`. A Play Mode check assigned the compiled output to the existing TMP timer, verified text `4` without overflow, logged the revision and restored the original text; `hud-preview.json` records the result. This was a UI preview, not a participant trial. Pre-change recovery copies are under the evidence folder. No physical timing or headset readability acceptance is claimed. A new APK has not been built, so installed Players require rebuilding to receive the change.

The work used the local uGUI skill and the installed Pipeline documentation `commands/scripts.md` and `commands/editor-lifecycle-and-observability.md`. Play Mode was stopped after the preview; pre-existing auto-tick remains enabled.

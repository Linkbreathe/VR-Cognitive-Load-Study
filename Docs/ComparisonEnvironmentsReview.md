# Comparison environments

2026-11-07, Stage 2/3 engineering revision `comparison-environments-v1`, Console `evidence-console-v1.18-comparison-environments`.

## Operator choice

Adaptive Comparison now has a separate **Comparison environment** selector. It is independent of the saved model / EEG-free rehearsal run option. The chosen environment is frozen at Start Session and applies to both A and B.

| Environment | Preparation | Decision eye input |
| --- | --- | --- |
| Desktop — no headset, fixed gaze 75% / 70% | Unity desktop view, C to center, A/B button checks, Enter to continue | Assumed valid-eye coverage 0.75 and task share 0.70 of valid eye time |
| Quest Pro — headset with real eye tracking | Tracked headset, user presence when exposed by the runtime, and tracked eyes | Measured eye intervals; no fixed-value fallback |

Quest Pro remains the default. For the requested Wei_004 workflow, choose **A/B with a saved model**, select the saved personal model, select the environment, and keep the same chosen ON/OFF thresholds for both periods. Desktop does not waive EEG reception, model loading, clock, media or preparation gates. The separate EEG-free rehearsal option remains explicitly labeled.

The fixed values are inputs, not new thresholds. Existing eye thresholds remain 0.70 valid coverage and 0.60 task share. For ten eligible seconds, the assumed valid time is 7.5 seconds and assumed task time is 5.25 seconds. No movie-directed gaze or eye position is invented. Empty intervals remain unavailable, and model-only OFF review still requires actual decoded-video overlap before responses, the existing minimum duration, score-rise timing and behavioral conditions.

## Runtime and evidence

The desktop view disables the camera's tracked pose driver, stops active XR displays for the duration of the desktop view, and uses a fixed monocular seated camera. Returning to VR restores the previous camera/driver/display settings. Automatic headset-absence mount checks do not apply to Desktop; explicit electrode/mount interruption reporting remains available.

Python substitutes only the requested decision aggregates. It does not inject fabricated raw gaze observations, gaze positions or tracked-eye event relations. Every base decision and patent review records `gazeInput`, including branches that do not evaluate eyes. `simulated_fixed_gaze` is explicitly distinguished from `quest_pro_tracked_eyes`. Configuration, Unity session metadata, protocol metadata, decision graph and comparison exports retain the source. Desktop raw-gaze timelines remain empty/missing rather than implying measured tracking. The existing A observe / B enforce distinction, real physiological scoring and behavioral rules are unchanged.

Fixed input is restricted to operator-managed engineering comparisons. Participant configurations and unrelated stages cannot opt into it. Server assignment checks reject mid-session changes. Existing configurations without the new field select real tracking.

## Validation and limits

- Fifty-one targeted Python tests passed. Checks cover aggregate denominators, empty intervals, desktop ON, RF disagreement, behavior OFF, playback-overlap requirements, VR missing/low tracking, configuration restrictions, assignment immutability and archived provenance. Existing probe, compact policy, model reuse and Console checks are included.
- Unity 6000.3.27f2 Editor compiled and ran an isolated `ENVIRONMENT_CHECK` rehearsal against a separate service/registry. Desktop preparation was ready with `HeadsetTracked=false`; C/A/B checks and Enter advancement passed. Camera mode was monocular; returning to VR restored the pose driver and stereo mask. Four VR gate combinations were checked in software.
- This runtime check used the explicit EEG-free rehearsal option. It does not establish live Mindtooth inference or worn Quest Pro performance. Saved-model acquisition behavior is unchanged and remains subject to its existing checks.
- Worn Quest Pro tracking, physical presentation/audio, a deployed Player and a full live EEG A/B run remain pending. Simulated-gaze outcomes cannot establish gaze accuracy or participant benefit.

Restore point: [source-before.zip](../Evidence/ComparisonEnvironments-20261107/source-before.zip). Validation: [Python test output](../Evidence/ComparisonEnvironments-20261107/python-tests.txt), [Unity preparation](../Evidence/ComparisonEnvironments-20261107/unity-preparation.json), [keyboard checks](../Evidence/ComparisonEnvironments-20261107/unity-keyboard.json), [validation script](../AgentScripts/ComparisonEnvironmentValidation.cs).

The implementation used the project `unity-pipeline` and uGUI skills. Local Pipeline references read: `Documentation~/commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/runtime.md`, and `commands/capture.md`.

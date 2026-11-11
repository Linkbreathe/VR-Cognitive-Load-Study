# Console engineering gaze debug inheritance

2026-11-10, Stage 2/3, revision `engineering-gaze-claim-v1`.

## Cause and repair

The Editor menu persisted `engineeringGazeHighlight` and `engineeringGazeRay` in the local adaptive configuration. Operator-managed sessions subsequently replaced that configuration with the Console assignment, whose template disabled both flags. Recent Ptest and P001 records showed requested/effective false even while genuine tracked-eye samples were available.

`TestSessionClient` now reads the local preferences when claiming an assignment. Only an enabled local engineering configuration can request either flag; absent, disabled or participant configurations request both off. The claim sends two booleans and a versioned preference object. The service applies them only on the first claim, before returning the authoritative assignment and before Unity freezes or records the session. This preserves strict configuration equality in telemetry and core archives. No unrelated local task settings override the assignment.

Repeated claims from the same client return the original frozen values, even if local preferences change. Other clients cannot take the assignment. Invalid preference types/revisions fail before changing the assignment. Old clients without the preference object retain the existing default-off workflow. Participant/disabled assignment configurations cannot enable either overlay; existing runtime participant lockout remains unchanged, including participant practice and live ray enable attempts.

The service records requested/effective values and the inheritance revision in `test-session.json`. Unity retains its existing session metadata flags and visual revisions and adds a `gaze_debug_inheritance` event. The renderer styles, real-eye requirement, visibility/phase gates, stimulus timing, scoring, gaze ROI and adaptation are unchanged.

## Verification

- Unity 6000.3.27f2 compilation completed with no compiler errors.
- 24 Python unittest cases passed across `test_gaze_debug_inheritance` and the existing `test_model_reuse` suite. The new checks cover all four flag combinations, persisted configuration, accepted telemetry, completed hash-verified core archives, immutable retry behavior, participant/disabled denial, malformed preferences, legacy clients and wrong clients. Unrelated configuration fields remain identical.
- 97 compiled Unity checks passed for the local permission matrix, revision and input immutability, including missing configuration.
- A real Unity-to-local-HTTP integration session, `20261110T143942_9ee9eeb85ee4`, used identity `ENGINEERING_GAZE_INHERITANCE`, port 8766 and an isolated service/state/storage folder. Twelve live checks passed: both flags reached the frozen task configuration and saved configuration, telemetry was accepted, two outline renderers and one ray renderer were allocated, and a separate participant fixture allocated no renderers even with both requested flags true and a live ray enable attempt.
- Archived `session.json` reports both requested and effective flags true. All three configuration/inheritance events were read back, including the diagnostic Unity log. The session stayed in Preparation, was explicitly aborted, and completed its core archive. Practice and scored trial files contain headers only. No synthetic eye samples were generated or injected.
- The first live assertion ran before assignment receipt and failed its readiness check; after receipt, the full check passed. During this run the gaze status was `NO XR RUNTIME`, so the eye ray remained correctly hidden and preparation outlines remained hidden. This verifies configuration/initialization and denial behavior, not worn-headset visibility or gaze accuracy. Physical Quest Pro display confirmation remains pending.

Evidence: `Evidence/GazeDebugInheritance-20261110/` (restore copies, model/live reports, console output, service/archive status and isolated integration data). Reproducible checks: `AgentScripts/GazeDebugInheritanceValidation.cs`, `AgentScripts/gaze_debug_inheritance_server.py`, `Tools/AdaptiveTraining/test_gaze_debug_inheritance.py`. The installed Pipeline references used were `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md` and `commands/runtime.md`.

## Operation and final state

Start the updated Console service, or restart it after finishing any existing run, and allow Unity to compile this client update. Before claiming a fresh engineering assignment, select **Evidence VR > Advanced > Eye Tracking Debug > Target Highlight / Eye Ray**. The first claim freezes these preferences into that session. Already-claimed or historical assignments do not change. Both client and service must contain this update; a new Unity Player build is required for installed Players. No APK was built here.

Eye Ray still supports a logged live toggle for the current engineering session; that toggle does not rewrite the frozen configuration or next-session preference. Target Highlight remains a next-session choice. Target outlines require a running trial, valid tracked eyes and a visible matching target; Eye Ray may appear during Preparation, Instructions and Running. Neither is a guarantee of valid eye input.

Play Mode was stopped, the isolated server was stopped and the original local configuration was restored byte-for-byte (both preferences remain on, original port 8765). Pre-existing Pipeline auto-tick remains enabled. Production Console state, participant records and task rules were not changed. Start the normal Console workflow and create a fresh assignment for the intended identity before resuming.

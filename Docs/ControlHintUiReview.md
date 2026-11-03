# Controller guidance UI review

Date: 2026-11-03. Source revision: `controller-guidance-v1`. Shared Stage 1/Stage 2 instruction surface; application identity remains dev12/code 24. No new APK was built.

## Reason and implementation

The user requested more expressive, readable guidance for the left trigger and A/B buttons. The existing three preparation cards were updated in place, retaining the world-space canvas, panel geometry and controller input handling.

- A static side-view trigger schematic and large, cream-colored A/B keycaps identify the physical controls. Labels retain their meaning and keyboard alternatives after completion.
- A warm amber border and `TRY NOW` identify the first unfinished check. `TO CHECK` marks other unfinished checks; a sage `READY` pill marks completion. The state uses text as well as color, with no flashing, animation, haptics or new audio.
- The display counts completed checks from `0 / 3` to `3 / 3`. This is an input-check count, not model-training progress.
- The initial duplicate A/B footer is replaced by the count and a physical-button reminder. Compact A/B legends return on other instruction pages. The separate trial HUD remains unchanged.
- Preparation copy is shorter, with additional space reserved above the cards. Other pages regain the original 245-unit body-text height. The existing media/live-connection gate still controls continuation.
- `session.json` adds `controlHintRevision=controller-guidance-v1`, independently of the environment, personalized protocol and model-policy revisions.

The suggested order is instructional only: participants can check B before A, and the original three checks are still required before continuing. The input asset, preparation gate, trial engine, timing, scoring, physiological model and model-wait page behavior were not changed.

## Owning files

- `Assets/TaskPlatform/Runtime/LabPresentation.cs`: serialized card references, state rendering and preparation visibility.
- `Assets/TaskPlatform/Runtime/PersonalizedPresentation.cs`: concise personalized preparation wording.
- `Assets/TaskPlatform/Runtime/SessionRecorder.cs`: visual revision metadata.
- `Assets/TaskPlatform/Scenes/NBackExperiment.unity`: updated existing instruction hierarchy and serialized references.
- `AgentScripts/ControlHintUiUpgrade.cs`: repeatable edit-mode authoring helper, using existing RoundedSurface geometry and TMP.
- `AgentScripts/ControlHintUiValidation.cs`: engineering-only keyboard/visual verification helper outside runtime Assets.

## Software verification

Unity 6000.3.27f2 compiled the runtime update successfully. The authoring helper compiled without diagnostics and saved the existing scene. The engineering input helper then passed 113 assertions, including active-text overflow checks across initial, seating-complete, all-ready and practice-instruction states; actual C/A/B/Enter input processing; blocked early continuation; out-of-order B completion; persistent key labels; session metadata; instruction visibility; and entry into the existing observe-only practice/HUD with four-second responses and a 4.5-second cycle.

The four `CONTROL HINT UI PASS` messages were read back from the Unity console. Current console ground truth reported zero errors, two warnings and no compilation failure; retained historical messages and unrelated Unity AI subscription output were not cleared. The first validation-helper compile used two incorrect TaskConfig field names; these were corrected to the existing `responseSeconds` and `trialSeconds` before execution. No production compile failure resulted. During Play Mode initialization the Pipeline briefly became unavailable while OpenXR reported `XR_ERROR_FORM_FACTOR_UNAVAILABLE`; the same Editor subsequently resumed with an advancing player loop. No XR settings were changed.

The initial, intermediate, all-ready and instruction captures were inspected; text fits without clipping, and the trigger/A/B contrast and state changes are visible. Eleven protected engine/input/model/configuration/player-setting files match their pre-change SHA-256 hashes. Evidence is under `Evidence/ControlHintUI/`, including `validation.txt`, captures, protected-file comparison and console readback.

This evidence is synthetic Editor verification. Quest Pro controller ergonomics, binocular readability, seated comfort and standalone performance remain physically unverified for this revision. No participant evidence, device acceptance, model training or physiology qualification is implied. The Python service was not started for this UI work.

## Operation and restore

Open the current scene and enter Play Mode to see the new preparation cards. Continue using the left trigger (keyboard C), A, B and X/Enter exactly as before. The cards are visual instructions, not pointer-clickable buttons. The new visuals are in source/scene only; existing dev12 APKs do not contain them.

Restore point: `Evidence/ControlHintUI/pre-guidance-source.zip` contains pre-edit source, scene and three current project records. The working tree already contained earlier project changes; no unrelated work was reverted. The Unity Pipeline reference pages consulted were `commands/scripts.md`, `commands/capture.md`, `commands/editor-lifecycle-and-observability.md` and `commands/runtime.md`. Auto-tick was already enabled at 16 ms and remains enabled. Validation Play Mode was stopped after the checks.

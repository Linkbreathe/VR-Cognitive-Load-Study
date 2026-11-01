# Seated desk, full-window stimulus and minimal HUD

Date: 2026-11-01. Application `1.5.0-stage1` (Android code 8), presentation `spatial-lab-v1.5-seated-minimal-ui`, configuration `stage1-v1.5-visible4s`. Restore point: `46c432f`.

The user requested a lower desk for seated use, a sphere that stays visible until the four-second response window ends, and simpler UI with green/red countdown feedback.

## Result

The tabletop's upper surface is fixed at 0.72 m above the virtual floor. It no longer follows the calibrated eye height. Legs remain grounded; the plinth stem adjusts to keep the stimulus support below the eye-height sphere. Sphere radius, calibrated distance and position are retained. This fixes the previous desk behavior in which a higher tracked head raised the entire workbench.

The sphere is visible throughout the full four-second response window, including after an early answer. At four seconds it disappears and the response window closes. The next color still begins at 4.5 seconds, leaving a half-second blank gap. Pause freezes the remaining exposure time. Warmups use the same visibility duration and still reject answers. The onset cue and distraction schedule remain unchanged.

Running UI has four text components: a compact stage/mode/color-count line, a short matching rule, the countdown number and a small countdown caption. The large stage/countdown backplates, side rule card, progress bar, controls strip and separate feedback text are hidden. Two-back trials retain the exact comparison indices, for example `Compare #7 with #5`. Response controls remain on the pre-block instruction screen.

Practice feedback changes the countdown number and small caption to green (`Correct`) or red (`Incorrect`). An omission shows red `No response` during the gap. The next trial resets to neutral. Formal trials show no correctness text or color, preserving the experiment's existing no-feedback rule. The countdown runs `4`, `3`, `2`, `1`, then `0` during the closed gap. Instruction and pause screens retain their readable full-board layout.

## Verification

- Core task validation: 194,113 assertions across 2,000 sequence sets and five complete synthetic sessions.
- Independent export verification: 400 scored rows passed.
- `AgentScripts/SeatedMinimalProbe.cs`: 63 checks covering fixed desk height at 1.05, 1.20 and 1.65 m eye heights; grounded legs; visible sphere at 3.999 seconds; hidden sphere at the four-second deadline; pause-adjusted offset; correct/incorrect practice colors; neutral reset; absent formal correctness feedback; four active running text components and no text overflow.
- Fourteen-second live Editor check: four onsets and four onset cues; sphere observed after three seconds and hidden in the closed gap; green feedback observed; a response after 3.2 seconds accepted and saved. Software onset intervals: 4.5007, 4.5003 and 4.4994 seconds.
- The first live probe encountered the existing frame-stall pause while its own script compiler held the Editor thread. The probe was corrected to allow an initial preparation frame after compilation; the rerun passed. The production stall safeguard was retained.
- Neutral, correct, incorrect and paused captures were visually inspected. These are desktop and synthetic/editor-test observations, not physical headset comfort evidence.

Evidence: `Evidence/seated-minimal-v1.5.txt`, `Evidence/response-window-live-v1.5.txt`, `Evidence/automated-validation.txt`, and `Evidence/Visuals-v1.5/`. Android package evidence is in `AcceptanceRecord.md`.

Previews: [neutral](../Evidence/Visuals-v1.5/practice-neutral.png), [correct](../Evidence/Visuals-v1.5/practice-correct.png), [incorrect](../Evidence/Visuals-v1.5/practice-incorrect.png).

## Implementation and protocol notes

Skills used: `ui/SKILL.md`, `ui-ugui/SKILL.md` and Unity Pipeline. Existing Canvas objects were edited through the Editor builder; unused presentation elements were hidden rather than destroyed. Runtime behavior remains in `SpatialTaskView`; default visibility is set in `TaskModel` and `default-task-config.json`. No packages, audio assets, lighting settings or distraction assets changed.

The longer visible stimulus and reduced cues change the presentation/protocol; the new configuration and presentation versions are saved in session metadata. Existing custom `task-config.json` overrides retain their own explicit visibility values and must be updated to four seconds to adopt this revision. All authored content remains English. Play Mode was stopped after verification and the scene was saved.

# Spatial task and response timing review

Date: 2026-11-01. Presentation: `spatial-lab-v1.3-response4s`. Configuration: `stage1-v1.3-response4s`. Application: `1.3.0-stage1` (Android version code 5). Source checkpoint before this change: `71005d7`.

## Approved behavior

The user requested a sphere separated from the large interface and confirmed that the four-second countdown starts after every color appears. Each onset now starts one four-second response window and one quiet sound. The sphere stays visible for one second, preserving the memory task; responses remain possible for the rest of the window. A half-second closed gap follows before the next onset. Early answers do not advance the schedule, and only the first valid answer counts. Warmups use the same timing and cue but remain observe-only. Pause freezes task time; resume does not replay the cue. Practice errors say `Incorrect`.

During trials, the large instruction board is hidden. The existing three-dimensional sphere floats above a plinth and workbench, with separate world-space signs for stage, comparison rule, color/trial number, controls and countdown. The calibrated sphere distance remains 1.5 m. Its size, unlit color material and scoring are preserved. Instructions and pause screens use the full board. All authored content is English.

The cue is a cached, generated mono 48 kHz, 120 ms, 660 Hz sine with an 8 ms attack, 16 ms release and exponential decay. Default source gain is 0.12, configurable through `onsetCueVolume`; `onsetCueEnabled` can disable it. One non-spatial source provides consistent gain as the head turns. No new packages or mixer assets are required for this single sound layer. There is no music or response-dependent sound.

`trial_countdown_start` records each new window, and `audio_cue_requested` records the software playback request, cue revision and gain. Neither event measures physical sound onset. Session configuration and presentation revision distinguish this protocol from earlier timing versions.

## Visual review

The first captures showed the plinth stem obscuring progress and controls, and the plinth overlapping the instruction board. The signs and board were moved forward in depth. Final front, angled and instruction views were inspected after the fix:

- [Trial view](../Evidence/Visuals-v1.3/block-3-color-7.png)
- [Angled spatial view](../Evidence/Visuals-v1.3/spatial-angle.png)
- [Instructions](../Evidence/Visuals-v1.3/hard-rules.png)

Scoped visual scorecard (subjective desktop assessment, five-point scale): palette 4, consistency 4, lighting 3, stimulus separation 4, interface readability 4, timing feedback 4. The intentionally unlit sphere prioritizes stable stimulus color over shaded realism. Headset readability, comfort and perceived cue loudness require physical review.

## Verification

- Core validation: 194,112 assertions, 2,000 generated sequence sets and five complete synthetic sessions passed (`Evidence/automated-validation.txt`).
- Independent export verification: 400 scored rows from five sessions passed (`Evidence/export-validation.txt`).
- Presentation probe: 29 states, all 88 color counters, two-back references, warmup guidance, active text overflow, practice feedback and absence of formal correctness feedback passed (`Evidence/visual-validation-v1.3.txt`).
- Live Editor probe: four onsets produced four playing cue requests; countdown states `4`, `3`, `2`, `1`, `NEXT` were observed; a programmatic response after 3.2 seconds was accepted and saved. Measured software onset intervals were 4.5002, 4.5008 and 4.4995 seconds (`Evidence/response-window-live-v1.3.txt`).
- Android build and package evidence are recorded in `AcceptanceRecord.md`.

Synthetic probes are isolated from participant data. The live probe is marked `editor-test` and uses programmatic input. Quest 3 Link was confirmed by the user on the previous presentation; this revision's physical input, audibility and standalone launch are not yet verified.

## Implementation references

The Unity UI/uGUI, Unity CLI/Pipeline and craft-game-floors skills guided the implementation. Relevant craft references were art-direction, lighting, visual-scorecard and game-audio. Audio implementation was checked against Unity's [AudioClip.Create](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioClip.Create.html) and [AudioSource.PlayOneShot](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource.PlayOneShot.html) documentation. The user-approved protocol controls the scope of the visual and sound changes.

# Sequence guidance v1.2

The participant reported difficulty identifying their current stage and comparison target. The update uses the existing world-space panel to show four-stage progress, practice/main block number, total presented color position, and scored response-trial number. Warmups explicitly prohibit responses. The 2-back prompt names the current and reference positions, without showing the reference color. The instructional example numbers its three colors and explains the subsequent comparisons.

Application version: 1.2.0-stage1 (Android version code 3). Presentation: quiet-lab-v1.2-sequence-guidance. All project text remains English.

Validation: the isolated presentation probe checks stage/color counters over all 88 stimuli, verifies two-back indices and warm-up instructions, and checks 29 captured states for active TMP overflow and panel meshes. Practice feedback remains present; formal answer feedback remains absent. This probe uses synthetic states and does not establish headset usability or physical input performance. Runtime task logic and input bindings are unchanged.

The final captures use a temporary centered camera view; the original camera pose and URP XR-rendering setting are restored after each capture. The first inspection led to a font-size refinement for the stage label and numbered examples. The capture helper uses URP's allowXRRendering setting; the initial built-in-renderer stereo API produced a warning and was replaced. No scenery, palette or stimulus changes were made.

![Main block comparison](../Evidence/Visuals-v1.2/block-3-color-7.png)

![Warm-up guidance](../Evidence/Visuals-v1.2/block-1-color-1.png)

![Numbered rule example](../Evidence/Visuals-v1.2/hard-rules.png)

Evidence: `Evidence/visual-validation-v1.2.txt`. Pre-change source/scene/settings copies: `Evidence/BeforeSequenceGuidance/`. Build and inspection reports use the v1.2 suffix. Existing v1.1 evidence remains historical. Quest Link was previously confirmed visible by the user; the new guidance still requires a headset usability check.

The updated APK built successfully and passed signature verification. Final Play Mode validation reported zero Console errors; an ADB authorization warning remains separate from Link preview. Review-owned Play Mode was stopped after testing, matching the starting state. The saved scene is ready for the next Play session.

Patch 1.2.1 changes the incorrect practice-response label from `Try again` to `Incorrect`. No retry is introduced. Its application version is 1.2.1-stage1 (code 4), with presentation metadata quiet-lab-v1.2.1-sequence-guidance. Task behavior and layout are unchanged.

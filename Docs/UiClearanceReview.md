# Seated UI clearance correction

Application `1.6.1-stage1`, Android code 10, presentation `spatial-lab-v1.6.1-readable-seated-ui`. Restore point: `152b980`.

The user reported occlusion of both the large instruction panel and the compact running labels. The earlier instruction panel bottom could approach or intersect the fixed tabletop at lower calibrated eye heights. Running text was close to the stimulus and had poor contrast when a centered street video was behind it.

The instruction panel now sits 0.25 m ahead of the stimulus anchor (1.25 m from the calibration position), with its center at least 0.12 m above the anchor. Placement also enforces a minimum 0.12 m vertical clearance between the panel bottom and the 0.65 m tabletop. The same geometry covers preparation, instructions, pause, rest and completion. Placement happens during calibration and remains world-fixed during a block.

The running HUD is 0.22 m ahead of the anchor (1.28 m from the calibration position). The compact stage/rule group is 0.40 m above the sphere center; the countdown remains alongside the sphere. All four running labels use a dedicated TextMeshPro material with a narrow dark outline and the Mobile Distance Field shader's `OUTLINE_ON` variant. The stage label uses the regular light text color. There are still four running labels and no large running backplates. The outline keeps the countdown readable over changing video content without changing the stimulus material or adding feedback.

Stimulus timing, geometry, scoring, audio and the v1.6 distraction schedule are unchanged. All authored content remains English. Session metadata records the new presentation revision; configuration remains `stage1-v1.6-front-distractions` because task parameters are unchanged.

## Verification

`AgentScripts/UiOcclusionProbe.cs` renders isolated synthetic states at calibrated eye heights 1.05, 1.20 and 1.40 m. It checks text rectangles against the desk, plinth and sphere from three viewing offsets, across preparation, both task instructions, practice/formal running HUD, pause, rest and completion. It also checks TMP overflow and the outline shader variant. Evidence and 19 screenshots are under `Evidence/Occlusion-v1.6.1` and `Evidence/ui-occlusion-v1.6.1.txt`.

The seated/minimal-HUD regression probe verifies full four-second visibility, green/red practice feedback and neutral formal feedback. Five front-wall video positions are captured with the active sphere and formal HUD in `Evidence/DistractionPlayback-v1.6.1`. These are Editor checks, not physical Quest 3 acceptance. Sitting comfort and readability during natural head movements still need wearer review.

Implementation used the Unity uGUI and Unity CLI/Pipeline skills, with the installed Pipeline `commands/scripts.md`, `commands/capture.md` and `commands/build-and-compilation.md` references. Android package results are recorded in `AcceptanceRecord.md`.

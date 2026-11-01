# Satin stimulus review

Date: 2026-11-01. Application `1.3.1-stage1`, Android code 6; presentation `spatial-lab-v1.3.1-satin-sphere`. Restore point: `e26a5a7`.

The user requested a more tactile-looking sphere. The stimulus now uses URP Lit with metallic 0 and smoothness 0.42, producing soft highlights and gradual shading under the existing fixed room light. All four base palette values remain unchanged. Shaded surface pixels differ from the previous unlit version, so the presentation revision is recorded in session metadata.

The first material preview exposed coarse surface tessellation in close-up. The fix pass replaced the mesh with a smooth 64-segment, 32-ring sphere (3,968 triangles), preserving its 0.5-unit mesh radius and the existing calibrated world size. Timing, audio, sequence, scoring, lighting and UI are unchanged. No packages, textures, post-processing or extra lights were added.

`LabVisualUpgrade.Apply` creates and assigns `StimulusSatin.mat` and `StimulusSmoothSphere.asset` through Editor APIs. The original unlit material remains available. The procedural mesh is an Editor-generated asset, with no per-frame mesh generation.

## Verification

`AgentScripts/StimulusMaterialProbe.cs` renders all four colors at the task distance and a blue close-up, using an isolated synthetic engine with no recorder or audio subscribers. The original engine and camera are restored afterward. Final captures were visually inspected, the supported Lit shader and smoothness were checked, and the successful probe log was read back. Editor frames advanced from 1 to 508 during the first pass. Final console check before building: zero errors and no compilation failure. Physical Quest 3 appearance remains a separate check.

- [Blue](../Evidence/Visuals-v1.3.1/blue.png)
- [Orange](../Evidence/Visuals-v1.3.1/orange.png)
- [Green](../Evidence/Visuals-v1.3.1/green.png)
- [Magenta](../Evidence/Visuals-v1.3.1/magenta.png)
- [Close-up](../Evidence/Visuals-v1.3.1/satin-closeup.png)
- Probe result: `Evidence/stimulus-material-v1.3.1.txt`

Scoped subjective desktop scorecard, before to after:

Palette discipline: 4/5 to 4/5 — all four base colors retained and visibly distinct.

Style uniformity: 4/5 to 4/5 — restrained satin surface fits the laboratory setting.

Lighting depth: 2/5 to 4/5 — the sphere now has a visible light-to-dark gradient and soft highlight.

Silhouette: 3/5 to 4/5 — smoother outline, same size and spherical shape.

UI intentionality: 4/5 to 4/5 — existing task signs retained.

Feedback visibility: 4/5 to 4/5 — existing response countdown retained.

Play Mode was started for the probe and stopped afterward. Existing background ticking settings were retained. The scene and generated assets were saved. APK evidence is recorded in `AcceptanceRecord.md`.

## References and scope

Skills used: craft-game-floors (`scene-lighting.md`, `visual-scorecard.md`), Unity CLI and Unity Pipeline. Pipeline references: `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, and the previously consulted build/capture references. The existing art direction takes precedence over general game-lighting suggestions, so no global lighting or post-processing changes were made. Material properties were checked against the official [Unity 6.3 URP Lit reference](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/built-in/shaders-in-universalrp/reference/lit-shader).

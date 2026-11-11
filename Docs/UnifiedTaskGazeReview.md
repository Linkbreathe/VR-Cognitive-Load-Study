# Shared task gaze area for practice and A/B

2026-11-10, Stage 2/3, revision `gaze-roi-v5-unified-sphere-tray-area`.

## Requested correction

The researcher clarified that task gaze means the task area: sphere, tray and nearby space, as in practice. The previous A/B-specific branch replaced the shared area with `sphere-bounds-v1` and required the sphere itself to be visible. That implementation did not match this clarified requirement. It has been removed for both A and B, including continuous and legacy comparison configurations.

## Current definition

All non-simulated adaptive modes now share `GazeRegions.TaskBounds`: the world-axis-aligned bounding box enclosing the sphere and visible tray, expanded by 0.04 m on each face. It includes the space between them. This reuses practice geometry without increasing its existing padding. It is a local task area, not the entire instruction panel, countdown or room. Missing/hidden tray falls back to padded sphere bounds.

The area is active only in Running while either the sphere or tray is visible. The persistent tray can keep it active during the inter-stimulus gap, matching practice. Preparation, instructions, pause and end remain excluded. Invalid eyes remain invalid. Task-before-video priority, movie classification and engineering-only debug rendering remain unchanged. The sphere outline still cannot reveal a hidden sphere.

Production classification, Console task-area projection, gaze CSV membership and evidence-guard target/visibility now use that same area. Guard `taskVisible` denotes this active task area. CSV `task_visible` retains its historical meaning (visible sphere); `task_region_visible` records the broader active area. Metadata, CSV ROI revision and `gaze_task_region` bounds events use the v5 revision. Console requires no separate geometry patch: its existing layout receives these production bounds.

ON/OFF numeric thresholds, valid-eye requirements, eligible-trial selection, pre-response/playback window intersections, task timing, response scoring and support-probe logic are unchanged. Area membership can change gaze evidence and therefore decisions; this is an intentional versioned semantic change. The new area is not evidence of better physiological validity or successful real-eye calibration.

## Validation

- Unity 6000.3.27f2 compilation completed; console ground truth has `compilationFailed=false`, `consoleErrors=0`.
- `UnifiedTaskGazeValidation.Run`: 13 checks in Edit Mode and 13 in Play Mode, using actual scene sphere/tray geometry with isolated synthetic rays. Sphere, tray, intervening space, 3 cm margin, exact 4 cm padding, outside/hidden/invalid exclusions and comparison/non-comparison metadata passed.
- `GazeDebugValidation.Model`: 2,042 synthetic geometry, boundaries, invalid-eye, priority, participant-lockout and shader checks passed.
- `GazeConsoleValidation.Run`: 16 projection, layout and transport checks passed.
- `EvidenceGuardValidation.Run`: 106 synthetic transport/execution checks passed, including task classification/visibility transport and missing-data duration caps.
- Runtime logs were read back. Evidence: [UnifiedTaskGaze-20261110](../Evidence/UnifiedTaskGaze-20261110/), including pre-edit recovery copies and isolated synthetic metadata. Existing fixture outputs also reside in `Evidence/GazeDebug/`, `Evidence/GazeConsole/` and `Evidence/Stage3/`.

The scene controller was temporarily disabled during synthetic Play Mode to prevent operator assignment claims, then restored. Play Mode is stopped; existing Pipeline auto-tick remains enabled. No new participant session, production gaze injection, service restart, APK build or worn Quest Pro acceptance was performed. Installed Pipeline references used: `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md`, `commands/runtime.md`.

## Operation and historical data

Restart Play Mode and create a fresh intended assignment to use the new classification and metadata. In an engineering practice/comparison, inspect the shared task-area outline in the Console and direct real gaze to the sphere, tray, gap and margin. Verify that surrounding points outside the area remain excluded. This physical check remains pending.

Archived P002 `sphere-bounds-v1` rows are retained unchanged. Their near-zero task share describes the old sphere-only definition; it must not be presented as a measurement of the newly requested area. Earlier practice v4 used the same geometry as v5; v5 identifies its unified application to comparisons and guard visibility. Do not pool historical/new fractions without accounting for ROI definition and eligibility. The preceding seated-height normalization remains in place; it is separate from this ROI correction.

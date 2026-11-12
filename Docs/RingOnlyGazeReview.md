# Ring-only engineering gaze marker

2026-11-12, Stage 2/3. Revision `engineering-gaze-reticle-v7-ring-only`.

The researcher requested removal of the thin directional ray and replacement of the animated bird with the earlier circle. `EngineeringGazeRay` now allocates only a closed 32-segment ring at the existing hit point. It restores the earlier 0.6-degree radius (approximately 1.2 degrees across), camera-facing geometry, distance scaling and depth-independent display. No directional line, bird renderer or GIF animation is instantiated. Existing bird source assets and earlier validation evidence are retained as historical material; they are not used by the current marker.

The existing **Evidence VR > Advanced > Eye Tracking Debug > Eye Ray** toggle and saved preference control the ring. LIVE is white; FROZEN is amber and still invalid in gaze data; HELD is gray at the last drawn point. The marker persists across phases once a position is available, and WAITING shows no invented point before the first sample. Stale input, pause/focus handling, engineering-only exclusion and the separate Target Highlight behavior remain unchanged. Gaze validity, classification, physiology, task timing, scoring, comparison stages and room presentation are not modified.

New session metadata uses the updated `gazeRayRevision` via the existing recorder; configuration and toggle events include the new revision. This is a Unity presentation change and does not require a Python Console restart. Start a fresh Play Mode/session after compilation; an installed Player requires a rebuild.

## Verification

Use `AgentScripts/GazeRayValidation.cs` for the current regression. `AgentScripts/BirdReticleValidation.cs` is a historical v6 fixture and is not a test of this revision. Evidence is isolated in `Evidence/RingOnlyGaze-20261112/`. The pre-edit runtime and regression sources are backed up in `Temporary/RingOnlyRestore-20261112/`.

Unity compilation passed. The regression passed 64 checks in Edit Mode and 64 checks in isolated Play Mode, covering ring geometry and angular scaling, absence of a directional line and bird renderer, state colors, persistence, toggling and engineering exclusion. The synthetic render was visually inspected: `Evidence/RingOnlyGaze-20261112/synthetic-ring.png`; the Edit Mode copy is `edit-mode-ring.png`. CLI results are retained in `Temporary/RingOnlyRestore-20261112/edit-validation.json` and `play-validation.json`. Current Unity console errors were zero. Original scene restored and Play Mode stopped; no production controller/acquisition client or participant session was started. Validation results are also recorded in the same-date ProjectLog and AcceptanceRecord entries.

Worn Quest Pro appearance, brightness/comfort and actual eye alignment require a separate engineering check; synthetic rendering is not device acceptance. The local Unity Pipeline `commands/scripts.md` and `commands/editor-lifecycle-and-observability.md` guided compilation and isolated testing.

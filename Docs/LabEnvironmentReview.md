# Warm study environment review — dev12

Date: 2026-11-03. Application `2.0.0-stage2-dev12`, Android code 24; environment `warm-study-v1`. The configuration/Python service stays `2.0.0-stage2-dev11`, protocol `personalized-rf-v2`, policy `personalized-rf-pilot-v2-window5`. This is a presentation change, not new model or participant qualification.

## User direction and implementation

The user requested sourced assets and a more complete room, then rejected the initial gray laboratory direction as reminiscent of an interrogation room. The final direction is a welcoming study: cream plaster, oak flooring and furniture, open bookshelves, muted book covers, a linen reading chair, fixed lamps and restrained plants. The unshipped laboratory drafts remain in engineering evidence only.

- Real CC0 wood/plaster textures from Poly Haven and static furniture models from Kenney. [Source credits](../Assets/TaskPlatform/Environment/ThirdParty/AssetCredits.md) and `Evidence/LabEnvironment/asset-downloads.json` record source, license, files and hashes. Texture source bytes are unchanged.
- Architecture, shelf/book details, sideboard, rug and beveled furniture surfaces are authored in the project. The dark front acoustic towers, metal floor grid and exposed equipment cart are removed. The monitor is inactive; book covers have no readable content.
- Four warm ceiling area lights and three practical lamps are fully baked. Two non-directional lightmaps, 24 texels/m, 512 atlas size reported, 1,310,720 bytes reported by the bake tool. No runtime light probes were added. The original realtime key and ambient settings still light the dynamic stimulus and movable task desk. No animated scenery, exposure effects, ambient sound or music.
- The new furnishing root has 124 renderers, 14 assigned materials and 13,419 triangles. Shared materials/static batching and 1K ASTC 6x6 textures limit mobile cost. These counts are not a headset frame-rate measurement.
- A fixed narrow charcoal backing under the existing two-line running status restores contrast against the brighter wall. Wording, text placement, countdown and feedback logic are unchanged. This also applies in Stage 1.
- `session.json` now records `environmentRevision`, independently of application, protocol and model versions. The scene applies the same environment to all task conditions. Legacy scenes without the component report `quiet-lab-legacy`.

## Preserved experiment behavior

The stimulus mesh/material/position, tray, 0.0864 m surface clearance, 0.65 m tabletop height, response scoring, four-second response/visibility, 4.5-second cycle, onset cue, movie dimensions/position/schedules, shared gaze region, calibration/model policy and condition allocation are retained. Practice-only correctness feedback, oral questionnaires, continuous headset wear and the no-rest preparation flow remain unchanged. The desktop material changes to oak without moving its geometry.

Ten protected source/configuration/material files were compared byte-for-byte with the pre-update restore ZIP and matched. `protected-source-check.json` records hashes. Changing visual context can still influence experience; it must not be silently pooled with earlier visual versions in participant analyses.

## Software verification

- Unity compilation passed. Final live console ground truth: zero errors and zero warnings. Historical output retains resolved intermediate compilation errors, unrelated Unity AI `NoSubscription` log messages and earlier XR audio fallback warnings; the entire history is not described as error-free.
- Existing protocol suite re-run: 15,945 synthetic-clock assertions passed. Python model code was unchanged and its 94-test dev11 evidence remains historical, not a new dev12 run.
- Saved scene was reopened: 421 checks passed, including baked model/material persistence, supported authored materials, texture constraints, no added animation/audio/colliders, original realtime light and dynamic-task exclusion. 405 sampled rays from nine seated positions to a grid on the 3.2 x 1.8 m movie rectangle were clear of new furnishing bounds. This is sampled software coverage, not all possible head positions.
- Live engineering session `20261102T233215323Z_9bcbbb34`: seven checks passed. Real Editor frames advanced; actual metadata includes the new application/environment and retained protocol contract. Sphere clearance read 0.08639991 m. Practice was started programmatically with an `engineering_preview` marker. No participant, full-duration run or physiology was involved.
- Three room views and the live task view were inspected. The first warm bake was too dim; increased baked fill, practical lamps, more indirect bounces and reduced AO distance corrected the dark walls/ceiling. A second live check verified the status backing after finding insufficient text contrast.

Evidence: `Evidence/LabEnvironment/`, `AgentScripts/LabEnvironmentValidation.cs` and `LabEnvironmentPreview.cs`. [Release and build verification](LabEnvironmentRelease.md).

## Scoped visual scorecard

Scores are an editorial inspection of captures, not participant ratings. The scope is environment appearance/readability; task feedback and UI wording were intentionally retained.

Palette discipline: 4/5 — warm cream/oak/linen with muted books and greenery; explicit authored materials; earlier gray draft 3/5.
Style uniformity: 3/5 — consistent study furnishings, simplified low-poly furniture rather than photorealistic assets; earlier gray draft 2/5 for the requested mood.
Lighting depth: 3/5 — baked fill/contact shading and readable ceiling; initial warm bake 2/5 due dark side walls.
Silhouette: 4/5 — stimulus remains isolated, desks/bookshelves/chair identifiable; retained task silhouette 4/5.
UI intentionality: 3/5 — existing compact HUD with a fixed contrast backing; first warm live frame 2/5 due low contrast.
Feedback visibility: 3/5 — countdown visible; practice-only response feedback and neutral formal behavior retained by unchanged implementation and protocol checks. No new feedback claim.

## Boundaries and handoff

Quest Pro standalone brightness, color recognition, reading comfort, furniture scale, distraction, full-duration performance/thermals, actual sensor contact and eye tracking remain pending. No physical-device acceptance is inferred from Editor or APK verification. No new packages, model qualification, sensor thresholds or media gains were introduced.

Play Mode was stopped. Existing auto-tick at 16 ms and run-in-background were retained. Restore: `Evidence/LabEnvironment/pre-dev12-source.zip` and `pre-dev12.patch` preserve the earlier dirty worktree. No commit or remote push was performed. [Style and skill/documentation references](LabEnvironmentStyle.md) include the art, lighting, source-selection and scorecard references; `ui` and `ui-ugui` were additionally used for the minimal contrast correction.

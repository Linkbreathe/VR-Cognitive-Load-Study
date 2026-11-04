# Lower stimulus and movie clearance

Date: 2026-11-03. User-approved shared task-geometry revision `low-stimulus-v2-movie-clearance`, delivered in engineering candidate `3.0.0-stage3-dev4` / Android code 28. The user requested a lower round support, a lower and slightly larger sphere, and minimal overlap with the movie area.

## Geometry

The round tray's top is now **0.72 m above the virtual floor**, 0.07 m above the unchanged 0.65 m tabletop. Its existing 0.05 m thickness and 0.30 m diameter are retained. The stem is shortened to 0.02 m and still joins the table and tray. At the nominal 1.20 m seated eye height, the previous tray top was approximately 1.035 m.

Sphere diameter is **1.08 times the existing configured size**. With the default 1.5 m distance and base six-degree `visualAngle`, diameter changes from approximately 0.157223 m to **0.169801 m**. The gap between the sphere's lower surface and tray top changes from 0.0864 m to **0.025 m**. Default sphere center is therefore **0.829901 m** above the floor, previously approximately 1.20 m. The diameter is recalculated from the base configuration at placement, so recalibration does not accumulate scaling. The configuration angle remains the base size parameter; actual diameter and the multiplier are recorded explicitly.

The support and sphere are tied to the fixed desk height when recalibrating the task anchor. The two desk signs retain their existing locations and appearance. Movie position and 3.2 x 1.8 m dimensions are unchanged. This shared geometry applies consistently to task conditions, including Stage 1, without changing timing, response scoring, sequence generation, media schedules or control rules.

`session.json` records the placement revision, 0.025 m clearance, diameter multiplier and tray surface height. Placement events record those values plus the actual sphere position; calibration events retain actual diameter. The gaze classification algorithm and padding are unchanged, but its sphere/tray bounds follow the new rendered geometry. Do not treat recordings from the two geometry revisions as identical visual exposures.

## Verification and limits

- Unity compilation passed; **81 retained progress/budget/UI checks passed**. Task/phase logic was not changed.
- **12 geometry/metadata/gaze checks passed**: actual size, tray/table/stem contact, air gap, lower sphere, retained movie size, dynamic gaze targets, missing-eye rejection, metadata, projected overlap and desk-relative placement after a synthetic 0.20 m anchor-height change. The PASS log was read back.
- At the nominal Editor camera position (eye height 1.20 m), a conservative projected bounding-box calculation gives **16.15 pixels of potential vertical overlap at 720p**. The actual sphere silhouette overlaps only a small part of the movie's bottom edge in the inspected image. This is a synthetic camera measurement, not a guarantee for every seated head position or headset eye projection.
- Actual-scene task-only and simultaneous movie/sphere captures were inspected: `Assets/Evidence/LowerStimulus/task-low-sphere.png` and `movie-low-sphere.png`. The latter uses a muted frame from an existing local clip on the unchanged movie surface, outside the protocol scheduler. It is explicitly marked as an engineering preview; it does not validate scheduled media playback or participant physiology. Temporary preview resources were released before leaving Play Mode.
- All seven protected files match their pre-change hashes: task engine, personalized protocol/presentation, media runtime, gaze algorithm, saved scene and restored PlayerSettings. The pre-build live console reported zero errors/warnings; whitespace checking passed. Python was unchanged; the earlier 124-test host run is retained evidence, not rerun for this geometry revision.

Quest Pro stereo projection, seated viewing comfort, target visibility, eye classification and standalone performance remain physically unverified for this revision. In particular, lower seated viewpoints can produce more overlap than the nominal Editor preview.

## Delivery and rollback

New APK: `Builds/Stage3Dev4/EvidenceVR-Stage3-dev4.apk`. It uses the existing dev2 Python host/guard/configuration package; no new host install is required. The dev3 APK retains the previous taller support and sphere position.

Android IL2CPP build succeeded in **71.933 s**, zero build errors and 347 retained warnings. APK size: **100,271,454 bytes**; SHA-256 `babab540689232453446b69ae5cb45affae69cfc30363aadd21645fe8db33fdb`. Manifest confirms `3.0.0-stage3-dev4`, code 28 and ARM64; APK v2 signature verified. Build report, badging, signature and identity are in `Evidence/LowerStimulus/`. BuildReport intermediate size is not the APK size.

Source restore ZIP: `Evidence/LowerStimulus/pre-lowering-source.zip`. Evidence: `Evidence/LowerStimulus/`. This is runtime placement; the saved scene is not rewritten. Existing uncommitted work and unrelated recovery assets are retained. Project PlayerSettings were restored to dev12/code 24 without a remaining diff. Play Mode is stopped and the pre-existing auto-tick setting is retained. No headset installation, participant recording or Git publication was performed.

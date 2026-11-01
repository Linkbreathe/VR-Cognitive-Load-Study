# Seated calibration and front-wall distractions

Application `1.6.0-stage1`, Android code 9; presentation `spatial-lab-v1.6-seated-front-windows`; configuration `stage1-v1.6-front-distractions`. Restore point: `e7a429e` (v1.5).

## Height correction

An earlier editor-test calibration logged a virtual eye height of 2.22 m. The tracking rig supplied a 1.20 m offset while requesting Device origin; a returned local eye height could therefore be added to that offset. The left trigger places the sphere and its support relative to the resulting eye position. It did not raise the fixed tabletop itself.

Calibration now prefers Floor tracking with zero rig height offset. If Floor is unavailable, Device tracking sets the rig offset to `1.20 - localEyeHeight`, providing a consistent seated virtual eye height of 1.20 m. This fallback is a virtual seating convention, not a measurement of the physical floor. Calibration waits two frames for the origin change, records the actual origin/local eye/rig values, and places the task. Repeated calibration does not accumulate height. Initial preparation placement also waits for the first tracked pose. The tabletop is now fixed at 0.65 m, with grounded legs; the sphere retains its existing distance and angular size.

Sit facing the front wall, then press the **left trigger** in preparation. **X** continues after calibration and the response-button check; it does not calibrate height. Recalibration is blocked during active trials.

## Distraction revision

Default formal blocks have nine events: three street videos, three door sounds and three conversations. The first event starts 0.5 seconds into the first scored trial, after the two observe-only warmups. Subsequent planned onsets are separated by three or four trials: 13.5 or 18 seconds with the current 4.5-second cycle. The order of these intervals is seeded. Each default block uses five three-trial gaps and three four-trial gaps; the last onset is 131 seconds into the 144-second block. Preparation, practice and rest remain distraction-free.

Both formal blocks share event types and planned onset positions. Audio sides are mirrored; media IDs differ. Default full sessions use six distinct street clips from the eight-file local pool. The longest exposure plus 1.5-second decoder allowance fits between events and before block completion. Short custom blocks reduce complete three-type sets; configurations with trial cycles too short to preserve this safe cadence yield no events. Existing custom overrides retain their requested count.

Only one front-wall video window is visible at a time. Positions are center, upper-left, upper-right, lower-left and lower-right. A seeded shuffled position pool covers all five across a default session without consecutive repeats, including the block boundary. A window remains still during each exposure. Its video surface measures approximately 1.49 by 0.84 m and lies at z=2.92 m, just inside the front wall. The original side windows are inactive. Rear audio locations and gains are unchanged.

Every planned/requested/started/ended event records `side=front` and `window_position` for video, along with asset ID, duration and timing. Session metadata records the new presentation/configuration versions. The schedule version is `distractions-v2-front-wall-3-or-4-trials`.

## Verification

- 66,013 checks over 1,000 seeds: nine events, balanced types, 3/4-trial gaps, non-overlap including decoder grace, reproducibility, six distinct videos, all five positions and no consecutive position repeats.
- 63 seated/HUD checks: 0.65 m tabletop at three eye heights, grounded legs, full four-second visibility, pause behavior, green/red practice feedback, neutral formal feedback and four active HUD labels.
- The actual calibration coroutine corrected simulated raw local height 1.02 m plus old rig offset 1.20 m to virtual eye height 1.20 m; repeated calibration remained stable. No live headset was connected during this probe.
- Core task validation and independent export checks were rerun; synthetic and Editor evidence remains separate from participant records.

A 138.73-second isolated live Editor run completed all nine scheduled exposures without skips or decoder errors and verified video pause/hide/resume. All five front-wall positions were captured and inspected, including synthetic views with the sphere and formal HUD. Centered footage is behind the foreground task elements; headset review should include label contrast during this exposure. The Android build succeeded in 139.7 seconds with zero errors and one crash-symbol warning. The 70,027,606-byte ARM64 APK passed v2 signature verification. Details are recorded in `AcceptanceRecord.md`. Physical seated comfort, floor calibration and front-wall visibility still require review by the Quest 3 wearer. Editor evidence cannot establish those outcomes.

Skills/reference workflow: Unity CLI and Unity Pipeline (`scripts`, `build`, `capture`, `runtime` and Editor lifecycle references). Unity 6.3 tracking-origin definitions: [Floor](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/XR.TrackingOriginModeFlags.Floor.html), [Device](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/XR.TrackingOriginModeFlags.Device.html).

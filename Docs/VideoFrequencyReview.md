# More frequent street videos

Application `1.7.1-stage1`, Android code 12; presentation `spatial-lab-v1.7.1-more-video`; configuration `stage1-v1.7.1-more-video`; schedule `distractions-v4-five-videos-six-audio`. Restore point: `019e94e`.

The user requested more frequent videos and no other feature changes. The default video count increases from three to five per formal block (ten per complete session). Each video still lasts ten seconds. Environmental sounds and speech retain three exposures each per formal block, including the same six audio families. Feedback, stimulus timing, room geometry, materials, media files and audio paths are unchanged.

To fit five ten-second videos plus six audio exposures inside the existing 144-second formal block without overlap, the eleven events use 12.3-second onset spacing after the warmups. Video occupies alternating inner slots: block-relative onsets 21.8, 46.4, 71.0, 95.6 and 120.2 seconds. Thus videos appear every 24.6 seconds, approximately once per 5.5 trials. Audio occupies the other six slots, with seeded type order. Both formal blocks share onsets and event types.

The overlap allowance remains ten seconds of exposure plus 1.5 seconds of video-start grace. All events finish before block completion. Pause freezes the schedule as before. The old 3/4-trial spacing for all distractions is superseded by this denser interleaving; the sound count is unchanged.

`streetEventsPerBlock` now controls video count independently. `distractionsPerTypePerBlock` controls each of the two audio categories. Custom short blocks cap the requested counts to fit safely. A default block uses five distinct clips and all five window positions. The ten video presentations across a complete session use the eight-clip pool, so two clips repeat across blocks; consecutive video positions do not repeat. Practice remains distraction-free.

Verification uses the existing 1,000-seed scheduling checks, with explicit five-video/six-audio counts, 24.6-second video intervals, unchanged audio-family coverage, non-overlap and deterministic replay. The real-time Editor probe checks all eleven events and ten-second exposure durations. Evidence is isolated under `Evidence/`; build and playback results are recorded in `AcceptanceRecord.md`.

Workflow: Unity CLI/Pipeline, with installed `commands/scripts.md` and `commands/build-and-compilation.md` references. This is a schedule revision, not a new media or feedback revision.

# Environmental distractions

Current application `1.6.0-stage1` (Android code 9), presentation `spatial-lab-v1.6-seated-front-windows`, configuration `stage1-v1.6-front-distractions`. See `FrontWallCalibrationReview.md` for the current revision; the media catalog originated in v1.4.

## Approved protocol

The user approved three distractions and explicitly chose formal blocks only, equal frequency in Easy and Hard, with no overlap between distraction types. Default formal blocks contain three events of each type: nine events per block, eighteen per complete session. Practice, instructions and rest have no scheduled exposure. The existing four-second stimulus visibility, four-second response window, half-second gap, onset cue, color sequence and scoring are preserved.

| Type | Presentation | Duration | Local pool |
|---|---|---|---|
| Street | One silent video appears at a seeded front-wall position, then disappears | Seeded 5–8 seconds | Eight street videos |
| Door | One recorded door action from a rear-left or rear-right source | 1–2 seconds, full prepared clip | Eight door clips |
| Conversation | Two distinct English voices outside the rear wall, with a gentle low-pass filter | Approximately 5.7–7.7 seconds, full clip | Eight original two-speaker dialogues |

Conversations are synthesized locally with Microsoft David Desktop and Microsoft Zira Desktop. They are not recordings of real conversations. The scripts are ordinary neutral exchanges, saved in the media manifest, with no task answers or instructions. The voices can be replaced with consented recordings while retaining the clip IDs and updating the catalog revision/hash.

## Randomization and timing

`distractionSeed=0` creates a fresh seed for each new session, independently of the existing stimulus-sequence seed. The resolved seed is frozen in `session.json`. Setting a nonzero seed reproduces the distraction plan. `DistractionPlan.Version` is `distractions-v2-front-wall-3-or-4-trials`.

The two formal blocks use the same randomized order of distraction types and onset positions, with mirrored audio sides and different selections from shuffled media pools. Each type occurs three times per default block. Six distinct street clips are selected across the complete session, from the eight-clip pool. A later session may select different clips. Audio clip lengths vary slightly, so frequency and onset positions are matched; cumulative audio duration is not forced equal.

Events begin after the warmups. The first event starts 0.5 seconds into the first scored trial. Subsequent planned onsets are separated by three or four trials (13.5 or 18 seconds by default). Events reserve room for the longest ten-second exposure and a 1.5-second video-start grace period. An overlap guard skips a conflicting event and records the reason instead of mixing it. Very short custom blocks reduce the number of complete three-type sets to fit safely; the default 30-scored-trial blocks retain three sets.

Video preparation occurs ahead of exposure. The windows become visible only after the first decoded frame arrives. If a video cannot start within 1.5 seconds, it is skipped and logged. Playback errors do not change task answers or silently substitute another clip. Pause hides video windows, pauses media and freezes the task-relative clock; resume continues the active event. Block completion, restart, abort and platform errors cancel active media.

## Files and playback

- `Assets/StreamingAssets/Distractions/Streets/`: eight silent 1280 × 720, 24 fps, H.264 baseline MP4 files, each eight seconds long.
- `Assets/TaskPlatform/Media/Doors/`: eight mono 48 kHz WAV files.
- `Assets/TaskPlatform/Media/Conversation/`: eight mono 48 kHz WAV files.
- `Assets/TaskPlatform/Media/distraction-media-manifest.json`: source URLs, source/license category, transformation details, durations, dialogue text and SHA256 values for all 24 prepared assets.
- `MediaSources/`: original downloaded video/audio files, source-page and license snapshots, and generated speech originals.
- `Tools/prepare_distraction_media.py` and `Tools/create_conversation_media.ps1`: repeatable preparation scripts.

Prepared runtime media totals 28,004,343 bytes. Street audio is removed, keeping the visual event distinct from auditory distractions. Door source files longer than two seconds are time-compressed to fit; source durations and tempo factors are recorded in the manifest. Short clips are padded to at least one second. Audio is normalized and uses a short fade-in. One decoder/render texture supplies the active front-wall window. Five seeded positions cover the center and four corners without consecutive position repeats; the window remains stationary during exposure.

All runtime media is bundled locally. On Android the video files are copied from packaged StreamingAssets into the app's private persistent `DistractionMedia-v1` directory before playback. No online streaming or login is required.

Audio sources use mono clips, full 3D blend, zero Doppler and fixed world positions. Door sources are at x ±2.4 m, y 1.35 m, z −2.65 m. Conversation sources are at x ±1.9 m, y 1.55 m, z −3.35 m, beyond the rear wall. The existing room origin and walls define these positions. An installed spatializer is used if available; the inspected Editor currently has no spatializer plugin, so native distance attenuation and stereo panning are used. Physical front/back localization is not established by source coordinates alone and requires headset review.

## Configuration

| Field | Default | Meaning |
|---|---:|---|
| `distractionsEnabled` | true | Enable the formal-block schedule |
| `distractionSeed` | 0 | Generate and save a fresh seed; nonzero fixes the seed |
| `distractionsPerTypePerBlock` | 3 | Requested count of each type, reduced only if a custom block is too short |
| `distractionMasterVolume` | 1.0 | Multiplier for both distraction audio types |
| `doorVolume` | 0.35 | Door source gain before spatial attenuation |
| `conversationVolume` | 0.30 | Conversation source gain before spatial attenuation |

This uses an explicit configuration gain layer rather than an AudioMixer asset. The onset cue retains its independent gain. Actual quietness and intelligibility depend on Quest output level and seating position; they have not been perceptually calibrated.

## Data and interpretation

Every session saves a copy of the media manifest as `distraction-media.json`. `session.json` contains the resolved seed, configuration and presentation revision. `events.jsonl` includes `distraction_configuration`, `distraction_media_ready`, `distraction_planned`, `distraction_video_prepared`, `distraction_requested`, `distraction_started`, `distraction_paused`, `distraction_resumed`, `distraction_ended`, `distraction_skipped` and `distraction_media_error` where applicable.

Event details identify the type, index, media ID, side, front-window position, planned block-relative onset, duration and gain. Actual start events use a video first-frame callback or an audio playback request; these are software timestamps, not measured photon or acoustic onset. Planned timestamps saved before a pause retain their original schedule; the start event's planned timestamp reflects the current pause-adjusted clock. Exposure durations exclude pauses. Decode failures and interrupted exposures remain explicit in the log.

These additions provide distractors, not evidence that attention or physiology changed. Preserve the protocol/presentation revision when analyzing data or comparing with earlier sessions.

## Historical v1.4 validation

- 37,004 checks over 1,000 seeds: balanced counts, formal-only behavior, deterministic replay, no overlaps including video grace, nonrepeating street selections, pause clocks, media durations, mono clips and source placement.
- Existing task validation: 194,112 assertions and 2,000 sequence sets passed; independent export verification passed on 400 scored rows from five synthetic sessions.
- A 127.47-second isolated live Editor probe played all six scheduled events without skips or decoder errors, captured both windows, and verified video hiding, pause/resume and the frozen clock.
- A focused audio probe confirmed both sides' door and conversation sources actually advanced, followed the master gain, paused/resumed, stopped on cancellation and stopped on abort. One enabled listener was verified.
- All eight prepared videos were inspected as silent H.264 files and reviewed in a contact sheet; left and right in-scene window captures were inspected.
- Probe data lives under `Evidence/`, separate from participant and ordinary editor-test session data. Final build/package checks are in `AcceptanceRecord.md`.

Physical Quest 3 video decoding/performance, perceived direction, comfortable loudness and intelligibility remain device acceptance checks. The Editor probe does not substitute for them.

## Sources and review references

Street footage: [Mixkit street collection](https://mixkit.co/free-stock-video/street/), under the per-item [Stock Video Free License](https://mixkit.co/license/modal/videoFree/). Door recordings: [Mixkit door collection](https://mixkit.co/free-sound-effects/door/), under the [Sound Effects Free License](https://mixkit.co/license/modal/sfxFree/). Each file's exact page and download URL is in the manifest. Sound effects may be incorporated into the application; do not redistribute the source recordings as standalone stock assets or a source-file collection. Local license snapshots are preserved in `MediaSources/`.

Implementation references: official Unity 6.3 [VideoPlayer](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/video/videoplayer) and [AudioSource.spatialBlend](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/audiosource/spatialblend). Skills/references used: Unity CLI/Pipeline, craft-game-floors `game-audio.md` and `visual-scorecard.md`. The explicit user-approved distractions supersede the earlier no-extra-audio/scenery convention; no other ambient bed, music or response-dependent effects were introduced.

Scoped desktop visual assessment: palette 4/5 (neutral frames), style 4/5 (architectural windows), spatial depth 4/5 (walls and framing), silhouette 4/5 (existing sphere), UI 4/5 (unchanged task signs), feedback visibility 4/5 (unchanged countdown). The intended photographic footage is a separate visual stimulus, not a palette-matched room decoration.

Historical preview: [Left window](../Evidence/DistractionPlayback-v1.4/left-window.png), [right window](../Evidence/DistractionPlayback-v1.4/right-window.png), [eight-video catalog](../Evidence/MediaReview-v1.4/street-catalog.jpg).

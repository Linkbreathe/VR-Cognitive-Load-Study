# Ten-second video, expanded spatial audio and formal feedback

The v1.7.1 video-frequency update supersedes the three-video schedule below: five videos plus six audio events now occur per default formal block. See `VideoFrequencyReview.md`; other v1.7 behavior and media are retained.

Application `1.7.0-stage1`, Android code 11; presentation `spatial-lab-v1.7-feedback-spatial-audio`; configuration `stage1-v1.7-feedback-spatial-audio`; schedule `distractions-v3-ten-second-video-spatial-pools`. Restore point: `ed2047a`.

## Approved changes

The user explicitly requested ten-second street videos, additional speech based on recent news, a wider environmental sound pool with spatial movement, and the practice green/red feedback in formal trials. This supersedes the earlier no-formal-correctness-feedback protocol. Data from this revision must retain its presentation/configuration identifiers.

Street exposure is ten task-clock seconds from the first decoded frame, excluding pauses. All eight local videos were regenerated from their preserved originals. Files contain 10.25 seconds, including a short decoder tail so clip EOF cannot end the requested exposure early. A source shorter than 10.25 seconds loops during asset preparation; the runtime player does not loop. Video remains silent and uses the existing five front-wall positions.

The local catalog now contains 40 assets:

| Family | Pool | Presentation |
|---|---:|---|
| Street video | 8 | Ten-second front-wall window |
| Door | 8 | Rear-left or rear-right, existing 1–2-second recordings |
| Traffic | 4 | Recorded passing/arriving cars and engine hum, moving along the outside of the front wall |
| Footsteps | 4 | Recorded steps moving from rear-side toward the seated user |
| Conversation | 8 | Existing synthesized two-speaker English exchanges outside the rear wall |
| News | 6 | New English paraphrases of verified October 2026 NASA/JPL and ESA reports, outside the rear wall |
| Soft voice | 2 | Neutral synthesized speech placed approximately 0.41 m to the side/back of the head at onset, reduced gain |

Each default formal block still has nine non-overlapping exposures, separated by three or four trials: three videos, three environmental sounds and three speech clips. The environmental slots include one door, one traffic and one footstep clip; speech slots include one conversation, one news and one near-ear soft voice. Clip IDs and family order are seeded. The two formal blocks share type/onset schedules with mirrored sides and different media picks. No distractors occur in practice, instructions or rest.

`doorClips`, `doorSources` and `doorVolume` are retained serialized/API names for the expanded environmental pool. `conversationClips` and `conversationSources` similarly carry the speech pool. Event kinds now read `Environment` and `Speech`, with asset IDs and `distraction_audio_path` records identifying the exact family and trajectory.

## Spatial playback

All sound files are mono 48 kHz PCM and use Unity 3D AudioSources with zero Doppler. The configured source uses a spatializer if one is installed; this project currently uses native distance attenuation and stereo panning, without an HRTF plugin. Source movement is verified in the Editor; perceived externalization, front/back direction and near-ear quality need a Quest 3 listening check.

Traffic travels between x ±3.8 m at z=3.35 m, outside the front wall. Footsteps start approximately 2.8 m behind and 1.6 m to one side of the listener, at floor level, and end 0.35 m behind and 0.55 m to the side. Footsteps use a 0.4–6 m attenuation range. Near-ear speech is positioned using the listener's horizontal orientation at onset and then remains world-fixed; it does not track subsequent head turns. Its gain is 45% of `conversationVolume`, multiplied by the master distraction gain. It is quiet speech, not a true whisper-synthesis model.

Paths use the paused task clock. Pausing freezes both audio and movement; resume continues. Cancel, restart, abort and block completion stop active sources. Path start/end coordinates, profile and spatializer name are recorded. Software playback times are not measured acoustic onsets.

## Feedback

`formalFeedbackEnabled=true` is saved with the frozen session configuration. Accepted scored responses turn the countdown green with `Correct` or red with `Incorrect`; omissions show red `No response`. The color resets at the next stimulus. Warmups do not accept responses or show correctness feedback. Neither early answers nor feedback shorten the four-second sphere visibility/response window, change the 4.5-second onset interval, allow retries or play a response sound. The two formal difficulties use the same feedback behavior.

## News provenance and preparation

Sources were checked on 2026-11-01. News speech is an original short paraphrase, synthesized locally with Microsoft David Desktop and Microsoft Zira Desktop. These are not broadcast recordings or impersonations. The catalog is frozen, not a live news feed. Full scripts, dates, URLs, voices, durations and hashes are in `Tools/news-speech-2026-11-01.json` and the runtime media manifest.

1. [Ancient Martian meteorite, October 29](https://www.jpl.nasa.gov/news/nasa-funded-study-details-one-of-oldest-mars-meteorites-ever-found/).
2. [Voyager power and communications, October 29](https://www.jpl.nasa.gov/news/nasas-voyager-1-conserves-power-resolves-brief-communications-pause/).
3. [Mars dust and spacesuit samples, October 26](https://www.jpl.nasa.gov/news/nasas-perseverance-offers-clues-to-protect-crews-from-mars-dust/).
4. [Psyche's electric propulsion, October 22](https://www.jpl.nasa.gov/news/pedal-to-the-metal-nasas-psyche-heads-toward-asteroid-belt/).
5. [Thwaites Glacier projections, October 20](https://www.jpl.nasa.gov/news/nasa-study-refines-projections-for-thwaites-glacier/).
6. [Hubble/Webb Cat's Head Nebula, October 29](https://esahubble.org/news/heic2614/).

New environmental recordings come from the [Mixkit car](https://mixkit.co/free-sound-effects/car/) and [footsteps](https://mixkit.co/free-sound-effects/footsteps/) catalogs under the Sound Effects Free License. Exact asset URLs, original files and page snapshots are preserved locally. Prepared clips are limited to eight seconds, normalized and gently faded. Existing source-license restrictions still apply; do not redistribute recordings as a standalone stock library.

To rebuild the v1.7 catalog after the original base-media preparation, run `python Tools/extend_distraction_media.py`, then `powershell -NoProfile -File Tools/create_news_speech.ps1`. The second command merges speech provenance into the runtime manifest. Run `LabVisualUpgrade.Apply()` in the stopped Editor to import mono audio and wire the catalogs.

## Verification

The scheduling validation covers 1,000 seeds, balanced families, reproducibility, ten-second video duration, 3/4-trial intervals, no overlaps and media bounds. The audio probe exercises all six sound profiles on both sides, including actual source playback, moving positions, approach direction, near-ear distance, gain, pause/frozen motion, resume and abort. The UI probe checks formal green/red feedback while retaining full response timing. Live block and Android package results are recorded in `AcceptanceRecord.md`. All synthetic and Editor outputs remain separate from participant records.


Final results: 70,013 scheduling checks, 12 live audio profile/side cases, 88 seated/feedback checks including both formal difficulties and omissions, 194,113 core assertions and independent verification of 400 exported rows passed. The 138.44-second live block completed nine events without skips or decoder errors; three video exposures measured 10.013–10.025 seconds. The Android build succeeded in 110.4 seconds with zero errors and one crash-debug-symbol warning. The 88,634,246-byte ARM64 APK passed v2 signature verification and all eight packaged video hashes match the catalog. Final Editor state: stopped, saved, zero console errors. Physical headset listening remains unverified.

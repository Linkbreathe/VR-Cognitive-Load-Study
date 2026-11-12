# Randomized HD film scene pool

2026-11-12, Stage 2/3. Catalog `film-scenes-hd-v1-20261112`; allocation and presentation `participant-film-pool-v1`.

## Requested change

Expand real movie scenes, exclude trailers and advertising, assemble multiple videos, and randomly use different videos across Fixed Exposure, Adaptive Without Evidence and Adaptive With Evidence. Preserve the 34-position protocol, four-second responses, 4.5-second cycles, screen geometry, controller decisions and resume behavior.

## Content and quality

The library contains **90 nonoverlapping 20-second excerpts**, assembled into **six disjoint five-minute videos**: `film-mix-v1-01.mp4` through `film-mix-v1-06.mp4`. There are 30 minutes of unique excerpt content. Each mix has 15 excerpts and includes all nine film families, with one or two excerpts from each. An individual stage shows only the portion permitted by its existing video-ON rules; five minutes is capacity, not a changed exposure budget.

| Film or series | Downloaded scene sources | Selected excerpts |
| --- | ---: | ---: |
| Interstellar | 2 | 10 |
| Leon: The Professional | 2 | 10 |
| Inception | 2 | 10 |
| The Truman Show | 2 | 10 |
| The Godfather | 2 | 10 |
| The Avengers / Age of Ultron | 2 | 10 |
| Star Wars: The Empire Strikes Back / The Force Awakens | 2 | 10 |
| How to Train Your Dragon (2010) | 1 | 10 |
| Raiders of the Lost Ark | 2 | 10 |

Sources are public scene uploads from Paramount Movies, Movieclips, Marvel Entertainment, Binge Society and JoBlo channels. The versioned [source recipe](../Tools/AdaptiveTraining/media-sources.film-pool.json) contains all URLs, in-points and exclusions. Downloaded source metadata, channel identities, native dimensions and SHA-256 values are in `Builds/FilmPool20261112/source-provenance.json`. These records identify the source; they do not assert a redistribution license. Binary files remain local and excluded from Git.

Selection excludes trailer/teaser/TV-spot/featurette formats, studio-logo introductions, end-card promotions and three otherwise official sources with persistent Disney+ streaming advertisements. Unavailable Dragon uploads were replaced by an available scene source. Source ranges 32–52 and 52–72 seconds of the asteroid chase were replaced after near-black-frame review. Natural film lighting, original soundtrack, small source channel marks and existing English subtitles remain. There is no added music, narration or decorative transition.

Native sources must be at least 1280 x 720. Output preserves aspect ratio within 1280 x 720, with H.264 baseline, 30 fps and 48 kHz stereo AAC. The continuous videos copy the normalized video bitstream without another lossy video encode; audio timestamps are rebuilt across boundaries. Clips receive measured loudness adjustment toward -23 LUFS, constrained by peak headroom. The unchanged Unity volume setting still applies. `volumeReviewed` remains false pending a worn-headset review.

## Randomization and history

Only independent comparisons use the new pool. The 90 new items have a separate `continuous20` split; the original 92 `training20` items, 22 practice/transfer items and both original 300-second streams are preserved. Historical paired 60-trial playback retains its first-two-playlist seed-parity rule, and 144-trial scheduling retains its original source pool.

On the same application installation, selection shares a persistent history across the three stages for the same participant ID, storage root and session type. It chooses randomly, using the session seed, among the least-reserved videos. Six distinct allocations therefore precede reuse. At cycle boundaries, recent choices are excluded when alternatives exist, preserving different videos in successive three-stage runs. A repeated load of the same session/stage returns its original allocation. Reservations survive application restarts; aborted preparations conservatively consume an allocation even if no frame was shown. Reservation counts are not exposure counts.

History is under `<persistentDataPath>/ContinuousMediaHistory/`, keyed by a SHA-256 of the scope. Do not delete it between stages. Different installations, participant IDs or storage roots have separate histories. The six videos have disjoint excerpt content, but films and characters recur; scene familiarity is not eliminated.

The assigned ID, playlist hash, catalog revision, selection revision, seed and previous reservation count are recorded in `continuous_media_assignment`. `continuous_media_playlist` includes the full selected source sequence in the core event archive, including source URLs, in-points and excerpt hashes. The Unity session directory additionally retains `continuous-media-selection.json`, the bank manifest and the full media catalog. `session.json` records the new presentation revision. Actual decoded starts, source boundaries, OFF/end/interruption and resumed cursors remain distinct events.

History corruption, inconsistent reserved content or a missing pool prevents readiness rather than silently substituting media. No new participant/model qualification or controller rule is introduced.

## Build, installation and evidence

Use the project Python environment to run `Tools/AdaptiveTraining/prepare_film_pool.py` with the checked recipe, downloaded source directory, original catalog, original two-stream bank and an output directory. `--resume` reuses only artifacts whose source recipe and hashes match. `Tools/AdaptiveTraining/verify_film_pool.py` verifies all media, provenance, disjoint allocation, native output format, decoded frames, duration, audio and black-frame flags.

Output: `Builds/FilmPool20261112/`; open `preview.html` there to preview all six mixes. Final source and assembled media hashes are in the catalogs in that folder. Preparation and validation evidence: `Evidence/FilmLibrary-20261112/`. Source restore point: `Evidence/FilmLibrary-20261112/source-before.zip`; prior installed catalogs: `before-layer-ab-144-media.json` and `before-continuous-comparison-media.json` in that evidence folder. New binaries use unique filenames; original media bytes are retained.

Passed: Unity compilation; 18,754 allocation checks in Edit Mode and again in isolated Play Mode; 100,004 existing practice/media scheduling checks; full decoding of all 96 generated MP4 files; exact 600/9,000-frame counts, 20/300-second duration and aligned zero-start audio/video; source-window and playlist disjointness; unchanged original 114 catalog entries and both legacy stream manifests. Fifteen accepted sources are native 1080p and two are native 720p. Final measured integrated loudness spans -26.1 to -23.0 LUFS (peak-constrained adjustment); maximum decoded true peak is -2.6 dBFS. No interval meets the specified near-black flag after the two replacements. These technical checks do not imply identical perceived distraction or loudness.

Installed 186 new binary files (1,389,374,002 bytes) and the two catalogs into this PC's application persistent-data folder, with hash equality checked after copying. Updated the Resource catalog. All six actual Unity decoder fixtures passed distinct allocation, first decoded frame/audio, an encoded source boundary, OFF hide/pause, retained cursor, ON resume, completion stop and source-recipe event logging. Full real-time Fixed playback passed all 34 positions: no sampled OFF leaks or ON gaps, first decoded frame 42.0 ms after the position-19 request, and playback stopped at completion. The original scene was restored and Play Mode stopped. Pre-existing auto-tick remains enabled. Current Unity console errors are zero; the log buffer also contains an older unrelated Unity AI relay error from before this work. All testing is synthetic/engineering and separate from participant data.

Pipeline documentation used: the installed package's `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md` and `commands/runtime.md`. No backend update or restart is needed for this Unity/media-only change.

Pending physical checks: worn Quest Pro decoding/performance, perceived loudness, comfort and actual audiovisual timing. No new standalone/Quest build is produced by this media update. Different film content is not perceptually equivalent; randomized content does not by itself establish a causal comparison between stages.

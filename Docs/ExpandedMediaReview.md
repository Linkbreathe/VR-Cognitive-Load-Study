# Expanded paired film library

Date: 2026-11-02. Application/configuration: `2.0.0-stage2-dev4`, Android code 16. Manifest: `stage2-paired-media-v2-expanded`.

The user requested a remote project update and more content of the same type. The dev3 implementation was committed and pushed to `origin/main` as `53242df`; this revision expands the usable library and is delivered in a subsequent commit. Raw recordings, downloaded films, generated media/builds and engineering evidence remain outside Git under the existing ignore rules. Source recipes, manifests, software, tests and English documentation are versioned.

## Library and use

Twelve additional pairs (129 seconds of unique exposure content) bring the library to 22 pairs / 44 files / 237 seconds. Each pair contains a 10–12-second H.264 720p/30 fps video and the identical decoded mono WAV soundtrack. There are now six Titanic, six Little Women, five LOTR and five Harry Potter excerpts. Existing ten entries and their media bytes are preserved, including the four held-out clips.

The twelve additions come from previously downloaded, provenance-recorded uploads, using nonoverlapping source intervals. They are new excerpts, not twelve new films or wholly unfamiliar narratives. Shared scenes/characters and original incidental score remain. A candidate with a decorative Magical Movie Mode overlay was rejected on visual review and replaced with a clean Hogwarts arrival scene. New encodes use H.264 baseline without B frames; original ten encodes are retained unchanged.

Training now has 18 clips, partitioned reproducibly into nine audio-only and nine audiovisual clips per counterbalance group. Practice selects three distinct AV-assigned clips using the session seed. After practice, six other AV clips remain unseen; least-exposed-first selection uses them before repeating the practice content. There are nine initially unseen audio-only clips. Existing IDs retain their modality assignment because added IDs sort after the original training set. Four reserved pre/post clips remain separate.

Practice still has three exposures around 10/28/46 seconds, and formal training still has three per 108-second evaluation. No extra exposure frequency, volume, response sound, UI, sphere or timing change was made. The centered wall display and compact status are unchanged. More content reduces immediate repetition; it does not establish equal difficulty/familiarity or training efficacy.

## Implementation and reproducibility

- Runtime manifest validation supports an even training pool of at least six pairs, with exactly two pairs in each held-out split; duplicate IDs and malformed allocation are rejected. Historical ten-pair libraries remain supported.
- Preparation supports explicit append manifests, verifies every existing pair hash before processing additions, rejects duplicate IDs and refuses to overwrite existing pair files. The audit validates layout, unique IDs/hashes, duration/codec, exact paired PCM, peaks and runtime loudness.
- The effective dev4 config and selected manifest are frozen in every session. Legacy dev1/dev3 overrides migrate while retaining mode/seed/endpoint. Historical policy decisions remain replayable; physiology calibration must match the incoming config version, so an older calibration cannot authorize dev4 promotion.
- `Tools/AdaptiveTraining/media-sources.expansion.json` holds the added source intervals, source links, categories, rights notes and baseline-profile setting. The original source recipe now uses project-relative paths for portability. Downloaded sources are needed locally to reproduce media; they are not embedded in the repository.
- Current external release: `Builds/Stage2MediaRelease/` and `Builds/Stage2MediaRelease.zip`. The Editor persistent library is updated, with Stage 2 enabled and the user's OFF setting retained. Enter a new Play session to use the new random pool. Same seed/group remains reproducible; changing the seed changes the draw.

## Verification and limits

All 22 pairs pass codec/hash/paired-PCM/duration/peak/loudness checks; gains match -24 LUFS within 0.15 LU. All same-source intervals are nonoverlapping, old entries are identical, and a frame from each addition was visually inspected. Automated speech timestamps and available captions guided complete sentence boundaries; this is not signed human semantic review. `volumeReviewed=false` remains in every item, blocking participant readiness.

Model checks: 100,004 expanded-library assertions across 1,000 seeds and four groups; 51,068 adaptive regression assertions across 1,000 seeds/30 complete synthetic sessions; 31 Python controller/feature tests. Evidence is under `Evidence/MediaExpansion/`. Thirty live Editor checks passed: all 22 pairs loaded, all twelve additions decoded offscreen, and three full practice exposures completed through the normal wall/audio system (pair_22, pair_20, pair_02). Offscreen decoding was silent and is not an audio/listening check. The remote command transport timed out at 60 seconds, but the probe completed in the Editor; its saved result, runtime state and CSV logs were checked independently. Android build passed in 93.8 seconds (zero errors, one diagnostics-symbol warning); ARM64, code 16 and APK v2 signature verified. APK size: 88,757,512 bytes. ZIP integrity/44-file contents and actual saved dev3 config migration were checked. Detailed build/signature/hash evidence is in the acceptance record and `Evidence/MediaExpansion/`.

Headset listening, content/emotional matching, wall-screen comfort/performance and real physiological calibration remain pending. The new Titanic dialogue uses a tense ship-railing scene; content suitability and familiarity must be reviewed before participant deployment. Quest 3 is the available test headset. Quest Pro support is not physical verification.

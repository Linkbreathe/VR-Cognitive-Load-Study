# Heidi media removal

2026-11-07, Stage 2/3 media content revision `layer-ab-144-dialogue-v2-no-heidi`.

The researcher rejected Heidi footage because of its visual quality. Removed eight 20-second clips (`ab20-006`, `012`, `016`, `044`, `048`, `086`, `092`, `098`) from the Resource, build and installed PC catalogs. Deleted their video/audio files in both media installations and the three original source MP4s: 35 files, 103,579,693 bytes. Source metadata and archived experiment records remain historical provenance; they cannot supply playable excluded media.

The current catalog contains 92 comparison clips and the original 22 practice/test items. All retained item fields and source hashes are unchanged. The generator no longer includes Heidi. Catalogs declare `trainingClipCount`; runtime checks the declared count, with a 100-clip fallback for legacy catalogs. File, hash, layout and participant volume-review gates remain in effect.

Both continuous streams were re-encoded and deployed to the PC persistent-data folder. Only three source slots changed:

| Playlist | Source time | Removed | Replacement |
| --- | ---: | --- | --- |
| continuous-comparison-0 | 100 s | ab20-006 | ab20-031 |
| continuous-comparison-0 | 220 s | ab20-012 | ab20-032 |
| continuous-comparison-1 | 0 s | ab20-016 | ab20-033 |

The other 27 slots retain their order and source hashes. Each stream remains 300 seconds with 15 contiguous 20-second segments. The transport format remains `continuous-clips-resume-v1`; `catalogVersion` and new playlist SHA-256 values identify the changed content in the bank manifest copied into future session records. `prepare_continuous_media.py --previous-bank <manifest>` preserves retained slots and fills removed slots deterministically from unused available clips.

## Verification

- [Removal inventory](../Evidence/MediaHeidiRemoval-20261107/removal-inventory.json): exact file paths, original hashes and removed catalog entries; every selected binary is absent.
- [Media verification](../Evidence/MediaHeidiRemoval-20261107/verification.json): identical deployed catalogs, all 92 retained clip/audio hashes in both installations, unchanged retained item metadata, 27 unchanged slots, 30 unique allowed sources, deployed playlist hashes and two 300-second audiovisual streams. Both streams decoded fully with FFmpeg without errors.
- [Unity compilation](../Evidence/MediaHeidiRemoval-20261107/unity-compilation.json) and [catalog checks](../Evidence/MediaHeidiRemoval-20261107/unity-catalog-verification.json): current/legacy count compatibility, rejection of an incomplete catalog, and Resource import.

The completed Editor run was stopped to release cached media. No new study session was assigned or started. Thresholds, EEG/model settings, A/B controller rules and task timing were not changed. Existing records were not rewritten. This revision is software verified on this PC; headset playback/loudness and physical acceptance remain pending. Existing standalone/Quest builds were not rebuilt or deployed.

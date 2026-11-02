# Stage 2 film-pair catalog and review

Current dev4 practice selects three of nine AV-assigned training clips before baseline. Least-exposed-first scheduling uses six other AV clips before repeating those practice clips; audio-assigned and reserved pre/post clips remain unused in practice. See [practice protocol and familiarity limits](WallPracticeReview.md). The original ten pairs are retained unchanged; twelve added pairs are listed below.

Prepared: 2026-11-02. Revision: `stage2-paired-media-v2-expanded`. Status: **22 playable candidate pairs; human semantic matching and headset listening not yet accepted**.

The user requested online acquisition. Sources are publicly available studio promotional clips and Movieclips uploads. Source download metadata and originals are retained locally under `MediaSources/Stage2/`; no source recording was taken from the P007 physiological dataset. The current deployment package is `Builds/Stage2MediaRelease/`. Media are local deliverables, excluded from Git with other build/source artifacts. See [expansion implementation and evidence](ExpandedMediaReview.md).

Each pair contains a video and WAV decoded from the identical mono AAC soundtrack. Source start/end below refer to the downloaded upload, not the feature film running time. Short completed dialogue units are padded with the final frame and silence to preserve a 10–12 second exposure. Padding is not extra dialogue or stimulus extension. No additional music, response-dependent audio or visual effects are introduced. Original incidental score, effects, letterboxing and source watermarks remain.

The build/import audit verifies H.264, 1280 × 720, 30 fps, mono 48 kHz audio, durations, SHA-256 and byte-identical paired PCM. A two-pass -23 LUFS processing target and limiter are followed by **recorded per-pair runtime attenuation** to match the decoded clips to -24 LUFS (within 0.15 LU; no gain boosts). Audio and AV apply the same gain. The global default volume is 0.25; this is not a measured headset sound-pressure level. All `volumeReviewed` flags remain false.

## Candidate cuts

| Pair | Work | Source interval (s) | Exposure / end padding (s) | Split | Provisional content / emotion | Source |
|---|---|---|---|---|---|---|
| pair_01 | Titanic | 0.00–8.60 | 10 / 1.40 | train | social introduction / neutral | [20th Century Studios UK](https://www.youtube.com/watch?v=oklPl95DC8c) |
| pair_02 | Titanic | 11.13–21.60 | 11 / 0.53 | train | travel and chance / neutral | [20th Century Studios UK](https://www.youtube.com/watch?v=oklPl95DC8c) |
| pair_03 | Titanic | 28.48–37.78 | 10 / 0.70 | test_a | lifestyle disagreement / mild tension | [20th Century Studios UK](https://www.youtube.com/watch?v=oklPl95DC8c) |
| pair_04 | Little Women (2019) | 68.95–78.60 | 10.5 / 0.85 | train | relationship boundary / sadness | [Movieclips](https://www.youtube.com/watch?v=3cEg2HCl50I) |
| pair_05 | Little Women (2019) | 19.65–28.00 | 10 / 1.65 | train | artistic ambition / reflective | [Movieclips](https://www.youtube.com/watch?v=0D8nRpJsQlk) |
| pair_06 | Little Women (2019) | 112.35–123.00 | 11.5 / 0.85 | test_b | relationship boundary / frustration | [Movieclips](https://www.youtube.com/watch?v=GRiC-B6Of9I) |
| pair_07 | The Lord of the Rings: The Fellowship of the Ring | 27.17–36.22 | 10 / 0.95 | train | difficult journey / concern | [Movieclips](https://www.youtube.com/watch?v=Bypor4e04rg) |
| pair_08 | The Lord of the Rings: The Fellowship of the Ring | 190.90–201.40 | 11 / 0.50 | test_a | friendship and support / warmth | [Movieclips](https://www.youtube.com/watch?v=Bypor4e04rg) |
| pair_09 | Harry Potter and the Philosopher's Stone | 7.40–19.40 | 12 / 0.00 | train | classroom disagreement / mild tension | [Harry Potter](https://www.youtube.com/watch?v=49o5QSVRtfg) |
| pair_10 | Harry Potter and the Philosopher's Stone | 64.50–76.50 | 12 / 0.00 | test_b | classmate conversation / confidence | [Warner Bros. Entertainment](https://www.youtube.com/watch?v=phdwdrPs3Bo) |
| pair_11 | Titanic | 88.65–99.70 | 11.5 / 0.45 | train | childhood recollection / reflective | [Source upload](https://www.youtube.com/watch?v=zSRvmHSgaBg) |
| pair_12 | Titanic | 100.65–111.80 | 11.5 / 0.35 | train | ice-fishing conversation / light teasing | [Source upload](https://www.youtube.com/watch?v=zSRvmHSgaBg) |
| pair_13 | Titanic | 151.70–160.20 | 10 / 1.50 | train | request for reassurance / concern | [Source upload](https://www.youtube.com/watch?v=zSRvmHSgaBg) |
| pair_14 | Little Women (2019) | 49.35–60.40 | 11.5 / 0.45 | train | artistic self-assessment / reflective | [Source upload](https://www.youtube.com/watch?v=0D8nRpJsQlk) |
| pair_15 | Little Women (2019) | 118.45–129.60 | 11.5 / 0.35 | train | choice and affection / thoughtful disagreement | [Source upload](https://www.youtube.com/watch?v=0D8nRpJsQlk) |
| pair_16 | Little Women (2019) | 60.35–70.20 | 10 / 0.15 | train | travel and business plans / neutral | [Source upload](https://www.youtube.com/watch?v=GRiC-B6Of9I) |
| pair_17 | The Lord of the Rings: The Fellowship of the Ring | 0.00–8.30 | 10 / 1.70 | train | explaining a task / serious | [Source upload](https://www.youtube.com/watch?v=Bypor4e04rg) |
| pair_18 | The Lord of the Rings: The Fellowship of the Ring | 139.05–150.20 | 11.5 / 0.35 | train | offering support / reassuring | [Source upload](https://www.youtube.com/watch?v=Bypor4e04rg) |
| pair_19 | The Lord of the Rings: The Fellowship of the Ring | 160.05–170.05 | 10 / 0.00 | train | commitment to cooperation / resolute | [Source upload](https://www.youtube.com/watch?v=Bypor4e04rg) |
| pair_20 | Harry Potter and the Philosopher's Stone | 35.95–45.00 | 10 / 0.95 | train | classroom introduction / confident | [Source upload](https://www.youtube.com/watch?v=hMIN4wmHuiA) |
| pair_21 | Harry Potter and the Philosopher's Stone | 88.90–98.00 | 10 / 0.90 | train | classroom question and answer / mild tension | [Source upload](https://www.youtube.com/watch?v=hMIN4wmHuiA) |
| pair_22 | Harry Potter and the Philosopher's Stone | 231.10–242.60 | 11.5 / 0.00 | train | welcoming new students / instructive | [Source upload](https://www.youtube.com/watch?v=phdwdrPs3Bo) |

## Review evidence and limitations

- All 22 pairs passed the current automated audit; see `Evidence/MediaExpansion/pair-audit.json`. The original ten-pair evidence remains in `Evidence/Stage2/film-pair-audit.json`. Final pair gains and source/output hashes are in the deployment and Resources manifests. A later human volume review must produce a new manifest revision.
- Candidate boundaries were checked against available English timed captions and local automated English speech recognition. The fifth cut was revised after the first speech check exposed a truncated sentence. These are automated selection aids, not signed human semantic approval. No verbatim film transcript is included in the project documentation.
- Frames from the original ten and the twelve additions were visually inspected in `Evidence/Stage2/film-contact-sheet.png` and `Evidence/MediaExpansion/contact-sheet.png`; the candidates show dialogue scenes rather than synthetic stimulus patterns. Final real-film Unity rendering is recorded in `Evidence/Stage2/film-runtime.png` and `film-playback-probe.txt`.
- Three Titanic excerpts share one scene, and the two LOTR excerpts share a council scene. Reserved clips do not duplicate the training intervals, but share characters/context. Record film familiarity and pilot the effect; do not describe this as transfer to wholly unfamiliar stories.
- Eighteen training clips yield nine per modality for each participant. Practice exposes three AV clips; the six other AV clips remain available before repeat exposures. Counterbalance groups exchange assignment, and repeat counts are logged. Four held-out clips support one distinct exposure per pre/post modality block. Exposure dose therefore differs from training; this is an explicit protocol limitation.
- Category/emotion labels are provisional curator annotations. They do not establish emotional equivalence, intelligibility, cognitive difficulty, or that AV is harder than Audio. Listen to every pair on the actual headset and collect the fixed-condition pilot results before participant acceptance.
- Public availability/source provenance is not an assertion of a redistribution license. The source/usage record is retained per item for the study operator. The local media package is not published or pushed to a remote service.

See [calibration worksheet](Stage2CalibrationReport.md), [operator guide](Stage2Guide.md) and [implementation record](Stage2Implementation.md).

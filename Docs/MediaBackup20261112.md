# Film pool backup and source publication

2026-11-12, Stage 2/3; catalog `film-scenes-hd-v1-20261112`.

## Download and restore

[Google Drive media folder](https://drive.google.com/drive/folders/1Cus3zTk8vBEBp5mwbdDrq-ciHlNoB5CW).

The distribution contains six standalone `film-mix-v1-01.mp4` through
`film-mix-v1-06.mp4` videos and ten independently extractable companion ZIPs.
The companion ZIPs contain 90 twenty-second excerpts, 90 WAVs, the two complete
catalogs, source provenance, thumbnails, preview HTML and a SHA-256 manifest.

1. Download all ten companion ZIPs and extract them into the same empty folder.
2. Download all six standalone mixes and place them in its `AdaptiveMedia` folder.
3. Verify each file against `upload-manifest.json`, and each downloaded ZIP
   against `companion-checksums.json`. Open `preview.html` to inspect the mixes.
4. Stop the application and back up its existing media catalogs. Copy the
   `AdaptiveMedia` folder, `layer-ab-144-media.json` and
   `continuous-comparison-media.json` into `Application.persistentDataPath`.
   On this PC this is
   `%USERPROFILE%/AppData/LocalLow/EvidenceVR/Evidence-Aware VR Task Platform/`.
   Preserve existing media, configuration, allocation history and recordings.

This is the new film-pool backup, not the entire historical media installation.
The unchanged original 114 catalog items, two legacy continuous streams and
`Assets/Resources/CalibrationForest.mp4` remain separate prerequisites. Original
downloaded scene sources are not included. The checked-in source recipe and
source provenance retain the source URLs, cut points and hashes.

## Publication and validation boundaries

The project checkpoint uses `codex/layer-ab-timing-study` in
[VR-Cognitive-Load-Study](https://github.com/Linkbreathe/VR-Cognitive-Load-Study).
Source, project configuration, authored assets, generators, validation scripts
and review documents are included under the existing Git policy. Local recordings,
builds, downloaded source media, evidence folders, caches and new Unity automatic
recovery scenes remain local. Existing tracked recovery history is retained.

Publication checks cover Python/JavaScript syntax, staged whitespace, all 186 new
media hashes against the catalogs, and ZIP CRC integrity. Drive readback checks
file identity, destination and byte size; the connector does not expose remote
content checksums. SHA-256 manifests support independent download verification.
This maintenance does not rerun behavioral regression or Unity/device acceptance.
See [FilmPoolReview](FilmPoolReview.md), [Comparison34Review](Comparison34Review.md),
[RingOnlyGazeReview](RingOnlyGazeReview.md) and [AcceptanceRecord](AcceptanceRecord.md)
for existing software evidence and pending physical checks.

# Stage 3 dev1 release record

Historical artifact record. The later dev2/code-26 candidate adds the user-approved short calibration and right-side progress, without changing the guard core. See the [current update/release review](ShortCalibrationProgressReview.md); the hashes and validation below describe the original dev1 files.

Date: 2026-11-03. Engineering candidate only. APK application `3.0.0-stage3-dev1`, Android code 25, package `com.evidencevr.taskplatform`, ARM64; guard `evidence-guard-v3-minimal-1`; Python package `evidence-guard==0.1.0`. Retained host config/HTTP version `2.0.0-stage2-dev11`, protocol `personalized-rf-v2`, policy `personalized-rf-pilot-v3-control70-40`, environment `warm-study-v1`.

## Local deliverables

- `Builds/Stage3/EvidenceVR-Stage3-dev1.apk`: 100,264,726 bytes; SHA-256 `4463fda39e58ca4ddda99a8f1f46b7f88813d9f9bb6e0a719898baaac0d9405e`.
- `Builds/Stage3/evidence_guard-0.1.0-py3-none-any.whl`: 12,149 bytes; SHA-256 `f890a19492b22662a618e51d0d71b93b42925d794c11663f09412a9361848309`.
- `Builds/Stage3/EvidenceGuard-Stage3-dev1-delivery.zip`: APK, wheel, package source/tests, Python host service, engineering mode examples, guides and synthetic engineering evidence. `release-manifest.json` lists per-file SHA-256 values. Existing AV media is deliberately reused and is not included. No participant recording or local virtual environment is packaged.
- `Builds/Stage3/adaptive-training-config-{off,observe,enforce}.json`: complete engineering examples; choose one and deploy under the standard configuration filename. Defaults in source remain off; no existing session/override was enabled or replaced.

## Verification evidence

Android IL2CPP build succeeded in 249.882 seconds with **zero build errors** and 347 warnings: 345 Sentis shader warnings, the existing Quest Link member-hiding warning, and a diagnostic-symbol warning. APK v2 signature verified with the bundled Android SDK; manifest confirms version/code, ARM64 and eye-tracking permission. BuildReport `totalSizeBytes` includes intermediate files and is not the APK size. Full report: `Evidence/Stage3/build-report.json`; signature and badging results are alongside it.

Final Python run: 121 host/Stage 2 tests passed in 48.892 s, including ten guard integration tests; 14 independent core tests passed in 0.464 s. Total: **135 Python tests**. Tests ran while the Android build consumed resources, so elapsed suite time is not guard latency. Actual uninstall was followed by all 17 retained Stage 2 control-rule tests passing; then the optional package was reinstalled.

Unity compiled and completed 106 isolated Play Mode assertions, including duration transport and metadata. PASS log was read back. One previous-domain GC-handle assertion and Unity AI subscription messages are retained in the Editor evidence, as described in the [review](EvidenceGuardReview.md); zero build errors does not mean a clean Editor console. A console MCP call timed out, after which CLI inspection succeeded. CLI `1.0.0-beta.15` was current according to its update check. Local venv packaging tools were updated to setuptools 84.2.1/wheel 0.48.0 to build/install the standalone distribution; signal/model dependency pins were not changed.

The three scripted demonstration families retain all five valid action candidates in enforce, defer the one unsupported recovery upgrade, and identify both explicit attribution conflicts. Off/observe preserve the original base action sequence. These are small programmed examples, not population rates. `Evidence/Stage3/Demos/summary.json` includes each denominator, timing and all three mode results. Review latency excludes network/device latency; full-duration device performance remains untested.

ADB returned no connected devices. The APK was neither installed on a headset nor physically tested. Quest Pro acceptance and genuine viewing/physiological validation remain pending. Package construction does not authorize participant deployment.

## Rebuild and restore

Read the [operating guide](EvidenceGuardGuide.md). `AgentScripts/EvidenceGuardBuild.cs` prepares version/code and engineering examples. Build the existing NBackExperiment scene to the Stage 3 output path using Android, then call `EvidenceGuardBuild.Restore` to restore the project's prior PlayerSettings. This was done after this build: source project version/code is again dev12/24; the delivered APK remains Stage 3 dev1/25. Play Mode is stopped and previously enabled auto-tick is retained.

`AgentScripts/package_stage3.py` rebuilds the local delivery archive from the named outputs and writes a checksum manifest. `Evidence/` and `Builds/` remain local ignored artifacts; source/docs are in the working tree. No Git commit, push, external upload or participant session was performed for this task.

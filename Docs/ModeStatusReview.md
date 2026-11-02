# Experiment mode and source status display

Historical review: current dev3 presentation, practice exposure, configuration and compact status behavior supersede the corresponding details below. See [Wall and practice revision](WallPracticeReview.md).

Date: 2026-11-02. Application: `2.0.0-stage2-dev2` (Android code 14). Display revision: `mode-status-v1`. Training configuration/controller protocol remains `2.0.0-stage2-dev1`.

## Reason and behavior

The user could not distinguish the preserved Stage 1 task from Stage 2, or determine whether automatic control and recorded physiology were active. The old `STAGE 1/4` wording described session steps and was easy to confuse with the project stage.

A persistent three-line status display now appears in the preparation/instruction/pause/end panel and above the running task HUD. The panel also names the current phase; the running HUD retains the practice/training phase, task rule and distraction level. The display reads the running engine/configuration and bridge replies rather than guessing from filenames or the participant code.

| Display | Meaning |
|---|---|
| `STAGE 1 - FIXED TASK` | Preserved fixed-block protocol; physiology is not used in this mode |
| `STAGE 2 - ADAPTIVE TRAINING` | Adaptive training protocol is actually loaded |
| `OFF - Auto OFF (manual)` | No automatic controller actions; fixed protocol transitions still occur |
| `SHADOW - Auto OFF (advice only)` | Recommendations are recorded without execution |
| `ACTIVE - Auto ON` | Automatic decisions are enabled and apply only at eligible boundaries |
| `Offline / HOLD` | ACTIVE is configured, but no bridge response has arrived within three seconds; an enabled setting is not evidence of an available controller |
| `Basis: Behavior` | Explicit behavioral fallback; displayed physiology does not authorize promotions |
| `Basis: Behavior + physiology` | Existing calibrated physiological eligibility gate is configured |
| `RECORDED REPLAY` / `SIMULATED DATA` | Recorded or synthetic source provenance reported by the service |
| `LIVE STREAM (not validated)` | A live-source stream is reported; this is not calibration or physiological validation |
| `MIXED SOURCES - includes simulation` | Sources include replay/simulation together with another source type |
| `Waiting for samples` / `Receiving` / `Partial / signal missing` / `No current samples` | Source transport state; samples must be recent to count as receiving |
| `SERVICE OFFLINE` | No recent bridge reply; a known prior replay source is explicitly labeled `Last source` |
| `UNKNOWN - source status unavailable` | An older bridge returned no source-status metadata; no live-source assumption is made |
| `NO SOURCE CONFIGURED` | Service is reachable but has no configured receiver status |

The source status is sent with normal telemetry before a complete feature window exists. `Receiving` only describes sample arrival; it does not claim good signal quality, valid features, current-task causation, or eligibility for physiological control. A three-second freshness limit is applied to both sample arrival and bridge responses.

The display is static text with no animation or new sound. Header spacing was adjusted to keep the mode/control/source lines readable. Task timing, sphere geometry, scoring, stimulus schedules and controller thresholds are unchanged. Step numbering in the preserved protocol uses `STEP`, avoiding collision with project-stage labels.

## Logging and compatibility

Every new session includes `statusDisplayVersion: mode-status-v1`. Changes in the displayed control/source status produce `mode_status_display` events and concise Unity console entries. Existing presentation versions and protocol versions remain available to interpret earlier sessions. The bridge adds optional `physiologyStatus` response metadata; older clients can ignore it, and the new client shows UNKNOWN when an older bridge omits it.

The new Android candidate is `Builds/Quest/EvidenceVR-Stage2-dev2.apk`; the external film/config package is unchanged. The user-created Stage 2/OFF configuration was restored byte-for-byte after testing. No background replay/service was left running.

## Validation evidence

- Unity compiled without errors. `AgentScripts/StatusViewProbe.cs` exercised actual Stage 1 and Stage 2 preparation, running practice and pause screens, verified text bounds and the new metadata field.
- Actual P007 LSL replay and the local HTTP bridge showed waiting, then `RECORDED REPLAY | Receiving`. After the service exited, the same session showed `SERVICE OFFLINE | Last source: RECORDED REPLAY` and ACTIVE `Offline / HOLD`.
- Six additional label/layout checks cover SHADOW, ACTIVE offline, missing old-server metadata, live-but-unvalidated sources, mixed sources, and the longest text combination. These label inputs are synthetic, not physical sensor verification.
- Twenty-nine Python tests pass, including new tests for source freshness/partial dropout/mixed provenance and source status before feature readiness. Existing controller tests continue to pass.
- Screenshots and reports: `Evidence/Stage2Status/stage1-preparation.png`, `stage1-practice.png`, `stage2-off-preparation.png`, `stage2-off-practice.png`, `active-replay-preparation.png`, `active-replay-receiving.png`, `replay-connected.txt`, `replay-disconnected.txt`, `label-checks.txt` and `python-tests-final.txt`.
- The first receiving assertion ran while the replay warmup was still waiting; the display correctly remained Waiting. It passed after actual samples arrived. No false receiving result was accepted.

The dev2 Android build succeeded with zero errors and one existing diagnostics-symbol warning. ARM64, version code 14 and APK v2 signature were verified; the APK is 88,748,060 bytes. Build/signature/hash results are recorded in `Evidence/Stage2Status/`. Headset text readability, comfort and full physical-device acceptance remain pending. No participant or live physiological validation was performed.

Implementation used the installed Pipeline `commands/scripts.md`, `commands/capture.md`, `commands/editor-lifecycle-and-observability.md` and `commands/build-and-compilation.md` references.

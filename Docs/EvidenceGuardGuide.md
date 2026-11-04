# Stage 3 operating guide

Date: 2026-11-03. Guard revision `evidence-guard-v3-minimal-1`, Python distribution `evidence-guard==0.1.0`. Read the [engineering review](EvidenceGuardReview.md) before interpreting results.

Current APK: `Builds/Stage3Dev5/EvidenceVR-Stage3-dev5.apk`, application `3.0.0-stage3-dev5` / code 29, with [clearer sign typography](SignTypographyReview.md), [lower stimulus/movie clearance](LowerStimulusReview.md) and retained [desk signs](DeskSignHudReview.md). It uses the same guard package, updated Python host and four-block configuration examples from `Builds/Stage3Dev2/`. Historical dev1 files remain under `Builds/Stage3/`. Existing saved eight-block configurations still request eight blocks. [Acquisition budget and dev2 package](ShortCalibrationProgressReview.md).

## Install and select a mode

Use the study service's interpreter. From the project root:

```powershell
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe -m pip install --no-deps .\Builds\Stage3Dev2\evidence_guard-0.1.0-py3-none-any.whl
```

For a source installation use `pip install ./Tools/EvidenceGuard`; an editable development install uses `-e`. Set `evidenceGuardMode` in the existing `adaptive-training-config.json` to `off`, `observe` or `enforce`. Start a **new session** after changing mode or guard settings. The Unity config remains `2.0.0-stage2-dev11` / `personalized-rf-v2`; all Stage 2 control thresholds and qualification gates stay in force.

Start with **observe** when testing the guard. The source defaults to **off**. Current complete four-block engineering examples are under `Builds/Stage3Dev2/adaptive-training-config-{off,observe,enforce}.json`. Copy the chosen file as `adaptive-training-config.json` into the application's persistent-data directory. Set `controllerUrl` to the study computer's reachable HTTP address; `127.0.0.1` in the examples only addresses the same machine unless an explicit ADB reverse tunnel is used. Do not overwrite a participant allocation with an engineering example.

On Quest Pro the existing package data path is `/sdcard/Android/data/com.evidencevr.taskplatform/files/`. The APK does not embed the AV library; retain/deploy the existing reviewed media manifest and `AdaptiveMedia` files using the Stage 2 procedure. Install the new Stage 3 candidate APK for guard-enabled standalone operation. Earlier dev12 APKs cannot transmit the new gaze contract or enforce the reply handshake.

```powershell
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe Tools/AdaptiveTraining/service.py --output Evidence/Stage3Service --personalized-policy Tools/AdaptiveTraining/personalized-policy.json --evidence-guard-config Tools/EvidenceGuard/config.example.json
```

Add the existing `--lsl-config` argument for the explicitly selected live streams. The example policy still does not establish model/device qualification. Do not relax gates to make the demonstrator produce physiological decisions. Behavioral fallback remains explicitly labeled.

`config.mode` and `evidenceGuardMode` are independent. Stage 2 ACTIVE + Patent observe executes the original controller and audits hypothetical guard verdicts. Stage 2 SHADOW still executes no base changes. Patent enforce only filters eligible base candidates; it never starts trials or media.

## Inspect a run

1. Confirm `evidenceGuardMode` and `evidenceGuardRevision` in Unity `session.json`, plus the frozen adaptive configuration. Preserve both Unity and service session folders.
2. Read `evidence_guard_audit.jsonl` in the service's session directory. Follow `host_decision.base` to `review.checks`, `chosen_path`, `delivered`, then the actual `enactment`. An ALLOW response is not proof of execution.
3. Inspect each gaze denominator and interval, not just the ratio. Missing eye tracking produces unavailable task-view evidence, not a zero-attention diagnosis. No head-direction substitute exists. The task ROI is the existing sphere/tray region, counted during actual sphere visibility; movie gaze uses actual playback.
4. For disputed attribution, inspect the named event/signal relationship and affected judgment IDs. OFF remains available. A normal independent L0 challenge does not claim recovered L1 tolerance.
5. Review `defer_duration_seconds`, missing coverage, fault records and actual level history. Long L0 occupancy is not training improvement. HOLD between decision opportunities does not clear an ongoing deferral.
6. A `plugin_fault` latches for that session. In enforce it prevents ON and leaves reductions available. Fix the underlying failure and start a fresh session. Unity independently rejects an enforce ON with a missing/mismatched guard revision, mode, request ID or ALLOW verdict, including replies from an older service.

Media audio evidence reports the software source's active/unmuted/nonzero-volume state. It does not certify acoustic delivery. RF counterfactual diagnostics replace only `movie_fraction` with the configured reference in the same frozen model; they never change predictions used by Stage 2 or authorize/forbid actions. Unavailable diagnostics do not disable the rest of the audit.

## Reproduce the demonstrations

```powershell
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe -m evidence_guard.demos --output Evidence/Stage3/Demos
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe -m evidence_guard.replay Evidence/Stage3/Demos/C_selective_invalidation.jsonl --mode enforce --output Evidence/Stage3/ReplayC
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe -m unittest discover -s Tools/EvidenceGuard/tests -v
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe -m unittest discover -s Tools/AdaptiveTraining -v
```

The demo generator writes normalized input operations, audits for all three modes, and a summary with explicit expected-case denominators. All are synthetic engineering data. The recorded-service replay tool preserves recorded candidates without refitting a model or executing Unity:

```powershell
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe Tools/AdaptiveTraining/replay_evidence_guard.py PATH_TO_SERVICE_SESSION --mode observe --output Evidence/Stage3RecordedReplay
```

Use a new output directory outside the source recording. Old recordings without `guardGaze` remain analyzable but cannot support missing eye evidence. The manifest reports unmatched decisions and faults. Replay is not a new participant or device session. Generic normalized JSONL also accepts independently qualified EEG/ECG change observations and explicit attribution claims; the current Stage 2 adapter does not invent those claims.

## Disable or remove

Stop the session, set `evidenceGuardMode` to `off`, restart the service and begin a new session. Then optionally run `python -m pip uninstall evidence-guard` using the service interpreter. Keep the lightweight host shim; its off path never imports the package. The existing Stage 2 controller remains independent. Removing the package while leaving enforce selected intentionally blocks upgrades instead of silently disabling the guard. No persistent sample setting was enabled during development.

## Quest Pro acceptance still required

No headset was connected during this implementation. Record device identity, OS/runtime, Link or standalone route, build hash, engineering session IDs and the actual eye permission/calibration results. Verify real eye validity and ROI exclusivity, actual movie/sound delivery, packet latency and packet loss, unchanged task geometry/timing, and comfort/performance over a complete protocol. First inspect observe-mode viewing distributions and false deferrals; the 60% reference is not validated. Then exercise enforce with valid and missing tracking, service loss, pause/exit and OFF. Record failures without mixing engineering files with participant data. This does not establish Patent ON/OFF participant benefit or cover the Disclosure's active acquisition mechanisms.

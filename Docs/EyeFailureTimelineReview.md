# Afternoon eye-tracking failure timeline

2026-11-10, Stage 2/3, `eye-afternoon-timeline-v1`. Historical source/configuration comparison and archived-log analysis. No runtime, task, device setting or participant record was changed. Recovery remains unverified.

## Time basis and user clarification

All times below are Europe/Copenhagen (UTC+01:00 on this date). Unity `utcStarted` and session identifiers use UTC. Meta service log timestamps and its filename use local time. A session identifier denotes assignment creation; use `utcStarted` for the Unity session start. Per-event wall times are approximate, anchored from `utcStarted` and the recorded `session_start` application time.

The user explicitly reports no wearer change, headset removal/refit, chair change or eye recalibration across the transition. The change from the Ptest label to P002 does not establish a change of wearer. Recorded eye-height changes therefore cannot be explained as a confirmed physical seating or wearer change.

## Closest before/after records

| Local time | Record | Observation |
|---|---|---|
| 16:21:05 | `20261110T152104_80cf7ac60563`, Practice | 1,494 recorded samples flagged valid; 466 task hits (31.19% of valid samples). Eye-ray direction varies. This is a useful preceding comparison, not a fixation-accuracy acceptance test. |
| 16:26:10.472 | Meta service log, line 23073 | `WinUsb_ReadPipe` fails with Windows error 31; inbound I/O fails and the USB basic session terminates. |
| 16:26:10.472 | Meta service log, line 23117 | Runtime health event `-4503: Flaky cable`. This is a vendor diagnostic label, not proof that the physical cable is the root cause. |
| 16:26:19.279 | Meta service log, line 24287 | A streaming session starts after reconnection; the log reports wired mode. EyeTrackingServiceServer is advertised again at 16:26:21.982. |
| 16:26:28 | `20261110T152626_3ee5fe520905`, Preparation | Initially tracked, then repeated `EYES NOT TRACKED` / `TRACKED EYES` transitions from approximately 16:26:33.9. `NO XR RUNTIME` appears around 16:27:04.5. No scored gaze rows; this is not zero valid coverage during a trial. |
| 16:28:12 | `20261110T152759_9178df1559c2`, Practice | 1,489 samples flagged valid by the old reader, zero task hits under the same practice ROI. Brief initial device/tracking transitions precede the trial rows. |
| 17:41:52 | `20261110T164151_ff635d6fb7dc`, Comparison | 5,501 samples flagged valid, one task hit. This comparison uses the smaller historical sphere-only ROI, so its fraction is not directly comparable to Practice. |

The first two scored Practice configurations differ only in `participantCode`, `seed` and `testSessionId`. Both record `engineering-gaze-ray-v2-soft-white`, `engineering-gaze-outline-v1`, `gaze-roi-v4-sphere-tray-area`, both overlays enabled, and successful `quest-link-eye-space-v1` null-path retry. This does not support a changed eye setting or ROI as the immediate cause of their difference.

Preparation records nevertheless show raw floor-mode local eye heights of 1.343557 m at 16:21, approximately 0.92–0.97 m at 16:26, and approximately 1.49–1.51 m at 16:28, with rig Y=0. Given the user's clarification, these are unexplained tracking/reference-pose changes, not confirmed chair movement. The old records do not contain synchronized continuous head-relative eye samples or native eye timestamps, so they cannot retrospectively prove the exact instant or mechanism of eye freezing.

## Source and revision comparison

- The preserved `AdaptiveRuntime.cs` before the integer-countdown update and before the later unified-ROI update differ only in an initialization log string. Their eye acquisition and world-coordinate conversion code match.
- The compatibility source preserved before today's settings repair and before the later native-layer investigation is byte-identical (SHA-256 `E52B62E7641DE02FD7B1ED74A2C18A2CCC13F355A5245274B11F1DE69D2C3AFE`).
- These snapshots and metadata provide no evidence of an eye-reader code change between the 16:21 and 16:28 sessions. Per-session binary hashes were not recorded, so they are not a complete proof of identical loaded binaries.
- The seated-height normalization and unified ROI are present in the 18:14:40 session; the preceding 17:56:54 session still has the old height behavior and sphere-only comparison ROI.
- The Editor-focus display revision is present at 18:39:27; the hit reticle at 19:02:43; the angular freeze guard at 19:11:49; and native timestamp gating at 20:09:24. These are observed session deployment times, not exact edit times. They follow the 16:26–16:28 transition and cannot explain its onset.
- The November 8 disabled-feature regression is a separate historical issue. Both afternoon Practice sessions already report a successful compatibility retry.

## Correction to the earlier service-restart attribution

The earlier ProjectLog entry described failure as occurring "since the 15:28 Meta Link service restart" while comparing sessions named `15:27...`. That mixed local service time with UTC session time. The service actually started at 15:28 local (14:28 UTC); the 16:21 session with changing rays and task hits occurred afterward. The previous restart-causation and headset-only attribution is withdrawn. World-space ray angular speed also includes head motion and must not be labeled a measured saccade rate.

The closer recorded interruption is the USB I/O failure at 16:26:10 local, followed by reconnection, unstable tracked status, unexplained reference-pose changes and near-zero task hits. This is a temporal association and a stronger investigation target, not proof of USB cable causation. A head-tracker/reference-space fault, eye tracking or calibration fault, Link transport fault, runtime cache problem, and application handling of reconnection are not yet separated.

Later native probes establish repeated eye sample times and head-relative frozen poses in those later runs. They do not prove that precisely the same mechanism already existed at 16:28.

## Evidence and remaining work

Derived counts, exact configuration differences, revision markers, approximate event times and source hashes: [analysis.json](../Evidence/EyeFailureTimeline-20261110/analysis.json). Selected original service lines: [service-excerpt.txt](../Evidence/EyeFailureTimeline-20261110/service-excerpt.txt). Original session data remain under the existing LocalLow `AdaptiveTrainingData/editor-test` archive. A pre-correction ProjectLog copy is retained in `Evidence/EyeFailureTimeline-20261110/restore/`.

Checks: nine archived sessions inspected; exact three-key Practice configuration difference asserted; sample counts checked; preserved compatibility source hashes matched; source diff inspected; service time compared using the same local timezone. No compilation, live eye test, service restart, settings change or new task session was performed.

The useful differential baseline is the 16:21 configuration and unchanged acquisition path, with raw head/eye poses and native timestamps observed independently across a Link reconnection. A future engineering check must establish independent eye motion and coordinate consistency before restoring readiness; removing validity guards alone is not recovery. Physical USB/port/transport discrimination and sustained worn tracking remain pending.

# Evidence Console

## Study stages and comparison report

Use the Study stages link or open `/tests` on the local service. Each assignment has its own stage, participant ID, data root and model identity where applicable. In a comparison session card select **Open comparison report**, choose a checkpoint, then choose one of its linked trials. The evidence table shows the applicable values, denominators and thresholds. A missing receipt is shown as unknown for a delivered change. Refresh to include new records or download the evidence JSON from the same session. [Operator steps](ExperimentGuide.md#independent-study-stages) · [Engineering thresholds](LayerABStudyGuide.md#playback-eye-criteria-and-audit).

## Live EEG sampling rate

All Evidence Console pages and Test sessions show the selected EEG stream's measured sampling rate beside its nominal LSL rate. This follows the current receiver, including while viewing an older session or before starting Unity. Over approximately two seconds, the bridge divides received sample intervals by elapsed raw LSL time. It separately measures delivery against the study computer's monotonic clock. Hover over the strip to see source and throughput. The first reading needs one second of arrivals. Chunk timing can cause small fluctuations. The display refreshes every half second.

If either rate falls below 90% of nominal (225 Hz for a 250 Hz stream), a red **Low EEG sampling rate** message appears. One second without a new chunk shows **No EEG samples received** and 0 Hz on the next refresh. Invalid timestamps and a service outage have separate warnings. Resume streaming and check that the actual rate returns near 250 Hz before calibration. This operator display is independent of electrode quality and model admission.

During Relax and Calculate, a quality banner above the active page updates once per second after the first ten-second interval and a one-second ingestion allowance. Test sessions shows the same warning. The count tracks disjoint training intervals. See [rolling calibration quality](LoadReferenceCalibration.md).

## Current acquisition

The 2026-11-05 service uses `mindtooth-eeg250-8ch-v1`. Earlier source and display verification below applies to archived revisions. [Mindtooth acquisition, verification and pending checks](MindtoothAcquisitionReview.md).

Date: 2026-11-03. Service revision: `evidence-console-v1.2-model-reuse`; display revision: `console-gaze-overview-v1` (retains receive-only waveforms). The original monitoring pages remain read-only: **Patent decisions**, **Physiology** and **Experiment guide**. All assets are served locally; fonts and charts require no CDN. Google Forms still requires an Internet connection.

**Gaze overview** on Physiology shows the latest valid eye-ray projection, a ten-second distribution and Task/Movie/Valid tracking metrics. It requires compatible Unity telemetry and tracked eyes; it never derives gaze from EEG, head direction or ROI fractions. Missing inputs remain explicit with no fabricated points. Refresh the page for the UI, use updated Editor source or rebuild the Player for the new telemetry. [Coordinate definitions, validation and pending Quest Pro checks](GazeConsoleReview.md).

The **Test sessions** page at `/tests` provides local operator actions: ID/storage selection, engineering assignment, model selection and stop/release. Use the single launcher **StartEvidenceConsole.cmd** for direct entry. These actions are distinct from receive-only monitoring; mapped and receiving Mindtooth EEG are required for managed task preparation. Settings survive bridge output-directory changes under `UserSettings/TestSessions/mindtooth-eeg250-v1/port-8765/`. [Complete test/model-reuse guide](ModelReuseTestGuide.md), [unified entry](UnifiedConsoleReview.md).

The launcher opens the engineering test page. The current profile accepts the live Mindtooth amplifier. The new test registry starts independently of retired simulations.

## First setup

1. Connect amplifier 104209-1083 and enable **LSL Stream** in the Mindtooth app. In Physiology, check for eight traces at 250 Hz. The saved profile selects the complete ordered montage. Check contact and verify output units before personal calibration.
2. Prepare the correct Unity configuration and reviewed media. For Editor engineering work, stop Play Mode and use **Evidence VR > Stage 2 > Enable dev11 Personalized Engineering Protocol**. Participant work requires its managed allocation. Keep `controllerUrl` at `http://127.0.0.1:8765` for the local Editor or PC Link route. Guard mode is frozen in the Unity session; this console cannot change it.
3. Double-click **StartEvidenceConsole.cmd** in the project root. It uses the existing project Python environment, starts the service in the background, waits for its health endpoint, and opens `http://127.0.0.1:8765/tests`. An identical running bridge is reused. No replay/synthetic publisher or task assignment is started. The saved selection and optional `UserSettings/personalized-policy.json` are used; an explicit policy argument overrides the saved policy. Use the Evidence Console link to reach monitoring pages.
4. Enter Unity Play Mode or start the configured Quest application. Confirm the actual session ID and participant code, fresh Unity telemetry, intended Guard mode, source identities and source provenance. On **Physiology**, distinguish receiving samples from valid five-trial features and personal-model admission. Samples enter this session's waveform view only while Unity telemetry is fresh.
5. Complete placement, physical A/B checks and signal checks in Unity. Follow the in-page guide through practice, calibration/model admission, oral calibration questions, Training 1/questions, Training 2/questions and archiving. The guide's stage buttons do not advance Unity or mark anything complete. The actual configured calibration budget appears in the guide.

At initial delivery no source selection was saved. A subsequent 2026-11-03 connection repair refreshed the operator's `02_007_5_eeg` selection and verified receipt at 500 Hz in OBSERVE mode. Source provenance remains unknown; the first column is sample index, followed by eight signal channels. Physiological mapping and qualification remain pending. The launcher does not enable a Unity override, start trials, calibrate hardware or grant model qualification. The built-in policy is deliberately non-admitting. Select an explicitly reviewed policy with `--personalized-policy`; do not relax qualification gates to obtain a particular display.

If an old bridge is running, finish and close its Unity session before stopping it. When using this launcher, use the source panel's **Save settings**, not its **Connect** button, to avoid starting a second bridge. A bridge started by the updated source panel can also serve the console; the launcher accepts an equivalent validated LSL selection snapshot. Changed source/policy content or a different bind address requires a service restart and a fresh Unity session.

## Daily startup and shutdown

Normal order: **acquisition/LSL → StartEvidenceConsole.cmd → configured Unity session → preparation checks**. If the outlet UID changes, re-scan/save before starting the bridge. On later launches, the script reloads the saved selection. Double-clicking again reopens the existing compatible service instead of duplicating it.

Closing the browser leaves acquisition running. To finish: end the experiment in Unity, exit Play Mode or the Quest app, allow at least five seconds for telemetry to become inactive, then use **StartEvidenceConsole.cmd --stop** or Disconnect in Unity's LSL source window. The same launcher verifies this project's service identity and refuses while Unity is still sending fresh telemetry. It retains all recorded files. A restarted personalized service requires a new Unity session; it never resumes an old personal model from memory.

Records are under `Evidence/EvidenceConsole/<UTC launch time>/service/<session type>/<session ID>/`. `service.log` and `launcher.json` sit beside that service directory. Existing Unity session folders must also be retained. Display buffers are temporary; the authoritative full records remain raw signals, aligned windows, base decisions, Guard audit and execution events on disk. Service session metadata records `consoleRevision=evidence-console-v1`.

## Quest standalone connection

The monitor does not install an APK or configure the headset network. Current Stage 3 dev5/code-29 can use the existing telemetry/decision contract without a console-specific rebuild. Earlier Stage 2 APKs lack the complete Stage 3 gaze/authorization handshake. Follow [Stage 3 operation](EvidenceGuardGuide.md) for the correct build, configuration and media.

- For an explicitly established USB route, use the existing Android platform tools to run `adb reverse tcp:8765 tcp:8765` for the intended connected device, and keep Unity's `controllerUrl` at `http://127.0.0.1:8765`. Re-establish the tunnel after USB/device restarts.
- For LAN, start `StartEvidenceConsole.cmd --host 0.0.0.0` and set the Quest configuration's `controllerUrl` to the study PC's reachable LAN IPv4 address and port 8765. The PC browser still opens localhost. The devices must share the intended reachable network and the PC firewall must permit this connection. The existing bridge has no Internet-facing authentication; keep it on the controlled study network. The launcher does not alter firewall rules.

Changing an already-running loopback bridge to LAN requires stopping it first. Do not change controller URLs or Guard policy in an ongoing session.

## Reading the pages

**Receive-only raw waveforms:** open `http://127.0.0.1:8765/#physiology` or reload the page. Curves appear before numeric cards and use original numeric OBSERVE samples, including large/unconfirmed values, without filtering, centering or physiological interpretation. No Unity session or EEG/ECG mapping is needed. Choose all signal columns or one named column; explicitly labeled sample index is excluded by default and remains separately selectable. Axes are marked as raw, with **Units unconfirmed** above the plots. Each y-axis auto-scales independently.

The preview contains up to six seconds/12,000 samples and the first sixteen selected columns. Min/max display envelopes preserve extrema. Numeric gaps/nonfinite values break curves; text is not converted to numbers. Outlet/mapping changes and timestamp resets clear the buffer. Samples older than three seconds are marked stale. Preview scope is the **current LSL receiver**, even when an earlier Unity session is selected; it is not archived replay. Full recording and feature/control eligibility retain their existing gates. Snapshot `observedSignals` is separate from task-scoped `signals`.

The v1.1 backend was loaded by ending the paused, zero-trial editor-test preparation and restarting its bridge. Previous records were preserved and the publisher was left running. Unity is currently stopped; raw monitoring remains available. Start a fresh Unity session for the next experiment. An older service requires a controlled restart after ending its session to gain this capability; browser refresh alone cannot update Python code.

Validation: 134 service tests passed, including ten console tests. Browser checks confirmed eight changing curves from the connected unknown-provenance source, selectable sample index, no-Unity operation, stale indication and six viewport widths without page errors. Evidence: `Evidence/EvidenceConsoleValidation/observe-waveforms/`. The display revision is exposed in the page and recorded for new service sessions. This verifies transport/rendering, not physiological quality or device acceptance.

Display revision `console-receive-only-v1` corrects receive-only visibility. On **Physiology**, **Incoming signals** shows connection state and latest OBSERVE/FEATURES channel values directly, refreshed once per second. This is a display snapshot, not a full-rate waveform. Missing EEG/ECG mappings are labeled separately and never imply that the LSL source is absent. Stale/disconnected data are marked explicitly; unknown units/provenance remain unqualified. Reload the browser to receive this static-page update; there is no need to restart an active experiment. The current source has one sample-index column followed by eight signal columns.

**Patent decisions:** selects real request IDs and separates the original base proposal, actual Guard verdict/delivered action, and a matching Unity `personal_execution` receipt. A HOLD delivery, missing receipt or current telemetry level never becomes an invented execution. Off bypasses review; Observe preserves the base action despite a hypothetical DEFER; Enforce can deliver HOLD. The graph summarizes recorded support at the request time. Inspect Guard detail for the exact judgment IDs, checks, chosen path and subsequent revocations. Missing tracking is unavailable evidence, not a diagnosis of inattention. Random authorization and explicit behavioral fallback retain their own basis.

**Physiology** displays eight live EEG waveforms before Unity starts. The operator can inspect individual electrodes or all channels. Personal feature windows show frontal theta and parietal alpha, with movie playback fraction displayed separately. Tracking quality and actual decision records remain visible on their respective pages.

**Experiment guide:** reference instructions for the no-rest protocol, with actual session identity and calibration budget. Calibration and Training 1/2 links use the frozen session's Google Forms URLs where valid, otherwise the published project defaults. It includes calibration scales, six Raw TLX dimensions with the Performance direction, interference/fatigue/discomfort/timing questions, optional comments and oral-administration instructions. The experimenter must verify submission and participant/session linkage externally. No answers are collected by this console, and no completion is inferred from opening a link or pressing X in Unity.

## Commands and recovery

**Permanent Waiting for selected outlet:** check both the selected outlet UID and `/console/health`. A publisher restart changes its UID even when its name remains identical. Refresh and explicitly select the intended current instance. Also verify that the responding service loaded the expected LSL selection. The 2026-11-03 repair found an older empty service answering requests intended for an Editor-launched bridge. `console-listener-exclusive-v1` now prevents competing Windows binds instead of allowing a second listener to appear started. Seven console tests passed, including attempted collision by the legacy HTTP server. The Editor connection field and HTTP status were both read back as Receiving after repair. The existing Unity flow was called with Pipeline `script run`; matching local references were `commands/scripts.md` and `commands/editor-lifecycle-and-observability.md`. Auto-tick was already enabled and was retained. No Play Mode or scene change occurred. Code/configuration backups and discovery evidence are in `Evidence/EvidenceConsoleValidation/connection-repair/`; a backed-up stale UID is historical evidence, not a working reconnection configuration.

```powershell
.\StartEvidenceConsole.cmd
.\StartEvidenceConsole.cmd --lsl-config PATH_TO_REVIEWED_SELECTION.json --personalized-policy PATH_TO_REVIEWED_POLICY.json
.\StartEvidenceConsole.cmd --host 0.0.0.0
.\StartEvidenceConsole.cmd --stop
```

For a custom port, update the Unity URL and any USB tunnel consistently. An unrelated process on the port is never killed. Error output remains visible in the launcher window. If startup dependencies are missing, create the project environment with Python and install `Tools/AdaptiveTraining/requirements.txt` plus `Tools/EvidenceGuard` using that environment; the launcher does not perform unreviewed package upgrades. If source identity or configuration changes, stop acquisition for the session, save the correct selection, restart the bridge and begin a new Unity session.

## Implementation and verification

- Existing `/telemetry`, `/decide` and `/status` contracts remain in place. `/` now serves the console; `/console/health` identifies the project/process/configuration and `/console/snapshot` exposes a read-only projection. Static serving uses an explicit allowlist, and responses disable caching. There is no browser control endpoint.
- Monitoring retains 64 decisions, 64 execution receipts, 64 windows/predictions, 128 recent relevant audit/events and bounded raw display buffers. Projection copies are isolated from controller state. Display envelope generation and JSON encoding occur outside the service lock. No polling of large audit files is introduced.
- Software validation: all 130 adaptive-service tests (including six console tests) and all 14 Guard package tests passed. Logs are recorded in `Evidence/EvidenceConsoleValidation/`. Added checks cover three modes, supported/unsupported viewing paths, base/delivered/receipt separation, immutable snapshot copies, session isolation, freshness, raw provenance, envelope peaks/gaps, HTTP assets and unknown routes, launcher identity and configuration/host matching.
- Headless Edge verified all three pages, eight guide stages and widths 1440/1080/736/560/360/320 without horizontal page overflow or JavaScript errors. Separate synthetic HTTP fixtures verified real fetched values, waveforms, delayed execution receipt and service-disconnection rendering. A real LSL outlet carrying explicitly synthetic samples verified acquisition-to-console transport, exact UID retention, provenance and sample freshness. These fixtures are stored separately under `Evidence/EvidenceConsoleValidation/`, never as participant records.
- The Windows launch/reuse/stop/restart cycle was exercised with no Unity session connected. A fresh synthetic HTTP session confirmed that the stop command refuses active telemetry and leaves the service running. Current delivery is an idle service on localhost, waiting for real setup. No live EEG/ECG, Unity Editor play session, Quest Pro, questionnaire submission, device latency or complete participant protocol was validated in this change. No APK was built or installed. Older packaged service folders are unchanged; use this checkout's root launcher for the new console.

Task rules, timing, scoring, geometry, media, Random/Bioadaptive scheduling, model fitting and Guard authorization remain unchanged. Quest Pro physical acceptance and real personal-model qualification are still required.

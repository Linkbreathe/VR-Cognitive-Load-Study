# LSL source selection and source-name status

Date: 2026-11-02. Stage: Stage 2 engineering. Application remains `2.0.0-stage2-dev8`; last built Android package is dev7. Display revision: `mode-status-v4-source-identity`; selection revision: `lsl-source-selector-v2`. Training configuration, task behavior and `rolling10-stride2-v1` policy are unchanged.

## 2026-11-03 — Receiving-service ownership and disconnect correction

Revision: `lsl-bridge-disconnect-v1`, Editor-only. The window tracked only the Python PID it had launched. After a service restart, a same-project listener could keep receiving while the window displayed Disconnected with a disabled Disconnect button. A stale lock message could also survive process exit.

The window now checks `/console/health` for an Evidence Console belonging to this exact project, then polls source status. A recognized externally started service locks source changes and enables Disconnect. Disconnect uses the existing launcher identity and recent-telemetry checks; it does not adopt that service as an Editor-owned process for shutdown. Stale lock wording clears when the service is gone. Poll generations discard replies from before connect/disconnect actions. Play Mode guards remain intact.

Software evidence: compilation passed. The existing idle same-project listener was detected as Receiving with Disconnect enabled and the picker locked, then stopped using the window action. Scan/Connect/picker became enabled, Disconnect disabled and status Disconnected. Matching/missing/foreign application/project health checks passed. Port 8765 was verified closed; discovery still found `Gin_mock_p007_test` UID `660660ea-f206-4a10-9a59-40e37e119074` and the original `mock_p007_test` publisher. No publisher, mapping, task data or model was changed. Evidence: `Evidence/LslDisconnect/`; restore copy: `LslSourceWindow-before.cs` in that directory.

No APK rebuild is needed for this Editor window. No Quest Pro or physiological acceptance is implied. Verification used installed Pipeline documentation `commands/scripts.md` and `commands/build-and-compilation.md`.

## Purpose

The operator can choose LSL inputs outside VR, and the VR status identifies the stream rather than displaying REPLAY as its main label. Real-time transport and recording provenance are separate: a replay can stream in real time, but remains recorded input in audit data and cannot authorize physiological promotion. No source is silently relabeled as live physiology.

## Operator workflow

1. Start the device publisher or the replay publisher. For this workflow, start the publisher alone; the combined `launch_replay_demo.py` already owns a service and should not run alongside a second bridge on the same port.
2. Stop Unity Play Mode. Open **Evidence VR > Stage 2 > LSL Signal Sources**. No scene object or prefab is required.
3. Click **Scan**, choose **Signal source**, then click **Connect**. The list shows outlet name, publisher-declared type, channel count, nominal rate and shortened UID. Full identity is in **Advanced settings**. The main actions are Scan, Connect and Disconnect; there are no EEG/ECG/FEATURES enable toggles.
4. A new source defaults to **Receive only**, preserving all numeric or text channels without guessing physiology. Irregular event streams are supported; their quiet periods display **Connected, waiting for events**. BioFeatures can be received in this mode as well.
5. For known physiology only, expand **Advanced settings > Processing** and configure EEG, ECG, or both for the selected outlet. Raw processing requires numeric samples at least 100 Hz, explicit zero-based channels and units; new mappings have no guessed channels. **Import mapping** can load a known EEG/ECG configuration after checking its source identity. Python executable, HTTP port, provenance, save/import and diagnostics are also under Advanced settings. Normally keep connection defaults; this is the Unity-to-service HTTP port, not an LSL publisher port.
6. Set source provenance. The supported XDF replay metadata is detected automatically during discovery and acquisition. Other replay publishers must be marked **Recorded replay** by the operator. Unknown/unreviewed input stays unknown. Changing a UI label cannot override a detected replay marker.
7. After **Connect**, confirm actual receipt in the panel; **Advanced settings > Open monitor** shows full details. Connect saves the selection and starts the local Python service with a frozen snapshot. The enabled Editor controller URL must match the selected loopback port (default 8765).
8. Enter Play Mode with an enabled Stage 2 configuration. The panel does not alter automatic-control mode, control basis, task parameters or participant settings. To change input, stop Play Mode, click **Disconnect**, choose a source and reconnect before starting a new session.

Selection pins the actual outlet UID and checks name/type/source ID as additional descriptors. Shared source IDs and identical names remain separately selectable; a missing source ID is allowed with a UID. Publisher restarts require scanning and selecting the new UID. The receiver does not silently substitute another outlet. Compatible saved mappings may be reused, but simultaneous same-descriptor outlets require an exact UID match. The panel selects one outlet with optional separate EEG/ECG mappings; legacy multi-input/FEATURES configurations remain available through the service CLI.

Read-only inspection of `sub-P007.xdf` found five streams, including a nine-column, 500 Hz main stream whose metadata does not establish reliable EEG/ECG channel assignments. The previously used five-minute file is a contiguous subset of this longer recording, with rewritten timestamps. This inspection did not start playback or establish a new mapping.

Selections are local to this checkout at `UserSettings/lsl-source-selection.json`, not committed protocol defaults. Each start creates `Evidence/LslSourceSelection/Bridge-<UTC>/` containing the frozen selection, bridge log and service data. The panel manages only its own process, checks PID and creation time across Editor domain reloads, and closes it when Unity exits. It neither stops nor reconfigures externally launched services. The monitor and log remain accessible when configuration controls are locked. Use the existing CLI for remote-host binding or an explicitly validated calibration file.

## VR and data behavior

Example: `STAGE 2 | AUTO ON: BEHAVIOR | LSL: BioFeatures | RECEIVING`.

`WAITING`, `IDLE`, `PARTIAL`, `LOST` and `SERVICE OFFLINE` distinguish connection states. The displayed name comes from the actual resolved outlet, with source ID as fallback. Long names are shortened in VR; full names, types, source IDs and UIDs remain in the monitor and `mode_status_display` events. TMP markup is disabled for source labels. Older services without identity metadata display SOURCE UNKNOWN instead of inventing a name. Stage 1 still reports PHYSIO NOT USED.

Engineering eye-ray diagnostics have a separate running line, so they no longer replace source status. They remain engineering-only. `statusDisplayVersion` in `session.json` records the new presentation revision; source identities and provenance are retained in the existing status events. Source names do not determine scoring or adaptation.

Explicitly mapped raw EEG/ECG continue through the existing quality and feature pipeline. Receive-only (`OBSERVE`) and legacy FEATURES inputs retain selected values, source timestamps, labels and provenance in service `raw_signals.jsonl`; latest values appear under `SENSORS.external_values`. Strings remain strings. These inputs do not produce EEG/ECG features or become controller-eligible. Provenance also persists for receive-only profiles. Scientific interpretation or physiological use requires separately validated processing.

## Validation and limitations

Current source-first revision:

- **62 Python tests passed**, including exact UID selection with shared identifiers, text/event receipt, idle handling, raw observation retention and recorded-data exclusion from participant sessions.
- Unity compilation passed; **16 Editor integration checks** passed with three real fixture outlets sharing a source ID. Two had identical names/types but different numeric samples: the selected instance's values were received without sibling substitution. String events, idle state, primary actions and selection locks passed. **Five additional profile/revision checks** passed.
- Actual LSL -> Python -> HTTP -> Unity integration preserved exact UID and provenance. Receive-only input produced no physiological features. The compact Editor panel and preparation source row were visually inspected; data stayed under `editor-test`. Two XR audio fallback warnings occurred with no headset; no task runtime errors or compilation failure.
- Original runtime configuration restored byte-for-byte; test bridge/publishers stopped and test selection removed from active use. Evidence: `Evidence/LslSourceFirst/`, including backup `before-source-first.zip`, reports, status snapshots and `vr-source.png`. Helpers: `AgentScripts/LslSourceFirstValidation.cs` and `AgentScripts/lsl_source_first_fixture.py`.
- No new APK or physical headset check. Quest Pro readability/comfort, standalone behavior, physiological calibration and participant acceptance remain pending.
- Final Editor diagnostics included a misspelled Pipeline `script recompile-status` command (corrected to `script recompile_status`, which passed), plus an unrelated Unity AI model-service NoSubscription message after reload. These are recorded in `final-tool-diagnostics.json`; no zero-error-console claim is made.

Historical first-version checks below retain their original scope; the toggle-based workflow is superseded:

- Python suite: 57 tests passed, including source identity/freshness, replay provenance, invalid mappings, combined-source roles and low-rate feature-stream classification.
- Unity: compilation passed; 20 focused Editor checks passed for source labels, provenance, channel rejection, Windows process-argument round trips, UI import, actual LSL discovery and owned-bridge startup/selection locking.
- Integration: two explicitly simulated fixture outlets (250 Hz raw EEG and 2 Hz external features) passed actual LSL -> Python -> HTTP -> Unity receipt. Names and both source IDs survived; raw EEG features were produced while the external feature stream remained observation-only. Runtime logs, source metadata and the `editor-test` output location were checked.
- Visual inspection: Editor panel, preparation badge and running HUD checked. The running source line and engineering eye line are distinct. Two XR audio-driver fallback warnings occurred without a headset runtime; no task runtime or compilation errors were observed. An incorrectly spelled Pipeline command was corrected and did not change project behavior.
- Original Editor configuration was restored byte-for-byte; temporary source selection and bridge were removed from active use. Play Mode is stopped. Evidence: `Evidence/LslSourceSelection/`, including source backup `before-source-selection.zip`, test reports, service snapshot and preparation/running images. `AgentScripts/LslSourceValidation.cs` and `lsl_source_fixture.py` are engineering validation helpers.
- Live discovery also found a `BioFeatures` outlet (`biofeatures_001`, 2 Hz, ten labeled channels). It was not selected as a participant source or admitted for control.
- No new APK was built. Headset readability/stereo comfort, standalone behavior, real sensor calibration and participant acceptance remain pending. This change does not establish real physiological control or training efficacy.

Implementation references consulted: the installed Pipeline `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md`, `commands/capture.md` and `commands/runtime.md`.

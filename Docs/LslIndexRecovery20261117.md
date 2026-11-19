# LSL connection recovery — 2026-11-17

Revision: `lsl-session-index-recovery-v1`. Stage 2/3 engineering maintenance.

## Diagnosis

The receiving service repeatedly exited before acquisition while loading `UserSettings/TestSessions/mindtooth-eeg250-v1/port-8765/test-session-index.json`. The file contained 802,051 zero bytes, causing `JSONDecodeError` at character zero. Repeated failures are preserved in `Evidence/LslSourceSelection/Bridge-20261117T153436909Z/bridge.log` and preceding bridge logs. The cause of the zero-filled file is unknown; no disk, power-loss or application-write cause is claimed.

After restoring startup, the saved outlet UID was also obsolete. A fresh scan found the same named Mindtooth source, source ID, eight channel labels and 250 Hz nominal rate with a new UID. The normal Unity source selection workflow was used to refresh the identity and retain the channel mapping.

Initially the current outlet was discoverable but supplied no samples, including to an independent Python inlet. The Mindtooth application had LSL enabled; its Recorder initially showed Connected with no waveform. During inspection it began showing Streaming. Opening Signal Quality Check interrupted reception; closing that dialog was followed by sustained receiving. This observation does not establish the original reason for the publisher's lack of samples.

## Recovery and preservation

- Backed up the exact corrupt bytes, operator settings and pre-reconnection LSL selection under `Evidence/LslIndexRecovery-20261117/`.
- Rebuilt 133 registry entries from independently parseable `D:/ExperimentData/PatentDemo/*/sessions/*/test-session.json` files, checking session ID, participant, folder, storage root, session type and uniqueness.
- Preserved per-source SHA-256 hashes and recovery provenance. Verified the source files remained unchanged. Replaced only the local index using a flushed, fsynced temporary file and atomic replacement.
- Marked all recovered entries `service_restarted` and revoked, keeping old assignments unavailable for continuation. No pending assignment was created. Settings retained the existing participant selection and storage root.
- Assignment snapshots may lack final completion state, archive summaries and training summaries formerly held only in the index. Recovery does not assert those fields were fully restored. Original trial, event, archive and model files were not edited.
- No production source code, task rules, signal quality gates, model admission rules or scaling values changed. The original null-filled file is retained for further diagnosis.

The one-time helpers are [RecoverSessionIndex20261117.py](../AgentScripts/RecoverSessionIndex20261117.py) and [ReconnectLsl20261117.cs](../AgentScripts/ReconnectLsl20261117.cs). They check for the specific recovery inputs and stop if a backup already exists. Use the normal experiment guide for subsequent connection troubleshooting.

Local evidence files under `Evidence/LslIndexRecovery-20261117/` include `recovery-report.json`, `discovery.json` and `reception-final.json`.

## Validation and handoff state

Seven status samples over approximately 30 seconds all reported receiving, with advancing sample timestamps and no active session. The Unity window reported `Status: 104209-1083_eeg — Receiving`. The Unity console reported zero errors and no compilation failure. At handoff, the Unity-owned bridge was running at `http://127.0.0.1:8765/` and Play Mode was stopped. The local bridge log is `Evidence/LslSourceSelection/Bridge-20261117T153851372Z/bridge.log`. Mindtooth was on its Recorder page with Signal Quality Check closed, and the repair did not start a recording.

This verifies live transport recovery only. It does not establish EEG quality, amplitude-unit correctness, sustained study reliability or worn Quest Pro/participant acceptance. Review normal signal checks and create a fresh assignment before a new study session. The cause and recurrence risk of the corrupted index remain unresolved.

The Unity Pipeline command documentation consulted was `Documentation~/commands/scripts.md` and `Documentation~/commands/editor-lifecycle-and-observability.md` in the installed `com.unity.pipeline` package.

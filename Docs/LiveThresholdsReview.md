# Editable thresholds during A/B comparison

2026-11-07, Stage 2/3, Console `evidence-console-v1.20-editable-live-thresholds`, display `console-editable-live-thresholds-v1`.

The researcher explicitly removed the requirement to freeze ON/OFF thresholds at Start Session. Saved workload RF comparisons now support edits while prepared, claimed or running, including either A/B round. The same current pair applies until another explicit edit; A and B need not retain the initial pair.

## Operation and evidence

Edit the ON/OFF fields in Study setup and click **Apply thresholds**. Held-out forest/arithmetic trigger counts update while typing. The Applied line identifies the actual saved pair and revision. The UI keeps unsaved edits through polling; model-bound pre-session selections are retained locally across reloads. Empty, nonfinite, out-of-range or unordered pairs are rejected. A revision check detects conflicting edits from another window. The local Console header and loopback restriction protect the new `/tests/thresholds` endpoint.

The service serializes edits with controller requests. Every new decision reads the current pair and records `lowThreshold`, `highThreshold` and `thresholdRevision`, including requests that cannot yet produce an action. Existing request IDs return their original cached result. No previously issued adjustment is withdrawn or recalculated by an edit. The model, EEG preprocessing, behavior/eye criteria, probe state and Unity configuration handshake are unchanged.

`test-session.json` and the persistent session registry retain `initialScoreThresholds`, current `scoreThresholds`, `thresholdRevision` and `thresholdHistory`. Each history entry records UTC time, old/new values, researcher source and the latest known stage/trial/request context; that context is not a claim of the next effective trial. The next decision's revision identifies actual use. Recent-session configuration details and report downloads include this history. Existing personal-model source metadata remains the initial-load snapshot. Finished/revoked sessions cannot be edited.

## Verification

Synthetic regression tests cover persistence, invalid/stale edits, completed-session rejection, local HTTP access, actual A/B decision changes, immutable retries, decision-log thresholds and report history. Existing Console, model reuse, workload and compact comparison regressions were run alongside these tests. JavaScript syntax was checked.

An isolated browser fixture at port 8789, participant `SYNTHETIC_UI`, showed enabled threshold fields while the assignment was running. Applying 0.55/0.65 changed the Applied line from revision 0 to revision 1. It did not connect to EEG, Unity or participant storage. Fixture artifacts are under [Evidence/LiveThresholds-20261107](../Evidence/LiveThresholds-20261107).

These are software checks. Live worn-device edits, physical video transitions and participant validity have not been verified by this change.

## Deployment status

The attempted idle-bridge stop/restart was rejected by automatic approval review with `blocked by policy`, without a more specific reason. The local production bridge remains v1.19 until restarted by the operator. The updated UI checks the service's `thresholdEditing` capability and disables running-session edits on the old bridge, with a restart message. Do not treat the source update or isolated test as deployment. With Unity stopped and no active assignment, run `StartEvidenceConsole.cmd --stop`, then launch `StartEvidenceConsole.cmd`; refresh the website after the new bridge is ready.

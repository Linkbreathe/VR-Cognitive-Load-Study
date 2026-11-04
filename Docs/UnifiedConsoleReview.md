# Unified console entry

Date: 2026-11-03. Revision: `unified-console-entry-v1`. Stage: study-computer operation.

The user requested one CMD entry instead of separate monitoring, test, Gin and stop launchers. The project root now contains only **StartEvidenceConsole.cmd**. Double-clicking it opens **http://127.0.0.1:8765/tests**. Test setup links to the existing console, where Physiology, Patent decisions and Experiment guide remain available. Starting the console never prepares or starts a task session.

The launcher defaults to the Test sessions page, reads the saved selection at `UserSettings/lsl-source-selection.json`, and uses `UserSettings/personalized-policy.json` when present. An explicit policy argument still overrides that file. The current saved selection is the already-reviewed Gin replay mapping (EEG columns 1-4, differential ECG 7 minus 8, uV, same publisher UID), and the saved policy matches the V3 test model. No role is inferred from arbitrary sources. Replay remains labeled replay, with all model, provenance and signal-quality checks retained. Other sources require a deliberate new selection and a matching reviewed policy; stale mapping/policy is not calibration.

The old idle 8765 monitor and isolated 8877 test listener were stopped using the existing identity/telemetry checks. One updated service was started on 8765. The Editor bootstrap URL is now 8765; Unity was verified out of Play Mode before editing it. The inactive 8877 test index was merged into 8765 without overwriting conflicting records or resuming an assignment. The original index remains archived in place. Model and session files were neither moved nor deleted. Current tester/root remain `SIM_GIN_TEST` and `D:/ExperimentData/PatentDemo`; model `20261103T184931_8f2739fb95e8-1` remains unvalidated.

Removed obsolete root wrappers: `StartTestConsole.cmd`, `StartGinTestConsole.cmd`, `StopEvidenceConsole.cmd`; removed the obsolete `launch_gin_model_test.py` wrapper. Their originals and all changed configuration files are retained in `Evidence/UnifiedConsole/before.zip`. The source package script now includes only the unified CMD. Existing delivered ZIP/APK artifacts were not rebuilt or rewritten. The optional Gin engineering helper now points to 8765, but was not executed.

To stop recording, use **Stop / release test**, wait for its archive, then exit Unity Play Mode/the Quest app. To stop the Python service, wait at least five seconds after telemetry stops and either use the existing Unity LSL window's **Disconnect**, or run `StartEvidenceConsole.cmd --stop`. There is no separate stop CMD. Closing a browser tab does not stop the service. `--page console` remains an optional advanced entry to monitoring; the default entry supports configured engineering replay and visibly labels it.

## Validation and limits

- Exactly one CMD in the project root; port 8877 is closed.
- The real CMD was invoked with `--no-browser` and reused the existing 8765 listener. Saved source and policy paths matched the launcher defaults.
- Gin was receiving with both EEG and ECG roles; the V3 registry model loaded successfully with identical policy/mapping and remained non-admitted.
- One historical test remained visible; settings retained SIM_GIN_TEST and its existing storage root. No active assignment was created.
- All ten affected console regressions passed. Evidence: `Evidence/UnifiedConsole/console-tests.txt`, `migration.json` and `verification.json`.
- No task rules, timing, signal algorithms, model thresholds, APK, physical-device acceptance or model-efficacy claims changed. This operation supersedes prior instructions to use port 8877 or additional CMD wrappers. The earlier V3 new-live-run validation remains pending; this update starts only the console service.

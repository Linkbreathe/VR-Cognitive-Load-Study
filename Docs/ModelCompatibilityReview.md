# Persistent model configuration and compatibility diagnostics

Date: 2026-11-04. Stage: Stage 2 personalized RF engineering model reuse.
Revision: `model-config-persistence-v1`; console service `evidence-console-v1.3-persistent-model-config`.

## Diagnosis

The Unity LSL Connect action launched `service.py` without a personal-policy argument. Unlike the console launcher, that entry point used built-in defaults even when `UserSettings/personalized-policy.json` existed. The Gin V3 model expected four theta/alpha channels, confirmed EEG units and its recorded electrode description; the service used one channel, unconfirmed units and an unconfirmed description. Software version, feature revision/order and sklearn 1.5.1 matched. The combined error incorrectly suggested recalibration as the only remedy.

A second incompatibility followed: the Unity `LslInputSelection` serialization omitted `channel_labels` and `units`. Numeric channel indices, sample rate and scale still matched, but the model's signal contract included those missing fields. Correcting only policy loading exposed this second error.

## Implemented behavior

- Both service and console launcher resolve the same saved policy automatically. An explicit `--personalized-policy` path takes precedence. Without either, built-in non-admitting defaults remain. A malformed saved policy fails visibly; it never silently falls back.
- Unity saves, reloads and copies channel labels and analysis units in stream selections and remembered profiles. Advanced settings expose **Analysis units (after scaling)**. Units are an explicit mapping value; they are not guessed from a model or automatically qualified.
- Reusing a known profile checks channel-label order as well as existing source/layout checks. A publisher restart still requires scanning and explicitly selecting its new UID; ambiguous sources or changed known labels do not silently inherit a profile.
- Model configuration errors name each differing field with saved/current values. Signal-contract errors identify the role and differing metadata. All compatibility, artifact-hash, candidate, admission and quality checks remain in force.
- The console service revision changes so launchers reject an outdated listener rather than treating it as the fixed service. No task, feature-extraction, training-label, control-threshold or visual revision changed.

## One-time local recovery and operation

Before editing, affected source, documentation and saved configuration were copied to [the restore folder](../Evidence/ModelCompatibilityFix/backup/). The idle old bridge was stopped after confirming Unity was stopped and no test assignment was active.

The exact already-selected Gin outlet was inspected through LSL. Its current XML declares the retained nine-channel layout, 500 Hz and uV on the mapped channels. With the existing scale of 1, those observed labels and units were restored to the saved streams and matching profiles. Model artifacts/manifests were not edited. This recovery did not follow a stale UID or infer units from compatibility requirements. Evidence: [current source XML](../Evidence/ModelCompatibilityFix/current-source.xml), [identity and units](../Evidence/ModelCompatibilityFix/current-source.json), [recovery record](../Evidence/ModelCompatibilityFix/metadata-recovery.json).

Unity Connect then saved the repaired configuration and started the updated bridge on 8765. The unified launcher successfully reused that same bridge. For subsequent compatible engineering replay tests, retain the same ID/root/model and use **Bioadaptive test** with **Allow unvalidated candidate model**. Ordinary bridge restarts do not require retraining or manual policy edits. A restarted publisher requires a fresh selection; a genuine feature, policy, software or acquisition change still requires reviewing the reported difference and restoring a valid matching configuration or retraining as appropriate. This does not authorize reuse of Gin physiology for a participant or replace personal calibration.

## Validation and remaining limits

- All **157 Python regression tests passed**, including policy-path precedence, malformed-policy handling, field-specific policy errors, exact predictions after restoring the policy, and continued rejection of changed channels/rates/labels/units. [Test output](../Evidence/ModelCompatibilityFix/python-tests.txt).
- After tightening diagnostics for missing/null policy keys, all **17 model-reuse tests passed** again. Exact configuration equality remains the gate, independently of diagnostic formatting. [Final targeted checks](../Evidence/ModelCompatibilityFix/model-tests-final.txt).
- Unity 6000.3.27f2 compiled successfully; six isolated metadata/layout/reselection checks passed. Actual Unity Connect preserved both Gin unit fields and channel-label arrays through its save/validate/snapshot path. [Metadata checks](../Evidence/ModelCompatibilityFix/unity-validation.txt), [Connect check](../Evidence/ModelCompatibilityFix/unity-connect-validation.txt). Final console ground truth reported zero errors and no compilation failure.
- The actual new bridge reports the saved-policy path/hash. Loading model `20261103T184931_8f2739fb95e8-1` against that bridge's policy and selection passed all registry checks and model deserialization with its original SHA-256. This was a read-only load using an isolated temporary registry, not a new assignment or trial run. [Load evidence](../Evidence/ModelCompatibilityFix/live-model-load.json).
- The console launcher reused the Unity-started service; EEG and ECG were receiving recorded replay, units were confirmed in the saved engineering policy, and no assignment was active. [Launcher check](../Evidence/ModelCompatibilityFix/launcher-reuse.txt), [final status](../Evidence/ModelCompatibilityFix/final-status.json).
- A second actual disconnect/Connect cycle, with no metadata repair between cycles, retained the same compatible saved mapping/policy and again passed model loading. The final load/status evidence above reflects that second connection.

Unity remains stopped; the corrected acquisition bridge remains running. Existing auto-tick was already enabled and remains enabled. Validation helper: `AgentScripts/ModelCompatibilityValidation.cs`. CLI was updated from beta.15 to beta.16. Pipeline references consulted: `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md` and `commands/runtime.md` in the installed package documentation.

No new 96-trial run, APK, participant calibration, scientific model admission or Quest Pro physical acceptance was performed. The Gin model remains an unvalidated replay/synthetic engineering candidate. Historical models without the required preprocessing revision remain incompatible.

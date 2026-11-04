# Stage 3 Evidence Guard engineering review

Date: 2026-11-03. Specification: supplied revised v3, no active probes. Revision `evidence-guard-v3-minimal-1`; Python 0.1.0; Stage 2 baseline commit `b380e54d416a512f4490beabc5c891a0bff373f1` (restore pointer `codex/stage3-pre-guard-b380e54`). Scope: Stage 3.1–3.3 software implementation, with physical acceptance pending.

## Implementation and preserved boundaries

`Tools/EvidenceGuard` is an independently installable standard-library Python package. `Tools/AdaptiveTraining/evidence_guard_adapter.py` wraps the existing service **after** `PersonalSession.receive` produces/caches its original decision. `personalized_runtime.py`, `personalized_model.py`, `personalized_signals.py`, controller thresholds and policy files are unchanged. Base decisions remain in `personal_decisions.jsonl`; guard records are separate. No model refit, resting baseline, probe, extra trial, extra media slot or correctness-feedback change was introduced.

The Unity adapter transports bounded eye-observation intervals, preserves exclusive production ROI classification, records software audio state, binds guard mode/revision to session metadata, and independently rejects unauthorized enforce ON replies at reception and at the existing execution boundary. Four-second visibility/responses, 4.5-second cycles, five-trial requests, twenty-trial behavioral control and the existing media-safe/stale-response gates remain intact. The visual scene and assets are unchanged.

Mode behavior:

| Mode | Analysis/audit | Effective base actions |
|---|---|---|
| off | No package import or guard processing | Original Stage 2 |
| observe | Full guard review, including faults | Original Stage 2 |
| enforce | Full review with explicit authorization | Unsupported ON becomes HOLD; reductions remain available |

The independent Random schedule is an explicit protocol authorization path, without a gaze requirement. Bioadaptive and behavioral-fallback ON candidates use their actual recent twenty-trial basis, with the configured viewing dependency; RF candidates additionally cite their original good prediction windows. They are ordinary challenge judgments, never implicit claims of recovered L1 tolerance. Generic host proposals and replay cases can explicitly depend on attribution or recovery judgments.

## Evidence and relations

Evidence is immutable and retains identity, source, time, quality, level, scene revision, epoch and payload. Session/condition changes revoke support relationships, not measurements. Authorization paths are OR alternatives of AND dependencies. Every invalidation names affected judgments and already recorded authorizations. Reusing old condition evidence under a new judgment ID does not repair it. New applicable evidence requires a new, auditable judgment; granting an independent challenge leaves the old revoked claim revoked.

The time index supports actual-event forward checks and reverse lookup after quality-valid nonoverlapping RF windows cross stable/struggling bands. Reverse checks also accept explicit attribution/qualified feature-change triggers. They query media, task, gaze, session and actual intervention records, keeping task/movement/quality/intervention/model-input alternatives visible. Delays are separately configurable for RF, EEG, ECG and behavior. Default broad ranges are engineering associations, not validated response latencies.

Relations are keyed by event ID plus signal ID. Only the same relationship accumulates forward/reverse membership. A confirmed temporal/exposure association is SUPPORTED; missing/ambiguous information is UNRESOLVED; explicit failed-exposure or pre-event/timing contradictions are DISPUTED. An overlapping aggregate RF window alone cannot locate the start of a difficulty. The adapter consequently does **not** infer a pre-movie onset from its window start or declare real physiological change from RF output. No observed response is not proof of no effect.

Gaze is duration-weighted, capped at 100 ms per observation to expose gaps, and limited to the supported judgment's actual intervals. The three ratios are valid/total time, task/valid time, and movie/valid time. Missing valid eyes leaves task/movie ratios null. The existing task-first mutually exclusive ROI classification prevents double counting. Task judgments use actual sphere visibility over the same twenty trials; physiology relationships use their windows; movie exposure uses actual playback. Candidate thresholds are 0.70 validity and 0.60 task viewing, explicitly unvalidated.

The optional counterfactual copies a window's feature vector, changes only movie fraction to the configured reference and calls the same frozen RF. Scores, model artifacts and base decisions remain unchanged. It is an input-dependence diagnostic with unknown distributional support, never a causal forecast or a direct action gate.

## Faults and provenance

The adapter latches import, schema, processing and audit-writing failures. Enforce upgrades fail closed; OFF/REST remain available. Observe keeps the exact base action even on plugin failure. Request identities are cached and checked; raw evidence identity reuse with different content is rejected. Pauses, stage changes, mount interruption and explicit resets advance evidence scope. Actual execution acknowledgments carry request, previous/actual level, time and scene. Unity still owns execution timing, cancellation and the real scene.

Unity `session.json` and the frozen adaptive config identify the guard policy; Python configuration records include thresholds/delays. `evidence_guard_audit.jsonl` links records and returns defensive copies through `export_audit`. Raw Unity/service telemetry remains separate and retained. Recorded-service replay writes to a new directory, does not refit/execute, and labels its provenance. Synthetic demo data and isolated Editor tests are never participant evidence.

## Verification

- Core suite: 14 tests, including three demonstration families across all modes, duration weighting/gaps/exclusivity, missing gaze, asynchronous unresolved relations, modality latency, nonoverlapping RF triggers, selective disputes, no timed restoration, natural new evidence, condition round trips, immutable retries, counterfactual independence and protected reductions.
- Host integration: 10 tests covering original controller output, valid/missing gaze, RF and fallback basis preservation, exact retry behavior, actual failed media versus plans, package/import/runtime/disk faults, real localhost HTTP, actual acknowledgments and separate recorded replay.
- Full Python regression and final build evidence are recorded in `Evidence/Stage3/`; the retained Stage 2 tests are rerun with the optional distribution actually uninstalled, then the package is reinstalled.
- Unity: 106 isolated Play Mode assertions passed for off/observe/enforce and ON/OFF/HOLD/REST combinations, missing/stale/mismatched authorization, duration transport/gap caps, serialization and frozen metadata. Production scripts compiled, and the PASS log was read back.
- Editor caveat: one `Release of invalid GC handle. The handle is from a previous domain` assertion appeared after the first validation, without a source stack. The Editor also logged Unity AI `NoSubscription` messages. One MCP console read timed out; direct CLI inspection succeeded. These records are retained; this is not a clean-console claim. The guard test entry points completed successfully. Play Mode was stopped; the previously enabled auto-tick setting was retained.
- ADB device inventory was empty. No Quest Pro, genuine gaze, physical audio onset, participant or physiological qualification was performed.

The demo summary reports valid-action retention and unsupported-upgrade execution with explicit scripted denominators, conflict detection, review latency and audits of deferral duration. These values test programmed cases, not experimental effectiveness. Main cases: A normal first challenge stays ON; B actual 11-second exposure is distinguished from failed playback and preexisting difficulty, while all OFF requests remain OFF; C recovery-only ON is deferred and an independent challenge is allowed without restoring the invalid recovery claim. No probe path exists.

## Delivery and remaining checks

See [operation](EvidenceGuardGuide.md) and [release evidence](EvidenceGuardRelease.md) for package/APK paths, build status and reproduction commands. The build helper temporarily sets Stage 3 version/code and restores the prior project PlayerSettings afterward; it never changes task configuration or enables the plugin in a current session. Existing Stage 2 APKs are retained.

Stage 3.1 and 3.2 software acceptance is demonstrated; Stage 3.3 software dependency/action checks are demonstrated. Quest Pro trial acceptance, real viewing distributions/threshold selection, full-session device performance, personal physiological/model qualification, and Patent OFF/ON participant comparisons remain open. The package demonstrates the core evidence-audit mechanism and does not cover the Disclosure's active re-acquisition scheme. No benefit, causality, cognitive-effort or validated L1-tolerance claim follows from these engineering results.

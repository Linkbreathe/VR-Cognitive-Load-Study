# Evidence Guard 0.1.0

Stage 3 engineering demonstrator for the revised v3 specification. Pure Python 3.10+, no runtime dependencies, Unity imports, model fitting, active probes, additional trials, or media scheduling. The package audits proposed actions; it does not estimate participant state.

The public API exports `EvidenceGuard`, `Evidence`, `Judgment`, `Proposal`, `Enactment`, and `GuardConfig`. A guard provides `ingest`, `review`, `on_enactment`, `reset`, `export_audit`, and `add_judgment`. JSON-compatible dataclass fields are defined in `evidence_guard/contracts.py`. See the [project review](../../Docs/EvidenceGuardReview.md) and [operating guide](../../Docs/EvidenceGuardGuide.md).

```powershell
python -m pip install ./Tools/EvidenceGuard
python -m unittest discover -s Tools/EvidenceGuard/tests -v
python -m evidence_guard.demos --output Evidence/Stage3/Demos
python -m evidence_guard.replay Evidence/Stage3/Demos/C_selective_invalidation.jsonl --mode enforce --output Evidence/Stage3/ReplayC
```

Install into the **same interpreter that runs the study service**. For development, use `pip install -e ./Tools/EvidenceGuard`; old pip/setuptools installations may need updated packaging tools. The supplied wheel needs no build tools.

## Host contract

- All times are finite seconds in one session monotonic clock. The host aligns sensor clocks before ingestion. The package does not guess clock offsets.
- Evidence carries immutable ID, session, source, interval, level, visual scene revision, quality, lifecycle epoch and kind-specific data. All input objects are copied. Retry the same ID only with identical evidence.
- Kinds include `trial`, `gaze`, `media`, `signal`, `task`, `session`, `enactment`, and `schedule`. Raw host telemetry remains the authoritative acquisition record.
- `gaze` is an interval with one exclusive target (`task`, `movie`, `other`, `invalid`), explicit validity, and actual visibility flags. Gaps above 100 ms are not extrapolated. For task judgments, provide each trial's actual `visible_start` and `visible_end`. Movie gaze uses actual playback intervals.
- `media` must distinguish `actual` playback from plans/failures and preserve `clip_id`, actual endpoints, audio confirmation and provenance. Audio confirmation is a software playback observation, not measured sound at the ear.
- `signal` retains feature values, quality, modality, RF score and window endpoints. An explicit `claimed_event` and, when independently known, `change_onset` allow attribution conflicts to be tested. Absence of these fields is uncertainty, not a conflict. `physiological_change_confirmed` is a separate host-supplied, qualified observation; the adapter never sets it from RF changes.
- Judgments name their evidence, applicable level/scene, optional viewing dependency, and optional relation ID. `recovered_l1` additionally needs L1 evidence with actual media and explicit host-protocol comparability. The guard does not manufacture a recovery classifier or validate a comparability protocol.
- Proposal `legal` represents the **existing host controller's** eligibility. The guard cannot upgrade an illegal candidate. `paths` contains alternative lists of judgment IDs: all judgments within a list are required; any valid list can authorize the action. The host owns the semantic truth of its state estimates; the guard checks their evidence applicability.
- A new judgment ID with newly applicable evidence can be evaluated. A revoked judgment is never automatically restored by waiting or by authorizing an independent action. Renaming old condition evidence does not restore it.
- Enactments acknowledge actual execution, including previous/actual level, request identity, scene and time. Merely proposing OFF does not establish an environment change. The host must preserve its own stale-response and execution-boundary gates and cancel pending work after lifecycle invalidation.
- `reset` supports session, recalibration, mount, pause, resume and stage scopes. It revokes active support, advances the epoch, and retains measurements/history. Create a new guard for a different session ID.

## Review semantics

`ALLOW`, `DEFER`, and `NO_CHANGE` are audit verdicts. HOLD produces `NO_CHANGE`. In `enforce`, DEFER maps an unsupported ON to HOLD. OFF, PAUSE, EXIT, REST and SAFETY_REDUCE retain independent authority. In `observe`, verdicts are recorded while the base action is unchanged. The supplied host adapter's `off` path performs no package import, ingestion, analysis or guard audit.

The configurable 70% eye validity / 60% task-view references and modality delays are engineering candidates, not validated attention, effort, physiological or causal thresholds. Freshness rejects stale evidence; it never restores invalid support. Random scheduling has independent protocol authority and does not require gaze. Configure viewing dependencies per action, not as a universal XR gate.

Audit JSONL includes configuration, evidence, judgments, view checks, forward/reverse checks, relation revisions, invalidations, authorizations, actual enactments and execution discrepancies. Relations retain forward/reverse direction membership and `SUPPORTED` / `UNRESOLVED` / `DISPUTED` status. Support means scoped association, never demonstrated causation. `export_audit()` returns a defensive copy.

The optional sink is called before a record is accepted into the audit. Sink failures propagate to the adapter's latched fault boundary. Production hosts must apply the same fail-closed upgrade behavior; the package alone never operates a scene.

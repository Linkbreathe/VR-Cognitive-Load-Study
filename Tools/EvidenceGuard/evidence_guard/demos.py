"""Generate three reproducible, explicitly synthetic demonstration families."""
from dataclasses import asdict
from pathlib import Path
import argparse
import json

from .contracts import Evidence, Judgment, Proposal, Enactment
from .replay import replay

SESSION = "synthetic-stage3"
SCENE = "warm-study-v1"


def evidence(key, kind, start, end, level=0, quality=True, **data):
    return Evidence(key, kind, SESSION, start, end, "synthetic_script", level, SCENE, quality, data)


def op(kind, value):
    return {"op": kind, "value": asdict(value) if hasattr(value, "__dataclass_fields__") else value}


def stable(start=0, prefix="current", level=0):
    rows, ids = [], []
    for i in range(20):
        a = start + i * 4.5
        key = prefix + ":trial:" + str(i)
        ids.append(key)
        rows.append(op("ingest", evidence(key, "trial", a, a + 4.5, level,
            correct=i not in (3, 8, 13, 18), responded=i != 3, visible_start=a, visible_end=a + 4)))
        for s in range(40):
            rows.append(op("ingest", evidence(prefix + ":gaze:" + str(i * 40 + s), "gaze", a + s * .1, a + (s + 1) * .1,
                level, target="task", task_visible=True, movie_visible=False)))
    rows.append(op("judgment", Judgment(prefix, "training_challenge", tuple(ids), level, SCENE, True)))
    return rows, prefix


def proposal(key, when, paths=(), action="ON", level=0):
    return Proposal(key, SESSION, when, action, level, SCENE, True, tuple(paths), "synthetic_base_controller")


def cases():
    a, jid = stable()
    a.append(op("review", proposal("A-first", 90, [(jid,)])))
    b = []
    for i, (name, actual, onset) in enumerate((("exposed", True, 103), ("failed", False, 103), ("preexisting", True, 95))):
        offset = i * 200
        b.append(op("ingest", evidence(name, "media", offset + 100, offset + (111 if actual else 100), 1,
                                     actual=actual, audio_confirmed=actual, clip_id="synthetic-clip")))
        b.append(op("ingest", evidence(name + "-signal", "signal", offset + 90, offset + 112.5, 1, score=.75,
            response_observed=True, modality="rf", change_onset=offset + onset, claimed_event=name,
            physiological_change_confirmed=False, counterfactual={"reference_score": .42, "causal_prediction": False})))
        b.append(op("review", proposal("B-off-" + name, offset + 113, action="OFF", level=1)))
    c = [op("ingest", evidence("prior-difficulty", "signal", 0, 22.5, 1, score=.75, response_observed=True)),
         op("enactment", Enactment("actual-off", "prior-off", SESSION, 25, 1, 0, SCENE))]
    stable_ops, jid = stable(30, "new-l0")
    c += stable_ops
    c.append(op("judgment", Judgment("claimed-recovery", "recovered_l1", ("new-l0:trial:19",), 0, SCENE)))
    c.append(op("review", proposal("C-recovery-only", 120, [("claimed-recovery",)])))
    c.append(op("review", proposal("C-independent-challenge", 121, [("claimed-recovery",), (jid,)])))
    return {"A_normal_challenge": a, "B_attribution": b, "C_selective_invalidation": c}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--output", type=Path, required=True)
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    summary = {"data_origin": "scripted_synthetic", "device_verified": False, "cases": {}}
    for name, operations in cases().items():
        (args.output / (name + ".jsonl")).write_text("".join(json.dumps(o) + "\n" for o in operations), encoding="utf-8")
        item = {}
        for mode in ("off", "observe", "enforce"):
            guard, results = replay(operations, mode)
            (args.output / (name + "_" + mode + "_audit.jsonl")).write_text("".join(json.dumps(o) + "\n" for o in guard.export_audit()), encoding="utf-8")
            item[mode] = {"actions": [r["effective_action"] for r in results], "verdicts": [r["verdict"] for r in results],
                          "relation_states": {k: v["state"] for k, v in guard.relations.items()},
                          "review_latency_ms": [r["latency_ms"] for r in results],
                          "invalidations": guard.invalidations}
        summary["cases"][name] = item
    summary["scripted_metrics"] = {}
    for mode in ("off", "observe", "enforce"):
        family = summary["cases"]
        valid = (int(family["A_normal_challenge"][mode]["actions"][0] == "ON") +
                 sum(a == "OFF" for a in family["B_attribution"][mode]["actions"]) +
                 int(family["C_selective_invalidation"][mode]["actions"][1] == "ON"))
        summary["scripted_metrics"][mode] = {"valid_actions_retained": valid, "valid_actions_total": 5,
            "unsupported_upgrades_executed": int(family["C_selective_invalidation"][mode]["actions"][0] == "ON"),
            "unsupported_upgrade_candidates": 1, "conflicts_detected": sum(v == "DISPUTED" for v in family["B_attribution"][mode]["relation_states"].values()),
            "explicit_conflicts_total": 2, "interpretation": "scripted_engineering_cases_only_not_participant_effectiveness"}
    (args.output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()

"""Read recorded service telemetry and frozen base decisions; never refit or execute.

Old recordings without guardGaze remain usable with unavailable viewing evidence.
Outputs always live in a separate recorded-replay directory.
"""
import argparse
from copy import deepcopy
import json
from pathlib import Path
from types import SimpleNamespace

from evidence_guard_adapter import GuardAdapter
from personalized_signals import movie_fraction


def rows(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()] if path.exists() else []


def run(source, output, mode):
    source, output = Path(source).resolve(), Path(output).resolve()
    if output == source or source in output.parents or output.exists():
        raise ValueError("Use a new output directory outside the source recording")
    telemetry = rows(source / "telemetry.jsonl")
    predictions = rows(source / "personal_predictions.jsonl")
    decisions = rows(source / "personal_decisions.jsonl")
    if not telemetry:
        raise ValueError("No recorded telemetry")
    output.mkdir(parents=True)
    adapter = None; results = []; seen = set(); events = set(); last_change = float("-inf")
    for item in telemetry:
        p = deepcopy(item["packet"]); p["config"]["evidenceGuardMode"] = mode
        if adapter is None:
            adapter = GuardAdapter(p, output)
        for e in p.get("events", []):
            key = json.dumps(e, sort_keys=True)
            if key in events:
                continue
            events.add(key)
            if e["type"] in ("adaptive_level_changed", "pause", "resume", "protocol_stage", "personal_mount_interrupted"):
                last_change = max(last_change, e["actual"])
        personal = SimpleNamespace(trials={str(i): t for i, t in enumerate(p.get("trials", []))},
            predictions=[r for r in predictions if r["end"] <= p["now"]], model=None, last_change=last_change,
            exposure=lambda start, end: movie_fraction(start, end, [(e.start, e.end) for e in adapter.guard.evidence.values() if e.kind == "media" and e.data.get("actual")]) if adapter.guard else 0.)
        adapter.receive(p, personal)
        for entry in decisions:
            d = entry["decision"]
            if d["requestId"] in seen or entry["packet"]["now"] != p["now"]:
                continue
            seen.add(d["requestId"])
            results.append({"base": d, "delivered": adapter.receive(p, personal, d)})
    manifest = {"data_origin": "recorded_replay", "source": str(source), "mode": mode,
                "replayed_decisions": len(results), "recorded_decisions": len(decisions),
                "unmatched_decisions": len(decisions) - len(results), "fault": adapter.failure,
                "counterfactual": "unavailable_without_frozen_model; original_diagnostics_remain_in_source",
                "device_verified": False, "actions_executed": False}
    (output / "replay_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    (output / "replayed_decisions.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--mode", choices=["off", "observe", "enforce"], default="observe")
    args = parser.parse_args()
    print(json.dumps(run(args.source, args.output, args.mode), indent=2))

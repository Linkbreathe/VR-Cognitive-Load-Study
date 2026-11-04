"""Replay normalized JSONL operations without a device, model fit, or scene changes."""
import argparse
import json
from pathlib import Path

from .core import EvidenceGuard


def replay(operations, mode="observe", session="synthetic-stage3", config=None):
    guard = EvidenceGuard(session, mode, config)
    results = []
    for op in operations:
        kind, value = op["op"], op["value"]
        if kind == "ingest":
            guard.ingest(value)
        elif kind == "judgment":
            guard.add_judgment(value)
        elif kind == "review":
            results.append(guard.review(value))
        elif kind == "enactment":
            guard.on_enactment(value)
        elif kind == "reset":
            guard.reset(**value)
        else:
            raise ValueError("Unknown operation: " + kind)
    return guard, results


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("input", type=Path)
    p.add_argument("--mode", choices=["off", "observe", "enforce"], default="observe")
    p.add_argument("--session", default="synthetic-stage3")
    p.add_argument("--output", type=Path, required=True)
    p.add_argument("--config", type=Path)
    args = p.parse_args()
    config = json.loads(args.config.read_text()) if args.config else None
    guard, results = replay([json.loads(s) for s in args.input.read_text().splitlines() if s.strip()], args.mode, args.session, config)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "audit.jsonl").write_text("".join(json.dumps(r, allow_nan=False) + "\n" for r in guard.export_audit()), encoding="utf-8")
    (args.output / "decisions.json").write_text(json.dumps(results, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()

"""Reproducible four-cell allocation: training order x calibration starting condition."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import random
import re


def assignment(participant, enrollment_index, allocation_seed):
    if not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", participant) or enrollment_index < 0:
        raise ValueError("Use a pseudonymous participant code and a nonnegative enrollment index")
    block, position = divmod(enrollment_index, 4)
    groups = list(range(4)); random.Random(allocation_seed + block * 104729).shuffle(groups)
    digest = hashlib.sha256(f"{allocation_seed}:{enrollment_index}:{participant}".encode()).hexdigest()
    return {"participantCode": participant, "counterbalanceGroup": groups[position],
            "seed": int(digest[:7], 16), "assignmentId": f"allocation-v1-{allocation_seed}-{enrollment_index}",
            "enrollment_index": enrollment_index, "allocation_seed": allocation_seed}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--participant", required=True); parser.add_argument("--index", type=int, required=True)
    parser.add_argument("--allocation-seed", type=int, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--controller-url", required=True)
    parser.add_argument("--calibration-blocks", type=int, choices=[4, 6, 8], default=4)
    parser.add_argument("--engineering", action="store_true")
    args = parser.parse_args()
    allocation = assignment(args.participant, args.index, args.allocation_seed)
    root = Path(__file__).resolve().parents[2]
    config = json.loads((root / "Assets/TaskPlatform/Config/adaptive-training-config.json").read_text())
    config.update({k: v for k, v in allocation.items() if k in config})
    config.update(enabled=True, mode="ACTIVE", sessionType="engineering" if args.engineering else "participant",
                  controllerUrl=args.controller_url, calibrationBlocks=args.calibration_blocks)
    args.output.mkdir(parents=True, exist_ok=True)
    # Exclusive creation avoids silently overwriting an assigned participant's configuration.
    path = args.output / f"{args.participant}-adaptive-training-config.json"
    with path.open("x", encoding="utf-8") as f:
        json.dump(config, f, indent=2)
    with (args.output / f"{args.participant}-allocation.json").open("x", encoding="utf-8") as f:
        json.dump(allocation, f, indent=2)
    print(path)


if __name__ == "__main__":
    main()

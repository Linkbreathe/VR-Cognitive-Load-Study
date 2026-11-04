"""Seeded synthetic responses only; signal source binding is a separate explicit step."""
import argparse
import csv
from datetime import datetime, timezone
import json
from pathlib import Path
import uuid

import numpy as np
from personalized_model import policy_config, window_behavior, label_fit, label_rows


def generate(seed=20261103, blocks=8):
    rng = np.random.default_rng(seed)
    rows = []
    for block in range(blocks):
        # Predetermined performance scenarios are independent of signal values.
        scenario = "stable" if block % 4 < 2 else "struggling"
        colors = []
        for index in range(12):
            color = int(colors[-2] if index >= 2 and rng.random() < .4 else rng.integers(0, 4))
            colors.append(color)
            scored = index >= 2
            expected = scored and color == colors[-3]
            omission = scored and scenario == "struggling" and index % 5 == 2
            responded = scored and not omission
            correct = responded and (scenario == "stable" or index % 3 == 0)
            rt = float(rng.uniform(.6, 2.6)) if responded else None
            trial = block * 12 + index
            onset = trial * 4.5
            rows.append(dict(block=block, trial=trial, phaseTrial=trial, blockTrial=index,
                trainingStage="calibration", level=block % 2, color=color, seed=seed,
                scored=scored, expectedMatch=bool(expected), responded=responded,
                responseMatch=bool(expected if correct else not expected) if responded else None,
                correct=bool(correct), timeout=omission, reactionTime=rt,
                plannedOnset=onset, actualOnset=onset, trialEnd=onset+4.5,
                responseTime=onset+rt if rt is not None else None,
                interrupted=False, aborted=False, transition=not scored,
                synthetic=True, origin="generated_behavior", scenario=scenario))
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--seed", type=int, default=20261103)
    parser.add_argument("--blocks", type=int, choices=(4, 6, 8), default=8)
    args = parser.parse_args()
    folder = args.output / (datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S") + "_" + uuid.uuid4().hex[:8])
    folder.mkdir(parents=True, exist_ok=False)
    rows = generate(args.seed, args.blocks)
    policy = policy_config()
    windows = []
    for i, row in enumerate(rows):
        if row["blockTrial"] < 6:
            continue
        behavior = window_behavior(rows[i-4:i+1], policy)
        assert behavior["valid"]
        windows.append(dict(block=row["block"], trial=row["trial"], level=row["level"],
            behavior=behavior, signalStatus="awaiting_confirmed_source", features=None,
            valid=False, synthetic=True, origin="generated_behavior_only"))
    labels = label_rows(windows, label_fit(windows, policy), policy)
    for window, label in zip(windows, labels):
        window["simulatedBehaviorLabel"] = int(label)
    metadata = dict(participantCode="SIM_SOURCE_PENDING", sessionType="synthetic",
        revision="simulated-calibration-record-v1", seed=args.seed, blocks=args.blocks,
        trialCount=len(rows), durationSeconds=len(rows)*4.5, modelTrained=False,
        signalSource=None, status="awaiting_signal_source_identification",
        note="Generated responses and clock values, not a Unity recording or actual participant calibration. No signal features, quality acceptance or model admission are asserted.",
        behaviorGeneration="Preset block performance independent of physiology; labels are not ground truth for cognitive state.",
        simulatedLabelCounts={str(c):int(sum(labels==c)) for c in (-1,0,1)})
    for name, value in (("simulation.json", metadata), ("simulated-trials.json", rows), ("behavior-windows.json", windows)):
        (folder/name).write_text(json.dumps(value,indent=2,allow_nan=False),encoding="utf-8")
    with (folder/"simulated-trials.csv").open("w",newline="",encoding="utf-8") as f:
        writer=csv.DictWriter(f,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
    assert all(r["synthetic"] for r in rows) and all(not w["valid"] for w in windows)
    print(json.dumps(dict(folder=str(folder.resolve()),**metadata),indent=2))


if __name__ == "__main__":
    main()

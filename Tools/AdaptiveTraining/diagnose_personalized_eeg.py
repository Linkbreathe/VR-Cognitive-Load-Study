"""Recompute recorded EEG rejection reasons without changing data, mappings or gates."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np

from personalized_signals import eeg_features, sampled_window


def diagnose(folder):
    folder = Path(folder)
    raw = [json.loads(line) for line in (folder / "raw_signals.jsonl").open(encoding="utf-8")]
    raw = [r for r in raw if r["stream"]["kind"] == "EEG"]
    packets = [json.loads(line) for line in (folder / "telemetry.jsonl").open(encoding="utf-8")]
    windows = [json.loads(line) for line in (folder / "aligned_windows.jsonl").open(encoding="utf-8")]
    results = []
    for w in windows:
        if "eeg" not in w.get("quality", {}):
            results.append({"trial": w["trial"], "reason": w["reason"], "evaluated": False})
            continue
        candidates = [r for r in packets if r["packet"]["now"] >= w["end"] + .75 and
                      any(t["trainingStage"] == w["stage"] and t["phaseTrial"] == w["trial"]
                          for t in r["packet"].get("trials", []))]
        if not candidates:
            raise ValueError("No original acquisition packet for window " + str(w["trial"]))
        telemetry = candidates[0]; packet = telemetry["packet"]
        chunks = [r for r in raw if r["received_monotonic"] <= telemetry["received_monotonic"]]
        config = chunks[-1]["stream"]
        times = [t - packet["clockOffset"] for r in chunks for t in r["corrected_monotonic"]]
        samples = [x for r in chunks for x in r["samples"]]
        x, reason = sampled_window(times, samples, w["start"], w["end"], config["sample_rate"])
        if x is None:
            raise ValueError("Original window could not be reconstructed: " + reason)
        columns = list(range(x.shape[1]))
        _, quality = eeg_features(x, config["sample_rate"], columns, columns)
        solo = [eeg_features(x[:, [i]], config["sample_rate"], [0], [0])[1]["valid"] for i in columns]
        results.append({"trial": w["trial"], "block": w["block"], "level": w["level"],
            "start": w["start"], "end": w["end"], "evaluated": True, "quality": quality,
            "recorded_valid_fraction": w["quality"]["eeg"]["valid_fraction"],
            "fraction_reproduced": abs(quality["valid_fraction"] - w["quality"]["eeg"]["valid_fraction"]) < 1e-10,
            "single_channel_counterfactual_passes": solo})
    evaluated = [r for r in results if r["evaluated"]]
    config = raw[0]["stream"]
    summary = {"session": folder.name, "source": "recorded_replay_only", "stream": config,
        "mapping_note": "Saved samples already have channel selection and scale_to_units applied once by service.py.",
        "units_note": "Recorded source header declares uV and scale_to_units is 1; physical units still require device qualification.",
        "thresholds_changed": False, "channels_removed": False, "model_admitted": False,
        "evaluated_windows": len(evaluated), "all_fractions_reproduced": all(r["fraction_reproduced"] for r in evaluated),
        "joint_passes": sum(r["quality"]["valid"] for r in evaluated), "channels": [], "windows": results,
        "limits": "Overlapping windows/subsegments are dependent. Single-channel checks are diagnostic counterfactuals, not a revised montage or permission to drop channels."}
    for j, source_column in enumerate(config["channels"]):
        channels = [r["quality"]["channels"][j] for r in evaluated]
        summary["channels"].append({"selected_column": j, "source_column": source_column,
            "window_subsegments": sum(r["quality"]["subsegments"] for r in evaluated),
            "amplitude_rejections": sum(c["amplitude_rejections"] for c in channels),
            "flatline_rejections": sum(c["flatline_rejections"] for c in channels),
            "single_channel_window_passes": sum(r["single_channel_counterfactual_passes"][j] for r in evaluated)})
    return summary


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("session", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = diagnose(args.session)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False), encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "windows"}, indent=2))

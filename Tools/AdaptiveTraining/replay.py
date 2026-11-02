"""Replay controller requests and independently recompute saved feature windows."""
import argparse
import json
import math
from pathlib import Path
from controller import BaselineAdaptiveController
from features import extract_features


def replay(folder):
    folder=Path(folder);decisions=windows=0
    path=folder/"controller_decisions.jsonl"
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            record=json.loads(line)
            result=BaselineAdaptiveController(record["policy"]).decide(record["packet"],record["features"],record["calibration"])
            if result!=record["decision"]:raise AssertionError(f"Decision mismatch at {decisions}")
            decisions+=1
    path=folder/"feature_windows.jsonl"
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            r=json.loads(line);c=r["stream"]
            values,quality=extract_features(c["kind"],r["timestamps"],r["samples"],c["sample_rate"],c["window_seconds"],c["amplitude_limit"])
            if quality!=r["quality"] or len(values)!=len(r["features"]):raise AssertionError("Feature quality mismatch")
            for actual,expected in zip(values,r["features"]):
                if actual["name"]!=expected["name"] or not math.isclose(actual["value"],expected["value"],rel_tol=1e-10,abs_tol=1e-10):raise AssertionError("Feature value mismatch")
            windows+=1
    if not decisions and not windows:raise ValueError("No replayable service records found")
    return {"decision_replays":decisions,"feature_windows_recomputed":windows,"status":"pass"}


if __name__=="__main__":
    p=argparse.ArgumentParser();p.add_argument("folder");a=p.parse_args();print(json.dumps(replay(a.folder),indent=2))

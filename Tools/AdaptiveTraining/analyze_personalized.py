"""Descriptive Random/Bioadaptive exports, preserving failed and incomplete sessions."""
from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
import statistics

from personalized_signals import movie_fraction


def read_csv(path):
    if not path.exists(): return []
    with path.open(encoding="utf-8-sig", newline="") as f: return list(csv.DictReader(f))


def performance(rows):
    clean = [r for r in rows if r["scored"] == "1" and r["interrupted"] != "1" and r["aborted"] != "1" and r["transition"] != "1"]
    groups = [[r for r in clean if r["expected_match"] == label] for label in ("0", "1")]
    ba = .5 * sum(sum(r["correct"] == "1" for r in g) / len(g) for g in groups) if all(groups) else None
    rt = [float(r["rt_s"]) for r in clean if r["correct"] == "1" and r["responded"] == "1"]
    return {"recorded_stimuli": len(rows), "eligible": len(clean), "balanced_accuracy": ba,
            "median_correct_rt_seconds": statistics.median(rt) if rt else None,
            "omissions": sum(r["responded"] != "1" for r in clean),
            "excluded": len(rows) - len(clean)}


def summarize(folder):
    folder = Path(folder); trials = read_csv(folder / "trials.csv"); media = read_csv(folder / "media_events.csv")
    config_path = folder / "adaptive-training-config.json"
    config = json.loads(config_path.read_text()) if config_path.exists() else {}
    model_path = folder / "personal-model-report.json"
    model = json.loads(model_path.read_text()) if model_path.exists() else {"status": "missing_model_report", "admitted": False}
    metadata = json.loads((folder / "session.json").read_text())
    events = [json.loads(line) for line in (folder / "events.jsonl").read_text().splitlines()]
    mount_interruptions = [e for e in events if e["type"] == "personal_mount_interrupted"]
    result = {"session": folder.name, "session_type": metadata["sessionType"], "participant": metadata["participantCode"],
              "session_status": metadata["status"], "model": model, "counterbalance_group": config.get("counterbalanceGroup"),
              "questionnaire_administration": config.get("questionnaireAdministration", "historical_external_forms"),
              "mount_interruptions": mount_interruptions,
              "model_effectively_admitted": bool(model.get("admitted") and not mount_interruptions),
              "training": {}, "interpretation": "Descriptive only. Overlapping windows are not independent observations; exposure and order must be considered."}
    for stage in ("random", "bioadaptive"):
        rows = [r for r in trials if r["training_stage"] == stage]
        exposures = [e for e in media if e["stage"] == stage]
        pending = {}; intervals = []; incomplete = 0
        for e in exposures:
            key = (e["block"], e["media_id"], e["planned_s"])
            if e["status"] == "started": pending[key] = float(e["actual_s"])
            elif e["status"] in ("ended", "failure", "interrupted") and key in pending:
                intervals.append((pending.pop(key), float(e["actual_s"])))
        incomplete = len(pending)
        movie_rows = [r for r in rows if movie_fraction(float(r["planned_onset_s"]), float(r["planned_onset_s"]) + 4, intervals) > 0]
        switches = [e for e in events if e["type"] == "adaptive_level_changed" and "stage=" + stage in e["detail"]]
        segments = []
        for i in range(4):
            selected = [r for r in rows if i * 24 <= int(r["phase_trial"]) < (i + 1) * 24]
            segments.append({"segment": i + 1, **performance(selected), "levels": sorted({int(r["level"]) for r in selected})})
        bounds = (min(float(r["planned_onset_s"]) for r in rows), max(float(r["trial_end_s"]) for r in rows)) if rows else (0, 0)
        exposure_seconds = movie_fraction(*bounds, intervals) * (bounds[1] - bounds[0]) if bounds[1] > bounds[0] else 0
        result["training"][stage] = {**performance(rows), "complete_96_stimuli": len(rows) == 96,
            "segments": segments, "movie_overlap_performance": performance(movie_rows), "actual_movie_seconds": exposure_seconds,
            "movie_starts": sum(e["status"] == "started" for e in exposures), "unclosed_media_intervals": incomplete,
            "level_seconds_from_trials": {str(l): sum(float(r["trial_end_s"]) - float(r["planned_onset_s"]) for r in rows if int(r["level"]) == l and r["interrupted"] != "1") for l in (0, 1)},
            "switch_count": len(switches), "switches": switches,
            "label": "mount_interrupted_requires_restart" if mount_interruptions else
                "behavioral_fallback_or_engineering" if stage == "bioadaptive" and not model.get("admitted") else stage}
    return result


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("session", type=Path); parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(); args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(summarize(args.session), indent=2, allow_nan=False), encoding="utf-8")
    print(args.output)


if __name__ == "__main__": main()

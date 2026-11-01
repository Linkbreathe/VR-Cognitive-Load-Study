"""Recompute formal block metrics from trial-level CSV using Python standard library."""
import argparse
import csv
import json
import statistics
from collections import defaultdict
from pathlib import Path


def summarize(session: Path):
    metadata = json.loads((session / "session.json").read_text(encoding="utf-8-sig"))
    with (session / "trials.csv").open(encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))
    groups = defaultdict(list)
    for row in rows:
        if row["scored"] == "1":
            groups[row["block"]].append(row)
    output = []
    for block, all_rows in groups.items():
        usable = [r for r in all_rows if r["aborted"] == "0"]
        clean = [r for r in usable if r["interrupted"] == "0"]
        def metrics(trials):
            def recall(label):
                subset = [r for r in trials if r["expected_match"] == label]
                # Omissions are incorrect in BOTH classes; never count an omission as a correct rejection.
                return sum(r["correct"] == "1" for r in subset) / len(subset) if subset else None
            hit, reject = recall("1"), recall("0")
            rt = [float(r["rt_s"]) for r in trials if r["responded"] == "1" and r["correct"] == "1"]
            return {
                "n": len(trials),
                "accuracy": sum(r["correct"] == "1" for r in trials) / len(trials) if trials else None,
                "balanced_accuracy": (hit + reject) / 2 if hit is not None and reject is not None else None,
                "match_recall": hit, "nonmatch_recall": reject,
                "timeouts": sum(r["timeout"] == "1" for r in trials),
                "mean_correct_rt_s": statistics.mean(rt) if rt else None,
                "median_correct_rt_s": statistics.median(rt) if rt else None,
            }
        output.append({"block": int(block), "mode": all_rows[0]["mode"], "recorded": len(all_rows),
                       "aborted": sum(r["aborted"] == "1" for r in all_rows),
                       "interrupted": sum(r["interrupted"] == "1" for r in all_rows),
                       "all_completed_trials": metrics(usable), "clean_trials": metrics(clean)})
    events = [json.loads(line) for line in (session / "events.jsonl").read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    onsets = [e["deviation"] for e in events if e["type"] == "stimulus_on"]
    result = {"session": metadata["sessionId"], "status": metadata["status"], "blocks": output,
              "timing": {"onset_count": len(onsets), "maximum_onset_delay_s": max(onsets) if onsets else None,
                         "frame_gap_events": sum(e["type"] == "frame_gap" for e in events)},
              "note": "Software timestamps, not measured photon onset. clean_trials excludes interrupted/aborted trials. RT includes correct responses only."}
    (session / "summary.json").write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("session", type=Path, help="Directory containing session.json and trials.csv")
    args = parser.parse_args()
    print(json.dumps(summarize(args.session), indent=2, ensure_ascii=False))

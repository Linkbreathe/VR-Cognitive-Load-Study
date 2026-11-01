"""Independent export-level checks for the five synthetic sessions."""
import csv
import json
from pathlib import Path
from summarize_session import summarize

root = Path(__file__).resolve().parents[1]
sessions = sorted((root / "Evidence/SyntheticSessions/automated").glob("*/session.json"))[-5:]
assert len(sessions) == 5
checks = 0
for metadata_path in sessions:
    folder = metadata_path.parent
    metadata = json.loads(metadata_path.read_text())
    blocks = {}
    for name in ("trials.csv", "practice_trials.csv"):
        with (folder / name).open(newline="") as f:
            rows = list(csv.DictReader(f))
        for row in rows:
            blocks.setdefault(int(row["block"]), []).append(row)
    assert sum(map(len, blocks.values())) == 88
    for block, rows in blocks.items():
        rows.sort(key=lambda r: int(r["trial"]))
        for i, row in enumerate(rows):
            assert i == int(row["trial"])
            assert float(row["actual_onset_s"]) >= float(row["planned_onset_s"]) - 1e-8
            if i >= 2:
                expected = row["color"] == (str(metadata["config"]["targetColor"]) if row["mode"] == "0" else rows[i-2]["color"])
                assert expected == (row["expected_match"] == "1")
                assert (row["correct"] == "1") == (row["responded"] == "1" and row["response_match"] == row["expected_match"])
                assert (row["timeout"] == "1") == (row["responded"] == "0")
                if row["responded"] == "1":
                    assert abs(float(row["rt_s"]) - (float(row["response_time_s"]) - float(row["actual_onset_s"]))) < 1e-8
                checks += 1
        if block >= 2:
            assert len(rows) == 32
            assert sum(r["expected_match"] == "1" for r in rows[2:]) == 10
        events = [json.loads(line) for line in (folder / "events.jsonl").read_text().splitlines()]
        assert len([e for e in events if e["type"] == "stimulus_on" and e["block"] == block]) == len(rows)
    report = summarize(folder)
    for b in report["blocks"]:
        assert b["all_completed_trials"]["n"] == 30
        assert b["all_completed_trials"]["timeouts"] == 4
text = f"PASS: 5 saved synthetic sessions reopened; {checks} scored rows independently checked; answers, omission scoring, RT, event joins, block counts and summary recomputation verified.\n"
(root / "Evidence/export-validation.txt").write_text(text)
print(text)

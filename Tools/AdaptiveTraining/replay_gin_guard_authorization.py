"""Replay formal RF authorization in memory, with missing and resuming gaze inputs."""

if __name__ == "__main__":
    raise SystemExit("Retired P007/Gin workflow. Use StartEvidenceConsole.cmd with the Mindtooth device.")

import csv
import json
import tempfile
from pathlib import Path
from replay_gin_mixed_behavior import SERVICE, ROOT, records, simulate_answers, fit_candidate, replay

def main():
    output = ROOT/"Evidence/GinGuardAuthorizationMock"
    output.mkdir(exist_ok=True)
    clocks, rows = [], {}
    for record in records(SERVICE/"telemetry.jsonl"):
        packet = record["packet"]
        assert not packet.get("guardGaze")
        clocks.append((packet["now"],packet["clockOffset"],packet["clockUncertainty"]))
        for row in packet["trials"]:
            if row["trainingStage"] == "bioadaptive":
                rows[row["phaseTrial"]] = row
    answers = simulate_answers([rows[i] for i in range(96)])
    protocol = json.loads((SERVICE/"personal_protocol.json").read_text())
    model, report = fit_candidate(protocol["policy"])
    combined = []
    audit = []
    with tempfile.TemporaryDirectory() as tmp:
        for label, after in (("archived_missing_gaze",None),("task_gaze_from_trial_31",30)):
            for stage in ("layer_a","layer_b"):
                folder = Path(tmp)/(label+stage)
                group = replay(stage,"mock_admitted",answers,model,report,sorted(clocks),
                    packet["environmentRevision"],protocol,folder,gaze_after=after)
                for r in records(folder/"evidence_guard_audit.jsonl"):
                    if r["record"] in ("host_decision","forward","reverse","relation","view_check"):
                        audit.append(dict(scenario=label,condition=stage,**r))
                assert all(d["basis"]=="personalized_rf" for d in group)
                combined.extend(dict(scenario=label,**d) for d in group)
    with (output/"decisions.csv").open("w",newline="") as f:
        writer = csv.DictWriter(f,fieldnames=combined[0])
        writer.writeheader()
        writer.writerows(combined)
    with (output/"review.jsonl").open("w") as f:
        for record in audit:
            f.write(json.dumps(record)+"\n")
    assert not report["admitted"]
    print("Saved", len(combined), "decision checkpoints")

if __name__=="__main__":
    main()

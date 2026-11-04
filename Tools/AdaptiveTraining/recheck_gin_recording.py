"""Offline chronological reprocessing of a prior engineering run; sends no device actions."""
import copy
import json
from collections import Counter
from pathlib import Path

from personalized_runtime import PersonalSession
from test_session_store import TestSessionStore, read, write


def main():
    evidence = Path("Evidence/SignalQualityV3")
    summary = read(evidence / "trained-model.json")
    source = Path("D:/ExperimentData/PatentDemo/SIM_GIN_TEST/sessions/20261103T183828_c4d88eae4bbc/service")
    output = evidence / "offline-runtime"
    if output.exists():
        raise ValueError("Preserve previous evidence; select a fresh output folder")
    policy = read(Path(summary["policy"]))
    store = TestSessionStore(evidence / "offline-registry", policy)
    store.streams = read(Path(summary["mapping"]))["streams"]
    saved, manifest = store.load_model(summary["storageFolder"], "SIM_GIN_TEST", summary["modelId"], True)
    timeline = []
    for name, kind in (("raw_signals.jsonl", "signal"), ("telemetry.jsonl", "packet")):
        with (source / name).open(encoding="utf-8") as stream:
            for line in stream:
                row = json.loads(line)
                timeline.append((row["received_monotonic"], kind, row))
    timeline.sort(key=lambda item: item[0])
    first = copy.deepcopy(next(row["packet"] for _, kind, row in timeline if kind == "packet"))
    first["sessionId"] = "offline_v3_previous_engineering_run"
    first["sessionType"] = "synthetic"
    first["config"]["operatorManaged"] = False
    session = PersonalSession(first, output, policy)
    session.model = saved["model"]
    session.report = saved["report"]
    session.candidate_control = True  # Offline engineering replay; outputs are logged only.
    session.model_status = "engineering_candidate_loaded"
    for _, kind, row in timeline:
        if kind == "signal":
            session.ingest(row["stream"], row["corrected_monotonic"], row["samples"], row["source_id"])
        else:
            packet = copy.deepcopy(row["packet"])
            packet.update(sessionId=first["sessionId"], sessionType="synthetic", config=first["config"])
            session.receive(packet, True)
            if any(trial["aborted"] for trial in packet["trials"]):
                break
    predictions = list(session.predictions)
    scores = [p["score"] for p in predictions if p["score"] is not None]
    decisions = list(session.decisions.values())
    result = dict(mode="offline_reprocessing_only_no_Unity_or_device_actions", sourceSession=source.parent.name,
        modelId=manifest["modelId"], featureRevision=manifest["featureRevision"],
        windows=len(predictions), validPredictions=len(scores),
        reasons=dict(Counter(p["reason"] for p in predictions)),
        scoreRange=[min(scores), max(scores)] if scores else None,
        decisions=decisions, allPhysiologicalAdmissionFalse=all(not d.get("physiologicalEligible") for d in decisions))
    write(evidence / "offline-runtime-result.json", result)
    print(json.dumps({k:v for k,v in result.items() if k != "decisions"}, indent=2))
    print("Offline decisions:", [(d.get("action"), d.get("score"), d.get("reason")) for d in decisions])


if __name__ == "__main__":
    main()

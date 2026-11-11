"""Publish a separately identified engineering model from a verified calibration replay."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Tools" / "AdaptiveTraining"))
import workload_rf
from test_session_store import TestSessionStore, code, digest, read, write


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--replay", type=Path, required=True)
    parser.add_argument("--suffix", default="bands-v1")
    args = parser.parse_args()
    replay = args.replay.resolve()
    summary = read(replay / "summary.json")
    original = Path(summary["source"])
    record = read(original / "test-session.json")
    report = read(replay / "service" / "load_reference.json")
    if record["config"]["sessionType"] != "engineering" or not summary["sourceSnapshotPreserved"]:
        raise ValueError("Verified engineering calibration replay required")
    if report["status"] != "load_reference_ready":
        raise ValueError("Replay did not finish workload model training")
    if any(report.get(k) != v for k,v in workload_rf.window_contract().items()):
        raise ValueError("Replay uses a different training contract")
    participant, sid = code(record["participantCode"]), code(record["sessionId"])
    model_id = code(sid + "-" + args.suffix)
    if report["participant"] != participant or report["session"] != sid:
        raise ValueError("Source identity mismatch")
    source_names = ("raw_signals.jsonl", "load_reference.json", "load_windows.jsonl")
    for name in source_names:
        key = str(Path("service") / name)
        if digest(original / key) != summary["sourceSnapshotHashes"][key]:
            raise ValueError("Original calibration content changed")
    provenance = dict(kind="recorded_calibration_reanalysis", revision=workload_rf.BAND_SELECTION_REVISION,
                      aucPolicyRevision=report.get("aucPolicyRevision"),
                      originalSession=sid, originalStatus=read(original / "service" / "load_reference.json")["status"],
                      replaySummary=str(replay / "summary.json"), synthetic=False,
                      freshAcquisition=False, sourceSnapshotHashes=summary["sourceSnapshotHashes"])
    report = dict(report, reanalysis=provenance)
    policy = read(original / "service" / "personal_protocol.json")["policy"]
    root = Path(record["storageFolder"])
    destination = root / participant / "models" / model_id
    if destination.exists():
        raise FileExistsError("Never overwrite an existing model")
    staged_root = replay / "staged-registry"
    staged = staged_root / participant / "models" / model_id
    staged.mkdir(parents=True, exist_ok=False)
    shutil.copy2(replay / "service" / "workload_rf.joblib", staged / "workload_rf.joblib")
    write(staged / "reference.json", report)
    write(staged / "manifest.json", dict(modelId=model_id, participantCode=participant, sourceSession=sid,
          createdUtc=datetime.now(timezone.utc).isoformat(), admitted=False,
          reasons=["engineering_load_observation", "recorded_calibration_reanalysis"],
          sha256=digest(staged / "reference.json"), features=report["features"],
          featureRevision=report["featureRevision"], workloadWindowContract=workload_rf.window_contract(),
          bandProfile=report["bandProfile"], reanalysis=provenance,
          aucPolicyRevision=report.get("aucPolicyRevision"), meanHeldAuc=report.get("meanHeldAuc"),
          signalContract=record["signalContract"], policy=policy))
    store = TestSessionStore(replay / "validation-registry", policy)
    store.streams = record["signalContract"]
    store.load_model(staged_root, participant, model_id, False)
    shutil.copytree(staged, destination)
    store.load_model(root, participant, model_id, False)
    result = dict(modelId=model_id, folder=str(destination), sourceSession=sid,
                  status="saved_and_reload_verified", originalResultPreserved=True,
                  meanHeldAuc=report["meanHeldAuc"], bandMode=report["bandProfile"]["mode"])
    write(replay / "published-model.json", result)
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()

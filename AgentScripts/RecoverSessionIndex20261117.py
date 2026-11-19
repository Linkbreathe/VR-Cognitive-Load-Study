"""Explicit one-time recovery of the all-zero local engineering registry."""
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / "UserSettings/TestSessions/mindtooth-eeg250-v1/port-8765"
INDEX = STATE / "test-session-index.json"
EVIDENCE = ROOT / "Evidence/LslIndexRecovery-20261117"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    with socket.socket() as probe:
        probe.settimeout(2)
        if probe.connect_ex(("127.0.0.1", 8765)) == 0:
            raise RuntimeError("Stop the receiving service before registry recovery")
    original = INDEX.read_bytes()
    if len(original) != 802051 or any(original):
        raise RuntimeError("Registry differs from the diagnosed all-zero file; inspect again")
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    backup = EVIDENCE / "test-session-index.corrupt.bin"
    with backup.open("xb") as stream:
        stream.write(original)
        stream.flush()
        os.fsync(stream.fileno())
    shutil.copy2(STATE / "test-session-settings.json", EVIDENCE / "test-session-settings.original.json")
    settings = json.loads((STATE / "test-session-settings.json").read_text(encoding="utf-8-sig"))
    storage = Path(settings["storageFolder"]).resolve()
    records, sources = {}, []
    now = datetime.now(timezone.utc).isoformat()
    for path in sorted(storage.glob("*/sessions/*/test-session.json"), key=lambda p: p.parent.name):
        data = path.read_bytes()
        record = json.loads(data.decode("utf-8-sig"))
        sid = record["sessionId"]
        if (sid != path.parent.name or record["participantCode"] != path.parents[2].name
                or Path(record["folder"]).resolve() != path.parent.resolve()
                or Path(record["storageFolder"]).resolve() != storage
                or record["config"].get("sessionType") not in ("engineering", "editor-test")
                or sid in records):
            raise ValueError(f"Invalid or duplicate source identity: {path}")
        record["registryRecovery"] = {
            "revision": "lsl-session-index-recovery-v1", "recoveredUtc": now,
            "source": str(path), "sha256": sha(data), "sourceState": record["state"],
            "limitation": "Recovered from assignment snapshot; final state, archive and training summaries may be missing. Original files are retained.",
        }
        record["state"] = "service_restarted"
        record["revoked"] = True
        records[sid] = record
        sources.append({"path": str(path), "sha256": sha(data), "sessionId": sid})
    if len(records) != 133:
        raise RuntimeError("Source inventory changed; inspect before replacing the registry")
    payload = json.dumps(records, indent=2, allow_nan=False).encode("utf-8")
    candidate = EVIDENCE / "test-session-index.recovered.json"
    candidate.write_bytes(payload)
    for source in sources:
        if sha(Path(source["path"]).read_bytes()) != source["sha256"]:
            raise RuntimeError("A source changed during recovery")
    if INDEX.read_bytes() != original:
        raise RuntimeError("Registry changed during recovery")
    temp = INDEX.with_suffix(".recovery.tmp")
    with temp.open("xb") as stream:
        stream.write(payload)
        stream.flush()
        os.fsync(stream.fileno())
    temp.replace(INDEX)
    assert json.loads(INDEX.read_text(encoding="utf-8")) == records
    report = {"revision": "lsl-session-index-recovery-v1", "utc": now,
              "originalBytes": len(original), "originalSha256": sha(original),
              "backup": str(backup), "recoveredRecords": len(records),
              "recoveredSha256": sha(payload), "sources": sources,
              "sourceFilesUnchanged": True, "oldAssignmentsRevoked": True}
    (EVIDENCE / "recovery-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "sources"}))


if __name__ == "__main__":
    main()

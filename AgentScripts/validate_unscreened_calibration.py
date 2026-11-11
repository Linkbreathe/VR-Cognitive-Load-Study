"""Replay calibration into isolated engineering evidence; never publish or edit source data."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Tools" / "AdaptiveTraining"))
from personalized_runtime import PersonalSession


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    if source == output or source in output.parents or output.exists():
        raise ValueError("Use a new evidence folder outside the source session")
    snapshot = {str(p.relative_to(source)): p.read_bytes() for p in source.rglob("*") if p.is_file()}
    before = {name: hashlib.sha256(data).hexdigest() for name, data in snapshot.items()}
    read = lambda name: [json.loads(s) for s in snapshot[str(Path("service") / name)].decode("utf-8").splitlines()]
    telemetry, signals = read("telemetry.jsonl"), read("raw_signals.jsonl")
    policy = json.loads(snapshot[str(Path("service") / "personal_protocol.json")])["policy"]
    class NoPublish:
        def publish_reference(self, session):
            pass
    if telemetry[0]["packet"]["config"].get("testSessionMode") != "calibration":
        raise ValueError("This diagnostic only supports workload calibration")
    session = PersonalSession(telemetry[0]["packet"], output / "service", policy, NoPublish())
    ordered = sorted([(r["received_monotonic"], 0, i) for i,r in enumerate(signals)] +
                     [(r["received_monotonic"], 1, i) for i,r in enumerate(telemetry)])
    for _, kind, i in ordered:
        if kind == 0:
            r = signals[i]
            session.ingest(r["stream"], r["corrected_monotonic"], r["samples"], r["source_id"])
        else:
            session.receive(telemetry[i]["packet"])
    spectra = {}
    for stage, windows in session.load_rows.items():
        spectra[stage] = np.stack([v if v is not None else np.full((5,16,8), np.nan) for v in windows])
    np.savez_compressed(output / "training-spectra.npz", **spectra)
    # A still-connected Console may append telemetry after completion; compare the captured prefix.
    after = {name: (source / name).read_bytes() for name in snapshot}
    preserved = all(after[name].startswith(data) for name,data in snapshot.items())
    appended = [name for name,data in snapshot.items() if len(after[name]) != len(data)]
    summary = dict(source=str(source), output=str(output), purpose="isolated engineering replay",
                   productionModelPublished=False, sourceSnapshotPreserved=preserved,
                   sourceAppendOnlyFiles=appended, sourceSnapshotHashes=before, report=session.report)
    (output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    if not preserved:
        raise AssertionError("Captured source content changed during replay")


if __name__ == "__main__":
    main()

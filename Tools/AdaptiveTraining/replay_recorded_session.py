"""Offline engineering replay of a recorded operator-managed session through the current service code.

The original Unity packets and EEG sample blocks are fed in their original receive order. This is an
open-loop replay: recorded task behavior, playback and receipts are reused, so results describe the
current code only while its delivered commands match the original ones. The tool writes a per-request
comparison and flags the first divergence. Original session folders are only read.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path

from service import StudyService
from test_session_store import read, write

REVISION = "recorded-session-replay-v1"


def rows(path):
    if not path.exists():
        return []
    with path.open(encoding="utf-8") as stream:
        return [json.loads(line) for line in stream if line.strip()]


def decision_requests(service_folder):
    """Request IDs and packet times of the original decision calls."""
    found = {}
    for row in rows(service_folder / "personal_decisions.jsonl"):
        decision = row.get("decision", row)
        found[decision["requestId"]] = None
    for row in rows(service_folder / "comparison_deliveries.jsonl"):
        found[row["decision"]["requestId"]] = row["time"]
    return found


def original_outcomes(service_folder):
    base = {}
    for row in rows(service_folder / "personal_decisions.jsonl"):
        decision = row.get("decision", row)
        base[decision["requestId"]] = decision
    delivered = {row["decision"]["requestId"]: row["decision"] for row in rows(service_folder / "comparison_deliveries.jsonl")}
    return base, delivered


def compare(original_base, original_delivered, replay_base, replay_delivered):
    keys = ("action", "reason", "basis", "score", "behaviorTrigger", "modelTrigger")
    result, first = [], None
    for request in sorted(set(original_base) | set(original_delivered), key=lambda r: (r.split(":")[-2], int(r.split(":")[-1]))):
        a, b = original_base.get(request, {}), replay_base.get(request, {})
        c, d = original_delivered.get(request, {}), replay_delivered.get(request, {})
        base_diff = {k: [a.get(k), b.get(k)] for k in keys if a.get(k) != b.get(k)}
        delivered_diff = {k: [c.get(k), d.get(k)] for k in ("action", "guardVerdict", "guardReason") if c.get(k) != d.get(k)}
        row = dict(request=request, baseDifferences=base_diff, deliveredDifferences=delivered_diff,
                   originalDelivered=c.get("action"), replayDelivered=d.get("action"))
        result.append(row)
        if first is None and (row["originalDelivered"] != row["replayDelivered"]):
            first = request
    return result, first


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--session", type=Path, required=True, help="Recorded session folder containing service/ and test-session.json")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--policy", type=Path, help="Personalized policy file used by the original service")
    parser.add_argument("--model-root", type=Path, help="Storage root to load the saved model from instead of the recorded one")
    parser.add_argument("--feature-reference", type=Path,
                        help="load_reference.json from a replayed calibration; its forest hash must match the saved model")
    args = parser.parse_args()
    session, output = args.session.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    source = session / "service"
    record = read(session / "test-session.json")
    sid = record["sessionId"]
    # Never publish into the original storage root; a replayed calibration writes its model here.
    record.update(folder=str(output / "session"), state="claimed", storageFolder=str(output / "storage"))
    registry = output / "registry"
    registry.mkdir()
    policy = read(args.policy) if args.policy else None
    service = StudyService(output / "service-root", personalized_policy=policy, test_state_dir=registry)
    # Register after startup: the store revokes in-flight records it finds when it starts.
    service.test_store.records[sid] = record
    signals = rows(source / "raw_signals.jsonl")
    if not signals:
        raise ValueError("Recording has no raw signal blocks")
    service.test_store.streams = [signals[0]["stream"]]
    if args.model_root:
        load = service.test_store.load_model
        root = str(args.model_root.resolve())
        service.test_store.load_model = lambda folder, participant, model, allow: load(root, participant, model, allow)
    reference = None
    if args.feature_reference:
        replayed = read(args.feature_reference)
        config = record["config"]
        saved = read(Path(config["testStorageFolder"]) / record["participantCode"] / "models" / config["testModelId"] / "reference.json")
        if replayed.get("forestSha256") != saved.get("forestSha256") or "featureReference" not in replayed:
            raise ValueError("Feature reference does not come from the saved model's calibration")
        reference = replayed["featureReference"]
    telemetry = rows(source / "telemetry.jsonl")
    requests = decision_requests(source)
    events = sorted([(t["received_monotonic"], 0, i) for i, t in enumerate(telemetry)] +
                    [(s["received_monotonic"], 1, i) for i, s in enumerate(signals)])
    decided = set()
    for _, kind, index in events:
        if kind == 1:
            s = signals[index]
            service.record_signal(s["stream"], s["raw_lsl_timestamps"], s["corrected_monotonic"], s["samples"],
                                  s["lsl_time_correction"], s["source_id"])
            continue
        packet = telemetry[index]["packet"]
        request = packet.get("requestId")
        decide = (request in requests and request not in decided and
                  (requests[request] is None or abs(packet["now"] - requests[request]) < 1e-6))
        if decide:
            decided.add(request)
        session_state = service.sessions.get(sid)
        if reference and session_state and "personal" in session_state and session_state["personal"].feature_reference is None:
            session_state["personal"].feature_reference = reference
        service.receive(packet, decide=decide)
        session_state = service.sessions.get(sid)
        if reference and session_state and "personal" in session_state and session_state["personal"].feature_reference is None:
            session_state["personal"].feature_reference = reference
        # A session can never deliver after its own model wait finishes training synchronously.
    state = service.sessions.get(sid, {})
    personal = state.get("personal")
    if personal is not None and getattr(personal, "fit_thread", None):
        personal.fit_thread.join()
    replay_folder = output / "session" / "service"
    original_base, original_delivered = original_outcomes(source)
    replay_base, replay_delivered = original_outcomes(replay_folder)
    comparison, first = compare(original_base, original_delivered, replay_base, replay_delivered)
    summary = dict(revision=REVISION, createdUtc=datetime.now(timezone.utc).isoformat(), session=sid,
                   source=str(session), output=str(output), packets=len(telemetry), signalBlocks=len(signals),
                   decisionRequests=len(requests), replayedDecisions=len(decided),
                   deliveredMatches=sum(r["originalDelivered"] == r["replayDelivered"] for r in comparison),
                   baseExactMatches=sum(not r["baseDifferences"] for r in comparison),
                   firstDeliveredDivergence=first, modelRoot=str(args.model_root) if args.model_root else None,
                   featureReference=str(args.feature_reference) if args.feature_reference else None,
                   method="Open loop: recorded Unity packets and EEG in original receive order. Valid only up to the first delivered divergence.")
    write(output / "replay-summary.json", dict(summary, comparison=comparison))
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()

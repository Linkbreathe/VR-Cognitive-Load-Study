"""Closed-loop engineering simulation over a recorded comparison session.

The recorded Unity packets and EEG blocks are reused in their original receive order: responses, timing
and EEG stay exactly as recorded. Only the video state follows the current code's own decisions. Recorded
level changes, media and execution events are removed and regenerated at each decision's boundary, and
trial/packet levels are rewritten. Limitation: the participant's real responses and EEG were produced
under the original video schedule, so this shows how the rules would switch, not how a person would react.
Original session folders are only read.
"""
import argparse
import copy
from datetime import datetime, timezone
import json
from pathlib import Path

from comparison_report import report
from replay_recorded_session import rows, decision_requests, original_outcomes
from service import StudyService
from test_session_store import read, write

REVISION = "recorded-closed-loop-v1"
LAYERS = ("layer_a", "layer_b")
VIDEO_EVENTS = ("adaptive_level_changed", "personal_execution", "adaptive_media_")


class Video:
    """Simulated video state for one round, with generated Unity-style events."""

    def __init__(self, stage, scene):
        self.stage, self.scene, self.level, self.changes, self.media, self.exposures = stage, scene, 0, [], None, 0

    def level_at(self, when):
        level = 0
        for time, value in self.changes:
            if time <= when:
                level = value
        return level

    def detail(self):
        return json.dumps(dict(mediaId=f"sim-{self.stage}-exposure-{self.exposures}", modality="audiovisual", stage=self.stage,
                               block=0, level=1, previousExposures=self.exposures - 1, onset=0.0, duration=300.0))

    def execute(self, request, action, when):
        """Events Unity would emit at the boundary after a decision reply."""
        previous = self.level
        target = 1 if action == "ON" else 0 if action == "OFF" else previous
        out = [dict(type="personal_execution", actual=when, planned=when,
                    detail=f"request={request};action={action};level={target};previous_level={previous};scene={self.scene}")]
        if target != previous:
            self.level = target
            self.changes.append((when, target))
            out.append(dict(type="adaptive_level_changed", actual=when, planned=when,
                            detail=f"level={target};stage={self.stage};previous_level={previous}"))
            if target == 1:
                self.exposures += 1
                self.media = self.detail()
                out.append(dict(type="adaptive_media_started", actual=when, planned=when,
                                detail=self.media + f";first_decoded_frame;not_photon_onset;audio_playing=true;scene={self.scene}"))
            else:
                out.extend(self.stop(when))
        return out

    def stop(self, when):
        if not self.media:
            return []
        media, self.media = self.media, None
        return [dict(type="adaptive_media_ended", actual=when, planned=when,
                     detail=media + f";automatic_off;cursor_retained;audio_playing=true;scene={self.scene}")]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--session", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--policy", type=Path)
    parser.add_argument("--feature-reference", type=Path)
    args = parser.parse_args()
    session, output = args.session.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    source = session / "service"
    record = read(session / "test-session.json")
    sid = record["sessionId"]
    record.update(folder=str(output / "session"), state="claimed", storageFolder=str(output / "storage"))
    (output / "registry").mkdir()
    service = StudyService(output / "service-root", personalized_policy=read(args.policy) if args.policy else None,
                           test_state_dir=output / "registry")
    service.test_store.records[sid] = record
    reference = None
    if args.feature_reference:
        replayed, config = read(args.feature_reference), record["config"]
        saved = read(Path(config["testStorageFolder"]) / record["participantCode"] / "models" / config["testModelId"] / "reference.json")
        if replayed.get("forestSha256") != saved.get("forestSha256"):
            raise ValueError("Feature reference does not come from the saved model's calibration")
        reference = replayed["featureReference"]
    signals = rows(source / "raw_signals.jsonl")
    service.test_store.streams = [signals[0]["stream"]]
    telemetry = rows(source / "telemetry.jsonl")
    requests = decision_requests(source)
    events = sorted([(t["received_monotonic"], 0, i) for i, t in enumerate(telemetry)] +
                    [(s["received_monotonic"], 1, i) for i, s in enumerate(signals)])
    videos, pending, decided, replies, current = {}, [], set(), {}, None
    for _, kind, index in events:
        if kind == 1:
            s = signals[index]
            service.record_signal(s["stream"], s["raw_lsl_timestamps"], s["corrected_monotonic"], s["samples"],
                                  s["lsl_time_correction"], s["source_id"])
            continue
        p = copy.deepcopy(telemetry[index]["packet"])
        stage, scene = p["stage"], p.get("environmentRevision", "scene")
        if current in videos and stage != current:
            pending.extend(videos[current].stop(p["now"]))  # Unity ends playback with the round.
        current = stage
        if stage in LAYERS and stage not in videos:
            videos[stage] = Video(stage, scene)
        video = videos.get(stage)
        due = [e for e in pending if e["actual"] <= p["now"]]
        pending = [e for e in pending if e["actual"] > p["now"]]
        p["events"] = [e for e in p.get("events", []) if not e["type"].startswith(VIDEO_EVENTS)] + due
        if video:
            p["level"] = video.level_at(p["now"])
        # Rewrite each trial by its own round: packets of a later stage can still carry earlier trials.
        for t in p.get("trials", []) + ([p["closedBehavior"]] if p.get("closedBehavior") else []):
            if t.get("trainingStage") in videos:
                t["level"] = videos[t["trainingStage"]].level_at(t["actualOnset"])
        if video:
            for g in p.get("guardGaze", []):
                g["level"] = video.level_at(g["start"])
                g["movieVisible"] = g["level"] == 1
        request = p.get("requestId")
        decide = (request in requests and request not in decided and
                  (requests[request] is None or abs(p["now"] - requests[request]) < 1e-6))
        state = service.sessions.get(sid)
        if reference and state and "personal" in state and state["personal"].feature_reference is None:
            state["personal"].feature_reference = reference
        reply = service.receive(p, decide=decide)
        state = service.sessions.get(sid)
        if reference and state and "personal" in state and state["personal"].feature_reference is None:
            state["personal"].feature_reference = reference
        if decide:
            decided.add(request)
            action = (reply or {}).get("decision", {}).get("action", "HOLD")
            replies[request] = action
            if video:
                pending.extend(video.execute(request, action, p["decisionDeadline"]))
    folder = output / "session" / "service"
    original_base, original_delivered = original_outcomes(source)
    sim_base, sim_delivered = original_outcomes(folder)
    comparison = report(folder)
    rounds = {}
    for stage in LAYERS:
        keys = sorted((k for k in original_delivered if f":{stage}:" in k), key=lambda k: int(k.split(":")[-1]))
        first = next((k for k in keys if original_delivered[k]["action"] != sim_delivered.get(k, {}).get("action")), None)
        checks = [c for c in comparison["checkpoints"] if c["stage"] == stage]
        rounds[stage] = dict(
            summary=comparison["summary"].get(stage),
            originalCommands=[(original_delivered[k].get("lastTrial", None), original_delivered[k]["action"]) for k in keys],
            simulatedCommands=[(sim_delivered.get(k, {}).get("lastTrial"), sim_delivered.get(k, {}).get("action")) for k in keys],
            firstDifferentCommand=first,
            tests=[dict(trial=c["trial"], reason=c["review"].get("reason"), delivered=c["delivered"],
                        verification=c["review"].get("verification")) for c in checks
                   if str(c["review"].get("reason", "")).startswith(("probe_", "failed_probe")) or c["review"].get("verification")])
    summary = dict(revision=REVISION, createdUtc=datetime.now(timezone.utc).isoformat(), session=sid, source=str(session),
                   output=str(output), decisions=len(decided), rounds=rounds,
                   method="Recorded responses, timing and EEG; video state follows the current code's decisions.")
    write(output / "closed-loop-summary.json", summary)
    for stage in LAYERS:
        r = rounds[stage]
        changed = sum(a != b for (_, a), (_, b) in zip(r["originalCommands"], r["simulatedCommands"]))
        print(stage, "commands changed:", changed, "first:", r["firstDifferentCommand"],
              "video seconds:", round((r["summary"] or {}).get("videoSeconds", 0), 1),
              "executed changes:", (r["summary"] or {}).get("executedChanges"))
    print("OUTPUT", output)


if __name__ == "__main__":
    main()

"""Reconstruct an engineering comparison from immutable service records."""
from bisect import bisect_left
import json
from comparison_environment import provenance

from probe_control import response_end
from evidence_guard_adapter import identity as event_identity
from decision_gaze import attach_gaze


def records(path):
    if path.exists():
        with path.open(encoding="utf-8") as stream:
            for line in stream:
                if line.endswith("\n"):
                    yield json.loads(line)


def slice_gaze(samples, starts, start, end):
    """Intersect samples with the interval and expose gaps and elapsed time by target."""
    runs = []
    cursor = start

    def add(a, b, target):
        if b <= a:
            return
        if runs and runs[-1][2] == target and abs(runs[-1][1] - a) < 1e-8:
            runs[-1][1] = b
        else:
            runs.append([a, b, target])

    for i in range(bisect_left(starts, start - .1), bisect_left(starts, end)):
        a, b, target = samples[i]
        a, b = max(a, cursor), min(b, end)
        if b <= a:
            continue
        add(cursor, a, "missing")
        add(a, b, target)
        cursor = b
    add(cursor, end, "missing")
    return runs


def report(folder):
    meta = json.loads((folder / "personal_protocol.json").read_text(encoding="utf-8"))
    config = meta["config"]
    assignment_path = folder.parent / "test-session.json"
    assignment = json.loads(assignment_path.read_text(encoding="utf-8")) if assignment_path.exists() else {}
    first_signal = next(records(folder / "raw_signals.jsonl"), {})
    signal_source = first_signal.get("stream", {}).get("data_origin", "not recorded")
    eyes, trials, decisions, signals, media = [], {}, {}, [], {}
    segment_cuts = []
    receipts = {}
    latest = 0.
    for row in records(folder / "evidence_guard_audit.jsonl"):
        kind = row["record"]
        if kind == "host_decision":
            decisions[row["base"]["requestId"]] = row
        elif kind == "enactment" and row["actual"]:
            receipts[row["request_id"]] = row
        elif kind == "evidence":
            data = row["data"]
            latest = max(latest, row["end"])
            if row["kind"] == "gaze":
                target = data["target"] if row["quality"] else "missing"
                if (target == "task" and not data["task_visible"]) or (target == "movie" and not data["movie_visible"]):
                    target = "other"
                eyes.append((row["start"], min(row["end"], row["start"] + .1), target))
            elif row["kind"] == "trial" and data["trainingStage"] in ("layer_a", "layer_b"):
                trials[row["id"]] = data
            elif row["kind"] == "signal":
                signals.append(dict(id=row["id"], start=row["start"], end=row["end"],
                                    stage=data["stage"], score=data.get("score"), valid=row["quality"]))
    for e in records(folder / "events.jsonl"):
        latest = max(latest, e["actual"])
        if e["type"] == "adaptive_media_segment_changed":
            segment_cuts.append(dict(time=e["actual"], detail=e["detail"]))
        if not e["type"].startswith("adaptive_media_") or not e["detail"].startswith("{"):
            continue
        details, offset = json.JSONDecoder().raw_decode(e["detail"])
        key = (details["block"], details["mediaId"], details["onset"])
        if e["type"] == "adaptive_media_started":
            media[key] = dict(clip=details["mediaId"], block=details["block"], scheduledOffset=details["onset"],
                              startEvent="event:"+event_identity(e), stage=details.get("stage"), start=e["actual"],
                              end=None, visual=details.get("modality") == "audiovisual" and "first_decoded_frame" in e["detail"][offset:],
                              audio="audio_playing=true" in e["detail"][offset:] or details.get("modality") == "audio")
        elif key in media and e["type"] in ("adaptive_media_ended", "adaptive_media_interrupted", "adaptive_media_failure"):
            media[key]["end"] = e["actual"]
            media[key]["endEvent"] = "event:"+event_identity(e)
    eyes.sort()
    starts = [s[0] for s in eyes]
    timeline = []
    for identity, r in sorted(trials.items(), key=lambda item: item[1]["actualOnset"]):
        a = r["actualOnset"]
        end = min(r["closeTime"], r["offsetTime"] if r.get("offsetTime", 0) > 0 else a + 4, a + 4)
        cut = max(a, min(end, response_end(r)))
        pre = slice_gaze(eyes, starts, a, cut)
        valid = sum(y-x for x, y, target in pre if target != "missing")
        timeline.append(dict(id=identity, stage=r["trainingStage"], trial=r["phaseTrial"]+1,
                             start=a, visibleEnd=end, response=r["responseTime"] if r["responded"] else None,
                             deadline=r["closeTime"], correct=r["correct"], scored=r["scored"] and not r["interrupted"] and not r["aborted"],
                             preResponse=pre, afterResponse=slice_gaze(eyes, starts, cut, end),
                             preResponseSeconds=cut-a, validEyeSeconds=valid,
                             media=[m for m in media.values() if m["stage"] == r["trainingStage"]
                                    and m["start"] < end and (m["end"] if m["end"] is not None else latest) > a],
                             eegWindows=[s["id"] for s in signals if s["stage"] == r["trainingStage"] and s["start"] < end and s["end"] > a]))
    deliveries = {r["decision"]["requestId"]:r["decision"] for r in records(folder / "comparison_deliveries.jsonl")}
    checkpoints = []
    summary = {s:dict(ON=0, OFF=0, deferredON=0, deferredOFF=0, deferredEpisodes=0, missingEyeChecks=0,
                      executedChanges=0, postUpgradeErrors=0, postUpgradeObserved=0,
                      videoSeconds=sum((m["end"] if m["end"] is not None else latest)-m["start"]
                                       for m in media.values() if m["visual"] and m["stage"] == s)) for s in ("layer_a","layer_b")}
    last_episode = {}
    seen = set()
    for event in records(folder / "personal_decisions.jsonl"):
        p, base = event["packet"], event["decision"]
        key, stage = base["requestId"], p["stage"]
        if stage not in summary or key in seen:
            continue
        seen.add(key)
        audit = decisions.get(key, {})
        review = audit.get("review", {})
        receipt = receipts.get(key)
        delivery = deliveries.get(key, audit.get("delivered", {}))
        delivered = delivery.get("action")
        if not review and delivery.get("guardReason"):
            review = dict(reason=delivery["guardReason"], verdict=delivery.get("guardVerdict"))
        action = base["action"]
        stats = summary[stage]
        episode = None
        if action in ("ON", "OFF"):
            stats[action] += 1
            if review.get("recommended") == "HOLD":
                stats["deferred"+action] += 1
                episode = (action, review["reason"], base["level"])
                if last_episode.get(stage) != episode:
                    stats["deferredEpisodes"] += 1
        last_episode[stage] = episode
        if review.get("reason") in ("eye_tracking_insufficient", "playback_eye_tracking_insufficient"):
            stats["missingEyeChecks"] += 1
        follow = []
        if receipt and receipt["previous_level"] != receipt["level"]:
            stats["executedChanges"] += 1
            if receipt["level"] == 1:
                follow = [t for t in timeline if t["stage"] == stage and p["completedTrials"] < t["trial"] <= p["completedTrials"]+10 and t["scored"]]
                stats["postUpgradeObserved"] += len(follow)
                stats["postUpgradeErrors"] += sum(not t["correct"] for t in follow)
        checkpoints.append(dict(request=key, stage=stage, trial=p["completedTrials"], time=p["now"], base=base,
                                review=review, delivered=delivered, receipt=receipt,
                                execution="confirmed" if receipt else "no change requested" if delivered=="HOLD" else "unknown",
                                followUpTrials=[t["id"] for t in follow]))
    by_checkpoint = {(c["stage"],c["trial"]):c for c in checkpoints}
    for c in checkpoints:
        review = c["review"]
        if review.get("probeStart") is None or "playbackSeconds" not in review:
            continue
        original = by_checkpoint.get((c["stage"],review["probeStart"]))
        if original:
            original["verificationRequest"] = c["request"]
            c["upgradeRequest"] = original["request"]
    gaze_source = provenance(config) if "comparisonEnvironment" in config else None
    attach_gaze(folder, checkpoints, timeline, simulated=bool((gaze_source or {}).get("simulated")) or assignment.get("clientId") == "seeded-simulator")
    return dict(spatialEvidenceRevision="decision-gaze-density-v1", evidenceScope=meta.get("sessionType",config.get("sessionType")), configRevision=config.get("evaluationPolicy"), reviewRevision=meta.get("comparisonReview"), modelHash=config.get("testModelHash"), session=meta["session"], participant=meta["participant"], model=config.get("testModelId"),
                mode=config.get("testSessionMode"), gazeInput=provenance(config) if "comparisonEnvironment" in config else None,
                signalSource=signal_source, syntheticSignal=first_signal.get("synthetic"),
                trialBudget=config.get("trainingTrials"), decisionStride=2 if config.get("evaluationPolicy")=="behavior5-continuous-stride2-v3" else 5,
                summary=summary, checkpoints=checkpoints,
                initialScoreThresholds=assignment.get("initialScoreThresholds", assignment.get("scoreThresholds")),
                currentScoreThresholds=assignment.get("scoreThresholds"), thresholdHistory=assignment.get("thresholdHistory", []),
                trials=timeline, eegWindows=signals, media=list(media.values()), segmentCuts=segment_cuts)

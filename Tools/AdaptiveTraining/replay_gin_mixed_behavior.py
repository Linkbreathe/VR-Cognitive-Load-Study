"""Offline engineering replay. Never publishes a model or writes to a source session."""

if __name__ == "__main__":
    raise SystemExit("Retired P007/Gin workflow. Use StartEvidenceConsole.cmd with the Mindtooth device.")

from bisect import bisect_right
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import tempfile

import numpy as np

from replay_gin_sliding_round import records, PersonalSession, GuardAdapter, SLIDING, ab_packet
from personalized_model import forest, label_fit, label_rows, window_behavior
from personalized_signals import sampled_window, eeg_features, ecg_features, FEATURES

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path("D:/ExperimentData/PatentDemo/SIM_GIN_TEST")
SERVICE = SOURCE / "sessions/20261103T192021_a50f04b7f959/service"
CAPTURE = SOURCE / "captures/gin-20261103/raw-replay.npz"
CAL = SOURCE.parent / "Simulations/SourcePending/20261103T182001_964d733b/simulated-trials.json"
OUT = ROOT / "Evidence/GinMixedBehaviorReplay"


def fit_candidate(policy):
    rows = json.loads(CAL.read_text())
    with np.load(CAPTURE) as archive:
        times = archive["timestamps"]
        times = times - times[0]
        samples = archive["samples"]
    qualified = []
    for end in range(5, len(rows) + 1):
        window = rows[end-5:end]
        behavior = window_behavior(window, policy, sliding=True)
        if not behavior["valid"]:
            continue
        x, _ = sampled_window(times, samples, behavior["start"], behavior["end"], 500)
        if x is None:
            continue
        ev, eq = eeg_features(x[:, 1:5], 500, policy["theta_channels"], policy["alpha_channels"])
        cv, cq = ecg_features(x[:, 7]-x[:, 8], 500)
        if eq["valid"] and cq["valid"]:
            levels = [r["level"] for r in window]
            values = dict(ev, **cv, movie_fraction=.5*np.mean(levels))
            qualified.append(dict(behavior=behavior, pattern=",".join(map(str,levels)),
                                  features=[values[f] for f in FEATURES]))
    labels = label_rows(qualified, label_fit(qualified, policy), policy)
    model = forest(policy, 20261103)
    chosen = [r for r,y in zip(qualified,labels) if y >= 0]
    model.fit([r["features"] for r in chosen], labels[labels >= 0])
    return model, dict(admitted=False, windowRevision=SLIDING,
        supported_level_patterns=sorted({r["pattern"] for r in chosen}),
        stable=sum(int(y==0) for y in labels), struggling=sum(int(y==1) for y in labels))


def simulate_answers(source):
    simulated = []
    for n, raw in enumerate(source, 1):
        t = dict(raw)
        if t["scored"]:
            stable = n <= 30 or 51 <= n <= 75
            responded = stable or n % 2 == 0
            correct = responded and (n % 11 != 0 if stable else n % 8 == 0)
            rt = (1.1 if stable else 2.5) + .08*(n % 5) if responded else -1
            t.update(correct=correct, responded=responded, timeout=not responded, reactionTime=rt,
                     responseTime=t["actualOnset"]+rt if responded else -1,
                     responseMatch=(t["expectedMatch"] == correct) if responded else False)
        simulated.append(t)
    return simulated


def replay(stage, mode, history, model, report, clocks, scene, protocol, folder, gaze_after=None):
    p = ab_packet(stage)
    p["config"].update(slidingRevision=SLIDING, seed=protocol["config"]["seed"])
    p.update(participantCode="SIM_GIN_MIXED", sessionId=stage+"_"+mode, environmentRevision=scene)
    s = PersonalSession(p,folder,protocol["policy"])
    guard = GuardAdapter(p,folder)
    s.report = report
    if mode == "candidate":
        s.model = model
        s.candidate_control = True
    elif mode == "mock_admitted":
        # Isolated counterfactual. The fitted artifact and model registry retain their provenance.
        s.report = dict(report, admitted=True)
        s.model = model
    elif mode == "fallback":
        s.attempts = 2
    chunks = iter(records(SERVICE/"raw_signals.jsonl"))
    chunk = next(chunks,None)
    times = [c[0] for c in clocks]
    trials, decisions, events = [], [], []
    level, changed = 0, -100
    for i,raw in enumerate(history):
        n = i+1
        row = dict(raw,trainingStage=stage,level=level,transition=0<=i-changed<2)
        if n % 5 == 0 and 25 <= n <= 95:
            now = row["closeTime"]
            _,offset,unc = clocks[bisect_right(times,now)-1]
            while chunk and chunk["received_monotonic"] <= now+offset:
                s.ingest(chunk["stream"],chunk["corrected_monotonic"],chunk["samples"],chunk["source_id"])
                chunk = next(chunks,None)
            p.update(now=now, level=level, completedTrials=n,lastTrial=n,requestId=f"{stage}-{n}",
                     evaluationId=n//5, closedBehavior=dict(row,trialEnd=0),trials=trials,
                     guardGaze=[],events=events,clockOffset=offset,clockUncertainty=unc,
                     decisionDeadline=row["plannedOnset"]+4.5)
            if gaze_after is not None:
                for t in (trials+[row])[-5:]:
                    if t["phaseTrial"] < gaze_after:
                        continue
                    for k in range(40):
                        start = t["actualOnset"] + k*.1
                        end = min(start+.1, now, t["actualOnset"]+4)
                        if end > start:
                            p["guardGaze"].append(dict(id=t["phaseTrial"]*40+k, start=start, end=end,
                                level=t["level"], valid=True, target="task", taskVisible=True,
                                movieVisible=s.exposure(start,end)>0))
            feature = s.extract(trials[-5:],p)
            pattern = ",".join(map(str,feature["levels"]))
            score = None
            if s.model is not None and feature["valid"] and pattern in report["supported_level_patterns"]:
                score = float(model.predict_proba([feature["features"]])[0,1])
            s.predictions.append(dict(feature,score=score))
            d = s.decide(p)
            reply = guard.receive(p,s,d)
            assert not guard.failure and d["behavior"]["valid"], (guard.failure, d)
            action = reply["action"]
            previous = level
            events = []
            if action in ("ON","OFF"):
                level = int(action=="ON")
                assert previous != level and not s.playing
                changed = n
                boundary = row["plannedOnset"]+4.5
                events = [dict(type="adaptive_level_changed",actual=boundary,detail=f"level={level}"),
                          dict(type="personal_execution",actual=boundary,detail=f"request={d['requestId']};level={level};previous_level={previous};action={action}")]
                s.event(events[0])
            decisions.append(dict(mode=mode,condition=stage,trial=n,before=previous,after=level,
                state=d["behavior"]["state"],omissions=d["behavior"]["omissions"],ba=d["behavior"]["ba"],
                feature_valid=feature["valid"],feature_reason=feature["reason"],movie_fraction=feature["movie_fraction"],
                candidate_score=score,base=d["action"],basis=d["basis"],base_reason=d["reason"],
                verdict=reply["guardVerdict"],guard_reason=reply["guardReason"],effective=action))
        # A ten-second simulated clip starts one second into each five-trial L1 group.
        # It always finishes before the next decision. No eye tracking is invented.
        if row["level"] and i%5 == 0:
            start = row["plannedOnset"]+1
            detail = json.dumps(dict(block=row["block"],mediaId=f"sim-{i}",onset=start,level=1))
            for status,when in (("started",start),("ended",start+10)):
                e = dict(type="adaptive_media_"+status,actual=when,detail=detail+";audio_playing=true")
                s.event(e)
                events.append(e)
        s.trials[(stage,i)] = row
        trials.append(row)
    assert len(decisions)==15
    return decisions


def main():
    protocol = json.loads((SERVICE/"personal_protocol.json").read_text())
    rows, clocks = {}, []
    for record in records(SERVICE/"telemetry.jsonl"):
        p = record["packet"]
        assert not p.get("guardGaze")
        clocks.append((p["now"],p["clockOffset"],p["clockUncertainty"]))
        for row in p["trials"]:
            if row["trainingStage"]=="bioadaptive":
                rows[row["phaseTrial"]] = row
    assert len(rows)==96
    answers = simulate_answers([rows[i] for i in range(96)])
    model,report = fit_candidate(protocol["policy"])
    results = []
    summary = {}
    with tempfile.TemporaryDirectory() as tmp:
        for mode in ("archive","candidate","fallback"):
            for stage in ("layer_a","layer_b"):
                group = replay(stage,mode,answers,model,report,sorted(clocks),p["environmentRevision"],
                               protocol,Path(tmp)/(stage+mode))
                results.extend(group)
                summary[mode+"_"+stage] = dict(on=[d["trial"] for d in group if d["base"]=="ON"],
                    blocked=[d["trial"] for d in group if d["base"]=="ON" and d["effective"]!="ON"],
                    switches=[d["trial"] for d in group if d["before"]!=d["after"]],
                    usable_features=sum(d["feature_valid"] for d in group))
    OUT.mkdir(exist_ok=True)
    for name,data in (("decisions",results),("simulated-answers",answers)):
        with (OUT/(name+".csv")).open("w",newline="",encoding="utf-8") as f:
            writer = csv.DictWriter(f,fieldnames=data[0])
            writer.writeheader()
            writer.writerows(data)
    inputs = {str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in
              [CAPTURE,CAL,SERVICE/"telemetry.jsonl",SERVICE/"raw_signals.jsonl",SERVICE/"personal_protocol.json"]}
    (OUT/"summary.json").write_text(json.dumps(dict(engineering_only=True,source_hashes=inputs,candidate=report,
        simulated="Answers, calibration labels and movie context, playback and timely boundary enactments. No gaze.",
        archive="Original model eligibility; fallback inactive.",candidate_mode="Unadmitted offline diagnostic RF.",
        fallback="Assumes two unsuccessful fits.",results=summary),indent=2))
    print(json.dumps(summary))

if __name__ == "__main__":
    main()

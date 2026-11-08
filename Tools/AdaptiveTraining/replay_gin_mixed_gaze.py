"""Paired Gin replay with full-round gaze and trial-by-trial Guard evidence delivery."""

if __name__ == "__main__":
    raise SystemExit("Retired P007/Gin workflow. Use StartEvidenceConsole.cmd with the Mindtooth device.")

import csv
import json
import tempfile
from bisect import bisect_right
from collections import Counter
from pathlib import Path

from replay_gin_mixed_behavior import (ROOT, SERVICE, records, simulate_answers, fit_candidate,
    PersonalSession, GuardAdapter, ab_packet, SLIDING)

OUT = ROOT/"Evidence/GinMixedGazeAB"

def run(stage, answers, model, report, protocol, clocks, scene, folder):
    packet = ab_packet(stage)
    packet["config"].update(slidingRevision=SLIDING, seed=protocol["config"]["seed"])
    packet.update(participantCode="SIM_GIN_MIXED",sessionId="mixed_gaze_"+stage,environmentRevision=scene)
    session = PersonalSession(packet, folder, protocol["policy"])
    session.model = model
    session.report = dict(report,admitted=True)
    guard = GuardAdapter(packet,folder)
    chunks = iter(records(SERVICE/"raw_signals.jsonl"))
    chunk = next(chunks,None)
    clock_times = [c[0] for c in clocks]
    history, pending, output, timeline = [], [], [], []
    level, change = 0, -100
    clips = {}
    for i, raw in enumerate(answers):
        n = i+1
        row = dict(raw,trainingStage=stage,level=level,transition=0<=i-change<2)
        now = row["closeTime"]
        if level and i%5==0 and i+3<=len(answers):
            start = row["plannedOnset"]+1
            clips[n] = (start,start+10)
            detail = json.dumps(dict(block=row["block"],mediaId="clip-"+str(n),onset=start,level=1))
            for label,t in zip(("started","ended"),clips[n]):
                pending.append(dict(type="adaptive_media_"+label,actual=t,detail=detail+";audio_playing=true"))
        events = sorted([e for e in pending if e["actual"]<=now],key=lambda e:e["actual"])
        pending = [e for e in pending if e["actual"]>now]
        for e in events:
            session.event(e)
        _,offset,unc = clocks[bisect_right(clock_times,now)-1]
        while chunk and chunk["received_monotonic"]<=now+offset:
            session.ingest(chunk["stream"],chunk["corrected_monotonic"],chunk["samples"],chunk["source_id"])
            chunk = next(chunks,None)
        gaze = []
        for k in range(40):
            start = row["actualOnset"]+k*.1
            end = min(now,start+.1)
            if end<=start:
                continue
            playing = any(a<=start and end<=b for a,b in clips.values())
            target = "movie" if playing and k%20<10 else "task"
            gaze.append(dict(id=i*40+k,start=start,end=end,level=level,valid=True,target=target,
                             taskVisible=True,movieVisible=playing))
        packet.update(now=now,level=level,completedTrials=n,lastTrial=n,requestId=f"{stage}-{n}",
            evaluationId=n//5,closedBehavior=dict(row,trialEnd=0),trials=history,guardGaze=gaze,events=events,
            clockOffset=offset,clockUncertainty=unc,decisionDeadline=row["plannedOnset"]+4.5)
        if len(history)>=5:
            feature=session.extract(history[-5:],packet)
            score=None
            if feature["valid"] and ",".join(map(str,feature["levels"])) in report["supported_level_patterns"]:
                score=float(model.predict_proba([feature["features"]])[0,1])
            session.predictions.append(dict(feature,score=score))
        checkpoint=n in range(25,96,5)
        base=session.decide(packet) if checkpoint else None
        prior=len(guard.guard.audit)
        reply=guard.receive(packet,session,base)
        assert not guard.failure,guard.failure
        for event in guard.guard.audit[prior:]:
            if event["record"] in ("forward","reverse","relation","authorization","view_check"):
                timeline.append(dict(condition=stage,trial=n,**event))
        if checkpoint:
            action=reply["action"]
            before=level
            if action in ("ON","OFF"):
                assert not session.playing
                level=int(action=="ON")
                assert before!=level
                change=n
                boundary=row["plannedOnset"]+4.5
                pending.extend([dict(type="adaptive_level_changed",actual=boundary,detail=f"level={level}"),
                    dict(type="personal_execution",actual=boundary,detail=f"request={base['requestId']};level={level};previous_level={before};action={action}")])
            output.append(dict(condition=stage,trial=n,before=before,after=level,score=score,state=base["behavior"]["state"],
                proposed=base["action"],verdict=reply["guardVerdict"],action=action,reason=reply["guardReason"]))
        history.append(row)
        session.trials[(stage,i)]=row
    assert len(output)==15 and not pending
    return output,timeline

def main():
    OUT.mkdir(exist_ok=True)
    clocks, rows = [], {}
    for rec in records(SERVICE/"telemetry.jsonl"):
        p=rec["packet"]
        clocks.append((p["now"],p["clockOffset"],p["clockUncertainty"]))
        for t in p["trials"]:
            if t["trainingStage"]=="bioadaptive":
                rows[t["phaseTrial"]]=t
    answers=simulate_answers([rows[i] for i in range(96)])
    protocol=json.loads((SERVICE/"personal_protocol.json").read_text())
    model,report=fit_candidate(protocol["policy"])
    combined=[]
    with tempfile.TemporaryDirectory() as tmp, (OUT/"timeline.jsonl").open("w") as audit:
        for stage in ("layer_a","layer_b"):
            group,timeline=run(stage,answers,model,report,protocol,sorted(clocks),p["environmentRevision"],Path(tmp)/stage)
            combined.extend(group)
            for event in timeline:
                audit.write(json.dumps(event)+"\n")
            first={}
            for e in timeline:
                if e["record"]=="forward":
                    first.setdefault(e["event"],e["trial"])
            print(stage,"ON/OFF",[(r["trial"],r["action"]) for r in group if r["proposed"]!="HOLD"],
                  "first forwards",list(first.values()),"reverse",[e["trial"] for e in timeline if e["record"]=="reverse"])
    with (OUT/"decisions.csv").open("w",newline="") as f:
        writer=csv.DictWriter(f,fieldnames=combined[0])
        writer.writeheader()
        writer.writerows(combined)

if __name__=="__main__":
    main()

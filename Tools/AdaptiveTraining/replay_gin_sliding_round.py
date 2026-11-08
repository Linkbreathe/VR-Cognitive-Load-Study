"""Read-only offline replay of a historical Gin engineering session at response close."""

if __name__ == "__main__":
    raise SystemExit("Retired P007/Gin workflow. Use StartEvidenceConsole.cmd with the Mindtooth device.")

import argparse
from bisect import bisect_right
from collections import Counter
import copy
import csv
import hashlib
import json
from pathlib import Path
import tempfile
from evidence_guard_adapter import GuardAdapter
from layer_ab import SLIDING
from personalized_runtime import PersonalSession
from test_layer_ab import ab_packet

def records(path):
    with path.open(encoding="utf-8") as f:
        for line in f:
            yield json.loads(line)

def run(source,out):
    sdir=source/"service"
    protocol=json.loads((sdir/"personal_protocol.json").read_text())
    rows={}; clocks=[];gaze=0;scene=None
    for record in records(sdir/"telemetry.jsonl"):
        p=record["packet"]
        for row in p["trials"]:
            if row["trainingStage"]=="bioadaptive":rows[row["phaseTrial"]]=row
        clocks.append((p["now"],p["clockOffset"],p["clockUncertainty"]))
        gaze+=len(p.get("guardGaze") or [])
        scene=p.get("environmentRevision")
    rows=[rows[i] for i in range(96)]
    assert all(t["level"]==0 and not t["aborted"] and not t["interrupted"] for t in rows)
    ev=Counter(e["type"] for e in records(sdir/"events.jsonl"))
    assert not ev["adaptive_level_changed"] and not ev["adaptive_media_started"] and gaze==0
    clocks.sort(); times=[t[0] for t in clocks]
    historical={r["decision"]["lastTrial"]:r["decision"] for r in records(sdir/"personal_decisions.jsonl")}
    out.mkdir(parents=True,exist_ok=False)
    results=[]
    with tempfile.TemporaryDirectory() as tmp:
        for stage in ("layer_a","layer_b"):
            p=ab_packet(stage);p["config"]["slidingRevision"]=SLIDING;p["participantCode"]="REPLAY_GIN";p["sessionId"]="offline_"+stage
            p["environmentRevision"]=scene;p["config"]["seed"]=protocol["config"]["seed"]
            folder=Path(tmp)/stage
            personal=PersonalSession(p,folder,protocol["policy"])
            guard=GuardAdapter(p,folder)
            chunks=iter(records(sdir/"raw_signals.jsonl"));chunk=next(chunks,None)
            for n in range(25,96,5):
                current=[dict(t,trainingStage=stage) for t in rows[:n]]
                preview=current.pop();now=preview["closeTime"]
                j=bisect_right(times,now)-1
                assert j>=0
                _,offset,unc=clocks[j]
                # Clock mapping is from the last telemetry packet preceding response close.
                while chunk and chunk["received_monotonic"]<=now+offset:
                    personal.ingest(chunk["stream"],chunk["corrected_monotonic"],chunk["samples"],chunk["source_id"])
                    chunk=next(chunks,None)
                p.update(now=now,level=0,completedTrials=n,lastTrial=n,requestId=f"{stage}-{n}",evaluationId=n//5,
                         closedBehavior=dict(preview,trialEnd=0),trials=current,guardGaze=[],events=[],
                         clockOffset=offset,clockUncertainty=unc,decisionDeadline=preview["plannedOnset"]+4.5)
                personal.trials={(stage,i):t for i,t in enumerate(current)}
                feature=personal.extract(current[-5:],p)
                personal.predictions.append(dict(feature,score=None))
                d=personal.decide(p);reply=guard.receive(p,personal,d)
                assert not guard.failure and reply["guardVerdict"]=="NO_CHANGE" and d["behavior"]["valid"], (n,d,reply,guard.failure)
                # Additional counterfactual: explicitly enabled fallback after two failed fits.
                # The original session did not perform those attempts.
                personal.attempts=2; fb=personal.decide(p);personal.attempts=0
                assert d["action"]==fb["action"]==reply["action"]=="HOLD"
                results.append(dict(condition=stage,trial=n,level=0,omissions=d["behavior"]["omissions"],
                    balanced_accuracy=d["behavior"]["ba"],state=d["behavior"]["state"],base=d["action"],
                    base_reason=d["reason"],guard=reply["guardVerdict"],delivered=reply["action"],
                    guard_reason=reply["guardReason"],fallback_sensitivity=fb["action"],historical_action=historical[n]["action"],
                    feature_valid=feature["valid"],feature_reason=feature["reason"],clock_uncertainty=unc))
            (out/(stage+"-guard.jsonl")).write_bytes((folder/"evidence_guard_audit.jsonl").read_bytes())
    with (out/"decisions.csv").open("w",encoding="utf-8",newline="") as f:
        writer=csv.DictWriter(f,fieldnames=results[0]);writer.writeheader();writer.writerows(results)
    report=dict(source=str(source.resolve()),provenance="Recorded Gin EEG/ECG replay and existing Editor task responses; engineering only.",
        inputs_sha256={name:hashlib.sha256((sdir/name).read_bytes()).hexdigest() for name in
                       ("raw_signals.jsonl","telemetry.jsonl","events.jsonl","personal_decisions.jsonl","personal_protocol.json")},
        revision=SLIDING,archived_model=protocol["config"]["testModelId"],compatible_admitted_model=False,
        trial_count=len(rows),scored=sum(t["scored"] for t in rows),scored_responses=sum(t["scored"] and t["responded"] for t in rows),
        actual_media_starts=ev["adaptive_media_started"],historical_level_changes=ev["adaptive_level_changed"],guard_gaze_intervals=gaze,
        analysis="Offline response-close reconstruction. Original telemetry clock mapping, recorded sensor chunk arrival cutoff, no generated answers, predictions, gaze or playback. No Unity actions issued.",
        conditions={})
    for stage in ("layer_a","layer_b"):
        group=[r for r in results if r["condition"]==stage]
        report["conditions"][stage]=dict(checkpoints=len(group),stable=sum(r["state"]=="stable" for r in group),
            struggling=sum(r["state"]=="struggling" for r in group),valid_feature_windows=sum(r["feature_valid"] for r in group),
            proposed_on=sum(r["base"]=="ON" for r in group),blocked_on=sum(r["base"]=="ON" and r["delivered"]!="ON" for r in group),
            level_changes=sum(r["delivered"] in ("ON","OFF") for r in group),holds=sum(r["delivered"]=="HOLD" for r in group))
    (out/"summary.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps(report["conditions"]));print("responses",report["scored_responses"],"of",report["scored"])
if __name__=="__main__":
    cli=argparse.ArgumentParser(description=__doc__);cli.add_argument("source",type=Path);cli.add_argument("output",type=Path)
    args=cli.parse_args();run(args.source,args.output)

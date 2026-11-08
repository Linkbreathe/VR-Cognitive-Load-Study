"""Measure Guard review and adapter latency on isolated synthetic fixtures.

No live service, participant data, model inference or network requests are used.
Real JSONL audit writes remain enabled (buffered OS writes, not durable fsync).
"""
import argparse
import csv
import hashlib
import json
import platform
import tempfile
import time
from pathlib import Path

import numpy as np
from evidence_guard_adapter import GuardAdapter
from test_evidence_guard import complete_predictions, gaze
from test_personalized import trials
from test_personalized_control import control_rows, decision_fixture


def stats(values):
    a=np.asarray(values)
    return dict(n=len(a),median_ms=float(np.median(a)),p95_ms=float(np.percentile(a,95)),
                p99_ms=float(np.percentile(a,99)),max_ms=float(a.max()),
                over_10ms=int(sum(a>10)),over_100ms=int(sum(a>100)),over_500ms=int(sum(a>500)))


def fixture(folder,mode,scenario):
    rows=trials(95,"bioadaptive") if scenario=="late_on" else control_rows()
    s,p=decision_fixture(folder,rows,admitted=False);complete_predictions(s)
    p.update(completedTrials=len(rows),lastTrial=len(rows),now=len(rows)*4.5+1.5)
    p["config"]["evidenceGuardMode"]=mode
    if scenario!="missing_eyes":p["guardGaze"]=gaze(p["trials"])
    d=s.decide(p)
    assert d["action"]=="ON",d
    if scenario=="off":d["action"]="OFF"
    a=GuardAdapter(p,folder)
    assert a.failure is None,a.failure
    return s,p,d,a


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",type=Path,required=True)
    parser.add_argument("--repeats",type=int,default=300)
    parser.add_argument("--cold",type=int,default=20)
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    samples=[];summary=[]
    with tempfile.TemporaryDirectory(prefix="guard-latency-",dir=args.output) as tmp:
        for mode in ("observe","enforce"):
            for scenario in ("normal_on","missing_eyes","off","late_on"):
                case=f"{mode}/{scenario}";core=[];warm=[];cold=[]
                for iteration in range(args.cold+1):
                    folder=Path(tmp)/mode/scenario/str(iteration);folder.mkdir(parents=True)
                    s,p,d,a=fixture(folder,mode,scenario)
                    review=a.guard.review
                    def measured(proposal):
                        started=time.perf_counter_ns();value=review(proposal)
                        core.append((time.perf_counter_ns()-started)/1e6)
                        return value
                    a.guard.review=measured
                    count=args.repeats+20 if iteration==args.cold else 1
                    if iteration==args.cold:
                        a.receive(p,s)  # Pre-ingest evidence as telemetry does.
                    for j in range(count):
                        request=dict(d,requestId=f"{case}/{iteration}/{j}")
                        before=len(core);started=time.perf_counter_ns();out=a.receive(p,s,request)
                        elapsed=(time.perf_counter_ns()-started)/1e6
                        assert a.failure is None,a.failure
                        assert len(core)==before+1,"Cached result accidentally timed"
                        verdict="DEFER" if scenario=="missing_eyes" else "ALLOW"
                        action="OFF" if scenario=="off" else "HOLD" if scenario=="missing_eyes" and mode=="enforce" else "ON"
                        assert out["guardVerdict"]==verdict and out["action"]==action,out
                        if iteration==args.cold and j<20:continue
                        phase="warm" if iteration==args.cold else "first_ingestion"
                        (warm if phase=="warm" else cold).append(elapsed)
                        samples.append(dict(case=case,phase=phase,iteration=j if phase=="warm" else iteration,
                            review_ms=core[-1],adapter_ms=elapsed,verdict=verdict,action=action,
                            evidence_count=len(a.guard.evidence)))
                measured_core=[r["review_ms"] for r in samples if r["case"]==case and r["phase"]=="warm"]
                item=dict(case=case,review=stats(measured_core),adapter=stats(warm),first_ingestion=stats(cold))
                summary.append(item)
                print(case,json.dumps(item),flush=True)
    with (args.output/"samples.csv").open("w",newline="",encoding="utf-8") as f:
        writer=csv.DictWriter(f,fieldnames=samples[0].keys());writer.writeheader();writer.writerows(samples)
    import evidence_guard.core
    sources=[Path(__file__).with_name("evidence_guard_adapter.py"),Path(evidence_guard.core.__file__)]
    report=dict(origin="synthetic_engineering_benchmark",platform=platform.platform(),python=platform.python_version(),
        clock=time.get_clock_info("perf_counter")._asdict() if hasattr(time.get_clock_info("perf_counter"),"_asdict") else str(time.get_clock_info("perf_counter")),
        measurement="external perf_counter_ns around real review (including review audit) and adapter.receive (including full audit writes)",
        excluded="acquisition wait, feature extraction, RF inference including optional diagnostic prediction, HTTP, Unity and physical device latency",
        audit="real local JSONL writes; OS buffered, no fsync guarantee; unique requests, no cache-hit timings",
        sources={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},results=summary)
    (args.output/"summary.json").write_text(json.dumps(report,indent=2),encoding="utf-8")


if __name__=="__main__":main()

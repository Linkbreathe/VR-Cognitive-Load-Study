"""Synthetic witnesses and bounded optimization, never participant evidence.

Optimize responses on one actual Unity stimulus sequence. Verify every resulting
decision through PersonalSession; physiology descriptors are explicitly mocked.
"""
import copy
import itertools
import json
from pathlib import Path
import tempfile
from unittest.mock import patch

import numpy as np
from scipy.optimize import milp, Bounds, LinearConstraint
from personalized_runtime import PersonalSession
from personalized_model import BEHAVIOR_REVISION
from test_personalized import packet

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Evidence/BehaviorWindowRevision"
ROWS = json.loads((OUT / "unity-sequence.json").read_text())["trials"]
CHECKS = list(range(25, 96, 5))
# A reply to trial 95 can only reach the boundary at 96, where Unity ends the
# phase before ApplyPersonalDecision. It is an audit opportunity, not a switch.
EXECUTABLE_CHECKS = CHECKS[:-1]


def solve(schedule, mode):
    # Binary correct/omission variables and one OR selector per decision.
    size = 207; constraints = []; lower = []; upper = []
    def add(row, lo=-np.inf, hi=np.inf):
        constraints.append(row); lower.append(lo); upper.append(hi)
    for i in range(96):
        r=np.zeros(size); r[i]=r[96+i]=1; add(r, hi=1)
    level=0; last=-100
    for j,n in enumerate(CHECKS):
        eligible=n-last >= (10 if mode=="fallback" else 15)
        window=ROWS[n-20:n]; matches=sum(t["expectedMatch"] for t in window)
        if min(matches,20-matches)<2:
            if n in schedule:return None
            continue
        ba=np.zeros(size); om=np.zeros(size)
        for i in range(n-20,n):
            ba[i]=.5/(matches if ROWS[i]["expectedMatch"] else 20-matches)
            om[96+i]=1
        if n in schedule:
            if not eligible:return None
            if level==0:add(ba,lo=.70); add(om,hi=3)
            else:
                r=ba.copy();r[192+j]=-2;add(r,hi=.40)
                r=om.copy();r[192+j]=-21;add(r,lo=-15)
            level=1-level;last=n
        elif eligible and mode=="fallback" and n<95:
            if level==0:
                r=ba.copy();r[192+j]=-2;add(r,hi=.70-1e-5)
                r=om.copy();r[192+j]=-21;add(r,lo=-17)
            else:add(ba,lo=.40+1e-5);add(om,hi=5)
    result=milp(np.zeros(size),integrality=np.ones(size),bounds=Bounds(0,1),
                constraints=LinearConstraint(np.array(constraints),lower,upper))
    if result.status==2:return None
    if not result.success:raise RuntimeError(result.message)
    return np.rint(result.x[:96]).astype(bool).tolist(),np.rint(result.x[96:192]).astype(bool).tolist()


def optimize(mode):
    gap=10 if mode=="fallback" else 15
    examined=0
    for count in range(7 if mode=="fallback" else 5,-1,-1):
        for schedule in itertools.combinations(EXECUTABLE_CHECKS,count):
            if any(b-a<gap for a,b in zip(schedule,schedule[1:])):continue
            examined+=1; solution=solve(schedule,mode)
            if solution is not None:
                return {"mode":mode,"requests":list(schedule),"correct":solution[0],"omitted":solution[1],
                        "examined_schedules":examined,"maximum_for_exported_sequence":count}
    raise AssertionError("No feasible schedule")


def verify(trace):
    p=packet();p.update(stage="bioadaptive",now=0,trials=[],completedTrials=0,level=0)
    p["config"]["seed"]=trace.get("seed",20261102)
    mode=trace["mode"]; requested=[]; executed=[]; decisions=[]; level=0; pending=None; transition=0
    origin=ROWS[0]["plannedOnset"]
    class MockForest:
        score=.5
        def predict_proba(self,x):return np.array([[1-self.score,self.score]])
    model=MockForest()
    with tempfile.TemporaryDirectory() as folder:
        s=PersonalSession(p,folder,{"eeg_units_confirmed":True});s.attempts=2
        if mode=="physiology":s.report={"admitted":True};s.model=model
        # Preserve real window, timestamp, transition and exposure gates. Only
        # sensor extraction/model output are synthetic, to test control capacity.
        with patch.object(s,"raw_window",return_value=(np.zeros((5625,1)),"ok",250)), \
             patch("personalized_runtime.eeg_features",return_value=({"log_theta_relative":0.,"log_alpha_relative":0.},{"valid":True})):
            for i in range(96):
                n=i+1;t=copy.deepcopy(ROWS[i]);t.update(trainingStage="bioadaptive",level=level,
                    transition=transition>0,correct=trace["correct"][i],responded=not trace["omitted"][i])
                t["plannedOnset"]-=origin;t["actualOnset"]-=origin;t["trialEnd"]-=origin
                if transition:transition-=1
                s.trials[("bioadaptive",i)]=t
                if level:s.intervals.append((t["plannedOnset"],t["trialEnd"]))
                p.update(now=t["trialEnd"],completedTrials=n,lastTrial=n,level=level)
                # Synthetic model consistently supports ON in L0 and OFF in L1;
                # prediction history is never rewritten to force a decision.
                model.score=.1 if level==0 else .9
                s.process_windows(p)
                if n%5==0 and n<96:
                    d=s.decide(p);decisions.append({"trial":n,"action":d["action"],"reason":d["reason"],
                        "behavior":d.get("behavior"),"valid_predictions":d.get("valid_predictions")})
                    if d["action"] in ("ON","OFF") and n<95:
                        requested.append(n);pending=(n,1-level)
                # Reply is installed AFTER the request boundary, just as Unity's
                # coroutine yields before accepting the HTTP result.
                if pending and pending[0]<n and n<96:
                    level=pending[1];executed.append(n);pending=None;transition=2
                    s.event({"type":"adaptive_level_changed","actual":t["trialEnd"],"detail":"synthetic"})
                p["now"]=t["trialEnd"]+1;s.process_windows(p)
        assert requested==trace["requests"],(mode,requested,trace["requests"],decisions)
        assert executed==[n+1 for n in requested]
        assert len(decisions)==19
        return {"mode":mode,"seed":trace.get("seed",20261102),"requests":requested,"executions":executed,"decisions":decisions,
                "maximum_for_exported_sequence":trace["maximum_for_exported_sequence"],
                "examined_schedules":trace["examined_schedules"]}


if __name__=="__main__":
    traces=[optimize(mode) for mode in ("fallback","physiology")]
    for trace in traces:trace["seed"]=20261102
    results=[verify(trace) for trace in traces]
    (OUT/"traces.json").write_text(json.dumps({"traces":traces},indent=2))
    (OUT/"switch-results.json").write_text(json.dumps({"behaviorRevision":BEHAVIOR_REVISION,
        "scope":"synthetic; exact exported Unity sequence; favorable media/Guard/quality; no device evidence",
        "terminal_request_95":"auditable but cannot execute before the phase ends at trial 96",
        "results":results},indent=2))
    for r in results:print(r["mode"],"maximum",r["maximum_for_exported_sequence"],"requests",r["requests"],"executions",r["executions"])

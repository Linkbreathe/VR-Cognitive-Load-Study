"""Bound realized flips for one seeded stimulus sequence; replay through production decisions.

RF outputs and media exposure are controlled engineering inputs, not participant data.
"""
import json
from pathlib import Path
import tempfile
import numpy as np
from scipy.optimize import milp, LinearConstraint, Bounds
from layer_ab import SLIDING
from test_layer_ab import ab_packet
from personalized_runtime import PersonalSession
from evidence_guard_adapter import GuardAdapter
from test_evidence_guard import gaze

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"Evidence/SlidingControl"
def optimize(rows):
    # Binary correct/omitted trials and ON / OFF-by-accuracy / OFF-by-omissions at each check.
    n=237; A=[];lo=[];hi=[]
    def constraint(a,l=-np.inf,h=np.inf):A.append(a.copy());lo.append(l);hi.append(h)
    def zeros():return np.zeros(n)
    for i in range(96):
        a=zeros();a[i]=a[96+i]=1;constraint(a,h=1)
    state=zeros()
    for j,k in enumerate(range(25,96,5)):
        match=sum(t["expectedMatch"] for t in rows[k-20:k]); a=zeros(); omissions=zeros()
        on,off1,off2=192+3*j,193+3*j,194+3*j
        if min(match,20-match)<2:
            b=zeros();b[[on,off1,off2]]=1;constraint(b,h=0)
        else:
            for i in range(k-20,k):
                a[i]=.5/(match if rows[i]["expectedMatch"] else 20-match);omissions[96+i]=1
            b=a.copy();b[on]=-.7;constraint(b,l=0)
            b=omissions.copy();b[on]=17;constraint(b,h=20)
            b=a.copy();b[off1]=.6;constraint(b,h=1)
            b=omissions.copy();b[off2]=-6;constraint(b,l=0)
        b=state.copy();b[on]+=1;constraint(b,h=1)
        b=-state.copy();b[[off1,off2]]+=1;constraint(b,h=0)
        state[on]+=1;state[[off1,off2]]-=1
    cost=zeros();cost[192:]=-1
    res=milp(cost,integrality=np.ones(n),bounds=Bounds(0,1),constraints=LinearConstraint(A,lo,hi),
             options={"mip_rel_gap":0})
    assert res.success and res.mip_gap < 1e-8,res.message
    bits=np.rint(res.x).astype(bool)
    return dict(correct=bits[:96].tolist(),omitted=bits[96:192].tolist(),
                requests=[25+5*i for i in range(15) if bits[192+3*i:195+3*i].any()],
                upper_bound=int(round(-res.mip_dual_bound)))

def replay(rows,trace,stage,folder):
    p=ab_packet(stage);p["config"]["slidingRevision"]=SLIDING
    p["config"]["seed"]=20261102;p.update(now=0,level=0,events=[],trials=[])
    s=PersonalSession(p,folder,None);guard=GuardAdapter(p,folder);s.report={"admitted":True,"windowRevision":SLIDING,
        "supported_level_patterns":[",".join(map(str,[a]*k+[1-a]*(5-k))) for a in (0,1) for k in range(6)]}
    level=0;changed=-100;executed=[];decisions=[];history=[];pending_events=[]
    for i,r in enumerate(rows):
        n=i+1;start=4.5*i;close=start+4;end=start+4.5
        row=dict(r,plannedOnset=start,actualOnset=start,trialEnd=end,closeTime=close,offsetTime=close,
                 trainingStage=stage,level=level,transition=0<=i-changed<2,
                 responded=not trace["omitted"][i],correct=trace["correct"][i],reactionTime=1.)
        if level and i-changed<3:s.intervals.append((start,start+4))  # 12s actual synthetic exposure.
        if i>=5:
            w=history[-5:];levels=[t["level"] for t in w]
            score=(.1 if level==0 else .9) if n in trace["requests"] else .5
            s.predictions.append(dict(trial=i-1,stage=stage,levels=levels,level=levels[-1],start=w[0]["plannedOnset"],
                end=start,valid=all(t["scored"] for t in w),score=score,windowRevision=SLIDING,features=[]))
        if n%5==0 and n<96:
            p.update(now=close,level=level,completedTrials=n,lastTrial=n,requestId=f"{stage}-{n}",
                     events=pending_events,trials=history,closedBehavior=dict(row,trialEnd=0),
                     decisionDeadline=end,guardGaze=gaze(history+[row]))
            pending_events=[]
            d=s.decide(p);out=guard.receive(p,s,d);assert not guard.failure,guard.failure
            decisions.append([n,d["action"],out["guardVerdict"],out["action"],d["reason"]])
            if out["action"] in ("ON","OFF"):
                prev=level;level=int(out["action"]=="ON");changed=n;executed.append(n)
                pending_events=[dict(type="adaptive_level_changed",actual=end,detail=f"level={level}"),
                    dict(type="personal_execution",actual=end,detail=f"request={d['requestId']};action={out['action']};level={level};previous_level={prev}")]
                s.event(pending_events[0])
        history.append(row);s.trials[(stage,i)]=row
    assert len(decisions)==19
    if stage=="layer_a":assert executed==trace["requests"],(executed,trace,decisions)
    return dict(executed=executed,decisions=decisions)

if __name__=="__main__":
    rows=json.loads((OUT/"sequence.json").read_text())["trials"]
    trace=optimize(rows)
    with tempfile.TemporaryDirectory() as tmp:
        for stage in ("layer_a","layer_b"):
            trace[stage]=replay(rows,trace,stage,Path(tmp)/stage)
    (OUT/"capacity.json").write_text(json.dumps(trace,indent=2))
    print("Seed 20261102 upper bound:",trace["upper_bound"],"A:",trace["layer_a"]["executed"],"B (same scripted inputs):",trace["layer_b"]["executed"])

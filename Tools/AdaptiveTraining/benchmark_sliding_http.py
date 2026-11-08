"""Local HTTP deadline probe, synthetic sensors and RF fixture, no active Unity/LSL service."""
from pathlib import Path
import json
import tempfile
import threading
import time
from urllib.request import Request,urlopen
import numpy as np
from layer_ab import SLIDING
from personalized_model import forest,policy_config
from service import StudyService,StudyHTTPServer,make_handler
from test_layer_ab import ab_packet
from test_personalized import trials
from test_evidence_guard import gaze

def run():
    with tempfile.TemporaryDirectory() as tmp:
        service=StudyService(tmp,personalized_policy={"eeg_units_confirmed":True},test_state_dir=Path(tmp)/"state")
        p=ab_packet("layer_b");p["config"]["slidingRevision"]=SLIDING
        p.update(events=[],trials=[],now=0,clockOffset=0)
        service.receive(p)
        s=service.sessions[p["sessionId"]]["personal"]
        # Explicitly simulated qualified path, never a participant admission claim.
        model=forest(policy_config(),3)
        model.fit([[0.,0.,0.]]*16+[[50.,50.,1.]]*16,[0]*16+[1]*16)
        s.model=model;s.report={"admitted":True,"windowRevision":SLIDING,"supported_level_patterns":["0,0,0,0,0"]}
        server=StudyHTTPServer(("127.0.0.1",0),make_handler(service))
        worker=threading.Thread(target=server.serve_forever,daemon=True);worker.start()
        result=[];history=[];rr=trials(96,"layer_b")
        try:
            for i,t in enumerate(rr):
                n=i+1;start=i*4.5
                # Streaming acquisition through the previous complete trial only.
                if i:
                    ts=np.arange((i-1)*1125,i*1125)/250
                    values = 20*np.sin(2*np.pi*6*ts) + 10*np.sin(2*np.pi*10*ts)
                    eeg = np.repeat(values[:, None], 8, axis=1)
                    s.ingest({"kind":"EEG","sample_rate":250,"units":"uV","data_origin":"synthetic","synthetic":True},
                             ts.tolist(), eeg.tolist(), "EEG")
                t.update(closeTime=start+4,offsetTime=start+4)
                p.update(trials=history,now=start+4,completedTrials=n,lastTrial=n,requestId=f"bench-{n}",
                         decisionDeadline=start+4.5,closedBehavior=dict(t,trialEnd=0),guardGaze=gaze(rr)[i*40:(i+1)*40])
                if n%5==0 and n<96:
                    begin=time.perf_counter_ns()
                    req=Request(f"http://127.0.0.1:{server.server_port}/decide",data=json.dumps(p).encode(),headers={"Content-Type":"application/json"})
                    with urlopen(req,timeout=5) as response: out=json.load(response)["decision"]
                    ms=(time.perf_counter_ns()-begin)/1e6
                    if n>=25:assert out.get("predictionTrial")==n-2 and out["guardVerdict"]=="ALLOW",(n,out)
                    result.append(dict(trial=n,roundtrip_ms=ms,before_500ms=ms<500,action=out["action"],reason=out["reason"]))
                else:service.receive(p)
                history.append(t)
        finally:server.shutdown();worker.join();server.server_close()
    a=np.array([r["roundtrip_ms"] for r in result])
    data=dict(scope="synthetic localhost HTTP; RF and raw feature processing; repeated L0 ON, no physical enactment",samples=result,
              timely=int(sum(a<500)),total=len(a),median_ms=float(np.median(a)),p95_ms=float(np.percentile(a,95)),max_ms=float(max(a)))
    out=Path(__file__).resolve().parents[2]/"Evidence/SlidingControl/http-timing.json"
    out.write_text(json.dumps(data,indent=2));print({k:v for k,v in data.items() if k!="samples"})
if __name__=="__main__":run()

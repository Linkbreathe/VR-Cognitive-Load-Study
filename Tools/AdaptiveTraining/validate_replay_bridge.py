"""Engineering-only telemetry fixture for validating a real LSL replay receiver."""
import argparse
import json
from pathlib import Path
import time
import urllib.request


def main():
    p=argparse.ArgumentParser();p.add_argument("--url",default="http://127.0.0.1:8766");p.add_argument("--duration",type=float,default=75);p.add_argument("--output",type=Path,default=Path("Evidence/Stage2/p007-bridge-validation.json"));a=p.parse_args()
    began=time.perf_counter();sid="p007_replay_fixture_"+str(time.time_ns())
    packet=dict(sessionId=sid,configVersion="2.0.0-stage2-dev3",sessionType="engineering",participantCode="P007-recorded-replay",
        requestId=sid+":1",stage="replay_fixture",level=0,block=1,trial=0,clockOffset=0,clockUncertainty=0,
        config=dict(mode="OFF",controlBasis="behavioral_fallback",data_origin="recorded_xdf_replay",task_context="synthetic_telemetry_fixture_not_VR"),blocks=[],
        events=[dict(type="block_start",actual=began,planned=began,frame=0,block=1,trial=0,detail="level=0;stage=replay_fixture")])
    updates=0
    while time.perf_counter()-began<a.duration:
        packet["now"]=time.perf_counter()
        request=urllib.request.Request(a.url+"/telemetry",json.dumps(packet).encode(),{"Content-Type":"application/json"})
        with urllib.request.urlopen(request,timeout=5) as response:json.load(response)
        packet["events"]=[];updates+=1;time.sleep(.5)
    with urllib.request.urlopen(a.url+"/status") as response:status=json.load(response)
    features=status["features"]
    assert features,"No features received from recorded LSL replay"
    assert all(f["synthetic"] for f in features),"Replay feature must never masquerade as live"
    result=dict(session_id=sid,telemetry_updates=updates,data_origin="recorded_xdf_replay",task_context="synthetic_telemetry_fixture_not_VR",
        physiology_control_eligible=False,source="P007 recorded relaxation-task XDF",sensor_status=status["sensors"],feature_names=sorted({f["name"] for f in features}),status="passed")
    a.output.write_text(json.dumps(result,indent=2),encoding="utf-8");print(json.dumps(result,indent=2),flush=True)


if __name__=="__main__":main()

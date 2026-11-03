"""Finite synthetic outlets with deliberately shared identities for engineering tests."""
import json
from pathlib import Path
import sys
import time
import pylsl

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"Tools/AdaptiveTraining"))
from lsl_sources import stream_identity

def outlet(name,kind,count,rate,fmt):
    info=pylsl.StreamInfo(name,kind,count,rate,fmt,"source-first-shared-id")
    info.desc().append_child("xdf_replay").append_child_value("recorded_data","true")
    channels=info.desc().append_child("channels")
    for n in range(count):channels.append_child("channel").append_child_value("label",f"fixture_{n}")
    return pylsl.StreamOutlet(info)

a=outlet("Source First Numeric","EEG",2,250,"float32")
b=outlet("Source First Numeric","EEG",2,250,"float32")
marker=outlet("Source First Markers","Markers",1,0,"string")
Path("Evidence/LslSourceFirst/fixture-outlets.json").write_text(json.dumps({"streams":[stream_identity(x.get_info()) for x in (a,b,marker)]},indent=2))
start=time.perf_counter();next_numeric=start;next_marker=start
while time.perf_counter()-start<240:
    now=time.perf_counter()
    if now>=next_numeric:
        a.push_sample([111,222]);b.push_sample([999,888]);next_numeric+=.004
    if now>=next_marker:
        marker.push_sample(["fixture_event"]);next_marker+=8
    time.sleep(.001)

"""Finite engineering-only LSL publisher for the source-selection acceptance probe."""
import math
import time
import pylsl

raw=pylsl.StreamInfo("Source Panel EEG Fixture","EEG",2,250,"float32","source-panel-eeg-fixture")
raw.desc().append_child("xdf_replay").append_child_value("recorded_data","true")
values=pylsl.StreamInfo("Source Panel Features Fixture","Features",2,2,"float32","source-panel-features-fixture")
values.desc().append_child("xdf_replay").append_child_value("recorded_data","true")
channels=values.desc().append_child("channels")
for label in ("fixture_alpha","fixture_heart_rate"):
    channels.append_child("channel").append_child_value("label",label)
outlet=pylsl.StreamOutlet(raw);feature_outlet=pylsl.StreamOutlet(values)
start=time.perf_counter();next_raw=start;next_feature=start
while time.perf_counter()-start<300:
    now=time.perf_counter()
    if now>=next_raw:
        outlet.push_sample([math.sin(next_raw*2*math.pi*6),math.sin(next_raw*2*math.pi*10)])
        next_raw+=.004
    if now>=next_feature:
        feature_outlet.push_sample([.5,72]);next_feature+=.5
    time.sleep(.001)

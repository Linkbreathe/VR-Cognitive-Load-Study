"""Local synthetic LSL marker transport and sample-clock test; no physical signals."""
import json
from pathlib import Path
import sys
import time
from datetime import datetime, timezone

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/AdaptiveTraining'))
import pylsl
from study_timing import StudyTiming


def main():
    timing=StudyTiming(True)
    if timing.outlet is None:raise RuntimeError(timing.marker_error)
    streams=pylsl.resolve_byprop('source_id',timing.source_id,timeout=3)
    if len(streams)!=1:raise RuntimeError('Synthetic marker outlet was not discoverable')
    inlet=pylsl.StreamInlet(streams[0]);inlet.open_stream(timeout=3)
    correction=inlet.time_correction(timeout=3)
    root=ROOT/'Evidence/LayerAB/LslLoopback'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S')
    root.mkdir(parents=True,exist_ok=False)
    before=time.perf_counter();snapshot=timing.snapshot();after=time.perf_counter()
    # Synthetic Unity clock is precisely 100 seconds behind this test process.
    snapshot.update(id='synthetic-clock-1',unitySent=before-100,unityReceived=after-100)
    event_perf=time.perf_counter();event_unity=event_perf-100
    packet={'sessionId':'SYNTHETIC_LSL_LOOPBACK','participantCode':'SYNTHETIC','now':event_unity,'abClock':snapshot}
    timing.record({'path':root,'kind':'synthetic'},packet,{'type':'input_edge','actual':event_unity,'detail':'synthetic_only'})
    values,stamp=inlet.pull_sample(timeout=3)
    if not values:raise RuntimeError('Marker was not received')
    payload=json.loads(values[0]);row=json.loads((root/'aligned_events.jsonl').read_text())
    if abs(stamp-row['marker_lsl_s'])>1e-6:raise AssertionError('LSL timestamp changed')
    if abs(payload['event_perf_s']-event_perf)>.01:raise AssertionError('Incorrect software-clock transfer')
    result={'provenance':'synthetic_local_transport_only','marker_source_id':timing.source_id,'received_timestamp_s':stamp,
            'explicit_timestamp_error_s':stamp-row['marker_lsl_s'],'known_clock_mapping_error_s':payload['event_perf_s']-event_perf,
            'lsl_inlet_correction_s':correction,'network_uncertainty_s':payload['network_uncertainty_s'],
            'physical_validation':False}
    (root/'result.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2));inlet.close_stream()


if __name__=='__main__':main()

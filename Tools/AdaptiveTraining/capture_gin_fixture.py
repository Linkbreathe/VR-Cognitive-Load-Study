"""Capture the explicitly selected Gin replay for an isolated simulation fixture."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import time

import numpy as np
import pylsl

from test_session_store import digest


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--selection",type=Path,required=True)
    p.add_argument("--output",type=Path,required=True)
    p.add_argument("--seconds",type=float,default=436)
    args=p.parse_args()
    selected=json.loads(args.selection.read_text(encoding="utf-8-sig"))["streams"][0]
    if selected["name"]!="Gin_mock_p007_test" or selected["data_origin"]!="recorded_xdf_replay":
        raise ValueError("This fixture requires the explicitly selected Gin recorded replay")
    infos=pylsl.resolve_byprop("uid",selected["uid"],timeout=5)
    if len(infos)!=1:raise ValueError("Selected outlet not found; do not substitute another source")
    inlet=pylsl.StreamInlet(infos[0],max_buflen=450,recover=False)
    info=inlet.info(timeout=3)
    if info.name()!=selected["name"] or info.source_id()!=selected["source_id"] or info.channel_count()!=9:
        raise ValueError("Selected outlet identity/layout changed")
    args.output.mkdir(parents=True,exist_ok=True)
    target=args.output/"raw-replay.npz"
    if target.exists():raise ValueError("Capture already exists; choose a fresh output folder")
    (args.output/"source.xml").write_text(info.as_xml(),encoding="utf-8")
    inlet.open_stream(timeout=3)
    timestamps=[];samples=[];chunks=[];began=time.monotonic();next_update=0
    while time.monotonic()-began<args.seconds:
        x,t=inlet.pull_chunk(timeout=.5,max_samples=2048)
        if t:
            chunks.append(dict(first=len(timestamps),count=len(t),receivedLslClock=pylsl.local_clock(),
                receivedElapsed=time.monotonic()-began))
            timestamps.extend(t);samples.extend(x)
        elapsed=time.monotonic()-began
        if elapsed>=next_update:
            print(json.dumps(dict(elapsedSeconds=round(elapsed,1),samples=len(samples))),flush=True)
            next_update=elapsed+30
    inlet.close_stream()
    np.savez_compressed(target,timestamps=np.asarray(timestamps),samples=np.asarray(samples,dtype=np.float32))
    manifest=dict(revision="gin-recorded-signal-simulation-v1",capturedUtc=datetime.now(timezone.utc).isoformat(),
        selection=selected,rawFile=str(target.resolve()),sha256=digest(target),samples=len(samples),
        sourceTimestampsPreserved=True,syntheticBehaviorPairedLater=True,participantEvidence=False,
        note="Numeric payload received from the exact selected replay. Generated responses are paired offline by elapsed source time, not real task responses.",chunks=chunks)
    (args.output/"capture.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
    print(json.dumps({k:v for k,v in manifest.items() if k not in ("chunks","selection")}),flush=True)


if __name__=="__main__":main()

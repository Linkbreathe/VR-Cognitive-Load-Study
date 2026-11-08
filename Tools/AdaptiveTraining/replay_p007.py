"""Replay the user's P007 recording through its existing XDF scheduler.

All outlets and downstream records are explicitly recorded-data simulation.
The original P007 project and XDF files are read-only inputs.
"""

if __name__ == "__main__":
    raise SystemExit("Retired P007/Gin workflow. Use StartEvidenceConsole.cmd with the Mindtooth device.")

import argparse
import hashlib
import json
from pathlib import Path
import sys
import time


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--project",type=Path,default=Path(r"D:\Code-Space\patent_code"))
    parser.add_argument("--xdf",type=Path,default=Path(r"D:\Code-Space\patent_code\P007\EEG-ECG-unlabeled-07-12min.xdf"))
    parser.add_argument("--output",type=Path,default=Path("Evidence/Stage2/P007Replay"))
    parser.add_argument("--duration",type=float,default=300)
    parser.add_argument("--repeat",type=int,default=1)
    parser.add_argument("--warmup",type=float,default=15)
    args=parser.parse_args()
    if args.duration<=0 or args.repeat<1 or args.warmup<0:raise ValueError("Invalid replay bounds")
    sys.path.insert(0,str(args.project.resolve()))
    from xdf_replay.xdf_replay import load_streams,build_plan,create_outlets,replay_cycles
    import pylsl
    import numpy as np
    args.output.mkdir(parents=True,exist_ok=True)
    plan=build_plan(load_streams(args.xdf),args.xdf,names=["02_007_5_eeg"],duration=args.duration)
    outlets=create_outlets(plan,speed=1,name_prefix="Stage2_RECORDED_P007_")
    source_id=outlets[0].get_info().source_id()
    rate=outlets[0].get_info().nominal_srate()
    config={"streams":[
        dict(kind="EEG",source_id=source_id,sample_rate=rate,channels=[1,2,3,4],scale_to_units=1,
             amplitude_limit=300,window_seconds=12,synthetic=True,data_origin="recorded_xdf_replay"),
        dict(kind="ECG",source_id=source_id,sample_rate=rate,channels=[7,8],differential=True,scale_to_units=1,
             amplitude_limit=5000,window_seconds=30,hrv_window_seconds=60,synthetic=True,data_origin="recorded_xdf_replay")],
        "note":"Recorded P007 relaxation-task signals, not live physiology and not calibrated anti-distraction evidence."}
    (args.output/"lsl-replay-config.json").write_text(json.dumps(config,indent=2),encoding="utf-8")
    manifest={"source_file":str(args.xdf.resolve()),"source_sha256":hashlib.sha256(args.xdf.read_bytes()).hexdigest(),
        "scheduler":str((args.project/"xdf_replay/xdf_replay.py").resolve()),"scheduler_sha256":hashlib.sha256((args.project/"xdf_replay/xdf_replay.py").read_bytes()).hexdigest(),
        "source_id":source_id,"sample_rate":rate,"selected_stream":"02_007_5_eeg","selected_samples":len(plan[0].samples),
        "source_offset_seconds":[float(plan[0].offsets[0]),float(plan[0].offsets[-1])],"speed":1,
        "repetitions":args.repeat,"recorded_data":True,"synthetic":True,"data_origin":"recorded_xdf_replay",
        "eeg_zero_based_columns":[1,2,3,4],"ecg_derivation":"column 7 minus column 8; microvolts",
        "purpose":"transport, feature and monitor engineering validation only; cannot support physiological UP"}
    (args.output/"replay-manifest.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
    print("RECORDED P007 REPLAY - NOT LIVE PHYSIOLOGY",flush=True)
    print("Receiver config: "+str(args.output/"lsl-replay-config.json"),flush=True)
    print(f"Outlets ready; {args.warmup:g} seconds for receiver discovery.",flush=True)
    time.sleep(args.warmup)
    def cycle(index,counts):
        with (args.output/"replay-cycles.jsonl").open("a",encoding="utf-8") as file:
            file.write(json.dumps({"cycle":index,"counts":counts,"finished_lsl_clock":pylsl.local_clock(),"recorded_data":True})+"\n")
    sent=replay_cycles(plan,outlets,repetitions=args.repeat,speed=1,cycle_gap=1,on_cycle=cycle)
    time.sleep(1)
    manifest["sent_counts"]=sent;manifest["status"]="completed"
    (args.output/"replay-manifest.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
    print(json.dumps({"status":"completed","sent_counts":sent}),flush=True)


if __name__=="__main__":main()

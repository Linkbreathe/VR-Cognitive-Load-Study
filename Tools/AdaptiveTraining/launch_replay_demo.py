"""Launch the labeled P007 replay and local bridge; optionally configure Editor Play Mode.

No headset or live physiological validation is implied. Ctrl+C restores the prior Editor configuration.
"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import time


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--editor",action="store_true",help="Temporarily enable the Editor engineering demo and install prepared media")
    p.add_argument("--mode",choices=["OFF","SHADOW","ACTIVE"],default="SHADOW")
    p.add_argument("--port",type=int,default=8765)
    p.add_argument("--repeat",type=int,default=6)
    p.add_argument("--duration",type=float,default=300)
    p.add_argument("--xdf",type=Path,default=Path(r"D:\Code-Space\patent_code\P007\EEG-ECG-unlabeled-07-12min.xdf"))
    a=p.parse_args()
    root=Path(__file__).resolve().parents[2];os.chdir(root)
    with socket.socket() as sock:
        if sock.connect_ex(("127.0.0.1",a.port))==0:raise RuntimeError(f"Port {a.port} already in use; stop the earlier study service first")
    output=root/"Evidence"/"Stage2"/("ReplayDemo-"+datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ"));output.mkdir(parents=True)
    processes=[];handles=[];restore=[]
    try:
        if a.editor:
            media=root/"Builds/Stage2MediaRelease"
            persistent=Path(os.environ["USERPROFILE"])/"AppData/LocalLow/EvidenceVR/Evidence-Aware VR Task Platform"
            persistent.mkdir(parents=True,exist_ok=True);(persistent/"AdaptiveMedia").mkdir(exist_ok=True)
            backup=output/"editor-backup";backup.mkdir()
            for name in ["adaptive-training-config.json","adaptive-media-manifest.json"]:
                target=persistent/name;prior=target.read_bytes() if target.exists() else None
                restore.append((target,prior))
                if prior is not None:(backup/name).write_bytes(prior)
            for source in (media/"AdaptiveMedia").glob("*"):
                target=persistent/"AdaptiveMedia"/source.name
                if target.exists() and target.read_bytes()!=source.read_bytes():
                    raise RuntimeError(f"Existing media differs; preserve or relocate it before installation: {target}")
                shutil.copy2(source,target)
            shutil.copy2(media/"adaptive-media-manifest.json",persistent/"adaptive-media-manifest.json")
            config=json.loads((root/"Assets/TaskPlatform/Config/adaptive-training-config.json").read_text())
            config.update(enabled=True,mode=a.mode,participantCode="P007-recorded-replay-demo",sessionType="engineering",
                controlBasis="behavioral_fallback",controllerUrl=f"http://127.0.0.1:{a.port}")
            (persistent/"adaptive-training-config.json").write_text(json.dumps(config,indent=2),encoding="utf-8")
        def launch(script,args,log):
            handle=(output/log).open("w",encoding="utf-8");handles.append(handle)
            proc=subprocess.Popen([sys.executable,str(root/"Tools/AdaptiveTraining"/script),*args],stdout=handle,stderr=subprocess.STDOUT,
                creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0));processes.append(proc);return proc
        replay=launch("replay_p007.py",["--xdf",str(a.xdf),"--output",str(output/"source"),"--repeat",str(a.repeat),"--duration",str(a.duration),"--warmup","15"],"replay.log")
        config=output/"source/lsl-replay-config.json";deadline=time.perf_counter()+45
        while not config.exists():
            if replay.poll() is not None or time.perf_counter()>deadline:raise RuntimeError(f"Replay failed to initialize; inspect {output/'replay.log'}")
            time.sleep(.2)
        service=launch("service.py",["--port",str(a.port),"--output",str(output/"service"),"--lsl-config",str(config)],"service.log")
        print(f"RECORDED P007 REPLAY / SIMULATION. Behavioral control only. Monitor: http://127.0.0.1:{a.port}/",flush=True)
        print("Enter Unity Play Mode for the Editor demo, or start the configured Quest app. Ctrl+C stops both processes.",flush=True)
        print("Evidence: "+str(output),flush=True)
        while replay.poll() is None:
            if service.poll() is not None:raise RuntimeError(f"Service stopped; inspect {output/'service.log'}")
            time.sleep(.5)
    except KeyboardInterrupt:
        print("Replay demo stopped.")
    finally:
        for proc in processes:
            if proc.poll() is None:
                if os.name=="nt":
                    subprocess.run(["taskkill","/PID",str(proc.pid),"/T","/F"],capture_output=True,check=False)
                else:proc.terminate()
        for proc in processes:
            try:proc.wait(timeout=5)
            except subprocess.TimeoutExpired:proc.kill();proc.wait()
        for handle in handles:handle.close()
        for target,prior in restore:
            if prior is None:target.unlink(missing_ok=True)
            else:target.write_bytes(prior)
        if a.editor:print("Prior Editor configuration restored for the next Play session. Prepared media retained.")


if __name__=="__main__":main()

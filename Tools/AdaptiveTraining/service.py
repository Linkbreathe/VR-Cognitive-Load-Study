"""Local study-computer bridge, LSL acquisition, monitor, and auditable decisions.

Run from the project root: python Tools/AdaptiveTraining/service.py --output Evidence/Stage2Service
"""
from __future__ import annotations

import argparse
import copy
from collections import deque
import csv
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import math
from pathlib import Path
import re
import threading
import time
import sys

from controller import BaselineAdaptiveController, VERSION, SUPPORTED_VERSIONS, baseline
from features import extract_features, condition_coverage
from lsl_sources import stream_identity, validate_config, matching_streams, observation_values
from personalized_model import VERSION as PERSONAL_VERSION, PROTOCOL as PERSONAL_PROTOCOL
from personalized_runtime import PersonalSession


def append_json(path, value):
    def clean(v):
        if isinstance(v,float) and not math.isfinite(v):return None
        if isinstance(v,list):return [clean(x) for x in v]
        if isinstance(v,dict):return {k:clean(x) for k,x in v.items()}
        return v
    with path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(clean(value), allow_nan=False, separators=(",", ":"))+"\n")


def latest_features(features):
    """Retain the latest value of every stream/feature, including slower ECG outputs."""
    latest={}
    for feature in features:
        latest[(feature["source_id"],feature["name"])]=feature
    return list(latest.values())


def physiology_display_status(sensors, now):
    """Report source provenance independently of feature quality or control eligibility."""
    rows=[dict(sensor) for sensor in list(sensors.values())]
    if not rows:return {"source":"none","connection":"waiting","sourceName":"","streams":[]}
    origins=set()
    fresh=0
    for sensor in rows:
        origin=sensor.get("data_origin","unknown")
        origins.add("recorded_replay" if origin=="recorded_xdf_replay" else "synthetic" if sensor.get("synthetic")
            else "live" if origin=="live_sensor" else "unknown")
        if sensor.get("connected") and 0<=now-sensor.get("last_sample",float("-inf"))<=3:fresh+=1
    source=next(iter(origins)) if len(origins)==1 else "mixed" if origins & {"recorded_replay","synthetic"} else "unknown"
    connection="receiving" if fresh==len(rows) else "partial" if fresh else "disconnected" if any("last_sample" in s for s in rows) else "waiting"
    if not fresh and all(s.get("connected") and s.get("sample_rate") == 0 for s in rows):connection="idle"
    identities=[{"name":s.get("name", ""), "sourceId":s.get("source_id", ""), "kind":s.get("kind", ""),
                 "origin":s.get("data_origin", "unknown"),"uid":s.get("uid", ""),"streamType":s.get("stream_type", "")} for s in rows]
    names=list(dict.fromkeys(s["name"] or s["sourceId"] for s in identities if s["name"] or s["sourceId"]))
    return {"source":source,"connection":connection,"sourceName":" + ".join(names),"streams":identities}


class StudyService:
    def __init__(self, output, calibration=None, policy=None, personalized_policy=None):
        self.output = Path(output)
        self.calibration = calibration or {}
        self.controller = BaselineAdaptiveController(policy)
        self.sessions = {}
        self.latest = None
        self.lock = threading.RLock()
        self.sensors = {}
        self.personalized_policy = personalized_policy

    def receive(self, packet, decide=False):
        packet = copy.deepcopy(packet)
        now = time.perf_counter()
        if packet.get("configVersion") not in (*SUPPORTED_VERSIONS, PERSONAL_VERSION):
            raise ValueError("Configuration version mismatch")
        sid = packet.get("sessionId", "")
        if not re.fullmatch(r"[A-Za-z0-9_-]{1,100}", sid):
            raise ValueError("Invalid session identifier")
        kind = packet.get("sessionType")
        if kind not in ("participant", "editor-test", "engineering", "synthetic"):
            raise ValueError("Unknown session type")
        with self.lock:
            session = self.sessions.get(sid)
            if session is None:
                folder = self.output / kind / sid
                folder.mkdir(parents=True, exist_ok=True)
                session = {"path": folder, "config":packet["config"], "kind":kind, "features":deque(maxlen=1000),
                           "conditions":[], "seen_events":set(), "decisions":{}, "packet":packet}
                metadata=folder/"session.json"
                if metadata.exists():
                    saved=json.loads(metadata.read_text(encoding="utf-8"))
                    if saved["config"]!=packet["config"] or saved["sessionType"]!=kind or saved["policy"]!=self.controller.policy or saved["calibration"]!=self.calibration:
                        raise ValueError("Persisted session configuration differs; start a new session")
                    decisions_path=folder/"controller_decisions.jsonl"
                    if decisions_path.exists():
                        for line in decisions_path.read_text(encoding="utf-8").splitlines():
                            saved_decision=json.loads(line)["decision"]
                            session["decisions"][saved_decision["requestId"]]=saved_decision
                    append_json(folder/"service_restarts.jsonl",{"time":now,"condition_coverage":"reacquiring"})
                else:metadata.write_text(json.dumps({"sessionId":sid, "sessionType":kind,
                    "config":packet["config"], "policy":self.controller.policy, "calibration":self.calibration,
                    "status":"in_progress", "clock":"Python perf_counter monotonic seconds; see clock_sync.jsonl"}, indent=2),encoding="utf-8")
                (folder/"calibration.json").write_text(json.dumps(self.calibration,indent=2),encoding="utf-8")
                if packet.get("configVersion") == PERSONAL_VERSION:
                    session["personal"] = PersonalSession(packet, folder, self.personalized_policy)
                self.sessions[sid] = session
            if session["config"] != packet["config"] or session["kind"] != kind:
                raise ValueError("Session configuration is immutable")
            if "personal" in session and packet["now"] < session["packet"]["now"]:
                raise ValueError("Stale personalized telemetry timestamp")
            session["packet"] = packet
            session["received"] = now
            self.latest = sid
            append_json(session["path"]/"telemetry.jsonl", {"received_monotonic":now,"packet":packet})
            append_json(session["path"]/"clock_sync.jsonl", {"received_monotonic":now,"unity_now":packet["now"],
                "offset":packet.get("clockOffset"), "uncertainty":packet.get("clockUncertainty",999)})
            for event in packet.get("events", []):
                key = json.dumps(event, sort_keys=True)
                if key in session["seen_events"]:
                    continue
                session["seen_events"].add(key)
                append_json(session["path"]/"events.jsonl",event)
                if event["type"] in ("block_start", "adaptive_level_changed", "pause", "resume", "session_complete", "session_aborted"):
                    detail = dict(part.split("=",1) for part in event.get("detail","").split(";") if "=" in part)
                    condition = {"time":event["actual"], "level":int(detail.get("level", packet["level"])),
                                 "stage":detail.get("stage", packet["stage"]),
                                 "running":event["type"] in ("block_start", "adaptive_level_changed", "resume")}
                    session["conditions"].append(condition)
                    if event["type"] in ("session_complete","session_aborted"):
                        metadata_path=session["path"]/"session.json"
                        meta=json.loads(metadata_path.read_text(encoding="utf-8"));meta["status"]=event["type"]
                        metadata_path.write_text(json.dumps(meta,indent=2),encoding="utf-8")
            features = list(session["features"])
            calibration = dict(self.calibration, behavioral_baseline=baseline(packet.get("blocks",[])))
            reply = {"serverClock":time.perf_counter(), "featuresJson":json.dumps(latest_features(features)),
                     "calibrationJson":json.dumps(calibration),"physiologyStatus":physiology_display_status(self.sensors,time.perf_counter())}
            if "personal" in session:
                reply.update(session["personal"].receive(packet, decide))
            elif decide:
                key = packet["requestId"]
                if key not in session["decisions"]:
                    decision = self.controller.decide(packet,features,self.calibration)
                    session["decisions"][key] = decision
                    append_json(session["path"]/"controller_decisions.jsonl", {"packet":packet,"features":features,
                        "calibration":self.calibration,"policy":self.controller.policy,"decision":decision})
                reply["decision"] = session["decisions"][key]
            reply["serverClock"] = time.perf_counter()
            return reply

    def record_signal(self, stream_config, raw_times, corrected_monotonic, samples, correction, source_id):
        with self.lock:
            if self.latest is None:
                return
            session = self.sessions[self.latest]
            if session["kind"]=="participant" and stream_config.get("synthetic"):
                raise ValueError("Recorded/synthetic signals cannot be written into participant sessions")
            if time.perf_counter()-session.get("received",0)>5:
                return
            if "personal" in session:
                session["personal"].ingest(stream_config, corrected_monotonic, samples, source_id)
            append_json(session["path"]/"raw_signals.jsonl", {"stream":stream_config,"source_id":source_id,
                "raw_lsl_timestamps":raw_times,"corrected_monotonic":corrected_monotonic,"samples":samples,
                "lsl_time_correction":correction,"received_monotonic":time.perf_counter(),"synthetic":stream_config.get("synthetic",False)})

    def add_features(self, stream_config, times, samples, source_id):
        with self.lock:
            if self.latest is None:
                return
            sid = self.latest
            session = self.sessions[sid]
            if "personal" in session:
                return  # Dev9 extracts only completed, trial-aligned windows in PersonalSession.
            packet = session["packet"]
            if session["kind"]=="participant" and stream_config.get("synthetic"):
                raise ValueError("Recorded/synthetic features cannot be written into participant sessions")
            if time.perf_counter()-session.get("received",0)>5:
                return
            offset = packet.get("clockOffset",0)
            start,end=times[0]-offset,times[-1]-offset
            levels,stages,covered=condition_coverage(start,end,session["conditions"])
            values,quality=extract_features(stream_config["kind"], times, samples, stream_config["sample_rate"],
                stream_config["window_seconds"], stream_config["amplitude_limit"])
            append_json(session["path"]/"feature_windows.jsonl", {"stream":stream_config,"timestamps":times,"samples":samples,
                "source_id":source_id,"quality":quality,"features":values,"window_start":start,"window_end":end,
                "levels":levels,"stages":stages,"coverage_complete":covered})
            for f in values:
                if stream_config.get("feature_scope")=="hrv_only" and "rmssd" not in f["name"]:
                    continue
                record=dict(f,sessionId=sid,configVersion=packet["configVersion"],source_id=source_id,quality=quality["quality"],
                    quality_reason=quality["reason"],window_start=start,window_end=end,
                    levels=levels,stages=stages,coverage_complete=covered,synthetic=stream_config.get("synthetic",False),
                    clock_uncertainty=packet.get("clockUncertainty",999),algorithm="engineering-welch-rr-v1")
                session["features"].append(record)
                path=session["path"]/"physio_features.csv"
                exists=path.exists()
                with path.open("a",newline="",encoding="utf-8") as f:
                    writer=csv.DictWriter(f,fieldnames=list(record))
                    if not exists:writer.writeheader()
                    writer.writerow(record)
            self.sensors[stream_config["kind"]+":"+source_id].update(quality=quality, last_feature=time.perf_counter(),feature_count=len(values))

    def status(self):
        with self.lock:
            session=self.sessions.get(self.latest)
            return {"clock":time.perf_counter(),"sensors":self.sensors,"session":self.latest,
                "physiologyStatus":physiology_display_status(self.sensors,time.perf_counter()),
                "latest_telemetry":session.get("received") if session else None,
                "task":session["packet"] if session else None,
                "features":list(session["personal"].predictions)[-5:] if session and "personal" in session else latest_features(session["features"]) if session else [],
                "decisions":list(session["personal"].decisions.values())[-3:] if session and "personal" in session else list(session["decisions"].values())[-3:] if session else [],
                "personalModel": session["personal"].report if session and "personal" in session else None}


def lsl_worker(service, config):
    import pylsl
    while True:
        stream_id=config["source_id"]
        sensor_key=config["kind"]+":"+stream_id
        service.sensors.setdefault(sensor_key,{})
        service.sensors[sensor_key].update(connected=False,state="discovering",kind=config["kind"],name=config.get("name", ""),source_id=stream_id,uid=config.get("uid", ""),stream_type=config.get("stream_type", ""),sample_rate=config["sample_rate"],synthetic=config.get("synthetic",False),data_origin=config.get("data_origin","unknown"))
        inlet = None
        try:
            lookup="uid" if config.get("uid") else "source_id"
            streams=matching_streams(pylsl.resolve_byprop(lookup,config.get("uid") or stream_id,minimum=2,timeout=2),config)
            if len(streams)!=1:
                service.sensors[sensor_key]["state"] = "selected_outlet_not_found; refresh and reselect after publisher restart" if not streams else "ambiguous_source; select an outlet UID"
                time.sleep(1)
                continue
            inlet=pylsl.StreamInlet(streams[0],max_buflen=180,recover=False)
            identity=stream_identity(inlet.info(timeout=2))
            recorded=identity["recorded"]
            if config.get("channel_count") and config["channel_count"]!=identity["channel_count"]:
                raise ValueError("Outlet channel count differs from selected source")
            if config.get("channel_format") and config["channel_format"]!=identity["channel_format"]:
                raise ValueError("Outlet sample format differs from selected source")
            config=dict(config,synthetic=config.get("synthetic",False) or recorded,
                        name=identity["name"],uid=identity["uid"],stream_type=identity["stream_type"],channel_count=identity["channel_count"],channel_format=identity["channel_format"],channel_labels=identity["channel_labels"],
                        data_origin="recorded_xdf_replay" if recorded else config.get("data_origin","live_sensor"))
            validate_config({"streams":[config]})
            service.sensors[sensor_key].update(name=config["name"],uid=config["uid"],stream_type=config["stream_type"],data_origin=config["data_origin"],synthetic=config["synthetic"])
            rate=identity["sample_rate"]
            if abs(rate-config["sample_rate"])>.01:
                raise ValueError("Stream sample rate differs from frozen device configuration")
            rows=deque(maxlen=int(rate*65)+10)
            correction=inlet.time_correction(timeout=2)
            if rate==0:service.sensors[sensor_key].update(connected=True,state="idle")
            next_correction=time.perf_counter()+5
            last_sample=time.perf_counter();next_feature=0
            while True:
                samples,timestamps=inlet.pull_chunk(timeout=.25,max_samples=max(1,min(int(rate) if rate else 256,8192)))
                now=time.perf_counter()
                if now>=next_correction:
                    correction=inlet.time_correction(timeout=1);next_correction=now+5
                if not samples:
                    if rate>0 and now-last_sample>3:
                        raise RuntimeError("No samples for three seconds")
                    continue
                last_sample=now
                clock_delta=time.perf_counter()-pylsl.local_clock()
                corrected=[t+correction+clock_delta for t in timestamps]
                service.sensors[sensor_key].update(connected=True,state="receiving",last_sample=now)
                channels=config["channels"]
                selected=[[sample[i] if config["kind"]=="OBSERVE" else sample[i]*config.get("scale_to_units",1) for i in channels] for sample in samples]
                if config.get("differential"):
                    if len(channels)!=2:raise ValueError("Differential ECG requires exactly two columns")
                    selected=[[row[0]-row[1]] for row in selected]
                service.record_signal(config,timestamps,corrected,selected,correction,stream_id)
                if config["kind"] in ("FEATURES","OBSERVE"):
                    service.sensors[sensor_key].update(external_values=observation_values(config,selected),
                        control_eligible=False,processing="external values only; no calibrated feature admission")
                    continue
                rows.extend(zip(corrected,selected))
                if now>=next_feature and rows:
                    next_feature=now+2
                    window=[r for r in rows if r[0]>=rows[-1][0]-config["window_seconds"]]
                    service.add_features(config,[r[0] for r in window],[r[1] for r in window],stream_id)
                    if config["kind"]=="ECG" and config.get("hrv_window_seconds",60)==60:
                        hrv=[r for r in rows if r[0]>=rows[-1][0]-60]
                        hrv_config=dict(config,window_seconds=60,feature_scope="hrv_only")
                        service.add_features(hrv_config,[r[0] for r in hrv],[r[1] for r in hrv],stream_id)
        except Exception as error:
            service.sensors[sensor_key].update(connected=False,state=str(error))
            time.sleep(1)
        finally:
            if inlet is not None:inlet.close_stream()


MONITOR = b'''<!doctype html><html lang="en"><meta charset="utf-8"><title>Stage 2 monitor</title>
<style>body{font:17px system-ui;background:#172127;color:#e8eee9;margin:32px}h1{font-size:24px}pre{white-space:pre-wrap;padding:20px;background:#24333b;border-radius:8px}.note{color:#b9c9cf}</style>
<h1>Adaptive training monitor</h1><p id="origin" class="note">Engineering observations. Feature validity and physiological calibration are separate requirements.</p>
<div id="state">Waiting for service...</div><pre id="task"></pre><pre id="sensors"></pre><pre id="features"></pre><pre id="decisions"></pre>
<script>async function refresh(){try{let s=await(await fetch('/status')).json();let t=s.task;
document.getElementById('state').textContent=t?'Session '+s.session+' | telemetry age '+(s.clock-s.latest_telemetry).toFixed(1)+' s':'No Unity session connected';
document.getElementById('origin').textContent=Object.values(s.sensors).some(x=>x.synthetic)?'RECORDED XDF REPLAY / SIMULATION - NOT LIVE PHYSIOLOGY. Cannot authorize physiological promotion.':'Engineering observations. Feature validity and physiological calibration are separate requirements.';
document.getElementById('task').textContent=t?JSON.stringify({stage:t.stage,level:t.level,block:t.block,trial:t.trial,media:t.mediaId,mode:t.config.mode,basis:t.config.controlBasis,gazeValid:t.gazeValidFraction,gazeTask:t.gazeTaskFraction,gazeVideo:t.gazeVideoFraction,lastBlock:t.blocks.at(-1)},null,2):'Task unavailable';
for(let k of ['sensors','features','decisions'])document.getElementById(k).textContent=k.toUpperCase()+'\n'+JSON.stringify(s[k],null,2);
}catch(e){document.getElementById('state').textContent='Service disconnected';}}refresh();setInterval(refresh,1000);</script></html>'''


def make_handler(service):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self,*args):
            pass
        def reply(self,code,data,content_type="application/json"):
            body=data if isinstance(data,bytes) else json.dumps(data,allow_nan=False).encode()
            self.send_response(code);self.send_header("Content-Type",content_type);self.send_header("Content-Length",str(len(body)));self.end_headers();self.wfile.write(body)
        def do_GET(self):
            if self.path=="/":self.reply(200,MONITOR,"text/html; charset=utf-8")
            elif self.path=="/status":self.reply(200,service.status())
            else:self.reply(404,{"error":"Not found"})
        def do_POST(self):
            if self.path not in ("/telemetry","/decide"):
                self.reply(404,{"error":"Not found"});return
            try:
                length=int(self.headers.get("Content-Length",0))
                if not 0<length<=2_000_000:raise ValueError("Invalid packet length")
                packet=json.loads(self.rfile.read(length),parse_constant=lambda x:(_ for _ in ()).throw(ValueError("Nonfinite JSON")))
                self.reply(200,service.receive(packet,self.path=="/decide"))
            except (ValueError,KeyError,TypeError,FileExistsError) as error:
                self.reply(400,{"error":str(error)})
    return Handler


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--host",default="127.0.0.1");parser.add_argument("--port",type=int,default=8765)
    parser.add_argument("--output",type=Path,required=True);parser.add_argument("--calibration",type=Path)
    parser.add_argument("--lsl-config",type=Path);parser.add_argument("--policy",type=Path)
    parser.add_argument("--personalized-policy", type=Path, help="Dev9 frozen pilot policy; defaults never admit a physiological model")
    parser.add_argument("--log-file",type=Path)
    args=parser.parse_args()
    if args.log_file:
        args.log_file.parent.mkdir(parents=True,exist_ok=True)
        sys.stdout=args.log_file.open("a",encoding="utf-8",buffering=1);sys.stderr=sys.stdout
    read=lambda p:json.loads(p.read_text(encoding="utf-8-sig")) if p else None
    service=StudyService(args.output,read(args.calibration),read(args.policy),read(args.personalized_policy))
    selected=validate_config(read(args.lsl_config)) if args.lsl_config else None
    server=ThreadingHTTPServer((args.host,args.port),make_handler(service))
    if args.lsl_config:
        args.output.mkdir(parents=True,exist_ok=True)
        (args.output/"lsl-selection.json").write_text(json.dumps(selected,indent=2),encoding="utf-8")
        for stream in selected["streams"]:
            import pylsl  # Fail clearly when acquisition dependencies are unavailable.
            threading.Thread(target=lsl_worker,args=(service,stream),daemon=True).start()
    print(f"Stage 2 service: http://{args.host}:{args.port} | output={args.output}",flush=True)
    server.serve_forever()


if __name__=="__main__":
    main()

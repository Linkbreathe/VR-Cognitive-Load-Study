"""Clock transfer and optional LSL markers. No arrival-time substitution for event time."""
import json
import math
import time
import uuid

REVISION = "unity-perf-lsl-four-stamp-v1"


def append(path, row):
    with path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(row, allow_nan=False, separators=(",", ":")) + "\n")


def map_event(unity_time, sample, clock_id, packet_now):
    if not sample or sample.get("clockId") != clock_id:
        return {"alignment_status": "missing_or_foreign_clock"}
    keys = ("unitySent", "unityReceived", "serverReceived", "serverSent")
    if any(type(sample.get(k)) not in (int, float) or not math.isfinite(sample[k]) for k in keys):
        return {"alignment_status": "invalid_clock"}
    u0, u3, p1, p2 = (sample[k] for k in keys)
    rtt = (u3-u0)-(p2-p1)
    age = packet_now-u3
    if rtt < 0 or p2 < p1 or u3 < u0 or age < -.01 or age > 5:
        return {"alignment_status": "stale_or_invalid_clock"}
    offset = ((p1-u0)+(p2-u3))/2
    # An old unsent event is retained but is not presented as tightly synchronized.
    distance = abs(unity_time-(u0+u3)/2)
    status = "estimated" if rtt/2 <= .05 and distance <= 5 else "uncertain"
    return {"alignment_status": status, "clock_sample_id": sample.get("id"),
            "event_perf_s": unity_time+offset, "unity_to_perf_offset_s": offset,
            "network_uncertainty_s": rtt/2, "clock_distance_s": distance}


class StudyTiming:
    def __init__(self, enable_markers=False):
        self.clock_id = uuid.uuid4().hex
        self.lsl = self.outlet = None
        self.marker_error = "disabled"
        self.source_id = "EvidenceVR-TaskEvents-" + self.clock_id
        if enable_markers:
            try:
                import pylsl
                self.lsl = pylsl
                info = pylsl.StreamInfo("EvidenceVR_TaskEvents", "Markers", 1, 0, "string", self.source_id)
                info.desc().append_child_value("timing_revision", REVISION)
                info.desc().append_child_value("timestamp_note", "Estimated Unity software event time; inspect JSON uncertainty, not photon or switch onset")
                self.outlet = pylsl.StreamOutlet(info)
                self.marker_error = ""
            except Exception as error:
                self.marker_error = type(error).__name__ + ": " + str(error)

    def snapshot(self):
        received = time.perf_counter()
        row = {"clockId": self.clock_id, "serverReceived": received, "lslAvailable": self.lsl is not None,
               "lslClock": 0, "lslSamplePerf": 0, "lslReadUncertainty": 0,
               "markerSourceId": self.source_id, "markerStatus": self.marker_error or "available"}
        if self.lsl is not None:
            before = time.perf_counter(); lsl = self.lsl.local_clock(); after = time.perf_counter()
            row.update(lslClock=lsl, lslSamplePerf=(before+after)/2, lslReadUncertainty=(after-before)/2)
        row["serverSent"] = time.perf_counter()
        return row

    def record(self, session, packet, event):
        sample = packet.get("abClock")
        mapped = map_event(event["actual"], sample, self.clock_id, packet["now"])
        row = {"revision": REVISION, "sessionId": packet["sessionId"], "participantCode": packet["participantCode"],
               "sessionType": session["kind"], "event": event, "received_perf_s": time.perf_counter(),
               "marker_source_id": self.source_id, "marker_sent": False, **mapped}
        if self.outlet is not None and mapped["alignment_status"] == "estimated":
            try:
                before=time.perf_counter(); lsl=self.lsl.local_clock(); after=time.perf_counter()
                row["lsl_minus_perf_s"]=lsl-(before+after)/2
                row["lsl_clock_read_uncertainty_s"]=(after-before)/2
                row["marker_lsl_s"]=mapped["event_perf_s"]+row["lsl_minus_perf_s"]
                # Explicit historical timestamp, never the HTTP arrival timestamp.
                payload={k:v for k,v in row.items() if k!="marker_sent"}
                self.outlet.push_sample([json.dumps(payload, allow_nan=False)], timestamp=row["marker_lsl_s"])
                row["marker_sent"]=True
            except Exception as error:
                row["marker_error"]=str(error)
        elif self.marker_error:
            row["marker_error"]=self.marker_error
        append(session["path"] / "aligned_events.jsonl", row)

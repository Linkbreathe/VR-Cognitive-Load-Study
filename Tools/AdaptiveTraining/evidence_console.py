"""Read-only, bounded console projection. Never participates in control decisions."""
from collections import deque
from copy import deepcopy
from dataclasses import asdict
import math
import hashlib
import json
from pathlib import Path
import time
import threading


REVISION = "evidence-console-v1.21-decision-gaze"
WEB = Path(__file__).with_name("console")


class EegRateMonitor:
    """Measure delivered sample rows on the receiver clock, independent of Unity."""

    def __init__(self):
        self.lock = threading.Lock()
        self.source = None
        self.nominal = None
        self.receipts = deque()
        self.total = 0

    def select(self, config):
        with self.lock:
            self.source = config.get("name") or config["source_id"]
            self.nominal = config["sample_rate"]
            self.receipts.clear()
            self.total = 0

    def received(self, now, timestamps):
        with self.lock:
            if self.receipts and now - self.receipts[-1][0] >= 1:
                self.receipts.clear()
            self.total += len(timestamps)
            valid = all(math.isfinite(t) for t in timestamps) and all(
                b > a for a, b in zip(timestamps, timestamps[1:]))
            if self.receipts:
                valid = valid and timestamps[0] > self.receipts[-1][2]
            self.receipts.append((now, self.total, timestamps[-1], valid))
            # Retain one anchor at or before the two-second boundary.
            while len(self.receipts) > 1 and self.receipts[1][0] <= now - 2:
                self.receipts.popleft()

    def status(self, now):
        with self.lock:
            result = {"source": self.source, "nominalHz": self.nominal, "actualHz": None, "deliveryHz": None}
            if self.source is None:
                return dict(result, state="unconfigured")
            if not self.receipts or now - self.receipts[-1][0] >= 1:
                return dict(result, state="no_samples", actualHz=0, deliveryHz=0)
            start, first_count, first_lsl, _ = self.receipts[0]
            end, last_count, last_lsl, _ = self.receipts[-1]
            if end - start < 1:
                return dict(result, state="measuring")
            count = last_count - first_count
            delivery = count / (end - start)
            if not all(row[3] for row in self.receipts) or last_lsl <= first_lsl:
                return dict(result, state="timestamps", deliveryHz=round(delivery, 1))
            hz = count / (last_lsl - first_lsl)
            state = "low" if min(hz, delivery) < self.nominal * 0.90 else "receiving"
            return dict(result, state=state, actualHz=round(hz, 1), deliveryHz=round(delivery, 1))


def config_fingerprint(path, lsl=False):
    value = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    if lsl:
        from lsl_sources import validate_config
        value = validate_config(value)["streams"]
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def finite_json(value):
    if isinstance(value, float) and not math.isfinite(value):
        return None
    if isinstance(value, dict):
        return {str(k): finite_json(v) for k, v in value.items()}
    if isinstance(value, (tuple, list, deque)):
        return [finite_json(v) for v in value]
    return value


def record_wave(session, config, times, samples, source_id, retain_seconds=6):
    if config["kind"] not in ("EEG", "OBSERVE") or not times:
        return
    streams = session.setdefault("console_waves", {})
    key = config["kind"]
    identity = (source_id, config.get("uid"), tuple(config.get("channels", [])))
    if key not in streams or streams[key]["identity"] != identity:
        streams[key] = {"identity": identity, "config": deepcopy(config), "rows": deque(maxlen=12000)}
    rows = streams[key]["rows"]
    # Numeric observations are displayable without units or physiological admission.
    # Keep strings/nonfinite values as breaks rather than inventing numeric samples.
    for t, values in zip(times, samples):
        if not isinstance(t, (int, float)) or not math.isfinite(t):
            continue
        if rows and t <= rows[-1][0]:
            rows.clear()  # A clock restart must never join two incompatible time ranges.
        rows.append((t, [v if isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v)
                         else None for v in values[:16]]))
    while rows and rows[0][0] < rows[-1][0] - retain_seconds:
        rows.popleft()


def waves(session, now):
    result = []
    for stream in session.get("console_waves", {}).values():
        rows = list(stream["rows"])
        if not rows:
            continue
        rows = [row for row in rows if row[0] >= rows[-1][0] - 6]
        config = stream["config"]
        # Min/max envelopes retain visible peaks. Insert a break at actual timestamp gaps.
        stride = max(1, math.ceil(len(rows) / 300))
        channels = []
        for channel in range(min(16, max(len(row[1]) for row in rows))):
            if not any(channel < len(row[1]) and row[1][channel] is not None for row in rows):
                channels.append([])
                continue
            points = []
            for start in range(0, len(rows), stride):
                group = rows[start:start + stride]
                picks = {0, len(group) - 1}
                value = lambda row: row[1][channel] if channel < len(row[1]) else None
                numeric = [i for i in range(len(group)) if value(group[i]) is not None]
                if numeric:
                    picks.add(min(numeric, key=lambda i: value(group[i])))
                    picks.add(max(numeric, key=lambda i: value(group[i])))
                for i, row in enumerate(group):
                    previous = rows[start + i - 1] if start + i else row
                    if (config["sample_rate"] > 0 and row[0] - previous[0] > 2.5 / config["sample_rate"]) or value(row) is None:
                        points.append([row[0], None])
                    if i in picks or value(previous) is None:
                        points.append([row[0], value(row)])
            channels.append(points)
        result.append({"kind": config["kind"], "config": config, "channels": channels,
                       "lastSampleAge": max(0, now - rows[-1][0]), "start": rows[0][0], "end": rows[-1][0],
                       "displayOnly": True, "displayLimit": "6 s, 12000 samples, first 16 mapped channels; min/max envelope"})
    return result


def snapshot(service, session_id=None):
    """Copy under the service lock; plotting and JSON serialization happen outside it."""
    now = time.perf_counter()
    with service.lock:
        sid = session_id or service.latest
        if session_id and sid not in service.sessions:
            raise KeyError("Unknown session")
        s = service.sessions.get(sid)
        result = {"revision": REVISION, "clock": now, "session": sid,
                  "service": getattr(service, "console_info", {}),
                  "sessions": [{"id": k, "kind": v["kind"]} for k, v in list(service.sessions.items())[-100:]],
                  "sensors": deepcopy(service.sensors), "task": None, "decisions": [],
                  "windows": [], "predictions": [], "signals": [], "model": None, "guard": None}
        observed = {k: {"config": deepcopy(v["config"]), "rows": list(v["rows"])}
                    for k, v in service.observation_display.get("console_waves", {}).items()}
        if s:
            p = s["packet"]
            result.update(telemetryAge=max(0, now - s["received"]), output=str(s["path"]),
                          task={k: deepcopy(v) for k, v in p.items() if k not in ("trials", "blocks", "events", "guardGaze")},
                          decisions=deepcopy(list(s.get("console_decisions", {}).values())[-64:]),
                          acknowledgments=deepcopy(s.get("console_acks", {})),
                          events=deepcopy(list(s.get("console_events", []))))
            personal = s.get("personal")
            if personal:
                with personal.lock:
                    result.update(calibrationQuality=personal.calibration_quality(), windows=deepcopy(list(personal.windows.values())[-64:]),
                                  playback={"videoPlaying": bool(personal.video_playing)},
                                  predictions=deepcopy(list(personal.predictions)[-64:]),
                                  model={"status": personal.model_status, "report": deepcopy(personal.report),
                                         "mountIntact": personal.mount_intact, "policy": deepcopy(personal.policy)})
            adapter = s.get("guard")
            if adapter:
                result["guard"] = {"mode": adapter.mode, "failure": adapter.failure,
                                   "audit": deepcopy(list(adapter.console_audit))}
                core = adapter.guard
                if core:
                    for decision in result["decisions"]:
                        key=decision["delivered"]["requestId"]
                        if key in adapter.probe_reviews:
                            decision["probeReview"]=deepcopy(adapter.probe_reviews[key])
                        review = core.reviews.get(decision["delivered"]["requestId"])
                        if review:
                            decision["review"] = deepcopy(review[2])
                            ids = {c["judgment"] for path in review[2]["checks"] for c in path}
                            decision["judgments"] = [asdict(core.judgments[j]) for j in ids if j in core.judgments]
                            decision["revokedNow"] = [j for j in ids if j in core.revoked]
            # Small copy here; envelope processing below never holds up telemetry.
            # Stored rows are append-only copies owned by this projection; retain row references
            # while copying the deque so acquisition holds the lock only briefly.
            raw = {k: {"config": deepcopy(v["config"]), "rows": list(v["rows"])}
                   for k, v in s.get("console_waves", {}).items()}
        else:
            raw = {}
    result["signals"] = waves({"console_waves": raw}, now)
    result["observedSignals"] = waves({"console_waves": observed}, now)
    result["observationScope"] = "current_lsl_receiver_not_selected_session"
    return finite_json(result)

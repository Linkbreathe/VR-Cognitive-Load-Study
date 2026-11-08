"""Read the licensed Mindtooth neurometric stream on the study computer."""
import json
import math
import os
import threading
import time
from urllib.parse import quote, urlencode

from websockets.sync.client import connect


def workload_ready(values):
    score, threshold = values.get("workload"), values.get("workload_th")
    stamp = values.get("calculatedAt")
    if not all(type(v) in (int, float) and 0 <= v <= 1 for v in (score, threshold)):
        return False
    if type(stamp) not in (int, float):
        return False
    seconds = stamp / 1000 if stamp > 1e11 else stamp
    return 0 <= time.time() - seconds <= 3


class NeurometricMonitor:
    def __init__(self):
        self.lock = threading.Lock()
        self.stop = None
        self.state = "disconnected"
        self.latest = {}

    def configure(self, enabled):
        with self.lock:
            if self.stop:
                self.stop.set()
            self.stop = None
            self.latest = {}
            self.state = "disconnected"
            if enabled:
                keys = ("PARTICIPANT_ID", "ORGANIZATION_EMAIL", "ACTIVATION_TOKEN")
                participant, email, token = (os.environ.get(k, "").strip() for k in keys)
                if not all((participant, email, token)):
                    raise ValueError("Set PARTICIPANT_ID, ORGANIZATION_EMAIL and ACTIVATION_TOKEN on the study computer, then restart the bridge.")
                query = urlencode({"activation": token, "email": email})
                url = "wss://api.prod.mindtooth.com/users/" + quote(participant, safe="") + "/neurometrics/?" + query
                self.state = "connecting"
                self.stop = threading.Event()
                threading.Thread(target=self.read, args=(url, participant, self.stop), daemon=True).start()

    def read(self, url, participant, stop):
        while not stop.is_set():
            try:
                with connect(url, open_timeout=5, close_timeout=1) as socket:
                    with self.lock:
                        if stop.is_set():
                            return
                        self.state = "waiting for neurometrics"
                    while not stop.is_set():
                        try:
                            message = socket.recv(timeout=1)
                        except TimeoutError:
                            continue
                        packet = json.loads(message)
                        if not isinstance(packet, dict) or str(packet.get("user_id")) != participant:
                            continue
                        values = {k: v for k, v in packet.items()
                                  if k in ("workload", "stress", "vigilance", "workload_th", "stress_th")
                                  and type(v) in (float, int) and math.isfinite(v)}
                        if not any(k in values for k in ("workload", "stress", "vigilance")):
                            continue
                        with self.lock:
                            if stop.is_set():
                                return
                            self.latest = dict(values, session=str(packet.get("session_id", "")),
                                               participant=participant, calculatedAt=packet.get("calculated_at"),
                                               received=time.monotonic())
                            self.state = "receiving"
            except Exception:
                with self.lock:
                    if stop.is_set():
                        return
                    self.latest = {}
                    self.state = "connection failed; retrying"
                stop.wait(5)

    def snapshot(self):
        with self.lock:
            state = self.state
            if self.latest and time.monotonic() - self.latest["received"] > 3:
                state = "stale"
            return {"state": state, "values": dict(self.latest) if state == "receiving" else {}}

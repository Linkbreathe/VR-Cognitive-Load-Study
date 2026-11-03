"""Personalized session orchestration; Unity only presents and acknowledges."""
from __future__ import annotations

from collections import deque
import json
import math
from pathlib import Path
import random
import threading
import time

from personalized_model import VERSION, PROTOCOL, policy_config, behavioral_summary, window_behavior, train_personal, load_personal
from personalized_signals import FEATURES, sampled_window, eeg_features, ecg_features, movie_fraction


def write_json(path, data):
    Path(path).write_text(json.dumps(data, indent=2, allow_nan=False), encoding="utf-8")


def append(path, data):
    with Path(path).open("a", encoding="utf-8") as f:
        f.write(json.dumps(data, allow_nan=False) + "\n")


def random_schedule(seed):
    """Pre-generated 5-trial decisions; no physiological or behavioral inputs."""
    rng = random.Random(seed)
    return {str(trial): rng.randrange(2) for trial in range(5, 96, 5)}


class PersonalSession:
    def __init__(self, packet, folder, policy):
        self.sid = packet["sessionId"]; self.participant = packet["participantCode"]
        self.kind = packet["sessionType"]; self.folder = Path(folder); self.policy = policy_config(policy)
        self.config = packet["config"]; self.seed = int(self.config["seed"])
        if packet["configVersion"] != VERSION or self.config.get("protocol") != PROTOCOL:
            raise ValueError("Personalized protocol mismatch")
        if self.config.get("calibrationBlocks") not in (6, 8) or self.config.get("trainingTrials") != 96:
            raise ValueError("Protocol requires six/eight calibration blocks and 96 training trials")
        self.folder.mkdir(parents=True, exist_ok=True)
        # Do not silently resume with stale signal buffers or a different model after a process restart.
        if (self.folder / "personal_protocol.json").exists():
            raise ValueError("Personalized service restart: start a new Unity session; automatic resume is disabled")
        self.plan = random_schedule(self.seed)
        write_json(self.folder / "personal_protocol.json", {"version": VERSION, "participant": self.participant,
            "session": self.sid, "config": self.config, "policy": self.policy, "random_plan": self.plan,
            "counterbalance_group": self.config["counterbalanceGroup"], "label_alignment": self.policy["label_alignment"],
            "flow_revision": "preparation-practice-calibration-no-rest-v1", "resting_baseline_seconds": 0})
        self.signals = {}; self.trials = {}; self.events_seen = set(); self.windows = {}; self.decisions = {}
        self.intervals = []; self.playing = {}
        self.model = None; self.report = None; self.model_status = "awaiting_calibration"
        self.mount_intact = True
        self.fit_thread = None; self.attempts = 0; self.condition_epoch = 0
        self.last_change = float("-inf"); self.last_stage = None; self.predictions = deque(maxlen=200)
        self.packet = packet; self.lock = threading.RLock()

    def ingest(self, config, times, samples, source_id):
        if config["kind"] not in ("EEG", "ECG"):
            return
        origin = config.get("data_origin", "unknown")
        if self.kind == "participant" and (config.get("synthetic") or origin != "live_sensor"):
            raise ValueError("Participant personalized sessions require explicitly live sources")
        with self.lock:
            key = config["kind"]
            identity = (source_id, config.get("uid", ""))
            if key in self.signals and self.signals[key]["identity"] != identity:
                raise ValueError("Source identity changed within a frozen session")
            source = self.signals.setdefault(key, {"identity": identity, "config": dict(config), "rows": deque()})
            source["rows"].extend(zip(times, samples))
            # Preserve the acquisition buffer for late task-boundary packets.
            while source["rows"] and source["rows"][0][0] < times[-1] - 180:
                source["rows"].popleft()

    def raw_window(self, kind, start, end, offset):
        source = self.signals.get(kind)
        if not source or not source["rows"]:
            return None, "source_missing", 0
        rows = list(source["rows"]); rate = source["config"]["sample_rate"]
        x, reason = sampled_window([r[0] - offset for r in rows], [r[1] for r in rows], start, end, rate)
        return x, reason, rate

    def event(self, e):
        key = json.dumps(e, sort_keys=True)
        if key in self.events_seen:
            return
        self.events_seen.add(key)
        if e["type"] == "personal_mount_interrupted":
            self.mount_intact = False; self.model_status = "personal_model_not_validated"
            self.predictions.clear(); self.last_change = e["actual"]; self.condition_epoch += 1
            write_json(self.folder / "mount_integrity.json", {"intact": False, "event": e,
                "action": "restart_required; no automatic offset correction or physiological readmission"})
        if e["type"] in ("adaptive_level_changed", "pause", "resume", "protocol_stage"):
            self.last_change = e["actual"]; self.condition_epoch += 1; self.predictions.clear()
        if e["type"].startswith("adaptive_media_") and e["detail"].startswith("{"):
            value, _ = json.JSONDecoder().raw_decode(e["detail"])
            identity = (value["block"], value["mediaId"], value["onset"])
            status = e["type"].replace("adaptive_media_", "")
            if status == "started":
                self.playing[identity] = e["actual"]
            elif status in ("ended", "interrupted", "failure"):
                if identity in self.playing:
                    self.intervals.append((self.playing.pop(identity), e["actual"]))
            if status in ("failure", "interrupted"):
                self.last_change = e["actual"]; self.condition_epoch += 1; self.predictions.clear()

    def exposure(self, start, end):
        return movie_fraction(start, end, self.intervals + [(a, end) for a in self.playing.values()])

    def extract(self, rows, packet):
        first, last = rows[0], rows[-1]
        start, end = first["plannedOnset"], last["plannedOnset"] + 4.5
        result = {"block": last["block"], "trial": last["phaseTrial"], "stage": last["trainingStage"],
                  "level": last["level"], "start": start, "end": end, "valid": False,
                  "reason": "incomplete_or_mixed_window", "features": [], "origin": "unknown"}
        if (len(rows) != 5 or abs(end - start - 22.5) > .01 or
                len({r["level"] for r in rows}) != 1 or len({r["trainingStage"] for r in rows}) != 1 or
                any(not r["scored"] or r["interrupted"] or r["aborted"] or r["transition"] for r in rows) or
                any(abs(r["actualOnset"] - r["plannedOnset"]) > .1 or abs(r["trialEnd"] - r["plannedOnset"] - 4.5) > .1 for r in rows)):
            return result
        if not self.mount_intact:
            result["reason"] = "headset_or_electrode_movement_requires_new_session"; return result
        if not math.isfinite(packet.get("clockUncertainty", 999)) or packet.get("clockUncertainty", 999) > self.policy["max_clock_uncertainty"]:
            result["reason"] = "clock_uncertainty"; return result
        if not self.policy["eeg_units_confirmed"]:
            result["reason"] = "eeg_units_unconfirmed"; return result
        offset = packet["clockOffset"]
        eeg, er, ef = self.raw_window("EEG", start, end, offset)
        ecg, cr, cf = self.raw_window("ECG", start, end, offset)
        if eeg is None or ecg is None:
            result["reason"] = "EEG:" + er + ";ECG:" + cr; return result
        ev, eq = eeg_features(eeg, ef, self.policy["theta_channels"], self.policy["alpha_channels"])
        cv, cq = ecg_features(ecg, cf)
        result["quality"] = {"eeg": eq, "ecg": cq}
        if not eq["valid"] or not cq["valid"]:
            result["reason"] = "signal_quality"; return result
        origins = {s["config"].get("data_origin", "unknown") for s in self.signals.values()}
        result["origin"] = "live_sensor" if origins == {"live_sensor"} else "non_live_or_unknown"
        values = dict(ev, **cv, movie_fraction=self.exposure(start, end))
        result.update(valid=True, reason="ok", features=[values[f] for f in FEATURES])
        return result

    def process_windows(self, packet):
        rows = sorted(self.trials.values(), key=lambda r: r["plannedOnset"])
        for i, last in enumerate(rows):
            if last["trainingStage"] not in ("calibration", "random", "bioadaptive") or i < 4:
                continue
            window = rows[i - 4:i + 1]
            if last["trainingStage"] == "calibration":
                if len({r["block"] for r in window}) != 1 or last["blockTrial"] < 5:
                    continue
            elif last["phaseTrial"] < 4:
                continue
            key = (last["trainingStage"], last["phaseTrial"])
            if key in self.windows:
                continue
            # Allow transport/acquisition to catch up, then permanently log missing data.
            if packet["now"] < last["trialEnd"] + .75:
                continue
            record = self.extract(window, packet)
            self.windows[key] = record
            append(self.folder / "aligned_windows.jsonl", record)
            if last["trainingStage"] != "calibration":
                prediction = dict(record, score=None, epoch=self.condition_epoch)
                if record["valid"] and self.model is not None and self.mount_intact and record["start"] >= self.last_change:
                    prediction["score"] = float(self.model.predict_proba([record["features"]])[0, 1])
                self.predictions.append(prediction)
                append(self.folder / "personal_predictions.jsonl", prediction)

    def prepare_training_rows(self):
        result = []
        for record in self.windows.values():
            if record["stage"] != "calibration":
                continue
            block = sorted([t for t in self.trials.values() if t["trainingStage"] == "calibration" and t["block"] == record["block"]], key=lambda r: r["phaseTrial"])
            block = [t for t in block if record["trial"] - 4 <= t["phaseTrial"] <= record["trial"]]
            b = window_behavior(block, self.policy)
            if b.get("valid") and (abs(record["start"] - b["start"]) > .01 or abs(record["end"] - b["end"]) > .1):
                b = {"valid": False, "reason": "feature_and_label_interval_mismatch"}
            result.append(dict(record, behavior=b, label_alignment=self.policy["label_alignment"]))
        return result

    def start_fit(self):
        if not self.mount_intact or (self.fit_thread is not None and self.fit_thread.is_alive()):
            return
        self.model_status = "training"; self.attempts += 1
        rows = self.prepare_training_rows(); attempt = self.attempts
        write_json(self.folder / ("training_rows_" + str(attempt) + ".json"), rows)
        def fit():
            try:
                model, report = train_personal(rows, self.participant, self.sid, self.kind, self.seed,
                                               self.policy, self.folder / ("model_attempt_" + str(attempt)))
                # Load the persisted frozen artifact, checking identity and digest before admitting it.
                if report["admitted"]:
                    saved = load_personal(self.folder / ("model_attempt_" + str(attempt)) / "personal_model.joblib",
                                          self.participant, self.sid, report["model_sha256"])
                    model = saved["model"]
                with self.lock:
                    self.model = model if self.mount_intact else None
                    self.report = report
                    self.model_status = report["status"] if self.mount_intact else "personal_model_not_validated"
            except Exception as error:
                with self.lock:
                    self.model_status = "personal_model_not_validated"
                    self.report = {"status": self.model_status, "admitted": False, "reasons": ["training_error:" + str(error)]}
                    write_json(self.folder / ("model_error_" + str(attempt) + ".json"), self.report)
        self.fit_thread = threading.Thread(target=fit, daemon=True); self.fit_thread.start()

    def receive(self, packet, decide=False):
        with self.lock:
            if packet["participantCode"] != self.participant or packet["config"] != self.config:
                raise ValueError("Personal identity/configuration is immutable")
            self.packet = packet
            for e in packet.get("events", []):
                self.event(e)
            for trial in packet.get("trials", []):
                key = (trial["trainingStage"], trial["phaseTrial"])
                if key in self.trials and self.trials[key] != trial:
                    raise ValueError("Completed trial changed")
                self.trials[key] = trial
            self.process_windows(packet)
            if packet["stage"] == "model_wait" and packet["now"] - packet.get("stageStarted", packet["now"]) >= 2:
                requested = 2 if packet.get("supplementUsed") else 1
                if self.attempts < requested:
                    self.start_fit()
            reply = {"modelStatus": self.model_status, "modelAdmitted": bool(self.mount_intact and self.report and self.report.get("admitted")),
                     "modelReportJson": json.dumps(self.report or {"status": self.model_status}),
                     "modelAttempt": self.attempts, "featuresJson": json.dumps(list(self.predictions)[-1:])}
            if decide:
                key = packet["requestId"]
                if not self.mount_intact:
                    reply["decision"] = self.decide(packet)
                    append(self.folder / "personal_decisions.jsonl", {"packet": packet, "decision": reply["decision"]})
                    return reply
                if key not in self.decisions:
                    self.decisions[key] = self.decide(packet)
                    append(self.folder / "personal_decisions.jsonl", {"packet": packet, "decision": self.decisions[key]})
                reply["decision"] = self.decisions[key]
            return reply

    def decide(self, p):
        d = {"sessionId": self.sid, "configVersion": VERSION, "requestId": p["requestId"],
             "policyRevision": self.policy["revision"],
             "block": p["block"], "level": p["level"], "epoch": p["epoch"], "lastTrial": p["lastTrial"],
             "evaluationId": p["evaluationId"], "action": "HOLD", "reason": "insufficient_data", "basis": "none", "physiologicalEligible": False}
        count = p.get("completedTrials", 0)
        if p["stage"] not in ("random", "bioadaptive") or count <= 0 or count % 5 or count >= 96:
            d["reason"] = "not_a_five_trial_decision_boundary"; return d
        if not self.mount_intact:
            d["reason"] = "headset_or_electrode_movement_requires_new_session"; return d
        current = sorted([t for t in self.trials.values() if t["trainingStage"] == p["stage"] and t["phaseTrial"] < count], key=lambda t: t["phaseTrial"])
        fresh = [t for t in current if t["plannedOnset"] >= self.last_change and not t["interrupted"] and not t["aborted"]]
        if len(fresh) < self.policy["minimum_dwell_trials"]:
            d["reason"] = "minimum_new_condition_dwell"; return d
        if p["stage"] == "random":
            target = self.plan[str(count)]
            d.update(action="HOLD" if target == p["level"] else "ON" if target else "OFF",
                     reason="preregistered_seeded_random_plan", basis="random_schedule")
            return d
        behavior = behavioral_summary(current, 20); d["behavior"] = behavior
        if not behavior["valid"]:
            d["reason"] = behavior["reason"]; return d
        stable = behavior["ba"] >= self.policy["stable_ba"] and behavior["omissions"] < self.policy["stable_omissions_below"]
        difficult = (behavior["ba"] <= self.policy["struggling_ba"] or
                     behavior["omissions"] > self.policy["struggling_omissions_above"])
        behavior.update(stable_criteria_met=stable, struggling_criteria_met=difficult,
                        state="struggling" if difficult else "stable" if stable else "undetermined")
        admitted = bool(self.report and self.report.get("admitted"))
        if not admitted:
            d.update(reason="personal_model_not_validated", basis="behavioral_fallback")
            if self.policy["enable_behavioral_fallback"] and self.attempts >= 2:
                d["action"] = "ON" if stable and p["level"] == 0 else "OFF" if difficult and p["level"] == 1 else "HOLD"
            return d
        recent = [r for r in self.predictions if r["stage"] == p["stage"] and r["trial"] < count][-5:]
        good = [r for r in recent if r["valid"] and r["score"] is not None and r["level"] == p["level"] and r["start"] >= self.last_change]
        d.update(valid_predictions=len(good), basis="personalized_rf", physiologicalEligible=False)
        if len(good) < 3 or not good or good[-1]["trial"] < count - 3:
            d["reason"] = "fewer_than_three_recent_pure_condition_predictions"; return d
        if p["level"] == 1 and self.exposure(max(self.last_change, current[0]["plannedOnset"]), current[-1]["trialEnd"]) * (current[-1]["trialEnd"] - max(self.last_change, current[0]["plannedOnset"])) < self.policy["minimum_media_seconds"]:
            d["reason"] = "insufficient_actual_movie_exposure"; return d
        score = sum(r["score"] for r in good) / len(good); threshold = self.policy["thresholds"][str(p["level"])]
        d.update(score=score, physiologicalEligible=True, reason="physiology_behavior_disagree_or_hold")
        if score <= threshold["stable_max"] and stable and p["level"] == 0:
            d.update(action="ON", reason="stable_score_and_behavior")
        elif score >= threshold["struggling_min"] and difficult and p["level"] == 1:
            d.update(action="OFF", reason="struggling_score_and_behavior")
        return d

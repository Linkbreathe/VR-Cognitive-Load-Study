"""Optional Stage 3 boundary. Stage 2 decisions and RF artifacts remain untouched."""
from copy import deepcopy
from layer_ab import guard_mode
from collections import deque
from dataclasses import asdict
import hashlib
import json
import math
import time


REVISION = "evidence-guard-v3-minimal-1"


def identity(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, allow_nan=False).encode()).hexdigest()[:24]


class GuardAdapter:
    def __init__(self, packet, folder, config=None):
        self.mode = guard_mode(packet["config"], packet["stage"])
        self.console_audit = deque(maxlen=128)
        if self.mode not in ("off", "observe", "enforce"):
            raise ValueError("Unknown evidenceGuardMode")
        self.guard = None
        self.failure = None
        self.fault_defer_since = None
        self.folder = folder
        self.seen = set()
        self.results = {}
        self.level = packet["level"]
        self.scene = packet.get("environmentRevision") or "unreported_scene_revision"
        self.now = packet["now"]
        self.sid = packet["sessionId"]
        if self.mode == "off":
            return  # No package import, audit, analysis or optional dependency.
        try:
            from evidence_guard import EvidenceGuard
            self.guard = EvidenceGuard(self.sid, self.mode, config, self._persist)
        except Exception as error:
            self.failure = self.failure or type(error).__name__ + ":" + str(error)

    def _persist(self, row):
        with (self.folder / "evidence_guard_audit.jsonl").open("a", encoding="utf-8") as f:
            f.write(json.dumps(row, allow_nan=False) + "\n")
        if row.get("record") != "evidence":
            self.console_audit.append(deepcopy(row))

    def _evidence(self, key, kind, start, end, level, quality, data, source="unity"):
        from evidence_guard import Evidence
        if key in self.guard.evidence:
            return
        self.guard.ingest(Evidence(key, kind, self.sid, start, end, source, level, self.scene,
                                   bool(quality), data, self.guard.epoch))

    def receive(self, packet, personal, decision=None):
        self.mode = guard_mode(packet["config"], packet["stage"])
        if self.guard is not None: self.guard.mode = self.mode
        if self.mode == "off":
            return decision
        started = time.perf_counter()
        try:
            if self.failure or self.guard is None:
                raise RuntimeError(self.failure or "Guard unavailable")
            self.now = packet["now"]
            reported_scene = packet.get("environmentRevision") or "unreported_scene_revision"
            if reported_scene != self.scene:
                self.guard.reset("stage", self.now)
                self.scene = reported_scene
            for e in sorted(packet.get("events", []), key=lambda e: e["actual"]):
                key = identity(e)
                if key in self.seen:
                    continue
                self.seen.add(key)
                typ, when = e["type"], e["actual"]
                detail = dict(x.split("=", 1) for x in e.get("detail", "").split(";") if "=" in x)
                if typ in ("pause", "resume", "protocol_stage", "personal_mount_interrupted"):
                    self.guard.reset({"protocol_stage": "stage", "personal_mount_interrupted": "mount"}.get(typ, typ), when)
                    self.reset_time = when
                if typ == "personal_execution":
                    from evidence_guard import Enactment
                    target = int(detail["level"])
                    self.guard.on_enactment(Enactment("ack:" + key, detail.get("request", ""), self.sid,
                        when, int(detail.get("previous_level", self.level)), target, detail.get("scene", self.scene)))
                    self.level = target
                if typ == "adaptive_level_changed":
                    self.level = int(detail.get("level", packet["level"]))
                self._evidence("event:" + key, "session" if typ in ("pause", "resume", "personal_mount_interrupted") else "task",
                               when, when, self.level, True, e)
            self.level = packet["level"]
            # Old raw observations remain auditable but cannot cross reset boundaries.
            for g in packet.get("guardGaze", []):
                if g["start"] < getattr(self, "reset_time", float("-inf")):
                    continue
                self._evidence("gaze:" + str(g["id"]), "gaze", g["start"], g["end"], g["level"], g["valid"],
                               {"target": g["target"], "task_visible": g["taskVisible"], "movie_visible": g["movieVisible"]}, "tracked_eyes")
            # PersonalSession already pairs actual starts with ends, but media identity/audio
            # must also be preserved here. Plans never become actual exposures.
            for e in packet.get("events", []):
                if not e["type"].startswith("adaptive_media_") or not e.get("detail", "").startswith("{"):
                    continue
                value, end = json.JSONDecoder().raw_decode(e["detail"])
                key = "media:" + identity([value["block"], value["mediaId"], value["onset"]])
                status = e["type"].replace("adaptive_media_", "")
                starts = getattr(self, "media_starts", {})
                self.media_starts = starts
                if status == "started":
                    if key not in self.guard.evidence:
                        starts.setdefault(key, (e["actual"], value, "audio_playing=true" in e["detail"][end:]))
                if status in ("ended", "interrupted", "failure") and key not in self.guard.evidence:
                    start, metadata, audio = starts.pop(key, (e["actual"], value, False))
                    actual = start < e["actual"]
                    self._evidence(key, "media", start, e["actual"], value["level"], True,
                        {"actual": actual, "status": status, "clip_id": value["mediaId"], "metadata": metadata,
                         "audio_confirmed": actual and (audio or "audio_playing=true" in e["detail"][end:]),
                         "audio_evidence": "software_playback_state_not_acoustic_measurement"})
            for t in packet.get("trials", []):
                key = "trial:" + t["trainingStage"] + ":" + str(t["phaseTrial"])
                quality = t["scored"] and not (t["aborted"] or t["interrupted"] or t["transition"])
                data = dict(t, visible_start=t["actualOnset"], visible_end=min(t.get("offsetTime", 0) or t["actualOnset"] + 4, t["trialEnd"]),
                            media_exposure_seconds=personal.exposure(t["actualOnset"], t["trialEnd"]) * (t["trialEnd"] - t["actualOnset"]))
                self._evidence(key, "trial", t["actualOnset"], t["trialEnd"], t["level"], quality, data)
            for r in personal.predictions:
                key = "signal:" + r["stage"] + ":" + str(r["trial"])
                if key in self.guard.evidence:
                    continue
                score = r.get("score")
                data = dict(r, modality="rf", response_observed=score is not None,
                            physiological_change_confirmed=False)
                # Read-only diagnostic. Never changes model, prediction, base decision or authorization.
                features = r.get("features", [])
                if personal.model is not None and r.get("valid") and len(features) == 4:
                    try:
                        x = list(features); x[-1] = self.guard.config.movie_reference
                        alternate = float(personal.model.predict_proba([x])[0, 1])
                        data["counterfactual"] = {"actual_score": score, "reference_score": alternate,
                            "movie_fraction_reference": x[-1], "causal_prediction": False, "in_distribution": "unknown"}
                    except Exception as error:
                        data["counterfactual"] = {"unavailable": type(error).__name__}
                self._evidence(key, "signal", r["start"], r["end"], r["level"], r["valid"] and score is not None, data, "personal_rf")
            if decision is None:
                return None
            key = decision["requestId"]
            if key in self.results:
                saved, epoch, result = self.results[key]
                if saved != decision or epoch != self.guard.epoch:
                    raise ValueError("Stale or mutated decision retry")
                return deepcopy(result)
            from evidence_guard import Judgment, Proposal
            paths = []
            if decision["action"] == "ON":
                if decision["basis"] == "random_schedule":
                    eid = "schedule:" + key
                    self._evidence(eid, "schedule", packet["now"], packet["now"], packet["level"], True, {"decision": decision}, "stage2_random_plan")
                    jid = "random:" + key
                    self.guard.add_judgment(Judgment(jid, "random_schedule", (eid,), packet["level"], self.scene))
                    paths = [(jid,)]
                else:
                    rows = sorted([t for t in personal.trials.values() if t["trainingStage"] == packet["stage"] and t["phaseTrial"] < packet["completedTrials"]], key=lambda t: t["phaseTrial"])[-20:]
                    ids = tuple("trial:" + t["trainingStage"] + ":" + str(t["phaseTrial"]) for t in rows)
                    jid = "challenge:" + key
                    if len(rows) == 20 and decision["basis"] in ("personalized_rf", "behavioral_fallback"):
                        self.guard.add_judgment(Judgment(jid, "training_challenge", ids, packet["level"], self.scene,
                            requires_view=self.guard.config.require_task_view_for_challenge))
                        path = [jid]
                        if decision["basis"] == "personalized_rf":
                            recent = [r for r in personal.predictions if r["stage"] == packet["stage"] and r["trial"] < packet["completedTrials"]][-5:]
                            good = [r for r in recent if r["valid"] and r.get("score") is not None and r["level"] == packet["level"] and r["start"] >= personal.last_change]
                            sid = "model_support:" + key
                            self.guard.add_judgment(Judgment(sid, "current_stable", tuple("signal:" + r["stage"] + ":" + str(r["trial"]) for r in good), packet["level"], self.scene))
                            path.append(sid)
                        paths = [tuple(path)]
            result = self.guard.review(Proposal(key, self.sid, packet["now"], decision["action"], packet["level"], self.scene,
                                                True, tuple(paths), decision["basis"]))
            output = dict(decision, action=result["effective_action"], guardMode=self.mode, guardRevision=REVISION,
                          guardVerdict=result["verdict"], guardRequestId=key, guardReason=result["reason"])
            # Preserve the original basis and physiological eligibility; guard cannot promote either.
            self._persist({"record": "host_decision", "base": decision, "review": result, "delivered": output,
                           "adapter_latency_ms": (time.perf_counter() - started) * 1000})
            self.results[key] = (deepcopy(decision), self.guard.epoch, deepcopy(output))
            return output
        except Exception as error:
            # Latch faults for this session. No silent recovery or selective loss of evidence.
            self.failure = self.failure or type(error).__name__ + ":" + str(error)
            if decision is not None and decision["action"] == "ON" and self.fault_defer_since is None:
                self.fault_defer_since = packet["now"]
            duration = packet["now"] - self.fault_defer_since if self.fault_defer_since is not None else 0.
            try:
                self._persist({"record": "plugin_fault", "time": packet["now"], "error": self.failure,
                               "mode": self.mode, "restart_required": True, "defer_duration_seconds": duration,
                               "base_decision": decision})
            except OSError:
                pass  # Returned fault still blocks ON in enforce, including disk failures.
            if decision is None:
                return None
            return dict(decision, action="HOLD" if self.mode == "enforce" and decision["action"] == "ON" else decision["action"],
                        guardMode=self.mode, guardRevision=REVISION, guardVerdict="DEFER" if decision["action"] == "ON" else "ALLOW",
                        guardRequestId=decision["requestId"], guardReason="plugin_fault:" + self.failure)

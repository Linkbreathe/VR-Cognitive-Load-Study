"""Personalized session orchestration; Unity only presents and acknowledges."""
from __future__ import annotations
from layer_ab import PROBE, SLIDING, TRAINING, STAGES, compact, enabled as ab_enabled, validate as validate_ab

from collections import deque
import json
import math
import load_reference
import workload_rf
import joblib
import hashlib
import sklearn
from pathlib import Path
import random
import threading
import time

from personalized_model import VERSION, PROTOCOL, BEHAVIOR_REVISION, policy_config, behavioral_summary, window_behavior, train_personal, load_personal
from mindtooth_monitor import workload_ready
from personalized_signals import FEATURES, REVISION as FEATURE_REVISION, sampled_window, eeg_features, movie_fraction


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
    def __init__(self, packet, folder, policy, test_store=None):
        self.sid = packet["sessionId"]; self.participant = packet["participantCode"]
        self.kind = packet["sessionType"]; self.folder = Path(folder); self.policy = policy_config(policy)
        self.config = packet["config"]; self.seed = int(self.config["seed"])
        if packet["configVersion"] != VERSION or self.config.get("protocol") != PROTOCOL:
            raise ValueError("Personalized protocol mismatch")
        self.probe = self.config.get("studyDesign") == PROBE
        if self.config.get("calibrationBlocks") not in (4, 6, 8) or self.config.get("trainingTrials") != (60 if compact(self.config) else 144 if self.probe else 96):
            raise ValueError("Protocol requires four/six/eight calibration blocks and the allocated training length")
        validate_ab(self.config)
        from comparison_environment import provenance
        self.sliding = self.config.get("slidingRevision") == SLIDING
        self.barrier = float("-inf")
        self.hard_resets = []
        self.folder.mkdir(parents=True, exist_ok=True)
        # Do not silently resume with stale signal buffers or a different model after a process restart.
        if (self.folder / "personal_protocol.json").exists():
            raise ValueError("Personalized service restart: start a new Unity session; automatic resume is disabled")
        self.plan = random_schedule(self.seed)
        direct_test = self.config.get("testSessionMode") in ("bioadaptive", "vendor_workload", "comparison", "comparison_candidate", "comparison_rehearsal")
        mode = self.config.get("testSessionMode", "full")
        skips_forest = direct_test or mode in ("practice", "practice_eeg", "control_calibration")
        write_json(self.folder / "personal_protocol.json", {"version": VERSION, "participant": self.participant,
            "session": self.sid, "sessionType": self.kind, "config": self.config, "policy": self.policy, "random_plan": self.plan,
            "gazeInput": provenance(self.config),
            "featureRevision": FEATURE_REVISION, "behaviorRevision": "scored5-or-v3" if compact(self.config) else "scored10-or-v2" if self.probe else BEHAVIOR_REVISION,
            "counterbalance_group": self.config["counterbalanceGroup"], "label_alignment": self.policy["label_alignment"],
            "flow_revision": "independent-stages-v1" if mode in ("practice","practice_eeg","calibration_rehearsal","control_calibration","comparison","comparison_candidate","comparison_rehearsal") else "preparation-practice-rest-math-v1",
            "comparisonReview": ("pre-response-continuous-gaze-v2" if compact(self.config) else "pre-response-movie-gaze-v1") if mode in ("comparison","comparison_candidate","comparison_rehearsal") else None,
            "calibrationWindowPolicy": None if skips_forest or mode=="calibration_rehearsal" else workload_rf.WINDOW_POLICY,
            "rest_seconds": 0 if skips_forest else workload_rf.STAGE_SECONDS, "arithmetic_seconds": 0 if skips_forest else workload_rf.STAGE_SECONDS,
            "calibration_revision": "skipped_direct_test" if direct_test else SLIDING if mode=="control_calibration" else "none" if skips_forest else "task_rehearsal" if mode=="calibration_rehearsal" else workload_rf.REVISION})
        self.signals = {}; self.trials = {}; self.events_seen = set(); self.windows = {}; self.decisions = {}
        self.intervals = []; self.playing = {}
        self.video_intervals = []; self.video_playing = {}
        self.model = None; self.report = None; self.model_status = "awaiting_calibration"
        self.mount_intact = True
        self.fit_thread = None; self.attempts = 0; self.condition_epoch = 0
        self.last_change = float("-inf"); self.last_stage = None; self.predictions = deque(maxlen=200)
        self.packet = packet; self.lock = threading.RLock()
        self.test_store = test_store
        self.candidate_control = False
        self.workload_control = False
        self.reference = None
        self.load_starts = {}
        self.score_thresholds = dict(workload_rf.DEFAULT_THRESHOLDS)
        self.load_rows = {}
        self.load_scoring_rows = {}
        self.load_scan = {}
        self.load_quality = None
        self.vendor_sample = {}
        if self.config.get("testSessionMode") == "vendor_workload" and not self.config.get("operatorManaged"):
            raise ValueError("Vendor workload control requires an operator assignment")
        if self.config.get("operatorManaged"):
            if test_store is None or self.kind not in ("engineering", "editor-test"):
                raise ValueError("Operator-managed model reuse requires an assigned engineering session")
            if self.config.get("testSessionMode") in ("bioadaptive", "comparison", "comparison_candidate"):
                saved, meta = test_store.load_model(self.config["testStorageFolder"], self.participant,
                    self.config["testModelId"], self.config["allowCandidateModel"])
                if meta["sha256"] != self.config["testModelHash"]: raise ValueError("Selected model changed after preparation")
                self.reference = saved.get("reference")
                self.workload_control = (self.config["testSessionMode"] == "comparison_candidate"
                                         and meta["featureRevision"] == workload_rf.REVISION and self.reference is not None)
                if self.workload_control:
                    assignment = test_store.records[self.sid]
                    self.score_thresholds = dict(assignment.get("scoreThresholds", workload_rf.DEFAULT_THRESHOLDS))
                self.model = saved.get("model"); self.report = saved["report"]
                self.candidate_control = self.workload_control or (not meta["admitted"] and self.reference is None)
                self.model_status = "engineering_candidate_loaded" if self.candidate_control else "personal_model_validated"
                if self.reference:
                    self.model_status = "engineering_workload_rf_loaded" if self.workload_control else "load_reference_ready"
                write_json(self.folder / "model-reuse.json", {"source": meta, "revision": "engineering-model-reuse-v1",
                    "scoreThresholds": self.score_thresholds if self.workload_control else None,
                    "candidateControl": self.candidate_control, "waivedAdmissionReasons": meta["reasons"] if self.candidate_control else [],
                    "crossSessionValidityEstablished": False, "freshBuffers": True})

    def ingest(self, config, times, samples, source_id):
        if config["kind"] != "EEG":
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
        rate = source["config"]["sample_rate"]
        rows = [row for row in source["rows"] if start <= row[0] - offset < end]
        if not rows:
            return None, "missing_samples_or_timestamp_gap", rate
        x, reason = sampled_window([r[0] - offset for r in rows], [r[1] for r in rows], start, end, rate)
        return x, reason, rate

    def event(self, e):
        key = json.dumps(e, sort_keys=True)
        if key in self.events_seen:
            return
        self.events_seen.add(key)
        if self.config.get("testSessionMode") == "calibration_rehearsal":
            return
        if e["type"] == "protocol_stage" and e["detail"] in ("load_rest", "load_math"):
            stage = e["detail"]
            self.load_starts[stage] = e["actual"]
            self.load_scoring_rows[stage] = {}
            self.load_rows[stage] = [None] * (workload_rf.STAGE_SECONDS // 10)
            self.load_scan[stage] = dict(nextStart=0, available=0, counts=[0, 0, 0])
            self.load_quality = None
        if e["type"] in ("pause", "personal_mount_interrupted"):
            for stage, onset in list(self.load_starts.items()):
                if onset <= e["actual"] < onset + workload_rf.STAGE_SECONDS:
                    self.load_scan[stage]["interrupted"] = True
                    self.load_quality = None
        if e["type"] == "personal_mount_interrupted":
            self.mount_intact = False; self.model_status = "personal_model_not_validated"
            self.predictions.clear(); self.last_change = e["actual"]; self.condition_epoch += 1
            write_json(self.folder / "mount_integrity.json", {"intact": False, "event": e,
                "action": "restart_required; no automatic offset correction or physiological readmission"})
        if self.sliding and e["type"] == "adaptive_level_changed":
            self.last_change = e["actual"]  # Exposure/dwell boundary only; retain prediction history.
        elif e["type"] in ("adaptive_level_changed", "pause", "resume", "protocol_stage"):
            self.last_change = e["actual"]; self.condition_epoch += 1; self.predictions.clear()
        if e["type"] in ("pause", "resume", "protocol_stage", "personal_mount_interrupted", "adaptive_media_failure", "adaptive_media_interrupted"):
            self.barrier = max(self.barrier, e["actual"])
            if self.sliding: self.hard_resets.append(e["actual"])
        if e["type"].startswith("adaptive_media_") and e["detail"].startswith("{"):
            value, _ = json.JSONDecoder().raw_decode(e["detail"])
            identity = (value["block"], value["mediaId"], value["onset"])
            status = e["type"].replace("adaptive_media_", "")
            if status == "started" and value.get("modality") == "audiovisual" and "first_decoded_frame" in e["detail"]:
                self.video_playing[identity] = e["actual"]
            elif status in ("ended", "failure", "interrupted") and identity in self.video_playing:
                self.video_intervals.append((self.video_playing.pop(identity), e["actual"]))
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
                  "reason": "incomplete_or_mixed_window", "features": [], "origin": "unknown",
                  "featureRevision": FEATURE_REVISION}
        if self.sliding:
            levels = [r["level"] for r in rows]
            intervals = sorted((max(start,a),min(end,b)) for a,b in self.intervals + [(a,end) for a in self.playing.values()] if max(start,a)<min(end,b))
            result.update(windowRevision=SLIDING, levels=levels, source_blocks=sorted({r["block"] for r in rows}),
                          transitions=[r["phaseTrial"] for r in rows if r["transition"]],
                          level_changes=[rows[i]["plannedOnset"] for i in range(1,len(rows)) if levels[i]!=levels[i-1]],
                          playback_intervals=intervals, movie_seconds=self.exposure(start,end)*(end-start),
                          movie_fraction=self.exposure(start,end))
            if any(start < reset < end for reset in self.hard_resets) or [r["phaseTrial"] for r in rows] != list(range(first["phaseTrial"], first["phaseTrial"]+5)):
                return result
        if (len(rows) != 5 or abs(end - start - 22.5) > .01 or
                (not self.sliding and len({r["level"] for r in rows}) != 1) or len({r["trainingStage"] for r in rows}) != 1 or
                any(not r["scored"] or r["interrupted"] or r["aborted"] or (not self.sliding and r["transition"]) for r in rows) or
                any(abs(r["actualOnset"] - r["plannedOnset"]) > .1 or abs(r["trialEnd"] - r["plannedOnset"] - 4.5) > .1 for r in rows)):
            return result
        if not self.mount_intact:
            result["reason"] = "headset_or_electrode_movement_requires_new_session"; return result
        if not math.isfinite(packet.get("clockUncertainty", 999)) or packet.get("clockUncertainty", 999) > self.policy["max_clock_uncertainty"]:
            result["reason"] = "clock_uncertainty"; return result
        if (not self.policy["eeg_units_confirmed"] or
                ("EEG" in self.signals and self.signals["EEG"]["config"].get("units") not in ("uV", "µV"))):
            result["reason"] = "eeg_units_unconfirmed"; return result
        offset = packet["clockOffset"]
        eeg, er, ef = self.raw_window("EEG", start, end, offset)
        if eeg is None:
            result["reason"] = "EEG:" + er; return result
        ev, eq = eeg_features(eeg, ef, self.policy["theta_channels"], self.policy["alpha_channels"],
                              screen_artifacts=not self.workload_control)
        result["quality"] = {"eeg": eq}
        if not eq["valid"]:
            result["reason"] = "signal_quality"; return result
        origins = {s["config"].get("data_origin", "unknown") for s in self.signals.values()}
        result["origin"] = "live_sensor" if origins == {"live_sensor"} else "non_live_or_unknown"
        values = dict(ev, movie_fraction=self.exposure(start, end))
        result.update(valid=True, reason="ok", features=[values[f] for f in FEATURES])
        return result

    def process_windows(self, packet):
        rows = sorted(self.trials.values(), key=lambda r: r["plannedOnset"])
        for i, last in enumerate(rows):
            if last["trainingStage"] not in ("calibration", *TRAINING) or i < 4:
                continue
            window = rows[i - 4:i + 1]
            if last["trainingStage"] == "calibration":
                if not self.sliding and (len({r["block"] for r in window}) != 1 or last["blockTrial"] < 5):
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
                if record["valid"] and self.model is not None and self.mount_intact and record["start"] >= (self.barrier if self.sliding else self.last_change) and (not self.sliding or self.report and self.report.get("windowRevision")==SLIDING):
                    if not self.sliding or ",".join(map(str,record["levels"])) in self.report.get("supported_level_patterns",[]):
                        prediction["score"] = float(self.model.predict_proba([record["features"]])[0, 1])
                    else:
                        prediction["reason"] = "unseen_calibration_level_pattern"
                if self.reference:
                    method = workload_rf if self.report["featureRevision"] == workload_rf.REVISION else load_reference
                    valid = record["valid"] and record["end"] - 10 >= self.barrier
                    if not valid:
                        values, reason = None, record["reason"] if not record["valid"] else "interrupted_window"
                    elif self.workload_control:
                        values, reason = workload_rf.measure(self, packet, record["end"] - 10, screen_artifacts=False)
                    else:
                        values, reason = method.measure(self, packet, record["end"] - 10)
                    prediction["loadScore"] = method.score(self.reference, values) if values is not None else None
                    prediction["loadReason"] = reason
                    prediction["loadRevision"] = method.REVISION
                    if self.workload_control:
                        prediction.update(artifactScreening=False, score=prediction["loadScore"], sourceWindowStart=record["start"],
                                          start=record["end"]-10, scoreSource=workload_rf.REVISION,
                                          scoreMeaning="arithmetic_class_score", valid=prediction["loadScore"] is not None,
                                          reason=reason)
                self.predictions.append(prediction)
                append(self.folder / "personal_predictions.jsonl", prediction)

    def prepare_training_rows(self):
        result = []
        for record in self.windows.values():
            if record["stage"] != "calibration":
                continue
            block = sorted([t for t in self.trials.values() if t["trainingStage"] == "calibration" and (self.sliding or t["block"] == record["block"])], key=lambda r: r["phaseTrial"])
            block = [t for t in block if record["trial"] - 4 <= t["phaseTrial"] <= record["trial"]]
            b = window_behavior(block, self.policy, sliding=self.sliding)
            if b.get("valid") and (abs(record["start"] - b["start"]) > .01 or abs(record["end"] - b["end"]) > .1):
                b = {"valid": False, "reason": "feature_and_label_interval_mismatch"}
            result.append(dict(record, behavior=b, label_alignment=self.policy["label_alignment"]))
        return result

    def update_load_calibration(self, packet):
        for stage, onset in self.load_starts.items():
            rows = self.load_rows[stage]
            scan = self.load_scan[stage]
            while (not scan.get("interrupted") and scan["nextStart"] <= workload_rf.STAGE_SECONDS - 10
                   and packet["now"] >= onset + scan["nextStart"] + 11):
                second = scan["nextStart"]
                scan["nextStart"] += 1
                details = {}
                values, reason = workload_rf.measure(self, packet, onset + second, details)
                fold = second // workload_rf.FOLD_SECONDS
                # Earliest finishing usable interval maximizes the disjoint count in each fold.
                selected = values is not None and second >= scan["available"] and second % workload_rf.FOLD_SECONDS <= workload_rf.FOLD_SECONDS - 10
                if selected:
                    slot = (workload_rf.FOLD_SECONDS // 10) * fold + scan["counts"][fold]
                    rows[slot] = values
                    full, _ = workload_rf.measure(self, packet, onset + second, screen_artifacts=False)
                    self.load_scoring_rows[stage][slot] = full
                    scan["counts"][fold] += 1
                    scan["available"] = second + 10
                result = dict(stage=stage, onset=onset, start=onset + second, startSecond=second,
                              endSecond=second + 10, selected=selected, usableWindows=sum(scan["counts"]),
                              windowPolicy=workload_rf.WINDOW_POLICY, revision=workload_rf.REVISION,
                              trainingEpochs=len(values) if selected else 0, reason=reason, **details)
                append(self.folder / "load_windows.jsonl", result)
                if stage == packet["stage"]:
                    self.load_quality = result
        if packet["stage"] != "model_wait" or self.attempts or packet["now"] < packet.get("stageStarted", packet["now"])+2:
            return
        self.reference, reason, diagnostics = workload_rf.fit(self.load_rows, self.seed, self.load_scoring_rows)
        if not self.mount_intact:
            self.reference, reason = None, "mount_interrupted"
        if not self.reference and (not self.policy["eeg_units_confirmed"] or self.signals.get("EEG",{}).get("config",{}).get("units") not in ("uV","µV")):
            reason += ";eeg_units_unconfirmed"
        self.attempts = 1
        self.model_status = "load_reference_ready" if self.reference else "load_reference_failed"
        self.report = dict(status=self.model_status, admitted=False, reasons=[reason], **diagnostics,
                           windowPolicy=workload_rf.WINDOW_POLICY,
                           featureRevision=workload_rf.REVISION, features=workload_rf.FEATURES, seed=self.seed,
                           participant=self.participant, session=self.sid, sklearn=sklearn.__version__)
        if self.reference:
            artifact = self.folder / "workload_rf.joblib"
            joblib.dump(self.reference, artifact, compress=3)
            self.report["forestSha256"] = hashlib.sha256(artifact.read_bytes()).hexdigest()
        write_json(self.folder / "load_reference.json", self.report)
        if self.test_store:
            self.test_store.publish_reference(self)

    def calibration_quality(self):
        stage = self.packet["stage"]
        if stage not in self.load_scan or self.load_scan[stage].get("interrupted"):
            return None
        row = self.load_quality
        if not row or row["stage"] != stage:
            return dict(warning=False, text="EEG quality: collecting the first 10 seconds.")
        ok = row["reason"] == "ok"
        counts = row.get("flaggedChannels", {})
        flagged = ", ".join(sorted(counts, key=counts.get, reverse=True)[:3])
        label = "EEG quality OK" if ok else "Check EEG signal"
        summary = (f"{label} · {row['startSecond']}–{row['endSecond']} s · "
                   f"{row['usableWindows']} non-overlapping windows accepted (6 required per stage).")
        if "cleanSeconds" in row:
            summary += f" Clean seconds: {row['cleanSeconds']}/10."
        if not ok:
            summary += " " + row["reason"].replace("_", " ") + "."
            if flagged:
                summary += " Check channels: " + flagged + "."
        return dict(warning=not ok, text=summary, window=row)

    def start_fit(self):
        if self.config.get("testSessionMode") in ("bioadaptive", "vendor_workload", "comparison", "comparison_candidate", "comparison_rehearsal"):
            raise ValueError("Reused models are frozen; calibration is required for refitting")
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
                                          self.participant, self.sid, report["model_sha256"], SLIDING if self.sliding else None)
                    model = saved["model"]
                if self.test_store is not None:
                    self.test_store.publish(self, attempt)
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
            if (packet["stage"] in STAGES and not ab_enabled(self.config)) or (ab_enabled(self.config) and packet["stage"] in ("random", "bioadaptive")):
                raise ValueError("Training stage does not match frozen study design")
            self.packet = packet
            for e in packet.get("events", []):
                self.event(e)
            for trial in packet.get("trials", []):
                key = (trial["trainingStage"], trial["phaseTrial"])
                if key in self.trials and self.trials[key] != trial:
                    raise ValueError("Completed trial changed")
                self.trials[key] = trial
            if self.load_starts:
                self.update_load_calibration(packet)
            self.process_windows(packet)
            if not self.load_starts and packet["stage"] == "model_wait" and packet["now"] - packet.get("stageStarted", packet["now"]) >= 2:
                requested = 2 if packet.get("supplementUsed") else 1
                if self.attempts < requested:
                    self.start_fit()
            reply = {"modelStatus": self.model_status, "modelAdmitted": bool(self.mount_intact and self.report and self.report.get("admitted")),
                     "modelTestUsable": bool(self.mount_intact and self.model is not None and (self.candidate_control or self.report and self.report.get("admitted"))),
                     "modelReportJson": json.dumps(self.report or {"status": self.model_status}),
                     "modelAttempt": self.attempts, "featuresJson": json.dumps(list(self.predictions)[-1:])}
            if self.reference:
                reply["modelTestUsable"] = self.mount_intact
            if self.config.get("testSessionMode") == "vendor_workload":
                self.model_status = "vendor_workload_ready" if workload_ready(self.vendor_sample) else "waiting_for_vendor_session"
                reply.update(modelStatus=self.model_status, modelTestUsable=bool(self.mount_intact and workload_ready(self.vendor_sample)))
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
        threshold_revision = 0
        if self.workload_control and self.test_store:
            assignment = self.test_store.records[self.sid]
            self.score_thresholds = dict(assignment["scoreThresholds"])
            threshold_revision = assignment.get("thresholdRevision", 0)
        d = {"sessionId": self.sid, "configVersion": VERSION, "requestId": p["requestId"],
             "policyRevision": self.policy["revision"], "behaviorRevision": BEHAVIOR_REVISION,
             "block": p["block"], "level": p["level"], "epoch": p["epoch"], "lastTrial": p["lastTrial"],
             "evaluationId": p["evaluationId"], "action": "HOLD", "reason": "insufficient_data", "basis": "none", "physiologicalEligible": False}
        if self.workload_control:
            d.update(lowThreshold=self.score_thresholds["on"], highThreshold=self.score_thresholds["off"],
                     thresholdRevision=threshold_revision)
        if self.reference or self.load_starts:
            last = next((r for r in reversed(self.predictions) if r["stage"] == p["stage"] and 0 < p.get("completedTrials",0)-r["trial"] <= 2), {})
            d.update(loadScore=last.get("loadScore"), loadRevision=(self.report or {}).get("featureRevision", workload_rf.REVISION))
        vendor = self.config.get("testSessionMode") == "vendor_workload"
        if vendor:
            d.update(basis="mindtooth_vendor_workload", vendorPolicy="workload-threshold-engineering-v1",
                     vendorMetrics=dict(self.vendor_sample), engineeringModelEligible=False)
        if self.sliding: d["slidingRevision"] = SLIDING
        count = p.get("completedTrials", 0)
        if p["stage"] not in TRAINING or count <= 0 or count % (2 if compact(self.config) else 5) or count >= self.config["trainingTrials"]:
            d["reason"] = "not_a_five_trial_decision_boundary"; return d
        if not self.mount_intact:
            d["reason"] = "headset_or_electrode_movement_requires_new_session"; return d
        current = sorted([t for t in self.trials.values() if t["trainingStage"] == p["stage"] and t["phaseTrial"] < count], key=lambda t: t["phaseTrial"])
        if self.sliding:
            preview = p.get("closedBehavior") or {}
            if (preview.get("phaseTrial") != count-1 or preview.get("trainingStage") != p["stage"] or
                    preview.get("level") != p["level"] or not current or current[-1]["phaseTrial"]!=count-2 or
                    not preview.get("closeTime") or not (preview["plannedOnset"]+4-1e-5 <= preview["closeTime"] <= p["now"] < preview["plannedOnset"]+4.5) or
                    abs(p.get("decisionDeadline",0)-preview["plannedOnset"]-4.5)>.001):
                d["reason"]="missing_closed_response_or_deadline";return d
            current.append(dict(preview,trialEnd=preview["closeTime"]))
            if self.probe:
                from probe_control import base_decision
                return base_decision(self,p,d,current)
            if len(current)>=20 and ([t["phaseTrial"] for t in current[-20:]]!=list(range(count-20,count)) or current[-20]["plannedOnset"]<self.barrier):
                d["reason"]="behavior_gap_or_hard_reset";return d
        fresh = [t for t in current if t["plannedOnset"] >= self.last_change and not t["interrupted"] and not t["aborted"]]
        if not self.sliding and len(fresh) < self.policy["minimum_dwell_trials"]:
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
        if vendor:
            uncertainty = p.get("clockUncertainty", float("inf"))
            if not 0 <= uncertainty <= self.policy["max_clock_uncertainty"]:
                return dict(d, reason="clock_uncertainty")
            if not workload_ready(self.vendor_sample):
                return dict(d, reason="vendor_score_or_threshold_unavailable")
            last_window = next((w for w in reversed(self.windows.values()) if w["stage"] == p["stage"]), None)
            if (not last_window or not last_window["valid"] or last_window.get("origin") != "live_sensor"
                    or last_window["level"] != p["level"] or last_window["start"] < max(self.last_change, self.barrier)
                    or not count - 2 <= last_window["trial"] < count):
                return dict(d, reason="vendor_eeg_window_unqualified")
            start, end = max(self.last_change, current[0]["plannedOnset"]), current[-1]["trialEnd"]
            if p["level"] == 1 and self.exposure(start, end) * (end - start) < self.policy["minimum_media_seconds"]:
                return dict(d, reason="insufficient_actual_movie_exposure")
            value, threshold = self.vendor_sample["workload"], self.vendor_sample["workload_th"]
            d.update(reason="vendor_workload_and_behavior_hold", engineeringModelEligible=True)
            if value < threshold and stable and p["level"] == 0:
                d.update(action="ON", reason="below_vendor_threshold_and_stable")
            elif value > threshold and difficult and p["level"] == 1:
                d.update(action="OFF", reason="above_vendor_threshold_and_struggling")
            return d
        if self.reference or self.load_starts:
            d.update(basis="behavioral_fallback", reason="load_reference_observation")
            if self.policy["enable_behavioral_fallback"]:
                d["action"] = "ON" if stable and p["level"] == 0 else "OFF" if difficult and p["level"] == 1 else "HOLD"
            return d
        admitted = bool(self.report and self.report.get("admitted"))
        if not admitted and not self.candidate_control:
            d.update(reason="personal_model_not_validated", basis="behavioral_fallback")
            if self.policy["enable_behavioral_fallback"] and self.attempts >= 2:
                d["action"] = "ON" if stable and p["level"] == 0 else "OFF" if difficult and p["level"] == 1 else "HOLD"
            return d
        recent = [r for r in self.predictions if r["stage"] == p["stage"] and r["trial"] < count][-5:]
        good = [r for r in recent if r["valid"] and r["score"] is not None and r["level"] == p["level"] and r["start"] >= self.last_change]
        d.update(valid_predictions=len(good), basis="engineering_candidate_rf" if self.candidate_control else "personalized_rf", physiologicalEligible=False,
                 engineeringModelEligible=False)
        if self.sliding:
            # At response close the newest completed five-trial window ends one trial earlier.
            # Never silently substitute an older valid result for the current missing/invalid one.
            uncertainty=p.get("clockUncertainty",999)
            if not math.isfinite(uncertainty) or not 0 <= uncertainty <= self.policy["max_clock_uncertainty"]:
                d["reason"]="sliding_clock_unqualified";return d
            good=[r for r in recent if r["trial"]==count-2 and r.get("windowRevision")==SLIDING and r["valid"] and
                  r["score"] is not None and math.isfinite(r["score"]) and r["start"]>=self.barrier and
                  abs(r["end"]-preview["plannedOnset"])<=.1 and r["end"]<=p["now"] and
                  self.report and self.report.get("windowRevision")==SLIDING and
                  ",".join(map(str,r["levels"])) in self.report.get("supported_level_patterns",[])]
            d.update(valid_predictions=len(good), predictionTrial=good[0]["trial"] if good else -1)
            if not good:
                d["reason"]="sliding_window_unavailable_or_unsupported";return d
        elif len(good) < 3 or not good or good[-1]["trial"] < count - 3:
            d["reason"] = "fewer_than_three_recent_pure_condition_predictions"; return d
        if p["level"] == 1 and self.exposure(max(self.last_change, current[0]["plannedOnset"]), current[-1]["trialEnd"]) * (current[-1]["trialEnd"] - max(self.last_change, current[0]["plannedOnset"])) < self.policy["minimum_media_seconds"]:
            d["reason"] = "insufficient_actual_movie_exposure"; return d
        score = sum(r["score"] for r in good) / len(good); threshold = self.policy["thresholds"][str(p["level"])]
        d.update(score=score, physiologicalEligible=not self.candidate_control, engineeringModelEligible=self.candidate_control,
                 reason="physiology_behavior_disagree_or_hold")
        if score <= threshold["stable_max"] and stable and p["level"] == 0:
            d.update(action="ON", reason="stable_score_and_behavior")
        elif score >= threshold["struggling_min"] and difficult and p["level"] == 1:
            d.update(action="OFF", reason="struggling_score_and_behavior")
        return d

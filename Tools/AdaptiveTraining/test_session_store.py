"""Explicit engineering sessions and trusted local model registry (never participant admission)."""
from __future__ import annotations

import base64
import copy
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import uuid
import threading
import struct
from functools import wraps

import load_reference
import workload_rf
import comparison_environment
import joblib
import sklearn
from personalized_model import VERSION, PROTOCOL, policy_config
from personalized_signals import FEATURES, REVISION as FEATURE_REVISION

REVISION = "engineering-model-reuse-v1"
GAZE_DEBUG_INHERITANCE_REVISION = "engineering-gaze-claim-v1"
MODES = ("practice", "practice_eeg", "calibration_rehearsal", "control_calibration", "comparison", "comparison_candidate", "comparison_rehearsal", "full", "calibration", "bioadaptive", "vendor_workload")
ARCHIVE_FILES = {"session.json", "adaptive-training-config.json", "trials.csv", "practice_trials.csv", "events.jsonl"}

def synchronized(method):
    @wraps(method)
    def call(self, *args, **kwargs):
        with self.lock: return method(self, *args, **kwargs)
    return call


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write(path, value):
    path = Path(path)
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(value, indent=2, allow_nan=False), encoding="utf-8")
    temp.replace(path)


def code(value):
    if not isinstance(value, str) or not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", value) or value.upper() in {"CON", "PRN", "AUX", "NUL", *(f"COM{i}" for i in range(10)), *(f"LPT{i}" for i in range(10))}:
        raise ValueError("Use 1-80 letters, digits, underscores or hyphens for Participant ID")
    return value


def digest(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""): h.update(chunk)
    return h.hexdigest()


def model_threshold_profile(folder, manifest, report):
    profile = report.get("thresholdProfile")
    if profile is None:
        # Older models keep their original report hash; replay can supply a bound sidecar.
        path = folder / "threshold-profile.json"
        if path.is_file():
            sidecar = read(path)
            if sidecar.get("modelHash") == manifest["sha256"]:
                profile = sidecar.get("thresholdProfile")
    return profile


def checked_thresholds(value):
    if not isinstance(value, dict) or set(value) != {"on", "off"} or any(
            type(v) not in (int, float) for v in value.values()) or not 0 <= value["on"] < value["off"] <= 1:
        raise ValueError("Set thresholds between 0 and 1, with ON lower than OFF.")
    return dict(value)


def signal_contract(streams):
    """Publisher UID may change; feature meaning, scaling and montage may not."""
    keys = ("kind", "sample_rate", "channels", "scale_to_units", "differential", "channel_count", "channel_labels", "units")
    return sorted([{k: s.get(k) for k in keys} for s in streams if s["kind"] == "EEG"], key=lambda s: s["kind"])


def configuration_differences(saved, current, path=""):
    if saved == current:
        return []
    if isinstance(saved, dict) and isinstance(current, dict):
        return [difference for key in sorted(set(saved) | set(current))
                for difference in configuration_differences(saved[key] if key in saved else {"missing": True},
                    current[key] if key in current else {"missing": True},
                    (path + "." if path else "") + key)]
    display = lambda value: json.dumps(value, ensure_ascii=True)[:240]
    return [f"{path}: saved={display(saved)}, current={display(current)}"]


class TestSessionStore:
    def __init__(self, state_dir, policy=None):
        self.lock = threading.RLock()
        self.state_dir = Path(state_dir)
        self.settings_path = self.state_dir / "test-session-settings.json"
        self.index_path = self.state_dir / "test-session-index.json"
        self.settings = read(self.settings_path) if self.settings_path.exists() else {"storageFolder": "D:/ExperimentData/PatentDemo", "participantCode": "TEST001"}
        self.records = read(self.index_path) if self.index_path.exists() else {}
        self.policy = policy_config(policy)
        self.streams = []
        self.pending = None
        # Old sessions remain available for archive recovery only, never task resume.
        for record in self.records.values():
            if record["state"] in ("prepared", "claimed", "running"):
                record["state"] = "service_restarted"; record["revoked"] = True
        if self.index_path.exists(): self.save()

    @synchronized
    def save(self):
        self.state_dir.mkdir(parents=True, exist_ok=True)
        write(self.index_path, self.records)

    def storage(self, value):
        path = Path(value).expanduser()
        if not path.is_absolute(): raise ValueError("Storage Folder must be an absolute path on the study computer")
        path = path.resolve()
        path.mkdir(parents=True, exist_ok=True)
        if shutil.disk_usage(path).free < 1024 ** 3: raise ValueError("At least 1 GiB free space is required before a new test")
        probe = path / (".write-check-" + uuid.uuid4().hex)
        with probe.open("xb") as f: f.write(b"ok")
        probe.unlink()
        return path

    def list_models(self, root, participant):
        folder = Path(root).expanduser().resolve() / code(participant) / "models"
        result = []
        for path in sorted(folder.glob("*/manifest.json"), reverse=True):
            try:
                meta = read(path)
                if meta.get("featureRevision") not in (FEATURE_REVISION, load_reference.REVISION, workload_rf.REVISION):
                    continue
                observation = meta["featureRevision"] in (load_reference.REVISION, workload_rf.REVISION)
                available = (path.parent / ("reference.json" if observation else "model.joblib")).is_file()
                report_path = path.parent / "model_report.json"
                training = read(report_path) if not observation and report_path.is_file() else {}
                if meta["featureRevision"] == workload_rf.REVISION:
                    if available:
                        reference_report = read(path.parent/"reference.json")
                        meta["thresholdProfile"] = model_threshold_profile(path.parent, meta, reference_report)
                        meta["meanHeldAuc"] = reference_report.get("meanHeldAuc")
                        meta["foldAuc"] = [f["auc"] for f in reference_report.get("folds", [])]
                        meta["aucGating"] = reference_report.get("aucGating")
                    comparison = "Personal workload RF for engineering A/B"
                elif observation:
                    comparison = "Workload observation only"
                elif not available:
                    comparison = "Missing model artifact"
                elif meta.get("participantCode") != participant or meta.get("signalContract") != signal_contract(self.streams) or meta.get("policy") != self.policy:
                    comparison = "Participant, signal mapping or policy mismatch"
                elif training.get("windowRevision") and training["windowRevision"] != "sliding-five-close-v1":
                    comparison = "Incompatible control window revision"
                elif training and not training.get("supported_level_patterns"):
                    comparison = "No supported response-close patterns"
                else:
                    comparison = "Admitted at source" if meta["admitted"] else "Engineering candidate only"
                result.append(dict(meta, available=available, comparisonStatus=comparison))
            except (OSError, ValueError): continue
        return result

    @synchronized
    def prepare(self, request, vendor_identity=None):
        if any(r["state"] in ("prepared", "claimed", "running") for r in self.records.values()):
            raise ValueError("A test is already prepared or running. Finish or stop it before preparing another.")
        participant = code(request["participantCode"])
        mode = request["sessionMode"]
        if mode == "vendor_workload" and (not vendor_identity or not all(vendor_identity.get(k) for k in ("participant", "session"))):
            raise ValueError("A live vendor participant and session are required")
        if mode not in MODES: raise ValueError("Unknown test session mode")
        environment = request.get("comparisonEnvironment", comparison_environment.VR)
        comparison_environment.validate(dict(comparisonEnvironment=environment, operatorManaged=True,
                                             sessionType="engineering", testSessionMode=mode))
        root = self.storage(request["storageFolder"])
        candidate = request.get("allowCandidate", False)
        if type(candidate) is not bool: raise ValueError("Candidate option must be a boolean")
        candidate = mode == "comparison_candidate" or (candidate and mode == "bioadaptive")
        template = read(Path(__file__).resolve().parents[2] / "Assets/TaskPlatform/Config/adaptive-training-config.json")
        sid = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S") + "_" + uuid.uuid4().hex[:12]
        model_id = request.get("modelId", "") if mode in ("bioadaptive", "comparison", "comparison_candidate") else ""
        manifest = None
        cutoffs = recommendation = None
        if mode in ("bioadaptive", "comparison", "comparison_candidate"):
            saved, manifest = self.load_model(root, participant, model_id, candidate)
            workload_comparison = mode == "comparison_candidate" and manifest["featureRevision"] == workload_rf.REVISION
            if workload_comparison:
                profile = model_threshold_profile(root / participant / "models" / model_id, manifest, saved["report"])
                recommendation = profile["recommended"] if profile else None
                cutoffs = checked_thresholds(request.get("scoreThresholds", recommendation or workload_rf.DEFAULT_THRESHOLDS))
            if mode in ("comparison", "comparison_candidate") and not workload_comparison and (
                    saved["report"].get("windowRevision") != "sliding-five-close-v1"
                    or not saved["report"].get("supported_level_patterns") or saved.get("model") is None):
                raise ValueError("Qualified comparison requires a compatible admitted response-close control RF. For a personal workload RF choose A/B with a saved personal model.")
        if "scoreThresholds" in request and cutoffs is None:
            raise ValueError("Editable thresholds require a saved workload model in Adaptive Comparison.")
        blocks = request.get("calibrationBlocks", 4)
        if blocks not in (4, 6, 8): raise ValueError("Calibration requires four, six or eight blocks")
        config = dict(template, enabled=True, mode="ACTIVE", sessionType="engineering", participantCode=participant,
                      operatorManaged=True, testSessionMode=mode, testSessionId=sid, testRevision=REVISION,
                      comparisonEnvironment=environment,
                      testModelId=model_id, allowCandidateModel=candidate, calibrationBlocks=blocks,
                      testStorageFolder=str(root), testModelHash=manifest["sha256"] if manifest else "",
                      seed=int(uuid.uuid4().hex[:7], 16))
        if mode in ("comparison", "comparison_candidate", "comparison_rehearsal", "control_calibration"):
            from layer_ab import configure_probe
            order = request.get("order", "auto")
            if order == "auto":
                assigned = {r["participantCode"]: r["config"]["counterbalanceGroup"]//2 for r in self.records.values()
                            if r["config"].get("testSessionMode") == mode}
                next_group = int(sum(v == 0 for v in assigned.values()) > sum(v == 1 for v in assigned.values()))
                order = ("AB", "BA")[assigned.get(participant, next_group)]
            if order not in ("AB", "BA"):
                raise ValueError("Choose automatic allocation, A/B or B/A.")
            links = read(Path(__file__).resolve().parents[2] / "Tools/LayerAB/questionnaire-links.json")
            configure_probe(config, 0 if order=="AB" else 2, links, short=mode in ("comparison", "comparison_candidate", "comparison_rehearsal"))
        config.setdefault("evidenceGuardMode", "off")
        from layer_ab import INDEPENDENT_STAGES, INDEPENDENT_REVISION, validate as validate_comparison
        stage = request.get("comparisonStage", "")
        if stage and (stage not in INDEPENDENT_STAGES or mode not in ("comparison", "comparison_candidate", "comparison_rehearsal")):
            raise ValueError("Choose a valid independent comparison stage")
        config.update(comparisonStage=stage, comparisonRevision=INDEPENDENT_REVISION if stage else "")
        if stage:
            config.update(trainingTrials=34, counterbalanceGroup=2 if stage=="layer_b" else 0)
        validate_comparison(config)
        # Match Unity JsonUtility's serialized float32 values exactly at the handshake.
        f32 = lambda x: struct.unpack("f", struct.pack("f", x))[0]
        for field in ("mediaVolume", "onsetJitterSeconds", "videoAngle", "videoWidth", "videoHeight", "practiceOnsetJitterSeconds"):
            config[field] = f32(config[field])
        for field in ("mediaOnsets", "practiceMediaOnsets"):
            config[field] = [f32(x) for x in config[field]]
        # Transport URL is supplied by the Unity bootstrap, not an arbitrary web form.
        folder = root / participant / "sessions" / sid
        folder.mkdir(parents=True, exist_ok=False)
        record = {"sessionId": sid, "participantCode": participant, "folder": str(folder), "storageFolder": str(root),
                  "state": "prepared", "clientId": "", "config": config, "archive": {"status": "waiting_for_unity"},
                  "sourceModel": manifest, "signalContract": signal_contract(self.streams), "revision": REVISION}
        if cutoffs is not None:
            record.update(scoreThresholds=cutoffs, initialScoreThresholds=dict(cutoffs), recommendedThresholds=recommendation,
                          thresholdRevision=0, thresholdHistory=[])
        if mode == "vendor_workload":
            record["vendorIdentity"] = {k: vendor_identity[k] for k in ("participant", "session")}
        write(folder / "test-session.json", record)
        self.records[sid] = record; self.pending = sid
        self.settings = {"storageFolder": str(root), "participantCode": participant}
        self.state_dir.mkdir(parents=True, exist_ok=True)
        write(self.settings_path, self.settings); self.save()
        return record

    @synchronized
    def update_thresholds(self, request, context=None):
        r = self.records[request["sessionId"]]
        if r["state"] not in ("prepared", "claimed", "running") or r.get("revoked") or r.get("stopRequested"):
            raise ValueError("Thresholds can only be edited for an active assignment.")
        if not r.get("scoreThresholds") or (r.get("sourceModel") or {}).get("featureRevision") != workload_rf.REVISION:
            raise ValueError("Editable thresholds require a saved workload model in Adaptive Comparison.")
        revision = r.get("thresholdRevision", 0)
        if type(request.get("expectedRevision")) is not int or request["expectedRevision"] != revision:
            raise ValueError("Thresholds changed in another window. Review the current values and apply again.")
        values = checked_thresholds(request["scoreThresholds"])
        if values == r["scoreThresholds"]: return copy.deepcopy(r)
        updated = copy.deepcopy(r)
        updated.setdefault("initialScoreThresholds", dict(r["scoreThresholds"]))
        change = dict(revision=revision+1, changedUtc=datetime.now(timezone.utc).isoformat(),
                      previous=dict(r["scoreThresholds"]), current=values, source="researcher_console",
                      effective="next_new_decision", context=context or {})
        updated.setdefault("thresholdHistory", []).append(change)
        updated.update(scoreThresholds=values, thresholdRevision=revision+1)
        write(Path(r["folder"])/"test-session.json", updated)
        self.records[r["sessionId"]] = updated
        self.save()
        return copy.deepcopy(updated)

    @synchronized
    def claim(self, request):
        client = code(request["clientId"])
        sid = self.pending
        if not sid: return {"ready": False}
        r = self.records[sid]
        if r["state"] not in ("prepared", "claimed") or r["clientId"] not in ("", client): return {"ready": False}
        from urllib.parse import urlsplit
        url = request["controllerUrl"].rstrip("/")
        parsed = urlsplit(url)
        if parsed.scheme != "http" or not parsed.hostname or parsed.query or parsed.fragment: raise ValueError("Invalid controller URL")
        if r["clientId"] and r["config"]["controllerUrl"] != url: raise ValueError("Claim transport changed")
        if not r["clientId"]:
            preferences = request.get("gazeDebug")
            if preferences is not None:
                if (not isinstance(preferences, dict) or preferences.get("revision") != GAZE_DEBUG_INHERITANCE_REVISION
                        or type(preferences.get("highlight")) is not bool or type(preferences.get("ray")) is not bool):
                    raise ValueError("Invalid engineering gaze debug preferences")
                allowed = r["config"].get("enabled") is True and r["config"].get("sessionType") == "engineering"
                r["config"]["engineeringGazeHighlight"] = allowed and preferences["highlight"]
                r["config"]["engineeringGazeRay"] = allowed and preferences["ray"]
                r["gazeDebugInheritance"] = dict(revision=GAZE_DEBUG_INHERITANCE_REVISION,
                    source="local_engineering_preferences", requested=copy.deepcopy(preferences),
                    highlight=r["config"]["engineeringGazeHighlight"], ray=r["config"]["engineeringGazeRay"])
            # Never authorize participant overlays, including a malformed legacy assignment.
            if r["config"].get("sessionType") != "engineering" or not r["config"].get("enabled"):
                r["config"]["engineeringGazeHighlight"] = False
                r["config"]["engineeringGazeRay"] = False
        r["clientId"] = client; r["state"] = "claimed"; r["config"]["controllerUrl"] = url
        write(Path(r["folder"]) / "test-session.json", r); self.save()
        return {"ready": True, "sessionId": sid, "config": copy.deepcopy(r["config"]),
                "gazeDebugInheritanceRevision": r.get("gazeDebugInheritance", {}).get("revision", "")}

    @synchronized
    def verify_packet(self, packet):
        sid = packet["sessionId"]; r = self.records.get(sid)
        if r and r.get("revoked"): raise ValueError("Operator ended this test; prepare a new session")
        if not r or r["state"] not in ("claimed", "running", "complete", "aborted"): raise ValueError("Test was not claimed, or service restarted; create a new session")
        if packet["sessionType"] not in ("engineering", "editor-test") or packet["participantCode"] != r["participantCode"] or packet["config"] != r["config"]:
            raise ValueError("Test identity/configuration differs from the operator assignment")
        if packet.get("testClientId") != r["clientId"]: raise ValueError("Test device identity mismatch")
        if signal_contract(self.streams) != r["signalContract"]: raise ValueError("Signal mapping changed after preparation")
        if r["state"] == "claimed": r["state"] = "running"; self.save()
        return Path(r["folder"]) / "service"

    @synchronized
    def complete(self, sid, state):
        if sid in self.records:
            self.records[sid]["state"] = state
            if self.pending == sid: self.pending = None
            self.save()

    @synchronized
    def publish(self, personal, attempt):
        r = self.records[personal.sid]
        folder = personal.folder / ("model_attempt_" + str(attempt))
        report = read(folder / "model_report.json")
        if report.get("featureRevision") != FEATURE_REVISION:
            raise ValueError("Model preprocessing revision is incompatible; retrain")
        name = "personal_model.joblib" if report["admitted"] else "unvalidated_candidate.joblib"
        r["lastTraining"] = {"attempt": attempt, "status": report["status"], "reasons": report["reasons"], "hasModel": (folder / name).exists()}
        if not (folder / name).exists(): self.save(); return
        model_id = personal.sid + "-" + str(attempt)
        destination = Path(r["storageFolder"]) / personal.participant / "models" / model_id
        destination.mkdir(parents=True, exist_ok=False)
        shutil.copy2(folder / name, destination / "model.joblib")
        shutil.copy2(folder / "model_report.json", destination / "model_report.json")
        meta = {"modelId": model_id, "participantCode": personal.participant, "sourceSession": personal.sid,
                "createdUtc": datetime.now(timezone.utc).isoformat(), "admitted": report["admitted"], "reasons": report["reasons"],
                "sha256": digest(destination / "model.joblib"), "features": FEATURES, "version": VERSION,
                "featureRevision": FEATURE_REVISION,
                "policy": personal.policy, "signalContract": r["signalContract"], "sklearn": sklearn.__version__,
                "trainingRows": str(personal.folder / ("training_rows_" + str(attempt) + ".json")), "revision": REVISION}
        write(destination / "manifest.json", meta)
        r["lastTraining"]["modelId"] = model_id; self.save()

    @synchronized
    def publish_reference(self, personal):
        r = self.records[personal.sid]
        report = personal.report
        r["lastTraining"] = dict(status=report["status"], reasons=report["reasons"], hasModel=bool(personal.reference),
                                 meanHeldAuc=report.get("meanHeldAuc"),
                                 foldAuc=[f["auc"] for f in report.get("folds", [])],
                                 aucGating=report.get("aucGating"), aucPolicyRevision=report.get("aucPolicyRevision"))
        if personal.reference:
            model_id = personal.sid + "-load"
            folder = Path(r["storageFolder"]) / personal.participant / "models" / model_id
            folder.mkdir(parents=True, exist_ok=False)
            shutil.copy2(personal.folder / "workload_rf.joblib", folder / "workload_rf.joblib")
            write(folder / "reference.json", report)
            write(folder / "manifest.json", dict(modelId=model_id, participantCode=personal.participant,
                  sourceSession=personal.sid, createdUtc=datetime.now(timezone.utc).isoformat(), admitted=False,
                  reasons=["engineering_load_observation"], sha256=digest(folder/"reference.json"),
                  features=report["features"], featureRevision=report["featureRevision"],
                  workloadWindowContract=workload_rf.window_contract(),
                  bandProfile=report.get("bandProfile"),
                  aucPolicyRevision=report.get("aucPolicyRevision"), meanHeldAuc=report.get("meanHeldAuc"),
                  signalContract=r["signalContract"], policy=personal.policy))
            r["lastTraining"]["modelId"] = model_id
        self.save()

    def load_model(self, root, participant, model_id, allow_candidate):
        if not model_id:
            raise ValueError("Select a saved model for this participant.")
        code(model_id); code(participant)
        folder = Path(root).resolve() / participant / "models" / model_id
        if not (folder / "manifest.json").is_file():
            raise FileNotFoundError("The selected model is unavailable for this participant and storage folder.")
        meta = read(folder / "manifest.json")
        if meta["participantCode"] != participant or meta["modelId"] != model_id: raise ValueError("Model participant/identity mismatch")
        if meta.get("featureRevision") == "rest-math-iaf-rf-v2":
            raise ValueError("This is a historical 10-second workload model. Run a fresh 5-second calibration; do not relabel old models or reuse their thresholds.")
        if meta.get("featureRevision") in (load_reference.REVISION, workload_rf.REVISION):
            if meta["signalContract"] != signal_contract(self.streams) or not self.streams or meta["policy"] != self.policy:
                raise ValueError("Load reference signal mapping or policy changed")
            if digest(folder/"reference.json") != meta["sha256"]:
                raise ValueError("Load reference hash mismatch")
            report = read(folder/"reference.json")
            if report["participant"] != participant or report["session"] != meta["sourceSession"]:
                raise ValueError("Load reference identity mismatch")
            if report.get("featureRevision") != meta["featureRevision"]:
                raise ValueError("Load model revision mismatch")
            if meta["featureRevision"] == workload_rf.REVISION:
                contract = workload_rf.window_contract()
                if meta.get("workloadWindowContract") != contract or any(report.get(k) != v for k, v in contract.items()):
                    raise ValueError("Workload window contract mismatch; run a fresh calibration")
                artifact = folder / "workload_rf.joblib"
                if report["sklearn"] != sklearn.__version__ or digest(artifact) != report["forestSha256"]:
                    raise ValueError("Workload forest software version or hash mismatch")
                return dict(reference=joblib.load(artifact), report=report), meta
            return dict(reference=report["reference"], report=report), meta
        if meta.get("featureRevision") != FEATURE_REVISION:
            raise ValueError("Model preprocessing revision is incompatible; retrain")
        saved_config = {k: meta.get(k) for k in ("version", "features", "sklearn", "policy")}
        current_config = {"version": VERSION, "features": FEATURES, "sklearn": sklearn.__version__, "policy": self.policy}
        if saved_config != current_config:
            differences = configuration_differences(saved_config, current_config)
            raise ValueError("Model configuration is incompatible: " + "; ".join(differences) +
                             ". Restore the matching saved policy/software configuration, or retrain for an intentional change. Do not edit model manifests.")
        if not meta["signalContract"] or meta["signalContract"] != signal_contract(self.streams):
            differences = configuration_differences(
                {s["kind"]: s for s in (meta["signalContract"] or [])},
                {s["kind"]: s for s in signal_contract(self.streams)}, "signalContract")
            raise ValueError("Model signal mapping is incompatible: " + "; ".join(differences) +
                             ". Review and save the source channels, labels, analysis units and scaling before reconnecting.")
        if not meta["admitted"] and not allow_candidate: raise ValueError("Select Allow unvalidated candidate model to use this engineering model")
        if digest(folder / "model.joblib") != meta["sha256"]: raise ValueError("Model file hash mismatch")
        # Only a local registry artifact is deserialized. No HTTP file upload or arbitrary model path.
        saved = joblib.load(folder / "model.joblib")
        if saved["report"].get("featureRevision") != FEATURE_REVISION:
            raise ValueError("Model contents use an incompatible preprocessing revision; retrain")
        if saved["participant"] != participant or saved["session"] != meta["sourceSession"] or saved["features"] != FEATURES or saved["report"]["policy"] != self.policy or saved["report"]["admitted"] != meta["admitted"]:
            raise ValueError("Model contents do not match registry metadata")
        model = saved["model"]
        if model.n_features_in_ != len(FEATURES) or list(model.classes_) != [0, 1]: raise ValueError("Incompatible classifier shape/classes")
        return saved, meta

    @synchronized
    def archive(self, request):
        r = self.records.get(request["sessionId"])
        if not r or not r["clientId"] or request["clientId"] != r["clientId"]: raise ValueError("Archive identity mismatch")
        folder = Path(r["folder"]) / "unity"; folder.mkdir(exist_ok=True)
        if request.get("manifest"):
            entries = request["manifest"]
            if {x["name"] for x in entries} != ARCHIVE_FILES or len(entries) != len(ARCHIVE_FILES): raise ValueError("Incomplete archive manifest")
            for entry in entries:
                path = folder / entry["name"]
                if not path.exists() or path.stat().st_size != entry["size"] or digest(path) != entry["sha256"]: raise ValueError("Archive incomplete: " + entry["name"])
            metadata = read(folder / "session.json")
            if metadata.get("sessionId") != r["sessionId"] or metadata.get("participantCode") != r["participantCode"] or metadata.get("sessionType") not in ("engineering", "editor-test"):
                raise ValueError("Archived Unity identity mismatch")
            if read(folder / "adaptive-training-config.json") != r["config"]: raise ValueError("Archived Unity configuration mismatch")
            status = metadata["status"]
            if status == "in_progress": raise ValueError("Unity session has not closed")
            r["archive"] = {"status": "complete_core_records", "files": entries, "unityStatus": status}
            write(folder / "archive-manifest.json", r["archive"]); self.save()
            self.complete(request["sessionId"], "complete" if status == "complete" else "aborted")
            return {"ok": True}
        name = request["name"]
        if name not in ARCHIVE_FILES: raise ValueError("Unsupported archive file")
        data = base64.b64decode(request["data"], validate=True)
        if len(data) > 65536: raise ValueError("Archive chunk too large")
        offset = request["offset"]
        if type(offset) is not int or offset < 0: raise ValueError("Invalid archive offset")
        path = folder / name
        # Metadata is an atomic replace; append-only trial/event files support idempotent retries.
        if name.endswith(".json"):
            if offset != 0: raise ValueError("Metadata must be one complete chunk")
            json.loads(data)
            temp = path.with_suffix(".tmp"); temp.write_bytes(data); temp.replace(path)
        else:
            size = path.stat().st_size if path.exists() else 0
            if offset > size: return {"ok": False, "offset": size}
            if offset < size:
                overlap = min(len(data), size - offset)
                with path.open("rb") as f: f.seek(offset); prior = f.read(overlap)
                if prior != data[:overlap]: raise ValueError("Archive retry differs from saved bytes")
                if len(data) > overlap:
                    with path.open("ab") as f: f.write(data[overlap:])
            else:
                with path.open("ab") as f: f.write(data)
        r["archive"] = {"status": "syncing_core_records"}; self.save()
        return {"ok": True, "offset": offset + len(data)}

    @synchronized
    def status(self):
        return copy.deepcopy({"revision": REVISION, "thresholdEditing": True, "settings": self.settings, "sessions": list(self.records.values())[-20:]})

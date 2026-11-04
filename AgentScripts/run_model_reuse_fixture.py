"""Isolated transport/UI fixture. No physical sensors or participant evidence."""
from pathlib import Path
import json
import sys
import threading
import time
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools/AdaptiveTraining"))
import joblib
import numpy as np
from personalized_model import forest, VERSION
from personalized_signals import FEATURES, REVISION as FEATURE_REVISION
from service import StudyService, StudyHTTPServer, make_handler
from test_session_store import write

base = ROOT / "Evidence/ModelReuse/integration-v3"
service = StudyService(base / "service")
store = service.test_store
store.streams = [{"kind": "EEG", "sample_rate": 250, "channels": [0], "scale_to_units": 1, "synthetic": True, "data_origin": "synthetic_fixture"},
                 {"kind": "ECG", "sample_rate": 250, "channels": [1], "scale_to_units": 1, "synthetic": True, "data_origin": "synthetic_fixture"}]
store.settings = {"storageFolder": str(base / "data"), "participantCode": "TEST_FIXTURE"}
store.state_dir.mkdir(parents=True, exist_ok=True)
write(store.settings_path, store.settings)
if not store.list_models(base / "data", "TEST_FIXTURE"):
    record = store.prepare(dict(store.settings, sessionMode="calibration"))
    folder = Path(record["folder"]) / "service"
    attempt = folder / "model_attempt_1"; attempt.mkdir(parents=True)
    x = np.array([[0, 0, 60, 0]] * 12 + [[2, 2, 90, .5]] * 12)
    model = forest(store.policy, 4); model.fit(x, [0] * 12 + [1] * 12)
    report = {"participant": "TEST_FIXTURE", "session": record["sessionId"], "session_type": "synthetic",
        "version": VERSION, "policy": store.policy, "admitted": False, "status": "personal_model_not_validated",
        "reasons": ["synthetic_ui_fixture_no_admission_validation"], "features": FEATURES, "featureRevision": FEATURE_REVISION}
    joblib.dump({"model": model, "report": report, "participant": "TEST_FIXTURE", "session": record["sessionId"], "features": FEATURES}, attempt / "unvalidated_candidate.joblib")
    write(attempt / "model_report.json", report)
    write(folder / "training_rows_1.json", {"synthetic": True, "purpose": "UI/transport fixture only", "features": x.tolist()})
    store.publish(SimpleNamespace(sid=record["sessionId"], participant="TEST_FIXTURE", folder=folder, policy=store.policy), 1)
    store.complete(record["sessionId"], "aborted")

def heartbeat():
    while True:
        with service.lock:
            for role in ("EEG", "ECG"):
                service.sensors[role] = {"kind": role, "name": "SYNTHETIC TRANSPORT FIXTURE", "source_id": role,
                    "connected": True, "state": "receiving", "last_sample": time.perf_counter(), "synthetic": True,
                    "data_origin": "synthetic_fixture", "sample_rate": 250}
        time.sleep(.5)

threading.Thread(target=heartbeat, daemon=True).start()
print("Engineering fixture at http://127.0.0.1:8876/tests; synthetic connection heartbeat, no physiological qualification", flush=True)
StudyHTTPServer(("127.0.0.1", 8876), make_handler(service)).serve_forever()

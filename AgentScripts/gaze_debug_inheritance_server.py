"""Isolated HTTP integration fixture; no acquisition worker or production state."""
import json
from pathlib import Path
import sys

project = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(project / "Tools/AdaptiveTraining"))
from service import StudyService, StudyHTTPServer, make_handler

root = project / "Evidence/GazeDebugInheritance-20261110/integration"
root.mkdir(parents=True, exist_ok=True)
service = StudyService(root / "service", test_state_dir=root / "state")
server = StudyHTTPServer(("127.0.0.1", 8766), make_handler(service))
record = service.test_store.prepare(dict(participantCode="ENGINEERING_GAZE_INHERITANCE", storageFolder=str(root / "data"), sessionMode="practice"))
(root / "assignment.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
print("ISOLATED ENGINEERING SERVER READY " + record["sessionId"], flush=True)
server.serve_forever()

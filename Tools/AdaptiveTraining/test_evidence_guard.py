"""Stage 3 integration against the unmodified Stage 2 controller and real HTTP handler."""
import builtins
import importlib.util
from copy import deepcopy
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch
from urllib.request import Request, urlopen
from http.server import ThreadingHTTPServer

from evidence_guard_adapter import GuardAdapter
from service import StudyService, make_handler
from test_personalized_control import control_rows, decision_fixture


def gaze(rows):
    return [{"id": i * 40 + k, "start": t["actualOnset"] + k * .1,
             "end": t["actualOnset"] + (k + 1) * .1, "level": t["level"],
             "valid": True, "target": "task", "taskVisible": True, "movieVisible": False}
            for i, t in enumerate(rows) for k in range(40)]


def complete_predictions(personal):
    # Existing controller-only fixtures omit fields carried by real extraction records.
    for r in personal.predictions:
        r.update(end=r["start"] + 22.5, features=[], origin="synthetic")


@unittest.skipUnless(importlib.util.find_spec("evidence_guard"), "Optional evidence-guard package is not installed")
class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.folder = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def fixture(self, mode="enforce", eyes=True, admitted=False):
        personal, p = decision_fixture(self.folder, control_rows(), admitted=admitted)
        complete_predictions(personal)
        p["config"]["evidenceGuardMode"] = mode
        if eyes:
            p["guardGaze"] = gaze(p["trials"])
        d = personal.decide(p)
        return personal, p, d, GuardAdapter(p, self.folder)

    def test_all_modes_preserve_valid_challenge_and_base_fields(self):
        for mode in ("off", "observe", "enforce"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as folder:
                s, p = decision_fixture(Path(folder), control_rows(), admitted=False)
                complete_predictions(s)
                p["config"]["evidenceGuardMode"] = mode; p["guardGaze"] = gaze(p["trials"])
                d = s.decide(p); a = GuardAdapter(p, Path(folder)); out = a.receive(p, s, d)
                self.assertEqual(d["action"], "ON"); self.assertEqual(out["action"], "ON", a.failure)
                for k, v in d.items(): self.assertEqual(out[k], v)
                self.assertEqual(out["basis"], "behavioral_fallback")
                self.assertFalse(out["physiologicalEligible"])
                if mode == "off": self.assertFalse((Path(folder) / "evidence_guard_audit.jsonl").exists())

    def test_missing_eyes_defer_enforce_but_observe_keeps_base(self):
        for mode, expected in (("observe", "ON"), ("enforce", "HOLD")):
            with tempfile.TemporaryDirectory() as folder:
                s, p = decision_fixture(Path(folder), control_rows(), admitted=False)
                complete_predictions(s)
                p["config"]["evidenceGuardMode"] = mode
                d = s.decide(p); a = GuardAdapter(p, Path(folder)); out = a.receive(p, s, d)
                self.assertIsNone(a.failure); self.assertEqual(out["action"], expected)
                self.assertEqual(out["guardVerdict"], "DEFER")

    def test_model_path_keeps_rf_and_model_artifacts_unchanged(self):
        s, p, d, a = self.fixture(admitted=True)
        before = deepcopy(list(s.predictions)); out = a.receive(p, s, d)
        self.assertEqual(out["action"], "ON", a.failure)
        self.assertEqual(list(s.predictions), before)
        self.assertEqual(s.decide(p), d)

    def test_fault_latches_and_reduction_survives(self):
        s, p, d, a = self.fixture()
        with patch.object(a.guard, "review", side_effect=RuntimeError("injected")):
            self.assertEqual(a.receive(p, s, d)["action"], "HOLD")
        self.assertIn("injected", a.failure)
        self.assertEqual(a.receive(p, s, dict(d, action="OFF"))["action"], "OFF")
        self.assertEqual(a.receive(p, s, dict(d, action="REST"))["action"], "REST")

    def test_missing_package_off_and_enforce(self):
        s, p, d, _ = self.fixture()
        original = builtins.__import__
        def missing(name, *args, **kw):
            if name.startswith("evidence_guard"):
                raise ImportError("package removed")
            return original(name, *args, **kw)
        with patch("builtins.__import__", side_effect=missing):
            a = GuardAdapter(p, self.folder)
            self.assertEqual(a.receive(p, s, d)["action"], "HOLD")
            p["config"]["evidenceGuardMode"] = "off"
            a = GuardAdapter(p, self.folder)
            self.assertEqual(a.receive(p, s, d), d)

    def test_disk_failure_cannot_allow_upgrade(self):
        s, p, d, a = self.fixture()
        with patch.object(a.guard, "sink", side_effect=OSError("disk full")), patch.object(a, "_persist", side_effect=OSError("disk full")):
            self.assertEqual(a.receive(p, s, d)["action"], "HOLD")
            self.assertEqual(a.receive(p, s, dict(d, action="OFF"))["action"], "OFF")

    def test_actual_failed_media_and_plans(self):
        s, p, d, a = self.fixture()
        media = {"block": 1, "mediaId": "clip", "onset": 10, "level": 1}
        p["events"] = [{"type": "adaptive_media_planned", "actual": 100, "detail": json.dumps(media)},
                       {"type": "adaptive_media_failure", "actual": 100, "detail": json.dumps(media) + ";missing"}]
        a.receive(p, s, d)
        events = [e for e in a.guard.evidence.values() if e.kind == "media"]
        self.assertEqual(len(events), 1); self.assertFalse(events[0].data["actual"])

    def test_ack_and_retry_are_idempotent(self):
        s, p, d, a = self.fixture()
        out = a.receive(p, s, d)
        self.assertEqual(a.receive(p, s, d), out)
        p = deepcopy(p); p["now"] = 118; p["level"] = 1
        p["events"] = [{"type": "personal_execution", "actual": 117, "detail": "request=" + d["requestId"] + ";action=ON;level=1"}]
        a.receive(p, s); a.receive(p, s)
        self.assertEqual(len(a.guard.timeline), 1)
        self.assertTrue(a.guard.revoked)

    def test_http_boundary_and_off_observe_sequence(self):
        sequences = []
        for mode in ("off", "observe", "enforce"):
            with tempfile.TemporaryDirectory() as folder:
                s, p = decision_fixture(Path(folder) / "fixture", control_rows(), admitted=False)
                p["config"]["evidenceGuardMode"] = mode
                service = StudyService(Path(folder) / "service")
                service.receive(p)
                host = service.sessions[p["sessionId"]]["personal"]
                host.attempts = 2; host.report = {"admitted": False}
                server = ThreadingHTTPServer(("127.0.0.1", 0), make_handler(service))
                worker = threading.Thread(target=server.serve_forever, daemon=True); worker.start()
                try:
                    request = Request("http://127.0.0.1:" + str(server.server_port) + "/decide", data=json.dumps(p).encode(), headers={"Content-Type": "application/json"})
                    with urlopen(request) as response: result = json.load(response)
                    sequences.append(result["decision"]["action"])
                    self.assertEqual(host.decisions[p["requestId"]]["action"], "ON")
                finally:
                    server.shutdown(); server.server_close(); worker.join()
        self.assertEqual(sequences, ["ON", "ON", "HOLD"])

    def test_recorded_replay_is_separate_and_reuses_frozen_candidates(self):
        from replay_evidence_guard import run
        s, p, d, _ = self.fixture(eyes=False)
        source = self.folder / "source"; source.mkdir()
        (source / "telemetry.jsonl").write_text(json.dumps({"packet": p}) + "\n", encoding="utf-8")
        (source / "personal_decisions.jsonl").write_text(json.dumps({"packet": p, "decision": d}) + "\n", encoding="utf-8")
        output = self.folder / "recorded-replay"
        before = (source / "telemetry.jsonl").read_bytes()
        report = run(source, output, "enforce")
        self.assertEqual(report["replayed_decisions"], 1); self.assertIsNone(report["fault"])
        self.assertEqual(json.loads((output / "replayed_decisions.json").read_text())[0]["delivered"]["action"], "HOLD")
        self.assertEqual((source / "telemetry.jsonl").read_bytes(), before)
        with self.assertRaises(ValueError): run(source, source / "overlap", "observe")


if __name__ == "__main__":
    unittest.main()

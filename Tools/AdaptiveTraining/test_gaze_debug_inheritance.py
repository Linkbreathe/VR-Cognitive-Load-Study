"""Isolated engineering fixtures for gaze preferences at the assignment boundary."""
import base64
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from service import StudyService
from test_personalized import packet
from test_session_store import ARCHIVE_FILES, GAZE_DEBUG_INHERITANCE_REVISION, read


class GazeDebugInheritanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.service = StudyService(self.root / "service", test_state_dir=self.root / "state")
        self.store = self.service.test_store
        self.request = dict(participantCode="ENGINEERING_GAZE_FIXTURE", storageFolder=str(self.root / "data"), sessionMode="practice")

    def tearDown(self):
        self.temp.cleanup()

    def claim(self, highlight=True, ray=True, **extra):
        return dict(clientId="fixture_client", controllerUrl="http://127.0.0.1:8766",
                    gazeDebug=dict(revision=GAZE_DEBUG_INHERITANCE_REVISION, highlight=highlight, ray=ray), **extra)

    def test_flags_survive_freeze_telemetry_and_archive(self):
        for highlight, ray in ((False, False), (True, False), (False, True), (True, True)):
            with self.subTest(highlight=highlight, ray=ray):
                record = self.store.prepare(self.request)
                before = copy.deepcopy(record["config"])
                reply = self.store.claim(self.claim(highlight, ray))
                expected = dict(before, controllerUrl="http://127.0.0.1:8766", engineeringGazeHighlight=highlight, engineeringGazeRay=ray)
                self.assertEqual(reply["config"], expected)
                self.assertEqual(reply["gazeDebugInheritanceRevision"], GAZE_DEBUG_INHERITANCE_REVISION)
                persisted = read(Path(record["folder"]) / "test-session.json")
                self.assertEqual(persisted["config"], expected)
                self.assertEqual(persisted["gazeDebugInheritance"]["highlight"], highlight)
                # A retry cannot change a previously frozen assignment.
                self.assertEqual(self.store.claim(self.claim(not highlight, not ray)), reply)
                p = packet()
                p.update(sessionId=record["sessionId"], participantCode=self.request["participantCode"],
                         sessionType="editor-test", config=reply["config"], stage="preparation", testClientId="fixture_client")
                self.service.receive(p)
                changed = copy.deepcopy(p)
                changed["config"]["engineeringGazeRay"] = not ray
                with self.assertRaisesRegex(ValueError, "differs"):
                    self.store.verify_packet(changed)
                entries = []
                for name in ARCHIVE_FILES:
                    value = expected if name == "adaptive-training-config.json" else dict(status="aborted", sessionId=record["sessionId"],
                            participantCode=self.request["participantCode"], sessionType="editor-test")
                    data = json.dumps(value).encode() if name.endswith(".json") else b"header\n"
                    self.store.archive(dict(sessionId=record["sessionId"], clientId="fixture_client", name=name,
                                            offset=0, data=base64.b64encode(data).decode()))
                    entries.append(dict(name=name, size=len(data), sha256=hashlib.sha256(data).hexdigest()))
                self.store.archive(dict(sessionId=record["sessionId"], clientId="fixture_client", manifest=entries))
                self.assertEqual(record["archive"]["status"], "complete_core_records")
                self.store.complete(record["sessionId"], "aborted")

    def test_participant_and_disabled_config_never_enable_overlays(self):
        for kind, enabled in (("participant", True), ("engineering", False)):
            record = self.store.prepare(self.request)
            record["config"].update(sessionType=kind, enabled=enabled)
            reply = self.store.claim(self.claim())
            self.assertFalse(reply["config"]["engineeringGazeHighlight"])
            self.assertFalse(reply["config"]["engineeringGazeRay"])
            self.store.complete(record["sessionId"], "aborted")

    def test_invalid_preferences_do_not_claim_or_change_config(self):
        record = self.store.prepare(self.request)
        before = copy.deepcopy(record)
        for preferences in ("true", {}, dict(revision="unknown", highlight=True, ray=True),
                            dict(revision=GAZE_DEBUG_INHERITANCE_REVISION, highlight=1, ray=True),
                            dict(revision=GAZE_DEBUG_INHERITANCE_REVISION, highlight=True, ray="false")):
            request = self.claim()
            request["gazeDebug"] = preferences
            with self.assertRaisesRegex(ValueError, "Invalid engineering gaze"):
                self.store.claim(request)
            self.assertEqual(record, before)

    def test_legacy_claim_defaults_and_wrong_client(self):
        record = self.store.prepare(self.request)
        request = self.claim()
        request.pop("gazeDebug")
        reply = self.store.claim(request)
        self.assertFalse(reply["config"]["engineeringGazeHighlight"])
        self.assertFalse(reply["config"]["engineeringGazeRay"])
        self.assertEqual(reply["gazeDebugInheritanceRevision"], "")
        wrong = self.claim()
        wrong["clientId"] = "other_client"
        self.assertFalse(self.store.claim(wrong)["ready"])
        self.assertFalse(record["config"]["engineeringGazeRay"])


if __name__ == "__main__":
    unittest.main()

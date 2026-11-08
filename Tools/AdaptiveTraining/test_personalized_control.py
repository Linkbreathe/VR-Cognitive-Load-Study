"""Synthetic regressions for the user-approved 70/40 behavior and 0.45/0.65 score rules."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import threading
import unittest
from http.server import ThreadingHTTPServer
from urllib.request import Request, urlopen

import joblib

from personalized_model import DEFAULT_POLICY, VERSION, behavioral_summary, load_personal, policy_config
from personalized_runtime import PersonalSession
from service import StudyService, make_handler
from test_personalized import packet, trials


def control_rows(wrong=(), omissions=(), rt=3.9):
    """Twenty scored trials, balanced across answer classes, preceded by five older trials."""
    rows = trials(25, "bioadaptive")
    for i, row in enumerate(rows[-20:]):
        row.update(expectedMatch=i % 2 == 0, responded=i not in omissions,
                   correct=i not in wrong and i not in omissions,
                   reactionTime=-1 if i in omissions else rt)
    return rows


def control_packet(rows, level=0):
    p = packet()
    p.update(stage="bioadaptive", trials=copy.deepcopy(rows), now=114,
             completedTrials=25, lastTrial=25, level=level)
    for row in p["trials"]:
        row["level"] = level
    return p


def decision_fixture(folder, rows, level=0, score=.45, admitted=True, attempts=2):
    p = control_packet(rows, level)
    s = PersonalSession(p, folder, None)
    s.trials = {r["phaseTrial"]: r for r in p["trials"]}
    s.report = {"admitted": admitted}
    s.attempts = attempts
    s.intervals = [(0, 20)]  # Synthetic actual-media evidence for the unchanged L1 gate.
    for i in range(22, 25):
        s.predictions.append({"stage": "bioadaptive", "trial": i, "start": i * 4.5 - 22.5,
                              "level": level, "valid": True, "score": score})
    return s, p


class ControlRulesTests(unittest.TestCase):
    def decide(self, rows=None, **kwargs):
        with tempfile.TemporaryDirectory() as folder:
            session, p = decision_fixture(folder, control_rows() if rows is None else rows, **kwargs)
            return session.decide(p)

    def test_stable_inclusive_ba_and_score_with_three_omissions_and_slow_rt(self):
        rows = control_rows(wrong=range(6), omissions=(0, 2, 4), rt=3.99)
        d = self.decide(rows)
        self.assertAlmostEqual(d["behavior"]["ba"], .7)
        self.assertEqual(d["behavior"]["omissions"], 3)
        self.assertEqual(d["action"], "ON")
        self.assertEqual(d["behavior"]["state"], "stable")

    def test_stable_rejects_ba_below_seventy(self):
        d = self.decide(control_rows(wrong=range(7)))
        self.assertAlmostEqual(d["behavior"]["ba"], .65)
        self.assertEqual(d["action"], "HOLD")

    def test_four_and_five_omissions_do_not_support_either_adjustment(self):
        for count in (4, 5):
            rows = control_rows(omissions=range(count))
            for level, score in ((0, .2), (1, .9)):
                with self.subTest(omissions=count, level=level):
                    d = self.decide(rows, level=level, score=score)
                    self.assertEqual(d["behavior"]["state"], "undetermined")
                    self.assertEqual(d["action"], "HOLD")

    def test_six_cumulative_omissions_trigger_difficulty_without_a_streak(self):
        d = self.decide(control_rows(omissions=(0, 2, 4, 6, 8, 10)), level=1, score=.65)
        self.assertAlmostEqual(d["behavior"]["ba"], .7)
        self.assertEqual(d["behavior"]["omissions"], 6)
        self.assertEqual(d["behavior"]["state"], "struggling")
        self.assertEqual(d["action"], "OFF")

    def test_three_consecutive_omissions_no_longer_mean_difficulty(self):
        d = self.decide(control_rows(omissions=(0, 1, 2)))
        self.assertEqual(d["behavior"]["state"], "stable")
        self.assertEqual(d["action"], "ON")

    def test_difficulty_ba_boundary_and_buffer(self):
        for wrong_count, expected in ((12, "OFF"), (11, "HOLD")):
            with self.subTest(wrong=wrong_count):
                d = self.decide(control_rows(wrong=range(wrong_count)), level=1, score=.65)
                self.assertEqual(d["action"], expected)

    def test_all_wrong_and_all_omitted_can_reduce_without_a_correct_rt(self):
        for rows in (control_rows(wrong=range(20)), control_rows(omissions=range(20))):
            d = self.decide(rows, level=1, score=.65)
            self.assertTrue(d["behavior"]["valid"])
            self.assertIsNone(d["behavior"]["rt"])
            self.assertEqual(d["action"], "OFF")
            json.dumps(d, allow_nan=False)

    def test_model_score_boundaries(self):
        for score, expected in ((.449999, "ON"), (.45, "ON"), (.450001, "HOLD")):
            self.assertEqual(self.decide(score=score)["action"], expected)
        for score, expected in ((.649999, "HOLD"), (.65, "OFF"), (.650001, "OFF")):
            self.assertEqual(self.decide(control_rows(wrong=range(12)), level=1, score=score)["action"], expected)

    def test_behavior_model_disagreement_holds(self):
        self.assertEqual(self.decide(score=.8)["action"], "HOLD")
        self.assertEqual(self.decide(control_rows(wrong=range(12)), level=1, score=.2)["action"], "HOLD")

    def test_recent_twenty_only(self):
        rows = control_rows(omissions=(0, 2, 4))
        for row in rows[:5]:
            row.update(correct=False, responded=False, reactionTime=-1)
        d = self.decide(rows)
        self.assertEqual(d["behavior"]["omissions"], 3)
        self.assertEqual(d["action"], "ON")

    def test_fallback_uses_same_rules_only_after_two_attempts(self):
        stable = control_rows(wrong=range(6), omissions=(0, 2, 4))
        difficult = control_rows(omissions=(0, 2, 4, 6, 8, 10))
        self.assertEqual(self.decide(stable, admitted=False, attempts=1)["action"], "HOLD")
        for rows, level, expected in ((stable, 0, "ON"), (difficult, 1, "OFF")):
            d = self.decide(rows, level=level, admitted=False)
            self.assertEqual(d["action"], expected)
            self.assertEqual(d["basis"], "behavioral_fallback")
            self.assertFalse(d["physiologicalEligible"])

    def test_retained_prediction_exposure_mount_and_fallback_gates(self):
        with tempfile.TemporaryDirectory() as folder:
            s, p = decision_fixture(folder, control_rows(omissions=range(6)), level=1, score=.65)
            s.intervals = []
            self.assertEqual(s.decide(p)["reason"], "insufficient_actual_movie_exposure")
            s.intervals = [(0, 20)]; s.predictions.pop()
            self.assertEqual(s.decide(p)["reason"], "fewer_than_three_recent_pure_condition_predictions")
            s.mount_intact = False
            self.assertEqual(s.decide(p)["action"], "HOLD")
            s.mount_intact = True; s.report["admitted"] = False
            s.policy["enable_behavioral_fallback"] = False
            self.assertEqual(s.decide(p)["action"], "HOLD")

    def test_retained_behavior_validity_gates(self):
        for field in ("interrupted", "aborted"):
            rows = control_rows(); rows[-1][field] = True
            self.assertEqual(self.decide(rows)["action"], "HOLD")
        rows = control_rows(); rows[-1]["transition"] = True
        self.assertEqual(self.decide(rows)["action"], "ON")
        self.assertFalse(behavioral_summary(control_rows()[:19])["valid"])
        rows = control_rows()
        for row in rows[-20:]: row["expectedMatch"] = False
        self.assertFalse(behavioral_summary(rows)["valid"])

    def test_both_policy_files_and_revision_match_defaults(self):
        saved = policy_config(json.loads(Path(__file__).with_name("personalized-policy.json").read_text(encoding="utf-8")))
        for key in ("revision", "stable_ba", "stable_omissions_below", "struggling_ba", "struggling_omissions_above", "thresholds"):
            self.assertEqual(saved[key], DEFAULT_POLICY[key])
        self.assertFalse(saved["frozen"])
        self.assertFalse(saved["device_qualified"])
        with self.assertRaises(ValueError):
            policy_config({"revision":"personalized-rf-pilot-v3-control70-40"})
        self.assertEqual(self.decide()["policyRevision"], DEFAULT_POLICY["revision"])

    def test_old_policy_and_invalid_omission_thresholds_rejected(self):
        for changes in ({"revision": "personalized-rf-pilot-v2-window5"}, {"stable_max_rt": 3.0},
                        {"stable_omissions_below": 4.5}, {"struggling_omissions_above": 3},
                        {"struggling_omissions_above": 20}, {"stable_omissions_below": True}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                policy_config(changes)

    def test_old_model_policy_rejected_even_with_valid_hash(self):
        with tempfile.TemporaryDirectory() as folder:
            artifact = Path(folder) / "old_fixture.joblib"
            joblib.dump({"participant": "fixture", "session": "fixture", "report": {
                "admitted": True, "version": VERSION, "policy": {"revision": "personalized-rf-pilot-v2-window5"}}}, artifact)
            digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
            with self.assertRaises(ValueError):
                load_personal(artifact, "fixture", "fixture", digest)

    def test_http_roundtrip_audits_new_policy_and_caches_decision(self):
        with tempfile.TemporaryDirectory() as folder:
            service = StudyService(folder)
            p = control_packet(control_rows(wrong=range(6), omissions=(0, 2, 4)))
            service.receive(p)
            session = service.sessions[p["sessionId"]]["personal"]
            session.attempts = 2
            server = ThreadingHTTPServer(("127.0.0.1", 0), make_handler(service))
            worker = threading.Thread(target=server.serve_forever, daemon=True); worker.start()
            try:
                responses = []
                for _ in range(2):
                    request = Request(f"http://127.0.0.1:{server.server_port}/decide", data=json.dumps(p).encode(),
                                      headers={"Content-Type": "application/json"})
                    with urlopen(request, timeout=5) as response: responses.append(json.load(response))
                d = responses[0]["decision"]
                self.assertEqual(d["action"], "ON")
                self.assertEqual(d["policyRevision"], DEFAULT_POLICY["revision"])
                self.assertEqual(d, responses[1]["decision"])
                saved = json.loads((session.folder / "personal_protocol.json").read_text())
                self.assertEqual(saved["policy"]["revision"], d["policyRevision"])
                self.assertIn(d["policyRevision"], (session.folder / "personal_decisions.jsonl").read_text())
            finally:
                server.shutdown(); server.server_close(); worker.join(timeout=2)


if __name__ == "__main__":
    unittest.main()

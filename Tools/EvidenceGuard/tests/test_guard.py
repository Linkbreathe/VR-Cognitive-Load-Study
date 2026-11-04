"""Engineering evidence only; no inference about participant benefit or gaze cutoffs."""
from dataclasses import replace
import json
import unittest

from evidence_guard import EvidenceGuard, Judgment, Enactment
from evidence_guard.demos import cases, evidence, proposal, stable, SCENE, SESSION
from evidence_guard.gaze import summarize
from evidence_guard.replay import replay


class GuardTests(unittest.TestCase):
    def test_three_case_acceptance(self):
        for mode in ("off", "observe", "enforce"):
            for name, ops in cases().items():
                g, results = replay(ops, mode)
                expected = ["ON"] if name.startswith("A") else ["OFF"] * 3 if name.startswith("B") else ["HOLD", "ON"] if mode == "enforce" else ["ON", "ON"]
                self.assertEqual([r["effective_action"] for r in results], expected)
                if mode != "off" and name.startswith("B"):
                    self.assertEqual(g.relations["exposed::exposed-signal"]["state"], "SUPPORTED")
                    self.assertEqual(g.relations["failed::failed-signal"]["state"], "DISPUTED")
                    self.assertEqual(g.relations["preexisting::preexisting-signal"]["state"], "DISPUTED")
                json.dumps(g.export_audit(), allow_nan=False)

    def test_missing_gaze_is_unavailable_not_zero_task(self):
        g = EvidenceGuard(SESSION, "enforce")
        e = evidence("t", "trial", 0, 4, visible_start=0, visible_end=4)
        g.ingest(e); g.add_judgment(Judgment("j", "training_challenge", ("t",), 0, SCENE, True))
        self.assertIsNone(g.gaze([(0, 4)])["task_gaze_ratio"])
        self.assertEqual(g.review(proposal("p", 5, [("j",)]))["verdict"], "DEFER")
        g.add_judgment(Judgment("nonvisual", "training_challenge", ("t",), 0, SCENE))
        self.assertEqual(g.review(proposal("q", 5, [("nonvisual",)]))["verdict"], "ALLOW")

    def test_gaze_duration_missing_time_exclusive_and_window(self):
        samples = [evidence("g1", "gaze", 0, .1, target="task", task_visible=True),
                   evidence("g2", "gaze", .1, .2, target="movie", movie_visible=True),
                   evidence("g3", "gaze", .2, .3, quality=False, target="invalid"),
                   evidence("g4", "gaze", .3, .4, target="task", task_visible=False)]
        r = summarize(samples + samples, [(0, .5), (0, .2)])
        self.assertAlmostEqual(r["gaze_valid_ratio"], .6)
        self.assertAlmostEqual(r["task_gaze_ratio"], 1 / 3)
        self.assertAlmostEqual(r["movie_gaze_ratio"], 1 / 3)
        self.assertAlmostEqual(summarize(samples, [(0, .1)])["task_gaze_ratio"], 1)

    def test_rf_crossing_requires_nonoverlap_and_quality(self):
        for end, quality, expected in ((22.5, True, 1), (30, True, 0), (22.5, False, 0)):
            g = EvidenceGuard(SESSION)
            g.ingest(evidence("first", "signal", 0, end, quality=quality, score=.3))
            g.ingest(evidence("next", "signal", 22.5, 45, score=.8))
            reverse = [r for r in g.audit if r["record"] == "reverse"]
            self.assertEqual(len(reverse), expected)
            if reverse:
                self.assertTrue(reverse[0]["rf_change_is_not_physiology"])

    def test_async_relation_and_unresolved_not_disputed(self):
        g = EvidenceGuard(SESSION)
        g.ingest(evidence("s", "signal", 10, 32.5, 1, score=.8, response_observed=True))
        g.ingest(evidence("m", "media", 15, 26, 1, actual=True))
        self.assertEqual(g.relations["m::s"]["state"], "UNRESOLVED")
        g.ingest(evidence("eye", "gaze", 15, 15.1, 1, target="movie", movie_visible=True))
        self.assertEqual(g.relations["m::s"]["state"], "UNRESOLVED")

    def test_dispute_only_revokes_dependent_path(self):
        g = EvidenceGuard(SESSION, "enforce")
        g.ingest(evidence("m", "media", 10, 10, actual=False))
        g.ingest(evidence("s", "signal", 10, 20, score=.8, claimed_event="m"))
        g.ingest(evidence("t", "trial", 15, 20))
        g.add_judgment(Judgment("bad", "movie_attribution", ("s",), 0, SCENE, relation_id="m::s"))
        g.add_judgment(Judgment("good", "training_challenge", ("t",), 0, SCENE))
        self.assertEqual(g.review(proposal("p", 21, [("bad",)]))["verdict"], "DEFER")
        self.assertEqual(g.review(proposal("q", 22, [("bad",), ("good",)]))["verdict"], "ALLOW")
        self.assertIn("bad", g.revoked); self.assertNotIn("good", g.revoked)
        self.assertEqual(len(g.evidence), 3)

    def test_no_time_based_restoration_and_natural_new_evidence(self):
        g = EvidenceGuard(SESSION, "enforce")
        g.ingest(evidence("l0", "trial", 10, 20))
        g.add_judgment(Judgment("recovery", "recovered_l1", ("l0",), 0, SCENE))
        self.assertEqual(g.review(proposal("p", 21, [("recovery",)]))["verdict"], "DEFER")
        g.review(proposal("intermediate-hold", 90, action="HOLD"))
        r = g.review(proposal("q", 200, [("recovery",)]))
        self.assertEqual(r["verdict"], "DEFER"); self.assertEqual(r["defer_duration_seconds"], 179)
        g.ingest(evidence("new-movie", "media", 180, 191, 1, actual=True, audio_confirmed=True))
        g.ingest(evidence("new-l1", "trial", 190, 194, 1))
        g.add_judgment(Judgment("reassessed", "recovered_l1", ("new-movie", "new-l1"), 1, SCENE, comparable=True))
        self.assertEqual(g.review(proposal("r", 201, [("reassessed",)]))["verdict"], "ALLOW")
        self.assertIn("recovery", g.revoked)

    def test_reset_preserves_raw_but_revokes_support(self):
        g = EvidenceGuard(SESSION, "enforce")
        g.ingest(evidence("t", "trial", 0, 4))
        g.add_judgment(Judgment("j", "current_stable", ("t",), 0, SCENE))
        g.reset("mount", 5)
        self.assertEqual(g.review(proposal("p", 6, [("j",)]))["verdict"], "DEFER")
        self.assertIn("t", g.evidence)

    def test_reductions_hold_and_illegal_upgrades(self):
        for mode in ("off", "observe", "enforce"):
            g = EvidenceGuard(SESSION, mode)
            for action in ("OFF", "PAUSE", "EXIT", "REST", "SAFETY_REDUCE"):
                self.assertEqual(g.review(proposal(action, 1, action=action))["effective_action"], action)
            self.assertEqual(g.review(proposal("hold", 1, action="HOLD"))["verdict"], "NO_CHANGE")
            self.assertEqual(g.review(replace(proposal("bad", 1), legal=False))["verdict"], "DEFER")

    def test_retry_identity_and_immutable_records(self):
        g = EvidenceGuard(SESSION, "enforce")
        e = evidence("t", "trial", 0, 4); g.ingest(e); g.ingest(e)
        self.assertEqual(len(g.evidence), 1)
        with self.assertRaises(ValueError): g.ingest(replace(e, end=5))
        p = proposal("p", 5, action="HOLD")
        self.assertEqual(g.review(p), g.review(p))
        with self.assertRaises(ValueError): g.review(replace(p, action="ON"))
        g.reset("pause", 6)
        with self.assertRaises(ValueError): g.review(p)

    def test_counterfactual_never_changes_authorization(self):
        for cf in (None, {"reference_score": .01}, {"unavailable": "error"}):
            g = EvidenceGuard(SESSION, "enforce")
            g.ingest(evidence("s", "signal", 0, 22.5, score=.3, counterfactual=cf))
            g.add_judgment(Judgment("j", "current_stable", ("s",), 0, SCENE))
            self.assertEqual(g.review(proposal("p", 23, [("j",)]))["verdict"], "ALLOW")

    def test_configurable_modality_delay(self):
        g = EvidenceGuard(SESSION, config={"response_delays": {"rf": [0, 10], "eeg": [0, 0], "ecg": [5, 15], "behavior": [0, 4.5]}})
        g.ingest(evidence("movie", "media", 10, 21, 1, actual=True, audio_confirmed=True))
        for kind in ("eeg", "ecg"):
            g.ingest(evidence(kind, "signal", 23, 25, 1, modality=kind, response_observed=True, claimed_event="movie"))
        self.assertEqual(g.relations["movie::eeg"]["state"], "DISPUTED")
        self.assertEqual(g.relations["movie::ecg"]["state"], "SUPPORTED")

    def test_actual_ack_not_proposal_changes_support(self):
        g = EvidenceGuard(SESSION, "enforce")
        g.ingest(evidence("t", "trial", 0, 4, 1))
        g.add_judgment(Judgment("j", "current_stable", ("t",), 1, SCENE))
        g.review(proposal("off", 5, action="OFF", level=1))
        self.assertNotIn("j", g.revoked)
        ack = Enactment("ack", "off", SESSION, 6, 1, 0, SCENE)
        g.on_enactment(ack); g.on_enactment(ack)
        self.assertIn("j", g.revoked); self.assertEqual(len(g.timeline), 1)

    def test_renaming_old_evidence_cannot_restore_support_after_roundtrip(self):
        g = EvidenceGuard(SESSION, "enforce")
        g.ingest(evidence("t", "trial", 0, 4))
        g.on_enactment(Enactment("on", "p", SESSION, 5, 0, 1, SCENE))
        g.on_enactment(Enactment("off", "q", SESSION, 6, 1, 0, SCENE))
        g.add_judgment(Judgment("new-name-old-evidence", "current_stable", ("t",), 0, SCENE))
        self.assertEqual(g.review(proposal("r", 7, [("new-name-old-evidence",)]))["verdict"], "DEFER")


if __name__ == "__main__":
    unittest.main()

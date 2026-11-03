"""Dev10 validity regressions; all data are synthetic or isolated test fixtures."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import numpy as np

from personalized_model import policy_config, window_behavior, label_fit, label_rows, metrics
from personalized_runtime import PersonalSession
from personalized_signals import eeg_features
from personalized_validation import circular_permutation_test
from test_personalized import packet, trials, training_rows
from analyze_personalized import summarize


class WindowLabelTests(unittest.TestCase):
    def setUp(self):
        self.policy = policy_config()
        self.rows = trials(12)[2:7]

    def label(self, rows):
        b = window_behavior(rows, self.policy)
        return b, label_rows([{"behavior": b}], label_fit([], self.policy), self.policy)[0]

    def test_exact_scores_correct_error_omission_and_buffer(self):
        b, label = self.label(self.rows)
        self.assertAlmostEqual(b["score"], 1.05); self.assertEqual(label, 0)
        self.rows[0].update(correct=False)
        self.assertEqual(self.label(self.rows)[1], -1)
        self.rows[1].update(correct=False, responded=False, reactionTime=-1)
        b, label = self.label(self.rows)
        self.assertEqual(label, 1); self.assertEqual(b["omissions"], 1)
        self.assertAlmostEqual(b["score"], .23)

    def test_no_balanced_accuracy_or_match_quota(self):
        for t in self.rows: t["expectedMatch"] = False
        b, label = self.label(self.rows)
        self.assertTrue(b["valid"]); self.assertNotIn("ba", b); self.assertEqual(label, 0)

    def test_reaction_time_is_small_and_only_for_correct_responses(self):
        fast = self.label(self.rows)[0]["score"]
        for t in self.rows: t["reactionTime"] = 3.9
        slow, label = self.label(self.rows)
        self.assertGreater(fast, slow["score"]); self.assertEqual(label, 0)
        for t in self.rows: t.update(correct=False, reactionTime=0)
        self.assertEqual(self.label(self.rows)[0]["score"], -1)

    def test_transition_gap_and_nonfinite_rt_are_rejected(self):
        for key, value in (("transition", True), ("interrupted", True), ("scored", False), ("reactionTime", float("nan"))):
            rows = copy.deepcopy(self.rows); rows[0][key] = value
            self.assertFalse(window_behavior(rows, self.policy)["valid"])
        rows = copy.deepcopy(self.rows); rows[-1]["phaseTrial"] += 1
        self.assertFalse(window_behavior(rows, self.policy)["valid"])

    def test_later_answers_cannot_relabel_an_earlier_window(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); p["trials"] = trials()
            session = PersonalSession(p, folder, None); session.receive(p)
            before = next(r for r in session.prepare_training_rows() if r["trial"] == 6)
            for t in session.trials.values():
                if t["phaseTrial"] >= 7: t.update(correct=False, responded=False)
            after = next(r for r in session.prepare_training_rows() if r["trial"] == 6)
            self.assertEqual(before["behavior"], after["behavior"])
            later = next(r for r in session.prepare_training_rows() if r["trial"] == 11)
            self.assertNotEqual(after["behavior"]["score"], later["behavior"]["score"])

    def test_old_label_policy_and_unqualified_null_cannot_be_frozen(self):
        with self.assertRaises(ValueError): policy_config({"label_alignment": "retrospective_block10"})
        with self.assertRaises(ValueError): policy_config({"label_trials": 10})
        with self.assertRaises(ValueError): policy_config({"permutation_count": 19})
        with self.assertRaises(ValueError): policy_config({"frozen": True, "auc_minimum": .7,
            "device_qualified": True, "eeg_units_confirmed": True, "electrode_description": "fixture"})


class NullAndMountTests(unittest.TestCase):
    def test_one_label_per_block_cannot_pass_the_circular_gate(self):
        rows = [dict(r, label=r["block"] % 2) for r in training_rows()]
        report = circular_permutation_test(rows, 1., policy_config(), 42, None, metrics)
        self.assertEqual(report["status"], "invariant_label_trajectories")
        self.assertEqual(report["p_value"], 1); self.assertFalse(report["passed"])

    def test_circular_null_preserves_each_block_and_refits_every_draw(self):
        rows = training_rows()
        for r in rows: r["label"] = 1 if r["behavior"]["score"] < .4 else 0
        rows[2]["label"] = -1; rows[10]["valid"] = False
        original = copy.deepcopy(rows); seen = []
        def validate(permuted, *args, **kwargs):
            self.assertFalse(kwargs["comparators"])
            for block in range(8):
                a = [r for r in original if r["block"] == block]
                b = [r for r in permuted if r["block"] == block]
                self.assertTrue(any(np.array_equal(np.roll([r["label"] for r in a], shift), [r["label"] for r in b]) for shift in range(6)))
                self.assertEqual([r["valid"] for r in a], [r["valid"] for r in b])
                self.assertEqual([r["features"] for r in a], [r["features"] for r in b])
            seen.append(1)
            return {"permutation_statistic": .5}
        with patch("personalized_validation.block_validation", side_effect=validate):
            report = circular_permutation_test(rows, .9, policy_config(), 42, None, metrics)
        self.assertEqual(rows, original); self.assertEqual(len(seen), 199)
        self.assertEqual(report["p_value"], .005); self.assertTrue(report["passed"])
        self.assertFalse(report["exchangeability_qualified"])

    def test_incomplete_null_draws_count_against_admission(self):
        rows = [dict(r, label=i % 2) for i, r in enumerate(training_rows())]
        with patch("personalized_validation.block_validation", return_value={"permutation_statistic": None}):
            report = circular_permutation_test(rows, .9, policy_config(), 42, None, metrics)
        self.assertEqual(report["unusable_draws"], 199)
        self.assertEqual(report["p_value"], 1); self.assertFalse(report["passed"])

    def test_mount_movement_latches_and_cancels_cached_control(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); p.update(stage="random", trials=trials(20, "random"), now=91)
            s = PersonalSession(p, folder, None)
            s.receive(p, True); s.report = {"admitted": True}; s.model = object()
            p["events"] = [{"type": "personal_mount_interrupted", "actual": 92, "detail": "operator"}]
            reply = s.receive(p, True)
            self.assertFalse(reply["modelAdmitted"]); self.assertEqual(reply["decision"]["action"], "HOLD")
            self.assertIn("movement", reply["decision"]["reason"])
            s.event({"type": "resume", "actual": 93, "detail": ""})
            self.assertFalse(s.mount_intact)
            s.start_fit(); self.assertEqual(s.attempts, 0)

    def test_analysis_retains_mount_invalidation_after_an_earlier_model_pass(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "session.json").write_text(json.dumps({"sessionType": "synthetic", "participantCode": "fixture", "status": "aborted"}))
            (root / "adaptive-training-config.json").write_text(json.dumps({"questionnaireAdministration": "interviewer_oral_headset_on-v1"}))
            (root / "personal-model-report.json").write_text(json.dumps({"admitted": True}))
            (root / "events.jsonl").write_text(json.dumps({"type": "personal_mount_interrupted", "detail": "fixture"}) + "\n")
            report = summarize(root)
            self.assertFalse(report["model_effectively_admitted"])
            self.assertEqual(report["training"]["bioadaptive"]["label"], "mount_interrupted_requires_restart")


class ChannelAuditTests(unittest.TestCase):
    def test_one_bad_channel_is_identified_without_silent_removal(self):
        t = np.arange(0, 22.5, 1 / 250)
        x = np.stack([20 * np.sin(2 * np.pi * 6 * t), 200 * np.sin(2 * np.pi * 10 * t)], axis=1)
        _, quality = eeg_features(x, 250, [0, 1], [0, 1])
        self.assertFalse(quality["valid"])
        self.assertEqual(quality["channels"][0]["amplitude_rejections"], 0)
        self.assertEqual(quality["channels"][1]["amplitude_rejections"], 21)
        self.assertEqual(quality["flatline_rejected_subsegments"], 0)
        self.assertTrue(eeg_features(x[:, [0]], 250, [0], [0])[1]["valid"])

    def test_volts_require_conversion_once_and_dc_is_not_amplitude(self):
        t = np.arange(0, 22.5, 1 / 250); uv = (20 * np.sin(2 * np.pi * 6 * t))[:, None]
        self.assertFalse(eeg_features(uv / 1e6, 250, [0], [0])[1]["valid"])
        _, converted = eeg_features(uv / 1e6 * 1e6, 250, [0], [0])
        self.assertTrue(converted["valid"])
        self.assertTrue(eeg_features(uv + 5000, 250, [0], [0])[1]["valid"])
        self.assertFalse(eeg_features(uv * 1e6, 250, [0], [0])[1]["valid"])


if __name__ == "__main__":
    unittest.main()

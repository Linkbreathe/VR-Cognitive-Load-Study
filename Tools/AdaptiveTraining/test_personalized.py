"""Synthetic engineering fixtures only. No participant/device acceptance claims."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import numpy as np

from allocate_personalized import assignment
from personalized_model import VERSION, PROTOCOL, DEFAULT_POLICY, policy_config, behavioral_summary, label_fit, label_rows, train_personal, load_personal, forest
from personalized_runtime import PersonalSession, random_schedule
from personalized_signals import sampled_window, eeg_features, ecg_features, movie_fraction, resting_reference
from service import StudyService


def packet():
    return {"sessionId": "synthetic_dev11", "participantCode": "synthetic", "sessionType": "synthetic",
            "configVersion": VERSION, "config": {"protocol": PROTOCOL, "seed": 123, "counterbalanceGroup": 0,
                "calibrationBlocks": 8, "trainingTrials": 96, "mode": "ACTIVE"},
            "now": 433, "clockOffset": 0, "clockUncertainty": .01, "stage": "calibration", "level": 0,
            "events": [], "trials": [], "blocks": [], "requestId": "decision1", "block": 1,
            "epoch": 1, "lastTrial": 20, "completedTrials": 20, "evaluationId": 1}


def trials(count=96, stage="calibration"):
    rows = []
    for i in range(count):
        block = i // 12 if stage == "calibration" else i // 24
        level = block % 2 if stage == "calibration" else 0
        rows.append({"block": block, "trial": i, "phaseTrial": i, "blockTrial": i % (12 if stage == "calibration" else 24),
            "trainingStage": stage, "level": level, "plannedOnset": i * 4.5, "actualOnset": i * 4.5,
            "trialEnd": (i + 1) * 4.5, "scored": i >= 2, "interrupted": False, "aborted": False,
            "transition": stage == "calibration" and i >= 12 and i % 12 < 2,
            "expectedMatch": i % 3 == 0, "responded": True, "correct": True, "reactionTime": 1.0})
    return rows


def training_rows():
    rng = np.random.default_rng(10); result = []
    for block in range(8):
        states = rng.permutation([0, 0, 0, 1, 1, 1])
        for i, struggling in enumerate(states):
            result.append({"block": block, "trial": block * 12 + i + 6, "level": block % 2,
                "valid": True, "origin": "live_sensor", "features": [struggling * 4 + rng.normal(0, .1),
                struggling * -4 + rng.normal(0, .1), 65 + struggling * 30 + rng.normal(), (block % 2) * .4],
                "behavior": {"valid": True, "score": .2 if struggling else 1.0}})
    return result


class SignalsTests(unittest.TestCase):
    def test_subsegment_artifacts_and_relative_bands(self):
        rate = 250; t = np.arange(0, 22.5, 1 / rate)
        x = (20 * np.sin(2 * np.pi * 6 * t) + 10 * np.sin(2 * np.pi * 10 * t))[:, None]
        f, q = eeg_features(x, rate, [0], [0])
        self.assertTrue(q["valid"]); self.assertGreater(f["log_theta_relative"], f["log_alpha_relative"])
        offset_features, offset_quality = eeg_features(x + 4000, rate, [0], [0])
        self.assertTrue(offset_quality["valid"])
        self.assertAlmostEqual(offset_features["log_theta_relative"], f["log_theta_relative"], places=6)
        x[:4000] = 200
        self.assertFalse(eeg_features(x, rate, [0], [0])[1]["valid"])

    def test_timestamp_gaps_reject_even_with_sufficient_count(self):
        t = np.arange(0, 22.5, .004); x = np.ones((len(t), 1))
        self.assertIsNotNone(sampled_window(t, x, 0, 22.5, 250)[0])
        t[200:400] += .3
        self.assertIsNone(sampled_window(t, x, 0, 22.5, 250)[0])

    def test_ecg_mean_hr_and_flatline(self):
        rate = 250; t = np.arange(0, 22.5, 1 / rate)
        x = sum(np.exp(-((t - peak) / .012) ** 2) for peak in np.arange(.5, 22.5, 1))
        features, quality = ecg_features(x, rate)
        self.assertTrue(quality["valid"], quality); self.assertAlmostEqual(features["mean_hr"], 60, delta=2)
        self.assertNotIn("lnrmssd", features)
        self.assertFalse(ecg_features(np.zeros_like(t), rate)[1]["valid"])

    def test_exposure_union_uses_actual_intervals(self):
        self.assertAlmostEqual(movie_fraction(0, 20, [(2, 7), (5, 10), (15, 30)]), .65)

    def test_no_forced_iaf(self):
        p = policy_config()
        result = resting_reference(None, None, 0, 0, p)
        self.assertIsNone(result["iaf_hz"]); self.assertFalse(result["used_for_labels"])


class ModelTests(unittest.TestCase):
    def test_no_manufactured_struggling_class(self):
        rows = training_rows()
        for r in rows: r["behavior"].update(score=.95 + r["trial"] / 10000)
        p = policy_config(); labels = label_rows(rows, label_fit(rows, p), p)
        self.assertFalse(np.any(labels == 1))

    def test_unfrozen_policy_and_bad_gate_rejected(self):
        with self.assertRaises(ValueError): policy_config({"frozen": True})
        with self.assertRaises(ValueError): policy_config({"auc_minimum": .4})

    def test_model_block_validation_freezing_and_identity(self):
        p = policy_config({"frozen": True, "device_qualified": True, "eeg_units_confirmed": True,
                           "permutation_exchangeability_qualified": True,
                           "electrode_description": "synthetic fixture simulating qualified live metadata", "auc_minimum": .7})
        with tempfile.TemporaryDirectory() as folder:
            # Fast unit fixture; separate integration evidence exercises the production 300-tree forest.
            with patch("personalized_model.forest", side_effect=lambda policy, seed: forest(dict(policy, n_estimators=7), seed)):
                model, report = train_personal(training_rows(), "test_person", "test_session", "participant", 12, p, folder)
            self.assertTrue(report["admitted"], report["reasons"])
            self.assertEqual(len(report["folds"]), 8)
            for fold in report["folds"]: self.assertNotIn(fold["block"], fold["train_blocks"])
            self.assertIn("exposure_only", report["metrics"]); self.assertIn("physiology_only", report["metrics"])
            path = Path(folder) / "personal_model.joblib"
            loaded = load_personal(path, "test_person", "test_session", report["model_sha256"])
            self.assertEqual(loaded["features"], report["features"])
            with self.assertRaises(ValueError): load_personal(path, "another_person", "test_session", report["model_sha256"])
            with self.assertRaises(ValueError): load_personal(path, "test_person", "test_session", "wrong_hash")

    def test_missing_data_failure_report_is_saved(self):
        with tempfile.TemporaryDirectory() as folder:
            model, report = train_personal([], "test", "session", "synthetic", 1, DEFAULT_POLICY, folder)
            self.assertIsNone(model); self.assertFalse(report["admitted"])
            self.assertTrue((Path(folder) / "model_report.json").exists())
            self.assertIn("engineering_or_replay_session", report["reasons"])


class ProtocolTests(unittest.TestCase):
    def test_trial_aligned_acquisition_to_failed_model_roundtrip(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); p.update(now=0, stageStarted=0)
            service = StudyService(folder, personalized_policy={"eeg_units_confirmed": True})
            service.receive(p)
            rows = trials(); rate = 250
            for i, trial in enumerate(rows):
                t = np.arange(i * 4.5, (i + 1) * 4.5, 1 / rate)
                eeg = (20 * np.sin(2 * np.pi * 6 * t) + 10 * np.sin(2 * np.pi * 10 * t))[:, None]
                ecg = np.exp(-(((t + .5) % 1 - .5) / .012) ** 2)[:, None]
                for kind, samples in (("EEG", eeg), ("ECG", ecg)):
                    cfg = {"kind": kind, "sample_rate": rate, "data_origin": "synthetic", "synthetic": True}
                    service.record_signal(cfg, t.tolist(), t.tolist(), samples.tolist(), 0, kind)
                p.update(now=trial["trialEnd"] + 1, trials=rows[:i+1])
                service.receive(p)
            personal = service.sessions[p["sessionId"]]["personal"]
            self.assertEqual(len(personal.windows), 56)
            self.assertGreater(sum(r["valid"] for r in personal.windows.values()), 35)
            p.update(stage="model_wait", stageStarted=432, now=435)
            service.receive(p)
            personal.fit_thread.join(timeout=30)
            result = service.receive(p)
            self.assertEqual(result["modelStatus"], "personal_model_not_validated")
            self.assertEqual(result["modelAttempt"], 1)
            self.assertFalse(result["modelAdmitted"])

    def test_old_packets_do_not_roll_back_the_session(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); service = StudyService(folder); service.receive(p)
            p["now"] -= 1
            with self.assertRaises(ValueError): service.receive(p)

    def test_four_cell_randomized_balance(self):
        groups = [assignment("P" + str(i), i, 123)["counterbalanceGroup"] for i in range(16)]
        for start in range(0, 16, 4): self.assertEqual(sorted(groups[start:start+4]), [0, 1, 2, 3])
        self.assertEqual(assignment("P0", 0, 123), assignment("P0", 0, 123))

    def test_exact_56_candidates_48_five_trial_labels(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); s = PersonalSession(p, folder, None)
            p["trials"] = trials(); s.receive(p)
            self.assertEqual(len(s.windows), 56)
            self.assertTrue(all(not w["valid"] for w in s.windows.values()))
            labeled = s.prepare_training_rows()
            self.assertEqual(sum(r["behavior"]["valid"] for r in labeled), 48)
            for row in labeled:
                if row["behavior"]["valid"]:
                    self.assertEqual(row["behavior"]["trial_ids"], list(range(row["trial"]-4, row["trial"]+1)))
                    self.assertEqual(row["behavior"]["start"], row["start"])
                    self.assertEqual(row["behavior"]["end"], row["end"])
            self.assertTrue(all(r["stage"] == "calibration" for r in labeled))

    def test_strict_behavior_exclusion(self):
        rows = trials(24, "random")
        self.assertTrue(behavioral_summary(rows)["valid"])
        rows[-1]["transition"] = True
        self.assertFalse(behavioral_summary(rows)["valid"])

    def test_random_plan_independent_of_signals(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); p.update(stage="random", trials=trials(20, "random"), now=91)
            s = PersonalSession(p, folder, None); s.receive(p)
            a = s.decide(p)
            s.model = object(); s.report = {"admitted": True}
            for row in s.trials.values(): row.update(correct=False, responded=False)
            b = s.decide(p)
            self.assertEqual(a, b); self.assertEqual(a["basis"], "random_schedule")
            self.assertEqual(s.plan, random_schedule(123))

    def test_minimum_dwell_after_switch(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); p.update(stage="random", trials=trials(20, "random"), now=91)
            s = PersonalSession(p, folder, None); s.receive(p); s.last_change = 85
            self.assertEqual(s.decide(p)["action"], "HOLD")

    def test_insufficient_pure_predictions_and_exposure_hold(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); p.update(stage="bioadaptive", trials=trials(25, "bioadaptive"), now=114, completedTrials=25, lastTrial=25)
            s = PersonalSession(p, folder, None); s.receive(p); s.report = {"admitted": True}
            self.assertEqual(s.decide(p)["action"], "HOLD")
            for i in range(22, 25):
                s.predictions.append({"stage": "bioadaptive", "trial": i, "start": i * 4.5 - 22.5,
                    "level": 0, "valid": True, "score": .1})
            self.assertEqual(s.decide(p)["action"], "ON")
            p["level"] = 1
            for r in s.predictions: r["level"] = 1
            self.assertEqual(s.decide(p)["reason"], "insufficient_actual_movie_exposure")

    def test_replay_never_written_to_participant_raw_file(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); p["sessionType"] = "participant"; s = StudyService(folder); s.receive(p)
            cfg = {"kind": "EEG", "data_origin": "unknown", "sample_rate": 250}
            with self.assertRaises(ValueError): s.record_signal(cfg, [1], [1], [[1]], 0, "source")
            self.assertFalse((Path(folder) / "participant" / p["sessionId"] / "raw_signals.jsonl").exists())

    def test_decisions_idempotent_and_service_restart_fails_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            p = packet(); p.update(stage="random", trials=trials(20, "random"), now=91)
            service = StudyService(folder); a = service.receive(p, True); b = service.receive(p, True)
            self.assertEqual(a["decision"], b["decision"])
            restarted = StudyService(folder)
            for _ in range(2):
                with self.assertRaises(ValueError): restarted.receive(p)
            self.assertFalse(restarted.sessions)


if __name__ == "__main__":
    unittest.main()

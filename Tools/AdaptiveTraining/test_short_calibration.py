"""Reduced acquisition budget without relaxed model, label or sensor qualification."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from personalized_model import DEFAULT_POLICY, train_personal
from personalized_runtime import PersonalSession
from test_personalized import packet, training_rows


class ShortCalibrationTests(unittest.TestCase):
    def test_legacy_block_configs_use_current_timed_calibration(self):
        with tempfile.TemporaryDirectory() as directory:
            for blocks in (4, 6, 8):
                p = packet(); p["config"]["calibrationBlocks"] = blocks
                folder = Path(directory) / str(blocks)
                s = PersonalSession(p, folder, None)
                metadata = json.loads((folder / "personal_protocol.json").read_text())
                self.assertEqual(metadata["rest_seconds"], 180)
                self.assertEqual(metadata["arithmetic_seconds"], 180)
                self.assertEqual(metadata["calibration_revision"], "rest-math-iaf-rf-v3-5s")
                self.assertEqual(s.policy, DEFAULT_POLICY)
            for blocks in (2, 3, 5, 10):
                p = packet(); p["config"]["calibrationBlocks"] = blocks
                with self.assertRaises(ValueError): PersonalSession(p, Path(directory) / ("bad" + str(blocks)), None)

    def test_new_allocations_default_to_four_and_keep_explicit_legacy_option(self):
        root = Path(__file__).resolve().parents[2]
        template = json.loads((root / "Assets/TaskPlatform/Config/adaptive-training-config.json").read_text())
        self.assertEqual(template["calibrationBlocks"], 4)
        with tempfile.TemporaryDirectory() as directory:
            for name, args, expected in (("short", [], 4), ("long", ["--calibration-blocks", "8"], 8)):
                result = subprocess.run([sys.executable, str(root / "Tools/AdaptiveTraining/allocate_personalized.py"),
                    "--participant", name, "--index", "0", "--allocation-seed", "123", "--output", directory,
                    "--controller-url", "http://127.0.0.1:8765", "--engineering", *args], capture_output=True, text=True)
                self.assertEqual(result.returncode, 0, result.stderr)
                config = json.loads((Path(directory) / (name + "-adaptive-training-config.json")).read_text())
                self.assertEqual(config["calibrationBlocks"], expected)
                self.assertEqual(config["trainingTrials"], 96)
                self.assertEqual(config["rollingWindowTrials"], 20)

    def test_short_dataset_does_not_bypass_class_or_qualification_gates(self):
        rows = [r for r in training_rows() if r["block"] < 4]
        for row in rows: row["behavior"]["score"] = 1.
        with tempfile.TemporaryDirectory() as directory:
            model, report = train_personal(rows, "synthetic", "short", "synthetic", 3, None, directory)
        self.assertIsNone(model)
        self.assertFalse(report["admitted"])
        self.assertIn("insufficient_classes_or_block_distribution", report["reasons"])
        self.assertIn("pilot_policy_or_device_not_frozen", report["reasons"])
        self.assertEqual(report["policy"]["min_class_windows"], 10)
        self.assertEqual(report["policy"]["minimum_varying_blocks_per_condition"], 2)


if __name__ == "__main__":
    unittest.main()

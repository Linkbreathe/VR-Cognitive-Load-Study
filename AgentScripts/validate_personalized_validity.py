"""Run a production-sized forest/null on synthetic engineering fixtures, never participant data."""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools/AdaptiveTraining"))
from personalized_model import policy_config, train_personal
from test_personalized import training_rows

folder = ROOT / "Evidence/PersonalizedValidity/ProductionForestSynthetic"
print("Starting 300-tree leave-block-out validation and 199 circular null refits on synthetic data.", flush=True)
policy = policy_config({"auc_minimum": .7})
_, report = train_personal(training_rows(), "synthetic_validity_fixture", "synthetic_dev10_production_forest",
                           "synthetic", 12, policy, folder)
assert not report["admitted"]
assert report["permutation"]["passed"], report["permutation"]
assert report["permutation"]["completed"] == 199
assert all(report["within_condition"][str(level)]["roc_auc"] >= .7 for level in (0, 1))
assert "engineering_or_replay_session" in report["reasons"]
print(json.dumps({k: report[k] for k in ["elapsed_seconds", "admitted", "metrics", "within_condition", "reasons"]}, indent=2))
print("PASS: full forest/null engineering integration; synthetic fixture does not qualify policy or devices.", flush=True)

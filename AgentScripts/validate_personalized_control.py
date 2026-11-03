"""Generate explicit synthetic Python decisions for the isolated Unity boundary check."""
from pathlib import Path
import json
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools/AdaptiveTraining"))
from personalized_model import DEFAULT_POLICY
from test_personalized_control import control_rows, decision_fixture

cases = []
specifications = [
    ("stable_at_both_thresholds", control_rows(wrong=range(6), omissions=(0, 2, 4)), 0, .45, True, "ON"),
    ("four_omissions_hold", control_rows(omissions=range(4)), 0, .2, True, "HOLD"),
    ("five_omissions_hold", control_rows(omissions=range(5)), 1, .9, True, "HOLD"),
    ("six_scattered_omissions", control_rows(omissions=(0, 2, 4, 6, 8, 10)), 1, .65, True, "OFF"),
    ("forty_percent_ba", control_rows(wrong=range(12)), 1, .65, True, "OFF"),
    ("model_buffer", control_rows(), 0, .450001, True, "HOLD"),
    ("no_correct_rt", control_rows(omissions=range(20)), 1, .65, True, "OFF"),
    ("behavior_fallback_on", control_rows(wrong=range(6), omissions=(0, 2, 4)), 0, .9, False, "ON"),
    ("behavior_fallback_off", control_rows(omissions=(0, 2, 4, 6, 8, 10)), 1, .1, False, "OFF"),
]
for name, rows, level, score, admitted, expected in specifications:
    with tempfile.TemporaryDirectory() as folder:
        session, packet = decision_fixture(folder, rows, level, score, admitted)
        decision = session.decide(packet)
        assert decision["action"] == expected, (name, decision)
        cases.append({"name": name, "initialLevel": level,
                      "expectedLevel": 1 if expected == "ON" else 0 if expected == "OFF" else level,
                      "decisionJson": json.dumps(decision, allow_nan=False)})
out = ROOT / "Evidence/PersonalizedControlRules"
out.mkdir(parents=True, exist_ok=True)
lines = [DEFAULT_POLICY["revision"]]
for case in cases:
    (out / (case["name"] + ".json")).write_text(case["decisionJson"], encoding="utf-8")
    lines.append(f'{case["name"]}\t{case["initialLevel"]}\t{case["expectedLevel"]}')
(out / "unity-fixtures.tsv").write_text("\n".join(lines) + "\n", encoding="utf-8")
(out / "unity-decision-fixtures.json").write_text(json.dumps({
    "evidence": "Synthetic behavior, predictions and admitted-status fixtures; no sensor/model acceptance.",
    "policyRevision": DEFAULT_POLICY["revision"], "cases": cases}, indent=2), encoding="utf-8")
print(f"PASS: {len(cases)} serialized production decisions for isolated Unity verification.")

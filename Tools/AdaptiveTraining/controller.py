"""Deterministic Stage 2 baseline policy. No source attribution or active probes."""
from __future__ import annotations

import math

VERSION = "2.0.0-stage2-dev5"
SUPPORTED_VERSIONS = ("2.0.0-stage2-dev1", "2.0.0-stage2-dev3", "2.0.0-stage2-dev4", VERSION)
POLICY = {
    "baseline_margin": 0.10, "accuracy_floor": 0.75, "rt_ratio": 1.20,
    "max_omissions": 2, "difficulty_accuracy": 0.65, "difficulty_rt_ratio": 1.5,
    "difficulty_omissions": 4, "severe_accuracy": 0.45, "severe_omissions": 8,
    "consecutive_passes": 2, "consecutive_failures": 2,
    "max_feature_age": 5.0, "min_quality": 0.8, "max_clock_uncertainty": 0.1,
}
LEGACY_POLICY = dict(POLICY)
POLICY.update(rolling_max_omissions=1, rolling_difficulty_omissions=2,
              rolling_severe_omissions=4, rolling_min_class=3)


def finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def valid_block(block):
    return (block.get("valid") is True and block.get("eligible", 0) >= 20
            and block.get("matches", 0) >= 4 and block.get("nonmatches", 0) >= 4
            and finite(block.get("balancedAccuracy")) and 0 <= block["balancedAccuracy"] <= 1
            and finite(block.get("medianCorrectRt")) and 0 < block["medianCorrectRt"] < 4)


def baseline(blocks):
    rows = [b for b in blocks if b.get("stage") == "baseline" and valid_block(b)]
    if len(rows) != 2:
        return None
    if any(b["balancedAccuracy"] < .75 or b["omissions"] > 2 for b in rows):
        return None
    return {"balanced_accuracy": sum(b["balancedAccuracy"] for b in rows) / 2,
            "median_correct_rt": sum(b["medianCorrectRt"] for b in rows) / 2,
            "method": "mean_of_two_valid_baseline_block_metrics"}


def physiology_status(features, calibration, packet, policy):
    """Require individual, task-specific validation and fresh single-condition windows."""
    if (calibration.get("participantCode") != packet["participantCode"]
            or calibration.get("configVersion") != packet["configVersion"]
            or calibration.get("task") != "2-back-distraction"
            or not calibration.get("validation_record")
            or calibration.get("validated") is not True):
        return False, False, "individual_calibration_unavailable"
    rules = calibration.get("features", [])
    eligible, adverse = False, False
    for rule in rules:
        if (rule.get("validated") is not True or not finite(rule.get("scale")) or rule["scale"] <= 0
                or not finite(rule.get("center")) or rule.get("adverse_direction") not in (-1, 1)
                or not finite(rule.get("adverse_z")) or rule["adverse_z"] <= 0):
            continue
        candidates = []
        for f in features:
            if (f.get("name") != rule.get("name") or f.get("source_id") != rule.get("source_id")
                    or f.get("sessionId") != packet["sessionId"] or not finite(f.get("value"))
                    or not finite(f.get("quality")) or f["quality"] < policy["min_quality"] or f.get("synthetic", True)
                    or f.get("levels") != [packet["level"]] or f.get("stages") != [packet["stage"]]
                    or not f.get("coverage_complete") or not finite(f.get("clock_uncertainty")) or f["clock_uncertainty"] > policy["max_clock_uncertainty"]
                    or f.get("window_start", -1) < (packet["evaluations"][-1]["conditionStart"] if packet["configVersion"] == VERSION else packet["blocks"][-1]["start"])
                    or not 0 <= packet["now"] - f.get("window_end", -999) <= policy["max_feature_age"]):
                continue
            candidates.append(f)
        if not candidates:
            continue
        eligible = True
        # An adverse candidate blocks promotion; it never independently triggers a downgrade.
        latest = max(candidates, key=lambda f: f["window_end"])
        z = rule["adverse_direction"] * (latest["value"] - rule["center"]) / rule["scale"]
        if z >= rule["adverse_z"]:
            adverse = True
    return eligible, adverse, "adverse_candidate" if adverse else "calibrated_feature_available" if eligible else "no_fresh_valid_feature"


class BaselineAdaptiveController:
    def __init__(self, policy=None):
        self.policy = dict(POLICY if policy is None else policy)
        if set(self.policy) not in (set(POLICY), set(LEGACY_POLICY)) or not all(finite(v) and v > 0 for v in self.policy.values()):
            raise ValueError("Invalid policy parameters")

    def decide(self, packet, features=(), calibration=None):
        if packet.get("configVersion") == VERSION:
            return self.decide_rolling(packet, features, calibration)
        p = self.policy
        out = {key: packet[key] for key in ("sessionId", "requestId", "block", "level", "configVersion")}
        out.update(action="HOLD", reason="insufficient_data", basis=packet["config"]["controlBasis"], physiologicalEligible=False)
        def finish(action, reason):
            return dict(out, action=action, reason=reason)
        if packet["configVersion"] not in SUPPORTED_VERSIONS:
            return finish("HOLD", "configuration_version_mismatch")
        if packet["config"]["mode"] == "OFF":
            return finish("HOLD", "controller_off")
        if packet.get("stop_requested") or packet.get("discomfort"):
            return finish("REST", "participant_stop_or_discomfort")
        blocks = packet.get("blocks", [])
        if not blocks or packet["stage"] != "training":
            return finish("HOLD", "fixed_protocol_stage")
        current = blocks[-1]
        if (current.get("valid") is True and current.get("eligible",0)>=20 and current["block"]==packet["block"]
                and current["level"]==packet["level"] and current.get("omissions",0)>=p["severe_omissions"]):
            return finish("DOWN" if packet["level"]>0 else "REST", "severe_omissions")
        if current["block"] != packet["block"] or current["level"] != packet["level"] or not valid_block(current):
            return finish("HOLD", "invalid_current_evaluation")
        ref = baseline(blocks)
        if ref is None:
            return finish("REST", "baseline_unstable_or_missing")
        recent = []
        for b in reversed(blocks):
            if b.get("stage") != "training" or b.get("level") != packet["level"] or not valid_block(b):
                break
            if recent and b["block"] != recent[-1]["block"]-1:
                break
            recent.append(b)
        def passed(b):
            return (b["balancedAccuracy"] >= max(p["accuracy_floor"], ref["balanced_accuracy"]-p["baseline_margin"])
                    and b["medianCorrectRt"] <= ref["median_correct_rt"]*p["rt_ratio"] and b["omissions"] <= p["max_omissions"])
        def difficult(b):
            return (b["balancedAccuracy"] < p["difficulty_accuracy"] or b["omissions"] > p["difficulty_omissions"]
                    or b["medianCorrectRt"] > ref["median_correct_rt"]*p["difficulty_rt_ratio"])
        if current["balancedAccuracy"] < p["severe_accuracy"] or current["omissions"] >= p["severe_omissions"]:
            return finish("DOWN" if packet["level"] > 0 else "REST", "severe_behavioral_difficulty")
        failures = int(p["consecutive_failures"])
        if len(recent) >= failures and all(difficult(b) for b in recent[:failures]):
            return finish("DOWN" if packet["level"] > 0 else "REST", "consecutive_behavioral_difficulty")
        if out["basis"] == "physiological":
            eligible, adverse, reason = physiology_status(features, calibration or {}, packet, p)
            out["physiologicalEligible"] = eligible
            if not eligible or adverse:
                return finish("HOLD", reason)
        passes = int(p["consecutive_passes"])
        if len(recent) >= passes and all(passed(b) for b in recent[:passes]):
            return finish("UP", "two_valid_passes" if packet["level"] < 2 else "level_two_candidate_mastery")
        return finish("HOLD", "practice_opportunity")

    def decide_rolling(self, packet, features=(), calibration=None):
        p = self.policy
        out = {key: packet[key] for key in ("sessionId", "requestId", "block", "level", "configVersion", "evaluationId", "epoch", "lastTrial")}
        out.update(action="HOLD", reason="insufficient_rolling_data", basis=packet["config"]["controlBasis"], physiologicalEligible=False)
        def finish(action, reason):
            return dict(out, action=action, reason=reason)
        if packet["config"]["mode"] == "OFF":
            return finish("HOLD", "controller_off")
        if packet.get("stop_requested") or packet.get("discomfort"):
            return finish("REST", "participant_stop_or_discomfort")
        if packet["stage"] != "training":
            return finish("HOLD", "fixed_protocol_stage")
        if packet["config"].get("evaluationPolicy") != "rolling10-stride2-v1" or set(p) != set(POLICY):
            return finish("HOLD", "rolling_policy_mismatch")
        ref = baseline(packet.get("blocks", []))
        if ref is None:
            return finish("REST", "baseline_unstable_or_missing")
        rows = packet.get("evaluations", [])
        if not rows:
            return finish("HOLD", "insufficient_rolling_data")
        current = rows[-1]
        if (any(current.get(k) != packet[k] for k in ("evaluationId", "epoch", "lastTrial", "level", "block"))
                or current.get("stage") != "training" or current.get("eligible") != 10
                or current.get("matches", 0) + current.get("nonmatches", 0) != 10
                or not finite(current.get("end")) or not 0 <= packet["now"] - current["end"] < .5):
            return finish("HOLD", "stale_or_invalid_rolling_evaluation")
        down = "DOWN" if packet["level"] > 0 else "REST"
        # Omissions remain actionable when no correct response exists to estimate RT.
        if current.get("omissions", 0) >= p["rolling_severe_omissions"]:
            return finish(down, "severe_rolling_omissions")
        def valid(row):
            return (row.get("valid") is True and row.get("eligible") == 10
                    and row.get("matches", 0) >= p["rolling_min_class"] and row.get("nonmatches", 0) >= p["rolling_min_class"]
                    and finite(row.get("balancedAccuracy")) and 0 <= row["balancedAccuracy"] <= 1)
        if not valid(current):
            return finish("HOLD", "insufficient_response_class_coverage")
        if current["balancedAccuracy"] < p["severe_accuracy"]:
            return finish(down, "severe_rolling_difficulty")
        consecutive = (len(rows) >= 2 and valid(rows[-2])
                       and all(rows[-2].get(k) == current[k] for k in ("epoch", "level", "stage"))
                       and rows[-2]["evaluationId"] + 1 == current["evaluationId"]
                       and rows[-2]["lastTrial"] + 2 == current["lastTrial"])
        def difficult(row):
            return (row["balancedAccuracy"] < p["difficulty_accuracy"]
                    or row["omissions"] >= p["rolling_difficulty_omissions"]
                    or finite(row.get("medianCorrectRt")) and row["medianCorrectRt"] > ref["median_correct_rt"] * p["difficulty_rt_ratio"])
        if consecutive and all(difficult(r) for r in rows[-2:]):
            return finish(down, "two_difficult_rolling_evaluations")
        if out["basis"] == "physiological":
            eligible, adverse, reason = physiology_status(features, calibration or {}, packet, p)
            out["physiologicalEligible"] = eligible
            if not eligible or adverse:
                return finish("HOLD", reason)
        def passed(row):
            return (row["balancedAccuracy"] >= max(p["accuracy_floor"], ref["balanced_accuracy"] - p["baseline_margin"])
                    and finite(row.get("medianCorrectRt")) and 0 < row["medianCorrectRt"] < 4
                    and row["medianCorrectRt"] <= ref["median_correct_rt"] * p["rt_ratio"]
                    and row["omissions"] <= p["rolling_max_omissions"])
        if consecutive and all(passed(r) for r in rows[-2:]):
            return finish("UP", "two_valid_rolling_evaluations" if packet["level"] < 2 else "level_two_candidate_mastery")
        return finish("HOLD", "rolling_practice_opportunity")

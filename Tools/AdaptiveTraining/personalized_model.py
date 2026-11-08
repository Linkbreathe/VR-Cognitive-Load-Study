"""Per-session, per-participant forests with block-held-out validation and explicit admission."""
from __future__ import annotations

import hashlib
import json
import math
import time
from pathlib import Path

import joblib
import numpy as np
import sklearn
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import balanced_accuracy_score, roc_auc_score
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

from personalized_signals import FEATURES, REVISION as FEATURE_REVISION
from personalized_validation import block_validation, circular_permutation_test

VERSION = "2.0.0-stage2-dev11"
PROTOCOL = "personalized-rf-v2"
BEHAVIOR_REVISION = "behavior20-stride5-include-transition-v1"
DEFAULT_POLICY = {
    "revision": "mindtooth-eeg250-pilot-v1", "frozen": False, "device_qualified": False,
    "auc_minimum": None, "minimum_auc_gain": 0.0, "min_class_windows": 10,
    "min_class_blocks": 2, "n_estimators": 300, "max_depth": 3, "min_samples_leaf": 5,
    "theta_channels": [0, 1, 2, 3, 4], "alpha_channels": [5, 6, 7],
    "electrode_description": "Mindtooth AFz AF3 AF4 AF7 AF8 frontal theta; Pz P3 P4 parietal alpha",
    "eeg_units_confirmed": False, "iaf_qualified": False, "label_trials": 5,
    "label_alignment": "within_window5", "label_rt_weight": .1,
    "label_stable_min": .8, "label_struggling_max": .4,
    "permutation_count": 199, "permutation_alpha": .05,
    "permutation_method": "within_block_circular_labels",
    "permutation_exchangeability_qualified": False,
    "minimum_varying_blocks_per_condition": 2,
    "stable_ba": .7, "stable_omissions_below": 4, "struggling_ba": .4,
    "struggling_omissions_above": 5,
    "thresholds": {"0": {"stable_max": .45, "struggling_min": .65},
                   "1": {"stable_max": .45, "struggling_min": .65}},
    "threshold_method": "pilot_fixed_no_test_set_tuning", "max_clock_uncertainty": .05,
    "minimum_media_seconds": 10, "minimum_dwell_trials": 5,
    "enable_behavioral_fallback": True,
}


def resolve_policy_path(explicit=None, project_root=None):
    """Both bridge entry points reuse the same saved policy; explicit paths win."""
    if explicit is not None:
        return Path(explicit).resolve()
    root = Path(project_root) if project_root is not None else Path(__file__).resolve().parents[2]
    saved = root / "UserSettings/personalized-policy.json"
    return saved.resolve() if saved.is_file() else None


def policy_config(overrides=None):
    p = json.loads(json.dumps(DEFAULT_POLICY))
    if overrides:
        unknown = set(overrides) - set(p)
        if unknown:
            raise ValueError("Unknown personalized policy fields: " + str(sorted(unknown)))
        p.update(overrides)
    if (p["revision"] != DEFAULT_POLICY["revision"] or p["label_alignment"] != "within_window5" or p["label_trials"] != 5 or not 300 <= p["n_estimators"] <= 500 or
            not 2 <= p["max_depth"] <= 3 or p["min_samples_leaf"] < 5 or
            p["min_class_windows"] < 10 or p["min_class_blocks"] < 2 or
            p["minimum_dwell_trials"] < 5 or p["minimum_media_seconds"] < 0 or
            not 0 < p["max_clock_uncertainty"] <= .1 or
            not 0 <= p["struggling_ba"] < p["stable_ba"] <= 1 or
            type(p["stable_omissions_below"]) is not int or not 1 <= p["stable_omissions_below"] <= 20 or
            type(p["struggling_omissions_above"]) is not int or
            not p["stable_omissions_below"] <= p["struggling_omissions_above"] < 20 or
            not 0 <= p["label_rt_weight"] <= .2 or
            not -1 < p["label_struggling_max"] < p["label_stable_min"] <= 1 or
            not isinstance(p["permutation_count"], int) or p["permutation_count"] < 199 or
            not 0 < p["permutation_alpha"] <= .05 or
            p["permutation_method"] != "within_block_circular_labels" or
            p["minimum_varying_blocks_per_condition"] < 2):
        raise ValueError("Invalid personalized engineering policy")
    if p["auc_minimum"] is not None and not .5 < p["auc_minimum"] <= 1:
        raise ValueError("AUC gate must be above chance and at most one")
    if p["frozen"] and (p["auc_minimum"] is None or not p["device_qualified"] or
                        not p["eeg_units_confirmed"] or not p["permutation_exchangeability_qualified"] or
                        p["electrode_description"] == "unconfirmed"):
        raise ValueError("Freeze only after specifying the AUC gate, actual electrodes and device qualification")
    for level in ("0", "1"):
        t = p["thresholds"][level]
        if not 0 <= t["stable_max"] < t["struggling_min"] <= 1:
            raise ValueError("Invalid frozen score thresholds")
    return p


def behavioral_summary(trials, count=20):
    if len(trials) < count:
        return {"valid": False, "reason": "insufficient_behavior_trials"}
    rows = trials[-count:]
    # Level transitions remain observable behavior. Physiological windows and
    # calibration labels independently retain their transition exclusions.
    if any(not t["scored"] or t["interrupted"] or t["aborted"] for t in rows):
        return {"valid": False, "reason": "behavior_unscored_or_interrupted"}
    if len({t["trainingStage"] for t in rows}) != 1:
        return {"valid": False, "reason": "behavior_crosses_stage"}
    m = [t for t in rows if t["expectedMatch"]]
    n = [t for t in rows if not t["expectedMatch"]]
    if len(m) < 2 or len(n) < 2:
        return {"valid": False, "reason": "behavior_class_coverage"}
    rts = [t["reactionTime"] for t in rows if t["responded"] and t["correct"] and 0 <= t["reactionTime"] < 4]
    return {"valid": True, "ba": float(.5 * (np.mean([t["correct"] for t in m]) + np.mean([t["correct"] for t in n]))),
            "rt": float(np.median(rts)) if rts else None,
            "omissions": sum(not t["responded"] for t in rows),
            "count": count, "levels": sorted({t["level"] for t in rows}),
            "behaviorRevision": BEHAVIOR_REVISION,
            "transition_trials": sum(bool(t["transition"]) for t in rows),
            "start": rows[0]["plannedOnset"], "end": rows[-1]["trialEnd"]}


def window_behavior(trials, p, sliding=False):
    """Exactly the feature window's five trials; no class-coverage or later-behavior requirement."""
    rows = sorted(trials, key=lambda t: t["phaseTrial"])
    if len(rows) != 5 or any(not t["scored"] or (not sliding and t["transition"]) or t["interrupted"] or t["aborted"] for t in rows):
        return {"valid": False, "reason": "five_clean_scored_trials_required"}
    if (len({t["trainingStage"] for t in rows}) != 1 or (not sliding and len({(t["block"], t["level"]) for t in rows}) != 1) or
            [t["phaseTrial"] for t in rows] != list(range(rows[0]["phaseTrial"], rows[0]["phaseTrial"] + 5)) or
            abs(rows[-1]["trialEnd"] - rows[0]["plannedOnset"] - 22.5) > .1):
        return {"valid": False, "reason": "label_window_crosses_boundary_or_gap"}
    scores = []
    for t in rows:
        if t["correct"] and t["responded"]:
            rt = t["reactionTime"]
            if not math.isfinite(rt) or not 0 <= rt < 4:
                return {"valid": False, "reason": "invalid_correct_reaction_time"}
            scores.append(1 + p["label_rt_weight"] * (1 - 2 * rt / 4))
        else:
            scores.append(-1.)
    return {"valid": True, "score": float(np.mean(scores)), "trial_scores": scores,
            "trial_ids": [t["phaseTrial"] for t in rows], "count": 5,
            "correct": sum(t["correct"] and t["responded"] for t in rows),
            "omissions": sum(not t["responded"] for t in rows),
            "start": rows[0]["plannedOnset"], "end": rows[-1]["trialEnd"]}


def label_fit(rows, p):
    # Fixed engineering score units and buffer; no per-session quantiles or forced class balance.
    return {"stable_min": p["label_stable_min"], "struggling_max": p["label_struggling_max"],
            "correct_score": 1, "error_or_omission_score": -1, "rt_weight": p["label_rt_weight"],
            "formula": "correct: 1 + rt_weight*(1-2*rt/4); otherwise -1; mean of exactly five trials",
            "alignment": "within_window5", "fitted_on_data": False, "policy_revision": p["revision"]}


def label_rows(rows, definition, p):
    labels = []
    for r in rows:
        b = r["behavior"]
        score = b["score"]
        labels.append(1 if score <= definition["struggling_max"] else
                      0 if score >= definition["stable_min"] else -1)
    return np.array(labels)


def forest(p, seed):
    # Fit preprocessing within each training fold; no validation values enter the scaler.
    return make_pipeline(StandardScaler(), RandomForestClassifier(
        n_estimators=p["n_estimators"], max_depth=p["max_depth"], min_samples_leaf=p["min_samples_leaf"],
        max_features="sqrt", class_weight="balanced_subsample", random_state=seed, n_jobs=1))


def metrics(y, scores):
    if len(set(y)) < 2:
        return {"roc_auc": None, "balanced_accuracy": None, "n": len(y)}
    return {"roc_auc": float(roc_auc_score(y, scores)),
            "balanced_accuracy": float(balanced_accuracy_score(y, np.asarray(scores) >= .5)), "n": len(y)}


def train_personal(rows, participant, session, kind, seed, policy, folder):
    started = time.perf_counter(); p = policy_config(policy); folder = Path(folder)
    report = {"participant": participant, "session": session, "session_type": kind, "version": VERSION,
              "status": "personal_model_not_validated", "admitted": False, "reasons": [], "policy": p,
              "features": FEATURES, "featureRevision": FEATURE_REVISION,
              "sklearn_version": sklearn.__version__, "candidate_windows": len(rows),
              "validation": "leave_one_block_out; fixed five-trial score labels; within-block circular null",
              "score_semantics": "uncalibrated classifier score, not a state probability",
              "overlap": .8, "independent_sample_claim": False, "threshold_tuning": False}
    from layer_ab import SLIDING
    sliding = bool(rows) and all(r.get("windowRevision") == SLIDING for r in rows)
    if sliding:
        report["windowRevision"] = SLIDING
    behavior_rows = [r for r in rows if r.get("behavior", {}).get("valid")]
    definition = label_fit(behavior_rows, p)
    labeled = [dict(r, label=int(label)) for r, label in zip(behavior_rows, label_rows(behavior_rows, definition, p))]
    valid = [r for r in labeled if r["valid"]]
    report["eligible_windows"] = len(valid)
    model = None
    if any(len(row.get("features", [])) != len(FEATURES) for row in valid):
        raise ValueError("Incompatible feature vector for Mindtooth calibration")
    if valid:
        y = np.array([r["label"] for r in valid])
        report["label_definition"] = definition
        report["class_counts"] = {str(c): int(np.sum(y == c)) for c in (0, 1)}
        report["class_blocks"] = {str(c): sorted({r["block"] for r, label in zip(valid, y) if label == c}) for c in (0, 1)}
        if any(report["class_counts"][str(c)] < p["min_class_windows"] or
               len(report["class_blocks"][str(c)]) < p["min_class_blocks"] for c in (0, 1)):
            report["reasons"].append("insufficient_classes_or_block_distribution")
        else:
            report["varying_blocks_by_condition"] = {str(level): [b for b in sorted({r["block"] for r in valid if r["level"] == level})
                if {r["label"] for r in valid if r["block"] == b and r["label"] >= 0} == {0, 1}] for level in (0, 1)}
            if any(len(v) < p["minimum_varying_blocks_per_condition"] for v in report["varying_blocks_by_condition"].values()):
                report["reasons"].append("insufficient_within_block_label_variation")
            validation = block_validation(labeled, p, seed, forest, metrics)
            report.update(validation)
            if sliding:
                mixed = [o for o in validation["oof_predictions"] if o.get("mixed")]
                support = metrics([o["label"] for o in mixed], [o["combined"] for o in mixed])
                report["mixed_held_out"] = support
                report["supported_level_patterns"] = sorted({",".join(map(str,r["levels"])) for r in valid if r["label"]>=0})
                if support["roc_auc"] is None or p["auc_minimum"] is None or support["roc_auc"] < p["auc_minimum"]:
                    report["reasons"].append("mixed_window_held_out_auc_unqualified")
            if not validation["complete"]:
                report["reasons"].append("incomplete_held_out_block_coverage")
            auc = report["metrics"]["combined"]["roc_auc"]
            if auc is None or p["auc_minimum"] is None or auc < p["auc_minimum"]:
                report["reasons"].append("auc_gate_unset_or_failed")
            for level in ("0", "1"):
                value = report["within_condition"][level]["roc_auc"]
                if value is None or p["auc_minimum"] is None or value < p["auc_minimum"]:
                    report["reasons"].append("within_condition_auc_gate_unset_or_failed_" + level)
            for name in ("condition_only", "exposure_only", "time_only"):
                baseline_auc = report["metrics"][name]["roc_auc"]
                if auc is None or baseline_auc is None or auc <= baseline_auc + p["minimum_auc_gain"]:
                    report["reasons"].append("no_gain_over_" + name)
            report["permutation"] = circular_permutation_test(labeled, validation["permutation_statistic"], p, seed, forest, metrics)
            if not report["permutation"]["passed"]:
                report["reasons"].append("circular_permutation_gate_failed_or_unavailable")
            chosen = [r for r in valid if r["label"] >= 0]
            model = forest(p, seed)
            model.fit([r["features"] for r in chosen], y[y >= 0])
    else:
        report["reasons"].append("no_valid_labeled_windows")
    if not p["frozen"] or not p["device_qualified"] or not p["eeg_units_confirmed"]:
        report["reasons"].append("pilot_policy_or_device_not_frozen")
    if not p["permutation_exchangeability_qualified"]:
        report["reasons"].append("permutation_exchangeability_unqualified")
    if kind != "participant":
        report["reasons"].append("engineering_or_replay_session")
    if any(r.get("origin") != "live_sensor" for r in valid):
        report["reasons"].append("non_live_provenance")
    report["admitted"] = model is not None and not report["reasons"]
    report["status"] = "personal_model_validated" if report["admitted"] else "personal_model_not_validated"
    report["elapsed_seconds"] = time.perf_counter() - started
    report["policy_sha256"] = hashlib.sha256(json.dumps(p, sort_keys=True).encode()).hexdigest()
    folder.mkdir(parents=True, exist_ok=True)
    if model is not None:
        artifact = folder / ("personal_model.joblib" if report["admitted"] else "unvalidated_candidate.joblib")
        joblib.dump({"model": model, "report": report, "participant": participant, "session": session,
                     "features": FEATURES, "thresholds": p["thresholds"]}, artifact)
        report["model_sha256"] = hashlib.sha256(artifact.read_bytes()).hexdigest()
    (folder / "model_report.json").write_text(json.dumps(report, indent=2, allow_nan=False), encoding="utf-8")
    return model, report


def load_personal(path, participant, session, sha256, window_revision=None):
    """Load only trusted, locally produced artifacts; never deserialize network-supplied joblib files."""
    path = Path(path)
    if hashlib.sha256(path.read_bytes()).hexdigest() != sha256:
        raise ValueError("Model hash mismatch")
    saved = joblib.load(path)
    if (saved["participant"] != participant or saved["session"] != session or not saved["report"]["admitted"] or
            saved.get("features") != FEATURES or saved["report"].get("featureRevision") != FEATURE_REVISION or
            (window_revision is not None and saved["report"].get("windowRevision") != window_revision) or
            saved["report"]["version"] != VERSION or saved["report"]["policy"]["revision"] != DEFAULT_POLICY["revision"]):
        raise ValueError("Model identity or admission mismatch")
    return saved

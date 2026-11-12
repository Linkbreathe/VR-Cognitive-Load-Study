"""Exploratory, behavior-anchored thresholds from the independent fixed exposure stage."""
import math
import statistics
from layer_ab import COMPACT_ON_BA, COMPACT_OFF_BA, INDEPENDENT_REVISION, fixed_on_index

REVISION = "fixed-exposure-behavior-thresholds-v1"
REVISION_34 = "fixed-exposure-behavior-thresholds34-v2"


def policy_revision(config):
    return REVISION_34 if config.get("comparisonRevision") == INDEPENDENT_REVISION else REVISION


def summarize(config, trials, predictions, events):
    if config.get("comparisonStage") != "fixed_exposure":
        return None
    rows = {t["phaseTrial"]: t for t in trials if t.get("trainingStage") == "fixed_exposure"}
    groups = {"videoOff": [], "videoOn": []}
    anchors = {"stable": [], "struggling": []}
    excluded = []
    # Non-overlapping five-response groups; warmups and the mixed-state boundary are excluded.
    revised = config.get("comparisonRevision") == INDEPENDENT_REVISION
    total, on_index = (34, fixed_on_index(config)) if revised else (30, 15)
    endpoints = set(range(6, on_index, 5)) | set(range(on_index + 4, total, 5))
    seen = set()
    for p in predictions:
        index = p.get("trial")
        if p.get("stage") != "fixed_exposure" or index not in endpoints or index in seen:
            continue
        seen.add(index)
        window = [rows.get(i) for i in range(index-4, index+1)]
        score = p.get("loadScore", p.get("score"))
        reason = None
        if not p.get("valid") or not isinstance(score, (float, int)) or not math.isfinite(score) or not 0 <= score <= 1:
            reason = "invalid_or_missing_model_score"
        elif p.get("origin") != "live_sensor":
            reason = "non_live_signal"
        elif any(not r or not r.get("scored") or r.get("interrupted") or r.get("aborted") or r.get("transition") for r in window):
            reason = "ineligible_behavior_window"
        elif len({r["level"] for r in window}) != 1:
            reason = "mixed_condition_window"
        elif not isinstance(p.get("movie_fraction"), (int, float)) or (window[-1]["level"] == 1 and p["movie_fraction"] < .95) or (window[-1]["level"] == 0 and p["movie_fraction"] > 0):
            reason = "actual_playback_does_not_match_schedule"
        if reason:
            excluded.append(dict(lastTrial=index+1, reason=reason))
            continue
        classes = [[r for r in window if bool(r["expectedMatch"]) == match] for match in (True, False)]
        omissions = sum(not r.get("responded", False) for r in window)
        ba = statistics.mean(sum(bool(r["correct"]) for r in group)/len(group) for group in classes) if all(classes) else None
        label = "stable" if ba is not None and ba >= COMPACT_ON_BA and omissions < 2 else "struggling" if omissions >= 2 or ba is not None and ba <= COMPACT_OFF_BA else "unlabeled"
        item = dict(firstTrial=index-3, lastTrial=index+1, score=score, balancedAccuracy=ba, omissions=omissions,
                    label=label, scoreStart=p.get("loadWindowStart", p.get("start")), scoreEnd=p.get("loadWindowEnd", p.get("end")))
        groups["videoOn" if window[-1]["level"] else "videoOff"].append(item)
        if label in anchors:
            anchors[label].append(score)
    interrupted = any(e.get("type") in ("pause", "personal_mount_interrupted", "session_aborted", "continuous_media_failure") for e in events)
    completed = len(rows) == total and set(rows) == set(range(total))
    recommended = None
    reason = "insufficient_behavior_anchors"
    if not completed:
        reason = "stage_not_complete"
    elif interrupted:
        reason = "stage_interrupted"
    elif not groups["videoOff"] or not groups["videoOn"]:
        reason = "missing_valid_exposure_group"
    elif all(len(v) >= 2 for v in anchors.values()):
        on, off = statistics.median(anchors["stable"]), statistics.median(anchors["struggling"])
        if off-on >= .05:
            recommended = dict(on=on, off=off)
            reason = "exploratory_candidate_manual_review_required"
        else:
            reason = "behavior_scores_not_separated_by_005"
    distributions = {key: dict(count=len(values), median=statistics.median([v["score"] for v in values]) if values else None, windows=values) for key, values in groups.items()}
    return dict(revision=policy_revision(config), modelId=config.get("testModelId"), modelHash=config.get("testModelHash"),
                completed=completed, interrupted=interrupted, groups=distributions, anchors={k: len(v) for k,v in anchors.items()},
                recommended=recommended, reason=reason, excluded=excluded,
                method="Median score in stable / struggling non-overlapping five-response groups; at least two groups per label and OFF - ON >= 0.05.",
                limitation=f"Exploratory within-session estimate from at most {len(endpoints)} groups. Fixed OFF-then-ON order confounds time and exposure. Video presence is not a workload label. No automatic application or model admission.")

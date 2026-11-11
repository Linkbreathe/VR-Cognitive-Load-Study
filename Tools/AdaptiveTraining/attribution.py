"""Attribution claims, OFF source annotation and linked ON review (pure functions).

A claim names the decoded clip that may relate to a workload-score change and bounds when the change
happened. The Evidence Guard engine then judges the clip-signal relation (supported, unresolved or
disputed). Claims and annotations describe temporal association only; they never assert causation.
"""
import math

REVISION = "attribution-claim-v1"
BANDS_REVISION = "reverse-bands-session-cutoffs-v1"
ANNOTATION_REVISION = "off-annotation-on-review-v2"
MIN_CHANGE = .05          # Minimum score change for a reverse crossing.
FEATURE_SD = 1.0          # EEG feature change, in calibration within-condition SD units.
LOOKBACK_SECONDS = 10.0   # Workload-score response delay upper bound (matches the Guard RF delay).
LEGACY_BANDS = dict(stable_max=.45, struggling_min=.65, min_change=0., revision="fixed-0.45-0.65")


def finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def bands(on, off, threshold_revision=None):
    """Reverse-check bands follow the session cutoffs in effect: stable <= ON, struggling >= OFF."""
    return dict(stable_max=on, struggling_min=off, min_change=MIN_CHANGE, revision=BANDS_REVISION,
                thresholdRevision=threshold_revision)


def state(score, band):
    return "stable" if score <= band["stable_max"] else "struggling" if score >= band["struggling_min"] else "buffer"


def crossing(previous_score, score, band):
    """Return 'up', 'down' or None for a stable <-> struggling crossing of at least the minimum change."""
    if not (finite(previous_score) and finite(score)):
        return None
    pair = (state(previous_score, band), state(score, band))
    if abs(score - previous_score) < band.get("min_change", 0.):
        return None
    return {"stable", "struggling"} == set(pair) and ("up" if pair[1] == "struggling" else "down") or None


def candidates(start, end, clips, lookback=LOOKBACK_SECONDS):
    """Decoded clips overlapping [start - lookback, end], ordered by overlap with that interval."""
    found = []
    for clip in clips:
        clip_end = clip["end"] if finite(clip.get("end")) else end
        overlap = min(clip_end, end) - max(clip["start"], start - lookback)
        if overlap > 0 and clip["start"] <= end:
            found.append(dict(clip, overlapSeconds=overlap, playing=not finite(clip.get("end"))))
    return sorted(found, key=lambda c: (-c["overlapSeconds"], c["start"], c["id"]))


def feature_change(reference, previous, current, direction):
    """Projection of the feature change onto the model's arithmetic direction, in calibration SD units."""
    if not reference:
        return dict(available=False, reason="model_has_no_feature_reference")
    if not (previous and current) or len(previous) != len(current):
        return dict(available=False, reason="window_features_missing")
    terms = [d * (c - p) / s for p, c, d, s in zip(previous, current, reference["loadDirection"], reference["withinConditionSd"])
             if d and finite(s) and s > 0 and finite(p) and finite(c)]
    if not terms:
        return dict(available=False, reason="no_informative_features")
    index = sum(terms) / len(terms)
    sign = 1 if direction == "up" else -1
    return dict(available=True, index=index, threshold=FEATURE_SD, consistent=sign * index >= FEATURE_SD,
                features=len(terms), referenceRevision=reference.get("revision"))


def claim(signal, previous, band, clips, reference=None):
    """Claim for a reverse crossing. Returns None when the window does not cross the bands."""
    direction = crossing(previous["score"], signal["score"], band)
    if direction is None:
        return None
    found = candidates(signal["start"], signal["end"], clips)
    chosen = found[0]["id"] if found else None
    return dict(revision=REVISION, direction=direction, signal=signal["id"], score=signal["score"],
                previousSignal=previous["id"], previousScore=previous["score"], bands=band,
                onsetEarliest=previous["end"], onsetLatest=signal["end"],
                candidates=[dict(id=c["id"], start=c["start"], end=c.get("end"), playing=c["playing"],
                                 overlapSeconds=c["overlapSeconds"]) for c in found],
                claimedEvent=chosen, reason="candidate_clip" if chosen else "no_candidate_clip",
                featureChange=feature_change(reference, previous.get("features"), signal.get("features"), direction),
                causalClaim=False)


def behavior_source(failures, exposed, source):
    """Failures in the decision window: >50% during decoded playback, all before it, mixed, or none."""
    if not failures:
        return "none"
    if exposed > failures / 2:
        return "during"
    return "before" if source == "before_playback" else "mixed"


def score_source(before, peak, line):
    """Did the workload score rise while the video played?

    Compares the last score that ended before the current playback began with the highest score during
    it, against the session OFF cutoff. Available at the turn-off itself; it needs no clip-end relation.
    """
    if not finite(line) or not finite(peak):
        return "insufficient"
    if finite(before) and before >= line:
        return "before"
    if peak >= line:
        return "during" if finite(before) else "insufficient"
    return "none"


def combine(behavior, physiology):
    known = [v for v in (behavior, physiology) if v in ("during", "before", "mixed")]
    if not known:
        return "insufficient"
    if len(known) == 2 and known[0] != known[1]:
        return "mixed"
    return known[0]


def annotation(behavior, score, evidence=None):
    """Turn-off note: did the difficulty start while the video was playing? Both clues are always used."""
    return dict(revision=ANNOTATION_REVISION, behavior=behavior, score=score,
                result=combine(behavior, score), scoreEvidence=evidence or {})


def requires_verified_reopening(result):
    """Mixed evidence is treated like difficulty during playback (approved conservative choice)."""
    return result in ("during", "mixed")

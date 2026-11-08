"""Explicit engineering gaze substitution; never invent raw eye samples."""

REVISION = "comparison-environments-v1"
VR = "quest_pro"
DESKTOP = "desktop_fixed_gaze"
MODES = ("comparison", "comparison_candidate", "comparison_rehearsal")


def validate(config):
    mode = config.get("comparisonEnvironment", VR)
    if mode not in (VR, DESKTOP):
        raise ValueError("Choose Desktop (fixed gaze) or Quest Pro (tracked eyes).")
    if mode == DESKTOP and not (config.get("operatorManaged") and
            config.get("sessionType") == "engineering" and config.get("testSessionMode") in MODES):
        raise ValueError("Fixed gaze requires an operator-managed engineering comparison.")


def provenance(config):
    fixed = config.get("comparisonEnvironment", VR) == DESKTOP
    return dict(revision=REVISION, environment=DESKTOP if fixed else VR,
                source="simulated_fixed_gaze" if fixed else "quest_pro_tracked_eyes",
                simulated=fixed, validRatio=.75 if fixed else None, taskShare=.70 if fixed else None,
                taskShareDenominator="valid_eye_time", fallbackToFixed=False)


def gaze(config, core, intervals, level, scene):
    info = provenance(config)
    if not info["simulated"]:
        return dict(core.gaze(intervals, level, scene), **info)
    validate(config)
    merged = []
    for start, end in sorted(intervals):
        if end <= start:
            continue
        if merged and start <= merged[-1][1]:
            merged[-1][1] = max(end, merged[-1][1])
        else:
            merged.append([start, end])
    seconds = sum(b-a for a, b in merged)
    valid = seconds * .75
    return dict(info, available=seconds > 0, duration=seconds, covered_seconds=seconds,
                valid_seconds=valid, task_seconds=valid * .70, movie_seconds=0.,
                gaze_valid_ratio=.75 if seconds else None, task_gaze_ratio=.70 if seconds else None,
                movie_gaze_ratio=None, intervals=merged,
                note="Fixed aggregate assumptions, not measured eye samples. No movie gaze is inferred.")

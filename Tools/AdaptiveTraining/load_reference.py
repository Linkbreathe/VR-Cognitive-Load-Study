"""Personal two-condition reference. Engineering score in units of anchor separation."""
import math
import numpy as np
from personalized_signals import eeg_features

REVISION = "rest-math-120s-v1"
FEATURES = ["log_theta_relative", "log_alpha_relative"]


def measure(session, packet, start):
    signal = session.signals.get("EEG", {}).get("config", {})
    if not session.mount_intact:
        return None, "mount_interrupted"
    if session.kind == "participant" and (signal.get("data_origin") != "live_sensor" or signal.get("synthetic")):
        return None, "live_eeg_required"
    if not session.policy["eeg_units_confirmed"] or signal.get("units") not in ("uV", "µV"):
        return None, "eeg_units_unconfirmed"
    if not 0 <= packet.get("clockUncertainty", math.inf) <= session.policy["max_clock_uncertainty"]:
        return None, "clock_uncertainty"
    x, reason, rate = session.raw_window("EEG", start, start + 10, packet["clockOffset"])
    if x is None:
        return None, reason
    values, quality = eeg_features(x, rate, session.policy["theta_channels"], session.policy["alpha_channels"])
    return ([values[k] for k in FEATURES] if quality["valid"] else None), quality["reason"]


def fit(rows):
    groups = [np.array([v for v in rows.get(stage, []) if v is not None]) for stage in ("load_rest", "load_math")]
    if min(map(len, groups)) < 6:
        return None, "fewer_than_six_valid_windows_per_condition"
    rest, math_center = [np.median(g, axis=0) for g in groups]
    residuals = np.vstack([g-c for g, c in zip(groups, (rest, math_center))])
    scale = np.maximum(1.4826*np.median(abs(residuals), axis=0), .01)
    direction = (math_center-rest)/scale
    separation = float(np.linalg.norm(direction))
    if separation < 1:
        return None, "anchors_too_close"
    return dict(rest=rest.tolist(), math=math_center.tolist(), weights=(direction/scale/separation**2).tolist()), "ok"


def score(reference, values):
    return float(np.dot(np.subtract(values, reference["rest"]), reference["weights"]))

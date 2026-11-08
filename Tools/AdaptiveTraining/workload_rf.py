"""Within-person workload classifier for the rest/arithmetic engineering protocol."""
import math

import numpy as np
from scipy import signal
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import roc_auc_score

from personalized_signals import raw_integrity, sampled_window

REVISION = "rest-math-iaf-rf-v2"
LABELS = ["AFz", "AF3", "AF4", "AF7", "AF8", "Pz", "P3", "P4"]
FEATURES = [name + ("_theta_power" if i < 5 else "_alpha_power") for i, name in enumerate(LABELS)]
STAGES = ("load_rest", "load_math")


STAGE_SECONDS = 180
FOLD_SECONDS = STAGE_SECONDS // 3
WINDOW_POLICY = "rolling10-stride1-disjoint-fold60-v2"


def measure(session, packet, start, quality=None, *, screen_artifacts=True):
    config = session.signals.get("EEG", {}).get("config", {})
    if not session.mount_intact:
        return None, "mount_interrupted"
    if session.kind == "participant" and (config.get("data_origin") != "live_sensor" or config.get("synthetic")):
        return None, "live_eeg_required"
    if not 0 <= packet.get("clockUncertainty", math.inf) <= session.policy["max_clock_uncertainty"]:
        return None, "clock_uncertainty"
    return measure_eeg(config, session.policy,
                       lambda: session.raw_window("EEG", start, start + 10, packet["clockOffset"]), quality, screen_artifacts=screen_artifacts)


def measure_live_buffer(config, rows, policy, now):
    """Measure the receiver window shared by the live score and channel diagnostics."""
    if config.get("data_origin") != "live_sensor" or config.get("synthetic"):
        return None, {"reason": "live_eeg_required"}
    if not rows:
        return None, {"reason": "waiting_for_live_eeg"}
    last = rows[-1][0]
    if not 0 <= now - last <= 3:
        return None, {"reason": "live_eeg_stale"}
    rate = config["sample_rate"]
    if rate != 250:
        return None, {"reason": "mindtooth_sampling_required"}
    end = last + 1 / rate
    start = end - 10
    if rows[0][0] > start + 2 / rate:
        return None, dict(reason="collecting_ten_seconds", seconds=min(10, last - rows[0][0]))

    def read_window():
        times, samples = zip(*rows)
        values, reason = sampled_window(times, samples, start, end, rate)
        return values, reason, rate

    epochs, reason = measure_eeg(config, policy, read_window, screen_artifacts=False)
    return epochs, dict(reason=reason, end=end, artifactScreening=False)


def measure_eeg(config, policy, read_window, quality=None, *, screen_artifacts=True):
    """Apply the same montage, unit, epoch-quality and spectral checks on either clock."""
    if not policy["eeg_units_confirmed"] or config.get("units") not in ("uV", "碌V"):
        return None, "eeg_units_unconfirmed"
    if config.get("channel_labels") != LABELS or config.get("channels") != list(range(8)):
        return None, "mindtooth_montage_required"
    x, reason, rate = read_window()
    if x is None:
        return None, reason
    if rate != 250 or x.shape[1] != len(LABELS) or len(x) < 2500:
        return None, "mindtooth_sampling_required"
    x = x[:2500]
    filtered = signal.sosfiltfilt(signal.butter(5, [2, 30], fs=250, btype="bandpass", output="sos"), x, axis=0)
    if not screen_artifacts:
        epochs = filtered.reshape(10, 250, 8)
        _, psd = signal.periodogram(epochs, fs=rate, window="hann", axis=1)
        return psd[:, :16, :], "ok"
    spectra = []
    rejected = np.zeros(8, dtype=int)
    for raw, epoch in zip(x.reshape(10, 250, 8), filtered.reshape(10, 250, 8)):
        bad = raw_integrity(raw, rate) | (np.std(epoch, axis=0) < .01) | (np.abs(epoch).max(axis=0) > 80)
        if bad.any():
            rejected += bad
            continue
        # One-second Hann periodogram, 1 Hz bins. Keep only the bins used downstream.
        _, psd = signal.periodogram(epoch, fs=rate, window="hann", axis=0)
        spectra.append(psd[:16])
    if quality is not None:
        quality.update(cleanSeconds=len(spectra), flaggedChannels={label: int(count)
                       for label, count in zip(LABELS, rejected) if count})
    if len(spectra) < 5:
        return None, "insufficient_clean_epochs"
    return np.asarray(spectra), "ok"


def alpha_peak(rest):
    posterior = np.median(rest[:, :, 5:].mean(axis=2), axis=0)
    band = posterior[6:15]
    peaks, _ = signal.find_peaks(band, prominence=max(float(band.max()) * .15, 1e-10))
    return int(6 + peaks[np.argmax(band[peaks])]) if len(peaks) else None


def features(epochs, peak):
    theta = epochs[:, peak-6:peak-2, :5].sum(axis=1)
    alpha = epochs[:, peak-2:peak+2, 5:].sum(axis=1)
    return np.column_stack((theta, alpha))


def train(groups, seed):
    rest, arithmetic = (np.concatenate(group) for group in groups)
    peak = alpha_peak(rest)
    if peak is None:
        return None
    x = features(np.concatenate((rest, arithmetic)), peak)
    y = np.repeat([0, 1], [len(rest), len(arithmetic)])
    forest = RandomForestClassifier(n_estimators=100, max_depth=4, class_weight="balanced",
                                    random_state=seed, n_jobs=1)
    forest.fit(x, y)
    return {"iafHz": peak, "forest": forest}


def score(model, epochs):
    return float(model["forest"].predict_proba(features(epochs, model["iafHz"]))[:, 1].mean())


DEFAULT_THRESHOLDS = {"on": .45, "off": .65}


def threshold_profile(forest_scores, arithmetic_scores):
    """Choose inclusive cutoffs from out-of-fold windows on the deployment score scale."""
    rest, math_scores = np.asarray(forest_scores), np.asarray(arithmetic_scores)
    center = float(np.clip((np.median(rest) + np.median(math_scores)) / 2, .005, .995))
    grid = [i / 100 for i in range(101)]

    def choose(low):
        candidates = [t for t in grid if (t < center if low else t > center)]
        def rank(t):
            target = np.mean(rest <= t) if low else np.mean(math_scores >= t)
            opposite = np.mean(math_scores <= t) if low else np.mean(rest >= t)
            budget = .15 if low else .10
            return (opposite <= budget, -max(0, opposite-budget), target, -opposite, -abs(t-center))
        return max(candidates, key=rank)

    return dict(revision="held-out-workload-cutoffs-v1", forest=list(forest_scores),
                arithmetic=list(arithmetic_scores), recommended=dict(on=choose(True), off=choose(False)))


def fit(rows, seed, scoring_rows=None):
    groups = [[(i, v) for i, v in enumerate(rows.get(stage, [])) if v is not None] for stage in STAGES]
    diagnostics = {"validWindows": dict(zip(STAGES, map(len, groups))), "folds": []}
    if min(map(len, groups)) < 6:
        return None, "fewer_than_six_valid_windows_per_condition", diagnostics
    slots_per_fold = len(rows[STAGES[0]]) // 3
    held_scores = [[], []]
    for fold in range(3):
        training = [[v for i, v in group if i // slots_per_fold != fold] for group in groups]
        held = [[v for i, v in group if i // slots_per_fold == fold] for group in groups]
        if not all(held) or not all(training):
            return None, "insufficient_temporal_coverage", diagnostics
        model = train(training, seed)
        if model is None:
            return None, "rest_alpha_peak_unclear", diagnostics
        predicted = [score(model, window) for group in held for window in group]
        if scoring_rows is not None:
            for side, stage in enumerate(STAGES):
                held_scores[side].extend(score(model, scoring_rows[stage][i])
                                         for i, _ in groups[side] if i // slots_per_fold == fold)
        auc = float(roc_auc_score(np.repeat([0, 1], list(map(len, held))), predicted))
        diagnostics["folds"].append(dict(fold=fold, iafHz=model["iafHz"], heldWindows=list(map(len, held)), auc=auc))
    diagnostics["meanHeldAuc"] = float(np.mean([f["auc"] for f in diagnostics["folds"]]))
    if diagnostics["meanHeldAuc"] < .65:
        return None, "weak_held_out_discrimination", diagnostics
    model = train([[v for _, v in group] for group in groups], seed)
    if model is None:
        return None, "rest_alpha_peak_unclear", diagnostics
    if scoring_rows is not None:
        diagnostics["thresholdProfile"] = threshold_profile(*held_scores)
    diagnostics["iafHz"] = model["iafHz"]
    return model, "ok", diagnostics

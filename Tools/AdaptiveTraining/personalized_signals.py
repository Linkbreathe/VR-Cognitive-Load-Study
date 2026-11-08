"""Trial-aligned personalized signal features. Engineering thresholds require device validation."""
from __future__ import annotations

import numpy as np
from scipy import signal

REVISION = "mindtooth-eeg250-8ch-v1"
FEATURES = ["log_theta_relative", "log_alpha_relative", "movie_fraction"]


def sampled_window(times, samples, start, end, rate):
    times, samples = np.asarray(times, float), np.asarray(samples, float)
    if samples.ndim == 1:
        samples = samples[:, None]
    select = (times >= start) & (times < end)
    t, x = times[select], samples[select]
    expected = (end - start) * rate
    valid = (len(t) >= expected * .95 and len(t) > 2 and
             np.isfinite(x).all() and np.isfinite(t).all() and
             t[0] - start <= 2 / rate and end - t[-1] <= 2 / rate and
             np.all(np.diff(t) > 0) and np.max(np.diff(t)) <= 2.5 / rate)
    if not valid:
        return None, "missing_samples_or_timestamp_gap"
    # One sampling grid; retain the original timestamps separately in raw_signals.jsonl.
    grid = np.arange(start, end, 1 / rate)
    return np.stack([np.interp(grid, t, x[:, i]) for i in range(x.shape[1])], axis=1), "ok"


def eeg_features(samples, rate, theta_channels, alpha_channels, amplitude_uv=100, *, screen_artifacts=True):
    x = np.asarray(samples, float)
    if x.ndim == 1:
        x = x[:, None]
    if rate <= 100 or not np.isfinite(x).all():
        return {}, {"valid": False, "reason": "eeg_rate_or_nonfinite"}
    indices = sorted(set(theta_channels + alpha_channels))
    if not theta_channels or not alpha_channels or min(indices) < 0 or max(indices) >= x.shape[1]:
        return {}, {"valid": False, "reason": "electrode_mapping_missing"}
    if len(x) < round(2 * rate):
        return {}, {"valid": False, "reason": "eeg_insufficient_samples"}
    # Keep raw integrity checks separate from residual amplitude after interference removal.
    b, a = signal.iirnotch(50, 30, fs=rate)
    filtered = signal.filtfilt(b, a, x, axis=0)
    filtered = signal.sosfiltfilt(signal.butter(4, [1, 40], fs=rate, btype="bandpass", output="sos"), filtered, axis=0)
    spectra, total = [], 0
    channel_peaks, channel_stds, raw_peaks, integrity_bad = [], [], [], []
    size, stride = round(2 * rate), round(rate)
    if not screen_artifacts:
        frequencies, psd = signal.welch(filtered, fs=rate, nperseg=size, noverlap=size-stride, axis=0)
        return relative_powers(frequencies, psd, theta_channels, alpha_channels), {"valid": True, "artifactScreening": False}
    for left in range(0, len(x) - size + 1, stride):
        raw = x[left:left + size, indices]
        raw = raw - np.median(raw, axis=0)
        residual = filtered[left:left + size, indices]
        residual = residual - np.median(residual, axis=0)
        total += 1
        peaks = np.max(np.abs(residual), axis=0)
        stds = np.minimum(np.std(raw, axis=0), np.std(residual, axis=0))
        raw_peaks.append(np.max(np.abs(raw), axis=0))
        bad = raw_integrity(raw, rate)
        integrity_bad.append(bad)
        channel_peaks.append(peaks); channel_stds.append(stds)
        if np.any(peaks > amplitude_uv) or np.any(stds < .01) or np.any(bad):
            continue
        frequencies, psd = signal.welch(filtered[left:left + size], fs=rate, nperseg=size, axis=0)
        spectra.append(psd)
    fraction = len(spectra) / total if total else 0
    quality = {"valid": fraction >= .5, "valid_fraction": fraction, "subsegments": total,
               "accepted_subsegments": len(spectra), "units": "uV", "amplitude_reference": "filtered_subsegment_median",
               "revision": REVISION, "amplitude_limit_uv": amplitude_uv, "flatline_std_uv": .01,
               "channel_policy": "all_preselected_channels_must_pass; no_automatic_channel_drop",
               "reason": "ok" if fraction >= .5 else "eeg_artifact_fraction"}
    quality["channels"] = []
    if total:
        peaks, stds = np.asarray(channel_peaks), np.asarray(channel_stds)
        amplitude_bad, flat_bad = peaks > amplitude_uv, stds < .01
        raw_bad = np.asarray(integrity_bad)
        quality["raw_integrity_rejected_subsegments"] = int(np.any(raw_bad, axis=1).sum())
        quality["raw_integrity_policy"] = "repeat_100ms;extrema_plateau_20ms;jump_over_1000uV_per_sample;pilot_not_ADC_calibration"
        quality["amplitude_rejected_subsegments"] = int(np.any(amplitude_bad, axis=1).sum())
        quality["flatline_rejected_subsegments"] = int(np.any(flat_bad, axis=1).sum())
        for j, index in enumerate(indices):
            quality["channels"].append({"selected_column": index,
                "amplitude_rejections": int(amplitude_bad[:, j].sum()),
                "flatline_rejections": int(flat_bad[:, j].sum()),
                "raw_integrity_rejections": int(raw_bad[:, j].sum()),
                "raw_centered_peak_uv_median": float(np.median(np.asarray(raw_peaks)[:, j])),
                "accepted_subsegments": int((~(amplitude_bad[:, j] | flat_bad[:, j] | raw_bad[:, j])).sum()),
                "centered_peak_uv_median": float(np.median(peaks[:, j])),
                "centered_peak_uv_max": float(np.max(peaks[:, j])),
                "std_uv_median": float(np.median(stds[:, j]))})
    if not quality["valid"]:
        return {}, quality
    return relative_powers(frequencies, np.mean(spectra, axis=0), theta_channels, alpha_channels), quality


def relative_powers(frequencies, psd, theta_channels, alpha_channels):
    def power(lo, hi):
        mask = (frequencies >= lo) & (frequencies < hi)
        return np.sum(psd[mask], axis=0) * (frequencies[1] - frequencies[0])
    denominator = power(1, 30)
    theta = np.mean(power(4, 8)[theta_channels] / np.maximum(denominator[theta_channels], 1e-12))
    alpha = np.mean(power(8, 13)[alpha_channels] / np.maximum(denominator[alpha_channels], 1e-12))
    return {"log_theta_relative": float(np.log(max(theta, 1e-12))),
            "log_alpha_relative": float(np.log(max(alpha, 1e-12)))}


def raw_integrity(x, rate):
    """Pilot dropout/plateau/discontinuity screen, not knowledge of device ADC rails."""
    bad = []
    def longest(mask):
        edges = np.diff(np.r_[False, mask, False].astype(int))
        return max(np.flatnonzero(edges == -1) - np.flatnonzero(edges == 1), default=0)
    for column in np.asarray(x).T:
        delta = np.diff(column)
        flat = longest(np.abs(delta) <= 1e-12) + 1 >= round(.1 * rate)
        plateau = any(longest(column == extreme) >= round(.02 * rate)
                      for extreme in (column.min(), column.max()))
        bad.append(flat or plateau or np.any(np.abs(delta) > 1000))
    return np.asarray(bad, dtype=bool)


def movie_fraction(start, end, intervals):
    """Union actual playback intervals; duplicate/overlapping event rows cannot inflate exposure."""
    spans = sorted((max(start, a), min(end, b)) for a, b in intervals if b > start and a < end)
    merged = []
    for a, b in spans:
        if b <= a:
            continue
        if merged and a <= merged[-1][1]:
            merged[-1][1] = max(merged[-1][1], b)
        else:
            merged.append([a, b])
    return sum(b - a for a, b in merged) / (end - start)

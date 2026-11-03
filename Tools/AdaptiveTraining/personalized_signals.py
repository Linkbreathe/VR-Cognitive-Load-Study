"""Trial-aligned personalized signal features. Engineering thresholds require device validation."""
from __future__ import annotations

import numpy as np
from scipy import signal

REVISION = "trial-aligned-eeg-ecg-v2-channel-audit"
FEATURES = ["log_theta_relative", "log_alpha_relative", "mean_hr", "movie_fraction"]


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


def eeg_features(samples, rate, theta_channels, alpha_channels, amplitude_uv=100):
    x = np.asarray(samples, float)
    if x.ndim == 1:
        x = x[:, None]
    if rate <= 100 or not np.isfinite(x).all():
        return {}, {"valid": False, "reason": "eeg_rate_or_nonfinite"}
    indices = sorted(set(theta_channels + alpha_channels))
    if not theta_channels or not alpha_channels or min(indices) < 0 or max(indices) >= x.shape[1]:
        return {}, {"valid": False, "reason": "electrode_mapping_missing"}
    # Reject AC artifacts before filtering without mistaking amplifier DC bias for EEG amplitude.
    b, a = signal.iirnotch(50, 30, fs=rate)
    filtered = signal.filtfilt(b, a, x, axis=0)
    filtered = signal.sosfiltfilt(signal.butter(4, [1, 40], fs=rate, btype="bandpass", output="sos"), filtered, axis=0)
    spectra, total = [], 0
    channel_peaks, channel_stds = [], []
    size, stride = round(2 * rate), round(rate)
    for left in range(0, len(x) - size + 1, stride):
        raw = x[left:left + size, indices]
        raw = raw - np.median(raw, axis=0)
        total += 1
        peaks, stds = np.max(np.abs(raw), axis=0), np.std(raw, axis=0)
        channel_peaks.append(peaks); channel_stds.append(stds)
        if np.any(peaks > amplitude_uv) or np.any(stds < .01):
            continue
        frequencies, psd = signal.welch(filtered[left:left + size], fs=rate, nperseg=size, axis=0)
        spectra.append(psd)
    fraction = len(spectra) / total if total else 0
    quality = {"valid": fraction >= .5, "valid_fraction": fraction, "subsegments": total,
               "accepted_subsegments": len(spectra), "units": "uV", "amplitude_reference": "subsegment_median",
               "revision": REVISION, "amplitude_limit_uv": amplitude_uv, "flatline_std_uv": .01,
               "channel_policy": "all_preselected_channels_must_pass; no_automatic_channel_drop",
               "reason": "ok" if fraction >= .5 else "eeg_artifact_fraction"}
    quality["channels"] = []
    if total:
        peaks, stds = np.asarray(channel_peaks), np.asarray(channel_stds)
        amplitude_bad, flat_bad = peaks > amplitude_uv, stds < .01
        quality["amplitude_rejected_subsegments"] = int(np.any(amplitude_bad, axis=1).sum())
        quality["flatline_rejected_subsegments"] = int(np.any(flat_bad, axis=1).sum())
        for j, index in enumerate(indices):
            quality["channels"].append({"selected_column": index,
                "amplitude_rejections": int(amplitude_bad[:, j].sum()),
                "flatline_rejections": int(flat_bad[:, j].sum()),
                "accepted_subsegments": int((~(amplitude_bad[:, j] | flat_bad[:, j])).sum()),
                "centered_peak_uv_median": float(np.median(peaks[:, j])),
                "centered_peak_uv_max": float(np.max(peaks[:, j])),
                "std_uv_median": float(np.median(stds[:, j]))})
    if not quality["valid"]:
        return {}, quality
    psd = np.mean(spectra, axis=0)
    def power(lo, hi):
        mask = (frequencies >= lo) & (frequencies < hi)
        return np.sum(psd[mask], axis=0) * (frequencies[1] - frequencies[0])
    denominator = power(1, 30)
    theta = np.mean(power(4, 8)[theta_channels] / np.maximum(denominator[theta_channels], 1e-12))
    alpha = np.mean(power(8, 13)[alpha_channels] / np.maximum(denominator[alpha_channels], 1e-12))
    return {"log_theta_relative": float(np.log(max(theta, 1e-12))),
            "log_alpha_relative": float(np.log(max(alpha, 1e-12)))}, quality


def ecg_features(samples, rate):
    x = np.asarray(samples, float).reshape(-1)
    if rate < 100 or not np.isfinite(x).all() or np.std(x) < 1e-10:
        return {}, {"valid": False, "reason": "ecg_rate_or_flatline"}
    filtered = signal.sosfiltfilt(signal.butter(3, [5, 25], fs=rate, btype="bandpass", output="sos"), x)
    # Detect either polarity; this candidate detector must be checked against device R annotations.
    envelope = np.abs(filtered)
    peaks, _ = signal.find_peaks(envelope, distance=round(.3 * rate),
                                prominence=max(np.std(envelope) * 1.5, 1e-12))
    rr = np.diff(peaks) / rate
    if len(rr) < 8:
        return {}, {"valid": False, "reason": "insufficient_rr", "rr_count": len(rr)}
    median = np.median(rr)
    valid = (rr >= .3) & (rr <= 2) & (np.abs(rr - median) <= .2 * median)
    fraction = float(np.mean(valid))
    covered = peaks[0] / rate <= 2 and (len(x) - peaks[-1]) / rate <= 2
    quality = {"valid": bool(fraction >= .8 and covered), "valid_rr_fraction": fraction,
               "rr_count": len(rr), "reason": "ok" if fraction >= .8 and covered else "ecg_rr_quality"}
    if not quality["valid"]:
        return {}, quality
    # lnRMSSD is intentionally excluded until ultra-short-window reliability is established.
    return {"mean_hr": float(60 / np.mean(rr[valid]))}, quality


def resting_reference(eeg, ecg, rate_eeg, rate_ecg, policy):
    """Inputs cover only closed-eye seconds 30-60, or open-eye seconds 60-120."""
    result = {"iaf_hz": None, "iaf_available": False, "bands": {"theta": [4, 8], "alpha": [8, 13]},
              "iaf_reason": "not_qualified_or_unavailable", "used_for_labels": False}
    if eeg is not None:
        features, quality = eeg_features(eeg, rate_eeg, policy["theta_channels"], policy["alpha_channels"])
        result.update(eeg=features, eeg_quality=quality)
        if policy.get("iaf_qualified") and quality["valid"] and quality["valid_fraction"] == 1 and len(eeg) / rate_eeg >= 29:
            x = np.asarray(eeg)[:, policy["alpha_channels"]].mean(axis=1)
            f, p = signal.welch(x, fs=rate_eeg, nperseg=round(4 * rate_eeg))
            mask = (f >= 8) & (f <= 13)
            band = p[mask]; peak = int(np.argmax(band))
            if 0 < peak < len(band) - 1 and band[peak] >= 3 * np.median(band):
                result.update(iaf_hz=float(f[mask][peak]), iaf_available=True, iaf_reason="candidate_peak_requires_review")
    if ecg is not None:
        result["ecg"], result["ecg_quality"] = ecg_features(ecg, rate_ecg)
    return result


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

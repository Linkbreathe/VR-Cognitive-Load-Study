"""Exploratory online features, recomputable from saved timestamped samples."""
from __future__ import annotations

import numpy as np
from scipy.signal import butter, find_peaks, sosfiltfilt, welch


def extract_features(kind, timestamps, samples, sample_rate, window_seconds, amplitude_limit):
    times = np.asarray(timestamps, dtype=float)
    values = np.asarray(samples, dtype=float)
    if values.ndim == 1:
        values = values[:, None]
    if sample_rate <= 0 or len(times) < max(16, int(sample_rate * window_seconds * .8)):
        return [], {"quality": 0.0, "reason": "insufficient_samples"}
    finite = np.isfinite(values).all(axis=1) & np.isfinite(times)
    if not finite.all():
        return [], {"quality": 0.0, "reason": "nonfinite_samples"}
    dt = np.diff(times)
    if np.any(dt <= 0) or np.max(dt) > 3/sample_rate:
        return [], {"quality": 0.0, "reason": "timestamp_gap_or_order"}
    if times[-1]-times[0] < window_seconds-2/sample_rate:
        return [], {"quality": 0.0, "reason": "incomplete_window"}
    if np.any(np.std(values, axis=0) < 1e-12) or np.max(np.abs(values-np.median(values,axis=0))) > amplitude_limit:
        return [], {"quality": 0.0, "reason": "flatline_or_amplitude_limit"}
    quality = min(1., len(times)/(sample_rate*window_seconds))
    out = []
    if kind == "EEG":
        if sample_rate < 100:
            return [], {"quality": 0., "reason": "eeg_rate_below_100hz"}
        for channel in range(values.shape[1]):
            freq, power = welch(values[:, channel], fs=sample_rate, nperseg=min(len(times), int(sample_rate*2)), detrend="constant")
            def band(low, high):
                mask = (freq >= low) & (freq < high)
                return float(np.trapz(power[mask], freq[mask]))
            total = band(1, 40)
            if total <= 0:
                continue
            for name, lo, hi in (("theta",4,8),("alpha",8,13),("beta",13,30)):
                out.append({"name": f"eeg_ch{channel}_{name}_relative", "value": band(lo,hi)/total})
    elif kind == "ECG":
        if sample_rate < 100:
            return [], {"quality": 0., "reason": "ecg_rate_below_100hz"}
        x = values[:, 0]
        filtered = sosfiltfilt(butter(3,[5, min(25, sample_rate*.4)],fs=sample_rate,btype="bandpass",output="sos"), x)
        # Polarity-independent energy detector; device-specific R-peak validation is required.
        peaks, _ = find_peaks(np.abs(filtered), distance=int(sample_rate*.3), prominence=max(np.std(filtered),1e-12))
        rr = np.diff(times[peaks])
        if len(rr) < 10 or np.any((rr < .3) | (rr > 2.0)):
            return [], {"quality": 0., "reason": "unreliable_rr_detection"}
        out.append({"name":"ecg_heart_rate", "value":float(60/np.median(rr))})
        out.append({"name":"ecg_mean_ibi_ms", "value":float(np.mean(rr)*1000)})
        if window_seconds >= 60:
            out.append({"name":"ecg_rmssd_ms_exploratory", "value":float(np.sqrt(np.mean(np.diff(rr)**2))*1000)})
    else:
        raise ValueError("Expected EEG or ECG")
    return out, {"quality":quality, "reason":"engineering_quality_checks_passed"}


def condition_coverage(start, end, events):
    """Coverage is valid only when an explicit running interval covers the entire window."""
    ordered = sorted(events, key=lambda e:e["time"])
    before = [e for e in ordered if e["time"] <= start]
    if not before:
        return [], [], False
    covered = [before[-1]]+[e for e in ordered if start < e["time"] <= end]
    return (sorted({e["level"] for e in covered}), sorted({e["stage"] for e in covered}),
            all(e.get("running",False) for e in covered))

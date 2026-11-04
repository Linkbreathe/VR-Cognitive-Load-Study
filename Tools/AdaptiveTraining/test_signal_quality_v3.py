"""Independent synthetic signal fixtures for the pilot preprocessing revision."""
import unittest
import numpy as np
from personalized_signals import eeg_features, ecg_features, sampled_window


class FilteredQualityTests(unittest.TestCase):
    def setUp(self):
        self.rate = 500
        self.t = np.arange(0, 22.5, 1 / self.rate)
        self.clean = 20 * np.sin(2 * np.pi * 6 * self.t) + 10 * np.sin(2 * np.pi * 10 * self.t)

    def eeg(self, x):
        return eeg_features(np.asarray(x)[:, None], self.rate, [0], [0])

    def test_line_noise_is_removed_without_modifying_input_or_band_features(self):
        x = self.clean + 700 * np.sin(2 * np.pi * 50 * self.t)
        before = x.copy()
        base, _ = self.eeg(self.clean)
        features, q = self.eeg(x)
        np.testing.assert_array_equal(x, before)
        self.assertTrue(q['valid'])
        self.assertGreater(q['channels'][0]['raw_centered_peak_uv_median'], 600)
        for key in base: self.assertAlmostEqual(base[key], features[key], delta=.04)

    def test_in_band_artifact_still_fails(self):
        self.assertFalse(self.eeg(self.clean + 200 * np.sin(2 * np.pi * 3 * self.t))[1]['valid'])

    def test_raw_clipping_dropouts_and_large_impulses_are_not_hidden_by_filters(self):
        clipped = np.clip(200 * np.sin(2 * np.pi * 6 * self.t), -30, 30)
        dropout = self.clean.copy()
        impulses = self.clean.copy()
        for start in np.arange(0, 22, 1):
            i = round(start * self.rate)
            dropout[i:i + 100] = 0
            impulses[i + 200] = 3000
        for x in (clipped, dropout, impulses):
            q = self.eeg(x)[1]
            self.assertFalse(q['valid'])
            self.assertGreater(q['raw_integrity_rejected_subsegments'], 0)

    def test_flatline_wrong_units_nonfinite_and_timestamp_gaps_still_fail(self):
        for x in (np.zeros(len(self.t)), self.clean / 1e6, self.clean * 1e6,
                  np.full(len(self.t), np.nan)):
            self.assertFalse(self.eeg(x)[1]['valid'])
        times = self.t.copy(); times[500:] += .05
        self.assertIsNone(sampled_window(times, self.clean, 0, 22.5, self.rate)[0])

    def ecg_signal(self, beats):
        x = np.zeros_like(self.t)
        for beat in beats:
            x += 400 * np.exp(-((self.t - beat) / .015) ** 2)
            x -= 250 * np.exp(-((self.t - beat - .03) / .018) ** 2)
            x += 45 * np.exp(-((self.t - beat - .28) / .055) ** 2)
        return x + 10 * np.sin(2 * np.pi * .2 * self.t)

    def test_gradual_rate_change_polarity_and_count_against_known_beats(self):
        beats = [.7]
        while beats[-1] < 21.2:
            beats.append(beats[-1] + .72 + .45 * (1 + np.sin(beats[-1] * .25)) / 2)
        x = self.ecg_signal(beats)
        features, quality = ecg_features(x, self.rate)
        inverted, other = ecg_features(-x, self.rate)
        self.assertTrue(quality['valid']); self.assertTrue(other['valid'])
        self.assertEqual(quality['rr_count'], len(beats) - 1)
        self.assertAlmostEqual(features['mean_hr'], 60 / np.mean(np.diff(beats)), delta=.5)
        self.assertAlmostEqual(features['mean_hr'], inverted['mean_hr'], places=8)

    def test_isolated_missing_beat_is_flagged_and_poor_coverage_fails(self):
        beats = np.arange(.7, 22, .8)
        _, q = ecg_features(self.ecg_signal(np.delete(beats, 12)), self.rate)
        self.assertIn(False, q['rr_valid'])
        _, q = ecg_features(self.ecg_signal(beats[(beats > 4) & (beats < 18)]), self.rate)
        self.assertFalse(q['valid']); self.assertFalse(q['coverage_passed'])


if __name__ == '__main__':
    unittest.main()

"""Independent synthetic signal fixtures for the pilot preprocessing revision."""
import unittest
import numpy as np
from personalized_signals import eeg_features, sampled_window


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

if __name__ == '__main__':
    unittest.main()

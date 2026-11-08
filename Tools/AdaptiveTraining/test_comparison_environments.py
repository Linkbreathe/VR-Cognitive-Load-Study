"""Engineering-only mode boundaries and real vs. assumed gaze decision paths."""
import unittest
from unittest.mock import Mock

from comparison_environment import DESKTOP, VR, gaze, validate
from probe_control import ProbeReview
from test_compact_comparison import fixture


class ComparisonEnvironmentTests(unittest.TestCase):
    def setup_mode(self, mode, **kwargs):
        s,p,d,c=fixture(**kwargs)
        s.config.update(comparisonEnvironment=mode, operatorManaged=True, sessionType='engineering')
        return s,p,d,c

    def test_fixed_aggregate_has_correct_denominators_and_no_raw_samples(self):
        s,p,d,c=self.setup_mode(DESKTOP)
        c.gaze=Mock(side_effect=AssertionError('Desktop must not read real gaze'))
        v=gaze(s.config,c,[(0,4),(2,6)],0,'test')
        self.assertEqual(v['duration'],6)
        self.assertEqual(v['valid_seconds'],4.5)
        self.assertAlmostEqual(v['task_seconds'],3.15)
        self.assertEqual(v['gaze_valid_ratio'],.75)
        self.assertEqual(v['task_gaze_ratio'],.70)
        self.assertIsNone(v['movie_gaze_ratio'])
        self.assertTrue(v['simulated'])
        self.assertFalse(gaze(s.config,c,[],0,'test')['available'])

    def test_desktop_allows_on_with_good_model_and_behavior(self):
        s,p,d,c=self.setup_mode(DESKTOP,level=0,score=.45)
        c.gaze=Mock(side_effect=AssertionError('No raw eyes required'))
        r=ProbeReview().assess(p,s,d,c,'test')
        self.assertEqual(r['recommended'],'ON')
        self.assertEqual(r['gaze']['source'],'simulated_fixed_gaze')
        self.assertTrue(r['gazeInput']['simulated'])

    def test_fixed_gaze_does_not_override_model_failure(self):
        s,p,d,c=self.setup_mode(DESKTOP,level=0,score=.62)
        r=ProbeReview().assess(p,s,d,c,'test')
        self.assertEqual(r['recommended'],'HOLD')
        self.assertEqual(r['reason'],'current_condition_agreement_missing')
        self.assertNotIn('gaze',r)
        self.assertTrue(r['gazeInput']['simulated'])

    def test_vr_requires_real_eyes_and_never_substitutes(self):
        for value in (None,.3):
            s,p,d,c=self.setup_mode(VR,level=0,score=.45)
            c.gaze=lambda *args:dict(available=value is not None,gaze_valid_ratio=value,
                                    task_gaze_ratio=.7 if value else None,duration=20,valid_seconds=0)
            r=ProbeReview().assess(p,s,d,c,'test')
            self.assertEqual(r['recommended'],'HOLD')
            self.assertEqual(r['reason'],'eye_tracking_insufficient')
            self.assertEqual(r['gaze']['gaze_valid_ratio'],value)
            self.assertFalse(r['gazeInput']['simulated'])

    def test_model_only_off_uses_fixed_gaze_but_retains_playback_timing(self):
        s,p,d,c=self.setup_mode(DESKTOP)
        c.gaze=Mock(side_effect=AssertionError('No raw eyes required'))
        r=ProbeReview().assess(p,s,d,c,'test')
        self.assertEqual(d['action'],'OFF')
        self.assertEqual(r['recommended'],'HOLD')
        self.assertEqual(r['playbackGaze']['gaze_valid_ratio'],.75)
        s.video_intervals=[(80,80.5)]
        r=ProbeReview().assess(p,s,d,c,'test')
        self.assertEqual(r['recommended'],'OFF')
        self.assertEqual(r['reason'],'insufficient_pre_response_playback')

    def test_behavior_off_not_overridden_by_fixed_gaze(self):
        s,p,d,c=self.setup_mode(DESKTOP,omitted=(15,16))
        self.assertEqual(ProbeReview().assess(p,s,d,c,'test')['recommended'],'OFF')

    def test_fixed_mode_cannot_enter_participant_or_other_stage(self):
        s,*_=self.setup_mode(DESKTOP)
        validate(s.config)
        for change in (dict(sessionType='participant'),dict(operatorManaged=False),dict(testSessionMode='calibration'),dict(comparisonEnvironment='typo')):
            with self.assertRaises(ValueError):validate(dict(s.config,**change))
        validate({})  # Existing configurations remain on the real tracking path.


if __name__=='__main__':unittest.main()

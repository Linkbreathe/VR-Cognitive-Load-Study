"""Boundary and evidence checks for the approved five-response comparison policy."""
import unittest
from types import SimpleNamespace

from layer_ab import COMPACT_POLICY, PROBE, SLIDING, validate
from probe_control import base_decision, ProbeReview
from test_personalized import trials


def fixture(level=1, wrong=(), omitted=(), score=.65, missing_class=False):
    rows=trials(20,'layer_b')
    for i,r in enumerate(rows):
        r.update(level=level,scored=True,correct=i not in wrong and i not in omitted,
                 responded=i not in omitted,actualOnset=i*4.5,plannedOnset=i*4.5,
                 closeTime=i*4.5+4,responseTime=i*4.5+2 if i not in omitted else -1)
        if missing_class:r['expectedMatch']=False
    config=dict(studyDesign=PROBE,evaluationPolicy=COMPACT_POLICY,trainingTrials=60,
        rollingWindowTrials=5,rollingStrideTrials=2,slidingRevision=SLIDING,testSessionMode='comparison_candidate')
    predictions=[dict(stage='layer_b',trial=15,start=60,end=70,valid=True,score=.40,levels=[level]*5),
        dict(stage='layer_b',trial=18,start=75.5,end=85.5,valid=True,score=score,levels=[level]*5)]
    session=SimpleNamespace(config=config,policy={'max_clock_uncertainty':.1},barrier=0,report={'admitted':True},predictions=predictions,
        score_thresholds={'on':.49,'off':.57},workload_control=True,candidate_control=True,
        trials={('layer_b',i):r for i,r in enumerate(rows[:-1])},intervals=[(80,89.5)],playing={},
        video_intervals=[(80,89.5)],video_playing={})
    p=dict(stage='layer_b',completedTrials=20,requestId='fixture',now=89.5,level=level,
        clockUncertainty=.01,closedBehavior=rows[-1])
    d=base_decision(session,p,dict(level=level,requestId='fixture'),rows)
    core=SimpleNamespace(relations={},config=SimpleNamespace(min_gaze_valid=.7,min_task_gaze=.6))
    def gaze(intervals,level,scene):
        duration=sum(b-a for a,b in intervals)
        return dict(available=bool(duration),gaze_valid_ratio=1.,task_gaze_ratio=1.,movie_gaze_ratio=0.,
                    duration=duration,valid_seconds=duration,task_seconds=duration)
    core.gaze=gaze
    return session,p,d,core


class CompactComparisonTests(unittest.TestCase):
    def test_ba_class_counts_and_missing_class(self):
        _,_,d,_=fixture(wrong=(15,))
        self.assertEqual(d['behavior']['matches'],2)
        self.assertEqual(d['behavior']['nonmatches'],3)
        self.assertEqual(d['behavior']['ba'],.75)
        _,_,d,_=fixture(level=0,score=.52,missing_class=True)
        self.assertIsNone(d['behavior']['ba'])
        self.assertFalse(d['behaviorTrigger'])
        self.assertEqual(d['behavior']['reason'],'missing_response_class')

    def test_two_omissions_trigger_off_even_without_ba(self):
        _,_,d,_=fixture(omitted=(15,16),score=.52,missing_class=True)
        self.assertTrue(d['behaviorTrigger'])
        self.assertEqual(d['action'],'OFF')

    def test_strict_defer_requires_good_behavior(self):
        s,p,d,c=fixture()
        review=ProbeReview().assess(p,s,d,c,'fixture')
        self.assertEqual(d['action'],'OFF')
        self.assertEqual(review['recommended'],'HOLD')
        self.assertEqual(review['thresholds']['deferBA'],.8)
        s,p,d,c=fixture(wrong=(15,))
        review=ProbeReview().assess(p,s,d,c,'fixture')
        self.assertEqual(review['recommended'],'OFF')

    def test_eye_boundaries_and_loss(self):
        for coverage,task,expected in [(.7,.6,'HOLD'),(.69,.6,'OFF'),(.7,.59,'OFF')]:
            s,p,d,c=fixture();original=c.gaze
            c.gaze=lambda *args:dict(original(*args),gaze_valid_ratio=coverage,task_gaze_ratio=task)
            self.assertEqual(ProbeReview().assess(p,s,d,c,'fixture')['recommended'],expected)

    def test_verification_uses_ten_new_trials_and_off_cutoff(self):
        s,p,d,c=fixture(score=.52);s.video_intervals=[(45,89.5)]
        probe=ProbeReview();probe.probe_at=10;probe.since=45
        review=probe.assess(p,s,d,c,'fixture')
        self.assertEqual(review['reason'],'probe_passed')
        self.assertEqual(review['applicableBehavior']['count'],5)
        self.assertEqual(review['verificationBehavior']['count'],10)
        s,p,d,c=fixture(score=.57);s.video_intervals=[(45,89.5)]
        probe=ProbeReview();probe.probe_at=10;probe.since=45
        self.assertEqual(probe.assess(p,s,d,c,'fixture')['reason'],'probe_failed')

    def test_version_rejects_wrong_stride(self):
        s,*_=fixture();s.config['rollingStrideTrials']=5
        with self.assertRaisesRegex(ValueError,'window 5 and stride 2'):validate(s.config)


if __name__=='__main__':unittest.main()

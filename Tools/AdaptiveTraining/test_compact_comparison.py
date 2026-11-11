"""Boundary and evidence checks for the approved five-response comparison policy."""
import json
from pathlib import Path
import tempfile
import unittest
from types import SimpleNamespace

from layer_ab import COMPACT_POLICY, COMPACT_REVIEW, COMPACT_BEHAVIOR_REVISION, COMPACT_ON_BA, PROBE, SLIDING, validate
from probe_control import base_decision, ProbeReview, evidence_checks
from evidence_guard import GuardConfig
from test_personalized import trials


def fixture(level=1, wrong=(), omitted=(), score=.65, missing_class=False, count=20, stage='layer_b'):
    rows=trials(count,stage)
    for i,r in enumerate(rows):
        r.update(level=level,scored=True,correct=i not in wrong and i not in omitted,
                 responded=i not in omitted,actualOnset=i*4.5,plannedOnset=i*4.5,
                 closeTime=i*4.5+4,responseTime=i*4.5+2 if i not in omitted else -1)
        if missing_class:r['expectedMatch']=False
    config=dict(studyDesign=PROBE,evaluationPolicy=COMPACT_POLICY,trainingTrials=60,
        rollingWindowTrials=5,rollingStrideTrials=2,slidingRevision=SLIDING,testSessionMode='comparison_candidate')
    shift=(count-20)*4.5
    predictions=[dict(stage=stage,trial=count-5,start=60+shift,end=70+shift,valid=True,score=.40,levels=[level]*5),
        dict(stage=stage,trial=count-2,start=75.5+shift,end=85.5+shift,valid=True,score=score,levels=[level]*5)]
    session=SimpleNamespace(config=config,policy={'max_clock_uncertainty':.1},barrier=0,report={'admitted':True},predictions=predictions,
        score_thresholds={'on':.49,'off':.57},workload_control=True,candidate_control=True,
        trials={(stage,i):r for i,r in enumerate(rows[:-1])},intervals=[(80+shift,89.5+shift)],playing={},
        video_intervals=[(80+shift,89.5+shift)],video_playing={})
    p=dict(stage=stage,completedTrials=count,requestId='fixture',now=89.5+shift,level=level,
        clockUncertainty=.01,closedBehavior=rows[-1])
    d=base_decision(session,p,dict(level=level,requestId='fixture'),rows)
    core=SimpleNamespace(relations={},config=GuardConfig())
    def gaze(intervals,level,scene):
        duration=sum(b-a for a,b in intervals)
        return dict(available=bool(duration),gaze_valid_ratio=1.,task_gaze_ratio=1.,movie_gaze_ratio=0.,
                    duration=duration,valid_seconds=duration,task_seconds=duration)
    core.gaze=gaze
    return session,p,d,core


class CompactComparisonTests(unittest.TestCase):
    def test_on_gaze_uses_five_trials_and_ba_failure_does_not_mean_tracking_loss(self):
        s,p,d,c=fixture(level=0,score=.4)
        calls=[];original=c.gaze
        def capture(intervals,level,scene):
            calls.append(intervals)
            return original(intervals,level,scene)
        c.gaze=capture
        result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
        self.assertEqual(result['windowTrials'],list(range(15,20)))
        self.assertEqual(calls,[[(i*4.5,i*4.5+4) for i in range(15,20)]])
        self.assertEqual(result['recommended'],'ON')
        s,p,d,c=fixture(level=0,score=.4,wrong=(15,16))
        c.gaze=lambda *args: self.fail('BA failure must stop authorization before gaze')
        result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
        self.assertAlmostEqual(result['applicableBehavior']['ba'],7/12)
        self.assertEqual(result['reason'],'current_condition_agreement_missing')
        self.assertNotIn('gaze',result)

    def test_off_ba_half_boundary_and_recorded_thresholds(self):
        for stage in ('layer_a','layer_b'):
            for wrong,expected_ba,expected in [((15,18),.5,'OFF'),((15,16),7/12,'HOLD'),((15,16,17),5/12,'OFF')]:
                with self.subTest(stage=stage,wrong=wrong):
                    s,p,d,c=fixture(level=1,score=.52,wrong=wrong,stage=stage)
                    self.assertAlmostEqual(d['behavior']['ba'],expected_ba)
                    self.assertEqual(d['action'],expected)
                    self.assertEqual(d['behaviorThresholds']['offBA'],.5)
                    result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
                    self.assertEqual(result['recommended'],expected)
                    self.assertEqual(result['thresholds']['offBA'],.5)
                    self.assertEqual(result['thresholds']['onBA'],.65)
                    self.assertEqual(result['thresholds']['deferBA'],.8)
                    check=next(r for r in evidence_checks(d,result,c.config) if r['name']=='Base balanced accuracy')
                    self.assertEqual(check['threshold'],.5)
                    self.assertEqual(check['status'],'met' if expected=='OFF' else 'outside threshold')

    def test_lower_off_ba_does_not_block_model_or_omission_off(self):
        s,p,d,c=fixture(level=1,score=.65,wrong=(15,16))
        self.assertGreater(d['behavior']['ba'],.5)
        self.assertFalse(d['behaviorTrigger'])
        self.assertTrue(d['modelTrigger'])
        result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
        self.assertEqual((result['recommended'],result['reason']),('OFF','model_reduction'))
        s,p,d,c=fixture(level=1,score=.52,omitted=(16,17))
        self.assertGreater(d['behavior']['ba'],.5)
        self.assertTrue(d['behaviorTrigger'])
        self.assertEqual(ProbeReview(s.config).assess(p,s,d,c,'fixture')['recommended'],'OFF')

    def test_legacy_144_trial_off_threshold_stays_point_six(self):
        s,p,_,c=fixture(level=1,score=.52,wrong=(10,11,12,15))
        s.config.update(evaluationPolicy='behavior10-or-probe-v2',trainingTrials=144,rollingWindowTrials=10,rollingStrideTrials=5)
        rows=list(s.trials.values())+[p['closedBehavior']]
        d=base_decision(s,p,dict(level=1,requestId='fixture'),rows)
        self.assertGreater(d['behavior']['ba'],.5)
        self.assertLess(d['behavior']['ba'],.6)
        self.assertEqual(d['action'],'OFF')
        result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
        self.assertEqual(result['thresholds']['offBA'],.6)
        self.assertEqual(result['recommended'],'OFF')

    def test_relaxed_on_ba_applies_to_proposal_review_and_probe(self):
        import attribution
        for stage in ('layer_a','layer_b'):
            # Two wrong non-match answers leave BA = (1 + 1/3) / 2, above .65 but below .80.
            s,p,d,c=fixture(level=0,score=.4,wrong=(16,17),stage=stage)
            self.assertAlmostEqual(d['behavior']['ba'],2/3)
            self.assertTrue(d['behaviorTrigger'])
            self.assertEqual(d['behaviorRevision'],COMPACT_BEHAVIOR_REVISION)
            self.assertEqual(d['behaviorThresholds']['onBA'],.65)
            review=ProbeReview(s.config)
            result=review.assess(p,s,d,c,'fixture')
            self.assertEqual((d['action'],result['recommended']),('ON','ON'))
            self.assertEqual(result['thresholds']['onBA'],.65)
            checks=evidence_checks(d,result,c.config)
            ba_checks=[r for r in checks if r['name'] in ('Base balanced accuracy','Current-condition balanced accuracy')]
            self.assertEqual(len(ba_checks),2)
            self.assertTrue(all(r['threshold']==.65 and r['status']=='met' for r in ba_checks))
            # EEG remains independently required by B review, despite a valid behavioral proposal.
            s,p,d,c=fixture(level=0,score=.52,wrong=(16,17),stage=stage)
            result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
            self.assertEqual((d['action'],result['recommended']),('ON','HOLD'))
            if stage=='layer_b':
                s,p,d,c=fixture(level=0,score=.4,wrong=(16,17),stage=stage)
                review=ProbeReview(s.config);review.reset(stage)
                review.enact(60,1,0,dict(requestId='off',trial=12,offAnnotation=attribution.annotation('during','during')))
                result=review.assess(p,s,d,c,'fixture')
                self.assertEqual((result['recommended'],result['support']['authorization']),('ON','Probe_ON'))

    def test_on_ba_below_threshold_and_omissions_still_block_review(self):
        # Latest five: one matching trial, four nonmatching; three wrong nonmatches produce .625.
        s,p,_,c=fixture(level=0,score=.4)
        rows=list(s.trials.values())+[p['closedBehavior']]
        for row in rows[-5:]:
            row['expectedMatch']=row['phaseTrial']==15
            row['correct']=row['phaseTrial'] in (15,19)
        d=base_decision(s,p,dict(level=0,requestId='fixture'),rows)
        self.assertEqual(d['behavior']['ba'],.625)
        self.assertFalse(d['behaviorTrigger'])
        self.assertEqual(ProbeReview(s.config).assess(p,s,d,c,'fixture')['recommended'],'HOLD')
        s,p,d,c=fixture(level=0,score=.4,omitted=(16,17))
        self.assertAlmostEqual(d['behavior']['ba'],2/3)
        self.assertFalse(d['behaviorTrigger'])
        self.assertEqual(ProbeReview(s.config).assess(p,s,d,c,'fixture')['recommended'],'HOLD')

    def test_legacy_144_trial_on_threshold_is_still_point_eight(self):
        s,p,_,c=fixture(level=0,score=.4,wrong=(10,12))
        s.config.update(evaluationPolicy='behavior10-or-probe-v2',trainingTrials=144,rollingWindowTrials=10,rollingStrideTrials=5)
        rows=list(s.trials.values())+[p['closedBehavior']]
        d=base_decision(s,p,dict(level=0,requestId='fixture'),rows)
        self.assertGreater(d['behavior']['ba'],.65)
        self.assertLess(d['behavior']['ba'],.8)
        self.assertFalse(d['behaviorTrigger'])
        result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
        self.assertEqual(result['thresholds']['onBA'],.8)
        self.assertEqual(result['recommended'],'HOLD')

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
        self.assertEqual(d['behavior']['state'],'undetermined')
        review=ProbeReview().assess(p,s,d,c,'fixture')
        self.assertEqual(review['recommended'],'OFF')

    def test_eye_boundaries_and_loss(self):
        for coverage,task,expected in [(.6,.4,'HOLD'),(.599,.4,'OFF'),(.6,.399,'OFF')]:
            s,p,d,c=fixture();original=c.gaze
            c.gaze=lambda *args:dict(original(*args),gaze_valid_ratio=coverage,task_gaze_ratio=task)
            self.assertEqual(ProbeReview().assess(p,s,d,c,'fixture')['recommended'],expected)

    def test_on_eye_boundaries_and_missing_tracking(self):
        for coverage,task,available,expected in [(.6,.4,True,'ON'),(.599,.4,True,'HOLD'),
                                                (.6,.399,True,'HOLD'),(.6,.4,False,'HOLD')]:
            with self.subTest(coverage=coverage,task=task,available=available):
                s,p,d,c=fixture(level=0,score=.4);original=c.gaze
                c.gaze=lambda *args:dict(original(*args),gaze_valid_ratio=coverage,
                                        task_gaze_ratio=task,available=available)
                result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
                self.assertEqual(result['recommended'],expected)
                self.assertEqual(result['thresholds']['onEyeCoverage'],.6)
                self.assertEqual(result['thresholds']['onTaskShare'],.4)
                self.assertEqual(result['gazeReviewRevision'],'gaze-review-coverage60-task40-v1')

    def test_two_errors_across_six_trials_do_not_override_the_five_trial_rule(self):
        s,p,d,c=fixture(score=.52,wrong=(14,19));s.video_intervals=[(63,89.5)]
        review=ProbeReview(s.config)
        review.enact(63,0,1,dict(trial=14))
        result=review.assess(p,s,d,c,'fixture')
        self.assertAlmostEqual(result['applicableBehavior']['ba'],5/6)
        self.assertEqual((d['action'],result['recommended']),('HOLD','HOLD'))
        self.assertIsNone(review.probe_at)
        self.assertNotIn('verification',result)
        self.assertNotIn('probeStart',result)
        self.assertEqual(result['reviewPolicy'],COMPACT_REVIEW)
        self.assertFalse(any(k.startswith('verification') for k in result['thresholds']))

    def test_ordinary_model_off_remains_after_an_opening(self):
        s,p,d,c=fixture(score=.57);s.video_intervals=[(63,89.5)]
        review=ProbeReview(s.config);review.enact(63,0,1,dict(trial=14))
        result=review.assess(p,s,d,c,'fixture')
        self.assertEqual((result['recommended'],result['reason']),('OFF','model_reduction'))
        self.assertNotIn('verification',result)

    def test_missing_score_is_not_a_new_post_opening_timeout(self):
        s,p,d,c=fixture(score=.52);s.predictions=[];d.update(score=None,action='HOLD',modelTrigger=False)
        review=ProbeReview(s.config);review.enact(63,0,1,dict(trial=14))
        result=review.assess(p,s,d,c,'fixture')
        self.assertEqual((result['recommended'],result['reason']),('HOLD','base_hold'))

    def test_round_a_off_notes_remain_history_only(self):
        import attribution
        for source in ('before','during','mixed','insufficient'):
            with self.subTest(source=source):
                s,p,d,c=fixture(level=0,score=.40,stage='layer_a')
                note=attribution.annotation('none',source)
                review=ProbeReview(s.config);review.reset('layer_a')
                review.enact(60,1,0,dict(requestId='off',trial=12,offAnnotation=note))
                result=review.assess(p,s,d,c,'fixture')
                self.assertEqual((result['recommended'],result['reason']),('ON','current_condition_supported'))
                self.assertEqual(result['linkedOff']['annotation'],note)
                self.assertEqual(result['linkedOff']['rule'],'annotation_only')
                self.assertNotIn('support',result)
                self.assertNotIn('verifiedReopening',result['linkedOff'])
                review.enact(90,0,1,result)
                self.assertIsNone(review.probe_at)
                self.assertIsNone(review.support_probe)

    def test_round_b_playback_difficulty_invalidates_support_and_requires_probe_on(self):
        import attribution
        for behavior,score,invalidated in [('none','before',False),('none','insufficient',False),('none','none',False),
                                           ('during','insufficient',True),('none','during',True),('during','before',True)]:
            with self.subTest(behavior=behavior,score=score):
                s,p,d,c=fixture(level=0,score=.40)
                note=attribution.annotation(behavior,score)
                review=ProbeReview(s.config);review.reset('layer_b')
                review.enact(60,1,0,dict(requestId='off',trial=12,offAnnotation=note))
                self.assertEqual(review.support_invalidated,invalidated)
                result=review.assess(p,s,d,c,'fixture')
                self.assertEqual(result['linkedOff']['rule'],'selective_support_probe')
                self.assertEqual(result['linkedOff']['supportInvalidated'],invalidated)
                expected=('ON','support_probe_on','Probe_ON') if invalidated else ('ON','current_condition_supported','Normal_ON')
                self.assertEqual((result['recommended'],result['reason'],result['support']['authorization']),expected)
                review.enact(90,0,1,result)
                self.assertEqual(review.on_kind,expected[2])
                self.assertEqual(review.support_probe is not None,invalidated)

    def test_old_state_evidence_still_cannot_authorize_on(self):
        s,p,d,c=fixture(level=0,score=.40)
        review=ProbeReview(s.config);review.enact(80,1,0,dict(requestId='off',trial=18))
        result=review.assess(p,s,d,c,'fixture')
        self.assertEqual(result['applicableBehavior']['count'],2)
        self.assertEqual((result['recommended'],result['reason']),('HOLD','current_condition_agreement_missing'))

    def test_late_openings_are_reviewed_normally(self):
        for n in (54,56,58):
            with self.subTest(trial=n):
                s,p,d,c=fixture(level=0,score=.40,count=n)
                result=ProbeReview(s.config).assess(p,s,d,c,'fixture')
                self.assertEqual((result['recommended'],result['reason']),('ON','current_condition_supported'))
                s,p,d,c=fixture(level=0,score=.52,count=n)
                self.assertEqual(ProbeReview(s.config).assess(p,s,d,c,'fixture')['recommended'],'HOLD')

    def test_probe_on_is_last_requested_at_54(self):
        for n,expected in ((54,('ON','support_probe_on')),(56,('HOLD','support_probe_window_closed')),
                           (58,('HOLD','support_probe_window_closed'))):
            with self.subTest(trial=n):
                s,p,d,c=fixture(level=0,score=.40,count=n)
                review=ProbeReview(s.config);review.reset('layer_b');review.support_invalidated=True
                result=review.assess(p,s,d,c,'fixture')
                self.assertEqual((result['recommended'],result['reason']),expected)
                self.assertEqual(result['thresholds']['lastProbeOnTrial'],54)

    def probe(self, start_trial, **kw):
        """Round B review inside a Probe ON that started at the onset of phase trial start_trial."""
        s,p,d,c=fixture(**kw)
        review=ProbeReview(s.config);review.reset('layer_b');review.support_invalidated=True
        review.enact(start_trial*4.5,0,1,dict(requestId='probe',trial=start_trial,support=dict(authorization='Probe_ON')))
        return review,review.assess(p,s,d,c,'fixture')

    def test_probe_passes_after_four_trials_and_restores_authorization(self):
        review,result=self.probe(16,score=.52)
        self.assertEqual(result['support']['probeEvaluation']['completed'],4)
        self.assertEqual((result['recommended'],result['reason'],result['ordinaryReason']),('HOLD','support_probe_passed','base_hold'))
        self.assertEqual(result['support']['audit'],'Authorization Restored via Logged Probe')
        self.assertEqual((review.support_invalidated,review.required_off_trials,review.on_kind,review.support_probe),
                         (False,5,'Restored_ON',None))
        self.assertEqual([e['event'] for e in review.support_events],['Probe_ON','Probe_Passed'])
        self.assertEqual({x['name']:x['status'] for x in result['checks']}['Probe trials completed (video on)'],'met')

    def test_probe_running_after_two_trials_keeps_ordinary_review(self):
        review,result=self.probe(18,score=.52)
        self.assertEqual((result['recommended'],result['reason']),('HOLD','base_hold'))
        self.assertEqual(result['support']['probeEvaluation']['completed'],2)
        self.assertNotIn('outcome',result['support'])
        self.assertIsNotNone(review.support_probe)

    def test_two_probe_errors_fail_at_the_first_check(self):
        review,result=self.probe(18,score=.52,wrong=(18,),omitted=(19,))
        self.assertEqual((result['recommended'],result['reason'],result['support']['outcome']),
                         ('OFF','support_probe_failed_behavior','Probe_Failed'))
        self.assertEqual(result['support']['probeEvaluation']['errors'],2)
        self.assertIn(result['offAnnotation']['behavior'],('during','mixed','before'))
        review.enact(90,1,0,result)
        self.assertEqual((review.support_invalidated,review.required_off_trials,review.probe_failures),(True,7,1))

    def test_one_probe_error_does_not_fail(self):
        _,result=self.probe(16,score=.52,wrong=(17,))
        self.assertEqual(result['reason'],'support_probe_passed')

    def test_current_model_off_during_probe_fails_it(self):
        _,result=self.probe(16,score=.65)
        self.assertEqual((result['recommended'],result['reason'],result['ordinaryReason']),
                         ('OFF','support_probe_failed_model','model_reduction'))

    def test_playback_failure_or_no_decoded_playback_is_inconclusive_without_cooldown(self):
        for required in (5,7):
            for case in ('playback','no_decoded_playback'):
                with self.subTest(required=required,case=case):
                    s,p,d,c=fixture(score=.52)
                    if case=='no_decoded_playback':s.video_intervals=[]
                    review=ProbeReview(s.config);review.reset('layer_b')
                    review.support_invalidated=True;review.required_off_trials=required;review.probe_failures=required>5
                    review.enact(72,0,1,dict(requestId='probe',trial=16,support=dict(authorization='Probe_ON')))
                    if case=='playback':review.failed_media=True
                    result=review.assess(p,s,d,c,'fixture')
                    self.assertEqual((result['recommended'],result['reason'],result['support']['outcome']),
                                     ('OFF','support_probe_inconclusive_'+case,'Probe_Inconclusive'))
                    self.assertEqual(result['support']['nextRequiredOffTrials'],required)
                    self.assertIn('offAnnotation',result)
                    review.enact(90,1,0,result)
                    self.assertEqual((review.support_invalidated,review.required_off_trials,review.probe_failures,review.probe_inconclusive),
                                     (True,required,int(required>5),1))
                    self.assertEqual(review.support_events[-1]['event'],'Probe_Inconclusive')

    def test_participant_failures_still_add_cooldown(self):
        for kw,reason in ((dict(score=.52,wrong=(18,),omitted=(19,),start=18),'support_probe_failed_behavior'),
                          (dict(score=.65,start=16),'support_probe_failed_model')):
            with self.subTest(reason=reason):
                start=kw.pop('start')
                review,result=self.probe(start,**kw)
                self.assertEqual((result['reason'],result['support']['outcome'],result['support']['nextRequiredOffTrials']),
                                 (reason,'Probe_Failed',7))
                review.enact(90,1,0,result)
                self.assertEqual((review.required_off_trials,review.probe_failures,review.probe_inconclusive),(7,1,0))

    def test_failed_probes_add_capped_cooldown_and_never_lock(self):
        review=ProbeReview(fixture()[0].config);review.reset('layer_b');review.support_invalidated=True
        for failures,required in ((1,7),(2,7),(3,7)):
            review.enact(10*failures,0,1,dict(requestId=f'p{failures}',trial=failures,support=dict(authorization='Probe_ON')))
            review.enact(10*failures+5,1,0,dict(requestId=f'f{failures}',trial=failures,reason='support_probe_failed_behavior'))
            self.assertEqual((review.probe_failures,review.required_off_trials,review.support_invalidated),(failures,required,True))
        self.assertFalse(review.locked)
        for since,expected in ((63,('HOLD','support_cooldown_trials_missing')),(54,('ON','support_probe_on'))):
            with self.subTest(currentTrials=(20*4.5-since)/4.5):
                s,p,d,c=fixture(level=0,score=.40)
                review.since=since
                result=review.assess(p,s,d,c,'fixture')
                self.assertEqual((result['recommended'],result['reason']),expected)
                checks={x['name']:x for x in result['checks']}
                self.assertEqual(checks['Current-condition eligible trials']['threshold'],7)
                self.assertEqual(result['applicableBehavior']['count'],5)

    def test_normal_on_close_without_playback_difficulty_keeps_direct_reopening(self):
        import attribution
        review=ProbeReview(fixture()[0].config);review.reset('layer_b')
        review.enact(10,0,1,dict(requestId='on',trial=2,support=dict(authorization='Normal_ON')))
        review.enact(40,1,0,dict(requestId='off',trial=8,offAnnotation=attribution.annotation('before','none')))
        self.assertEqual((review.support_invalidated,review.required_off_trials,review.probe_failures),(False,5,0))

    def test_version_rejects_wrong_stride(self):
        s,*_=fixture();s.config['rollingStrideTrials']=5
        with self.assertRaisesRegex(ValueError,'window 5 and stride 2'):validate(s.config)

    def run_round(self, stage, folder, wrong=(), interrupt=()):
        """Real base/review calls with simulated responses, model scores and execution receipts."""
        from evidence_guard_adapter import GuardAdapter
        from personalized_runtime import PersonalSession, append
        from test_layer_ab import ab_packet

        p=ab_packet(stage)
        p['sessionType']='editor-test'
        p['config'].update(studyDesign=PROBE,trainingTrials=60,slidingRevision=SLIDING,
            evaluationPolicy=COMPACT_POLICY,rollingWindowTrials=5,rollingStrideTrials=2,
            testSessionMode='comparison_rehearsal',operatorManaged=True,sessionType='engineering',
            comparisonEnvironment='desktop_fixed_gaze')
        session=PersonalSession(p,folder,None,object())
        session.report={'admitted':True}  # Synthetic model eligibility; no participant acquisition.
        adapter=GuardAdapter(p,folder)
        rows=[];events=[];changes=[];level=0;media=None;decisions=[];reviews=[]
        for i,raw in enumerate(trials(60,stage)):
            n=i+1;start=i*4.5;close=start+4
            row=dict(raw,level=level,closeTime=close,offsetTime=close,responseTime=start+1,correct=n not in wrong)
            if n in interrupt and media:
                # Unity reports a playback interruption during this trial.
                events.append(dict(type='adaptive_media_interrupted',actual=start+1,detail=media))
            for event in events:
                session.event(event)
                append(folder/'events.jsonl',event)
            if n>=6:
                window=[r for r in rows if r['trialEnd']>start-10]
                session.predictions.append(dict(stage=stage,trial=n-2,start=start-10,end=start,
                    level=window[-1]['level'],levels=[r['level'] for r in window],valid=True,
                    score=.70 if n in (16,28,40,52) else .35,features=[]))
            p.update(trials=rows,events=events,guardGaze=[],level=level,now=close,
                closedBehavior=dict(row,trialEnd=0),completedTrials=n,lastTrial=n,
                requestId=f'{stage}-{n}',evaluationId=n//2,decisionDeadline=start+4.5)
            check=n%2==0 and n<60
            base=session.decide(p) if check else None
            output=adapter.receive(p,session,base)
            self.assertIsNone(adapter.failure)
            events=[]
            if check:
                self.assertEqual(adapter.receive(p,session,base),output,'Retries must remain idempotent')
                append(folder/'personal_decisions.jsonl',dict(packet=p,decision=base))
                append(folder/'comparison_deliveries.jsonl',dict(decision=output))
                decisions.append(output)
                review=adapter.probe_reviews[p['requestId']]
                reviews.append((n,review))
                self.assertEqual(review['reviewPolicy'],COMPACT_REVIEW)
                self.assertFalse(review['reason'].startswith(('probe_','failed_probe_')))
                self.assertFalse(any(k in review for k in ('probeStart','verification','verificationRevision')))
                if stage=='layer_a':
                    self.assertEqual(output['action'],base['action'])
                    self.assertNotIn('support',review)
                if output['action'] in ('ON','OFF'):
                    previous=level;level=int(output['action']=='ON');when=start+4.5
                    changes.append((n,output['action'],review.get('support',{}).get('authorization') or review['reason']))
                    events=[dict(type='personal_execution',actual=when,
                        detail=f'request={p["requestId"]};level={level};previous_level={previous}'),
                        dict(type='adaptive_level_changed',actual=when,detail=f'level={level}')]
                    if level:
                        media=json.dumps(dict(stage=stage,modality='audiovisual',level=1,
                            block=0,onset=when,mediaId=f'synthetic-{n}'))
                        events.append(dict(type='adaptive_media_started',actual=when,
                            detail=media+';first_decoded_frame;audio_playing=true'))
                    elif media:
                        events.append(dict(type='adaptive_media_ended',actual=when,detail=media))
                        media=None
            rows.append(row);session.trials[(stage,i)]=row
        self.assertEqual(len(decisions),29)
        self.assertEqual(session.decide(p)['reason'],'not_a_five_trial_decision_boundary')
        self.assertFalse(adapter.probe.locked)
        self.assertIsNone(adapter.probe.probe_at)
        return adapter,changes,dict(reviews)

    def check_export(self, folder):
        from comparison_report import report
        metadata=json.loads((folder/'personal_protocol.json').read_text(encoding='utf-8'))
        self.assertEqual(metadata['comparisonReview'],COMPACT_REVIEW)
        self.assertEqual(metadata['behaviorRevision'],COMPACT_BEHAVIOR_REVISION)
        self.assertEqual(metadata['comparisonBehaviorThresholds'],dict(onBA=.65,offBA=.50,deferBA=.80,omissions=2))
        exported=report(folder)
        self.assertEqual(exported['reviewRevision'],COMPACT_REVIEW)
        self.assertEqual(exported['decisionStride'],2)
        self.assertEqual(len(exported['checkpoints']),29)
        self.assertFalse(any('verificationRequest' in c for c in exported['checkpoints']))
        return exported

    def test_full_rounds_probe_after_playback_difficulty_and_hold_late_probes(self):
        for stage in ('layer_a','layer_b'):
            with self.subTest(stage=stage), tempfile.TemporaryDirectory() as tmp:
                adapter,changes,reviews=self.run_round(stage,Path(tmp))
                if stage=='layer_a':
                    # Round A executes the base proposal and records no support state.
                    self.assertEqual([c[:2] for c in changes],[(6,'ON'),(16,'OFF'),(18,'ON'),(28,'OFF'),
                        (30,'ON'),(40,'OFF'),(42,'ON'),(52,'OFF'),(54,'ON')])
                    self.assertEqual(adapter.probe.support_events,[])
                else:
                    # Every model OFF follows a score rise during playback, so each reopening is a Probe ON.
                    self.assertEqual(changes,[(8,'ON','Normal_ON'),(16,'OFF','model_reduction'),(22,'ON','Probe_ON'),
                        (28,'OFF','model_reduction'),(34,'ON','Probe_ON'),(40,'OFF','model_reduction'),
                        (46,'ON','Probe_ON'),(52,'OFF','model_reduction')])
                    for n in (26,38,50):
                        self.assertEqual(reviews[n]['reason'],'support_probe_passed')
                        self.assertEqual(reviews[n]['support']['afterReview']['state'],'Restored_ON')
                    self.assertEqual((reviews[58]['recommended'],reviews[58]['reason']),('HOLD','support_probe_window_closed'))
                    self.assertEqual([e['event'] for e in adapter.probe.support_events],
                        ['Normal_ON','Support_Invalidated','Probe_ON','Probe_Passed']*1+
                        ['Support_Invalidated','Probe_ON','Probe_Passed']*2+['Support_Invalidated'])
                audit=[json.loads(x) for x in (Path(tmp)/'evidence_guard_audit.jsonl').read_text(encoding='utf-8').splitlines()]
                logged=[r['event'] for r in audit if r.get('record')=='support_event']
                self.assertEqual(logged,[e['event'] for e in adapter.probe.support_events])
                if stage=='layer_b':
                    self.assertTrue(any(r.get('audit')=='Authorization Restored via Logged Probe' for r in audit))
                exported=self.check_export(Path(tmp))
                if stage=='layer_b':
                    self.assertEqual(exported['summary']['layer_b']['support'],
                                     dict(normalOn=1,probeOn=3,probePassed=3,probeFailed=0,probeInconclusive=0,probeWindowClosed=1))

    def test_full_round_b_failed_probes_cool_down_without_a_lock(self):
        with tempfile.TemporaryDirectory() as tmp:
            # Two mistakes in each of the first two probes (23-24 and 33-34).
            adapter,changes,reviews=self.run_round('layer_b',Path(tmp),wrong=(23,24,33,34))
            self.assertEqual(reviews[24]['reason'],'support_probe_failed_behavior')
            self.assertEqual(reviews[30]['reason'],'support_cooldown_trials_missing')
            self.assertEqual(reviews[30]['thresholds']['requiredOffTrials'],7)
            self.assertEqual(reviews[34]['reason'],'support_probe_failed_behavior')
            self.assertEqual(reviews[34]['support']['requiredOffTrials'],7)
            self.assertEqual(reviews[46]['reason'],'support_probe_passed')
            self.assertEqual(reviews[46]['support']['afterReview']['requiredOffTrials'],5)
            self.assertEqual(changes,[(8,'ON','Normal_ON'),(16,'OFF','model_reduction'),(22,'ON','Probe_ON'),
                (24,'OFF','support_probe_failed_behavior'),(32,'ON','Probe_ON'),(34,'OFF','support_probe_failed_behavior'),
                (42,'ON','Probe_ON'),(52,'OFF','model_reduction')])
            self.assertEqual(adapter.probe.probe_failures,2)
            exported=self.check_export(Path(tmp))
            self.assertEqual(exported['summary']['layer_b']['support'],
                             dict(normalOn=1,probeOn=3,probePassed=1,probeFailed=2,probeInconclusive=0,probeWindowClosed=1))


    def test_full_round_b_interrupted_probe_is_inconclusive_without_cooldown(self):
        with tempfile.TemporaryDirectory() as tmp:
            adapter,changes,reviews=self.run_round('layer_b',Path(tmp),interrupt=(24,))
            self.assertEqual((reviews[24]['reason'],reviews[24]['support']['outcome']),
                             ('support_probe_inconclusive_playback','Probe_Inconclusive'))
            self.assertEqual(reviews[24]['support']['nextRequiredOffTrials'],5)
            self.assertEqual(changes[:4],[(8,'ON','Normal_ON'),(16,'OFF','model_reduction'),(22,'ON','Probe_ON'),
                                          (24,'OFF','support_probe_inconclusive_playback')])
            self.assertEqual(changes[4][1:],('ON','Probe_ON'))
            self.assertEqual(adapter.probe.probe_failures,0)
            self.assertEqual(adapter.probe.probe_inconclusive,1)
            self.assertNotIn('support_cooldown_trials_missing',[r['reason'] for r in reviews.values()])
            events=[e['event'] for e in adapter.probe.support_events]
            self.assertEqual(events[:5],['Normal_ON','Support_Invalidated','Probe_ON','Probe_Inconclusive','Probe_ON'])
            self.assertEqual(self.check_export(Path(tmp))['summary']['layer_b']['support']['probeInconclusive'],1)
            # The 5-trial requirement is unchanged: reopening at 30, not 32 as after a failed probe.
            self.assertEqual(changes[4:],[(30,'ON','Probe_ON'),(40,'OFF','model_reduction'),(46,'ON','Probe_ON'),(52,'OFF','model_reduction')])


if __name__=='__main__':unittest.main()

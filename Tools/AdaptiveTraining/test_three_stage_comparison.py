"""Synthetic regression evidence only; no participant data or sensor acceptance."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from layer_ab import INDEPENDENT_REVISION, LEGACY_INDEPENDENT_REVISION, validate, trial_budget, fixed_on_index
from test_session_store import TestSessionStore
from test_personalized import trials
from test_compact_comparison import fixture
from probe_control import ProbeReview
from fixed_exposure_thresholds import summarize
from personalized_runtime import PersonalSession
from service import StudyService
from test_personalized import packet
from comparison_report import report


class ThreeStageTests(unittest.TestCase):
    def test_fixed_service_ingestion_and_report_include_all_thirty_four_trials(self):
        with tempfile.TemporaryDirectory() as tmp:
            service=StudyService(Path(tmp)/"service");store=service.test_store
            record=store.prepare(dict(participantCode="SYNTHETIC",storageFolder=str(Path(tmp)/"data"),sessionMode="comparison_rehearsal",comparisonStage="fixed_exposure"))
            claim=store.claim(dict(clientId="synthetic",controllerUrl="http://127.0.0.1:8773"))
            p=packet();p.update(sessionId=record["sessionId"],participantCode="SYNTHETIC",sessionType="editor-test",config=claim["config"],stage="preparation",testClientId="synthetic",now=0)
            service.receive(p)
            rows=trials(34,"fixed_exposure")
            for r in rows:r.update(level=int(r["phaseTrial"]>=18),closeTime=r["plannedOnset"]+4,offsetTime=r["plannedOnset"]+4,responseTime=r["plannedOnset"]+1)
            p.update(stage="fixed_exposure",trials=rows,completedTrials=34,now=154,level=1)
            service.receive(p)
            result=report(Path(record["folder"])/"service")
            self.assertEqual(len(result["trials"]),34)
            self.assertEqual(result["comparisonRevision"],INDEPENDENT_REVISION)
            self.assertEqual(result["fixedExposureThresholds"]["revision"],"fixed-exposure-behavior-thresholds34-v2")
            self.assertEqual(result["comparisonStage"],"fixed_exposure")
            self.assertTrue(result["fixedExposureThresholds"]["completed"])
            self.assertIsNone(result["fixedExposureThresholds"]["recommended"])
            self.assertEqual(result["checkpoints"],[])

    def test_three_assignments_are_frozen_independent_and_legacy_is_preserved(self):
        with tempfile.TemporaryDirectory() as tmp:
            store=TestSessionStore(Path(tmp)/"state")
            for stage in ("fixed_exposure","layer_a","layer_b",""):
                r=store.prepare(dict(participantCode="SYNTHETIC",storageFolder=str(Path(tmp)/"data"),sessionMode="comparison_rehearsal",comparisonStage=stage))
                c=r["config"];validate(c)
                self.assertEqual(c["trainingTrials"],34 if stage else 60)
                self.assertEqual(c["comparisonStage"],stage)
                p=dict(sessionId=r["sessionId"],participantCode="SYNTHETIC",sessionType="engineering",config=c,configVersion=c["version"])
                personal=PersonalSession(p,Path(r["folder"])/"service",store.policy,store)
                if stage=="fixed_exposure":
                    with self.assertRaisesRegex(ValueError,"Fixed exposure"):
                        personal.decide({})
                store.complete(r["sessionId"],"complete")
            self.assertEqual(len({r["sessionId"] for r in store.records.values()}),4)

    def test_invalid_stage_and_length_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store=TestSessionStore(Path(tmp)/"state")
            request=dict(participantCode="SYNTHETIC",storageFolder=str(Path(tmp)/"data"),sessionMode="comparison_rehearsal",comparisonStage="typo")
            with self.assertRaises(ValueError):store.prepare(request)
            request["comparisonStage"]="layer_b";r=store.prepare(request)
            for field,value in (("trainingTrials",60),("comparisonRevision","old"),("comparisonStage","unknown")):
                c=dict(r["config"]);c[field]=value
                with self.assertRaises(ValueError):validate(c)

    def test_probe_last_on_28_and_normal_on_32(self):
        for count in (28,30,32):
            s,p,d,core=fixture(level=0,score=.4,count=count)
            s.config.update(trainingTrials=34,comparisonStage="layer_b",comparisonRevision=INDEPENDENT_REVISION)
            review=ProbeReview(s.config);review.support_invalidated=True
            result=review.assess(p,s,d,core,"synthetic")
            self.assertEqual(result["reason"],"support_probe_on" if count==28 else "support_probe_window_closed")
            review=ProbeReview(s.config)
            self.assertEqual(review.assess(p,s,d,core,"synthetic")["recommended"],"ON")

    def test_legacy_assignment_keeps_its_length_and_switch(self):
        with tempfile.TemporaryDirectory() as tmp:
            store=TestSessionStore(Path(tmp)/"state")
            r=store.prepare(dict(participantCode="SYNTHETIC",storageFolder=str(Path(tmp)/"data"),sessionMode="comparison_rehearsal",comparisonStage="fixed_exposure"))
            c=dict(r["config"])
            self.assertEqual((trial_budget(c),fixed_on_index(c)),(34,18))
            c.update(comparisonRevision=LEGACY_INDEPENDENT_REVISION,trainingTrials=30)
            validate(c)
            self.assertEqual((trial_budget(c),fixed_on_index(c)),(30,15))
            for length in (32,34):
                with self.assertRaises(ValueError):validate(dict(c,trainingTrials=length))

    def test_new_fixed_groups_stay_within_scored_conditions(self):
        rows=trials(34,"fixed_exposure")
        for r in rows:r.update(level=int(r["phaseTrial"]>=18),correct=r["phaseTrial"]<18)
        preds=[dict(stage="fixed_exposure",trial=i,valid=True,origin="live_sensor",score=.3 if i<18 else .7,movie_fraction=0 if i<18 else 1) for i in range(34)]
        c=dict(comparisonStage="fixed_exposure",comparisonRevision=INDEPENDENT_REVISION,trainingTrials=34)
        result=summarize(c,rows,preds,[])
        self.assertEqual(result["recommended"],dict(on=.3,off=.7))
        self.assertEqual(result["anchors"],dict(stable=3,struggling=3))
        self.assertEqual([(w["firstTrial"],w["lastTrial"]) for w in result["groups"]["videoOff"]["windows"]],[(3,7),(8,12),(13,17)])
        self.assertEqual([(w["firstTrial"],w["lastTrial"]) for w in result["groups"]["videoOn"]["windows"]],[(19,23),(24,28),(29,33)])
        self.assertEqual(sum(r["scored"] and r["level"]==0 for r in rows),16)
        self.assertEqual(sum(r["scored"] and r["level"]==1 for r in rows),16)
        self.assertFalse(summarize(c,rows[:-1],preds,[])["completed"])


class FixedThresholdTests(unittest.TestCase):
    def setUp(self):
        self.config=dict(comparisonStage="fixed_exposure",testModelId="synthetic-model",testModelHash="f"*64)
        self.rows=trials(30,"fixed_exposure")
        for r in self.rows:
            r["level"]=int(r["phaseTrial"]>=15)
            r["correct"]=r["phaseTrial"]<15
        self.predictions=[dict(stage="fixed_exposure",trial=i,valid=True,origin="live_sensor",score=.3 if i<15 else .7,movie_fraction=0 if i<15 else 1) for i in (6,11,19,24,29)]
    def result(self,events=()):return summarize(self.config,self.rows,self.predictions,events)
    def test_complete_behavior_anchored_candidate(self):
        result=self.result();self.assertEqual(result["recommended"],dict(on=.3,off=.7));self.assertEqual(result["anchors"],dict(stable=2,struggling=3))
    def test_all_correct_does_not_label_video_as_struggling(self):
        for r in self.rows:r["correct"]=True
        self.assertIsNone(self.result()["recommended"])
        self.assertEqual(self.result()["anchors"],dict(stable=5,struggling=0))
    def test_missing_samples_rehearsal_and_playback_fail_closed(self):
        for mutation in (dict(score=None),dict(origin="synthetic"),dict(valid=False),dict(movie_fraction=.5)):
            rows=copy.deepcopy(self.predictions)
            for p in self.predictions:p.update(mutation)
            self.assertIsNone(self.result()["recommended"]);self.predictions=rows
    def test_pause_incomplete_and_inverted_scores_have_no_candidate(self):
        self.assertIsNone(self.result([dict(type="pause")])["recommended"])
        last=self.rows.pop();self.assertIsNone(self.result()["recommended"]);self.rows.append(last)
        for p in self.predictions:p["score"]=1-p["score"]
        self.assertIsNone(self.result()["recommended"])
    def test_mixed_warmup_and_duplicate_windows_do_not_inflate_counts(self):
        self.predictions+=copy.deepcopy(self.predictions)+[dict(stage="fixed_exposure",trial=15,valid=True,score=0)]
        self.assertEqual(self.result()["anchors"],dict(stable=2,struggling=3))
        self.rows[2]["scored"]=False
        self.assertIsNone(self.result()["recommended"])


if __name__=="__main__":unittest.main()

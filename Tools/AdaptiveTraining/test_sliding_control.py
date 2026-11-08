"""Engineering regressions for the versioned response-close sliding control."""
import copy
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from layer_ab import SLIDING
from personalized_model import window_behavior, forest, metrics, policy_config, train_personal
from personalized_validation import block_validation
from personalized_runtime import PersonalSession
from test_layer_ab import ab_packet
from test_personalized import trials, training_rows
from test_evidence_guard import gaze
from evidence_guard_adapter import GuardAdapter

class SlidingTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.p=ab_packet();self.p["config"]["slidingRevision"]=SLIDING
        self.s=PersonalSession(self.p,Path(self.tmp.name),None)
        rows=trials(25,"layer_a")
        for t in rows:t.update(closeTime=t["trialEnd"]-.5)
        self.rows=rows
        self.s.trials={("layer_a",i):r for i,r in enumerate(rows[:-1])}
        self.p.update(trials=rows[:-1],closedBehavior=dict(rows[-1],trialEnd=0),
                      now=112,decisionDeadline=112.5,completedTrials=25,lastTrial=25)
        self.r=dict(trial=23,stage="layer_a",level=0,levels=[1,0,0,0,0],start=85.5,end=108,
                    score=.2,valid=True,windowRevision=SLIDING,features=[])
        self.s.predictions.append(self.r)
        self.s.report={"admitted":True,"windowRevision":SLIDING,"supported_level_patterns":["1,0,0,0,0"]}
    def test_mixed_evidence_survives_level_change_not_pause(self):
        self.s.event(dict(type="adaptive_level_changed",actual=90,detail="level=0"))
        self.assertEqual(self.s.decide(self.p)["action"],"ON")
        self.s.event(dict(type="pause",actual=109,detail=""))
        self.assertFalse(self.s.predictions)
        self.assertEqual(self.s.decide(self.p)["action"],"HOLD")
    def test_old_model_bad_pattern_missing_latest_and_deadline(self):
        for change in ({"admitted":True},{"windowRevision":SLIDING,"admitted":True,"supported_level_patterns":[]}):
            saved=self.s.report;self.s.report=change
            self.assertEqual(self.s.decide(self.p)["action"],"HOLD");self.s.report=saved
        self.r["trial"]=22;self.assertEqual(self.s.decide(self.p)["action"],"HOLD")
        self.r["trial"]=23;self.r["valid"]=False;self.assertEqual(self.s.decide(self.p)["action"],"HOLD")
        self.r["valid"]=True;self.p["now"]=112.5
        self.assertEqual(self.s.decide(self.p)["reason"],"missing_closed_response_or_deadline")
    def test_clock_and_window_end_must_match_decision(self):
        self.p["clockUncertainty"]=1
        self.assertEqual(self.s.decide(self.p)["reason"],"sliding_clock_unqualified")
        self.p["clockUncertainty"]=.01;self.r["end"]-=4.5
        self.assertEqual(self.s.decide(self.p)["reason"],"sliding_window_unavailable_or_unsupported")
    def test_l1_requires_actual_exposure(self):
        self.p["level"]=self.p["closedBehavior"]["level"]=1
        self.r["score"]=.9
        for t in self.rows:t.update(correct=False,responded=False)
        self.assertEqual(self.s.decide(self.p)["reason"],"insufficient_actual_movie_exposure")
        self.s.intervals=[(95,106)]
        self.assertEqual(self.s.decide(self.p)["action"],"OFF")
    def test_guard_audits_exact_signal_and_separate_closed_response(self):
        self.p["guardGaze"]=gaze(self.rows)
        d=self.s.decide(self.p);a=GuardAdapter(self.p,Path(self.tmp.name))
        out=a.receive(self.p,self.s,d)
        self.assertIsNone(a.failure);self.assertEqual(out["guardVerdict"],"ALLOW")
        self.assertEqual(a.guard.judgments["model_support:"+d["requestId"]].evidence_ids,("signal:layer_a:23",))
        self.assertIn("closed_response:"+d["requestId"],a.guard.judgments["challenge:"+d["requestId"]].evidence_ids)
        self.assertNotIn(("layer_a",24),self.s.trials)
    def test_mixed_calibration_window_retains_timing_and_quality(self):
        w=trials(16)[10:15]
        self.s.intervals=[(45,51),(48,55)]
        self.assertTrue(window_behavior(w,self.s.policy,sliding=True)["valid"])
        self.assertFalse(window_behavior(w,self.s.policy)["valid"])
        r=self.s.extract(w,self.p)
        self.assertEqual(r["source_blocks"],[0,1]);self.assertEqual(r["movie_seconds"],10)
        self.assertEqual(r["level_changes"],[54]);self.assertEqual(r["reason"],"eeg_units_unconfirmed")
        w[-1]["interrupted"]=True
        self.assertFalse(window_behavior(w,self.s.policy,sliding=True)["valid"])
    def test_purge_shared_source_blocks_and_missing_mixed_admission(self):
        rows=training_rows()
        for r in rows:
            r.update(windowRevision=SLIDING,levels=[r["level"]]*5,source_blocks=[r["block"]])
        rows[12]["source_blocks"]=[1,2]
        with patch("personalized_model.forest",side_effect=lambda p,k:forest(dict(p,n_estimators=3),k)),patch(
                "personalized_model.circular_permutation_test",return_value={"passed":True}):
            _,report=train_personal(rows,"test","s","synthetic",3,policy_config(),self.tmp.name)
        fold=next(f for f in report["folds"] if f["block"]==2)
        self.assertEqual(fold["held_source_blocks"],[1,2]);self.assertNotIn(1,fold["train_blocks"])
        self.assertIn("mixed_window_held_out_auc_unqualified",report["reasons"])
        self.assertEqual(report["windowRevision"],SLIDING)

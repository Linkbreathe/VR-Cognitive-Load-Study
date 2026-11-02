"""Synthetic dev5 policy, audit and historical replay regression tests."""
import tempfile
import unittest
import csv
from pathlib import Path
from controller import VERSION, BaselineAdaptiveController
from service import StudyService
from replay import replay
from test_stage2 import block
from analyze import summarize


def evaluation(number, last):
    return dict(evaluationId=number, epoch=3, block=3, level=1, stage="training", firstTrial=last-9,
                lastTrial=last, eligible=10, matches=3, nonmatches=7, omissions=0, valid=True,
                balancedAccuracy=1, medianCorrectRt=1, start=300+(last-9)*4.5,
                end=300+last*4.5+4, conditionStart=300)


def packet():
    rows=[evaluation(1,11),evaluation(2,13)]
    return dict(sessionId="synthetic_rolling",sessionType="synthetic",participantCode="test",
                requestId="rolling:2",configVersion=VERSION,stage="training",block=3,level=1,
                evaluationId=2,epoch=3,lastTrial=13,now=rows[-1]["end"],clockOffset=0,clockUncertainty=.01,
                config=dict(mode="ACTIVE",controlBasis="behavioral_fallback",evaluationPolicy="rolling10-stride2-v1"),
                blocks=[block(1,0,"baseline",accuracy=.9),block(2,0,"baseline",accuracy=.9)],events=[],evaluations=rows)


class RollingTests(unittest.TestCase):
    def setUp(self):self.p=packet();self.c=BaselineAdaptiveController()
    def action(self):return self.c.decide(self.p)["action"]
    def test_two_rolling_passes(self):self.assertEqual(self.action(),"UP")
    def test_first_window_holds(self):
        self.p["evaluations"].pop(0);self.assertEqual(self.action(),"HOLD")
    def test_overlap_and_identity_required(self):
        for patch in [dict(lastTrial=10),dict(epoch=2),dict(level=2),dict(evaluationId=0),dict(valid=False)]:
            with self.subTest(patch=patch):
                self.p=packet();self.p["evaluations"][0].update(patch);self.assertEqual(self.action(),"HOLD")
    def test_statistics_boundary_does_not_reset_history(self):
        self.p["evaluations"][0]["block"]=2;self.assertEqual(self.action(),"UP")
    def test_class_coverage_holds(self):
        self.p["evaluations"][-1].update(matches=2,nonmatches=8,valid=False);self.assertEqual(self.action(),"HOLD")
    def test_short_window_omission_thresholds(self):
        for row in self.p["evaluations"]:row["omissions"]=1
        self.assertEqual(self.action(),"UP")
        for row in self.p["evaluations"]:row["omissions"]=2
        self.assertEqual(self.action(),"DOWN")
    def test_single_difficulty_holds(self):
        self.p["evaluations"][-1].update(balancedAccuracy=.6);self.assertEqual(self.action(),"HOLD")
    def test_severe_single_window(self):
        self.p["evaluations"][-1]["balancedAccuracy"]=.4;self.assertEqual(self.action(),"DOWN")
    def test_all_omissions_without_rt_or_class_coverage(self):
        self.p["evaluations"][-1].update(omissions=10,medianCorrectRt=-1,matches=2,nonmatches=8,valid=False)
        self.assertEqual(self.action(),"DOWN")
    def test_zero_level_difficulty_stops(self):
        self.p["level"]=0
        for row in self.p["evaluations"]:row.update(level=0,omissions=4)
        self.assertEqual(self.action(),"REST")
    def test_stale_and_wrong_identity_hold(self):
        for patch in [dict(epoch=4),dict(evaluationId=3),dict(lastTrial=14),dict(block=4),dict(end=0),dict(end=999)]:
            with self.subTest(patch=patch):
                self.p=packet();self.p["evaluations"][-1].update(patch);self.assertEqual(self.action(),"HOLD")
    def test_modes_and_fixed_stages(self):
        self.p["config"]["mode"]="OFF";self.assertEqual(self.action(),"HOLD")
        self.p["config"]["mode"]="SHADOW";self.assertEqual(self.action(),"UP")
        self.p["stage"]="pretest";self.assertEqual(self.action(),"HOLD")
    def test_physiology_missing_holds_but_behavior_can_downgrade(self):
        self.p["config"]["controlBasis"]="physiological";self.assertEqual(self.action(),"HOLD")
        self.p["evaluations"][-1]["omissions"]=4;self.assertEqual(self.action(),"DOWN")
    def test_physiology_requires_current_condition_not_short_behavior_window(self):
        self.p["config"]["controlBasis"]="physiological"
        cal=dict(participantCode="test",configVersion=VERSION,task="2-back-distraction",validated=True,validation_record="SYNTHETIC_ONLY",
                 features=[dict(name="rmssd",source_id="synthetic-fixture",validated=True,center=1,scale=1,adverse_direction=-1,adverse_z=2)])
        f=dict(sessionId=self.p["sessionId"],name="rmssd",source_id="synthetic-fixture",value=1,quality=1,
               synthetic=False,levels=[1],stages=["training"],coverage_complete=True,clock_uncertainty=.01,
               window_start=301,window_end=self.p["now"]-1)
        self.assertEqual(self.c.decide(self.p,[f],cal)["action"],"UP")
        for patch in [dict(window_start=299),dict(synthetic=True),dict(levels=[0,1]),dict(value=-2)]:
            self.assertEqual(self.c.decide(self.p,[dict(f,**patch)],cal)["action"],"HOLD")
        cal["configVersion"]="2.0.0-stage2-dev4"
        self.assertEqual(self.c.decide(self.p,[f],cal)["action"],"HOLD")
    def test_service_unique_requests_restart_replay_and_condition_changes(self):
        with tempfile.TemporaryDirectory() as folder:
            service=StudyService(folder)
            self.p["events"]=[dict(type="adaptive_level_changed",actual=300,detail="level=1;stage=training")]
            first=service.receive(self.p,True)["decision"]
            self.assertEqual(first["action"],"UP")
            self.assertTrue(service.sessions[self.p["sessionId"]]["conditions"][-1]["running"])
            restarted=StudyService(folder)
            self.assertEqual(restarted.receive(self.p,True)["decision"],first)
            self.p["requestId"]="rolling:3";self.p["evaluationId"]=3;self.p["lastTrial"]=15
            self.p["evaluations"]=[evaluation(2,13),evaluation(3,15)];self.p["now"]=self.p["evaluations"][-1]["end"]
            restarted.receive(self.p,True)
            result=replay(Path(folder)/"synthetic"/self.p["sessionId"])
            self.assertEqual(result["decision_replays"],2)
    def test_mixed_statistics_are_not_attributed_to_last_level(self):
        with tempfile.TemporaryDirectory() as directory:
            folder=Path(directory)
            (folder/"training_blocks.csv").write_text("block,stage,level,valid,balanced_accuracy,median_correct_rt_s,omissions\n3,training,-1,True,1,1,0\n",encoding="utf-8")
            with (folder/"trials.csv").open("w",newline="",encoding="utf-8") as f:
                fields=["block","training_stage","level","scored","transition","interrupted","aborted","expected_match","correct","responded","rt_s"]
                writer=csv.DictWriter(f,fieldnames=fields);writer.writeheader()
                for i in range(24):
                    writer.writerow(dict(block=3,training_stage="training",level=1 if i<14 else 2,scored=1,transition=0,
                                         interrupted=0,aborted=0,expected_match=int(i%3==0),correct=1,responded=1,rt_s=1))
            result=summarize(folder)
            self.assertEqual(result["mixed_level_statistics_blocks"],1)
            self.assertNotIn("training_level_2",result["conditions"])
            self.assertEqual(result["training_trial_metrics_by_actual_level"]["1"]["trials"],14)
            self.assertEqual(result["training_trial_metrics_by_actual_level"]["2"]["trials"],10)
            self.assertEqual(result["training_trial_metrics_by_actual_level"]["1"]["balanced_accuracy"],1)


if __name__=="__main__":unittest.main()

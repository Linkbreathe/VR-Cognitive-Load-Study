import copy
import json
from pathlib import Path
import tempfile
import unittest

import numpy as np
from controller import BaselineAdaptiveController, VERSION, physiology_status, POLICY
from features import extract_features, condition_coverage
from service import StudyService
from replay import replay


def block(index, level, stage="training", accuracy=.88, rt=1, omissions=0, valid=True):
    return dict(block=index, level=level, stage=stage, valid=valid, eligible=24, matches=8, nonmatches=16,
        balancedAccuracy=accuracy,medianCorrectRt=rt,omissions=omissions,start=index*108,end=(index+1)*108)


def packet():
    return dict(sessionId="synthetic_policy",participantCode="test",sessionType="synthetic",requestId="r4",
        configVersion=VERSION,stage="training",block=4,level=1,now=539.5,clockOffset=0,clockUncertainty=.01,
        config=dict(mode="ACTIVE",controlBasis="behavioral_fallback"),events=[],
        blocks=[block(1,0,"baseline"),block(2,0,"baseline"),block(3,1),block(4,1)])


class ControllerTests(unittest.TestCase):
    def setUp(self):self.c=BaselineAdaptiveController();self.p=packet()
    def test_two_passes_upgrade(self):self.assertEqual(self.c.decide(self.p)["action"],"UP")
    def test_historical_protocol_remains_replayable(self):
        self.p["configVersion"]="2.0.0-stage2-dev1"
        self.assertEqual(self.c.decide(self.p)["action"],"UP")
        with tempfile.TemporaryDirectory() as folder:
            self.assertEqual(StudyService(folder).receive(self.p,True)["decision"]["action"],"UP")
    def test_old_calibration_cannot_promote_new_presentation(self):
        c,f=self.fixture();c["configVersion"]="2.0.0-stage2-dev1"
        self.assertEqual(self.c.decide(self.p,[f],c)["action"],"HOLD")
    def test_one_pass_holds(self):
        self.p["blocks"].pop(-2);self.assertEqual(self.c.decide(self.p)["action"],"HOLD")
    def test_two_difficult_blocks_reduce(self):
        for b in self.p["blocks"][-2:]:b["balancedAccuracy"]=.60
        self.assertEqual(self.c.decide(self.p)["action"],"DOWN")
    def test_severe_reduces_at_boundary(self):
        self.p["blocks"][-1]["balancedAccuracy"]=.4;self.assertEqual(self.c.decide(self.p)["action"],"DOWN")
    def test_all_omissions_still_reduce_without_rt(self):
        self.p["blocks"][-1].update(omissions=24,balancedAccuracy=0,medianCorrectRt=-1)
        self.assertEqual(self.c.decide(self.p)["action"],"DOWN")
    def test_user_stop_immediate(self):
        self.p["stop_requested"]=True;self.assertEqual(self.c.decide(self.p)["action"],"REST")
    def test_invalid_breaks_streak(self):
        self.p["blocks"][-2]["valid"]=False;self.assertEqual(self.c.decide(self.p)["action"],"HOLD")
    def test_wrong_level_breaks_streak(self):
        self.p["blocks"][-2]["level"]=0;self.assertEqual(self.c.decide(self.p)["action"],"HOLD")
    def test_slow_rt_prevents_pass(self):
        self.p["blocks"][-1]["medianCorrectRt"]=1.3;self.assertEqual(self.c.decide(self.p)["action"],"HOLD")
    def test_omissions_prevent_pass(self):
        self.p["blocks"][-1]["omissions"]=3;self.assertEqual(self.c.decide(self.p)["action"],"HOLD")
    def test_missing_baseline_rests(self):
        self.p["blocks"].pop(0);self.assertEqual(self.c.decide(self.p)["action"],"REST")
    def test_missing_physiology_never_upgrades(self):
        self.p["config"]["controlBasis"]="physiological";self.assertEqual(self.c.decide(self.p)["action"],"HOLD")
    def fixture(self):
        self.p["config"]["controlBasis"]="physiological"
        calibration=dict(participantCode="test",configVersion=VERSION,task="2-back-distraction",validated=True,
            validation_record="SYNTHETIC_TEST_ONLY",features=[dict(name="eeg_ch0_theta_relative",source_id="ear-eeg",validated=True,center=.4,scale=.1,adverse_direction=1,adverse_z=2)])
        f=dict(sessionId=self.p["sessionId"],name="eeg_ch0_theta_relative",source_id="ear-eeg",value=.45,quality=1,
            synthetic=False,levels=[1],stages=["training"],coverage_complete=True,clock_uncertainty=.01,window_start=526,window_end=538)
        return calibration,f
    def test_calibrated_feature_allows_up(self):
        c,f=self.fixture();d=self.c.decide(self.p,[f],c);self.assertEqual(d["action"],"UP");self.assertTrue(d["physiologicalEligible"])
    def test_quality_failures_hold(self):
        c,f=self.fixture()
        for patch in [dict(quality=.2),dict(synthetic=True),dict(levels=[0,1]),dict(stages=["pretest","training"]),
                      dict(window_end=500),dict(window_end=550),dict(clock_uncertainty=.3),dict(coverage_complete=False),
                      dict(source_id="other"),dict(sessionId="other"),dict(value=float("nan"))]:
            with self.subTest(patch=patch):self.assertEqual(self.c.decide(self.p,[dict(f,**patch)],c)["action"],"HOLD")
    def test_unvalidated_and_wrong_person_hold(self):
        c,f=self.fixture()
        for patch in [dict(validated=False),dict(participantCode="other"),dict(validation_record="")]:
            self.assertEqual(self.c.decide(self.p,[f],dict(c,**patch))["action"],"HOLD")
    def test_adverse_single_window_holds_does_not_oscillate(self):
        c,f=self.fixture();f["value"]=.8;self.assertEqual(self.c.decide(self.p,[f],c)["action"],"HOLD")
    def test_off_never_proposes_up(self):
        self.p["config"]["mode"]="OFF";self.assertEqual(self.c.decide(self.p)["action"],"HOLD")
    def test_shadow_records_same_recommendation(self):
        self.p["config"]["mode"]="SHADOW";self.assertEqual(self.c.decide(self.p)["action"],"UP")
    def test_duplicate_service_requests_and_replay(self):
        with tempfile.TemporaryDirectory() as folder:
            service=StudyService(folder)
            a=service.receive(self.p,True);b=service.receive(self.p,True)
            self.assertEqual(a["decision"],b["decision"])
            result=replay(Path(folder)/"synthetic"/self.p["sessionId"])
            self.assertEqual(result["decision_replays"],1)
            restarted=StudyService(folder)
            self.assertEqual(restarted.receive(self.p,True)["decision"],a["decision"])
            changed=copy.deepcopy(self.p);changed["config"]["mode"]="OFF"
            with self.assertRaises(ValueError):service.receive(changed)
    def test_path_and_version_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            service=StudyService(folder)
            self.p["sessionId"]="../participant"
            with self.assertRaises(ValueError):service.receive(self.p)
    def test_nonfinite_metrics_hold(self):
        self.p["blocks"][-1]["balancedAccuracy"]=float("nan");self.assertEqual(self.c.decide(self.p)["action"],"HOLD")


class FeatureTests(unittest.TestCase):
    def test_display_source_and_freshness_are_not_control_eligibility(self):
        from service import physiology_display_status
        self.assertEqual(physiology_display_status({},10),dict(source="none",connection="waiting"))
        replay=dict(synthetic=True,data_origin="recorded_xdf_replay",connected=True,last_sample=9)
        self.assertEqual(physiology_display_status({"EEG":replay},10),dict(source="recorded_replay",connection="receiving"))
        self.assertEqual(physiology_display_status({"EEG":replay},13),dict(source="recorded_replay",connection="disconnected"))
        live=dict(synthetic=False,data_origin="live_sensor",connected=True,last_sample=9)
        self.assertEqual(physiology_display_status({"EEG":live},10)["source"],"live")
        self.assertEqual(physiology_display_status({"EEG":replay,"ECG":live},10)["source"],"mixed")
        live["last_sample"]=0
        self.assertEqual(physiology_display_status({"EEG":replay,"ECG":live},10)["connection"],"partial")
        self.assertEqual(physiology_display_status({"EEG":dict(connected=True,last_sample=9)},10)["source"],"unknown")
    def test_telemetry_exposes_source_before_feature_window_ready(self):
        with tempfile.TemporaryDirectory() as folder:
            service=StudyService(folder)
            service.sensors={"EEG":dict(synthetic=True,data_origin="recorded_xdf_replay",connected=False)}
            reply=service.receive(packet())
            self.assertEqual(reply["featuresJson"],"[]")
            self.assertEqual(reply["physiologyStatus"],dict(source="recorded_replay",connection="waiting"))
    def test_raw_dc_offset_does_not_mask_ac_amplitude_check(self):
        t=np.arange(0,12,1/500);x=np.sin(2*np.pi*6*t)
        a,qa=extract_features("EEG",t,x,500,12,10)
        b,qb=extract_features("EEG",t,x+4000,500,12,10)
        self.assertEqual(qa,qb)
        np.testing.assert_allclose([f["value"] for f in a],[f["value"] for f in b],atol=1e-10)
        self.assertEqual(extract_features("EEG",t,x*20+4000,500,12,10)[1]["quality"],0)
    def test_monitor_preserves_slow_ecg_and_latest_eeg(self):
        from service import latest_features
        records=[dict(source_id="eeg",name=f"band_{i}",value=1) for i in range(12)]
        records.insert(0,dict(source_id="ecg",name="heart_rate",value=60))
        records.append(dict(source_id="eeg",name="band_0",value=2))
        result=latest_features(records)
        self.assertEqual(len(result),13)
        self.assertEqual(next(f["value"] for f in result if f["name"]=="band_0"),2)
        self.assertTrue(any(f["name"]=="heart_rate" for f in result))
    def test_known_eeg_frequency(self):
        t=np.arange(0,12,1/250);x=np.sin(2*np.pi*6*t)
        result,q=extract_features("EEG",t,x,250,12,10)
        self.assertGreater(q["quality"],.99)
        self.assertGreater(next(f["value"] for f in result if "theta" in f["name"]),.99)
    def test_flatline_gaps_nan_and_clipping(self):
        t=np.arange(0,12,1/250);x=np.sin(2*np.pi*6*t)
        for times,signal in [(t,np.zeros(len(t))),(t,np.full(len(t),np.nan)),(t,x*100),(np.r_[t[:100],t[101:]],np.r_[x[:100],x[101:]])]:
            # One missing sample is tolerated; a longer missing interval must be rejected.
            if len(times)!=len(t):times=np.r_[t[:100],t[110:]];signal=np.r_[x[:100],x[110:]]
            self.assertEqual(extract_features("EEG",times,signal,250,12,10)[1]["quality"],0)
    def test_ecg_known_rr(self):
        t=np.arange(0,60,1/250);x=np.zeros(len(t))
        for beat in range(1,60):x+=np.exp(-((t-beat)/.012)**2)
        values,q=extract_features("ECG",t,x,250,60,5)
        self.assertGreater(q["quality"],.99)
        self.assertAlmostEqual(next(f["value"] for f in values if f["name"]=="ecg_heart_rate"),60,delta=.5)
        self.assertLess(next(f["value"] for f in values if "rmssd" in f["name"]),5)
    def test_condition_crossing_and_pause(self):
        events=[dict(time=0,level=0,stage="baseline",running=True),dict(time=10,level=1,stage="training",running=True)]
        levels,stages,covered=condition_coverage(5,15,events);self.assertEqual(levels,[0,1]);self.assertTrue(covered)
        self.assertFalse(condition_coverage(-1,5,events)[2])
        events.append(dict(time=12,level=1,stage="training",running=False))
        self.assertFalse(condition_coverage(11,15,events)[2])


if __name__=="__main__":unittest.main(verbosity=2)

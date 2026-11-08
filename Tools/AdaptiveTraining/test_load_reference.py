"""End-to-end local reference fixture, independent of vendor credentials and hardware."""
from tempfile import TemporaryDirectory
from pathlib import Path
import unittest
import numpy as np

from load_reference import fit, score
from personalized_runtime import PersonalSession
from test_personalized import packet, trials
import workload_rf
from test_session_store import TestSessionStore


class ReferenceTests(unittest.TestCase):
    def test_reference_requires_both_separated_conditions(self):
        self.assertIsNone(fit({"load_rest":[[1,2]]*12})[0])
        self.assertIsNone(fit({k:[[1,2]]*12 for k in ("load_rest","load_math")})[0])

    def test_six_minutes_to_saved_reference(self):
        with TemporaryDirectory() as tmp:
            store = TestSessionStore(Path(tmp)/"registry", {"eeg_units_confirmed":True})
            stream = dict(kind="EEG", sample_rate=250, channels=list(range(8)), scale_to_units=1,
                          data_origin="recorded_csv_replay", synthetic=True, units="uV", channel_labels=workload_rf.LABELS)
            store.streams = [stream]
            r = store.prepare(dict(participantCode="FIXTURE", storageFolder=tmp,sessionMode="calibration"))
            p = dict(packet(), sessionId=r["sessionId"], participantCode="FIXTURE", sessionType="editor-test",config=r["config"])
            session = PersonalSession(p,Path(r["folder"])/"service",store.policy,store)
            for stage,onset,freq in (("load_rest",0,10),("load_math",190,6)):
                for i in range(18):
                    t = np.arange(2500)/250
                    x = 7*np.sin(2*np.pi*freq*t)+2*np.sin(2*np.pi*(16 if freq==10 else 14)*t)
                    session.ingest(stream,onset+10*i+t,np.repeat(x[:,None],8,axis=1),"fixture")
                    events=[dict(type="protocol_stage", detail=stage,actual=onset)] if i==0 else []
                    session.receive(dict(p,stage=stage,now=onset+10*i+11,events=events))
            session.receive(dict(p,stage="model_wait",now=373,stageStarted=370))
            self.assertEqual(session.model_status,"load_reference_ready")
            saved,_=store.load_model(tmp,"FIXTURE",r["sessionId"]+"-load",False)
            ref=saved["reference"]
            self.assertEqual(ref["iafHz"], 10)
            self.assertEqual(session.report["meanHeldAuc"], 1)
            self.assertEqual([f["heldWindows"] for f in session.report["folds"]], [[6,6]] * 3)
            # Exercise the same task-window and reload paths as a subsequent operator assignment.
            store.complete(r["sessionId"], "complete")
            reuse = store.prepare(dict(participantCode="FIXTURE", storageFolder=tmp, sessionMode="bioadaptive",
                                       modelId=r["sessionId"]+"-load"))
            q = dict(p, sessionId=reuse["sessionId"], config=reuse["config"])
            live = PersonalSession(q, Path(reuse["folder"])/"service", store.policy, store)
            t = np.arange(8000)/250
            x = 7*np.sin(2*np.pi*6*t)+2*np.sin(2*np.pi*14*t)
            live.ingest(stream, t, np.repeat(x[:, None],8,axis=1), "fixture")
            from layer_ab import STAGES
            stage = STAGES[0] if live.probe else "bioadaptive"
            live.receive(dict(q, stage=stage, trials=trials(7,stage), now=33))
            prediction = live.predictions[-1]
            self.assertGreater(prediction["loadScore"], .9)
            self.assertIsNone(prediction["score"])
            self.assertFalse(live.report["admitted"])

            # Reuse this fixture to compare pre-Unity and A/B scores for the same EEG interval.
            from service import StudyService
            from probe_control import base_decision
            from unittest.mock import patch
            store.complete(reuse["sessionId"], "complete")
            request = dict(participantCode="FIXTURE", storageFolder=tmp, modelId=r["sessionId"]+"-load")
            bridge = StudyService(Path(tmp)/"preview")
            bridge.test_store = store
            self.assertEqual(bridge.preview_workload(request)["reason"], "waiting_for_live_eeg")
            simulated_live = dict(stream, synthetic=False, data_origin="live_sensor")
            t = np.arange(10125)/250
            x = 7*np.sin(2*np.pi*6*t)+2*np.sin(2*np.pi*14*t)
            values = np.repeat(x[:,None], 8, axis=1)
            values[:,6] = 0  # P3 flat for the full window would reject every screened epoch.
            self.assertEqual(workload_rf.measure_eeg(simulated_live, store.policy,
                             lambda: (values[-2500:], "ok", 250))[1], "insufficient_clean_epochs")
            samples = values.tolist()
            def feed(start,end):
                times = t[start:end].tolist()
                bridge.record_signal(simulated_live, times, times, samples[start:end], 0, "fixture")
            feed(0, 9000)
            self.assertEqual(bridge.preview_workload(request)["reason"], "live_eeg_stale")
            feed(9000, 10125)
            with patch("service.time.perf_counter", return_value=40.5):
                preview = bridge.preview_workload(request)
            self.assertEqual(preview["reason"], "ok")
            self.assertFalse(preview["artifactScreening"])
            self.assertFalse(bridge.sessions)
            profile = store.list_models(tmp,"FIXTURE")[0]["thresholdProfile"]
            self.assertEqual([len(profile[k]) for k in ("forest","arithmetic")], [18,18])
            with self.assertRaisesRegex(ValueError, "ON lower than OFF"):
                store.prepare(dict(request,sessionMode="comparison_candidate",scoreThresholds={"on":.7,"off":.4}))
            edited = {"on": .49, "off": .55}
            assignment = store.prepare(dict(request,sessionMode="comparison_candidate",scoreThresholds=edited))
            self.assertEqual(assignment["recommendedThresholds"], profile["recommended"])
            self.assertEqual(assignment["scoreThresholds"], edited)
            for stage in STAGES:
                task = dict(q, sessionId=assignment["sessionId"], config=assignment["config"],
                            stage=stage, completedTrials=10, level=1, now=46)
                arm = PersonalSession(task, Path(tmp)/stage, store.policy, store)
                arm.ingest(stream,t,np.asarray(samples),"fixture")
                rows = [dict(row,level=1) for row in trials(10,stage)]
                arm.receive(dict(task,trials=rows))
                decision = base_decision(arm,task,{},rows)
                self.assertAlmostEqual(decision["score"], preview["score"], places=10)
                self.assertEqual((decision["lowThreshold"],decision["highThreshold"]),(.49,.55))
                self.assertTrue(decision["modelTrigger"])
                arm.predictions[-2]["score"] = .55
                self.assertTrue(base_decision(arm,task,{},rows)["modelTrigger"])
                self.assertFalse(decision["artifactScreening"])

    def test_shifted_candidates_recover_windows_without_counting_overlap(self):
        from unittest.mock import patch
        with TemporaryDirectory() as tmp:
            session = PersonalSession(packet(), Path(tmp), {"eeg_units_confirmed": True})
            def measure(session, packet, start, details=None, *, screen_artifacts=True):
                if start < 1:
                    return None, "missing_samples_or_timestamp_gap"
                if details is not None: details.update(cleanSeconds=7, flaggedChannels={})
                return np.ones((7, 16, 8)), "ok"
            onset = dict(type="protocol_stage", detail="load_math", actual=0)
            session.event(onset)
            with patch.object(workload_rf, "measure", side_effect=measure):
                session.packet = dict(stage="load_math", now=11)
                session.update_load_calibration(session.packet)
                self.assertTrue(session.calibration_quality()["warning"])
                session.packet["now"] = 181
                session.update_load_calibration(session.packet)
                self.assertFalse(session.calibration_quality()["warning"])
            import json
            rows = [json.loads(line) for line in (Path(tmp)/"load_windows.jsonl").read_text().splitlines()]
            self.assertEqual(len(rows), 171)
            kept = [row["startSecond"] for row in rows if row["selected"]]
            self.assertEqual(kept, [1,11,21,31,41,60,70,80,90,100,110,120,130,140,150,160,170])
            self.assertTrue(all(row["trainingEpochs"]==0 for row in rows if not row["selected"]))
            session.event(dict(type="protocol_stage", detail="load_math", actual=130))
            self.assertIsNone(session.load_quality)
            self.assertEqual(session.load_scan["load_math"]["counts"], [0,0,0])

    def test_no_alpha_peak_or_discrimination(self):
        spectra = np.ones((10,16,8))
        rows = {stage: [spectra]*12 for stage in workload_rf.STAGES}
        self.assertEqual(workload_rf.fit(rows, 1)[1], "rest_alpha_peak_unclear")
        spectra[:,10,5:] = 10
        self.assertEqual(workload_rf.fit(rows, 1)[1], "weak_held_out_discrimination")
        rows["load_rest"] = [None]*4 + [spectra]*8
        self.assertEqual(workload_rf.fit(rows, 1)[1], "insufficient_temporal_coverage")

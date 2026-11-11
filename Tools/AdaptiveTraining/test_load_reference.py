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
    def test_training_keeps_artifacts_but_requires_computable_samples(self):
        import json
        stream = dict(kind="EEG", sample_rate=250, channels=list(range(8)), scale_to_units=1,
                      units="uV", channel_labels=workload_rf.LABELS, synthetic=True,
                      data_origin="recorded_csv_replay")
        t = np.arange(1500)/250
        x = np.repeat((7*np.sin(2*np.pi*10*t))[:,None], 8, axis=1)
        x[:, 7] += 1500*np.sin(2*np.pi*50*t)
        x[:, 1] = 0
        policy = {"eeg_units_confirmed": True}
        self.assertEqual(workload_rf.measure_eeg(stream, policy, lambda: (x, "ok", 250))[1],
                         "insufficient_clean_epochs")
        with TemporaryDirectory() as tmp:
            p = packet()
            session = PersonalSession(p, Path(tmp), policy)
            session.ingest(stream, t, x, "artifact-fixture")
            session.event(dict(type="protocol_stage", detail="load_rest", actual=0))
            session.packet = dict(p, stage="load_rest", now=6)
            session.update_load_calibration(session.packet)
            self.assertEqual(session.load_rows["load_rest"][0].shape, (5,16,8))
            row = json.loads((Path(tmp)/"load_windows.jsonl").read_text().splitlines()[0])
            self.assertTrue(row["selected"])
            self.assertFalse(row["artifactScreening"])
            self.assertEqual(row["trainingSeconds"], 5)
            self.assertNotIn("cleanSeconds", row)
            self.assertIn("Artifact screening is off", session.calibration_quality()["text"])
            self.assertNotIn("quality OK", session.calibration_quality()["text"])
        invalid = x.copy(); invalid[0,0] = np.nan
        self.assertEqual(workload_rf.measure_eeg(stream, policy,
                         lambda: (invalid, "ok", 250), screen_artifacts=False)[1], "nonfinite_samples")
        self.assertEqual(workload_rf.measure_eeg(stream, policy,
                         lambda: (None, "missing_samples_or_timestamp_gap", 250), screen_artifacts=False)[1],
                         "missing_samples_or_timestamp_gap")

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
            from test_session_store import read, write
            manifest_path = Path(tmp)/"FIXTURE"/"models"/(r["sessionId"]+"-load")/"manifest.json"
            manifest = read(manifest_path)
            mismatched = dict(manifest, workloadWindowContract=dict(manifest["workloadWindowContract"], windowSeconds=10))
            write(manifest_path, mismatched)
            with self.assertRaisesRegex(ValueError, "window contract mismatch"):
                store.load_model(tmp,"FIXTURE",r["sessionId"]+"-load",False)
            write(manifest_path, manifest)
            ref=saved["reference"]
            self.assertEqual(ref["iafHz"], 10)
            self.assertEqual(session.report["meanHeldAuc"], 1)
            self.assertEqual([f["heldWindows"] for f in session.report["folds"]], [[12,12]] * 3)
            self.assertEqual(session.report["validWindows"], {k:36 for k in workload_rf.STAGES})
            self.assertEqual(session.report["trainingSeconds"], {k:180 for k in workload_rf.STAGES})
            self.assertFalse(session.report["artifactScreening"])
            self.assertEqual(session.report["minCleanSeconds"], 0)
            self.assertNotIn("cleanSeconds", session.report)
            self.assertEqual(session.report["windowSeconds"], 5)
            self.assertEqual(session.report["candidateStepSeconds"], 1)
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
            self.assertEqual(prediction["loadWindowEnd"]-prediction["loadWindowStart"], 5)
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
            self.assertEqual(preview["windowSeconds"], 5)
            self.assertEqual(preview["end"]-preview["start"], 5)
            self.assertFalse(bridge.sessions)
            profile = store.list_models(tmp,"FIXTURE")[0]["thresholdProfile"]
            self.assertEqual([len(profile[k]) for k in ("forest","arithmetic")], [36,36])
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
                self.assertEqual(arm.predictions[-1]["end"]-arm.predictions[-1]["start"], 5)
                self.assertEqual(decision["scoreSource"], "rest-math-iaf-rf-v3-5s")
                self.assertEqual(decision["workloadWindowSeconds"], 5)
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
                self.assertFalse(screen_artifacts)
                if start < 1:
                    return None, "missing_samples_or_timestamp_gap"
                if details is not None: details.update(trainingSeconds=5)
                return np.ones((5, 16, 8)), "ok"
            onset = dict(type="protocol_stage", detail="load_math", actual=0)
            session.event(onset)
            with patch.object(workload_rf, "measure", side_effect=measure):
                session.packet = dict(stage="load_math", now=6)
                session.update_load_calibration(session.packet)
                self.assertTrue(session.calibration_quality()["warning"])
                session.packet["now"] = 181
                session.update_load_calibration(session.packet)
                self.assertFalse(session.calibration_quality()["warning"])
            import json
            rows = [json.loads(line) for line in (Path(tmp)/"load_windows.jsonl").read_text().splitlines()]
            self.assertEqual(len(rows), 176)
            kept = [row["startSecond"] for row in rows if row["selected"]]
            self.assertEqual(kept, list(range(1,56,5)) + list(range(60,116,5)) + list(range(120,176,5)))
            self.assertTrue(all(row["endSecond"]-row["startSecond"]==5 for row in rows))
            self.assertTrue(all(row["startSecond"]//60 == (row["endSecond"]-1)//60 for row in rows if row["selected"]))
            self.assertIn("12 required per stage", session.calibration_quality()["text"])
            self.assertIn("5/5", session.calibration_quality()["text"])
            self.assertTrue(all(row["trainingEpochs"]==0 for row in rows if not row["selected"]))
            session.event(dict(type="protocol_stage", detail="load_math", actual=130))
            self.assertIsNone(session.load_quality)
            self.assertEqual(session.load_scan["load_math"]["counts"], [0,0,0])

    def test_auc_report_only_retains_temporal_coverage_requirement(self):
        spectra = np.ones((5,16,8))
        rows = {stage: [spectra]*36 for stage in workload_rf.STAGES}
        model,reason,report = workload_rf.fit(rows, 1)
        self.assertEqual(reason, "ok")
        self.assertIsNotNone(model)
        self.assertEqual(report["meanHeldAuc"], .5)
        self.assertFalse(report["aucGating"])
        self.assertIsNone(report["aucMinimum"])
        spectra[:,10,5:] = 10
        self.assertEqual(workload_rf.fit(rows, 1)[1], "ok")
        rows["load_rest"] = [None]*12 + [spectra]*24
        self.assertEqual(workload_rf.fit(rows, 1)[1], "insufficient_temporal_coverage")

    def test_low_auc_model_is_published_reloadable_and_reported(self):
        with TemporaryDirectory() as tmp:
            store = TestSessionStore(Path(tmp)/"registry", {"eeg_units_confirmed":True})
            stream = dict(kind="EEG", sample_rate=250, channels=list(range(8)), scale_to_units=1,
                          data_origin="recorded_csv_replay", synthetic=True, units="uV", channel_labels=workload_rf.LABELS)
            store.streams = [stream]
            record = store.prepare(dict(participantCode="FIXTURE", storageFolder=tmp, sessionMode="calibration"))
            p = dict(packet(), sessionId=record["sessionId"], participantCode="FIXTURE", sessionType="editor-test", config=record["config"])
            session = PersonalSession(p, Path(record["folder"])/"service", store.policy, store)
            spectra = np.ones((5,16,8))
            session.load_rows = {stage:[spectra.copy() for _ in range(36)] for stage in workload_rf.STAGES}
            session.load_scoring_rows = {stage:dict(enumerate(rows)) for stage,rows in session.load_rows.items()}
            session.update_load_calibration(dict(p, stage="model_wait", now=3, stageStarted=0))
            self.assertEqual(session.report["meanHeldAuc"], .5)
            self.assertEqual(session.model_status, "load_reference_ready")
            training = store.records[record["sessionId"]]["lastTraining"]
            self.assertTrue(training["hasModel"])
            self.assertEqual(training["foldAuc"], [.5,.5,.5])
            self.assertFalse(training["aucGating"])
            saved,_ = store.load_model(tmp,"FIXTURE",training["modelId"],False)
            self.assertEqual(workload_rf.score(saved["reference"],spectra),
                             workload_rf.score(session.reference,spectra))
            self.assertEqual(store.list_models(tmp,"FIXTURE")[0]["meanHeldAuc"],.5)
            self.assertFalse(saved["report"]["admitted"])

    def test_band_fallback_is_explicit_and_does_not_invent_a_peak(self):
        rest = np.ones((20,16,8))
        fallback = workload_rf.select_bands(rest)
        self.assertEqual(fallback["mode"], "fixed_bands_fallback")
        self.assertIsNone(fallback["detectedIafHz"])
        self.assertEqual((fallback["thetaHz"], fallback["alphaHz"]), ([4,8],[8,12]))
        rest[:,10,5:] = 1.05
        self.assertEqual(workload_rf.select_bands(rest)["reason"], "alpha_peak_below_prominence")
        rest[:,10,5:] = 10
        detected = workload_rf.select_bands(rest)
        self.assertEqual(detected["mode"], "individual_alpha_peak")
        self.assertEqual(detected["detectedIafHz"], 10)

    def test_fallback_fit_save_reload_and_fold_local_selection(self):
        import joblib
        from unittest.mock import patch
        rest = np.ones((5,16,8))
        arithmetic = rest.copy(); arithmetic[:,4:8,:5] = 8
        rows = {"load_rest":[rest.copy() for _ in range(36)],
                "load_math":[arithmetic.copy() for _ in range(36)]}
        scoring = {s:dict(enumerate(v)) for s,v in rows.items()}
        with patch.object(workload_rf, "select_bands", wraps=workload_rf.select_bands) as select:
            model, reason, report = workload_rf.fit(rows, 1, scoring)
        self.assertEqual(reason, "ok")
        self.assertEqual([call.args[0].shape[0] for call in select.call_args_list], [120,120,120,180])
        self.assertEqual(report["meanHeldAuc"], 1)
        self.assertIsNone(report["iafHz"])
        self.assertTrue(all(f["iafHz"] is None for f in report["folds"]))
        self.assertEqual(report["featureReference"]["bandProfile"]["mode"], "fixed_bands_fallback")
        with TemporaryDirectory() as tmp:
            path = Path(tmp)/"model.joblib"; joblib.dump(model, path)
            restored = joblib.load(path)
            self.assertEqual(workload_rf.score(model, arithmetic), workload_rf.score(restored, arithmetic))
        # An alpha peak present only in fold zero must not leak into its training subset.
        for v in rows["load_rest"][:12]: v[:,10,5:] = 20
        _, _, report = workload_rf.fit(rows, 1, scoring)
        self.assertEqual(report["folds"][0]["bandProfile"]["mode"], "fixed_bands_fallback")

    def test_five_second_quality_boundary_and_live_readiness(self):
        config = dict(sample_rate=250, channels=list(range(8)), channel_labels=workload_rf.LABELS,
                      units="uV", data_origin="live_sensor", synthetic=False)
        policy = {"eeg_units_confirmed": True}
        t = np.arange(1250)/250
        clean = np.repeat((7*np.sin(2*np.pi*10*t))[:,None], 8, axis=1)
        for bad_seconds, expected in ((2, "ok"), (3, "insufficient_clean_epochs")):
            values = clean.copy()
            values[:bad_seconds*250] = 0
            quality = {}
            epochs, reason = workload_rf.measure_eeg(config, policy, lambda: (values, "ok", 250), quality)
            self.assertEqual(reason, expected)
            self.assertEqual(quality["cleanSeconds"], 5-bad_seconds)
            self.assertEqual(len(epochs) if epochs is not None else None, 3 if bad_seconds==2 else None)
        rows = list(zip(t, clean))
        self.assertEqual(workload_rf.measure_live_buffer(config, rows[:1000], policy, 4)[1]["reason"], "collecting_five_seconds")
        epochs, details = workload_rf.measure_live_buffer(config, rows, policy, 5)
        self.assertEqual(epochs.shape, (5,16,8))
        self.assertEqual((details["start"], details["end"]), (0,5))
        # Stale samples before the last five seconds must not affect the live score input.
        older = list(zip(t-5, np.zeros_like(clean)))
        actual, _ = workload_rf.measure_live_buffer(config, older+rows, policy, 5)
        np.testing.assert_array_equal(actual, epochs)

    def test_twelve_windows_required_and_old_model_refused_before_loading(self):
        spectra = np.ones((5,16,8))
        rows = {stage: [spectra]*11+[None]*25 for stage in workload_rf.STAGES}
        self.assertEqual(workload_rf.fit(rows, 1)[1], "fewer_than_twelve_valid_windows_per_condition")
        import json
        with TemporaryDirectory() as tmp:
            model = Path(tmp)/"FIXTURE"/"models"/"OLD"
            model.mkdir(parents=True)
            manifest = dict(participantCode="FIXTURE", modelId="OLD", featureRevision="rest-math-iaf-rf-v2")
            path = model/"manifest.json"
            path.write_text(json.dumps(manifest))
            before = path.read_bytes()
            store = TestSessionStore(Path(tmp)/"registry")
            with self.assertRaisesRegex(ValueError, "historical 10-second"):
                store.load_model(tmp,"FIXTURE","OLD",True)
            self.assertEqual(store.list_models(tmp,"FIXTURE"), [])
            self.assertEqual(path.read_bytes(), before)

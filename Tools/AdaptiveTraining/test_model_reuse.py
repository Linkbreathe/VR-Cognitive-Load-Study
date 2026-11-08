"""Engineering fixtures only: identity, persistence, model inference and archive recovery."""
import base64
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import joblib
import numpy as np
import sklearn
import threading
from urllib.request import Request, urlopen
from urllib.error import HTTPError

from personalized_model import policy_config, forest, VERSION, resolve_policy_path
from personalized_runtime import PersonalSession
from personalized_signals import FEATURES, REVISION as FEATURE_REVISION
from service import StudyService, StudyHTTPServer, make_handler
from test_personalized import packet, trials
from test_session_store import TestSessionStore, ARCHIVE_FILES, write, read, digest, signal_contract, code


class ReuseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.service = StudyService(self.root / "service")
        self.store = self.service.test_store
        self.store.streams = [{"kind": "EEG", "sample_rate": 250, "channels": [0], "scale_to_units": 1},
                              {"kind": "ECG", "sample_rate": 250, "channels": [1], "scale_to_units": 1}]
        self.request = {"participantCode": "TEST001", "storageFolder": str(self.root / "data"), "sessionMode": "calibration"}

    def tearDown(self): self.temp.cleanup()

    def test_independent_assignments_and_qualified_comparison_restart(self):
        from layer_ab import SLIDING, condition, validate
        for mode in ("practice","practice_eeg","calibration_rehearsal","control_calibration","comparison_rehearsal"):
            r, p = self.prepared(sessionMode=mode, order="BA")
            validate(p["config"])
            self.service.receive(p)
            if mode=="comparison_rehearsal":
                self.assertEqual((p["config"]["trainingTrials"],p["config"]["rollingWindowTrials"],p["config"]["rollingStrideTrials"]),(60,5,2))
                self.assertEqual(condition(p["config"],1),"B")
                self.assertFalse(p["config"]["testModelId"])
            self.store.complete(r["sessionId"],"complete")
        _, _, folder = self.model(admitted=True)
        with self.assertRaisesRegex(ValueError, "response-close"):
            self.prepared(sessionMode="comparison", modelId="fixture-model")
        artifact = folder / "model.joblib"
        bundle = joblib.load(artifact)
        bundle["report"].update(windowRevision=SLIDING,supported_level_patterns=["0,0,0,0,0","1,1,1,1,1"])
        joblib.dump(bundle, artifact)
        write(folder/"manifest.json", dict(read(folder/"manifest.json"),sha256=digest(artifact)))
        self.service.test_store = self.store = TestSessionStore(self.store.state_dir,self.store.policy)
        self.store.streams = [{"kind": "EEG", "sample_rate":250, "channels":[0], "scale_to_units":1},
                              {"kind":"ECG","sample_rate":250,"channels":[1],"scale_to_units":1}]
        with self.assertRaises((FileNotFoundError,ValueError)):
            self.prepared(sessionMode="comparison",modelId="fixture-model",participantCode="OTHER")
        r,p=self.prepared(sessionMode="comparison",modelId="fixture-model")
        reply=self.service.receive(p)
        self.assertTrue(reply["modelTestUsable"])
        self.assertFalse(reply["testSignalsReady"])
        self.assertEqual(r["sourceModel"]["modelId"],"fixture-model")
        self.store.complete(r["sessionId"],"complete")
        bundle["report"]["admitted"]=False
        joblib.dump(bundle,artifact)
        write(folder/"manifest.json",dict(read(folder/"manifest.json"),admitted=False,sha256=digest(artifact)))
        with self.assertRaisesRegex(ValueError, "unvalidated candidate"):
            self.prepared(sessionMode="comparison",modelId="fixture-model")
        r,p=self.prepared(sessionMode="comparison_candidate",modelId="fixture-model")
        reply=self.service.receive(p)
        self.assertTrue(reply["modelTestUsable"])
        self.assertFalse(reply["modelAdmitted"])
        self.assertEqual(reply["modelStatus"],"engineering_candidate_loaded")

    def test_comparison_order_balances_new_ids_and_reuses_existing_order(self):
        orders=[]
        for code in ("FIRST","SECOND","FIRST"):
            record=self.store.prepare(dict(self.request,sessionMode="comparison_rehearsal",participantCode=code))
            orders.append(record["config"]["counterbalanceGroup"])
            self.store.complete(record["sessionId"],"complete")
        self.assertEqual(orders,[0,2,0])

    def test_comparison_environment_frozen_through_claim_and_archive(self):
        for environment in ('desktop_fixed_gaze','quest_pro'):
            r,p=self.prepared(sessionMode='comparison_rehearsal',comparisonEnvironment=environment)
            self.service.receive(p)
            self.assertEqual(p['config']['comparisonEnvironment'],environment)
            meta=read(Path(r['folder'])/'service'/'personal_protocol.json')
            self.assertEqual(meta['gazeInput']['simulated'],environment=='desktop_fixed_gaze')
            changed=copy.deepcopy(p)
            changed['config']['comparisonEnvironment']='quest_pro' if environment=='desktop_fixed_gaze' else 'desktop_fixed_gaze'
            with self.assertRaisesRegex(ValueError,'differs'):
                self.store.verify_packet(changed)
            self.store.complete(r['sessionId'],'complete')
        with self.assertRaisesRegex(ValueError,'engineering comparison'):
            self.store.prepare(dict(self.request,comparisonEnvironment='desktop_fixed_gaze'))

    def prepared(self, **changes):
        r = self.store.prepare(dict(self.request, **changes))
        claim = self.store.claim({"clientId": "device1", "controllerUrl": "http://127.0.0.1:8765"})
        p = packet(); p.update(sessionId=r["sessionId"], participantCode="TEST001", sessionType="editor-test",
                               config=claim["config"], stage="preparation", testClientId="device1")
        return r, p

    def model(self, admitted=False):
        folder = self.root / "data/TEST001/models/fixture-model"
        folder.mkdir(parents=True)
        model = forest(dict(self.store.policy, n_estimators=7), 4)
        x = np.array([[0, 0, 0]] * 12 + [[2, 2, .5]] * 12)
        model.fit(x, [0] * 12 + [1] * 12)
        report = {"admitted": admitted, "version": VERSION, "policy": self.store.policy, "featureRevision": FEATURE_REVISION,
                  "status": "personal_model_validated" if admitted else "personal_model_not_validated", "reasons": [] if admitted else ["engineering_or_replay_session"]}
        joblib.dump({"model": model, "participant": "TEST001", "session": "source-session", "features": FEATURES, "report": report}, folder / "model.joblib")
        write(folder / "manifest.json", {"modelId": "fixture-model", "participantCode": "TEST001", "sourceSession": "source-session",
              "createdUtc": "fixture", "admitted": admitted, "reasons": report["reasons"], "version": VERSION, "features": FEATURES,
              "featureRevision": FEATURE_REVISION,
              "policy": self.store.policy, "signalContract": signal_contract(self.store.streams), "sklearn": sklearn.__version__, "sha256": digest(folder / "model.joblib")})
        return model, x, folder

    def test_old_preprocessing_is_rejected_before_model_deserialization(self):
        _, _, folder = self.model()
        manifest = read(folder / "manifest.json")
        for revision in (None, "trial-aligned-eeg-ecg-v2-channel-audit"):
            manifest["featureRevision"] = revision
            write(folder / "manifest.json", manifest)
            with patch("test_session_store.joblib.load") as load:
                with self.assertRaisesRegex(ValueError, "preprocessing revision"):
                    self.store.load_model(self.root / "data", "TEST001", "fixture-model", True)
                load.assert_not_called()

    def test_ids_paths_and_disk_checks(self):
        for bad in ("../x", "", "A/B", "CON", "a b"):
            with self.assertRaises(ValueError): code(bad)
        with self.assertRaises(ValueError): self.store.prepare(dict(self.request, storageFolder="relative"))
        with patch("test_session_store.shutil.disk_usage") as usage:
            usage.return_value.free = 1
            with self.assertRaisesRegex(ValueError, "1 GiB"): self.store.prepare(self.request)

    def test_claim_freeze_and_new_session_no_overwrite(self):
        r, p = self.prepared()
        self.assertFalse(self.store.claim({"clientId": "another", "controllerUrl": "http://127.0.0.1:8765"})["ready"])
        self.service.receive(p)
        for key, value in (("participantCode", "TEST002"), ("sessionType", "participant"), ("testClientId", "wrong")):
            changed = copy.deepcopy(p); changed[key] = value
            with self.assertRaises(ValueError): self.service.receive(changed)
        changed = copy.deepcopy(p); changed["config"]["testStorageFolder"] = "elsewhere"
        with self.assertRaises(ValueError): self.service.receive(changed)
        with self.assertRaises(ValueError): self.store.prepare(self.request)
        self.store.complete(r["sessionId"], "complete")
        new, _ = self.prepared()
        self.assertNotEqual(new["folder"], r["folder"])
        self.assertTrue((Path(r["folder"]) / "service/personal_protocol.json").exists())

    def test_restart_recovers_preferences_but_never_task_buffers(self):
        r, p = self.prepared(); self.service.receive(p)
        restarted = TestSessionStore(self.store.state_dir)
        self.assertEqual(restarted.settings["participantCode"], "TEST001")
        self.assertEqual(restarted.settings["storageFolder"], str(self.root / "data"))
        with self.assertRaises(ValueError): restarted.verify_packet(p)
        self.assertFalse(restarted.claim({"clientId": "device1"})["ready"])

    def test_preferences_survive_a_different_bridge_output_directory(self):
        first = StudyService(self.root / "bridge-one", test_state_dir=self.root / "shared-state")
        record = first.test_store.prepare(self.request)
        second = StudyService(self.root / "bridge-two", test_state_dir=self.root / "shared-state")
        self.assertEqual(second.test_store.settings,first.test_store.settings)
        self.assertEqual(second.test_store.records[record["sessionId"]]["state"],"service_restarted")

    def test_model_roundtrip_and_candidate_is_not_admitted(self):
        model, x, _ = self.model()
        with self.assertRaisesRegex(ValueError, "Allow unvalidated"): self.store.prepare(dict(self.request, sessionMode="bioadaptive", modelId="fixture-model"))
        r, p = self.prepared(sessionMode="bioadaptive", modelId="fixture-model", allowCandidate=True)
        reply = self.service.receive(p)
        personal = self.service.sessions[r["sessionId"]]["personal"]
        np.testing.assert_array_equal(personal.model.predict_proba(x), model.predict_proba(x))
        self.assertTrue(reply["modelTestUsable"]); self.assertFalse(reply["modelAdmitted"])
        self.assertEqual(personal.model_status, "engineering_candidate_loaded")
        self.assertFalse(personal.signals); self.assertFalse(personal.trials); self.assertFalse(personal.predictions)
        with self.assertRaises(ValueError): personal.start_fit()
        self.assertEqual(read(personal.folder / "model-reuse.json")["waivedAdmissionReasons"], ["engineering_or_replay_session"])

    def test_model_hash_identity_policy_and_mapping_rejected(self):
        _, _, folder = self.model(True)
        original = read(folder / "manifest.json")
        for key, value in (("participantCode", "OTHER"), ("features", ["wrong"]), ("sklearn", "old"), ("signalContract", []), ("sha256", "wrong")):
            write(folder / "manifest.json", dict(original, **{key: value}))
            with self.assertRaises(ValueError): self.store.load_model(self.root / "data", "TEST001", "fixture-model", False)
        write(folder / "manifest.json", original)
        self.store.streams[0]["scale_to_units"] = 1000000
        with self.assertRaises(ValueError): self.store.load_model(self.root / "data", "TEST001", "fixture-model", False)

    def test_policy_resolution_persists_across_entry_points_and_respects_override(self):
        self.assertIsNone(resolve_policy_path(project_root=self.root))
        saved = self.root / "UserSettings/personalized-policy.json"
        saved.parent.mkdir()
        configured = dict(self.store.policy, theta_channels=[0, 1], alpha_channels=[0, 1])
        write(saved, configured)
        self.assertEqual(resolve_policy_path(project_root=self.root), saved.resolve())
        self.assertEqual(policy_config(read(resolve_policy_path(project_root=self.root))), configured)
        explicit = self.root / "explicit.json"
        write(explicit, self.store.policy)
        self.assertEqual(resolve_policy_path(explicit, self.root), explicit.resolve())
        saved.write_text("invalid JSON", encoding="utf-8")
        with self.assertRaises(ValueError): read(resolve_policy_path(project_root=self.root))
        self.assertEqual(resolve_policy_path(explicit, self.root), explicit.resolve())

    def test_policy_error_identifies_fields_and_restored_policy_loads_without_refit(self):
        model, x, _ = self.model()
        original = copy.deepcopy(self.store.policy)
        self.store.policy = dict(original, theta_channels=[0, 1], eeg_units_confirmed=True)
        with self.assertRaises(ValueError) as error:
            self.store.load_model(self.root / "data", "TEST001", "fixture-model", True)
        self.assertIn("policy.theta_channels: saved=[0, 1, 2, 3, 4], current=[0, 1]", str(error.exception))
        self.assertIn("policy.eeg_units_confirmed: saved=false, current=true", str(error.exception))
        self.store.policy = dict(original, unknown_policy_field=None)
        with self.assertRaisesRegex(ValueError, "policy.unknown_policy_field"):
            self.store.load_model(self.root / "data", "TEST001", "fixture-model", True)
        self.store.policy = original
        loaded, _ = self.store.load_model(self.root / "data", "TEST001", "fixture-model", True)
        np.testing.assert_array_equal(model.predict_proba(x), loaded["model"].predict_proba(x))

    def test_signal_metadata_errors_identify_role_and_preserve_compatibility_gate(self):
        for stream in self.store.streams:
            stream.update(channel_labels=["EEG", "ECG"], units="uV")
        self.model()
        original = copy.deepcopy(self.store.streams)
        for key, value in (("units", None), ("channel_labels", []), ("channels", [1]), ("sample_rate", 500)):
            self.store.streams = copy.deepcopy(original)
            self.store.streams[0][key] = value
            with self.assertRaises(ValueError) as error:
                self.store.load_model(self.root / "data", "TEST001", "fixture-model", True)
            self.assertIn("signalContract.EEG." + key, str(error.exception))
        self.store.streams = original
        self.store.load_model(self.root / "data", "TEST001", "fixture-model", True)

    def test_candidate_control_requires_quality_and_behavior(self):
        self.model(); r, p = self.prepared(sessionMode="bioadaptive", modelId="fixture-model", allowCandidate=True)
        self.service.receive(p); s = self.service.sessions[r["sessionId"]]["personal"]
        p.update(stage="bioadaptive", completedTrials=25, lastTrial=25, now=114)
        s.trials = {(t["trainingStage"], t["phaseTrial"]): t for t in trials(25, "bioadaptive")}
        self.assertEqual(s.decide(p)["action"], "HOLD")
        for i in range(20,25): s.predictions.append({"stage":"bioadaptive","trial":i,"valid":True,"score":.1,"level":0,"start":10})
        d = s.decide(p)
        self.assertEqual(d["action"], "ON"); self.assertTrue(d["engineeringModelEligible"]); self.assertFalse(d["physiologicalEligible"])
        self.assertEqual(d["basis"], "engineering_candidate_rf")
        s.mount_intact=False
        self.assertEqual(s.decide(p)["action"], "HOLD")

    def test_archive_retry_and_final_hash_manifest(self):
        r, _ = self.prepared(); sid=r["sessionId"]
        base={"sessionId":sid,"clientId":"device1"}
        entries=[]
        for name in ARCHIVE_FILES:
            data=json.dumps(r["config"] if name=="adaptive-training-config.json" else
                {"status":"complete","sessionId":sid,"participantCode":"TEST001","sessionType":"editor-test"}).encode() if name.endswith(".json") else b"header\nrow\n"
            payload=dict(base,name=name,offset=0,data=base64.b64encode(data).decode())
            self.store.archive(payload); self.store.archive(payload)
            entries.append({"name":name,"size":len(data),"sha256":hashlib.sha256(data).hexdigest()})
        wrong=copy.deepcopy(entries);wrong[0]["sha256"]="wrong"
        with self.assertRaises(ValueError):self.store.archive(dict(base,manifest=wrong))
        self.store.archive(dict(base,manifest=entries))
        self.assertEqual(r["archive"]["status"],"complete_core_records")
        with self.assertRaises(ValueError):self.store.archive(dict(base,name="../escape",offset=0,data=""))

    def test_missing_model_and_legacy_formal_flow_fail_closed(self):
        with self.assertRaises(OSError): self.store.prepare(dict(self.request,sessionMode="bioadaptive",modelId="missing"))
        p=packet();p["config"]["testSessionMode"]="bioadaptive"
        with self.assertRaises(ValueError):self.service.receive(p)

    def test_archive_restart_can_extend_a_partially_saved_chunk(self):
        r, _ = self.prepared()
        payload = {"sessionId":r["sessionId"],"clientId":"device1","name":"events.jsonl","offset":0}
        self.store.archive(dict(payload,data=base64.b64encode(b"first\n").decode()))
        self.store.archive(dict(payload,data=base64.b64encode(b"first\nsecond\n").decode()))
        self.assertEqual((Path(r["folder"])/"unity/events.jsonl").read_bytes(),b"first\nsecond\n")
        with self.assertRaises(ValueError):self.store.archive(dict(payload,data=base64.b64encode(b"changed\n").decode()))

    def test_completed_test_does_not_append_raw_samples(self):
        r, p = self.prepared(); self.service.receive(p)
        config = dict(self.store.streams[0], source_id="fixture", data_origin="synthetic_fixture", synthetic=True)
        self.service.record_signal(config,[1],[1],[[2]],0,"fixture")
        path = Path(r["folder"])/"service/raw_signals.jsonl"; before=path.read_bytes()
        self.store.complete(r["sessionId"],"complete")
        self.service.record_signal(config,[2],[2],[[3]],0,"fixture")
        self.assertEqual(path.read_bytes(),before)

    def test_actual_training_artifact_is_registered_and_no_model_is_reported(self):
        from personalized_model import train_personal
        from test_personalized import training_rows
        from types import SimpleNamespace
        r, p = self.prepared(); self.service.receive(p)
        folder = Path(r["folder"]) / "service"
        personal = SimpleNamespace(sid=r["sessionId"], participant="TEST001", folder=folder, policy=self.store.policy)
        with patch("personalized_model.forest", side_effect=lambda policy, seed: forest(dict(policy, n_estimators=7), seed)):
            model, report = train_personal(training_rows(), "TEST001", r["sessionId"], "editor-test", 4, self.store.policy, folder / "model_attempt_1")
        write(folder / "training_rows_1.json", training_rows())
        self.store.publish(personal, 1)
        self.assertTrue(r["lastTraining"]["hasModel"])
        loaded, meta = self.store.load_model(self.root / "data", "TEST001", r["lastTraining"]["modelId"], True)
        x = [training_rows()[0]["features"]]
        np.testing.assert_array_equal(model.predict_proba(x), loaded["model"].predict_proba(x))
        self.assertFalse(meta["admitted"])
        train_personal([], "TEST001", r["sessionId"], "editor-test", 4, self.store.policy, folder / "model_attempt_2")
        self.store.publish(personal, 2)
        self.assertFalse(r["lastTraining"]["hasModel"])
        self.assertEqual(len(self.store.list_models(self.root / "data", "TEST001")), 1)

    def test_http_start_requires_local_console_header_and_archive_has_identity(self):
        server = StudyHTTPServer(("127.0.0.1", 0), make_handler(self.service))
        thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
        url = "http://127.0.0.1:" + str(server.server_port)
        try:
            with self.assertRaises(HTTPError) as rejected:
                urlopen(Request(url + "/tests/prepare", json.dumps(self.request).encode(), {"Content-Type": "application/json"}))
            self.assertEqual(rejected.exception.code, 403)
            with urlopen(Request(url + "/tests/prepare", json.dumps(self.request).encode(),
                {"Content-Type": "application/json", "X-Test-Console": "engineering-model-reuse-v1"})) as response:
                r = json.load(response)
            self.assertEqual(r["state"], "prepared")
            with urlopen(url + "/tests") as response: self.assertIn(b'Participant ID', response.read())
            with self.assertRaises(HTTPError):
                urlopen(Request(url + "/tests/archive", json.dumps({"sessionId":r["sessionId"],"clientId":"stranger"}).encode(), {"Content-Type":"application/json"}))
        finally: server.shutdown(); server.server_close(); thread.join()


if __name__ == "__main__": unittest.main()

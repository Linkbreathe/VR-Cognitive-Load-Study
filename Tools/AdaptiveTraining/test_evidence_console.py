"""Console integration regressions using labeled synthetic sessions only."""
from copy import deepcopy
from http.server import ThreadingHTTPServer
import json
from pathlib import Path
import tempfile
import threading
import time
import unittest
from urllib.request import Request, urlopen
from urllib.error import HTTPError

from evidence_console import EegRateMonitor, snapshot, record_wave, waves, config_fingerprint
from service import StudyService, StudyHTTPServer, make_handler
from test_personalized_control import control_packet, control_rows
from test_evidence_guard import gaze
from launch_evidence_console import compatible, ROOT


def fixture(folder, mode="enforce", eyes=False):
    service = StudyService(folder)
    packet = control_packet(control_rows())
    packet["config"]["evidenceGuardMode"] = mode
    if eyes:packet["guardGaze"] = gaze(packet["trials"])
    service.receive(packet)
    personal = service.sessions[packet["sessionId"]]["personal"]
    personal.attempts = 2
    personal.report = {"admitted": False}
    service.receive(packet, True)
    return service, packet


class ConsoleTests(unittest.TestCase):
    def test_delivered_eeg_rate_and_outage(self):
        meter = EegRateMonitor()
        self.assertEqual(meter.status(0)["state"], "unconfigured")
        config = {"source_id": "engineering-eeg", "sample_rate": 250}
        meter.select(config)
        self.assertEqual(meter.status(0)["state"], "no_samples")
        # Model 250 Hz and then 125 Hz, with an identical advertised rate.
        for tick in range(16):
            meter.received(tick / 5, [tick / 5 + (i - 49) / 250 for i in range(50)])
        self.assertEqual(meter.status(3)["actualHz"], 250)
        for tick in range(16, 26):
            meter.received(tick / 5, [tick / 5 + (i - 24) / 125 for i in range(25)])
        self.assertEqual(meter.status(5)["actualHz"], 125)
        self.assertEqual(meter.status(5)["state"], "low")
        self.assertEqual(meter.status(6)["actualHz"], 0)
        self.assertEqual(meter.status(6)["state"], "no_samples")
        meter.received(7, [7 + (i - 49) / 250 for i in range(50)])
        self.assertEqual(meter.status(7)["state"], "measuring")
        for tick in range(36, 46):
            meter.received(tick / 5, [tick / 5 + (i - 49) / 250 for i in range(50)])
        self.assertEqual(meter.status(9)["state"], "receiving")
        self.assertEqual(meter.status(9)["actualHz"], 250)
        meter.select(dict(config, source_id="replacement"))
        self.assertEqual(meter.status(9)["state"], "no_samples")
        # A misleading 250 Hz timestamp clock must not mask 125 Hz delivery.
        for tick in range(11):
            meter.received(tick / 5, [(tick * 25 + i) / 250 for i in range(25)])
        result = meter.status(2)
        self.assertEqual((result["actualHz"], result["deliveryHz"], result["state"]), (250, 125, "low"))
        meter.received(2.2, [0.1, 0.1])
        self.assertEqual(meter.status(2.2)["state"], "timestamps")

    def test_observation_waveforms_without_unity_or_units(self):
        with tempfile.TemporaryDirectory() as folder:
            service = StudyService(folder)
            now = time.perf_counter()
            config = {"kind": "OBSERVE", "uid": "outlet-a", "sample_rate": 500,
                      "channels": [0, 1, 2], "channel_labels": ["sample index", "ch0", "status"],
                      "data_origin": "unknown"}
            service.record_signal(config, [now, now+.002], [now, now+.002],
                                  [[1, -349695.88, "open"], [2, 271578.97, "closed"]], 0, "source-a")
            result = snapshot(service)
            self.assertIsNone(result["session"])
            self.assertEqual(result["signals"], [])
            self.assertEqual(result["windows"], [])
            raw = result["observedSignals"][0]
            self.assertEqual(raw["channels"][1], [[now, -349695.88], [now+.002, 271578.97]])
            self.assertEqual(raw["channels"][2], [])
            self.assertEqual(raw["config"]["data_origin"], "unknown")
            self.assertEqual(result["observationScope"], "current_lsl_receiver_not_selected_session")
            self.assertEqual(list(Path(folder).iterdir()), [])
            # A new outlet cannot inherit the previous source's display trace.
            service.record_signal(dict(config, uid="outlet-b"), [now+1], [now+1], [[3, 12, "x"]], 0, "source-a")
            self.assertEqual(snapshot(service)["observedSignals"][0]["channels"][1], [[now+1, 12]])

    def test_raw_eeg_waveforms_display_without_artifact_screening(self):
        import workload_rf
        with tempfile.TemporaryDirectory() as folder:
            service = StudyService(folder, personalized_policy={"eeg_units_confirmed": True})
            config = dict(kind="EEG", channels=list(range(8)), channel_labels=workload_rf.LABELS,
                          sample_rate=250, units="uV", data_origin="live_sensor")
            now = time.perf_counter()
            service.record_signal(config, [now], [now], [[0.] * 8], 0, "fixture")
            stream = snapshot(service)["observedSignals"][0]
            self.assertEqual(len(stream["channels"]), 8)
            self.assertNotIn("channelCheck", stream)

    def test_observation_preview_does_not_change_session_or_model(self):
        with tempfile.TemporaryDirectory() as folder:
            service, packet = fixture(folder)
            before = snapshot(service)
            service.sessions[packet["sessionId"]]["received"] -= 10
            now = time.perf_counter()
            config = {"kind": "OBSERVE", "sample_rate": 500, "channels": [0], "data_origin": "unknown"}
            service.record_signal(config, [now], [now], [[500000]], 0, "test")
            after = snapshot(service)
            self.assertEqual(before["decisions"], after["decisions"])
            self.assertEqual(before["windows"], after["windows"])
            self.assertEqual(before["model"], after["model"])
            self.assertEqual(after["signals"], [])
            self.assertEqual(after["observedSignals"][0]["channels"][0][0][1], 500000)

    def test_observation_gaps_nonfinite_and_irregular_sources(self):
        store = {}
        config = {"kind": "OBSERVE", "sample_rate": 0, "channels": [0]}
        record_wave(store, config, [1, 2, 3], [[4], [float("nan")], [5]], "test")
        points = waves(store, 3)[0]["channels"][0]
        self.assertIn([2, None], points)
        self.assertEqual(points[-1], [3, 5])
        record_wave(store, config, [.5], [[7]], "test")
        self.assertEqual(waves(store, 3)[0]["channels"][0], [[.5, 7]])

    def test_service_port_is_exclusive_even_against_legacy_listener(self):
        with tempfile.TemporaryDirectory() as folder:
            service = StudyService(folder)
            server = StudyHTTPServer(("127.0.0.1", 0), make_handler(service))
            try:
                for candidate in (StudyHTTPServer, ThreadingHTTPServer):
                    with self.subTest(server=candidate.__name__), self.assertRaises(OSError):
                        other = candidate(("127.0.0.1", server.server_port), make_handler(service))
                        other.server_close()
            finally:server.server_close()

    def test_base_delivered_execution_are_distinct_in_all_modes(self):
        for mode in ("off", "observe", "enforce"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as folder:
                service, packet = fixture(folder, mode)
                before = deepcopy(service.sessions[packet["sessionId"]]["personal"].decisions)
                value = snapshot(service)
                d = value["decisions"][-1]
                self.assertEqual(d["base"]["action"], "ON")
                self.assertEqual(d["delivered"]["action"], "HOLD" if mode == "enforce" else "ON")
                self.assertEqual(value["acknowledgments"], {})
                if mode != "off":self.assertEqual(d["review"]["verdict"], "DEFER")
                packet["now"] += 1
                packet["events"] = [{"type": "personal_execution", "actual": packet["now"],
                                     "detail": f"request={packet['requestId']};action=HOLD;level=0;previous_level=0"}]
                service.receive(packet)
                self.assertEqual(snapshot(service)["acknowledgments"][packet["requestId"]]["level"], "0")
                self.assertEqual(service.sessions[packet["sessionId"]]["personal"].decisions, before)

    def test_supported_path_and_view_details(self):
        with tempfile.TemporaryDirectory() as folder:
            service, _ = fixture(folder, eyes=True)
            d = snapshot(service)["decisions"][-1]
            self.assertEqual(d["delivered"]["guardVerdict"], "ALLOW")
            self.assertTrue(d["review"]["chosen_path"])
            self.assertTrue(d["judgments"])
            self.assertTrue(d["viewChecks"][-1]["available"])

    def test_round_b_support_state_is_projected_for_the_monitor(self):
        from layer_ab import COMPACT_POLICY, PROBE
        from probe_control import ProbeReview
        with tempfile.TemporaryDirectory() as folder:
            service, packet = fixture(folder)
            self.assertNotIn("support", snapshot(service)["guard"])
            adapter = service.sessions[packet["sessionId"]]["guard"]
            adapter.probe = ProbeReview(dict(studyDesign=PROBE, evaluationPolicy=COMPACT_POLICY))
            adapter.probe.reset("layer_b")
            adapter.probe.support_invalidated, adapter.probe.required_off_trials = True, 7
            value = snapshot(service)["guard"]["support"]
            self.assertEqual((value["stage"], value["invalidated"], value["requiredOffTrials"]), ("layer_b", True, 7))
            value["requiredOffTrials"] = 5
            self.assertEqual(adapter.probe.required_off_trials, 7, "Snapshot must be a copy")
            adapter.probe.reset("layer_a")
            self.assertNotIn("support", snapshot(service)["guard"])

    def test_raw_origin_channel_mapping_and_snapshot_isolation(self):
        with tempfile.TemporaryDirectory() as folder:
            service, packet = fixture(folder)
            now = time.perf_counter()
            config = {"kind": "EEG", "sample_rate": 250, "channels": [2], "data_origin": "synthetic", "synthetic": True}
            service.record_signal(config, [now], [now], [[17.25]], 0, "synthetic-console-test")
            value = snapshot(service)
            self.assertEqual(value["signals"][0]["channels"][0][0][1], 17.25)
            self.assertEqual(value["signals"][0]["config"]["data_origin"], "synthetic")
            value["task"]["config"]["evidenceGuardMode"] = "off"
            self.assertEqual(service.sessions[packet["sessionId"]]["config"]["evidenceGuardMode"], "enforce")
            service.sessions[packet["sessionId"]]["received"] -= 10
            self.assertGreater(snapshot(service)["telemetryAge"], 5)
            other = deepcopy(packet); other["sessionId"] = "synthetic_other"
            service.receive(other)
            self.assertEqual(snapshot(service)["signals"], [])
            self.assertTrue(snapshot(service, packet["sessionId"])["signals"])

    def test_envelope_is_bounded_retains_peaks_and_breaks(self):
        session = {}
        config = {"kind": "EEG", "sample_rate": 1000, "channels": [0]}
        times = [i / 1000 for i in range(12000)]
        values = [[99 if i == 11997 else 0] for i in range(12000)]
        record_wave(session, config, times, values, "test")
        self.assertLessEqual(len(session["console_waves"]["EEG"]["rows"]), 6001)
        result = waves(session, 12)[0]
        self.assertIn(99, [v for _, v in result["channels"][0]])
        self.assertLess(len(result["channels"][0]), 1300)
        record_wave(session, config, [13], [[5]], "test")
        self.assertTrue(any(v is None for _, v in waves(session, 13)[0]["channels"][0]))

    def test_http_assets_snapshot_and_unknown_paths(self):
        with tempfile.TemporaryDirectory() as folder:
            service, packet = fixture(folder)
            server = ThreadingHTTPServer(("127.0.0.1", 0), make_handler(service))
            thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
            base = f"http://127.0.0.1:{server.server_port}"
            try:
                for path in ("/", "/console.js", "/console.css", "/console/health", "/status", "/console/snapshot"):
                    with urlopen(base + path) as response:
                        self.assertEqual(response.status, 200)
                        self.assertEqual(response.headers["Cache-Control"], "no-store")
                for path in ("/../service.py", "/console/snapshot?session=missing"):
                    with self.assertRaises(HTTPError) as error:urlopen(base + path)
                    self.assertEqual(error.exception.code, 404)
                request = Request(base + "/decide", data=json.dumps(packet).encode())
                self.assertEqual(json.load(urlopen(request))["decision"]["action"], "HOLD")
                packet["gazeOverview"] = {"revision": "gaze-console-front-plane-v1", "source": "tracked_eyes",
                    "now": packet["now"], "samples": [{"id": 1, "time": packet["now"], "valid": False,
                    "projected": False, "target": "invalid"}], "layout": {"available": False}}
                request = Request(base + "/telemetry", data=json.dumps(packet).encode())
                with urlopen(request) as response:self.assertEqual(response.status, 200)
                with urlopen(base + "/console/snapshot") as response:
                    self.assertEqual(json.load(response)["task"]["gazeOverview"], packet["gazeOverview"])
                request = Request(base + "/decide", data=json.dumps(packet).encode())
                self.assertEqual(json.load(urlopen(request))["decision"]["action"], "HOLD")
            finally:server.shutdown(); server.server_close(); thread.join()

    def test_empty_service_and_launcher_identity(self):
        with tempfile.TemporaryDirectory() as folder:
            value = snapshot(StudyService(folder))
            self.assertIsNone(value["task"]); self.assertEqual(value["signals"], [])
            from personalized_signals import REVISION as FEATURE_REVISION
            info = {"application": "Evidence Console", "project": str(ROOT), "revision": value["revision"],
                    "featureRevision": FEATURE_REVISION}
            self.assertTrue(compatible(info)); self.assertFalse(compatible(dict(info, revision="old")))
            self.assertFalse(compatible(dict(info, featureRevision="old")))
            self.assertFalse(compatible(dict(info, featureRevision=None)))
            self.assertFalse(compatible(info, Path(folder) / "new-selection.json"))
            selection = Path(folder) / "selection.json"
            content = json.loads(Path(__file__).with_name("lsl-config.example.json").read_text())
            selection.write_text(json.dumps(content))
            info.update(lslSelection="different-equivalent-snapshot.json", lslHash=config_fingerprint(selection, lsl=True), host="127.0.0.1")
            self.assertTrue(compatible(info, selection, host="127.0.0.1"))
            self.assertFalse(compatible(info, selection, host="0.0.0.0"))
            content["streams"][0]["window_seconds"] = 11
            selection.write_text(json.dumps(content))
            self.assertFalse(compatible(info, selection))


if __name__ == "__main__":unittest.main()

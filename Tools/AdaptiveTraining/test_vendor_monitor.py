import json
import threading
import time
import unittest
from websockets.sync.server import serve

from mindtooth_monitor import NeurometricMonitor
import test_model_reuse


class VendorStreamTest(unittest.TestCase):
    def test_local_socket_skips_other_identity_and_expires(self):
        monitor = NeurometricMonitor()
        stop = threading.Event()

        def publish(socket):
            for user, value in (("other", 0.9), ("fixture", 0.2)):
                socket.send(json.dumps(dict(user_id=user, session_id="s1", workload=value, stress=0)))
            stop.wait(4)

        with serve(publish, "127.0.0.1", 0) as server:
            threading.Thread(target=server.serve_forever, daemon=True).start()
            port = server.socket.getsockname()[1]
            worker = threading.Thread(target=monitor.read, args=(f"ws://127.0.0.1:{port}", "fixture", stop))
            worker.start()
            try:
                deadline = time.monotonic() + 2
                while not monitor.snapshot()["values"] and time.monotonic() < deadline:
                    time.sleep(.02)
                self.assertEqual(monitor.snapshot()["values"]["workload"], .2)
                self.assertEqual(monitor.snapshot()["values"]["stress"], 0)
                with monitor.lock:
                    monitor.latest["received"] -= 4
                self.assertEqual(monitor.snapshot(), {"state": "stale", "values": {}})
            finally:
                stop.set()
                worker.join(4)
                server.shutdown()

    def test_assignment_binds_vendor_session_and_holds(self):
        harness = test_model_reuse.ReuseTests()
        harness.setUp()
        try:
            store, service = harness.store, harness.service
            metrics = dict(participant="fixture", session="s1", workload=.7, workload_th=.5, calculatedAt=time.time()*1000, stress=.3, received=time.monotonic())
            request = dict(harness.request, sessionMode="vendor_workload")
            with self.assertRaises(ValueError):
                store.prepare(request)
            record = store.prepare(request, metrics)
            claim = store.claim(dict(clientId="device1", controllerUrl="http://127.0.0.1:8765"))
            from test_personalized import packet
            test = packet()
            test.update(sessionId=record["sessionId"], participantCode="TEST001", sessionType="editor-test",
                        config=claim["config"], stage="bioadaptive", testClientId="device1")
            service.neurometrics.state = "receiving"
            service.neurometrics.latest = metrics
            result = service.receive(test, True)
            self.assertTrue(result["modelTestUsable"])
            self.assertEqual(result["decision"]["action"], "HOLD")
            self.assertEqual(result["decision"]["vendorMetrics"]["workload"], .7)
            from test_personalized import trials
            personal = service.sessions[record["sessionId"]]["personal"]
            rows = trials(25, "bioadaptive")
            personal.trials = {(r["trainingStage"], r["phaseTrial"]): r for r in rows}
            personal.windows[("bioadaptive", 24)] = dict(stage="bioadaptive", valid=True, level=0,
                origin="live_sensor", start=90, trial=24)
            request = dict(test, completedTrials=25)
            personal.vendor_sample = dict(metrics, workload=.2)
            self.assertEqual(personal.decide(request)["action"], "ON")
            personal.vendor_sample = dict(metrics, workload=.5)
            self.assertEqual(personal.decide(request)["action"], "HOLD")
            personal.windows[("bioadaptive", 24)]["level"] = 1
            for row in rows:
                row.update(correct=False, level=1, responded=False)
            personal.intervals = [(0, 110)]
            personal.vendor_sample = metrics
            self.assertEqual(personal.decide(dict(request, level=1))["action"], "OFF")
            personal.vendor_sample = dict(metrics, calculatedAt=(time.time()-20)*1000)
            self.assertEqual(personal.decide(dict(request, level=1))["action"], "HOLD")
            service.neurometrics.latest = dict(metrics, session="different")
            result = service.receive(dict(test, requestId="second"), True)
            self.assertFalse(result["modelTestUsable"])
            self.assertEqual(result["decision"]["vendorMetrics"], {})
        finally:
            harness.tearDown()

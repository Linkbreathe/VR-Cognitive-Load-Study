"""Closed-loop probe regressions with actual base/Guard calls and execution receipts."""

import json
import tempfile
import unittest
from pathlib import Path

from evidence_guard_adapter import GuardAdapter
from layer_ab import PROBE, SLIDING
from personalized_runtime import PersonalSession
from test_layer_ab import ab_packet
from test_personalized import trials


def replay(stage, scenario, *, playback_eyes=None, export=None):
    p = ab_packet(stage)
    p["config"].update(studyDesign=PROBE, trainingTrials=144, slidingRevision=SLIDING)
    level = 0
    rows = []
    decisions = []
    events = []
    active_end = None
    with tempfile.TemporaryDirectory() as tmp:
        if playback_eyes:
            p["config"].update(testSessionMode="comparison_rehearsal", sessionType="engineering", operatorManaged=True)
            p["sessionType"] = "editor-test"
        s = PersonalSession(p, Path(tmp), None, object() if playback_eyes else None)
        s.report = {"admitted": True}  # Injected engineering scores for a paired decision test.
        g = GuardAdapter(p, Path(tmp))
        for i, raw in enumerate(trials(144, stage)):
            if playback_eyes and i == 32:
                break
            n = i + 1
            start = i * 4.5
            close = start + 4
            omitted = scenario == "reopen" and n in (26, 27, 28)
            row = dict(
                raw,
                closeTime=close,
                offsetTime=close,
                level=level,
                responded=not omitted,
                correct=not omitted,
                responseTime=-1 if omitted else start + 1,
            )
            if level and i % 6 == 0 and scenario != "silent":
                actual = start + 0.2
                detail = json.dumps(
                    dict(
                        stage=stage, modality="audiovisual", level=1,
                        block=row["block"],
                        onset=actual,
                        mediaId="clip" + str(i),
                    )
                )
                events.append(
                    dict(type="adaptive_media_started", actual=actual, detail=detail+";first_decoded_frame")
                )
                active_end = (actual + 20, detail)
            if active_end and active_end[0] <= close:
                events.append(
                    dict(
                        type="adaptive_media_ended",
                        actual=active_end[0],
                        detail=active_end[1],
                    )
                )
                active_end = None
            for e in events:
                s.event(e)
            if n >= 6:
                score = 0.35
                if scenario == "reopen" and (26 <= n <= 30 or n >= 49):
                    score = 0.70 if n <= 30 else 0.55
                if scenario == "spike" and n == 30:
                    score = 0.70
                if scenario == "disagree":
                    score = 0.55
                w = rows[-5:]
                s.predictions.append(
                    dict(
                        stage=stage,
                        trial=i - 1,
                        levels=[r["level"] for r in w],
                        level=w[-1]["level"],
                        start=w[0]["plannedOnset"],
                        end=start,
                        valid=n >= 10,
                        score=score,
                        features=[],
                    )
                )
            eyes = [
                dict(
                    id=i * 40 + k,
                    start=start + k * 0.1,
                    end=start + (k + 1) * 0.1,
                    level=level,
                    valid=not (playback_eyes=="missing" and 24 <= n <= 29),
                    target="movie" if 24 <= n <= 29 and ((playback_eyes=="movie" and k < 10) or (playback_eyes=="post" and k >= 10)) else
                           "other" if scenario == "away" else "task",
                    taskVisible=True,
                    movieVisible=bool(playback_eyes and level),
                )
                for k in range(40)
            ]
            p.update(
                trials=rows,
                events=events,
                guardGaze=eyes,
                level=level,
                now=close,
                closedBehavior=dict(row, trialEnd=0),
                completedTrials=n,
                lastTrial=n,
                requestId=f"{stage}-{n}",
                evaluationId=n // 5,
                decisionDeadline=start + 4.5,
            )
            check = n % 5 == 0
            base = s.decide(p) if check else None
            out = g.receive(p, s, base)
            assert not g.failure, g.failure
            events = []
            if export is not None:
                from personalized_runtime import append
                for event in p["events"]:
                    append(Path(tmp) / "events.jsonl", event)
                if check:
                    append(Path(tmp) / "personal_decisions.jsonl", dict(packet=p, decision=base))
            if check:
                assert g.receive(p, s, base) == out
                decisions.append((n, base["action"], out["action"], out["guardReason"]))
                if out["action"] in ("ON", "OFF"):
                    prev = level
                    level = int(out["action"] == "ON")
                    assert prev != level
                    if not level and active_end:
                        events.append(
                            dict(
                                type="adaptive_media_ended",
                                actual=start + 4.5,
                                detail=active_end[1],
                            )
                        )
                        active_end = None
                    events.extend(
                        [
                            dict(
                                type="personal_execution",
                                actual=start + 4.5,
                                detail=f"request={stage}-{n};level={level};previous_level={prev}",
                            ),
                            dict(
                                type="adaptive_level_changed",
                                actual=start + 4.5,
                                detail=f"level={level}",
                            ),
                        ]
                    )
            rows.append(row)
            s.trials[(stage, i)] = row
        if export is not None:
            from comparison_report import report
            export.update(report(Path(tmp)))
    return decisions


class ProbeStudyTests(unittest.TestCase):
    def test_playback_gaze_review_and_archived_report(self):
        for stage in ("layer_a", "layer_b"):
            for eyes, expected_review in (("task","HOLD"), ("missing","OFF"), ("movie","OFF"), ("post","HOLD")):
                with self.subTest(stage=stage, eyes=eyes):
                    exported = {}
                    steps = replay(stage, "spike", playback_eyes=eyes, export=exported)
                    decision = next(v for v in exported["checkpoints"] if v["trial"]==30)
                    self.assertEqual(decision["base"]["action"], "OFF")
                    self.assertEqual(decision["review"]["recommended"], expected_review)
                    expected = "OFF" if stage=="layer_a" else expected_review
                    self.assertEqual(decision["delivered"], expected)
                    self.assertEqual(decision["execution"], "confirmed" if expected=="OFF" else "no change requested")
                    summary = exported["summary"][stage]
                    self.assertEqual(summary["OFF"], sum(row[1]=="OFF" for row in steps))
                    self.assertEqual(summary["deferredOFF"], int(expected_review=="HOLD"))
                    self.assertTrue(decision["review"]["gazeIntervals"])
                    self.assertLessEqual(max(b for a,b in decision["review"]["gazeIntervals"]), decision["time"])
                    trial = next(t for t in exported["trials"] if t["trial"]==27)
                    self.assertLessEqual(max(r[1] for r in trial["preResponse"]),trial["response"])
                    self.assertGreaterEqual(min(r[0] for r in trial["afterResponse"]),trial["response"])
                    if eyes=="post":
                        self.assertTrue(any(r[2]=="movie" for r in trial["afterResponse"]))
                        self.assertFalse(any(r[2]=="movie" for r in trial["preResponse"]))

    def test_report_missing_receipt_and_audio_only_gap(self):
        from comparison_report import report
        from personalized_runtime import append, write_json
        with tempfile.TemporaryDirectory() as tmp:
            folder=Path(tmp)
            write_json(folder/"personal_protocol.json",dict(config={},session="fixture",participant="synthetic"))
            event=dict(block=1,mediaId="speech",onset=2,stage="layer_b",modality="audio")
            for kind,when in (("started",2),("ended",6)):
                append(folder/"events.jsonl",dict(type="adaptive_media_"+kind,actual=when,detail=json.dumps(event)))
            base=dict(requestId="open",action="ON",level=0)
            append(folder/"personal_decisions.jsonl",dict(packet=dict(stage="layer_b",completedTrials=15,now=10),decision=base))
            append(folder/"comparison_deliveries.jsonl",dict(decision=base))
            data=report(folder)
            self.assertEqual(data["checkpoints"][0]["execution"],"unknown")
            self.assertEqual(data["summary"]["layer_b"]["executedChanges"],0)
            self.assertEqual(data["summary"]["layer_b"]["videoSeconds"],0)
            self.assertTrue(data["media"][0]["audio"])

    def test_same_base_observe_and_enforce(self):
        for scenario in ("away", "disagree"):
            a = replay("layer_a", scenario)
            b = replay("layer_b", scenario)
            self.assertTrue(any(r[2] == "ON" for r in a))
            self.assertFalse(any(r[2] == "ON" for r in b))
            self.assertEqual(len(a), 28)

    def test_verified_upgrade_and_movie_coincident_model_spike(self):
        b = replay("layer_b", "spike")
        self.assertIn((25, "HOLD", "HOLD", "probe_passed"), b)
        self.assertIn(
            (30, "OFF", "HOLD", "model_rise_coincides_with_playback_behavior_stable"), b
        )

    def test_inconclusive_probe_does_not_permanently_lock(self):
        b = replay("layer_b", "silent")
        self.assertIn((25, "HOLD", "OFF", "probe_inconclusive_retry_permitted"), b)
        self.assertTrue(any(r[0] > 25 and r[2] == "ON" for r in b))

    def test_reopen_failure_blocks_until_round_end(self):
        b = replay("layer_b", "reopen")
        self.assertIn((30, "OFF", "OFF", "behavior_reduction"), b)
        self.assertIn((40, "ON", "ON", "conditional_reopening_probe"), b)
        self.assertIn((50, "HOLD", "OFF", "probe_failed"), b)
        self.assertTrue(all(r[2] == "HOLD" for r in b if r[0] > 50))

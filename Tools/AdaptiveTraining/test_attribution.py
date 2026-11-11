"""Attribution claims, session-following reverse bands, OFF annotation and linked ON review."""
from pathlib import Path
import sys
import unittest

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EvidenceGuard"))
import attribution
import workload_rf
from comparison_report import evidence_relations
from probe_control import ProbeReview
from evidence_guard import Evidence, EvidenceGuard

BAND = attribution.bands(.56, .60, 2)


class Rules(unittest.TestCase):
    def test_bands_follow_session_cutoffs_and_minimum_change(self):
        self.assertEqual((BAND["stable_max"], BAND["struggling_min"], BAND["thresholdRevision"]), (.56, .60, 2))
        self.assertEqual(attribution.crossing(.549, .604, BAND), "up")
        self.assertEqual(attribution.crossing(.651, .516, BAND), "down")
        self.assertIsNone(attribution.crossing(.57, .59, BAND), "buffer to buffer is not a crossing")
        self.assertIsNone(attribution.crossing(.559, .601, BAND), "a crossing smaller than the minimum change is ignored")
        self.assertIsNone(attribution.crossing(None, .7, BAND))

    def test_candidates_include_playing_clips_and_prefer_larger_overlap(self):
        clips = [dict(id="old", start=0, end=5), dict(id="near", start=12, end=18), dict(id="now", start=20, end=None),
                 dict(id="late", start=40, end=45)]
        found = attribution.candidates(25, 35, clips)
        self.assertEqual([c["id"] for c in found], ["now", "near"])
        self.assertTrue(found[0]["playing"])

    def test_feature_change_uses_model_direction_and_calibration_spread(self):
        ref = dict(loadDirection=[1, -1], withinConditionSd=[.5, .25], revision="r")
        up = attribution.feature_change(ref, [0, 0], [1, -.5], "up")
        self.assertAlmostEqual(up["index"], 2.0)
        self.assertTrue(up["consistent"])
        self.assertFalse(attribution.feature_change(ref, [0, 0], [.2, 0], "up")["consistent"])
        self.assertTrue(attribution.feature_change(ref, [1, -.5], [0, 0], "down")["consistent"])
        self.assertEqual(attribution.feature_change(None, [0], [1], "up")["reason"], "model_has_no_feature_reference")

    def test_claim_bounds_onset_and_names_no_clip_explicitly(self):
        signal = dict(id="s2", score=.62, start=20, end=30, features=None)
        previous = dict(id="s1", score=.52, end=18, features=None)
        made = attribution.claim(signal, previous, BAND, [dict(id="m", start=22, end=None)])
        self.assertEqual((made["direction"], made["claimedEvent"], made["onsetEarliest"], made["onsetLatest"]), ("up", "m", 18, 30))
        self.assertFalse(made["causalClaim"])
        none = attribution.claim(signal, previous, BAND, [])
        self.assertEqual((none["claimedEvent"], none["reason"]), (None, "no_candidate_clip"))
        self.assertIsNone(attribution.claim(dict(signal, score=.58), previous, BAND, []))

    def test_annotation_rules(self):
        self.assertEqual(attribution.behavior_source(3, 3, "during_playback"), "during")
        self.assertEqual(attribution.behavior_source(2, 0, "before_playback"), "before")
        self.assertEqual(attribution.behavior_source(2, 1, "other_or_unknown"), "mixed")
        self.assertEqual(attribution.behavior_source(0, 0, "other_or_unknown"), "none")
        # Clue 2: last score before playback versus the highest score during it, against the OFF line.
        self.assertEqual(attribution.score_source(.549, .613, .60), "during")
        self.assertEqual(attribution.score_source(.607, .617, .60), "before", "already high before the video")
        self.assertEqual(attribution.score_source(.546, .565, .60), "none", "never reached the line")
        self.assertEqual(attribution.score_source(None, .65, .60), "insufficient", "no score before the video")
        self.assertEqual(attribution.score_source(.5, None, .60), "insufficient", "no score during the video")
        self.assertEqual(attribution.combine("during", "before"), "mixed")
        self.assertEqual(attribution.combine("none", "during"), "during")
        self.assertEqual(attribution.combine("none", "unresolved"), "insufficient")
        self.assertEqual(attribution.annotation("none", "before")["result"], "before", "the score clue alone can answer")
        self.assertEqual(attribution.annotation("during", "before")["result"], "mixed")
        self.assertEqual(attribution.annotation("none", "none")["result"], "insufficient")
        self.assertTrue(attribution.requires_verified_reopening("mixed"))
        self.assertFalse(attribution.requires_verified_reopening("before"))
        self.assertFalse(attribution.requires_verified_reopening("insufficient"))

    def test_calibration_feature_reference(self):
        rng = np.random.default_rng(1)
        rest = [rng.uniform(1, 2, (10, 16, 8)) for _ in range(6)]
        arithmetic = [w * 1.5 for w in rest]
        ref = workload_rf.feature_reference(rest, arithmetic, 8, dict(detectedIafHz=8, mode="individual_alpha_peak"))
        self.assertEqual(len(ref["withinConditionSd"]), 8)
        self.assertEqual(set(ref["loadDirection"]), {1})
        self.assertEqual(ref["revision"], workload_rf.FEATURE_REFERENCE_REVISION)


def guard():
    return EvidenceGuard("s", "observe", None, lambda row: None)


def signal(g, name, start, score, **data):
    g.ingest(Evidence(name, "signal", "s", start, start + 10, "rf", 0, "scene", True,
                      dict(score=score, stage="layer_b", **data), g.epoch))


class EngineBands(unittest.TestCase):
    def test_default_bands_are_unchanged(self):
        g = guard()
        signal(g, "a", 0, .55)
        signal(g, "b", 10, .62)
        self.assertNotIn("b", g.reverse_results, "0.55 -> 0.62 stays inside the fixed 0.45/0.65 buffer")
        signal(g, "c", 20, .40)
        signal(g, "d", 30, .70)
        self.assertEqual(g.reverse_results["d"]["bands"]["revision"], "fixed-0.45-0.65")

    def test_per_signal_session_bands_and_minimum_change(self):
        g = guard()
        signal(g, "a", 0, .55, reverse_bands=BAND)
        signal(g, "b", 10, .62, reverse_bands=BAND)
        self.assertEqual(g.reverse_results["b"]["bands"]["stable_max"], .56)
        signal(g, "c", 20, .559, reverse_bands=BAND)
        signal(g, "d", 30, .601, reverse_bands=BAND)
        self.assertNotIn("d", g.reverse_results, "a 0.042 change is below the minimum")


class FakeCore:
    def __init__(self, items, relations):
        self.items, self.relations = items, relations

    def query(self, start, end, kinds):
        return [e for e in self.items if e.start <= end and e.end >= start]


class LinkedOn(unittest.TestCase):
    def test_turn_off_note_is_reused_at_the_next_on(self):
        review = ProbeReview()
        review.reset("layer_b")
        note = attribution.annotation("none", "during", dict(before=.549, peak=.613, line=.60))
        review.enact(40, 1, 0, dict(requestId="r", trial=8, offAnnotation=note))
        linked = review.linked_off(FakeCore([], {}))
        self.assertEqual((linked["annotation"]["result"], linked["basis"], linked["verifiedReopening"]), ("during", "playback_difficulty", True))
        review.enact(60, 0, 1, dict(trial=12))
        self.assertIsNone(review.off_context, "an executed ON consumes the linked OFF")

    def test_difficulty_before_the_video_allows_an_ordinary_opening(self):
        review = ProbeReview()
        review.reset("layer_b")
        review.enact(40, 1, 0, dict(requestId="r", trial=8, offAnnotation=attribution.annotation("none", "before")))
        linked = review.linked_off(FakeCore([], {}))
        self.assertEqual((linked["basis"], linked["verifiedReopening"]), ("none", False))

    def test_off_during_verification_keeps_the_existing_rule(self):
        review = ProbeReview()
        review.reset("layer_b")
        review.enact(10, 0, 1, dict(trial=4))
        review.enact(20, 1, 0, dict(requestId="r", trial=6, offAnnotation=attribution.annotation("none", "none")))
        linked = review.linked_off(FakeCore([], {}))
        self.assertEqual((linked["basis"], linked["verifiedReopening"]), ("off_during_verification", True))


class ReportProjection(unittest.TestCase):
    def test_relations_attach_to_the_decision_window(self):
        timeline = [dict(stage="layer_b", start=4.5 * i, trial=i + 1) for i in range(12)]
        claim = dict(direction="up", claimedEvent="media:x", onsetLatest=30)
        signals = {"signal:layer_b:6": dict(start=20, end=30, stage="layer_b", score=.62, quality=True, level=1, bands=BAND, claim=claim),
                   "signal:layer_b:4": dict(start=10, end=19, stage="layer_b", score=.52, quality=True, level=1, bands=BAND, claim=None)}
        clips = {"media:x": dict(id="media:x", clip="continuous-comparison-1-exposure-1", stage="layer_b", start=22, end=40,
                                 actual=True, audio=True, level=1)}
        relations = {"media:x::signal:layer_b:6": dict(id="media:x::signal:layer_b:6", event="media:x", signal="signal:layer_b:6",
                     state="SUPPORTED", directions=["forward"], actual_seconds=18, time_correspondence=True,
                     visual_exposure=False, audio_confirmed=True, alternatives=["task"], causal_claim=False)}
        checkpoints = [dict(stage="layer_b", base=dict(predictionTrial=6))]
        result = evidence_relations(timeline, checkpoints, signals, clips, relations, {"signal:layer_b:6": dict(signal="signal:layer_b:6")})
        view = checkpoints[0]["evidenceRelations"]
        self.assertEqual((view["previousScore"], view["claim"]["relationState"], view["relations"][0]["state"]), (.52, "SUPPORTED", "SUPPORTED"))
        summary = result["summary"]["layer_b"]
        self.assertEqual((summary["clips"], summary["reverse"]["total"], summary["reverse"]["withClip"]), (1, 1, 1))
        self.assertEqual(result["clips"]["layer_b"][0]["startTrial"], 5)


if __name__ == "__main__":
    unittest.main()

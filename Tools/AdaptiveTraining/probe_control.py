"""Common OR controller and stateful authorization for new 144-trial A/B allocations."""

import math
import comparison_environment

from personalized_signals import movie_fraction
from layer_ab import COMPARISON_MODES, COMPACT_POLICY, compact


def summarize(rows):
    matches = [r for r in rows if r["expectedMatch"]]
    others = [r for r in rows if not r["expectedMatch"]]
    ba = None
    if matches and others:
        ba = 0.5 * sum(
            sum(bool(r["correct"]) for r in group) / len(group)
            for group in (matches, others)
        )
    return dict(count=len(rows), ba=ba, omissions=sum(not r["responded"] for r in rows),
                matches=len(matches), nonmatches=len(others), classCoverage=bool(matches and others))


def base_decision(session, packet, decision, current):
    short = compact(session.config)
    size, omissions = (5, 2) if short else (10, 3)
    rows = [
        r
        for r in current
        if r["scored"]
        and not r["interrupted"]
        and not r["aborted"]
        and r["plannedOnset"] >= session.barrier
    ][-size:]
    behavior = summarize(rows)
    ba = behavior["ba"]
    full = len(rows) == size
    stable = full and ba is not None and ba >= 0.80 and (not short or behavior["omissions"] < omissions)
    difficult = full and (behavior["omissions"] >= omissions or ba is not None and ba <= 0.60)
    count = packet["completedTrials"]
    latest = next(
        (
            r
            for r in reversed(session.predictions)
            if r["stage"] == packet["stage"] and r["trial"] == count - 2
        ),
        None,
    )
    score = None
    if (
        session.report
        and (session.report.get("admitted") or (session.config.get("testSessionMode") == "comparison_candidate" and session.candidate_control))
        and latest
        and latest["valid"]
        and latest.get("score") is not None
        and math.isfinite(latest["score"])
        and latest["start"] >= session.barrier
        and abs(latest["end"] - current[-1]["plannedOnset"]) <= 0.1
        and 0
        <= packet.get("clockUncertainty", 999)
        <= session.policy["max_clock_uncertainty"]
    ):
        score = latest["score"]
    up = packet["level"] == 0
    behavior_trigger = stable if up else difficult
    low, high = session.score_thresholds["on"], session.score_thresholds["off"]
    model_trigger = score is not None and (score <= low if up else score >= high)
    action = ("ON" if up else "OFF") if behavior_trigger or model_trigger else "HOLD"
    behavior.update(
        valid=full and ba is not None,
        windowTrials=size, omissionThreshold=omissions,
        reason="insufficient_trials" if not full else "missing_response_class" if ba is None else "ok",
        state="struggling" if difficult else "stable" if stable else "undetermined",
    )
    decision.update(
        gazeInput=comparison_environment.provenance(session.config),
        action=action,
        reason="any_threshold_met" if action != "HOLD" else "thresholds_not_met",
        basis="probe_or_controller",
        policyRevision=COMPACT_POLICY if short else "behavior10-or-probe-v2",
        behaviorRevision="scored5-or-v3" if short else "scored10-or-v2",
        behavior=behavior,
        behaviorTrigger=behavior_trigger,
        modelTrigger=model_trigger,
        score=score,
        predictionTrial=count - 2 if score is not None else -1,
        physiologicalEligible=score is not None and bool(session.report.get("admitted")),
        engineeringModelEligible=score is not None and session.candidate_control,
    )
    if session.workload_control:
        decision.update(scoreSource="rest-math-iaf-rf-v2", scoreMeaning="arithmetic_class_score",
                        artifactScreening=False, scorePolicy="engineering-workload-session-cutoffs-v3", lowThreshold=low, highThreshold=high)
    return decision


# Frozen engineering thresholds for the overlapping, pre-response playback interval.
PLAYBACK_POLICY = "pre-response-movie-gaze-v1"
MIN_VIEW_SECONDS = 1.0
MIN_EYE_COVERAGE = 0.70
MIN_TASK_SHARE = 0.60


def response_end(row):
    end = min(row["actualOnset"] + 4, row["closeTime"])
    if row.get("offsetTime", 0) > 0:
        end = min(end, row["offsetTime"])
    return min(end, row["responseTime"]) if row["responded"] else end


def evidence_checks(decision, review, config):
    """Document the rule branch evaluated at this checkpoint, using decision-time data."""
    checks = []

    def add(name, value, operator, threshold, denominator=None):
        if value is None:
            status = "missing"
        else:
            passed = {">=": value >= threshold, "<=": value <= threshold, "<": value < threshold,
                      ">": value > threshold}[operator]
            status = "met" if passed else "outside threshold"
        checks.append(dict(name=name, value=value, operator=operator, threshold=threshold,
                           denominator=denominator, status=status))

    low, high = decision.get("lowThreshold", .45), decision.get("highThreshold", .65)
    level = decision["level"]
    base = decision["behavior"]
    limits = review.get("thresholds", {})
    size, omissions = limits.get("behaviorTrials", 10), limits.get("omissions", 3)
    add("Base balanced accuracy", base["ba"] if base["valid"] else None, ">=" if level==0 else "<=", .80 if level==0 else .60, base["count"])
    if level==1:
        add("Base omissions", base["omissions"], ">=", omissions, base["count"])
    add("Match trials", base.get("matches"), ">=", 1, base["count"])
    add("Non-match trials", base.get("nonmatches"), ">=", 1, base["count"])
    add("Base model score", decision.get("score"), "<=" if level==0 else ">=", low if level==0 else high)
    reason = review["reason"]
    if decision["action"]=="ON" or reason.startswith("probe_"):
        verification = "verificationBehavior" in review
        behavior = review.get("verificationBehavior", review["applicableBehavior"])
        add("Current-condition eligible trials", behavior["count"], ">=", 10 if verification else size)
        add("Current-condition balanced accuracy", behavior["ba"], ">=", .80, behavior["count"])
        add("Current-condition omissions", behavior["omissions"], "<", 3 if verification else omissions, behavior["count"])
        maintain = verification and limits.get("verificationScoreOperator") == "<"
        add("Applicable model score", review["score"], "<" if maintain else "<=", high if maintain else low)
    if "playbackSeconds" in review:
        add("Playback in verification (seconds)", review["playbackSeconds"], ">=", 15)
    if "modelRise" in review:
        add("Prior nonoverlapping score", review["modelRise"]["priorScore"], "<", high)
        add("Current score", review["modelRise"]["score"], ">=", high)
        behavior = review["applicableBehavior"]
        add("Behavior permits OFF review (BA)", behavior["ba"], limits.get("deferBAOperator", ">"), limits.get("deferBA", .60), behavior["count"])
        add("Behavior permits OFF review (omissions)", behavior["omissions"], "<", omissions, behavior["count"])
    if "playbackGaze" in review:
        view = review["playbackGaze"]
        add("Pre-response movie overlap (seconds)", view["duration"], ">=", MIN_VIEW_SECONDS)
        add("Valid eye coverage", view["gaze_valid_ratio"], ">=", MIN_EYE_COVERAGE, view["duration"])
        add("Task share of valid eye time", view["task_gaze_ratio"], ">=", MIN_TASK_SHARE, view["valid_seconds"])
    elif "gaze" in review:
        view = review["gaze"]
        add("Valid eye coverage", view["gaze_valid_ratio"], ">=", config.min_gaze_valid, view["duration"])
        add("Task share of valid eye time", view["task_gaze_ratio"], ">=", config.min_task_gaze, view["valid_seconds"])
    return checks


class ProbeReview:
    def __init__(self):
        self.stage = None
        self.reset(None)

    def reset(self, stage):
        self.stage = stage
        self.since = float("-inf")
        self.reopen = False
        self.off_record = None
        self.probe_at = None
        self.locked = False
        self.failed_media = False

    def enact(self, when, before, after, review):
        if before == after:
            return
        self.since = when
        self.failed_media = False
        if after == 0:
            self.off_record = review.get("requestId")
            self.reopen = (
                review.get("source") == "during_playback" or self.probe_at is not None
            )
            self.probe_at = None
        else:
            self.probe_at = review["trial"]
            self.reopen = False

    def assess(self, packet, session, decision, core, scene):
        short = compact(session.config)
        size, omissions = (5, 2) if short else (10, 3)
        low, high = decision.get("lowThreshold", .45), decision.get("highThreshold", .65)
        n = packet["completedTrials"]
        stage = packet["stage"]
        rows = sorted(
            (
                r
                for r in session.trials.values()
                if r["trainingStage"] == stage and r["phaseTrial"] < n - 1
            ),
            key=lambda r: r["phaseTrial"],
        )
        rows = (rows + [packet["closedBehavior"]])[-10:]
        rows = [
            r
            for r in rows
            if r["level"] == packet["level"]
            and r["actualOnset"] >= max(self.since, session.barrier)
            and r["scored"]
            and not r["aborted"]
            and not r["interrupted"]
        ]
        verification_rows = rows
        rows = rows[-size:]
        preds = [
            r
            for r in session.predictions
            if r["stage"] == stage
            and r["valid"]
            and r.get("score") is not None
            and r["start"] >= max(self.since, session.barrier)
            and all(v == packet["level"] for v in r["levels"])
        ]
        score = (
            decision.get("score")
            if preds and preds[-1]["trial"] == decision.get("predictionTrial")
            else None
        )
        behavior = summarize(rows)
        ba = behavior["ba"]
        stable = (
            len(rows) == size
            and ba is not None
            and ba >= 0.8
            and behavior["omissions"] < omissions
        )
        intervals = session.intervals + [
            (a, packet["now"]) for a in session.playing.values()
        ]
        gaze_review = session.config.get("testSessionMode") in COMPARISON_MODES
        if gaze_review:
            intervals = session.video_intervals + [(a, packet["now"]) for a in session.video_playing.values()]
        failures = [r for r in rows if not r["correct"]]
        exposed = sum(
            any(
                a <= (r["responseTime"] if r["responded"] else r["closeTime"]) <= b
                for a, b in intervals
            )
            for r in failures
        )
        recent_intervals = [
            (a, b) for a, b in intervals if a >= self.since and b > session.barrier
        ]
        source = "other_or_unknown"
        if failures and exposed > len(failures) / 2:
            source = "during_playback"
        elif (
            failures
            and recent_intervals
            and all(
                r["closeTime"] < min(a for a, b in recent_intervals) for r in failures
            )
        ):
            source = "before_playback"
        action = decision["action"]
        result = dict(
            trial=n,
            source=source,
            failures=len(failures),
            exposedFailures=exposed,
            score=score,
            applicableBehavior=behavior,
            probeStart=self.probe_at,
            previousOff=self.off_record,
            requestId=packet["requestId"],
            windowTrials=[r["phaseTrial"] for r in rows],
            validPredictionTrial=preds[-1]["trial"] if score is not None else None,
        )
        if gaze_review:
            result.update(reviewPolicy="pre-response-continuous-gaze-v2" if short else PLAYBACK_POLICY, stage=stage, time=packet["now"],
                          predictionWindow=[preds[-1]["start"], preds[-1]["end"]] if score is not None else None,
                          thresholds=dict(onBA=.80, offBA=.60, omissions=omissions, behaviorTrials=size,
                                          deferBA=.80 if short else .60, deferBAOperator=">=" if short else ">",
                                          verificationScoreOperator="<" if short else "<=", onScore=low, offScore=high,
                                          onEyeCoverage=core.config.min_gaze_valid, onTaskShare=core.config.min_task_gaze,
                                          verificationTrials=10, verificationPlaybackSeconds=15,
                                          playbackSecondsMin=MIN_VIEW_SECONDS, eyeCoverageMin=MIN_EYE_COVERAGE,
                                          taskShareMin=MIN_TASK_SHARE))
        if decision["action"] == "OFF":
            signal_id = f"signal:{stage}:{decision.get('predictionTrial', -1)}"
            result["relations"] = [
                dict(id=r["id"], directions=r["directions"], state=r["state"])
                for r in core.relations.values()
                if r["signal"] == signal_id
            ]
            result["playbackIntervals"] = [
                (a, b)
                for a, b in recent_intervals
                if rows and b >= rows[0]["actualOnset"]
            ]

        def finish(effective, reason):
            result["gazeInput"] = comparison_environment.provenance(session.config)
            result.update(recommended=effective, reason=reason)
            if gaze_review:
                result["checks"] = evidence_checks(decision, result, core.config)
            return result

        if self.locked:
            return finish(
                "OFF" if packet["level"] else "HOLD", "failed_probe_phase_lock"
            )
        if self.probe_at is not None and n >= self.probe_at + 10:
            ids = [r["phaseTrial"] for r in verification_rows]
            verification = summarize(verification_rows)
            result["verificationBehavior"] = verification
            result["verificationWindowTrials"] = ids
            verification_stable = len(verification_rows) == 10 and verification["ba"] is not None and verification["ba"] >= .8 and verification["omissions"] < 3
            seconds = 0
            if ids == list(range(n - 10, n)):
                start = verification_rows[0]["actualOnset"]
                seconds = movie_fraction(start, packet["now"], intervals) * (
                    packet["now"] - start
                )
            result["playbackSeconds"] = seconds
            if n != self.probe_at + 10:
                return finish("OFF", "probe_checkpoint_missed_retry_permitted")
            if self.failed_media or seconds + 1e-6 < 15 or score is None or verification["ba"] is None:
                return finish("OFF", "probe_inconclusive_retry_permitted")
            if not verification_stable or (score >= high if short else score > low):
                self.locked = True
                return finish("OFF", "probe_failed")
            self.probe_at = None
            return finish("HOLD", "probe_passed")
        if action == "ON":
            if n + 10 >= session.config["trainingTrials"]:
                return finish("HOLD", "insufficient_remaining_probe_checkpoints")
            if not stable or score is None or score > low:
                return finish("HOLD", "current_condition_agreement_missing")
            view = comparison_environment.gaze(
                session.config, core, [(r["actualOnset"], r["closeTime"]) for r in rows], 0, scene
            )
            result["gaze"] = view
            if (
                not view["available"]
                or view["gaze_valid_ratio"] < core.config.min_gaze_valid
                or view["task_gaze_ratio"] < core.config.min_task_gaze
            ):
                if gaze_review:
                    reason = "eye_tracking_insufficient" if not view["available"] or view["gaze_valid_ratio"] < core.config.min_gaze_valid else "task_view_insufficient"
                    return finish("HOLD", reason)
                return finish("HOLD", "task_view_insufficient")
            return finish(
                "ON",
                "conditional_reopening_probe"
                if self.reopen
                else "current_condition_supported",
            )
        current_behavior_off = (
            len(rows) == size and (behavior["omissions"] >= omissions
            or len(rows) == size
            and ba is not None
            and ba <= 0.60)
        )
        if action == "OFF" and not current_behavior_off:
            if not decision["modelTrigger"]:
                return finish("HOLD", "behavior_evidence_predates_switch")
            if score is None:
                return finish("HOLD", "model_window_not_current_condition")
            behavior_normal = (
                len(rows) == size
                and ba is not None
                and (ba >= .80 if short else ba > .60)
                and behavior["omissions"] < omissions
            )
            if behavior_normal and len(preds) >= 2:
                latest = preds[-1]
                prior = next(
                    (r for r in reversed(preds[:-1]) if r["end"] <= latest["start"]),
                    None,
                )
                # A window crossing is a temporal proxy. Do not infer a change onset from an isolated high score.
                coincident = (
                    prior
                    and prior["score"] < high
                    and any(latest["start"] <= a < latest["end"] for a, b in intervals)
                )
                if coincident:
                    result["modelRise"] = dict(
                        priorScore=prior["score"],
                        priorEnd=prior["end"],
                        score=score,
                        windowStart=latest["start"],
                        windowEnd=latest["end"],
                        playbackOnsets=[
                            a
                            for a, b in intervals
                            if latest["start"] <= a < latest["end"]
                        ],
                    )
                    result["source"] = "during_playback"
                    if gaze_review:
                        # Only decoded playback overlapping this score window, before an answer.
                        overlap = []
                        for row in rows:
                            for start, stop in recent_intervals:
                                a = max(start, row["actualOnset"], latest["start"])
                                b = min(stop, response_end(row), latest["end"], packet["now"])
                                if a < b:
                                    overlap.append((a, b))
                        view = comparison_environment.gaze(session.config, core, overlap, packet["level"], scene)
                        result.update(playbackGaze=view, gazeIntervals=overlap)
                        if view["duration"] < MIN_VIEW_SECONDS:
                            return finish("OFF", "insufficient_pre_response_playback")
                        if not view["available"] or view["gaze_valid_ratio"] < MIN_EYE_COVERAGE:
                            return finish("OFF", "playback_eye_tracking_insufficient")
                        if view["task_gaze_ratio"] < MIN_TASK_SHARE:
                            return finish("OFF", "playback_task_view_insufficient")
                        return finish("HOLD", "playback_score_rise_with_stable_behavior_and_task_gaze")
                    return finish(
                        "HOLD", "model_rise_coincides_with_playback_behavior_stable"
                    )
        return finish(
            action,
            ("behavior_reduction" if current_behavior_off else "model_reduction")
            if action == "OFF"
            else "base_hold",
        )

"""Shared OR controller, current-condition review and legacy 144-trial verification."""

import math
import attribution
import comparison_environment
import workload_rf

from personalized_signals import movie_fraction
from layer_ab import COMPARISON_MODES, COMPACT_POLICY, COMPACT_REVIEW, COMPACT_BEHAVIOR_REVISION, COMPACT_ON_BA, COMPACT_OFF_BA, compact, guard_mode

# Round B of the 60-trial comparison only: selective support invalidation and a logged probe.
SUPPORT_POLICY = "selective-support-probe4-cooldown2-v1"
PROBE_TRIALS = 4           # scored trials with the video on, judged at the two following checks
PROBE_ERRORS = 2           # mistakes or omissions among those trials that fail the probe
BASE_OFF_TRIALS = 5        # current-OFF eligible trials required before an ON
COOLDOWN_STEP = 2          # added after each failed probe
MAX_OFF_TRIALS = 7


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
    stable_ba = COMPACT_ON_BA if short and packet["level"] == 0 else .80
    stable = full and ba is not None and ba >= stable_ba and (not short or behavior["omissions"] < omissions)
    difficult = full and (behavior["omissions"] >= omissions or ba is not None and ba <= (COMPACT_OFF_BA if short else .60))
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
        behaviorRevision=COMPACT_BEHAVIOR_REVISION if short else "scored10-or-v2",
        behaviorThresholds=dict(onBA=COMPACT_ON_BA if short else .80, offBA=COMPACT_OFF_BA if short else .60, omissions=omissions),
        behavior=behavior,
        behaviorTrigger=behavior_trigger,
        modelTrigger=model_trigger,
        score=score,
        predictionTrial=count - 2 if score is not None else -1,
        physiologicalEligible=score is not None and bool(session.report.get("admitted")),
        engineeringModelEligible=score is not None and session.candidate_control,
    )
    if session.workload_control:
        decision.update(scoreSource=workload_rf.REVISION, scoreMeaning="arithmetic_class_score",
                        workloadWindowSeconds=workload_rf.WINDOW_SECONDS,
                        artifactScreening=False, scorePolicy="engineering-workload-session-cutoffs-v3", lowThreshold=low, highThreshold=high)
    return decision


# Frozen engineering thresholds for the overlapping, pre-response playback interval.
PLAYBACK_POLICY = "pre-response-movie-gaze-v1"
MIN_VIEW_SECONDS = 1.0
GAZE_REVIEW_REVISION = "gaze-review-coverage60-task40-v1"
MIN_EYE_COVERAGE = 0.60
MIN_TASK_SHARE = 0.40


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
    base_limits = decision.get("behaviorThresholds", limits)
    add("Base balanced accuracy", base["ba"] if base["valid"] else None, ">=" if level==0 else "<=",
        base_limits.get("onBA", .80) if level==0 else base_limits.get("offBA", .60), base["count"])
    if level==1:
        add("Base omissions", base["omissions"], ">=", omissions, base["count"])
    add("Match trials", base.get("matches"), ">=", 1, base["count"])
    add("Non-match trials", base.get("nonmatches"), ">=", 1, base["count"])
    add("Base model score", decision.get("score"), "<=" if level==0 else ">=", low if level==0 else high)
    reason = review["reason"]
    if decision["action"]=="ON" or reason.startswith("probe_"):
        verification = "verificationBehavior" in review
        behavior = review.get("verificationBehavior", review["applicableBehavior"])
        add("Current-condition eligible trials", review.get("currentConditionTrials", behavior["count"]), ">=",
            10 if verification else limits.get("requiredOffTrials", size))
        add("Current-condition balanced accuracy", behavior["ba"], ">=", .80 if verification else limits.get("onBA", .80), behavior["count"])
        add("Current-condition omissions", behavior["omissions"], "<", 3 if verification else omissions, behavior["count"])
        maintain = verification and limits.get("verificationScoreOperator") == "<"
        add("Applicable model score", review["score"], "<" if maintain else "<=", high if maintain else low)
    if "playbackSeconds" in review:
        add("Playback in verification (seconds)", review["playbackSeconds"], ">=", 15)
    probe = review.get("support", {}).get("probeEvaluation")
    if probe:
        add("Probe trials completed (video on)", probe["completed"], ">=", PROBE_TRIALS)
        add("Probe mistakes and omissions", probe["errors"], "<", PROBE_ERRORS, probe["completed"])
        if probe["completed"] >= PROBE_TRIALS:
            add("Decoded playback in probe (seconds)", probe["playbackSeconds"], ">", 0)
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
    def __init__(self, config=None):
        self.config = config or {}
        self.verify_openings = not compact(self.config)
        self.stage = None
        self.reset(None)

    def reset(self, stage):
        self.stage = stage
        self.since = float("-inf")
        self.reopen = False
        self.off_record = None
        self.off_context = None
        self.probe_at = None
        self.locked = False
        self.failed_media = False
        # Selective support (applied Round B of the 60-trial comparison only).
        self.support_applied = bool(stage) and compact(self.config) and guard_mode(self.config, stage) == "enforce"
        self.support_invalidated = False  # cross_condition_on_support_invalidated
        self.required_off_trials = BASE_OFF_TRIALS
        self.probe_failures = 0
        self.probe_inconclusive = 0
        self.on_kind = None  # Normal_ON, Probe_ON or Restored_ON while the video is on
        self.support_probe = None
        self.support_events = []

    def support_state(self, level):
        state = "OFF" if level == 0 else self.on_kind or "Normal_ON"
        return dict(policy=SUPPORT_POLICY, state=state, invalidated=self.support_invalidated,
                    requiredOffTrials=self.required_off_trials, probeFailures=self.probe_failures,
                    probeInconclusive=self.probe_inconclusive,
                    probe=dict(self.support_probe) if self.support_probe else None)

    def _support_event(self, kind, when, review, **detail):
        event = dict(event=kind, time=when, request=review.get("requestId"), trial=review.get("trial"), **detail)
        self.support_events.append(event)
        return event

    def enact(self, when, before, after, review):
        if before == after:
            return
        self.since = when
        self.failed_media = False
        if after == 0:
            self.off_record = review.get("requestId")
            # The note is final at OFF and retained as history for the next ON review.
            self.off_context = dict(request=review.get("requestId"), trial=review.get("trial"), time=when,
                                    duringVerification=self.probe_at is not None,
                                    annotation=review.get("offAnnotation"))
            self.reopen = False
            self.probe_at = None
            if self.support_applied:
                note = (review.get("offAnnotation") or {}).get("result")
                if self.support_probe is not None and (review.get("support") or {}).get("outcome") == "Probe_Inconclusive":
                    # Playback failure or no decoded playback: no comparable evidence, so support stays
                    # suspended, but this is not a participant failure and adds no cooldown.
                    self.probe_inconclusive += 1
                    self.support_invalidated = True
                    self._support_event("Probe_Inconclusive", when, review, reason=review.get("reason"),
                                        probeRequest=self.support_probe["request"],
                                        requiredOffTrials=self.required_off_trials)
                elif self.support_probe is not None:
                    # Any other executed OFF during a probe is a probe failure; support stays invalidated.
                    self.probe_failures += 1
                    self.required_off_trials = min(MAX_OFF_TRIALS, self.required_off_trials + COOLDOWN_STEP)
                    self.support_invalidated = True
                    self._support_event("Probe_Failed", when, review, reason=review.get("reason"),
                                        probeRequest=self.support_probe["request"],
                                        requiredOffTrials=self.required_off_trials)
                elif attribution.requires_verified_reopening(note):
                    # Mixed is treated like difficulty during playback (approved conservative rule).
                    self.support_invalidated = True
                    self._support_event("Support_Invalidated", when, review, annotation=note,
                                        requiredOffTrials=self.required_off_trials)
                self.support_probe = None
                self.on_kind = None
                if self.off_context is not None:
                    self.off_context["supportInvalidated"] = self.support_invalidated
                    self.off_context["requiredOffTrials"] = self.required_off_trials
        else:
            self.probe_at = review["trial"] if self.verify_openings else None
            self.reopen = False
            self.off_context = None
            if self.support_applied:
                kind = (review.get("support") or {}).get("authorization") or (
                    "Probe_ON" if self.support_invalidated else "Normal_ON")
                self.on_kind = kind
                self.support_probe = (dict(request=review.get("requestId"), trial=review.get("trial"), time=when)
                                      if kind == "Probe_ON" else None)
                self._support_event(kind, when, review, requiredOffTrials=self.required_off_trials)

    def claims(self, core, stage, start, end):
        """Recorded attribution claims for this round's score windows ending in [start, end], with relation states."""
        found = []
        query, relations = getattr(core, "query", None), getattr(core, "relations", {})
        if query is None:
            return found  # No evidence index (for example a minimal host): no claims, physiology "none".
        for e in query(start, end, {"signal"}):
            claim = e.data.get("attribution")
            if not claim or e.data.get("stage") != stage or not start <= e.end <= end:
                continue
            state = relations.get(f"{claim['claimedEvent']}::{e.id}", {}).get("state") if claim["claimedEvent"] else None
            found.append(dict(claim, relationState=state))
        return found

    def linked_off(self, core, *, annotation_only=False):
        """The latest executed OFF's turn-off note. It was final when written, so it is reused as recorded."""
        context = self.off_context
        resolved = dict((context or {}).get("annotation") or attribution.annotation("none", "insufficient"))
        if annotation_only and self.support_applied:
            return dict(request=context["request"], trial=context["trial"], time=context["time"],
                        annotation=resolved, rule="selective_support_probe",
                        supportInvalidated=context.get("supportInvalidated", False),
                        requiredOffTrials=context.get("requiredOffTrials", BASE_OFF_TRIALS))
        if annotation_only:
            return dict(request=context["request"], trial=context["trial"], time=context["time"],
                        annotation=resolved, rule="annotation_only")
        by_annotation = attribution.requires_verified_reopening(resolved["result"])
        basis = ("playback_difficulty" if by_annotation else
                 "off_during_verification" if context["duringVerification"] else "none")
        return dict(request=context["request"], trial=context["trial"], time=context["time"], annotation=resolved,
                    duringVerification=context["duringVerification"], verifiedReopening=basis != "none",
                    basis=basis, rule="verified_reopening_after_playback_difficulty")

    def assess(self, packet, session, decision, core, scene):
        short = compact(session.config)
        self.verify_openings = not short
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
        eligible = lambda r: (r["level"] == packet["level"]
                              and r["actualOnset"] >= max(self.since, session.barrier)
                              and r["scored"] and not r["aborted"] and not r["interrupted"])
        current_all = [r for r in rows + [packet["closedBehavior"]] if eligible(r)]
        rows = [r for r in (rows + [packet["closedBehavior"]])[-10:] if eligible(r)]
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
            and ba >= (COMPACT_ON_BA if short else .80)
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
            previousOff=self.off_record,
            requestId=packet["requestId"],
            windowTrials=[r["phaseTrial"] for r in rows],
            validPredictionTrial=preds[-1]["trial"] if score is not None else None,
        )
        if not short:
            result["probeStart"] = self.probe_at
        support = short and guard_mode(session.config, stage) == "enforce"
        self.support_applied = support
        probe_eval = None
        if support:
            result["currentConditionTrials"] = len(current_all)
            result["support"] = self.support_state(packet["level"])
            if packet["level"] == 1 and self.support_probe is not None:
                probe_rows = [r for r in current_all if r["actualOnset"] >= self.support_probe["time"]][:PROBE_TRIALS]
                start = self.support_probe["time"]
                played = movie_fraction(start, packet["now"], intervals) * (packet["now"] - start) if packet["now"] > start else 0.
                probe_eval = dict(trials=[r["phaseTrial"] for r in probe_rows], completed=len(probe_rows),
                                  errors=sum(not r["correct"] for r in probe_rows),
                                  omissions=sum(not r["responded"] for r in probe_rows),
                                  playbackSeconds=played, playbackFailure=self.failed_media)
                result["support"]["probeEvaluation"] = probe_eval
        if gaze_review:
            result.update(reviewPolicy=COMPACT_REVIEW if short else PLAYBACK_POLICY,
                          gazeReviewRevision=GAZE_REVIEW_REVISION, stage=stage, time=packet["now"],
                          predictionWindow=[preds[-1]["start"], preds[-1]["end"]] if score is not None else None,
                          thresholds=dict(onBA=COMPACT_ON_BA if short else .80, offBA=COMPACT_OFF_BA if short else .60, omissions=omissions, behaviorTrials=size,
                                          deferBA=.80 if short else .60, deferBAOperator=">=" if short else ">",
                                          onScore=low, offScore=high,
                                          onEyeCoverage=core.config.min_gaze_valid, onTaskShare=core.config.min_task_gaze,
                                          playbackSecondsMin=MIN_VIEW_SECONDS, eyeCoverageMin=MIN_EYE_COVERAGE,
                                          taskShareMin=MIN_TASK_SHARE))
            if not short:
                result["thresholds"].update(verificationScoreOperator="<=", verificationTrials=10,
                                            verificationPlaybackSeconds=15)
            if support:
                result["thresholds"].update(requiredOffTrials=self.required_off_trials, probeTrials=PROBE_TRIALS,
                                            probeErrors=PROBE_ERRORS, cooldownStep=COOLDOWN_STEP,
                                            maxOffTrials=MAX_OFF_TRIALS,
                                            lastProbeOnTrial=session.config["trainingTrials"] - 2 - PROBE_TRIALS)
        def off_note():
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
            # Turn-off note (A and B alike): did the difficulty start while the video was playing?
            # Recorded only; it never delays the OFF.
            now = packet["now"]
            began = max((a for a, b in intervals if a <= now), default=None)
            scored = [r for r in session.predictions if r["stage"] == stage and r.get("valid")
                      and attribution.finite(r.get("score")) and r["end"] <= now]
            prior = max((r for r in scored if began is not None and r["end"] <= began), key=lambda r: r["end"], default=None)
            during = [r for r in scored if began is not None and r["end"] > began]
            peak = max(during, key=lambda r: r["score"], default=None)
            evidence = dict(playbackStart=began, line=high,
                            before=prior["score"] if prior else None, beforeEnd=prior["end"] if prior else None,
                            peak=peak["score"] if peak else None, peakEnd=peak["end"] if peak else None)
            result["offAnnotation"] = dict(attribution.annotation(
                attribution.behavior_source(len(failures), exposed, source),
                attribution.score_source(evidence["before"], evidence["peak"], high), evidence),
                window=dict(stage=stage, start=rows[0]["actualOnset"] if rows else now - size * 4.5, end=now))

        if decision["action"] == "OFF":
            off_note()

        def finish(effective, reason, probe_final=False):
            if probe_eval is not None and not probe_final:
                # Probe ON: the ordinary OFF review still decides OFF; any OFF here fails the probe.
                if effective == "OFF":
                    result["ordinaryReason"] = reason
                    reason = "support_probe_failed_behavior" if reason == "behavior_reduction" else "support_probe_failed_model"
                elif probe_eval["completed"] >= PROBE_TRIALS:
                    result["ordinaryReason"] = reason
                    if probe_eval["playbackSeconds"] <= 0:
                        effective, reason = "OFF", "support_probe_inconclusive_no_decoded_playback"
                    else:
                        # Authorization Restored via Logged Probe: comparable evidence re-acquired with the video on.
                        effective, reason = "HOLD", "support_probe_passed"
                        self.support_invalidated = False
                        self.required_off_trials = BASE_OFF_TRIALS
                        self.on_kind = "Restored_ON"
                        self._support_event("Probe_Passed", packet["now"], result,
                                            probeRequest=self.support_probe["request"],
                                            audit="Authorization Restored via Logged Probe")
                        self.support_probe = None
                        result["support"]["outcome"] = "Probe_Passed"
                        result["support"]["audit"] = "Authorization Restored via Logged Probe"
            if probe_eval is not None and effective == "OFF":
                if "offAnnotation" not in result:
                    off_note()  # A probe that ends without a base OFF still gets its turn-off note.
                inconclusive = reason.startswith("support_probe_inconclusive_")
                result["support"]["outcome"] = "Probe_Inconclusive" if inconclusive else "Probe_Failed"
                result["support"]["nextRequiredOffTrials"] = (self.required_off_trials if inconclusive else
                                                              min(MAX_OFF_TRIALS, self.required_off_trials + COOLDOWN_STEP))
            if support:
                result["support"]["afterReview"] = self.support_state(packet["level"])
            result["gazeInput"] = comparison_environment.provenance(session.config)
            result.update(recommended=effective, reason=reason)
            if action == "OFF" and decision.get("modelTrigger") and "offAnnotation" in result:
                # The existing OFF deferral rule is unchanged; record whether it agrees with the claim.
                result["attributionAgreement"] = dict(modelRiseCoincident="modelRise" in result,
                                                      scoreAnswer=result["offAnnotation"]["score"])
            if gaze_review:
                result["checks"] = evidence_checks(decision, result, core.config)
            return result

        if not short and self.locked:
            return finish(
                "OFF" if packet["level"] else "HOLD", "failed_probe_phase_lock"
            )
        if not short and self.probe_at is not None and n >= self.probe_at + 10:
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
            if not verification_stable or score > low:
                self.locked = True
                return finish("OFF", "probe_failed")
            self.probe_at = None
            return finish("HOLD", "probe_passed")
        if probe_eval is not None:
            # Probe-specific failures, judged at either of the two probe checks; neither waits for a base OFF.
            if probe_eval["playbackFailure"]:
                return finish("OFF", "support_probe_inconclusive_playback", probe_final=True)
            if probe_eval["errors"] >= PROBE_ERRORS:
                return finish("OFF", "support_probe_failed_behavior", probe_final=True)
        if action == "ON":
            if self.off_context:
                result["linkedOff"] = self.linked_off(core, annotation_only=short)
                self.reopen = result["linkedOff"].get("verifiedReopening", False)
            if not short and n + 10 >= session.config["trainingTrials"]:
                return finish("HOLD", "insufficient_remaining_probe_checkpoints")
            if not stable or score is None or score > low:
                return finish("HOLD", "current_condition_agreement_missing")
            if support and len(current_all) < self.required_off_trials:
                return finish("HOLD", "support_cooldown_trials_missing")
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
            if support and self.support_invalidated:
                # OFF-state evidence cannot directly re-authorize a Normal ON after difficulty during playback.
                if n > session.config["trainingTrials"] - 2 - PROBE_TRIALS:
                    return finish("HOLD", "support_probe_window_closed")
                result["support"]["authorization"] = "Probe_ON"
                return finish("ON", "support_probe_on")
            if support:
                result["support"]["authorization"] = "Normal_ON"
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
            and ba <= (COMPACT_OFF_BA if short else .60))
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

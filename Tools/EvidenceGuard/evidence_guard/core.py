"""Evidence -> Judgment -> Authorization. Measurements are append-only.

Association is not causation. Disputes revoke named supports, never raw data.
"""
from bisect import insort, bisect_right, bisect_left
from copy import deepcopy
from dataclasses import asdict
import time

from .contracts import Evidence, Judgment, Proposal, Enactment, GuardConfig, finite
from .gaze import summarize


class EvidenceGuard:
    def __init__(self, session, mode="observe", config=None, sink=None):
        if mode not in ("off", "observe", "enforce"):
            raise ValueError("Unknown guard mode")
        self.session, self.mode = session, mode
        self.config = config if isinstance(config, GuardConfig) else GuardConfig(**(config or {}))
        self.sink = sink
        self.evidence, self.judgments, self.relations = {}, {}, {}
        self.invalidations, self.authorizations, self.timeline = [], [], []
        self.index, self.audit, self.reviews, self.acks = [], [], {}, {}
        self.kind_indexes, self.forward_results = {}, {}
        self.revoked = set()
        self.epoch = 0
        self.condition_since = float("-inf")
        self.defer_since = None
        self._log("configuration", {"session": session, "mode": mode, "config": asdict(self.config)})

    def _log(self, kind, data):
        row = {"sequence": len(self.audit), "record": kind, **deepcopy(data)}
        if self.sink:
            self.sink(row)  # Failure propagates to the host's fail-closed boundary.
        self.audit.append(row)

    def query(self, start, end, kinds=None):
        result = []
        indexes = [(k, self.kind_indexes.get(k, [])) for k in kinds] if kinds else [(None, self.index)]
        for kind, index in indexes:
            stop = bisect_right(index, (end, chr(0x10ffff)))
            first = bisect_left(index, (start - self.config.max_sample_seconds, "")) if kind == "gaze" else 0
            result.extend(self.evidence[i] for _, i in index[first:stop] if self.evidence[i].end >= start)
        return result

    def ingest(self, event):
        if self.mode == "off":
            return
        e = event if isinstance(event, Evidence) else Evidence(**event)
        e = deepcopy(e)
        if e.session != self.session or e.epoch != self.epoch:
            raise ValueError("Evidence session/epoch mismatch")
        if e.id in self.evidence:
            if self.evidence[e.id] != e:
                raise ValueError("Immutable evidence identity reused")
            return
        if e.kind == "gaze" and e.data.get("target") not in ("task", "movie", "other", "invalid"):
            raise ValueError("Gaze target must be exclusive")
        self.evidence[e.id] = e
        insort(self.index, (e.start, e.id))
        insort(self.kind_indexes.setdefault(e.kind, []), (e.start, e.id))
        self._log("evidence", asdict(e))
        if e.kind == "signal":
            self._reverse(e)
        if e.kind in ("media", "signal", "trial", "gaze"):
            # Asynchronous arrival may complete a previously unresolved relationship.
            if e.kind == "media":
                movies = [e]
            else:
                delay = max(pair[1] for pair in self.config.response_delays.values())
                movies = self.query(e.start - delay, e.end, {"media"})
            for movie in movies:
                self.forward(movie.id)

    def add_judgment(self, judgment):
        j = judgment if isinstance(judgment, Judgment) else Judgment(**judgment)
        j = deepcopy(j)
        if j.id in self.judgments and self.judgments[j.id] != j:
            raise ValueError("Judgment identity reused; create a new judgment for new evidence")
        if j.id not in self.judgments:
            self.judgments[j.id] = j
            self._log("judgment", asdict(j))
        return j.id

    def gaze(self, intervals, level=None, scene=None):
        if not intervals:
            return summarize([], [], self.config.max_sample_seconds)
        samples = self.query(min(x[0] for x in intervals), max(x[1] for x in intervals), {"gaze"})
        samples = [e for e in samples if e.epoch == self.epoch and
                   (level is None or e.level == level) and (scene is None or e.scene == scene)]
        return summarize(samples, intervals, self.config.max_sample_seconds)

    def _relate(self, movie, signal, direction):
        modality = signal.data.get("modality", "rf")
        lo, hi = self.config.response_delays.get(modality, self.config.response_delays["rf"])
        actual = movie.data.get("actual", False) and movie.end > movie.start
        overlap = signal.end >= movie.start + lo and signal.start <= movie.end + hi
        claimed = signal.data.get("claimed_event") == movie.id
        onset = signal.data.get("change_onset")
        # An overlapping aggregate window alone cannot locate the change onset.
        conflict = claimed and (not actual or not overlap or (finite(onset) and onset < movie.start + lo))
        gaze = self.gaze([(movie.start, movie.end)], movie.level, movie.scene)
        visual = gaze["available"] and gaze["gaze_valid_ratio"] >= self.config.min_gaze_valid and gaze["movie_seconds"] > 0
        audio = movie.data.get("audio_confirmed", False)
        same_condition = movie.epoch == signal.epoch and movie.scene == signal.scene and movie.level == signal.level
        response = signal.quality and signal.data.get("response_observed", False)
        state = "DISPUTED" if conflict else "SUPPORTED" if actual and overlap and response and same_condition and (visual or audio) else "UNRESOLVED"
        key = movie.id + "::" + signal.id
        relation = {"id": key, "event": movie.id, "signal": signal.id, "state": state,
                    "actual_seconds": movie.end - movie.start if actual else 0., "visual_exposure": visual,
                    "audio_confirmed": audio, "gaze": gaze, "time_correspondence": overlap,
                    "response_kind": modality, "physiological_change_confirmed": bool(signal.data.get("physiological_change_confirmed", False)),
                    "alternatives": ["task", "movement", "quality", "intervention", "model_movie_fraction"],
                    "causal_claim": False, "directions": sorted(set(self.relations.get(key, {}).get("directions", []) + [direction]))}
        old = self.relations.get(key)
        if old != relation:
            self.relations[key] = relation
            self._log("relation", relation)
            if state == "DISPUTED":
                for j in self.judgments.values():
                    if j.relation_id == key:
                        self._invalidate(j.id, "attribution_conflict", signal.end, key)
        return relation

    def forward(self, event_id):
        movie = self.evidence[event_id]
        if movie.kind != "media":
            raise ValueError("Expected actual/failed media event")
        delay = max(pair[1] for pair in self.config.response_delays.values())
        signals = [e for e in self.query(movie.start, movie.end + delay, {"signal"}) if e.epoch == movie.epoch]
        # Explicit references are also checked outside the ordinary latency range.
        signals += [e for e in self.evidence.values() if e.kind == "signal" and e.data.get("claimed_event") == event_id and e not in signals]
        results = [self._relate(movie, s, "forward") for s in signals]
        summary = {"event": event_id, "status": "event_exposure_confirmed_response_unconfirmed" if movie.data.get("actual") else "exposure_insufficient",
                   "relations": [r["id"] for r in results], "absence_is_not_no_effect": True,
                   "actual_seconds": movie.end - movie.start if movie.data.get("actual") else 0.,
                   "gaze": self.gaze([(movie.start, movie.end)], movie.level, movie.scene),
                   "eligible_trials": [e.id for e in self.query(movie.start, movie.end + self.config.response_delays["behavior"][1], {"trial"}) if e.quality and e.epoch == movie.epoch]}
        if results:
            summary["status"] = "event_response_association_checked"
        if self.forward_results.get(event_id) != summary:
            self.forward_results[event_id] = summary
            self._log("forward", summary)
        return results

    def _reverse(self, signal):
        score = signal.data.get("score")
        previous = [e for e in self.query(float("-inf"), signal.start, {"signal"})
                    if e.id != signal.id and e.end <= signal.start and e.epoch == signal.epoch and e.quality and finite(e.data.get("score"))]
        previous.sort(key=lambda e: (e.end, e.id))
        state = lambda x: "stable" if x <= .45 else "struggling" if x >= .65 else "buffer"
        crossed = bool(signal.quality and finite(score) and previous and
                       {state(score), state(previous[-1].data["score"])} == {"stable", "struggling"})
        claimed = signal.data.get("claimed_event")
        if not crossed and not claimed and not signal.data.get("physiological_change_confirmed"):
            return
        delay = max(pair[1] for pair in self.config.response_delays.values())
        sources = self.query(signal.start - delay, signal.end, {"media", "task", "session", "enactment", "gaze", "trial"})
        movies = [e for e in sources if e.kind == "media" and e.epoch == signal.epoch]
        if claimed in self.evidence and self.evidence[claimed] not in movies:
            movies.append(self.evidence[claimed])
        self._log("reverse", {"signal": signal.id, "trigger": "rf_state_crossing" if crossed else "attribution_or_feature_check",
                    "comparison": previous[-1].id if previous else None, "comparison_nonoverlap": bool(previous),
                    "sources": [e.id for e in sources], "rf_change_is_not_physiology": True,
                    "counterfactual": signal.data.get("counterfactual"), "diagnostic_controls_action": False})
        for movie in movies:
            self._relate(movie, signal, "reverse")

    def _invalidate(self, judgment_id, reason, when, relation=""):
        if judgment_id in self.revoked:
            return
        self.revoked.add(judgment_id)
        row = {"judgment": judgment_id, "reason": reason, "time": when, "relation": relation,
               "raw_measurements_retained": True, "automatic_expiry": False}
        row["affected_authorizations"] = [a["request_id"] for a in self.authorizations if judgment_id in (a["chosen_path"] or [])]
        self.invalidations.append(row)
        self._log("invalidation", row)

    def _check(self, jid, p):
        j = self.judgments.get(jid)
        if j is None or jid in self.revoked:
            return False, "missing_or_revoked_judgment"
        rows = [self.evidence.get(i) for i in j.evidence_ids]
        if not rows or any(e is None or not e.quality for e in rows):
            return False, "missing_or_invalid_evidence"
        if any(e.epoch != self.epoch or e.end > p.time for e in rows) or p.time - max(e.end for e in rows) > self.config.max_evidence_age:
            return False, "stale_future_or_reset_evidence"
        if any(e.level != j.level or e.scene != j.scene for e in rows):
            self._invalidate(jid, "condition_mismatch", p.time)
            return False, "condition_mismatch"
        if j.kind == "recovered_l1":
            if j.level != 1 or not j.comparable or not any(e.kind == "media" and e.data.get("actual") for e in rows):
                self._invalidate(jid, "l0_recovery_does_not_establish_l1_tolerance", p.time)
                return False, "inapplicable_cross_condition_recovery"
        elif any(e.start < self.condition_since for e in rows):
            self._invalidate(jid, "evidence_predates_current_condition", p.time)
            return False, "evidence_predates_current_condition"
        elif j.level != p.level or j.scene != p.scene:
            self._invalidate(jid, "environment_changed", p.time)
            return False, "environment_changed"
        if j.kind == "movie_attribution":
            relation = self.relations.get(j.relation_id, {})
            if relation.get("state") == "DISPUTED":
                self._invalidate(jid, "attribution_conflict", p.time, j.relation_id)
            if relation.get("state") != "SUPPORTED":
                return False, "attribution_" + relation.get("state", "missing").lower()
        if j.requires_view:
            intervals = [(e.data.get("visible_start", e.start), e.data.get("visible_end", e.end)) for e in rows if e.kind == "trial"]
            if not intervals:
                intervals = [(min(e.start for e in rows), max(e.end for e in rows))]
            gaze = self.gaze(intervals, j.level, j.scene)
            self._log("view_check", {"judgment": jid, **gaze})
            if not gaze["available"] or gaze["gaze_valid_ratio"] < self.config.min_gaze_valid or gaze["task_gaze_ratio"] < self.config.min_task_gaze:
                return False, "task_view_unavailable_or_insufficient"
        if j.kind not in ("current_stable", "training_challenge", "task_view", "movie_attribution", "recovered_l1", "random_schedule"):
            return False, "unsupported_judgment_kind"
        return True, "supported"

    def review(self, proposal):
        p = proposal if isinstance(proposal, Proposal) else Proposal(**proposal)
        start = time.perf_counter()
        if p.session != self.session or not finite(p.time) or p.level not in (0, 1):
            raise ValueError("Invalid proposal scope")
        if p.id in self.reviews:
            saved, epoch, result = self.reviews[p.id]
            if saved != p or epoch != self.epoch:
                raise ValueError("Request identity reused or invalidated")
            return deepcopy(result)
        checks, chosen = [], None
        if p.action in ("OFF", "PAUSE", "EXIT", "REST", "SAFETY_REDUCE"):
            verdict, reason = "ALLOW", "independent_reduction_or_safety_authority"
        elif p.action == "HOLD":
            verdict, reason = "NO_CHANGE", "base_hold"
        elif self.mode == "off":
            verdict, reason = ("ALLOW", "guard_off") if p.legal else ("DEFER", "illegal_base_candidate")
        elif p.action != "ON" or not p.legal:
            verdict, reason = "DEFER", "illegal_base_candidate"
        else:
            for path in p.paths:
                results = [{"judgment": j, "valid": ok, "reason": why} for j in path for ok, why in [self._check(j, p)]]
                checks.append(results)
                if path and all(r["valid"] for r in results) and chosen is None:
                    chosen = list(path)
            verdict, reason = ("ALLOW", "independent_valid_support") if chosen else ("DEFER", "no_valid_authorization_path")
        if verdict == "DEFER" and self.defer_since is None:
            self.defer_since = p.time
        duration = max(0., p.time - self.defer_since) if self.defer_since is not None else 0.
        result = {"request_id": p.id, "session": self.session, "time": p.time, "mode": self.mode,
                  "verdict": verdict, "reason": reason, "base_action": p.action, "basis": p.basis,
                  "effective_action": "HOLD" if self.mode == "enforce" and verdict == "DEFER" else p.action,
                  "chosen_path": chosen, "checks": checks, "defer_duration_seconds": duration,
                  "epoch": self.epoch, "scene": p.scene, "latency_ms": (time.perf_counter() - start) * 1000}
        if p.action == "ON" and verdict == "ALLOW":
            self.defer_since = None
        self.authorizations.append(result)
        self.reviews[p.id] = (deepcopy(p), self.epoch, deepcopy(result))
        self._log("authorization", result)
        return deepcopy(result)

    def on_enactment(self, ack):
        a = ack if isinstance(ack, Enactment) else Enactment(**ack)
        if a.session != self.session or not finite(a.time) or a.level not in (0, 1) or a.previous_level not in (0, 1):
            raise ValueError("Invalid enactment scope")
        if a.id in self.acks:
            if self.acks[a.id] != a:
                raise ValueError("Execution identity reused")
            return
        self.acks[a.id] = a
        self.timeline.append(asdict(a))
        self._log("enactment", asdict(a))
        if not a.actual:
            return
        if a.previous_level != a.level:
            self.condition_since = max(self.condition_since, a.time)
            if a.level > a.previous_level:
                authorization = self.reviews.get(a.request_id)
                self._log("execution_check", {"ack": a.id, "request_id": a.request_id,
                    "matched_allow": bool(authorization and authorization[2]["verdict"] == "ALLOW"),
                    "mode": self.mode, "actual_level": a.level})
            for j in self.judgments.values():
                if j.kind != "recovered_l1" and (j.level != a.level or j.scene != a.scene):
                    self._invalidate(j.id, "actual_environment_change", a.time)
        self.ingest(Evidence(a.id, "enactment", a.session, a.time, a.time, "host_execution", a.level, a.scene, True, asdict(a), self.epoch))

    def reset(self, scope="session", when=0.):
        if scope not in ("session", "recalibration", "mount", "pause", "resume", "stage"):
            raise ValueError("Unknown reset scope")
        for jid in self.judgments:
            self._invalidate(jid, "reset_" + scope, when)
        self.epoch += 1
        self.condition_since = max(self.condition_since, when)
        self.defer_since = None
        self._log("reset", {"scope": scope, "time": when, "epoch": self.epoch})

    def export_audit(self):
        return deepcopy(self.audit)

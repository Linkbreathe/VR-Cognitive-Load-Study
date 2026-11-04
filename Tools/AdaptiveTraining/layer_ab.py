"""Frozen A/B allocation and short, experimenter-administered questionnaires."""
import json
import math
import time

REVISION = "layer-ab-v1"
QUESTIONNAIRE = "raw-tlx6-preference-v1"
STAGES = ("layer_a", "layer_b")
TRAINING = ("random", "bioadaptive", *STAGES)
TLX = ("mental", "physical", "temporal", "performance", "effort", "frustration")


def enabled(config):
    return config.get("studyDesign") == REVISION


def validate(config):
    if config.get("studyDesign", "legacy") not in ("legacy", REVISION):
        raise ValueError("Unknown study design")
    if enabled(config) and (config.get("operatorManaged") or config.get("allowCandidateModel") or
            config.get("protocol") != "personalized-rf-v2" or config.get("mode") != "ACTIVE" or
            config.get("evidenceGuardMode") != "observe" or config.get("questionnaireRevision") != QUESTIONNAIRE or
            config.get("abInterConditionRestSeconds") != 180 or config.get("counterbalanceGroup") not in range(4)):
        raise ValueError("Invalid frozen A/B study configuration")


def guard_mode(config, stage):
    return ("enforce" if stage == "layer_b" else "observe") if enabled(config) else config.get("evidenceGuardMode", "off")


def condition(config, ordinal):
    return "A" if (ordinal == 1) == (config["counterbalanceGroup"] // 2 == 0) else "B"


def questionnaire_status(session):
    stage = session["packet"]["stage"]
    ordinal = 1 if stage == "questionnaire_1" else 2 if stage == "questionnaire_2" else 0
    path = session["path"] / f"questionnaire_{ordinal}.json"
    return {"sessionId": session["packet"]["sessionId"], "participantCode": session["packet"]["participantCode"],
            "sessionType": session["kind"], "ordinal": ordinal, "saved": ordinal > 0 and path.exists(),
            "revision": QUESTIONNAIRE, "stage": stage}


def save_questionnaire(session, payload):
    if not enabled(session["config"]):
        raise ValueError("This session does not use the A/B questionnaires")
    status = questionnaire_status(session)
    ordinal = status["ordinal"]
    if not ordinal or payload.get("ordinal") != ordinal or payload.get("sessionId") != status["sessionId"]:
        raise ValueError("Questionnaire occasion does not match the current session break")
    if payload.get("revision") != QUESTIONNAIRE or set(payload.get("tlx", {})) != set(TLX):
        raise ValueError("All six Raw TLX ratings are required")
    ratings = payload["tlx"]
    if any(type(v) not in (int, float) or not math.isfinite(v) or v < 0 or v > 100 or v % 5 for v in ratings.values()):
        raise ValueError("Use 0 to 100 in steps of 5; no missing or preselected ratings")
    preference = payload.get("preference", "")
    if (ordinal == 2 and preference not in ("session_1", "session_2", "no_preference")) or (ordinal == 1 and preference):
        raise ValueError("A preference (including no preference) is required only after Session 2")
    reason = payload.get("reason", "")
    discomfort = payload.get("discomfort", "")
    if not isinstance(reason, str) or len(reason) > 2000 or discomfort not in ("none_reported", "reported", "not_checked"):
        raise ValueError("Invalid optional reason or operator comfort-check record")
    answers = {"tlx": ratings, "preference": preference, "reason": reason, "discomfort": discomfort}
    path = session["path"] / f"questionnaire_{ordinal}.json"
    if path.exists():
        saved = json.loads(path.read_text(encoding="utf-8"))
        if saved["answers"] != answers:
            raise ValueError("Already saved; original responses cannot be overwritten")
        return saved
    packet = session["packet"]
    row = {**status, "saved": True, "condition": condition(session["config"], ordinal), "answers": answers,
           "raw_tlx": sum(ratings.values()) / 6, "administration": "interviewer_oral_headset_on-v1",
           "submitted_perf_s": time.perf_counter(), "latest_unity_time_s": packet["now"],
           "preference_condition": condition(session["config"], int(preference[-1])) if preference in ("session_1", "session_2") else None}
    with path.open("x", encoding="utf-8") as f:
        json.dump(row, f, indent=2, allow_nan=False)
    return row

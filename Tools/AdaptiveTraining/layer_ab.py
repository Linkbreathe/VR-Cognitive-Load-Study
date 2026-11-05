"""Frozen A/B allocation and external Google Forms questionnaire revision."""

REVISION = "layer-ab-v1"
QUESTIONNAIRE = "raw-tlx7-ueq4-preference-google-forms-v4"
STAGES = ("layer_a", "layer_b")
TRAINING = ("random", "bioadaptive", *STAGES)


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

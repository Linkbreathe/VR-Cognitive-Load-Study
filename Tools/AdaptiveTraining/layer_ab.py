"""Frozen A/B allocation and external Google Forms questionnaire revision."""

SLIDING = "sliding-five-close-v1"
REVISION = "layer-ab-v1"
PROBE = "layer-ab-probe-v2"
COMPACT_POLICY = "behavior5-continuous-stride2-v3"
COMPACT_REVIEW = "current-condition-support-probe4-on65-off50-v3"
COMPACT_BEHAVIOR_REVISION = "scored5-on65-off50-v5"
COMPACT_ON_BA = 0.65
COMPACT_OFF_BA = 0.50
QUESTIONNAIRE = "raw-tlx7-ueq4-experience7-preference-google-forms-v5.1"
COMPARISON_MODES = ("comparison", "comparison_candidate", "comparison_rehearsal")
STAGES = ("layer_a", "layer_b")
TRAINING = ("random", "bioadaptive", *STAGES)


def compact(config):
    return config.get("studyDesign") == PROBE and config.get("evaluationPolicy") == COMPACT_POLICY


def configure_probe(config, group, links, short=False):
    config.update(counterbalanceGroup=group, trainingTrials=144, videoWidth=3.84, videoHeight=2.16,
                  evaluationPolicy="behavior10-or-probe-v2", rollingWindowTrials=10, rollingStrideTrials=5,
                  mediaManifest="layer-ab-144-media.json", mediaVolume=.40, onsetJitterSeconds=0,
                  studyDesign=PROBE, slidingRevision=SLIDING, questionnaireRevision=QUESTIONNAIRE,
                  evidenceGuardMode="observe", abInterConditionRestSeconds=180, calibrationFormUrl="",
                  trainingFormUrl=links["training"]["responder"], preferenceFormUrl=links["preference"]["responder"])
    if short:
        config.update(trainingTrials=60, evaluationPolicy=COMPACT_POLICY, rollingWindowTrials=5, rollingStrideTrials=2)


def enabled(config):
    return config.get("studyDesign") in (REVISION, PROBE)


def validate(config):
    from comparison_environment import validate as validate_environment
    validate_environment(config)
    if config.get("studyDesign") == PROBE and (config.get("trainingTrials") != (60 if compact(config) else 144) or config.get("slidingRevision") != SLIDING):
        raise ValueError("Probe trial budget must match its versioned response-close policy")
    if compact(config) and (config.get("rollingWindowTrials") != 5 or config.get("rollingStrideTrials") != 2):
        raise ValueError("Compact comparison requires window 5 and stride 2")
    if config.get("slidingRevision") and (not enabled(config) or config["slidingRevision"] != SLIDING):
        raise ValueError("Unknown sliding control revision")
    if config.get("studyDesign", "legacy") not in ("legacy", REVISION, PROBE):
        raise ValueError("Unknown study design")
    managed_probe = (config.get("studyDesign") == PROBE and config.get("sessionType") == "engineering" and
                     config.get("testSessionMode") in ("comparison", "comparison_candidate", "comparison_rehearsal", "control_calibration"))
    if enabled(config) and ((config.get("operatorManaged") and not managed_probe) or (config.get("allowCandidateModel") and not (managed_probe and config.get("testSessionMode")=="comparison_candidate")) or
            config.get("protocol") != "personalized-rf-v2" or config.get("mode") != "ACTIVE" or
            config.get("evidenceGuardMode") != "observe" or config.get("questionnaireRevision") != QUESTIONNAIRE or
            config.get("abInterConditionRestSeconds") != 180 or config.get("counterbalanceGroup") not in range(4)):
        raise ValueError("Invalid frozen A/B study configuration")


def guard_mode(config, stage):
    return ("enforce" if stage == "layer_b" else "observe") if enabled(config) else config.get("evidenceGuardMode", "off")


def condition(config, ordinal):
    return "A" if (ordinal == 1) == (config["counterbalanceGroup"] // 2 == 0) else "B"

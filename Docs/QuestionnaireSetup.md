# Questionnaire administration

Current A/B questionnaire revision: `raw-tlx7-ueq4-preference-google-forms-v4`. Use the seven-point adaptation of Raw TLX on page 1 (six required 1-7 ratings), UEQ+ on page 2, and final preference after Training 2. See [scoring and literature](SevenPointTLXReview.md).

## Layer A/B study (2026-11-05)

For `layer-ab-v1`, use the [two-page Raw TLX (six items) / UEQ+ Attractiveness (four items) and separate final preference Google Forms](LayerABGoogleForms.md). The local questionnaire webpage is removed. Confirm successful submissions with X/Enter; first confirmation starts 180 seconds of rest. The legacy forms below remain unchanged and do not apply to A/B.

# Oral questionnaire administration for dev11

For dev11, the experimenter reads all questions and scale endpoints aloud and enters the participant's spoken answers in Google Forms. The participant keeps the headset and electrodes in place after calibration and between Training 1 and Training 2. Read verbatim, do not coach answers, and allow rest without adjusting the equipment. This user-selected procedure supersedes dev9's headset-removal instructions. Administration revision is `interviewer_oral_headset_on-v1`; it is saved in the Unity configuration and acknowledgement events. The method was introduced in dev10 and is retained in dev11. See [validity revision](PersonalizedValidityReview.md) and [current flow](NoRestProtocolReview.md).

The same two English pilot forms were created, published and inspected on 2026-11-02; their questions and published settings were not changed by dev10. Keep participant codes pseudonymous; link responses using participant code, session ID and questionnaire occasion. Do not expose Random/Bioadaptive strategy names to participants before the study is complete. Oral administration is a revised measurement procedure and needs pilot review; do not silently pool it with self-administered dev9 responses.

| Occasion | Participant form | Owner editing |
|---|---|---|
| Calibration | [Open questionnaire](https://docs.google.com/forms/d/e/1FAIpQLSeghD4XMzg3o9_Z0C3wX79ns8VwIqtdPLRScHXn829bPKI0_A/viewform) | [Edit](https://docs.google.com/forms/d/1o5qVojVTBfPtVeazs_S34WqzO2k6keOFIw01Ex8TghE/edit) |
| Training 1 and Training 2 | [Open questionnaire](https://docs.google.com/forms/d/e/1FAIpQLScKnSC3dnP8edc19qBj5Zbs2sRH62p1eQ5s9zbi9y5T9LTlEQ/viewform) | [Edit](https://docs.google.com/forms/d/1GWVFxQJIOXGC8uF9JV_uQxFL6Z1P_XHNz_JE-v5p2JY/edit) |

The forms accept anyone with the responder link; response summaries are not shared. Email collection, one-response sign-in restriction, quiz scoring and question shuffling are off. There are eight required calibration questions, fourteen required training questions and one optional comment. No response was submitted during creation. The published respondent pages, required flags, endpoint labels and all dropdown options were read back. Actual participant submission and export linkage still need an operator rehearsal. Evidence: `Evidence/PersonalizedProtocol/*-form.jpg` and `*-form-dom.txt`.

## Calibration form

Record participant code, session ID and occasion `calibration`. Ask participants to recall the two conditions separately: no movie, and movie clips playing. For each condition, use the following three 1-7 items:

1. How much effort did completing the task require? (1 = very little, 7 = very much)
2. How easily were you distracted? (1 = not at all, 7 = very easily)
3. How easy was it to maintain attention on the central task? (1 = very difficult, 7 = very easy)

Retain the direction of item 3 in the codebook. These retrospective condition ratings are supporting subjective evidence and must not become per-window Stable/Struggling labels.

## Post-training form

Use the same form after `training_1` and `training_2`, with participant code and session ID. Keep the actual strategy mapping in the experimenter's allocation record.

- The pilot form uses all six NASA-TLX dimensions: Mental Demand, Physical Demand, Temporal Demand, Performance, Effort and Frustration. Each dropdown has 21 values, 0 to 100 in increments of 5. Performance runs from Perfect (0) to Failure (100); the other dimensions run from Very low to Very high. The provisional scoring choice is Raw TLX: retain all six ratings and, if complete, report their unweighted mean separately from the individual dimensions. Do not describe this as weighted TLX. Dropdown administration and the Raw TLX choice must be reviewed and frozen in the pilot; no equivalence to NASA's original rating interface is claimed. [NASA's official overview and instrument resources](https://www.nasa.gov/human-systems-integration-division/nasa-task-load-index-tlx/) identify the six dimensions and original weighted procedure.
- Movie interference: How much did the movie clips interfere with the central task? (1 = not at all, 7 = very much)
- Appearance timing: How appropriate were the times when movie clips appeared? (1 = very inappropriate, 7 = very appropriate; include Not applicable)
- Disappearance timing: How appropriate were the times when movie clips stopped? (same scale; include Not applicable)
- Fatigue: How tired do you feel after this training? (1 = not at all, 7 = very tired)
- Discomfort: How uncomfortable did this training feel? (1 = not at all, 7 = very uncomfortable)
- Optional free text: Describe anything that affected your attention or comfort.

IPQ can be added as a separately identified optional presence measure. Keep scoring rules, questionnaire versions and any missing/not-applicable answers in the analysis codebook. No questionnaire result is proof of physiological model validity.

## Operator linkage

Published form URLs are included in the configuration template and current engineering override, and recorded in `Tools/AdaptiveTraining/questionnaire-links.json`. The allocation tool copies them into new session configurations; the Editor enable command preserves existing links or uses the template. The experimenter opens the appropriate link on the study computer/tablet/phone, reads the wording and full scale, and enters each spoken answer. The headset shows the oral-question break instructions. Its `external_questionnaire_acknowledged` event records a continue action, not a confirmed Forms submission; `questionnaire_administration` records the method. Verify the session/occasion and submission externally, without removing or repositioning the participant's equipment.

If removal or electrode adjustment is needed, pause and start a new session after checks. H on the application's keyboard explicitly records movement; supported XR presence loss also latches invalidation. An invalidated session cannot resume or reuse its model. On standalone, use pause/stop and Left trigger to restart. Do not apply an unvalidated baseline offset correction. See [mount-integrity behavior](PersonalizedValidityReview.md#oral-questionnaires-and-mount-integrity).

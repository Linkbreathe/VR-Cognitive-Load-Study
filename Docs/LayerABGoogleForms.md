# Layer A/B Google Forms revision

Date: 2026-11-05. Revision: `raw-tlx6-preference-google-forms-v2`. Branch: `codex/layer-ab-timing-study`.

## Forms and administration

| When | Form | Owner editing | Required fields |
|---|---|---|---|
| Immediately after Training 1 and Training 2 | [Six Raw NASA-TLX ratings](https://docs.google.com/forms/d/e/1FAIpQLSeIC0ZO2EYBvbqF-zqy50panjnd_gNcXdG6wTTMkksT93FYLA/viewform) | [Edit](https://docs.google.com/forms/d/1wV0sCvKddY8j4sFaUxLETLqFSuxs24RBgXCcdTrb7PU/edit) | Participant code, session ID, training occasion, six ratings (9 total) |
| After the second workload form, once per session | [Final preference](https://docs.google.com/forms/d/e/1FAIpQLSeqZYypO-Gm_ZMSr8K_qVQxLf-7cDv6JbCU2oPjkY-TDCcymA/viewform) | [Edit](https://docs.google.com/forms/d/1xiB5ZjopRAanD6GGLAgFfSrH-IxdsL98pZA2l1Bu2Dk/edit) | Participant code, session ID, preference; reason optional (4 fields total) |

Workload dimensions: Mental Demand, Physical Demand, Temporal Demand, Performance, Effort and Frustration; 0-100 in steps of 5, no preselected rating. Performance anchors are Perfect=0 and Failure=100; other dimensions are Very low=0 and Very high=100. Calculate Raw TLX as the mean of six complete ratings, without weighting or filling missing values. Read wording and endpoints consistently; oral administration requires pilot comprehension review.

Preference choices: Training 1, Training 2, No preference. Do not reveal A/B or frame either controller as better. IMI, adjustment-experience items, repeated SSQ and calibration questionnaires are omitted. Brief verbal discomfort checks belong in session notes, not additional scored questions.

The participant keeps headset/electrodes in place; the experimenter fills the forms. Equipment movement still invalidates physiological qualification. Use pseudonymous codes only. Forms accept anyone with the responder link. Email collection, required sign-in, one-response restriction, quiz scoring, shuffled questions and shared response summaries are off. Owner editing remains restricted. Original legacy forms were copied, not modified.

## Runtime and data

Removed: local `ab-study.html`, preview server, `/ab-study`, `/ab-study/status`, `/ab-study/questionnaire`, local answer JSON persistence and Unity receipt polling. The launcher opens workload Google Forms and prints both links; new allocations and engineering examples contain both URLs. The controller, raw physiology, input audit, four-stamp synchronization and optional LSL outlet remain.

After the first submission succeeds, X/Enter records one acknowledgement and starts 180 seconds of rest. Repeated presses neither skip rest nor duplicate acknowledgement. X/Enter after rest enters Training 2 instructions. After both final submissions succeed, X/Enter records workload and preference acknowledgements and completes the session. The software does not query Google responses; all confirmations retain `submission_verified=false`.

The aligned export writes `questionnaire_acknowledgements.csv`, not questionnaire scores. Export both Google response tables separately. Join participant code and exact session ID; workload additionally uses Training occasion. Map Training 1/2 and preference through the saved allocation. Flag missing, duplicate or mismatched answers. Google submission wall-clock times and operator confirmation are not per-answer RT or LSL acquisition timestamps. Task button edges and original RT remain available for signal alignment.

Old `raw-tlx6-preference-v1` allocations fail the revision check; generate fresh allocations for new sessions. Do not rewrite recorded configurations or answers. No new APK accompanies this source update.

## Validation and remaining checks

- Both new forms were created, published and inspected in signed-out respondent views. Workload has nine required fields; preference has three required fields and one optional reason. All six 21-option dropdowns, endpoint labels, required flags and preference options were read back. No participant or fabricated answer was submitted.
- Browser evidence: `Evidence/LayerAB/GoogleForms/workload-dom.txt`, `preference-dom.txt`, `workload.png`, `preference.png`.
- 165 Python tests passed; Unity compilation passed and 3,934 synthetic Play Mode checks passed, including four allocation groups, complete phase budgets, manual confirmation/rest gates, duplicate suppression, three external acknowledgements and retained input edges/RT. The PASS log was read back; Play Mode was stopped. Evidence: `Evidence/LayerAB/GoogleForms/python-regression.txt`, `unity-validation.txt`, `unity-console.json`. Removed local GET/POST routes return 404; the dedicated clock endpoint and external-link allocation remain covered.
- Console review found two unsupported CLI command-name errors from validation tooling, not task exceptions. They are preserved in `unity-command-errors.json`; after clearing the reviewed buffer, console errors were zero and compilation was successful.
- Pending: real operator submission/export/join rehearsal, full real-time A/B run, Quest Pro and physical timing acceptance, personal model admission and participant effects. This change does not demonstrate a physiological or subjective benefit of B.

See [study guide](LayerABStudyGuide.md) for operation, timing, export and prior engineering evidence.

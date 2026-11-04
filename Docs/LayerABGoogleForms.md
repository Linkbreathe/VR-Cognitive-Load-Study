# Layer A/B Google Forms revision

Date: 2026-11-05. Revision: `raw-tlx6-ueq4-preference-google-forms-v3`. Branch: `codex/layer-ab-timing-study`. This supersedes v2 for new sessions; the form URLs are unchanged.

## Forms and administration

| When | Form | Owner editing | Required fields |
|---|---|---|---|
| Immediately after Training 1 and Training 2 | [Two-page post-condition questionnaire](https://docs.google.com/forms/d/e/1FAIpQLSeIC0ZO2EYBvbqF-zqy50panjnd_gNcXdG6wTTMkksT93FYLA/viewform) | [Edit](https://docs.google.com/forms/d/1wV0sCvKddY8j4sFaUxLETLqFSuxs24RBgXCcdTrb7PU/edit) | Page 1: participant code, session ID, training occasion and six Raw TLX ratings. Page 2: four UEQ+ Attractiveness ratings. All 13 fields required. |
| After both pages of the second post-condition form, once per session | [Final preference](https://docs.google.com/forms/d/e/1FAIpQLSeqZYypO-Gm_ZMSr8K_qVQxLf-7cDv6JbCU2oPjkY-TDCcymA/viewform) | [Edit](https://docs.google.com/forms/d/1xiB5ZjopRAanD6GGLAgFfSrH-IxdsL98pZA2l1Bu2Dk/edit) | Participant code, session ID, preference; reason optional (4 fields total) |

Workload dimensions: Mental Demand, Physical Demand, Temporal Demand, Performance, Effort and Frustration; 0-100 in steps of 5, no preselected rating. Performance anchors are Perfect=0 and Failure=100; other dimensions are Very low=0 and Very high=100. Calculate Raw TLX as the mean of six complete ratings, without weighting or filling missing values. Read wording and endpoints consistently; oral administration requires pilot comprehension review.

Page 2 uses the standard four-item **UEQ+ Attractiveness** scale, measuring overall liking rather than visual beauty. Retain its introductory sentence, "In my opinion, the product is generally:", and specify that the product is the VR system during the just-completed condition. The four endpoint pairs, in order, are annoying/enjoyable, bad/good, unpleasant/pleasant, and unfriendly/friendly. Each item has seven positions: 1 = left term, 7 = right term, midpoint 4, no preselection. Compute the mean of the four complete ratings after subtracting 4 from each (-3 to +3). Higher scores indicate a more positive overall impression. Do not combine this score with TLX. Do not fill missing values. The importance question is omitted because no importance-weighted UX KPI is planned. This is the UEQ+ four-item scale, not an arbitrary shortened original six-item UEQ Attractiveness scale.

The two Google Forms sections are actual pages with Next/Back navigation and a progress bar. Only completing page 2 and submitting completes the post-condition form. Both scales refer to the same immediately preceding condition; the participant is not told which layer policy was used. The extra scale does not create another Unity acknowledgement or alter the fixed rest.

## Public availability and permitted use

Verified before editing on 2026-11-05:

- [NASA's official TLX page](https://www.nasa.gov/human-systems-integration-division/nasa-task-load-index-tlx/) explicitly permits worldwide use and modification without obtaining NASA permission. [Hart's review](https://www.nasa.gov/wp-content/uploads/2026/01/hfes-2006-paper.pdf) discusses the unweighted Raw TLX approach. Our dropdown/oral presentation remains an adapted administration, not a claim of interface equivalence.
- The [official UEQ+ site](https://ueqplus.ueq-research.org/) provides the instruments and materials free of charge for constructing research-specific questionnaires. The authors' [UEQ+ benchmark paper](https://revistas.unir.net/index.php/ijimai/article/download/278/123) explicitly states that UEQ+ and the supplied materials are free to use. The [official Handbook v4](https://ueqplus.ueq-research.org/Material/UEQ%2B_Handbook_V4.pdf), printed page 5, specifies these four English items; page 19 permits omitting importance ratings when only scale scores are required.
- Free research use does not mean public domain or a blanket copyright waiver. Keep attribution; no CC/public-domain license is asserted. The final preference question is a study-authored descriptive item, not a validated psychometric scale.
- Publicly accessible instruments and responder links do not make collected answers public. Shared response summaries and email collection remain off; owner editing remains restricted. The Google account shown to a signed-in operator is not collected as an answer.

Preference choices: Training 1, Training 2, No preference. Do not reveal A/B or frame either controller as better. IMI, adjustment-experience items, repeated SSQ and calibration questionnaires are omitted. Brief verbal discomfort checks belong in session notes, not additional scored questions.

The participant keeps headset/electrodes in place; the experimenter fills the forms. Equipment movement still invalidates physiological qualification. Use pseudonymous codes only. Forms accept anyone with the responder link. Email collection, required sign-in, one-response restriction, quiz scoring, shuffled questions and shared response summaries are off. Owner editing remains restricted. Original legacy forms were copied, not modified.

## Runtime and data

Removed: local `ab-study.html`, preview server, `/ab-study`, `/ab-study/status`, `/ab-study/questionnaire`, local answer JSON persistence and Unity receipt polling. The launcher opens workload Google Forms and prints both links; new allocations and engineering examples contain both URLs. The controller, raw physiology, input audit, four-stamp synchronization and optional LSL outlet remain.

After the first submission succeeds, X/Enter records one acknowledgement and starts 180 seconds of rest. Repeated presses neither skip rest nor duplicate acknowledgement. X/Enter after rest enters Training 2 instructions. After both final submissions succeed, X/Enter records workload and preference acknowledgements and completes the session. The software does not query Google responses; all confirmations retain `submission_verified=false`.

The aligned export writes `questionnaire_acknowledgements.csv`, not questionnaire scores. Export both Google response tables separately. Join participant code and exact session ID; the post-condition table contains both scales and additionally uses Training occasion. Map Training 1/2 and preference through the saved allocation. Flag missing, duplicate or mismatched answers. Google submission wall-clock times and operator confirmation are not per-answer RT or LSL acquisition timestamps. Task button edges and original RT remain available for signal alignment.

Old v1 and v2 questionnaire allocations fail the revision check; generate fresh allocations for new sessions. Do not rewrite recorded configurations or answers. Do not pool old six-item-only responses with the revised procedure without retaining version provenance. No new APK accompanies this source update.

## Validation and remaining checks

### v3: two-page TLX and UEQ+ revision

- Verified the published first page without sign-in; confirmed all six TLX option sets and the Page 1 of 2 navigation indicator. In Chrome, completed page 1 with clearly marked temporary engineering identifiers solely to inspect navigation, then verified the published second page's four required 1-7 scales and correct endpoint labels. No second-page rating or final submission was made. Cleared the temporary drafts; the response dashboard remained at zero responses.
- Reviewed email collection, sign-in restriction, quiz, shuffle and response-summary settings; enabled the two-page progress bar. The preference choices and optional reason are retained, with administration text updated to follow both pages of Training 2. Browser evidence: `Evidence/LayerAB/GoogleFormsV3/page1-dom.txt`, `page2-dom.txt`, `settings.txt`, `responses.txt`, `page1.png`, `page2.png`.
- Eight targeted A/B Python tests passed (`python-regression.txt`); Unity recompile completed and console review reported zero errors with `compilationFailed=false` (`unity-console-status.json`). Only revision metadata and prompt/launcher text changed in software; Play Mode and the full regression suite were not rerun for v3. Editor remains stopped and its pre-existing auto-tick remains enabled. No control, event timing, scoring or LSL implementation changed. Real operator submission/export/join rehearsal, oral comprehension review and Quest Pro acceptance remain pending.

### Historical v2: replacement of local questionnaires

- Both new forms were created, published and inspected in signed-out respondent views. Workload has nine required fields; preference has three required fields and one optional reason. All six 21-option dropdowns, endpoint labels, required flags and preference options were read back. No participant or fabricated answer was submitted.
- Browser evidence: `Evidence/LayerAB/GoogleForms/workload-dom.txt`, `preference-dom.txt`, `workload.png`, `preference.png`.
- 165 Python tests passed; Unity compilation passed and 3,934 synthetic Play Mode checks passed, including four allocation groups, complete phase budgets, manual confirmation/rest gates, duplicate suppression, three external acknowledgements and retained input edges/RT. The PASS log was read back; Play Mode was stopped. Evidence: `Evidence/LayerAB/GoogleForms/python-regression.txt`, `unity-validation.txt`, `unity-console.json`. Removed local GET/POST routes return 404; the dedicated clock endpoint and external-link allocation remain covered.
- Console review found two unsupported CLI command-name errors from validation tooling, not task exceptions. They are preserved in `unity-command-errors.json`; after clearing the reviewed buffer, console errors were zero and compilation was successful.
- Pending: real operator submission/export/join rehearsal, full real-time A/B run, Quest Pro and physical timing acceptance, personal model admission and participant effects. This change does not demonstrate a physiological or subjective benefit of B.

See [study guide](LayerABStudyGuide.md) for operation, timing, export and prior engineering evidence.

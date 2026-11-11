# Post-condition questionnaire: experience page

Date: 2026-11-08. Stage 2/3. Revision: `raw-tlx7-ueq4-experience7-preference-google-forms-v5`.

## Implemented change

The researcher requested seven additional questions after the existing workload and overall-impression pages. The existing published post-condition Google Form now has three sections/pages. Its URL and the original thirteen fields are retained. Page 3 is titled **Movie display and training experience** and adds seven required items, in this order:

| ID | Question | Response endpoints |
|---|---|---|
| A1 | How much did the movie clips interfere with the central task? | 1 = not at all; 7 = very much |
| A2 | How appropriate were the times when movie clips appeared? | 1 = very inappropriate; 7 = very appropriate; N/A |
| A3 | How appropriate were the times when movie clips stopped? | 1 = very inappropriate; 7 = very appropriate; N/A |
| A4 | The changes in the movie display matched what I needed at that moment. | 1 = strongly disagree; 7 = strongly agree |
| A5 | I would be willing to let the system control the movie display this way again. | 1 = strongly disagree; 7 = strongly agree |
| B1 | How tired do you feel after this training? | 1 = not at all; 7 = very tired |
| B2 | How uncomfortable did this training feel? | 1 = not at all; 7 = very uncomfortable |

A1, A4, A5, B1 and B2 use horizontal 1-7 linear scales. A2 and A3 use single-choice lists with seven numeric choices and an eighth, nonnumeric N/A choice. No answer is preselected. Their N/A instruction is: "For A2 and A3, choose N/A if you did not notice the relevant event or cannot judge its timing." The central task is identified as the task with the colored sphere.

The form introduction now lists all three pages and the v5 revision. Final preference still has the same questions and choices; only its administration instruction changes from completing both pages to completing all three pages of the second post-condition questionnaire. At v5, the original UEQ+ page and items were unchanged and the explanations below were proposals only. They were subsequently approved and applied in v5.1; see UEQClarificationConsoleReview.md.

## Administration and analysis

Complete all three pages after each round, then submit. After the second round, also complete final preference. The experimenter records oral answers while headset and electrodes remain in place. Unity acknowledgement and rest timing are unchanged. Google Forms responses remain external; no test response was submitted.

Report the seven new items individually as study-specific exploratory ratings. They are not a validated composite scale. Do not add them to TLX or UEQ+ scores. Higher A1, B1 and B2 indicate more interference, fatigue and discomfort respectively; higher A2-A5 indicate more positive appraisals. Keep N/A distinct from missing data and from the midpoint 4. For A2/A3, report the number of numeric answers and N/A answers; exclude N/A from numeric summaries. Do not impute ratings for old responses.

B1 is post-training fatigue, not a measured change from a pre-training baseline. B2 is a broad discomfort item, not a validated cybersickness assessment. A2/A3 are perceived timing, not proof that a controller acted optimally. If a participant cannot judge an item without N/A (for example A4 after no noticed change), do not invent a rating or treat 4 as N/A; record the limitation and retain incomplete data if necessary. Any extension of N/A beyond A2/A3 requires a separate wording decision.

Python and Unity questionnaire constants and the link manifest identify v5 for new allocations. Existing session records and historical v4 example allocations are preserved; generate a fresh allocation for new work. Because the same Google Form was edited in place, its URL is not an immutable version identifier. Link exports to saved session metadata and retain whether Page 3 was actually administered. The seven appended fields are absent in earlier submissions. Do not relabel historical data as v5.

## UEQ+ clarification proposal (subsequently applied in v5.1)

Update: the researcher approved the four item descriptions, which were applied on 2026-11-08 as v5.1. The original common instruction was retained. See [implementation and verification](UEQClarificationConsoleReview.md).

The [official UEQ+ Handbook v4](https://ueqplus.ueq-research.org/Material/UEQ%2B_Handbook_V4.pdf), printed page 5, defines Attractiveness as overall liking of the product and lists the four current adjective pairs. It does not supply the item-specific explanations proposed here. These are study-authored aids and would constitute an administration adaptation requiring comprehension checks.

Retain the original pairs, order, seven positions and scoring. Proposed common instruction: "Rate your overall impression of the VR system during the training you just completed. Use your immediate impression; there are no right or wrong answers."

| Existing pair | Proposed brief explanation |
|---|---|
| annoying / enjoyable | Think about whether the overall experience of using the system felt irritating or enjoyable. |
| bad / good | Give your overall negative or positive impression of the system. |
| unpleasant / pleasant | Think about whether using the system felt unpleasant or pleasant overall. |
| unfriendly / friendly | Think about whether the system gave you an unwelcoming or welcoming impression. |

Do not redefine these as four independent constructs. Do not equate friendly with ease of operation or pleasant with absence of physical symptoms. Do not tell respondents that an adaptive system should feel better, and do not improvise different explanations across rounds. If adopted, record the wording/version and use the same explanation for every participant and round. Existing standard wording and adapted wording should retain separate provenance.

## Verification and remaining checks

Browser-verified: the editor saved three sections; preview navigation reached Page 3 of 3 through the unchanged TLX and UEQ+ pages. All seven titles, endpoints, required markers and both N/A choices were inspected; Page 3 began with no selected answers. Google preview disabled submission. Temporary engineering-only preview entries on Pages 1/2 were cleared, and the preview returned to an empty Page 1 before being closed. The participant's already-open respondent tab was not modified. Final preference's updated instruction was read back after save.

Evidence: [Page 3 screenshot](../Evidence/QuestionnaireV5-20261108/page3-preview.jpg), [preview DOM](../Evidence/QuestionnaireV5-20261108/page3-preview-dom.txt), [editor screenshot](../Evidence/QuestionnaireV5-20261108/page3-editor.jpg). Pre-edit local files are backed up under `Evidence/QuestionnaireV5-20261108/before/`; Google Form edits are external to Git.

Pending: participant comprehension, real operator submission/export/join rehearsal, headset/EEG checks and psychometric validation of the additional items. No participant-effect claim, APK build or new trial is part of this update. Software and service verification are recorded in the project log and acceptance record.

Unity verification references consulted: installed Pipeline `commands/scripts.md`, `commands/build-and-compilation.md`, `commands/editor-lifecycle-and-observability.md` and `commands/runtime.md` for Unity 6000.3.27f2.

Software-verified: all 8 existing A/B tests passed (1.044 s), JavaScript syntax and git whitespace checks passed. Unity recompile completed, compilationFailed=false and current consoleErrors=0; an eval read back the compiled v5 constant. The log buffer retains one earlier Unity AI relay-process error at 20:51 UTC, unrelated to this compile. Play Mode remains stopped; pre-existing auto-tick remains enabled.

Deployment limitation: automatic approval review rejected the idle Console service restart command with "blocked by policy" and no further reason. No restart occurred; health still reports PID 11796. Restart the Console before generating the next assignment so it loads the v5 Python constant, then create a fresh assignment. Do not reuse a v4 allocation with the recompiled v5 Unity code. Form edits are already saved and independent of this local service restart.

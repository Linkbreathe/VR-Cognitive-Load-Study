# UEQ+ explanations and Console questionnaire access

Date: 2026-11-08. Stage 2/3. Questionnaire revision: `raw-tlx7-ueq4-experience7-preference-google-forms-v5.1`. Display revision: `console-questionnaire-links-v1`.

## Request and implemented result

The researcher approved the four explanations proposed in [the experience-page review](QuestionnaireExperienceReview.md), and asked to open the corresponding Google Forms directly from the Console.

The existing post-condition Google Form now displays these descriptions immediately below their original UEQ+ item titles:

| Item | Description |
|---|---|
| annoying / enjoyable | Think about whether the overall experience of using the system felt irritating or enjoyable. |
| bad / good | Give your overall negative or positive impression of the system. |
| unpleasant / pleasant | Think about whether using the system felt unpleasant or pleasant overall. |
| unfriendly / friendly | Think about whether the system gave you an unwelcoming or welcoming impression. |

Original titles, endpoints, required status, order and 1-7 scales are retained. The existing Page 2 introductory instruction is retained. These explanations are study-authored administration aids, not official additional UEQ+ items or independently validated definitions. Use the same descriptions in both rounds. The overall-impression score remains the mean of the four complete ratings after subtracting four. Page 3 and final preference are unchanged.

The form introduction identifies v5.1. Python/Unity revision constants and the link manifest match it. Old allocations and recorded data retain their revisions. The live URL points to the edited form and is not a frozen copy of an earlier questionnaire.

## Console operation

Open **Run monitor**, choose the intended A/B comparison in **Session**, then use **Questionnaires**:

- **Open post-condition questionnaire**: complete after each round.
- **Open final preference**: complete once after both rounds.

Buttons open the responder links saved in the selected session configuration in a new tab. The card displays the exact session ID and participant code. Training 1/2 means temporal order, regardless of whether A or B came first. The Console neither fills answers nor verifies submission. Check the Google Forms confirmation before acknowledging in Unity. Google may restore an existing respondent draft; check identifiers and occasion before entering/submitting answers.

Practice/calibration sessions do not show these A/B links, including inherited legacy form defaults. Missing or invalid links are not replaced with a different form. URLs pass the existing HTTPS Google Forms validator and are escaped before display. Selecting a different session clears prior links while the new snapshot loads. The earlier **Guide → Adaptive Comparison → Current A/B questionnaires** links remain available.

## Verification and boundaries

The form was saved, and preview navigation verified all four exact descriptions under the original questions, unchanged endpoints and no selected UEQ+ answers. Temporary engineering preview values were cleared and preview closed; no response was submitted. The user's existing respondent drafts were not edited. The public responder page showed v5.1.

The served Run monitor showed the two links from the selected recorded A/B session. Both buttons opened their expected Google Forms, whose titles and URLs were inspected without changing answers. Switching sessions cleared the previous links. Static assets are served directly from disk, so these Console buttons are available after a browser refresh.

Software checks: eight existing A/B tests passed (1.069 s), JavaScript syntax and existing decision-display contracts passed. Unity recompile completed; compiled v5.1 constant read back successfully; `compilationFailed=false`, current `consoleErrors=0`. An earlier Unity AI relay-process error remains in the log buffer. Play Mode remains stopped and the pre-existing auto-tick setting remains enabled. Installed Pipeline command references were read during the preceding v5 update; no control logic changed.

The prior automatic-approval rejection of the Console service restart remains unresolved. The existing process has not been restarted in this update. Before creating a new assignment, restart the Console so its Python revision matches the compiled Unity v5.1 revision, then create a fresh assignment. This does not prevent opening the forms from the new browser buttons. No device, participant, response-export or psychometric acceptance was performed.

Evidence and restore copies: `Evidence/QuestionnaireV5-1-20261108/`, including [UEQ+ preview](../Evidence/QuestionnaireV5-1-20261108/ueq-preview.jpg), [preview DOM](../Evidence/QuestionnaireV5-1-20261108/ueq-preview-dom.txt) and [Console buttons](../Evidence/QuestionnaireV5-1-20261108/console-questionnaires.jpg). Google Form changes are external to Git.

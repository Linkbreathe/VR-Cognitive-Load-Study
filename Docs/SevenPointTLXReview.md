# Seven-point Raw NASA-TLX adaptation

Date: 2026-11-05. Revision: `raw-tlx7-ueq4-preference-google-forms-v4`. Scope: opt-in Layer A/B study. Source rollback: `2e66baf`; external Forms edits are not reverted by Git.

## Literature checked

- Yu et al. (2021), *Gaze-Supported 3D Object Manipulation in Virtual Reality*, CHI, DOI [10.1145/3411764.3445343](https://doi.org/10.1145/3411764.3445343). Section 5.3 explicitly describes Raw NASA-TLX using seven-point scales. This supports a seven-point unweighted workload instrument in VR. [Author PDF](https://www.jorgegoncalves.com/docs/chi21c.pdf). The passage does not establish that our exact endpoint presentation or oral administration was used in their study.
- Mott et al. (2017), *Improving Dwell-Based Gaze Typing with Dynamic, Cascading Dwell Times*, CHI, DOI [10.1145/3025453.3025517](https://doi.org/10.1145/3025453.3025517). Printed page 2566, under Subjective Preferences and Workload Ratings, explicitly retains all six dimensions and replaces the traditional 21 positions with seven at collection time. Ratings follow use of each technique. [Author PDF](https://faculty.washington.edu/wobbrock/pubs/chi-17.02.pdf). This is an administration precedent; it does not alone establish our aggregate scoring convention.
- Lee et al. (2026), [DOI 10.1145/3837858](https://doi.org/10.1145/3837858), reviews 522 CHI papers. The [coauthor's explanation](https://kaikunze.de/post/2026-08-16-nasa-tlx-in-chi/) explicitly recommends retaining 21 positions and discusses altered factor structure with coarse ratings. We therefore document seven points as a user-selected adaptation for simpler, consistent administration, not as a psychometrically equivalent or superior replacement. The exact claimed 96/50 counts were not independently verified in the accessible material and are not relied upon here.

## Frozen project implementation

Page 1 contains linkage fields and all six NASA-TLX dimensions. Each is a required horizontal linear scale with integers 1 through 7, no default answer. Keep the existing English question wording. Mental Demand, Physical Demand, Temporal Demand, Effort and Frustration use Very low=1 and Very high=7. Performance uses Perfect=1 and Failure=7; higher already means worse and must not be reversed again. Introduce and explain the six dimensions consistently during preparation, and read both endpoints at each administration.

Compute the overall workload score as the arithmetic mean of six complete ratings, retaining the six individual ratings. Output range is 1-7; higher is greater workload. Do not convert to 0-100 and imply original-scale equivalence. Missing responses remain missing. Keep UEQ+ Attractiveness separate (mean of four ratings minus 4, range -3 to +3). Final preference remains a separate descriptive item.

After each 96-trial condition, administer TLX then UEQ+ in two Google Forms pages. After the second submission, administer final preference. The experimenter enters spoken answers with headset/electrodes in place. This oral procedure is our adaptation; pilot comprehension remains unverified. Existing acknowledgement/rest, task timing, RT/button edges and LSL alignment are unchanged.

Suggested methods wording: We administered a seven-point adaptation of the Raw NASA-TLX after each condition, following seven-point precedents in HCI research (Mott et al., 2017; Yu et al., 2021). All six dimensions were retained and the overall score was their unweighted mean. Ratings ranged from 1 to 7; higher values indicated greater workload, with Performance anchored from Perfect to Failure. An experimenter recorded participants' spoken responses while their headset and electrodes remained in place.

## Verification

The published first page shows six required seven-position radio groups, correct anchors, no selected response and Next to page 2. UEQ+ and preference were not edited. No answer was entered or submitted. Browser evidence is in `Evidence/LayerAB/GoogleFormsV4/`. Revision metadata is synchronized across Python, Unity, the form-link manifest and four engineering examples. New allocations are required; old recorded data are not rewritten. Eight targeted A/B Python tests passed. Unity recompile completed; console verification is saved in `Evidence/LayerAB/GoogleFormsV4/unity-status.json`. No Play Mode/full-suite rerun or APK was produced for this metadata-only source revision. Actual submission/export linkage, participant effects and physical Quest Pro acceptance remain pending.

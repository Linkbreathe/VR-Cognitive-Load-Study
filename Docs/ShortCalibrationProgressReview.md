# Short calibration and completed-trial progress

The acquisition/counting rules below remain current. The later [dev3 desk-sign update](DeskSignHudReview.md) supersedes the unboxed floating-text layout and its two presentation revisions; dev2 artifacts and evidence remain historical.

Date: 2026-11-03. Stage 2 personalized protocol update, delivered with the optional Stage 3 adapter in engineering candidate `3.0.0-stage3-dev2` / Android code 26. The user approved a 3–4 minute calibration and a right-side progress count opposite the left-side countdown.

## Acquisition budget

New configurations and allocations use four alternating L0/L1 blocks, twelve trials per block: **48 × 4.5 s = 216 s (3:36)**. Each condition receives two blocks. The first failed model attempt can offer the existing two-block supplement: another 24 trials / 108 s, making 324 s (5:24) of calibration. Explicit six- and eight-block configurations remain valid; existing participant allocations are not silently rewritten.

Practice remains at most fourteen trials / 63 s and is skippable. Each training phase remains 96 trials / 432 s. With full practice and no supplement, timed activity is **19:03**; with the supplement it is **20:51**. Instructions, preparation, model fitting, oral questions, pauses and rest are additional. Calibration duration does not include Python fitting and permutation computation.

The actual local engineering override was changed from eight to four blocks, preserving all its other settings. Its pre-change copy is `Evidence/ShortCalibrationProgress/engineering-config-before.json`. Participant allocation files and historic recordings were not migrated. Restart the updated Python service and begin a fresh session. A previously generated configuration containing `calibrationBlocks: 8` will still request eight blocks; use a new allocation for the short protocol.

Shorter acquisition provides fewer blocks and fewer potentially eligible windows. It can increase class-coverage, held-out-validation or permutation failures; no failure rate has been measured for the short protocol. There are only two blocks per condition before supplementation, leaving less tolerance for unusable or behaviorally uniform blocks. Signal, class-count, varying-block, AUC, permutation, policy-freeze and device gates are unchanged. Failed qualification is not automatically converted to an admitted physiological model; the existing explicitly labeled behavioral fallback remains available under its original rules.

## Running display

The existing countdown stays to the left of the sphere. A matching unboxed progress display is created at the mirrored position on the right:

| Phase | Example | Scope |
| --- | --- | --- |
| Practice | `2/14`, `Completed` | Completed practice trial slots |
| Calibration | `2/12`, `Block 1/4` | Completed slots in the current twelve-trial block |
| Supplemental calibration | `2/12`, `Extra block 1/2` | Current supplemental block |
| Training 1 or 2 | `24/96`, `Completed` | Entire current training phase, including internal segment boundaries |

The count increases when a trial ends, not when a button is pressed. It includes observe-only trials and consumed interrupted slots, matching the executor's fixed budget; it is neither correct-answer count nor valid physiology-window count. At a calibration block boundary it resets to `0/12` as the block caption advances. No extra end-of-block dwell is added. Practice skip moves to calibration at the existing safe boundary. Progress is hidden on preparation, instruction, pause, questionnaire and model-wait screens. Training labels retain the existing participant-facing ordinal without revealing the strategy.

Task geometry, scene, response scoring, four-second response/visibility, 4.5-second cycle, media scheduling and feedback rules are unchanged. The progress number stays neutral even when practice countdown feedback is colored. Stage 1 is unaffected.

Metadata records `personalized-lab-v2.4-trial-progress`, `trial-progress-v1-completed` and `calibration-block-budget-v2-short-default`. Session events record the display/count scope and calibration budget. Python `personal_protocol.json` records initial trials/seconds and the supplemental budget. No participant data is mixed with the synthetic checks or previews.

## Verification

- Updated Python host regression: **124 tests passed**, including three new allocation/contract/qualification tests, in 49.096 s. The independent Evidence Guard core was unchanged; its fourteen previous passing tests are retained Stage 3 dev1 evidence, not a new run for this UI/budget change.
- Unity compilation and **81 synthetic progress/budget/UI/metadata checks passed**. Coverage includes early responses, neutral display, pause/resume, skip, four calibration blocks, supplement, unique trial identities, training segment boundaries, text fit and non-admission after failed models.
- After the final caption-size adjustment and build, the 81-check run passed again and its PASS log was read back. The final live console sample reported zero errors/warnings; this does not erase retained earlier tooling/build warnings. Play Mode is stopped and project PlayerSettings have no remaining diff.
- Full executor validation: **21,269 assertions passed**, covering four/six/eight blocks across all four counterbalance groups. The initial calibration lasts 216 s on the synthetic clock and 324 s with supplementation.
- Fresh Editor camera captures for calibration `2/12`, practice `2/14` and training `24/96` were produced with intentionally static synthetic previews. They verify display placement, not real-device usability or physiological performance. See `Assets/Evidence/ShortCalibrationProgress/`.
- Evidence and source restore ZIP: `Evidence/ShortCalibrationProgress/`. The scene, media runtime, model extraction/training/validation and frozen-policy source files are compared with the pre-change hash record. Existing uncommitted Stage 3 work is retained.

All six protected files match the pre-change SHA-256 manifest: model, signals, validation, policy, media runtime and scene. Whitespace checking passed with only Git line-ending notices. Quest Pro readability, comfort, standalone performance, real EEG/ECG calibration yield and model qualification remain pending. Passing software fixtures does not establish these outcomes.

## Delivery

The candidate and full engineering package are under `Builds/Stage3Dev2/`. Use its four-block engineering examples or create a new participant allocation; a persistent older override takes precedence over the defaults. Evidence Guard remains off by default. The AV library is reused from the existing reviewed deployment and is not embedded in the APK or delivery archive. Use the [Stage 3 guide](EvidenceGuardGuide.md) for service/package setup and the [experiment guide](ExperimentGuide.md) for the protocol.

- Android IL2CPP build succeeded in **246.180 s**, zero build errors and 347 warnings. These include existing Sentis shader warnings, Quest Link member hiding and diagnostic-symbol configuration; they are not short-calibration acceptance evidence.
- `Builds/Stage3Dev2/EvidenceVR-Stage3-dev2.apk`: **100,265,262 bytes**, SHA-256 `9f0d88804951dd0232cbf179027bb7eaad36a936d62610f1095933c89f3178a8`. Manifest confirms `3.0.0-stage3-dev2`, code 26, `arm64-v8a` and the eye-tracking permission. APK v2 signature verified. BuildReport size includes intermediate files and is not the APK size.
- `Builds/Stage3Dev2/EvidenceVR-Stage3-dev2-delivery.zip`: APK, unchanged guard wheel, updated host/configuration tools, runtime sources, review/operating documents, engineering evidence and three UI previews. `release-manifest.json` records file hashes. Packaging verifies ZIP CRCs and each archived file's SHA-256; the archive identity is recorded outside it in `Evidence/ShortCalibrationProgress/package-result.json`.
- Three complete four-block engineering configurations select off, observe or enforce. The unchanged wheel remains `evidence_guard-0.1.0-py3-none-any.whl`. No local environment, raw participant recording or AV source library is included.

The Unity CLI briefly lost its server connection during domain reload after recompilation and after entering Play Mode. The server recovered; build completion and Editor status were checked directly. Earlier validation-helper compile errors were corrected before the passing run. These are engineering tooling observations, not a device test.

Project PlayerSettings are restored after building; the separately built APK carries its own dev2/code-26 identity. No headset installation, participant recording, commit, push or upload is included in this update.

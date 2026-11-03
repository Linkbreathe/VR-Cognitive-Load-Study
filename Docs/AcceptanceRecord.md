# Current acceptance record

Updated 2026-11-03. Current application `2.0.0-stage2-dev12`, Android code 24, environment `warm-study-v1`; configuration/HTTP contract `2.0.0-stage2-dev11`; protocol family `personalized-rf-v2`; updated Python policy `personalized-rf-pilot-v3-control70-40`. Engineering candidate; no physical or scientific acceptance is implied.

## Controller guidance UI v1 (current source)

Implemented: static trigger illustration, prominent A/B keycaps, current/pending/ready card states and a completed-control count; compact A/B legends outside preparation; `controlHintRevision=controller-guidance-v1` session metadata. No control remapping, task-timing, scoring, model or running-HUD change.

Software verified: Unity compilation and 113 engineering keyboard/input, text-fit, metadata and screen-transition assertions; four PASS logs read back; initial/intermediate/ready/instruction captures visually inspected; eleven protected source/configuration files unchanged. Current console zero errors/two warnings. See [UI review](ControlHintUiReview.md) for evidence and resolved helper/initialization issues.

Source/scene only; existing dev12 APKs retain their original UI. Quest Pro physical readability, controls, comfort and standalone performance remain pending. No new participant or physiological acceptance.

## Personalized control policy v3 (current)

Implemented: Stable BA >= 0.70 with omissions < 4; Struggling BA <= 0.40 or total omissions > 5 within the latest twenty trials; no online RT ceiling or consecutive-omission trigger. Model score thresholds are <= 0.45 / >= 0.65. No-correct-response windows retain descriptive RT=null and can support difficulty. Policy files, decision audit and old-policy/model rejection updated; labels/admission/device gates unchanged.

Software verified: 111 Python tests passed in 26.239 s, including 17 new regressions and real localhost HTTP/cache/audit checks. Nine synthetic Python decisions passed 73 isolated-engine Play Mode checks for deserialization, media-safe deferral, ON/OFF/HOLD execution and events. Helper compiled without diagnostics; PASS log read back. Current console: zero errors/two warnings, with historical retained messages preserved. Initial serialization and ephemeral fixture-wrapper issues resolved; see [review](PersonalizedControlRulesReview.md).

No new APK or physical session. Existing dev12 APK/wire contract remain compatible; existing ZIPs retain their original policy. Restart the updated service and begin a fresh session/calibration. Threshold efficacy, real sensors, personal model qualification and Quest Pro acceptance remain pending. Evidence: `Evidence/PersonalizedControlRules/`.

## Warm study environment (dev12, current)

Implemented: cream/oak study, bookshelves, linen reading chair, static plants/practical lamps, fixed baked lighting and a narrow HUD contrast backing. The user-selected study replaces the unshipped gray lab draft. Session metadata records `warm-study-v1`; all conditions share the scene.

Software verified: compilation; reopened scene/material/lightmap persistence; 421 checks including 405 clear sampled movie rays; 15,945 retained engine assertions; seven live frame/metadata/timing/clearance checks; three room captures and live task readability. Ten protected source/config/material files match the restore ZIP byte-for-byte. New furnishing root: 124 renderers, 13,419 triangles; two lightmaps and seven baked lights, with the original realtime task light retained. Final live console ground truth had zero errors/warnings; historical resolved errors and unrelated log messages remain recorded.

Android build succeeded in 177.607 s with zero errors and 347 recorded warnings (345 existing Sentis shader warnings, one existing Quest Link member-hiding warning and one crash-symbol warning). APK: 100,260,882 bytes, ARM64, code 24/name `2.0.0-stage2-dev12`, APK v2 signature verified. SHA-256 `2703d6356fe470ea212379c4b97a905d5f6325c1c11a84c352fdf1d62bf63319`. BuildReport totalSize includes intermediate artifacts and is not the APK size. No headset installation was performed.

Pending: physical Quest Pro appearance, comfort, colors, scale, distraction, standalone frame time/thermals and full sessions. No new participant, sensor or scientific qualification. [Detailed review](LabEnvironmentReview.md), [build/release](LabEnvironmentRelease.md).

## No-rest flow (dev11, retained in dev12)

Implemented: preparation and signal checks -> practice -> calibration; no timed closed/open-eye baseline or Python resting-reference output. Flow revision `preparation-practice-calibration-no-rest-v1`; presentation `personalized-lab-v2.3-preparation-to-practice`. Default timed activity is 22:39; preparation, model fitting and questionnaires/rests are additional. See [review](NoRestProtocolReview.md) and [release](NoRestProtocolRelease.md).

Software evidence:

- Unity compilation passed. 15,945 synthetic-clock assertions cover all four groups and six/eight calibration blocks, durations, budgets, memory, supplementation, interruptions and safe-boundary decisions, with no resting stage in any complete flow.
- The unchanged combined Python suite passed 94 tests in 26.617 s. Model/label/quality algorithms remain unchanged. The dev10 production-size permutation benchmark and P007 audit below are retained historical evidence, not newly performed dev11 experiments.
- A fresh engineering Editor session passed 15 live preparation/input/gate assertions: incomplete seating/A/B checks block continuation; full preparation leads directly to practice; skipping occurs at a trial boundary before calibration. Transient mock media readiness and signal-status fields verified participant connection gates; all mocks were restored. The actual media review gate remains false. No device or EEG/ECG source was connected. Both UI screens were inspected and the new runtime flow log was read back.
- Earlier harness attempts exposed an unreviewed-media assumption, unfocused synthetic-keyboard routing and duplicated instruction/start stage events; only the harness was corrected. The final fresh-session run passed. Unity AI NoSubscription messages remain unrelated historical console noise.
- Android build succeeded in 83.560 s with zero errors and 350 recorded warnings. APK: 95,981,506 bytes, ARM64, code 23/name `2.0.0-stage2-dev11`, v2 signature verified. SHA-256 `d28376148519195cf5e7a0fd23797c3d38d63911f1fcf0c068af4476bbcdd62e`. Three warnings concern orphan metadata files from relocated validation screenshots, subsequently removed by the Unity import refresh. No runtime source changed after building. No ADB device was attached, so no installation or physical acceptance was performed.

Evidence: `Evidence/NoRestProtocol/`, `AgentScripts/PersonalizedProtocolValidation.cs`, `AgentScripts/NoRestProtocolValidation.cs`. Restore ZIP/patch preserve the pre-update dirty tree. No installation/device check is implied by software evidence.

Pending: actual Quest Pro presence/eye/comfort/standalone/full-session tests, real EEG/ECG contact/mappings/units/timing, label/AUC/permutation qualification, oral Forms submission/export rehearsal and model admission/scientific effects. All existing quality/admission gates remain in place.

## Personalized validity revision (dev10, historical)

Implemented: five-trial window-local score labels and buffer, circular-label null admission checks with block-held-out refitting, within-condition/time gates, practice-only correctness feedback, oral headset-on questionnaire procedure, latched mount invalidation/restart and per-channel EEG diagnostics. See [review](PersonalizedValidityReview.md).

Software evidence:

- Unity compilation passed. 15,965 pure-engine protocol assertions retained exact timings, budgets, memory, allocation, interruption and safe-boundary behavior. 79 additional checks exercised correct/incorrect/omitted answers across practice/calibration/Random/Bioadaptive on the actual scene UI, confirmed neutral non-practice feedback with unchanged scoring, and checked mount latching, stale model refusal and Stage 1 feedback preservation.
- The final combined Python suite passed 94 tests in 16.984 s. It includes exact window membership/no-future-label checks, scoring/buffer and low-Match coverage, circular-transform structure, conservative incomplete-null handling, invariant block-label rejection, units/channel diagnostics and mount/cache/analysis invalidation. The separate production 300-tree/199-null run is recorded below; the smaller-forest unit fixture is explicitly identified.
- Production-size engineering integration completed with 300 trees and all 199 full circular-null refits in 591.192 s (about 9:51) on this computer. The intentionally separable synthetic fixture had observed within-condition AUCs of 1.0 and null tail value .005, with zero unusable draws. These are fixture checks, not measured cognitive-state accuracy. Admission remained false for unfrozen/unqualified policy, unqualified circular assumptions and synthetic session provenance. The report and null draws are retained in `ProductionForestSynthetic/model_report.json`; model binaries are excluded from release.
- A new engineering Editor session `20261102T223346621Z_17cdfa3a` advanced real frames through calibration, accepted a programmatic incorrect answer and read back neutral countdown text/color. Oral-question UI was inspected. This direct-phase preview is explicitly marked synthetic engineering UI evidence, not a full timed session or sensor test. No headset/runtime was connected. The successful smoke's console ground truth reported zero errors; the session history also retains an earlier harness/domain-reload failure, a transient GC-handle assertion and unrelated Unity AI NoSubscription logs.
- Original replay audit reconstructed all fifteen EEG-evaluated P007 windows with exact acceptance fractions. Failures were amplitude-only, not flatline; source columns 1/2/3/4 had 133/244/92/296 failures respectively out of 315 overlapping window-subsegments. No original XDF bytes, mappings or thresholds changed. A separate read-only header inspection confirmed declared uV/500 Hz.
- Android dev10 build succeeded in 174.291 s, zero errors and 347 recorded warnings. APK: 95,983,490 bytes, ARM64, code 22/name `2.0.0-stage2-dev10`, APK v2 signature verified. SHA-256 `2aed3232fc410a15687c4c40f6825b601b2e78af2543c5161e9424f1c505fc63`. See [release instructions](PersonalizedValidityRelease.md). No installation/device acceptance was performed.

Evidence: `Evidence/PersonalizedValidity/` contains the restore ZIP/patch, test outputs, channel audit, source-header inspection and UI captures. Scripts: `PersonalizedValidityValidation.cs`, `validate_personalized_validity.py`, `test_personalized_validity.py` and `diagnose_personalized_eeg.py`.

Pending: physical Quest Pro user-presence support and handling, live electrodes/contact/units and R-peak validation, label and circular-null assumptions, frozen AUC/score gates, oral questionnaire submission/scoring/export rehearsal, media/volume review, eye calibration/comfort/standalone/full-duration operation and scientific effects. A model passing a synthetic null fixture is never participant qualification.

## Personalized two-level protocol (dev9, historical)

Implemented: the timed flow, four-cell counterbalancing, two AV levels, external-form breaks, personal RF training/self-test/freeze/failure paths, five-trial decisions, quality gates and audited boundary execution. See [protocol review](PersonalizedProtocolReview.md).

Software verified:

- Unity compilation: zero errors. 15,965 pure-engine assertions passed across all allocation cells and six/eight calibration blocks, exact nominal timings, continuous memory, transition flags, supplementation, practice skip and pause boundaries.
- Python: 81 combined legacy/dev9 tests passed with the user-selected retrospective default. Fixtures cover signal quality, 56 candidate windows, labels, class gates, held-out blocks, comparators, artifact identity/hash, provenance, idempotency, stale packets and raw acquisition to failed-model report. Signal regression includes amplifier DC offsets; defaults remain unqualified.
- Play Mode: advancing frames confirmed with Pipeline job wait; baseline UI and actual L1 AV playback inspected. Session `20261102T204303650Z_74ac71f8` is `editor-test` with an `engineering_preview` acceleration marker. The preview's resting minutes were shortened for UI review only; exact times were independently checked in the pure engine. All 22 pairs loaded; `Media.Ready=false` correctly retained human volume review. Zero console errors; two XR audio fallback warnings are not device acceptance.
- Android APK built successfully in 494.0 s, zero errors and 347 warnings: 345 existing AI Inference/Sentis shader warnings, one existing Quest Link member-hiding warning, and one crash-symbol warning. APK is 95,982,146 bytes, ARM64, version code 21, version name `2.0.0-stage2-dev9`, and APK v2 signature verified. SHA-256 `0205f9085e60f33cf301ed380e9b6f2d323b64e70f623f9b0190a0f169e75e65`. BuildReport totalSize includes intermediate artifacts; it is not the APK size.

Pending, not passed: actual EEG/ECG hardware/montage and R-peak qualification, frozen AUC/label/score policy, true clock/onset agreement, questionnaire scoring/submission/export linkage, media review, Quest Pro comfort/controllers/eye calibration/standalone and full device sessions. The user's retrospective labels are a provisional engineering choice. P007 is recorded replay only.

Evidence: `Evidence/PersonalizedProtocol/unity-validation.txt`, `python-tests.txt`, baseline/practice images, `preview-summary.json`, build report and pre-change restore ZIP/patch. Helpers: `PersonalizedProtocolValidation.cs`, `PersonalizedPreview.cs`, `test_personalized.py`.

Delivery: `Builds/Stage2PersonalizedDev9.zip` contains the signed APK, media, Python tools/configurations and documentation. Archive CRC verification passed; `Evidence/PersonalizedProtocol/release-package.json` records its size and SHA-256. [Release instructions](PersonalizedRelease.md). Allocation with the published form URLs and descriptive export of an incomplete engineering session also completed successfully.

Additional dev9 transport check: actual Unity HTTP telemetry reached the local service with eight completed practice trial records and a measured software clock uncertainty of approximately 3.3 ms in editor-test session `20261102T205456651Z_9e7e4f35`. No sensors were connected in that smoke. This is a localhost transport check, not physical onset synchronization. The smoke service and Play Mode were stopped. Existing Editor auto-tick was retained. The local override is now dev9 engineering/ACTIVE, gaze debug off; its previous contents were backed up by the enable command. The APK starts Stage 1 until an enabled override is deployed. ADB inspection found no connected devices, so no installation or device acceptance was performed.

The supplied `D:/Code-Space/patent_code/P007/sub-P007.xdf` was inspected read-only: Hyperscanner `02_007_5_eeg`, 500 Hz, 895,494 rows, nine columns, about 1,791 s. Column 0 is the sample index and columns 1-8 are labeled ch0-ch7 in uV. The user confirmed EEG columns 1-4 and differential ECG column 7 minus column 8. The separate replay policy uses all four selected EEG columns without claiming anatomical locations, and retains recorded-data/non-admission flags.

Actual recorded-source check: `20261102T210152833Z_a1cbfb03` completed 32 calibration stimuli with no participant answers after an explicitly accelerated resting/practice setup. Replay -> LSL -> Python -> Unity recorded 1,298 raw chunks with four mapped EEG channels and one differential ECG channel, all marked synthetic. Seventeen aligned candidates were logged: 15 signal-quality failures (all failed the EEG subsegment gate; ten passed the ECG gate), and two excluded transition windows. Zero valid model windows and no model admission. The last software clock uncertainty was 3.23 ms; this is not physical synchronization. A prior shorter run exhausted its source before complete window coverage and is retained separately. Thresholds were not relaxed. See `p007-replay-summary.json`, `P007Replay2/` and `P007Service2/` under the evidence directory.

The two pilot Google Forms were created and published, with eight required calibration questions and fifteen training questions (fourteen required). Their respondent pages, scales, options and required flags were inspected. Email collection and respondent access to results are off; nobody was notified. No questionnaire response was submitted. See [links, provisional Raw TLX scoring and pending rehearsal](QuestionnaireSetup.md). URLs are in the configuration template and current engineering override. Final cleanup stopped Play Mode and owned replay/service processes. The Editor remains on Android after the build; auto-tick remains as originally configured.

## Historical dev8 acceptance summary

Updated: 2026-11-02. Source/application `2.0.0-stage2-dev8` (next Android code 20); last built APK dev7/code 19; training configuration/controller remain dev5. **Quest Pro Link now returns real tracked eyes with the engineering Editor compatibility feature; the user confirmed the visible ray follows eye movements. Calibration accuracy, standalone and final physiological/device acceptance remain incomplete.** Historical evidence is retained below.

## Source-first LSL selector (dev8 follow-up, current)

Implemented: Scan / select source / Connect, with optional processing under Advanced settings and default receive-only numeric/text input. Exact UID separates shared source IDs and names. Irregular event silence is idle. Status revision `mode-status-v4-source-identity` retains UID/type in audit events. Task/controller behavior remains unchanged.

Software verified: 62 Python tests; compilation; 16 real-outlet Editor checks; five profile/revision checks; actual LSL -> Python -> HTTP -> Unity exact-instance receipt without physiological feature generation. Compact panel and preparation HUD inspected. Tests stayed in engineering directories, original runtime configuration was restored, and test processes stopped. Two XR audio fallback warnings occurred without a headset; no task runtime errors. Evidence: `Evidence/LslSourceFirst/`; [review and operation](LslSourceSelectionReview.md).

No new APK, headset readability/stereo check, standalone check or physiological acceptance is claimed. The following initial-selector record is historical; its toggle-based workflow is superseded.

## LSL source panel and source-name status (dev8 follow-up)

Editor usability follow-up: Python executable/HTTP port moved into default-collapsed Advanced settings; raw brain/heart and precomputed-feature labels clarified. Compilation, closed-foldout readback and panel visual inspection passed. This is an Editor-only presentation change; source configuration and acquisition behavior were preserved, with no new APK or physical-device claim.

Implemented: external-to-VR Editor source panel, actual outlet discovery, raw EEG/ECG mappings and explicit precomputed FEATURES input, frozen service configuration, owned-process lifecycle and no source changes during Play. `mode-status-v3-source-name` displays actual source names with receipt state and keeps the engineering eye line separate. Full IDs/provenance remain in events and raw records; existing physiological admission and participant isolation are preserved. See [review and operation](LslSourceSelectionReview.md).

Software verified: 57 Python tests; compilation; 20 Editor checks including real outlet discovery, configuration locks and process arguments; actual 250 Hz EEG plus 2 Hz feature fixtures over LSL -> Python -> HTTP -> Unity. Source metadata and active receipt were read back, external feature labels/values remained observation-only, and UI screenshots were inspected. Test data stayed in `editor-test`. Original Editor configuration restored and owned bridge stopped. Two XR audio fallback warnings occurred with no headset runtime; no task runtime errors. Evidence: `Evidence/LslSourceSelection/`.

Pending: physical source-label readability/stereo/comfort, standalone build/device behavior, real sensor calibration and participant acceptance. No new APK; last build remains dev7. Current source receipt is not evidence of validated physiological control.

## Fixed sphere-to-tray clearance (dev8 follow-up)

Implemented 2026-11-02 following the user's explicit clarification: hold the sphere-to-tray gap fixed while retaining tray height behavior. `sphere-tray-clearance-v1` places the sphere bottom 0.0864 m above the tray renderer's top after calibration/placement. The value retains the approximately 8.6 cm observed pre-change gap. Only sphere world Y is adjusted; tray placement, horizontal position, sphere size, timing and scoring are unchanged. Metadata stores the placement revision and clearance; placement events record sphere position. The existing focus region automatically follows the actual sphere/tray bounds. New headset comfort/alignment remain pending; no APK build.

Software-verified: compilation passed; 36 isolated checks across three tray heights and three sphere diameters confirmed constant surface gap, unchanged tray/horizontal position and no repeated-placement drift. Live scene readback measured 0.08639991 m clearance, tray center Y 1.01 m and sphere center Y 1.200012 m; the existing tray-height formula and session metadata were checked. Runtime placement logs were read back. Evidence: `Evidence/FixedSphereHeight/validation.json` and `console.json`. Synthetic objects were removed immediately and never supplied production gaze input. Play Mode remains in Preparation for user review; auto-tick remains enabled.

## Sphere-and-tray task focus area (dev8 follow-up)

Implemented 2026-11-02 at the user's request: `gaze-roi-v4-sphere-tray-area` uses a continuous box enclosing the sphere and tray renderer bounds with 4 cm padding per side, including the gap. Focus is region membership, not correct-answer feedback or a fixation estimate. During running trials the visible tray keeps the area active between sphere presentations. Sphere outlines still require a visible sphere; participant overlays remain prohibited. Metadata/CSV revision and `gaze_task_region` geometry events expose the definition. `task_region_visible` is appended to the CSV; existing `task_visible` retains sphere visibility semantics. See [operation and analysis implications](EyeRayLinkReview.md). Physical comfort, alignment and agreement with intended focus require user assessment.

Software-verified: compilation passed; 2,042 synthetic classifier/gating/shader checks passed. Six checks using actual scene bounds and synthetic directions passed for the sphere, tray, gap, both tray edges and inactive region. Session revision, CSV header and the geometry event were read back. Current uncalibrated preview bounds measured 0.3800 x 0.3736 x 0.3800 m, automatically following scene placement. Evidence: `Evidence/GazeArea/model-result.json`, `scene-result.json`, `console.json`; helper `AgentScripts/GazeFocusAreaValidation.cs`. These directions were never injected into the production eye reader. Play Mode remains in Preparation for user testing; the existing auto-tick remains enabled. No new physical acceptance or APK build.

## Eye-line comfort and wider sphere region (dev8 follow-up, superseded ROI)

Implemented on 2026-11-02 at the user's request: a continuous white engineering eye line (1.5 mm, alpha 0.22, 0.65 m near clearance) replaces cyan dashes. Shared sphere gaze half-angle increases from 3 to 4.5 degrees. Revisions are `engineering-gaze-ray-v2-soft-white` and `gaze-roi-v3-task-4p5deg`; metadata and gaze CSVs distinguish the new definition. Visible sphere geometry and task rules are unchanged. Previous physical confirmation applies to gaze acquisition and the earlier cyan visualization; comfort and target tolerance of this new presentation require user assessment. See [current operation](EyeRayLinkReview.md).

Software validation for the comfort revision: compilation passed; 2,033 synthetic classifier/gating/shader checks and 17 isolated line checks passed. The latter cover line style, endpoints, participant lockout, invalid/stale/focus/phase clearing and live disable. Runtime log and `session.json` readback confirmed both new revisions. Synthetic images are under `Evidence/GazeComfort/`; no synthetic pose was injected into the production reader. The current smoke reported no eye device, so no new hardware comfort or tolerance pass is claimed.

## Quest Pro Link compatibility follow-up (dev8)

Physical evidence: after retrying the same eye-only action space with the standard aggregate path, native creation succeeded, the production reader returned `tracked=true` with a nonzero pose, and the user confirmed eye-driven cyan dashes. This supersedes the earlier Link acquisition failure below. It is not a quantified accuracy or stereo-alignment assessment.

Software evidence: final C# compilation, 11 retry-boundary checks, x64 native structure layout and Standalone-only feature configuration passed. Revision `quest-link-eye-space-v1` is gated to enabled engineering Editor sessions; it does not apply in participant mode or Android players. Metadata and transition events expose its use. A project-local Oculus runtime selection avoids the observed Windows VirtualDesktopXR/Link mismatch without changing global settings. See [operation, evidence and limits](EyeRayLinkReview.md#resolved-engineering-link-acquisition-2026-11-02-follow-up).

## Engineering dashed eye ray and Link diagnosis (dev8)

Implemented and software-verified: default-off true-eye dashed ray, live engineering-only switch, phase/invalid/stale/focus clearing, status display and metadata/events. Compilation, 72 isolated ray tests and 2,033 existing classifier/gating/shader checks passed; synthetic rendering inspected. Participant rendering remains prohibited. The local Editor engineering override is enabled for debugging.

Live evidence: user reported seeing the room while wearing a calibrated Quest Pro; readback showed tracked head/controllers and untracked eyes. Oculus 1.208.0 rejects eye action-space creation with `XR_ERROR_PATH_UNSUPPORTED` for `/user/eyes_ext`. This persisted in a process-only no-API-layer comparison. Environment restored. Audit: 1,506 production engineering gaze rows, all invalid. No genuine eye-acquisition pass, stereo-accuracy pass or standalone pass is claimed. ADB remains unauthorized. No dev8 APK built. See [detailed review and evidence](EyeRayLinkReview.md).

## Meta Quest Pro eye permission (dev7)

Implemented: explicit Android Stage 2 eye-permission request before trials, request/grant/denial events and authorization checks before accepting gaze. Existing Meta Quest Support, Quest Pro device declaration, Touch Pro controls and OpenXR Eye Gaze remain enabled. Participant highlights stay prohibited; debug remains default off. See [Quest Pro support](QuestProSupport.md).

Verified: Editor C# compilation, 2,033 synthetic geometry/lockout/shader checks and a separate `editor-test` smoke session. The desktop permission route and debug-off state were read back with zero current console errors. The user's local configuration hash remains unchanged. No ADB device is connected. Android system permission interactions are not exercised by the Editor smoke.

Delivery verified: Android build succeeded in 102.9 seconds with zero errors and one warning. `Builds/Quest/EvidenceVR-Stage2-dev7.apk`: 88,799,882 bytes, ARM64, version code 19, APK v2 signature verified; SHA-256 `4C409379C70CB760A2A99EAE3FE1E3A5D7A94DA1F6ACDA29D8A6071EB5B5C6F0`. Final APK inspection confirms `cambria`, `com.oculus.permission.EYE_TRACKING` and the eye-tracking feature declaration. The OpenXR-generated eye feature is marked required; a multi-device declaration does not establish eye-less-device distribution compatibility. The final dev7 Editor smoke passed; Play Mode is stopped. External media/config ZIP is unchanged. Evidence: `Evidence/QuestProSupport/`.

Pending: Quest Pro grant/denial/revocation, calibration and actual eye-pose acquisition, target alignment, standalone/Link-specific behavior, controls, media, stereo comfort and exports. Earlier Quest 3 observations are historical evidence only. All physiological and participant-readiness gates remain open.

## Engineering gaze highlight (dev6)

Implemented: default-off engineering sphere/movie outlines, Editor menu switch, participant lockout, real-eye-only input, shared visible-target classifier, invalid/stale/pause/end clearing and explicit ROI/debug metadata. No effect on task timing, original colors/media, scoring or adaptation. See [gaze-debug review](GazeDebugReview.md).

Verified: Unity C# / shader compilation, 2,033 synthetic model checks, 19 live Editor checks and 48,128 adaptive regression assertions passed. A separately labeled synthetic visual fixture exercised a sphere and an actual decoded practice movie. Participant/default-off renderers were absent, invalid/stale/pause handling passed, and base colors were unchanged. The 302 production gaze samples were all invalid; no synthetic ray was saved as genuine eye data. Metadata, screenshots and original Editor configuration restoration were inspected. Evidence: `Evidence/GazeDebug/`.

Delivery verified: Android build succeeded in 246.8 seconds, zero errors, one diagnostics-symbol warning. `Builds/Quest/EvidenceVR-Stage2-dev6.apk` is 88,798,578 bytes; ARM64/code 18/APK v2 verified. SHA-256: `FE133AB65257E579FF82C48184B7A9DF2F542794E13453FB871BA64D38AE7334`. The 51,446,740-byte media ZIP passed integrity/unchanged-content checks and defaults to no gaze highlight. No ADB device was connected. Final console errors: zero, Editor stopped, local configuration restored.

Pending: real eye acquisition/calibration and Quest stereo/comfort/performance checks. Quest 3 still has no genuine eye input in this setup. Existing media-review, participant calibration and physiological/device gates remain pending. No participant-facing highlight was enabled.

## Rolling control and decimal countdown (dev5)

Implemented: ten eligible trials per rolling window, updates every two trials; 108-second statistics retained; safe trial-boundary execution after active media, reset/transition rules, revised short-window omissions/class checks, unique auditable decision IDs, current-condition physiology gates, mixed-level exports and two-decimal countdown in both task modes. See [review](RollingAdaptationReview.md).

Software verified: Unity compiled without errors; 48,128 adaptive model assertions across 1,000 schedule seeds and 30 complete synthetic sessions; 92 focused rolling/countdown/migration/reset checks; 194,113 Stage 1 assertions; 47 Python tests. The real-time Editor probe passed 7,332 checks with an actual HTTP-driven upgrade at 71.98 seconds, Level 2 candidate completion queued, four media starts/clean ends and no failures or interruptions. Countdown text fit was checked and the screenshot inspected. Both probes' 14 controller decisions replayed exactly; mixed-level statistics were independently summarized. Evidence is under `Evidence/RollingAdaptation/`. The initial 150-second test harness timeout is preserved and explained in the project log; it is not a participant/device failure.

Delivery verified: Android build succeeded in 122.5 seconds with zero errors and one diagnostics-symbol warning. `Builds/Quest/EvidenceVR-Stage2-dev5.apk` is 88,762,808 bytes, ARM64, version code 17 and APK v2 signed. SHA-256: `67E9AC87979E91551CA1C8ABB35F80B9A9A8C4E04B1572DB04E78EB4295E5BF3`. The 51,446,723-byte media ZIP passed integrity and unchanged-media hash checks. Final dev5 Editor smoke verified version/configuration/display metadata and the preserved OFF setting. Final console errors: zero; no headset was connected and XR audio fallback warnings are not audio-device acceptance. Editor and test service stopped.

Pending: hardware timing, Quest 3 standalone operation/readability/comfort, human media review, real physiology calibration and empirical short-window threshold validation. Existing `volumeReviewed=false` participant gates remain. Quest Pro remains unverified.

## Expanded media library (dev4)

Implemented: 22 pairs / 44 media files, including 12 new nonoverlapping source cuts, with the original ten entries and bytes unchanged. The training pool is 18 clips (nine per modality); practice uses three AV clips and least-exposed-first selection can use six other AV clips before repeating them. The four reserved clips and all exposure/task timing remain unchanged. Legacy library layouts remain supported; dev4 requires matching physiology calibration. See [expanded-media review](ExpandedMediaReview.md).

Verified: all 22 pairs passed codec/hash/PCM/loudness/duration checks, and every added clip was visually inspected. 100,004 expanded-library model assertions, 51,068 adaptive assertions and 31 Python tests passed. Thirty live Editor checks passed: all 22 pairs loaded, all 12 new clips prepared and decoded offscreen, and a full 63-second practice played pair_22, pair_20 and pair_02 through the normal wall/audio system with three clean ends and no media failures. Offscreen decoding did not exercise audio; it is not a headset test. Logs retained 12 correct programmatic answers, 14 practice rows and the 4.5-second cycle. Evidence: `Evidence/MediaExpansion/`. The remote tool response timed out at 60 seconds, but the Editor probe completed; its saved result, runtime state and media/trial CSVs were independently checked.

Android build passed in 93.8 seconds with zero errors and one diagnostics-symbol warning. `Builds/Quest/EvidenceVR-Stage2-dev4.apk` is 88,757,512 bytes; ARM64, code 16 and APK v2 signature verified. The release ZIP passed integrity checks with 44 media files and dev4 config. Migration of the actual saved dev3 override preserved mode/seed. Build/signature/hash evidence is in `Evidence/MediaExpansion/`. Current local config remains enabled/OFF; the release config remains SHADOW engineering. Human content/volume review and Quest 3 physical acceptance remain pending. All `volumeReviewed` flags remain false.

## Historical front-wall films, compact UI and practice update (dev3)

Implemented: fixed centered 3.2 x 1.8 m front-wall screen, no idle rectangle, two compact running status rows, and three randomized AV-assigned training clips during default 63-second practice (10/28/46 s plus up to 0.5 s jitter). Practice is separately attributed and counted in familiarity history; held-out pre/post clips remain unused. Configuration and presentation are versioned. See [wall/practice review](WallPracticeReview.md).

Software verified: 80,003 new schedule/config assertions across 1,000 seeds x four groups, 49,068 existing adaptive assertions, 31 Python tests, and 157 real-time Editor checks. All three actual practice films started and ended without failure, then the baseline was silent; 12 programmatic scored answers were correctly logged and the 4.5-second cycle was retained. A separate Stage 1 Play Mode check verified compact status and warmup reminders. Screenshots and a CSV audit are under `Evidence/WallPractice/`. Initial harness failures and observed decoder warnings are documented in the detailed review.

Android build passed in 127.7 seconds with zero errors and one diagnostics-symbol warning. `Builds/Quest/EvidenceVR-Stage2-dev3.apk` is 88,752,896 bytes; ARM64, version code 15 and APK v2 signature were verified. The media ZIP now includes the dev3 config while preserving the film bytes and archive root structure; ZIP integrity passed. Build/signature/hash evidence is in `Evidence/WallPractice/`. Physical acceptance remains pending: centered video occlusion/readability/comfort, newly positioned sound source, Quest decoding/performance, and genuine physiological integration. Practice familiarity now differs between AV and audio-only training; review this design before participant recruitment. Prior dev1 physiology calibration is not accepted for dev3 control.

## Historical mode/status display update (dev2)

Persistent Stage 1/Stage 2, controller OFF/SHADOW/ACTIVE, automatic-control state, bridge availability, control basis and source provenance are implemented. Actual Editor checks passed for both task modes, preparation/running/pause layouts, metadata, P007 replay receiving and service dropout. Twenty-nine Python tests and six additional label/layout checks pass. Evidence is in `Evidence/Stage2Status/`; see [status review](ModeStatusReview.md). Physical-headset readability remains pending.

The dev2 APK was `Builds/Quest/EvidenceVR-Stage2-dev2.apk`. Its build succeeded with zero errors and one diagnostics-symbol warning; ARM64, version code 14 and APK v2 signature were verified. Build/signature/hash evidence is under `Evidence/Stage2Status/`. At dev2, the film package and training/controller configuration version were unchanged. The dev1 evidence below remains historical evidence for the underlying protocol.

## Stage 2 software evidence (dev1 protocol)

| Check | Result and evidence |
|---|---|
| Compilation and model | No current compiler errors. 49,068 adaptive assertions; 1,000 schedule seeds and 30 complete synthetic sessions; practice/formal pause rewarm included. `Evidence/Stage2/model-validation.txt` |
| Stage 1 regression | 194,113 task assertions and 96,013 distraction schedule assertions passed. Existing fixed-mode timing/scoring/presentation retained. |
| Python controller and quality | 27 tests passed, including calibrated/unvalidated/missing/stale/mixed/synthetic inputs, severe omissions, idempotence/restart, known-frequency EEG and known-RR ECG fixtures. `Evidence/Stage2/python-tests.txt` |
| ACTIVE boundary integration | Real local HTTP recommendations executed at Unity boundaries; two programmatic baselines plus four programmatic training evaluations reached candidate completion. Six ACTIVE and three SHADOW decision records replayed exactly. This was an accelerated engineering probe. |
| Actual film library | Ten candidate pairs (20 files), 10–12 s, H.264 720p/30 fps, identical decoded mono audio. Hash/codec/duration/loudness matching audit passed. Source, cut, padding and gain recorded. `film-pair-audit.json` and [media catalog](Stage2MediaCatalog.md) |
| Actual film runtime | Ten pairs loaded; audio-only playback, immediate pause, actual AV decoding, shared fixed source, running layout and bounded exposure end passed in Editor. `film-playback-probe.txt`, `film-runtime.png` |
| P007 recorded replay | Existing external XDF scheduler used read-only. Isolated receiver: 267 feature rows, 84 windows recomputed. Unity-connected Editor session: EEG/ECG/HRV received, 646 rows all synthetic, 156 windows recomputed. `p007-offline-recompute.json`, `unity-p007-offline-recompute.json`, `film-and-p007-unity-probe.txt` |
| Isolation and restoration | Editor data under `editor-test`; recorded LSL data rejected from participant directories. Replay labels retained in source, feature and monitor data. Launcher process/config cleanup exercised; Stage 1 startup restored. |
| Android candidate | Final build succeeded, zero errors, one diagnostics-symbol warning. ARM64, minimum SDK 29, target 36, APK v2 signature verified. `Evidence/Stage2/android-build.json`, `apk-platform.txt`, `apk-signature.txt` |

All paths abbreviated in this table are beneath `Evidence/Stage2/`. The candidate APK is `Builds/Quest/EvidenceVR-Stage2-dev1.apk`; media/config package is `Builds/Stage2MediaRelease.zip`. The film files are external to the APK and must be installed separately. `delivery-artifacts.json` records SHA-256 and sizes. The sole build warning concerns debug symbols for crash-report symbolication; it is not a passed crash-reporting test.

## Pending Stage 2 acceptance

- Quest 3 standalone installation, full operation, physical A/B/pause/stop, actual headset volume, visibility, comfort, video performance and export; no headset was connected during this implementation. Quest Pro remains an unverified target.
- Human semantic/content matching and listening review of all film pairs; every `volumeReviewed` flag remains false. This blocks participant mode.
- Approximately 6–8 real fixed-condition pilot participants, individual baseline/feature calibration and validation of EEG/ECG artifacts and ECG beats. No feature is currently admitted as real physiological control evidence.
- Genuine supported eye-tracking hardware and permissions. Quest 3 does not acquire real gaze in this setup; head orientation is not substituted.
- Five complete real-device integration sessions, real command/sensor dropout checks and pre/post unseen-clip observations. One reserved exposure per transfer block differs from three during training; shared-scene familiarity also requires review.
- A validated physiological feature actually contributing to control and a reviewed final configuration before freezing the Stage 3 Patent OFF baseline. Recorded P007 playback and software success do not establish physiological adaptation or training improvement.

Detailed records: [implementation/review](Stage2Implementation.md), [operator guide](Stage2Guide.md), [calibration worksheet](Stage2CalibrationReport.md), [project log](ProjectLog.md).

---

## Historical Stage 1 acceptance record

For the current project stage and major-update history, see [Project status and update log](ProjectLog.md).

Date: 2026-11-01. Software implementation and desktop validation are available; full physical-headset acceptance remains pending. The current presentation is spatial-lab-v1.7.1-more-video, application version 1.7.1-stage1.

## Verified software evidence

| Check | Evidence |
|---|---|
| Sequence generation and scoring | 2,000 sequence sets, 1,000 seeds per mode; counts, ratio, independent answers and replay pass |
| Core assertions | 194,113 assertions in `Evidence/automated-validation.txt`, including full four-second visibility, response boundaries and pause behavior |
| Complete synthetic runs | Five sessions per validation invocation; 88 stimuli each, 60 formal scored trials, both orders |
| Export-level recomputation | `Evidence/export-validation.txt`: 400 scored rows from five saved sessions independently checked |
| Real-time editor integration | `Evidence/live-playmode.txt`: queued InputSystem states, 40 seconds, 21,939 advancing frames, ten practice rows, pause/resume |
| Restart and session isolation | `Evidence/restart-validation.txt`: previous session saved as aborted_restart, fresh editor-test session created |
| OpenXR project configuration | `Evidence/openxr-validation.txt`; Android validation reports zero issues |
| Prior APK | v1.0 built and signature-verified; ARM64, VR launch category, Quest Pro/Quest 3 support declarations |
| Prior v1.1 APK | v1.1.0-stage1, version code 2; Android build succeeded in 191.7 seconds with zero errors; APK v2 signature verified; 41,735,754 bytes |
| Prior v1.2 APK | v1.2.0-stage1, version code 3; Android build succeeded in 111.3 seconds with zero errors; APK v2 signature verified; 41,737,330 bytes |
| Prior v1.2.1 APK | v1.2.1-stage1, version code 4; feedback wording changed to Incorrect; Android build succeeded in 53.2 seconds with zero errors; APK v2 signature verified; 41,736,466 bytes |
| Prior v1.3 APK | v1.3.0-stage1, version code 5; Android build succeeded in 121.0 seconds with zero errors; ARM64 and APK v2 signature verified; 41,831,962 bytes |
| Prior v1.3.1 APK | v1.3.1-stage1, version code 6; Android build succeeded in 55.2 seconds with zero errors; ARM64 and APK v2 signature verified; 41,874,802 bytes |
| Prior v1.4 APK | v1.4.0-stage1, version code 7; Android build succeeded in 266.1 seconds with zero errors; ARM64 and APK v2 signature verified; 70,020,954 bytes; all eight packaged videos match their catalog SHA256 values |
| Prior v1.5 APK | v1.5.0-stage1, version code 8; Android build succeeded in 116.0 seconds with zero errors; ARM64 and APK v2 signature verified; 70,022,502 bytes |
| Prior v1.6 APK | v1.6.0-stage1, version code 9; Android build succeeded in 139.7 seconds with zero errors; ARM64 and APK v2 signature verified; 70,027,606 bytes |
| Prior v1.6.1 APK | v1.6.1-stage1, version code 10; Android build succeeded in 63.5 seconds with zero errors; ARM64 and APK v2 signature verified; 70,027,754 bytes |
| Prior v1.7 APK | v1.7.0-stage1, version code 11; Android build succeeded in 110.4 seconds with zero errors; ARM64 and APK v2 signature verified; 88,634,246 bytes; all eight packaged videos match catalog SHA256 values |
| Current APK | v1.7.1-stage1, version code 12; Android build succeeded in 65.3 seconds with zero errors; ARM64 and APK v2 signature verified; 88,633,438 bytes |
| Visual refinement | See `VideoFrequencyReview.md` for current v1.7.1; media and feedback are recorded in `SpatialAudioFeedbackReview.md`; UI clearance is recorded in `UiClearanceReview.md`; v1.6 is recorded in `FrontWallCalibrationReview.md`; v1.5 is recorded in `SeatedMinimalReview.md`; prior refinements remain in `EnvironmentalDistractions.md`, `StimulusMaterialReview.md`, `SpatialTaskReview.md`, `VisualReview.md` and `SequenceGuidanceReview.md` |
| Seated minimal presentation | 88 checks: tabletop 0.65 m at three eye heights, sphere visible through 3.999 s and hidden at 4 s, practice and both formal modes green/red feedback, omission feedback, neutral warmups and reset, four active text elements and no overflow; `Evidence/seated-minimal-v1.7.txt` |
| Prior v1.5 live timing | 14-second Editor run verified persistent sphere visibility, hidden gap, green practice feedback, four cues and a saved 3.2-second response; `Evidence/response-window-live-v1.5.txt` |
| UI clearance | 39,214 checks at 1.05/1.20/1.40 m eye heights and three viewing offsets; sampled text rectangles clear of desk, plinth and sphere across all phases; no TMP overflow; SDF outline enabled; 19 screenshots saved; sampled phase views and all five video-position previews inspected; `Evidence/ui-occlusion-v1.6.1.txt` |
| Seated calibration | Actual coroutine corrected simulated 2.22 m combined height to 1.20 m, repeated calibration stable, fixed 0.65 m tabletop; `Evidence/seated-calibration-v1.6.txt`; no live headset connected during probe |
| Distraction scheduling | 96,013 checks over 1,000 seeds: eleven events per block, five videos at 24.6-second intervals and six audio events, balanced types and six audio families, ten-second video, no overlap, deterministic replay, eight distinct videos per session and five front-wall positions without consecutive repeats; `Evidence/distraction-validation-v1.7.1.txt` |
| Distraction playback | 141.27-second isolated Editor run: eleven completed exposures, no skips or decoder errors, five video exposures measured 10.009–10.021 seconds; pause/hide/resume and clock freeze verified; `Evidence/distraction-playback-v1.7.1.txt` |
| Spatial audio | 12 cases: six profiles on both sides; actual audio advance, moving traffic, approaching footsteps, near-ear distance, gain, pause/frozen motion, resume, cancellation and abort verified; `Evidence/distraction-audio-v1.7.txt` |
| Local media | 40 assets: eight silent H.264 videos, sixteen environmental recordings and sixteen synthesized speech clips; 46,603,614 prepared bytes; source and hash catalog saved in `Assets/TaskPlatform/Media/distraction-media-manifest.json` |
| Satin sphere | Four colors and a close-up rendered and visually reviewed; supported Lit material verified; sphere radius preserved with smoother mesh; `Evidence/stimulus-material-v1.3.1.txt` |
| Spatial layout and countdown | 29 states and 88 color counters checked; final front, angled and instruction views inspected after fixing plinth/sign occlusion; `Evidence/visual-validation-v1.3.txt` |
| Four-second live integration | 14-second Editor probe: four stimuli, four playing cues, countdown 4/3/2/1/NEXT; a programmatic response after 3.2 seconds accepted and saved; `Evidence/response-window-live-v1.3.txt` |
| Sequence guidance | 29 snapshots checked; stage/color counters across 88 stimuli, two-back indices, warm-up prompts and absence of formal answer feedback verified; `Evidence/visual-validation-v1.2.txt` |
| Quest Link initialization | Standalone OpenXR configuration added after diagnosing missing Editor XR settings; Oculus runtime, a running XR display subsystem and head-tracking device observed in Play Mode. See `Evidence/quest-link-runtime.json`. Visual confirmation and physical input remain separate checks. |
| Quest 3 Link visual confirmation | On 2026-11-01, the user confirmed that the experiment scene was visible inside Quest 3 while Unity Play Mode was running through Link. This confirms Editor preview, not standalone APK operation or complete task/input acceptance. |

Synthetic data under `Evidence/SyntheticSessions/` and editor probes are not participant or device-test evidence. Historical live input probes remain as provenance; core validation and export verification were refreshed for v1.7. The response-window probe is explicitly marked editor-test, uses programmatic input, and does not establish physical sound onset or headset audibility. Distraction and seated-layout probes use isolated engines and store their data under `Evidence/`.

The official Unity OpenJDK 17.0.18+8 module was installed during initial delivery after the first build found it missing. The current build has one warning: crash diagnostics lack debug symbols. Current build outcome is in `Evidence/android-build-report-v1.7.1.json`; package inspection and checksum are in `Evidence/apk-inspection-v1.7.1.txt`. SHA256: `A69F8B4C9B0668D8A1AFEB078794F381979FD5803A9A0AF76CE472961E48E81A`. Older versioned reports remain historical. The latest observed ADB connection required debugging authorization for APK installation; Quest Link preview is a separate connection. Final Editor checks found zero console errors, no compilation failure, Play Mode stopped and the scene saved.

## Required physical-device checks

| Item | Status | Record when tested |
|---|---|---|
| Standalone Quest 3 launch | Pending | Firmware, APK SHA256, date |
| Quest Pro launch | Pending | Separate device-specific evidence |
| Head tracking and world-fixed 1.5 m task placement | Pending | Look away/return, recenter, seated comfort |
| Physical A/B input, holds, simultaneous and late presses | Pending | Correspondence with event records |
| Pause, resume, restart, headset backgrounding | Pending | Trial order and interruption flags |
| Color discrimination and text readability | Pending | Review all four colors and all instructions |
| v1.3 cue audibility and comfortable gain | Pending | Quest 3 output volume and perceived onset cue level |
| v1.4 local video and spatial distraction audio | Pending | Android decoding/performance, comfortable gain, dialogue intelligibility and perceived rear localization; the Editor uses native 3D panning with no HRTF plugin |
| At least ten minutes of device operation | Pending | Refresh rate, software gaps, device performance tools |
| Five complete real sessions | Pending | Session IDs, complete status, 60 formal scored trials each |
| Export, metric recomputation, file persistence | Pending on device | Paths and summaries |
| Photon onset | Not measured | External instrumentation if required |

Do not label Stage 1 fully accepted or begin claiming physiological/adaptive validation until the required physical tests pass. The new room/panel change the visual environment; record the presentation revision and review comparability before combining versions.

Dev8 final follow-up: preparation also shows eye status beside the mode/source badge; its rendered fit was checked. Unbound-engine cleanup after script reload was guarded and passed two focused lifecycle scenarios. Final current Editor console errors/warnings: zero. Editor left in Preparation with local engineering ray on; release template remains off. See the dev8 review for session and environment state.

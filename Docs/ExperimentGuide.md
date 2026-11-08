# Experiment guide: current modes

## Current media bank

The installed PC bank is `layer-ab-144-dialogue-v2-no-heidi` (2026-11-07): 92 comparison clips, with Heidi excluded. Both continuous A/B playlists were rebuilt to remove embedded Heidi excerpts while preserving their 300-second duration. Restart Unity Play Mode before the next assignment to load the updated bank. The completed run was stopped during deployment; no new session was started. Existing standalone/Quest installations need the updated runtime, catalog and media before use. Historical session media manifests retain their original content provenance. [Changes and validation](MediaExclusionReview.md).

## Unity Editor menu

The current **Evidence VR** menu has **Open Web Console**, **Signal Sources** and **Advanced**. Use Open Web Console for the existing service's study setup page; start the Python service using the existing launcher or Signal Sources connection workflow when needed. Choose stages, models, Desktop/Quest Pro and thresholds on the website, then use Unity Play Mode.

Advanced contains **Use Web Console Workflow**, **Quest Link Settings**, **Eye Tracking Debug**, **Validate Setup** and **Build Quest App**. Use Web Console Workflow enables the existing operator-managed startup mode; it does not start a trial. Eye-debug entries are checked when enabled. Target Highlight is configured outside Play Mode; Eye Ray also supports the existing live engineering mode. Validate Setup runs the retained software checks and does not certify equipment. The old scene-building and protocol-switching menu entries are removed, with their underlying methods preserved. [Menu mapping and verification](EditorMenuReview.md).

## Researcher workspace

Display `console-display-v3-clarity` (2026-11-08). Every page shares one header: **Study setup**, **Run monitor**, **Signals** and **Guide**, plus **Session report** when a report is open. Terms are consistent across pages. Round A executes every proposal and only observes the review; Round B enforces the review. The **workload model** is the saved Personal RF. A **command** is what the backend sends; a **Unity receipt** confirms it. [Revision details](ConsoleDisplayV3Review.md).

Open **Study setup** at `http://127.0.0.1:8765/tests`. Choose **Practice**, **Calibration** or **Adaptive Comparison**. For comparison, select the participant and saved workload model, choose **Desktop** or **Quest Pro**, review the ON and OFF thresholds with their held-out forest/arithmetic counts, then choose the A/B order. **Task rehearsal instead** prepares a task-only check without EEG or model scoring. Storage and legacy options are folded below the setup.

The **Next step** card lists only the inputs the selected run option uses. If the option needs EEG and none is arriving, it reads **Start the EEG stream · Not ready to record**; start streaming in the Mindtooth app before starting the task. **Create session assignment** behaves as before. Open Unity and complete its preparation before starting trials. Video thresholds remain editable with **Apply thresholds**. **Stop / release test** ends the assignment and keeps its records.

Open **Run monitor** for trial progress, the next scheduled check, playback state and the decision graph (Behavior and Workload model → Proposal → Review → Unity receipt, with the Gaze check). The **Workload model** step shows the rolling score, thresholds and recent trend, and can preview the selected model before Unity starts when enough EEG arrives. No number means no usable current score. Record paths are under **Session details** in the connection notice; the notice turns amber only when the service is offline or a running session's telemetry is stale. **Signals** shows the workload model output, gaze overview, raw waveforms and features; LSL source IDs and UIDs are under **Source identifiers**.

In **Recent sessions**, select **Open report** on a comparison session. The **Session report** opens with the A/B summary. Use **Round**, **Show** and the **decision timeline** to select a decision: arrow keys, Home/End or the ←/→ buttons move through the filtered decisions. Each marker sits at its trial; the band shows when the video was commanded on. **Technical details** under Execution & input source contains the original record. Reports identify replay and simulated inputs.

<a id="decision-flow-wording"></a>

### Reading a decision

This replaces the earlier numbered-card wording (`console-decision-flow-wording-v1`, [history](DecisionFlowWordingReview.md)).


The card heading states what happened, for example **Video ON blocked by review**, followed by the deciding reason. **Proposal**, **Review** and **Command** follow as three rows; HOLD keeps the video state unchanged and does not pause the task. In Round A, the review row reads **Would hold · not applied** when B rules would have held the change. **How this decision was reached** shows the two proposal routes (either route can propose a change; status comes from the recorded triggers) and the review checks (every check is needed), with the **first unmet** check marked. **Not reached** means the review did not get that far; it is not a failed eye check. For an OFF deferral, an unmet check means the closure went ahead. A Unity receipt confirms the command only; check **Trial records** for actual playback.

## Comparison environments

2026-11-07, `comparison-environments-v1`. Open **Study stages > Adaptive Comparison**. Keep **A/B with a saved model** for real EEG/model scoring, select the participant/model and video thresholds, then choose **Comparison environment**:

- **Desktop — no headset, fixed gaze 75% / 70%**: use the Unity desktop/Game view. C centers the task, A/B check response controls, Enter continues/starts, and P pauses. Eye inputs are fixed assumptions; task share is 70% of the valid-eye time. Mindtooth reception and model loading remain required for the saved-model run option.
- **Quest Pro — headset with real eye tracking**: wear Quest Pro, enable eye tracking, complete seating/controller checks and continue. Missing real tracking is never replaced with fixed inputs.

Start Session freezes the environment and thresholds for both A and B. Change environments by ending the current session and preparing a new one. The session card, Decision graph and comparison report identify **Simulated gaze** versus **Quest Pro gaze**. A branch that stops before eye evaluation still records the selected source; it does not claim an eye check passed. Desktop does not generate raw eye trajectories.

The EEG-free **A/B task rehearsal** is a separate run option, suitable for software operation checks only. Use the updated Unity Editor source or a rebuilt Player; an older build does not implement the new environment configuration. See [review](ComparisonEnvironmentsReview.md) and [acceptance](AcceptanceRecord.md#comparison-environments).

## Continuous 60-trial A/B

New Web Console Adaptive Comparison sessions use 60 stimuli per arm, including two unscored warmups, and check every two responses using the latest five eligible trials. BA >= 0.80 with at most one omission supports ON; BA <= 0.60 or two omissions supports OFF. Both response classes are required for BA. B's gaze-supported model-only OFF deferral requires BA >= 0.80, valid eye coverage >= 70%, task share >= 60% and the existing temporal checks. The selected model and its pre-session score cutoffs remain shared across A/B.

Video clips continue into the next clip while ON or HOLD. OFF hides and mutes while retaining the cursor; ON resumes. Source clips, cut points, first decoded frames and control actions are logged separately. Install `continuous-comparison-media.json` and both `continuous-comparison-*.mp4` files from `Builds/ContinuousComparison60` alongside the original media bank; the current PC is prepared. Create a fresh session to use this revision. Old assignments keep their original rules. Ten-new-trial verification and failed-probe phase locking remain; see [complete rules and verification](ContinuousComparisonReview.md).

## Independent study stages

Open **Physiology > Raw source waveforms** to inspect each live EEG channel. The engineering waveform display shows the received signal directly.

In **Adaptive Comparison**, select the participant and saved forest/arithmetic RF. Open **Decision graph > Personal RF** to see a rolling ten-second preview from the selected Mindtooth stream, including before **Start Session** or Unity connection. It refreshes about once a second. Engineering scoring uses all ten one-second epochs in a complete, finite, correctly mapped EEG window. Amplitude, plateau and flatline rejection are disabled in both preview and comparison scoring. Preview data stay in memory. Once Unity connects, the node displays task-stage predictions. Select a specific controller request to inspect the score recorded for that decision. Both A and B use the same saved RF, spectral preprocessing and arithmetic-class score on task-aligned EEG, subject to session timing, sample availability and decision gates. A score may differ from the earlier preview because the windows end at different times.

Selecting a forest/arithmetic model reveals **Video proposal thresholds**. Review the recommended ON and OFF values and edit either before **Start Session**. The two counts beside each field show how many held-out forest and arithmetic windows meet the inclusive cutoff. ON must be lower than OFF. You can also edit during preparation or either A/B round: click **Apply thresholds** to use the new pair from the next new decision. Typing alone does not apply a running-session change. **Applied** shows the currently saved pair and revision; the Personal RF node shows the current cutoffs. Previously generated decisions and retries retain their original values. **Recent sessions > Archive & configuration** and the downloaded report include the initial thresholds and timestamped change history. Completed sessions cannot be edited. Model selection and experiment environment remain part of the original assignment. A legacy model without stored held-out scores shows editable legacy defaults and unavailable counts. [Recommendation method](LoadReferenceCalibration.md#video-threshold-recommendations), [live-edit semantics and checks](LiveThresholdsReview.md).

Run `StartEvidenceConsole.cmd` and open **Study stages**. Use the operator-managed Unity configuration and matching media pack. Enter a participant code and storage folder, choose a stage and run option, then start the assignment. In Unity, complete seating and button checks. After a stage closes, check the archive status. A later stage can use the same code and folder in a new session. Legacy engineering runs remain accessible via **Legacy engineering sessions**.

For an A/B control calibration choose **Calibrate the A/B control model**. To reuse a saved forest/arithmetic workload RF, open **Adaptive Comparison**, enter the same participant ID, use the default **A/B with a saved model**, select the model and click **Start Session**. Both periods use its arithmetic-class score. Automatic order allocation balances new participant IDs within each run option and reuses the saved order for a returning ID. Select A/B or B/A explicitly when using an external allocation. Install the 144-trial media pack. Check actual source, rate, units, mounting and tracked eyes before worn operation. Use **Open comparison report** on a session card to inspect or download its recorded decisions and post-response evidence. [Decision criteria and acceptance boundaries](LayerABStudyGuide.md#independent-stage-operation).

## 2026-11-06 — Occasional same-digit arithmetic options

Each 18-prompt Calculate stage includes three prompts with one wrong answer sharing the true result’s final digit, one randomly positioned in each group of six. Participants still press A when the result appears and B when absent. The rule is recorded as `vermont-180s-math-occasional-decoy-v10`. [Calibration procedure](LoadReferenceCalibration.md).

## Check the live EEG rate before calibration

Use the top strip on Test sessions or the Evidence Console. Watch the actual rate settle near 250 Hz before starting Relax. A red low-rate or no-samples message during a task calls for an operator check of the amplifier and EEG App streaming. Task time continues. [Sampling-rate monitor](EvidenceConsoleGuide.md#live-eeg-sampling-rate)

## 2026-11-06 — Calculate timeout feedback

Stage 2/3 Calculate revision `vermont-180s-math-timeout-v9` scores an unanswered ten-second prompt as incorrect and displays its expression in red for 1.5 seconds. The log retains response=omitted and an empty reaction time. Interruptions remain unscored. Quest Pro readability is pending. [Calibration procedure](LoadReferenceCalibration.md).

## 2026-11-06 — Dimmer Relax lighting

Relax uses 40% of the room lighting settings during the forest walk. The original settings return on stage exit or interruption. [Calibration procedure](LoadReferenceCalibration.md).

## 2026-11-06 — Three-minute calibration (v6)

Start Relax with X / Enter and watch the Vermont trail for three minutes. At the Calculate instructions, review the worked example, then press X / Enter for eighteen ten-second questions. Press A for a listed result or B for an absent result. Restart the acquisition service before using the revised protocol. [Calibration procedure](LoadReferenceCalibration.md).

## 2026-11-06 — Rolling calibration quality

During Relax and Calculate, watch the calibration banner on any Evidence Console page or on Test sessions. After ten seconds of signal and a one-second ingestion allowance it checks the latest complete ten seconds at one-second steps. A warning shows the reason and flagged electrodes when available. Check the waveform and mapping. The timer continues. Electrode adjustment requires a fresh session. The training count uses disjoint windows and the model report records the outcome. [Calibration procedure](LoadReferenceCalibration.md).

## 2026-11-06 — Calculate response feedback

During Calculate, the expression turns green for a correct A/B response or red for an incorrect one. Feedback lasts until the next question. The four numbers retain their normal text color. Each new prompt starts in the neutral color. [Calibration procedure](LoadReferenceCalibration.md).

## 2026-11-06 — Selected EEG input

Select the outlet, choose EEG mapping, and enter the verified conversion to uV. Connect and check for Receiving before preparing a test. The engineering calibration accepts compatible selected EEG sources including CSV replay. Receive only is for preview.

## Selecting an LSL source in the Unity Editor

Stop Play Mode and disconnect the bridge. Open **Evidence VR > Stage 2 > LSL Signal Sources**, click **Scan**, choose an outlet by name, type, channel count, rate and UID, then click **Connect**. New outlets default to **Receive only**, which accepts numeric samples and text events. Rescan and reselect after a publisher restarts. In Advanced settings, verify source provenance before configuring EEG processing. The study EEG mapping requires the live eight-electrode, 250 Hz profile. Recorded XDF metadata takes precedence over operator provenance.

After Connect, the status changes from Connecting to Receiving when recent samples arrive. An irregular event stream can show Connected, waiting for events between markers.

During Relax, watch the Vermont forest trail for three minutes with eyes open. The room dims and the front screen plays forest sounds. During Calculate, solve each expression, then press A if its value appears among the four numbers or B if absent. Each prompt lasts ten seconds. The first valid press counts. The desktop A/B keys match the right-hand controller buttons. [Calibration procedure](LoadReferenceCalibration.md).

## 2026-11-05 — Personal workload RF (rest-math-iaf-rf-v2)

Select Calibration only and complete two minutes each of eyes-open rest in the room and mental arithmetic. On Physiology inspect the personal RF score, resting alpha peak and held-out AUC. Start a fresh calibration to use this revision. Verify the LSL amplitude conversion before collecting model data. [Method and operation](LoadReferenceCalibration.md).

## 2026-11-05 — Local workload reference calibration

Local personal calibration now uses 120 seconds of eyes-open rest and 120 seconds of arithmetic. The engineering load score is observed independently of the RF control thresholds. Synthetic processing and Editor stage transitions were checked. Worn EEG, units, questionnaire applicability and Quest Pro remain pending. [Operation, scoring and verification](LoadReferenceCalibration.md).

## 2026-11-05 — Choose the physiological source

In Test sessions, choose a saved personal model or the Mindtooth Workload engineering test. For the vendor option first connect AI-Based Workload on the Physiology page and check the vendor participant and session. [Operation, decision rules and verification](MindtoothModelSources.md).

## Mindtooth acquisition

All new sessions use the eight-electrode Mindtooth profile. The app displays µV. Verify the LSL scale using time-aligned device and LSL samples before enabling calibration. Record this outlet together with Unity task markers for synchronized task recordings. [Mindtooth acquisition, verification and pending checks](MindtoothAcquisitionReview.md).

## Current A/B allocation

Use `layer-ab-probe-v2` for new sessions. Generate a fresh allocation, install the 20-second media pack, restart the Python bridge and load the matching Unity configuration. Each training period has 144 stimuli. Both arms use the same OR thresholds. B reviews every proposed upgrade and verifies it after ten new trials. [Current setup, decision table, outcome definitions and pending acceptance](ProbeStudyReview.md). Earlier 96-trial counts, thresholds and mock plans below are retained for the corresponding study versions.

**Historical 96-trial allocations:** `sliding-five-close-v1` retains behavioral and physiological history across ordinary level changes. Behavior uses the response just closed, while physiology uses the latest fully acquired, compatible five-trial window (one trial behind). Recalibrate with the new revision; start the updated Python service and updated Unity source/player. Supersedes the previous switch-capacity estimates only for the new allocations. [Operation, timing and validation](SlidingControlReview.md).


**Behavior window update (2026-11-05):** `behavior20-stride5-include-transition-v1` evaluates the latest twenty behavioral trials every five trials without invalidating the whole window solely for transition flags. Physiological windows, calibration labels, actual exposure and execution gates are unchanged. Restart the Python service after ending the old session, then start a fresh session; running services do not hot-reload this change. Keep the 4.5-second cycle. Verified synthetic capacity is seven fallback or five physiology level changes per 96-trial phase under favorable conditions, not an expected human rate. The final trial-95 request cannot execute before phase end. [Rules, timing, tests and limitations](BehaviorWindowRevisionReview.md).

Current A/B questionnaire revision: `raw-tlx7-ueq4-preference-google-forms-v4`. Use the seven-point adaptation of Raw TLX on page 1 (six required 1-7 ratings), UEQ+ on page 2, and final preference after Training 2. See [scoring and literature](SevenPointTLXReview.md).

**Optional A/B study (2026-11-05):** use the separate `StartLayerABStudy.cmd` entry on port **8766**, then load an allocated `layer-ab-v1` configuration from **Evidence VR > Layer A-B study** while Unity is stopped. Both periods use the same adaptive controller: A observes the layer and B enforces it. The new [Google Forms](LayerABGoogleForms.md) collect six Raw TLX ratings on page 1 and four UEQ+ Attractiveness ratings on page 2 immediately after each period, then a separate final preference after Training 2. Complete both pages before submitting. After successful submissions, the experimenter presses X/Enter in Unity; the first confirmation starts 180 seconds of rest. The local questionnaire page and save endpoints are removed; no IMI, repeated SSQ, adjustment-experience or calibration questions are included. Preserve Unity/Python folders and record `EvidenceVR_TaskEvents` with Mindtooth EEG in LabRecorder. [Full setup, reduced-questionnaire flow, timestamps, export and pending device acceptance](LayerABStudyGuide.md). The existing workflow below remains the legacy/test-console workflow, and its saved bootstrap was restored after validation.

## Gaze overview on Physiology

Refresh the console and select **Physiology > Gaze overview**. **Last 10 seconds** shows a fading distribution; **Latest point** shows only the freshest valid projected eye position. Task/Movie percentages use valid tracked time; Valid tracking uses the observed window. This is a front-wall projection with schematic object outlines, not a calibrated screen heatmap.

The updated Unity source and genuine tracked eyes are required. Mindtooth EEG data alone does not create gaze points. **Waiting for Unity**, **Tracking unavailable** or **Gaze telemetry unavailable** identify different missing inputs. Existing APKs need rebuilding for this telemetry; an updated Editor session can provide it. No new build or Quest Pro physical acceptance accompanies this UI update. [Definitions, missing-data behavior and validation](GazeConsoleReview.md).

## Engineering calibration and saved-model tests

**Persistent configuration (2026-11-04):** Unity LSL **Connect** and **StartEvidenceConsole.cmd** now automatically use `UserSettings/personalized-policy.json` when present. Explicit command-line policy paths take precedence. Unity preserves channel labels and **Analysis units (after scaling)** in saved mappings. For the same compatible engineering source/model, a bridge restart does not require recalibration or re-entering those settings. After a publisher restart, scan and explicitly reselect its new outlet; a matching remembered mapping is restored. For a new source or changed known layout, review the mapping and policy. Errors now show specific saved/current differences; do not edit a model manifest to bypass them. [Diagnosis, recovery and validation](ModelCompatibilityReview.md).

Launch **StartEvidenceConsole.cmd** and use a fresh test identity. In Physiology, verify eight Mindtooth traces at 250 Hz. In Unity select the operator-managed engineering configuration before starting an assignment. A live EEG mapping is required for managed preparation. [Mindtooth acquisition, verification and pending checks](MindtoothAcquisitionReview.md).

Use **Calibration only** to save responses, training rows, reports and any fitted model. Next time select the same ID/root, choose **Bioadaptive test** and a saved model; compatible models load without calibration or refitting. A rejected model additionally requires **Allow unvalidated candidate model** and stays visibly engineering-only. Insufficient labeled data may produce no estimator. **Full protocol** retains the original sequence. The page shows model/control reasons and archive state; keep the app/service open until `complete_core_records`. This state verifies the five core files, including aborted runs, rather than successful experiment completion.

Latest candidate: dev6/code 30, with the updated Python host and operator bootstrap. Use [the complete setup and recovery guide](ModelReuseTestGuide.md) for paths, model checks, restart behavior, APK installation, USB/LAN configuration and limitations. The normal participant protocol below remains separate from this engineering test entry point.

## Evidence Console startup

The LSL source window detects this project's receiving service even when it was restarted outside the window. Stop Play Mode, then click **Disconnect** before changing sources. When already disconnected, Disconnect is disabled while Scan, Connect and the source picker remain available. The publisher stays running, so its name can still appear in the list. An external service with recent Unity/Quest telemetry must finish its session first. [Disconnect correction](LslSourceSelectionReview.md).

Enable LSL on the connected amplifier. The console selects `104209-1083_eeg` and displays a bounded preview before Unity starts. If the selected publisher restarts during operation, rescan and start a fresh session.

The pages distinguish base proposals, Guard verdicts/delivered actions and matching Unity receipts; show current Mindtooth EEG waveforms and completed feature windows; and provide the protocol with oral questionnaire links. The guide is reference only and cannot confirm Google Forms submission. Closing the browser leaves recording running. After ending the experiment and closing Unity Play/the Quest app, wait five seconds and use **StartEvidenceConsole.cmd --stop**, or Disconnect in the Unity LSL window. Changed sources and service restarts require a fresh personalized session. Standalone Quest requires an explicit USB reverse tunnel or reachable LAN URL. [Complete setup, connection routes, records and verification boundaries](EvidenceConsoleGuide.md).

## Current task protocol

Updated 2026-11-03 for current source and separate candidate `3.0.0-stage3-dev6` / code 30, retaining the dev11 configuration/HTTP contract, protocol `personalized-rf-v2` and Python policy `mindtooth-eeg250-pilot-v1`. Project PlayerSettings remain dev12/24 outside separate builds. Stage 1 remains the default without an enabled adaptive override. Read the [lower-stimulus review](LowerStimulusReview.md), [desk-sign HUD review](DeskSignHudReview.md), [short calibration review](ShortCalibrationProgressReview.md), [control rules](PersonalizedControlRulesReview.md), [validity revision](PersonalizedValidityReview.md) and [acceptance record](AcceptanceRecord.md).

## Optional Stage 3 evidence audit

Stage 3 adds `evidenceGuardMode=off|observe|enforce`, default off. The dev5/code-29 candidate includes the short calibration, clearer desk-sign typography and lower/slightly larger sphere; its Python/configuration files are unchanged from dev2/code 26. Original dev1/code-25 artifacts retain their earlier budget/UI. Install the independent package into the service interpreter, select a frozen session mode and start a fresh session; begin with observe when testing the guard. Patent observe executes the base controller normally when Stage 2 is ACTIVE; it is distinct from Stage 2 SHADOW. Enforce only filters unsupported ON; OFF/pause/exit remain available. Earlier dev12 APKs cannot supply the new transport/authorization contract. Use the [Stage 3 guide](EvidenceGuardGuide.md) for installation, audit/replay, removal and required Quest Pro checks. No active probes or participant qualification are introduced.

## Current source: illustrated controller guidance

The preparation screen uses a left-trigger illustration and large A/B keycaps. The warm border and `TRY NOW` show the first unfinished check; `READY` confirms each actual input, and the count shows how many of the three checks are complete. The order is a suggestion, not a new gate. Squeeze the left trigger (keyboard C) to place the task, press A and B once, then use X/Enter when the existing preparation and service checks permit continuation. These cards are instructions for physical buttons, not pointer-clickable UI.

The A/B meanings remain visible after checks and in a compact legend on subsequent instruction pages. Sessions record `controlHintRevision=controller-guidance-v1`; Stage 3 candidates include these cards, while the historical dev12 APK does not. [Implementation and verification](ControlHintUiReview.md).

## Run the current personalized protocol with the dev11 configuration contract

1. Stop Play Mode. Choose **Evidence VR > Stage 2 > Enable dev11 Personalized Engineering Protocol**. The old override is backed up; configuration freezes at session start. For participants, generate a new allocation with the current tool and a unique managed enrollment index.
2. Start the updated Python service and matching `mindtooth-eeg250-pilot-v1` policy. Select exact LSL outlets and confirm actual Mindtooth EEG channels, units, reference and contact. The source panel starts with a non-admitting policy; `service.py --personalized-policy` selects an explicitly reviewed policy. The dev11 configuration/HTTP contract remains supported. Old policy files/models are rejected; restart the service and begin a fresh session/calibration after updating. Existing deployment ZIPs contain their original Python policy and must not be mistaken for this update.
3. Complete seating/A/B checks and inspect source identity, Mindtooth EEG mapping, units and contact. Participant mode still requires ready media and a recent live/receiving service status; that connection check does not certify electrode quality. Continue directly to practice instructions and up to 14 practice stimuli. There is no timed resting baseline. X/Enter skips practice at the next boundary. Correctness feedback is shown in practice only.
4. Run four alternating twelve-stimulus calibration blocks by default: 48 trials / 216 s (3:36). Explicit six/eight-block allocations remain supported. Existing saved allocations keep their specified budget; generate a new allocation or use the new four-block engineering example for this revision. Calibration and both training phases have a neutral countdown and no correctness feedback. Python labels each 22.5 s window from exactly its own five trials, trains the personal model and runs held-out-block and circular-permutation checks. Model computation may take several minutes in addition to acquisition. A first failure offers one supplemental L0/L1 pair (108 s, total 5:24); a second retains the invalid-model status and may use an explicitly logged behavioral fallback.
5. Keep the headset and electrodes in place. The experimenter reads the calibration questionnaire aloud and records spoken answers in Forms. Then run Training 1 (96 stimuli), oral questionnaire/rest, Training 2 (96 stimuli), and the final oral questionnaire. Participant UI hides the strategy. X confirms completion, not a Forms submission. See [wording, scoring and linkage](QuestionnaireSetup.md).
6. If removal or electrode movement is necessary, pause and start a new session after checks. H on the application's keyboard records movement; supported XR presence loss also invalidates and pauses. An invalidated session cannot resume or accept late model/control results. X stops; Left trigger / C restarts from preparation, practice and fresh calibration. Unsupported presence reporting cannot certify unchanged contact. On standalone, use pause/stop and Left trigger to restart.
7. Save Unity and Python session folders. `analyze_personalized.py` retains failures, incomplete sessions, questionnaire administration and mount interruptions. Check actual exposure as well as performance.

Four-second visibility/responses, the 4.5-second cycle, onset cue, two-decimal countdown, scoring and media schedules are retained. The two observe-only stimuli are included in each phase's budget. Default timed duration with full practice is now 19:03, or 20:51 with the supplement, plus model fitting, instructions, oral questions, rest and preparation. Explicit six/eight-block initial budgets retain 20:51/22:39 before any supplement.

During trials two small static desk signs show `TIME LEFT` on the left and `PROGRESS` on the right, below and away from the sphere. Sign centers are 0.80 m apart with bases on the tabletop. Both values use large bold numerals, with bold headings and smaller regular captions; [typography details](SignTypographyReview.md). Practice shows `2/14`; calibration shows `2/12` with `Block 1/4` (or `Extra block 1/2`); each training phase shows a phase-wide count such as `24/96`. The number increases when a trial ends, including observe-only and consumed interrupted slots, not immediately on an answer. It is not a correct-answer or valid-window count. Calibration resets per block; training does not reset at internal 24-trial boundaries. No progress count appears during pauses, instructions, questions or model fitting. [Layout and previews](DeskSignHudReview.md).

The five-trial score thresholds, circular-null assumptions and AUC floor require pilot qualification. Default policy remains unfrozen, device-unqualified, permutation-unqualified and without an AUC minimum. ACTIVE cannot override these gates. The model is frozen before training and is never refitted during Random or Bioadaptive. Continuous wear was selected instead of automatic rewear offset correction.

L0 has no added media; L1 uses complete AV films. All 18 training pairs are eligible, four remain reserved, and actual exposure is logged. Human media/volume review remains required. P007 is recorded replay only; its failed EEG windows were reproduced and audited per channel without changing thresholds or dropping channels.

## Current personalized adjustment rules

Every five completed trials, evaluate the latest twenty-trial behavior window with its existing eligibility checks. Stable requires balanced accuracy >= 0.70 and fewer than four omissions, without an RT ceiling. Struggling requires balanced accuracy <= 0.40 or more than five total omissions (at least six), not necessarily consecutive. Four/five omissions alone do not support adjustment. Correct RT remains descriptive and is null if no correct response exists; this no longer blocks difficulty detection.

With an admitted model, the mean of at least three valid recent current-condition predictions must also be <= 0.45 for L0 -> L1 or >= 0.65 for L1 -> L0. Intermediate/disagreeing or insufficient evidence gives HOLD. Quality, dwell, exposure, safe-boundary and mount gates remain active. After two failed model attempts, enabled behavioral fallback uses the revised behavior rules and stays marked as fallback. Random and five-trial labels are unchanged. [Exact rules and verification](PersonalizedControlRulesReview.md).

## Warm study presentation

The scene uses cream walls, oak surfaces, static bookshelves/plants, linen seating and fixed baked lights. Check `environmentRevision=warm-study-v1` and the application version of the actual running build in `session.json`. Keep this environment consistent across conditions and record revisions when comparing sessions. The configuration/HTTP contract remains dev11; use the existing **Enable dev11 Personalized Engineering Protocol** menu with the updated Python policy. Model admission and signal preparation are unchanged. Before participant use, inspect the actual Quest Pro for contrast, color recognition, seated scale, comfort, distraction and performance. [Review](LabEnvironmentReview.md).

## Retained device/presentation rules

Meta Quest Pro remains the deployment and genuine-eye target. Engineering eye rays/outlines are default off and forbidden in participant sessions; invalid eyes never become head-direction gaze. Current shared placement is `low-stimulus-v2-movie-clearance`: tray top 0.72 m, sphere/tray gap 0.025 m, diameter 1.08 times the configured base size (about 0.1698 m by default), tabletop 0.65 m. The movie surface is unchanged; inspect sphere/movie clearance from the actual seated eye position before use. Start a fresh session for this geometry revision. Gaze ROI `gaze-roi-v4-sphere-tray-area` follows actual sphere/tray bounds; its classification rules are unchanged. Current source records `personalized-lab-v2.6-sign-typography`, `trial-progress-v3-clear-type` and `calibration-block-budget-v2-short-default`, with retained flow `preparation-practice-calibration-no-rest-v1` and oral administration `interviewer_oral_headset_on-v1`. Historical Quest Pro Link acquisition is not current standalone, presence-sensor or physiological acceptance.

---

## Preserved Stage 1 experiment guide

For project progress and major changes, see [Project status and update log](ProjectLog.md).

Open `Assets/TaskPlatform/Scenes/NBackExperiment.unity` in Unity 6000.3.27f2 and enter Play mode. All participant-facing and project-authored content uses English. Meta Quest Pro is the intended deployment and genuine eye-tracking target. Earlier Quest 3 checks remain historical evidence; physical Quest Pro acceptance is pending. See [Quest Pro support and eye permission](QuestProSupport.md).

## Controls

| Action | Quest controller | Desktop keyboard |
|---|---|---|
| Match | Right A | A |
| Non-match | Right B | B |
| Continue / start block | Left X | Enter |
| Pause / resume | Left Y | P |
| Place task area during preparation | Left trigger | C |
| Restart while paused | Left trigger | C or R |

These are the default bindings. The displayed response prompts follow the configured action mapping. Only the first valid response is accepted. Held buttons do not generate repeat answers. Simultaneous responses and presses on the trial-transition frame are rejected conservatively. Releasing and pressing again is necessary for another response. All prompts are controller instructions, not pointer-clickable UI buttons.

Practice and formal feedback use the countdown: green `Correct`, red `Incorrect`, or red `No response`. It does not reveal the expected answer or offer a retry. The color resets for the next stimulus. Formal scored trials use the same green/red feedback by default (`formalFeedbackEnabled=true`). Warmups remain neutral and observe-only. Response controls are explained before each block.

## Session procedure

1. The experimenter checks configuration version, seed and formal block order, then records the anonymous participant code against the generated session ID externally. The app does not request names.
2. The participant sits comfortably, looks straight ahead and presses the left trigger. The task is placed at initial eye height, 1.5 m forward, then stays fixed in world space. Press A and B once each. All three preparation cards must say READY before X continues.
3. Read the 0-back rule: match BLUE; all other colors are non-matches. Press X. Observe two unscored warmups without answering, then complete eight scored practice trials. Practice includes answer feedback.
4. Read the 2-back rule and static BLUE > ORANGE > BLUE example. The third color matches two positions ago. Press X, observe two warmups, then complete 12 practice trials while continually updating color memory.
5. Confirm understanding before formal testing. If needed, pause and restart as a new session; the incomplete session remains saved. Repeating only one practice block is not implemented.
6. Read the first formal rule and press X. The block contains two warmups plus 30 scored trials, taking 144 seconds. The full instruction board gives way to the freestanding stimulus, workbench and compact rule/countdown signs. A quiet tone marks each color onset. Correct answers turn the countdown green; incorrect answers and omissions turn it red. There is no score panel or response-dependent audio.
7. Rest for at least 25 seconds. The next rule is shown. After the countdown, X opens the instructions and a second X starts the next block.
8. Complete the other formal block. The completion screen confirms that responses have been saved. Wait for the experimenter before removing the headset. X creates a fresh session without overwriting the previous one.
9. Export the files and recompute metrics with the Python tool below. Inspect omissions, interrupted trials, configuration and scheduling deviations.

Default formal order is Easy then Hard. Set `hardFirst=true` for Hard then Easy; practice order stays unchanged. The v1.3 task intervals total 396 seconds (45 + 63 practice; 144 + 144 formal), plus 25 seconds of rest. Preparation, rule reading and pauses are additional.

### Reading the sequence guidance

One session is the complete run, containing four task stages: Practice 1, Practice 2, Main Block 1 and Main Block 2. The break is between the two main blocks. Instruction screens identify the complete stage and block, such as `STAGE 3/4 / MAIN BLOCK 1/2`. The minimal running header uses `Main 1/2 | 0-back | Color 7/32`.

`Color 7/32` identifies the seventh presented color, including the two warmups. Counts restart at each block. The fuller pause screen also shows the scored response-trial count. During warmups, the minimal rule says `Observe only`.

For 0-back, always compare with the named target color (BLUE by default). For 2-back, compare with two positions earlier: #3 with #1, #4 with #2, #5 with #3, and so on. The current instruction states the exact pair, such as `Compare #7 with #5`. Remember the earlier color yourself; running trials do not display color history or reveal the answer. Same color means MATCH; a different color means NON-MATCH.

## Timing, sequence and analysis

Each trial has a fixed 4.5-second cycle: show the sphere and accept responses for 0–4 s, then hide the sphere and close the response window for 0.5 s until the next onset. The current shared countdown displays two decimals (for example 4.00, 3.27, then 0.00); the deadline is unchanged. An onset tone is requested once per color, including warmups. Early responses never hide the sphere, advance the trial or stop the countdown. Warmups reject responses and display Observe only. Pause freezes remaining visibility and response time; resume does not replay the tone. Each formal block has exactly ten match and twenty non-match scored trials.

Each block has its own memory history and seed. The generator uses a fixed xorshift32 implementation and Fisher–Yates shuffle; block seed = session seed + block index × 104729, using 32-bit arithmetic. Identical configuration and seed reproduce the colors and answers. Randomization does not forbid all coincidental runs or impose an unspecified maximum streak length.

Pause preserves the remaining trial time and marks the active trial `interrupted=1`. RT is measured from actual software onset to response and includes pause time. The summary reports both all completed trials and clean trials excluding interruptions. A player-loop stall over 250 ms automatically pauses instead of silently skipping stimuli. The pause event records its actual handling time and separately identifies the frozen interval.

Accuracy = correct / scored trials, counting omissions as incorrect. Balanced accuracy is the average of match recall and non-match recall. An omission is never a correct non-match. RT summaries use correct responded trials. Aborted incomplete trials are reported separately.

## Configuration and presentation

Current application is `1.7.1-stage1`, presentation `spatial-lab-v1.7.1-more-video`, configuration `stage1-v1.7.1-more-video`. The tabletop is fixed at 0.65 m above the virtual floor. The sphere stays visible for all four response seconds. Running UI is reduced to stage/mode/color count, a short comparison rule and the countdown; practice and formal feedback are colored on the countdown. Floor tracking uses zero rig height offset; Device fallback normalizes seated eye height to 1.20 m. Sit facing the front wall and use the left trigger to calibrate. The large instruction panel keeps at least 12 cm clearance above the tabletop; the running labels sit forward of the sphere support and use narrow outlines for video contrast. See `UiClearanceReview.md` and `FrontWallCalibrationReview.md`.

Version 1.7.1 schedules five ten-second front-wall videos, one door sound, one passing vehicle, one approaching footstep clip, one conversation, one news clip and one near-ear soft voice per formal block. Video onsets are 24.6 seconds apart (about 5.5 trials); six audio events are interleaved, giving eleven total events per default formal block. Each video uses one of five seeded front-wall positions without consecutive repeats. Practice remains distraction-free. The schedule is balanced between Easy and Hard, non-overlapping and saved with a separate resolved random seed. All media is local. See `VideoFrequencyReview.md` for the current schedule and `SpatialAudioFeedbackReview.md` for media, audio paths and feedback. Speech uses two synthesized English voices. Six added news scripts preserve dated source links; near-ear clips are quiet speech, not true whisper synthesis.

Version 1.3.1 introduced presentation `spatial-lab-v1.3.1-satin-sphere`. The user-approved sphere refinement adds a satin lit material and smoother surface tessellation. Base colors, physical radius, placement, four-second response timing and scoring are retained. Shading changes apparent surface brightness; session metadata distinguishes this revision from the earlier unlit stimulus. See `StimulusMaterialReview.md` for visual checks.

The default configuration is `Assets/TaskPlatform/Config/default-task-config.json`. Color indices: 0 BLUE, 1 ORANGE, 2 GREEN, 3 MAGENTA. Sphere diameter is `2 × distance × tan(visualAngle / 2)`; defaults yield about 0.157 m at 1.5 m (6 degrees).

The v1.1 quiet-lab presentation adds a warm neutral room, architectural seams, matte rounded panel, English typography, preparation checks, button legends and a static 2-back example. The existing unlit stimulus colors are retained. The room and panel remain identical across formal conditions, with no animation, music, post-processing or feedback. `presentationVersion` in session.json identifies this visual revision; do not silently pool it with v1.0 without considering the changed visual environment.

Version 1.2 adds persistent stage/block labels, color and response-trial counters, explicit warm-up instructions, numbered examples and current comparison indices. It records `quiet-lab-v1.2-sequence-guidance` in session metadata. These cues may affect task experience; review comparability before combining presentation versions. Sequence generation, timing, colors, geometry and scoring are unchanged.

Version 1.3 implements the user's approved spatial layout and timing revision. That revision used configuration `stage1-v1.3-response4s`, presentation is `spatial-lab-v1.3-response4s`, and application is `1.3.0-stage1`. The sphere retains its 1.5 m distance, 6-degree diameter and unlit colors; it floats above a grounded workbench plinth with separate world-space signs. Instruction, rest and completion screens still use the full board. The onset cue is a 120 ms mono 660 Hz sine with a smooth envelope, fixed pitch and default gain 0.12. `onsetCueEnabled` and `onsetCueVolume` are frozen into each session configuration. There is no music, ticking, or answer-dependent sound. Audibility depends on headset output and volume and must be checked physically.

This revision changes response time, auditory cues and visual context. Treat it as a new protocol revision; do not silently pool its data with earlier versions. In v1.3 the sphere was visible for one second; v1.5 extends visibility to all four response seconds. Existing custom configuration overrides retain their explicitly configured timing; remove or update an old override to use the new defaults.

`matchAction` and `nonmatchAction` default to RightPrimary and RightSecondary and can be swapped. Continue and pause use `advanceAction` and `pauseAction`. Actions are pre-authored in `TaskControls.inputactions` with Oculus Touch and Quest Pro Touch layouts. Every operation must use a distinct button action; changing the action asset requires rebuilding.

To override a device configuration, place a complete `task-config.json` in `Application.persistentDataPath` and restart the app. Each session deep-copies and saves its configuration. Update its version when changing parameters. Runtime file edits never alter an active sequence.

`TaskEngine.RequestDifficulty(mode, now, frame)` accepts requests only in Preparation, Instructions or Rest, and rejects Running/Paused requests with a logged event. Each new block rebuilds the sequence and warmups. No automatic adaptation or physiological processing exists in Stage 1.

## Data and export

Editor: `%USERPROFILE%/AppData/LocalLow/EvidenceVR/Evidence-Aware VR Task Platform/TaskPlatformData/editor-test/<sessionId>/`.

Quest: `/sdcard/Android/data/com.evidencevr.taskplatform/files/TaskPlatformData/participant/<sessionId>/`.

Synthetic validation data is stored separately in `Evidence/SyntheticSessions/automated/`. `sessionType` records the category as well.

| File | Contents |
|---|---|
| session.json | Session ID, type, UTC start, application/Unity/presentation versions, configuration, status |
| trials.csv | Formal records including warmups, omissions, interruptions and aborted trials |
| practice_trials.csv | Separate practice records |
| events.jsonl | Planned/actual times, deviation and frame number for stimuli, input, rules, pauses and frame gaps |
| summary.json | Metrics generated by the analysis script |

Events use `Time.realtimeSinceStartupAsDouble`. These are software events, not measured photon onset. Frame gaps are software diagnostics, not compositor dropped-frame counts. Writers flush each row; forced termination can leave an `in_progress` session, which must not be treated as complete.

`trial_countdown_start` records the window duration and whether the trial permits a response. `audio_cue_requested` records cue version, gain and software playback-request time; it is not a measured acoustic onset. The visual timer follows the same planned trial clock and pause offset as the response gate.

```powershell
adb install -r Builds/Quest/EvidenceVR-Stage1.apk
adb pull /sdcard/Android/data/com.evidencevr.taskplatform/files/TaskPlatformData ./ExportedSessions
python Tools/summarize_session.py "path/to/session-directory"
```

Enable developer mode and accept USB debugging on the headset before using ADB. Quest Link is not needed for standalone APK testing.

## Rebuild and validation

### Unity Play Mode with Quest Link

USB attachment alone does not stream Unity Play Mode to Quest. For Editor preview, start Meta Quest Link / Meta Horizon Link on the PC, then enter Settings > Quest Link > Launch Quest Link inside the headset. The PC OpenXR runtime must be Meta Link. Unity uses the Windows/Standalone XR settings in Play Mode even when the active build target is Android.

Standalone OpenXR automatic initialization and Oculus Touch controller profiles are configured for this project. `AgentScripts/ConfigureQuestLink.cs` can reapply these settings through Unity Pipeline. Once the headset is inside Link, press Play in Unity. Link runs the application on the PC; it does not measure standalone Quest performance.

For standalone testing, install and launch the APK instead. ADB status `unauthorized` means USB debugging permission must be accepted inside the headset before APK installation. This authorization is separate from the Link connection.

Reference: [Meta Link development workflow](https://developers.meta.com/vr/documentation/unity/unity-link/).

### Build and software checks

Use `Evidence VR / Apply Quiet Lab Presentation` for the scene presentation, `Evidence VR / Run Automated Validation` for core checks, and `Evidence VR / Build Quest APK` for the Android package. Core results go to `Evidence/automated-validation.txt`; independent export verification is `python Tools/verify_exports.py`.

See [AcceptanceRecord.md](AcceptanceRecord.md) for the evidence boundary and remaining headset checks. OpenXR setup follows the installed package and [Meta's configuration documentation](https://developers.meta.com/vr/documentation/unity/unity-xr-plugin/).
# Isolated recorded EEG comparison

For engineering replay, run `Tools/AdaptiveTraining/simulate_saved_model_comparison.py` with the project virtual environment. Supply `--model` (saved model folder), `--recording` (raw_signals JSONL or Mindtooth semicolon CSV), and `--output` (a new isolated folder). Optional `--reuse-inputs` points to a previous simulation folder to preserve its exact responses and gaze targets/validity/timing. Video visibility and task level follow the new decisions. The model and thresholds must match when reusing inputs.

CSV input requires the model's eight channel labels in order, 250 Hz metadata, finite samples and increasing, sufficiently continuous millisecond timestamps. CSV units are not established by this format; the report explicitly labels the model's uV/scale 1 assumption. Each arm starts at its recorded replay offset; short recordings wrap. Outputs include provenance, decisions, simulated inputs, verification evidence when separately checked, and `report/index.html`. The report may be served locally; this workflow does not require Unity, change the live Console assignment, or establish synchronized participant outcomes. See [eye-open replay acceptance](AcceptanceRecord.md#eye-open-csv-replay).


## Decision gaze evidence

Console v1.21 provides the same compact evidence card in Run monitor and the session's Decision evidence page. Select a checkpoint, then switch **At decision** / **After decision**. The first view uses the recorded decision intervals; the second shows subsequent completed trials separately. Expand **Time-aligned evidence** for response/playback context and **Criteria & source records** for observed values, thresholds and the original receipt. Session totals are under **A/B session summary**.

For a real heatmap, select the Quest Pro environment and supply genuine tracked-eye telemetry from Unity. The monitor updates the recorded map every five seconds; use Refresh in archived reports. Desktop's fixed 75%/70% values do not create positions. Missing or legacy coordinates display an empty map. If multiple projection frames exist, choose a frame; different coordinate frames are never overlaid. Heat intensity is elapsed time per cell, with a local scale printed below the map. It is not RF workload or proof that a decision was correct. [Detailed method and pending device checks](DecisionGazeEvidenceReview.md).


## Decision clarity and operator wording

Use **Create session assignment** in Study setup; complete preparation and start the task in Unity with Enter/X. The primary Experiment guide covers independent Practice, Calibration and Adaptive Comparison and identifies the instruction environment. Old continuous-protocol instructions are in the legacy reference.

**Train Personal RF · forest / arithmetic** produces the saved workload RF for A/B. Legacy control calibration is a separate protocol, not an extra mandatory step for that RF. Mindtooth Neurometrics remains a separate single-period vendor test.

Decision evidence separates base proposal inputs from current-condition review inputs. An unavailable current-condition RF is not replaced by the base score. The recorded reason identifies the review outcome; contextual explanations derived from saved fields are labeled. Desktop ratios are assumptions, and real gaze is not collected in that mode. A command receipt confirms acknowledgment; inspect playback records for decoded exposure.

**Copy recommendation to draft** only fills the threshold fields. In a running session, current applied values remain unchanged until **Apply thresholds**. The display labels pending edits and calibration-window trigger counts. This wording revision changes no experimental rules. [Review](ConsoleClarityRevisionReview.md).


## Checkpoint progress navigation

In the session report, drag the Checkpoint bar or use the previous/next buttons to browse recorded decisions. Arrow keys move one checkpoint; Home/End select the first/last. The counter counts filtered checkpoints, not elapsed time or trial completion. The caption shows the arm, trial and base-to-delivered action. Round/Show filters update the available steps; Refresh retains the selected request when it remains available. This navigation never controls Unity. Key numeric evidence is bold while base and review windows remain separate.

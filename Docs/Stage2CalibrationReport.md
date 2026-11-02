# Stage 2 calibration and integration record

Current protocol: `2.0.0-stage2-dev5`; use [rolling-policy review](RollingAdaptationReview.md). The original worksheet was prepared for dev1. The candidate library now contains 22 pairs. All individual calibration must explicitly match dev5; shorter control windows and thresholds require real-participant validation. Prepared 2026-11-02. **No participant calibration study has been run.** This is the working report template, not a completed result.

## Film-pair readiness

All 22 current candidate pairs have been downloaded, paired, normalized and software-audited; see [media catalog](Stage2MediaCatalog.md). Human semantic review, content/emotion matching and headset volume review remain pending. Record title/version, exact source interval, duration, audio/video hashes, content/emotion, background music/effects, source/usage record, fixed gain and observed visibility/intelligibility. Record rejection reasons as well as accepted clips. The nominal 3/3/2/2 title allocation is a starting point, not a matched final stimulus set.

## Fixed-condition pilot

Use the `calibration` protocol for approximately 6–8 volunteer engineering-pilot participants, under the applicable study procedure. Each completes familiarization, two silent 2-back baselines, and counterbalanced Audio/AV blocks. Record the real device and sensor setup, participant code, seed, counterbalance group and configuration hashes.

| Participant/session | Device | Group | L0 baseline BA / RT / omissions | Audio BA / RT / omissions | AV BA / RT / omissions | Subjective distraction/workload per condition | Clip/replay/quality issues |
|---|---|---|---|---|---|---|---|
| Pending | Pending | Pending | Not measured | Not measured | Not measured | Not collected | Not assessed |

For each clip, compare trials during exposure and the next two trials with matched silent-slot timing, retaining omissions and interrupted/transition exclusions. Record whether the participant noticed or understood the conversation. Analyze the actual exported onsets, not only planned slots. Condition-level CSV summaries alone do not establish clip-specific effects.

If the content has little observable effect, review semantic engagement and visibility first. If AV is not harder than audio, report that result and recalibrate content/order or redefine the nominal levels; do not force a monotonic-load interpretation. Do not raise volume simply to create an effect.

## Individual physiological eligibility

| Participant | Source/channel/unit | Feature/window | Valid-data fraction and timing uncertainty | Repeated fixed-condition evidence | Offline recomputation / beat review | Validation decision and record |
|---|---|---|---|---|---|---|
| Pending | Not connected | Candidate only | Not measured | Not collected | Recorded P007 windows recomputed; live beat validation pending | Not eligible |

No EEG, ECG or eye feature is currently admitted as validated control evidence. Check real stream disconnection/recovery, timestamp mapping, clipping, flatline, movement artifacts and mixed-level windows. Validate ECG peaks against raw ECG before interpreting HR/HRV. HRV remains exploratory; no fast return-to-rest requirement is imposed.

## Five complete device sessions

| Run | Device/date/session ID | Standalone input and task | Three levels/media | Pause/stop/resume | Service/command/sensor failure handling | Export/replay | Outcome |
|---|---|---|---|---|---|---|---|
| 1 | Pending | Pending | Pending | Pending | Pending | Pending | Not run |
| 2 | Pending | Pending | Pending | Pending | Pending | Pending | Not run |
| 3 | Pending | Pending | Pending | Pending | Pending | Pending | Not run |
| 4 | Pending | Pending | Pending | Pending | Pending | Pending | Not run |
| 5 | Pending | Pending | Pending | Pending | Pending | Pending | Not run |

Use Quest 3 for the available physical platform checks. Record Quest Pro checks separately if that headset becomes available. A Quest 3 run does not verify Quest Pro eye tracking.

## Transfer and final freeze

Record new-media pre/post balanced-accuracy cost relative to silent baseline, RT and omission changes, exact held-out allocation, clip matching, exposure history and subjective experience. Results are preliminary near-transfer observations; practice, fatigue, order and familiarity remain alternative explanations. Longer-term/generalizable training effects require a suitable control and delayed follow-up.

Final Stage 2 acceptance requires all real integration checks, at least one personally calibrated usable physiological feature actually contributing to control, five complete device sessions and a reviewed fixed protocol. Record software success, valid physiology and training-performance changes as separate conclusions. Only then freeze the controller/configuration as the Stage 3 Patent OFF baseline.

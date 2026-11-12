# Independent comparison stages and animated engineering eye marker

2026-11-11. Stage 2/3. Protocol `comparison-three-stages30-v1`; Console `evidence-console-v1.31-three-stages30`; eye marker `engineering-gaze-ray-v6-animated-bird`.

## Implemented behavior

Console offers three separate comparison assignments alongside Practice and Calibration:

| Selection | Trial budget | Video control |
| --- | --- | --- |
| Fixed Exposure | 30, including two observe-only warmups | Positions 1–15 OFF; positions 16–30 continuously ON. The switch occurs at the start of position 16. No adaptive requests or commands are accepted. |
| Adaptive — Without Evidence | 30, including two observe-only warmups | Existing Round A controller; evidence review remains observational. |
| Adaptive — With Evidence | 30, including two observe-only warmups | Existing Round B controller with enforced review and selective support probes. |

Each assignment starts with fresh controller, playback and support state. The selected model, thresholds, environment, stage and revision are recorded. A stage ends with its post-condition oral questionnaire; operator acknowledgment completes that assignment. It does not start another round, impose the paired inter-condition rest or request the paired final-preference questionnaire. Select the next stage independently. Use the same participant, model and intended threshold values when comparing A and B.

New adaptive stages retain five-response windows and checks every two positions, at response close on counts 2 through 28. Four-trial support probes may open through count 24 and complete by 28. Ordinary ON remains possible at 26 and 28 when authorized. The current BA thresholds (ON 0.65, OFF 0.50, model-only OFF deferral 0.80), omissions, gaze checks and support cooldown rules are unchanged. No failure lock is added. Historical 60/144-trial allocations remain readable and keep their recorded policies.

The four-second response window, 4.5-second onset cycle, integer countdown, current final-choice scoring/input acknowledgement, display geometry and media content/audio remain unchanged. Fixed ON is a request to the existing continuous player; first-decoded-frame events establish actual exposure. Missing media pauses the task through the existing failure path. Pause, interruption and completion stop exposure. The first two positions count within the first fifteen; no extra warmups are appended.

## Exploratory threshold candidates

Policy `fixed-exposure-behavior-thresholds-v1` is a read-only report calculation. It does not retrain, admit or replace the saved model, modify calibration recommendations or automatically change later thresholds.

The report uses non-overlapping five-response groups at positions **3–7, 8–12, 16–20, 21–25 and 26–30**. This avoids the observe-only warmups and groups crossing the fixed switch. Each retained score is the existing model's latest five-second EEG score at that group's end; its timestamp is included. The score is associated with the five-response behavioral summary, not assumed to represent an independently measured workload ground truth over all 22.5 seconds.

Eligible groups require a valid finite score from a live-sensor prediction, eligible scored responses, one video condition and decoded playback matching the intended exposure (OFF: no overlap; ON: at least 95% of the source behavioral window). Current acquisition, units, clock and mount gates remain in the scoring path. Rehearsal, missing scores, interrupted trials and mixed conditions cannot supply candidate thresholds.

Stable anchors use BA >= 0.65 and fewer than two omissions. Struggling anchors use BA <= 0.50 or at least two omissions. BA is computed only when both response classes are present. Intermediate behavior remains unlabeled. Video presence itself is never a workload label.

Candidates require a completed, uninterrupted stage, at least one valid group from each exposure and at least two stable plus two struggling groups. Candidate ON is the median stable score; candidate OFF is the median struggling score. OFF must exceed ON by at least 0.05. Otherwise the report shows the explicit reason no candidate was produced. It includes group scores, behavioral labels, exclusions, model identity and model hash. Copy reviewed values manually into a later stage's existing threshold fields; verify the participant and saved model match.

At most five groups are available. This is a provisional within-session suggestion, not held-out validation. Fixed OFF-then-ON order confounds time and exposure; it cannot establish that video caused a score or behavioral change. A session with consistently good performance may correctly produce no OFF candidate.

## Animated engineering marker

The supplied GIF is decoded, without repainting, into eight RGBA PNG frames. The original 150 ms frame duration and 1.2 s loop are preserved. Source SHA-256 and timings are stored in `Assets/Resources/GazeBird/animation.json`; the project uses English asset names.

A camera-facing textured quad replaces the eye-ray endpoint ring, preserving its hit position and angular size. The thin ray and separate target highlights remain. LIVE uses the bird's original colors; FROZEN uses amber tint; HELD uses grayscale tint and the last drawn position. The existing HUD identifies these states. Animation continues in held state, which is a display behavior and does not assert fresh eye input. Before any usable position, the marker remains WAITING. Turning the toggle off hides the marker and ray. It has no collider and does not affect gaze validity, target classification or logging. Engineering-only exclusion includes participant practice.

## Validation and acceptance

Validation is isolated under `Evidence/ThreeStageComparison-20261111/`; no synthetic data were written into participant sessions. The source restore reference is `codex/restore-before-three-stage-20261111` at `dbd2eec6f5f9ef3f9f32c77e5e7e4b3d585d4f86`. Existing untracked recovery scenes were left intact.

Passed: 267 Python tests and 9 final targeted checks, Unity compilation, all three actual backend/Unity configuration round trips, 90-position deterministic TaskEngine checks, a full real-time 30-position fixed stage with real local decoding (zero OFF leaks, zero sampled ON gaps, first decoded frame 41.5 ms after the request), eight-frame rendering/timing/state checks, 33 gaze regression checks, three browser assignment flows and four report fixtures at desktop/mobile widths. The old-backend UI test emitted no preparation request. Pipeline verification used local `commands/scripts.md`, `commands/editor-lifecycle-and-observability.md` and `commands/capture.md` from the installed package. Original scene restored; Play Mode stopped.

Deployment remains pending: automatic approval review rejected the stop/restart of the idle production service, returning only `blocked by policy`. No service process was stopped by that rejected call. Port 8765 still reports v1.30; the new UI blocks independent-stage creation until the updated backend is running. Worn Quest Pro marker size/comfort, real gaze alignment, physical audiovisual timing, headset builds, live Mindtooth threshold utility and participant acceptance require separate evidence. Remote Google Forms were not edited or submitted. If eye tracking freezes, follow the existing hand-tracking diagnostic suggestion in the ExperimentGuide; animation does not repair acquisition.

Source and reproducible checks: `AgentScripts/ThreeStageComparisonValidation.cs`, `AgentScripts/ThreeStagePlaybackValidation.cs`, `AgentScripts/BirdReticleValidation.cs`, `AgentScripts/validate_three_stage_console.py`, and `Tools/AdaptiveTraining/test_three_stage_comparison.py`.

# Stage 1 acceptance record

Date: 2026-11-01. Software implementation and desktop validation are available; full physical-headset acceptance remains pending. The current presentation is spatial-lab-v1.3-response4s, application version 1.3.0-stage1.

## Verified software evidence

| Check | Evidence |
|---|---|
| Sequence generation and scoring | 2,000 sequence sets, 1,000 seeds per mode; counts, ratio, independent answers and replay pass |
| Core assertions | 194,112 assertions in `Evidence/automated-validation.txt`, including four-second response boundaries and pause behavior |
| Complete synthetic runs | Five sessions per validation invocation; 88 stimuli each, 60 formal scored trials, both orders |
| Export-level recomputation | `Evidence/export-validation.txt`: 400 scored rows from five saved sessions independently checked |
| Real-time editor integration | `Evidence/live-playmode.txt`: queued InputSystem states, 40 seconds, 21,939 advancing frames, ten practice rows, pause/resume |
| Restart and session isolation | `Evidence/restart-validation.txt`: previous session saved as aborted_restart, fresh editor-test session created |
| OpenXR project configuration | `Evidence/openxr-validation.txt`; Android validation reports zero issues |
| Prior APK | v1.0 built and signature-verified; ARM64, VR launch category, Quest Pro/Quest 3 support declarations |
| Prior v1.1 APK | v1.1.0-stage1, version code 2; Android build succeeded in 191.7 seconds with zero errors; APK v2 signature verified; 41,735,754 bytes |
| Prior v1.2 APK | v1.2.0-stage1, version code 3; Android build succeeded in 111.3 seconds with zero errors; APK v2 signature verified; 41,737,330 bytes |
| Prior v1.2.1 APK | v1.2.1-stage1, version code 4; feedback wording changed to Incorrect; Android build succeeded in 53.2 seconds with zero errors; APK v2 signature verified; 41,736,466 bytes |
| Current v1.3 APK | v1.3.0-stage1, version code 5; Android build succeeded in 121.0 seconds with zero errors; ARM64 and APK v2 signature verified; 41,831,962 bytes |
| Visual refinement | See `SpatialTaskReview.md` for current v1.3, `VisualReview.md` for v1.1 history and `SequenceGuidanceReview.md` for v1.2 history |
| Spatial layout and countdown | 29 states and 88 color counters checked; final front, angled and instruction views inspected after fixing plinth/sign occlusion; `Evidence/visual-validation-v1.3.txt` |
| Four-second live integration | 14-second Editor probe: four stimuli, four playing cues, countdown 4/3/2/1/NEXT; a programmatic response after 3.2 seconds accepted and saved; `Evidence/response-window-live-v1.3.txt` |
| Sequence guidance | 29 snapshots checked; stage/color counters across 88 stimuli, two-back indices, warm-up prompts and absence of formal answer feedback verified; `Evidence/visual-validation-v1.2.txt` |
| Quest Link initialization | Standalone OpenXR configuration added after diagnosing missing Editor XR settings; Oculus runtime, a running XR display subsystem and head-tracking device observed in Play Mode. See `Evidence/quest-link-runtime.json`. Visual confirmation and physical input remain separate checks. |
| Quest 3 Link visual confirmation | On 2026-11-01, the user confirmed that the experiment scene was visible inside Quest 3 while Unity Play Mode was running through Link. This confirms Editor preview, not standalone APK operation or complete task/input acceptance. |

Synthetic data under `Evidence/SyntheticSessions/` and editor probes are not participant or device-test evidence. Historical live input probes remain as provenance; core validation and export verification were refreshed for v1.3. The response-window probe is explicitly marked editor-test, uses programmatic input, and does not establish physical sound onset or headset audibility.

The official Unity OpenJDK 17.0.18+8 module was installed during initial delivery after the first build found it missing. The current build has one warning: crash diagnostics lack debug symbols. Current build outcome is in `Evidence/android-build-report-v1.3.json`; package inspection and checksum are in `Evidence/apk-inspection-v1.3.txt`. SHA256: `3865FE023656F384B8D7A5A64901633966ADF9E6E3B54865D5A11BB5AC64FDAC`. Older versioned reports remain historical. The latest observed ADB connection required debugging authorization for APK installation; Quest Link preview is a separate connection. Final Editor checks found zero console errors, no compilation failure, Play Mode stopped and the scene saved.

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
| At least ten minutes of device operation | Pending | Refresh rate, software gaps, device performance tools |
| Five complete real sessions | Pending | Session IDs, complete status, 60 formal scored trials each |
| Export, metric recomputation, file persistence | Pending on device | Paths and summaries |
| Photon onset | Not measured | External instrumentation if required |

Do not label Stage 1 fully accepted or begin claiming physiological/adaptive validation until the required physical tests pass. The new room/panel change the visual environment; record the presentation revision and review comparability before combining versions.

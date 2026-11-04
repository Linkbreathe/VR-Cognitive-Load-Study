# Desk signs for countdown and trial progress

The later [dev4 lower-stimulus update](LowerStimulusReview.md) retains these signs and lowers the central support/sphere. The dev3 APK and images below retain their original taller support.

The [dev5 typography update](SignTypographyReview.md) retains the sign geometry and replaces the original font styling with clearer bold numbers/headings.

Date: 2026-11-03. User-approved presentation update, delivered as `3.0.0-stage3-dev3` / Android code 27. The user requested greater separation or small signs above the desk. The current personalized HUD uses two static desk signs.

## Layout and behavior

- The countdown sign is left of the stimulus and the completed-trial sign is right. Their centers are 0.80 m apart, increased from 0.44 m. Each face is 0.26 m wide and 0.18 m high, with a dark green-gray face, restrained wood-color edge, a stem and a small base.
- Sign centers are 0.86 m above the virtual floor, 0.21 m above the unchanged 0.65 m tabletop. The bases meet the tabletop. Their height stays tied to the desk when seated calibration changes the task anchor height. The world-space HUD plane and central sphere/tray remain unchanged.
- Static headings are `TIME LEFT` and `PROGRESS`. Existing countdown/feedback text and completed-slot counts are reused. Practice `/14`, calibration `/12` with block caption and training `/96` retain the prior semantics. Calibration remains four blocks / 216 s plus the existing optional 108 s supplement.
- Existing visibility rules apply: signs appear during running trials, and are hidden with the task canvas during instructions, pauses, questions and model fitting. The signs have no interactive raycast targets, animations or sound. Practice feedback retains its original colors; formal personalized countdown feedback remains neutral. Stage 1 presentation is unchanged.
- Presentation is `personalized-lab-v2.5-desk-signs`; progress presentation is `trial-progress-v2-desk-signs`. Session metadata records both. `personal_progress_display` also records desk placement, lateral positions and center height. Calibration-budget revision and all physiology/control policies are unchanged.

Runtime modifications decorate the existing countdown and progress copy. Shutdown removes only the generated decoration, restores the countdown's previous size/text layout and returns its original position. The saved scene is not rewritten.

## Software verification

Unity 6000.3.27f2, CLI 1.0.0-beta.15. Runtime compilation passed. All **81 existing progress/budget/UI checks passed** after the layout change. Another **31 geometry, text-fit and metadata checks passed**, including sign corners inside the camera view, tabletop contact, 0.80 m separation, passive UI and stable desk height after a synthetic 0.20 m task-anchor height change. The new PASS log was read back from the Editor console.

Calibration, practice and training previews were captured from the actual running scene using explicitly static synthetic states. Files: `Assets/Evidence/DeskSigns/calibration-desk-signs.png`, `practice-desk-signs.png`, `training-desk-signs.png`. These are engineering previews, not participant recordings or headset captures. Evidence: `Evidence/DeskSigns/`.

All eight protected files match their pre-update hashes: task engine, spatial task view, config model, media runtime, saved scene, Python model/runtime and restored PlayerSettings. The pre-build live console reported zero errors and one warning; retained historical messages were not cleared. Whitespace checking passed with Windows CRLF handled. No Python logic changed, so the prior 124-test host result is retained evidence and was not rerun for this visual update. The first hash-list attempt named a nonexistent `TaskEngine.cs`; the list was corrected to the actual `TaskModel.cs` before creating the final comparison manifest.

## Delivery and restore

New APK: `Builds/Stage3Dev3/EvidenceVR-Stage3-dev3.apk`. It uses the same Python service, guard wheel and four-block configs from the dev2 delivery; no new host package is needed. The dev2 APK/ZIP remain historical artifacts with the earlier floating-text layout.

Android IL2CPP build succeeded in **154.521 s**, with zero build errors and 347 retained warnings. APK size is **100,271,758 bytes**; SHA-256 `b3f158de3918bcb34214af41b9dbd8b647171fb797f44d6c7c664be4e1e1e2d7`. Manifest confirms application `3.0.0-stage3-dev3`, code 27 and `arm64-v8a`; APK v2 signature verified. Full build report, badging, signature and file identity are in `Evidence/DeskSigns/`. BuildReport intermediate size is not the APK size.

Source restore ZIP: `Evidence/DeskSigns/pre-desk-signs-source.zip`. Existing uncommitted work and unrelated recovery assets are retained. Project PlayerSettings were restored to dev12/code 24 with no remaining diff. Play Mode is stopped; the previously enabled Editor auto-tick is retained. No headset installation, Git publication or participant data collection was performed.

Pending: actual Quest Pro stereo readability, downward glance/comfort and standalone performance. Synthetic Editor checks cannot certify these. Revisions must remain distinguishable when comparing sessions because sign position and framing change the visual presentation.

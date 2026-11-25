# VR Cognitive Load Study

A Unity 2-back study with audiovisual distraction, a Python Evidence Console, and optional Mindtooth EEG and Quest Pro eye tracking. Run Fixed Exposure, Adaptive — Without Evidence (A), and Adaptive — With Evidence (B) as independent stages.

Install **Unity Editor 6000.3.27f2** (revision `22188f149405`) in Unity Hub before opening the project. [Project version file](ProjectSettings/ProjectVersion.txt)

## 1. Install the study software

Use Windows x64 and Python **3.10 x64** (tested with 3.10.4). Run these commands in PowerShell.

```powershell
git clone --branch main https://github.com/Linkbreathe/VR-Cognitive-Load-Study.git
cd VR-Cognitive-Load-Study
python --version
python -m venv Tools/AdaptiveTraining/.venv
.\Tools\AdaptiveTraining\.venv\Scripts\python.exe -m pip install -r Tools/AdaptiveTraining/requirements-verified.txt ./Tools/EvidenceGuard
```

Open the repository folder in Unity Hub and open `Assets/TaskPlatform/Scenes/NBackExperiment.unity`. With Play Mode stopped, select **Evidence VR → Advanced → Use Web Console Workflow**. This creates the local `adaptive-training-config.json` and logs its path. For Desktop or Quest Link, use `http://127.0.0.1:8765` as its `controllerUrl`.

## 2. Install the study media

- **Comparison stages.** Sign in to SharePoint with an account that has access and download the six HD mixes. The complete restore also requires ten companion ZIPs, the original clip bank, and two legacy streams. Ask the study operator for any missing files. Unity checks all files referenced by the catalogs. Place `AdaptiveMedia/`, `layer-ab-144-media.json`, and `continuous-comparison-media.json` in `%USERPROFILE%\AppData\LocalLow\EvidenceVR\Evidence-Aware VR Task Platform\`. Preserve existing media and `ContinuousMediaHistory/` when updating. [SharePoint video folder](https://aarhusuniversitet-my.sharepoint.com/:f:/r/personal/au759496_uni_au_dk/Documents/XR%20Lab/Wei%20Xiong/Patent/video?d=w94870fe5abb2459a9cc586e5c7540295&csf=1&web=1&e=bQiBEA) · [Restore instructions and checksums](Docs/MediaBackup20261112.md)
- **Workload calibration.** Obtain the approved forest video from the study operator. Save it as `Assets/Resources/CalibrationForest.mp4` and verify its hash. The forest video and external film packs are distributed separately from Git. [Calibration guide and video checksum](Docs/LoadReferenceCalibration.md)

## 3. Connect Mindtooth EEG via LSL

1. **Publish the stream.** Connect the amplifier in the Mindtooth app and turn on **LSL Stream**.
2. **Select the outlet.** With Unity out of Play Mode and the bridge disconnected, open **Evidence VR → Signal Sources** and click **Scan**. Select the live EEG outlet for your amplifier (`104209-1083_eeg` on the study PC). Check for 8 channels at 250 Hz.
3. **Map the channels.** Under **Advanced settings**, set **Processing** to **EEG mapping** and **Source provenance** to `live_sensor`. Use indices `0,1,2,3,4,5,6,7` for AFz, AF3, AF4, AF7, AF8, Pz, P3, and P4, in that order. Keep the bridge port at `8765`.
4. **Check units before calibration.** Verify the units transmitted over LSL. Set **Scale to analysis units** to the verified conversion factor and **Analysis units (after scaling)** to `uV`. Set `eeg_units_confirmed` to `true` in the reviewed local `UserSettings/personalized-policy.json` only after verification. The Mindtooth app's displayed units alone do not establish the units sent over LSL. [Policy template](Tools/AdaptiveTraining/personalized-policy.json) · [Mindtooth acquisition notes](Docs/MindtoothAcquisitionReview.md)
5. **Save and check reception.** Click **Save settings** to write `UserSettings/lsl-source-selection.json`. Run `.\StartEvidenceConsole.cmd` from the repository root. It uses the saved selection and opens **Study setup** at `http://127.0.0.1:8765/tests`. Look for **Receiving** in Unity's **Signal Sources**, a live rate near 250 Hz in **Study setup**, and updating EEG waveforms under **Signals**.

If Mindtooth restarts and samples stop, end the run, exit Play Mode, and click **Disconnect** in **Signal Sources**. Click **Scan**, select the current outlet, save, and relaunch the Console. Confirm that new samples arrive before preparing a fresh assignment. [LSL reconnection guide](Docs/ExperimentGuide.md#after-the-2026-11-17-lsl-recovery)

## 4. Enable Quest Pro eye tracking over Link

1. **On the headset**, turn on eye tracking and run **Calibrate eye tracking** under **Settings → Movement tracking → Eye tracking**. Some headset versions list **Eye tracking** under **Hands and eyes**. [Meta Quest Pro eye tracking guide](https://www.meta.com/en-gb/help/quest/8107387169303764/)
2. **On the PC**, open **Meta Horizon Link → Settings → General** and set Meta Horizon Link as the active **OpenXR Runtime**. Under **Settings → Developer**, turn on **Developer Runtime Features** and **Eye tracking over Meta Horizon Link**. Restart Unity after changing Link settings. Launch **Quest Link** from the headset. [Meta Link setup guide](https://developers.meta.com/vr/documentation/unity/unity-link/)
3. **In Unity**, with Play Mode stopped, choose **Evidence VR → Advanced → Quest Link Settings → Use Oculus for Editor Preview**.
4. **Check live gaze.** Select **Quest Pro** in **Study setup**, create an engineering assignment, and enter Play Mode. During preparation, confirm **Eye input** shows `TRACKED EYES`. Under **Signals → Gaze overview → Latest point**, keep your head still and look between two points. The live gaze point should move with your eyes. [Quest Pro Link verification notes](Docs/QuestLinkEyeRepairReview.md)

If eye tracking freezes or repeatedly drops out, recalibrate, reconnect Link, and restart Play Mode. As a diagnostic, try turning off **Hand and body tracking** under headset **Settings → Movement tracking**. Keep eye tracking enabled, use Touch controllers, and verify recovery in a separate engineering session. [Eye tracking troubleshooting](Docs/ExperimentGuide.md#eye-tracking-interruptions-hand-tracking-check)

For a standalone build, install **Android Build Support** with SDK, NDK, and OpenJDK in Unity Hub. Use **Evidence VR → Advanced → Build Quest App**, then deploy the APK, local bootstrap configuration, and media to the headset. Grant eye-tracking permission when the task first requests it. [Standalone USB and LAN setup](Docs/EvidenceConsoleGuide.md#quest-standalone-connection)

## 5. Run a session

For a desktop rehearsal, launch `StartEvidenceConsole.cmd`, select **Desktop** and **Task rehearsal instead**, and use a separate engineering ID and storage folder. This mode runs without EEG and uses simulated gaze.

1. In **Study setup**, enter a participant ID and a writable storage folder. To create a personal model, choose **Calibration → Train workload model · forest / arithmetic**. For a comparison, choose the stage and environment. For EEG-backed runs, select the participant's saved model and review the ON and OFF thresholds. ON must be lower than OFF.
2. Click **Create session assignment**, enter Unity Play Mode, and complete seating and A/B input checks. Press **X** on the Touch controller or **Enter** on the desktop to start. Each comparison stage has 2 observe-only warmups and 32 scored trials. Fixed Exposure keeps video off for the first 16 scored trials and on for the last 16.
3. After each comparison stage, complete the post-condition questionnaire and verify the archive under **Recent sessions**. Use the same participant ID and storage folder across related stages, with a fresh assignment for each. Select **Open report** to review the results. Keep engineering and participant records separate.

[Full experiment procedure](Docs/ExperimentGuide.md) · [Device acceptance status](Docs/AcceptanceRecord.md)

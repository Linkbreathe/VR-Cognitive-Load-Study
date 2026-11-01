using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace EvidenceVR
{
    public sealed class TaskPlatformController : MonoBehaviour
    {
        public TextAsset defaultConfig;
        public InputActionAsset inputActions;
        public Transform taskAnchor;
        public Transform head;
        public Renderer stimulus;
        public TMP_Text heading, body, footer, badge;
        public LabPresentation presentation;
        public SpatialTaskView spatialView;
        public TrialOnsetCue onsetCue;
        public EnvironmentalDistractions distractions;
        public bool Calibrated => calibrated;
        public bool MatchChecked => checkedMatch;
        public bool NonmatchChecked => checkedNonmatch;
        public TaskConfig SessionConfig => config;
        public TaskEngine Engine { get; private set; }
        public string DataPath { get; private set; }
        public static readonly string[] ColorNames = { "BLUE", "ORANGE", "GREEN", "MAGENTA" };
        static readonly Color[] Colors = LabPalette.Stimuli;
        TaskConfig config;
        SessionRecorder recorder;
        InputAction match, nonmatch, advance, pause, calibrate;
        Material sphereMaterial;
        bool checkedMatch, checkedNonmatch, calibrated, terminalRecorded;
        bool simultaneousLatch;
        double lastTime;
        string setupError;
        bool floorTracking, calibrationPending;
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        TaskPhase lastPhase = (TaskPhase)(-1);
        int lastTrial = -2;

        void Start()
        {
            try
            {
                config = JsonUtility.FromJson<TaskConfig>(defaultConfig.text);
                string overridePath = Path.Combine(Application.persistentDataPath, "task-config.json");
                if (File.Exists(overridePath)) config = JsonUtility.FromJson<TaskConfig>(File.ReadAllText(overridePath));
                config.Validate();
                ConfigureTrackingSpace();
                match = inputActions.FindAction(config.matchAction, true);
                nonmatch = inputActions.FindAction(config.nonmatchAction, true);
                advance = inputActions.FindAction(config.advanceAction, true);
                pause = inputActions.FindAction(config.pauseAction, true);
                calibrate = inputActions.FindAction("Calibrate", true);
                if (match.type != InputActionType.Button || nonmatch.type != InputActionType.Button || advance.type != InputActionType.Button || pause.type != InputActionType.Button)
                    throw new ArgumentException("Responses and navigation must use button actions.");
                if (match == calibrate || nonmatch == calibrate || advance == calibrate || pause == calibrate)
                    throw new ArgumentException("Calibration action cannot be assigned as a response or navigation action.");
                inputActions.Enable();
                sphereMaterial = stimulus.material;
                BeginSession();
            }
            catch (Exception e) { Fail(e); }
        }
        public void BeginSession()
        {
            if (Engine != null && !terminalRecorded) { Engine.Abort(Time.realtimeSinceStartupAsDouble, Time.frameCount, "restart"); recorder.Complete("aborted_restart"); }
            recorder?.Dispose();
            var frozen=JsonUtility.FromJson<TaskConfig>(JsonUtility.ToJson(config));
            if(frozen.distractionSeed==0)frozen.distractionSeed=BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);
            Engine = new TaskEngine(frozen);
            recorder = new SessionRecorder(Path.Combine(Application.persistentDataPath, "TaskPlatformData"), Engine.Config, kind: Application.isEditor ? "editor-test" : "participant");
            DataPath = recorder.DirectoryPath; Debug.Log("Task platform session: " + DataPath);
            Engine.OnEvent += recorder.Event; Engine.OnTrial += recorder.Trial;
            onsetCue?.Bind(Engine,config);
            distractions?.Bind(Engine,Engine.Config,DataPath);
            Engine.Note("session_start", "configuration frozen; generator=xorshift32-v1", Time.realtimeSinceStartupAsDouble, Time.frameCount);
            checkedMatch = checkedNonmatch = calibrated = terminalRecorded = false;
            lastPhase = (TaskPhase)(-1); lastTrial = -2; lastTime = Time.realtimeSinceStartupAsDouble;
            Recenter(false); Render(); presentation?.Refresh();
            StartCoroutine(PlaceAfterTrackingReady(Engine));
        }
        IEnumerator PlaceAfterTrackingReady(TaskEngine owner)
        {
            yield return null;yield return null;
            if(Engine==owner && Engine.Phase==TaskPhase.Preparation && !calibrated && !calibrationPending)
            {ConfigureTrackingSpace();PlaceTask(false);}
        }
        public void Recenter(bool confirm = true)
        {
            if (Engine != null && Engine.Phase != TaskPhase.Preparation) return;
            if(confirm)
            {
                if(!calibrationPending)StartCoroutine(CalibrateSeatedPose());
                return;
            }
            PlaceTask(false);
        }
        void ConfigureTrackingSpace()
        {
            floorTracking=false;
            var inputs=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(inputs);
            foreach(var input in inputs)if(input.running)
            {
                if((input.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor)!=0)
                    floorTracking=input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                if(!floorTracking)input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
                break;
            }
            var position=head.parent.position;
            position.y=TrackingRigHeight(floorTracking,head.localPosition.y);head.parent.position=position;
        }
        public static float TrackingRigHeight(bool floorMode,float localEyeHeight)=>floorMode ? 0 : 1.2f-localEyeHeight;
        IEnumerator CalibrateSeatedPose()
        {
            var owner=Engine;
            calibrationPending=true;ConfigureTrackingSpace();
            // Let a requested tracking-origin change reach the tracked pose before placement.
            yield return null;yield return null;
            if(Engine==owner && Engine!=null && Engine.Phase==TaskPhase.Preparation)
            {
                var inputs=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(inputs);
                foreach(var input in inputs)if(input.running){floorTracking=input.GetTrackingOriginMode()==TrackingOriginModeFlags.Floor;break;}
                var position=head.parent.position;position.y=TrackingRigHeight(floorTracking,head.localPosition.y);head.parent.position=position;
                Engine.Note("tracking_calibration","origin="+(floorTracking ? "floor" : "seated_device_fallback")+";local_eye_y="+head.localPosition.y+";rig_y="+head.parent.position.y,Time.realtimeSinceStartupAsDouble,Time.frameCount);
                PlaceTask(true);
            }
            calibrationPending=false;
        }
        void PlaceTask(bool confirm)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            taskAnchor.position = head.position + forward * config.distance;
            taskAnchor.rotation = Quaternion.LookRotation(forward, Vector3.up);
            float diameter = 2 * config.distance * Mathf.Tan(config.visualAngle * Mathf.Deg2Rad * .5f);
            stimulus.transform.localScale = Vector3.one * diameter;
            spatialView?.PlaceSupports(taskAnchor);
            calibrated = confirm;
            Engine?.Note("calibration", "head=" + head.position + ";forward=" + forward + ";diameter_m=" + diameter, Time.realtimeSinceStartupAsDouble, Time.frameCount);
            lastPhase = (TaskPhase)(-1);
        }
        void Update()
        {
            if (Engine == null || setupError != null) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                double delta = now - lastTime; lastTime = now;
                float refresh = 0; SubsystemManager.GetSubsystems(displays);
                foreach (var display in displays) if (display.running) display.TryGetDisplayRefreshRate(out refresh);
                double target = refresh > 0 ? 1.0 / refresh : 1.0 / 72;
                if (delta > target * 1.5 && Engine.Phase == TaskPhase.Running) Engine.Note("frame_gap", "seconds=" + delta.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";target_hz=" + (refresh > 0 ? refresh : 72), now, Time.frameCount);
                // Never silently catch up multiple trials after a stalled player loop.
                if (delta > .25 && Engine.Phase == TaskPhase.Running) Engine.Pause(now, Time.frameCount, "frame_stall", now - delta);
                int previousTrial = Engine.TrialIndex, previousBlock = Engine.Block;
                TaskPhase previousPhase = Engine.Phase;
                bool ma = match.WasPressedThisFrame(), nm = nonmatch.WasPressedThisFrame();
                if (!match.IsPressed() && !nonmatch.IsPressed()) simultaneousLatch = false;
                if (ma && nm) simultaneousLatch = true;
                Engine.Tick(now, Time.frameCount);
                if (pause.WasPressedThisFrame())
                {
                    if (Engine.Phase == TaskPhase.Paused) Engine.Resume(now, Time.frameCount); else Engine.Pause(now, Time.frameCount);
                    ma = nm = false;
                }
                if (calibrate.WasPressedThisFrame())
                {
                    if (Engine.Phase == TaskPhase.Paused) BeginSession(); else Recenter();
                }
                if (Engine.Phase == TaskPhase.Preparation)
                {
                    if (ma) { checkedMatch = true; Engine.Note("input_check", "match", now, Time.frameCount); }
                    if (nm) { checkedNonmatch = true; Engine.Note("input_check", "nonmatch", now, Time.frameCount); }
                    if (ma || nm) lastPhase = (TaskPhase)(-1);
                }
                else if (ma || nm)
                {
                    if (simultaneousLatch) Engine.Note("button_ignored", "simultaneous", now, Time.frameCount);
                    else if (previousTrial != Engine.TrialIndex || previousBlock != Engine.Block || previousPhase != Engine.Phase) Engine.Note("button_ignored", "boundary_frame", now, Time.frameCount);
                    else Engine.Respond(ma, now, Time.frameCount);
                }
                if (advance.WasPressedThisFrame())
                {
                    if (Engine.Phase == TaskPhase.Preparation) { if (calibrated && checkedMatch && checkedNonmatch) Engine.Advance(now, Time.frameCount); }
                    else if (Engine.Phase == TaskPhase.Complete || Engine.Phase == TaskPhase.Aborted) BeginSession();
                    else Engine.Advance(now, Time.frameCount);
                }
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && Engine.Phase == TaskPhase.Paused) BeginSession();
                distractions?.Tick(now);
                if ((Engine.Phase == TaskPhase.Complete || Engine.Phase == TaskPhase.Aborted) && !terminalRecorded)
                {
                    recorder.Complete(Engine.Phase == TaskPhase.Complete ? "complete" : "aborted"); terminalRecorded = true;
                }
                Render(); presentation?.Refresh();
            }
            catch (Exception e) { Fail(e); }
        }
        void Render()
        {
            bool running = Engine.Phase == TaskPhase.Running;
            stimulus.enabled = running && Engine.StimulusVisible;
            if (Engine.Current != null && running) sphereMaterial.color = Colors[Engine.Current.color];
            if (presentation != null) { presentation.Refresh(); spatialView?.Refresh(this,Time.realtimeSinceStartupAsDouble); return; }
            if (lastPhase == Engine.Phase && lastTrial == Engine.TrialIndex && Engine.Phase != TaskPhase.Rest && !(Engine.Practice && running)) return;
            lastPhase = Engine.Phase; lastTrial = Engine.TrialIndex;
            badge.text = "EVIDENCE-AWARE VR  /  TASK PLATFORM 01";
            string rule = Engine.Mode == TaskMode.Easy ? "Match only " + ColorNames[config.targetColor] + ".\nAll other colors are non-matches." : "Match the color from TWO positions ago.\nKeep updating your color memory.";
            footer.text = Key(config.matchAction) + "  MATCH       " + Key(config.nonmatchAction) + "  NON-MATCH\n" + Key(config.pauseAction) + "  Pause";
            switch (Engine.Phase)
            {
                case TaskPhase.Preparation:
                    heading.text = "N-BACK  /  PREPARATION";
                    body.text = "Sit comfortably and look straight ahead.\n\n" + (calibrated ? "[OK] " : "[   ] ") + "Left trigger / C: place task at eye level\n" + (checkedMatch ? "[OK] " : "[   ] ") + "Press " + Key(config.matchAction) + ": match\n" + (checkedNonmatch ? "[OK] " : "[   ] ") + "Press " + Key(config.nonmatchAction) + ": non-match\n\nThen {continue} to continue.";
                    footer.text = "Seated task  /  No movement required"; break;
                case TaskPhase.Instructions:
                    heading.text = (Engine.Practice ? "PRACTICE" : "FORMAL BLOCK " + (Engine.Block - 1)) + "  /  " + (Engine.Mode == TaskMode.Easy ? "0-BACK" : "2-BACK");
                    body.text = rule + "\n\nFirst " + config.warmupTrials + " colors: observe and remember.\nNo answers needed during warm-up.\n\n" + (Engine.Practice ? "Practice includes answer feedback." : "No feedback during the formal block.") + "\n\n{continue} to begin."; break;
                case TaskPhase.Running:
                    heading.text = (Engine.Practice ? "PRACTICE  /  " : "") + (Engine.Mode == TaskMode.Easy ? "0-BACK  /  " + ColorNames[config.targetColor] : "2-BACK") + (Engine.Current.scored ? "" : "  /  WARM-UP");
                    badge.text = ""; body.text = "";
                    if (Engine.Practice && Engine.Current.scored && Engine.CurrentResult != null && (Engine.CurrentResult.responded || Engine.CurrentResult.timeout))
                        body.text = "\n\n\n\n" + (Engine.CurrentResult.timeout ? "No response" : Engine.CurrentResult.correct ? "Correct" : "Incorrect") + "  /  Answer: " + (Engine.Current.expectedMatch ? "MATCH" : "NON-MATCH");
                    break;
                case TaskPhase.Rest:
                    heading.text = "REST";
                    var next = config.hardFirst ? "0-BACK: match " + ColorNames[config.targetColor] : "2-BACK: match TWO positions ago";
                    body.text = "Relax your hands and remain seated.\n\nNext: " + next + "\n\n" + (Engine.RestRemaining(Time.realtimeSinceStartupAsDouble) > 0 ? Math.Ceiling(Engine.RestRemaining(Time.realtimeSinceStartupAsDouble)) + " seconds remaining" : "{continue} to review the next rule."); break;
                case TaskPhase.Paused:
                    heading.text = "PAUSED"; body.text = "{pause} to resume.\n\nThe current trial is marked as interrupted.\n\nLeft trigger / C / R: restart as a new session."; break;
                case TaskPhase.Complete:
                    heading.text = "SESSION COMPLETE"; body.text = "Thank you. The task is finished.\n\nYour responses have been saved.\nYou may now remove the headset.\n\n{continue} starts a new session."; footer.text = "Please wait for the experimenter."; break;
                case TaskPhase.Aborted:
                    heading.text = "SESSION STOPPED"; body.text = "The incomplete session has been preserved.\n{continue} starts a new session."; break;
            }
            body.text = body.text.Replace("{continue}", Key(config.advanceAction)).Replace("{pause}", Key(config.pauseAction));
        }
        static string Key(string name) => name == "RightPrimary" ? "A" : name == "RightSecondary" ? "B" : name == "LeftPrimary" ? "X / Enter" : name == "LeftSecondary" ? "Y / P" : name;
        void Fail(Exception e)
        {
            setupError = e.Message; if (stimulus != null) stimulus.enabled = false;
            distractions?.Cancel("platform_error");
            spatialView?.ShowError();
            heading.text = "PLATFORM ERROR"; body.text = "Please ask the experimenter for assistance.\n\n" + e.Message; Debug.LogException(e);
            recorder?.Dispose();
        }
        void OnApplicationPause(bool paused) { if (paused && Engine != null) Engine.Pause(Time.realtimeSinceStartupAsDouble, Time.frameCount, "application_pause"); }
        void OnApplicationFocus(bool focused) { if (!focused && !Application.isEditor && Engine != null) Engine.Pause(Time.realtimeSinceStartupAsDouble, Time.frameCount, "focus_lost"); }
        void OnDestroy()
        {
            if (Engine != null && !terminalRecorded && setupError == null)
            {
                try { Engine.Abort(Time.realtimeSinceStartupAsDouble, Time.frameCount, "application_exit"); recorder?.Complete("aborted_exit"); }
                catch (Exception e) { Debug.LogException(e); }
            }
            recorder?.Dispose(); inputActions?.Disable();
            if (sphereMaterial != null) Destroy(sphereMaterial);
        }
    }
}

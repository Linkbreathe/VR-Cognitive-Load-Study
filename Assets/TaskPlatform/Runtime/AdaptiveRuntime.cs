using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.XR;

namespace EvidenceVR
{
    public sealed partial class AdaptiveRuntime : MonoBehaviour
    {
        [Serializable] sealed class Packet
        {
            public string sessionId,configVersion,sessionType,participantCode,requestId,stage,mediaId;
            public int block,level,trial;
            public double now,clockOffset,clockUncertainty;
            public double gazeValidFraction,gazeTaskFraction,gazeVideoFraction;
            public AdaptiveTrainingConfig config;
            public TrainingBlock[] blocks;
            public RollingEvaluation[] evaluations;
            public int evaluationId, epoch, lastTrial;
            public TaskEvent[] events;
            public TrialResult[] trials;
            public int completedTrials;
            public double stageStarted;
            public bool supplementUsed;
        }
        [Serializable] sealed class Reply
        {
            public double serverClock;
            public AdaptiveDecision decision;
            public string calibrationJson,featuresJson;
            public PhysiologyDisplayStatus physiologyStatus;
            public string modelStatus,modelReportJson;
            public bool modelAdmitted;
            public int modelAttempt;
        }
        TaskPlatformController owner;
        TaskEngine engine;
        AdaptiveTrainingConfig config;
        public AdaptiveMedia Media {get;private set;}
        public GazeDebugHighlighter GazeDebug {get;private set;}
        public EngineeringGazeRay GazeRay {get;private set;}
        public string GazeTrackingStatus {get;private set;}="WAITING";
        string lastGazeStatus;
        Renderer focusTray;
        Bounds lastFocusBounds;
        bool focusBoundsRecorded;
        double nextGazeStatusLog;
        public bool CanStart=>config.sessionType!="participant" || (Media!=null && Media.Ready &&
            (!config.IsPersonalized || (ServiceConnected && PhysiologyStatus?.source=="live" && PhysiologyStatus?.connection=="receiving")));
        public PhysiologyDisplayStatus PhysiologyStatus {get;private set;}
        double lastServiceReply=double.NegativeInfinity;
        public bool ServiceConnected=>Time.realtimeSinceStartupAsDouble-lastServiceReply<=3;
        readonly Dictionary<string,StreamWriter> writers=new Dictionary<string,StreamWriter>();
        readonly List<TaskEvent> events=new List<TaskEvent>();
        readonly List<UnityEngine.XR.InputDevice> eyeDevices=new List<UnityEngine.XR.InputDevice>();
        string sessionId;
        bool disposed,busy;
        const string QuestEyePermission="com.oculus.permission.EYE_TRACKING";
        public string GazePermissionStatus {get;private set;}="not_initialized";
#if UNITY_ANDROID && !UNITY_EDITOR
        UnityEngine.Android.PermissionCallbacks gazePermissionCallbacks;
#endif
        double nextTelemetry,nextGaze,offset,uncertainty=999;
        int gazeSamples,gazeValid,gazeTask,gazeVideo;
        int rating=3,ratingIndex;
        RectTransform countdown;
        Vector2 originalCountdownPosition;
        public void Bind(TaskPlatformController controller,AdaptiveTrainingConfig configuration)
        {
            owner=controller;engine=controller.Engine;config=configuration;sessionId=Path.GetFileName(owner.DataPath);
            focusTray=owner.spatialView?.bench?.Find("StimulusPlinth")?.GetComponent<Renderer>();
            if(focusTray==null)Debug.LogWarning("Task gaze region: tray unavailable; using padded sphere bounds only.");
            countdown=(RectTransform)owner.spatialView.timer.transform.parent;originalCountdownPosition=countdown.anchoredPosition;
            countdown.anchoredPosition=new Vector2(-220,originalCountdownPosition.y);
            File.WriteAllText(Path.Combine(owner.DataPath,"adaptive-training-config.json"),JsonUtility.ToJson(config,true));
            File.WriteAllText(Path.Combine(owner.DataPath,"calibration.json"),"{\"status\":\"not_calibrated\",\"physiological_features\":[],\"control_basis\":\""+config.controlBasis+"\"}");
            Open("training_blocks.csv","session_id,config_version,block,stage,level,start_s,end_s,eligible,matches,nonmatches,balanced_accuracy,median_correct_rt_s,omissions,transition_trials,valid,mixed_level,levels");
            Open("media_events.csv","session_id,config_version,block,stage,level,media_id,modality,status,planned_s,actual_s,duration_s,prior_exposures,x,y,z,detail");
            Open("gaze_trials.csv","session_id,config_version,block,trial,level,samples,valid_samples,task_samples,video_samples,valid_fraction,task_fraction_valid,video_fraction_valid");
            Open("gaze_samples.csv","session_id,config_version,time_s,block,trial,level,valid,origin_x,origin_y,origin_z,direction_x,direction_y,direction_z,task_roi,video_roi,task_visible,video_visible,roi_revision,gaze_source,task_region_visible");
            Open("physio_features.csv","session_id,config_version,received_s,payload_json");
            Open("controller_decisions.jsonl",null);Open("clock_sync.jsonl",null);
            Open("rolling_evaluations.jsonl",null);
            Open("subjective_ratings.csv","session_id,config_version,time_s,item,rating_1_to_7");
            var mediaObject=new GameObject("Adaptive media runtime");mediaObject.transform.SetParent(transform,false);
            Media=mediaObject.AddComponent<AdaptiveMedia>();Media.OnMedia+=RecordMedia;Media.Bind(owner,config);
            var debugObject=new GameObject("Engineering gaze debug");debugObject.transform.SetParent(transform,false);
            GazeDebug=debugObject.AddComponent<GazeDebugHighlighter>();GazeDebug.Bind(owner,config);GazeDebug.OnStateChanged+=OnGazeVisualChanged;
            GazeRay=debugObject.AddComponent<EngineeringGazeRay>();GazeRay.Bind(config);
            engine.Note("gaze_ray_configuration",EngineeringGazeRay.Revision+";requested="+config.engineeringGazeRay+";effective="+GazeRay.RayEnabled,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            if(Application.isEditor)engine.Note("gaze_link_compatibility",QuestLinkEyeSpaceFeature.Revision+";"+QuestLinkEyeSpaceFeature.LastResult,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            engine.CanApplyAdaptiveChange=()=>!Media.IsPlaying;
            engine.OnEvent+=OnEvent;engine.OnTrial+=OnTrial;engine.OnTrainingBlock+=OnBlock;
            engine.Note("protocol_configuration",config.protocol+";version="+config.version,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            if(config.IsPersonalized)
            {
                engine.Note("personal_presentation_policy","feedback=practice_only;questionnaires="+TaskEngine.PersonalQuestionnaireAdministration+";mount_movement=restart_required",Time.realtimeSinceStartupAsDouble,Time.frameCount);
                engine.Note("personal_flow_policy",TaskEngine.PersonalFlowRevision+";resting_baseline_seconds=0;preparation_checks=required",Time.realtimeSinceStartupAsDouble,Time.frameCount);
                Debug.Log("Personalized flow: preparation and signal checks -> practice -> calibration; no resting baseline. Revision: "+TaskEngine.PersonalFlowRevision);
            }
            engine.Note("gaze_debug_configuration",GazeDebugHighlighter.Revision+";requested="+config.engineeringGazeHighlight+";effective="+GazeDebug.Allowed+";roi="+GazeRegions.Revision+";source=tracked_eyes_only",Time.realtimeSinceStartupAsDouble,Time.frameCount);
            InitializeGazePermission();
            Debug.Log("Stage 2 initialized: "+config.evaluationPolicy+"; countdown F2; mode="+config.mode+"; basis="+config.controlBasis+"; data="+owner.DataPath);
        }
        void InitializeGazePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(UnityEngine.Android.Permission.HasUserAuthorizedPermission(QuestEyePermission)){RecordGazePermission("granted");return;}
            gazePermissionCallbacks=new UnityEngine.Android.PermissionCallbacks();
            gazePermissionCallbacks.PermissionGranted+=OnGazePermissionGranted;
            gazePermissionCallbacks.PermissionDenied+=OnGazePermissionDenied;
            RecordGazePermission("requested");
            // Request once during session initialization, before any task trials begin.
            UnityEngine.Android.Permission.RequestUserPermission(QuestEyePermission,gazePermissionCallbacks);
#else
            RecordGazePermission("desktop_runtime_managed");
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        void OnGazePermissionGranted(string permission){if(permission==QuestEyePermission)RecordGazePermission("granted");}
        void OnGazePermissionDenied(string permission){if(permission==QuestEyePermission)RecordGazePermission("denied");}
#endif
        void RecordGazePermission(string status)
        {
            if(disposed)return;
            GazePermissionStatus=status;
            engine.Note("gaze_permission","quest-eye-permission-v1;status="+status,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            Debug.Log("Stage 2 eye permission: "+status+". Tracked eyes are still required; no head-direction fallback.");
        }
        void OnGazeVisualChanged(string target)=>engine.Note("gaze_debug_visual",GazeDebugHighlighter.Revision+";target="+target,Time.realtimeSinceStartupAsDouble,Time.frameCount);
        void Open(string file,string header){var writer=new StreamWriter(Path.Combine(owner.DataPath,file),false,new UTF8Encoding(false)){AutoFlush=true};writers[file]=writer;if(header!=null)writer.WriteLine(header);}
        static string Cell(object value)=>"\""+Convert.ToString(value,CultureInfo.InvariantCulture).Replace("\"","\"\"")+"\"";
        void Row(string file,params object[] values){if(!disposed)writers[file].WriteLine(string.Join(",",values.Select(Cell)));}
        void Json(string file,object value){if(!disposed)writers[file].WriteLine(JsonUtility.ToJson(value));}
        void RecordMedia(AdaptiveExposure e,string status,double now,string detail)
        {
            var pos=Media.Screen.position;
            Row("media_events.csv",sessionId,config.version,e.block,e.stage,e.level,e.mediaId,e.modality,status,Media.PlannedOnset(e),now,e.duration,e.previousExposures,pos.x,pos.y,pos.z,detail);
            engine.Note("adaptive_media_"+status,JsonUtility.ToJson(e)+";"+detail,now,Time.frameCount);
        }
        void OnEvent(TaskEvent e)
        {
            if(disposed)return;
            if(e.type=="pause" || e.type=="session_complete" || e.type=="session_aborted" || e.type=="block_complete"){GazeDebug?.Clear();GazeRay?.Clear();}
            if(events.Count<2048)events.Add(e);
            else {events.Clear();engine.Pause(e.actual,e.frame,"telemetry_queue_full");}
            if(e.type=="personal_decision_due" && config.mode!="OFF")StartCoroutine(Send(true));
            if(e.type.StartsWith("personal_execution"))Json("controller_decisions.jsonl",e);
            if(e.type=="rolling_evaluation")
            {
                Json("rolling_evaluations.jsonl",engine.LatestRolling);
                if(config.mode!="OFF")StartCoroutine(Send(true));
            }
            if(e.type=="adaptive_execution" || e.type=="adaptive_rolling_execution" || e.type=="rolling_execution_deferred" || e.type=="rolling_decision_rejected")Json("controller_decisions.jsonl",e);
        }
        void OnTrial(TrialResult t)
        {
            Row("gaze_trials.csv",sessionId,config.version,t.block,t.trial,t.level,gazeSamples,gazeValid,gazeTask,gazeVideo,
                gazeSamples>0?(double)gazeValid/gazeSamples:0,gazeValid>0?(double)gazeTask/gazeValid:-1,gazeValid>0?(double)gazeVideo/gazeValid:-1);
            gazeSamples=gazeValid=gazeTask=gazeVideo=0;
        }
        void OnBlock(TrainingBlock b)
        {
            b.sessionId=sessionId;b.configVersion=config.version;
            if(Media.BlockFailed)b.valid=false;
            Row("training_blocks.csv",sessionId,config.version,b.block,b.stage,b.level,b.start,b.end,b.eligible,b.matches,b.nonmatches,b.balancedAccuracy,b.medianCorrectRt,b.omissions,b.transitionTrials,b.valid,b.mixedLevel,string.Join("|",b.levels??new int[0]));
        }
        Packet Snapshot(bool decision)
        {
            var blocks=new List<TrainingBlock>(engine.Adaptive.Blocks);
            var evaluation=engine.LatestRolling;
            var packet=new Packet{sessionId=sessionId,configVersion=config.version,sessionType=Application.isEditor?"editor-test":config.sessionType,
                participantCode=config.participantCode,requestId=sessionId+":rolling:"+(evaluation?.evaluationId??0),stage=engine.Practice?"practice":engine.Adaptive.Stage,
                mediaId=Media.ActiveId,block=engine.Block,level=engine.Practice?2:engine.Adaptive.Level,trial=engine.TrialIndex,now=Time.realtimeSinceStartupAsDouble,
                clockOffset=offset,clockUncertainty=uncertainty,config=config,blocks=blocks.ToArray(),events=events.ToArray(),
                evaluations=engine.RollingHistory.ToArray(),evaluationId=evaluation?.evaluationId??0,epoch=engine.RollingEpoch,lastTrial=evaluation?.lastTrial??-1,
                gazeValidFraction=gazeSamples>0?(double)gazeValid/gazeSamples:0,gazeTaskFraction=gazeValid>0?(double)gazeTask/gazeValid:-1,gazeVideoFraction=gazeValid>0?(double)gazeVideo/gazeValid:-1};
            if(config.IsPersonalized)
            {
                packet.trials=engine.Results.ToArray();packet.completedTrials=engine.ProtocolCompletedTrials;
                packet.stageStarted=engine.ProtocolStageStarted;packet.supplementUsed=engine.SupplementUsed;
                packet.stage=engine.Adaptive.Stage;packet.level=engine.Practice?1:engine.Adaptive.Level;
                packet.evaluationId=engine.PersonalDecisionId;packet.lastTrial=engine.ProtocolCompletedTrials;
                packet.requestId=sessionId+":"+packet.stage+":"+packet.evaluationId;
            }
            return packet;
        }
        IEnumerator Send(bool decision)
        {
            if(disposed || (!decision && busy))yield break;
            if(!decision)busy=true;
            var packet=Snapshot(decision);var sentEvents=packet.events;
            string json=JsonUtility.ToJson(packet);
            using(var request=new UnityWebRequest(config.controllerUrl.TrimEnd('/')+(decision?"/decide":"/telemetry"),"POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.downloadHandler=new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type","application/json");request.timeout=2;
                if(decision)Json("controller_decisions.jsonl",packet);
                yield return request.SendWebRequest();
                if(disposed)yield break;
                double received=Time.realtimeSinceStartupAsDouble;
                if(request.result==UnityWebRequest.Result.Success)
                {
                    var reply=JsonUtility.FromJson<Reply>(request.downloadHandler.text);
                    lastServiceReply=received;PhysiologyStatus=reply.physiologyStatus;
                    if(config.IsPersonalized)
                    {
                        if(!string.IsNullOrEmpty(reply.modelReportJson))File.WriteAllText(Path.Combine(owner.DataPath,"personal-model-report.json"),reply.modelReportJson);
                        engine.ReceivePersonalModel(reply.modelStatus,reply.modelAdmitted,reply.modelAttempt,received,Time.frameCount);
                    }
                    uncertainty=(received-packet.now)*.5;offset=reply.serverClock-(received+packet.now)*.5;
                    writers["clock_sync.jsonl"].WriteLine("{\"unity_sent\":"+packet.now.ToString("R",CultureInfo.InvariantCulture)+",\"unity_received\":"+received.ToString("R",CultureInfo.InvariantCulture)+",\"server_clock\":"+reply.serverClock.ToString("R",CultureInfo.InvariantCulture)+"}");
                    foreach(var item in sentEvents)events.Remove(item);
                    if(!string.IsNullOrEmpty(reply.featuresJson))Row("physio_features.csv",sessionId,config.version,received,reply.featuresJson);
                    if(!string.IsNullOrEmpty(reply.calibrationJson))File.WriteAllText(Path.Combine(owner.DataPath,"calibration.json"),reply.calibrationJson);
                    if(decision && reply.decision!=null)
                    {
                        var d=reply.decision;Json("controller_decisions.jsonl",d);
                        bool accept=engine.Phase==TaskPhase.Running && !engine.Practice && engine.Adaptive.Stage=="training" && engine.TrialIndex==packet.trial &&
                            received-packet.now<.5 && !Media.BlockFailed && d.evaluationId==engine.LatestRolling?.evaluationId && d.epoch==engine.RollingEpoch && d.lastTrial==packet.lastTrial &&
                            engine.Block==d.block && d.sessionId==sessionId && d.requestId==packet.requestId &&
                            d.configVersion==config.version && d.level==engine.Adaptive.Level && new[]{"UP","HOLD","DOWN","REST"}.Contains(d.action) &&
                            (config.controlBasis!="physiological"||d.action!="UP"||d.physiologicalEligible);
                        if(config.IsPersonalized)
                            accept=engine.Phase==TaskPhase.Running && (engine.Adaptive.Stage=="random" || engine.Adaptive.Stage=="bioadaptive") &&
                                engine.Adaptive.Stage==packet.stage && received-packet.now<4.5 && !Media.BlockFailed &&
                                d.evaluationId==engine.PersonalDecisionId && d.epoch==engine.RollingEpoch && d.lastTrial==packet.lastTrial &&
                                d.sessionId==sessionId && d.requestId==packet.requestId && d.configVersion==config.version && d.level==engine.Adaptive.Level &&
                                new[]{"ON","OFF","HOLD","REST"}.Contains(d.action) &&
                                (engine.Adaptive.Stage=="random" || d.action=="HOLD" || d.action=="REST" ||
                                 (d.physiologicalEligible && engine.PersonalModelAdmitted) ||
                                 (d.basis=="behavioral_fallback" && engine.SupplementUsed && !engine.PersonalModelAdmitted));
                        if(accept && config.mode=="ACTIVE")engine.Adaptive.Pending=d;
                        engine.Note("controller_reply",accept?(config.mode=="SHADOW"?"shadow_not_executed":"accepted_for_boundary"):"rejected_stale_or_invalid",received,Time.frameCount);
                    }
                }
                else if(decision)engine.Note("controller_unavailable","rolling_fallback=HOLD;"+request.error,received,Time.frameCount);
            }
            if(!decision)busy=false;
        }
        public bool RequestLevel(int level)
        {
            if(config.IsPersonalized)return false;
            if(disposed||level<0||level>2||engine.Adaptive.Stage!="training"||config.mode=="ACTIVE")return false;
            engine.Adaptive.ManualLevel=level;engine.Note("manual_level_request","next_boundary="+level,Time.realtimeSinceStartupAsDouble,Time.frameCount);return true;
        }
        public void StopNow()=>engine.Abort(Time.realtimeSinceStartupAsDouble,Time.frameCount,"participant_stop");
        public void AdjustRating(int direction){if(!config.IsPersonalized && ratingIndex<2)rating=Mathf.Clamp(rating+direction,1,7);}
        public bool RecordRating()
        {
            if(config.IsPersonalized)return false;
            if(ratingIndex>=2)return false;
            Row("subjective_ratings.csv",sessionId,config.version,Time.realtimeSinceStartupAsDouble,ratingIndex==0?"perceived_distraction":"mental_workload",rating);
            ratingIndex++;rating=3;return true;
        }
        public void Tick(double now)
        {
            if(disposed)return;
            Media.Tick(now);
            if(Keyboard.current!=null)
            {
                if(Keyboard.current.digit0Key.wasPressedThisFrame)RequestLevel(0);
                if(Keyboard.current.digit1Key.wasPressedThisFrame)RequestLevel(1);
                if(Keyboard.current.digit2Key.wasPressedThisFrame)RequestLevel(2);
                if(Keyboard.current.escapeKey.wasPressedThisFrame)StopNow();
            }
            if(now>=nextGaze && (engine.Phase==TaskPhase.Running || EngineeringGazeRay.IsAllowed(config))){nextGaze=now+1.0/30;SampleGaze(now);}
            else if(engine.Phase!=TaskPhase.Running)GazeDebug?.Clear();
            if(now>=nextTelemetry){nextTelemetry=now+1;StartCoroutine(Send(false));}
        }
        string lastGazeCompatibilityState;
        void SampleGaze(double now)
        {
            if(Application.isEditor && lastGazeCompatibilityState!=QuestLinkEyeSpaceFeature.LastResult)
            {
                lastGazeCompatibilityState=QuestLinkEyeSpaceFeature.LastResult;
                engine.Note("gaze_link_compatibility",QuestLinkEyeSpaceFeature.Revision+";"+lastGazeCompatibilityState,now,Time.frameCount);
            }
            eyeDevices.Clear();UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking,eyeDevices);
            GazeTrackingStatus=string.IsNullOrEmpty(UnityEngine.XR.OpenXR.OpenXRRuntime.name)?"NO XR RUNTIME":eyeDevices.Count==0?"NO EYE DEVICE":"EYES NOT TRACKED";
#if UNITY_ANDROID && !UNITY_EDITOR
            // Recheck authorization so revocation cannot leave an accepted gaze sample.
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(QuestEyePermission)){eyeDevices.Clear();GazeTrackingStatus="EYE PERMISSION MISSING";}
#endif
            Vector3 position=Vector3.zero;Quaternion rotation=Quaternion.identity;bool valid=false;
            foreach(var device in eyeDevices)
                if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool tracked)&&tracked &&
                    device.TryGetFeatureValue(new InputFeatureUsage<Vector3>("gazePosition"),out position)&&
                    device.TryGetFeatureValue(new InputFeatureUsage<Quaternion>("gazeRotation"),out rotation) && GazeRegions.ValidRotation(rotation)){valid=true;break;}
            var origin=valid?owner.head.parent.TransformPoint(position):Vector3.zero;
            var direction=valid?owner.head.parent.TransformDirection(rotation*Vector3.forward):Vector3.zero;
            bool sphereVisible=engine.StimulusVisible && owner.stimulus.enabled && owner.stimulus.gameObject.activeInHierarchy;
            bool trayVisible=focusTray!=null && focusTray.enabled && focusTray.gameObject.activeInHierarchy;
            bool taskVisible=engine.Phase==TaskPhase.Running && (sphereVisible || trayVisible),videoVisible=Media.IsVideoVisible;
            var sphereBounds=owner.stimulus.bounds;
            var taskBounds=GazeRegions.TaskBounds(sphereBounds,trayVisible?focusTray.bounds:sphereBounds);
            if(!focusBoundsRecorded || lastFocusBounds!=taskBounds)
            {
                focusBoundsRecorded=true;lastFocusBounds=taskBounds;
                string detail=GazeRegions.Revision+";padding_m="+GazeRegions.TaskPaddingMeters.ToString(CultureInfo.InvariantCulture)+";tray_available="+trayVisible+";world_center_m="+taskBounds.center.ToString("F6",CultureInfo.InvariantCulture)+";world_size_m="+taskBounds.size.ToString("F6",CultureInfo.InvariantCulture);
                engine.Note("gaze_task_region",detail,now,Time.frameCount);Debug.Log("Task gaze region: "+detail);
            }
            var sample=GazeRegions.Classify(valid,origin,direction,taskBounds,taskVisible,Media.Screen,videoVisible);
            if(sample.valid)GazeTrackingStatus="TRACKED EYES";
            else if(valid)GazeTrackingStatus="INVALID EYE POSE";
            bool rayPhase=engine.Phase==TaskPhase.Preparation || engine.Phase==TaskPhase.Instructions || engine.Phase==TaskPhase.Running;
            GazeRay?.Present(sample,GazeTrackingStatus,now,rayPhase);
            if(now>=nextGazeStatusLog && lastGazeStatus!=GazeTrackingStatus)
            {
                nextGazeStatusLog=now+1;lastGazeStatus=GazeTrackingStatus;
                string detail=GazeTrackingStatus+";runtime="+UnityEngine.XR.OpenXR.OpenXRRuntime.name+";eye_devices="+eyeDevices.Count;
                engine.Note("gaze_tracking_status",detail,now,Time.frameCount);
                Debug.Log("Eye gaze: "+detail);
            }
            // Preparation diagnostics never enter trial statistics or production gaze rows.
            if(engine.Phase!=TaskPhase.Running){GazeDebug?.Clear();return;}
            gazeSamples++;if(sample.valid)gazeValid++;if(sample.task)gazeTask++;if(sample.video)gazeVideo++;
            Row("gaze_samples.csv",sessionId,config.version,now,engine.Block,engine.TrialIndex,engine.Practice?(config.IsPersonalized?1:2):engine.Adaptive.Level,sample.valid,
                sample.origin.x,sample.origin.y,sample.origin.z,sample.direction.x,sample.direction.y,sample.direction.z,sample.task,sample.video,
                sphereVisible,videoVisible,GazeRegions.Revision,"tracked_eyes",taskVisible);
            GazeDebug?.Present(sample,now);
        }
        public void SetEngineeringGazeRay(bool enabled)
        {
            if(!EngineeringGazeRay.IsAllowed(config))throw new InvalidOperationException("Eye-ray debugging is restricted to engineering sessions.");
            GazeRay.SetEnabled(enabled);
            engine.Note("gaze_ray_toggle",EngineeringGazeRay.Revision+";enabled="+GazeRay.RayEnabled,Time.realtimeSinceStartupAsDouble,Time.frameCount);
            Debug.Log("Engineering eye ray: "+(GazeRay.RayEnabled?"ON":"OFF")+"; "+GazeTrackingStatus);
        }
        public void Render()
        {
            if(disposed)return;
            if(config.IsPersonalized){RenderPersonal();return;}
            var view=owner.spatialView;
            if(engine.Phase==TaskPhase.Running)
            {
                view.stage.text=(engine.Practice?"Practice":engine.Adaptive.Stage+" | Level "+engine.Adaptive.Level)+" | 2-back";
                view.rule.text=engine.Current.scored?"":"Observe only";
            }
            else if(engine.Phase==TaskPhase.Preparation)
            {
                owner.body.text=!CanStart?"Media preparation is incomplete. Ask the experimenter.":"Calibrate your seat and check A / B.\nKeep your attention on the sphere.";
                owner.presentation.actionHint.text=owner.Calibrated && owner.MatchChecked && owner.NonmatchChecked && CanStart?"X / ENTER  Begin 2-back practice":"Complete preparation checks";
            }
            else if(engine.Phase==TaskPhase.Instructions)
            {
                owner.badge.text="ADAPTIVE DISTRACTION TRAINING";
                owner.presentation.actionHint.text="X / ENTER  Start "+(engine.Practice?"practice":"baseline");
                owner.body.text="Compare with the color TWO positions earlier.\nA: same   |   B: different\nFirst two colors: observe only.";
                owner.presentation.exampleCaption.text=engine.Practice?"Practice includes random movie clips. Keep answering the sphere.":"The baseline starts without movie distractions.";
            }
            else if(engine.Phase==TaskPhase.Paused)
                owner.body.text="Y / P: resume with two observe-only colors.\nThe interrupted evaluation is excluded.\nX / ENTER: stop. C: restart session.";
            else if(engine.Phase==TaskPhase.Complete)
            {
                owner.badge.text="TRAINING FINISHED";
                owner.body.text=ratingIndex<2?(ratingIndex==0?"How distracting was the external content?":"How demanding was the task?")+"\n1 = very low, 7 = very high\nRating: "+rating+"\nA: decrease   B: increase":"Your responses have been saved.\nPlease wait for the experimenter.";
                owner.presentation.actionHint.text=ratingIndex<2?"X / ENTER  Save rating":"X / ENTER  New session";
            }
        }
        public void Shutdown()
        {
            if(disposed)return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if(gazePermissionCallbacks!=null)
            {
                gazePermissionCallbacks.PermissionGranted-=OnGazePermissionGranted;
                gazePermissionCallbacks.PermissionDenied-=OnGazePermissionDenied;
                gazePermissionCallbacks=null;
            }
#endif
            StopAllCoroutines();
            if(engine!=null){engine.CanApplyAdaptiveChange=null;engine.OnEvent-=OnEvent;engine.OnTrial-=OnTrial;engine.OnTrainingBlock-=OnBlock;}
            if(GazeDebug!=null){GazeDebug.Clear();GazeDebug.OnStateChanged-=OnGazeVisualChanged;Destroy(GazeDebug.gameObject);}
            if(Media!=null){Media.OnMedia-=RecordMedia;Destroy(Media.gameObject);}
            foreach(var writer in writers.Values)writer.Dispose();disposed=true;
            if(countdown!=null)countdown.anchoredPosition=originalCountdownPosition;
        }
        void OnDestroy()=>Shutdown();
    }
}

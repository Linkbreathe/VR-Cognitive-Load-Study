using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace EvidenceVR
{
    public sealed class SessionRecorder : IDisposable
    {
        [Serializable] sealed class Metadata
        {
            public string sessionId, sessionType, participantCode, utcStarted, applicationVersion, unityVersion, device, status, timingNote;
            public string presentationVersion = LabPresentation.Version;
            public string controlHintRevision=LabPresentation.ControlHintRevision;
            public string environmentRevision=LabEnvironmentIdentity.CurrentRevision;
            public string experimentMode = "FixedBlockExperiment";
            public string statusDisplayVersion = ExperimentStatusView.Revision;
            public string countdownDisplayVersion = SpatialTaskView.CountdownRevision;
            public string responseSelectionRevision="first-selection-v1",selectionFeedbackRevision="not_used",selectionCueRevision="not_used";
            public string stimulusPlacementRevision=SpatialTaskView.StimulusPlacementRevision;
            public string seatedHeightRevision=TaskPlatformController.SeatedHeightRevision;
            public float seatedEyeHeightMeters=TaskPlatformController.SeatedEyeHeightMeters;
            public float sphereTrayClearanceMeters=SpatialTaskView.SphereTrayClearanceMeters;
            public float stimulusDiameterScale=SpatialTaskView.StimulusDiameterScale;
            public float traySurfaceHeightMeters=SpatialTaskView.TraySurfaceHeight;
            public string gazeRoiRevision="not_used", gazeDebugRevision="not_used";
            public string gazeOverviewRevision="not_used";
            public bool gazeDebugRequested,gazeDebugEnabled;
            public string gazeRayRevision="not_used"; public string eyeFreezeRevision="not_used";
            public string eyeSampleFreshnessRevision="not_used";
            public bool gazeRayRequested,gazeRayInitiallyEnabled;
            public string gazeLinkCompatibilityRevision="not_used",gazeLinkCompatibilityInitialState="inactive";
            public string evidenceGuardMode="off",evidenceGuardRevision=EvidenceGuardContract.Revision;
            public string trialProgressRevision="not_used",personalCalibrationRevision="not_used";
            public string studyDesign="legacy",inputTimingRevision="legacy-frame-polled";
            public string comparisonEnvironment="not_used",gazeInputSource="not_used",comparisonEnvironmentRevision="not_used";
            public bool simulatedGaze;
            public TaskConfig config;
        }
        public string DirectoryPath { get; }
        readonly StreamWriter trials, practice, events, inputEvents;
        readonly Metadata metadata;
        bool disposed;
        static string F(double n) => n.ToString("R", CultureInfo.InvariantCulture);
        static string B(bool b) => b ? "1" : "0";
        public SessionRecorder(string root, TaskConfig config, string participant = "anonymous", string kind = "participant", string presentationVersion = null,
            bool gazeDebugRequested=false,bool gazeDebugEnabled=false,bool gazeRayRequested=false,bool gazeRayEnabled=false,string evidenceGuardMode="off",string assignedSessionId=null,string studyDesign="legacy",bool comparison=false,string comparisonEnvironment="quest_pro",bool revisableComparisonResponses=false)
        {
            string id = assignedSessionId ?? DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[A-Za-z0-9_-]{1,100}$"))throw new ArgumentException("Invalid assigned session ID.");
            DirectoryPath = Path.Combine(root, kind, id);
            if(Directory.Exists(DirectoryPath))throw new IOException("Session already exists; prepare a new operator session.");
            Directory.CreateDirectory(DirectoryPath);
            metadata = new Metadata { sessionId = id, sessionType = kind, participantCode = participant, utcStarted = DateTime.UtcNow.ToString("O"), applicationVersion = Application.version, unityVersion = Application.unityVersion, device = SystemInfo.deviceModel, status = "in_progress", config = config,
                timingNote = "Unity realtimeSinceStartupAsDouble in seconds; software events, not measured photon onset. RT from actual software onset includes pauses; interrupted trials are excluded from clean summaries. Warmups are unscored and do not require an answer. Frame gaps are diagnostic, not hardware dropped-frame counts." };
            if(presentationVersion!=null){metadata.presentationVersion=presentationVersion;metadata.experimentMode="AdaptiveDistractionTraining";}
            if(presentationVersion!=null){metadata.gazeRoiRevision=GazeRegions.Revision;metadata.gazeDebugRevision=GazeDebugHighlighter.Revision;metadata.gazeOverviewRevision=GazeConsoleProjection.Revision;}
            metadata.gazeDebugRequested=gazeDebugRequested;metadata.gazeDebugEnabled=gazeDebugEnabled;
            if(presentationVersion!=null){metadata.gazeRayRevision=EngineeringGazeRay.Revision;metadata.eyeFreezeRevision=EyeFreezeDetector.Revision;metadata.eyeSampleFreshnessRevision=EyeSampleFreshness.Revision;}
            metadata.gazeRayRequested=gazeRayRequested;metadata.gazeRayInitiallyEnabled=gazeRayEnabled;
            metadata.evidenceGuardMode=evidenceGuardMode;
            metadata.studyDesign=studyDesign;
            if(revisableComparisonResponses)
            {metadata.responseSelectionRevision=TaskEngine.ComparisonResponseRevision;metadata.selectionFeedbackRevision=SpatialTaskView.SelectionFeedbackRevision;metadata.selectionCueRevision=TrialOnsetCue.SelectionCueVersion;}
            if(comparison)
            {
                metadata.comparisonEnvironment=comparisonEnvironment;metadata.comparisonEnvironmentRevision="comparison-environments-v1";
                metadata.simulatedGaze=comparisonEnvironment=="desktop_fixed_gaze";
                metadata.gazeInputSource=metadata.simulatedGaze?"simulated_fixed_gaze":"quest_pro_tracked_eyes";
            }
            if(studyDesign==LayerABStudy.Revision || studyDesign=="layer-ab-probe-v2")
            {
                metadata.inputTimingRevision=InputTimingAudit.Revision;metadata.evidenceGuardMode="per-condition:A=observe;B=enforce";
                metadata.timingNote+=" Input-action timestamps are separately retained in Unity's realtime clock domain; callback latency and original scored RT remain distinct. See clock_sync_ab.jsonl for clock transfer. No physical switch or photon latency is measured.";
                inputEvents=Writer("input_events.jsonl");
            }
            if(presentationVersion==TaskEngine.PersonalPresentationRevision)
            {metadata.trialProgressRevision=TaskEngine.PersonalProgressRevision;metadata.personalCalibrationRevision=TaskEngine.PersonalCalibrationRevision;}
            if(presentationVersion!=null){metadata.gazeLinkCompatibilityRevision=QuestLinkEyeSpaceFeature.Revision;metadata.gazeLinkCompatibilityInitialState=QuestLinkEyeSpaceFeature.LastResult;}
            WriteMetadata();
            trials = Writer("trials.csv"); practice = Writer("practice_trials.csv"); events = Writer("events.jsonl");
            const string header = "block,trial,mode,practice,scored,color,seed,expected_match,responded,response_match,correct,timeout,interrupted,aborted,planned_onset_s,actual_onset_s,onset_frame,response_time_s,response_frame,rt_s,offset_time_s,close_time_s,level,training_stage,transition,phase_trial,block_trial,trial_end_s";
            string timingHeader=",input_event_id,input_event_time_s,input_rt_s";
            trials.WriteLine(header+timingHeader); practice.WriteLine(header+timingHeader);
        }
        StreamWriter Writer(string name) => new StreamWriter(Path.Combine(DirectoryPath, name), false, new UTF8Encoding(false)) { AutoFlush = true };
        void WriteMetadata() => File.WriteAllText(Path.Combine(DirectoryPath, "session.json"), JsonUtility.ToJson(metadata, true), new UTF8Encoding(false));
        public void Event(TaskEvent e) { if (!disposed){events.WriteLine(JsonUtility.ToJson(e));if(e.type=="input_edge")inputEvents?.WriteLine(e.detail);} }
        public void Trial(TrialResult t)
        {
            if (disposed) return;
            (t.practice ? practice : trials).WriteLine(string.Join(",", t.block, t.trial, t.mode, B(t.practice), B(t.scored), t.color, t.seed, B(t.expectedMatch), B(t.responded), t.responded ? B(t.responseMatch) : "", B(t.correct), B(t.timeout), B(t.interrupted), B(t.aborted), F(t.plannedOnset), F(t.actualOnset), t.onsetFrame, F(t.responseTime), t.responseFrame, F(t.reactionTime), F(t.offsetTime), F(t.closeTime),t.level,t.trainingStage,B(t.transition),t.phaseTrial,t.blockTrial,F(t.trialEnd),t.inputEventId,F(t.inputEventTime),F(t.inputReactionTime)));
        }
        public void Complete(string status) { if (disposed) return; metadata.status = status; WriteMetadata(); Dispose(); }
        public void Dispose() { if (disposed) return; disposed = true; trials?.Dispose(); practice?.Dispose(); events?.Dispose(); inputEvents?.Dispose(); }
    }
}

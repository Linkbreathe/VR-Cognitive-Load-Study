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
            public string experimentMode = "FixedBlockExperiment";
            public string statusDisplayVersion = ExperimentStatusView.Revision;
            public string countdownDisplayVersion = SpatialTaskView.CountdownRevision;
            public string gazeRoiRevision="not_used", gazeDebugRevision="not_used";
            public bool gazeDebugRequested,gazeDebugEnabled;
            public TaskConfig config;
        }
        public string DirectoryPath { get; }
        readonly StreamWriter trials, practice, events;
        readonly Metadata metadata;
        bool disposed;
        static string F(double n) => n.ToString("R", CultureInfo.InvariantCulture);
        static string B(bool b) => b ? "1" : "0";
        public SessionRecorder(string root, TaskConfig config, string participant = "anonymous", string kind = "participant", string presentationVersion = null,
            bool gazeDebugRequested=false,bool gazeDebugEnabled=false)
        {
            string id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            DirectoryPath = Path.Combine(root, kind, id); Directory.CreateDirectory(DirectoryPath);
            metadata = new Metadata { sessionId = id, sessionType = kind, participantCode = participant, utcStarted = DateTime.UtcNow.ToString("O"), applicationVersion = Application.version, unityVersion = Application.unityVersion, device = SystemInfo.deviceModel, status = "in_progress", config = config,
                timingNote = "Unity realtimeSinceStartupAsDouble in seconds; software events, not measured photon onset. RT from actual software onset includes pauses; interrupted trials are excluded from clean summaries. Warmups are unscored and do not require an answer. Frame gaps are diagnostic, not hardware dropped-frame counts." };
            if(presentationVersion!=null){metadata.presentationVersion=presentationVersion;metadata.experimentMode="AdaptiveDistractionTraining";}
            if(presentationVersion!=null){metadata.gazeRoiRevision=GazeRegions.Revision;metadata.gazeDebugRevision=GazeDebugHighlighter.Revision;}
            metadata.gazeDebugRequested=gazeDebugRequested;metadata.gazeDebugEnabled=gazeDebugEnabled;
            WriteMetadata();
            trials = Writer("trials.csv"); practice = Writer("practice_trials.csv"); events = Writer("events.jsonl");
            const string header = "block,trial,mode,practice,scored,color,seed,expected_match,responded,response_match,correct,timeout,interrupted,aborted,planned_onset_s,actual_onset_s,onset_frame,response_time_s,response_frame,rt_s,offset_time_s,close_time_s,level,training_stage,transition";
            trials.WriteLine(header); practice.WriteLine(header);
        }
        StreamWriter Writer(string name) => new StreamWriter(Path.Combine(DirectoryPath, name), false, new UTF8Encoding(false)) { AutoFlush = true };
        void WriteMetadata() => File.WriteAllText(Path.Combine(DirectoryPath, "session.json"), JsonUtility.ToJson(metadata, true), new UTF8Encoding(false));
        public void Event(TaskEvent e) { if (!disposed) events.WriteLine(JsonUtility.ToJson(e)); }
        public void Trial(TrialResult t)
        {
            if (disposed) return;
            (t.practice ? practice : trials).WriteLine(string.Join(",", t.block, t.trial, t.mode, B(t.practice), B(t.scored), t.color, t.seed, B(t.expectedMatch), B(t.responded), t.responded ? B(t.responseMatch) : "", B(t.correct), B(t.timeout), B(t.interrupted), B(t.aborted), F(t.plannedOnset), F(t.actualOnset), t.onsetFrame, F(t.responseTime), t.responseFrame, F(t.reactionTime), F(t.offsetTime), F(t.closeTime),t.level,t.trainingStage,B(t.transition)));
        }
        public void Complete(string status) { if (disposed) return; metadata.status = status; WriteMetadata(); Dispose(); }
        public void Dispose() { if (disposed) return; disposed = true; trials?.Dispose(); practice?.Dispose(); events?.Dispose(); }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace EvidenceVR
{
    // Personalized sequence executor. All model and policy decisions belong to Python.
    public sealed partial class TaskEngine
    {
        public bool IsPersonalized => Adaptive?.Config.IsPersonalized==true;
        public int ProtocolCompletedTrials {get;private set;}
        public double ProtocolStageStarted {get;private set;}
        public bool SupplementUsed {get;private set;}
        public string PersonalModelStatus {get;private set;}="awaiting_calibration";
        public bool PersonalModelAdmitted {get;private set;}
        public bool PersonalModelTestUsable {get;private set;}
        public bool IsOperatorTest => IsPersonalized && Adaptive.Config.operatorManaged;
        public bool IsDirectBioadaptiveTest => IsOperatorTest && Adaptive.Config.testSessionMode=="bioadaptive";
        public bool PersonalMountIntact {get;private set;}=true;
        public const string PersonalPresentationRevision="personalized-lab-v2.6-sign-typography";
        public const string PersonalCalibrationRevision="calibration-block-budget-v2-short-default";
        public const string PersonalProgressRevision="trial-progress-v3-clear-type";
        public const string PersonalFlowRevision="preparation-practice-calibration-no-rest-v1";
        public const string PersonalQuestionnaireAdministration="interviewer_oral_headset_on-v1";
        public int PersonalDecisionId {get;private set;}
        public int TrainingOrdinal {get;private set;}
        public string ProtocolStageLabel => Adaptive.Stage=="random" || Adaptive.Stage=="bioadaptive" ? "Training "+TrainingOrdinal :
            Adaptive.Stage=="calibration"?"Calibration":Adaptive.Stage=="practice"?"Practice":
            Adaptive.Stage=="preparation" || Adaptive.Stage=="model_wait"?"Preparation":"Questionnaire / rest";
        int protocolLength, phaseOffset, blockStartIndex;
        public int PersonalProgressTotal => !IsPersonalized?0:Adaptive.Stage=="calibration"?12:
            Adaptive.Stage=="practice"?14:Adaptive.Stage=="random" || Adaptive.Stage=="bioadaptive"?96:0;
        public int PersonalProgressCompleted => PersonalProgressTotal==0 ||
            (Phase!=TaskPhase.Running && !(Phase==TaskPhase.Paused && resumePhase==TaskPhase.Running))?0:
            Math.Max(0,Math.Min(PersonalProgressTotal,ProtocolCompletedTrials-(Adaptive.Stage=="calibration"?phaseOffset+blockStartIndex:0)));
        public string PersonalProgressText => PersonalProgressCompleted+"/"+PersonalProgressTotal;
        public string PersonalProgressCaption => Adaptive.Stage=="calibration"?
            (SupplementUsed?"Extra block ":"Block ")+Math.Max(1,Math.Min(SupplementUsed?2:Adaptive.Config.calibrationBlocks,TrialIndex/12+1))+"/"+(SupplementUsed?2:Adaptive.Config.calibrationBlocks):"Completed";
        bool skipPractice;
        string firstTraining => Adaptive.Config.counterbalanceGroup/2==0?"random":"bioadaptive";
        string secondTraining => firstTraining=="random"?"bioadaptive":"random";
        public event Action<TrialResult> OnProtocolTrialEnd;

        void PersonalStage(string stage,int level,double now,int frame,TaskPhase phase=TaskPhase.Instructions)
        {
            Adaptive.SetPersonalStage(stage,level);ProtocolStageStarted=now;Phase=phase;Practice=stage=="practice";
            StimulusVisible=false;CurrentResult=null;Mode=TaskMode.Hard;TrialIndex=-1;
            ResetRolling(now,frame,"stage_change");Emit("protocol_stage",now,frame,now,stage);
        }
        void AdvancePersonal(double now,int frame)
        {
            if(!PersonalMountIntact)return;
            if(Phase==TaskPhase.Preparation)
            {
                if(IsDirectBioadaptiveTest && !PersonalModelTestUsable)return;
                Block=0;
                if(IsDirectBioadaptiveTest){TrainingOrdinal=1;PersonalStage("bioadaptive",0,now,frame);}
                else PersonalStage(IsOperatorTest && Adaptive.Config.testSessionMode=="calibration"?"calibration":"practice",
                    IsOperatorTest && Adaptive.Config.testSessionMode=="calibration"?Adaptive.Config.counterbalanceGroup%2:1,now,frame);
                return;
            }
            if(Phase==TaskPhase.Running && Practice){skipPractice=true;Emit("practice_skip_requested",now,frame,now);return;}
            if(Phase==TaskPhase.Rest)
            {
                if(Adaptive.Stage=="questionnaire_calibration")
                {TrainingOrdinal=1;AcknowledgeOralQuestionnaire("calibration",now,frame);PersonalStage(firstTraining,0,now,frame);}
                else if(Adaptive.Stage=="questionnaire_1")
                {TrainingOrdinal=2;AcknowledgeOralQuestionnaire("training_1",now,frame);PersonalStage(secondTraining,0,now,frame);}
                else if(Adaptive.Stage=="questionnaire_2")
                {AcknowledgeOralQuestionnaire("training_2",now,frame);Phase=TaskPhase.Complete;Emit("session_complete",now,frame,now,PersonalModelStatus);}
                return;
            }
            if(Phase!=TaskPhase.Instructions)return;
            Emit("operator_recheck_acknowledged",now,frame,now,"seating;task_area;sensors;eye_calibration;stage="+Adaptive.Stage);
            BeginPersonalTrials(now,frame);
        }
        void AcknowledgeOralQuestionnaire(string occasion,double now,int frame)
        {
            Emit("external_questionnaire_acknowledged",now,frame,now,occasion);
            Emit("questionnaire_administration",now,frame,now,"occasion="+occasion+";method="+PersonalQuestionnaireAdministration+";submission_verified=false");
        }
        public void ReportPersonalMountMovement(double now,int frame,string reason)
        {
            if(!IsPersonalized || !PersonalMountIntact || Phase==TaskPhase.Preparation || Phase==TaskPhase.Complete || Phase==TaskPhase.Aborted)return;
            PersonalMountIntact=false;PersonalModelAdmitted=false;PersonalModelStatus="personal_model_not_validated";
            Adaptive.Pending=null;ResetRolling(now,frame,"mount_interrupted");
            Emit("personal_mount_interrupted",now,frame,now,reason+";new_session_required=true;automatic_offset_correction=false");
            Pause(now,frame,"headset_or_electrode_movement");
        }
        void BeginPersonalTrials(double now,int frame)
        {
            phaseOffset=Adaptive.Stage=="calibration" && SupplementUsed?Adaptive.Config.calibrationBlocks*12:0;
            protocolLength=Practice?14:Adaptive.Stage=="calibration"?(SupplementUsed?24:Adaptive.Config.calibrationBlocks*12):96;
            ProtocolCompletedTrials=phaseOffset;blockStartIndex=0;skipPractice=false;scoredInBlock=0;
            sequence=SequenceGenerator.Generate(config,TaskMode.Hard,protocolLength-2,unchecked(config.seed+Block*104729));
            TrialIndex=0;origin=now;pausedTotal=0;ProtocolStageStarted=now;Phase=TaskPhase.Running;
            transitionRemaining=0;ResetRolling(now,frame,"phase_start");
            Emit("protocol_stage",now,frame,now,Adaptive.Stage);
            Emit("block_start",now,frame,now,"level="+Adaptive.Level+";stage="+Adaptive.Stage+";continuous_memory=false;warmups_in_trial_budget=true");
            BeginPersonalTrial(now,frame);
        }
        void BeginPersonalTrial(double now,int frame)
        {
            BeginTrial(now,frame);
            CurrentResult.level=Practice?1:Adaptive.Level;
            CurrentResult.phaseTrial=phaseOffset+TrialIndex;CurrentResult.blockTrial=TrialIndex-blockStartIndex;
        }
        void FinishPersonalTrial(double now,int frame)
        {
            CurrentResult.trialEnd=now;
            var completed=CurrentResult;
            FinishTrial();ProtocolCompletedTrials=phaseOffset+TrialIndex+1;
            Emit("trial_end",now,frame,Planned+4.5,UnityEngine.JsonUtility.ToJson(completed));
            OnProtocolTrialEnd?.Invoke(completed);
            if((Adaptive.Stage=="random" || Adaptive.Stage=="bioadaptive") && ProtocolCompletedTrials%5==0 && ProtocolCompletedTrials<96)
            {PersonalDecisionId++;Emit("personal_decision_due",now,frame,now,"completed_trials="+ProtocolCompletedTrials);}
        }
        void SummarizePersonalBlock(double now,int frame)
        {
            var rows=Results.Where(t=>t.block==Block && !t.practice).ToList();
            if(rows.Count>0)
            {
                var summary=TrainingBlock.Summarize(rows,Block,Adaptive.Level,Adaptive.Stage);
                summary.start=rows[0].plannedOnset;summary.end=now;
                summary.valid=rows.Count==(Adaptive.Stage=="calibration"?12:24) && !rows.Any(t=>t.interrupted||t.aborted);
                Adaptive.Blocks.Add(summary);OnTrainingBlock?.Invoke(summary);
            }
            Emit("block_complete",now,frame,now);Block++;
        }
        void TickPersonal(double now,int frame)
        {
            if(Phase!=TaskPhase.Running)return;
            double elapsed=now-Planned;
            if(StimulusVisible && elapsed>=4){StimulusVisible=false;CurrentResult.offsetTime=now;Emit("stimulus_off",now,frame,Planned+4);}
            if(!closed && elapsed>=4)Close(now,frame);
            if(elapsed<4.5)return;
            FinishPersonalTrial(now,frame);TrialIndex++;
            if(Practice && (TrialIndex>=protocolLength || skipPractice))
            {
                Emit("practice_summary",now,frame,now,"actual_stimuli="+TrialIndex+";skipped="+(TrialIndex<14));
                Emit("block_complete",now,frame,now);Block++;
                PersonalStage("calibration",Adaptive.Config.counterbalanceGroup%2,now,frame);return;
            }
            if(TrialIndex>=protocolLength)
            {
                SummarizePersonalBlock(now,frame);
                if(Adaptive.Stage=="calibration")PersonalStage("model_wait",0,now,frame,TaskPhase.Rest);
                else if(IsDirectBioadaptiveTest)CompleteOperatorTest(now,frame);
                else PersonalStage("questionnaire_"+TrainingOrdinal,0,now,frame,TaskPhase.Rest);
                return;
            }
            if(!Practice && TrialIndex%(Adaptive.Stage=="calibration"?12:24)==0)
            {
                SummarizePersonalBlock(now,frame);blockStartIndex=TrialIndex;
                if(Adaptive.Stage=="calibration")
                {
                    int before=Adaptive.Level;Adaptive.SetPersonalLevel(1-before);transitionRemaining=2;
                    ResetRolling(now,frame,"calibration_condition_change");
                    Emit("adaptive_level_changed",now,frame,now,"level="+Adaptive.Level+";stage=calibration;previous_level="+before);
                }
                Emit("block_start",now,frame,Planned,"level="+Adaptive.Level+";stage="+Adaptive.Stage+";continuous_memory=true");
            }
            ApplyPersonalDecision(now,frame);
            if(Phase==TaskPhase.Running)BeginPersonalTrial(now,frame);
        }
        void ApplyPersonalDecision(double now,int frame)
        {
            if(!PersonalMountIntact){Adaptive.Pending=null;return;}
            var d=Adaptive.Pending;if(d==null)return;
            if(!EvidenceGuardContract.Allows(Adaptive.Config,d))
            {Adaptive.Pending=null;Emit("evidence_guard_rejected",now,frame,now,"execution_authorization_missing;request="+d.requestId);return;}
            if(d.epoch!=RollingEpoch || d.level!=Adaptive.Level || d.evaluationId!=PersonalDecisionId ||
                ProtocolCompletedTrials-d.lastTrial>5 || (Adaptive.Stage!="random" && Adaptive.Stage!="bioadaptive"))
            {Adaptive.Pending=null;Emit("personal_execution_rejected",now,frame,now,"stale_epoch_or_decision");return;}
            if(CanApplyAdaptiveChange!=null && !CanApplyAdaptiveChange())
            {Emit("personal_execution_deferred",now,frame,now,"active_media;request="+d.requestId);return;}
            Adaptive.Pending=null;
            if(d.action=="REST")
            {Pause(now,frame,"python_rest_request");return;}
            int target=d.action=="ON"?1:d.action=="OFF"?0:Adaptive.Level;
            int before=Adaptive.Level;Adaptive.SetPersonalLevel(target);
            Emit("personal_execution",now,frame,now,"request="+d.requestId+";action="+d.action+";level="+target+";reason="+d.reason+";basis="+d.basis+
                (Adaptive.Config.evidenceGuardMode=="off"?"":";previous_level="+before+";scene="+LabEnvironmentIdentity.CurrentRevision+";guard="+d.guardVerdict));
            if(before!=target)
            {
                transitionRemaining=2;ResetRolling(now,frame,"level_change");
                Emit("adaptive_level_changed",now,frame,now,"level="+target+";stage="+Adaptive.Stage+";previous_level="+before);
            }
        }
        void CompleteOperatorTest(double now,int frame)
        {Phase=TaskPhase.Complete;StimulusVisible=false;Emit("session_complete",now,frame,now,"engineering_test;"+PersonalModelStatus);}
        public void ReceivePersonalModel(string status,bool admitted,int attempt,double now,int frame,bool testUsable=false)
        {
            if(IsDirectBioadaptiveTest && PersonalMountIntact)
            {PersonalModelStatus=status;PersonalModelAdmitted=admitted;PersonalModelTestUsable=testUsable;return;}
            if(!IsPersonalized || !PersonalMountIntact || Adaptive.Stage!="model_wait")return;
            PersonalModelStatus=status;PersonalModelAdmitted=admitted;
            if(status!="personal_model_validated" && status!="personal_model_not_validated")return;
            if(attempt<(SupplementUsed?2:1))return;
            Emit("personal_model_result",now,frame,now,"status="+status+";admitted="+admitted+";attempt="+attempt);
            if(!admitted && !SupplementUsed)
            {SupplementUsed=true;PersonalStage("calibration",Adaptive.Config.counterbalanceGroup%2,now,frame);}
            else if(IsOperatorTest && Adaptive.Config.testSessionMode=="calibration")CompleteOperatorTest(now,frame);
            else PersonalStage("questionnaire_calibration",0,now,frame,TaskPhase.Rest);
        }
        void ResumePersonal(double now,int frame)
        {
            if(Phase!=TaskPhase.Paused || !PersonalMountIntact)return;
            double duration=now-pauseStart;Phase=resumePhase;
            if(Phase==TaskPhase.Running)
            {
                if(CurrentResult!=null)
                {
                    CurrentResult.interrupted=true;CurrentResult.trialEnd=now;
                    var ended=CurrentResult;FinishTrial();ProtocolCompletedTrials=phaseOffset+TrialIndex+1;
                    OnProtocolTrialEnd?.Invoke(ended);TrialIndex++;
                }
                // Preserve the 96-stimulus budget. The next two positions become unscored rewarm stimuli.
                for(int i=TrialIndex;i<Math.Min(TrialIndex+2,sequence.Count);i++)sequence[i].scored=false;
                pausedTotal=now-origin-TrialIndex*4.5;transitionRemaining=2;
                if(TrialIndex>=protocolLength)
                {
                    SummarizePersonalBlock(now,frame);
                    if(IsDirectBioadaptiveTest)CompleteOperatorTest(now,frame);
                    else PersonalStage(Practice?"calibration":Adaptive.Stage=="calibration"?"model_wait":"questionnaire_"+TrainingOrdinal,Practice?Adaptive.Config.counterbalanceGroup%2:0,now,frame,Practice?TaskPhase.Instructions:TaskPhase.Rest);
                }
                else
                {
                    if(!Practice && TrialIndex%(Adaptive.Stage=="calibration"?12:24)==0)
                    {
                        SummarizePersonalBlock(now,frame);blockStartIndex=TrialIndex;
                        if(Adaptive.Stage=="calibration")Adaptive.SetPersonalLevel(1-Adaptive.Level);
                        Emit("block_start",now,frame,now,"level="+Adaptive.Level+";stage="+Adaptive.Stage+";resumed_boundary=true");
                    }
                    BeginPersonalTrial(now,frame);
                }
            }
            ResetRolling(now,frame,"resume");Emit("resume",now,frame,now,"pause_seconds="+duration+";rewarm_in_budget=true");
        }
    }
}

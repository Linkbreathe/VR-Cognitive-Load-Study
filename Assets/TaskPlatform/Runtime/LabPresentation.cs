using System;
using TMPro;
using UnityEngine;

namespace EvidenceVR
{
    public sealed class LabPresentation : MonoBehaviour
    {
        public const string Version = "spatial-lab-v1.7.1-more-video";
        public TaskPlatformController controller;
        public GameObject setupCards, exampleCards;
        public TMP_Text[] setupLabels;
        public TMP_Text actionHint, matchKey, nonmatchKey, pauseHint, exampleCaption;
        string lastSignature;
        public void Refresh()
        {
            var e=controller.Engine; if(e==null) return;
            var c=controller.SessionConfig;
            string signature=$"{e.Phase}|{e.Mode}|{e.Block}|{e.TrialIndex}|{e.CurrentResult?.responded}|{e.CurrentResult?.timeout}|{controller.Calibrated}|{controller.MatchChecked}|{controller.NonmatchChecked}|{Math.Ceiling(e.RestRemaining(Time.realtimeSinceStartupAsDouble))}";
            if(signature==lastSignature) return; lastSignature=signature;
            setupCards.SetActive(e.Phase==TaskPhase.Preparation);
            exampleCards.SetActive(e.Phase==TaskPhase.Instructions && e.Mode==TaskMode.Hard);
            matchKey.text=Key(c.matchAction); nonmatchKey.text=Key(c.nonmatchAction);
            controller.badge.text=e.Block>=0 && e.Block<4 ? Stage(e) : "EVIDENCE  /  TASK LAB";
            controller.footer.text="";
            controller.body.rectTransform.anchoredPosition=new Vector2(0,125);
            controller.body.fontSize=30;
            pauseHint.text=Key(c.pauseAction)+"  Pause";
            string next=Key(c.advanceAction);
            actionHint.text="";
            switch(e.Phase)
            {
                case TaskPhase.Preparation:
                    controller.heading.text="Settle in. Get ready.";
                    bool ready=controller.Calibrated && controller.MatchChecked && controller.NonmatchChecked;
                    controller.body.text=(ready ? "You are ready to begin." : !controller.Calibrated ? "Sit comfortably and look straight ahead.\nPress the LEFT TRIGGER to place the task area." : "Check both response buttons.\nPress each button once to confirm the mapping.")+"\n\nOne session = 2 practice blocks + 2 main blocks.";
                    SetStep(0,controller.Calibrated,"01  POSITION","LEFT TRIGGER  /  C");
                    SetStep(1,controller.MatchChecked,"02  MATCH",Key(c.matchAction));
                    SetStep(2,controller.NonmatchChecked,"03  NON-MATCH",Key(c.nonmatchAction));
                    actionHint.text=ready ? next+"   Begin practice" : "Complete all three checks to continue";
                    pauseHint.text="SEATED TASK  /  NO MOVEMENT REQUIRED"; break;
                case TaskPhase.Instructions:
                    controller.heading.text=e.Mode==TaskMode.Easy ? "0-back  /  Color match" : "2-back  /  Remember two";
                    controller.body.text=e.Mode==TaskMode.Easy ? "Always compare with "+TaskPlatformController.ColorNames[c.targetColor]+".\nSame color: "+Key(c.matchAction)+". Different color: "+Key(c.nonmatchAction)+".\n\nFirst "+c.warmupTrials+" colors: observe only." : "Compare with the color TWO positions earlier.\nSame color: "+Key(c.matchAction)+". Different color: "+Key(c.nonmatchAction)+".\nFirst "+c.warmupTrials+" colors: remember only; do not respond.";
                    exampleCaption.text="Example: #3 matches #1. Press "+Key(c.matchAction)+".\nNext: #4 compares with #2, then #5 with #3.";
                    actionHint.text=next+"   Start "+(e.Practice ? "practice" : "block")+"     /     "+(e.Practice || c.formalFeedbackEnabled ? "Feedback on" : "No answer feedback"); break;
                case TaskPhase.Running:
                    controller.heading.text=e.Mode==TaskMode.Easy ? "0-BACK  /  TARGET: "+TaskPlatformController.ColorNames[c.targetColor] : "2-BACK  /  TWO POSITIONS BACK";
                    controller.body.rectTransform.anchoredPosition=new Vector2(0,200);
                    int position=e.TrialIndex+1;
                    controller.footer.text=Progress(e,c);
                    controller.body.text=!e.Current.scored ? "OBSERVE ONLY  /  Do not respond\n"+(e.Mode==TaskMode.Hard ? "Remember this color for a later comparison." : "The target for this block is "+TaskPlatformController.ColorNames[c.targetColor]+".") :
                        (e.Mode==TaskMode.Easy ? "Compare this color with "+TaskPlatformController.ColorNames[c.targetColor]+"." : "Compare color #"+position+" with color #"+(position-2)+".")+"\nSame color: "+Key(c.matchAction)+". Different color: "+Key(c.nonmatchAction)+".";
                    if((e.Practice || c.formalFeedbackEnabled) && e.Current.scored && e.CurrentResult!=null && (e.CurrentResult.responded || e.CurrentResult.timeout))
                        actionHint.text=e.CurrentResult.timeout ? "No response" : e.CurrentResult.correct ? "Correct" : "Incorrect";
                    break;
                case TaskPhase.Rest:
                    controller.badge.text="BREAK  /  MAIN BLOCK 1 OF 2 COMPLETE";
                    controller.heading.text="Take a short break.";
                    controller.body.text="Relax your hands and remain seated.\nNext: STAGE 4/4  /  MAIN BLOCK 2/2\n\n"+(c.hardFirst ? "0-back / Match "+TaskPlatformController.ColorNames[c.targetColor] : "2-back / Remember two positions ago");
                    double remaining=e.RestRemaining(Time.realtimeSinceStartupAsDouble);
                    actionHint.text=remaining>0 ? Math.Ceiling(remaining)+" seconds remaining" : next+"   Review next rule"; break;
                case TaskPhase.Paused:
                    controller.heading.text="Session paused";
                    controller.body.text="Your progress is preserved.\nAny active trial is marked as interrupted.\n\nLEFT TRIGGER / C / R starts a new session.";
                    if(e.Current!=null) controller.footer.text=Progress(e,c);
                    actionHint.text=Key(c.pauseAction)+"   Resume session"; break;
                case TaskPhase.Complete:
                    controller.badge.text="SESSION COMPLETE  /  ALL 4 STAGES FINISHED";
                    controller.heading.text="All done. Thank you.";
                    controller.body.text="Your responses have been saved.\nPlease wait for the experimenter\nbefore removing the headset.";
                    actionHint.text=next+"   Start a new session"; pauseHint.text="SESSION COMPLETE"; break;
                case TaskPhase.Aborted:
                    controller.heading.text="Session stopped";
                    controller.body.text="The incomplete session has been preserved.";
                    actionHint.text=next+"   Start a new session"; break;
            }
        }
        void SetStep(int index,bool done,string title,string control)
        { setupLabels[index].text=title+"\n<size=26>"+(done ? "READY" : control)+"</size>"; setupLabels[index].color=done ? LabPalette.Accent : LabPalette.Muted; }
        static string Key(string name) => name=="RightPrimary" ? "A" : name=="RightSecondary" ? "B" : name=="LeftPrimary" ? "X / ENTER" : name=="LeftSecondary" ? "Y / P" : name;
        static string Stage(TaskEngine e) => "STAGE "+(e.Block+1)+"/4  /  "+(e.Practice ? "PRACTICE "+(e.Block+1)+"/2" : "MAIN BLOCK "+(e.Block-1)+"/2");
        static string Progress(TaskEngine e,TaskConfig c)
        {
            int count=e.Practice ? (e.Mode==TaskMode.Easy ? c.easyPracticeTrials : c.hardPracticeTrials) : c.scoredTrials;
            string phase=e.Current.scored ? "RESPONSE TRIAL "+(e.TrialIndex-c.warmupTrials+1)+"/"+count : "WARM-UP "+(e.TrialIndex+1)+"/"+c.warmupTrials;
            return "COLOR "+(e.TrialIndex+1)+"/"+(count+c.warmupTrials)+"  /  "+phase;
        }
    }
}

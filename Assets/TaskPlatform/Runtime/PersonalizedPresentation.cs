using UnityEngine;

namespace EvidenceVR
{
    public sealed partial class AdaptiveRuntime
    {
        void RenderPersonal()
        {
            var view=owner.spatialView;
            string stage=engine.Adaptive.Stage;
            if(engine.Phase==TaskPhase.Running)
            {
                view.stage.text=engine.ProtocolStageLabel+" | 2-back";
                view.rule.text=engine.Current.scored?"":"Observe only";
                return;
            }
            if(engine.Phase==TaskPhase.Preparation)
            {
                owner.heading.text="Prepare for the study";
                owner.body.text=!CanStart?"Try the controls below while the experimenter\nchecks the media and live signal connection.":"Sit comfortably. Try each control once.\nThe experimenter will check the signals.";
                owner.presentation.actionHint.text=owner.Calibrated && owner.MatchChecked && owner.NonmatchChecked && CanStart?"X / ENTER  Continue":"Complete preparation checks";
                return;
            }
            if(engine.Phase==TaskPhase.Paused)
            {
                owner.body.text=engine.PersonalMountIntact?
                    "Keep the headset and sensors in place.\nY / P: resume. Two colors will be observe-only.\nX / ENTER: stop. Left trigger / C: restart.":
                    "Headset or sensor movement was recorded.\nPlease ask the experimenter to check the equipment.\nLeft trigger / C: start a new session. X / ENTER: stop.";
                return;
            }
            if(engine.Phase==TaskPhase.Complete)
            {
                owner.heading.text="Study complete";owner.body.text="Your responses have been saved.\nPlease wait for the experimenter.";
                owner.presentation.actionHint.text="X / ENTER  New session";return;
            }
            if(engine.Phase==TaskPhase.Rest)
            {
                // Rest pages use the existing instruction surface, not a new UI hierarchy.
                view.instructions.enabled=true;view.taskCanvas.enabled=false;
                owner.presentation.exampleCaption.text="";
                if(stage=="model_wait")
                {
                    owner.heading.text="Preparation";owner.body.text="Please rest while the study computer prepares the next stage.\nWait for the experimenter.";
                    owner.presentation.actionHint.text="Please wait";
                }
                else
                {
                    owner.heading.text="Questions and rest";
                    owner.body.text="Keep your headset and sensors in place.\nThe experimenter will read the questions aloud\nand record your answers. Answer verbally.\nTell the experimenter if you need to remove the headset.";
                    owner.presentation.actionHint.text="X / ENTER  Confirm questions completed";
                }
                return;
            }
            if(engine.Phase==TaskPhase.Instructions)
            {
                owner.heading.text=engine.ProtocolStageLabel;
                owner.body.text=
                    "Compare with the color TWO positions earlier.\nA: Match   |   B: Non-match\nFirst two colors: observe only.\nContinue answering during movie clips.\n"+(stage=="practice"?"Practice shows answer feedback.":"No answer feedback in this stage.");
                owner.presentation.exampleCaption.text=stage=="practice"?"X / ENTER during practice skips the remaining practice at the next boundary.":
                    stage=="calibration"?"Conditions alternate without a break. Keep the color sequence in memory.":"Keep the headset and sensors in place. Wait for the experimenter before starting.";
                owner.presentation.actionHint.text="X / ENTER  Start";
            }
        }
    }
}

using System;
using TMPro;
using UnityEngine;

namespace EvidenceVR
{
    public sealed class SpatialTaskView : MonoBehaviour
    {
        public Canvas instructions, taskCanvas;
        public TMP_Text stage, rule, progress, comparison, timer, timerLabel, controls, feedback;
        public Transform bench, leftLeg, rightLeg;
        public void Refresh(TaskPlatformController controller,double now)
        {
            var engine=controller.Engine;
            bool running=engine!=null && engine.Phase==TaskPhase.Running;
            instructions.enabled=!running; taskCanvas.enabled=running;
            if(!running)return;
            stage.text=controller.badge.text; rule.text=controller.heading.text;
            progress.text=controller.footer.text;
            comparison.text=controller.body.text;
            controls.text=controller.presentation.matchKey.text+"  MATCH       "+controller.presentation.nonmatchKey.text+"  NON-MATCH       "+controller.presentation.pauseHint.text;
            feedback.text=controller.presentation.actionHint.text;
            double remaining=engine.ResponseRemaining(now);
            timer.text=remaining>0 ? Math.Ceiling(remaining).ToString() : "NEXT";
            timerLabel.text=remaining<=0 ? "RESPONSE WINDOW CLOSED" : engine.Current.scored ? "SECONDS TO RESPOND" : "OBSERVE ONLY";
        }
        public void PlaceSupports(Transform anchor)
        {
            // Keep the workbench grounded after seated height calibration.
            float height=Mathf.Max(.1f,anchor.position.y-.51f);
            foreach(var leg in new[]{leftLeg,rightLeg})
            {
                var position=leg.localPosition; position.y=-.51f-height*.5f; leg.localPosition=position;
                var scale=leg.localScale; scale.y=height; leg.localScale=scale;
            }
        }
        public void ShowError() { instructions.enabled=true; taskCanvas.enabled=false; }
    }
}

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
        public const float TabletopHeight=.65f;
        public const float InstructionForwardOffset=-.25f, HudForwardOffset=-.22f;
        public static readonly Color CorrectColor=LabPalette.Hex("70D69A"), IncorrectColor=LabPalette.Hex("F27D7D");
        public void Refresh(TaskPlatformController controller,double now)
        {
            var engine=controller.Engine;
            bool running=engine!=null && engine.Phase==TaskPhase.Running;
            instructions.enabled=!running; taskCanvas.enabled=running;
            if(!running)return;
            var config=controller.SessionConfig;
            int position=engine.TrialIndex+1;
            int count=(engine.Practice ? engine.Mode==TaskMode.Easy ? config.easyPracticeTrials : config.hardPracticeTrials : config.scoredTrials)+config.warmupTrials;
            stage.text=(engine.Practice ? "Practice "+(engine.Block+1) : "Main "+(engine.Block-1))+"/2  |  "+(engine.Mode==TaskMode.Easy ? "0-back" : "2-back")+"  |  Color "+position+"/"+count;
            rule.text=!engine.Current.scored ? "Observe only" : engine.Mode==TaskMode.Easy ? "Match "+TaskPlatformController.ColorNames[config.targetColor] : "Compare #"+position+" with #"+(position-2);
            feedback.text="";
            double remaining=engine.ResponseRemaining(now);
            timer.text=remaining>0 ? Math.Ceiling(remaining).ToString() : "0";
            timerLabel.text=remaining>0 ? "" : "Next";
            timer.color=LabPalette.Text;timerLabel.color=LabPalette.Muted;
            var result=engine.CurrentResult;
            if(engine.Practice && engine.Current.scored && result!=null && (result.responded || result.timeout))
            {
                bool correct=result.responded && result.correct;
                timer.color=timerLabel.color=correct ? CorrectColor : IncorrectColor;
                timerLabel.text=result.timeout ? "No response" : correct ? "Correct" : "Incorrect";
            }
        }
        public void PlaceSupports(Transform anchor)
        {
            // Keep reading surfaces ahead of the furniture and clear of the seated tabletop.
            float panelHalfHeight=((RectTransform)instructions.transform).rect.height*instructions.transform.lossyScale.y*.5f;
            float panelY=Mathf.Max(.12f,TabletopHeight+.12f+panelHalfHeight-anchor.position.y);
            instructions.transform.localPosition=new Vector3(0,panelY,InstructionForwardOffset);
            taskCanvas.transform.localPosition=new Vector3(0,0,HudForwardOffset);
            // The desk stays at floor height, independently of the calibrated eye height.
            var benchPosition=bench.position;benchPosition.y=0;bench.position=benchPosition;
            bench.Find("WorkSurface").localPosition=new Vector3(0,TabletopHeight-.0275f,.08f);
            bench.Find("FrontEdge").localPosition=new Vector3(0,TabletopHeight-.0475f,-.25f);
            float height=TabletopHeight-.055f;
            foreach(var leg in new[]{leftLeg,rightLeg})
            {
                var position=leg.localPosition; position.y=height*.5f; leg.localPosition=position;
                var scale=leg.localScale; scale.y=height; leg.localScale=scale;
            }
            var plinth=bench.Find("StimulusPlinth");var plinthPosition=plinth.localPosition;
            plinthPosition.y=Mathf.Max(TabletopHeight+.055f,anchor.position.y-.19f);plinth.localPosition=plinthPosition;
            var stem=bench.Find("PlinthStem");float stemHeight=plinthPosition.y-.025f-TabletopHeight;
            stem.localPosition=new Vector3(0,TabletopHeight+stemHeight*.5f,0);
            stem.localScale=new Vector3(.09f,stemHeight*.5f,.09f);
        }
        public void ShowError() { instructions.enabled=true; taskCanvas.enabled=false; }
    }
}

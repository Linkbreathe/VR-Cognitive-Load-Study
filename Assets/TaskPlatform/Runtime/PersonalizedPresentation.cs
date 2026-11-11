using UnityEngine;
using TMPro;

namespace EvidenceVR
{
    public sealed partial class AdaptiveRuntime
    {
        RectTransform personalProgressCard;
        GameObject personalCountdownSign;
        GameObject progressHeading;
        TMP_Text arithmeticExpression;
        TMP_Text mathTimeoutNotice;
        string renderedMathPrompt;
        bool? renderedMathCorrect;
        void RenderMathPrompt()
        {
            var prompt=engine.MathPrompt;
            var correct=engine.MathAnswerCorrect;
            if(prompt==renderedMathPrompt && correct==renderedMathCorrect)return;
            renderedMathPrompt=prompt;
            renderedMathCorrect=correct;
            if(!correct.HasValue){arithmeticExpression.text=prompt;return;}
            int end=prompt.IndexOf('\n');
            string color=correct.Value?"167343":"AD2933";
            arithmeticExpression.text="<color=#"+color+">"+prompt.Substring(0,end)+"</color>"+prompt.Substring(end);
        }
        readonly System.Collections.Generic.List<GameObject> hiddenRestHud=new System.Collections.Generic.List<GameObject>();
        bool roomRestVisible;
        Material deskSignMaterial;
        DeskTextStyle originalNumberStyle,originalCaptionStyle;
        sealed class DeskTextStyle
        {
            readonly float size;
            readonly bool automatic;
            readonly FontStyles style;
            readonly Material material;
            public DeskTextStyle(TMP_Text text)
            {size=text.fontSize;automatic=text.enableAutoSizing;style=text.fontStyle;material=text.fontSharedMaterial;}
            public void Restore(TMP_Text text)
            {text.fontSharedMaterial=material;text.fontStyle=style;text.enableAutoSizing=automatic;text.fontSize=size;}
        }
        Vector2 countdownOriginalSize,numberOriginalPosition,numberOriginalSize,captionOriginalPosition,captionOriginalSize;
        public TMP_Text PersonalProgressValue {get;private set;}
        public TMP_Text PersonalProgressLabel {get;private set;}
        void CreatePersonalProgress()
        {
            if(!config.IsPersonalized)return;
            // Decorate the existing countdown and its progress copy without altering the task geometry.
            personalProgressCard=Instantiate(countdown,countdown.parent);
            personalProgressCard.name="PersonalTrialProgress";
            personalProgressCard.anchoredPosition=new Vector2(-countdown.anchoredPosition.x,countdown.anchoredPosition.y);
            PersonalProgressValue=personalProgressCard.Find(owner.spatialView.timer.name).GetComponent<TMP_Text>();
            PersonalProgressLabel=personalProgressCard.Find(owner.spatialView.timerLabel.name).GetComponent<TMP_Text>();
            foreach(var text in new[]{PersonalProgressValue,PersonalProgressLabel})
            {text.raycastTarget=false;text.richText=false;text.textWrappingMode=TextWrappingModes.NoWrap;}
            var number=owner.spatialView.timer.rectTransform;var caption=owner.spatialView.timerLabel.rectTransform;
            countdownOriginalSize=countdown.sizeDelta;numberOriginalPosition=number.anchoredPosition;numberOriginalSize=number.sizeDelta;
            captionOriginalPosition=caption.anchoredPosition;captionOriginalSize=caption.sizeDelta;
            originalNumberStyle=new DeskTextStyle(owner.spatialView.timer);originalCaptionStyle=new DeskTextStyle(owner.spatialView.timerLabel);
            // A local material keeps the text crisp on opaque signs without changing other HUD text.
            deskSignMaterial=new Material(owner.spatialView.timer.fontSharedMaterial){name="DeskSignTypography"};
            deskSignMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth,0);
            deskSignMaterial.DisableKeyword("OUTLINE_ON");deskSignMaterial.DisableKeyword("UNDERLAY_ON");
            personalCountdownSign=DecorateDeskSign(countdown,owner.spatialView.timer,owner.spatialView.timerLabel,"TIME LEFT");
            var progressFrame=DecorateDeskSign(personalProgressCard,PersonalProgressValue,PersonalProgressLabel,"PROGRESS");
            progressHeading=progressFrame.transform.Find("Heading").gameObject;
            arithmeticExpression=Instantiate(PersonalProgressValue,countdown.parent);
            arithmeticExpression.name="CalibrationArithmetic";
            arithmeticExpression.rectTransform.sizeDelta=new Vector2(950,152);
            arithmeticExpression.alignment=TextAlignmentOptions.Center;
            arithmeticExpression.enableAutoSizing=false;
            arithmeticExpression.richText=true;
            arithmeticExpression.fontSize=48;
            arithmeticExpression.color=LabPalette.Hex("24332C");
            arithmeticExpression.gameObject.SetActive(false);
            mathTimeoutNotice=Instantiate(arithmeticExpression,countdown.parent);
            mathTimeoutNotice.name="ArithmeticTimeout";
            mathTimeoutNotice.color=LabPalette.Hex("AD2933");
            mathTimeoutNotice.fontSize=34;
            PlacePersonalDeskSigns();
            personalProgressCard.gameObject.SetActive(false);
        }
        GameObject DecorateDeskSign(RectTransform card,TMP_Text number,TMP_Text caption,string heading)
        {
            card.sizeDelta=new Vector2(260,180);
            number.rectTransform.anchoredPosition=Vector2.zero;number.rectTransform.sizeDelta=new Vector2(234,88);
            caption.rectTransform.anchoredPosition=new Vector2(0,-57);caption.rectTransform.sizeDelta=new Vector2(238,40);
            number.fontSharedMaterial=deskSignMaterial;number.enableAutoSizing=false;number.fontSize=78;number.fontStyle=FontStyles.Bold;
            caption.fontSharedMaterial=deskSignMaterial;caption.enableAutoSizing=false;caption.fontSize=26;caption.fontStyle=FontStyles.Normal;
            var root=new GameObject("DeskSignFrame",typeof(RectTransform));
            var rect=(RectTransform)root.transform;rect.SetParent(card,false);rect.sizeDelta=card.sizeDelta;rect.SetAsFirstSibling();
            DeskSignSurface(rect,"Stem",new Vector2(0,-150),new Vector2(14,120),"87694A");
            DeskSignSurface(rect,"Foot",new Vector2(0,-205),new Vector2(170,10),"87694A");
            DeskSignSurface(rect,"OakEdge",Vector2.zero,new Vector2(260,180),"A98A62");
            DeskSignSurface(rect,"Face",Vector2.zero,new Vector2(252,172),"303A37");
            var title=Instantiate(caption,rect);title.name="Heading";
            title.rectTransform.anchoredPosition=new Vector2(0,63);title.rectTransform.sizeDelta=new Vector2(230,34);
            title.text=heading;title.enableAutoSizing=false;title.fontSize=28;title.fontStyle=FontStyles.Bold;title.color=LabPalette.Hex("F3E7D2");
            title.raycastTarget=false;title.richText=false;title.textWrappingMode=TextWrappingModes.NoWrap;
            return root;
        }
        static void DeskSignSurface(RectTransform parent,string name,Vector2 position,Vector2 size,string color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));
            var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.anchoredPosition=position;rect.sizeDelta=size;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=LabPalette.Hex(color);image.raycastTarget=false;
        }
        void PlacePersonalDeskSigns()
        {
            // The desk is floor-fixed while the task anchor follows seated calibration height.
            var parent=countdown.parent;
            float y=parent.InverseTransformPoint(new Vector3(parent.position.x,SpatialTaskView.TabletopHeight+.21f,parent.position.z)).y;
            float x=.4f/Mathf.Abs(parent.lossyScale.x);
            countdown.anchoredPosition=new Vector2(-x,y);personalProgressCard.anchoredPosition=new Vector2(x,y);
        }
        void RenderPersonalProgress()
        {
            if(personalProgressCard==null)return;
            PlacePersonalDeskSigns();
            bool visible=engine.Phase==TaskPhase.Running && engine.PersonalProgressTotal>0;
            personalProgressCard.gameObject.SetActive(visible);
            if(!visible)return;
            PersonalProgressValue.text=engine.PersonalProgressText;
            PersonalProgressLabel.text=engine.PersonalProgressCaption;
            PersonalProgressValue.color=LabPalette.Text;PersonalProgressLabel.color=LabPalette.Text;
        }
        void DisposePersonalProgress()
        {
            if(personalProgressCard==null)return;
            SetRoomRestVisible(false);
            if(personalCountdownSign!=null){personalCountdownSign.SetActive(false);Destroy(personalCountdownSign);personalCountdownSign=null;}
            Destroy(arithmeticExpression.gameObject);
            Destroy(mathTimeoutNotice.gameObject);
            countdown.sizeDelta=countdownOriginalSize;
            owner.spatialView.timer.rectTransform.anchoredPosition=numberOriginalPosition;owner.spatialView.timer.rectTransform.sizeDelta=numberOriginalSize;
            owner.spatialView.timerLabel.rectTransform.anchoredPosition=captionOriginalPosition;owner.spatialView.timerLabel.rectTransform.sizeDelta=captionOriginalSize;
            originalNumberStyle.Restore(owner.spatialView.timer);originalCaptionStyle.Restore(owner.spatialView.timerLabel);
            personalProgressCard.gameObject.SetActive(false);Destroy(personalProgressCard.gameObject);
            if(deskSignMaterial!=null){Destroy(deskSignMaterial);deskSignMaterial=null;}
            personalProgressCard=null;PersonalProgressValue=null;PersonalProgressLabel=null;
        }
        void SetRoomRestVisible(bool visible)
        {
            if(roomRestVisible==visible)return;
            roomRestVisible=visible;
            progressHeading.SetActive(!visible);
            if(visible)
            {
                foreach(Transform item in countdown.parent)
                {
                    if(item==countdown || item==personalProgressCard || !item.gameObject.activeSelf)continue;
                    hiddenRestHud.Add(item.gameObject);
                    item.gameObject.SetActive(false);
                }
            }
            else
            {
                foreach(var item in hiddenRestHud)if(item!=null)item.SetActive(true);
                hiddenRestHud.Clear();
            }
        }
        void RenderPersonal()
        {
            var view=owner.spatialView;
            string stage=engine.Adaptive.Stage;
            bool calculating=stage=="load_math" && engine.Phase==TaskPhase.Rest;
            double now=Time.realtimeSinceStartupAsDouble;
            bool timeout=engine.Phase==TaskPhase.Rest && (calculating || stage=="model_wait") && now<engine.MathTimeoutUntil;
            bool roomRest=(stage=="load_rest" || calculating || timeout) && engine.Phase==TaskPhase.Rest;
            SetRoomRestVisible(roomRest);
            arithmeticExpression.gameObject.SetActive(calculating);
            mathTimeoutNotice.gameObject.SetActive(timeout);
            PersonalProgressValue.enableAutoSizing=calculating;
            if(roomRest)
            {
                PlacePersonalDeskSigns();
                view.instructions.enabled=false;
                view.taskCanvas.enabled=true;
                personalProgressCard.gameObject.SetActive(true);
                int seconds=Mathf.Clamp(Mathf.CeilToInt((float)(TaskEngine.CalibrationStageSeconds-(Time.realtimeSinceStartupAsDouble-engine.ProtocolStageStarted))),0,TaskEngine.CalibrationStageSeconds);
                if(timeout)
                {
                    mathTimeoutNotice.rectTransform.anchoredPosition=new Vector2(0,countdown.anchoredPosition.y+(calculating?305:185));
                    mathTimeoutNotice.text="No response: "+engine.TimedOutExpression;
                    if(!calculating)seconds=0;
                }
                view.timer.text=$"{seconds/60}:{seconds%60:00}";
                view.timer.color=LabPalette.Text;
                view.timerLabel.text="";
                if(calculating)
                {
                    arithmeticExpression.rectTransform.anchoredPosition=new Vector2(0,countdown.anchoredPosition.y+185);
                    RenderMathPrompt();
                    PersonalProgressValue.fontSizeMin=38;
                    PersonalProgressValue.fontSizeMax=78;
                }
                PersonalProgressValue.text=calculating || timeout?"calculate":"relax";
                PersonalProgressValue.color=SpatialTaskView.CorrectColor;
                PersonalProgressLabel.text="";
                return;
            }
            RenderPersonalProgress();
            if(stage.StartsWith("load_") && (engine.Phase==TaskPhase.Rest || engine.Phase==TaskPhase.Instructions))
            {
                view.instructions.enabled=true;view.taskCanvas.enabled=false;
                return;
            }
            if(engine.Phase==TaskPhase.Running)
            {
                view.stage.text=(engine.IsDirectBioadaptiveTest?(config.allowCandidateModel?"Bioadaptive test / candidate enabled":"Bioadaptive test"):engine.ProtocolStageLabel)+" | 2-back";
                if(engine.IsOperatorTest)owner.badge.text=config.allowCandidateModel?"ENGINEERING TEST / CANDIDATE MODEL ENABLED":"ENGINEERING TEST";
                view.rule.text=engine.Current.scored?"":"Observe only";
                return;
            }
            if(engine.Phase==TaskPhase.Preparation)
            {
                owner.heading.text=engine.IsOperatorTest?"Test: "+config.participantCode+" / "+config.testSessionMode:"Prepare for the study";
                owner.body.text=!CanStart?"Try the controls below while the experimenter\nchecks the media and live signal connection.":"Sit comfortably. Try each control once.\nThe experimenter will check the signals.";
                if(engine.IsOperatorTest && !CanStart)
                    owner.body.text=!ServiceConnected || !TestAssignmentConfirmed?"Waiting for the study computer to confirm this test.":
                        !config.TaskOnly && !TestSignalsReady?"Waiting for eligible Mindtooth EEG.\nCheck sources on the study computer.":
                        !ComparisonTrackingReady?"Wear Quest Pro and enable eye tracking.\nWaiting for a tracked headset and valid eyes.":
                        config.IsLayerAB && !StudyClockReady?"Waiting for clock synchronization.":
                        Media==null || !Media.EngineeringReady?"Waiting for verified media files.\nCheck the installed media package.":"Waiting for the selected model to load.";
                owner.presentation.actionHint.text=owner.Calibrated && owner.MatchChecked && owner.NonmatchChecked && CanStart?"X / ENTER  Continue":"Complete preparation checks";
                if(config.DesktopComparison)owner.body.text+="\nDesktop mode: C to center; A / B to check controls.\nSimulated gaze: 75% valid / 70% task.";
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
                owner.presentation.actionHint.text=engine.IsOperatorTest?"Select the next test on the study computer":"X / ENTER  New session";return;
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
                    if(config.IsLayerAB)
                    {
                        owner.body.text="Keep your headset and sensors in place.\nAnswer six workload and four overall impression ratings aloud.\n"+(stage=="questionnaire_2"?"Then give your preference between the two sessions.":"Tell the experimenter about any discomfort.");
                        owner.presentation.actionHint.text=!engine.ABQuestionnaireAcknowledged?"Experimenter: X / ENTER after Google Forms submission":
                            engine.ABRestRemaining(Time.realtimeSinceStartupAsDouble)>0?"Rest: "+Mathf.CeilToInt((float)engine.ABRestRemaining(Time.realtimeSinceStartupAsDouble))+" s":"X / ENTER  Continue";
                    }
                }
                return;
            }
            if(engine.Phase==TaskPhase.Instructions)
            {
                owner.heading.text=engine.ProtocolStageLabel;
                owner.body.text=
                    "Compare with the color TWO positions earlier.\nA: Match   |   B: Non-match\nFirst two colors: observe only.\nContinue answering during movie clips.\n"+(engine.AllowsResponseRevision?"Change your answer until zero; your last choice counts.\nGreen timer + quiet tone = input received, not correctness.":stage=="practice"?"Practice shows answer feedback.":"No answer feedback in this stage.");
                owner.presentation.exampleCaption.text=stage=="practice"?"X / ENTER during practice skips the remaining practice at the next boundary.":
                    stage=="calibration"?"Conditions alternate without a break. Keep the color sequence in memory.":"Keep the headset and sensors in place. Wait for the experimenter before starting.";
                owner.presentation.actionHint.text="X / ENTER  Start";
            }
        }
    }
}

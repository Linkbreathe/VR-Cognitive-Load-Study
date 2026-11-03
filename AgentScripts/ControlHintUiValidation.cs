using System;
using System.IO;
using System.Threading.Tasks;
using EvidenceVR;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ControlHintUiValidation
{
    const string Folder="Evidence/ControlHintUI/";
    static TaskPlatformController Owner()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(!Application.isPlaying || o?.Engine?.Adaptive?.Config.sessionType!="engineering")
            throw new InvalidOperationException("Use an engineering Play Mode session.");
        return o;
    }
    static void Check(bool value,string message)
    {
        if(!value)throw new Exception(message);
        File.AppendAllText(Folder+"validation.txt","PASS: "+message+Environment.NewLine);
    }
    static async Task Press(params Key[] keys)
    {
        var original=InputSystem.settings.editorInputBehaviorInPlayMode;
        var background=InputSystem.settings.backgroundBehavior;
        var keyboard=InputSystem.AddDevice<Keyboard>("ControlHintEngineeringKeyboard");
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            InputSystem.EnableDevice(keyboard);
            foreach(var key in keys)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Task.Delay(150);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(150);
            }
        }
        finally{InputSystem.RemoveDevice(keyboard);InputSystem.settings.editorInputBehaviorInPlayMode=original;InputSystem.settings.backgroundBehavior=background;}
    }
    static void CheckText(TaskPlatformController o)
    {
        Canvas.ForceUpdateCanvases();
        foreach(var label in o.spatialView.instructions.GetComponentsInChildren<TMP_Text>())
        {
            if(!label.gameObject.activeInHierarchy || string.IsNullOrEmpty(label.text))continue;
            label.ForceMeshUpdate();
            Check(!label.isTextOverflowing,"text fits: "+label.name);
        }
    }
    public static async Task<string> Position()
    {
        var o=Owner();Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder+"validation.txt","Engineering keyboard/UI checks only. No physical or participant acceptance.\nSession: "+o.DataPath+"\n");
        Check(o.Engine.Phase==TaskPhase.Preparation && !o.Calibrated && !o.MatchChecked && !o.NonmatchChecked,"fresh preparation");
        o.Engine.Note("engineering_preview","controller-guidance-v1 UI/input validation; synthetic keyboard; no participant data",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        Check(File.ReadAllText(Path.Combine(o.DataPath,"session.json")).Contains("\"controlHintRevision\": \"controller-guidance-v1\""),"session records control hint revision");
        Check(o.presentation.preparationCards[0].status.text=="TRY NOW","left trigger is the first cue");CheckText(o);
        await Press(Key.Enter);
        Check(o.Engine.Phase==TaskPhase.Preparation,"Continue cannot bypass preparation checks");
        await Press(Key.C);
        Check(o.Calibrated && !o.MatchChecked && !o.NonmatchChecked,"C completes only seating check");
        Check(o.presentation.preparationCards[0].status.text=="READY" && o.presentation.preparationCards[0].control.text=="LT","completed trigger retains its glyph");
        Check(o.presentation.preparationCards[1].status.text=="TRY NOW" && o.presentation.preparationProgress.text=="1 / 3 controls ready","next cue and count update after real input");
        CheckText(o);Debug.Log("CONTROL HINT UI PASS: initial and seating states verified.");return "Position state verified.";
    }
    public static async Task<string> Ready()
    {
        var o=Owner();await Press(Key.B,Key.Enter);
        Check(o.NonmatchChecked && !o.MatchChecked && o.Engine.Phase==TaskPhase.Preparation,"B can be checked before A; missing A still blocks continuation");
        Check(o.presentation.preparationCards[1].status.text=="TRY NOW" && o.presentation.preparationCards[2].status.text=="READY","out-of-order completion keeps the correct next cue");
        await Press(Key.A);
        foreach(var card in o.presentation.preparationCards)Check(card.status.text=="READY","completed card stays READY");
        Check(o.presentation.preparationProgress.text=="3 / 3 controls ready","all three checks counted");
        Check(o.presentation.actionHint.text.Contains("Continue"),"existing continue gate renders its action");
        Check(o.presentation.preparationCards[1].control.text=="A" && o.presentation.preparationCards[2].control.text=="B","A/B mappings remain visible after completion");
        CheckText(o);Debug.Log("CONTROL HINT UI PASS: all-ready state and out-of-order input verified.");return "Ready state verified.";
    }
    public static async Task<string> Instructions()
    {
        var o=Owner();await Press(Key.Enter);
        Check(o.Engine.Phase==TaskPhase.Instructions,"Continue reaches practice instructions");
        Check(!o.presentation.setupCards.activeSelf && !o.presentation.preparationFooter.activeSelf,"preparation-only visuals hide on instructions");
        Check(o.presentation.matchKey.transform.parent.parent.gameObject.activeSelf && o.presentation.nonmatchKey.transform.parent.parent.gameObject.activeSelf,"compact A/B legend returns for instruction pages");
        Check(o.body.rectTransform.rect.height==245,"full instruction text height restored");CheckText(o);
        Debug.Log("CONTROL HINT UI PASS: instruction transition verified.");return "Instructions verified.";
    }
    public static async Task<string> Running()
    {
        var o=Owner();await Press(Key.Enter);
        Check(o.Engine.Phase==TaskPhase.Running,"practice starts with existing input");
        Check(!o.spatialView.instructions.enabled && o.spatialView.taskCanvas.enabled,"guidance canvas is hidden during trials; existing minimal HUD active");
        Check(!o.Engine.Current.scored,"first practice color remains observe-only");
        Check(o.SessionConfig.responseSeconds==4 && o.SessionConfig.trialSeconds==4.5f,"response timing preserved");
        Debug.Log("CONTROL HINT UI PASS: live practice and minimal HUD verified. Synthetic engineering evidence only.");return "Running verified.";
    }
}

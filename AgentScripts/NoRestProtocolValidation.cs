using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class NoRestProtocolValidation
{
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static async Task Press(Keyboard keyboard,Key key)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Task.Delay(100);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(100);
    }
    public static async Task<string> Run()
    {
        checks=0;
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        Check(Application.isPlaying && o?.Engine?.Phase==TaskPhase.Preparation,"fresh Play Mode preparation required");
        Check(o.Engine.Adaptive.Config.sessionType=="engineering","engineering session required");
        var events=new List<TaskEvent>();o.Engine.OnEvent+=events.Add;
        o.Engine.Note("engineering_preview","synthetic keyboard and transient signal-status fixtures; no participant or physical acceptance",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        var runtime=o.AdaptiveRuntime;
        var configField=typeof(AdaptiveRuntime).GetField("config",BindingFlags.Instance|BindingFlags.NonPublic);
        var replyField=typeof(AdaptiveRuntime).GetField("lastServiceReply",BindingFlags.Instance|BindingFlags.NonPublic);
        var statusProperty=typeof(AdaptiveRuntime).GetProperty("PhysiologyStatus");
        var readyProperty=typeof(AdaptiveMedia).GetProperty("Ready");bool originalReady=runtime.Media.Ready;
        var originalConfig=configField.GetValue(runtime);var originalReply=replyField.GetValue(runtime);var originalStatus=statusProperty.GetValue(runtime);
        try
        {
            // Only the gate's transient inputs are replaced; the recorded engine remains engineering.
            var fixture=JsonUtility.FromJson<AdaptiveTrainingConfig>(JsonUtility.ToJson(originalConfig));fixture.sessionType="participant";
            configField.SetValue(runtime,fixture);
            readyProperty.SetValue(runtime.Media,false);
            Check(!runtime.CanStart,"participant gate blocks unreviewed or missing media");
            readyProperty.SetValue(runtime.Media,true);
            replyField.SetValue(runtime,double.NegativeInfinity);
            Check(!runtime.CanStart,"participant gate blocks missing service");
            replyField.SetValue(runtime,Time.realtimeSinceStartupAsDouble);
            foreach(var status in new[]{new PhysiologyDisplayStatus{source="live",connection="waiting"},new PhysiologyDisplayStatus{source="replay",connection="receiving"}})
            {statusProperty.SetValue(runtime,status);Check(!runtime.CanStart,"participant gate blocks waiting or replay");}
            statusProperty.SetValue(runtime,new PhysiologyDisplayStatus{source="live",connection="receiving"});
            Check(runtime.Media.Ready && runtime.CanStart,"ready media and receiving live status permit participant gate");
        }
        finally{configField.SetValue(runtime,originalConfig);replyField.SetValue(runtime,originalReply);statusProperty.SetValue(runtime,originalStatus);readyProperty.SetValue(runtime.Media,originalReady);}
        var keyboard=InputSystem.AddDevice<Keyboard>("NoRestEngineeringKeyboard");
        var originalInputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
        var originalBackgroundBehavior=InputSystem.settings.backgroundBehavior;
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.EnableDevice(keyboard);
            UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            await Press(keyboard,Key.Enter);
            Check(o.Engine.Phase==TaskPhase.Preparation,"continue cannot skip preparation checks");
            await Press(keyboard,Key.C);await Press(keyboard,Key.A);await Press(keyboard,Key.Enter);
            Check(o.Calibrated && o.MatchChecked && !o.NonmatchChecked && o.Engine.Phase==TaskPhase.Preparation,"missing B still blocks continuation");
            await Press(keyboard,Key.B);await Press(keyboard,Key.Enter);
            Check(o.Engine.Phase==TaskPhase.Instructions && o.Engine.Adaptive.Stage=="practice","prepared input advances directly to practice instructions");
            Check(o.heading.text=="Practice" && !o.body.text.Contains("resting baseline"),"practice page rendered");
            await Press(keyboard,Key.Enter);
            Check(o.Engine.Phase==TaskPhase.Running && o.Engine.Practice,"practice starts without a timed rest");
            await Press(keyboard,Key.Enter);
            double deadline=Time.realtimeSinceStartupAsDouble+7;
            while(o.Engine.Phase==TaskPhase.Running && Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(50);
            Check(o.Engine.Phase==TaskPhase.Instructions && o.Engine.Adaptive.Stage=="calibration","practice skip respects trial boundary and enters calibration");
            Check(events.Where(e=>e.type=="protocol_stage").Select(e=>e.detail).SequenceEqual(new[]{"practice","practice","calibration"}),"practice instructions/start then calibration; no resting baseline");
            Check(events.Any(e=>e.type=="operator_recheck_acknowledged" && e.detail.Contains("sensors")),"operator sensor recheck remains logged");
        }
        finally{InputSystem.RemoveDevice(keyboard);InputSystem.settings.editorInputBehaviorInPlayMode=originalInputBehavior;InputSystem.settings.backgroundBehavior=originalBackgroundBehavior;o.Engine.OnEvent-=events.Add;}
        string result="PASS: "+checks+" live preparation/input/signal-gate checks; synthetic keyboard/status fixtures; no physical acceptance. Data: "+o.DataPath;
        File.WriteAllText("Evidence/NoRestProtocol/unity-live-validation.txt",result);
        Debug.Log(result);return result;
    }
}

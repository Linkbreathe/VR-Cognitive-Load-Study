using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using EvidenceVR;
using UnityEditor;
using UnityEngine;

public static class GinBioadaptiveTest
{
    [Serializable] sealed class Trial {public bool responded=false,correct=false;public double reactionTime=0;}
    static Trial[] ReadResponses()=>File.ReadAllLines("Evidence/GinModelTest/driver-responses.csv").Skip(1).Select(line=>{
        var fields=line.Split(',');return new Trial{responded=fields[0]=="1",correct=fields[1]=="1",reactionTime=double.Parse(fields[2],CultureInfo.InvariantCulture)};
    }).ToArray();
    public static string ValidateResponses()
    {
        var rows=ReadResponses();if(rows.Length!=96)throw new Exception("Expected unchanged 96-slot fixture");
        foreach(var row in rows)if(row.responded && (row.reactionTime<0 || row.reactionTime>=4))throw new Exception("Invalid response time");
        return "96 fixture responses parsed; all generated response times fit the four-second window";
    }
    static TaskPlatformController Owner()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(o==null || o.Engine==null || !o.Engine.IsDirectBioadaptiveTest || o.Engine.Adaptive.Config.participantCode!="SIM_GIN_TEST")
            throw new Exception("Only the SIM_GIN_TEST direct engineering assignment is allowed");
        return o;
    }
    public static string Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode before configuring the test");
        var path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
        Directory.CreateDirectory("Evidence/GinModelTest");
        var backup="Evidence/GinModelTest/editor-override-before.json";
        if(!File.Exists(backup))File.WriteAllText(backup,File.ReadAllText(path));
        var c=AdaptiveTrainingConfig.Personalized();c.operatorManaged=true;c.controllerUrl="http://127.0.0.1:8765";
        File.WriteAllText(path,JsonUtility.ToJson(c,true));
        return "Engineering bootstrap points to unified test service :8765; previous override backed up";
    }
    public static async Task<string> Exercise()
    {
        var o=Owner();var e=o.Engine;
        if(!o.AdaptiveRuntime.CanStart || !e.PersonalModelTestUsable || e.PersonalModelAdmitted)
            throw new Exception("Need acknowledged candidate, mapped signals and loaded media; no admission may be asserted");
        var rows=ReadResponses();
        e.Note("simulation_test_driver","generated responses; programmatic preparation advance; no physical input/device acceptance",Time.realtimeSinceStartupAsDouble,Time.frameCount);
        e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        if(e.Phase!=TaskPhase.Instructions || e.Adaptive.Stage!="bioadaptive")throw new Exception("Direct Bioadaptive entry failed");
        e.Advance(Time.realtimeSinceStartupAsDouble,Time.frameCount);
        int answered=-1;
        while(e.Results.Count<26)
        {
            if(o.Engine!=e || e.Phase!=TaskPhase.Running)throw new Exception("Test interrupted; no automatic resume");
            var current=e.CurrentResult;int i=e.Results.Count;var response=rows[i];
            double now=Time.realtimeSinceStartupAsDouble;
            if(e.Current.scored && response.responded && answered!=i && now>=current.plannedOnset+response.reactionTime)
            {
                e.Respond(response.correct?e.Current.expectedMatch:!e.Current.expectedMatch,now,Time.frameCount);answered=i;
            }
            await Task.Delay(40);
        }
        e.Abort(Time.realtimeSinceStartupAsDouble,Time.frameCount,"gin_simulation_smoke_test_complete");
        const string result="GIN BIOADAPTIVE PASS: candidate loaded; direct phase entered; 26 real-time slots with generated responses; stopped as aborted engineering test";
        File.WriteAllText("Evidence/GinModelTest/unity-exercise.txt",result);Debug.Log(result);return result;
    }
    public static string VerifyLoaded()
    {
        var o=Owner();var e=o.Engine;
        if(!o.AdaptiveRuntime.CanStart || !e.PersonalModelTestUsable || e.PersonalModelAdmitted)throw new Exception("Candidate not ready");
        var result="GIN MODEL LOADED: "+e.Adaptive.Config.testModelId+"; phase="+e.Phase+"; admitted=false; ready="+o.AdaptiveRuntime.CanStart;
        Debug.Log(result);File.WriteAllText("Evidence/GinModelTest/loaded.txt",result);return result;
    }
}

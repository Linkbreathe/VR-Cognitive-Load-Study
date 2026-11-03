using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EvidenceVR;
using UnityEngine;

public static class PersonalizedControlValidation
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static string Run()
    {
        checks = 0;
        Check(Application.isPlaying, "Run in Play Mode; synthetic engines remain isolated from the scene controller.");
        // JsonUtility cannot populate wrapper types from this ephemeral validation assembly.
        // Parse only the fixture index manually; deserialize each decision using the production type.
        var lines = File.ReadAllLines("Evidence/PersonalizedControlRules/unity-fixtures.tsv");
        foreach (string line in lines.Skip(1))
        {
            var fields = line.Split('\t');
            string name = fields[0]; int initialLevel = int.Parse(fields[1]), expectedLevel = int.Parse(fields[2]);
            var engine = new TaskEngine(new TaskConfig(), AdaptiveTrainingConfig.Personalized());
            Check(engine.Adaptive != null, "Personalized engine is enabled");
            var events = new List<TaskEvent>(); engine.OnEvent += events.Add;
            var stageMethod = typeof(TaskEngine).GetMethod("PersonalStage", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(stageMethod != null, "Personalized stage entry is available");
            stageMethod.Invoke(engine, new object[] { "bioadaptive", initialLevel, 0d, 0, TaskPhase.Instructions });
            engine.Advance(0, 0);
            for (int i = 1; i <= 25; i++) engine.Tick(i * 4.5, i);
            Check(engine.ProtocolCompletedTrials == 25, name + ": five-trial decision boundary");
            var decision = JsonUtility.FromJson<AdaptiveDecision>(File.ReadAllText("Evidence/PersonalizedControlRules/" + name + ".json"));
            Check(decision.configVersion == engine.Adaptive.Config.version && decision.level == engine.Adaptive.Level,
                name + ": unchanged Unity/Python wire contract");
            // Rebind only fixture identity fields to this isolated engine; preserve Python's action and basis.
            decision.epoch = engine.RollingEpoch; decision.evaluationId = engine.PersonalDecisionId;
            engine.Adaptive.Pending = decision; engine.CanApplyAdaptiveChange = () => false;
            engine.Tick(26 * 4.5, 26);
            Check(engine.Adaptive.Level == initialLevel && engine.Adaptive.Pending != null,
                name + ": active-media guard defers execution");
            engine.CanApplyAdaptiveChange = () => true; engine.Tick(27 * 4.5, 27);
            Check(engine.Adaptive.Level == expectedLevel && engine.Adaptive.Pending == null,
                name + ": expected ON/OFF/HOLD execution");
            Check(events.Any(e => e.type == "personal_execution_deferred"), name + ": deferral audited");
            Check(events.Any(e => e.type == "personal_execution"), name + ": execution audited");
        }
        string result = "CONTROL RULES V3 PASS: " + checks + " checks across " + (lines.Length - 1) +
            " synthetic Python decisions; policy=" + lines[0] + "; no physical or model qualification.";
        File.WriteAllText("Evidence/PersonalizedControlRules/unity-boundary-validation.txt", result);
        Debug.Log(result); return result;
    }
}

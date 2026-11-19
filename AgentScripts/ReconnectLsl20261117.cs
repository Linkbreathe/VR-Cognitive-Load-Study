using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EvidenceVR.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class ReconnectLsl20261117
{
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    static Task Invoke(LslSourceWindow window, string name) =>
        (Task)typeof(LslSourceWindow).GetMethod(name, Flags).Invoke(window, null);

    public static async Task<string> Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before source recovery.");
        var window = Resources.FindObjectsOfTypeAll<LslSourceWindow>().Single();
        File.Copy(LslSourceWindow.SelectionPath,
            Path.Combine(LslSourceWindow.ProjectRoot, "Evidence/LslIndexRecovery-20261117/lsl-selection.original.json"), false);
        await Invoke(window, "DisconnectBridgeAsync");
        await Invoke(window, "ScanAsync");
        var inventory = (LslDiscoveredStream[])typeof(LslSourceWindow).GetField("discovered", Flags).GetValue(window);
        var selected = inventory.Single(s => s.name == "104209-1083_eeg" && s.source_id == "mindtooth_eeg"
            && s.stream_type == "EEG" && s.channel_count == 8 && s.sample_rate == 250
            && s.channel_labels.SequenceEqual(new[] { "AFz", "AF3", "AF4", "AF7", "AF8", "Pz", "P3", "P4" }));
        window.SelectSource(selected);
        await Invoke(window, "StartBridgeAsync");
        return "Reconnected selected outlet " + selected.uid;
    }

    public static string Status()
    {
        var window = Resources.FindObjectsOfTypeAll<LslSourceWindow>().Single();
        return window.rootVisualElement.Q<Label>("serviceStatus").text;
    }
}

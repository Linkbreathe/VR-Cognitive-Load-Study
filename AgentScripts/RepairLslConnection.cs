using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using EvidenceVR.Editor;

public static class RepairLslConnection
{
    public static string Status()
    {
        var window = Resources.FindObjectsOfTypeAll<LslSourceWindow>().Single();
        return (string)typeof(LslSourceWindow).GetField("connection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
    }

    public static async Task<string> ReconnectSelectedSource()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("End Play Mode before reconnecting a source.");
        var window = Resources.FindObjectsOfTypeAll<LslSourceWindow>().Single();
        var type = typeof(LslSourceWindow);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var previous = (LslDiscoveredStream)type.GetField("selected", flags).GetValue(window);
        if (previous == null) throw new InvalidOperationException("No source was selected.");
        LslSourceWindow.StopBridge();
        await (Task)type.GetMethod("ScanAsync", flags).Invoke(window, null);
        var inventory = (LslDiscoveredStream[])type.GetField("discovered", flags).GetValue(window);
        var current = inventory.Single(s => s.name == previous.name && s.source_id == previous.source_id &&
            s.stream_type == previous.stream_type && s.channel_count == previous.channel_count &&
            s.channel_format == previous.channel_format && Math.Abs(s.sample_rate - previous.sample_rate) < .01);
        window.SelectSource(current);
        window.SaveSelection();
        await (Task)type.GetMethod("StartBridgeAsync", flags).Invoke(window, null);
        Debug.Log("LSL CONNECTION REPAIR: refreshed exact source " + current.name + " UID=" + current.uid);
        return current.name + " | " + current.uid;
    }
}

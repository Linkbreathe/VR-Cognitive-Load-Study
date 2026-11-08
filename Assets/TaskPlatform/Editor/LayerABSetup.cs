using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EvidenceVR.Editor
{
    public static class LayerABSetup
    {
        public static void Choose()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before loading a study allocation.");
            string chosen=EditorUtility.OpenFilePanel("Load an allocated A/B configuration",Application.dataPath,"json");
            if(!string.IsNullOrEmpty(chosen))Install(chosen);
        }
        public static string Install(string source)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            string text=File.ReadAllText(source);var config=JsonUtility.FromJson<AdaptiveTrainingConfig>(text);config.Validate();
            if(!config.IsLayerAB)throw new ArgumentException("Choose an allocated layer A/B configuration.");
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            string backup=path+".before-layer-ab-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            if(File.Exists(path))File.Copy(path,backup);else File.WriteAllText(backup,"ABSENT");
            Directory.CreateDirectory("UserSettings");File.WriteAllText("UserSettings/layer-ab-restore.txt",backup);
            File.WriteAllText(path,text);
            Debug.Log("Layer A/B allocation loaded. Start the separate service, select EvidenceVR_TaskEvents in LabRecorder, then enter Play. No session was started. Backup: "+backup);
            return path;
        }
        public static void Restore()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            string path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            string backup=File.ReadAllText("UserSettings/layer-ab-restore.txt");
            if(!backup.StartsWith(path+".before-layer-ab-",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid restore pointer.");
            string old=File.ReadAllText(backup);
            if(old=="ABSENT")File.Delete(path);else File.WriteAllText(path,old);
            Debug.Log("Restored the configuration from before A/B study setup. Recording folders were retained.");
        }
    }
}

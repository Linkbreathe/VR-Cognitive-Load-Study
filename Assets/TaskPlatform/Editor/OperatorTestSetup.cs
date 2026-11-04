using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EvidenceVR.Editor
{
    public static class OperatorTestSetup
    {
        [MenuItem("Evidence VR/Test sessions/Enable operator-managed engineering tests")]
        public static string Enable()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before changing startup mode.");
            var path=Path.Combine(Application.persistentDataPath,"adaptive-training-config.json");
            var config=AdaptiveTrainingConfig.Personalized();config.operatorManaged=true;
            if(File.Exists(path))
            {
                config.controllerUrl=JsonUtility.FromJson<AdaptiveTrainingConfig>(File.ReadAllText(path)).controllerUrl;
                File.Copy(path,path+".before-model-reuse-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            }
            config.Validate();File.WriteAllText(path,JsonUtility.ToJson(config,true));
            Debug.Log("Operator-managed engineering tests enabled. Open "+config.controllerUrl+"/tests. Bootstrap: "+path);
            return path;
        }
    }
}

using UnityEngine;

namespace EvidenceVR
{
    // Scene identity is independent of the task protocol and personal-model version.
    public sealed class LabEnvironmentIdentity : MonoBehaviour
    {
        public const string Revision="warm-study-v2-shelf-clearance";
        public string revision=Revision;
        public string assetManifest="lab-environment-assets-v1";
        void Start()=>Debug.Log("Laboratory environment: "+revision+"; static furnishings; stage-specific lighting; source manifest="+assetManifest);
        public static string CurrentRevision
        {
            get{var identity=Object.FindFirstObjectByType<LabEnvironmentIdentity>();return identity==null?"quiet-lab-legacy":identity.revision;}
        }
    }
}

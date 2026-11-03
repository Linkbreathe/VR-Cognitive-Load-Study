using TMPro;
using UnityEngine;
using System.Linq;

namespace EvidenceVR
{
    // Session identity and source provenance remain visible independently of task feedback.
    public sealed class ExperimentStatusView : MonoBehaviour
    {
        public const string Revision = "mode-status-v5-blinded-personal-protocol";
        TaskPlatformController owner;
        TMP_Text hud, eyeHud;
        string lastRecorded;
        public string DisplayText { get; private set; }

        public void Bind(TaskPlatformController controller)
        {
            owner=controller;lastRecorded=null;
            if(hud==null)
            {
                hud=Instantiate(owner.spatialView.stage,owner.spatialView.taskCanvas.transform);
                hud.name="ExperimentModeStatus";
                hud.rectTransform.anchoredPosition=new Vector2(0,600);
                hud.rectTransform.sizeDelta=new Vector2(1260,44);
                hud.fontSize=24;hud.alignment=TextAlignmentOptions.Center;
                hud.textWrappingMode=TextWrappingModes.NoWrap;hud.raycastTarget=false;
                hud.color=LabPalette.Text;
                hud.richText=false;hud.enableAutoSizing=true;hud.fontSizeMin=18;hud.fontSizeMax=24;
                eyeHud=Instantiate(hud,owner.spatialView.taskCanvas.transform);
                eyeHud.name="EngineeringEyeStatus";
                eyeHud.rectTransform.anchoredPosition=new Vector2(0,648);
            }
            var stageSign=owner.spatialView.stage.transform.parent as RectTransform;
            if(stageSign!=null)stageSign.anchoredPosition=new Vector2(0,538);
            owner.badge.rectTransform.anchoredPosition=new Vector2(0,410);
            owner.badge.rectTransform.sizeDelta=new Vector2(1160,45);
            owner.badge.fontSize=24;owner.badge.textWrappingMode=TextWrappingModes.NoWrap;
            owner.badge.richText=false;owner.badge.enableAutoSizing=true;owner.badge.fontSizeMin=16;owner.badge.fontSizeMax=24;
            owner.heading.rectTransform.anchoredPosition=new Vector2(0,350);
            owner.heading.rectTransform.sizeDelta=new Vector2(1160,85);
            var rule=owner.spatialView.instructions.transform.Find("HeaderRule") as RectTransform;
            if(rule!=null)rule.anchoredPosition=new Vector2(0,290);
            Refresh();
        }

        public static string ControllerText(string mode,string basis,bool connected)
        {
            string control=mode=="ACTIVE"?"ACTIVE - Auto ON":mode=="SHADOW"?"SHADOW - Auto OFF (advice only)":"OFF - Auto OFF (manual)";
            string link=connected?"Online":mode=="ACTIVE"?"Offline / HOLD":"Offline";
            return "Controller: "+control+" | "+link+" | Basis: "+(basis=="physiological"?"Behavior + physiology":"Behavior");
        }

        public static string PhysiologyText(PhysiologyDisplayStatus status,bool connected)
        {
            return "LSL: "+SourceLabel(status,false)+" | "+ConnectionLabel(status,connected)+
                " | Provenance: "+(status?.source??"unknown")+" | Identity: "+JsonUtility.ToJson(status??new PhysiologyDisplayStatus());
        }

        public static string SourceLabel(PhysiologyDisplayStatus status,bool compact=true)
        {
            string name=status?.sourceName;
            if(string.IsNullOrWhiteSpace(name) && status?.streams!=null)
                name=string.Join(" + ",status.streams.Where(s=>s!=null).Select(s=>string.IsNullOrWhiteSpace(s.name)?s.sourceId:s.name).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());
            if(string.IsNullOrWhiteSpace(name))return status?.source=="none"?"NO SOURCE":"SOURCE UNKNOWN";
            name=new string(name.Select(c=>char.IsControl(c)?' ':c).ToArray()).Trim();
            return compact && name.Length>44?name.Substring(0,41)+"...":name;
        }

        static string ConnectionLabel(PhysiologyDisplayStatus status,bool connected)
        {
            if(!connected)return "SERVICE OFFLINE";
            if(status==null)return "STATUS UNKNOWN";
            return status.connection=="receiving"?"RECEIVING":status.connection=="idle"?"IDLE":status.connection=="partial"?"PARTIAL":status.connection=="waiting"?"WAITING":"LOST";
        }

        public static string CompactStatus(string mode,string basis,PhysiologyDisplayStatus status,bool connected)
        {
            string control=mode=="ACTIVE"?(connected?"AUTO ON: "+(basis=="physiological"?"BIO":"BEHAVIOR"):"AUTO ON / HOLD"):
                mode=="SHADOW"?"SHADOW / AUTO OFF":"AUTO OFF";
            return "STAGE 2 | "+control+" | LSL: "+SourceLabel(status)+" | "+ConnectionLabel(status,connected);
        }

        public void Refresh()
        {
            if(owner?.Engine==null || hud==null)return;
            var engine=owner.Engine;
            bool adaptive=engine.Adaptive!=null;
            string mode=adaptive?"STAGE 2 - ADAPTIVE TRAINING":"STAGE 1 - FIXED TASK";
            string phase=engine.Phase==TaskPhase.Running || engine.Phase==TaskPhase.Instructions?
                (engine.Practice?"Practice":adaptive?engine.Adaptive.Stage:"Main block"):engine.Phase.ToString();
            var runtime=owner.AdaptiveRuntime;
            bool connected=runtime!=null && runtime.ServiceConnected;
            string controller=adaptive?ControllerText(engine.Adaptive.Config.mode,engine.Adaptive.Config.controlBasis,connected):
                "Controller: OFF - Auto OFF | Fixed schedule";
            string physiology=adaptive?PhysiologyText(runtime?.PhysiologyStatus,connected):"Physiology: NOT USED IN STAGE 1";
            DisplayText=mode+"\n"+controller+"\n"+physiology;
            string compact=adaptive?CompactStatus(engine.Adaptive.Config.mode,engine.Adaptive.Config.controlBasis,runtime?.PhysiologyStatus,connected):
                "STAGE 1 | AUTO OFF | PHYSIO NOT USED";
            if(engine.IsPersonalized && engine.Adaptive.Config.sessionType=="participant")
                compact="STUDY | "+engine.ProtocolStageLabel;
            hud.text=compact;
            if(eyeHud!=null)
            {
                eyeHud.enabled=runtime?.GazeRay?.RayEnabled==true;
                eyeHud.text=eyeHud.enabled?"ENGINEERING EYE RAY | "+runtime.GazeTrackingStatus+" | "+(runtime.GazeRay.Visible?"VISIBLE":"HIDDEN"):"";
            }
            owner.badge.text=compact+(runtime?.GazeRay?.RayEnabled==true?" | EYE: "+runtime.GazeTrackingStatus:"")+
                (engine.Phase==TaskPhase.Paused?" | Paused":"");
            if(engine.Phase==TaskPhase.Running && engine.Current!=null)
            {
                string hint=!engine.Current.scored?"Observe only":!adaptive && engine.Mode==TaskMode.Easy?
                    "Match "+TaskPlatformController.ColorNames[owner.SessionConfig.targetColor]:"";
                if(hint.Length>0)owner.spatialView.stage.text+=" | "+hint;
                owner.spatialView.rule.text="";
            }
            if(DisplayText!=lastRecorded)
            {
                lastRecorded=DisplayText;
                owner.Engine.Note("mode_status_display",Revision+";"+DisplayText.Replace('\n',';'),Time.realtimeSinceStartupAsDouble,Time.frameCount);
                Debug.Log("Experiment status: "+DisplayText.Replace('\n',';'));
            }
        }
    }

    [System.Serializable]
    public sealed class PhysiologyDisplayStatus
    {
        public string source,connection,sourceName;
        public PhysiologySourceIdentity[] streams;
    }
    [System.Serializable]
    public sealed class PhysiologySourceIdentity
    {
        public string name,sourceId,kind,origin,uid,streamType;
    }
}

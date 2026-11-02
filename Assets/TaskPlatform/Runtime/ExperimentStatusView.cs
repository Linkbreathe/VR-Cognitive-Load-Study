using TMPro;
using UnityEngine;

namespace EvidenceVR
{
    // Session identity and source provenance remain visible independently of task feedback.
    public sealed class ExperimentStatusView : MonoBehaviour
    {
        public const string Revision = "mode-status-v2-compact";
        TaskPlatformController owner;
        TMP_Text hud;
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
            }
            var stageSign=owner.spatialView.stage.transform.parent as RectTransform;
            if(stageSign!=null)stageSign.anchoredPosition=new Vector2(0,538);
            owner.badge.rectTransform.anchoredPosition=new Vector2(0,410);
            owner.badge.rectTransform.sizeDelta=new Vector2(1160,45);
            owner.badge.fontSize=24;owner.badge.textWrappingMode=TextWrappingModes.NoWrap;
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
            if(!connected)
                return "Physiology: SERVICE OFFLINE"+(status?.source=="recorded_replay"?" | Last source: RECORDED REPLAY":"");
            if(status==null)return "Physiology: UNKNOWN - source status unavailable";
            string source=status.source=="recorded_replay"?"RECORDED REPLAY":status.source=="synthetic"?"SIMULATED DATA":
                status.source=="live"?"LIVE STREAM (not validated)":status.source=="mixed"?"MIXED SOURCES - includes simulation":
                status.source=="none"?"NO SOURCE CONFIGURED":"UNKNOWN SOURCE";
            string state=status.connection=="receiving"?"Receiving":status.connection=="partial"?"Partial / signal missing":
                status.connection=="waiting"?"Waiting for samples":"No current samples";
            return "Physiology: "+source+(status.source=="none"?"":" | "+state);
        }

        public static string CompactStatus(string mode,string basis,PhysiologyDisplayStatus status,bool connected)
        {
            string control=mode=="ACTIVE"?(connected?"AUTO ON: "+(basis=="physiological"?"BIO":"BEHAVIOR"):"AUTO ON / HOLD"):
                mode=="SHADOW"?"SHADOW / AUTO OFF":"AUTO OFF";
            string source=status?.source=="recorded_replay"?"REPLAY":status?.source=="synthetic"?"SIMULATED":
                status?.source=="live"?"LIVE (UNVALIDATED)":status?.source=="mixed"?"MIXED / SIM":status?.source=="none"?"NO SOURCE":"UNKNOWN";
            if(!connected)source=status?.source=="recorded_replay"?"REPLAY / OFFLINE":"PHYSIO OFFLINE";
            else if(status!=null && status.source!="none" && status.connection!="receiving")
                source+=status.connection=="waiting"?" / WAIT":status.connection=="partial"?" / PARTIAL":" / LOST";
            return "STAGE 2 | "+control+" | "+source;
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
            hud.text=compact;
            owner.badge.text=compact+(engine.Phase==TaskPhase.Paused?" | Paused":"");
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
        public string source,connection;
    }
}

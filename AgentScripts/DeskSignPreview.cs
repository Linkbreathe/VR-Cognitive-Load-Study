using System;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEngine;
using TMPro;

public static class DeskSignPreview
{
    public static string Validate()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var left=(RectTransform)o.spatialView.timer.transform.parent;
        var right=(RectTransform)o.AdaptiveRuntime.PersonalProgressValue.transform.parent;
        int checks=0;
        Action<bool,string> check=(pass,message)=>{checks++;if(!pass)throw new Exception(message);};
        foreach(var card in new[]{left,right})
        {
            check(Mathf.Abs(card.position.y-.86f)<.001f,"Desk-fixed sign height");
            var frame=card.Find("DeskSignFrame");check(frame!=null,"Desk sign exists");
            var foot=(RectTransform)frame.Find("Foot");var corners=new Vector3[4];foot.GetWorldCorners(corners);
            check(Mathf.Abs(corners.Min(x=>x.y)-SpatialTaskView.TabletopHeight)<.001f,"Base rests on tabletop");
            card.GetWorldCorners(corners);
            foreach(var p in corners){var v=Camera.main.WorldToViewportPoint(p);check(v.z>0 && v.x>0 && v.x<1 && v.y>0 && v.y<1,"Sign is inside camera view");}
            foreach(var text in card.GetComponentsInChildren<TMP_Text>())
            {text.ForceMeshUpdate();check(!text.isTextOverflowing,"Text fits: "+text.name);check(!text.raycastTarget,"Passive sign text");}
            check(card.GetComponentsInChildren<UnityEngine.UI.Image>().All(i=>!i.raycastTarget),"Passive sign surfaces");
        }
        check(Mathf.Abs(Vector3.Distance(left.position,right.position)-.8f)<.001f,"Signs are 80 cm apart");
        string metadata=File.ReadAllText(Path.Combine(o.DataPath,"session.json"));
        check(metadata.Contains("personalized-lab-v2.5-desk-signs") && metadata.Contains("trial-progress-v2-desk-signs"),"Desk-sign revisions recorded");
        // Exercise a seated re-placement without moving the desk, then restore the task anchor.
        var parent=left.parent;var original=parent.position;
        try
        {
            parent.position+=new Vector3(0,.2f,0);o.AdaptiveRuntime.Render();
            check(Mathf.Abs(left.position.y-.86f)<.001f && Mathf.Abs(right.position.y-.86f)<.001f,"Signs stay on desk when anchor height changes");
        }
        finally{parent.position=original;o.AdaptiveRuntime.Render();}
        string result="DESK SIGNS PASS: "+checks+" geometry/text/metadata checks; 0.80 m separation; 0.65 m tabletop; synthetic Editor only.";
        File.WriteAllText("Evidence/DeskSigns/validation.txt",result);Debug.Log(result);return result;
    }
    public static string Inspect()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        var v=o.spatialView;var card=(RectTransform)v.timer.transform.parent;
        string result="canvas="+v.taskCanvas.transform.position.ToString("F4")+";scale="+v.taskCanvas.transform.lossyScale.ToString("F6")+
            ";card="+card.position.ToString("F4")+";rect="+card.rect+";table="+v.bench.Find("WorkSurface").position.ToString("F4")+
            ";sphere="+o.stimulus.transform.position.ToString("F4")+";camera="+Camera.main.transform.position.ToString("F4");
        foreach(var t in card.GetComponentsInChildren<TMP_Text>(true))
            result+="\n"+t.name+" pos="+t.rectTransform.anchoredPosition+" size="+t.rectTransform.sizeDelta+" anchor="+t.rectTransform.anchorMin+"/"+t.rectTransform.anchorMax+" font="+t.fontSize+" outline="+t.outlineWidth;
        Directory.CreateDirectory("Evidence/DeskSigns");File.WriteAllText("Evidence/DeskSigns/inspection.txt",result);return result;
    }
}

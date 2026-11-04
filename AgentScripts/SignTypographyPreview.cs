using System;
using System.IO;
using EvidenceVR;
using TMPro;
using UnityEngine;

public static class SignTypographyPreview
{
    public static string InspectFit()
    {
        var o=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        int checks=0;
        Action<TMP_Text,string[]> fit=(text,values)=>
        {
            string previous=text.text;
            try{foreach(string value in values){text.text=value;text.ForceMeshUpdate();checks++;if(text.isTextOverflowing)throw new Exception("Text overflow: "+value);}}
            finally{text.text=previous;text.ForceMeshUpdate();}
        };
        fit(o.spatialView.timer,new[]{"4.00","3.50","0.00"});
        fit(o.spatialView.timerLabel,new[]{"Correct","Incorrect","No response","Next"});
        fit(o.AdaptiveRuntime.PersonalProgressValue,new[]{"0/12","2/14","24/96","95/96","96/96"});
        fit(o.AdaptiveRuntime.PersonalProgressLabel,new[]{"Completed","Block 4/4","Extra block 2/2"});
        foreach(var text in o.spatialView.taskCanvas.GetComponentsInChildren<TMP_Text>())
            if(text.name=="Heading")fit(text,new[]{text.text});
        string result="SIGN TYPOGRAPHY PASS: "+checks+" text-fit cases; font="+o.spatialView.timer.font.name+
            "; number="+o.spatialView.timer.fontSize+" "+o.spatialView.timer.fontStyle+
            "; caption="+o.AdaptiveRuntime.PersonalProgressLabel.fontSize+"; outline="+o.spatialView.timer.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth);
        File.WriteAllText("Evidence/SignTypography/text-fit.txt",result);Debug.Log(result);return result;
    }
}

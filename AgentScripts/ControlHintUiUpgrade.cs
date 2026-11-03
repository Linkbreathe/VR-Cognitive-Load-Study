using System;
using System.IO;
using EvidenceVR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ControlHintUiUpgrade
{
    static Color C(string value)=>LabPalette.Hex(value);
    static TMP_FontAsset font;
    static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var found=parent.Find(name);
        var rect=found as RectTransform;
        if(found!=null && rect==null)throw new InvalidOperationException(name+" must be a RectTransform.");
        if(rect==null){rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);}
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;
        return rect;
    }
    static RoundedSurface Surface(Transform parent,string name,Vector2 position,Vector2 size,string color,float radius)
    {
        var rect=Rect(parent,name,position,size);
        var surface=rect.GetComponent<RoundedSurface>()??rect.gameObject.AddComponent<RoundedSurface>();
        surface.radius=radius;surface.color=C(color);surface.raycastTarget=false;surface.SetAllDirty();return surface;
    }
    static TMP_Text Label(Transform parent,string name,string value,Vector2 position,Vector2 size,float fontSize,string color)
    {
        var rect=Rect(parent,name,position,size);
        var label=rect.GetComponent<TextMeshProUGUI>()??rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font=font;label.text=value;label.fontSize=fontSize;label.color=C(color);
        label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.enableAutoSizing=false;
        label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Overflow;return label;
    }
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
        if(owner?.presentation?.setupCards==null)throw new InvalidOperationException("Open the existing task scene.");
        if(owner.gameObject.scene.isDirty)throw new InvalidOperationException("The scene has unsaved edits; save them before applying this upgrade.");
        var view=owner.presentation;var root=owner.heading.canvas.transform;font=owner.heading.font;
        // Update the existing instruction surface. The separate running HUD is untouched.
        root.Find("PanelFace").GetComponent<RoundedSurface>().color=C("30393D");
        root.Find("PanelEdge").GetComponent<RoundedSurface>().color=C("8B9288");
        view.preparationCards=new LabPresentation.PreparationCard[3];
        for(int i=0;i<3;i++)
        {
            var edge=Surface(view.setupCards.transform,"Step"+i,new Vector2((i-1)*370,-15),new Vector2(350,300),i==0?"E3BA7D":"596367",24);
            var face=Surface(edge.transform,"Face",Vector2.zero,new Vector2(346,296),i==0?"454944":"394246",22);
            face.transform.SetAsFirstSibling();
            Label(edge.transform,"StepNumber","0"+(i+1),new Vector2(-135,115),new Vector2(45,34),22,"C7CDC6");
            var status=Surface(edge.transform,"StatePill",new Vector2(64,115),new Vector2(146,32),i==0?"E3BA7D":"485358",16);
            var statusText=Label(status.transform,"State",i==0?"TRY NOW":"TO CHECK",Vector2.zero,new Vector2(140,30),19,i==0?"26332F":"D7DFDC");
            var diagram=Rect(edge.transform,"ControlIllustration",new Vector2(0,29),new Vector2(170,104));
            TMP_Text control;
            if(i==0)
            {
                // Side-view trigger schematic, deliberately not a hardware model.
                var grip=Surface(diagram,"Grip",new Vector2(-17,-6),new Vector2(55,87),"697674",25);
                grip.rectTransform.localEulerAngles=new Vector3(0,0,-14);
                Surface(diagram,"Top",new Vector2(-23,36),new Vector2(76,29),"98A59D",14);
                var trigger=Surface(diagram,"Trigger",new Vector2(20,24),new Vector2(29,51),"E3BA7D",12);
                trigger.rectTransform.localEulerAngles=new Vector3(0,0,-22);
                Surface(diagram,"Leader",new Vector2(46,19),new Vector2(25,2),"E3BA7D",0);
                control=Label(diagram,"Control","LT",new Vector2(75,19),new Vector2(45,40),27,"F5F0E6");
            }
            else
            {
                Surface(diagram,"ButtonWell",Vector2.zero,new Vector2(110,100),"2B3438",40);
                var disc=Surface(diagram,"Button",Vector2.zero,new Vector2(74,74),"ECE6D9",37);
                control=Label(disc.transform,"Control",i==1?"A":"B",Vector2.zero,new Vector2(68,70),44,"2B3438");
                control.fontStyle=FontStyles.Bold;
            }
            var title=Label(edge.transform,"Label",i==0?"Place your view":i==1?"Match":"Non-match",new Vector2(0,-52),new Vector2(320,45),31,"F5F0E6");
            view.setupLabels[i]=title;
            var hand=Label(edge.transform,"Hand",i==0?"LEFT TRIGGER":"RIGHT CONTROLLER",new Vector2(0,-93),new Vector2(320,30),21,"CFD6D0");
            var keyboard=Label(edge.transform,"Keyboard",i==0?"Keyboard: C":i==1?"Keyboard: A":"Keyboard: B",new Vector2(0,-126),new Vector2(320,27),19,"B5C0BC");
            view.preparationCards[i]=new LabPresentation.PreparationCard{edge=edge,face=face,statusBackground=status,status=statusText,control=control,hand=hand,keyboard=keyboard};
        }
        var footer=Rect(root,"PreparationFooter",Vector2.zero,new Vector2(1100,100));
        view.preparationFooter=footer.gameObject;
        view.preparationProgress=Label(footer,"Progress","0 / 3 controls ready",new Vector2(0,-361),new Vector2(1060,46),30,"F5F0E6");
        Label(footer,"Note","Use the physical buttons on your controllers.",new Vector2(0,-407),new Vector2(1060,35),22,"BFCBC6");
        foreach(var name in new[]{"MatchLegend","NonmatchLegend"})
        {
            var legend=root.Find(name);legend.GetComponent<RoundedSurface>().color=C("394246");
            var disc=legend.Find("KeyDisc").GetComponent<RoundedSurface>();disc.color=C("ECE6D9");
            disc.GetComponentInChildren<TMP_Text>().color=C("2B3438");
            legend.gameObject.SetActive(false);
        }
        owner.body.rectTransform.anchoredPosition=new Vector2(0,215);owner.body.rectTransform.sizeDelta=new Vector2(1100,110);
        owner.body.text="Sit comfortably and look straight ahead.\nTry each control once to get ready.";
        view.pauseHint.text="SEATED TASK  /  NO MOVEMENT REQUIRED";
        EditorUtility.SetDirty(view);EditorUtility.SetDirty(owner);
        EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);EditorSceneManager.SaveScene(owner.gameObject.scene);
        AssetDatabase.SaveAssets();
        string result="Controller guidance installed: "+LabPresentation.ControlHintRevision+"; three stateful cards; existing task/input/HUD unchanged.";
        Debug.Log(result);return result;
    }
}

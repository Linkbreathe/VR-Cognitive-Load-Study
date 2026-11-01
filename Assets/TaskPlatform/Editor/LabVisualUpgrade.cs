using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvidenceVR.Editor
{
    public static class LabVisualUpgrade
    {
        const string MaterialRoot="Assets/TaskPlatform/Materials/";
        [MenuItem("Evidence VR/Apply Quiet Lab Presentation")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before applying presentation.");
            var c=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
            if(c==null) throw new InvalidOperationException("Open NBackExperiment first.");
            if(c.gameObject.scene.isDirty) throw new InvalidOperationException("Save the scene before applying presentation.");
            var room=GameObject.Find("NeutralRoom").transform;
            Material wall=Mat("RoomGrey",LabPalette.Wall), floor=Mat("FloorGrey",LabPalette.Floor), trim=Mat("LabTrim",LabPalette.Trim), ceiling=Mat("LabCeiling",LabPalette.Ceiling);
            room.Find("Floor").GetComponent<Renderer>().sharedMaterial=floor;
            room.Find("Ceiling").GetComponent<Renderer>().sharedMaterial=ceiling;
            foreach(var name in new[]{"FrontWall","BackWall","LeftWall","RightWall"}) room.Find(name).GetComponent<Renderer>().sharedMaterial=wall;
            var detail=Child(room,"ArchitecturalDetails");
            Box(detail,"FrontBaseboard",new Vector3(0,.08f,2.925f),new Vector3(5.9f,.16f,.025f),trim);
            Box(detail,"BackBaseboard",new Vector3(0,.08f,-2.925f),new Vector3(5.9f,.16f,.025f),trim);
            Box(detail,"LeftBaseboard",new Vector3(-2.925f,.08f,0),new Vector3(.025f,.16f,5.9f),trim);
            Box(detail,"RightBaseboard",new Vector3(2.925f,.08f,0),new Vector3(.025f,.16f,5.9f),trim);
            Box(detail,"FrontUpperReveal",new Vector3(0,2.75f,2.925f),new Vector3(5.9f,.035f,.025f),trim);
            for(int i=-1;i<=1;i++)
            {
                Box(detail,"FrontPanelJoint"+i,new Vector3(i*1.8f,1.5f,2.93f),new Vector3(.009f,2.5f,.009f),trim);
                Box(detail,"FloorJoint"+i,new Vector3(i*1.8f,.004f,0),new Vector3(.006f,.002f,5.8f),trim);
            }
            Box(detail,"OverheadDiffusePanel",new Vector3(0,2.92f,.6f),new Vector3(2.4f,.025f,.8f),Mat("CeilingDiffuser",LabPalette.Text,true));
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=LabPalette.AmbientSky; RenderSettings.ambientEquatorColor=LabPalette.Wall; RenderSettings.ambientGroundColor=LabPalette.AmbientGround;
            var light=GameObject.Find("StableSoftLight").GetComponent<Light>(); light.color=LabPalette.KeyLight; light.intensity=.7f; light.shadows=LightShadows.None; light.transform.rotation=Quaternion.Euler(42,-28,0);
            var canvas=c.heading.canvas; canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(1280,940);
            var t=canvas.transform;
            Surface(t,"PanelEdge",Vector2.zero,new Vector2(1280,940),LabPalette.Edge,38).transform.SetAsFirstSibling();
            var background=Surface(t,"PanelFace",Vector2.zero,new Vector2(1274,934),LabPalette.Panel,35); background.transform.SetSiblingIndex(1);
            Surface(t,"HeaderRule",new Vector2(0,290),new Vector2(1140,1.5f),LabPalette.Edge,0);
            Surface(t,"FooterRule",new Vector2(0,-310),new Vector2(1140,1.5f),LabPalette.Edge,0);
            SetText(c.badge,new Vector2(0,410),new Vector2(1140,45),26,LabPalette.Text);
            SetText(c.heading,new Vector2(0,350),new Vector2(1160,85),44,LabPalette.Text);
            SetText(c.body,new Vector2(0,125),new Vector2(1100,245),30,LabPalette.Text);
            SetText(c.footer,new Vector2(0,-170),new Vector2(1100,55),26,LabPalette.Text);
            var v=c.GetComponent<LabPresentation>(); if(v==null) v=c.gameObject.AddComponent<LabPresentation>();
            v.controller=c; c.presentation=v;
            v.actionHint=Label(t,"PrimaryAction",new Vector2(0,-255),new Vector2(1130,60),26,c.heading.font,LabPalette.Accent);
            v.pauseHint=Label(t,"SecondaryAction",new Vector2(0,-440),new Vector2(1100,35),18,c.heading.font,LabPalette.Muted);
            v.matchKey=Answer(t,"MatchLegend",-275,"MATCH",c.heading.font);
            v.nonmatchKey=Answer(t,"NonmatchLegend",275,"NON-MATCH",c.heading.font);
            v.setupCards=Child(t,"PreparationSteps").gameObject; v.setupLabels=new TMP_Text[3];
            for(int i=0;i<3;i++)
            {
                var card=Surface(v.setupCards.transform,"Step"+i,new Vector2((i-1)*365,-80),new Vector2(340,150),LabPalette.Inset,18);
                v.setupLabels[i]=Label(card.transform,"Label",Vector2.zero,new Vector2(320,120),21,c.heading.font,LabPalette.Muted);
                v.setupLabels[i].text=(i==0 ? "01  POSITION\nLEFT TRIGGER / C" : i==1 ? "02  MATCH\nA" : "03  NON-MATCH\nB");
            }
            v.exampleCards=Child(t,"TwoBackExample").gameObject;
            for(int i=0;i<3;i++)
            {
                var card=Surface(v.exampleCards.transform,"Color"+i,new Vector2((i-1)*245,-25),new Vector2(180,130),LabPalette.Inset,18);
                Surface(card.transform,"ColorDot",new Vector2(0,15),new Vector2(52,52),LabPalette.Stimuli[i==1 ? 1 : 0],26);
                var label=Label(card.transform,"Label",new Vector2(0,-40),new Vector2(160,32),22,c.heading.font,LabPalette.Muted); label.text="#"+(i+1)+"  "+(i==1 ? "ORANGE" : "BLUE");
                if(i<2) { var arrow=Label(v.exampleCards.transform,"Arrow"+i,new Vector2(i==0 ? -122 : 122,-25),new Vector2(40,50),28,c.heading.font,LabPalette.Muted); arrow.text=">"; }
            }
            v.exampleCaption=Label(v.exampleCards.transform,"ExampleCaption",new Vector2(0,-150),new Vector2(1120,90),24,c.heading.font,LabPalette.Muted);
            v.exampleCards.SetActive(false);
            c.badge.text="EVIDENCE  /  TASK LAB"; c.heading.text="Settle in. Get ready.";
            c.body.text="Sit comfortably and look straight ahead.\nPress the LEFT TRIGGER to place the task area."; c.footer.text="";
            v.actionHint.text="Complete all three checks to continue"; v.pauseHint.text="SEATED TASK  /  NO MOVEMENT REQUIRED";
            v.matchKey.text="A"; v.nonmatchKey.text="B";
            PlayerSettings.bundleVersion="1.2.1-stage1"; PlayerSettings.Android.bundleVersionCode=4;
            EditorUtility.SetDirty(c); EditorUtility.SetDirty(v);
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene); EditorSceneManager.SaveScene(c.gameObject.scene); AssetDatabase.SaveAssets();
            Debug.Log("Quiet lab v1.2 sequence guidance applied. Stimulus materials, timing and scoring unchanged.");
        }
        static Material Mat(string name,Color color,bool unlit=false)
        {
            string path=MaterialRoot+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) { m=new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.color=color; if(m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",0); EditorUtility.SetDirty(m); return m;
        }
        static Transform Child(Transform parent,string name)
        {
            var t=parent.Find(name); if(t!=null)return t;
            var go=parent is RectTransform ? new GameObject(name,typeof(RectTransform)) : new GameObject(name);
            go.transform.SetParent(parent,false); return go.transform;
        }
        static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var t=parent.Find(name); if(t==null){t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform; t.name=name; t.SetParent(parent,false);}
            t.localPosition=position;t.localScale=scale;t.GetComponent<Renderer>().sharedMaterial=material;
        }
        static RoundedSurface Surface(Transform parent,string name,Vector2 position,Vector2 size,Color color,float radius)
        {
            var existing=Child(parent,name); if(existing.GetComponent<CanvasRenderer>()==null)existing.gameObject.AddComponent<CanvasRenderer>();
            var t=Child(parent,name); var surface=t.GetComponent<RoundedSurface>(); if(surface==null)surface=t.gameObject.AddComponent<RoundedSurface>();
            surface.rectTransform.sizeDelta=size;surface.rectTransform.anchoredPosition=position;surface.radius=radius;surface.color=color;surface.raycastTarget=false;return surface;
        }
        static TMP_Text Label(Transform parent,string name,Vector2 position,Vector2 size,float fontSize,TMP_FontAsset font,Color color)
        {
            var t=Child(parent,name); var text=t.GetComponent<TextMeshProUGUI>(); if(text==null)text=t.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=font;SetText(text,position,size,fontSize,color);return text;
        }
        static void SetText(TMP_Text text,Vector2 position,Vector2 size,float fontSize,Color color)
        {text.rectTransform.anchoredPosition=position;text.rectTransform.sizeDelta=size;text.fontSize=fontSize;text.alignment=TextAlignmentOptions.Center;text.color=color;text.raycastTarget=false;}
        static TMP_Text Answer(Transform parent,string name,float x,string label,TMP_FontAsset font)
        {
            var card=Surface(parent,name,new Vector2(x,-370),new Vector2(510,82),LabPalette.Inset,18);
            var key=Surface(card.transform,"KeyDisc",new Vector2(-185,0),new Vector2(52,52),LabPalette.Edge,26);
            var keyText=Label(key.transform,"Key",Vector2.zero,new Vector2(105,50),24,font,LabPalette.Text);
            Label(card.transform,"Meaning",new Vector2(35,0),new Vector2(320,55),27,font,LabPalette.Text).text=label;
            return keyText;
        }
    }
}

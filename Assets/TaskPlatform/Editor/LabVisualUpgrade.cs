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
            ConfigureSpatialTask(c);
            DistractionSceneSetup.Apply(c);
            PlayerSettings.bundleVersion="1.5.0-stage1"; PlayerSettings.Android.bundleVersionCode=8;
            EditorUtility.SetDirty(c); EditorUtility.SetDirty(v);
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene); EditorSceneManager.SaveScene(c.gameObject.scene); AssetDatabase.SaveAssets();
            Debug.Log("Spatial lab v1.5 applied: seated desk, minimal task HUD and four-second visible stimulus.");
        }
        static void ConfigureSpatialTask(TaskPlatformController c)
        {
            var satin=AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+"StimulusSatin.mat");
            if(satin==null) { satin=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(satin,MaterialRoot+"StimulusSatin.mat"); }
            satin.color=LabPalette.Stimuli[0];
            satin.SetFloat("_Metallic",0); satin.SetFloat("_Smoothness",.42f);
            satin.SetFloat("_SpecularHighlights",1); satin.SetFloat("_EnvironmentReflections",1);
            c.stimulus.sharedMaterial=satin; EditorUtility.SetDirty(satin);
            ConfigureSmoothStimulus(c);
            var view=c.GetComponent<SpatialTaskView>(); if(view==null)view=c.gameObject.AddComponent<SpatialTaskView>();
            c.spatialView=view; view.instructions=c.heading.canvas; view.instructions.enabled=true;
            view.instructions.transform.localPosition=new Vector3(0,0,-.20f);
            var anchor=c.taskAnchor;
            var bench=Child(anchor,"ExperimentWorkbench"); view.bench=bench;
            var top=Mat("WorkbenchTop",LabPalette.Trim); var leg=Mat("WorkbenchSupport",LabPalette.Floor);
            Box(bench,"WorkSurface",new Vector3(0,-.50f,.08f),new Vector3(1.85f,.055f,.65f),top);
            Box(bench,"FrontEdge",new Vector3(0,-.52f,-.25f),new Vector3(1.85f,.06f,.025f),leg);
            Box(bench,"LeftLeg",new Vector3(-.70f,-.85f,.08f),new Vector3(.065f,.65f,.50f),leg);
            Box(bench,"RightLeg",new Vector3(.70f,-.85f,.08f),new Vector3(.065f,.65f,.50f),leg);
            view.leftLeg=bench.Find("LeftLeg"); view.rightLeg=bench.Find("RightLeg");
            Cylinder(bench,"StimulusPlinth",new Vector3(0,-.19f,0),new Vector3(.30f,.025f,.30f),top);
            Cylinder(bench,"PlinthStem",new Vector3(0,-.35f,0),new Vector3(.09f,.14f,.09f),leg);
            var canvasTransform=anchor.Find("SpatialTaskHUD");
            if(canvasTransform==null)
            {
                var go=new GameObject("SpatialTaskHUD",typeof(RectTransform),typeof(Canvas));
                canvasTransform=go.transform; canvasTransform.SetParent(anchor,false);
            }
            canvasTransform.localPosition=new Vector3(0,0,.08f); canvasTransform.localScale=Vector3.one*.001f;
            var canvas=canvasTransform.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=c.heading.canvas.worldCamera;
            canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(2100,1100); view.taskCanvas=canvas;
            var font=c.heading.font;
            var banner=Surface(canvasTransform,"StageSign",new Vector2(0,365),new Vector2(1350,140),LabPalette.Panel,22);
            view.stage=Label(banner.transform,"Stage",new Vector2(0,36),new Vector2(1300,40),28,font,LabPalette.Muted);
            view.rule=Label(banner.transform,"Rule",new Vector2(0,-28),new Vector2(1300,60),36,font,LabPalette.Text);
            var rule=Surface(canvasTransform,"RuleCard",new Vector2(-650,0),new Vector2(560,275),LabPalette.Panel,22);
            Label(rule.transform,"Title",new Vector2(0,95),new Vector2(520,40),25,font,LabPalette.Muted).text="COMPARISON RULE";
            view.comparison=Label(rule.transform,"Comparison",new Vector2(0,-25),new Vector2(515,190),28,font,LabPalette.Text);
            var timer=Surface(canvasTransform,"CountdownCard",new Vector2(605,0),new Vector2(350,275),LabPalette.Panel,22);
            view.timer=Label(timer.transform,"Number",new Vector2(0,20),new Vector2(310,145),92,font,LabPalette.Text);
            view.timerLabel=Label(timer.transform,"Caption",new Vector2(0,-90),new Vector2(325,55),22,font,LabPalette.Muted);
            var progress=Surface(canvasTransform,"ProgressSign",new Vector2(0,-290),new Vector2(1300,75),LabPalette.Panel,16);
            view.progress=Label(progress.transform,"Progress",Vector2.zero,new Vector2(1250,65),28,font,LabPalette.Text);
            view.feedback=Label(canvasTransform,"PracticeFeedback",new Vector2(0,-350),new Vector2(1300,45),25,font,LabPalette.Accent);
            var controls=Surface(canvasTransform,"ControlsSign",new Vector2(0,-415),new Vector2(1450,65),LabPalette.Panel,14);
            view.controls=Label(controls.transform,"Controls",Vector2.zero,new Vector2(1400,55),25,font,LabPalette.Text);
            // Put reading surfaces in front of the plinth instead of behind its stem.
            foreach(var sign in new[]{progress.transform,controls.transform,view.feedback.transform})
            { var p=sign.localPosition; p.z=-230; sign.localPosition=p; }
            ConfigureMinimalHud(view);
            view.PlaceSupports(anchor); canvas.enabled=false;
            if(c.head.GetComponent<AudioListener>()==null)c.head.gameObject.AddComponent<AudioListener>();
            var cue=c.GetComponent<TrialOnsetCue>(); if(cue==null)cue=c.gameObject.AddComponent<TrialOnsetCue>();
            var source=cue.GetComponent<AudioSource>(); source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.volume=.12f;
            c.onsetCue=cue; EditorUtility.SetDirty(view); EditorUtility.SetDirty(cue); EditorUtility.SetDirty(source);
        }
        static void ConfigureMinimalHud(SpatialTaskView view)
        {
            var root=view.taskCanvas.transform;
            foreach(string name in new[]{"RuleCard","ProgressSign","ControlsSign","PracticeFeedback"})root.Find(name).gameObject.SetActive(false);
            var banner=root.Find("StageSign").GetComponent<RectTransform>();
            banner.anchoredPosition=new Vector2(0,210);banner.sizeDelta=new Vector2(1000,100);
            banner.GetComponent<RoundedSurface>().enabled=false;
            SetText(view.stage,new Vector2(0,22),new Vector2(1000,40),24,LabPalette.Muted);
            SetText(view.rule,new Vector2(0,-22),new Vector2(1000,45),28,LabPalette.Text);
            var countdown=root.Find("CountdownCard").GetComponent<RectTransform>();
            countdown.anchoredPosition=new Vector2(220,0);countdown.sizeDelta=new Vector2(230,180);
            countdown.GetComponent<RoundedSurface>().enabled=false;
            SetText(view.timer,new Vector2(0,15),new Vector2(210,110),72,LabPalette.Text);
            SetText(view.timerLabel,new Vector2(0,-58),new Vector2(230,40),21,LabPalette.Muted);
        }
        static void ConfigureSmoothStimulus(TaskPlatformController c)
        {
            const string path="Assets/TaskPlatform/Materials/StimulusSmoothSphere.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                // A denser surface removes visible faceting without changing the 0.5-unit radius.
                const int segments=64, rings=32, row=segments+1;
                var vertices=new Vector3[(rings+1)*row]; var normals=new Vector3[vertices.Length];
                var uv=new Vector2[vertices.Length]; var triangles=new System.Collections.Generic.List<int>();
                for(int y=0;y<=rings;y++) for(int x=0;x<=segments;x++)
                {
                    float theta=Mathf.PI*y/rings, phi=2*Mathf.PI*x/segments;
                    int i=y*row+x;
                    normals[i]=new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi));
                    vertices[i]=normals[i]*.5f; uv[i]=new Vector2((float)x/segments,(float)y/rings);
                    if(y==rings || x==segments)continue;
                    int a=i,b=i+row;
                    if(y>0)triangles.AddRange(new[]{a,a+1,b});
                    if(y<rings-1)triangles.AddRange(new[]{a+1,b+1,b});
                }
                mesh=new Mesh { name="StimulusSmoothSphere",vertices=vertices,normals=normals,uv=uv,triangles=triangles.ToArray() };
                mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,path);
            }
            c.stimulus.GetComponent<MeshFilter>().sharedMesh=mesh;
        }
        static void Cylinder(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var t=parent.Find(name); if(t==null){t=GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;t.name=name;t.SetParent(parent,false);}
            t.localPosition=position;t.localScale=scale;t.GetComponent<Renderer>().sharedMaterial=material;
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

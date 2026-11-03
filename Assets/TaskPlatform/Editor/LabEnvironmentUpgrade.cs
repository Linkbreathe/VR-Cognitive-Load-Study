using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EvidenceVR.Editor
{
    public static class LabEnvironmentUpgrade
    {
        const string Root="Assets/TaskPlatform/Environment/";
        static Mesh bevel;
        static Material white,metal,dark,soft,wall,floor,light,trim,wood,linen,leaf;
        [MenuItem("Evidence VR/Apply Warm Study Environment")]
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before changing the laboratory.");
            var owner=UnityEngine.Object.FindFirstObjectByType<TaskPlatformController>();
            var room=GameObject.Find("NeutralRoom");
            if(owner==null || room==null)throw new InvalidOperationException("Open the existing experiment scene first.");
            Directory.CreateDirectory(Root+"Materials");Directory.CreateDirectory(Root+"Meshes");AssetDatabase.Refresh();
            ConfigureImports();bevel=BeveledCube();
            white=Mat("CabinetEnamel",LabPalette.Hex("DDD2BA"),.18f);
            metal=Mat("SatinAluminum",LabPalette.Hex("8C806A"),.25f,.25f);
            dark=Mat("EquipmentCharcoal",LabPalette.Panel,.15f);
            soft=Mat("ChairUpholstery",LabPalette.Hex("89917A"),.04f);
            trim=Mat("ArchitecturalTrim",LabPalette.Hex("D1C3AC"),.16f);
            linen=Mat("WarmLinen",LabPalette.Hex("C8BBA0"),.02f);
            leaf=Mat("MutedFoliage",LabPalette.Hex("65785A"),.04f);
            wood=Mat("NaturalOak",LabPalette.Hex("F5EAD5"),.20f);
            SetTexture(wood,"oak_veneer_01",true,.16f,new Vector2(1,1));
            light=Mat("CeilingDiffuser",LabPalette.Hex("F2E2C5"),0);
            light.shader=Shader.Find("Universal Render Pipeline/Unlit");EditorUtility.SetDirty(light);
            wall=Mat("PaintedPlaster",LabPalette.Hex("E9DFCB"),.08f);
            SetTexture(wall,"white_plaster_02",false,.10f,new Vector2(8,4));
            floor=Mat("OakFloor",LabPalette.Hex("F7F0E1"),.18f);
            SetTexture(floor,"wood_floor",true,.16f,new Vector2(3.5f,3.5f));
            foreach(string n in new[]{"FrontWall","BackWall","LeftWall","RightWall"})room.transform.Find(n).GetComponent<Renderer>().sharedMaterial=wall;
            room.transform.Find("Floor").GetComponent<Renderer>().sharedMaterial=floor;
            room.transform.Find("Ceiling").GetComponent<Renderer>().sharedMaterial=Mat("CeilingPaint",LabPalette.Hex("E9E3D6"),.05f);
            var existing=room.transform.Find("ArchitecturalDetails");
            foreach(Transform t in existing)
                if(t.name.StartsWith("FrontPanelJoint") || t.name.StartsWith("FloorJoint") || t.name.Contains("Reveal") || t.name.Contains("Diffuse"))t.gameObject.SetActive(false);
                else if(t.GetComponent<Renderer>()!=null)t.GetComponent<Renderer>().sharedMaterial=trim;
            var old=room.transform.Find("ModernLaboratory");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var design=new GameObject("ModernLaboratory").transform;design.SetParent(room.transform,false);
            var identity=room.GetComponent<LabEnvironmentIdentity>();if(identity==null)identity=room.AddComponent<LabEnvironmentIdentity>();
            identity.revision=LabEnvironmentIdentity.Revision;
            Architecture(design);
            Workstation(design);
            Storage(design);
            ReadingCorner(design);
            TaskFurniture(owner);
            EnsureHudContrast();
            foreach(var r in design.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic);
            }
            EditorUtility.SetDirty(identity);EditorSceneManager.MarkSceneDirty(room.scene);
            EditorSceneManager.SaveScene(room.scene);AssetDatabase.SaveAssets();
            string report="Applied "+LabEnvironmentIdentity.Revision+"; "+design.GetComponentsInChildren<Renderer>().Length+" static environment renderers; stimulus/UI/timing and fixed light settings retained.";
            Debug.Log(report);return report;
        }
        static void ConfigureImports()
        {
            foreach(string path in Directory.GetFiles(Root+"ThirdParty","*.jpg",SearchOption.AllDirectories))
            {
                string normalized=path.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(normalized);
                if(importer==null)continue;
                importer.textureType=path.Contains("nor_gl")?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
                importer.textureCompression=TextureImporterCompression.Compressed;
                var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=1024;android.format=TextureImporterFormat.ASTC_6x6;importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
            foreach(string path in Directory.GetFiles(Root+"ThirdParty","*.fbx",SearchOption.AllDirectories))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.addCollider=false;importer.isReadable=false;
                importer.SaveAndReimport();
            }
        }
        static Material Mat(string name,Color color,float smooth,float metallic=0)
        {
            string path=Root+"Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);m.SetFloat("_EnvironmentReflections",0);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static void SetTexture(Material m,string asset,bool albedo,float normal,Vector2 tiling)
        {
            string path=Root+"ThirdParty/PolyHaven/"+asset+"/"+asset;
            m.SetTexture("_BaseMap",albedo?AssetDatabase.LoadAssetAtPath<Texture2D>(path+"_diff_1k.jpg"):null);
            m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path+"_nor_gl_1k.jpg"));m.SetFloat("_BumpScale",normal);m.EnableKeyword("_NORMALMAP");
            m.SetTextureScale("_BaseMap",tiling);EditorUtility.SetDirty(m);
        }
        static Transform Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.AddComponent<MeshFilter>().sharedMesh=bevel;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        static Transform Group(Transform parent,string name,Vector3 position,float yaw=0)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;t.localRotation=Quaternion.Euler(0,yaw,0);return t;}
        static void Architecture(Transform p)
        {
            // Keep the central 3.2 x 1.8 m movie surface clear in every condition.
            foreach(int side in new[]{-1,1})
            {
                Box(p,"WarmSidePanel"+side,new Vector3(side*2.917f,.46f,0),new Vector3(.035f,.72f,5.75f),white);
                Box(p,"OakDadoCap"+side,new Vector3(side*2.89f,.84f,0),new Vector3(.05f,.035f,5.75f),wood);
            }
            foreach(float x in new[]{-1.45f,1.45f})foreach(float z in new[]{-.95f,1.45f})
            {
                var t=Group(p,"FixedCeilingFixture",new Vector3(x,2.92f,z));
                Box(t,"OakSurround",Vector3.zero,new Vector3(.76f,.055f,1.16f),wood);
                Box(t,"FrostedDiffuser",new Vector3(0,-.032f,0),new Vector3(.69f,.012f,1.09f),light);
            }
            var door=Group(p,"ClosedStudyDoor",new Vector3(-1.92f,0,-2.92f));
            Box(door,"Frame",new Vector3(0,1.13f,0),new Vector3(1.09f,2.26f,.06f),white);
            Box(door,"OakLeaf",new Vector3(0,1.10f,.038f),new Vector3(.96f,2.17f,.035f),wood);
            Box(door,"HandlePlate",new Vector3(.34f,1,.066f),new Vector3(.045f,.15f,.018f),metal);
            Box(door,"LeverHandle",new Vector3(.28f,1.04f,.10f),new Vector3(.18f,.025f,.035f),metal);
            var rug=Box(p,"PlainWovenRug",new Vector3(0,.006f,.85f),new Vector3(2.65f,.012f,2.65f),linen);
            Box(rug,"InsetWeave",new Vector3(0,.51f,0),new Vector3(.96f,.015f,.96f),Mat("RugCenter",LabPalette.Hex("B9AD94"),.01f));
        }
        static Transform Model(Transform parent,string path,string name,Vector3 position,float yaw,float width,Func<Material,Material> map)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"ThirdParty/"+path);if(asset==null)throw new Exception("Missing model: "+path);
            var group=Group(parent,name,position,yaw);var obj=(GameObject)PrefabUtility.InstantiatePrefab(asset);obj.transform.SetParent(group,false);
            var renderers=obj.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            // Normalize in an unrotated temporary frame; authored FBX origins/units vary.
            group.localRotation=Quaternion.identity;obj.transform.localPosition=Vector3.zero;obj.transform.localRotation=Quaternion.identity;
            b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            float scale=width/b.size.x;obj.transform.localScale*=scale;
            b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            obj.transform.position+=new Vector3(group.position.x-b.center.x,group.position.y-b.min.y,group.position.z-b.center.z);
            group.localRotation=Quaternion.Euler(0,yaw,0);
            // Owned static furnishings are independent scene objects; material overrides survive reloads.
            PrefabUtility.UnpackPrefabInstance(obj,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var r in renderers)r.sharedMaterials=r.sharedMaterials.Select(map).ToArray();
            foreach(var collider in obj.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            return group;
        }
        static Material FurnitureMaterial(Material source)
        {
            string n=source.name.ToLowerInvariant();
            if(n.Contains("leaf") || n.Contains("plant") || n.Contains("green"))return leaf;
            if(n.Contains("dirt"))return dark;
            if(n.Contains("metal"))return metal;
            if(n.Contains("lamp"))return light;
            if(n.Contains("wood"))return wood;
            return linen;
        }
        static void Workstation(Transform p)
        {
            var desk=Model(p,"Kenney/desk.fbx","WritingDesk",new Vector3(2.25f,0,-1.15f),-90,1.55f,m=>wood);
            float surface=desk.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.y)+.002f;
            Model(p,"Kenney/chairDesk.fbx","OperatorChair",new Vector3(1.52f,0,-1.15f),-90,.58f,m=>m.name.Contains("carpet")?soft:wood);
            Model(p,"Kenney/computerScreen.fbx","InactiveMonitor",new Vector3(2.42f,surface,-1.15f),90,.49f,m=>dark);
            Model(p,"Kenney/computerKeyboard.fbx","Keyboard",new Vector3(2.05f,surface,-1.15f),90,.38f,m=>dark);
            Model(p,"Kenney/computerMouse.fbx","Mouse",new Vector3(2.03f,surface,-1.48f),90,.065f,m=>dark);
            Model(p,"Kenney/lampRoundTable.fbx","DeskLamp",new Vector3(2.40f,surface,-.64f),0,.24f,FurnitureMaterial);
            // Acquisition equipment is stored in a tidy, closed peripheral sideboard.
            var sideboard=Group(p,"EquipmentSideboard",new Vector3(.5f,0,-2.60f));
            Box(sideboard,"Body",new Vector3(0,.45f,0),new Vector3(1.70f,.78f,.53f),wood);
            Box(sideboard,"Top",new Vector3(0,.865f,0),new Vector3(1.77f,.05f,.60f),wood);
            for(int i=0;i<3;i++)
            {
                Box(sideboard,"Door"+i,new Vector3((i-1)*.555f,.46f,.28f),new Vector3(.54f,.70f,.025f),white);
                Box(sideboard,"Pull"+i,new Vector3((i-1)*.555f,.69f,.31f),new Vector3(.14f,.018f,.027f),metal);
            }
            Box(sideboard,"ClosedEquipmentCase",new Vector3(.38f,.98f,0),new Vector3(.48f,.18f,.32f),soft);
            Model(p,"Kenney/plantSmall1.fbx","SideboardPlant",new Vector3(-.05f,.895f,-2.58f),0,.22f,FurnitureMaterial);
        }
        static void Storage(Transform p)
        {
            Bookcase(p,"LeftBookshelf",new Vector3(-2.43f,0,2.68f),0,.82f,2.12f);
            Bookcase(p,"RearBookshelf",new Vector3(2.12f,0,-2.68f),180,1.12f,1.68f);
            var console=Group(p,"FrontOakConsole",new Vector3(2.44f,0,2.65f));
            Box(console,"Body",new Vector3(0,.40f,0),new Vector3(.81f,.76f,.40f),wood);
            Box(console,"Door",new Vector3(0,.40f,-.215f),new Vector3(.76f,.70f,.03f),white);
            Box(console,"Pull",new Vector3(.27f,.59f,-.24f),new Vector3(.018f,.12f,.025f),metal);
            Model(p,"Kenney/pottedPlant.fbx","CornerPlant",new Vector3(2.44f,.80f,2.65f),0,.50f,FurnitureMaterial);
            Model(p,"Kenney/lampRoundFloor.fbx","FrontReadingLamp",new Vector3(2.38f,0,1.65f),0,.44f,FurnitureMaterial);
        }
        static void Bookcase(Transform p,string name,Vector3 position,float yaw,float width,float height)
        {
            var shelf=Group(p,name,position,yaw);
            Box(shelf,"Back",new Vector3(0,height*.5f,.18f),new Vector3(width,height,.035f),white);
            foreach(int side in new[]{-1,1})Box(shelf,"Side"+side,new Vector3(side*(width*.5f-.022f),height*.5f,0),new Vector3(.044f,height,.40f),wood);
            var colors=new[]{"858D79","B1A084","B5A49B","756F66","CEC5B1"};
            var covers=colors.Select((c,i)=>Mat("BookCover"+i,LabPalette.Hex(c),.05f)).ToArray();
            for(int row=0;row<=5;row++)
            {
                float y=.09f+row*(height-.13f)/5f;
                Box(shelf,"Shelf"+row,new Vector3(0,y,0),new Vector3(width,.035f,.42f),wood);
                if(row==5)continue;
                for(int i=0;i<5;i++)
                {
                    float bookHeight=.22f+((i+row)%3)*.025f;
                    var book=Box(shelf,"Book"+row+"_"+i,new Vector3(-width*.5f+.10f+i*.061f,y+.018f+bookHeight*.5f,-.005f),new Vector3(.048f,bookHeight,.24f),covers[(row+i)%covers.Length]);
                    if(i==4)book.localRotation=Quaternion.Euler(0,0,-7);
                }
                if(row%2==0)Box(shelf,"HorizontalVolume"+row,new Vector3(width*.26f,y+.045f,0),new Vector3(width*.28f,.053f,.23f),covers[(row+2)%covers.Length]);
            }
        }
        static void ReadingCorner(Transform p)
        {
            Model(p,"Kenney/loungeChairRelax.fbx","ReadingArmchair",new Vector3(-2.12f,0,-.98f),-55,.84f,FurnitureMaterial);
            Model(p,"Kenney/lampRoundFloor.fbx","ReadingFloorLamp",new Vector3(-2.55f,0,-1.95f),0,.43f,FurnitureMaterial);
            var side=Group(p,"ReadingSideTable",new Vector3(-2.37f,0,-.18f));
            Box(side,"Top",new Vector3(0,.49f,0),new Vector3(.43f,.035f,.43f),wood);
            foreach(float x in new[]{-.17f,.17f})foreach(float z in new[]{-.17f,.17f})Box(side,"Leg",new Vector3(x,.245f,z),new Vector3(.033f,.49f,.033f),wood);
            Box(side,"ClosedBook",new Vector3(.01f,.532f,0),new Vector3(.23f,.04f,.29f),soft);
        }
        static void TaskFurniture(TaskPlatformController owner)
        {
            var bench=owner.spatialView.bench;
            // The stimulus, tray, stem, transforms and tabletop height are left intact.
            foreach(string name in new[]{"WorkSurface","FrontEdge","LeftLeg","RightLeg"})
            {
                var t=bench.Find(name);t.GetComponent<MeshFilter>().sharedMesh=bevel;
                t.GetComponent<Renderer>().sharedMaterial=name=="WorkSurface" || name=="FrontEdge"?wood:white;
            }
        }
        public static string EnsureHudContrast()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before editing the HUD.");
            var view=UnityEngine.Object.FindFirstObjectByType<SpatialTaskView>();
            var sign=view.stage.transform.parent;
            var existing=sign.Find("WarmStudyStatusBacking");
            var go=existing!=null?existing.gameObject:new GameObject("WarmStudyStatusBacking",typeof(RectTransform));
            go.transform.SetParent(sign,false);go.transform.SetAsFirstSibling();
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=new Vector2(0,42);rect.sizeDelta=new Vector2(1300,100);
            var image=go.GetComponent<UnityEngine.UI.Image>();if(image==null)image=go.AddComponent<UnityEngine.UI.Image>();
            image.color=new Color(.188f,.224f,.239f,.95f);image.raycastTarget=false;
            EditorSceneManager.MarkSceneDirty(go.scene);EditorSceneManager.SaveScene(go.scene);
            return "Added a fixed narrow HUD backing for contrast on the warm wall; text positions/content unchanged.";
        }
        static Mesh BeveledCube()
        {
            string path=Root+"Meshes/ManufacturedBox.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            float[] steps={-.5f,-.47f,.47f,.5f};
            foreach(Vector3 face in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                Vector3 u=Mathf.Abs(face.y)>.5f?Vector3.right:Vector3.up;Vector3 v=Vector3.Cross(face,u);int start=vertices.Count;
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {
                    Vector3 point=face*.5f+u*steps[x]+v*steps[y];
                    Vector3 core=new Vector3(Mathf.Clamp(point.x,-.47f,.47f),Mathf.Clamp(point.y,-.47f,.47f),Mathf.Clamp(point.z,-.47f,.47f));
                    Vector3 normal=(point-core).normalized;vertices.Add(core+normal*.03f);normals.Add(normal);uv.Add(new Vector2(steps[x]+.5f,steps[y]+.5f));
                }
                for(int y=0;y<3;y++)for(int x=0;x<3;x++){int a=start+y*4+x;indices.AddRange(new[]{a,a+1,a+4,a+1,a+5,a+4});}
            }
            var mesh=new Mesh{name="ManufacturedBox",vertices=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),triangles=indices.ToArray()};
            mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DawnGarden;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGarden.Editor
{
    public static class EmberCampSetup
    {
        public const string ScenePath="Assets/DawnGarden/Scenes/EmberCamp.unity";
        const string Root="Assets/DawnGarden", Art=Root+"/Art/EmberCamp", Prefabs=Root+"/Prefabs/EmberCamp";
        static readonly string[] Ids={"ember-tree","ember-boulder","ember-tent-v2","ember-monument","ember-seedling"};
        static string Source=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/ember-camp"));
        static Material sand, cliff, border, soil, stepping, orange, golden, wood;

        [MenuItem("Dawn Garden/Ember Camp/Import Formal Assets")]
        public static void Import()
        {
            EnsureEdit(); Directory.CreateDirectory(Art); Directory.CreateDirectory(Prefabs);
            foreach(var id in Ids)
            {
                Directory.CreateDirectory(Art+"/"+id);
                for(var i=0;i<3;i++) File.Copy(Path.Combine(Source,id,"lod"+i+".glb"),Art+"/"+id+"/lod"+i+".glb",true);
            }
            File.Copy(Path.Combine(Source,"ember-ground/tile.png"),Art+"/Sand.png",true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var file in new[]{"EmberMorning.wav","EmberMoonlight.wav"})
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(Root+"/Audio/"+file);
                var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
                importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
            var ti=(TextureImporter)AssetImporter.GetAtPath(Art+"/Sand.png");
            ti.wrapMode=TextureWrapMode.Repeat; ti.sRGBTexture=true; ti.mipmapEnabled=true;ti.maxTextureSize=2048;ti.anisoLevel=4;ti.SaveAndReimport();
            foreach(var id in Ids)
            {
                var holder=new GameObject(id);
                try
                {
                    var lods=new LOD[3];
                    for(var i=0;i<3;i++)
                    {
                        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/"+id+"/lod"+i+".glb");
                        if(asset==null) throw new InvalidOperationException("Missing imported asset "+id);
                        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,holder.transform);go.name="LOD"+i;
                        var renderers=go.GetComponentsInChildren<Renderer>();
                        if(renderers.Length==0 || renderers.SelectMany(r=>r.sharedMaterials).Any(m=>m==null || !m.shader.isSupported)) throw new InvalidOperationException("Invalid renderer: "+id);
                        lods[i]=new LOD(new[]{.12f,.05f,.008f}[i],renderers);
                    }
                    var group=holder.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
                    PrefabUtility.SaveAsPrefabAsset(holder,Prefabs+"/"+id+".prefab");
                }
                finally{UnityEngine.Object.DestroyImmediate(holder);}
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Dawn Garden/Ember Camp/Open Ember Camp")]
        public static void Open()
        {
            EnsureEdit();
            if(!File.Exists(ScenePath)) throw new InvalidOperationException("Assemble Ember Camp first");
            EditorSceneManager.OpenScene(ScenePath);
        }
        static void EnsureEdit()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene first");
        }
        [MenuItem("Dawn Garden/Ember Camp/Assemble Approved Camp")]
        public static void Assemble()
        {
            EnsureEdit();
            if(File.Exists(ScenePath))throw new InvalidOperationException("Camp already exists; keep manual edits. Use a versioned scene for redesign.");
            foreach(var id in Ids) if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/"+id+".prefab")==null)throw new InvalidOperationException("Import formal camp assets first");
            EditorSceneManager.OpenScene(Root+"/Scenes/DawnGarden.unity");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath,true);
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
            game.homeId="ember-camp-starter-v1";game.homeTitle="暖焰营地";game.homeSubtitle="我的第一块土地 · 营火与暖泉";game.terrainLabel="沙土地";
            var world=GameObject.Find("Garden World").transform;
            var decorations=new GameObject("Ember Camp Decorations").transform;decorations.SetParent(world,false);
            sand=Lit("Apricot Sand",new Color(.92f,.83f,.72f));sand.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Sand.png");
            cliff=Lit("Coral Sandstone Cliff",new Color(.57f,.30f,.21f));
            border=Lit("Rounded Red Sandstone",new Color(.64f,.37f,.25f));
            soil=Lit("Warm Garden Soil",new Color(.30f,.18f,.09f));
            stepping=Lit("Cream Sandstone Path",new Color(.72f,.61f,.47f));
            orange=Lit("Camp Coral Petals",new Color(.86f,.29f,.085f));
            golden=Lit("Golden Tuft",new Color(.66f,.43f,.11f));wood=Lit("Firewood",new Color(.25f,.12f,.045f));
            var island=GameObject.Find("Walkable Island");island.GetComponent<Renderer>().sharedMaterials=new[]{sand,cliff};
            var polish=GameObject.Find("Garden Polish Decorations"); if(polish!=null)UnityEngine.Object.DestroyImmediate(polish);
            foreach(var go in SceneObjects().Where(g=>g.name.StartsWith("Formal Tree ") || g.name=="Formal Travel Monument").ToArray())UnityEngine.Object.DestroyImmediate(go);
            foreach(var go in SceneObjects().Where(g=>new[]{"Central Boulder","West Boulder","North Rocks","Stepping Stone","Tool Stump","WateringCan","Pond Pebble","Flower","Flower Stem"}.Contains(g.name)).ToArray())go.SetActive(false);
            var treePositions=new[]{new Vector3(4.3f,0,-.45f),new Vector3(2.35f,0,-3.4f),new Vector3(-4.0f,0,-2.05f)};
            var treeScales=new[]{.88f,.94f,1.04f};
            var treeRenderers=new List<Renderer>();
            for(var i=0;i<3;i++)
            {
                var tree=Prop("ember-tree",treePositions[i],Vector3.one*treeScales[i],i*103,decorations);
                tree.name="Ember Tree "+(i+1);SimpleCollider(tree,new Vector3(0,.8f,0),new Vector3(.7f,1.6f,.7f));
                treeRenderers.AddRange(tree.GetComponentsInChildren<Renderer>(true));
            }
            var tent=Prop("ember-tent-v2",new Vector3(2.4f,.02f,-1.35f),Vector3.one,-10,decorations);tent.name="Camp Cream Tent";
            SimpleCollider(tent,new Vector3(0,.8f,0),new Vector3(2.3f,1.6f,2.2f));
            treeRenderers.AddRange(tent.GetComponentsInChildren<Renderer>(true));
            var monument=Prop("ember-monument",new Vector3(.1f,.02f,-3.8f),Vector3.one,0,decorations);monument.name="Camp Amber Monument";
            SimpleCollider(monument,new Vector3(0,.75f,0),new Vector3(.75f,1.5f,.65f));
            var rocks=new[]{new Vector3(-4.7f,0,-.3f),new Vector3(4.7f,0,1.05f),new Vector3(-2.8f,0,-3.5f),new Vector3(1.2f,0,-4.2f)};
            for(var i=0;i<rocks.Length;i++){var r=Prop("ember-boulder",rocks[i],Vector3.one*(i==2?.7f:1),i*73,decorations);SimpleCollider(r,new Vector3(0,.37f,0),new Vector3(1.1f,.75f,.9f));}
            var water=game.FindInteraction(GardenInteractionKind.Water);water.displayName="暖焰花幼苗";water.transform.position=new Vector3(-2.5f,0,-1.45f);
            foreach(var c in water.transform.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Formal Growing Leaves" || t.name=="Growing Leaves").ToArray())c.gameObject.SetActive(false);
            var plant=Prop("ember-seedling",water.transform.position+Vector3.up*.05f,Vector3.one,0,water.transform);water.visual=plant.transform;
            var bed=GameObject.Find("Seed Bed");bed.transform.position=water.transform.position+Vector3.up*.05f;bed.GetComponent<Renderer>().sharedMaterial=soil;
            var chest=game.FindInteraction(GardenInteractionKind.Chest);chest.transform.position=new Vector3(-.65f,0,-1.95f);
            var pond=new Vector3(-2.65f,0,2.0f);var oldPond=GameObject.Find("Pond Water").transform.position;
            foreach(var n in new[]{"Pond Water","Pond Bank","Pond No Walk Volume"})
            {var go=GameObject.Find(n);go.transform.position+=pond-new Vector3(oldPond.x,0,oldPond.z);}
            GameObject.Find("Pond Bank").GetComponent<Renderer>().sharedMaterial=border;
            var springMaterial=Water("Spring Water",new Color(.12f,.38f,.38f),new Color(.43f,.74f,.68f),3);
            GameObject.Find("Pond Water").GetComponent<Renderer>().sharedMaterial=springMaterial;
            GameObject.Find("Ocean").GetComponent<Renderer>().sharedMaterial=Water("Camp Ocean",new Color(.39f,.12f,.13f),new Color(.72f,.31f,.25f),.9f);
            for(var i=0;i<22;i++){float a=i*Mathf.PI*2/22;Pebble("Warm spring rim",pond+new Vector3(Mathf.Cos(a)*1.34f,.045f,Mathf.Sin(a)*1.06f),new Vector3(.32f,.20f,.26f),border,decorations,i*29);}
            for(var i=0;i<14;i++){float a=i*Mathf.PI*2/14;Pebble("Ember seed bed rim",water.transform.position+new Vector3(Mathf.Cos(a)*.62f,.06f,Mathf.Sin(a)*.57f),new Vector3(.22f,.15f,.19f),border,decorations,i*21);}
            for(var i=0;i<66;i++)
            {float a=i*Mathf.PI*2/66,r=1+.035f*Mathf.Sin(3*a+.3f)+.025f*Mathf.Cos(7*a);Pebble("Shore rim stone",new Vector3(Mathf.Cos(a)*6.01f*r,-.08f,Mathf.Sin(a)*5.03f*r),new Vector3(.49f,.26f,.40f),border,decorations,i*23);}
            PathStones(decorations);Flowers(decorations);
            var atmosphere=game.atmosphere; atmosphere.crystalGlow.transform.position=monument.transform.position+Vector3.up*1.1f;atmosphere.crystalGlow.color=new Color(1,.49f,.15f);
            atmosphere.daylightExposure=.65f;atmosphere.nightExposure=.70f;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Art+"/CampVolume.asset");
            foreach(var original in atmosphere.atmosphereVolume.sharedProfile.components)
            {var component=UnityEngine.Object.Instantiate(original);profile.components.Add(component);AssetDatabase.AddObjectToAsset(component,profile);}
            atmosphere.atmosphereVolume.sharedProfile=profile;
            EditorUtility.SetDirty(profile);
            if(profile.TryGet<ColorAdjustments>(out var color)){color.saturation.Override(8);color.contrast.Override(-6);}
            atmosphere.morningMusic.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/EmberMorning.wav");
            atmosphere.nightMusic.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/EmberMoonlight.wav");
            var cutaway=game.worldCamera.GetComponent<GardenTreeCutaway>();cutaway.foliage=treeRenderers.ToArray();cutaway.enabled=false;
            BuildEffects(game,decorations,pond);
            var exp=game.expansion;
            exp.revealTint=new Color(1,.74f,.3f);
            exp.land.GetComponent<Renderer>().sharedMaterials=new[]{sand,cliff};
            foreach(var r in exp.land.GetComponentsInChildren<Renderer>(true))if(r.gameObject.name=="New meadow shore stone")r.sharedMaterial=border;
            foreach(var r in exp.land.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="meadow-flower").ToArray())r.gameObject.SetActive(false);
            var foam=new Material(exp.effectMaterial);foam.SetColor("_BaseColor",new Color(1,.74f,.3f,.85f));AssetDatabase.CreateAsset(foam,Art+"/Amber Expansion Motes.mat");exp.effectMaterial=foam;
            exp.seamStones=SceneObjects().Where(o=>o.name=="Shore rim stone" && InExpansion(o.transform.position,exp.finalPosition)).ToArray();
            Bake(game,false);Bake(game,true);exp.land.gameObject.SetActive(false);
            foreach(var stone in exp.seamStones)stone.SetActive(true);
            var surface=world.GetComponent<NavMeshSurface>();surface.RemoveData();surface.navMeshData=exp.originalNavigation;surface.AddData();
            game.orbit.overview=true;game.orbit.yaw=180;game.orbit.pitch=39;game.orbit.distance=16;
            atmosphere.followRealTime=true;atmosphere.RefreshNow();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var build=EditorBuildSettings.scenes.ToList();if(!build.Any(s=>s.path==ScenePath))build.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=build.ToArray();
            File.WriteAllText(Path.Combine(GardenBootstrap.Output,"ember-assembled.json"),"{\"success\":true,\"scene\":\"EmberCamp.unity\",\"generatedAssets\":5,\"petReused\":true,\"independentSave\":true}");
        }
        static GameObject[] SceneObjects()=>UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(g=>g.scene==EditorSceneManager.GetActiveScene()).ToArray();
        [MenuItem("Dawn Garden/Ember Camp/Refine Lighting And Expansion Passage")]
        public static void Refine()
        {
            Open();var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
            game.atmosphere.daylightExposure=.65f;game.atmosphere.nightExposure=.70f;
            var profile=game.atmosphere.atmosphereVolume.sharedProfile;
            if(profile.TryGet<ColorAdjustments>(out var color)){color.saturation.Override(8);EditorUtility.SetDirty(color);}
            game.atmosphere.sun.shadowStrength=.58f;
            var props=GameObject.Find("Ember Camp Decorations").transform;
            // Keep a wide, physically clear approach to the expansion seam.
            var rock=props.Cast<Transform>().First(t=>t.name=="ember-boulder"&&t.position.x>4);
            rock.position=new Vector3(4.7f,0,2.65f);
            GameObject.Find("Ember Tree 1").transform.position=new Vector3(4.3f,0,-1.15f);
            Bake(game,false);Bake(game,true);game.expansion.land.gameObject.SetActive(false);
            foreach(var stone in game.expansion.seamStones)stone.SetActive(true);
            var surface=game.expansion.surface;surface.RemoveData();surface.navMeshData=game.expansion.originalNavigation;surface.AddData();
            game.atmosphere.RefreshNow();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
        }
        static Material Lit(string name,Color color)
        {
            var path=Art+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.10f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static Material Water(string name,Color deep,Color shallow,float scale)
        {
            var m=new Material(Shader.Find("Dawn Garden/Water"));m.SetColor("_BaseColor",deep);m.SetColor("_ShallowColor",shallow);m.SetFloat("_WaveScale",scale);
            AssetDatabase.CreateAsset(m,Art+"/"+name+".mat");return m;
        }
        static GameObject Prop(string id,Vector3 p,Vector3 scale,float yaw,Transform parent)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/"+id+".prefab");
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);g.name=id;g.transform.position=p;g.transform.localScale=Vector3.Scale(g.transform.localScale,scale);g.transform.rotation=Quaternion.Euler(0,yaw,0)*g.transform.rotation;return g;
        }
        static void SimpleCollider(GameObject g,Vector3 center,Vector3 size)
        {g.layer=9;var c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;}
        static GameObject Pebble(string name,Vector3 p,Vector3 scale,Material material,Transform parent,float yaw)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(parent,false);g.transform.position=p;g.transform.localScale=scale;g.transform.rotation=Quaternion.Euler(0,yaw,8);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;return g;
        }
        static void PathStones(Transform parent)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Garden/stepping-stone.prefab");
            var positions=new[]{new Vector3(0,0,3.4f),new Vector3(.3f,0,2.65f),new Vector3(.35f,0,1.9f),new Vector3(.35f,0,1.1f),new Vector3(.2f,0,.3f),new Vector3(-.2f,0,-.45f),new Vector3(-.65f,0,-.9f),new Vector3(1.0f,0,-.5f),new Vector3(1.55f,0,-.75f)};
            foreach(var p in positions){var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);g.name="Camp stepping stone";g.transform.position=p+Vector3.up*.02f;foreach(var r in g.GetComponentsInChildren<Renderer>())r.sharedMaterial=stepping;}
        }
        static void Flowers(Transform parent)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Garden/meadow-flower.prefab");
            for(var i=0;i<36;i++)
            {
                var a=i*2.39996f;var p=new Vector3(Mathf.Cos(a)*4.8f,.015f,Mathf.Sin(a)*3.8f);
                if(p.x>3.2f&&Mathf.Abs(p.z)<1.4f)continue;
                var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);g.name="Coral camp flower";g.transform.position=p;g.transform.localScale*=.55f+(i%4)*.10f;
                foreach(var r in g.GetComponentsInChildren<Renderer>())
                {var mats=r.sharedMaterials;for(var j=0;j<mats.Length;j++)if(mats[j].name.Contains("Ivory")||mats[j].name.Contains("petal"))mats[j]=orange;r.sharedMaterials=mats;}
            }
            for(var i=0;i<55;i++)
            {
                var a=i*2.39996f;var p=new Vector3(Mathf.Cos(a)*5.2f,.04f,Mathf.Sin(a)*4.2f);
                for(var j=0;j<3;j++)Pebble("Golden sand tuft",p+new Vector3((j-1)*.045f,.05f,0),new Vector3(.035f,.17f+j*.02f,.035f),golden,parent,i*29);
            }
        }
        static bool InExpansion(Vector3 p,Vector3 center){var d=p-center;return d.x*d.x/(3.3f*3.3f)+d.z*d.z/(2.8f*2.8f)<1;}
        static void Bake(DawnGardenGame game,bool expanded)
        {
            var exp=game.expansion;var surface=exp.surface;
            exp.land.gameObject.SetActive(expanded);exp.landCollider.enabled=true;
            foreach(var stone in exp.seamStones)stone.SetActive(!expanded);
            Physics.SyncTransforms();surface.BuildNavMesh();var generated=surface.navMeshData;
            if(generated==null)throw new InvalidOperationException("Camp navigation bake failed");
            surface.RemoveData();var path=Art+"/"+(expanded?"ExpandedNavigation":"StarterNavigation")+".asset";
            var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if(previous!=null){EditorUtility.CopySerialized(generated,previous);UnityEngine.Object.DestroyImmediate(generated);generated=previous;EditorUtility.SetDirty(previous);}
            else AssetDatabase.CreateAsset(generated,path);
            if(expanded)exp.expandedNavigation=generated;else exp.originalNavigation=generated;
        }
        static Mesh FlameMesh()
        {
            const int seg=12,rings=9;var v=new List<Vector3>();var tri=new List<int>();
            for(var j=0;j<rings;j++)
            {float t=(float)j/(rings-1),r=Mathf.Max(.003f,Mathf.Sin(Mathf.Pow(t,.65f)*Mathf.PI)*.20f+.06f*(1-t));for(var i=0;i<seg;i++){float a=i*Mathf.PI*2/seg;v.Add(new Vector3(Mathf.Cos(a)*r+.11f*t*t,t*.75f,Mathf.Sin(a)*r));}}
            for(var j=0;j<rings-1;j++)for(var i=0;i<seg;i++){int a=j*seg+i,b=j*seg+(i+1)%seg,c=a+seg,d=b+seg;tri.AddRange(new[]{a,c,b,b,c,d});}
            var m=new Mesh{name="Soft camp flame"};m.SetVertices(v);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,Art+"/Flame.asset");return m;
        }
        static Material Glow(string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,Art+"/"+name+".mat");return m;
        }
        static void BuildEffects(DawnGardenGame game,Transform parent,Vector3 pond)
        {
            var fire=new GameObject("Campfire");fire.transform.SetParent(parent,false);fire.transform.position=new Vector3(2.7f,.03f,1.8f);
            var effects=fire.AddComponent<EmberCampEffects>();effects.atmosphere=game.atmosphere;
            var basePlate=GameObject.CreatePrimitive(PrimitiveType.Cylinder);basePlate.name="Campfire charcoal bed";basePlate.layer=9;basePlate.transform.SetParent(fire.transform,false);basePlate.transform.localPosition=Vector3.up*.03f;basePlate.transform.localScale=new Vector3(1.12f,.05f,1.12f);basePlate.GetComponent<Renderer>().sharedMaterial=soil;
            for(var i=0;i<11;i++){float a=i*Mathf.PI*2/11;Pebble("Fire ring stone",fire.transform.position+new Vector3(Mathf.Cos(a)*.64f,.10f,Mathf.Sin(a)*.64f),new Vector3(.28f,.22f,.24f),border,fire.transform,i*31);}
            for(var i=0;i<4;i++){var log=GameObject.CreatePrimitive(PrimitiveType.Cylinder);UnityEngine.Object.DestroyImmediate(log.GetComponent<Collider>());log.name="Camp firewood";log.transform.SetParent(fire.transform,false);log.transform.localPosition=new Vector3(0,.18f,0);log.transform.localScale=new Vector3(.16f,.40f,.16f);log.transform.rotation=Quaternion.Euler(76,i*45,0);log.GetComponent<Renderer>().sharedMaterial=wood;}
            var mesh=FlameMesh();var outer=Glow("Fire warm orange",new Color(1.9f,.50f,.07f));var inner=Glow("Fire soft gold",new Color(2.1f,1.24f,.32f));
            effects.flameTongues=new Transform[5];
            for(var i=0;i<5;i++)
            {
                var f=new GameObject("Living flame "+i);f.transform.SetParent(fire.transform,false);f.transform.localPosition=new Vector3(Mathf.Sin(i*2.4f)*.13f,.20f,Mathf.Cos(i*2.4f)*.13f);f.transform.localScale=Vector3.one*(i==0?1:.68f);
                f.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=f.AddComponent<MeshRenderer>();renderer.sharedMaterial=i%2==0?outer:inner;renderer.shadowCastingMode=ShadowCastingMode.Off;effects.flameTongues[i]=f.transform;
            }
            var light=new GameObject("Warm firelight");light.transform.SetParent(fire.transform,false);light.transform.localPosition=Vector3.up*.75f;
            effects.firelight=light.AddComponent<Light>();effects.firelight.type=LightType.Point;effects.firelight.color=new Color(1,.50f,.18f);effects.firelight.range=4;effects.firelight.intensity=1;effects.firelight.shadows=LightShadows.None;
            effects.embers=new Transform[15];
            for(var i=0;i<effects.embers.Length;i++)effects.embers[i]=Pebble("Campfire ember",fire.transform.position,new Vector3(.01f,.01f,.01f),inner,fire.transform,i).transform;
            ConfigureSpring(effects,parent,pond);
        }
        [MenuItem("Dawn Garden/Ember Camp/Keep Trees And Improve Hot Spring")]
        public static void ImproveSpring()
        {
            Open();var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
            var cutaway=game.worldCamera.GetComponent<GardenTreeCutaway>();cutaway.enabled=false;
            foreach(var renderer in cutaway.foliage)if(renderer!=null)renderer.forceRenderingOff=false;
            var effects=UnityEngine.Object.FindFirstObjectByType<EmberCampEffects>();
            var pond=GameObject.Find("Pond Water").transform.position;
            ConfigureSpring(effects,GameObject.Find("Ember Camp Decorations").transform,new Vector3(pond.x,0,pond.z));
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
        }
        static void ConfigureSpring(EmberCampEffects effects,Transform parent,Vector3 pond)
        {
            var texturePath=Art+"/SteamSoftness.asset";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(tex==null){tex=new Texture2D(128,128,TextureFormat.RGBA32,false);AssetDatabase.CreateAsset(tex,texturePath);}
            else tex.Reinitialize(128,128,TextureFormat.RGBA32,false);
            var pixels=new Color[128*128];
            for(var y=0;y<128;y++)for(var x=0;x<128;x++)
            {
                var u=(x-63.5f)/63.5f;var v=(y-63.5f)/63.5f;
                var radius=Mathf.Sqrt(u*u+v*v);
                var soft=Mathf.Pow(Mathf.Clamp01(1-radius),1.4f);
                var curl=.78f+.22f*Mathf.PerlinNoise(u*3.2f+5.3f,v*3.2f+8.1f);
                pixels[y*128+x]=new Color(1,1,1,soft*curl);
            }
            tex.SetPixels(pixels);tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;tex.Apply();EditorUtility.SetDirty(tex);
            var shader=Shader.Find("Dawn Garden/Hot Spring Steam");
            if(shader==null)throw new InvalidOperationException("Hot spring shader is not imported");
            var matPath=Art+"/Spring Steam.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}else mat.shader=shader;
            mat.shaderKeywords=Array.Empty<string>();mat.SetTexture("_BaseMap",tex);mat.SetFloat("_Daylight",1);mat.renderQueue=3000;EditorUtility.SetDirty(mat);
            var ps=effects.springSteam;
            if(ps==null){var go=new GameObject("Warm spring steam");go.transform.SetParent(parent,false);ps=go.AddComponent<ParticleSystem>();}
            ps.transform.position=pond+Vector3.up*.12f;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=true;main.prewarm=true;main.duration=5;
            main.startLifetime=new ParticleSystem.MinMaxCurve(3.8f,5.2f);main.startSpeed=0;
            main.startSize=new ParticleSystem.MinMaxCurve(.65f,1.05f);main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            main.startColor=Color.white;main.maxParticles=100;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.gravityModifier=0;
            var emission=ps.emission;emission.rateOverTime=14;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.85f;shape.radiusThickness=1;shape.rotation=new Vector3(90,0,0);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=new ParticleSystem.MinMaxCurve(-.045f,.045f);velocity.y=new ParticleSystem.MinMaxCurve(.22f,.31f);velocity.z=new ParticleSystem.MinMaxCurve(-.045f,.045f);
            var noise=ps.noise;noise.enabled=true;noise.separateAxes=true;noise.strengthX=.06f;noise.strengthY=.025f;noise.strengthZ=.06f;noise.frequency=.55f;noise.scrollSpeed=.17f;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.65f,1,1.8f));
            var rotation=ps.rotationOverLifetime;rotation.enabled=true;rotation.z=.1f;
            var colors=ps.colorOverLifetime;colors.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.46f,.22f),new GradientAlphaKey(.30f,.55f),new GradientAlphaKey(0,1)});colors.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.maxParticleSize=.5f;
            effects.springSteam=ps;EditorUtility.SetDirty(effects);

        }
    }
}

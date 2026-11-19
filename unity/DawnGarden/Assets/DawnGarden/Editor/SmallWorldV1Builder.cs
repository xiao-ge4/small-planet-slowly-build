using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using DawnGarden;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden.Editor
{
    public static class SmallWorldV1Builder
    {
        const string Folder="Assets/SmallWorldV1";
        static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        [Serializable] public class AssetInput {public string id,name,hash,prefabPath;public int version;public Vector3 size,center,collisionSize,collisionCenter;public bool createCollider;}
        [Serializable] public class LibraryInput {public AssetInput[] assets;}
        [Serializable] public class BuildManifest {public string buildId,createdAt,appPath;public bool success;public int assets;public string[] scenes;}
        public static string Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before preparing v1.");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene changes first.");
            Directory.CreateDirectory(Folder+"/Resources");Directory.CreateDirectory(Path.Combine(Root,"artifacts/game-v1"));AssetDatabase.Refresh();
            PrepareRuntimeMaterials();
            StarterHomeDetailSetup.Import();
            var input=JsonUtility.FromJson<LibraryInput>(File.ReadAllText(Path.Combine(Root,"artifacts/game-v1/library-input.json")));
            var library=AssetDatabase.LoadAssetAtPath<SmallWorldLibrary>(Folder+"/Resources/SmallWorldLibrary.asset");
            if(library==null){library=ScriptableObject.CreateInstance<SmallWorldLibrary>();AssetDatabase.CreateAsset(library,Folder+"/Resources/SmallWorldLibrary.asset");}
            library.hideFlags=HideFlags.DontUnloadUnusedAsset;
            library.assets=input.assets.Select(a=>new SmallWorldAsset {id=a.id,name=a.name,version=a.version,hash=a.hash,size=a.size,center=a.center,createCollider=a.createCollider,collisionSize=a.collisionSize,collisionCenter=a.collisionCenter,prefab=AssetDatabase.LoadAssetAtPath<GameObject>(a.prefabPath)}).ToArray();
            if(library.assets.Any(a=>a.prefab==null))throw new InvalidOperationException("A compiled library prefab is missing.");
            EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            var dawn=PrepareHome("Assets/DawnGarden/Scenes/DawnGarden.unity",Folder+"/DawnHomeV1.unity",false);
            library.font=dawn.interfaceFont;
            PrepareHome("Assets/DawnGarden/Scenes/EmberCamp.unity",Folder+"/EmberHomeV1.unity",true);
            PrepareNeighborHome(Folder+"/DawnHomeV1.unity",Folder+"/StarDewHomeV2.unity",false);
            PrepareNeighborHome(Folder+"/DawnHomeV1.unity",Folder+"/WindCourtHomeV2.unity",true);
            var sources=Directory.GetFiles("Assets/DawnGarden/Scripts","*.cs").Concat(Directory.GetFiles("Assets/DawnGarden/Shaders","*.shader")).Concat(Directory.GetFiles("Assets/DawnGarden/Art/StarterDetails").Where(p=>!p.EndsWith(".meta"))).Concat(new[]{"Assets/DawnGarden/Editor/SmallWorldV1Builder.cs","Assets/DawnGarden/Editor/StarterHomeDetailSetup.cs",Folder+"/DawnHomeV1.unity",Folder+"/EmberHomeV1.unity",Folder+"/StarDewHomeV2.unity",Folder+"/WindCourtHomeV2.unity"}).OrderBy(p=>p).Select(p=>p+Hash(p));
            var binding=string.Join("|",sources)+File.ReadAllText(Path.Combine(Root,"artifacts/game-v1/library-input.json"));
            using(var sha=SHA256.Create())library.buildId=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(binding))).Replace("-","").ToLowerInvariant();
            EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var host=new GameObject("Small World v1");host.AddComponent<SmallWorldApp>().library=library;
            EditorSceneManager.SaveScene(scene,Folder+"/SmallWorldBoot.unity");
            library.hideFlags=HideFlags.None;EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            var manifest=new BuildManifest {buildId=library.buildId,createdAt=DateTime.UtcNow.ToString("o"),assets=library.assets.Length,scenes=new[]{Folder+"/SmallWorldBoot.unity",Folder+"/DawnHomeV1.unity",Folder+"/EmberHomeV1.unity",Folder+"/StarDewHomeV2.unity",Folder+"/WindCourtHomeV2.unity"}};
            File.WriteAllText(Path.Combine(Root,"artifacts/game-v1/build-manifest.json"),JsonUtility.ToJson(manifest,true));
            return "Prepared four home scenes and "+library.assets.Length+" resources";
        }
        static DawnGardenGame PrepareHome(string source,string output,bool ember)
        {
            var before=Hash(source);var scene=EditorSceneManager.OpenScene(source);EditorSceneManager.SaveScene(scene,output);
            var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();var exp=game.expansion;
            var old=exp.land;var main=GameObject.Find("Walkable Island");var materials=main!=null?main.GetComponent<Renderer>().sharedMaterials:old.GetComponent<Renderer>().sharedMaterials;
            var parent=exp.transform;UnityEngine.Object.DestroyImmediate(old.gameObject);
            var land=new GameObject("V1 starter-sized expansion");land.layer=LayerMask.NameToLayer("GardenGround");land.transform.SetParent(parent,false);land.transform.position=new Vector3(11.7f,0,0);
            var mesh=Store(LandMesh(),Folder+"/"+(ember?"Ember":"Dawn")+"Expansion.asset");
            land.AddComponent<MeshFilter>().sharedMesh=mesh;land.AddComponent<MeshRenderer>().sharedMaterials=materials;
            exp.land=land.transform;exp.landCollider=land.AddComponent<MeshCollider>();exp.landCollider.sharedMesh=mesh;exp.finalPosition=land.transform.position;exp.revealHalfSize=new Vector2(6.15f,5.15f);
            game.orbit.expandedOverviewCenter=new Vector3(5.8f,.5f,0);game.orbit.expandedOverviewDistance=29;game.orbit.expandedMaxDistance=38;
            var seam=SmallWorldSeam.Build(land.transform,materials[0],materials.Length>1?materials[1]:materials[0]);
            // Persist generated seam mesh in the scene's own asset area.
            var seamMesh=Store(seam.GetComponent<MeshFilter>().sharedMesh,Folder+"/"+(ember?"Ember":"Dawn")+"Seam.asset");seam.GetComponent<MeshFilter>().sharedMesh=seamMesh;seam.GetComponent<MeshCollider>().sharedMesh=seamMesh;
            exp.seamStones=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name=="Shore rim stone"&&t.position.x>4.2f&&Mathf.Abs(t.position.z)<3.05f).Select(t=>t.gameObject).ToArray();
            foreach(var stone in exp.seamStones)stone.SetActive(false);Physics.SyncTransforms();exp.surface.BuildNavMesh();var nav=exp.surface.navMeshData;exp.surface.RemoveData();
            nav=Store(nav,Folder+"/"+(ember?"Ember":"Dawn")+"ExpandedNavigation.asset");exp.expandedNavigation=nav;exp.surface.navMeshData=nav;exp.surface.AddData();
            var path=new NavMeshPath();if(!NavMesh.CalculatePath(game.spawn.position,exp.finalPosition,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new InvalidOperationException("V1 land connection is not walkable.");
            game.dailyCare=false;game.externalControlsBlocked=false; // Runtime app enables v1 behaviour before initialization.
            var cutaway=game.worldCamera.GetComponent<GardenTreeCutaway>();if(cutaway!=null)cutaway.enabled=false;
            StarterHomeDetailSetup.Apply(game,ember);
            foreach(var stone in exp.seamStones)stone.SetActive(true);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(Hash(source)!=before)throw new InvalidOperationException("Source demo scene was modified.");return game;
        }
        static void PrepareNeighborHome(string source,string output,bool wind)
        {
            var scene=EditorSceneManager.OpenScene(source);EditorSceneManager.SaveScene(scene,output);
            var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();game.homeTitle=wind?"风铃石庭":"星露小筑";game.homeId=wind?"wind-court-neighbor-v2":"star-dew-neighbor-v2";
            var library=AssetDatabase.LoadAssetAtPath<SmallWorldLibrary>(Folder+"/Resources/SmallWorldLibrary.asset");
            game.atmosphere.utcOffsetHours=wind?5:-7;game.atmosphere.nightExposure=wind?.55f:.72f;
            var decor=new GameObject(wind?"风铃景石与花径":"星露花园");
            var positions=new[]{new Vector3(-3.5f,.03f,1.4f),new Vector3(3.3f,.03f,1.3f),new Vector3(1.9f,.03f,-2.9f),new Vector3(-.4f,.03f,-3.1f)};
            for(int i=0;i<positions.Length;i++)
            {
                var asset=library.assets.First(a=>a.id==(wind&&i%2==0?"moss-boulder":"meadow-flower"));var go=(GameObject)PrefabUtility.InstantiatePrefab(asset.prefab);go.transform.SetParent(decor.transform,false);go.transform.position=positions[i];go.transform.localScale*=wind?.65f:.95f;go.transform.localRotation=Quaternion.Euler(0,i*55,0);
                foreach(var c in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
                foreach(var renderer in go.GetComponentsInChildren<Renderer>())
                {
                    var mats=renderer.sharedMaterials;for(int m=0;m<mats.Length;m++){var copy=new Material(mats[m]);if(copy.HasProperty("_BaseColor"))copy.SetColor("_BaseColor",copy.GetColor("_BaseColor")*(wind?new Color(.95f,.85f,1):new Color(.8f,.95f,1)));mats[m]=Store(copy,Folder+"/"+(wind?"Wind":"Dew")+"-"+i+"-"+renderer.GetInstanceID()+"-"+m+".mat");}renderer.sharedMaterials=mats;
                }
            }
            game.expansion.surface.BuildNavMesh();var nav=game.expansion.surface.navMeshData;game.expansion.surface.RemoveData();nav=Store(nav,Folder+"/"+(wind?"Wind":"Dew")+"Navigation.asset");game.expansion.surface.navMeshData=nav;game.expansion.expandedNavigation=nav;game.expansion.surface.AddData();
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static Mesh LandMesh()
        {
            var vertices=new List<Vector3>{new Vector3(0,.025f,0)};var uv=new List<Vector2>{new Vector2(2,2)};var top=new List<int>();var sides=new List<int>();const int count=96;
            for(var i=0;i<count;i++){var a=i*Mathf.PI*2/count;var r=1+.02f*Mathf.Sin(a*3)+.012f*Mathf.Cos(a*7);var v=new Vector3(Mathf.Cos(a)*6.15f*r,.025f,Mathf.Sin(a)*5.15f*r);vertices.Add(v);uv.Add(new Vector2((v.x+6.15f)/3.2f,(v.z+5.15f)/3.2f));}
            for(var i=0;i<count;i++)top.AddRange(new[]{0,1+(i+1)%count,1+i});
            for(var i=0;i<count;i++){var a=vertices[1+i];var b=vertices[1+(i+1)%count];var k=vertices.Count;vertices.AddRange(new[]{a,b,b*.9f+Vector3.down*1.3f,a*.9f+Vector3.down*1.3f});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});sides.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
            var mesh=new Mesh {name="Starter-sized new continent",subMeshCount=2};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(top,0);mesh.SetTriangles(sides,1);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void PrepareRuntimeMaterials()
        {
            var detail=Folder+"/Resources/GardenDetail";Directory.CreateDirectory(detail);AssetDatabase.Refresh();
            var meadowShader=Shader.Find("Dawn Garden/Living Meadow");if(meadowShader==null)throw new InvalidOperationException("Meadow shader is missing.");
            Store(new Material(meadowShader){name="Living garden grass and earth"},detail+"/Meadow.mat");
            var matteShader=Shader.Find("Dawn Garden/Matte Detail");if(matteShader==null)throw new InvalidOperationException("Detail matte shader is missing.");
            Store(new Material(matteShader){name="Garden matte detail"},detail+"/Matte.mat");
            // Runtime imports need the same alpha, AO and emissive shader variants as editor imports.
            // Explicit resource materials retain those variants without including every URP shader combination.
            var folder=Folder+"/Resources/RuntimeMaterials";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var generator=GLTFast.Materials.MaterialGenerator.GetDefaultMaterialGenerator();
            foreach(var mode in new[]{"OPAQUE","MASK","BLEND"})foreach(var doubleSided in new[]{false,true})foreach(var ao in new[]{false,true})foreach(var emissive in new[]{false,true})
            {
                var schema=new GLTFast.Schema.Material {alphaMode=mode,doubleSided=doubleSided,pbrMetallicRoughness=new GLTFast.Schema.PbrMetallicRoughness()};
                var material=generator.GenerateMaterial(schema,null);if(material==null)throw new InvalidOperationException("Runtime PBR shader is missing.");
                if(ao)material.EnableKeyword("_OCCLUSION");if(emissive)material.EnableKeyword("_EMISSIVE");
                var name="PBR-"+mode+"-"+doubleSided+"-"+ao+"-"+emissive;material.name=name;Store(material,folder+"/"+name+".mat");
            }
        }
        public static string BuildMac()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before building.");
            var manifest=JsonUtility.FromJson<BuildManifest>(File.ReadAllText(Path.Combine(Root,"artifacts/game-v1/build-manifest.json")));
            PlayerSettings.companyName="LittleWorld";PlayerSettings.productName="Small World";PlayerSettings.bundleVersion="0.5.0";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"local.littleworld.smallworld");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.runInBackground=true;
            PlayerSettings.MTRendering=false;
            PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
            var path=Path.Combine(Root,"artifacts/game-v5/Mac/SmallWorld.app");Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=manifest.scenes,locationPathName=path,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            manifest.success=report.summary.result==BuildResult.Succeeded;manifest.appPath=path;
            File.WriteAllText(Path.Combine(Root,"artifacts/game-v1/build-manifest.json"),JsonUtility.ToJson(manifest,true));
            Directory.CreateDirectory(Path.Combine(Root,"artifacts/game-v5"));File.WriteAllText(Path.Combine(Root,"artifacts/game-v5/build-manifest.json"),JsonUtility.ToJson(manifest,true));
            if(!manifest.success)throw new InvalidOperationException("Mac build failed: "+report.summary.result);return path;
        }
        static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static T Store<T>(T value,string path) where T:UnityEngine.Object
        {var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing==null){AssetDatabase.CreateAsset(value,path);return value;}EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;}
    }
}

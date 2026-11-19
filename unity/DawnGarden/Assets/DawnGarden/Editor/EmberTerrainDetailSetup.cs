using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DawnGarden;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden.Editor
{
    // Sand and shoreline are authored independently of interactive props and saves.
    public static class EmberTerrainDetailSetup
    {
        const string Art="Assets/DawnGarden/Art/EmberCamp/TerrainDetail";
        static string Source=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/ember-camp"));
        static readonly Mesh[] rocks=new Mesh[5];
        static readonly Material[] rockMaterials=new Material[6];
        static System.Random rng=new System.Random(22763);
        static float Rand(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
        static T Store<T>(T value,string file) where T:UnityEngine.Object
        {
            var path=Art+"/"+file;var prior=AssetDatabase.LoadAssetAtPath<T>(path);
            if(prior==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,prior);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(prior);return prior;
        }
        [MenuItem("Dawn Garden/Ember Camp/Build Detailed Sandstone Coast")]
        public static void Apply()
        {
            EmberCampSetup.Open();Directory.CreateDirectory(Art);rng=new System.Random(22763);
            for(var i=0;i<5;i++)File.Copy(Path.Combine(Source,"shore-kit/sandstone-"+i+".glb"),Art+"/sandstone-"+i+".glb",true);
            foreach(var file in new[]{"SandDetail.png","SandRelief.png"})File.Copy(Path.Combine(Source,"ember-sand-detail",file),Art+"/"+file,true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var file in new[]{"SandDetail.png","SandRelief.png"})
            {
                var imp=(TextureImporter)AssetImporter.GetAtPath(Art+"/"+file);imp.sRGBTexture=file=="SandDetail.png";imp.maxTextureSize=4096;imp.textureCompression=TextureImporterCompression.Uncompressed;
                imp.wrapMode=TextureWrapMode.Repeat;imp.mipmapEnabled=true;imp.anisoLevel=8;imp.SaveAndReimport();
            }
            Material rockSource=null;
            for(var i=0;i<5;i++)
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/sandstone-"+i+".glb");
                rocks[i]=asset.GetComponentInChildren<MeshFilter>().sharedMesh;
                if(rockSource==null)rockSource=asset.GetComponentInChildren<Renderer>().sharedMaterial;
            }
            var colors=new[]{new Color(1.02f,.99f,.93f),new Color(.92f,.90f,.90f),new Color(1.13f,1.08f,1.02f),new Color(.98f,.93f,.89f),new Color(1.08f,.99f,.91f),new Color(1.16f,1.12f,1.06f)};
            for(var i=0;i<6;i++)
            {var m=new Material(rockSource);m.name="Layered sandstone "+i;m.SetColor("baseColorFactor",colors[i]);m.SetFloat("roughnessFactor",.94f);m.enableInstancing=true;rockMaterials[i]=Store(m,"Sandstone "+i+".mat");}
            var sand=new Material(Shader.Find("Dawn Garden/Layered Sand"));sand.name="Grain and dust sand";
            sand.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/SandDetail.png"));sand.SetTexture("_ReliefMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/SandRelief.png"));
            sand.SetColor("_Tint",new Color(1.02f,1.02f,1.01f));sand.SetFloat("_Scale",.18f);sand.SetFloat("_Relief",.65f);sand=Store(sand,"Layered Sand.mat");
            var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();var exp=game.expansion;
            var decor=GameObject.Find("Ember Camp Decorations").transform;
            var old=decor.Find("Detailed Sandstone Coast");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            old=exp.land.Find("Detailed Expansion Coast");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var t in decor.Cast<Transform>().Where(t=>new[]{"Shore rim stone","Warm spring rim","Ember seed bed rim","Golden sand tuft"}.Contains(t.name)).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            foreach(var t in exp.land.Cast<Transform>().Where(t=>t.name=="New meadow shore stone").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            var baseLand=GameObject.Find("Walkable Island");
            var mesh=Ground(false);baseLand.GetComponent<MeshFilter>().sharedMesh=mesh;baseLand.GetComponent<MeshCollider>().sharedMesh=mesh;baseLand.GetComponent<Renderer>().sharedMaterials=new[]{sand,rockMaterials[0]};
            mesh=Ground(true);exp.land.GetComponent<MeshFilter>().sharedMesh=mesh;exp.landCollider.sharedMesh=mesh;exp.land.GetComponent<Renderer>().sharedMaterials=new[]{sand,rockMaterials[0]};
            var main=new GameObject("Detailed Sandstone Coast").transform;main.SetParent(decor,false);
            var extension=new GameObject("Detailed Expansion Coast").transform;extension.SetParent(exp.land,false);
            var seam=new List<GameObject>();
            const int count=40;
            for(var i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count+Rand(-.009f,.009f);var e=Edge(a,false)*.968f;
                var size=new Vector3(Rand(1.20f,1.58f),Rand(1.04f,1.28f),Rand(1.12f,1.40f));
                var p=e+new Vector3(0,-1.0f,0);var go=Rock("Shore rim stone",p,size,-a*Mathf.Rad2Deg-90+Rand(-14,14),main,i);
                if(InsideExpansion(p,exp.finalPosition))seam.Add(go);
                if(i%3==0)Rock("Waterline rock fragment",e*1.055f+Vector3.down*.92f,new Vector3(Rand(.22f,.36f),Rand(.25f,.39f),Rand(.22f,.39f)),i*67,main,i+2);
            }
            for(var i=0;i<29;i++)
            {
                float a=i*Mathf.PI*2/29;var e=Edge(a,true)*.968f;var world=e+exp.finalPosition;
                if(InsideMain(world))continue;
                Rock("New meadow shore stone",e+Vector3.down*1.0f,new Vector3(Rand(1.02f,1.30f),Rand(1.02f,1.26f),Rand(1.02f,1.23f)),-a*Mathf.Rad2Deg-90,extension,i);
            }
            exp.seamStones=seam.ToArray();
            Ring("Warm spring sandstone",GameObject.Find("Pond Water").transform.position,new Vector2(1.32f,1.04f),18,new Vector3(.44f,.30f,.38f),main);
            Ring("Seed bed sandstone",game.FindInteraction(GardenInteractionKind.Water).transform.position,new Vector2(.62f,.57f),11,new Vector3(.32f,.23f,.26f),main);
            var fire=GameObject.Find("Campfire").transform;
            foreach(var t in fire.Cast<Transform>().Where(t=>t.name=="Fire ring stone").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            Ring("Fire ring stone",fire.position,new Vector2(.64f,.64f),11,new Vector3(.32f,.24f,.28f),main);
            GroundDetails(main,false);GroundDetails(extension,true);
            SoilGrains(main,false);SoilGrains(extension,true);
            ConfigureSea(game,decor);
            RebuildNavigation(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
        }
        static GameObject Rock(string name,Vector3 position,Vector3 size,float yaw,Transform parent,int index)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(Rand(-3,3),yaw,Rand(-3,3));go.transform.localScale=size;
            go.AddComponent<MeshFilter>().sharedMesh=rocks[Math.Abs(index)%rocks.Length];var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=rockMaterials[Math.Abs(index)%rockMaterials.Length];return go;
        }
        static void Ring(string name,Vector3 center,Vector2 radius,int count,Vector3 size,Transform parent)
        {
            for(var i=0;i<count;i++){var a=i*Mathf.PI*2/count;var p=center+new Vector3(Mathf.Cos(a)*radius.x,-.09f,Mathf.Sin(a)*radius.y);Rock(name,p,Vector3.Scale(size,new Vector3(Rand(.85f,1.16f),Rand(.85f,1.1f),Rand(.9f,1.1f))),-a*Mathf.Rad2Deg-90,parent,i+3);}
        }
        static Vector3 Edge(float angle,bool expanded)
        {
            var r=expanded?1+.032f*Mathf.Sin(3*angle+.4f)+.018f*Mathf.Cos(7*angle):1+.035f*Mathf.Sin(3*angle+.3f)+.025f*Mathf.Cos(7*angle);
            r*=1+.007f*Mathf.Sin(angle*(expanded?29:40)+.6f);
            return new Vector3(Mathf.Cos(angle)*(expanded?3.25f:6.15f)*r,0,Mathf.Sin(angle)*(expanded?2.75f:5.15f)*r);
        }
        static Vector3 SandEdge(float angle,bool expanded)
        {
            var r=.964f+.025f*Mathf.Sin(angle*11+.9f)+.012f*Mathf.Sin(angle*27+2.3f)+.007f*Mathf.Cos(angle*43);
            return Edge(angle,expanded)*r;
        }
        static float SandRadius(Vector3 p,bool expanded)
        {
            var angle=Mathf.Atan2(p.z/(expanded?2.75f:5.15f),p.x/(expanded?3.25f:6.15f));
            var edge=SandEdge(angle,expanded);
            return new Vector2(p.x,p.z).magnitude/new Vector2(edge.x,edge.z).magnitude;
        }
        static bool InsideMain(Vector3 p)=>p.x*p.x/(6.20f*6.20f)+p.z*p.z/(5.20f*5.20f)<1.025f;
        static bool InsideExpansion(Vector3 p,Vector3 center){p-=center;return p.x*p.x/(3.55f*3.55f)+p.z*p.z/(2.98f*2.98f)<1;}
        static float Height(Vector3 p,float radius)
        {
            var broad=Mathf.PerlinNoise(p.x*.46f+19,p.z*.46f+33)-.5f;
            var fine=Mathf.PerlinNoise(p.x*2.1f+7,p.z*2.1f+15)-.5f;
            return .020f+broad*.08f+fine*.016f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.90f,1,radius))*.16f;
        }
        static Mesh Ground(bool expanded)
        {
            const int segments=192,rings=32;var v=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var top=new List<int>();var sides=new List<int>();
            var origin=expanded?new Vector3(7.5f,0,0):Vector3.zero;
            Action<Vector3,float> add=(p,r)=>{v.Add(p);uv.Add(new Vector2((p.x+origin.x)*.48f,p.z*.48f));colors.Add(new Color(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,r)),r,1,1));};
            add(new Vector3(0,Height(origin,0),0),0);
            for(var ring=1;ring<=rings;ring++)for(var i=0;i<segments;i++)
            {float radius=(float)ring/rings;var angle=i*Mathf.PI*2/segments;var p=SandEdge(angle,expanded)*radius;p.y=Height(p+origin,radius);add(p,radius);}
            for(var i=0;i<segments;i++)top.AddRange(new[]{0,1+(i+1)%segments,1+i});
            for(var r=1;r<rings;r++)for(var i=0;i<segments;i++)
            {var a=1+(r-1)*segments+i;var b=1+(r-1)*segments+(i+1)%segments;var c=a+segments;var d=b+segments;top.AddRange(new[]{a,b,d,a,d,c});}
            // A solid rock shelf supports the irregular, sloping sand lip. It is not a flat lid.
            for(var i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments,next=(i+1)*Mathf.PI*2/segments;
                var innerA=Edge(angle,expanded)*.80f;var innerB=Edge(next,expanded)*.80f;
                var outerA=Edge(angle,expanded)*1.018f;var outerB=Edge(next,expanded)*1.018f;
                innerA.y=innerB.y=-.10f;
                outerA.y=-.08f+.055f*Mathf.Sin(angle*17);outerB.y=-.08f+.055f*Mathf.Sin(next*17);
                int n=v.Count;add(innerA,1);add(innerB,1);add(outerB,1);add(outerA,1);
                sides.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                n=v.Count;add(outerA,1);add(outerB,1);add(new Vector3(outerB.x*.89f,-1.26f,outerB.z*.89f),1);add(new Vector3(outerA.x*.89f,-1.26f,outerA.z*.89f),1);
                uv[n]=new Vector2(i*.12f,1);uv[n+1]=new Vector2((i+1)*.12f,1);uv[n+2]=new Vector2((i+1)*.12f,0);uv[n+3]=new Vector2(i*.12f,0);
                sides.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            var mesh=new Mesh{name=expanded?"Detailed expansion sand terrain":"Detailed starter sand terrain",subMeshCount=2};
            mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(top,0);mesh.SetTriangles(sides,1);mesh.RecalculateNormals();mesh.RecalculateBounds();return Store(mesh,expanded?"ExpansionGround.asset":"StarterGround.asset");
        }
        static Material Lit(string name,Color color)
        {var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=name;mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.04f);mat.enableInstancing=true;return Store(mat,name+".mat");}
        static Mesh Chip()
        {
            var mesh=new Mesh{name="Angular sand pebbles"};
            var v=new List<Vector3>();var tri=new List<int>();
            var points=new[]{new Vector3(-.5f,0,-.35f),new Vector3(.4f,0,-.4f),new Vector3(.5f,0,.24f),new Vector3(-.34f,0,.4f),new Vector3(-.30f,.6f,-.22f),new Vector3(.27f,.75f,-.19f),new Vector3(.25f,.55f,.21f),new Vector3(-.20f,.64f,.24f)};
            var faces=new[]{new[]{4,7,6,5},new[]{0,4,5,1},new[]{1,5,6,2},new[]{2,6,7,3},new[]{3,7,4,0},new[]{3,0,1,2}};
            foreach(var f in faces){int n=v.Count;v.AddRange(f.Select(i=>points[i]));tri.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return Store(mesh,"SandChips.asset");
        }
        static Mesh Grass()
        {
            var v=new List<Vector3>();var tri=new List<int>();
            for(var i=0;i<9;i++)
            {
                var a=i*2.39996f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-dir.z,0,dir.x);
                float h=.14f+.13f*(i%3)/2f,w=.027f;
                var root=dir*.035f;var mid=dir*.09f+Vector3.up*h*.60f;var tip=dir*(.12f+(i%2)*.05f)+Vector3.up*h;
                int n=v.Count;v.AddRange(new[]{root-side*w,root+side*w,mid+side*w*.7f,mid-side*w*.7f,tip});
                tri.AddRange(new[]{n,n+2,n+1,n,n+3,n+2,n+3,n+4,n+2,n+1,n+2,n,n+2,n+3,n,n+2,n+4,n+3});
            }
            var m=new Mesh{name="Curved dry grass blades"};m.SetVertices(v);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();return Store(m,"DryGrass.asset");
        }
        static void GroundDetails(Transform parent,bool expanded)
        {
            var chip=Chip();var grass=Grass();
            var gravelMats=new[]{Lit("Sand pale gravel",new Color(.71f,.52f,.32f)),Lit("Sand clay gravel",new Color(.56f,.30f,.18f)),Lit("Sand light grit",new Color(.84f,.66f,.43f))};
            var grassMats=new[]{Lit("Dry grass honey",new Color(.72f,.48f,.12f)),Lit("Dry grass straw",new Color(.86f,.66f,.24f)),Lit("Dry grass olive",new Color(.51f,.42f,.11f))};
            var origin=expanded?new Vector3(7.5f,0,0):Vector3.zero;
            var groups=expanded?24:75;
            for(var i=0;i<groups;i++)
            {
                float a=i*2.39996f;var radius=Rand(.78f,.95f);var cluster=SandEdge(a,expanded)*radius;
                if(expanded&&InsideMain(cluster+origin))continue;
                if(!expanded&&cluster.x>4&&Mathf.Abs(cluster.z)<1.7f)continue;
                for(var j=0;j<5;j++)
                {
                    var p=cluster+new Vector3(Rand(-.22f,.22f),0,Rand(-.22f,.22f));p.y=Height(p+origin,radius)+.003f;
                    var go=new GameObject("Sand gravel cluster");go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,Rand(0,360),0);
                    var size=Rand(.028f,.090f);go.transform.localScale=new Vector3(size,Rand(.015f,.045f),size*Rand(.6f,1.1f));go.AddComponent<MeshFilter>().sharedMesh=chip;go.AddComponent<MeshRenderer>().sharedMaterial=gravelMats[(i+j)%3];
                }
                if(i%2==0)
                {
                    var go=new GameObject("Dry grass clump");go.transform.SetParent(parent,false);cluster.y=Height(cluster+origin,radius);go.transform.localPosition=cluster;
                    go.transform.localRotation=Quaternion.Euler(0,Rand(0,360),0);go.transform.localScale=Vector3.one*Rand(.75f,1.4f);go.AddComponent<MeshFilter>().sharedMesh=grass;go.AddComponent<MeshRenderer>().sharedMaterial=grassMats[i%3];
                }
            }
        }
        static void SoilGrains(Transform parent,bool expanded)
        {
            var origin=expanded?new Vector3(7.5f,0,0):Vector3.zero;
            var verts=new List<Vector3>();var triangles=new[]{new List<int>(),new List<int>(),new List<int>()};
            var source=Chip();var sv=source.vertices;var st=source.triangles;
            var pond=GameObject.Find("Pond Water").transform.position;
            var seed=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>().FindInteraction(GardenInteractionKind.Water).transform.position;
            var fire=GameObject.Find("Campfire").transform.position;
            var tent=GameObject.Find("Camp Cream Tent").transform;
            int count=0,target=expanded?3600:10500;
            for(var attempt=0;attempt<target*5&&count<target;attempt++)
            {
                var p=new Vector3(Rand(expanded?-3.3f:-6.3f,expanded?3.3f:6.3f),0,Rand(expanded?-2.8f:-5.4f,expanded?2.8f:5.4f));
                var radius=SandRadius(p,expanded);if(radius>.985f)continue;
                var w=p+origin;
                if(expanded&&InsideMain(w))continue;
                var dp=w-pond;if(dp.x*dp.x/1.6f+dp.z*dp.z/1.0f<1)continue;
                if(Vector3.Distance(w,seed)<.64f||Vector3.Distance(w,fire)<.73f)continue;
                var tp=tent.InverseTransformPoint(w);if(Mathf.Abs(tp.x)<1.3f&&Mathf.Abs(tp.z)<1.25f)continue;
                var patch=Mathf.PerlinNoise(w.x*1.4f+41,w.z*1.4f+5);
                float density=radius>.75f?.92f:.12f+Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.72f,patch))*.70f;
                if(Rand(0,1)>density)continue;
                var size=Rand(.012f,.042f);if(count%19==0)size=Rand(.045f,.072f);
                p.y=Height(w,radius)-.001f;
                var rotation=Quaternion.Euler(Rand(-12,12),Rand(0,360),Rand(-12,12));
                var scale=new Vector3(size,Rand(.005f,.014f),size*Rand(.60f,1.15f));
                var n=verts.Count;
                foreach(var vertex in sv)verts.Add(p+rotation*Vector3.Scale(vertex,scale));
                var indices=triangles[count%3];foreach(var index in st)indices.Add(n+index);
                count++;
            }
            var mesh=new Mesh{name=expanded?"Expansion real soil grains":"Starter real soil grains",indexFormat=IndexFormat.UInt32,subMeshCount=3};
            mesh.SetVertices(verts);for(var i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();
            mesh=Store(mesh,expanded?"ExpansionSoilGrains.asset":"StarterSoilGrains.asset");
            var go=new GameObject("Batched 3D soil grains");go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var grainRenderer=go.AddComponent<MeshRenderer>();grainRenderer.shadowCastingMode=ShadowCastingMode.Off;
            grainRenderer.sharedMaterials=new[]{Lit("Soil grain ochre",new Color(.83f,.65f,.39f)),Lit("Soil grain dust",new Color(.90f,.72f,.45f)),Lit("Soil grain clay",new Color(.75f,.55f,.30f))};
        }
        static void ConfigureSea(DawnGardenGame game,Transform decor)
        {
            // Reuse the garden's ordinary water surface and shift only its palette.
            var water=new Material(Shader.Find("Dawn Garden/Water"));water.name="Camp Ocean";
            water.SetColor("_BaseColor",new Color(.39f,.12f,.13f));
            water.SetColor("_ShallowColor",new Color(.72f,.31f,.25f));
            water.SetFloat("_WaveScale",.9f);water.SetFloat("_Daylight",1);
            water=StoreAsset(water,"Assets/DawnGarden/Art/EmberCamp/Camp Ocean.mat");GameObject.Find("Ocean").GetComponent<Renderer>().sharedMaterial=water;
            game.expansion.surroundingLabel="海面";
            var previous=decor.Find("Lava shore glow and cinders");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        }
        static T StoreAsset<T>(T value,string path) where T:UnityEngine.Object
        {
            var prior=AssetDatabase.LoadAssetAtPath<T>(path);
            if(prior==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,prior);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(prior);return prior;
        }
        static void RebuildNavigation(DawnGardenGame game)
        {
            var exp=game.expansion;var surface=exp.surface;
            foreach(var expanded in new[]{false,true})
            {
                exp.land.gameObject.SetActive(expanded);exp.landCollider.enabled=true;
                foreach(var stone in exp.seamStones)stone.SetActive(!expanded);
                Physics.SyncTransforms();surface.BuildNavMesh();var mesh=surface.navMeshData;surface.RemoveData();
                var previous=expanded?exp.expandedNavigation:exp.originalNavigation;
                EditorUtility.CopySerialized(mesh,previous);EditorUtility.SetDirty(previous);UnityEngine.Object.DestroyImmediate(mesh);
            }
            exp.land.gameObject.SetActive(false);foreach(var stone in exp.seamStones)stone.SetActive(true);
            surface.navMeshData=exp.originalNavigation;surface.AddData();
        }
    }
}

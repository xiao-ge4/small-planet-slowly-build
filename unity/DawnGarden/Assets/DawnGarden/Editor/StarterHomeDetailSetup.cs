using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace DawnGarden.Editor
{
    public static class StarterHomeDetailSetup
    {
        const string Art="Assets/DawnGarden/Art/StarterDetails";
        static readonly string[] Kits={"garden-understory-cluster","garden-flower-cluster","garden-rock-cluster","garden-grass-tuft"};
        static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        static string Theme(bool ember)=>ember?"Ember":"Dawn";
        static T Store<T>(T value,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;
        }
        public static void Import()
        {
            Directory.CreateDirectory(Art);
            foreach(var id in Kits)
            {
                var src=Path.Combine(Root,"artifacts/garden-detail-kit/v1",id,"lod0.glb");
                if(!File.Exists(src))throw new InvalidOperationException("Missing botanical kit: "+id);
                var dest=Art+"/"+id+".glb";
                if(!File.Exists(dest)||!File.ReadAllBytes(dest).SequenceEqual(File.ReadAllBytes(src)))File.Copy(src,dest,true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var id in Kits)if(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/"+id+".glb")==null)throw new InvalidOperationException("Botanical import failed: "+id);
        }
        static Material Matte(string name,Color color)
        {
            var shader=Shader.Find("Dawn Garden/Matte Detail");if(shader==null)throw new InvalidOperationException("Matte shader missing");
            var m=new Material(shader){name=name};m.SetColor("_BaseColor",color);m.enableInstancing=true;return Store(m,Art+"/"+name+".mat");
        }
        static Color SourceColor(Material source)
        {return source.HasProperty("baseColorFactor")?source.GetColor("baseColorFactor"):source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white;}
        static Material BotanicalMaterial(Material original,bool ember)
        {
            var name=original.name;Color c=SourceColor(original);
            if(ember)
            {
                if(name.Contains("Leaf")){var n=name.LastOrDefault();var palette=new[]{new Color(.29f,.23f,.055f),new Color(.45f,.35f,.08f),new Color(.61f,.46f,.12f),new Color(.68f,.49f,.17f),new Color(.56f,.42f,.10f),new Color(.37f,.31f,.07f),new Color(.73f,.56f,.21f)};c=palette[Mathf.Clamp(n-'0',0,6)];}
                else if(name.Contains("petal")){c=name.Contains("Pink")||name.Contains("Lilac")?new Color(.91f,.30f,.14f):name.Contains("Apricot")?new Color(.98f,.56f,.15f):new Color(.95f,.77f,.40f);}
                else if(name.Contains("Pollen"))c=new Color(.64f,.29f,.055f);
                else if(name.Contains("Stems"))c=new Color(.39f,.30f,.07f);
                else if(name.Contains("stone"))c=Color.Lerp(c,new Color(.67f,.35f,.21f),.50f);
            }
            return Matte(Theme(ember)+" "+name,c);
        }
        public static StarterHomeDetail Apply(DawnGardenGame game,bool ember)
        {
            var world=game.transform;var previous=world.Find("Starter landscape details");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root=new GameObject("Starter landscape details");root.transform.SetParent(world,false);
            root.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild=true;
            var detail=root.AddComponent<StarterHomeDetail>();detail.theme=ember?"ember":"dawn";
            var content=new GameObject("Botanicals, soil and small stones").transform;content.SetParent(root.transform,false);detail.content=content;
            var ground=GameObject.Find("Walkable Island").GetComponent<MeshCollider>();
            var pond=GameObject.Find("Pond Water").transform.position;
            var water=game.FindInteraction(GardenInteractionKind.Water).transform.position;var chest=game.FindInteraction(GardenInteractionKind.Chest).transform.position;
            float Height(Vector3 p){return ground.Raycast(new Ray(p+Vector3.up*8,Vector3.down),out var hit,20)?hit.point.y:.02f;}
            bool Clear(Vector3 p,float padding=.0f)
            {
                if(p.x*p.x/(5.7f*5.7f)+p.z*p.z/(4.7f*4.7f)>1)return false;
                var d=p-pond;if(d.x*d.x/(1.62f*1.62f)+d.z*d.z/(1.26f*1.26f)<1)return false;
                if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(water.x,water.z))<1.0f+padding||Vector2.Distance(new Vector2(p.x,p.z),new Vector2(chest.x,chest.z))<.85f+padding)return false;
                if(p.x>4.15f&&Mathf.Abs(p.z)<1.25f)return false;
                if(ember&&p.x>.95f&&p.x<3.85f&&p.z>-2.7f&&p.z<.12f)return false;
                if(ember&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(2.7f,1.8f))<1.15f)return false;
                return true;
            }
            int serial=0;
            void Put(int kit,float x,float z,float scale)
            {
                var p=new Vector3(x,0,z);if(!Clear(p,.06f))return;
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/"+Kits[kit]+".glb");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(model);go.name=Theme(ember)+" botanical pocket "+serial++;go.transform.SetParent(content,false);p.y=Height(p)+.014f;go.transform.position=p;go.transform.localScale=Vector3.one*scale;go.transform.rotation=Quaternion.Euler(0,serial*137.5f,0);
                foreach(var c in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
                foreach(var r in go.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>BotanicalMaterial(m,ember)).ToArray();r.shadowCastingMode=ShadowCastingMode.On;}
                detail.botanicalGroups++;
            }
            var pockets=ember?new[]{new Vector3(-4.0f,0,-2.9f),new Vector3(-4.9f,0,-1.5f),new Vector3(-3.2f,0,-3.3f),new Vector3(-1.2f,0,-3.9f),new Vector3(.8f,0,-4.0f),new Vector3(2.6f,0,-4.0f),new Vector3(3.8f,0,-3.0f),new Vector3(4.7f,0,-1.6f),new Vector3(4.3f,0,2.8f),new Vector3(-4.4f,0,.8f),new Vector3(-3.6f,0,3.1f),new Vector3(-1.5f,0,3.4f),new Vector3(1.5f,0,3.4f)}
                :new[]{new Vector3(-2.3f,0,1.7f),new Vector3(-3.7f,0,2.7f),new Vector3(-4.7f,0,-.7f),new Vector3(-3.6f,0,-3.5f),new Vector3(4.1f,0,-2.8f),new Vector3(4.7f,0,-1.65f),new Vector3(3.7f,0,3.25f),new Vector3(-3.3f,0,.25f),new Vector3(-1.8f,0,-3.65f),new Vector3(1.1f,0,-4.0f),new Vector3(1.45f,0,2.9f),new Vector3(-.5f,0,3.2f)};
            for(int k=0;k<pockets.Length;k++)
            {
                var p=pockets[k];float s=k%3==0?.76f:.59f;
                Put(k%4==3?2:0,p.x,p.z,s);
                Put(1,p.x+.35f,p.z+.28f,s*.89f);
                if(k%2==0)Put(3,p.x-.25f,p.z+.4f,.8f);
            }
            // Soil patches are feathered and conform to the original ground;
            // the collider and all interactive objects remain authoritative.
            var blend=new Material(Shader.Find("Dawn Garden/Soft Ground Detail")){name=Theme(ember)+" soft earth"};
            blend.SetColor("_BaseColor",ember?new Color(.56f,.32f,.14f,.32f):new Color(.55f,.40f,.22f,.52f));blend=Store(blend,Art+"/"+blend.name+".mat");
            var vs=new List<Vector3>();var cs=new List<Color>();var ts=new List<int>();
            void Patch(Vector3 center,float rx,float rz,float opacity)
            {
                int start=vs.Count;center.y=Height(center)+.013f;vs.Add(center);cs.Add(new Color(1,1,1,opacity));const int n=36;
                for(int ring=1;ring<=3;ring++)for(int k=0;k<n;k++){float a=k*Mathf.PI*2/n,r=ring/3f*(1+.07f*Mathf.Sin(a*5+center.x));var p=center+new Vector3(Mathf.Cos(a)*rx*r,0,Mathf.Sin(a)*rz*r);p.y=Height(p)+.013f;vs.Add(p);cs.Add(new Color(1,1,1,ring==3?0:opacity*(1-ring*.22f)));}
                for(int k=0;k<n;k++)ts.AddRange(new[]{start,start+1+(k+1)%n,start+1+k});
                for(int ring=1;ring<3;ring++)for(int k=0;k<n;k++){int a=start+1+(ring-1)*n+k,b=start+1+(ring-1)*n+(k+1)%n;ts.AddRange(new[]{a,b,b+n,a,b+n,a+n});}
            }
            foreach(var p in pockets)if(Clear(p))Patch(p,.86f,.66f,.62f);
            var path=new[]{new Vector3(0,0,3.25f),new Vector3(.2f,0,2.15f),new Vector3(.1f,0,.95f),new Vector3(0,0,-.2f),new Vector3(-.5f,0,-1.0f),water};
            for(int k=0;k<path.Length-1;k++)for(int j=0;j<4;j++){var p=Vector3.Lerp(path[k],path[k+1],j/4f);if(Clear(p))Patch(p,.54f,.64f,ember?.24f:.75f);}
            Geometry("Feathered soil and walking trail",vs,ts,blend,content,cs,ember);
            var rng=new System.Random(ember?2701:1208);float Rand(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)rng.NextDouble());
            var grainVerts=new List<Vector3>();var grainTriangles=new List<int>();var grassVerts=new List<Vector3>();var grassTriangles=new List<int>();
            for(int i=0;i<(ember?8500:7000);i++)
            {
                var p=new Vector3(Rand(-5.8f,5.8f),0,Rand(-4.8f,4.8f));if(!Clear(p))continue;p.y=Height(p)+.009f;
                if(i%4==0)
                {
                    float r=Rand(.012f,.043f),h=r*Rand(.18f,.42f);int a=grainVerts.Count;grainVerts.Add(p+Vector3.up*h);
                    for(int k=0;k<6;k++){float angle=k*Mathf.PI/3;grainVerts.Add(p+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r*.75f));}
                    for(int k=0;k<6;k++)grainTriangles.AddRange(new[]{a,a+1+(k+1)%6,a+1+k});detail.grains++;
                }
                else
                {
                    if(ember&&Mathf.PerlinNoise(p.x*.9f+6,p.z*.9f+5)<.58f)continue;
                    if(Mathf.Abs(p.x)<.65f&&p.z>-.4f)continue;
                    float angle=Rand(0,Mathf.PI*2),h=Rand(.024f,.072f),w=Rand(.003f,.011f);var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*w;int a=grassVerts.Count;grassVerts.AddRange(new[]{p-side,p+side,p+Vector3.up*h+side});grassTriangles.AddRange(new[]{a,a+2,a+1});detail.grassBlades++;
                }
            }
            Geometry("Fine three dimensional ground grains",grainVerts,grainTriangles,Matte(Theme(ember)+" fine gravel",ember?new Color(.68f,.48f,.26f):new Color(.54f,.50f,.32f)),content,null,ember);
            Geometry("Fine three dimensional meadow blades",grassVerts,grassTriangles,Matte(Theme(ember)+" fine grass",ember?new Color(.65f,.48f,.16f):new Color(.32f,.47f,.12f)),content,null,ember);
            return detail;
        }
        static void Geometry(string name,List<Vector3> v,List<int> t,Material material,Transform parent,List<Color> colors,bool ember)
        {
            var mesh=new Mesh{name=Theme(ember)+" "+name,indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(v);mesh.SetTriangles(t,0);if(colors!=null)mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=Store(mesh,Art+"/"+mesh.name+".asset");
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
}

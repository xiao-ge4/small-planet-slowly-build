using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    // Visual layers are attached to the land, so rising, travel and save restoration
    // all use the same coordinates. The existing walkable collider stays authoritative.
    public sealed class SmallWorldGardenSurface : MonoBehaviour
    {
        public const string Style="garden-meadow-v1";
        Transform land;GameObject detail;Renderer baseRenderer;Material ground;readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        Transform ocean;Vector3 originalOceanPosition;
        public bool Active=>detail!=null&&detail.activeSelf;
        public int GroundVertices {get;private set;}
        // The adjoining surveyed apron uses this same palette and world-space
        // origin. Its edge is no longer a bright circular carpet on white paving.
        public static void ConfigureMeadowMaterial(Material material,Vector3 origin)
        {
            // Color properties are converted for the linear shader by Unity.
            // Applying Color.linear here again turns the soil almost black.
            material.SetColor("_Grass",new Color(.45f,.57f,.34f));
            material.SetColor("_LightGrass",new Color(.58f,.66f,.43f));
            material.SetColor("_Earth",new Color(.72f,.62f,.46f));
            material.SetVector("_Origin",origin);
        }
        public void RestoreMeadowLook()
        {
            if(!Active||ground==null)return;
            ConfigureMeadowMaterial(ground,land.position);
            var renderer=detail.transform.Find("Meadow and footpath").GetComponent<Renderer>();
            renderer.sharedMaterial=ground;
            foreach(Transform child in detail.transform)child.gameObject.SetActive(true);
            HideBuriedIslandTrim();
        }
        public void Apply(Transform target,string style,IEnumerable<SmallWorldItem> items)
        {
            if(land==null){land=target;baseRenderer=land.GetComponent<Renderer>();ocean=GameObject.Find("Ocean")?.transform;if(ocean!=null)originalOceanPosition=ocean.position;}
            bool enabled=style==Style;if(detail==null&&enabled)Build();
            if(detail!=null)detail.SetActive(enabled);if(baseRenderer!=null)baseRenderer.enabled=!enabled;
            if(enabled)HideBuriedIslandTrim();
            if(ocean!=null)ocean.position=originalOceanPosition+(enabled?Vector3.down*.48f:Vector3.zero);
            if(!enabled||ground==null)return;
            ConfigureMeadowMaterial(ground,land.position);
            var anchors=items.Where(i=>!i.assetId.StartsWith("garden-grass")&&!i.assetId.StartsWith("meadow-flower")).Take(32).Select(i=>new Vector4(i.localPosition.x,0,i.localPosition.z,i.assetId.Contains("cluster")?.85f:.6f)).ToArray();
            var block=new MaterialPropertyBlock();block.SetInt("_ContactCount",anchors.Length);var padded=new Vector4[32];Array.Copy(anchors,padded,anchors.Length);block.SetVectorArray("_ContactPoints",padded);
            detail.transform.Find("Meadow and footpath").GetComponent<Renderer>().SetPropertyBlock(block);
        }
        void HideBuriedIslandTrim()
        {
            if(detail==null)return;
            bool joined=GameObject.Find("花园承接带 · 与原地块连续的草土")!=null;
            // The old free-standing island had a raised dark-green turf wall
            // at y=.05. Here it sticks above the joined lawn (.029..047) and
            // outlines the old ellipse. Its coastal grass no longer faces sea.
            // Keep those layers on standalone islands, not inside a level apron.
            foreach(string name in new[]{"Layered cliff 3","Coastal grass silhouette","Fine meadow grass"})
            {
                var child=detail.transform.Find(name);
                if(child!=null)child.gameObject.SetActive(!joined);
            }
            // Scattered grains/pebbles remain genuine small 3D surface detail;
            // thousands of subpixel grass triangles are omitted in this court.
        }
        static float Edge(float a)=>1+.02f*Mathf.Sin(a*3)+.012f*Mathf.Cos(a*7);
        Material Material(string name,Color color)
        {
            var source=Resources.Load<Material>("GardenDetail/Matte");if(source==null)throw new InvalidOperationException("Garden detail material is missing.");
            var m=new Material(source){name=name};m.SetColor("_BaseColor",color);owned.Add(m);return m;
        }
        GameObject Geometry(string name,List<Vector3> vertices,List<int> triangles,Material material,Transform parent)
        {
            var mesh=new Mesh {name=name,indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            var go=new GameObject(name);go.layer=2;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;return go;
        }
        void Build()
        {
            detail=new GameObject("Living garden surface");detail.transform.SetParent(land,false);
            var source=Resources.Load<Material>("GardenDetail/Meadow");if(source==null)throw new InvalidOperationException("Garden surface material was not built.");ground=new Material(source);owned.Add(ground);ConfigureMeadowMaterial(ground,land.position);
            var v=new List<Vector3>{new Vector3(0,.029f,0)};var t=new List<int>();const int n=144,rings=25;
            for(int ring=1;ring<=rings;ring++)for(int k=0;k<n;k++)
            {float a=k*Mathf.PI*2/n,r=ring/(float)rings,edge=Edge(a);float x=Mathf.Cos(a)*6.15f*edge*r,z=Mathf.Sin(a)*5.15f*edge*r;float h=.029f+Mathf.PerlinNoise(x*.8f+13,z*.8f+20)*.018f;v.Add(new Vector3(x,h,z));}
            for(int k=0;k<n;k++)t.AddRange(new[]{0,1+(k+1)%n,1+k});
            for(int ring=1;ring<rings;ring++)for(int k=0;k<n;k++){int a=1+(ring-1)*n+k,b=1+(ring-1)*n+(k+1)%n,c=a+n,d=b+n;t.AddRange(new[]{a,b,d,a,d,c});}
            GroundVertices=v.Count;Geometry("Meadow and footpath",v,t,ground,detail.transform);
            var earth=new[]{Material("Ochre soil upper",new Color(.44f,.31f,.17f)),Material("Earth middle",new Color(.34f,.25f,.16f)),Material("Cool earth lower",new Color(.29f,.24f,.18f)),Material("Fresh turf edge",new Color(.34f,.43f,.10f))};
            for(int band=0;band<4;band++)
            {
                var vs=new List<Vector3>();var ts=new List<int>();
                float[] levels={-.12f,-.46f,-.82f,-1.19f};float y0=band==3?.05f:levels[band],y1=band==3?levels[0]:levels[band+1];
                for(int k=0;k<n;k++)
                {
                    float a=k*Mathf.PI*2/n,b=(k+1)*Mathf.PI*2/n;
                    // The connection is handled by its continuous waist mesh.
                    if(Mathf.Cos(a)<-.88f&&Mathf.Abs(Mathf.Sin(a))<.53f)continue;
                    int j=vs.Count;
                    foreach(var pair in new[]{new Vector2(a,y0),new Vector2(b,y0),new Vector2(b,y1),new Vector2(a,y1)})
                    {float depth=Mathf.Clamp01(-pair.y/.2f);float inset=1+Mathf.Min(0,pair.y)*.065f,edge=Edge(pair.x)+depth*.006f*Mathf.Sin(pair.x*23);vs.Add(new Vector3(Mathf.Cos(pair.x)*6.15f*edge*inset,pair.y+depth*.035f*Mathf.Sin(pair.x*17),Mathf.Sin(pair.x)*5.15f*edge*inset));}
                    ts.AddRange(new[]{j,j+1,j+2,j,j+2,j+3});
                }
                Geometry("Layered cliff "+band,vs,ts,earth[band],detail.transform);
            }
            BuildSmallDetails(detail.transform);
        }
        void BuildSmallDetails(Transform root)
        {
            var rng=new System.Random(4871);float Next(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)rng.NextDouble());
            var pebble=Material("Tiny warm stones",new Color(.58f,.53f,.39f));var verts=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<420;i++)
            {
                var p=new Vector3(Next(-5.5f,5.5f),.048f,Next(-4.7f,4.7f));if(p.x*p.x/30+p.z*p.z/22>1)continue;
                float pathZ=.35f*Mathf.Sin(p.x*.65f)+.13f*(p.x+3);if(Mathf.Abs(p.z-pathZ)>.9f&&rng.NextDouble()>.18)continue;
                float r=Next(.018f,.068f),h=r*Next(.2f,.55f);int start=verts.Count;
                verts.Add(p+Vector3.up*h);for(int k=0;k<7;k++){float a=k*Mathf.PI*2/7;verts.Add(p+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*.7f));}
                for(int k=0;k<7;k++)triangles.AddRange(new[]{start,start+1+k,start+1+(k+1)%7});
            }
            Geometry("Scattered grains and pebbles",verts,triangles,pebble,root);
            var lawn=Material("Fine meadow blades",new Color(.46f,.53f,.33f));verts=new List<Vector3>();triangles=new List<int>();
            for(int i=0;i<7000;i++)
            {
                var p=new Vector3(Next(-6,6),.047f,Next(-5,5));if(p.x*p.x/35+p.z*p.z/24>.98f)continue;
                float pathZ=.35f*Mathf.Sin(p.x*.65f)+.13f*(p.x+3);
                if(p.x<2&&Mathf.Abs(p.z-pathZ)<.65f)continue;
                if(Mathf.PerlinNoise(p.x*.7f+15,p.z*.8f+9)<.46f)continue;
                float a=Next(0,Mathf.PI*2),h=Next(.035f,.085f),w=Next(.007f,.018f);var side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*w;var tip=p+Vector3.up*h+side*.4f;int j=verts.Count;
                verts.AddRange(new[]{p-side,p+side,tip});triangles.AddRange(new[]{j,j+2,j+1});
            }
            var fineGrass=Geometry("Fine meadow grass",verts,triangles,lawn,root);fineGrass.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            // Small blades and creeping leaves at the coast break its perfect silhouette.
            var green=Material("Turf blades",new Color(.42f,.50f,.29f));verts=new List<Vector3>();triangles=new List<int>();
            for(int i=0;i<800;i++)
            {
                float a=Next(0,Mathf.PI*2),r=Next(.93f,1);if(Mathf.Cos(a)<-.88f&&Mathf.Abs(Mathf.Sin(a))<.53f)continue;
                if(Mathf.Sin(a*7)+Mathf.Cos(a*11)<-.15f)continue;
                Vector3 p=new Vector3(Mathf.Cos(a)*6.15f*Edge(a)*r,.04f,Mathf.Sin(a)*5.15f*Edge(a)*r);float h=Next(.025f,.09f),w=Next(.012f,.03f);int j=verts.Count;Vector3 side=new Vector3(Mathf.Cos(a+1),0,Mathf.Sin(a+1))*w;
                var tip=p+new Vector3(Mathf.Cos(a)*.04f,h,Mathf.Sin(a)*.04f);
                verts.AddRange(new[]{p-side,p+side,tip});triangles.AddRange(new[]{j,j+2,j+1});
            }
            Geometry("Coastal grass silhouette",verts,triangles,green,root);
        }
        void OnDestroy(){foreach(var o in owned)if(o!=null){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}}
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        readonly Dictionary<Material,Material> coastDryRockMaterials=new();

        // Imported moss/stone albedo is valuable, but its small-prop roughness looks varnished
        // when a boulder becomes a five-metre cave roof. Keep the shared asset untouched.
        void MakeCoastRockMatte(GameObject model)
        {
            if(model==null)return;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var source=renderer.sharedMaterials;
                for(int i=0;i<source.Length;i++)
                {
                    if(source[i]==null)continue;
                    if(!coastDryRockMaterials.TryGetValue(source[i],out var dry))
                    {
                        dry=new Material(source[i]){name=source[i].name+" · 海崖干石苔面"};
                        foreach(var property in new[]{"_Smoothness","_Glossiness","_GlossMapScale"})
                            if(dry.HasProperty(property))dry.SetFloat(property,.10f);
                        foreach(var property in new[]{"roughnessFactor","_RoughnessFactor"})
                            if(dry.HasProperty(property))dry.SetFloat(property,.94f);
                        foreach(var property in new[]{"metallicFactor","_Metallic","_MetallicFactor","clearcoatFactor"})
                            if(dry.HasProperty(property))dry.SetFloat(property,0);
                        // The source's packed ORM can otherwise override the increased factor.
                        if(dry.HasProperty("metallicRoughnessTexture"))
                            dry.SetTexture("metallicRoughnessTexture",Texture2D.whiteTexture);
                        if(dry.HasProperty("_MetallicGlossMap"))dry.SetTexture("_MetallicGlossMap",null);
                        dry.DisableKeyword("_METALLICSPECGLOSSMAP");
                        coastDryRockMaterials[source[i]]=dry;materials.Add(dry);
                    }
                    source[i]=dry;
                }
                renderer.sharedMaterials=source;
            }
        }

        void BuildCoastGeologyFinish()
        {
            var root=Group("岸肩地质层 · 宽岩板与斜裂面",details);
            var face=Paint("干燥暖灰石层",new Color(.72f,.70f,.63f),.06f,.12f);
            var pale=Paint("新断浅灰石面",new Color(.81f,.78f,.69f),.06f,.10f);
            var shade=Paint("层缝深灰岩",new Color(.55f,.58f,.53f),.05f,.10f);
            var leaves=Paint("岩隙垂生圆叶",new Color(.36f,.48f,.18f),.04f,.08f);
            int seed=901;
            foreach(var line in CliffShoulderCoverage)
            {
                // Seven connected geological groups per wing, with a staggered lower bed.
                // Unlike a necklace of rocks, long cap planes share a bedding direction, while
                // skewed fracture ends break their silhouette. The closed original skin stays.
                for(int group=0;group<7;group++)
                {
                    float x=Mathf.Lerp(line[0].x+.60f,line[line.Length-1].x-.45f,(group+.15f)/7f);
                    float crest=0;
                    for(int part=0;part<line.Length-1;part++)
                    {
                        if(x<line[part].x||x>line[part+1].x)continue;
                        crest=Mathf.Lerp(line[part].y,line[part+1].y,Mathf.InverseLerp(line[part].x,line[part+1].x,x));
                        break;
                    }
                    float width=2.45f+.43f*Mathf.Sin(group*2.7f);
                    float yaw=(line[0].x<0?-8:17)+9*Mathf.Sin(group*1.9f);
                    var upper=new Vector2(x,crest+2.55f+.18f*Mathf.Sin(group));
                    BedrockSlab(root,upper,width,1.72f,.63f,yaw,seed++,group%3==0?pale:face);
                    var lower=new Vector2(x+.28f,crest+4.02f+.17f*Mathf.Cos(group));
                    BedrockSlab(root,lower,width*1.08f,1.85f,.50f,yaw-7,seed++,group%3==1?pale:face);
                    if(group%2==0)
                    {
                        var fracture=new Vector2(x-.55f,crest+3.28f);
                        BedrockSlab(root,fracture,1.24f,.92f,.82f,yaw+26,seed++,shade);
                    }
                    if(group%3==1)
                        CoastRockVine(root,new Vector3(x-.32f,CoastHeight(x-.32f,crest+3.15f)+.55f,crest+3.15f),group,leaves);
                }
            }
            CombineLandformRocks(root);
        }

        void BedrockSlab(Transform root,Vector2 center,float width,float depth,float relief,float yaw,int seed,Material material)
        {
            // Open circulation remains visually clear as well as physically unchanged.
            foreach(var road in CoastTracks)
            for(int i=0;i<road.Points.Length-1;i++)
                if(SegmentDistanceSquared(center,road.Points[i],road.Points[i+1],out _)<
                    Mathf.Pow(road.HalfWidth+depth*.26f,2))return;
            var ring=new[]{new Vector2(-.50f,-.30f),new Vector2(-.33f,-.50f),new Vector2(.34f,-.48f),
                new Vector2(.50f,-.27f),new Vector2(.47f,.31f),new Vector2(.29f,.50f),new Vector2(-.36f,.46f),new Vector2(-.50f,.24f)};
            var rotation=Quaternion.Euler(0,yaw,0);
            float baseHeight=CoastHeight(center.x,center.y);
            var bottom=new Vector3[8];var shoulder=new Vector3[8];var cap=new Vector3[8];
            for(int i=0;i<8;i++)
            {
                var offset=rotation*new Vector3(ring[i].x*width,0,ring[i].y*depth);
                float x=center.x+offset.x,z=center.y+offset.z;
                float ground=CoastHeight(x,z);
                // A rock bed keeps its own nearly horizontal cleavage plane. Conforming each
                // cap corner to the slope made tall triangular fins at steep terrain cuts.
                // The uphill half instead embeds in the existing closed rock shoulder.
                float top=baseHeight+relief+ring[i].x*.10f+ring[i].y*.08f+.025f*Mathf.Sin(seed+i*1.7f);
                bottom[i]=new Vector3(x,Mathf.Min(ground-.48f,top-.60f),z);
                shoulder[i]=new Vector3(x,top-.12f,z);
                cap[i]=new Vector3(center.x+offset.x*.88f,top,center.y+offset.z*.88f);
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            void Tri(Vector3 a,Vector3 b,Vector3 c)
            {
                int at=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.AddRange(new[]{at,at+1,at+2});
                foreach(var p in new[]{a,b,c})uv.Add(new Vector2(p.x*.55f+p.z*.18f,p.y*.65f));
            }
            for(int i=0;i<8;i++)
            {
                int j=(i+1)%8;
                Tri(bottom[i],shoulder[i],bottom[j]);Tri(bottom[j],shoulder[i],shoulder[j]);
                Tri(shoulder[i],cap[i],shoulder[j]);Tri(shoulder[j],cap[i],cap[j]);
            }
            for(int i=1;i<7;i++)
            {Tri(cap[0],cap[i+1],cap[i]);Tri(bottom[0],bottom[i],bottom[i+1]);}
            MeshObject("嵌入岸肩的宽层理断岩",root,vertices,triangles,material,false,uv);
        }

        void CoastRockVine(Transform root,Vector3 start,int seed,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var mapping=new List<Vector2>();
            for(int leafIndex=0;leafIndex<15;leafIndex++)
            {
                float t=leafIndex/14f;
                var p=start+new Vector3(.11f*Mathf.Sin(t*9+seed)+(leafIndex%2==0?-.09f:.09f),-t*.85f,t*.26f);
                float size=.075f+.025f*Mathf.Sin(leafIndex*2.1f);
                var a=new Vector3(size,0,0);var b=new Vector3(0,size*.78f,-size*.45f);
                for(int side=0;side<2;side++)
                {
                    int at=vertices.Count;vertices.Add(p+Vector3.forward*(side==0?.026f:-.026f));mapping.Add(new Vector2(.5f,.5f));
                    for(int j=0;j<8;j++)
                    {float angle=j*Mathf.PI/4;vertices.Add(p+a*Mathf.Cos(angle)+b*Mathf.Sin(angle));mapping.Add(new Vector2(Mathf.Cos(angle)*.5f+.5f,Mathf.Sin(angle)*.5f+.5f));}
                    for(int j=0;j<8;j++)triangles.AddRange(side==0?new[]{at,at+j+1,at+(j+1)%8+1}:new[]{at,at+(j+1)%8+1,at+j+1});
                }
            }
            MeshObject("石缝稀疏垂叶",root,vertices,triangles,material,false,mapping);
        }
    }
}

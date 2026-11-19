using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        sealed class CoastBotanyBatch
        {
            public readonly List<Vector3> vertices = new();
            public readonly List<int> indices = new();
            public void Petal(Vector3 basePoint, Vector3 tip, float width, float cup)
            {
                var along = tip - basePoint;
                var side = Vector3.Cross(along.normalized, Vector3.up);
                if(side.sqrMagnitude < .01f) side = Vector3.right;
                side = side.normalized * width;
                int first=vertices.Count;
                for(int section=0;section<=3;section++)
                {
                    float t=section/3f, breadth=Mathf.Sin(t*Mathf.PI);
                    var centre=basePoint+along*t+Vector3.up*(cup*breadth);
                    vertices.Add(centre-side*breadth);
                    vertices.Add(centre+Vector3.up*(width*.18f*breadth));
                    vertices.Add(centre+side*breadth);
                    if(section==0)continue;
                    int p=first+(section-1)*3,n=p+3;
                    indices.AddRange(new[]{p,p+1,n,p+1,n+1,n,p+1,p+2,n+1,p+2,n+2,n+1});
                }
            }
        }

        void BuildLivingGarden()
        {
            var root = Group("海岸花园 · 岩缝与道路的连续种植",details);
            BuildGardenPathEdges();
            var palette = new[]{
                Paint("花园 · 墨绿基叶",new Color(.31f,.47f,.14f),.10f,.12f),
                Paint("花园 · 翡翠叶",new Color(.42f,.60f,.20f),.14f,.10f),
                Paint("花园 · 晨光叶",new Color(.58f,.71f,.28f),.12f,.09f),
                Paint("花园 · 奶油小花",new Color(.98f,.88f,.61f),.13f,.05f),
                Paint("花园 · 日光黄",new Color(.98f,.66f,.13f),.12f,.05f),
                Paint("花园 · 薰衣草",new Color(.54f,.34f,.68f),.12f,.07f),
                Paint("花园 · 玫瑰粉",new Color(.88f,.43f,.55f),.12f,.06f)
            };
            foreach(var material in palette) material.SetFloat("_Cull",0);
            var batches = new CoastBotanyBatch[palette.Length];
            for(int i=0;i<batches.Length;i++) batches[i]=new CoastBotanyBatch();
            var random=new System.Random(810532);
            float R(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());

            // The distribution follows the same heightfield and protected paths as the playable
            // terrain. Clusters, not a uniform lawn, leave readable clearings and usable parcels.
            for(float z=-46;z<23;z+=.77f)
            for(float x=-34;x<50;x+=.77f)
            {
                float px=x+R(-.35f,.35f), pz=z+R(-.35f,.35f);
                float y=SampleCoast(px,pz,out int surface,out bool wet,out float road,out _,out _);
                if(wet || y < -.55f || road>.12f || DistanceFromShore(new Vector2(px,pz))<.8f) continue;
                if(px>-7.5f&&px<19.6f&&pz>-6.2f&&pz<7.2f) continue;
                if(px>11&&px<23&&pz>9&&pz<16.5f)continue;
                bool onPad=false;
                foreach(var pad in CoastPads)
                    if(Mathf.Abs(px-pad.Center.x)<pad.HalfSize.x*.94f && Mathf.Abs(pz-pad.Center.z)<pad.HalfSize.y*.94f) onPad=true;
                if(onPad)continue;
                float clustering=Mathf.PerlinNoise(px*.19f+35,pz*.17f+49);
                if(clustering<.39f || R(0,1)>.74f) continue;
                var basePoint=new Vector3(px,y+.025f,pz);
                float size=R(.74f,1.12f), height=R(.20f,.43f);
                // Upright spikes alternate with low flowering cushions, avoiding cloned rows.
                int bloom=3+Mathf.Min(3,Mathf.FloorToInt(Mathf.PerlinNoise(px*.11f+7,pz*.10f+25)*6));
                for(int leafIndex=0;leafIndex<31;leafIndex++)
                {
                    float angle=R(0,Mathf.PI*2), length=R(.12f,.34f)*size;
                    float ox=R(-.30f,.30f),oz=R(-.30f,.30f);
                    var origin=basePoint+new Vector3(ox,height*Mathf.Max(0,1-(ox*ox+oz*oz)/.19f),oz);
                    var tip=origin+new Vector3(Mathf.Cos(angle)*length,R(.09f,height),Mathf.Sin(angle)*length);
                    batches[random.Next(3)].Petal(origin,tip,length*.30f,length*.18f);
                }
                int blossoms=clustering>.52f?random.Next(4,10):random.Next(1,4);
                for(int blossom=0;blossom<blossoms;blossom++)
                {
                    var flower=basePoint+new Vector3(R(-.34f,.34f)*size,height+R(.08f,.20f),R(-.34f,.34f)*size);
                    batches[0].Petal(flower-Vector3.up*(height+.16f),flower,.012f,0);
                    float radius=R(.075f,.14f)*size;
                    if(bloom==5 && blossom%2==0)
                    {
                        for(int tier=0;tier<5;tier++) for(int p=0;p<4;p++)
                        {
                            float a=p*Mathf.PI*.5f+tier*.71f;
                            var at=flower+Vector3.up*tier*.08f;
                            batches[bloom].Petal(at,at+new Vector3(Mathf.Cos(a)*radius,.045f,Mathf.Sin(a)*radius),radius*.52f,.025f);
                        }
                    }
                    else for(int petal=0;petal<6;petal++)
                    {
                        float a=petal*Mathf.PI/3;
                        batches[bloom].Petal(flower,flower+new Vector3(Mathf.Cos(a)*radius,.028f,Mathf.Sin(a)*radius),radius*.48f,.035f);
                    }
                    batches[4].Petal(flower+new Vector3(-.027f,.012f,0),flower+new Vector3(.027f,.012f,0),.032f,.015f);
                }
            }
            for(int i=0;i<batches.Length;i++)
            {
                var b=batches[i]; if(b.vertices.Count==0)continue;
                var mesh=new Mesh{name=palette[i].name,indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(b.vertices);mesh.SetTriangles(b.indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                var obj=Group(palette[i].name,root).gameObject;
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=palette[i];
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }

            // Deliberate trees frame the valley. The two existing home/plot transforms remain intact.
            var trees=new[]{new Vector3(-28,0,-37),new Vector3(-31,0,-29),new Vector3(-29,0,-16),
                new Vector3(-9,0,-37),new Vector3(-2,0,-43),new Vector3(9,0,-40),new Vector3(15,0,-32),
                new Vector3(17,0,-23),new Vector3(43,0,-33),new Vector3(46,0,-21),new Vector3(41,0,-15),
                new Vector3(41,0,2),new Vector3(31,0,13),new Vector3(-10,0,9),new Vector3(-10,0,-6)};
            for(int i=0;i<trees.Length;i++)
            {
                var p=trees[i];p.y=CoastHeight(p.x,p.z)-.07f;
                if(!LandformClear(p.x,p.z,1.05f))continue;
                Asset("garden-tree",root,p,1.6f+R(0,.65f),i*73,false);
            }
            // Reuse the detailed flower asset at eye-level anchors, interwoven with the light
            // batched undergrowth, so each vista contains a few larger readable flower heads.
            var flowers=new[]{new Vector2(-8,7),new Vector2(-9,-3),new Vector2(-10,-10),new Vector2(-16,-13),
                new Vector2(-29,-22),new Vector2(-28,-35),new Vector2(-14,-35),new Vector2(-8,-28),
                new Vector2(3,-23),new Vector2(10,-30),new Vector2(17,-29),new Vector2(25,-43),
                new Vector2(39,-36),new Vector2(44,-20),new Vector2(39,-11),new Vector2(36,8),
                new Vector2(29,18),new Vector2(4,18),new Vector2(-5,17),new Vector2(1,-9)};
            for(int i=0;i<flowers.Length;i++)
            {
                var p=flowers[i];float y=CoastHeight(p.x,p.y);
                if(y<-.55f || !LandformClear(p.x,p.y,.35f))continue;
                Asset("meadow-flower",root,new Vector3(p.x,y+.025f,p.y),.85f+R(0,.35f),i*61,false);
            }
        }

        void BuildGardenPathEdges()
        {
            var border=Group("旧石路缘 · 随地形镶砌",details);
            int seed=184;
            foreach(var pathLine in CoastTracks)
            for(int part=0;part<pathLine.Points.Length-1;part++)
            {
                var start=pathLine.Points[part];var end=pathLine.Points[part+1];
                var heading=end-start;heading.y=0;
                var side=Vector3.Cross(Vector3.up,heading.normalized);
                int count=Mathf.CeilToInt(heading.magnitude/.76f);
                foreach(float sign in new[]{-1f,1f})
                for(int i=0;i<count;i++)
                {
                    if((i+part)%9==0)continue;
                    var centre=Vector3.Lerp(start,end,(i+.5f)/count);
                    var p=centre+side*(sign*(pathLine.HalfWidth*.70f+.08f));
                    if(p.x>5.15f && p.x<18.4f && Mathf.Abs(p.z)<6.0f)continue;
                    float y=CoastHeight(p.x,p.z);
                    if(Mathf.Abs(y-centre.y)>.6f || y<-.62f)continue;
                    p.y=y+.045f;
                    SampleStone(border,"风化道路镶石",p,new Vector3(.20f,.135f,heading.magnitude/count*.91f),seed++,
                        seed%5==0?sandstone:limestone,Quaternion.LookRotation(heading));
                }
            }
            CombineLandformRocks(border);
        }
    }
}

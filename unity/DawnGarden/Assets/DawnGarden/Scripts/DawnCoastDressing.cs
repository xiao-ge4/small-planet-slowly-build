using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        void DressCoast()
        {
            var rockface = Paint("风化浅砂岩", new Color(.67f,.61f,.47f),.14f,.31f);
            var submerged = Paint("潮线深岩", new Color(.40f,.46f,.44f),.23f,.24f);
            var grassMat = Paint("平台边缘植被", new Color(.51f,.66f,.32f),.08f,.25f);
            var lip = Group("岩层、坡肩和潮间带",details);
            Shelf(lip, new Vector3(-15,-.17f,-14.4f), new Vector2(15,20), -.92f,1.72f,rockface);
            Shelf(lip, new Vector3(29.5f,-.16f,-18.8f),new Vector2(15.7f,24),-.95f,2.92f,rockface);
            Shelf(lip, new Vector3(19.1f,-.54f,15.3f),new Vector2(19.3f,6.7f),-1.07f,3.7f,submerged);
            Shelf(lip, new Vector3(38.6f,-.49f,13.5f),new Vector2(12f,7.5f),-1.12f,5.1f,sandstone);
            foreach (var (center, rim) in shores)
            {
                if (center.x == 3 && center.z == -12) continue;
                for(int i=0;i<rim.Count;i++)
                {
                    var crest = rim[i];
                    var outward = (crest-center).normalized;
                    float weight = Mathf.PerlinNoise(center.x*.18f + 6.8f,i*.37f);
                    if (weight < .36f) continue;
                    float width = Mathf.Lerp(.67f,2.45f,Mathf.InverseLerp(.35f,.76f,weight));
                    float height = Mathf.Min(4.2f,(center.y+1.18f)*(.79f+.24f*weight));
                    var point = crest + outward*(.8f+width*.22f) - Vector3.up*(height*.55f + .12f);
                    PolygonRock(lip,point,new Vector3(width,height, width*.7f),i*.47f,unchecked(i*743+128),i%4<2?rockface:cliff);
                    if (i%3!=1 && center.y<3)
                    {
                        point += outward*(width*.48f+.17f);
                        point.y=-1.14f + (float)scatter.NextDouble()*.15f;
                        PolygonRock(lip,point,new Vector3(width*.85f,.33f+width*.28f,width*.7f),i*.63f,i*97+553,submerged);
                    }
                    if ((i%4==0 || weight>.69f) && center.y > -0.5f)
                    {
                        var p=crest - outward*.31f + Vector3.up*.015f;
                        Asset("meadow-flower",lip,p,.52f+width*.12f,i*65);
                    }
                }
            }
            // Rock shoulders visually join alleys and terraces while the authored collision stays on their broad walkways.
            var shoulders = new[]{
                (new Vector3(-12.2f,-.12f,-10.5f), new Vector3(5.2f,2.1f,4.7f)),
                (new Vector3(-8.5f,.04f,-15.5f),new Vector3(4.6f,2.8f,5.7f)),
                (new Vector3(-5.8f,-.51f,-19.7f),new Vector3(4.6f,1.55f,5.9f)),
                (new Vector3(13.7f,-.58f,-16.3f),new Vector3(4.8f,1.4f,5.4f)),
                (new Vector3(18.7f,.19f,-16.2f),new Vector3(3.4f,2.37f,4.4f)),
                (new Vector3(32.3f,.68f,-25.6f),new Vector3(6.4f,2.65f,5.7f)),
                (new Vector3(36.6f,2.75f,-29.3f),new Vector3(3.5f,3.74f,4.1f)),
                (new Vector3(25.4f,-.66f,10.35f),new Vector3(5.3f,1.03f,5.0f)),
                (new Vector3(9.3f,-.67f,9.49f),new Vector3(4.3f,1.05f,4.4f)),
                (new Vector3(19.3f,-.68f,11.9f),new Vector3(3.1f,.89f,3.7f))
            };
            for(int i=0;i<shoulders.Length;i++)
            {
                var p=shoulders[i].Item1;
                var size=shoulders[i].Item2;
                PolygonRock(lip,p,size,i*.83f,i*873,rockface);
                PolygonRock(lip,p+new Vector3(-size.x*.26f,-size.y*.2f,size.z*.35f),size*.72f,-i*.42f,i*181,cliff);
            }

            foreach (var p in new[]{ new Vector3(-8.6f,.09f,-6.8f),new Vector3(17.9f,.1f,-10.6f),
                new Vector3(28.4f,.12f,3.5f),new Vector3(-25.7f,2.63f,-29),new Vector3(36.6f,5.62f,-38.4f),
                new Vector3(5.2f,-.44f,18.6f),new Vector3(32,-.39f,14.7f) })
            {
                Rocks(lip,p,.85f,3);
                for (int j=0;j<5;j++)
                    Asset("meadow-flower",lip,p+new Vector3(j*.44f,.04f,Mathf.Sin(j*3)*.72f),.64f+j*.06f,j*73);
                for(int j=0;j<5;j++)
                {
                    var blades=Piece(PrimitiveType.Sphere,lip,"岸生叶束",p+new Vector3(-.25f+j*.24f,.20f,1.15f),new Vector3(.23f,.63f,.18f),grassMat);
                    blades.transform.localRotation=Quaternion.Euler(17+j*9,43,j*18-35);
                }
            }
            Asset("garden-tree",lip,new Vector3(-26.9f,2.61f,-17.0f),1.0f,56);
            Asset("garden-tree",lip,new Vector3(25.8f,5.62f,-40.2f),.91f,120);
            Asset("garden-tree",lip,new Vector3(36.85f,.12f,-5.6f),.74f,14);

            // The glazed tiles carry perspective details across the wide plaza without cluttering the editable ground.
            var rimTile = Paint("台地暖白镶边",new Color(.89f,.75f,.56f),.18f,.09f);
            var terrace = Group("预留台地的构筑边线",details);
            for (int i=0;i<14;i++)
            {
                float x=21.7f+i*.94f;
                Box(terrace,"南侧浅色界石",new Vector3(x,2.028f,-15.10f),new Vector3(.81f,.066f,.55f),rimTile);
                Box(terrace,"北侧浅色界石",new Vector3(x,2.027f,-24.65f),new Vector3(.81f,.066f,.55f),rimTile);
            }
            for(int i=0;i<9;i++)
            {
                float z=-16.03f-i*.94f;
                Box(terrace,"浅色界石",new Vector3(34.45f,2.028f,z),new Vector3(.55f,.066f,.81f),rimTile);
                if (i<2 || i>6) Box(terrace,"浅色界石",new Vector3(21.1f,2.028f,z),new Vector3(.55f,.066f,.81f),rimTile);
            }
        }

        void Shelf(Transform parent, Vector3 center, Vector2 extent, float rimY, float phase, Material material)
        {
            var vertices = new List<Vector3> { center };
            var indices = new List<int>();
            const int segments = 53, bands = 7;
            for (int band=1; band<=bands; band++)
            for (int i=0; i<segments; i++)
            {
                float t = band / (float)bands;
                float a = i * 2 * Mathf.PI / segments;
                float w = 1 + .12f * Mathf.Sin(a*3+phase) + .075f*Mathf.Cos(a*7-phase);
                var p = center + new Vector3(Mathf.Cos(a)*extent.x*t*w,0,Mathf.Sin(a)*extent.y*t*w);
                p.y = Mathf.Lerp(center.y,rimY,Mathf.SmoothStep(0,1,t)) +
                    (Mathf.PerlinNoise(p.x*.23f+9,p.z*.23f+4)-.5f)*.21f;
                vertices.Add(p);
                int current=(band-1)*segments+1+i;
                int ahead=(band-1)*segments+1+(i+1)%segments;
                if(band==1) indices.AddRange(new[]{0,ahead,current});
                else indices.AddRange(new[]{current-segments,ahead,current,current-segments,ahead-segments,ahead});
            }
            MeshObject("自然潮汐坡肩", parent, vertices, indices, material);
        }

        void PolygonRock(Transform parent,Vector3 at,Vector3 size,float rotation,int seed,Material material)
        {
            var points = new List<Vector3>();
            var indices = new List<int>();
            int segments=7;
            for (int band = 0; band < 4; band++)
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI*2/segments) + rotation + band*.17f;
                float radius = (band==0 ? .79f : band==3 ? .49f : 1f) *
                    (1 + Mathf.Sin((i+seed*.001f)*3.37f + band*.8f)*.15f);
                var p = new Vector3(Mathf.Cos(angle)*radius*.53f, -0.5f + band/3f + .095f*Mathf.Sin(i*3.39f+seed*.041f), Mathf.Sin(angle)*radius*.53f);
                points.Add(at + Vector3.Scale(p,size));
            }
            for(int band=0;band<3;band++)
            for(int i=0;i<segments;i++)
            {
                int a=band*segments+i, b=band*segments+(i+1)%segments;
                indices.AddRange(new[]{a,a+segments,b,b,a+segments,b+segments});
            }
            int top=(4-1)*segments;
            for (int i = 1; i < segments-1;i++) indices.AddRange(new[] {top,top+i+1,top+i});
            MeshObject("风化岩体",parent,points,indices,material);
        }
    }
}

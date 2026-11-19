using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        Material riverSurface, fallingSurface, caveFallSurface, caveSideSurface, spillFoam, estuaryShallows;

        void EnsureSampleWaterMaterials()
        {
            if (riverSurface != null) return;
            var shader = Resources.Load<Shader>("Coast/SampleFlow");
            if (shader == null) shader = Shader.Find("Dawn Garden/Coast Sample Flow");
            var riverShallow=new Color(.26f,.81f,.76f);
            var riverDeep=new Color(.035f,.34f,.48f);
            riverSurface = SampleWaterMaterial(shader, "连通水路 · 河面", riverShallow, riverDeep, 0, .92f);
            fallingSurface = SampleWaterMaterial(shader, "连通水路 · 日光下的水帘", new Color(.64f,.87f,.86f), new Color(.23f,.53f,.59f), 1, .86f);
            caveFallSurface = SampleWaterMaterial(shader, "连通水路 · 洞内落水", riverShallow, riverDeep, 3, .92f);
            caveFallSurface.renderQueue = (int)RenderQueue.Transparent + 1;
            caveSideSurface = SampleWaterMaterial(shader, "连通水路 · 岩沿断流", riverShallow, riverDeep, 3, .63f);
            caveSideSurface.renderQueue = (int)RenderQueue.Transparent + 2;
            spillFoam = SampleWaterMaterial(shader, "连通水路 · 跌水泡沫", new Color(.71f,.84f,.80f), new Color(.33f,.59f,.58f), 2, .64f);
            spillFoam.renderQueue = (int)RenderQueue.Transparent + 3;
            estuaryShallows = SampleWaterMaterial(shader, "海湾 · 入海浅滩", new Color(.42f,.75f,.64f), new Color(.12f,.45f,.52f), 4, .73f);
            estuaryShallows.renderQueue = (int)RenderQueue.Transparent - 2;
        }

        Material SampleWaterMaterial(Shader shader, string name, Color shallow, Color deep, float kind, float opacity)
        {
            if (shader == null)
            {
                var fallback = new Material(water) { name = name };
                materials.Add(fallback);
                return fallback;
            }
            var surface = new Material(shader) { name = name };
            surface.SetColor("_Shallow", shallow);
            surface.SetColor("_Deep", deep);
            surface.SetFloat("_Mode", kind);
            surface.SetFloat("_Opacity", opacity);
            surface.SetTexture("_SkyMap",Resources.Load<Texture2D>("Coast/MorningSkyV1"));
            materials.Add(surface);
            return surface;
        }

        void BuildSampleWaterCourse(Vector3[] controls, float[] halfWidths)
        {
            EnsureSampleWaterMaterials();
            int mouthIndex = controls.Length-2;
            var mouth = controls[mouthIndex];
            var mouthDelta = controls[mouthIndex+1]-mouth;
            var mouthDirection = HorizontalDirection(mouthDelta);
            riverSurface.SetVector("_MouthProfile",new Vector4(mouth.x,mouth.z,mouthDirection.x,mouthDirection.z));
            riverSurface.SetFloat("_MouthLength",new Vector2(mouthDelta.x,mouthDelta.z).magnitude);
            var river = Group("苗圃引水 · 桥下溪谷 · 潮洞汇海", details);
            var centers = new List<Vector3>();
            var radii = new List<float>();
            for (int segment = 0; segment < controls.Length - 1; segment++)
            {
                var from = controls[segment];
                var to = controls[segment + 1];
                int steps = Mathf.Max(8, Mathf.CeilToInt(Vector3.Distance(from,to) * 5));
                for (int step = 0; step <= steps; step++)
                {
                    if (step == 0 && segment > 0) continue;
                    float t = step / (float)steps;
                    centers.Add(RiverWaterline(segment,t));
                    radii.Add(1.035f*Mathf.Lerp(halfWidths[segment],halfWidths[segment+1],t));
                }
            }

            var samples = new List<Vector3>(centers.Count * 5);
            var mapping = new List<Vector2>(centers.Count * 5);
            var pool = new List<int>();
            var spill = new List<int>();
            var caveSpill = new List<int>();
            var fringe = new List<Vector3>();
            var fringeUV = new List<Vector2>();
            var fringeFaces = new List<int>();
            float travel = 0;
            bool previousIsSpill = false;
            bool previousInGrotto = false;
            for (int i = 0; i < centers.Count; i++)
            {
                var p = centers[i];
                var before = centers[Mathf.Max(0,i-1)];
                var after = centers[Mathf.Min(i+1,centers.Count-1)];
                var span=after-before;
                var tangent = HorizontalDirection(span);
                var side = Vector3.Cross(Vector3.up,tangent);
                travel += Vector3.Distance(before,p);
                bool inGrotto = new Vector2(p.x-Grotto.x,p.z-Grotto.z).sqrMagnitude < 47f;
                float slope=-span.y/Mathf.Max(.01f,new Vector2(span.x,span.z).magnitude);
                float separation=inGrotto?Ease(.25f,1.1f,slope):0;
                for (int j=0;j<=4;j++)
                {
                    float across = j / 4f;
                    float edge = (across * 2 - 1) * radii[i];
                    edge *= 1 - separation * (.23f + .087f * Mathf.Sin(travel * 4.6f + across * 4.7f));
                    edge += separation * .075f * Mathf.Cos(travel * 5.7f);
                    float distortion = Mathf.Sin(travel*2.7f+across*3.6f) * .025f * Mathf.Abs(across*2-1);
                    samples.Add(p + side * (edge+distortion) + Vector3.up * .014f);
                    mapping.Add(new Vector2(across,travel));
                }
                for (int branch=0;branch<2;branch++)
                {
                    float phase=travel*(branch==0?4.34f:5.77f)+branch*3.2f;
                    float sign=branch==0?-1:1;
                    float edge=(.905f+.05f*Mathf.Sin(phase))*radii[i]*sign;
                    float spread=(.029f+.058f*(.5f+.5f*Mathf.Sin(phase*1.37f)))*separation*radii[i];
                    var trace=p+side*edge+Vector3.up*(.023f+Mathf.Cos(phase)*.028f*separation);
                    fringe.Add(trace-side*spread);
                    fringe.Add(trace+side*spread);
                    fringeUV.Add(new Vector2(0,travel));
                    fringeUV.Add(new Vector2(1,travel));
                }
                if (i == 0) continue;
                var change = p-before;
                bool isSpill = -change.y > .24f * new Vector2(change.x,change.z).magnitude;
                var destination = !isSpill ? pool : inGrotto ? caveSpill : spill;
                for (int j = 0; j < 4; j++)
                {
                    int a = (i - 1) * 5 + j;
                    int b = i * 5 + j;
                    destination.AddRange(new[] { a,b,a+1,b,b+1,a+1 });
                }
                if (inGrotto && (isSpill || previousIsSpill))
                {
                    int a=(i-1)*4,b=i*4;
                    fringeFaces.AddRange(new[] { a,b,a+1,b,b+1,a+1,a+2,b+2,a+3,b+2,b+3,a+3 });
                }
                if (!previousIsSpill && isSpill)
                    BuildSpillLedge(river,before,tangent,radii[i],inGrotto);
                if (previousIsSpill && !isSpill)
                {
                    FoamingRunoff(river,p,tangent,radii[i],previousInGrotto);
                    if (previousInGrotto)
                    {
                        FoamingRunoff(river,p+side*radii[i]*.54f+tangent*.07f,tangent,radii[i]*.29f,true);
                        FoamingRunoff(river,p-side*radii[i]*.57f+tangent*.23f,tangent,radii[i]*.24f,true);
                    }
                    if (!previousInGrotto) BuildSpillBanks(river,p,tangent,radii[i]);
                }
                previousIsSpill = isSpill;
                previousInGrotto = inGrotto;
            }
            if (pool.Count > 0)
                MeshObject("连通水面 · 槽池到入海口",river,samples,pool,riverSurface,false,mapping);
            if (spill.Count > 0)
                MeshObject("石槽落瀑 · 连通上下水位",river,samples,spill,fallingSurface,false,mapping);
            if (caveSpill.Count > 0)
                MeshObject("潮洞的岩间水帘",river,samples,caveSpill,caveFallSurface,false,mapping);
            if (fringeFaces.Count > 0)
                MeshObject("绕过岩缘的两股细水",river,fringe,fringeFaces,caveSideSurface,false,fringeUV);
            FoamingRunoff(river,mouth,mouthDirection,halfWidths[mouthIndex]*.82f,false);
            BuildEstuaryShallows(river,controls[mouthIndex+1],mouthDirection,halfWidths[mouthIndex+1]);
            DressEstuaryWaterline(river,mouth,mouthDirection,halfWidths[mouthIndex]);
            BuildSampleRiverRockGroups(river, controls, halfWidths);
        }

        void BuildEstuaryShallows(Transform parent,Vector3 mouth,Vector3 direction,float riverWidth)
        {
            var center=mouth+direction*.7f;
            var across=Vector3.Cross(Vector3.up,direction);
            const int columns=36,rows=28;
            float width=riverWidth*3.24f;
            const float length=6.36f;
            var points=new List<Vector3>((columns+1)*(rows+1));
            var mapping=new List<Vector2>(points.Capacity);
            var triangles=new List<int>();
            for (int row=0;row<=rows;row++)
            for (int column=0;column<=columns;column++)
            {
                float x=column/(float)columns*2-1;
                float z=row/(float)rows*2-1;
                var position=center+across*(x*width)+direction*(z*length);
                position.y=CoastSeaLevel+.024f;
                float angle=Mathf.Atan2(z,x);
                float irregular=1+.065f*Mathf.Sin(angle*3+.67f)+.049f*Mathf.Cos(angle*5-1.1f);
                float edge=new Vector2(x,z).magnitude/irregular;
                float bayWeight=1-Ease(.38f,1.06f,edge);
                float submerged=CoastSeaLevel-CoastHeight(position.x,position.z);
                points.Add(position);
                mapping.Add(new Vector2(Mathf.Max(0,submerged),bayWeight));
                if (row==0||column==0) continue;
                int corner=points.Count-1;
                int previous=corner-(columns+1);
                triangles.AddRange(new[] { previous-1,corner-1,previous,previous,corner-1,corner });
            }
            MeshObject("海口浅水渐变与岸沫",parent,points,triangles,estuaryShallows,false,mapping);
        }

        void DressEstuaryWaterline(Transform parent,Vector3 mouth,Vector3 direction,float riverWidth)
        {
            var rocks=Group("半浸在浅湾岸边的岩石",parent);
            var side=Vector3.Cross(Vector3.up,direction);
            for (int i=0;i<12;i++)
            {
                float seed=8.19f*i+7.1f;
                float bank=i%2==0?-1:1;
                float shoreDistance=(1.03f+.44f*Mathf.Abs(Mathf.Sin(seed)))*riverWidth;
                var p=mouth+direction*(1.3f+i*.53f+.23f*Mathf.Cos(seed))+side*(shoreDistance*bank);
                float bed=CoastHeight(p.x,p.z);
                if (bed>CoastSeaLevel+.045f||bed<CoastSeaLevel-.57f) continue;
                float size=.32f+.38f*Mathf.Abs(Mathf.Cos(seed*1.23f));
                p.y=Mathf.Min(CoastSeaLevel+.015f,bed+size*.37f);
                SampleStone(rocks,"浅湾的潮湿礁石",p,new Vector3(size*1.2f,size*.71f,size*.96f),27+i*37,
                    i%4==0?limestone:cliff,Quaternion.Euler(6*Mathf.Sin(seed),i*46,11*Mathf.Cos(seed)));
            }
        }

        void BuildSampleRiverRockGroups(Transform river, Vector3[] controls, float[] halfWidths)
        {
            var banks = Group("上游水路 · 贴岸岩层",river);
            var pale = SampleCaveStone("上游 · 风化石灰岩",new Color(.53f,.52f,.47f),new Color(.77f,.71f,.61f));
            var gray = SampleCaveStone("上游 · 潮湿青灰岩",new Color(.35f,.42f,.40f),new Color(.60f,.61f,.54f));
            var forms = new[]
            {
                new Vector4(1f,.58f,1f,1.25f),
                new Vector4(2f,.55f,1f,1.45f),
                new Vector4(2f,.84f,-1f,1.1f),
                new Vector4(3f,.14f,1f,1.22f),
                new Vector4(3f,.55f,-1f,1.8f),
                new Vector4(4f,.14f,1f,1.33f),
                new Vector4(4f,.65f,-1f,1.55f),
                new Vector4(4f,.88f,1f,1.22f),
                new Vector4(5f,.30f,1f,1.08f),
                new Vector4(5f,.50f,-1f,1.34f)
            };
            for(int cluster=0;cluster<forms.Length;cluster++)
            {
                var form=forms[cluster];
                int segment=(int)form.x;
                var face=HorizontalDirection(controls[segment+1]-controls[segment]);
                var cross=Vector3.Cross(Vector3.up,face)*form.z;
                var center=RiverWaterline(segment,form.y)+cross*Mathf.Lerp(
                    halfWidths[segment],halfWidths[segment+1],form.y)*1.16f;
                center.y=CoastHeight(center.x,center.z);
                var rotation=Quaternion.LookRotation(face);
                float width=form.w;
                SampleStone(banks,"压入坡脚的整层母岩",center-Vector3.up*(width*.12f),
                    new Vector3(width*.85f,width*.84f,width*1.39f),38+31*cluster,gray,rotation*Quaternion.Euler(2,-13,-9));
                var shelf=center+cross*(width*.37f)-face*(width*.27f);
                shelf.y=CoastHeight(shelf.x,shelf.z);
                SampleStone(banks,"承托上岸的风化岩肩",shelf-Vector3.up*(width*.15f),
                    new Vector3(width,width*.71f,width*.87f),81+31*cluster,pale,rotation*Quaternion.Euler(-8,14,5));
                var basePart=center+face*.38f*width-cross*.21f*width;
                basePart.y=CoastHeight(basePart.x,basePart.z);
                SampleStone(banks,"有错层的潮线碎硌",basePart-Vector3.up*.16f,new Vector3(
                    width*.54f,width*.48f,width*.68f),124+31*cluster,wetStone,rotation*Quaternion.Euler(1,22,-8));
                if(cluster%2==0)
                {
                    var plants=shelf+cross*.24f+face*.12f;
                    plants.y=CoastHeight(plants.x,plants.z)+.03f;
                    if(plants.y>RiverWaterline(segment,form.y).y+.22f)
                        Asset("meadow-flower",banks,plants,.54f+.03f*cluster,cluster*33);
                }
            }
        }

        void BuildSpillLedge(Transform parent,Vector3 crest,Vector3 tangent,float halfWidth,bool inGrotto)
        {
            if (inGrotto) return;
            var rotation = Quaternion.LookRotation(tangent);
            var shoulder = Vector3.Cross(Vector3.up,tangent);
            var toe=crest-Vector3.up*.24f-tangent*.13f;
            for (int i=0;i<3;i++)
            {
                var piece=toe+shoulder*halfWidth*((i-1)*.58f)+tangent*(Mathf.Sin(i*4)*.22f);
                SampleStone(parent,"嵌在落水前缘的浅岩脊",piece,new Vector3(halfWidth*.64f,.29f,.51f+i*.10f),
                    37+i*33,limestone,rotation*Quaternion.Euler(0,7f-i*9f,0));
            }
            foreach(int direction in new[]{-1,1})
            {
                var center=crest+shoulder*halfWidth*(1.08f+.065f*direction)*direction+tangent*(.21f*direction);
                center.y=CoastHeight(center.x,center.z)-.13f;
                SampleStone(parent,"落瀑肩部的埋岩",center,new Vector3(.58f,.43f,.73f),78+direction,
                    limestone,rotation*Quaternion.Euler(-7,13f*direction,-4));
            }
        }

        void BuildSpillBanks(Transform parent,Vector3 plunge,Vector3 tangent,float halfWidth)
        {
            var shoulder=Vector3.Cross(Vector3.up,tangent);
            for (int flank=0;flank<2;flank++)
            for (int row=0;row<3;row++)
            {
                float seed=flank*1.81f+row*3.98f;
                var p=plunge+(flank==0?-1:1)*shoulder*halfWidth*(.98f+Mathf.Sin(seed)*.13f)+tangent*(row*.42f-.21f);
                p.y=CoastHeight(p.x,p.z)-.10f;
                var scale=new Vector3(.39f+.15f*(row%2),.23f+.07f*row,.58f+Mathf.Sin(seed)*.13f);
                SampleStone(parent,"跌水溅湿的碎岸岩",p,scale,61+flank*49+row*13,limestone,
                    Quaternion.LookRotation(tangent)*Quaternion.Euler(0,19f*Mathf.Cos(seed),0));
            }
        }

        static Vector3 HorizontalDirection(Vector3 delta)
        {
            delta.y=0;
            return delta.sqrMagnitude > .0001f ? delta.normalized : Vector3.forward;
        }

        void FoamingRunoff(Transform parent, Vector3 center, Vector3 forward, float radius, bool inGrotto)
        {
            var across = Vector3.Cross(Vector3.up,forward);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            int count=inGrotto?12:18;
            const int sides=9;
            for (int patch=0;patch<count;patch++)
            {
                var phase=patch*4.069f+center.x*.82f+center.z*.57f;
                float downstream=(patch/(count-1f))*(inGrotto?.63f:1.00f)*radius;
                var position=center+forward*downstream+across*(Mathf.Sin(phase)*radius*.66f)+Vector3.up*.021f;
                float broad=(.095f+.07f*(.5f+.5f*Mathf.Cos(phase*2.4f)))*radius;
                float length=broad*Mathf.Lerp(.6f,1.46f,patch/(count-1f));
                int start=v.Count;
                v.Add(position);
                uv.Add(Vector2.one*.5f);
                for (int side=0;side<sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides;
                    var rim=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    float scallop=1+.14f*Mathf.Sin(phase+side*1.95f);
                    v.Add(position+(across*(rim.x*broad)+forward*(rim.y*length))*scallop);
                    uv.Add((rim+Vector2.one)*.5f);
                    triangles.AddRange(new[] { start,start+1+(side+1)%sides,start+1+side });
                }
            }
            MeshObject("刚落水处散开的薄泡",parent,v,triangles,spillFoam,false,uv);
        }

        void SampleTrough(Transform parent, float width, params Vector3[] profile)
        {
            EnsureSampleWaterMaterials();
            var trough = Group("砌石引水槽",parent);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            var drops = new List<int>();
            float traveled = 0;

            for (int i=0;i<profile.Length;i++)
            {
                var point = profile[i];
                var before = profile[Mathf.Max(i-1,0)];
                var after = profile[Mathf.Min(i+1,profile.Length-1)];
                var offset = Vector3.Cross(Vector3.up,HorizontalDirection(after-before)) * (width*.5f);
                traveled += Vector3.Distance(before,point);
                v.Add(point+offset+Vector3.up*.013f);
                v.Add(point-offset+Vector3.up*.013f);
                uv.Add(new Vector2(0,traveled));
                uv.Add(new Vector2(1,traveled));
                if (i == 0) continue;
                int n = i*2;
                var d = point-before;
                bool isDrop=-d.y>.30f*new Vector2(d.x,d.z).magnitude;
                var surface=isDrop?drops:indices;
                surface.AddRange(new[] {n-2,n-1,n,n,n-1,n+1});
                var heading = Quaternion.LookRotation(d);
                var middle = (point+before)*.5f;
                var sill = Box(trough,"暖灰石引水槽底",middle-Vector3.up*.108f,new Vector3(width+.28f,.171f,d.magnitude+.09f),cliff);
                sill.transform.localRotation=heading;
                foreach (float sign in new[]{-1f,1f})
                {
                    var edge=Vector3.Cross(Vector3.up,HorizontalDirection(d))*(width*.5f+.072f)*sign;
                    int blocks=Mathf.CeilToInt(d.magnitude/.48f);
                    for (int j=0;j<blocks;j++)
                    {
                        var p=Vector3.Lerp(before,point,(j+.5f)/blocks)+edge;
                        float height=.195f+.04f*Mathf.Sin(i*2.37f+j*4.3f);
                        p.y+=.033f;
                        SampleStone(trough,"低而断续的暖石渠缘",p,new Vector3(.177f,height,d.magnitude/blocks+.028f),
                            17+i*21+j*11+(int)sign,(i+j)%5==0?sandstone:limestone,heading);
                    }
                }
            }

            if (indices.Count>0) MeshObject("砌槽里的流动水面",trough,v,indices,riverSurface,false,uv);
            if (drops.Count>0) MeshObject("窄槽里逐级跌落的水流",trough,v,drops,fallingSurface,false,uv);
        }
    }
}

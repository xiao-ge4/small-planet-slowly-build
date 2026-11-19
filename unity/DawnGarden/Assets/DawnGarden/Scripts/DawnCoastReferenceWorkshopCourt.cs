using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool referenceWorkshopCourtBuilt;

        // Call after the existing heroes/dressing and before BuildNavigation. The
        // surveyed brick court, saved plot, routes and character settings are untouched.
        void BuildReferenceWorkshopCourt()
        {
            if(referenceWorkshopCourtBuilt || details==null)return;
            referenceWorkshopCourtBuilt=true;
            Physics.SyncTransforms();
            var root=Group("工坊前场 · 弧形低石展示栏",details);
            var stone=new[]{
                Paint("前场展示栏暖灰石",new Color(.80f,.75f,.65f),.11f,.12f),
                Paint("前场展示栏浅蜜石",new Color(.88f,.81f,.69f),.12f,.10f),
                Paint("前场展示栏象牙压顶",new Color(.91f,.85f,.74f),.13f,.08f)
            };
            var masonry=new ReferenceMasonryBatches(this,root,stone);
            var palette=new[]{
                Paint("前场工艺品旧金铜",new Color(.72f,.49f,.20f),.48f,.04f),
                Paint("前场工艺品深铜座",new Color(.31f,.22f,.12f),.34f,.05f),
                stone[2],
                Paint("前场工艺品赤陶",new Color(.76f,.42f,.25f),.18f,.07f),
                Paint("前场工具旧橡木",new Color(.47f,.28f,.13f),.13f,.10f),
                Paint("前场花槽深土",new Color(.27f,.20f,.12f),.03f,.08f)
            };
            palette[0].SetFloat("_Metallic",.50f);
            palette[1].SetFloat("_Metallic",.35f);
            var objects=new ReferenceWorkshopBatch(this,root,palette);
            var spans=new List<ReferenceCourtWallSpan>();
            var posts=new List<Vector3>();
            int rejected=0,serial=6200;
            const int intervals=43;
            float left=ReferenceBrickCourt.x-ReferenceBrickCourtHalf.x+1.8f;
            float right=ReferenceBrickCourt.x+ReferenceBrickCourtHalf.x-.75f;
            for(int i=0;i<intervals;i++)
            {
                var a=ReferenceWorkshopCourtArc(Mathf.Lerp(left,right,i/(float)intervals));
                var b=ReferenceWorkshopCourtArc(Mathf.Lerp(left,right,(i+1)/(float)intervals));
                var along=(b-a).normalized;float length=Vector3.Distance(a,b);
                var middle=(a+b)*.5f;
                if(!ReferenceWorkshopCourtClear(root,middle,along,length+.035f,.52f,.70f,out float foot))
                {rejected++;continue;}
                middle.y=foot;
                spans.Add(new ReferenceCourtWallSpan(i,middle,along,length));
                masonry.Stone(middle+Vector3.up*.045f,along,length+.012f,.13f,.39f,serial++);
                masonry.Stone(middle+Vector3.up*.15f,along,length-.012f,.25f,.36f,serial++);
                // Two short upper stones offset the lower-course joints.
                for(int half=0;half<2;half++)
                    masonry.Stone(middle+along*((half==0?-1:1)*length*.25f)+Vector3.up*.385f,
                        along,length*.5f-.013f,.23f,.36f,serial++);
                masonry.Stone(middle+Vector3.up*.565f,along,length+.016f,.14f,.47f,serial++);
            }

            // The arc is deliberately interrupted at every full-width route. A
            // post is added only when its larger complete footprint also fits.
            float sincePost=10;
            for(int i=0;i<spans.Count;i++)
            {
                var span=spans[i];
                bool start=i==0||spans[i-1].ordinal+1!=span.ordinal;
                bool end=i==spans.Count-1||span.ordinal+1!=spans[i+1].ordinal;
                sincePost+=span.length;
                if(!start&&!end&&sincePost<2.8f)continue;
                if(posts.Exists(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(span.center.x,span.center.z))<1.25f))continue;
                if(!ReferenceWorkshopCourtClear(root,span.center,span.along,.66f,.66f,.94f,out float foot))continue;
                var p=new Vector3(span.center.x,foot,span.center.z);
                masonry.Stone(p+Vector3.up*.09f,span.along,.60f,.18f,.60f,serial++);
                masonry.Stone(p+Vector3.up*.43f,span.along,.43f,.55f,.43f,serial++);
                masonry.Stone(p+Vector3.up*.755f,span.along,.54f,.10f,.54f,serial++);
                masonry.Stone(p+Vector3.up*.84f,span.along,.62f,.10f,.62f,serial++);
                posts.Add(p);sincePost=0;
            }

            // Few recognisable silhouettes, spaced along the sea-facing edge.
            // The existing lumber/barrels stay where they are; overlap tests keep
            // the new capstones and display pieces out of those actual colliders.
            int displays=0;
            var used=new List<Vector3>();
            var desiredX=new[]{10.5f,20.0f,23.1f,31.3f};
            for(int kind=0;kind<desiredX.Length;kind++)
            {
                int candidate=-1;float nearest=float.PositiveInfinity;
                for(int i=0;i<posts.Count;i++)
                {
                    var p=posts[i];
                    if(used.Exists(q=>Vector3.Distance(p,q)<2.0f))continue;
                    float score=Mathf.Abs(p.x-desiredX[kind]);
                    if(score>=nearest)continue;
                    if(!ReferenceWorkshopCourtClear(root,p,Vector3.right,.88f,.74f,1.95f,out _))continue;
                    candidate=i;nearest=score;
                }
                if(candidate<0)continue;
                var support=posts[candidate];used.Add(support);
                ReferenceWorkshopCourtDisplay(root,objects,palette,support+Vector3.up*.89f,kind);
                displays++;
            }

            // One low sample board lies along a clear wall span, below the tall
            // display silhouettes. It cannot spill into the pier/entrance aisle.
            foreach(var span in spans)
            {
                if(span.center.x<18.5f||span.center.x>23.8f)continue;
                if(used.Exists(p=>Vector3.Distance(p,span.center)<1.2f)||posts.Exists(p=>Vector3.Distance(p,span.center)<.7f))continue;
                if(!ReferenceWorkshopCourtClear(root,span.center,span.along,.96f,.65f,1.30f,out _))continue;
                var basePoint=span.center+Vector3.up*.66f;
                var rotation=Quaternion.LookRotation(Vector3.Cross(Vector3.up,span.along));
                objects.Box(4,basePoint+Vector3.up*.04f,new Vector3(.84f,.08f,.46f),rotation);
                for(int tile=0;tile<3;tile++)
                    objects.Box(tile==1?2:3,basePoint+span.along*((tile-1)*.26f)+Vector3.up*.20f,
                        new Vector3(.22f,.30f,.055f),rotation*Quaternion.Euler(-18,0,0));
                break;
            }
            masonry.Build();objects.Build();
            int triangles=0;
            // The bevelled walls, capped columns, vases and bronze rings own
            // their exact static mesh colliders; there is no invisible proxy wall.
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                // The small soft flower canopy has no physical obstruction.
                if(filter.transform.parent!=root)continue;
                filter.gameObject.layer=ObstacleLayer;
                var collider=filter.GetComponent<MeshCollider>();
                if(collider==null)collider=filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh=filter.sharedMesh;
                triangles+=filter.sharedMesh.triangles.Length/3;
            }
            Physics.SyncTransforms();
            Debug.Log($"Reference workshop court: {spans.Count} supported curved wall spans, {posts.Count} capped posts, {displays} distinct displays, {rejected} spans omitted for full-footprint clearance; {triangles} triangles with real mesh collision; saved plot and navigation settings unchanged.");
            if(spans.Count<8||displays<2)
                Debug.LogWarning("Workshop display court has limited visible coverage after real support/obstacle checks; inspect its A18 silhouette before accepting this art pass.");
        }

        readonly struct ReferenceCourtWallSpan
        {
            public readonly int ordinal;
            public readonly Vector3 center,along;
            public readonly float length;
            public ReferenceCourtWallSpan(int index,Vector3 p,Vector3 tangent,float run)
            {ordinal=index;center=p;along=tangent;length=run;}
        }

        static Vector3 ReferenceWorkshopCourtArc(float x)
        {
            float u=(x-ReferenceBrickCourt.x)/ReferenceBrickCourtHalf.x;
            // This shallow bow stays on the existing top of the brick court.
            float z=ReferenceBrickCourt.z-ReferenceBrickCourtHalf.y+.46f+1.55f*u*u;
            return new Vector3(x,ReferenceBrickCourt.y,z);
        }

        bool ReferenceWorkshopCourtClear(Transform root,Vector3 center,Vector3 along,float length,float depth,float height,out float foot)
        {
            along.y=0;along.Normalize();
            var side=Vector3.Cross(Vector3.up,along);
            var c=new Vector2(center.x,center.z);
            var axis=new Vector2(along.x,along.z);var perpendicular=new Vector2(side.x,side.z);
            foot=ReferenceHeight(center.x,center.z);
            if(Mathf.Abs(foot-ReferenceBrickCourt.y)>.13f)return false;
            // An expanded oriented rectangle tests each complete road segment,
            // including bends; this cannot approve a long object by pivot alone.
            foreach(var road in ReferenceRoads)
                if(ReferenceWorkshopCourtHitsRoute(c,axis,perpendicular,length,depth,road.width+.30f,road.points))return false;
            if(ReferenceWorkshopCourtHitsRoute(c,axis,perpendicular,length,depth,1.40f,ReferenceWorkshopAccess))return false;
            int samples=Mathf.Max(2,Mathf.CeilToInt(length/.22f));
            for(int i=0;i<=samples;i++)for(int j=-1;j<=1;j++)
            {
                var p=center+along*((i/(float)samples-.5f)*length)+side*(j*depth*.5f);
                // Rectangular saved apron exclusion is deliberately wider than
                // the original editable ellipse, and works for both save states.
                if(Mathf.Abs(p.x-ReferenceWhiteCourt.x)<ReferenceWhiteCourtHalf.x+.55f &&
                   Mathf.Abs(p.z-ReferenceWhiteCourt.z)<ReferenceWhiteCourtHalf.y+.55f)return false;
                if(Mathf.Abs(p.x-ReferenceBrickCourt.x)>ReferenceBrickCourtHalf.x-.06f ||
                   Mathf.Abs(p.z-ReferenceBrickCourt.z)>ReferenceBrickCourtHalf.y-.06f)return false;
                if(Mathf.Abs(ReferenceHeight(p.x,p.z)-foot)>.13f)return false;
            }
            var centerWorld=details.TransformPoint(new Vector3(center.x,foot+height*.5f+.015f,center.z));
            var scale=details.lossyScale;
            float horizontal=Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            var half=new Vector3(depth*.5f*horizontal,height*.5f*Mathf.Abs(scale.y),length*.5f*horizontal);
            var orientation=details.rotation*Quaternion.LookRotation(along);
            foreach(var collider in Physics.OverlapBox(centerWorld,half,orientation,1<<ObstacleLayer,QueryTriggerInteraction.Ignore))
                if(!collider.transform.IsChildOf(root))return false;
            return true;
        }

        static bool ReferenceWorkshopCourtHitsRoute(Vector2 center,Vector2 along,Vector2 side,float length,float depth,float clearance,Vector3[] route)
        {
            for(int i=0;i<route.Length-1;i++)
                if(ReferenceRockSegmentHits(center,along,side,length*.5f+clearance,depth*.5f+clearance,
                    new Vector2(route[i].x,route[i].z),new Vector2(route[i+1].x,route[i+1].z)))return true;
            return false;
        }

        void ReferenceWorkshopCourtDisplay(Transform root,ReferenceWorkshopBatch batch,Material[] palette,Vector3 bottom,int kind)
        {
            if(kind==0)
            {
                // A small armillary: three intersecting physical bronze hoops.
                batch.Lathe(1,bottom,new[]{new Vector2(0,0),new(.23f,0),new(.23f,.08f),new(.12f,.12f),new(.12f,.22f),new(0,.22f)});
                var center=bottom+Vector3.up*.53f;
                ReferenceWorkshopCourtRing(root,center,Quaternion.identity,.31f,.021f,palette[0]);
                ReferenceWorkshopCourtRing(root,center,Quaternion.Euler(24,65,0),.31f,.021f,palette[0]);
                ReferenceWorkshopCourtRing(root,center,Quaternion.Euler(78,0,0),.31f,.019f,palette[0]);
                batch.Beam(0,center-Vector3.up*.37f,center+Vector3.up*.37f,.035f);
            }
            else if(kind==1)
            {
                batch.Lathe(2,bottom,new[]{new Vector2(0,0),new(.25f,0),new(.25f,.08f),new(.17f,.12f),
                    new(.16f,.56f),new(.24f,.60f),new(.24f,.69f),new(0,.69f)});
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI*.25f;var p=bottom+new Vector3(Mathf.Cos(a)*.155f,.34f,Mathf.Sin(a)*.155f);
                    batch.Box(2,p,new Vector3(.045f,.40f,.045f));
                }
            }
            else if(kind==2)
            {
                batch.Lathe(3,bottom,new[]{new Vector2(0,0),new(.28f,0),new(.31f,.08f),new(.36f,.29f),
                    new(.38f,.33f),new(.38f,.39f),new(.31f,.39f),new(.28f,.31f),new(0,.31f)});
                var foliage=Group("前场陶盆 · 低矮白金花",root);
                var plants=new Dictionary<Material,NurseryGeometry>();
                for(int i=0;i<4;i++)
                {
                    float a=i*2.399963f;var p=bottom+new Vector3(Mathf.Cos(a)*.14f,.35f,Mathf.Sin(a)*.12f);
                    BloomRosette(plants,p,.62f,715+i*31,true,i==0?0:2);
                }
                PublishPlants(foliage,"前场陶盆花叶",plants);
            }
            else
            {
                batch.Lathe(2,bottom,new[]{new Vector2(0,0),new(.25f,0),new(.25f,.07f),new(.16f,.13f),new(.18f,.19f),new(0,.19f)});
                var profile=new List<Vector2>();
                for(int i=0;i<=12;i++)
                {float a=Mathf.PI*i/12;profile.Add(new Vector2(Mathf.Sin(a)*.25f,.19f+.25f*(1-Mathf.Cos(a))));}
                profile.Add(new Vector2(0,.69f));batch.Lathe(2,bottom,profile.ToArray());
            }
        }

        void ReferenceWorkshopCourtRing(Transform root,Vector3 center,Quaternion rotation,float radius,float tube,Material material)
        {
            const int rings=32,sides=8;
            var vertices=new List<Vector3>();var faces=new List<int>();
            for(int i=0;i<rings;i++)for(int j=0;j<sides;j++)
            {
                float a=i*Mathf.PI*2/rings,b=j*Mathf.PI*2/sides;
                vertices.Add(center+rotation*new Vector3(Mathf.Cos(a)*(radius+Mathf.Cos(b)*tube),
                    Mathf.Sin(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube));
            }
            for(int i=0;i<rings;i++)for(int j=0;j<sides;j++)
            {
                int a=i*sides+j,b=i*sides+(j+1)%sides,c=((i+1)%rings)*sides+j,d=((i+1)%rings)*sides+(j+1)%sides;
                faces.AddRange(new[]{a,c,b,b,c,d});
            }
            MeshObject("前场黄铜环仪 · 实体铜环",root,vertices,faces,material);
        }
    }
}

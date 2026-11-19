using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool referenceCavePlantingBuilt;

        // Call after the cave model and its MeshColliders have been installed.
        // Every root is surveyed against that model, never against the cave's floor.
        void BuildReferenceCavePlanting()
        {
            if(referenceCavePlantingBuilt || details==null)return;
            Transform cave=null;
            foreach(Transform child in details)
                if(child.name=="参考资产 · Geology/cave_shell"){cave=child;break;}
            if(cave==null)return;
            var colliders=cave.GetComponentsInChildren<Collider>();
            if(colliders.Length==0)return;
            Physics.SyncTransforms();
            var bounds=colliders[0].bounds;
            foreach(var collider in colliders)bounds.Encapsulate(collider.bounds);

            var root=Group("潮洞 · 附岩苔草与檐口垂枝",details);
            var plants=new Dictionary<Material,NurseryGeometry>();
            var moss=Paint("潮洞 · 暗青附苔",new Color(.25f,.37f,.105f),.035f,.12f);
            moss.SetFloat("_Cull",0);
            var leaves=new[]{
                NurseryMat("潮洞 · 深绿匍匐叶",new Color(.25f,.40f,.105f),.06f),
                NurseryMat("潮洞 · 岩肩细叶草",new Color(.38f,.52f,.15f),.07f),
                NurseryMat("潮洞 · 迎光嫩叶",new Color(.51f,.63f,.205f),.08f)
            };
            var stems=Paint("潮洞 · 细藤茎",new Color(.25f,.30f,.08f),.03f,.07f);
            var random=new System.Random(912607);
            float R(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
            int mossFaces=0,tufts=0,flowers=0,trails=0;

            bool Clear(Vector3 p,float radius)
            {
                // The bridge's full width, its approaches and the flowing water
                // remain open even when the collider below them belongs to the roof.
                float bridge=SegmentDistanceSquared(new Vector2(p.x,p.z),
                    ReferenceBridgeStart,ReferenceBridgeEnd,out _);
                if(bridge<Mathf.Pow(1.625f+radius+.18f,2))return false;
                for(int i=0;i<ReferenceStream.Length-1;i++)
                {
                    float distance=SegmentDistanceSquared(new Vector2(p.x,p.z),
                        ReferenceStream[i],ReferenceStream[i+1],out float t);
                    float clearance=Mathf.Lerp(ReferenceStreamHalfWidths[i],ReferenceStreamHalfWidths[i+1],t)+radius+.22f;
                    if(distance<clearance*clearance)return false;
                }
                foreach(var road in ReferenceRoads)
                for(int i=0;i<road.points.Length-1;i++)
                {
                    float distance=SegmentDistanceSquared(new Vector2(p.x,p.z),
                        road.points[i],road.points[i+1],out float t);
                    float grade=Mathf.Lerp(road.points[i].y,road.points[i+1].y,t);
                    // A path on the interior floor does not exclude its roof above.
                    if(Mathf.Abs(p.y-grade)<1.0f && distance<Mathf.Pow(road.width+radius+.12f,2))return false;
                }
                return ReferenceHeight(p.x,p.z)<=p.y+.18f;
            }

            bool TryTop(Vector2 localXZ,float radius,out Vector3 point,out Vector3 normal)
            {
                point=normal=default;
                var origin=cave.TransformPoint(new Vector3(localXZ.x,0,localXZ.y));
                origin.y=bounds.max.y+2;
                var ray=new Ray(origin,Vector3.down);
                bool found=false;RaycastHit best=default;
                foreach(var collider in colliders)
                {
                    if(!collider.enabled || !collider.gameObject.activeInHierarchy ||
                        !collider.transform.IsChildOf(cave))continue;
                    if(!collider.Raycast(ray,out var hit,bounds.size.y+4))continue;
                    if(hit.normal.y<.46f || cave.InverseTransformPoint(hit.point).y<2.55f)continue;
                    if(!found || hit.point.y>best.point.y){found=true;best=hit;}
                }
                if(!found)return false;
                point=root.InverseTransformPoint(best.point);
                normal=root.InverseTransformDirection(best.normal).normalized;
                return Clear(point,radius);
            }

            // Broad, interrupted cushions. The water slit and the exposed middle
            // of the entrance remain visible instead of receiving a uniform border.
            var patches=new[]{
                new Vector4(-6.75f,-2.25f,1.18f,1.12f),
                new Vector4(-4.80f,-2.90f,1.53f,.84f),
                new Vector4(.85f,-2.72f,1.31f,.78f),
                new Vector4(3.65f,-2.25f,1.76f,1.12f),
                new Vector4(6.45f,-2.42f,1.19f,1.00f),
                new Vector4(7.05f,-.45f,.92f,1.18f)
            };
            var carpet=PlantMaterial(plants,moss);
            for(int patch=0;patch<patches.Length;patch++)
            {
                var p=patches[patch];
                int columns=Mathf.CeilToInt(p.z*2/.29f),rows=Mathf.CeilToInt(p.w*2/.29f);
                int stride=columns+1;
                var indices=new int[stride*(rows+1)];
                var samples=new Vector2[indices.Length];
                for(int i=0;i<indices.Length;i++)indices[i]=-1;
                for(int row=0;row<=rows;row++)for(int column=0;column<=columns;column++)
                {
                    float u=(float)column/columns*2-1,v=(float)row/rows*2-1;
                    float edge=.88f+.12f*Mathf.Sin(u*7.1f+patch)+.08f*Mathf.Cos(v*9.3f-patch);
                    if(u*u+v*v>edge)continue;
                    int index=row*stride+column;
                    var sample=new Vector2(p.x+u*p.z+R(-.055f,.055f),p.y+v*p.w+R(-.055f,.055f));
                    samples[index]=sample;
                    if(!TryTop(sample,.08f,out var at,out var normal))continue;
                    indices[index]=carpet.Points.Count;
                    carpet.Points.Add(at+normal*.022f);
                }
                void Face(int a,int b,int c)
                {
                    int ia=indices[a],ib=indices[b],ic=indices[c];
                    if(ia<0 || ib<0 || ic<0)return;
                    var pa=carpet.Points[ia];var pb=carpet.Points[ib];var pc=carpet.Points[ic];
                    if(Mathf.Max(pa.y,Mathf.Max(pb.y,pc.y))-Mathf.Min(pa.y,Mathf.Min(pb.y,pc.y))>.31f)return;
                    // Check the face interior as well: moss must not span a crack,
                    // a missing rock corner or a drop into the open mouth.
                    if(!TryTop((samples[a]+samples[b]+samples[c])/3,.07f,out var mid,out _))return;
                    if(Mathf.Abs(mid.y-(pa.y+pb.y+pc.y)/3)>.15f)return;
                    carpet.Faces.AddRange(new[]{ia,ib,ic});mossFaces++;
                }
                for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
                {
                    int a=row*stride+column,b=a+1,c=a+stride,d=c+1;
                    Face(a,c,d);Face(a,d,b);
                }
                for(int attempt=0;attempt<64;attempt++)
                {
                    float u=R(-1,1),v=R(-1,1);
                    if(u*u+v*v>.90f || R(0,1)>.72f)continue;
                    var sample=new Vector2(p.x+u*p.z,p.y+v*p.w);
                    if(!TryTop(sample,.24f,out var at,out var normal))continue;
                    at+=normal*.012f;
                    float size=R(.76f,1.28f),heading=R(0,360);
                    int blades=random.Next(5,8);
                    for(int blade=0;blade<blades;blade++)
                        BloomBlade(PlantMaterial(plants,leaves[(blade+patch)%3]),at,
                            heading+blade*137.5f,R(.17f,.45f),R(.16f,.27f)*size,
                            R(.025f,.048f)*size,R(.015f,.045f));
                    tufts++;
                }
            }

            // A few readable cream/gold flower clumps sit within the cushions.
            // Different sizes, yaw and setbacks avoid identical beads around the rim.
            var flowerSeats=new[]{
                new Vector4(-6.45f,-2.0f,.69f,0),new Vector4(-5.05f,-3.12f,.79f,1),
                new Vector4(-4.20f,-2.25f,.58f,0),new Vector4(.82f,-2.67f,.64f,0),
                new Vector4(2.65f,-2.97f,.72f,1),new Vector4(4.60f,-1.95f,.84f,0),
                new Vector4(6.10f,-2.92f,.66f,0),new Vector4(7.05f,-1.05f,.58f,1)
            };
            foreach(var seat in flowerSeats)
            {
                for(int attempt=0;attempt<4;attempt++)
                {
                    var sample=new Vector2(seat.x,seat.y)+(attempt==0?Vector2.zero:new Vector2(R(-.25f,.25f),R(-.22f,.22f)));
                    if(!TryTop(sample,seat.z*.85f,out var at,out var normal) || normal.y<.66f)continue;
                    int supported=0;bool tooSteep=false;
                    for(int side=0;side<4;side++)
                    {
                        float angle=side*Mathf.PI*.5f;
                        var probe=sample+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(seat.z*.50f);
                        if(!TryTop(probe,.08f,out var foot,out _))continue;
                        supported++;
                        if(Mathf.Abs(foot.y-at.y)>.34f)tooSteep=true;
                    }
                    if(supported<3 || tooSteep)continue;
                    var plant=ReferenceAsset("Geology/flowers_"+(seat.w>.5f?"gold":"cream"),at-normal*.045f,R(0,360));
                    if(plant==null)break;
                    plant.name="潮洞岩肩 · "+(seat.w>.5f?"金花":"白花")+" "+flowers;
                    plant.transform.SetParent(root,true);
                    plant.transform.localScale=new Vector3(seat.z*1.05f,seat.z*.88f,seat.z*.94f);
                    plant.transform.localRotation=Quaternion.FromToRotation(Vector3.up,
                        Vector3.Slerp(Vector3.up,normal,.30f))*plant.transform.localRotation;
                    flowers++;break;
                }
            }

            bool TryFront(float x,float y,out Vector3 point,out Vector3 normal)
            {
                point=normal=default;
                var ray=new Ray(cave.TransformPoint(new Vector3(x,y,-7.5f)),cave.forward);
                bool found=false;RaycastHit best=default;
                foreach(var collider in colliders)
                {
                    if(!collider.enabled || !collider.gameObject.activeInHierarchy ||
                        !collider.transform.IsChildOf(cave))continue;
                    if(!collider.Raycast(ray,out var hit,15))continue;
                    var local=cave.InverseTransformPoint(hit.point);
                    // A ray through the opening can hit its closed rear wall; it
                    // must never turn a hanging exterior vine into interior plants.
                    if(local.z> -2.65f || local.y<1.8f || Vector3.Dot(hit.normal,-cave.forward)<.18f)continue;
                    if(!found || hit.distance<best.distance){found=true;best=hit;}
                }
                if(!found)return false;
                normal=root.InverseTransformDirection(best.normal).normalized;
                point=root.InverseTransformPoint(best.point)+normal*.035f;
                return Clear(point,.19f);
            }

            var vineStarts=new[]{new Vector2(-6.05f,-3.27f),new Vector2(-4.48f,-3.33f),
                new Vector2(.22f,-3.40f),new Vector2(2.10f,-3.30f),
                new Vector2(4.75f,-3.20f),new Vector2(6.30f,-3.14f)};
            foreach(var seed in vineStarts)
            {
                if(!TryTop(seed,.25f,out var at,out _))continue;
                var local=cave.InverseTransformPoint(root.TransformPoint(at));
                int count=random.Next(6,11),made=0;Vector3 previous=default;
                float phase=R(0,6.28f);
                for(int step=0;step<count;step++)
                {
                    float x=local.x+Mathf.Sin(step*.71f+phase)*.075f;
                    if(!TryFront(x,local.y-.05f-step*.145f,out var p,out _))
                    {if(made>0)break;continue;}
                    if(made>0)
                    {
                        if(Vector3.Distance(previous,p)>.37f)break;
                        BloomStem(PlantMaterial(plants,stems),previous,p,.009f);
                    }
                    for(int sign=-1;sign<=1;sign+=2)
                        BloomBlade(PlantMaterial(plants,leaves[(step+made)%3]),p,
                            sign*R(102,123),-.17f,R(.15f,.23f),R(.045f,.073f),-.025f);
                    previous=p;made++;
                }
                if(made>1)trails++;
            }

            PublishPlants(root,"贴岩苔草与垂叶",plants);
            referenceCavePlantingBuilt=true;
            Debug.Log($"Cave shoulder planting: moss triangles={mossFaces}, grass tufts={tufts}, flower groups={flowers}, exterior trails={trails}");
        }
    }
}

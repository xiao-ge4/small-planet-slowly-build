using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        const string ReferenceForeshoreAsset="Geology/cliff_bedding";
        static ReferenceForeshoreGeometry referenceForeshoreGeometry;
        static List<ReferenceForeshoreVolume> referenceForeshoreVolumes;
        static bool referenceForeshoreLoaded;
        bool referenceCliffsBuilt;
        static readonly Vector2[] ReferenceForeshoreInlets={new(-13.5f,-40.7f),new(-7.8f,-39.2f)};

        // Before final roads: the pond and plateau north of z=-32 stay unchanged.
        static void ApplyReferenceForeshoreProfile(float x,float z,ref float y,ref int surface,ref float paving,ref float wet)
        {
            float alongMask=ReferenceForeshoreSmooth(-19.4f,-18.2f,x)*(1-ReferenceForeshoreSmooth(-6,-4.2f,x));
            if(alongMask<=0)return;
            float sectionZ=z-ReferenceForeshoreCurve(x);
            if(sectionZ>=-32||sectionZ<=-43)return;
            float profile;
            if(sectionZ>=-33.5f)profile=Mathf.Lerp(0,-.45f,(-32-sectionZ)/1.5f);
            else if(sectionZ>=-35.2f)profile=Mathf.Lerp(-.45f,-1.4f,(-33.5f-sectionZ)/1.7f);
            else if(sectionZ>=-36)profile=Mathf.Lerp(-1.4f,-2.4f,(-35.2f-sectionZ)/.8f);
            else if(sectionZ>=-38.5f)profile=Mathf.Lerp(-2.4f,-2.7f,(-36-sectionZ)/2.5f);
            else if(sectionZ>=-41)profile=Mathf.Lerp(-2.7f,-3.45f,(-38.5f-sectionZ)/2.5f);
            else profile=Mathf.Lerp(-3.45f,-4.65f,(-41-sectionZ)/2);
            float weight=alongMask*ReferenceForeshoreSmooth(-32,-32.25f,sectionZ)*ReferenceForeshoreSmooth(-43,-41.6f,sectionZ);
            y=Mathf.Lerp(y,profile,weight);
            // Two shallow scallops between the low rock tongues break the straight
            // retaining edge. The walking arc is restored by the final shared survey.
            for(int i=0;i<ReferenceForeshoreInlets.Length;i++)
            {
                float dx=(x-ReferenceForeshoreInlets[i].x)/1.55f,dz=(z-ReferenceForeshoreInlets[i].y)/1.25f;
                float inlet=1-ReferenceForeshoreSmooth(.18f,1,dx*dx+dz*dz);
                y=Mathf.Lerp(y,ReferenceSeaLevel-.26f,inlet);
                wet=Mathf.Max(wet,inlet);paving*=1-inlet;
            }
            if(weight>.5f)surface=sectionZ<-35.6f?4:3;
            paving*=1-weight;
            wet=Mathf.Max(wet,weight*ReferenceForeshoreSmooth(-2.3f,-3.35f,profile));
        }

        static float ReferenceForeshoreSmooth(float a,float b,float v)
            => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));

        static float ReferenceForeshoreCurve(float x)
        {
            float q=(x+12.5f)/7.5f;
            return .80f*q*q+.08f*Mathf.Sin((x+12.5f)*.4f);
        }

        void BuildReferenceCliffs()
        {
            if(referenceCliffsBuilt)return;
            referenceCliffsBuilt=true;
            EnsureReferenceForeshoreVolumes();
            if(referenceForeshoreGeometry==null)return;
            var root=Group("南岸完整岩坎样板",details);
            foreach(var volume in referenceForeshoreVolumes)
            {
                var model=ReferenceAsset(ReferenceForeshoreAsset,volume.position);
                if(model==null)continue;
                model.name="完整南岸岩坎 · "+volume.label;
                model.transform.SetParent(root,false);
                model.transform.localPosition=volume.position;
                model.transform.localRotation=volume.rotation;
                model.transform.localScale=volume.scale;
                SetReferenceRockMap(model,"cliff_bedding");
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    filter.gameObject.layer=ObstacleLayer;
                    var collider=filter.GetComponent<MeshCollider>();
                    if(collider==null)collider=filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh=filter.sharedMesh;
                }
            }
            // Broad, low reef tongues belong to the submerged shelf. They are not
            // free-standing cones, and their actual meshes own the same collision.
            var toes=new[]{new Vector3(-16.9f,-3.42f,-39.8f),new Vector3(-10.1f,-3.40f,-41.4f),new Vector3(-5,-3.42f,-39.2f)};
            var scales=new[]{new Vector3(1.0f,.36f,1.10f),new Vector3(.95f,.32f,1.05f),new Vector3(.86f,.31f,1.05f)};
            for(int i=0;i<toes.Length;i++)
            {
                var rock=ReferenceAsset("Geology/coast_rock",toes[i],14+i*37);
                if(rock==null)continue;rock.transform.SetParent(root,true);rock.transform.localScale=scales[i];
                rock.name="贴海低圆岩脚 · "+i;SetReferenceRockMap(rock,"coast_rock");
                var renderers=rock.GetComponentsInChildren<Renderer>();
                var surveyed=new Bounds();bool first=true;
                foreach(var renderer in renderers)for(int corner=0;corner<8;corner++)
                {
                    var bound=renderer.bounds;
                    var world=new Vector3((corner&1)==0?bound.min.x:bound.max.x,(corner&2)==0?bound.min.y:bound.max.y,(corner&4)==0?bound.min.z:bound.max.z);
                    var point=details.InverseTransformPoint(world);
                    if(first){surveyed=new Bounds(point,Vector3.zero);first=false;}else surveyed.Encapsulate(point);
                }
                if(!ReferenceForeshoreBoundsClear(surveyed))
                {
                    Debug.LogWarning("Foreshore toe skipped to preserve the complete walking corridor: "+i);
                    rock.SetActive(false);Destroy(rock);continue;
                }
                foreach(var filter in rock.GetComponentsInChildren<MeshFilter>())
                {filter.gameObject.layer=ObstacleLayer;var c=filter.gameObject.AddComponent<MeshCollider>();c.sharedMesh=filter.sharedMesh;}
            }
            Debug.Log("Foreshore solid cliffs: "+referenceForeshoreVolumes.Count+" closed source volumes; front soil and collision use the same lowered profile; closed rock colliders supply the cliff surface.");
        }

        // Keep every triangle; terrain height sampling handles the exposed cliff toe.
        static bool ReferenceCliffReplacesGround(Vector3 triangleCenter)=>false;

        static void EnsureReferenceForeshoreVolumes()
        {
            if(referenceForeshoreLoaded)return;
            referenceForeshoreLoaded=true;
            referenceForeshoreVolumes=new List<ReferenceForeshoreVolume>();
            var source=Resources.Load<GameObject>("CoastReference/"+ReferenceForeshoreAsset);
            if(source==null){Debug.LogWarning("Foreshore rock missing: leave terrain render intact.");return;}
            referenceForeshoreGeometry=ReferenceForeshoreGeometry.Read(source);
            if(referenceForeshoreGeometry==null){Debug.LogWarning("Foreshore rock unreadable: leave terrain render intact.");return;}
            // The diagonal stair crosses in front of the pond. The former western
            // solid volume occupied that exact corridor; leave a real opening and
            // keep these two complete rocks on the uphill side of the new flight.
            AddReferenceForeshoreVolume("斜阶内侧岩肩",-9.5f,2.1f,-.12f,-2.65f,1.5f,1.20f);
            AddReferenceForeshoreVolume("东侧退后岩肩",-6.6f,2.6f,-.12f,-2.80f,1.5f,.50f);
        }

        static void AddReferenceForeshoreVolume(string label,float x,float width,float top,float bottom,float depth,float setback=0)
        {
            // The back reaches the intact upper ground around z=-32.5; the full
            // front reaches the real lower toe around z=-36. A shallow block at
            // z=-33 would again leave lower faces buried behind foreground soil.
            float z=-34.3f+ReferenceForeshoreCurve(x)+setback;
            float slope=(ReferenceForeshoreCurve(x+.2f)-ReferenceForeshoreCurve(x-.2f))/.4f;
            var outward=new Vector2(slope,-1).normalized;
            var geometry=referenceForeshoreGeometry;
            float yaw=Mathf.Atan2(outward.x,outward.y)*Mathf.Rad2Deg-geometry.nativeFacing;
            var rotation=Quaternion.Euler(0,yaw,0);
            var scale=new Vector3(width/geometry.bounds.size.x,(top-bottom)/geometry.bounds.size.y,depth/geometry.bounds.size.z);
            var pivot=new Vector3(geometry.bounds.center.x,geometry.bounds.min.y,geometry.bounds.center.z);
            var position=new Vector3(x,bottom,z)-rotation*Vector3.Scale(scale,pivot);
            var matrix=Matrix4x4.TRS(position,rotation,scale);
            var bounds=new Bounds(matrix.MultiplyPoint3x4(geometry.bounds.center),Vector3.zero);
            for(int i=0;i<8;i++)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((i&1)==0?geometry.bounds.min.x:geometry.bounds.max.x,
                    (i&2)==0?geometry.bounds.min.y:geometry.bounds.max.y,(i&4)==0?geometry.bounds.min.z:geometry.bounds.max.z)));
            if(!ReferenceForeshoreBoundsClear(bounds))
            {
                Debug.LogWarning("Foreshore closed rock rejected by full-bounds path/pond check: "+label+" "+bounds);
                return;
            }
            referenceForeshoreVolumes.Add(new ReferenceForeshoreVolume
            {label=label,position=position,rotation=rotation,scale=scale,inverse=matrix.inverse,bounds=bounds,top=top,bottom=bottom});
        }

        static bool ReferenceForeshoreBoundsClear(Bounds bounds)
        {
            // Check the full transformed source bounds, not the placement pivot. The
            // conservative box includes every fracture tip and the actual MeshCollider.
            foreach(var road in ReferenceRoads)
            for(int i=0;i<road.points.Length-1;i++)
            {
                var a=road.points[i];var b=road.points[i+1];
                if(bounds.max.y<Mathf.Min(a.y,b.y)-.25f)continue;
                var center=new Vector2(bounds.center.x,bounds.center.z);
                if(ReferenceRockSegmentHits(center,Vector2.right,Vector2.up,
                    bounds.extents.x+road.width+.24f,bounds.extents.z+road.width+.24f,
                    new Vector2(a.x,a.z),new Vector2(b.x,b.z)))return false;
            }
            float nearX=Mathf.Clamp(ReferencePondX,bounds.min.x,bounds.max.x);
            float nearZ=Mathf.Clamp(ReferencePondZ,bounds.min.z,bounds.max.z);
            if(bounds.max.y>-.40f && Mathf.Pow((nearX-ReferencePondX)/2.38f,2)+Mathf.Pow((nearZ-ReferencePondZ)/1.88f,2)<1)return false;
            return true;
        }

        sealed class ReferenceForeshoreVolume
        {
            public string label;
            public Vector3 position,scale;
            public Quaternion rotation;
            public Matrix4x4 inverse;
            public Bounds bounds;
            public float top,bottom;
        }

        sealed class ReferenceForeshoreGeometry
        {
            public Bounds bounds;
            public float nativeFacing=180;

            public static ReferenceForeshoreGeometry Read(GameObject source)
            {
                var vertices=new List<Vector3>();
                float largest=0,facing=180;
                foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh;
                    if(mesh==null)continue;
                    if(!mesh.isReadable)return null;
                    var matrix=source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    var points=mesh.vertices;var faces=mesh.triangles;var normals=mesh.normals;
                    foreach(var p in points)vertices.Add(matrix.MultiplyPoint3x4(p));
                    // Broad unbroken back versus smaller fracture front faces.
                    for(int i=0;i+2<faces.Length;i+=3)
                    {
                        int a=faces[i],b=faces[i+1],c=faces[i+2];
                        var pa=matrix.MultiplyPoint3x4(points[a]);var pb=matrix.MultiplyPoint3x4(points[b]);var pc=matrix.MultiplyPoint3x4(points[c]);
                        float area=Vector3.Cross(pb-pa,pc-pa).sqrMagnitude;
                        var n=normals.Length==points.Length?matrix.MultiplyVector(normals[a]+normals[b]+normals[c]).normalized:Vector3.Cross(pb-pa,pc-pa).normalized;
                        if(area<=largest||Mathf.Abs(n.z)<.8f||Mathf.Abs(n.y)>.12f)continue;
                        largest=area;facing=Mathf.Atan2(-n.x,-n.z)*Mathf.Rad2Deg;
                    }
                }
                if(vertices.Count==0)return null;
                var bounds=new Bounds(vertices[0],Vector3.zero);
                foreach(var p in vertices)bounds.Encapsulate(p);
                return new ReferenceForeshoreGeometry{bounds=bounds,nativeFacing=facing};
            }
        }
    }
}

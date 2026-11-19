using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // Three actual tree assets sit on the surveyed rock crown. Their roots
        // follow the finished collider, including future local geological cuts.
        void BuildReferenceRidgeCanopy()
        {
            Transform ridge=null;
            foreach(Transform child in details)
                if(child.name=="参考资产 · Geology/central_ridge"){ridge=child;break;}
            if(ridge==null)return;
            Physics.SyncTransforms();
            var colliders=ridge.GetComponentsInChildren<Collider>();
            var seats=new[]{new Vector3(.3f,.55f,18.7f),new Vector3(2.2f,.42f,20.1f),new Vector3(-.65f,.36f,21.1f)};
            var plantedPoints=new System.Collections.Generic.List<Vector3>();
            int planted=0;
            for(int i=0;i<seats.Length;i++)
            {
                var p=seats[i];
                bool found=false;RaycastHit top=default;
                // Search a small neighbourhood for a real crown seat. A rejected
                // single ray used to silently omit two thirds of the canopy.
                for(int sample=0;sample<25&&!found;sample++)
                {
                    float a=sample*2.39996f,r=sample==0?0:Mathf.Sqrt(sample/24f)*1.75f;
                    var q=p+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                    if(ReferenceRidgePathContains(q.x,q.z,1.2f))continue;
                    bool crowded=false;
                    foreach(var previous in plantedPoints)
                        if(new Vector2(previous.x-q.x,previous.z-q.z).magnitude<1.45f)crowded=true;
                    for(int stream=0;stream<ReferenceStream.Length-1;stream++)
                        if(SegmentDistanceSquared(new Vector2(q.x,q.z),ReferenceStream[stream],ReferenceStream[stream+1],out _) < 1.4f*1.4f)crowded=true;
                    if(crowded)continue;
                    var ray=new Ray(details.TransformPoint(new Vector3(q.x,20,q.z)),-details.up);
                    foreach(var collider in colliders)
                        if(collider.Raycast(ray,out var hit,25)&&hit.normal.y>.52f&&hit.point.y>6&&(!found||hit.point.y>top.point.y))
                        {top=hit;found=true;}
                }
                if(!found)continue;
                var point=details.InverseTransformPoint(top.point);
                if(point.y<6)continue;
                var tree=ReferenceAsset("Buildings/courtyard_tree_hero",point-Vector3.up*.06f,37+i*93);
                if(tree==null)continue;
                tree.name="岩岭树冠 · "+i;
                tree.transform.localScale=Vector3.one*p.y;
                plantedPoints.Add(point);
                planted++;
            }
            Debug.Log("Reference ridge canopy: "+planted+" trees seated on actual closed rock surface; paths and water unchanged.");
        }
    }
}

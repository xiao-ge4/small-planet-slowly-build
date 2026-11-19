using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // The open court between the tree, pool and bridge is a worn stone court,
        // not a large bare limestone sheet. Shallow dressing only; the surveyed
        // ground remains the physical walking surface.
        void BuildReferenceHomeCourtFinish()
        {
            var root=Group("树池之间 · 旧石小院",details);
            var colors=new[]{new Color(.85f,.74f,.59f),new Color(.91f,.81f,.65f),new Color(.81f,.70f,.56f)};
            var vertices=new List<Vector3>[3];var faces=new List<int>[3];
            for(int i=0;i<3;i++){vertices[i]=new List<Vector3>();faces[i]=new List<int>();}
            var random=new System.Random(91723);int count=0;
            float J(float size)=>((float)random.NextDouble()-.5f)*size;
            bool Ground(Vector2 p,out float height)
            {
                height=SampleReferenceHeight(p.x,p.y,out int surface,out _,out _,out bool waterCovered);
                if(waterCovered||surface!=1||Mathf.Abs(height)>.16f)return false;
                if(Mathf.Pow((p.x+12.3f)/6.1f,2)+Mathf.Pow((p.y+27.1f)/5.2f,2)>1)return false;
                if(Mathf.Pow((p.x-ReferencePondX)/2.48f,2)+Mathf.Pow((p.y-ReferencePondZ)/1.96f,2)<1)return false;
                foreach(var road in ReferenceRoads)for(int k=0;k<road.points.Length-1;k++)
                    if(SegmentDistanceSquared(p,road.points[k],road.points[k+1],out _) <
                        Mathf.Pow(road.width*.82f+.13f,2))return false;
                return true;
            }
            for(int row=0;row<21;row++)for(int col=0;col<25;col++)
            {
                var center=new Vector2(-19+col*.57f+(row%2)*.285f+J(.06f),-33+row*.51f+J(.06f));
                if(!Ground(center,out float y))continue;
                var rim=new Vector3[6];bool okay=true;
                float heading=J(9)*Mathf.Deg2Rad;
                for(int j=0;j<6;j++)
                {
                    float angle=j*Mathf.PI/3+heading;
                    float radius=.32f+J(.035f);
                    var p=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                    if(!Ground(p,out float floor)||Mathf.Abs(floor-y)>.07f){okay=false;break;}
                    rim[j]=new Vector3(p.x,floor+.009f,p.y);
                }
                if(!okay)continue;
                int bucket=random.Next(3),first=vertices[bucket].Count;
                var v=vertices[bucket];var f=faces[bucket];v.AddRange(rim);
                var middle=new Vector3(center.x,y+.026f,center.y);
                for(int j=0;j<6;j++)v.Add(Vector3.Lerp(middle,rim[j]+Vector3.up*.017f,.86f));
                int cap=v.Count;v.Add(middle);
                for(int j=0;j<6;j++)
                {
                    int n=(j+1)%6;
                    f.AddRange(new[]{first+j,first+6+n,first+n,first+j,first+6+j,first+6+n,
                        cap,first+6+n,first+6+j});
                }
                count++;
            }
            for(int i=0;i<3;i++)if(vertices[i].Count>0)
                MeshObject("小院不规则浅石 · "+i,root,vertices[i],faces[i],Paint("旧院浅暖石 · "+i,colors[i],.07f,.09f));
            Debug.Log("Reference courtyard finish: "+count+" shallow stones; tree lawn, pool and original paths excluded.");
        }
    }
}

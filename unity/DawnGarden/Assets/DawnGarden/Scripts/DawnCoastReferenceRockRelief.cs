using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        void FinishReferenceRampPavers(Transform parent,Vector3 a,Vector3 b,Vector3 side)
        {
            var delta=b-a;var heading=delta;heading.y=0;float length=heading.magnitude;heading.Normalize();
            var across=side.normalized;float slope=delta.y/Mathf.Max(.01f,length);
            var colors=new[]{new Color(.88f,.69f,.51f),new Color(.92f,.73f,.55f),new Color(.83f,.62f,.47f)};
            var points=new List<Vector3>[3];var triangles=new List<int>[3];
            for(int k=0;k<3;k++){points[k]=new List<Vector3>();triangles[k]=new List<int>();}
            int rows=Mathf.CeilToInt(length/.55f);float step=length/rows;
            for(int row=0;row<rows;row++)for(int lane=0;lane<5;lane++)
            {
                float distance=(row+.5f)*step;
                var center=a+heading*distance+Vector3.up*(slope*distance)+across*((lane-2)*.56f);
                float width=.268f,depth=step*.475f;int bucket=(row*7+lane*2)%3;
                var v=points[bucket];var f=triangles[bucket];int first=v.Count;
                // Eight worn corners, two physical bevel rings. Same scale and
                // pigment as the adjoining real path stones, no painted grid.
                var offsets=new[]{new Vector2(-width,-depth+.07f),new(-width+.07f,-depth),
                    new(width-.07f,-depth),new(width,-depth+.07f),new(width,depth-.07f),
                    new(width-.07f,depth),new(-width+.07f,depth),new(-width,depth-.07f)};
                for(int ring=0;ring<2;ring++)foreach(var offset in offsets)
                {
                    var o=offset*(ring==0?1:.88f);
                    v.Add(center+across*o.x+heading*o.y+Vector3.up*(slope*o.y+(ring==0?.008f:.030f)));
                }
                int cap=v.Count;v.Add(center+Vector3.up*.035f);
                for(int corner=0;corner<8;corner++)
                {
                    int next=(corner+1)%8;
                    f.AddRange(new[]{first+corner,first+8+next,first+next,first+corner,first+8+corner,first+8+next,
                        cap,first+8+next,first+8+corner});
                }
            }
            for(int k=0;k<3;k++)MeshObject("桥头连接石路 · "+k,parent,points[k],triangles[k],
                Paint("桥头圆磨桃石 · "+k,colors[k],.1f,.12f));
        }

        // Shape the exposed land itself. This joins the grass cap and buried
        // authored rocks without adding a second, floating shell over the bank.
        // Kept outside every saved plot, road, staircase and water profile.
        void SculptReferenceRockRelief(List<Vector3> vertices,List<Vector2> surface,int columns,int rows,float spacing)
        {
            var original=vertices.ToArray();int changed=0;float largest=0;
            for(int row=1;row<rows-1;row++)for(int col=1;col<columns-1;col++)
            {
                int index=row*columns+col;var p=original[index];
                float mask=surface[index].y;
                if(mask<.08f||p.y<ReferenceSeaLevel+.10f)continue;
                if(p.x>3.7f&&p.x<19.7f&&Mathf.Abs(p.z)<6.7f)continue;
                if(p.x>-5.5f&&p.x<13.4f&&p.z> -30.5f&&p.z< -16.8f)continue;
                var point=new Vector2(p.x,p.z);
                foreach(var road in ReferenceRoads)
                for(int i=0;i<road.points.Length-1;i++)
                {
                    float d=Mathf.Sqrt(SegmentDistanceSquared(point,road.points[i],road.points[i+1],out float t));
                    float grade=Mathf.Lerp(road.points[i].y,road.points[i+1].y,t);
                    // A bank below a stair is part of the rock body, not its
                    // walkable surface. Keep the full corridor at foot height.
                    if(p.y<grade-.62f)continue;
                    mask*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(road.width+.68f,road.width+1.12f,d));
                }
                for(int i=0;i<ReferenceStream.Length-1;i++)
                {
                    float d=Mathf.Sqrt(SegmentDistanceSquared(point,ReferenceStream[i],ReferenceStream[i+1],out float t));
                    float w=Mathf.Lerp(ReferenceStreamHalfWidths[i],ReferenceStreamHalfWidths[i+1],t);
                    mask*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(w+.48f,w+.82f,d));
                }
                if(mask<.01f)continue;
                float dx=(original[index+1].y-original[index-1].y)/(spacing*2);
                float dz=(original[index+columns].y-original[index-columns].y)/(spacing*2);
                var normal=new Vector3(-dx,1,-dz).normalized;
                // Two broad fractured scales, bounded to 16cm. Their phases
                // depend on world space, never on the terrain grid index.
                float coarse=Mathf.PerlinNoise(p.x*.64f+p.y*.23f+31,p.z*.64f-p.y*.19f+57);
                float ledge=Mathf.PerlinNoise(p.x*1.31f+91,p.z*1.31f+p.y*.73f+17);
                float relief=((coarse-.46f)*.33f+(ledge-.5f)*.11f)*mask;
                relief=Mathf.Clamp(relief,-.12f,.16f);
                vertices[index]=p+normal*relief;
                largest=Mathf.Max(largest,Mathf.Abs(relief));changed++;
            }
            Debug.Log($"Exposed bank relief: {changed} terrain vertices, max {largest:F3}m; saved plot, water and road + .68m protected.");
        }
    }
}

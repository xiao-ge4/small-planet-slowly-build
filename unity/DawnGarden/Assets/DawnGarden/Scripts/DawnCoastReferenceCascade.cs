using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        Material referenceCascadeVolume;

        // Three connected flows leave the same surveyed lip and land in the
        // existing pool. Elliptical cross-sections give the fall real volume
        // from the walking camera as well as the fixed overview.
        void BuildReferenceCascade(Transform parent,Vector3 a,Vector3 b,Vector3 side,float sourceWidth,float targetWidth)
        {
            if(referenceCascadeVolume==null)
            {
                referenceCascadeVolume=new Material(caveFallSurface){name="潮洞 · 分股立体跌水"};
                referenceCascadeVolume.SetFloat("_Cull",2);
                referenceCascadeVolume.SetFloat("_Opacity",.95f);
                materials.Add(referenceCascadeVolume);
            }
            var forward=HorizontalDirection(b-a);float length=Vector3.Distance(a,b);
            const int rings=32,sides=12;
            var v=new List<Vector3>();var uv=new List<Vector2>();var f=new List<int>();
            for(int flow=0;flow<3;flow++)
            {
                int first=v.Count;float phase=flow*2.13f;
                for(int row=0;row<=rings;row++)
                {
                    float t=row/(float)rings,belly=Mathf.Sin(t*Mathf.PI);
                    float centre=(flow-1)*Mathf.Lerp(sourceWidth*.61f,targetWidth*.58f,t);
                    centre+=.055f*Mathf.Sin(t*8.7f+phase)*belly;
                    var p=Vector3.Lerp(a,b,t)+side*centre+forward*(.20f*belly);
                    p.y=Mathf.Lerp(a.y,b.y,t*.30f+t*t*.70f);
                    float width=Mathf.Lerp(sourceWidth*.39f,targetWidth*.34f,t)*(1-.13f*belly);
                    width*=1+.08f*Mathf.Sin(t*18+phase)*belly;
                    float thickness=.035f+.045f*belly;
                    for(int slice=0;slice<sides;slice++)
                    {
                        float angle=slice*Mathf.PI*2/sides;
                        v.Add(p+side*(Mathf.Cos(angle)*width)+forward*(Mathf.Sin(angle)*thickness));
                        uv.Add(new Vector2((Mathf.Cos(angle)+1)*.5f,t*length+flow*.73f));
                        if(row<rings)
                        {
                            int n=first+row*sides+slice,q=first+row*sides+(slice+1)%sides;
                            f.AddRange(new[]{n,n+sides,q,q,n+sides,q+sides});
                        }
                    }
                }
                // Flow caps remain in the upstream surface and receiving water.
                int top=v.Count;var topCenter=Vector3.zero;var bottomCenter=Vector3.zero;
                for(int slice=0;slice<sides;slice++){topCenter+=v[first+slice];bottomCenter+=v[first+rings*sides+slice];}
                v.Add(topCenter/sides);uv.Add(new Vector2(.5f,flow*.73f));
                v.Add(bottomCenter/sides);uv.Add(new Vector2(.5f,length+flow*.73f));
                for(int slice=0;slice<sides;slice++)
                {
                    int q=(slice+1)%sides;
                    f.AddRange(new[]{top,first+slice,first+q,top+1,first+rings*sides+q,first+rings*sides+slice});
                }
            }
            MeshObject("潮洞落水 · 三股收束与散开",parent,v,f,referenceCascadeVolume,false,uv);
            FoamingRunoff(parent,b+forward*.16f,forward,targetWidth*1.12f,false);
        }
    }
}

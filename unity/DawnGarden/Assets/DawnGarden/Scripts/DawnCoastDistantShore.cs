using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        Material distantShoreMaterial;
        void BuildDistantShore()
        {
            var horizon=Group("远海的层叠岸线 · 背景景深",details);
            var horizonShader=Resources.Load<Shader>("Coast/Horizon");
            if(horizonShader==null)return;
            distantShoreMaterial=new Material(horizonShader){name="远岸空气透视"};
            materials.Add(distantShoreMaterial);
            // Distant coastline only: no colliders, gameplay anchors or save data.
            var ranges=new[]{
                (new Vector3(-130,-1.35f,-355),new Vector3(260,34,105),127),
                (new Vector3(190,-1.35f,-410),new Vector3(195,27,88),413),
                (new Vector3(-320,-1.35f,-150),new Vector3(100,22,210),831)
            };
            for(int k=0;k<ranges.Length;k++)
            {
                var (center,extent,seed)=ranges[k];
                var vertices=new List<Vector3>();var triangles=new List<int>();
                const int columns=70,rows=20;
                for(int row=0;row<=rows;row++)
                for(int column=0;column<=columns;column++)
                {
                    float u=column/(float)columns,v=row/(float)rows;
                    float envelope=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI)),1.1f);
                    float ridges=.28f+.72f*Mathf.PerlinNoise(u*8.2f+seed,v*3.7f+seed*.13f);
                    float height=extent.y*envelope*ridges;
                    float cuts=.85f+.15f*Mathf.Cos(u*73+v*11);
                    var p=center+new Vector3((u-.5f)*extent.x,height*cuts,(v-.5f)*extent.z);
                    vertices.Add(p);
                    if(row==0||column==0)continue;
                    int a=(row-1)*(columns+1)+column-1,b=a+1,c=row*(columns+1)+column-1,d=c+1;
                    triangles.AddRange(new[]{a,c,b,b,c,d});
                }
                var ridge=MeshObject("远海岩岭 "+k,horizon,vertices,triangles,distantShoreMaterial);
                ridge.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}

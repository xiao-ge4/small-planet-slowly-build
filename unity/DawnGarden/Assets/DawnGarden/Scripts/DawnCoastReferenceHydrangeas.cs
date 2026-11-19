using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // The reference uses broad blue/cream flower heads at the upper terraces.
        // These share the planted-ground survey and the existing mesh batches; none
        // may occupy the saved plot, an aisle, a canal or unsupported cliff edge.
        int BuildReferenceHydrangeas(Dictionary<Material,NurseryGeometry> batches,
            Material[] leaves,Material stem,ReferencePlantGround survey)
        {
            var petals=new[]{
                NurseryMat("绣球 · 晨蓝",new Color(.34f,.61f,.86f),.04f),
                NurseryMat("绣球 · 粉蓝",new Color(.64f,.78f,.96f),.04f),
                NurseryMat("绣球 · 奶白",new Color(.94f,.92f,.78f),.04f)
            };
            var banks=new[]{new Vector2(10.4f,28.5f),new Vector2(18.8f,27.1f),
                new Vector2(25.8f,28.6f),new Vector2(8.1f,20.8f),
                new Vector2(-20.8f,14.7f),new Vector2(-15.5f,16.9f),new Vector2(-9.2f,14.9f)};
            int roots=0,heads=0;
            for(int bank=0;bank<banks.Length;bank++)for(int plant=0;plant<13;plant++)
            {
                float angle=plant*2.39996f+bank*.61f;
                float spread=Mathf.Sqrt(plant/12f)*1.38f;
                var point=banks[bank]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)*.64f)*spread;
                if(!survey(point,.45f,out var at,out _))continue;
                float scale=.90f+.13f*Mathf.Sin(plant*3.6f+bank);
                ReferenceBorderShrub(batches,at,scale*2,bank*151+plant,leaves,stem);
                for(int head=0;head<5;head++)
                {
                    float yaw=head*2.39996f+plant;
                    float radial=head==0?0:.25f*scale;
                    var center=at+new Vector3(Mathf.Cos(yaw)*radial,(head==0?.99f:.77f)*scale,Mathf.Sin(yaw)*radial);
                    BloomStem(PlantMaterial(batches,stem),at+Vector3.up*.20f,center,.013f);
                    float radius=(head==0?.25f:.21f)*scale;
                    for(int blossom=0;blossom<30;blossom++)
                    {
                        float v=.10f+.87f*(blossom+.5f)/30;
                        float theta=blossom*2.39996f+head;
                        var direction=new Vector3(Mathf.Cos(theta)*Mathf.Sqrt(1-v*v),v,Mathf.Sin(theta)*Mathf.Sqrt(1-v*v));
                        var p=center+direction*radius;
                        var tangent=Vector3.Cross(direction,Vector3.right).normalized;
                        var bitangent=Vector3.Cross(direction,tangent);
                        var geometry=PlantMaterial(batches,petals[(blossom+head+bank)%7<4?bank%2:(blossom%3)]);
                        float size=(.055f+.008f*Mathf.Sin(blossom*5))*scale;
                        for(int petal=0;petal<4;petal++)
                        {
                            float a=petal*Mathf.PI*.5f+head*.3f;
                            var forward=tangent*Mathf.Cos(a)+bitangent*Mathf.Sin(a);
                            var side=Vector3.Cross(direction,forward);
                            int first=geometry.Points.Count;
                            geometry.Points.Add(p);
                            geometry.Points.Add(p+forward*size*.65f-side*size*.50f+direction*.008f);
                            geometry.Points.Add(p+forward*size*1.40f-side*size*.27f+direction*.018f);
                            geometry.Points.Add(p+forward*size*1.40f+side*size*.27f+direction*.018f);
                            geometry.Points.Add(p+forward*size*.65f+side*size*.50f+direction*.008f);
                            geometry.Faces.AddRange(new[]{first,first+1,first+2,first,first+2,first+3,first,first+3,first+4});
                        }
                    }
                    heads++;
                }
                roots++;
            }
            Debug.Log($"Reference hydrangea banks: {roots} supported shrubs, {heads} blue/cream flower heads; shared batches, routes excluded.");
            return roots;
        }
    }
}

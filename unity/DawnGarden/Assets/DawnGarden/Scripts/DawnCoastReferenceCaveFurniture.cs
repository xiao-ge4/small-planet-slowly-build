using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        void BuildReferenceTreeBench(Transform parent)
        {
            var bench=Group("树下弧形浅石长凳",parent);
            var center=new Vector3(-13.5f,0,-23.5f);
            for(int i=0;i<8;i++)
            {
                float angle=(210+i*15)*Mathf.Deg2Rad;
                var at=center+new Vector3(Mathf.Cos(angle)*2,.61f,Mathf.Sin(angle)*2);
                at.y+=ReferenceHeight(at.x,at.z);
                var rotation=Quaternion.Euler(0,90-angle*Mathf.Rad2Deg,0);
                SampleStone(bench,"圆磨石凳座",at,new Vector3(.53f,.17f,.58f),1901+i,limestone,rotation);
                if(i%2==0)
                    SampleStone(bench,"嵌土石凳脚",at-Vector3.up*.33f,new Vector3(.30f,.52f,.40f),2001+i,limestone,rotation);
                var solid=Group("长凳通行边界",bench);solid.gameObject.layer=ObstacleLayer;
                solid.localPosition=at-Vector3.up*.28f;solid.localRotation=rotation;
                solid.gameObject.AddComponent<BoxCollider>().size=new Vector3(.53f,.74f,.57f);
            }
        }

        void BuildReferenceCaveFurniture()
        {
            var root=Group("潮洞休憩角 · 弧形石凳和柱灯",details);
            BuildReferencePlungePoolLip(root);
            var center=new Vector3(6,ReferenceGrotto.y,-22.8f);
            for(int i=0;i<10;i++)
            {
                float a=(36+i*12)*Mathf.Deg2Rad;
                var at=center+new Vector3(Mathf.Cos(a)*2.35f,.62f,Mathf.Sin(a)*2.35f);
                SampleStone(root,"弧形石凳座板",at,new Vector3(.53f,.18f,.66f),601+i,limestone,Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0));
                var support=Group("石凳实体边界",root);support.gameObject.layer=ObstacleLayer;
                support.localPosition=at-Vector3.up*.30f;support.localRotation=Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0);
                var collider=support.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.52f,.76f,.61f);
                if(i%3==0)
                    SampleStone(root,"石凳短腿",at-Vector3.up*.34f,new Vector3(.34f,.52f,.45f),811+i,limestone,Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0));
            }
            foreach(var point in new[]{new Vector3(-1.8f,ReferenceGrotto.y,-22.6f),new Vector3(7.7f,ReferenceGrotto.y,-23.3f)})
            {
                var lamp=Group("潮洞琥珀柱灯",root);lamp.localPosition=point;
                Cylinder(lamp,"石柱底盘",new Vector3(0,.10f,0),new Vector3(.86f,.10f,.86f),limestone);
                Cylinder(lamp,"石柱柱身",new Vector3(0,.48f,0),new Vector3(.54f,.30f,.54f),limestone);
                Cylinder(lamp,"石柱托盘",new Vector3(0,.84f,0),new Vector3(.77f,.09f,.77f),limestone);
                Cylinder(lamp,"铜灯底座",new Vector3(0,.96f,0),new Vector3(.43f,.045f,.43f),brass);
                Piece(PrimitiveType.Sphere,lamp,"暖琥珀灯芯",new Vector3(0,1.25f,0),new Vector3(.32f,.62f,.32f),warmGlass);
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3;
                    var foot=new Vector3(Mathf.Cos(a)*.17f,.99f,Mathf.Sin(a)*.17f);
                    var shoulder=new Vector3(Mathf.Cos(a)*.23f,1.31f,Mathf.Sin(a)*.23f);
                    var tip=new Vector3(0,1.64f,0);
                    Beam(lamp,"琥珀灯铜肋下",foot,shoulder,.026f,brass);
                    Beam(lamp,"琥珀灯铜肋上",shoulder,tip,.026f,brass);
                }
                var glow=Group("柱灯暖光",lamp);glow.localPosition=new Vector3(0,1.28f,0);
                var light=glow.gameObject.AddComponent<Light>();light.type=LightType.Point;
                light.color=new Color(1,.55f,.19f);light.range=3.2f;light.intensity=1.25f;light.shadows=LightShadows.None;nightLamps.Add(light);
                ReferenceObstacle(lamp,new Vector3(0,.72f,0),new Vector3(.65f,1.45f,.65f));
            }
        }

        void BuildReferencePlungePoolLip(Transform root)
        {
            var stone=Paint("跌水潭边 · 湿灰卵石",new Color(.48f,.54f,.50f),.18f,.14f);
            int placed=0;
            for(int i=0;i<22;i++)
            {
                float a=i*Mathf.PI*2/22;
                // An actual opening faces the downstream outlet; the pond is not
                // sealed off by a complete ornamental ring.
                if(Mathf.Sin(a)<-.64f)continue;
                var p=new Vector3(.4f+Mathf.Cos(a)*1.86f,0,-25.55f+Mathf.Sin(a)*1.50f);
                float h=ReferenceHeight(p.x,p.z);
                if(h<ReferenceSeaLevel-.03f || h>ReferenceGrotto.y+.10f)continue;
                p.y=h+.035f;
                SampleStone(root,"跌水潭 · 沿岸嵌石",p,new Vector3(.49f,.18f,.37f),2811+i,stone,
                    Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0));
                placed++;
            }
            Debug.Log($"Reference plunge pool: {placed} grounded rim stones; basin and outlet share actual terrain collision.");
        }
    }
}

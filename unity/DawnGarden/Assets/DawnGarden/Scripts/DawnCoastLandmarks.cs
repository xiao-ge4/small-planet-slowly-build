using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        void BuildWorkshop()
        {
            var district = Group("03 · 共创工坊",details);
            var shop = Group("红陶瓦共创工坊",district);
            shop.localPosition = new Vector3(31,.12f,-6.7f);
            shop.localRotation = Quaternion.Euler(0,-12,0);
            Box(shop,"工坊木地板",new Vector3(0,.02f,0),new Vector3(8.3f,.10f,5.8f),wood);
            Box(shop,"工坊后墙",new Vector3(0,1.6f,-2.6f),new Vector3(8.05f,3.2f,.28f),darkWood,ObstacleLayer);
            Box(shop,"工坊右墙",new Vector3(4.02f,1.6f,-.32f),new Vector3(.31f,3.2f,4.55f),wood,ObstacleLayer);
            foreach (int side in new[]{-1,1})
            {
                for (int i=0;i<7;i++)
                {
                    var x = side*(.31f+i*.63f);
                    var y = 4.34f - Mathf.Abs(x)*.30f;
                    var strip = Box(shop,"蜂蜜红色瓦槽",new Vector3(x,y,0),new Vector3(.68f,.15f,6.65f),i%4==1?brick:roof);
                    strip.transform.localRotation=Quaternion.Euler(0,0,-side*17);
                    for (int t=0;t<11;t++)
                        Cylinder(shop,"陶瓦梳",new Vector3(x,y+.085f,(t-5)*.56f),new Vector3(.20f,.32f,.18f),roof)
                            .transform.localRotation=Quaternion.Euler(90,0,0);
                }
                foreach(float z in new[]{-2.58f,2.46f})
                {
                    Box(shop,"承重木柱",new Vector3(side*3.94f,1.63f,z),new Vector3(.29f,3.26f,.32f),darkWood,ObstacleLayer);
                    Box(shop,"石柱脚",new Vector3(side*3.94f,.19f,z),new Vector3(.52f,.38f,.52f),limestone);
                    Beam(shop,"柱角木撑",new Vector3(side*3.94f,2.56f,z),new Vector3(side*3.2f,3.22f,z),.17f,wood);
                }
            }
            Beam(shop,"正面过梁",new Vector3(-4.03f,3.20f,2.48f),new Vector3(4.03f,3.20f,2.48f),.28f,darkWood);
            Box(shop,"工坊工作台面",new Vector3(-1.42f,.98f,.2f),new Vector3(4.6f,.17f,1.45f),wood);
            foreach(float x in new[]{-3.28f,.47f})
                Box(shop,"台腿",new Vector3(x,.46f,.2f),new Vector3(.22f,.92f,1.17f),darkWood,ObstacleLayer);
            for (int row=0;row<3;row++)
            {
                var y = .60f + row*.75f;
                Box(shop,"靠墙陈列搁架",new Vector3(0,y,-2.15f),new Vector3(6.1f,.11f,.66f),wood);
                for (int k=0;k<6;k++)
                {
                    var x = (k-2.5f)*.91f;
                    var sample = Box(shop,"工艺样品",new Vector3(x,y+.19f,-2.13f),new Vector3(.24f+k*.035f,.22f+row*.035f,.42f),((row+k)%3) switch {0=>brass,1=>limestone,_=>azure});
                    sample.transform.localRotation=Quaternion.Euler(0,k*31,0);
                }
            }
            Lantern(district,new Vector3(25.9f,.12f,-1.35f),false);
            Lantern(district,new Vector3(34.4f,.12f,-1.8f),false);
            for(int i=0;i<4;i++)
                Cylinder(district,"材料木桶",new Vector3(31.6f+i*.75f,.53f,-.05f),new Vector3(.66f,.4f,.66f),i%2==0?wood:darkWood,ObstacleLayer);
            TileBorder(district,new Vector3(19.6f,.12f,-9.5f),new Vector3(22.8f,.12f,-12.05f),.65f);
            TileBorder(district,new Vector3(20.7f,2,-23.4f),new Vector3(25f,2,-25.4f),.81f);
            TileBorder(district,new Vector3(29.6f,2,-25.65f),new Vector3(35.1f,2,-23.5f),.81f);
            for (int i=0;i<9;i++)
            {
                var p = new Vector3(18.5f+i*1.45f,.14f,4.04f+Mathf.Sin(i*.61f));
                if (i%3==0) Asset("moss-boulder",district,p,.68f,i*36);
                else Asset("meadow-flower",district,p,.58f,i*36);
            }
        }

        void Lantern(Transform parent,Vector3 point,bool cave)
        {
            var node=Group("暖色引路灯",parent);node.localPosition=point;
            Cylinder(node,"灯座",new Vector3(0,.1f,0),new Vector3(.42f,.10f,.42f),slate);
            Cylinder(node,"瓶灯",new Vector3(0,.54f,0),new Vector3(.27f,.18f,.27f),warmGlass);
            Cylinder(node,"黄铜灯帽",new Vector3(0,.75f,0),new Vector3(.32f,.038f,.32f),brass);
            Cylinder(node,"支柱",new Vector3(0,.25f,0),new Vector3(.13f,.22f,.13f),brass);
            var light=Group("漫反射暖光",node).gameObject.AddComponent<Light>();
            light.transform.localPosition=new Vector3(0,.80f,.12f);
            light.type=LightType.Point;light.range=cave?4.6f:3.1f;
            light.color=new Color(1f,.58f,.27f);light.intensity=.4f;
            nightLamps.Add(light);
        }

        void BuildFerry()
        {
            var district=Group("05 · 同星球渡口",details);
            var deck=Group("渡口木栈桥",district);
            deck.localPosition=new Vector3(48,-.17f,13);
            Box(deck,"防滑步行层",new Vector3(0,-.11f,0),new Vector3(7.7f,.22f,3.7f),wood,FloorLayer);
            for (int i=0;i<18;i++)
            {
                var x=(i-8.5f)*.42f;
                Box(deck,"栈桥木板",new Vector3(x,.021f,0),new Vector3(.403f,.06f,3.75f),i%4==1?darkWood:wood);
            }
            for (int i=0;i<6;i++)
            foreach(float z in new[]{-1.46f,1.46f})
                Cylinder(deck,"水中桩与系缆柱",new Vector3(i*1.32f-3.3f,-.18f,z),new Vector3(.31f,1.13f,.31f),darkWood,ObstacleLayer);
            Rail(deck,new Vector3(-2.65f,0,1.68f),new Vector3(3.48f,0,1.68f),1.1f,darkWood);
            Lantern(deck,new Vector3(-2.5f,0,-1.34f),false);
            var boat=Group("系泊的木渡船",district);
            boat.localPosition=new Vector3(49,-.86f,9.06f);
            boat.localRotation=Quaternion.Euler(0,-63,0);
            var rim=new List<Vector3>();
            var vertices=new List<Vector3>();
            var faces=new List<int>();
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8;
                var top=new Vector3(Mathf.Sin(a)*1.12f*Mathf.Pow(Mathf.Abs(Mathf.Sin(a)),.16f),.46f+.23f*Mathf.Abs(Mathf.Cos(a)),Mathf.Cos(a)*2.65f);
                var bottom=new Vector3(top.x*.61f,-.21f,top.z*.85f);
                rim.Add(top);vertices.Add(top);vertices.Add(bottom);
            }
            for(int i=0;i<16;i++)
            {
                int a=i*2,b=(i+1)%16*2;
                faces.AddRange(new[]{a,b,a+1,a+1,b,b+1,a,a+1,b,a+1,b+1,b});
                Beam(boat,"木舷",rim[i],rim[(i+1)%16],.11f,wood);
            }
            MeshObject("木渡船 · 船壳",boat,vertices,faces,darkWood);
            for(int i=-2;i<=2;i++)
                Box(boat,"渡船肋板",new Vector3(0,.24f,i*.46f),new Vector3(1.52f,.085f,.42f),wood);
            foreach (int sign in new[]{-1,1})
                Box(boat,"渡船横板凳",new Vector3(0,.45f,sign*.74f),new Vector3(1.79f,.10f,.46f),wood);
            Beam(district,"系泊缆绳",new Vector3(49.1f,.36f,11.6f),new Vector3(50.1f,-.25f,9.9f),.052f,sandstone);
        }

        void BuildStars()
        {
            var view=Group("06 · 跨星球星图台",details);
            var baseAt=new Vector3(31,5.64f,-36.3f);
            Cylinder(view,"星图蓝釉台面",baseAt,new Vector3(13.1f,.048f,13.1f),azure);
            Cylinder(view,"黄铜环刻边",baseAt+Vector3.up*.054f,new Vector3(11.1f,.008f,11.1f),brass);
            Cylinder(view,"星图釉面中心",baseAt+Vector3.up*.072f,new Vector3(10.92f,.014f,10.92f),azure);
            var instrument=Group("黄铜浑仪",view);instrument.localPosition=baseAt+new Vector3(0,0,-1.5f);
            Cylinder(instrument,"浑仪基座",new Vector3(0,.24f,0),new Vector3(2.55f,.24f,2.55f),limestone,ObstacleLayer);
            Cylinder(instrument,"深蓝基柱",new Vector3(0,.73f,0),new Vector3(1.46f,.31f,1.46f),slate,ObstacleLayer);
            var pivot = new Vector3(0,2.3f,0);
            Piece(PrimitiveType.Sphere,instrument,"浑仪中央星体",pivot,new Vector3(.92f,.92f,.92f),brass);
            Circle(instrument,pivot,1.53f,Quaternion.Euler(13,24,-13),.105f,brass);
            Circle(instrument,pivot,1.61f,Quaternion.Euler(55,60,30),.085f,brass);
            Circle(instrument,pivot,1.66f,Quaternion.Euler(90,0,0),.068f,brass);
            for(int i=0;i<14;i++)
            {
                float a=i*Mathf.PI*2/14;
                var p=baseAt+new Vector3(Mathf.Cos(a)*5.21f,.105f,Mathf.Sin(a)*5.21f);
                if(i<3||i>=11||i==3)Box(view,"星图护栏",p+Vector3.up*.34f,new Vector3(.36f,.75f,.37f),limestone,ObstacleLayer);
                Cylinder(view,"方位铜嵌",p,new Vector3(.23f,.009f,.23f),brass);
            }
            var scope=Group("观星望远镜",view);scope.localPosition=baseAt+new Vector3(4.15f,0,-1.05f);
            for(int i=0;i<3;i++)
            {
                var foot=Quaternion.Euler(0,i*120,0)*Vector3.forward*.66f;
                Beam(scope,"望远镜撑脚",foot,new Vector3(0,1.23f,0),.105f,darkWood,ObstacleLayer);
            }
            Vector3 a1=new(0,1.34f,0),b1=new(1.19f,1.98f,.20f);
            var tube=Cylinder(scope,"黄铜探星镜",(a1+b1)*.5f,new Vector3(.42f,(a1-b1).magnitude*.5f,.42f),brass);
            tube.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b1-a1);
            Piece(PrimitiveType.Sphere,scope,"蓝玻璃透镜",b1,new Vector3(.35f,.33f,.32f),azure);
        }

        void Circle(Transform parent, Vector3 pivot,float radius,Quaternion rotation,float width,Material mat)
        {
            const int count=32;
            for(int i=0;i<count;i++)
            {
                float start=i*Mathf.PI*2/count, end=(i+1)*Mathf.PI*2/count;
                var a=rotation*new Vector3(Mathf.Cos(start)*radius,Mathf.Sin(start)*radius,0);
                var b=rotation*new Vector3(Mathf.Cos(end)*radius,Mathf.Sin(end)*radius,0);
                Beam(parent,"浑仪弧线",pivot+a,pivot+b,width,mat);
            }
        }

    }
}

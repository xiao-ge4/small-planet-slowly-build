using System.Collections.Generic;
using UnityEngine;
namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        void BuildReferenceNurseryBeds(Transform root)
        {
            var crops=new Dictionary<Material,NurseryGeometry>();
            var fronts=new[]{-13.4f,-8.6f,-3.25f};
            var rows=new[]{-11f,-6f,-.65f};
            // Every new frame remains inside its former 5.3 x 3.65 m obstacle.
            // Shorter, outward-offset beds break the repeated six-box silhouette
            // without taking width from the surveyed aisle or fixed irrigation.
            var sizes=new[]{new Vector2(4.6f,3.2f),new(4.5f,3.4f),new(5.3f,3.60f),
                new(4.3f,3.1f),new(4.4f,3.15f),new(4.8f,3.3f)};
            var offsets=new[]{new Vector2(-.35f,.10f),new(.40f,.08f),new(0,0),
                new(.50f,.20f),new(-.45f,.18f),new(.25f,.10f)};
            var varieties=new[]{"金盏花","白雏菊","卷叶生菜","番茄灌丛","鼠尾草花穗","珊瑚百日菊"};
            // Each bed has its own pair of interlocking plant communities. Bare
            // soil crescents make the beds look tended and keep their edges legible.
            var companions=new[]{1,4,0,4,1,0};
            var hearts=new[]{new Vector2(-.24f,.17f),new(.24f,.08f),new(-.22f,.15f),
                new(.20f,.22f),new(-.18f,.08f),new(.17f,.18f)};
            int cropCount=0,highCount=0;
            for(int tier=0;tier<3;tier++)for(int side=0;side<2;side++)
            {
                int variety=tier*2+side;
                float elevation=ReferenceNurseryLevels[tier];
                var p=new Vector3((side==0?-13.9f:-6.9f)+offsets[variety].x,elevation,
                    rows[tier]+(side==1?.34f:0)+offsets[variety].y);
                float width=sizes[variety].x,depth=sizes[variety].y;
                var bed=Group("苗圃大畦 · "+varieties[variety],root);bed.localPosition=p;
                ReferenceObstacle(bed,new Vector3(0,.31f,0),new Vector3(width+.10f,.62f,depth+.10f));
                SampleStone(bed,"厚实种植土",new Vector3(0,.30f,0),new Vector3(width,.38f,depth),tier*31+side,soil);
                foreach(int sign in new[]{-1,1})
                {
                    SampleRetaining(bed,new Vector3(-width/2,0,sign*depth/2),new Vector3(width/2,0,sign*depth/2),.30f,2,limestone,false);
                    SampleRetaining(bed,new Vector3(sign*width/2,0,-depth/2),new Vector3(sign*width/2,0,depth/2),.30f,2,limestone,false);
                }
                int columns=Mathf.CeilToInt((width-.88f)/.46f),plantRows=Mathf.CeilToInt((depth-.86f)/.44f);
                for(int row=0;row<plantRows;row++)for(int col=0;col<columns;col++)
                {
                    int seed=variety*149+row*17+col*31;
                    float x=Mathf.Lerp(-width*.5f+.43f,width*.5f-.43f,(col+.5f)/columns);
                    float z=Mathf.Lerp(-depth*.5f+.42f,depth*.5f-.42f,(row+.5f)/plantRows);
                    x+=.13f*Mathf.Sin(seed*1.79f)+(row%2==0?.045f:-.045f);
                    z+=.12f*Mathf.Sin(seed*2.37f+1.1f);
                    x=Mathf.Clamp(x,-width*.5f+.38f,width*.5f-.38f);
                    z=Mathf.Clamp(z,-depth*.5f+.37f,depth*.5f-.37f);
                    // This small pocket is occupied by the existing waterable
                    // seedling, including its growth stages and real collider.
                    if(new Vector2(p.x+x-ReferenceSeed.x,p.z+z-ReferenceSeed.z).sqrMagnitude<.72f*.72f)continue;
                    if(new Vector2(p.x+x-ReferenceNurseryApproach.x,p.z+z-ReferenceNurseryApproach.z).sqrMagnitude<1.08f*1.08f)continue;
                    float u=x/(width*.5f),v=z/(depth*.5f);
                    float edge=Mathf.Max(Mathf.Abs(u),Mathf.Abs(v));
                    float sweep=u*(side==0?1:-1)+v*.43f;
                    // Open one corner and one short working pocket per bed; the
                    // gaps are broad enough to read between the overlapping leaves.
                    if((sweep>.39f&&v<.22f)||(u<-.45f&&v>.48f&&variety%2==0))continue;
                    float heart=Vector2.Distance(new Vector2(u,v),hearts[variety]);
                    float companionField=u*(side==0?1:-1)-v*.35f+.12f*Mathf.Sin(v*5+variety);
                    int species=companionField>.18f?companions[variety]:variety;
                    if(edge>.65f&&companionField<-.12f&&seed%3!=0)species=2;
                    // The lettuce bed keeps a broad green centre, with gold and
                    // sage concentrated in one rear corner rather than confetti.
                    if(variety==2&&v>.28f&&u<-.12f)species=u<-.43f?4:0;
                    bool tall=species!=2&&heart<.48f&&edge<.62f;
                    float variation=.5f+.5f*Mathf.Sin(seed*.93f);
                    float height=tall?Mathf.Lerp(.86f,1.14f,variation):edge>.58f||species==2?
                        Mathf.Lerp(.22f,.36f,variation):Mathf.Lerp(.43f,.69f,variation);
                    ReferenceNurseryCrop(crops,p+new Vector3(x,.49f,z),species,height,seed);
                    cropCount++;if(tall)highCount++;
                }
                if(side==0)
                {
                    float waterY=elevation+.19f;
                    ReferenceSupportedTrough(root,.44f,new Vector3(-17.1f,waterY,fronts[tier]),
                        new Vector3(-4.4f,waterY-.045f,fronts[tier]),
                        new Vector3(tier==2?-3.8f:tier==1?-2.2f:-.4f,tier==2?3.37f:tier==1?2.47f:2.15f,fronts[tier]));
                }
            }
            PublishReferenceNurseryCrops(root,crops);
            Debug.Log($"Reference nursery canopy: {cropCount} mixed crops across 6 distinct beds; {highCount} tall interior accents, low edges and open soil crescents; combined into {crops.Count} material meshes; bed obstacles stay within the original footprints.");
            for(int tier=0;tier<3;tier++)ReferenceCanalCrossing(root,-10.4f,fronts[tier],ReferenceNurseryLevels[tier]+.35f);
            ReferenceSupportedTrough(root,.42f,new Vector3(-17.1f,5.40f,4.0f),
                new Vector3(-17.1f,4.44f,-3.25f),new Vector3(-17.1f,3.64f,-8.6f),new Vector3(-17.1f,2.74f,-13.4f));
            SampleStone(root,"玩家幼苗的独立育苗土",ReferenceSeed+new Vector3(0,.2f,0),new Vector3(.82f,.4f,.82f),671,soil);
        }

        void PublishReferenceNurseryCrops(Transform root,Dictionary<Material,NurseryGeometry> crops)
        {
            foreach(var layer in crops)
            {
                if(layer.Value.Points.Count==0)continue;
                var mesh=new Mesh{name="连片蔬果与花苗 · "+layer.Key.name,
                    indexFormat=layer.Value.Points.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
                mesh.SetVertices(layer.Value.Points);mesh.SetTriangles(layer.Value.Faces,0);
                var uv=new List<Vector2>(layer.Value.Points.Count);
                foreach(var p in layer.Value.Points)uv.Add(new Vector2(p.x*.34f+p.z*.18f,p.z*.34f+p.y*.38f));
                mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                var obj=Group(mesh.name,root).gameObject;
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=layer.Key;
            }
        }

        void ReferenceNurseryCrop(Dictionary<Material,NurseryGeometry> batches,Vector3 root,int variety,float height,int seed)
        {
            MakeNurseryMaterials();
            var lightLeaves=PlantMaterial(batches,herbMoss);
            var shadeLeaves=PlantMaterial(batches,herbFern);
            var stems=PlantMaterial(batches,herbStem);
            float heading=Mathf.Repeat(seed*137.51f,360);
            float variation=.93f+.10f*Mathf.Sin(seed*1.31f);
            float canopyScale=Mathf.Clamp(.60f+height*.46f,.71f,1.08f);
            // The old leaf half-width was about .12m. These .20–.22m cupped
            // blades form a continuous canopy rather than separated green stars.
            int outerCount=variety==2?9:7;
            for(int leaf=0;leaf<outerCount;leaf++)
            {
                var material=(leaf+seed)%3==0?shadeLeaves:lightLeaves;
                float width=(variety==2?.22f:.205f)*variation*canopyScale;
                float length=(variety==4?.43f:.45f)*variation*canopyScale;
                ReferenceRoundedLeaf(material,root+Vector3.up*(.016f*(leaf%3)),heading+leaf*360f/outerCount,
                    variety==2?.29f:.24f,length,width,variety==2?.12f:.085f);
            }
            for(int leaf=0;leaf<(variety==2?6:4);leaf++)
                ReferenceRoundedLeaf(leaf%2==0?lightLeaves:shadeLeaves,root+Vector3.up*.065f,heading+leaf*137.5f,
                    variety==2?.72f:.52f,(variety==2?.30f:.27f)*variation*canopyScale,(variety==2?.155f:.14f)*variation*canopyScale,.085f);
            if(variety==2)return;

            int branches=variety==3?3:variety==4?2:variety==1?2:3;
            for(int branch=0;branch<branches;branch++)
            {
                float azimuth=heading+branch*137.5f;
                var direction=Quaternion.Euler(0,azimuth,0)*Vector3.forward;
                float branchHeight=height*(branch==0?1:.80f+.10f*Mathf.Sin(seed+branch));
                var top=root+direction*(.11f+.035f*branch)+Vector3.up*branchHeight;
                BloomStem(stems,root+Vector3.up*.06f,top,.018f);
                for(int leaf=0;leaf<(variety==3?4:2);leaf++)
                {
                    float t=.31f+leaf*.14f;
                    ReferenceRoundedLeaf((leaf+branch)%2==0?lightLeaves:shadeLeaves,Vector3.Lerp(root,top,t),
                        azimuth+leaf*165,.40f,variety==3?.29f:.22f,variety==3?.13f:.10f,.045f);
                }
                if(variety==3)
                {
                    var fruit=PlantMaterial(batches,herbCoral);
                    for(int berry=0;berry<3;berry++)
                    {
                        var center=top+Quaternion.Euler(0,berry*120+seed,0)*Vector3.forward*.075f-Vector3.up*(.035f+berry*.033f);
                        ReferenceNurseryBerry(fruit,center,.067f+.011f*(berry%2));
                        for(int leaf=0;leaf<4;leaf++)BloomBlade(shadeLeaves,center+Vector3.up*.067f,leaf*90+seed,.20f,.075f,.023f,.012f);
                    }
                    continue;
                }
                var petals=PlantMaterial(batches,variety==0?herbGold:variety==1?herbCream:variety==4?herbBlue:herbCoral);
                var middle=PlantMaterial(batches,herbGold);
                if(variety==4)
                {
                    // Stacked paired florets create a recognisable sage spike,
                    // rather than recolouring the same radial daisy used elsewhere.
                    for(int whorl=0;whorl<5;whorl++)
                    {
                        var center=top-Vector3.up*(.19f-whorl*.041f);
                        float length=.10f*(1-whorl*.105f);
                        for(int petal=0;petal<5;petal++)
                            BloomBlade(petals,center,azimuth+petal*72+whorl*23,.52f,length,.045f,.025f);
                    }
                    continue;
                }
                int petalCount=variety==1?10:8;
                for(int petal=0;petal<petalCount;petal++)
                    BloomBlade(petals,top,azimuth+petal*360f/petalCount,.10f,
                        variety==1?.155f:.14f,variety==1?.054f:.071f,.045f);
                if(variety==0||variety==5)
                    for(int petal=0;petal<6;petal++)
                        BloomBlade(petals,top+Vector3.up*.025f,azimuth+petal*60+23,.42f,.105f,.057f,.045f);
                ReferenceNurseryBerry(middle,top+Vector3.up*.025f,variety==1?.053f:.038f);
            }
        }

        static void ReferenceNurseryBerry(NurseryGeometry batch,Vector3 center,float radius)
        {
            const int around=8,vertical=5;
            int first=batch.Points.Count;
            for(int ring=0;ring<=vertical;ring++)for(int side=0;side<around;side++)
            {
                float latitude=ring*Mathf.PI/vertical,longitude=side*Mathf.PI*2/around;
                batch.Points.Add(center+new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),
                    Mathf.Cos(latitude)*.86f,Mathf.Sin(latitude)*Mathf.Sin(longitude))*radius);
                if(ring==0)continue;
                int a=first+(ring-1)*around+side,b=first+(ring-1)*around+(side+1)%around;
                int c=first+ring*around+side,d=first+ring*around+(side+1)%around;
                batch.Faces.AddRange(new[]{a,b,c,b,d,c});
            }
        }
        void ReferenceSupportedTrough(Transform parent,float width,params Vector3[] profile)
        {
            SampleTrough(parent,width,profile);
            for(int i=0;i<profile.Length-1;i++)
            {
                var a=profile[i];var b=profile[i+1];var delta=b-a;
                int count=Mathf.CeilToInt(new Vector2(delta.x,delta.z).magnitude/.82f);
                for(int n=0;n<count;n++)
                {
                    var p=Vector3.Lerp(a,b,(n+.5f)/count);float foot=ReferenceHeight(p.x,p.z)-.08f;
                    float cap=p.y-.17f, height=cap-foot;if(height<.07f)continue;
                    // Masonry rests on the terrain instead of a floating aqueduct plate.
                    p.y=(cap+foot)*.5f;
                    SampleStone(parent,"水渠落地基座",p,new Vector3(width+.22f,height,delta.magnitude/count+.035f),i*79+n,cliff,Quaternion.LookRotation(HorizontalDirection(delta)));
                }
            }
        }
        void ReferenceCanalCrossing(Transform parent,float x,float z,float top)
        {
            const float halfWidth=.75f;
            var knots=new[]{new Vector3(x,ReferenceHeight(x,z-1.65f)+.025f,z-1.65f),new Vector3(x,top,z-.60f),
                new Vector3(x,top,z+.60f),new Vector3(x,ReferenceHeight(x,z+1.65f)+.025f,z+1.65f)};
            var v=new List<Vector3>();var f=new List<int>();
            for(int i=0;i<knots.Length;i++)
            {
                v.Add(knots[i]-Vector3.right*halfWidth);v.Add(knots[i]+Vector3.right*halfWidth);
                if(i>0){int n=i*2;f.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}
            }
            MeshObject("苗圃通道跨渠石桥",ground,v,f,limestone,true);
            for(int i=0;i<4;i++)SampleStone(parent,"跨渠桥面石",new Vector3(x+(i-1.5f)*.34f,top-.065f,z),new Vector3(.33f,.13f,1.23f),241+i,limestone);
        }
    }
}

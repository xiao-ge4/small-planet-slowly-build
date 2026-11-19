using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        static readonly Vector2[] ReferenceRearCoast =
        {
            new(-103,26),new(-81,16),new(-62,14),new(-46,16),new(-34,19),
            new(-27,24),new(-29,32),new(-23,39),new(-24,49),new(-20,58),
            new(-29,75),new(-44,91),new(-66,103),new(-88,93),new(-106,71),new(-111,45)
        };
        // x/z, height above the low land datum, half-width. These surveyed spines
        // make unequal, short rock shoulders with real saddles between them.
        // The previous height field remains a strict upper envelope below.
        static readonly Vector4[][] ReferenceBackdropSpines =
        {
            new[]{new Vector4(-60,32,3.8f,6),new(-51,36,6.7f,7),new(-43,39,10,7.5f),new(-35,43,5.8f,5.8f),new(-31,51,2.9f,4.5f)},
            new[]{new Vector4(-88,48,3.2f,7),new(-77,53,7.3f,8),new(-65,55,12,9),new(-55,61,7.1f,7.4f),new(-46,63,3.6f,5)},
            new[]{new Vector4(-103,73,3.5f,7),new(-94,73,6.4f,8),new(-87,76,12,8.5f),new(-76,83,6.5f,7),new(-64,85,2.8f,6)},
            new[]{new Vector4(-45,57,3.8f,5),new(-36,67,9,6),new(-31,76,3.6f,4.8f)},
            new[]{new Vector4(-121,142,2.2f,7),new(-108,146,7,8),new(-96,149,3.4f,6),new(-79,151,8,8.5f),new(-66,145,3.6f,7),new(-45,139,6,7),new(-32,138,1.4f,5)},
            new[]{new Vector4(-39,196,1.3f,5),new(-28,199,5,6),new(-19,206,2.4f,5),new(-7,210,6,6.5f),new(3,207,2.4f,4.6f),new(13,202,4,5),new(22,201,1.1f,4)},
            new[]{new Vector4(70,152,1,1.6f),new(76,151,3.1f,2.5f),new(79,150,1.3f,1.8f),new(84,149,2.3f,2),new(89,149.8f,.7f,1.3f)}
        };

        // These are real background land masses, in the same world frame as the home.
        // There are no billboards, screen-space images or collision/navigation changes.
        void BuildReferenceBackdrop()
        {
            var root=Group("参考地图 · 远岸与群山",details);
            BuildReferenceBackdropLand(root,"左后连续岩岸",ReferenceRearCoast,0,.70f);
            BuildReferenceBackdropLand(root,"左远层叠山脊",ReferenceBackdropIsland(new(-75,143),new(55,22),19),1,.85f);
            BuildReferenceBackdropLand(root,"中央淡蓝远峰",ReferenceBackdropIsland(new(-8,205),new(33,19),71),2,.90f);
            BuildReferenceBackdropLand(root,"右后独立小岛",ReferenceBackdropIsland(new(79,150),new(11,5.5f),31),3,.42f);

            // Short bedrock shoulders sit inside the actual dry coast. Previously the
            // front rows were outside its outline and some tree roots lay under water.
            var rockBounds=new List<Bounds>();
            var crags=new[]{
                new Vector3(-35,1.10f,31),new Vector3(-40,1.30f,29),new Vector3(-47,1.05f,33),
                new Vector3(-32,.80f,42),new Vector3(-40,1.25f,46),new Vector3(-51,1.10f,43),
                new Vector3(-57,1.10f,36),new Vector3(-63,.90f,49),new Vector3(-76,1.0f,64)
            };
            for(int i=0;i<crags.Length;i++)
            {
                var p=crags[i];float y=ReferenceBackdropHeight(new Vector2(p.x,p.z),ReferenceRearCoast,0);
                var rock=ReferenceAsset("Geology/coast_crag",new Vector3(p.x,y-p.y*.56f,p.z),i*47+23);
                if(rock==null)continue;rock.transform.SetParent(root,true);
                rock.transform.localScale=new Vector3(p.y*1.25f,p.y*.88f,p.y);
                SetReferenceRockMap(rock,"coast_crag");DisableReferenceBackdropShadows(rock);
                foreach(var renderer in rock.GetComponentsInChildren<Renderer>())
                {var bounds=renderer.bounds;bounds.Expand(new Vector3(1.2f,1,1.2f));rockBounds.Add(bounds);}
            }
            // Wide, shallow bedding is partially buried in the coast, never stacked
            // into pillars. It breaks the formerly featureless green left silhouette
            // into connected rock shoulders without increasing its height envelope.
            var beds=new[]{new Vector2(-31,29),new Vector2(-35,29),new Vector2(-40,27),new Vector2(-46,26),
                new Vector2(-50,31),new Vector2(-31,39),new Vector2(-33,44),new Vector2(-39,45),
                new Vector2(-44,49),new Vector2(-49,48),new Vector2(-55,44),new Vector2(-57,52),
                new Vector2(-65,61),new Vector2(-72,62),new Vector2(-77,73),new Vector2(-86,77)};
            for(int i=0;i<beds.Length;i++)
            {
                var p=beds[i];float y=ReferenceBackdropHeight(p,ReferenceRearCoast,0);
                if(ReferenceBackdropDistance(p,ReferenceRearCoast)<2.2f)continue;
                float scale=.56f+.08f*(i%3);
                var bed=ReferenceAsset("Geology/cliff_bedding",new Vector3(p.x,y-.95f,p.y),154+(i%5)*13);
                if(bed==null)continue;bed.transform.SetParent(root,true);
                bed.transform.localScale=new Vector3(scale,.30f+.035f*(i%3),.62f);
                SetReferenceRockMap(bed,"cliff_bedding");DisableReferenceBackdropShadows(bed);
                foreach(var renderer in bed.GetComponentsInChildren<Renderer>())
                {var bounds=renderer.bounds;bounds.Expand(new Vector3(.6f,.5f,.6f));rockBounds.Add(bounds);}
            }
            var trees=new[]{new Vector2(-32,34),new Vector2(-36,36),new Vector2(-39,34),new Vector2(-44,38),
                new Vector2(-48,36),new Vector2(-54,40),new Vector2(-32,48),new Vector2(-36,51),
                new Vector2(-41,52),new Vector2(-48,54),new Vector2(-52,57),new Vector2(-60,60),
                new Vector2(-64,66),new Vector2(-70,69),new Vector2(-74,71),new Vector2(-80,75),new Vector2(-85,80)};
            var planted=new List<Vector2>();
            for(int i=0;i<trees.Length;i++)
            {
                for(int attempt=0;attempt<6;attempt++)
                {
                    var p=trees[i]+new Vector2(-1.35f*attempt,.8f*attempt);
                    float y=ReferenceBackdropHeight(p,ReferenceRearCoast,0);
                    float water=ReferenceSeaLevel+ReferenceOceanOffset(p.x,p.y);
                    if(y<water+.70f||ReferenceBackdropDistance(p,ReferenceRearCoast)<5)continue;
                    var world=root.TransformPoint(new Vector3(p.x,y+.5f,p.y));bool blocked=false;
                    foreach(var bounds in rockBounds)
                        if(world.x>bounds.min.x&&world.x<bounds.max.x&&world.z>bounds.min.z&&world.z<bounds.max.z)blocked=true;
                    foreach(var other in planted)if(Vector2.Distance(other,p)<3.1f)blocked=true;
                    if(blocked)continue;
                    var tree=ReferenceAsset("Buildings/courtyard_tree",new Vector3(p.x,y+.025f,p.y),i*79+21);
                    if(tree==null)break;
                    tree.transform.SetParent(root,true);tree.transform.localScale*=.34f+(i%4)*.055f;
                    DisableReferenceBackdropShadows(tree);planted.Add(p);break;
                }
            }
            // The water surrounds each mesh naturally. Existing atmospheric fog provides
            // the daylight/nighttime colour transition; this layer never changes it globally.
        }

        static void DisableReferenceBackdropShadows(GameObject model)
        {
            foreach(var r in model.GetComponentsInChildren<Renderer>())
            {r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            foreach(var collider in model.GetComponentsInChildren<Collider>())collider.enabled=false;
        }

        static Vector2[] ReferenceBackdropIsland(Vector2 center,Vector2 extent,int seed)
        {
            var points=new Vector2[48];
            for(int i=0;i<points.Length;i++)
            {
                float a=i*Mathf.PI*2/points.Length;
                float r=1+.10f*Mathf.Sin(a*3+seed)+.04f*Mathf.Sin(a*7+seed*.13f);
                points[i]=center+new Vector2(Mathf.Cos(a)*extent.x*r,Mathf.Sin(a)*extent.y*r);
            }
            return points;
        }

        static float ReferenceBackdropDistance(Vector2 p,Vector2[] outline)
        {
            bool inside=false;float nearest=float.PositiveInfinity;
            for(int i=0,j=outline.Length-1;i<outline.Length;j=i++)
            {
                var a=outline[i];var b=outline[j];var line=b-a;
                float t=Mathf.Clamp01(Vector2.Dot(p-a,line)/line.sqrMagnitude);
                nearest=Mathf.Min(nearest,(p-a-line*t).sqrMagnitude);
                if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return Mathf.Sqrt(nearest)*(inside?1:-1);
        }

        static float ReferenceBackdropHeight(Vector2 p,Vector2[] outline,int kind)
        {
            float shore=ReferenceBackdropDistance(p,outline);
            float n=Mathf.PerlinNoise(p.x*.095f+17,p.y*.095f+71);
            float detail=Mathf.PerlinNoise(p.x*.31f+6,p.y*.27f+31);
            float relief;
            if(kind==0)
            {
                relief=Mathf.Max(ReferenceBackdropRidge(p,new(-43,39),new(19,12),9.3f,-.30f),
                    ReferenceBackdropRidge(p,new(-65,55),new(29,17),12.0f,.44f));
                relief=Mathf.Max(relief,ReferenceBackdropRidge(p,new(-87,76),new(26,16),11.6f,-.33f));
                relief=Mathf.Max(relief,ReferenceBackdropRidge(p,new(-36,67),new(16,20),8.0f,.12f));
                relief=.65f+relief+(n-.5f)*.75f;
            }
            else if(kind==1)
            {
                relief=Mathf.Max(ReferenceBackdropRidge(p,new(-108,146),new(23,14),13.5f,-.18f),
                    ReferenceBackdropRidge(p,new(-79,151),new(28,17),17.5f,.13f));
                relief=Mathf.Max(relief,ReferenceBackdropRidge(p,new(-45,139),new(23,14),11.8f,-.25f));
            }
            else if(kind==2)
            {
                relief=Mathf.Max(ReferenceBackdropRidge(p,new(-28,199),new(16,12),10.2f,-.24f),
                    ReferenceBackdropRidge(p,new(-7,210),new(17,13),13.0f,.15f));
                relief=Mathf.Max(relief,ReferenceBackdropRidge(p,new(13,202),new(14,10),8.4f,-.36f));
            }
            else
                relief=Mathf.Max(ReferenceBackdropRidge(p,new(76,151),new(8,4.5f),7.0f,-.25f),
                    ReferenceBackdropRidge(p,new(84,149),new(6,3.7f),4.4f,.32f));
            // Distant relief must stay subordinate to the playable coast. The first
            // lit pass exposed how large the old nominal heights were in the long lens.
            relief*=kind==0?.85f:kind==1?.43f:kind==2?.40f:.38f;
            relief+=(detail-.5f)*(kind==0?.45f:.12f);
            // Erode the former smooth domes into short interlocking spurs. This only
            // cuts the approved silhouette envelope; no peak is lifted into the sky.
            float spine=.48f+.25f*Mathf.Abs(Mathf.Sin(p.x*.33f+p.y*.14f+kind*1.7f))
                +.17f*Mathf.Abs(Mathf.Sin(p.x*.71f-p.y*.19f+2.4f))
                +.10f*Mathf.Abs(Mathf.Sin(p.x*1.13f+p.y*.43f));
            float eroded=relief*Mathf.Lerp(kind==0?.78f:.65f,1,spine);
            float layer=kind==0?.78f:kind==3?.40f:.68f;
            float warped=eroded/layer+.12f*Mathf.Sin(p.x*.61f+p.y*.29f);
            float step=Mathf.Floor(warped),fraction=warped-step;
            float stratified=(step+Ease(.14f,.82f,fraction))*layer;
            relief=Mathf.Min(relief,Mathf.Lerp(eroded,stratified,kind==0?.38f:.27f));
            // Preserve every former maximum, but cut broad saddles and exposed
            // inclined faces into the dome. This is a structural section, not
            // another frequency of noise on an unchanged smooth hill.
            float envelope=relief;
            // Spine heights and the already attenuated envelope must use the
            // same scale. Otherwise min() clips the crests back into old domes.
            // Measured crest cuts rise to 88/84/73/76%, while peak loss stays
            // below 9% and the approved upper envelope is never exceeded.
            float shoulder=ReferenceBackdropShoulders(p,kind)*(kind==0?.82f:kind==1?.82f:kind==2?.80f:.72f);
            relief=Mathf.Min(envelope,Mathf.Max(envelope*.40f,.24f+shoulder));
            // A broad coastal gradient gives low ridges with real horizontal depth.
            // The old short edge blend made a long, sheer wall around every range.
            float edgeWidth=kind==0?11:kind==3?3.5f:9;
            float land=ReferenceSeaLevel+.25f+relief*Ease(0,edgeWidth,shore);
            float oceanOffset=ReferenceOceanOffset(p.x,p.y);
            if(shore<0)return ReferenceSeaLevel-.60f-14.4f*Ease(0,10,-shore)+oceanOffset;
            return Mathf.Lerp(ReferenceSeaLevel-.60f,land,Ease(0,2.3f,shore))+oceanOffset;
        }

        static float ReferenceBackdropRidge(Vector2 p,Vector2 center,Vector2 extent,float height,float angle)
        {
            var q=p-center;float c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            float u=(q.x*c+q.y*s)/extent.x,v=(-q.x*s+q.y*c)/extent.y;
            // Intersecting asymmetric ridges give broad shoulders and real saddles.
            // They replace one smooth, equal-height noise bank across the horizon.
            v+=.13f*Mathf.Sin(u*8.3f+angle*3)+.045f*Mathf.Sin(u*19.7f+1.8f);
            float footprint=Mathf.Pow(Mathf.Abs(u),1.6f)+Mathf.Pow(Mathf.Abs(v),1.22f);
            float crest=Mathf.Pow(Mathf.Clamp01(1-footprint),.78f);
            float worn=.94f+.055f*Mathf.Sin(q.x*.37f+q.y*.21f)+.025f*Mathf.Sin(q.x*.81f-q.y*.35f);
            return height*crest*worn;
        }

        static float ReferenceBackdropShoulders(Vector2 p,int kind)
        {
            int first=kind==0?0:kind+3,last=kind==0?4:first+1;
            float result=0;
            for(int ridge=first;ridge<last;ridge++)
            {
                var spine=ReferenceBackdropSpines[ridge];
                for(int i=1;i<spine.Length;i++)
                {
                    var aa=spine[i-1];var bb=spine[i];
                    var a=new Vector2(aa.x,aa.y);var b=new Vector2(bb.x,bb.y);var segment=b-a;
                    float t=Mathf.Clamp01(Vector2.Dot(p-a,segment)/segment.sqrMagnitude);
                    var q=p-Vector2.Lerp(a,b,t);
                    float width=Mathf.Lerp(aa.w,bb.w,t);
                    // A shorter seaward slope reveals a thick layered face;
                    // the inland shoulder stretches out into the next ridge.
                    float sign=segment.x*q.y-segment.y*q.x;
                    float distance=q.magnitude/(width*(sign<0?.82f:1.15f));
                    float section=distance<.20f?Mathf.Lerp(1,.95f,distance/.20f):
                        distance<.53f?Mathf.Lerp(.95f,.64f,(distance-.20f)/.33f):
                        distance<.69f?Mathf.Lerp(.64f,.60f,(distance-.53f)/.16f):
                        distance<1.15f?Mathf.Lerp(.60f,0,(distance-.69f)/.46f):0;
                    result=Mathf.Max(result,section*Mathf.Lerp(aa.z,bb.z,t));
                }
            }
            return result;
        }

        void BuildReferenceBackdropLand(Transform parent,string title,Vector2[] outline,int kind,float spacing)
        {
            var min=outline[0];var max=outline[0];
            foreach(var p in outline){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            min-=Vector2.one*11;max+=Vector2.one*11;
            int columns=Mathf.CeilToInt((max.x-min.x)/spacing)+1,rows=Mathf.CeilToInt((max.y-min.y)/spacing)+1;
            var positions=new List<Vector3>(columns*rows);var colors=new List<Color>(columns*rows);
            var rocks=new List<float>(columns*rows);
            var mapping=new List<Vector2>(columns*rows);var extra=new List<Vector2>(columns*rows);
            Color grassTint=kind==0?new Color(.43f,.52f,.33f):kind==1?new Color(.48f,.62f,.71f):
                kind==2?new Color(.64f,.76f,.83f):new Color(.77f,.53f,.40f);
            Color rockTint=kind==0?new Color(.70f,.72f,.66f):kind==1?new Color(.64f,.74f,.82f):
                kind==2?new Color(.75f,.83f,.88f):new Color(.94f,.73f,.58f);
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                var p=min+new Vector2(col*spacing,row*spacing);
                float y=ReferenceBackdropHeight(p,outline,kind);
                float dx=(ReferenceBackdropHeight(p+Vector2.right*.4f,outline,kind)-ReferenceBackdropHeight(p-Vector2.right*.4f,outline,kind))/.8f;
                float dz=(ReferenceBackdropHeight(p+Vector2.up*.4f,outline,kind)-ReferenceBackdropHeight(p-Vector2.up*.4f,outline,kind))/.8f;
                float rock=Ease(.23f,.68f,Mathf.Sqrt(dx*dx+dz*dz));
                float exposed=Mathf.PerlinNoise(p.x*.22f+81,p.y*.25f+7);
                rock=Mathf.Max(rock,kind==0?.12f+exposed*.36f:.48f+exposed*.28f);
                float localHeight=y-ReferenceOceanOffset(p.x,p.y);
                float pale=.95f+Mathf.PerlinNoise(p.x*.055f+13,p.y*.071f+71)*.08f;
                float bedding=Mathf.Abs(Mathf.Repeat(localHeight*.64f+p.x*.018f-p.y*.012f,1)-.5f);
                pale*=Mathf.Lerp(.92f,1,Ease(.03f,.16f,bedding)*rock+1-rock);
                var color=Color.Lerp(grassTint,rockTint,rock)*pale;color.a=1;
                positions.Add(new Vector3(p.x,y,p.y));colors.Add(color);rocks.Add(rock);
                float localWater=ReferenceSeaLevel+ReferenceOceanOffset(p.x,p.y);
                mapping.Add(new Vector2(p.x+y*.13f,p.y+y*.34f)*.19f);
                extra.Add(new Vector2(Ease(localWater+1.2f,localWater-.1f,y),rock));
            }
            var indices=new List<int>();
            for(int row=0;row<rows-1;row++)for(int col=0;col<columns-1;col++)
            {
                int a=row*columns+col,b=a+1,c=a+columns,d=c+1;
                // Alternating diagonals avoid a screen-visible shared triangular
                // bias across broad shore faces while retaining a closed surface.
                if((row+col)%2==0)indices.AddRange(new[]{a,c,d,a,d,b});
                else indices.AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=new Mesh{name=title,indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(positions);mesh.SetTriangles(indices,0);
            mesh.SetColors(colors);mesh.SetUVs(0,mapping);mesh.SetUVs(1,extra);
            mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            // SampleGround intentionally turns world Y<-3.25 into seabed sand. A
            // curved-world distant mountain can be well above its LOCAL sea there,
            // so using that shader incorrectly made all blue distant peaks khaki.
            var shader=Resources.Load<Shader>("Coast/ReferenceBackdrop");
            if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
            var detailMap=ReferenceBackdropStoneTexture();
            var material=new Material(shader){name=title+" · 岩肩与远近颜色",enableInstancing=true};
            material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",detailMap);materials.Add(material);
            var surface=Group(title,parent).gameObject;
            surface.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=surface.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            // All top faces are retained. Close the submerged rectangle well below the
            // water absorption depth so there is no visible edge or paper-thin island.
            var rim=new List<Vector3>();
            for(int col=0;col<columns;col++)rim.Add(positions[col]);
            for(int row=1;row<rows;row++)rim.Add(positions[row*columns+columns-1]);
            for(int col=columns-2;col>=0;col--)rim.Add(positions[(rows-1)*columns+col]);
            for(int row=rows-2;row>0;row--)rim.Add(positions[row*columns]);
            var sealedVertices=new List<Vector3>();var sealedFaces=new List<int>();
            const float bottom=-120;
            for(int i=0;i<rim.Count;i++)
            {
                var a=rim[i];var b=rim[(i+1)%rim.Count];int first=sealedVertices.Count;
                sealedVertices.AddRange(new[]{a,b,new Vector3(b.x,bottom,b.z),new Vector3(a.x,bottom,a.z)});
                sealedFaces.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
            }
            int floor=sealedVertices.Count;
            sealedVertices.AddRange(new[]{new Vector3(min.x,bottom,min.y),new Vector3(min.x+(columns-1)*spacing,bottom,min.y),
                new Vector3(min.x+(columns-1)*spacing,bottom,min.y+(rows-1)*spacing),new Vector3(min.x,bottom,min.y+(rows-1)*spacing)});
            sealedFaces.AddRange(new[]{floor,floor+1,floor+2,floor,floor+2,floor+3});
            var baseMesh=MeshObject(title+" · 水下闭合体",parent,sealedVertices,sealedFaces,cliff);
            baseMesh.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        Texture2D referenceBackdropStone;
        Texture2D ReferenceBackdropStoneTexture()
        {
            if(referenceBackdropStone!=null)return referenceBackdropStone;
            const int size=128;var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                float grain=Mathf.PerlinNoise(x*.34f+17,y*.34f+21);
                float mottling=Mathf.PerlinNoise(x*.073f+3,y*.067f+51);
                float strata=.5f+.5f*Mathf.Sin(v*Mathf.PI*32+1.4f*Mathf.Sin(u*Mathf.PI*4));
                float tone=.82f+.10f*mottling+.07f*grain-.035f*Mathf.Pow(strata,5);
                pixels[y*size+x]=new Color(tone,tone,tone,1);
            }
            referenceBackdropStone=new Texture2D(size,size,TextureFormat.RGBA32,true,false)
            {name="远岸岩层 · 细砂与薄层",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=2};
            referenceBackdropStone.SetPixels(pixels);referenceBackdropStone.Apply(true,true);textures.Add(referenceBackdropStone);
            return referenceBackdropStone;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        Material referenceTerrainMaterial;
        static readonly Vector2[] ReferenceOutline =
        {
            new(-18.7f,-34.8f),new(-20.1f,-27.8f),new(-23,-18),new(-22,-11),new(-24,-3),
            new(-23,6),new(-24.2f,16.8f),new(-22,19.2f),new(-16,21),new(-7,25),new(0,26),
            new(6,28),new(8.5f,38),new(11,48.5f),new(18,51),new(27,50),new(34,43),new(35,30),new(33,21),
            new(34,4),new(35,-4),new(34,-12),new(33,-16),new(30,-17),
            new(26,-16.3f),new(23,-16.7f),new(22,-19.4f),new(21.5f,-23),
            new(20.5f,-26.5f),new(17,-29),new(14,-30),new(9,-32),
            new(3,-31),new(-2,-36),new(-5,-39),new(-10,-41.3f),new(-16.6f,-40),new(-18.2f,-37.6f)
        };

        readonly struct ReferencePad
        {
            public readonly Vector3 center;
            public readonly Vector2 half;
            public readonly float shoulder;
            public readonly int material;
            public ReferencePad(Vector3 c,Vector2 h,float s,int m)
            {center=c;half=h;shoulder=s;material=m;}
        }
        readonly struct ReferenceRoad
        {
            public readonly float width;
            public readonly Vector3[] points;
            public ReferenceRoad(float w,params Vector3[] p){width=w;points=p;}
        }

        // Lower pads first. Higher terrace fronts are exposed rock; only the explicit
        // circulation lines below are softened into walkable grades.
        static readonly ReferencePad[] ReferencePads =
        {
            new(new(-10,0,-25.7f),new(6.5f,6.9f),1.6f,1),
            new(new(-11,2.55f,-11),new(7.8f,2.4f),.85f,1),
            new(new(-11,3.45f,-6),new(7.8f,2.6f),.70f,1),
            new(new(-11,4.25f,-.65f),new(7.5f,2.6f),.65f,1),
            new(ReferenceGreenhousePad,ReferenceGreenhousePadHalf,1.2f,1),
            new(ReferenceWhiteCourt,ReferenceWhiteCourtHalf,.7f,1),
            new(ReferenceBrickCourt,ReferenceBrickCourtHalf,.55f,2),
            new(ReferenceWorkshop,new(4.42f,3.57f),.6f,2),
            new(ReferenceReservedPad,new(10.5f,6),.6f,3),
            new(ReferenceObservatory,ReferenceObservatoryGroundHalf,.27f,1),
            new(ReferencePierLanding,new(4.8f,1.9f),1.5f,3)
        };

        static readonly ReferenceRoad[] ReferenceRoads =
        {
            new(.70f,ReferenceHome,new(-10.4f,1.1f,-18.6f),new(-10.4f,2.90f,-14.4f),new(-10.4f,2.90f,-12.5f),
                new(-10.4f,2.85f,-11.2f),new(-10.4f,3.80f,-9.4f),new(-10.4f,3.80f,-7.8f),ReferenceNurseryApproach),
            new(.70f,ReferenceNurseryApproach,new(-10.4f,4.60f,-4.15f),new(-10.4f,4.60f,-2.35f),
                new(-10.4f,4.25f,-1.3f),new(-12.2f,5.2f,3.8f),new(-15,5.2f,4.7f)),
            new(1.15f,new(-17.8f,2.55f,-11.5f),new(-19.8f,3.45f,-6.5f),new(-20.0f,4.25f,-.5f),new(-21.6f,5.2f,5.1f),new(-15,5.2f,4.7f)),
            new(1.35f,ReferenceHome,new(-8.8f,.9f,-22.3f),ReferenceWestLanding,ReferenceBridgeStart),
            new(1.20f,new(-10.4f,1.1f,-18.6f),new(-8,1.7f,-19.6f),ReferenceWestLanding),
            new(1.35f,ReferenceBridgeEnd,ReferenceEastLanding),
            new(1.35f,ReferenceBridgeCourtDescent),
            new(1.25f,ReferenceWorkshopCourt,new(24,-.4f,-9),new(27.4f,.7f,-8.1f),ReferenceWorkshop),
            new(1.35f,ReferenceWorkshopCourt,new(20.5f,-.4f,-3.4f),ReferenceReserveStairs[0]),
            new(1.35f,ReferenceReserveStairs),
            new(ReferenceRidgePathHalfWidth,ReferenceRidgeApproach),
            new(ReferenceRidgePathHalfWidth,ReferenceRidgePath),
            new(1.35f,ReferenceObservatoryStairs),
            new(1.45f,ReferencePierDescent),
            new(1.25f,ReferencePierLanding,new(21,-2.7f,-16.8f),new(20,-2.7f,-20),new(19,-2.65f,-23.5f),new(17.8f,-2.6f,-26.3f),
                new(14.5f,-2.6f,-27.8f),new(11,-2.6f,-28.4f),new(8,-2.6f,-27.8f),new(6,-2.6f,-24.8f),ReferenceGrotto),
            new(1.55f,ReferenceCaveSteps),
            new(.95f,ReferenceGrotto,new(4.2f,-2.67f,-25),ReferenceShoreExit),
            new(1.14f,ReferenceForeshoreWalk),
            new(1.0f,ReferenceForeshoreLegacyArc),
            // The old editable land still has its original west-facing waist, at z=0.
            new(1.12f,new(-.7f,0,0),new(2.4f,0,0),new(5.55f,0,0)),
            new(1.04f,new(-.7f,0,0),new(2,.25f,-5),new(3.5f,.8f,-10),new(6.5f,2.2f,-15),ReferenceBridgeEnd)
        };

        public static float ReferenceHeight(float x,float z)
            => SampleReferenceHeight(x,z,out _,out _,out _,out _);

        public static bool ReferenceRidgePathContains(float x,float z,float margin=0)
        {
            float nearest=ReferenceRidgeSurvey(new Vector2(x,z),out _);
            float width=ReferenceRidgePathHalfWidth+margin;
            return nearest<width*width;
        }

        static float ReferenceRidgeSurvey(Vector2 point,out float grade)
        {
            float nearest=float.PositiveInfinity;grade=0;
            for(int routeIndex=0;routeIndex<2;routeIndex++)
            {
                var route=routeIndex==0?ReferenceRidgeApproach:ReferenceRidgePath;
                for(int i=0;i<route.Length-1;i++)
                {
                    float square=SegmentDistanceSquared(point,route[i],route[i+1],out float t);
                    if(square>=nearest)continue;
                    nearest=square;grade=Mathf.Lerp(route[i].y,route[i+1].y,t);
                }
            }
            return nearest;
        }

        static float ReferenceNurseryPathHeight(float z)
        {
            // The narrow aisle needs a uniform cross-section. Blending several nearby
            // pad/path surveys produced side slopes steeper than the real bridge ramps.
            var points=ReferenceRoads[0].points;
            for(int i=1;i<points.Length;i++)
                if(z<=points[i].z)return Mathf.Lerp(points[i-1].y,points[i].y,
                    Mathf.InverseLerp(points[i-1].z,points[i].z,z));
            points=ReferenceRoads[1].points;
            for(int i=1;i<points.Length;i++)
                if(z<=points[i].z)return Mathf.Lerp(points[i-1].y,points[i].y,
                    Mathf.InverseLerp(points[i-1].z,points[i].z,z));
            return points[points.Length-1].y;
        }

        static float ReferenceRectWeight(float x,float z,ReferencePad pad)
        {
            if(pad.center==ReferenceReservedPad)
                return 1-Ease(0,pad.shoulder,Mathf.Max(0,-ReferencePolygonDistance(new Vector2(x,z),ReferenceReserveEdge)));
            if(pad.center==ReferenceObservatory)
            {
                float radius=Mathf.Sqrt(Mathf.Pow((x-pad.center.x)/pad.half.x,2)+Mathf.Pow((z-pad.center.z)/pad.half.y,2));
                return 1-Ease(1,1+pad.shoulder/Mathf.Min(pad.half.x,pad.half.y),radius);
            }
            float dx=Mathf.Max(0,Mathf.Abs(x-pad.center.x)-pad.half.x);
            float dz=Mathf.Max(0,Mathf.Abs(z-pad.center.z)-pad.half.y);
            return 1-Ease(0,pad.shoulder,Mathf.Sqrt(dx*dx+dz*dz));
        }

        static float ReferenceShoreDistance(Vector2 p)
            => ReferencePolygonDistance(p,ReferenceOutline);

        static float ReferencePolygonDistance(Vector2 p,Vector2[] outline)
        {
            bool inside=false;float nearest=float.PositiveInfinity;
            for(int i=0,j=outline.Length-1;i<outline.Length;j=i++)
            {
                var a=outline[i];var b=outline[j];var ab=b-a;
                var t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);
                nearest=Mathf.Min(nearest,(p-a-ab*t).sqrMagnitude);
                if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return Mathf.Sqrt(nearest)*(inside?1:-1);
        }

        static float ReferenceHill(float x,float z,float cx,float cz,float rx,float rz,float height)
        {
            float a=(x-cx)/rx,b=(z-cz)/rz;
            return height*Mathf.Exp(-(a*a+b*b)*2.6f);
        }

        static float SampleReferenceHeight(float x,float z,out int surface,out float paving,out float wet,out bool underWater)
        {
            float shore=ReferenceShoreDistance(new Vector2(x,z));
            float coarse=Mathf.PerlinNoise(x*.17f+51,z*.17f+32);
            float fine=Mathf.PerlinNoise(x*.87f+23,z*.76f+62);
            float y=-.15f+(coarse-.5f)*.48f+(fine-.5f)*.07f;
            y+=ReferenceHill(x,z,-17,10,12,12,4.5f);
            y+=ReferenceHill(x,z,0,10,9,9,3.2f);
            y+=ReferenceHill(x,z,22,14,11,10,2.6f);
            y+=ReferenceHill(x,z,-19,-20,6,11,1.05f);
            surface=0;paving=0;wet=0;underWater=false;
            // The observatory stands on a distinct round stone podium. Its natural
            // foot stays below the visible masonry instead of becoming a broad Y4
            // lawn that buries the south wall. Only the two surveyed approaches rise
            // to the platform; their full-width final grades are applied below.
            float starAngle=Mathf.Atan2((z-ReferenceObservatory.z)/10.8f,(x-ReferenceObservatory.x)/12.0f);
            float starRadius=Mathf.Sqrt(Mathf.Pow((x-ReferenceObservatory.x)/12.0f,2)+Mathf.Pow((z-ReferenceObservatory.z)/10.8f,2));
            starRadius/=(1+.045f*Mathf.Sin(starAngle*3+.6f)+.025f*Mathf.Cos(starAngle*7));
            float starFoot=1-Ease(.91f,1.18f,starRadius);
            float starApron=Ease(20.5f,22.5f,z)*(1-Ease(28,31,z))*(1-Ease(7.0f,10.2f,Mathf.Abs(x-19.5f)));
            float starSoil=Mathf.Lerp(2.4f,ReferenceObservatoryPlinthFoot,Ease(22,27.5f,z))
                +(coarse-.5f)*.13f+.045f*Mathf.Sin(x*.7f+z*.4f);
            float starGround=Mathf.Max(starFoot,starApron);
            y=Mathf.Lerp(y,starSoil,starGround);
            // The foreground on the right is a beach cut into the shared rock mass,
            // not another empty lawn at the same elevation as the upper workshop.
            float beach=Ease(10,17,x)*(1-Ease(-15,-10,z));
            y=Mathf.Lerp(y,-2.62f+(coarse-.5f)*.24f,beach);
            if(beach>.4f)surface=3;
            // Embed the rear and flanks of the closed cave shell. The inner floor is
            // carved separately below, so no heightfield surface crosses its headroom.
            float rear=Ease(-18.65f,-17.2f,z)*(1-Ease(-13,-10,z))*(1-Ease(6.9f,10,Mathf.Abs(x-3)));
            y=Mathf.Lerp(y,Mathf.Max(y,2.65f+(coarse-.5f)*.18f),rear);
            float side=Ease(6.5f,8.6f,Mathf.Abs(x-3))*(1-Ease(8.6f,12,Mathf.Abs(x-3)));
            side*=Ease(-25.4f,-23,z)*(1-Ease(-15,-12,z));
            y=Mathf.Lerp(y,Mathf.Max(y,x<3?1.15f:2.4f),side);
            foreach(var pad in ReferencePads)
            {
                float weight=ReferenceRectWeight(x,z,pad);
                y=Mathf.Lerp(y,pad.center.y,weight);
                if(weight>.55f)surface=pad.material;
            }
            // The home court has a planted lawn under its tree, not an uninterrupted
            // limestone sheet. Paths and pond geometry are applied afterwards.
            float homeLawn=Mathf.Pow((x+15.3f)/4.7f,2)+Mathf.Pow((z+24.3f)/5.0f,2);
            if(homeLawn<.88f+(coarse-.5f)*.45f)surface=0;

            // The expanded brick court meets the bowed sand-court wall. Its ground
            // follows exactly the same polygon as the masonry, including both rounded
            // corners; there is no straight hidden slab left underneath the arc.
            float sandEdge=ReferencePolygonDistance(new Vector2(x,z),ReferenceReserveEdge);
            if(sandEdge<0&&sandEdge>-3.5f&&z>5.6f&&z<13&&x>5&&x<31)
            {
                float join=1-Ease(2.7f,3.5f,-sandEdge);
                float ledge=Mathf.Lerp(ReferenceBrickCourt.y,ReferenceReservedPad.y,Ease(-.55f,0,sandEdge));
                y=Mathf.Lerp(y,ledge,join);if(join>.5f){surface=2;paving=0;}
            }

            // A complete, low floor is retained through the cave. The roof and side vault
            // are separate solid assets; no missing terrain faces are used to suggest a cave.
            float caveX=Mathf.Max(0,Mathf.Abs(x-3)-6.6f);
            float caveZ=Mathf.Max(0,Mathf.Abs(z+21.6f)-3.45f);
            float cave=1-Ease(0,.65f,Mathf.Sqrt(caveX*caveX+caveZ*caveZ));
            y=Mathf.Lerp(y,-2.6f,cave);
            if(cave>.65f)surface=4;
            float foreX=Mathf.Max(0,Mathf.Abs(x-3)-6.7f),foreZ=Mathf.Max(0,Mathf.Abs(z+26.25f)-1.45f);
            float fore=1-Ease(0,.75f,Mathf.Sqrt(foreX*foreX+foreZ*foreZ));
            y=Mathf.Lerp(y,-2.6f,fore);
            if(fore>.65f)surface=4;
            // The outfall spreads into a broad shallow bay rather than a slot in a flat plate.
            float bayX=(x-3)/9.4f,bayZ=(z+31.5f)/6.8f;
            float bay=1-Ease(.25f,1,bayX*bayX+bayZ*bayZ);
            y=Mathf.Lerp(y,ReferenceSeaLevel-.37f+coarse*.14f,bay);
            if(bay>.15f)surface=3;

            // Author circulation as a compact network. Distance-weighted grades avoid
            // a hard ridge where two paths join at an angle.
            float nearest=float.PositiveInfinity,nearestWidth=0;
            float grades=0,weights=0;
            var point=new Vector2(x,z);
            foreach(var road in ReferenceRoads)
            for(int segment=0;segment<road.points.Length-1;segment++)
            {
                float square=SegmentDistanceSquared(point,road.points[segment],road.points[segment+1],out float t);
                float distance=Mathf.Sqrt(square);
                if(square<nearest){nearest=square;nearestWidth=road.width;}
                float weight=Mathf.Pow(1-Ease(road.width*.70f,road.width+1.45f,distance),3)/(square+.035f);
                grades+=Mathf.Lerp(road.points[segment].y,road.points[segment+1].y,t)*weight;
                weights+=weight;
            }
            float roadDistance=Mathf.Sqrt(nearest);
            if(weights>.00001f)
            {
                float influence=1-Ease(nearestWidth-.07f,nearestWidth+1.3f,roadDistance);
                y=Mathf.Lerp(y,grades/weights,influence);
                paving=1-Ease(nearestWidth*.76f-.12f,nearestWidth*.76f+.18f,roadDistance+(fine-.5f)*.11f);
            }

            // The watercourse shares its survey with the water meshes; dry banks remain
            // wide enough for the nearby paths. This is a bed, not a floating water ribbon.
            float riverDistance=float.PositiveInfinity,riverY=0,riverWidth=0;
            for(int i=0;i<ReferenceStream.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,ReferenceStream[i],ReferenceStream[i+1],out float t);
                if(square>=riverDistance)continue;
                riverDistance=square;
                riverY=Mathf.Lerp(ReferenceStream[i].y,ReferenceStream[i+1].y,t);
                riverWidth=Mathf.Lerp(ReferenceStreamHalfWidths[i],ReferenceStreamHalfWidths[i+1],t);
            }
            float streamDistance=Mathf.Sqrt(riverDistance);
            // Water crossing the cave crown belongs to the solid cave asset, while
            // this mesh stays at the walkable interior floor below it.
            bool aboveCave=z<-18.35f&&z>-25.30f;
            // The northern rock ridge includes its own sculpted upper valley. Keep
            // the original low terrain beneath that solid asset, rather than raising
            // a duplicate green heightfield through its seven-metre stone shoulders.
            bool aboveRidge=z>7;
            if(!aboveCave&&!aboveRidge&&streamDistance<riverWidth+1.55f)
            {
                float bed=riverY-.24f;
                float shoulder=1-Ease(riverWidth+.40f,riverWidth+1.55f,streamDistance);
                y=Mathf.Lerp(y,Mathf.Max(y,riverY+.22f),shoulder);
                float influence=1-Ease(riverWidth*.91f,riverWidth+.55f,streamDistance);
                y=Mathf.Lerp(y,bed,influence);
                underWater=streamDistance<riverWidth*.98f;
                wet=1-Ease(riverWidth*.77f,riverWidth+.70f,streamDistance);
                if(wet>.35f)surface=4;
                paving*=1-wet;
            }

            // Cliff and beach toes blend into an invisible seabed. The render mesh is
            // continuous and closed, even where its underwater portion has no navigation.
            if(shore<2.0f)
            {
                float heel=ReferenceSeaLevel-.65f;
                float steep=Ease(-.6f,1.55f,shore);
                y=Mathf.Lerp(heel,y,steep);
                if(shore<1.2f)surface=3;
                wet=Mathf.Max(wet,1-Ease(ReferenceSeaLevel+.05f,ReferenceSeaLevel+.8f,y));
            }
            // A submerged shelf slopes outwards before reaching deep water. A
            // fifteen-metre drop just five metres from every bank made a bright
            // cyan outline around an otherwise uniformly blue sea.
            if(shore<-.6f)y=ReferenceSeaLevel-.65f-14.35f*Ease(0,22,-shore-.6f);

            // The water in front of the grotto has a real shallow bed, with a continuous
            // transition into the deeper sea rather than a cyan material rectangle.
            float coveRadius=Mathf.Sqrt(Mathf.Pow((x-3)/12,2)+Mathf.Pow((z+29.5f)/8.5f,2));
            float coveSide=1-Ease(.50f,1.15f,coveRadius);
            float coveDepthMask=coveSide*(1-Ease(-26.5f,-25.75f,z));
            float coveDepth=.15f+.65f*Ease(-26,-35,z);
            float coveFloor=ReferenceSeaLevel-coveDepth+(fine-.5f)*.035f;
            y=Mathf.Lerp(y,coveFloor,coveDepthMask);
            if(coveDepthMask>.45f){surface=3;wet=Mathf.Max(wet,coveDepthMask);}
            // The concept's visible reef bay continues into the foreground. Add
            // real sand/rock bed only below existing water, leaving every dry
            // bank, path and saved plot unchanged.
            if(y<ReferenceSeaLevel-.20f && z<-28)
            {
                float bayRadius=Mathf.Sqrt(Mathf.Pow((x-4)/23,2)+Mathf.Pow((z+34)/20,2));
                float bayWeight=(1-Ease(.55f,1.05f,bayRadius))*Ease(-28,-31,z);
                float bayDepth=.65f+3.85f*Ease(-29,-54,z)+(coarse-.5f)*.35f;
                float bayFloor=ReferenceSeaLevel-Mathf.Max(.45f,bayDepth);
                y=Mathf.Lerp(y,Mathf.Max(y,bayFloor),bayWeight);
                if(bayWeight>.15f){surface=3;wet=Mathf.Max(wet,bayWeight);}
            }

            // A five-metre sand apron follows the actual right shoreline at roughly
            // one in seven. The route survey below only forms the necessary dry tread.
            float rightBeach=Ease(11,14,x)*(1-Ease(26,29,x))*Ease(-30,-28,z)*(1-Ease(-21,-18,z));
            rightBeach*=Ease(-4,-1,shore);
            float sandGrade=ReferenceSeaLevel+.10f+Mathf.Clamp(shore,-3,6)*.14f;
            y=Mathf.Lerp(y,sandGrade,rightBeach);
            if(rightBeach>.45f){surface=3;paving=0;wet=Mathf.Max(wet,.45f*rightBeach);}

            // Low rock shelves and the path around the pond replace the old cliff
            // dropping straight into deep water along the lower-left foreground.
            float reef=Ease(-23,-19,x)*(1-Ease(-6,-3,x))*Ease(-40,-37,z)*(1-Ease(-35.7f,-32.6f,z));
            y=Mathf.Lerp(y,-2.58f+(coarse-.5f)*.20f,reef);
            if(reef>.45f){surface=4;paving=0;wet=Mathf.Max(wet,reef*.45f);}

            ApplyReferenceForeshoreProfile(x,z,ref y,ref surface,ref paving,ref wet);
            if(x>-21&&x<-4&&z<-31.7f&&z>-42)underWater=y<ReferenceSeaLevel+.08f;

            // The actual circulation survey has final priority over the visual hills.
            // A broad, smooth corridor keeps junctions and the cave approach continuous
            // without changing agent step height or adding invisible bridge colliders.
            if(weights>.00001f)
            {
                float corridor=1-Ease(nearestWidth*.83f,nearestWidth+.55f,roadDistance);
                y=Mathf.Lerp(y,grades/weights,corridor);
                if(corridor>.88f&&y>ReferenceSeaLevel+.12f){underWater=false;wet*=1-corridor;}
                paving=Mathf.Max(paving,corridor*.92f);
            }

            // Finish the bridge descent outside the brick court. Once inside, its
            // route is the courtyard pavement itself instead of a raised diagonal
            // ridge. Keep the workshop base and its separate entry stairs intact.
            if(x>=5.3f&&x<=33&&z>=-13&&z<=8.5f&&(x>=8||z>=-5.6f)&&(x<=21.8f||z<=-9.25f||z>=1))
            {y=ReferenceWorkshopCourt.y;surface=2;paving=0;wet=0;underWater=false;}

            // This support apron belongs to the original zero-height editing frame.
            // The editable ellipse is carved immediately afterwards and is never covered.
            if(x>=5.3f&&x<=18.3f&&z>=-5.6f&&z<=5.6f)
            {y=0;surface=1;paving=0;wet=0;underWater=false;}

            // Exactly the old land silhouette, with a short level apron rather than a
            // bowl around it. The original raising land and collider fill this socket.
            float bx=(x-11.7f)/6.15f,bz=z/5.15f;
            float angle=Mathf.Atan2(bz,bx);
            float rim=1+.02f*Mathf.Sin(angle*3)+.012f*Mathf.Cos(angle*7);
            float radius=Mathf.Sqrt(bx*bx+bz*bz)/rim;
            if(radius<1.05f)
            {
                y=Mathf.Lerp(ReferenceSeaLevel-.65f,0,Ease(.985f,1.045f,radius));
                underWater=radius<1.015f;surface=1;paving=0;wet=radius<1.02f?1:0;
            }
            // The original waist and the editable-west entrance remain at exactly y=0.
            if(x>-.9f&&x<5.8f&&Mathf.Abs(z)<1.15f)
            {y=Mathf.Lerp(y,0,1-Ease(.85f,1.15f,Mathf.Abs(z)));underWater=false;paving=1;surface=1;}
            // Stair support wins over cliff/river blending at the actual landings.
            float stepDistance=float.PositiveInfinity,stepGrades=0,stepWeights=0;
            for(int i=0;i<ReferenceCaveSteps.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,ReferenceCaveSteps[i],ReferenceCaveSteps[i+1],out float t);
                float weight=Mathf.Pow(1-Ease(1.45f,2.10f,Mathf.Sqrt(square)),3)/(square+.035f);
                stepGrades+=Mathf.Lerp(ReferenceCaveSteps[i].y,ReferenceCaveSteps[i+1].y,t)*weight;
                stepWeights+=weight;stepDistance=Mathf.Min(stepDistance,square);
            }
            if(stepWeights>.00001f)
            {
                float influence=1-Ease(1.45f,2.05f,Mathf.Sqrt(stepDistance));
                y=Mathf.Lerp(y,stepGrades/stepWeights,influence);
                if(influence>.88f)underWater=false;
                surface=1;paving=Mathf.Max(paving,influence*.95f);
            }
            // The former diagonal descended underneath the fixed workshop stock and
            // planters, creating low overhead collision even though its slope was mild.
            // This visible lane stays level behind the stock, then turns down its right
            // side. Keep a uniform tread across the full body clearance at the bends.
            float pierNearest=float.PositiveInfinity,pierGrade=0;
            for(int i=0;i<ReferencePierDescent.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,ReferencePierDescent[i],ReferencePierDescent[i+1],out float t);
                if(square>=pierNearest)continue;
                pierNearest=square;pierGrade=Mathf.Lerp(ReferencePierDescent[i].y,ReferencePierDescent[i+1].y,t);
            }
            float pierSupport=1-Ease(.98f,1.55f,Mathf.Sqrt(pierNearest));
            if(pierSupport>0)
            {
                y=Mathf.Lerp(y,pierGrade,pierSupport);paving=Mathf.Max(paving,pierSupport);
                if(pierSupport>.97f){underWater=false;surface=1;}
            }
            float reserveNearest=float.PositiveInfinity,reserveGrade=0;
            for(int i=0;i<ReferenceReserveStairs.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,ReferenceReserveStairs[i],ReferenceReserveStairs[i+1],out float t);
                if(square>=reserveNearest)continue;
                reserveNearest=square;reserveGrade=Mathf.Lerp(ReferenceReserveStairs[i].y,ReferenceReserveStairs[i+1].y,t);
            }
            float reserveSupport=1-Ease(1.20f,1.80f,Mathf.Sqrt(reserveNearest));
            if(reserveSupport>0)
            {
                y=Mathf.Lerp(y,reserveGrade,reserveSupport);paving=Mathf.Max(paving,reserveSupport);
                if(reserveSupport>.97f){underWater=false;surface=1;}
            }
            // The S bend is cut into the rock model and supported by this same land
            // mesh. Enforce its full-width grade after the neighboring court blends,
            // so the model's 12 cm lower channel floor never becomes the walking floor.
            // The old editable frame and its level apron always keep their own survey.
            float ridgeSquare=ReferenceRidgeSurvey(point,out float ridgeGrade);
            bool whiteFrame=x>=5.3f&&x<=18.3f&&z>=-5.6f&&z<=5.6f;
            if(!whiteFrame&&radius>=1.05f)
            {
                float ridgeSupport=1-Ease(ReferenceRidgePathHalfWidth,1.65f,Mathf.Sqrt(ridgeSquare));
                if(ridgeSupport>0)
                {
                    y=Mathf.Lerp(y,ridgeGrade,ridgeSupport);
                    paving=Mathf.Max(paving,ridgeSupport);
                    if(ridgeSupport>.98f){surface=1;underWater=false;wet=0;}
                }
            }
            // This is the physical descent from the bridge landing into the brick
            // court. A nearby high cave shoulder and the courtyard's hard level clamp
            // must not create a ridge across its middle or a drop at z=-13.
            float courtNearest=float.PositiveInfinity,courtGrade=0;
            for(int i=0;i<ReferenceBridgeCourtDescent.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,ReferenceBridgeCourtDescent[i],ReferenceBridgeCourtDescent[i+1],out float t);
                if(square>=courtNearest)continue;
                courtNearest=square;courtGrade=Mathf.Lerp(ReferenceBridgeCourtDescent[i].y,ReferenceBridgeCourtDescent[i+1].y,t);
            }
            float courtSupport=1-Ease(1.30f,1.85f,Mathf.Sqrt(courtNearest));
            if(courtSupport>0)
            {
                y=Mathf.Lerp(y,courtGrade,courtSupport);paving=Mathf.Max(paving,courtSupport);
                if(courtSupport>.98f){underWater=false;surface=1;wet=0;}
            }
            float starNearest=float.PositiveInfinity,starGrade=0;
            for(int i=0;i<ReferenceObservatoryStairs.Length-1;i++)
            {
                float square=SegmentDistanceSquared(point,ReferenceObservatoryStairs[i],ReferenceObservatoryStairs[i+1],out float t);
                if(square>=starNearest)continue;
                starNearest=square;starGrade=Mathf.Lerp(ReferenceObservatoryStairs[i].y,ReferenceObservatoryStairs[i+1].y,t);
            }
            float starSupport=1-Ease(1.30f,1.85f,Mathf.Sqrt(starNearest));
            if(starSupport>0)
            {
                y=Mathf.Lerp(y,starGrade,starSupport);paving=Mathf.Max(paving,starSupport);
                if(starSupport>.98f){underWater=false;surface=1;wet=0;}
            }
            ApplyReferenceWorkshopAccess(x,z,ref y,ref surface,ref paving,ref wet,ref underWater);
            // The upper approach crosses the cave roof on a separate, visible stone
            // landing. Its XZ projection must never raise the walkable cave floor.
            float roomSide=1-Ease(6.95f,7.65f,Mathf.Abs(x-ReferenceGrotto.x));
            float roomRear=1-Ease(-18.15f,-17.6f,z);
            float roomFront=Ease(-25.75f,-25.2f,z);
            float clearRoom=roomSide*roomRear*roomFront;
            y=Mathf.Lerp(y,ReferenceGrotto.y,clearRoom);
            if(clearRoom>.98f){underWater=false;surface=4;paving=0;wet=.15f;}

            // A genuine plunge pool catches the fall, then opens into the sea.
            // The old clear-room flattening filled this spot back to the dry floor,
            // so the sheet appeared to disappear into sand instead of landing in water.
            float plungeRadius=Mathf.Sqrt(Mathf.Pow((x-.40f)/1.75f,2)+Mathf.Pow((z+25.55f)/1.40f,2));
            float plunge=1-Ease(.73f,1.08f,plungeRadius);
            float outletDistance=SegmentDistanceSquared(new Vector2(x,z),new Vector3(.4f,0,-25.7f),new Vector3(1.3f,0,-28.5f),out _);
            float outlet=1-Ease(.70f,1.12f,Mathf.Sqrt(outletDistance));
            float poolMask=Mathf.Max(plunge,outlet);
            if(poolMask>0)
            {
                float bottom=ReferenceSeaLevel-.58f+(fine-.5f)*.055f;
                y=Mathf.Lerp(y,Mathf.Min(y,bottom),poolMask);
                wet=Mathf.Max(wet,poolMask);paving*=1-poolMask;
                if(poolMask>.75f){surface=4;underWater=true;}
            }

            // Match the one-metre-clear aisle to the bridge ramps, rather than letting
            // a weighted terrace blend protrude through their walking surface.
            float aisleWidth=Mathf.Abs(x+10.4f);
            if(aisleWidth<.95f&&z>=-18.6f&&z<=-1.3f)
            {
                float aisle=1-Ease(.80f,.95f,aisleWidth);
                y=Mathf.Lerp(y,ReferenceNurseryPathHeight(z),aisle);
                if(aisle>.98f){underWater=false;surface=1;paving=1;}
                for(int i=0;i<ReferenceNurseryCanalZ.Length;i++)
                {
                    float center=ReferenceNurseryCanalZ[i],delta=z-center;
                    if(Mathf.Abs(delta)>=1.65f||aisleWidth>=.82f)continue;
                    float deck=ReferenceNurseryLevels[i]+.35f;
                    float ramp=delta<-.6f?Mathf.Lerp(ReferenceNurseryPathHeight(center-1.65f)+.025f,deck,(delta+1.65f)/1.05f):
                        delta>.6f?Mathf.Lerp(deck,ReferenceNurseryPathHeight(center+1.65f)+.025f,(delta-.6f)/1.05f):deck;
                    y=Mathf.Min(y,ramp-.06f*Ease(1.65f,1.35f,Mathf.Abs(delta)));
                    // The bridge is the actual collider here; submerged ground is
                    // still rendered, but cannot form a competing steep nav surface.
                    if(Mathf.Abs(delta)<1.60f){underWater=true;paving=0;}
                    if(Mathf.Abs(delta)<.60f){y=Mathf.Min(y,ReferenceNurseryLevels[i]-.08f);surface=4;wet=1;}
                }
            }
            // The foreground rock toe previously lowered only the rendered mesh,
            // leaving the independent terrain collider floating above that surface.
            // Keep the identical final profile in this shared survey so rendering,
            // collision, navigation and all placement queries agree on its height.
            float foreshoreSection=z-ReferenceForeshoreCurve(x);
            float foreshoreSide=ReferenceForeshoreSmooth(-15.8f,-15.25f,x)*(1-ReferenceForeshoreSmooth(-5,-4.45f,x));
            float foreshoreBack=ReferenceForeshoreSmooth(-32.35f,-32.85f,foreshoreSection);
            float foreshoreFront=1-ReferenceForeshoreSmooth(-36.7f,-38.3f,foreshoreSection);
            float foreshoreLower=foreshoreSide*foreshoreBack*foreshoreFront;
            if(foreshoreLower>0)
            {
                float toe=-2.74f+.025f*Mathf.Sin(x*.7f);
                y=Mathf.Lerp(y,Mathf.Min(y,toe),foreshoreLower);
                if(foreshoreLower>.5f)surface=4;
                wet=Mathf.Max(wet,foreshoreLower*.38f);
            }
            // The two visible stair flights and the low arc share an actual land
            // support profile. This final grade also prevents the exposed-rock toe
            // treatment above from lowering soil away from underneath the stairs.
            if(z<-28.5f)
            {
                float nearestForeshore=float.PositiveInfinity,foreshoreGrade=0;
                for(int route=0;route<2;route++)
                {
                var profile=route==0?ReferenceForeshoreWalk:ReferenceForeshoreLegacyArc;
                for(int i=profile==ReferenceForeshoreWalk?1:0;i<profile.Length-1;i++)
                {
                    float square=SegmentDistanceSquared(point,profile[i],profile[i+1],out float t);
                    if(square>=nearestForeshore)continue;
                    nearestForeshore=square;foreshoreGrade=Mathf.Lerp(profile[i].y,profile[i+1].y,t);
                }
                }
                float road=1-Ease(1.10f,1.80f,Mathf.Sqrt(nearestForeshore));
                y=Mathf.Lerp(y,foreshoreGrade,road);
                if(road>.97f){surface=1;underWater=false;wet=0;}
                paving=Mathf.Max(paving,road);
            }
            // A real shallow basin sits under the unchanged garden water surface.
            // Its submerged floor is omitted from navigation, so click-to-walk cannot
            // shortcut through the pond on the way to the new west-side stairs.
            float pondRadius=Mathf.Sqrt(Mathf.Pow((x-ReferencePondX)/1.96f,2)+Mathf.Pow((z-ReferencePondZ)/1.47f,2));
            if(pondRadius<1.08f)
            {
                y=Mathf.Lerp(y,-.55f,1-Ease(.88f,1.08f,pondRadius));
                if(pondRadius<1.01f){underWater=true;surface=4;paving=0;wet=1;}
            }
            underWater|=y<ReferenceSeaLevel+.08f;
            return y;
        }

        void BuildReferenceTerrain()
        {
            // 72 extra underwater rows retain every original ground sample at
            // the same world coordinate and extend the visible seabed to -65m.
            const float spacing=.28f,minimumX=-28,minimumZ=-65.16f;
            const int columns=258,rows=452;
            var vertices=new List<Vector3>(columns*rows);
            var colors=new List<Color>(columns*rows);
            var uv=new List<Vector2>(columns*rows);
            var detailsUV=new List<Vector2>(columns*rows);
            var submerged=new bool[columns*rows];
            var palette=new[]{grass.color,limestone.color,brick.color,sandstone.color,wetStone.color};
            // Warm compacted sand stays distinct from the editable cream-stone
            // court. Only the reference world's visible finish changes here.
            palette[0]=new Color(.47f,.57f,.35f);
            palette[3]=new Color(.88f,.72f,.51f);
            palette[4]=new Color(.48f,.50f,.46f);
            for(int row=0;row<rows;row++)
            for(int column=0;column<columns;column++)
            {
                float x=minimumX+column*spacing,z=minimumZ+row*spacing;
                float y=SampleReferenceHeight(x,z,out int type,out float paving,out float wet,out bool waterCovered);
                vertices.Add(new Vector3(x,y,z));
                var color=palette[type];color.a=paving;colors.Add(color);
                uv.Add(new Vector2(x*.34f,z*.34f+y*.2f));detailsUV.Add(new Vector2(wet,0));
                submerged[vertices.Count-1]=waterCovered;
            }
            for(int row=1;row<rows-1;row++)
            for(int column=1;column<columns-1;column++)
            {
                int at=row*columns+column;
                float dx=(vertices[at+1].y-vertices[at-1].y)/(spacing*2);
                float dz=(vertices[at+columns].y-vertices[at-columns].y)/(spacing*2);
                float steep=Ease(.65f,1.45f,Mathf.Sqrt(dx*dx+dz*dz));
                var color=Color.Lerp(colors[at],cliff.color,steep);color.a=colors[at].a*(1-steep);
                colors[at]=color;detailsUV[at]=new Vector2(detailsUV[at].x,steep);
            }
            SculptReferenceRockRelief(vertices,detailsUV,columns,rows,spacing);
            var triangles=new List<int>((rows-1)*(columns-1)*6);
            var walkable=new List<int>();
            void Face(int a,int b,int c)
            {
                if(!ReferenceCliffReplacesGround((vertices[a]+vertices[b]+vertices[c])/3))
                    triangles.AddRange(new[]{a,b,c});
                if(!submerged[a]&&!submerged[b]&&!submerged[c])walkable.AddRange(new[]{a,b,c});
            }
            for(int row=0;row<rows-1;row++)for(int col=0;col<columns-1;col++)
            {
                int a=row*columns+col,b=a+1,c=a+columns,d=c+1;
                Face(a,c,d);Face(a,d,b);
            }
            var mesh=new Mesh{name="参考图 · 连续紧凑海岸",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.SetUVs(1,detailsUV);mesh.SetColors(colors);
            mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var collision=new Mesh{name="参考图 · 干岸与道路碰撞",indexFormat=IndexFormat.UInt32};
            collision.SetVertices(vertices);collision.SetTriangles(walkable,0);collision.RecalculateBounds();meshes.Add(collision);
            var obj=Group("参考地图 · 连续地表与地块承接",ground).gameObject;obj.layer=FloorLayer;
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var shader=Resources.Load<Shader>("Coast/SampleGround");
            var material=shader==null?grass:new Material(shader){name="参考地图 · 岸坡材质",color=grass.color};
            if(shader!=null){material.SetColor("_StoneColor",cliff.color);material.SetColor("_PathColor",path.color);materials.Add(material);}
            obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            referenceTerrainMaterial=material;
            obj.AddComponent<MeshCollider>().sharedMesh=collision;

            // Close every outer side and bottom below sea level. No camera direction can
            // expose a missing top triangle or a paper-thin ground slab.
            var rim=new List<Vector3>();
            for(int c=0;c<columns;c++)rim.Add(vertices[c]);
            for(int r=1;r<rows;r++)rim.Add(vertices[r*columns+columns-1]);
            for(int c=columns-2;c>=0;c--)rim.Add(vertices[(rows-1)*columns+c]);
            for(int r=rows-2;r>0;r--)rim.Add(vertices[r*columns]);
            var closed=new List<Vector3>();var closedFaces=new List<int>();
            float bottom=ReferenceSeaLevel-24;
            for(int i=0;i<rim.Count;i++)
            {
                int j=(i+1)%rim.Count,n=closed.Count;
                closed.Add(rim[i]);closed.Add(rim[j]);closed.Add(new Vector3(rim[j].x,bottom, rim[j].z));closed.Add(new Vector3(rim[i].x,bottom,rim[i].z));
                closedFaces.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            int floor=closed.Count;
            closed.Add(new Vector3(minimumX,bottom,minimumZ));
            closed.Add(new Vector3(minimumX+(columns-1)*spacing,bottom,minimumZ));
            closed.Add(new Vector3(minimumX+(columns-1)*spacing,bottom,minimumZ+(rows-1)*spacing));
            closed.Add(new Vector3(minimumX,bottom,minimumZ+(rows-1)*spacing));
            closedFaces.AddRange(new[]{floor,floor+1,floor+2,floor,floor+2,floor+3});
            MeshObject("连续地形水下封闭体",ground,closed,closedFaces,cliff);
            BuildReferenceBridgeLanding("东桥肩真实石质落地坡",ReferenceBridgeEnd,ReferenceEastLanding);
            BuildReferenceBridgeLanding("西桥肩真实石质落地坡",ReferenceWestLanding,ReferenceBridgeStart);
        }

        void BuildReferenceBridgeLanding(string name,Vector3 a,Vector3 b)
        {
            // A real masonry ramp crosses the cave shoulder above its interior. Its
            // shallow underside is embedded into the shell, not extended to cave floor.
            var along=b-a;along.y=0;along.Normalize();
            var side=Vector3.Cross(Vector3.up,along)*1.48f;
            a-=along*.22f;b+=along*.26f;
            var bottom=Vector3.down*1.35f;
            var v=new List<Vector3>{a-side,a+side,b+side,b-side,
                a-side+bottom,a+side+bottom,b+side+bottom,b-side+bottom};
            var f=new List<int>{0,2,1,0,3,2,4,5,6,4,6,7,
                0,5,4,0,1,5,1,6,5,1,2,6,2,7,6,2,3,7,3,4,7,3,0,4};
            var solid=MeshObject(name,ground,v,f,limestone,true);
            if(referenceTerrainMaterial!=null)
            {
                // The old support used one smoothly shaded yellow cuboid. Keep
                // its tested collision, but articulate the real visible solid
                // with irregular side planes beneath the unchanged paving.
                var points=new List<Vector3>();var faces=new List<int>();
                var colors=new List<Color>();var surface=new List<Vector2>();
                void Quad(Vector3 q0,Vector3 q1,Vector3 q2,Vector3 q3,bool top)
                {
                    int n=points.Count;points.AddRange(new[]{q0,q1,q2,q3});
                    faces.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                    var color=top?path.color:cliff.color;color.a=top?1:0;
                    for(int j=0;j<4;j++){colors.Add(color);surface.Add(new Vector2(0,top?0:1));}
                }
                Quad(v[0],v[3],v[2],v[1],true);
                Quad(v[4],v[5],v[6],v[7],false);
                for(int edge=0;edge<4;edge++)
                {
                    int next=(edge+1)%4;
                    var e0=v[edge];var e1=v[next];
                    var outward=Vector3.Cross((e1-e0).normalized,Vector3.up);
                    int cuts=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(e0,e1)/.72f));
                    Vector3 Middle(float t)
                    {
                        float wave=Mathf.Sin(t*6.3f+edge*1.7f)*Mathf.Sin(t*Mathf.PI);
                        return Vector3.Lerp(e0,e1,t)+Vector3.down*(.58f+wave*.11f)+outward*(wave*.075f);
                    }
                    for(int k=0;k<cuts;k++)
                    {
                        float s=k/(float)cuts,t=(k+1)/(float)cuts;
                        var upper0=Vector3.Lerp(e0,e1,s);var upper1=Vector3.Lerp(e0,e1,t);
                        var mid0=Middle(s);var mid1=Middle(t);
                        Quad(upper0,upper1,mid1,mid0,false);
                        Quad(mid0,mid1,upper1+bottom,upper0+bottom,false);
                    }
                }
                var shape=new Mesh{name=name+" · 厚岩肩断面"};shape.SetVertices(points);shape.SetTriangles(faces,0);
                shape.SetColors(colors);shape.SetUVs(1,surface);shape.RecalculateNormals();shape.RecalculateBounds();meshes.Add(shape);
                solid.GetComponent<MeshFilter>().sharedMesh=shape;
                // PathStones later disables the ground's painted paving below
                // individual cobbles. This raised top is a separate support:
                // it needs its own finish so it cannot become a flat tan card.
                var shoulderFinish=new Material(referenceTerrainMaterial){name=name+" · 独立石面"};
                shoulderFinish.SetVector("_PaverScale",Vector4.zero);
                shoulderFinish.SetColor("_PathColor",new Color(.88f,.70f,.53f));
                shoulderFinish.SetFloat("_MortarTone",.96f);shoulderFinish.SetFloat("_GroutWidth",1);
                materials.Add(shoulderFinish);
                solid.GetComponent<MeshRenderer>().sharedMaterial=shoulderFinish;
                FinishReferenceRampPavers(ground,a,b,side);
            }
        }
    }
}

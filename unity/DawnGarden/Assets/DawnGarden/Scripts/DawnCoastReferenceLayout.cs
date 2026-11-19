using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        // Reference-map coordinates: +X is screen-right and +Z goes inland.
        // The one existing editable plot remains in its original world frame.
        public const float ReferenceSeaLevel = -3.2f;
        public static readonly Vector3 ReferenceHome = new(-10,0,-24);
        // The interactive seedling belongs to the right edge of the middle-left
        // crop bed. Keep its real capsule off the narrow central through-route.
        public static readonly Vector3 ReferenceSeed = new(-11.85f,3.45f,-6.55f);
        public static readonly Vector3 ReferenceNurseryApproach = new(-10.4f,3.45f,-6.9f);
        public static readonly Vector3 ReferenceGreenhouse = new(-15,5.2f,11.5f);
        public static readonly Vector3 ReferenceGreenhousePad = new(-14.5f,5.2f,10.325f);
        public static readonly Vector2 ReferenceGreenhousePadHalf = new(7,5.875f);
        public static readonly float[] ReferenceNurseryLevels = {2.55f,3.45f,4.25f,5.2f};
        static readonly float[] ReferenceNurseryCanalZ = {-13.4f,-8.6f,-3.25f};
        public static readonly Vector3 ReferenceWorkshop = new(27.4f,.7f,-3.9f);
        public static readonly Vector3 ReferenceWorkshopCourt = new(20.5f,-.4f,-8);
        public static readonly Vector3 ReferenceWhiteCourt = new(11.8f,0,0);
        public static readonly Vector2 ReferenceWhiteCourtHalf = new(6.5f,5.6f);
        public static readonly Vector3 ReferenceBrickCourt = new(19.15f,-.4f,-2.25f);
        public static readonly Vector2 ReferenceBrickCourtHalf = new(13.85f,10.75f);
        public static readonly Vector3 ReferenceReservedPad = new(18,2.4f,16);
        public static readonly Vector2[] ReferenceReserveEdge =
        {
            new(7.5f,20.5f),new(7.5f,13),new(8.1f,11.3f),new(9.7f,10.1f),new(13.5f,9.5f),
            new(18,9.25f),new(22.5f,9.5f),new(26.3f,10.1f),new(27.9f,11.3f),new(28.5f,13),
            new(28.5f,20.5f),new(27.9f,21.5f),new(26.5f,22),new(9.5f,22),new(8.1f,21.5f)
        };
        public const float ReferenceObservatoryHeightScale=1.18f;
        public static readonly Vector3 ReferenceObservatory = new(20.8f,4,38.6f);
        static readonly Vector2 ReferenceObservatoryGroundHalf = new(9.24f,7.98f);
        static readonly Vector2 ReferenceObservatoryRimHalf = new(9.345f,8.085f);
        const float ReferenceObservatoryPlinthFoot=2.15f;
        public static readonly Vector3 ReferenceObservatoryApproach = new(14.4f,4,35.5f);
        public static readonly Vector3[] ReferenceObservatoryStairs =
        {
            ReferenceReservedPad,new(15.2f,2.4f,23.8f),new(15.2f,2.4f,26.1f),
            new(16.4f,4,31.9f),ReferenceObservatoryApproach
        };
        public static readonly Vector3 ReferenceGrotto = new(3,-2.6f,-21.5f);
        public static readonly Vector3 ReferenceFerry = new(24.96f,-2.7f,-18.04f);
        public static readonly Vector3 ReferencePierLanding = new(24.96f,-2.7f,-15.8f);
        public static readonly Vector3[] ReferencePierDescent =
        {
            ReferenceWorkshopCourt,new(20.5f,-.4f,-9.85f),new(28,-.4f,-9.85f),new(28,-.9f,-11.9f),
            new(26,-1.9f,-13.3f),ReferencePierLanding,ReferenceFerry
        };
        public static readonly Vector3[] ReferenceReserveStairs =
        {
            new(20.8f,-.4f,1.5f),new(23.5f,-.4f,4.8f),new(27.8f,1.25f,9.6f),
            new(27.8f,2.4f,13.6f),ReferenceReservedPad
        };
        // Shared with the open channel sculpted into central_ridge.fbx. Keep these
        // points unsmoothed: terrain, rock subtraction and visual edging use one survey.
        public const float ReferenceRidgePathHalfWidth=1.2f;
        public static readonly Vector3[] ReferenceRidgePath =
        {
            new(3.9f,0,-5.6f),new(3.9f,0,0),new(4,0,3),new(4,.1f,5.8f),
            new(6.3f,.5f,7.8f),new(7,1.25f,10),new(6.1f,2.4f,13),
            new(6.1f,3,17),new(7.8f,3.5f,21),new(11,4,25),
            new(12.6f,4,30.5f),ReferenceObservatoryApproach
        };
        public static readonly Vector3[] ReferenceRidgeApproach =
        {
            new(10.8f,2.4f,-17.6f),new(8.2f,2.45f,-16),new(6.5f,2.2f,-15),
            new(3.5f,.8f,-10),ReferenceRidgePath[0]
        };
        public static readonly Vector3[] ReferenceBridgeCourtDescent =
        {
            new(10.8f,2.4f,-17.6f),new(13.8f,.8f,-15.8f),new(16.2f,-.4f,-13.4f),
            new(16.2f,-.4f,-10.2f),ReferenceWorkshopCourt
        };
        public static readonly Vector3[] ReferenceForeshoreWalk =
        {
            ReferenceHome,new(-16.8f,0,-27.3f),new(-16.8f,0,-31.7f),new(-15.9f,0,-33.3f),
            new(-13.9f,0,-34),new(-11,-1.25f,-35.8f),new(-8,-2.6f,-37.4f),
            new(-5.1f,-2.68f,-35.95f)
        };
        // Keep the original destination, approaching the stair from its downhill face.
        // The direct diagonal from the west ran beneath the closed stair's side wall.
        public static readonly Vector3[] ReferenceForeshoreLegacyArc =
        {
            new(-13,-2.6f,-38.5f),new(-10.8f,-2.6f,-39.3f),new(-7.4f,-2.6f,-39.3f),
            new(-6.765f,-2.6f,-38.059f),ReferenceForeshoreWalk[6]
        };
        public static readonly Vector3 ReferenceShoreExit = new(6,-2.8f,-28);
        public const float ReferenceBridgeRise=.80f;
        public const float ReferenceBridgeRailScale=1.15f;
        public static readonly Vector3 ReferenceWestLanding = new(-6.3f,2.2f,-21);
        public static readonly Vector3 ReferenceBridgeStart = new(-2.55f,3,-19.84f);
        public static readonly Vector3 ReferenceBridgeEnd = new(6.79f,3,-19.84f);
        public static readonly Vector3 ReferenceBridgeMiddle = (ReferenceBridgeStart+ReferenceBridgeEnd)*.5f+Vector3.up*ReferenceBridgeRise;
        public static readonly Vector3 ReferenceEastLanding = new(10.8f,2.4f,-17.6f);
        public static readonly Vector3 ReferenceOverview = new(6,0,-10.4f);
        public static readonly Vector3 ReferenceCameraEye = new(6,27,-68);
        public static readonly Vector3[] ReferenceNavigationNodes =
        {
            ReferenceHome,ReferenceNurseryApproach,ReferenceWorkshop,ReferenceGrotto,
            ReferenceFerry,ReferenceObservatoryApproach,ReferenceReservedPad
        };
        public static readonly Vector3[] ReferenceCaveSteps =
        {
            ReferenceBridgeEnd,ReferenceEastLanding,new(14.8f,.7f,-19.9f),new(16,-.8f,-23.5f),
            new(14.5f,-2.6f,-27.8f),new(11,-2.6f,-28.4f),new(8,-2.6f,-27.8f),
            new(6,-2.6f,-24.8f),ReferenceGrotto
        };
        public static readonly Vector3[] ReferenceStream =
        {
            new(-2.6f,7.30f,20),new(-3.5f,7.25f,16.8f),new(-3.8f,6.25f,15.7f),
            new(-4.4f,6.20f,12),new(-4.7f,5.12f,10.7f),
            new(-5,5.12f,7),new(-4.6f,5.08f,3),new(-4.8f,4.17f,1.7f),
            new(-4.2f,4.13f,-2),new(-3.8f,3.37f,-3.1f),new(-3.2f,3.33f,-6.5f),
            new(-2.2f,2.47f,-7.8f),new(-1.4f,2.27f,-10.5f),new(-.4f,2.15f,-12.5f),
            new(.6f,2,-15),new(1.4f,1.88f,-17),new(.4f,1.78f,-18.4f),
            new(.4f,1.61f,-21),new(.4f,1.43f,-25.02f),new(.4f,ReferenceSeaLevel+.015f,-25.45f),
            new(1,-3.08f,-27),new(2.2f,ReferenceSeaLevel,-30)
        };
        public static readonly float[] ReferenceStreamHalfWidths =
        {
            .80f,.85f,.78f,.88f,.72f,
            .36f,.39f,.43f,.45f,.48f,.50f,.65f,.78f,.86f,.89f,.96f,.78f,.72f,.70f,.95f,2.15f,3.3f
        };
    }
}

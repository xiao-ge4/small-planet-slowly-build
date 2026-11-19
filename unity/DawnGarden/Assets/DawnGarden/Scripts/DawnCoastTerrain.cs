using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        public static readonly Vector3 Nursery = ReferenceNurseryApproach;
        public static readonly Vector3 Workshop = ReferenceWorkshopCourt;
        public static readonly Vector3 Grotto = ReferenceGrotto;
        public static readonly Vector3 Ferry = ReferenceFerry;
        public static readonly Vector3 StarMap = ReferenceObservatoryApproach;
        public static readonly Vector3 Reserve = ReferenceReservedPad;
        public static readonly Vector3 Overview = ReferenceOverview;
        public static readonly string[] ZoneNames = { "回家小院", "水渠苗圃", "共创工坊", "潮洞", "渡口", "星图台", "预留扩建台地" };
        public static readonly Vector3[] ZonePoints = { ReferenceHome, Nursery, Workshop, Grotto, Ferry, StarMap, Reserve };

        internal const float CoastSeaLevel = ReferenceSeaLevel;
        public const string ConnectedTerrainName = "参考地图 · 连续地表与地块承接";
        public static readonly Vector3 SampleBridgeStart = ReferenceBridgeStart;
        public static readonly Vector3 SampleBridgeEnd = ReferenceBridgeEnd;
        public static readonly Vector3 SampleBridgeMiddle = ReferenceBridgeMiddle;
        public static readonly Vector3 SampleShoreExit = ReferenceShoreExit;
        public static readonly Vector3 SampleGrottoOffset = new(7,0,-1.5f);
        internal static readonly Vector3[] SampleCaveSteps = ReferenceCaveSteps;

        // The nursery races, the gorge, the tidal pool and the ocean share this elevation survey.
        internal static readonly Vector3[] RiverProfile =
        {
            new(-12.5f,4.29f,-32.9f), new(-11.8f,3.92f,-28.5f), new(-9.5f,2.75f,-23),
            new(-6.2f,2.50f,-20.2f), new(-1.2f,2.36f,-20.8f), new(7.4f,2.30f,-20.2f),
            new(12.6f,2.25f,-12),new(21.8f,2.17f,-4),new(23.6f,2.10f,1.8f),new(24,2.04f,5.7f),
            new(22.8f,2.0f,7.3f),new(20,1.95f,7.8f),new(17.5f,1.85f,8.3f),
            new(17.3f,1.60f,11.1f),new(14,-.83f,14.2f),new(11.6f,-.92f,16.5f),new(11.6f,CoastSeaLevel,23.3f),
        };

        internal static readonly float[] RiverHalfWidths =
        {
            .52f,.62f,.78f,.88f,1.50f,1.75f,.90f,1.12f,1.08f,1.06f,1.04f,1.07f,1.08f,1.14f,2.04f,1.55f,2.48f
        };

        // Closed shoreline in world metres. Broad rock headlands and the nursery slope are part of
        // the same surface as the lower shore; these are the limits of the dry land, not raised walls.
        static readonly Vector2[] CoastOutline =
        {
            new(-9,20), new(-13,16), new(-15,9), new(-13,2), new(-14,-4), new(-14,-11),
            new(-24,-13), new(-30,-17), new(-34,-23), new(-35,-32), new(-32,-38), new(-27,-41),
            new(-21,-40), new(-15,-45), new(-8,-46), new(-2,-43), new(7,-46), new(17,-45),
            new(24,-48), new(33,-48), new(41,-45), new(47,-39), new(48,-32), new(46,-26),
            new(50,-21), new(50,-14), new(47,-7), new(50,-2), new(51,5), new(49,11),
            new(46,14), new(44,19), new(39,22), new(32,23.5f), new(27,22), new(21,24),
            new(16,24.5f), new(11,23), new(7.7f,22), new(4.8f,21), new(1,21.4f),
            new(-3,23.4f), new(-8,22),
        };

        // Flat areas are limited to real uses (pool/courtyard, nursery beds, build plots, workshop,
        // observatory and the dock abutment). Their feathered shoulders are welded to the slopes.
        static readonly CoastPad[] CoastPads =
        {
            new(new(-1,0,.9f), new(5.8f,6.6f), 3, 0),
            new(new(-19.7f,2.6f,-22.4f), new(6.5f,7.1f), 3.1f, 1),
            new(new(-22.8f,4.10f,-33.5f), new(5.75f,4.45f), 3, 1),
            new(new(27,.12f,-4.3f), new(7.8f,6.8f), 3.2f, 2),
            new(new(28,2,-20.2f), new(7f,5.05f), 2.6f, 1),
            new(new(31,5.6f,-36.3f), new(6.65f,6.55f), 4.5f, 3),
            new(new(17,-.42f,15), new(4.3f,3.7f), 2.4f, 3),
            new(new(39,-.40f,12.1f), new(6.8f,4.45f), 3.9f, 4),
        };

        // The y components are surveyed road grades. These deform the one shared ground mesh so
        // the paving can be a ground material, with transitions into the uphill and downhill banks.
        static readonly CoastTrack[] CoastTracks =
        {
            new(1.8f,new(-4,.015f,-3),new(-8,.13f,-6),new(-10.4f,1.01f,-8.8f),
                new(-12.55f,2.60f,-12.1f),new(-14.5f,2.60f,-16.1f),Nursery),
            new(1.25f,new(-21.7f,2.60f,-24.7f),new(-27.2f,2.62f,-27.3f),new(-30.75f,3.36f,-30.1f),
                new(-29.4f,4.10f,-32.7f),new(-25,4.10f,-33.2f)),
            new(1.45f,new(-3,0,4),new(-4,.18f,8),new(-3,.65f,12),new(0,1.25f,14),new(4,2.2f,14),new(8,3.15f,12.6f),SampleBridgeStart),
            new(1.5f,SampleBridgeEnd,new(27,3.7f,10),new(32,2.5f,8),new(35,1.1f,4.9f),new(34,.12f,2.3f),new(31,.12f,0),Workshop),
            new(1.52f,new(22.1f,.14f,-7),new(20.25f,.48f,-12.25f),new(20.1f,1.25f,-15.7f),
                new(23.4f,2.0f,-19.9f),Reserve),
            new(1.6f,new(31,2.0f,-23.4f),new(36.2f,2.05f,-23.8f),new(39.7f,3.16f,-25.1f),
                new(42,4.20f,-28.5f),new(41.15f,5.3f,-32.1f),new(38.5f,5.6f,-35),StarMap),
            new(1.76f,Workshop,new(35,.12f,-4),new(40,.10f,1),new(41,-.10f,6),new(39,-.40f,11.5f)),
            new(1.62f,new(35,-.40f,14),new(30,-.40f,17.6f),new(23.3f,-.42f,18.7f),
                new(21,-.42f,18.6f),new(18.4f,-.42f,17.5f),Grotto,SampleShoreExit),
            new(1.48f,SampleBridgeEnd,new(25,3f,12.5f),new(25,1.5f,15.5f),new(23.3f,-.42f,18.7f)),
            new(1.55f,new(38.8f,-.40f,11.5f),new(42.5f,-.36f,12.1f),new(45.8f,-.15f,13),Ferry),
            new(1.4f,new(-5.45f,.0f,6.7f),new(-9.6f,-.15f,9.8f),new(-9,-.45f,14.6f),new(-5,-.63f,18)),
        };

        readonly struct CoastPad
        {
            public readonly Vector3 Center;
            public readonly Vector2 HalfSize;
            public readonly float Shoulder;
            public readonly int Surface;

            public CoastPad(Vector3 center, Vector2 halfSize, float shoulder, int surface)
            { Center = center; HalfSize = halfSize; Shoulder = shoulder; Surface = surface; }

            public float Weight(float x, float z, bool rockyShoulder)
            {
                float dx = Mathf.Abs(x - Center.x) / HalfSize.x;
                float dz = Mathf.Abs(z - Center.z) / HalfSize.y;
                float distance = Mathf.Pow(dx * dx * dx * dx + dz * dz * dz * dz, .25f);
                float breadth = Shoulder / Mathf.Min(HalfSize.x, HalfSize.y);
                if (!rockyShoulder) return 1 - Ease(1, 1 + breadth, distance);

                // Vary the shoulder in plan while holding the nursery and observatory floors in place.
                // The steep bands have a shelf between them, like exposed bedding cut by the stream.
                float angle = Mathf.Atan2(z - Center.z, x - Center.x);
                float rim = 1 + .055f * Mathf.Sin(angle * 3 + .4f) + .062f * Mathf.Cos(angle * 6);
                float toe = 1 + breadth * (1.42f + .36f * Mathf.Sin(angle * 2 + .74f));
                float side = Mathf.Clamp01((distance - rim) / (toe - rim));
                float firstFace = Ease(0, .35f, side);
                float secondFace = Ease(.44f, .88f, side);
                return 1 - (.44f * firstFace + .56f * secondFace);
            }
        }

        readonly struct CoastTrack
        {
            public readonly float HalfWidth;
            public readonly Vector3[] Points;
            public CoastTrack(float halfWidth, params Vector3[] points)
            { HalfWidth = halfWidth; Points = points; }
        }

        internal static Vector3 RiverWaterline(int segment, float fraction)
        {
            var a = RiverProfile[segment];
            var b = RiverProfile[segment + 1];
            var p = Vector3.Lerp(a, b, fraction);
            if (segment >= 1 && segment <= 4)
                p.y = Mathf.Lerp(a.y, b.y, Ease(.32f, .69f, fraction));
            if(segment==13)
                p.y=Mathf.Lerp(a.y,b.y,Ease(.12f,.34f,fraction));
            return p;
        }

        internal static float CoastHeight(float x, float z) => ReferenceHeight(x, z);

        static float SampleCoast(float x, float z, out int terrainSurface, out bool waterCovered,
            out float trackPaint, out float wetPaint, out float padPaint)
        {
            Vector2 point = new(x, z);
            float shore = DistanceFromShore(point);
            float strata = Mathf.PerlinNoise((x + 84) * .184f, (z + 61) * .087f);
            float stone = Mathf.PerlinNoise((x + 58) * .32f, (z + 55) * .31f);
            float foothill = Mathf.PerlinNoise((x + 91) * .086f, (z + 96) * .071f);
            float y = -.59f + .65f * foothill + (strata - .5f) * .33f + (stone - .5f) * .17f;
            y += Hill(x, z, -18,-32,22,20,3.08f);
            y += Hill(x, z, 29,-37,24,19,3.0f);
            y += Hill(x, z, -7,-20,15,18,1.93f);
            y += Hill(x, z, 32,-21,16,16,1.19f);
            y += Hill(x, z, -2,-35,14,8,1.44f);
            y += Hill(x, z, -9,-9,7.8f,12,1.32f);
            // The island is a cut coastal massif, not an empty lawn. The functional floors and
            // surveyed paths below are flattened back into these ridges, producing real cuttings.
            y += Hill(x, z, -1.5f,-36.5f,15.5f,12.2f,3.1f);
            y += Hill(x, z, 12.0f,-28.5f,10.1f,12.8f,2.4f);
            y += Hill(x, z, -30.2f,-35.6f,8.5f,10.6f,2.1f);
            y += Hill(x, z, -8.8f,-19.9f,7.2f,10.7f,.72f);
            y += Hill(x, z, 40.9f,-15.1f,8.6f,11.2f,2.2f);
            y += Hill(x, z, 33.5f,5.1f,7.5f,5.2f,.85f);
            y += Hill(x, z, 12.5f,-14.8f,8.3f,7.5f,2.1f);

            terrainSurface = shore < 2.7f ? 4 : 0;
            padPaint = 1 - Ease(.60f, 3.05f, shore);
            for (int i = 0; i < CoastPads.Length; i++)
            {
                var pad = CoastPads[i];
                bool rocky = i == 1 || i == 2 || i == 5;
                float w = pad.Weight(x, z, rocky);
                y = Mathf.Lerp(y, pad.Center.y, w);
                if (w > .58f) terrainSurface = pad.Surface;
                if (w > .28f) padPaint = Mathf.Max(padPaint, Ease(.28f, .88f, w));
                if (rocky && w > .10f && w < .79f)
                {
                    float folds = Mathf.Sin((x + z * .61f) * 2.2f) * .08f + (strata - .5f) * .26f;
                    y += folds * Mathf.Sin(Mathf.PI * w);
                    terrainSurface = 6;
                }
            }

            float closest = float.PositiveInfinity;
            float target = y;
            float trackWidth = 0;
            float gradeSum=0,gradeWeight=0;
            foreach (var track in CoastTracks)
            {
                for (int segment = 0; segment < track.Points.Length - 1; segment++)
                {
                    var a = track.Points[segment];
                    var b = track.Points[segment + 1];
                    float distance = SegmentDistanceSquared(point, a, b, out float t);
                    float d=Mathf.Sqrt(distance);
                    float blend=Mathf.Pow(1-Ease(track.HalfWidth*.3f,track.HalfWidth+3f,d),3)/(distance+.06f);
                    gradeSum+=Mathf.Lerp(a.y,b.y,t)*blend;
                    gradeWeight+=blend;
                    if (distance >= closest) continue;
                    closest = distance;
                    target = Mathf.Lerp(a.y, b.y, t);
                    trackWidth = track.HalfWidth;
                }
            }
            float trackDist = Mathf.Sqrt(closest);
            if(gradeWeight>.0001f)target=gradeSum/gradeWeight;
            trackPaint = 1 - Ease(trackWidth*.70f - .19f, trackWidth*.70f + .23f, trackDist + (stone - .5f) * .24f);
            if (trackDist < trackWidth + 2.3f)
            {
                y = Mathf.Lerp(y, target, 1 - Ease(trackWidth - .16f, trackWidth + 2.3f, trackDist));
                if (trackDist < trackWidth*.70f - .08f + (stone - .5f) * .18f) terrainSurface = 5;
            }

            // This shoreline recess follows the shape of the existing rise-up land. The old
            // land, collider, waist and every local layout coordinate remain independent and intact.
            float bx = (x - 11.7f) / 6.15f;
            float bz = z / 5.15f;
            float angle = Mathf.Atan2(bz, bx);
            float deckRadius = 1 + .02f * Mathf.Sin(angle * 3) + .012f * Mathf.Cos(angle * 7);
            float bay = Mathf.Sqrt(bx * bx + bz * bz) / deckRadius;
            if (bay < 1.43f)
            {
                // Keep the bed below the existing movable plot and let the upper banks feather into
                // the new terrain. Leaving this lip rounded avoids an isolated vertical crater.
                float spread = 1.31f + .08f * Mathf.Sin(angle * 3 + .7f) + .03f * Mathf.Cos(angle * 5);
                float slope = Ease(.89f, spread, bay);
                y = Mathf.Lerp(-1.84f, y, slope);
                if (slope < .77f) terrainSurface = 6;
                // Shore-side overlap on the original flat waist lets the baked navigation reach
                // the parcel when unlocked. The underside never creates a path into a locked parcel.
                float lip = (1 - Ease(5.30f, 5.85f, x)) * (1 - Ease(2.32f, 2.93f, Mathf.Abs(z)));
                if (x < 5.85f && lip > 0 && bay > .979f) y = Mathf.Lerp(y, .012f, lip);
            }
            // Keep the unopened parcel a low coastal recess, not a circular mine inside raised
            // ramparts. The approach now rises on the seaward side; north/west shoulders sit at
            // the same level as the original home and soften into the surrounding meadow.
            if(bay>.979f&&bay<1.74f&&z<7.8f&&x<19.1f)
            {
                float collar=Ease(.979f,1.075f,bay)*(1-Ease(1.35f,1.74f,bay));
                float rimHeight=Mathf.Lerp(-.65f,.06f,Ease(.979f,1.18f,bay));
                y=Mathf.Lerp(y,Mathf.Min(y,rimHeight),collar);
            }

            float riverDist = float.PositiveInfinity;
            float riverY = CoastSeaLevel;
            float halfWidth = 1;
            for (int i = 0; i < RiverProfile.Length - 1; i++)
            {
                float distance = SegmentDistanceSquared(point, RiverProfile[i], RiverProfile[i + 1], out float t);
                if (distance >= riverDist) continue;
                riverDist = distance;
                riverY = RiverWaterline(i, t).y;
                halfWidth = Mathf.Lerp(RiverHalfWidths[i], RiverHalfWidths[i + 1], t);
            }
            float bank = Mathf.Sqrt(riverDist);
            float relativeBank = bank / halfWidth;
            float caveProgress=Mathf.InverseLerp(8.6f,16.3f,z);
            bool caveInterior=z>8.6f&&z<16.3f&&Mathf.Abs(x-17.2f)<Mathf.Lerp(3.35f,5.6f,caveProgress);
            if(caveInterior)y=-.42f;
            float bankShoulder=caveInterior?.38f:2.25f;
            if (bank < halfWidth + bankShoulder)
            {
                float lowBank = Mathf.Lerp(riverY - .46f, riverY - .105f, Ease(.08f, .75f, relativeBank));
                float upperBank = Mathf.Lerp(riverY - .105f, riverY + .18f, Ease(.75f, 1.05f, relativeBank));
                float profile = relativeBank < .75f ? lowBank : upperBank;
                y = Mathf.Lerp(profile, y, Ease(halfWidth * 1.05f, halfWidth + bankShoulder, bank));
                if (relativeBank < 1.32f) terrainSurface = 6;
            }
            if (relativeBank >= 1.05f)
            {
                float westAbutment = (point - new Vector2(SampleBridgeStart.x, SampleBridgeStart.z)).magnitude;
                float eastAbutment = (point - new Vector2(SampleBridgeEnd.x, SampleBridgeEnd.z)).magnitude;
                var abutment = westAbutment < eastAbutment ? SampleBridgeStart : SampleBridgeEnd;
                float abutmentWeight = 1 - Ease(1.32f, 2.03f, Mathf.Min(westAbutment, eastAbutment));
                y = Mathf.Lerp(y, abutment.y - .014f, abutmentWeight);
                if (abutmentWeight > .51f) terrainSurface = 5;
                trackPaint = Mathf.Max(trackPaint, abutmentWeight);
            }

            // The original expansion bay remains reserved; the obsolete inlet is removed
            // so the new west bridge ramp has continuous ground beneath it.
            bool inletCovered=false;

            // The original usable pool and its under-water blocker are preserved at their original position.
            float poolX = (x - 2.7f) / 1.10f;
            float poolZ = (z - 1.65f) / .91f;
            float pond = Mathf.Sqrt(poolX * poolX + poolZ * poolZ);
            if (pond < 1.14f) y = Mathf.Min(y, Mathf.Lerp(-.18f, y, Ease(.78f, 1.14f, pond)));

            float shoreBlend = Ease(-2.75f, 3.45f, shore + (stone - .5f) * .65f);
            float shoreHeight = CoastSeaLevel + (shore + .12f) * .19f;
            y = Mathf.Lerp(Mathf.Max(-2.48f, shoreHeight - (1 - shoreBlend) * .35f), y, shoreBlend);
            // A broad irregular shingle/sand fan enters the sea under a few centimetres of
            // shallow water. The walking landing is protected and the deeper channel is retained.
            float shoalX=(x-11.6f)/7.8f,shoalZ=(z-23.2f)/5.8f;
            float shoal=Mathf.Sqrt(shoalX*shoalX+shoalZ*shoalZ)+.035f*Mathf.Sin(x*1.3f+z*.71f);
            float exitDistance=Vector2.Distance(point,new Vector2(SampleShoreExit.x,SampleShoreExit.z));
            float beachBlend=(1-Ease(.62f,1,shoal))*Ease(18.5f,21,z)*Ease(1.5f,3.3f,exitDistance);
            float lowBeach=-.72f-(z-19.5f)*.14f+(stone-.5f)*.12f;
            y=Mathf.Lerp(y,Mathf.Min(y,lowBeach),beachBlend);
            if(beachBlend>.25f){terrainSurface=4;padPaint=Mathf.Max(padPaint,beachBlend);}
            // The bridge abutment may not bury the upper stair run. Carve a continuous bed
            // slightly below the exact same profile used by the ramp collision, including turns.
            float stairDistance=float.PositiveInfinity,stairGrade=0;
            for(int segment=0;segment<SampleCaveSteps.Length-1;segment++)
            {
                float distance=SegmentDistanceSquared(point,SampleCaveSteps[segment],SampleCaveSteps[segment+1],out float t);
                if(distance>=stairDistance)continue;
                stairDistance=distance;stairGrade=Mathf.Lerp(SampleCaveSteps[segment].y,SampleCaveSteps[segment+1].y,t);
            }
            float stairBed=1-Ease(1.31f,1.95f,Mathf.Sqrt(stairDistance));
            y=Mathf.Lerp(y,Mathf.Min(y,stairGrade-.055f),stairBed);
            waterCovered = y < CoastSeaLevel + .085f || relativeBank < .955f && y < riverY + .018f || bay <= .979f || inletCovered;
            wetPaint = Mathf.Max(1 - Ease(CoastSeaLevel + .14f, CoastSeaLevel + .87f, y),
                (1 - Ease(.84f, 1.31f, relativeBank)) * (1 - Ease(.10f, .77f, y - riverY)));
            return y;
        }

        static float Hill(float x, float z, float cx, float cz, float rx, float rz, float height)
        {
            float dx = (x - cx) / rx;
            float dz = (z - cz) / rz;
            float q = Mathf.Clamp01(1 - dx * dx - dz * dz);
            return height * q * q;
        }

        static float Ease(float start, float end, float value)
        {
            float t = Mathf.Clamp01((value - start) / (end - start));
            return t * t * (3 - 2 * t);
        }

        static float DistanceFromShore(Vector2 point)
        {
            bool inside = false;
            float distance = float.PositiveInfinity;
            int last = CoastOutline.Length - 1;
            for (int i = 0; i < CoastOutline.Length; i++)
            {
                var a = CoastOutline[i];
                var b = CoastOutline[last];
                float t = Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude);
                distance = Mathf.Min(distance, (point - Vector2.Lerp(a, b, t)).sqrMagnitude);
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
                last = i;
            }
            return Mathf.Sqrt(distance) * (inside ? 1 : -1);
        }

        static float SegmentDistanceSquared(Vector2 point, Vector3 a, Vector3 b, out float fraction)
        {
            Vector2 start = new(a.x, a.z), delta = new(b.x - a.x, b.z - a.z);
            fraction = Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude);
            return (point - start - delta * fraction).sqrMagnitude;
        }

        void BuildConnectedTerrain()
        {
            const int rows = 226, columns = 267;
            const float spacing = .36f, minimumX = -40.6f, minimumZ = -51.8f;
            var verts = new List<Vector3>(rows * columns);
            var uv = new List<Vector2>(rows * columns);
            var surface = new byte[rows * columns];
            var isWater = new bool[rows * columns];
            var paintColors = new List<Color>(rows * columns);
            var paintWeights = new List<Vector2>(rows * columns);
            var basePaint = new[] { grass, limestone, brick, slate, sandstone, path, cliff };
            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                float px = minimumX + x * spacing;
                float pz = minimumZ + z * spacing;
                float py = SampleCoast(px, pz, out int paint, out bool wet, out float trackPaint, out float wetPaint, out float padPaint);
                verts.Add(new Vector3(px, py, pz));
                uv.Add(new Vector2(px * .31f, pz * .31f + py * .24f));
                surface[verts.Count - 1] = (byte)paint;
                isWater[verts.Count - 1] = wet;
                var color = basePaint[paint].color;
                if (paint >= 1 && paint <= 4) color = Color.Lerp(grass.color, color, padPaint);
                paintColors.Add(new Color(color.r, color.g, color.b, trackPaint));
                paintWeights.Add(new Vector2(wetPaint, paint == 6 ? 1 : 0));
            }

            // Road paint belongs to the walkable upper surface, never a vertical retaining
            // face. Colour steep cuttings as exposed rock rather than stretching bricks down them.
            for(int z=1;z<rows-1;z++)
            for(int x=1;x<columns-1;x++)
            {
                int at=z*columns+x;
                float dx=(verts[at+1].y-verts[at-1].y)/(spacing*2);
                float dz=(verts[at+columns].y-verts[at-columns].y)/(spacing*2);
                float slope=Mathf.Sqrt(dx*dx+dz*dz);
                var pigment=paintColors[at];
                pigment.a*=1-Ease(.65f,1.0f,slope);
                if(slope>.95f&&!isWater[at])
                {
                    float rock=Ease(.95f,1.55f,slope);
                    var color=Color.Lerp(pigment,cliff.color,rock);
                    color.a=pigment.a; pigment=color;
                    surface[at]=6;
                }
                paintColors[at]=pigment;
            }

            var triangles = new List<int>[7];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = new List<int>();
            var colliderTriangles = new List<int>();
            for (int z = 0; z < rows - 1; z++)
            for (int x = 0; x < columns - 1; x++)
            {
                int a = z * columns + x, b = a + 1, c = a + columns, d = c + 1;
                AddCoastTriangle(verts, triangles, colliderTriangles, surface, isWater, a, c, d);
                AddCoastTriangle(verts, triangles, colliderTriangles, surface, isWater, a, d, b);
            }

            var mesh = new Mesh { name = ConnectedTerrainName, indexFormat = IndexFormat.UInt32, subMeshCount = triangles.Length };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, paintWeights);
            mesh.SetColors(paintColors);
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes.Add(mesh);

            var collision = new Mesh { name = "海岸干岸碰撞网格", indexFormat = IndexFormat.UInt32 };
            collision.SetVertices(verts);
            collision.SetTriangles(colliderTriangles, 0);
            collision.RecalculateBounds();
            meshes.Add(collision);

            var surfaceObject = Group(ConnectedTerrainName, ground).gameObject;
            surfaceObject.layer = FloorLayer;
            surfaceObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var surfaceShader = Resources.Load<Shader>("Coast/SampleGround");
            if (surfaceShader != null)
            {
                var blended = new Material[basePaint.Length];
                for (int i = 0; i < basePaint.Length; i++)
                {
                    var material = new Material(surfaceShader) { name = basePaint[i].name + " · 岸坡混合", color = basePaint[i].color };
                    material.SetColor("_StoneColor", cliff.color);
                    material.SetColor("_PathColor", path.color);
                    blended[i] = material;
                    materials.Add(material);
                }
                basePaint = blended;
            }
            surfaceObject.AddComponent<MeshRenderer>().sharedMaterials = basePaint;
            surfaceObject.AddComponent<MeshCollider>().sharedMesh = collision;
            BuildCoastLandformArt();
            BuildCoastStairFlight();
        }

        static void AddCoastTriangle(List<Vector3> verts, List<int>[] render, List<int> collider,
            byte[] surface, bool[] water, int a, int b, int c)
        {
            if (Mathf.Max(verts[a].y, verts[b].y, verts[c].y) < CoastSeaLevel - .82f) return;
            var center=(verts[a]+verts[b]+verts[c])/3;
            float caveT=Mathf.InverseLerp(8.6f,16.3f,center.z);
            bool authoredCaveBank=center.z>8.6f&&center.z<16.3f&&
                Mathf.Abs(center.x-17.2f)<Mathf.Lerp(3.35f,5.6f,caveT)&&
                Mathf.Max(verts[a].y,verts[b].y,verts[c].y)>-.15f;
            if(center.z>8.6f&&center.z<16.3f)
            {
                var point=new Vector2(center.x,center.z);
                for(int segment=12;segment<=14;segment++)
                {
                    float dist=SegmentDistanceSquared(point,RiverProfile[segment],RiverProfile[segment+1],out float t);
                    float width=Mathf.Lerp(RiverHalfWidths[segment],RiverHalfWidths[segment+1],t);
                    if(dist<(width+.47f)*(width+.47f))authoredCaveBank=true;
                }
            }
            int paint = surface[a] == surface[b] || surface[a] == surface[c] ? surface[a] : surface[b] == surface[c] ? surface[b] : surface[a];
            float rise = Mathf.Max(verts[a].y, verts[b].y, verts[c].y) - Mathf.Min(verts[a].y, verts[b].y, verts[c].y);
            if (paint == 0 && rise > .22f) paint = 6;
            if(!authoredCaveBank&&!ReplacedByCoastCliffShoulder(center))
            {
                render[paint].Add(a);
                render[paint].Add(b);
                render[paint].Add(c);
            }
            if (water[a] || water[b] || water[c]) return;
            collider.Add(a);
            collider.Add(b);
            collider.Add(c);
        }
    }
}

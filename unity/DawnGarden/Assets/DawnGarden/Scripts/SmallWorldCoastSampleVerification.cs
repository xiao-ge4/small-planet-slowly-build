using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        // The comparison camera is the same one used for the v7 aerial.
        static readonly Vector3 SampleOverviewEye = DawnCoast.ReferenceCameraEye;
        static readonly Vector3 SampleOverviewFocus = DawnCoast.ReferenceOverview;
        [Serializable] sealed class CoastRouteTrace
        {
            public CoastRouteLeg[] legs;
        }
        [Serializable] sealed class CoastRouteLeg
        {
            public string name;
            public Vector3 destination;
            public float distance;
            public Vector3[] points;
        }
        readonly List<CoastRouteLeg> sampleWalk = new();
        [Serializable] sealed class CoastNavDiagnostic
        {
            public Vector3 origin,target;
            public Vector3[] vertices,corners;
            public int[] triangles;
            public string status;
        }

        IEnumerator VerifyCoastSample() => RunHomecomingChecks(WalkCoastSample(), "coast-sample-report.json");

        IEnumerator PreviewCoastGallery()
        {
            if(string.IsNullOrEmpty(SmallWorldProfile.TestRoot))throw new InvalidOperationException("A coast preview requires its isolated profile.");
            yield return BeginJourney("dawn");
            yield return UntilV1(()=>coast!=null && Connected && !restoringOwnedLayout && !loading,45);
            Game.atmosphere.Preview(8.4f);
            Game.orbit.overview=true;Game.orbit.yaw=0;Game.orbit.pitch=18;Game.orbit.distance=100;
            Toast("晨光中的海岸。滚轮拉近，按 F 跟随精灵。");
        }

        IEnumerator WalkCoastSample()
        {
            var args = Environment.GetCommandLineArgs();
            bool gray = args.Contains("-smallWorldCoastGraybox");
            var directory = SmallWorldProfile.TestRoot;
            var expected = Path.Combine(directory,"expected-layout.json");
            bool legacy = File.Exists(expected);
            File.WriteAllText(Path.Combine(directory,"sample-route.json"), JsonUtility.ToJson(new CoastRouteTrace { legs = Array.Empty<CoastRouteLeg>() }, true));
            yield return EnterFromTitle("dawn");
            yield return UntilV1(() => coast != null && Connected && !restoringOwnedLayout && !loading, 45);
            CheckJourney(coast != null && Connected, "six districts loaded from isolated sample profile");
            CheckJourney(Resources.Load<Shader>("Coast/ReferenceSea").isSupported,"reference sea shader is supported by the player GPU");
            CheckJourney(Resources.Load<Shader>("Coast/SampleGlass").isSupported,"greenhouse glass shader is supported by the player GPU");
            CheckJourney(Resources.Load<Shader>("Coast/SampleGround").isSupported,"reference ground shader is supported by the player GPU");
            CheckJourney(Resources.Load<Shader>("Coast/ReferenceBackdrop").isSupported,"distant coast shader is supported by the player GPU");
            CheckJourney(Game.Progress.firstExpansionUnlocked == legacy, legacy ? "v7 expansion restored" : "fresh plot remains locked");
            CheckJourney(Vector3.Distance(Game.expansion.finalPosition,new Vector3(11.7f,0,0)) < .015f,
                "expansion retains its original world anchor");
            CheckJourney(Game.expansion.land.gameObject.activeSelf == legacy, "expansion terrain matches the saved state");
            Game.atmosphere.Preview(8.4f);
            yield return new WaitForSecondsRealtime(.25f);
            SaveCoastFrame("a-overview", SampleOverviewEye, SampleOverviewFocus, 42);
            SaveCoastFrame("a-perspective-a",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
            foreach(float pitch in new[]{24f,26f})
            {
                var focus=new Vector3(7.5f,0,-10.4f);
                float distance=105;
                var eye=focus+new Vector3(0,Mathf.Sin(pitch*Mathf.Deg2Rad)*distance,-Mathf.Cos(pitch*Mathf.Deg2Rad)*distance);
                SaveCoastFrame("a-pitch-"+pitch,eye,focus,20);
            }
            var framedFocus=new Vector3(7.5f,2,-10.4f);
            var framedEye=framedFocus+new Vector3(0,Mathf.Sin(22*Mathf.Deg2Rad)*110,-Mathf.Cos(22*Mathf.Deg2Rad)*110);
            SaveCoastFrame("a-pitch-22-framed",framedEye,framedFocus,20);
            var previousExposure=Game.atmosphere.daylightExposure;
            Game.atmosphere.daylightExposure=.70f;Game.atmosphere.RefreshNow();
            SaveCoastFrame("a-brighter-morning",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
            SaveCoastFrame("a-pitch-22-brighter",framedEye,framedFocus,20);
            Game.atmosphere.daylightExposure=previousExposure;Game.atmosphere.RefreshNow();
            yield return null;
            var daylightTint=Game.atmosphere.daytimeSunColor;
            Game.atmosphere.daytimeSunColor=new Color(1,.96f,.87f);Game.atmosphere.RefreshNow();
            SaveCoastFrame("a-neutral-sun",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
            Game.atmosphere.daytimeSunColor=daylightTint;Game.atmosphere.RefreshNow();
            yield return null;
            var seaDebug=GameObject.Find("Ocean")?.GetComponent<Renderer>()?.sharedMaterial;
            if(seaDebug!=null&&seaDebug.HasProperty("_DebugDepth"))
            {
                seaDebug.SetFloat("_DebugDepth",1);
                SaveCoastFrame("a-water-depth",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
                seaDebug.SetFloat("_DebugDepth",2);
                SaveCoastFrame("a-water-bed-color",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
                seaDebug.SetFloat("_DebugDepth",3);
                SaveCoastFrame("a-water-without-bed-color",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
                seaDebug.SetFloat("_DebugDepth",4);
                SaveCoastFrame("a-water-surveyed-depth",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
                seaDebug.SetFloat("_DebugDepth",5);
                SaveCoastFrame("a-water-shadow",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
                seaDebug.SetFloat("_DebugDepth",0);
                var seaBase=seaDebug.GetColor("_BaseColor");
                var seaShallow=seaDebug.GetColor("_ShallowColor");
                seaDebug.SetColor("_BaseColor",new Color(.10f,.32f,.44f));
                seaDebug.SetColor("_ShallowColor",new Color(.10f,.48f,.48f));
                SaveCoastFrame("a-water-soft-teal-study",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
                seaDebug.SetColor("_BaseColor",new Color(.09f,.37f,.47f));
                seaDebug.SetColor("_ShallowColor",new Color(.13f,.55f,.52f));
                SaveCoastFrame("a-water-warm-teal-study",new Vector3(7.5f,30.902f,-105.506f),new Vector3(7.5f,0,-10.4f),20);
                seaDebug.SetColor("_BaseColor",seaBase);
                seaDebug.SetColor("_ShallowColor",seaShallow);
            }
            SaveCoastFrame("a-perspective-d",new Vector3(7.5f,17,-78.4f),new Vector3(7.5f,0,-10.4f),35);
            SaveCoastFrame("a-perspective-b",new Vector3(7.5f,39.421f,-113.094f),new Vector3(7.5f,0,-10.4f),18.2f);
            SaveCoastFrame("a-perspective-c",new Vector3(7.5f,33.077f,-125.751f),new Vector3(7.5f,0,-10.4f),16.7f);
            SaveCoastFrame("a-composition",new Vector3(38,29,-55),new Vector3(5,1,-8),46);
            // Diagnostic frames are produced even when a navigation assertion fails later.
            SaveCoastFrame("static-garden",new Vector3(-20,16,-27),new Vector3(-10,3,0),50);
            SaveCoastFrame("static-greenhouse",new Vector3(-22,9.7f,.2f),new Vector3(-15,6.6f,11.5f),48);
            var glazing=coast.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.shader.name=="Dawn Garden/Coast Greenhouse Glass")).ToArray();
            File.WriteAllText(Path.Combine(directory,"greenhouse-glass.txt"),string.Join("\n",glazing.SelectMany(r=>r.sharedMaterials.Select(m=>r.name+" enabled="+r.enabled+" shader="+m.shader.name+" color="+m.GetColor("_Color")+" daylight="+m.GetFloat("_Daylight")))));
            SaveCoastFrame("static-bridge-cave",DawnCoast.Grotto+new Vector3(8,12,-20),DawnCoast.Grotto+new Vector3(0,2,0),48);
            SaveCoastFrame("static-grotto",DawnCoast.Grotto+new Vector3(0,2.4f,-8),DawnCoast.Grotto+new Vector3(0,1.8f,1),64);
            SaveCoastFrame("static-home",DawnCoast.ReferenceHome+new Vector3(2,11,-14),DawnCoast.ReferenceHome,53);
            SaveCoastFrame("static-foreshore",new Vector3(-10,4,-48),new Vector3(-11,-1.4f,-34.6f),58);
            SaveCoastFrame("static-workshop",new Vector3(28.8f,3.5f,-13.2f),new Vector3(27.4f,1.9f,-4.0f),52);
            var cliffGroup=coast.transform.Find("场景美术/南岸完整岩坎样板");
            if(cliffGroup!=null)
            {
                var cliffRenderers=cliffGroup.GetComponentsInChildren<Renderer>();
                foreach(var r in cliffRenderers)r.enabled=false;
                SaveCoastFrame("static-foreshore-terrain-cut",new Vector3(-10,4,-48),new Vector3(-11,-1.4f,-34.6f),58);
                foreach(var r in cliffRenderers)r.enabled=true;
                var other=coast.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!r.transform.IsChildOf(cliffGroup)).ToArray();
                foreach(var r in other)r.enabled=false;
                SaveCoastFrame("static-foreshore-solid",new Vector3(-10,4,-48),new Vector3(-11,-1.4f,-34.6f),58);
                foreach(var r in other)r.enabled=true;
            }
            var navNotes=new StringBuilder();
            foreach(var point in DawnCoast.ZonePoints.Concat(new[]{DawnCoast.SampleBridgeStart,DawnCoast.SampleBridgeMiddle,DawnCoast.SampleBridgeEnd,DawnCoast.SampleShoreExit}))
            {
                bool near=NavMesh.SamplePosition(point,out var contact,.7f,NavMesh.AllAreas);
                var routeNote=new NavMeshPath();
                if(near)NavMesh.CalculatePath(Game.actor.position,contact.position,NavMesh.AllAreas,routeNote);
                navNotes.AppendLine($"{point}: terrain={DawnCoast.CoastHeight(point.x,point.z):F3}, nav={near} at={contact.position}, path={routeNote.status}, corners={string.Join(";",routeNote.corners.Select(c=>c.ToString()))}");
            }
            File.WriteAllText(Path.Combine(directory,"navigation-diagnostics.txt"),navNotes.ToString());
            var grid=new StringBuilder("x,z,ground,nav,navY,connected\n");
            for(float z=-31;z<=-12;z+=.5f)for(float x=-5;x<=33;x+=.5f)
            {
                float y=DawnCoast.CoastHeight(x,z);bool found=NavMesh.SamplePosition(new Vector3(x,y,z),out var at,.4f,NavMesh.AllAreas);
                var path=new NavMeshPath();bool connected=found&&NavMesh.CalculatePath(Game.actor.position,at.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                grid.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5}\n",x,z,y,found?1:0,found?at.position.y:999,connected?1:0);
            }
            for(float z=-15;z<=-5;z+=.2f)for(float x=-11.6f;x<=-9.2f;x+=.2f)
            {
                float y=DawnCoast.CoastHeight(x,z);bool found=NavMesh.SamplePosition(new Vector3(x,y,z),out var at,.4f,NavMesh.AllAreas);
                var path=new NavMeshPath();bool connected=found&&NavMesh.CalculatePath(Game.actor.position,at.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                grid.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5}\n",x,z,y,found?1:0,found?at.position.y:999,connected?1:0);
            }
            File.WriteAllText(Path.Combine(directory,"navigation-grid.csv"),grid.ToString());
            var pierPhysics=new StringBuilder($"bodyRadius={Game.body.radius},bodyHeight={Game.body.height}\nx,z,ground,nav,blocking_colliders\n");
            for(float z=-15;z<=-7.5f;z+=.5f)for(float x=20;x<=31;x+=.5f)
            {
                float y=DawnCoast.ReferenceHeight(x,z);float radius=Game.body.radius;
                var low=new Vector3(x,y+radius+.04f,z);var high=new Vector3(x,y+Game.body.height-radius,z);
                var touching=Physics.OverlapCapsule(low,high,radius,(1<<8)|(1<<9),QueryTriggerInteraction.Ignore);
                bool nav=NavMesh.SamplePosition(new Vector3(x,y,z),out var nh,.3f,NavMesh.AllAreas);
                var names=string.Join("|",touching.Select(c=>$"{c.name} parent={c.transform.parent?.name} Y={c.bounds.min.y:F2}..{c.bounds.max.y:F2}"));
                pierPhysics.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4}\n",x,z,y,nav?1:0,names);
            }
            File.WriteAllText(Path.Combine(directory,"pier-collision-grid.csv"),pierPhysics.ToString());
            CoastRoadPhysicsDiagnostics.Write(directory,Game.actor.position,Game.body.radius,Game.body.height);
            if (gray)
            {
                SaveCoastGraybox();
                ExportCoastTerrain();
            }
            foreach (var zone in DawnCoast.ZonePoints)
            {
                var route = new NavMeshPath();
                var found = NavMesh.SamplePosition(zone,out var onMesh,.70f,NavMesh.AllAreas);
                if (found) NavMesh.CalculatePath(Game.actor.position,onMesh.position,NavMesh.AllAreas,route);
                CheckJourney(found && route.status == NavMeshPathStatus.PathComplete,
                    $"reachable district {DawnCoast.ZoneNames[Array.IndexOf(DawnCoast.ZonePoints,zone)]} ({route.status} {route.corners.LastOrDefault()})");
            }

            if (legacy)
            {
                VerifySampleLayout(expected);
                yield return WalkSampleLeg("old expansion terrain", Game.expansion.WalkTarget);
                SaveCoastFrame("e-restored-expansion", new Vector3(5.8f,7.5f,13.5f), new Vector3(12,0,-.4f), 48);
                if (gray) yield break;
                Game.atmosphere.Preview(20.4f);
                yield return UntilV1(() => lampAnchor != null && Journey == JourneyStep.Light, 20);
                CheckJourney(lampAnchor != null && !Profile.homecomingLampLit && !homecomingGlow.enabled, "saved layout loads with gift lamp ready to light");
                yield return WalkSampleLeg("walk to tide cave gift",DawnCoast.Grotto);
                CheckJourney(TryNearbyHomecomingLamp(),"lit the gift from the actual cave walkway");
                yield return UntilV1(() => Profile.homecomingLampLit && homecomingGlow != null && homecomingGlow.intensity > 4, 16);
                CheckJourney(Profile.homecomingLampLit && homecomingGlow != null && homecomingGlow.enabled, "gift lamp lit in the cave");
                yield return new WaitForSecondsRealtime(.65f);
                // Use the same outside-the-mouth frame as the daytime close-up.
                // The former reverse angle is now inside the rebuilt rear wall.
                SaveCoastFrame("d-grotto-night",DawnCoast.Grotto+new Vector3(0,2.4f,-8),DawnCoast.Grotto+new Vector3(0,1.8f,1),64);
                var neighbor = AtResident(0,(Profile.ownContinent+1)%4);
                CheckJourney(neighbor != null,"existing route to the neighbor is available");
                yield return Visit(0,neighbor.continent);
                yield return Visit(0,Profile.ownContinent);
                yield return UntilV1(() => !restoringOwnedLayout && lampAnchor != null);
                Game.atmosphere.Preview(20.4f);
                yield return UntilV1(() => homecomingGlow != null && homecomingGlow.enabled);
                VerifySampleLayout(expected);
                CheckJourney(SmallWorldProfile.Load().homecomingLampLit && homecomingGlow != null && homecomingGlow.enabled,
                    "returning home restores the v7 layout and the newly lit gift");
                yield break;
            }
            if (gray) yield break;

            var encoder = args.FirstOrDefault(a => a.StartsWith("-smallWorldCoastEncoder=",StringComparison.Ordinal));
            SmallWorldMovieRecorder recorder = null;
            if (!string.IsNullOrEmpty(encoder))
            {
                recorder = gameObject.AddComponent<SmallWorldMovieRecorder>();
                recorder.Begin(Path.Combine(directory,"walk-video"),encoder.Substring("-smallWorldCoastEncoder=".Length));
                var listener = FindFirstObjectByType<AudioListener>();
                if (listener != null) recorder.AttachAudio(listener.gameObject);
            }

            yield return WalkSampleLeg("home courtyard → nursery", DawnCoast.Nursery);
            CheckJourney(Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Water),true),"nursery watering started");
            yield return UntilV1(() => GardenSave.WateredToday(Game.Progress) && !Game.Busy, 25);
            CheckJourney(GardenSave.WateredToday(Game.Progress),"nursery watered using the existing interaction");
            SaveCoastFrame("b-nursery",new Vector3(-8.6f,6.4f,-16.1f),DawnCoast.ReferenceSeed+Vector3.up*.65f,58);
            SaveCoastFrame("b-garden-terraces",new Vector3(-20,16,-27),new Vector3(-10,3,0),50);
            yield return WalkSampleLeg("nursery → greenhouse rear stair",new Vector3(-21.35f,5.2f,9.0f),32);
            yield return WalkSampleLeg("climbed the greenhouse rear garden",new Vector3(-21.3f,7.02f,15.15f),18);
            SaveCoastFrame("b-upper-garden",new Vector3(-25,10.6f,8),new Vector3(-18.5f,7,15.5f),55);
            yield return WalkSampleLeg("returned from the greenhouse rear garden",new Vector3(-21.35f,5.2f,9.0f),18);
            var bridgeDirection = (DawnCoast.SampleBridgeEnd - DawnCoast.SampleBridgeStart).normalized;
            yield return WalkSampleLeg("nursery → bridge approach", DawnCoast.SampleBridgeStart - bridgeDirection * .7f);
            SaveCoastFrame("c-bridge-approach",Game.actor.position+new Vector3(-2.7f,1.65f,3.45f),DawnCoast.SampleBridgeMiddle,60);
            yield return WalkSampleLeg("crossed wooden bridge", DawnCoast.SampleBridgeMiddle);
            SaveCoastFrame("c-bridge-waterline",DawnCoast.SampleBridgeMiddle+new Vector3(.35f,1.2f,4.5f),DawnCoast.SampleBridgeMiddle+new Vector3(-.75f,-.1f,-4.3f),63);
            yield return WalkSampleLeg("reached the opposite bridge abutment",DawnCoast.SampleBridgeEnd+bridgeDirection * .65f);
            yield return WalkSampleLeg("bridge → tide cave", DawnCoast.Grotto,32);
            SaveCoastFrame("d-grotto-day",DawnCoast.Grotto+new Vector3(0,2.4f,-8),DawnCoast.Grotto+new Vector3(0,1.8f,1),64);
            yield return WalkSampleLeg("tide cave → outfall bank", DawnCoast.SampleShoreExit);
            SaveCoastFrame("e-outfall", DawnCoast.Grotto+new Vector3(6.5f,2.4f,-9.5f), DawnCoast.Grotto+new Vector3(.3f,-.22f,-3.4f),57);
            if (recorder != null) yield return recorder.Finish();

            yield return WalkSampleLeg("returned uphill from the coast to the bridge",DawnCoast.SampleBridgeEnd+bridgeDirection*.65f,35);
            yield return WalkSampleLeg("returned to the courtyard",DawnCoast.ReferenceHome);
            yield return WalkSampleLeg("descended to the western foreshore",new Vector3(-13,-2.6f,-38.5f));
            SaveCoastFrame("e-western-foreshore-walk",Game.actor.position+new Vector3(1,3,-8),Game.actor.position+new Vector3(0,1,3),57);
            yield return WalkSampleLeg("returned from the western foreshore",DawnCoast.ReferenceHome);
            CheckJourney(Game.GoToInteraction(Game.FindInteraction(GardenInteractionKind.Chest),true),"chest route started after the shoreline return walk");
            yield return UntilV1(() => Game.Progress.chestOpened && !Game.Busy, 35);
            CheckJourney(Game.Progress.chestOpened,$"chest opened after return walk ({Game.actor.position}: {Game.Status})");
            CheckJourney(Game.Progress.chestOpened && Game.expansion.Accept(),"used the existing expansion permit");
            yield return UntilV1(() => !Game.expansion.IsRising, 23);
            CheckJourney(Game.Progress.firstExpansionUnlocked && Game.expansion.land.gameObject.activeSelf &&
                GardenSave.Load(Game.SavePath,Game.homeId).firstExpansionUnlocked, "expansion unlock persists and remains aligned");
            yield return WalkSampleLeg("walked onto the newly risen plot", Game.expansion.WalkTarget);
            SaveCoastFrame("f-rise-socket",new Vector3(5.8f,7.5f,13.5f),new Vector3(12,0,-.4f),48);
            yield return WalkSampleLeg("ridge path entrance",new Vector3(3.9f,0,0));
            yield return WalkSampleLeg("ridge path middle",new Vector3(6.1f,2.4f,13));
            yield return WalkSampleLeg("ridge path upper bend",new Vector3(7.8f,3.5f,21));
            yield return WalkSampleLeg("ridge path reaches star platform",DawnCoast.ReferenceObservatoryApproach);
            yield return WalkSampleLeg("star upper court stair head",new Vector3(16.4f,4,31.9f));
            yield return WalkSampleLeg("descended the upper court stone stairs",new Vector3(15.2f,2.4f,26.1f));
            yield return WalkSampleLeg("climbed the upper court stone stairs",new Vector3(16.4f,4,31.9f));
            yield return WalkSampleLeg("returned to star platform after stairs",DawnCoast.ReferenceObservatoryApproach);
            SaveCoastFrame("g-ridge-path",new Vector3(17,15,-2),new Vector3(4.5f,3,12),56);
            yield return WalkSampleLeg("ridge descent upper bend",new Vector3(7.8f,3.5f,21));
            yield return WalkSampleLeg("ridge descent middle",new Vector3(6.1f,2.4f,13));
            yield return WalkSampleLeg("ridge descent entrance",new Vector3(3.9f,0,0));
            yield return WalkSampleLeg("ridge path returns to bridge",DawnCoast.SampleBridgeEnd+bridgeDirection*.65f);
            yield return WalkSampleLeg("workshop court approach",DawnCoast.ReferenceWorkshopCourt);
            yield return WalkSampleLeg("workshop actual doorway",new Vector3(28.1756f,1.0269f,-6.7946f));
            yield return WalkSampleLeg("workshop counter aisle",new Vector3(27.5367f,1.0269f,-4.4103f));
            SaveCoastFrame("h-workshop-inside",Game.actor.position+new Vector3(.45f,1.60f,-1.15f),new Vector3(27.0f,2.0f,-2.1f),65);
            yield return WalkSampleLeg("workshop exit doorway",new Vector3(28.1756f,1.0269f,-6.7946f));
            yield return WalkSampleLeg("returned to workshop court",DawnCoast.ReferenceWorkshopCourt);
        }

        IEnumerator WalkSampleLeg(string name, Vector3 requested,float maxDistance=float.PositiveInfinity)
        {
            float readyDeadline=Time.realtimeSinceStartup+10;
            while(Game.ControlsBlocked&&Time.realtimeSinceStartup<readyDeadline)yield return null;
            bool sampled=NavMesh.SamplePosition(requested,out var hit,1.10f,NavMesh.AllAreas);
            bool started=sampled&&Game.WalkTo(hit.position);
            if(!started)
            {
                var diagnostic=new NavMeshPath();
                bool origin=NavMesh.SamplePosition(Game.actor.position,out var originHit,.8f,NavMesh.AllAreas);
                if(origin&&sampled)NavMesh.CalculatePath(originHit.position,hit.position,NavMesh.AllAreas,diagnostic);
                var triangulation=NavMesh.CalculateTriangulation();
                File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot,"failed-navigation.json"),JsonUtility.ToJson(new CoastNavDiagnostic{origin=Game.actor.position,target=hit.position,vertices=triangulation.vertices,triangles=triangulation.indices,corners=diagnostic.corners,status=diagnostic.status.ToString()}));
                Debug.LogError($"Coast leg failed: {name}; controlsBlocked={Game.ControlsBlocked}; busy={Game.Busy}; external={Game.externalControlsBlocked}; actor={Game.actor.position}; originNav={origin}; requested={requested}; sampled={sampled} hit={hit.position}; path={diagnostic.status}; status={Game.Status}");
            }
            CheckJourney(started, name+" started");
            var points = new List<Vector3>{Game.actor.position};
            Game.orbit.FocusPlayer();
            Game.orbit.yaw = 12;
            Game.orbit.pitch = 26;
            Game.orbit.distance = 9;
            float deadline = Time.realtimeSinceStartup+65;
            float next = 0;
            while (Game.Navigating && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= next)
                {
                    points.Add(Game.actor.position);
                    next = Time.realtimeSinceStartup+.30f;
                }
                yield return null;
            }
            points.Add(Game.actor.position);
            float length = 0;
            for (int i=1;i<points.Count;i++) length += Vector3.Distance(points[i-1],points[i]);
            sampleWalk.Add(new CoastRouteLeg { name=name, destination=hit.position, distance=length, points=points.ToArray() });
            File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot,"sample-route.json"),JsonUtility.ToJson(new CoastRouteTrace { legs=sampleWalk.ToArray() },true));
            CheckJourney(!Game.Navigating && Vector3.Distance(Game.actor.position,hit.position) < .48f && points.Min(p=>p.y) > DawnCoast.ReferenceSeaLevel+.12f && length<maxDistance,
                $"{name} reached by walking, {length:F1} m, ended at {Game.actor.position} ({Game.Status})");
        }

        void VerifySampleLayout(string expected)
        {
            var previous = File.ReadAllText(expected);
            CheckJourney(File.Exists(LayoutFile) && File.ReadAllText(LayoutFile) == previous, "legacy committed-layout file remains byte-identical");
            var original = JsonUtility.FromJson<SmallWorldLayout>(previous);
            CheckJourney(original.instances.Length > 0 && committedLayout.version == original.version && placedRoot != null,
                "legacy committed layout has loaded for rendering");
            foreach (var item in original.instances)
            {
                var node = placedRoot.Find(item.instanceId);
                CheckJourney(node != null && Vector3.Distance(node.localPosition,item.localPosition)<.002f &&
                    Quaternion.Angle(node.localRotation,Quaternion.Euler(item.localEulerAngles)) < .1f &&
                    Vector3.Distance(node.localScale,item.localScale)<.002f, $"restored original local transform for {item.instanceId}");
            }
        }

        void SaveCoastFrame(string name,Vector3 eye,Vector3 focus,float fieldOfView,int? mask=null)
        {
            var camera = new GameObject("海岸样板 · 验收机位").AddComponent<Camera>();
            camera.CopyFrom(Game.worldCamera);
            var effects = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            effects.renderPostProcessing = true;
            effects.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            effects.stopNaN = true;
            camera.enabled = false;
            camera.fieldOfView = fieldOfView;
            camera.orthographic=name=="a-overview" || name=="a-graybox";
            if(camera.orthographic)camera.orthographicSize=16.9f;
            if (mask.HasValue) camera.cullingMask = mask.Value;
            camera.transform.position = eye;
            camera.transform.LookAt(focus);
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var splits=pipeline!=null?pipeline.cascade4Split:Vector3.zero;
            if(pipeline!=null)pipeline.cascade4Split=Vector3.Distance(eye,focus)>50?new Vector3(.40f,.65f,.85f):new Vector3(.123f,.293f,.536f);
            try
            {
                File.WriteAllBytes(Path.Combine(SmallWorldProfile.TestRoot,name+".png"),CaptureHome(camera,1600,900));
            }
            finally { if(pipeline!=null)pipeline.cascade4Split=splits;Destroy(camera.gameObject); }
        }

        void SaveCoastGraybox()
        {
            const int grayLayer = 29;
            var copies = new GameObject("Solo terrain capture");
            var gray = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gray.color = new Color(.59f,.61f,.63f);
            var terrain = coast.transform.Find("可行走地形与路径");
            var sources = terrain.GetComponentsInChildren<MeshFilter>()
                .Where(m => m.name == DawnCoast.ConnectedTerrainName || m.name == "桥面通行坡").ToList();
            var ocean = GameObject.Find("Ocean")?.GetComponent<MeshFilter>();
            if (ocean != null) sources.Add(ocean);
            foreach (var source in sources)
            {
                var copy = new GameObject(source.name) { layer = grayLayer };
                copy.transform.SetParent(copies.transform);
                copy.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source == ocean ? source.GetComponent<Renderer>().sharedMaterials :
                    Enumerable.Repeat(gray,source.sharedMesh.subMeshCount).ToArray();
            }
            try { SaveCoastFrame("a-graybox",SampleOverviewEye,SampleOverviewFocus,42,1<<grayLayer); }
            finally { Destroy(copies); Destroy(gray); }
        }

        void ExportCoastTerrain()
        {
            var filter = coast.GetComponentsInChildren<MeshFilter>()
                .FirstOrDefault(item => item.name == DawnCoast.ConnectedTerrainName);
            if (filter == null) return;
            var mesh = filter.sharedMesh;
            var meshData = new StringBuilder("mtllib coast-terrain.mtl\no DawnCoastTerrain\n");
            var palette = new StringBuilder();
            foreach (var vertex in mesh.vertices)
            {
                var point = filter.transform.TransformPoint(vertex);
                meshData.AppendFormat(CultureInfo.InvariantCulture,"v {0:R} {1:R} {2:R}\n",point.x,point.z,point.y);
            }
            foreach(var uv in mesh.uv)
                meshData.AppendFormat(CultureInfo.InvariantCulture,"vt {0:R} {1:R}\n",uv.x,uv.y);
            var renderer = filter.GetComponent<Renderer>();
            for(int part = 0;part < mesh.subMeshCount;part++)
            {
                var triangles = mesh.GetTriangles(part);
                meshData.AppendFormat(CultureInfo.InvariantCulture,"g terrain_{0}\n",part+1);
                var surface = renderer.sharedMaterials.ElementAtOrDefault(part);
                if(surface != null)
                {
                    var name = "terrain_material_" + part;
                    var color = surface.color;
                    palette.AppendFormat(CultureInfo.InvariantCulture,"newmtl {0}\nKd {1:R} {2:R} {3:R}\n\n",name,color.r,color.g,color.b);
                    meshData.Append("usemtl ").Append(name).Append('\n');
                }
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i]+1,b=triangles[i+2]+1,c=triangles[i+1]+1;
                    meshData.AppendFormat(CultureInfo.InvariantCulture,"f {0}/{0} {1}/{1} {2}/{2}\n",a,b,c);
                }
            }
            File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot,"coast-terrain.obj"),meshData.ToString());
            File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot,"coast-terrain.mtl"),palette.ToString());
        }
    }
}

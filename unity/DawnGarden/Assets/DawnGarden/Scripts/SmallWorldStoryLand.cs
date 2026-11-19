using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace DawnGarden
{
    /// <summary>
    /// Reversible presentation of the existing coast after DawnCoast.Build.
    /// Recording state stays isolated; playable land follows real GardenProgress.
    /// Authored meshes, normal progress saves and baked navigation stay intact.
    /// </summary>
    public sealed partial class SmallWorldStoryLand : MonoBehaviour, IDisposable
    {
        // Spatial territory state, never a global material/building reveal stage.
        static bool sessionGuardianRoute;
        static StoryTerritoryLedger sessionLedger;
        static readonly Rect Courtyard = Rect.MinMaxRect(-22, -37, -2.8f, -17.8f);
        // The explored connection follows the existing navigation survey. Its
        // narrow soil tread is generated separately from the authored districts.
        readonly List<Rect> guardianRouteAreas = new List<Rect>();
        static readonly Rect RestGarden = Rect.MinMaxRect(5.2f,-5.6f,18.5f,6.8f);
        static readonly Dictionary<string,Rect> PlotBounds = new Dictionary<string,Rect> {
            {"nursery-lower",Rect.MinMaxRect(-22,-17.8f,-2.8f,-3.4f)},
            {"nursery-upper",Rect.MinMaxRect(-22,-3.4f,-2.8f,18)},
            {"workshop-shore",Rect.MinMaxRect(18.5f,-28,35,6.8f)},
            {"ridge-garden",Rect.MinMaxRect(-2.8f,6.8f,25,20)}
        };
        public string[] OwnedIds => playableMode
            ? (playableRestOwned?new[]{"courtyard","rest-garden"}:new[]{"courtyard"})
            : sessionLedger?.OwnedIds ?? new[]{"courtyard"};
        public bool HasTerritory(string id) => playableMode
            ? id=="courtyard"||(id=="rest-garden"&&playableRestOwned)
            : sessionLedger != null && sessionLedger.Contains(id);
        public bool IsPlayable => playableMode;
        string LedgerFile => Path.Combine(SmallWorldProfile.TestRoot,"story-territories.json");
        public static readonly Vector3 CourtyardFocus = new Vector3(-11.4f, .6f, -26.8f);
        public static readonly Vector3 CourtyardCamera = new Vector3(-11.4f, 15.8f, -49.5f);
        public static readonly Vector3 CourtyardWater = new Vector3(-13.8f, .02f, -27.55f);
        public static readonly Vector3 CourtyardWaterApproach = new Vector3(-12.55f, .03f, -26.95f);

        public int Stage { get; private set; }
        public bool GuardianRouteOpen { get; private set; }
        public bool IsAttached => game != null && coast != null;
        // Carving is asynchronous; wait for this before calculating a new path.
        public bool NavigationReady => IsAttached && Time.frameCount >= navigationChangedFrame + 3;

        sealed class SurfaceState
        {
            public Renderer renderer;
            public bool enabled;
            public MeshFilter filter;
            public Mesh original;
            public Bounds bounds;
            public ConstructionKind kind;
            public readonly Dictionary<string,Mesh> crops = new Dictionary<string,Mesh>();
        }
        sealed class LampState
        {
            public Light light;
            public bool enabled;
        }
        readonly struct MovedTransform
        {
            public readonly Transform transform;
            public readonly Vector3 position;
            public MovedTransform(Transform target) { transform = target; position = target.position; }
        }
        readonly List<SurfaceState> surfaces = new List<SurfaceState>();
        readonly List<LampState> lamps = new List<LampState>();
        readonly List<MovedTransform> movedWater = new List<MovedTransform>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<GameObject> barriers = new List<GameObject>();
        readonly List<(GameObject node,bool active)> legacyDetailVisibility=new List<(GameObject,bool)>();
        DawnGardenGame game;
        DawnCoast coast;
        GardenInteraction water;
        string waterTitle;
        GameObject runtimeRoot, shoreSkirt, boundaryMist;
        Material mistMaterial;
        Rect? revealingArea;
        float revealStarted=-100;
        int navigationChangedFrame;

        /// <summary>Call once when starting a fresh recording, before Attach.</summary>
        public static void ResetSession()
        {
            sessionLedger = null;
            sessionGuardianRoute = false;
        }

        /// <summary>
        /// Reattaches after a scene reload and restores the construction ledger
        /// from this recording's isolated profile before presenting the coast.
        /// </summary>
        public void Attach(DawnGardenGame owner, DawnCoast landscape)
        {
            if (owner == null || landscape == null) throw new ArgumentNullException("Story land needs a built game and coast.");
            if (game == owner && coast == landscape && !playableMode) return;
            Restore();
            game = owner;
            coast = landscape;
            if (string.IsNullOrEmpty(SmallWorldProfile.TestRoot)) throw new InvalidOperationException("Territory presentation requires an isolated story save.");
            if (sessionLedger == null)
                sessionLedger = File.Exists(LedgerFile) ? JsonUtility.FromJson<StoryTerritoryLedger>(File.ReadAllText(LedgerFile)) : new StoryTerritoryLedger();
            sessionLedger.Validate();
            Stage = sessionLedger.OwnedIds.Length - 1;
            LoadConstruction();
            SaveTerritories();
            GuardianRouteOpen = sessionGuardianRoute;
            CapturePresentation();
        }

        void CapturePresentation()
        {
            // The original starter's dressing is parented under game.transform,
            // outside the coast's navigation surface. It was hidden by the full
            // terrain before cropping and otherwise floats over the new sea gaps.
            foreach(var detail in game.GetComponentsInChildren<StarterHomeDetail>(true))
                if(detail.content!=null&&!detail.content.IsChildOf(coast.transform))
                {
                    legacyDetailVisibility.Add((detail.content.gameObject,detail.content.gameObject.activeSelf));
                    detail.content.gameObject.SetActive(false);
                }
            runtimeRoot = new GameObject(playableMode?"Playable home · earned soil land":"Story demo · temporary coastline and navigation limits");
            runtimeRoot.transform.SetParent(coast.transform, false);
            foreach (var renderer in coast.GetComponentsInChildren<Renderer>(true)) Capture(renderer);
            // Capture only the existing editable land. Objects created afterwards
            // remain the real player's layout and are never hidden by this layer.
            if (game.expansion.land != null)
                foreach (var renderer in game.expansion.land.GetComponentsInChildren<Renderer>(true)) Capture(renderer);
            // The guardian preview is a render-only sibling, not the real expansion
            // land. Let it appear with the explored route, without touching ownership.
            // The story initially keeps the preview inactive. GameObject.Find
            // would miss it and its original round slab would reappear later.
            var preview = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .FirstOrDefault(t=>t.name=="苔岩守卫 · 尚未占领的土地预览"&&t.gameObject.scene.IsValid());
            if (preview != null)
                foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true)) Capture(renderer);
            foreach (var light in coast.GetComponentsInChildren<Light>(true))
                if (light.type != LightType.Directional)
                    lamps.Add(new LampState { light = light, enabled = light.enabled });
            CaptureGuardianRoute();
            MoveWaterIntoCourtyard();
            ApplyStage();
        }

        void Capture(Renderer renderer)
        {
            if (renderer == null || renderer.transform.IsChildOf(runtimeRoot.transform)) return;
            if (game.actor != null && renderer.transform.IsChildOf(game.actor)) return;
            if (renderer.GetComponentInParent<HomeLayoutInstance>() != null) return;
            if (ConstructionPath(renderer.transform).Contains("Editable Home Layout")) return;
            var filter = renderer.GetComponent<MeshFilter>();
            var state = new SurfaceState
            {
                renderer = renderer,
                enabled = renderer.enabled,
                filter = filter,
                original = filter != null ? filter.sharedMesh : null,
                bounds = renderer.bounds,
                kind = ClassifyConstruction(renderer)
            };
            surfaces.Add(state);
        }

        public void OpenGuardianRoute()
        {
            if(playableMode){SyncPlayableProgress();return;}
            GuardianRouteOpen = sessionGuardianRoute = true;
            if (IsAttached) ApplyStage();
        }

        public bool UnlockTerritory(string id,string evidence)
        {
            if (!IsAttached) throw new InvalidOperationException("Attach before unlocking a territory.");
            if(playableMode)throw new InvalidOperationException("Playable territory ownership comes only from saved GardenProgress.");
            var changed=sessionLedger.Grant(id,evidence);
            Stage=sessionLedger.OwnedIds.Length-1;
            SaveTerritories();
            if(changed)
            {
                constructionStages[id] = 0;
                SaveConstruction();
                revealingArea=PlotBounds.TryGetValue(id,out var revealed)?revealed:(Rect?)null;
                revealStarted=Time.time;ApplyStage();
            }
            return changed;
        }
        public bool ReloadTerritories()
        {
            var before=string.Join("|",OwnedIds);
            if(playableMode){SyncPlayableProgress();return before==string.Join("|",OwnedIds);}
            sessionLedger=JsonUtility.FromJson<StoryTerritoryLedger>(File.ReadAllText(LedgerFile));
            sessionLedger.Validate();Stage=sessionLedger.OwnedIds.Length-1;
            LoadConstruction();ApplyStage();
            return before==string.Join("|",OwnedIds);
        }
        void SaveTerritories()
        {
            var temporary=LedgerFile+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(sessionLedger,true));
            if(File.Exists(LedgerFile)) File.Replace(temporary,LedgerFile,null);
            else File.Move(temporary,LedgerFile);
        }
        List<Rect> VisibleAreas()
        {
            var areas=new List<Rect>{Courtyard};
            if(GuardianRouteOpen || HasTerritory("rest-garden"))
            {
                areas.AddRange(guardianRouteAreas);
                // This is the single guarded plot, visible before ownership as a destination.
                areas.Add(RestGarden);
            }
            foreach(var id in OwnedIds) if(PlotBounds.TryGetValue(id,out var region))areas.Add(region);
            return Disjoint(areas);
        }
        public bool PositionOpen(Vector3 point) => VisibleAreas().Any(a=>Inside(a,point));

        void ApplyStage()
        {
            var areas=VisibleAreas();
            foreach(var state in surfaces)
            {
                if(state.renderer==null)continue;
                var allowed=ConstructionAreas(state.kind);
                if(allowed.Count==0 || !allowed.Any(area=>Overlaps(area,state.bounds)))
                {state.renderer.enabled=false;continue;}
                if(allowed.Any(area=>Contains(area,state.bounds)))
                {
                    if(state.filter!=null)state.filter.sharedMesh=state.original;
                    state.renderer.enabled=state.enabled;continue;
                }
                if(state.filter!=null && state.original!=null && state.original.isReadable)
                {
                    string cacheKey=string.Join(";",allowed.Select(r=>$"{r.xMin},{r.yMin},{r.xMax},{r.yMax}"));
                    if(!state.crops.TryGetValue(cacheKey,out var cropped))
                    {
                        cropped=CropMesh(state.original,state.filter.transform,allowed);
                        if(cropped!=null)ownedMeshes.Add(cropped);
                        state.crops[cacheKey]=cropped;
                    }
                    if(cropped!=null)state.filter.sharedMesh=cropped;
                    state.renderer.enabled=state.enabled&&cropped!=null;
                }
                // An unreadable cross-boundary batch cannot be safely cropped.
                // Keep it hidden until its complete bounds have been constructed.
                else state.renderer.enabled=false;
            }
            foreach(var state in lamps)
                if(state.light!=null)
                    state.light.enabled=state.enabled&&ConstructionAreas(ClassifyConstruction(state.light.transform)).Any(area=>Inside(area,state.light.transform.position));
            RebuildConstructionGround(areas);
            RebuildLimits(areas);Physics.SyncTransforms();navigationChangedFrame=Time.frameCount;
        }

        // Disjoint rectangles avoid duplicate triangles where route and plot overlap.
        static List<Rect> Disjoint(IEnumerable<Rect> regions)
        {
            var result=new List<Rect>();
            foreach(var area in regions)
            {
                var pieces=new List<Rect>{area};
                foreach(var taken in result)pieces=pieces.SelectMany(p=>Subtract(p,taken)).ToList();
                result.AddRange(pieces);
            }
            return result;
        }
        static IEnumerable<Rect> Subtract(Rect source,Rect cut)
        {
            float l=Mathf.Max(source.xMin,cut.xMin),r=Mathf.Min(source.xMax,cut.xMax),
                b=Mathf.Max(source.yMin,cut.yMin),t=Mathf.Min(source.yMax,cut.yMax);
            if(l>=r || b>=t){yield return source;yield break;}
            if(source.xMin<l)yield return Rect.MinMaxRect(source.xMin,source.yMin,l,source.yMax);
            if(r<source.xMax)yield return Rect.MinMaxRect(r,source.yMin,source.xMax,source.yMax);
            if(source.yMin<b)yield return Rect.MinMaxRect(l,source.yMin,r,b);
            if(t<source.yMax)yield return Rect.MinMaxRect(l,t,r,source.yMax);
        }

        static bool Inside(Rect area, Vector3 p) => p.x >= area.xMin && p.x <= area.xMax && p.z >= area.yMin && p.z <= area.yMax;
        static bool Contains(Rect area, Bounds b) => Inside(area, b.min) && Inside(area, b.max);
        static bool Overlaps(Rect area, Bounds b) => b.max.x >= area.xMin && b.min.x <= area.xMax && b.max.z >= area.yMin && b.min.z <= area.yMax;

        void MoveWaterIntoCourtyard()
        {
            water = game.FindInteraction(GardenInteractionKind.Water);
            if (water == null) return;
            waterTitle = water.displayName;
            var delta = CourtyardWater - water.transform.position;
            movedWater.Add(new MovedTransform(water.transform));
            // Preserve independent transforms too; child transforms follow the root.
            if (water.visual != null && !water.visual.IsChildOf(water.transform))
            {
                movedWater.Add(new MovedTransform(water.visual));
                water.visual.position += delta;
            }
            if (water.approach != null && !water.approach.IsChildOf(water.transform))
                movedWater.Add(new MovedTransform(water.approach));
            else if (water.approach != null)
                movedWater.Add(new MovedTransform(water.approach));
            water.transform.position = CourtyardWater;
            if (water.approach != null) water.approach.position = CourtyardWaterApproach;
            water.displayName = "给小院的新芽浇水";
        }

        void RebuildLimits(List<Rect> areas)
        {
            foreach(var old in barriers)if(old!=null){old.SetActive(false);Destroy(old);}
            barriers.Clear();
            if(shoreSkirt!=null){shoreSkirt.SetActive(false);Destroy(shoreSkirt);shoreSkirt=null;}
            // Carve the complement of the exact visible union; hidden districts stay
            // physically inaccessible after every grant and after scene restoration.
            var blocked=new List<Rect>{Rect.MinMaxRect(-600,-600,600,600)};
            foreach(var area in areas)blocked=blocked.SelectMany(p=>Subtract(p,area)).ToList();
            int index=0;
            foreach(var block in blocked)
                AddBarrier((index++).ToString(),new Vector3(block.center.x,0,block.center.y),new Vector3(block.width,160,block.height));
            BuildShoreSkirt(areas);BuildBoundaryMist(areas);
        }

        void BuildBoundaryMist(List<Rect> areas)
        {
            if(boundaryMist!=null){boundaryMist.SetActive(false);Destroy(boundaryMist);}
            // No permanent perimeter fog: cropping billboards against an owned
            // rectangle creates a conspicuous sheet over the sea. Only reveal
            // the newly earned plot, then remove the cloud completely.
            if(!revealingArea.HasValue || Time.time-revealStarted>=1.1f)return;
            if(mistMaterial==null)
            {
                var shader=Resources.Load<Shader>("Coast/StoryBoundaryMist");
                if(shader==null)return;
                mistMaterial=new Material(shader){name="新地块 · 短暂揭雾"};
            }
            var reveal=revealingArea??Rect.zero;
            mistMaterial.SetVector("_RevealBounds",new Vector4(reveal.xMin,reveal.yMin,reveal.xMax,reveal.yMax));mistMaterial.SetFloat("_RevealProgress",Mathf.Clamp01((Time.time-revealStarted)/1.1f));
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var sizes=new List<Vector2>();var faces=new List<int>();
            if(revealingArea.HasValue && Time.time-revealStarted<1.2f)
            {
                var patch=revealingArea.Value;
                for(float x=patch.xMin+1;x<patch.xMax;x+=3.2f)for(float z=patch.yMin+1;z<patch.yMax;z+=3.2f)
                {
                    var center=new Vector3(x,DawnCoast.ReferenceHeight(x,z)+1.2f,z);
                    int n=vertices.Count;var q=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                    for(int j=0;j<4;j++){vertices.Add(center);uv.Add(q[j]);sizes.Add(new Vector2(7.4f,5.2f));}
                    faces.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
                }
            }
            if(vertices.Count==0)return;
            var mesh=new Mesh{name="新地块 · 揭雾"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,sizes);mesh.SetTriangles(faces,0);mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(12);mesh.bounds=bounds;ownedMeshes.Add(mesh);
            boundaryMist=new GameObject("新地块 · 短暂揭雾");boundaryMist.transform.SetParent(runtimeRoot.transform,false);boundaryMist.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            boundaryMist.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=boundaryMist.AddComponent<MeshRenderer>();renderer.sharedMaterial=mistMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        void LateUpdate()
        {
            if(mistMaterial!=null)mistMaterial.SetFloat("_RevealProgress",Mathf.Clamp01((Time.time-revealStarted)/1.1f));
            if(boundaryMist!=null && Time.time-revealStarted>=1.1f){Destroy(boundaryMist);boundaryMist=null;}
        }

        void AddBarrier(string label, Vector3 center, Vector3 size)
        {
            var node = new GameObject("Story boundary · " + label);
            node.transform.SetParent(runtimeRoot.transform, false);
            node.transform.position = center;
            // Not the camera's ground/obstacle layers (8/9), so the invisible
            // boundary does not pull a following camera into the player.
            node.layer = 2;
            var box = node.AddComponent<BoxCollider>();
            box.size = size;
            var obstacle = node.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            barriers.Add(node);
        }

        void BuildShoreSkirt(List<Rect> areas)
        {
            // Build the outside of the union, never the internal plot seams. A
            // sloped, layered soil/stone shoulder replaces the former black cut.
            EnsureConstructionMaterial();
            var vertices=new List<Vector3>();var triangles=new List<int>();
            var colors=new List<Color>();var uv=new List<Vector2>();var masks=new List<Vector2>();
            foreach(var outline in ShoreOutlines(areas))
            {
                int first=vertices.Count,count=outline.Count;
                for(int ring=0;ring<4;ring++)
                {
                    float t=ring/3f;
                    float shoulder=ring==0?0:ring==1?.70f:ring==2?1.65f:2.45f;
                    float drop=ring==0?0:ring==1?.12f:ring==2?.48f:1;
                    for(int i=0;i<count;i++)
                    {
                        var p=outline[i];var previous=outline[(i+count-1)%count];var next=outline[(i+1)%count];
                        var before=(p-previous).normalized;var after=(next-p).normalized;
                        var n1=new Vector2(-before.y,before.x);var n2=new Vector2(-after.y,after.x);
                        var outward=(n1+n2).normalized;
                        if(outward.sqrMagnitude<.01f)outward=n2;
                        // Both adjoining faces use this same miter point. Separate
                        // offsets per rectangle left open wedges at every turn.
                        float miter=Mathf.Min(1.5f,1/Mathf.Max(.5f,Vector2.Dot(outward,n2)));
                        float radius=shoulder*miter*(.90f+.10f*Mathf.Sin(p.x*1.73f+p.y*.83f));
                        var edge=p+outward*radius;
                        float top=PresentationHeight(p.x,p.y);
                        float bottom=Mathf.Min(DawnCoast.ReferenceSeaLevel-1.8f,top-2.3f);
                        vertices.Add(new Vector3(edge.x,Mathf.Lerp(top,bottom,drop),edge.y));
                        colors.Add(Color.Lerp(new Color(.49f,.54f,.32f,0),new Color(.57f,.51f,.39f,0),t));
                        masks.Add(new Vector2(0,t*.62f));uv.Add(edge*.34f);
                        if(ring==0)continue;
                        int prior=first+(ring-1)*count+i,priorNext=first+(ring-1)*count+(i+1)%count;
                        int lower=first+ring*count+i,lowerNext=first+ring*count+(i+1)%count;
                        triangles.AddRange(new[]{prior,lower,priorNext,priorNext,lower,lowerNext});
                    }
                }
            }
            if(vertices.Count==0)return;
            var mesh=new Mesh{name="Story land · layered solid earth shore",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,masks);
            mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            shoreSkirt=new GameObject("Story land · sloping earth and rock sides");shoreSkirt.transform.SetParent(runtimeRoot.transform,false);
            shoreSkirt.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);shoreSkirt.transform.localScale=Vector3.one;
            shoreSkirt.AddComponent<MeshFilter>().sharedMesh=mesh;shoreSkirt.AddComponent<MeshRenderer>().sharedMaterial=constructionMaterial;
        }

        // Exact exposed intervals of the rectangle union, joined before the
        // shoulder is extruded. Shared plot edges never create an interior wall.
        static List<List<Vector2>> ShoreOutlines(List<Rect> areas)
        {
            var segments=new List<(Vector2 a,Vector2 b)>();
            foreach(var area in areas)
            {
                var corners=new[]{new Vector2(area.xMin,area.yMin),new Vector2(area.xMin,area.yMax),
                    new Vector2(area.xMax,area.yMax),new Vector2(area.xMax,area.yMin)};
                for(int side=0;side<4;side++)
                {
                    var a=corners[side];var b=corners[(side+1)%4];var delta=b-a;
                    bool vertical=Mathf.Abs(delta.x)<.00001f;
                    float origin=vertical?a.y:a.x,length=vertical?delta.y:delta.x;
                    var cuts=new List<float>{0,1};
                    foreach(var other in areas)
                    {
                        float start=((vertical?other.yMin:other.xMin)-origin)/length;
                        float end=((vertical?other.yMax:other.xMax)-origin)/length;
                        if(start>0&&start<1)cuts.Add(start);
                        if(end>0&&end<1)cuts.Add(end);
                    }
                    cuts=cuts.Distinct().OrderBy(v=>v).ToList();
                    var outward=new Vector2(-delta.y,delta.x).normalized;
                    for(int part=0;part<cuts.Count-1;part++)
                    {
                        var p=Vector2.Lerp(a,b,cuts[part]);var q=Vector2.Lerp(a,b,cuts[part+1]);
                        if((q-p).sqrMagnitude<.00000001f)continue;
                        var outside=(p+q)*.5f+outward*.0005f;
                        if(areas.Any(other=>Inside(other,new Vector3(outside.x,0,outside.y))))continue;
                        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(p,q)/.55f));
                        for(int step=0;step<steps;step++)segments.Add((Vector2.Lerp(p,q,step/(float)steps),Vector2.Lerp(p,q,(step+1)/(float)steps)));
                    }
                }
            }
            Vector2Int Key(Vector2 p)=>new Vector2Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000));
            var starts=new Dictionary<Vector2Int,List<int>>();
            for(int i=0;i<segments.Count;i++)
            {
                var key=Key(segments[i].a);
                if(!starts.TryGetValue(key,out var entries))starts.Add(key,entries=new List<int>());
                entries.Add(i);
            }
            var used=new bool[segments.Count];var result=new List<List<Vector2>>();
            for(int first=0;first<segments.Count;first++)
            {
                if(used[first])continue;
                var outline=new List<Vector2>();int current=first;
                while(!used[current])
                {
                    used[current]=true;outline.Add(segments[current].a);
                    var end=Key(segments[current].b);
                    if(end==Key(segments[first].a))break;
                    if(!starts.TryGetValue(end,out var candidates))break;
                    current=-1;
                    foreach(int candidate in candidates)if(!used[candidate]){current=candidate;break;}
                    if(current<0)break;
                }
                if(outline.Count>=3)result.Add(outline);
            }
            return result;
        }

        struct ClipVertex
        {
            public Vector3 world, weights;
            public ClipVertex(Vector3 p, Vector3 w) { world = p; weights = w; }
        }

        // Clip only boundary triangles. Existing vertices, normals, vertex colours,
        // tangents and every populated UV channel remain intact on a runtime copy.
        static Mesh CropMesh(Mesh original, Transform frame, List<Rect> areas)
        {
            var positions = new List<Vector3>(original.vertices);
            int originalCount = positions.Count;
            var world = new Vector3[originalCount];
            for (int i = 0; i < originalCount; i++) world[i] = frame.TransformPoint(positions[i]);
            var normals = new List<Vector3>(); original.GetNormals(normals);
            var tangents = new List<Vector4>(); original.GetTangents(tangents);
            var colors = new List<Color>(); original.GetColors(colors);
            var uvs = new List<Vector4>[8];
            for (int channel = 0; channel < uvs.Length; channel++)
            {
                uvs[channel] = new List<Vector4>();
                original.GetUVs(channel, uvs[channel]);
            }
            bool hasNormals = normals.Count == originalCount;
            bool hasTangents = tangents.Count == originalCount;
            bool hasColors = colors.Count == originalCount;
            var faces = new List<int>[original.subMeshCount];
            var polygon = new ClipVertex[12]; var scratch = new ClipVertex[12];
            int faceCount = 0;
            var bounds = new Bounds(); bool hasBounds = false;
            void Include(int index)
            {
                if (!hasBounds) { bounds = new Bounds(positions[index], Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(positions[index]);
            }
            int Append(ClipVertex vertex, int a, int b, int c)
            {
                var w = vertex.weights;
                int index = positions.Count;
                positions.Add(positions[a] * w.x + positions[b] * w.y + positions[c] * w.z);
                if (hasNormals) normals.Add((normals[a] * w.x + normals[b] * w.y + normals[c] * w.z).normalized);
                if (hasTangents) tangents.Add(tangents[a] * w.x + tangents[b] * w.y + tangents[c] * w.z);
                if (hasColors) colors.Add(colors[a] * w.x + colors[b] * w.y + colors[c] * w.z);
                for (int channel = 0; channel < uvs.Length; channel++)
                    if (uvs[channel].Count >= originalCount)
                        uvs[channel].Add(uvs[channel][a] * w.x + uvs[channel][b] * w.y + uvs[channel][c] * w.z);
                Include(index);
                return index;
            }
            for (int sub = 0; sub < original.subMeshCount; sub++)
            {
                faces[sub] = new List<int>();
                if (original.GetTopology(sub) != MeshTopology.Triangles) continue;
                var source = original.GetTriangles(sub);
                for (int tri = 0; tri < source.Length; tri += 3)
                {
                    int a = source[tri], b = source[tri + 1], c = source[tri + 2];
                    var pa = world[a]; var pb = world[b]; var pc = world[c];
                    foreach(var area in areas)
                    {
                    if (Mathf.Max(pa.x, pb.x, pc.x) < area.xMin || Mathf.Min(pa.x, pb.x, pc.x) > area.xMax ||
                        Mathf.Max(pa.z, pb.z, pc.z) < area.yMin || Mathf.Min(pa.z, pb.z, pc.z) > area.yMax) continue;
                    if (Inside(area, pa) && Inside(area, pb) && Inside(area, pc))
                    {
                        faces[sub].Add(a); faces[sub].Add(b); faces[sub].Add(c);
                        Include(a); Include(b); Include(c); faceCount++;
                        continue;
                    }
                    polygon[0] = new ClipVertex(pa, Vector3.right);
                    polygon[1] = new ClipVertex(pb, Vector3.up);
                    polygon[2] = new ClipVertex(pc, Vector3.forward);
                    int count = 3;
                    for (int plane = 0; plane < 4 && count > 0; plane++)
                    {
                        int output = 0;
                        var previous = polygon[count - 1];
                        float pd = PlaneDistance(previous.world, area, plane);
                        for (int i = 0; i < count; i++)
                        {
                            var current = polygon[i];
                            float cd = PlaneDistance(current.world, area, plane);
                            if ((pd >= 0) != (cd >= 0))
                            {
                                float t = pd / (pd - cd);
                                scratch[output++] = new ClipVertex(Vector3.LerpUnclamped(previous.world, current.world, t),
                                    Vector3.LerpUnclamped(previous.weights, current.weights, t));
                            }
                            if (cd >= 0) scratch[output++] = current;
                            previous = current; pd = cd;
                        }
                        var swap = polygon; polygon = scratch; scratch = swap; count = output;
                    }
                    if (count < 3) continue;
                    int first = Append(polygon[0], a, b, c);
                    int previousIndex = Append(polygon[1], a, b, c);
                    for (int i = 2; i < count; i++)
                    {
                        int next = Append(polygon[i], a, b, c);
                        faces[sub].Add(first); faces[sub].Add(previousIndex); faces[sub].Add(next);
                        previousIndex = next; faceCount++;
                    }
                    }
                }
            }
            if (faceCount == 0) return null;
            var result = new Mesh { name = original.name + " · story crop", indexFormat = IndexFormat.UInt32 };
            result.SetVertices(positions);
            if (hasNormals) result.SetNormals(normals);
            if (hasTangents) result.SetTangents(tangents);
            if (hasColors) result.SetColors(colors);
            for (int channel = 0; channel < uvs.Length; channel++)
                if (uvs[channel].Count == positions.Count) result.SetUVs(channel, uvs[channel]);
            result.subMeshCount = faces.Length;
            for (int sub = 0; sub < faces.Length; sub++) result.SetTriangles(faces[sub], sub, false);
            if (!hasNormals) result.RecalculateNormals();
            result.bounds = bounds;
            return result;
        }

        static float PlaneDistance(Vector3 p, Rect area, int plane)
        {
            switch (plane)
            {
                case 0: return p.x - area.xMin;
                case 1: return area.xMax - p.x;
                case 2: return p.z - area.yMin;
                default: return area.yMax - p.z;
            }
        }

        /// <summary>Restores the current scene exactly; session stage remains available for reload.</summary>
        public void Restore()
        {
            foreach (var state in surfaces)
            {
                if (state.filter != null) state.filter.sharedMesh = state.original;
                if (state.renderer != null) state.renderer.enabled = state.enabled;
            }
            foreach (var state in lamps) if (state.light != null) state.light.enabled = state.enabled;
            // Root first, then independent/child approaches to their captured positions.
            foreach (var state in movedWater) if (state.transform != null) state.transform.position = state.position;
            if (water != null) water.displayName = waterTitle;
            RestoreConstruction();
            playableMode=playableRestOwned=false;playableSaveDirectory=null;playableState=null;
            if (runtimeRoot != null) { runtimeRoot.SetActive(false); Destroy(runtimeRoot); }
            foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            foreach(var item in legacyDetailVisibility)if(item.node!=null)item.node.SetActive(item.active);
            legacyDetailVisibility.Clear();
            surfaces.Clear(); lamps.Clear(); movedWater.Clear(); ownedMeshes.Clear(); barriers.Clear();
            if(mistMaterial!=null)Destroy(mistMaterial);mistMaterial=null;
            runtimeRoot = shoreSkirt = boundaryMist = null;revealingArea=null;revealStarted=-100;
            game = null; coast = null; water = null;
            Physics.SyncTransforms();
        }

        public void Dispose() => Restore();
        void OnDestroy() => Restore();
    }
}

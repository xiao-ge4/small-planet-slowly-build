using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class SmallWorldStoryLand
    {
        // Recording construction lives beside isolated story territories. The
        // playable sidecar mirrors real progress and never grants ownership.
        [Serializable] sealed class ConstructionFile
        {
            public int schemaVersion=1;
            public string provenance="preset-demo";
            public List<ConstructionEntry> entries=new List<ConstructionEntry>();
        }
        [Serializable] sealed class ConstructionEntry
        {
            public string id;
            public int stage;
        }
        enum ConstructionKind { Ground, Path, Facility, Planting, StarterOnly, Guardian, Hidden }
        readonly Dictionary<string,int> constructionStages=new Dictionary<string,int>();
        readonly List<Material> constructionMaterials=new List<Material>();
        readonly List<Renderer> constructionFacilities=new List<Renderer>();
        readonly List<Vector3> guardianRouteSurvey=new List<Vector3>();
        GameObject constructionGround,constructionDetails;
        Material constructionMaterial;
        string ConstructionFilePath=>Path.Combine(SmallWorldProfile.TestRoot,"story-construction.json");

        public int ConstructionStage(string id)
        {
            if(id=="courtyard")return 3;
            return HasTerritory(id)&&constructionStages.TryGetValue(id,out var stage)?stage:0;
        }
        public bool IsUndeveloped(string id)=>HasTerritory(id)&&id!="courtyard"&&ConstructionStage(id)==0;
        public Rect ConstructionBounds(string id)
        {
            if(id=="courtyard")return Courtyard;
            if(id=="rest-garden")return RestGarden;
            if(PlotBounds.TryGetValue(id,out var bounds))return bounds;
            throw new ArgumentException("Unknown story territory: "+id,nameof(id));
        }
        public void SetConstructionStage(string id,int stage)
        {
            if(!IsAttached)throw new InvalidOperationException("Attach before building a story territory.");
            if(playableMode)throw new InvalidOperationException("Playable construction uses the player's real layout, not preset stages.");
            if(id=="courtyard"||!HasTerritory(id))throw new InvalidOperationException("Only an earned, new territory can be constructed: "+id);
            if(stage<0||stage>3)throw new ArgumentOutOfRangeException(nameof(stage));
            constructionStages[id]=stage;
            SaveConstruction();
            ApplyStage();
        }

        [Serializable] sealed class PlayableLandFile
        {
            public int schemaVersion=1;
            public string provenance="player-progression",clientId,homeId,guardianOccupationId;
            public bool guardianRouteOpen,restGardenOwned;
        }
        bool playableMode,playableRestOwned;
        string playableSaveDirectory;
        PlayableLandFile playableState;
        string PlayableLandPath=>Path.Combine(playableSaveDirectory,"playable-land.json");

        /// <summary>
        /// Call for the ordinary home journey after coast and guardian creation.
        /// The complete-home showcase should keep its full coast unattached.
        /// This method never loads or writes the recording ledger or Profile.
        /// </summary>
        public void AttachPlayable(DawnGardenGame owner,DawnCoast landscape)
        {
            if(owner==null||landscape==null||owner.Progress==null)
                throw new ArgumentNullException("Playable land needs an initialized home and coast.");
            if(game==owner&&coast==landscape&&playableMode){SyncPlayableProgress();return;}
            string directory=Path.GetDirectoryName(owner.SavePath);
            string clientId=string.IsNullOrEmpty(directory)?"":new DirectoryInfo(directory).Name;
            string parent=string.IsNullOrEmpty(directory)?"":Path.GetDirectoryName(Path.GetFullPath(directory));
            if(!Guid.TryParseExact(clientId,"N",out _)||
               !string.Equals(parent,Path.GetFullPath(SmallWorldProfile.Root).TrimEnd(Path.DirectorySeparatorChar),StringComparison.Ordinal))
                throw new InvalidOperationException("Playable land requires the existing Root/clientId home save directory.");
            Restore();
            playableMode=true;game=owner;coast=landscape;playableSaveDirectory=directory;
            if(File.Exists(PlayableLandPath))
            {
                try{playableState=JsonUtility.FromJson<PlayableLandFile>(File.ReadAllText(PlayableLandPath));}
                catch(ArgumentException){playableState=null;}
            }
            if(playableState==null||playableState.schemaVersion!=1||playableState.provenance!="player-progression"||
               playableState.clientId!=clientId||playableState.homeId!=owner.homeId)
                playableState=new PlayableLandFile{clientId=clientId,homeId=owner.homeId};
            UpdatePlayableState();
            SavePlayableState();
            CapturePresentation();
        }

        /// <summary>Idempotent; call whenever ordinary gameplay progress changes.</summary>
        public bool SyncPlayableProgress()
        {
            if(!playableMode||!IsAttached||game.Progress==null)return false;
            // Ordinary layout previews can re-enable the original land renderer.
            // Keep only those captured source layers suppressed; real layout
            // instances and newly built meadow detail are not in this list.
            foreach(var state in surfaces)
                if(state.kind==ConstructionKind.Hidden&&state.renderer!=null&&state.renderer.enabled)state.renderer.enabled=false;
            bool changed=UpdatePlayableState();
            if(!changed)return false;
            SavePlayableState();ApplyStage();
            return true;
        }
        bool UpdatePlayableState()
        {
            var progress=game.Progress;
            bool owned=progress.firstExpansionUnlocked;
            bool route=progress.chestOpened||progress.expansionPermits>0||owned;
            string receipt=owned?progress.guardianOccupationId:"";
            bool changed=playableRestOwned!=owned||GuardianRouteOpen!=route||
                playableState.restGardenOwned!=owned||playableState.guardianRouteOpen!=route||playableState.guardianOccupationId!=receipt;
            playableRestOwned=owned;GuardianRouteOpen=route;Stage=owned?1:0;
            playableState.restGardenOwned=owned;playableState.guardianRouteOpen=route;playableState.guardianOccupationId=receipt;
            // The sidecar cannot unlock authored districts or resurrect a grant
            // absent from the authoritative home progress save.
            constructionStages.Clear();if(owned)constructionStages["rest-garden"]=0;
            return changed;
        }
        void SavePlayableState()
        {
            Directory.CreateDirectory(playableSaveDirectory);
            string temporary=PlayableLandPath+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(playableState,true));
            SmallWorldBrowserStorage.Commit(temporary,PlayableLandPath);
        }

        // Count actual enabled facility/plant renderers whose current cropped mesh
        // reaches the plot. The earth base, starter yard, avatar and guardian never
        // enter this count. Used by the recording's empty-land acceptance check.
        public int VisibleFacilityCount(string id)
        {
            var bounds=ConstructionBounds(id);
            int result=0;
            foreach(var state in surfaces)
            {
                if(state.kind!=ConstructionKind.Facility&&state.kind!=ConstructionKind.Planting)continue;
                var renderer=state.renderer;
                if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                if(!Overlaps(bounds,renderer.bounds)||!HasRenderedTrianglesIn(state,bounds))continue;
                result++;
            }
            foreach(var renderer in constructionFacilities)
                if(renderer!=null&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&Overlaps(bounds,renderer.bounds))result++;
            if(game!=null&&game.expansion.land!=null)
                foreach(var renderer in game.expansion.land.GetComponentsInChildren<Renderer>())
                    if(renderer.enabled&&renderer.GetComponentInParent<HomeLayoutInstance>()!=null&&Overlaps(bounds,renderer.bounds))result++;
            return result;
        }
        static bool HasRenderedTrianglesIn(SurfaceState state,Rect bounds)
        {
            if(state.filter==null||state.filter.sharedMesh==null)return Inside(bounds,state.renderer.bounds.center);
            var mesh=state.filter.sharedMesh;
            if(!mesh.isReadable)return Contains(bounds,state.renderer.bounds);
            var vertices=mesh.vertices;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                if(mesh.GetTopology(sub)!=MeshTopology.Triangles)continue;
                var triangles=mesh.GetTriangles(sub);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=state.filter.transform.TransformPoint(vertices[triangles[i]]);
                    var b=state.filter.transform.TransformPoint(vertices[triangles[i+1]]);
                    var c=state.filter.transform.TransformPoint(vertices[triangles[i+2]]);
                    // Testing rendered triangles, rather than mesh.bounds or all
                    // vertices, avoids counting the discarded part of a shared batch.
                    if(TriangleOverlapsPlot(a,b,c,bounds))return true;
                }
            }
            return false;
        }
        static bool TriangleOverlapsPlot(Vector3 a,Vector3 b,Vector3 c,Rect r)
        {
            const float epsilon=.002f;
            if(Mathf.Max(a.x,b.x,c.x)<=r.xMin+epsilon||Mathf.Min(a.x,b.x,c.x)>=r.xMax-epsilon||
               Mathf.Max(a.z,b.z,c.z)<=r.yMin+epsilon||Mathf.Min(a.z,b.z,c.z)>=r.yMax-epsilon)return false;
            return true;
        }
        void LoadConstruction()
        {
            if(playableMode)throw new InvalidOperationException("Recording construction is unavailable in playable mode.");
            constructionStages.Clear();
            var data=File.Exists(ConstructionFilePath)?JsonUtility.FromJson<ConstructionFile>(File.ReadAllText(ConstructionFilePath)):new ConstructionFile();
            if(data==null||data.schemaVersion!=1||data.provenance!="preset-demo"||data.entries==null)
                throw new InvalidOperationException("Invalid isolated story construction ledger.");
            foreach(var entry in data.entries)
            {
                if(entry==null||entry.id=="courtyard"||!HasTerritory(entry.id)||entry.stage<0||entry.stage>3||constructionStages.ContainsKey(entry.id))
                    throw new InvalidOperationException("Construction state must belong to an earned territory exactly once.");
                ConstructionBounds(entry.id);
                constructionStages.Add(entry.id,entry.stage);
            }
            foreach(var id in OwnedIds)if(id!="courtyard"&&!constructionStages.ContainsKey(id))constructionStages[id]=0;
            SaveConstruction();
        }
        void SaveConstruction()
        {
            if(playableMode)throw new InvalidOperationException("Recording construction is unavailable in playable mode.");
            var data=new ConstructionFile();
            foreach(var id in OwnedIds)
                if(id!="courtyard")data.entries.Add(new ConstructionEntry{id=id,stage=ConstructionStage(id)});
            Directory.CreateDirectory(SmallWorldProfile.TestRoot);
            string temporary=ConstructionFilePath+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(data,true));
            if(File.Exists(ConstructionFilePath))File.Replace(temporary,ConstructionFilePath,null);
            else File.Move(temporary,ConstructionFilePath);
        }

        static string ConstructionPath(Transform node)
        {
            string path="";
            for(var current=node;current!=null;current=current.parent)path=current.name+"/"+path;
            return path;
        }
        ConstructionKind ClassifyConstruction(Renderer renderer)=>ClassifyConstruction(renderer.transform);
        ConstructionKind ClassifyConstruction(Transform node)
        {
            string path=ConstructionPath(node);
            bool Has(params string[] values)=>values.Any(value=>path.IndexOf(value,StringComparison.OrdinalIgnoreCase)>=0);
            if(Has("MossstoneGuardian"))return ConstructionKind.Guardian;
            // The preview and editable plot's original white slab are replaced by
            // the same earth volume as the newly awarded districts.
            if(game.expansion.land!=null&&node.IsChildOf(game.expansion.land))return ConstructionKind.Hidden;
            if(Has("尚未占领的土地预览"))return ConstructionKind.Hidden;
            // These trees and the global bank-planting batch were seated on the
            // original high rock colliders. Those rock crowns are outside this
            // story's soil presentation, so do not reveal unsupported planting.
            if(Has("岩岭树冠"))return ConstructionKind.Hidden;
            if(Has("成团多层花草"))return ConstructionKind.StarterOnly;
            // A partially revealed continent has no credible headwater or outlet.
            // Keep the entire old main water network, cave and bridge system off.
            if(Has("连通阶梯水渠","苗圃引水","溪流 ","砌石引水槽","水渠落地基座","苗圃通道跨渠石桥",
                "水轮","弧形木桥","桥头","桥台","桥墩","桥面","桥栏","潮洞","cave_shell",
                "远岸","群山","背景景深","浅湾内可见","海口浅水","瀑布","水帘","水沫","上游水路","低层种植连接"))return ConstructionKind.Hidden;
            if(Has("连续地表","花园承接带"))return ConstructionKind.Ground;
            if(Has("central_ridge","Geology/道路","Geology/低岩层","岩层与风化","海崖","南岸完整岩坎","岸肩地质"))return ConstructionKind.Ground;
            // Material batches for nursery crops and coast planting cross plot
            // boundaries. Their triangles still go through ConstructionAreas.
            if(Has("连片蔬果与花苗","flowers_","courtyard_tree_hero","岩岭树冠","成团多层花草",
                "低层种植连接","成团花色","附岩苔草","低矮白金花","草叶","花穗","叶簇","垂枝","foliage","leaves"))return ConstructionKind.Planting;
            if(Has("随路圆磨桃石","阶地砌筑与土层","十四级完整矮石阶","后园实体承台","后园阶前接地石坪",
                "连续灰土墙芯","薄土壤断面","连续岩层"))return ConstructionKind.Path;
            return ConstructionKind.Facility;
        }
        List<Rect> ConstructionAreas(ConstructionKind kind)
        {
            if(kind==ConstructionKind.Hidden)return new List<Rect>();
            if(kind==ConstructionKind.Guardian)
                return GuardianRouteOpen||HasTerritory("rest-garden")?new List<Rect>{RestGarden}:new List<Rect>();
            var result=new List<Rect>{Courtyard};
            // Every new region always uses the natural soil volume. Original
            // ground shaders would expose pre-existing masonry and riverbeds.
            if(playableMode||kind==ConstructionKind.Ground||kind==ConstructionKind.StarterOnly)return result;
            int required=kind==ConstructionKind.Path?1:kind==ConstructionKind.Facility?2:3;
            foreach(var id in OwnedIds)
                if(id!="courtyard"&&ConstructionStage(id)>=required)result.Add(ConstructionBounds(id));
            return Disjoint(result);
        }

        void CaptureGuardianRoute()
        {
            guardianRouteAreas.Clear();guardianRouteSurvey.Clear();
            var destination=game.expansion.finalPosition+Vector3.left*(game.expansion.revealHalfSize.x+1.6f);
            var path=new NavMeshPath();
            if(NavMesh.SamplePosition(CourtyardFocus,out var from,4,NavMesh.AllAreas)&&
               NavMesh.SamplePosition(destination,out var to,2,NavMesh.AllAreas)&&
               NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)
                guardianRouteSurvey.AddRange(path.corners);
            else
            {
                // The existing surveyed path is also usable while old asynchronous
                // carving from a scene reload is retiring.
                guardianRouteSurvey.AddRange(new[]{DawnCoast.ReferenceHome,DawnCoast.ReferenceWestLanding,
                    DawnCoast.ReferenceBridgeStart,DawnCoast.ReferenceBridgeMiddle,DawnCoast.ReferenceBridgeEnd,
                    DawnCoast.ReferenceEastLanding,new Vector3(6.5f,2.2f,-15),new Vector3(3.5f,.8f,-10),
                    new Vector3(3.9f,0,-5.6f),new Vector3(3.9f,0,0),new Vector3(5.3f,0,0)});
            }
            const float halfWidth=1.65f;
            for(int i=0;i<guardianRouteSurvey.Count-1;i++)
            {
                var a=guardianRouteSurvey[i];var b=guardianRouteSurvey[i+1];
                int steps=Mathf.Max(1,Mathf.CeilToInt(new Vector2(b.x-a.x,b.z-a.z).magnitude/1.4f));
                for(int step=0;step<steps;step++)
                {
                    var p=Vector3.Lerp(a,b,step/(float)steps);var q=Vector3.Lerp(a,b,(step+1)/(float)steps);
                    guardianRouteAreas.Add(Rect.MinMaxRect(Mathf.Min(p.x,q.x)-halfWidth,Mathf.Min(p.z,q.z)-halfWidth,
                        Mathf.Max(p.x,q.x)+halfWidth,Mathf.Max(p.z,q.z)+halfWidth));
                }
            }
        }
        float GuardianSurveyDistance(float x,float z,out float height)
        {
            float nearest=float.PositiveInfinity;height=DawnCoast.ReferenceHeight(x,z);
            var p=new Vector2(x,z);
            for(int i=0;i<guardianRouteSurvey.Count-1;i++)
            {
                var a=guardianRouteSurvey[i];var b=guardianRouteSurvey[i+1];
                var ab=new Vector2(b.x-a.x,b.z-a.z);var ap=p-new Vector2(a.x,a.z);
                float t=ab.sqrMagnitude<.00001f?0:Mathf.Clamp01(Vector2.Dot(ap,ab)/ab.sqrMagnitude);
                float distance=(ap-ab*t).sqrMagnitude;
                if(distance>=nearest)continue;
                nearest=distance;height=Mathf.Lerp(a.y,b.y,t);
            }
            return Mathf.Sqrt(nearest);
        }
        float PresentationHeight(float x,float z)
        {
            float y=DawnCoast.ReferenceHeight(x,z);
            if(Inside(Courtyard,new Vector3(x,0,z)))return y;
            // The reference heightfield contains a submerged elliptical socket
            // for the original rising editable slab. The story replaces that slab
            // with earth, so it must fill the socket at the real editing height.
            if(Inside(RestGarden,new Vector3(x,0,z)))return game.expansion.finalPosition.y;
            // Close the old channel with earth sampled from its two shoulders.
            // This is real top geometry: there is no water ribbon below a cover.
            for(int i=0;i<DawnCoast.ReferenceStream.Length-1;i++)
            {
                var a=DawnCoast.ReferenceStream[i];var b=DawnCoast.ReferenceStream[i+1];
                var ab=new Vector2(b.x-a.x,b.z-a.z);var p=new Vector2(x-a.x,z-a.z);
                float t=ab.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(p,ab)/ab.sqrMagnitude);
                float distance=(p-ab*t).magnitude;
                float width=Mathf.Lerp(DawnCoast.ReferenceStreamHalfWidths[i],DawnCoast.ReferenceStreamHalfWidths[i+1],t);
                if(distance>width+1.2f)continue;
                var across=new Vector2(-ab.y,ab.x).normalized*(width+1.1f);
                var center=new Vector2(Mathf.Lerp(a.x,b.x,t),Mathf.Lerp(a.z,b.z,t));
                float shoulders=(DawnCoast.ReferenceHeight(center.x+across.x,center.y+across.y)+
                    DawnCoast.ReferenceHeight(center.x-across.x,center.y-across.y))*.5f;
                float fill=Mathf.Max(shoulders,Mathf.Lerp(a.y,b.y,t)+.12f);
                y=Mathf.Lerp(y,Mathf.Max(y,fill),1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(width+.15f,width+1.2f,distance)));
            }
            if(GuardianRouteOpen||HasTerritory("rest-garden"))
            {
                float distance=GuardianSurveyDistance(x,z,out float grade);
                if(distance<2.3f)
                {
                    // Preserve the actual baked route's height while replacing its
                    // bridge finish with a fully supported dry grass/earth ridge.
                    if(distance<1.75f&&NavMesh.SamplePosition(new Vector3(x,grade,z),out var at,1.4f,NavMesh.AllAreas))grade=at.position.y;
                    float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.35f,2.3f,distance));
                    y=Mathf.Lerp(y,grade+.018f,weight);
                }
            }
            return y;
        }
        void EnsureConstructionMaterial()
        {
            if(constructionMaterial!=null)return;
            var shader=Resources.Load<Shader>("Coast/SampleGround");
            if(shader==null)shader=Shader.Find("Universal Render Pipeline/Lit");
            constructionMaterial=new Material(shader){name="Story construction · warm soil and natural meadow"};
            if(constructionMaterial.HasProperty("_BaseColor"))constructionMaterial.SetColor("_BaseColor",new Color(.46f,.53f,.30f));
            if(constructionMaterial.HasProperty("_StoneColor"))constructionMaterial.SetColor("_StoneColor",new Color(.53f,.49f,.39f));
            if(constructionMaterial.HasProperty("_PaverScale"))constructionMaterial.SetVector("_PaverScale",Vector4.zero);
            constructionMaterials.Add(constructionMaterial);
        }
        void RebuildConstructionGround(List<Rect> visible)
        {
            if(constructionGround!=null){constructionGround.SetActive(false);Destroy(constructionGround);}
            if(constructionDetails!=null){constructionDetails.SetActive(false);Destroy(constructionDetails);}
            constructionFacilities.Clear();
            EnsureConstructionMaterial();
            var areas=visible.SelectMany(area=>Subtract(area,Courtyard)).ToList();
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();
            var uv=new List<Vector2>();var masks=new List<Vector2>();
            foreach(var area in areas)
            {
                int cols=Mathf.Max(1,Mathf.CeilToInt(area.width/.65f)),rows=Mathf.Max(1,Mathf.CeilToInt(area.height/.65f));
                int first=vertices.Count;
                for(int row=0;row<=rows;row++)for(int col=0;col<=cols;col++)
                {
                    float x=Mathf.Lerp(area.xMin,area.xMax,col/(float)cols),z=Mathf.Lerp(area.yMin,area.yMax,row/(float)rows);
                    vertices.Add(new Vector3(x,PresentationHeight(x,z)+.004f,z));uv.Add(new Vector2(x,z)*.34f);masks.Add(Vector2.zero);
                    float noise=Mathf.PerlinNoise(x*.22f+53,z*.22f+17);
                    float route=GuardianSurveyDistance(x,z,out _);
                    float tread=(GuardianRouteOpen||HasTerritory("rest-garden"))?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1.4f,route)):0;
                    var grass=Color.Lerp(new Color(.40f,.49f,.25f,0),new Color(.55f,.59f,.33f,0),noise);
                    colors.Add(Color.Lerp(grass,new Color(.55f,.45f,.30f,0),tread*.78f));
                    if(row==0||col==0)continue;
                    int n=first+row*(cols+1)+col;
                    triangles.AddRange(new[]{n-cols-2,n-1,n-cols-1,n-cols-1,n-1,n});
                }
            }
            if(vertices.Count>0)
            {
                var mesh=new Mesh{name="Story construction · continuous dry earth top",indexFormat=IndexFormat.UInt32};
                mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,masks);
                mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
                constructionGround=new GameObject("Story construction · blank grass and soil land");
                constructionGround.transform.SetParent(runtimeRoot.transform,false);constructionGround.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                constructionGround.transform.localScale=Vector3.one;
                constructionGround.AddComponent<MeshFilter>().sharedMesh=mesh;constructionGround.AddComponent<MeshRenderer>().sharedMaterial=constructionMaterial;
            }
            BuildLocalFacilities();
        }
        Material ConstructionPaint(string name,Color color)
        {
            var existing=constructionMaterials.FirstOrDefault(m=>m!=null&&m.name==name);if(existing!=null)return existing;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};
            material.SetFloat("_Smoothness",.16f);constructionMaterials.Add(material);return material;
        }
        GameObject ConstructionPiece(PrimitiveType type,string name,Vector3 position,Vector3 scale,Material material)
        {
            var node=GameObject.CreatePrimitive(type);node.name=name;node.transform.SetParent(constructionDetails.transform,false);
            node.transform.position=position;node.transform.localScale=scale;
            var collider=node.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Destroy(collider);}
            var renderer=node.GetComponent<Renderer>();renderer.sharedMaterial=material;constructionFacilities.Add(renderer);return node;
        }
        void BuildLocalFacilities()
        {
            if(playableMode)return;
            constructionDetails=new GameObject("Story construction · locally planned facilities");
            constructionDetails.transform.SetParent(runtimeRoot.transform,false);constructionDetails.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var stone=ConstructionPaint("Story build · warm weathered stone",new Color(.67f,.64f,.51f));
            var clay=ConstructionPaint("Story build · terracotta water tank",new Color(.48f,.30f,.19f));
            var waterMaterial=ConstructionPaint("Story build · still water in closed trough",new Color(.12f,.36f,.34f));
            foreach(var id in new[]{"nursery-lower","nursery-upper"})
            {
                if(ConstructionStage(id)<2)continue;
                float z=id=="nursery-lower"?-14.7f:4.3f;
                var tank=new Vector3(-18.5f,PresentationHeight(-18.5f,z),z);
                ConstructionPiece(PrimitiveType.Cylinder,id+" · local water tank",tank+Vector3.up*.60f,new Vector3(1.20f,.60f,1.20f),clay);
                ConstructionPiece(PrimitiveType.Cylinder,id+" · tank water",tank+Vector3.up*1.215f,new Vector3(1.04f,.018f,1.04f),waterMaterial);
                var trough=new Vector3(-16.2f,PresentationHeight(-16.2f,z),z);
                ConstructionPiece(PrimitiveType.Cube,id+" · closed irrigation base",trough+Vector3.up*.10f,new Vector3(2.65f,.20f,.84f),stone);
                foreach(float side in new[]{-.39f,.39f})ConstructionPiece(PrimitiveType.Cube,id+" · trough side",trough+new Vector3(0,.30f,side),new Vector3(2.65f,.32f,.12f),stone);
                foreach(float end in new[]{-1.265f,1.265f})ConstructionPiece(PrimitiveType.Cube,id+" · closed trough end",trough+new Vector3(end,.30f,0),new Vector3(.12f,.32f,.72f),stone);
                ConstructionPiece(PrimitiveType.Cube,id+" · enclosed still water",trough+Vector3.up*.305f,new Vector3(2.39f,.025f,.59f),waterMaterial);
                ConstructionPiece(PrimitiveType.Cube,id+" · tank supply spout",tank+new Vector3(.77f,1.02f,0),new Vector3(.52f,.13f,.15f),clay);
            }
        }
        void RestoreConstruction()
        {
            foreach(var material in constructionMaterials)if(material!=null)Destroy(material);
            constructionMaterials.Clear();constructionFacilities.Clear();constructionStages.Clear();
            guardianRouteAreas.Clear();guardianRouteSurvey.Clear();
            constructionMaterial=null;constructionGround=constructionDetails=null;
        }
    }
}

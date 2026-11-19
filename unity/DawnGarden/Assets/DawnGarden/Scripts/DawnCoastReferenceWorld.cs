using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        readonly Dictionary<Material,Material> referenceMaterials=new();
        readonly List<Material> referenceGlassMaterials=new();
        Material referencePondMaterial;
        const float ReferencePondX=-12.2f,ReferencePondZ=-30.6f;
        void BuildReferenceWorld(DawnGardenGame target,SmallWorldLibrary assets)
        {
            game=target;library=assets;
            var world=game.expansion.surface.transform;
            transform.SetParent(world,false);
            ground=Group("可行走地形与路径",transform);details=Group("场景美术",transform);
            CreateMaterials();
            // Weathered coastal limestone: the exposed terrain between closed
            // rock volumes should share their grey body colour instead of mud.
            cliff.color=new Color(.59f,.59f,.56f);
            cliff.DisableKeyword("_EMISSION");
            var interactions=world.GetComponentsInChildren<GardenInteraction>(true);
            bool Keep(Transform t)
            {
                if(t.IsChildOf(transform)||t.IsChildOf(game.actor)||t.IsChildOf(game.expansion.land))return true;
                if(t.name=="Ocean")return true;
                foreach(var item in interactions)
                    if(t.IsChildOf(item.transform)||(item.visual!=null&&t.IsChildOf(item.visual)))return true;
                return false;
            }
            foreach(var renderer in world.GetComponentsInChildren<Renderer>(true))if(!Keep(renderer.transform))renderer.enabled=false;
            foreach(var collider in world.GetComponentsInChildren<Collider>(true))if(!Keep(collider.transform))collider.enabled=false;
            MoveReferenceInteraction(game.FindInteraction(GardenInteractionKind.Water),ReferenceSeed+Vector3.up*.40f,"给苗圃浇水");
            game.FindInteraction(GardenInteractionKind.Water).approach.position=ReferenceNurseryApproach;
            MoveReferenceInteraction(game.FindInteraction(GardenInteractionKind.Chest),ReferenceHome+new Vector3(-3,0,1.5f),"打开小院宝箱");
            game.spawn.position=ReferenceHome+new Vector3(0,.03f,0);
            BuildReferenceTerrain();
            BuildReferenceTerraceArt();
            BuildReferenceHeroes();
            BuildReferenceUpperGarden();
            BuildReferenceGreenhouseApron();
            BuildReferenceWorkshopDressing();
            BuildReferenceRidgeCanopy();
            BuildReferenceGarden();
            BuildReferenceCliffs();
            BuildReferenceRockFaces();
            BuildReferenceBankConnections();
            BuildReferenceNaturalBanks();
            BuildReferenceWater();
            BuildReferenceShallowReefs();
            BuildReferenceBackdrop();
            Bridge(ReferenceBridgeStart,ReferenceBridgeEnd,3.25f);
            BuildSampleBridgeAbutments(false);
            BuildReferenceCavePlanting();
            BuildReferencePlanting();
            BuildReferencePlantTransitions(details);
            BuildExpansionArt();
            RefreshReferenceSurface();
            BuildReferenceWhiteApron();
            BuildReferencePathStones();
            BuildReferenceWorkshopCourt();
            BuildReferenceHomeCourtFinish();
            ConfigureSampleCamera();
            game.atmosphere.daytimeSunColor=new Color(1,.879f,.710f);
            game.atmosphere.daylightExposure=.68f;game.atmosphere.nightExposure=.79f;
            game.walkSpeed=4.1f;game.worldCamera.fieldOfView=43;game.worldCamera.farClipPlane=650;
            var skyShader=Resources.Load<Shader>("Coast/MorningPanoramaSky");
            if(skyShader!=null)
            {
                var sky=new Material(skyShader){name="海岸晨光云层"};
                sky.SetTexture("_SkyMap",Resources.Load<Texture2D>("Coast/MorningSkyV1"));
                materials.Add(sky);RenderSettings.skybox=sky;game.worldCamera.clearFlags=CameraClearFlags.Skybox;
            }
            if(game.atmosphere.sunDisc!=null)game.atmosphere.sunDisc.gameObject.SetActive(false);
            if(game.atmosphere.moonDisc!=null)game.atmosphere.moonDisc.gameObject.SetActive(false);
            fill=Group("沿岸柔光",details).gameObject.AddComponent<Light>();fill.type=LightType.Directional;
            fill.color=new Color(.79f,.9f,1);fill.transform.rotation=Quaternion.Euler(44,205,0);flowBlock=new MaterialPropertyBlock();
            game.orbit.largeWorld=true;game.orbit.overviewPerspective=true;game.orbit.overviewOrthographic=false;game.orbit.overviewFieldOfView=20;game.orbit.overviewYaw=0;game.orbit.overviewPitch=18;
            game.orbit.expandedMaxDistance=130;game.orbit.expandedOverviewDistance=100;
            game.orbit.expandedOverviewCenter=new Vector3(7.5f,0,-10.4f);
            game.orbit.overview=true;game.orbit.yaw=0;game.orbit.pitch=18;game.orbit.distance=100;
            game.expansion.waterLevel=ReferenceSeaLevel+.10f;
            var ocean=GameObject.Find("Ocean")?.GetComponent<Renderer>();
            if(ocean!=null)
            {
                var oceanShader=Resources.Load<Shader>("Coast/ReferenceSea");
                var m=oceanShader==null?new Material(ocean.sharedMaterial):new Material(oceanShader);materials.Add(m);
                if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",new Color(.09f,.37f,.47f));
                if(m.HasProperty("_ShallowColor"))m.SetColor("_ShallowColor",new Color(.13f,.55f,.52f));
                if(m.HasProperty("_SkyMap"))m.SetTexture("_SkyMap",Resources.Load<Texture2D>("Coast/MorningSkyV1"));
                ocean.sharedMaterial=m;var pos=ocean.transform.position;pos.y=ReferenceSeaLevel;ocean.transform.position=pos;
                ocean.transform.localScale=new Vector3(1450,ocean.transform.localScale.y,1450);
                BuildReferenceOceanMesh(ocean);
            }
            ApplyReferenceLook();
            BuildNavigation();
            game.PlaceActor(game.spawn.position,0);
        }
        void MoveReferenceInteraction(GardenInteraction item,Vector3 destination,string title)
        {
            if(item==null)return;
            var offset=destination-item.transform.position;
            if(item.approach!=null&&!item.approach.IsChildOf(item.transform))item.approach.position+=offset;
            if(item.visual!=null&&!item.visual.IsChildOf(item.transform))item.visual.position+=offset;
            item.transform.position=destination;item.displayName=title;
        }
        GameObject ReferenceAsset(string id,Vector3 at,float yaw=0)
        {
            var source=Resources.Load<GameObject>("CoastReference/"+id);
            if(source==null){Debug.LogWarning("Missing reference asset "+id);return null;}
            var model=Instantiate(source,details,false);model.name="参考资产 · "+id;
            model.transform.localPosition=at;model.transform.localRotation=Quaternion.Euler(0,yaw,0);
            foreach(var r in model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials=r.sharedMaterials.Select(ReferenceMaterial).ToArray();
            }
            return model;
        }
        Material ReferenceMaterial(Material original)
        {
            if(original==null)return limestone;
            if(referenceMaterials.TryGetValue(original,out var previous))return previous;
            var color=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original.color;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=original.name+" · 游戏材质",color=color,enableInstancing=true};
            material.SetFloat("_Smoothness",.22f);
            if(original.name.StartsWith("Hero foliage"))
            {
                material.SetFloat("_Smoothness",.05f);
                material.SetFloat("_SpecularHighlights",0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            var map=original.mainTexture;
            if(map!=null){material.SetTexture("_BaseMap",map);material.color=Color.white;}
            if(original.name.Contains("cave_vault"))
            {
                material.SetTexture("_BaseMap",Resources.Load<Texture2D>("CoastReference/Geology/cave_vault_basecolor"));
                material.color=Color.white;
                var vaultNormal=Resources.Load<Texture2D>("CoastReference/Geology/cave_vault_normal");
                if(vaultNormal!=null){material.SetTexture("_BumpMap",vaultNormal);material.SetFloat("_BumpScale",.40f);material.EnableKeyword("_NORMALMAP");}
            }
            if(original.name.Contains("Brass")){material.SetFloat("_Metallic",.5f);material.SetFloat("_Smoothness",.55f);}
            if(original.name.Contains("Glass"))
            {
                material.shader=Resources.Load<Shader>("Coast/SampleGlass");
                material.SetColor("_Color",new Color(.065f,.40f,.30f,.94f));material.renderQueue=3000;
                material.SetTexture("_SkyMap",Resources.Load<Texture2D>("Coast/MorningSkyV1"));
                referenceGlassMaterials.Add(material);
            }
            materials.Add(material);referenceMaterials.Add(original,material);return material;
        }
        void ReferenceObstacle(Transform parent,Vector3 center,Vector3 size)
        {
            var o=Group("通行边界",parent);o.gameObject.layer=ObstacleLayer;
            var c=o.gameObject.AddComponent<BoxCollider>();c.center=center;c.size=size;
        }
        void ReferenceSolidModel(GameObject model,bool walkable)
        {
            if(model==null)return;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<Renderer>();
                if(renderer!=null && renderer.sharedMaterials.All(m=>m.name.Contains("Glass")))continue;
                filter.gameObject.layer=walkable?FloorLayer:ObstacleLayer;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            }
        }
        void BuildReferenceHeroes()
        {
            var ridge=ReferenceAsset("Geology/central_ridge",Vector3.zero);
            SoftenReferenceRidgeCrown(ridge);
            SetReferenceRockMap(ridge,"central_ridge");
            ReferenceSolidModel(ridge,false);
            var greenhouse=ReferenceAsset("Buildings/greenhouse",ReferenceGreenhouse,-22);
            if(greenhouse!=null)greenhouse.transform.localScale=new Vector3(.9516f,.6864f,.78f);
            ReferenceSolidModel(greenhouse,true);
            var workshop=ReferenceAsset("Buildings/workshop",ReferenceWorkshop,165);
            if(workshop!=null)workshop.transform.localScale=new Vector3(.9605f,.85f,.9605f);
            ReferenceSolidModel(workshop,true);
            var yard=ReferenceAsset("Buildings/yard_props",new Vector3(19.7f,-.37f,-11.9f));
            ReferenceSolidModel(yard,false);
            var observatory=ReferenceAsset("Buildings/observatory",ReferenceObservatory-Vector3.up*(.82f*ReferenceObservatoryHeightScale),180);
            if(observatory!=null)observatory.transform.localScale=new Vector3(1.3335f,ReferenceObservatoryHeightScale,1.155f);
            ReferenceSolidModel(observatory,true);
            var dock=ReferenceAsset("Buildings/dock",ReferenceFerry+new Vector3(0,-1.975f,-2));
            if(dock!=null)dock.transform.localScale=new Vector3(1.30f,1,1.30f);
            ReferenceSolidModel(dock,true);
            var boat=ReferenceAsset("Buildings/dinghy",ReferenceFerry+new Vector3(3.8f,-.8f,-10.2f),-24);
            if(boat!=null)boat.transform.localScale*=1.18f;
            var cave=ReferenceAsset("Geology/cave_shell",ReferenceGrotto);
            if(cave!=null)
            {
                foreach(var mf in cave.GetComponentsInChildren<MeshFilter>())
                {mf.gameObject.layer=ObstacleLayer;mf.gameObject.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;}
            }
            var wheelRoot=Group("水轮地标",details);BuildSampleWheel(wheelRoot);
            var pivot=wheel.parent;pivot.position=new Vector3(-3.8f,3.4f,-9);pivot.rotation=Quaternion.Euler(0,18,0);pivot.localScale=Vector3.one*1.35f;
            // Warm fixtures are separate from the shell, so their light can be driven by time.
            foreach(float x in new[]{-5f,0f,5f})
            {
                var l=Group("潮洞壁灯",details);l.position=ReferenceGrotto+new Vector3(x,1.65f,2.4f);
                Box(l,"旧铜壁座",Vector3.zero,new Vector3(.26f,.54f,.18f),brass);
                Beam(l,"弯灯托",new Vector3(0,-.20f,-.08f),new Vector3(0,-.20f,-.42f),.055f,brass);
                Piece(PrimitiveType.Sphere,l,"琥珀灯罩",new Vector3(0,.09f,-.42f),new Vector3(.37f,.60f,.35f),warmGlass);
                Cylinder(l,"灯罩底圈",new Vector3(0,-.20f,-.42f),new Vector3(.40f,.045f,.38f),brass);
                Cylinder(l,"灯罩顶圈",new Vector3(0,.38f,-.42f),new Vector3(.30f,.045f,.29f),brass);
                foreach(float side in new[]{-1f,1f})Beam(l,"护灯铜筋",new Vector3(side*.16f,-.20f,-.45f),new Vector3(side*.11f,.39f,-.45f),.028f,brass);
                var lightNode=Group("灯前暖光",l);lightNode.localPosition=new Vector3(0,.10f,-.65f);
                var lamp=lightNode.gameObject.AddComponent<Light>();lamp.type=LightType.Point;lamp.color=new Color(1,.53f,.17f);lamp.range=4.8f;lamp.intensity=1.25f;lamp.shadows=LightShadows.Soft;lamp.shadowStrength=.8f;nightLamps.Add(lamp);
            }
            BuildReferenceCaveFurniture();
            var flagstones=Group("潮洞水磨石铺地",details);
            var caveStone=new[]{Paint("洞内浅灰旧石",new Color(.71f,.69f,.60f),.10f,.18f),
                Paint("洞内潮湿旧石",new Color(.52f,.55f,.51f),.18f,.19f),
                Paint("洞内暖灰旧石",new Color(.75f,.68f,.52f),.10f,.18f)};
            var floorRandom=new System.Random(48216);
            float FloorJitter(float amount)=>((float)floorRandom.NextDouble()-.5f)*amount;
            for(int row=0;row<9;row++)for(int col=0;col<14;col++)
            {
                float x=(col-6.5f)*.82f+(row%2)*.29f+FloorJitter(.12f);
                float z=(row-4)*.69f+FloorJitter(.11f);
                if(x*x/(5.8f*5.8f)+(z+.2f)*(z+.2f)/(3.1f*3.1f)>1)continue;
                var at=ReferenceGrotto+new Vector3(x,.025f,z);
                if(at.x<1.7f&&at.x>-.9f&&at.z< -23.6f)continue;
                float floor=ReferenceHeight(at.x,at.z);if(Mathf.Abs(floor-ReferenceGrotto.y)>.15f)continue;
                at.y=floor+.015f;int palette=floorRandom.Next(8);
                bool nearWater=new Vector2(at.x-.4f,at.z+25.55f).magnitude<3.6f;
                palette=palette<4?0:palette<7&&nearWater?1:2;
                SampleStone(flagstones,"不规则水磨石",at,new Vector3(.81f+FloorJitter(.12f),.060f,.68f+FloorJitter(.11f)),row*51+col,
                    caveStone[palette],Quaternion.Euler(0,FloorJitter(9),0));
            }
        }
        void BuildReferenceGarden()
        {
            MakeNurseryMaterials();
            var root=Group("苗圃种植与小院",details);
            BuildReferenceNurseryBeds(root);
            var tree=ReferenceAsset("Buildings/courtyard_tree_hero",new Vector3(-14,ReferenceHeight(-14,-23),-23),25);
            if(tree!=null)ReferenceObstacle(tree.transform,new Vector3(0,1.5f,0),new Vector3(.9f,3,.8f));
            BuildReferenceTreeBench(root);
            var pond=new Vector3(ReferencePondX,.055f,ReferencePondZ);
            var v=new List<Vector3>{pond};var tris=new List<int>();var uv=new List<Vector2>{new(.5f,.5f)};
            for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40;v.Add(pond+new Vector3(Mathf.Cos(a)*2.02f,0,Mathf.Sin(a)*1.53f));uv.Add(new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f));tris.AddRange(new[]{0,(i+1)%40+1,i+1});}
            EnsureSampleWaterMaterials();
            referencePondMaterial=new Material(Resources.Load<Shader>("Coast/ReferenceSea")){name="可见池底的庭院清水"};
            referencePondMaterial.SetColor("_BaseColor",new Color(.10f,.48f,.53f));
            referencePondMaterial.SetColor("_ShallowColor",new Color(.22f,.70f,.65f));
            referencePondMaterial.SetTexture("_SkyMap",Resources.Load<Texture2D>("Coast/MorningSkyV1"));
            materials.Add(referencePondMaterial);
            MeshObject("小院清水池",root,v,tris,referencePondMaterial,false,uv);
            for(int i=0;i<28;i++)
            {
                float a=i*Mathf.PI*2/28+.025f*Mathf.Sin(i*3.7f),r=1+.025f*Mathf.Sin(i*5.1f);
                SampleStone(root,"池边卵石",pond+new Vector3(Mathf.Cos(a)*2.13f*r,.02f,Mathf.Sin(a)*1.63f*r),
                    new Vector3(.46f+.07f*Mathf.Sin(i*1.9f),.20f+.05f*Mathf.Sin(i*2.1f),.35f+.05f*Mathf.Cos(i*3.1f)),i*19,limestone,
                    Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));
            }
            for(int i=0;i<24;i++)
            {
                float a=i*2.399963f,r=Mathf.Sqrt((i+.5f)/24f)*.84f;
                SampleStone(root,"浅池底卵石",new Vector3(pond.x+Mathf.Cos(a)*1.85f*r,-.48f,pond.z+Mathf.Sin(a)*1.28f*r),
                    new Vector3(.29f+.08f*Mathf.Sin(i*3.1f),.12f,.21f+.06f*Mathf.Cos(i*2.7f)),501+i,wetStone,Quaternion.Euler(0,i*71,0));
            }
            var patches=new[]{new Vector2(-16,-27),new Vector2(-18,-16),new Vector2(-10,-31),new Vector2(-5,-24),new Vector2(-6,-20),new Vector2(-22,8),new Vector2(-16,13),new Vector2(0,8),new Vector2(3,-12),new Vector2(8,-23),new Vector2(12,-26),new Vector2(31,-9),new Vector2(30,2),new Vector2(23,11),new Vector2(7,10)};
            for(int k=0;k<patches.Length;k++)for(int n=0;n<8;n++)
            {
                var p=patches[k]+new Vector2((n-3.5f)*.80f,Mathf.Sin(n*2+k)*.38f);
                if(Mathf.Pow((p.x-ReferencePondX)/2.68f,2)+Mathf.Pow((p.y-ReferencePondZ)/2.10f,2)<1)continue;
                if(ReferenceRidgePathContains(p.x,p.y,.65f))continue;
                float y=ReferenceHeight(p.x,p.y);if(y<ReferenceSeaLevel+.2f)continue;
                var name=k%5==3?"lavender":n%3==0?"gold":"cream";
                var plant=ReferenceAsset("Geology/flowers_"+name,new Vector3(p.x,y-.06f,p.y),k*31+n*73);
                if(plant!=null)plant.transform.localScale=Vector3.one*(.80f+.09f*(n%3));
            }
            BuildReferenceRockDressing(root);

        }
        Material referencePavingMaterial;
        public void RefreshReferenceSurface()
        {
            var ocean=GameObject.Find("Ocean");if(ocean!=null){var at=ocean.transform.position;at.y=ReferenceSeaLevel;ocean.transform.position=at;}
            if(referencePavingMaterial==null)
            {
                referencePavingMaterial=new Material(Resources.Load<Material>("GardenDetail/Meadow")){name="花园地块 · 与旧岸连续的草土"};
                materials.Add(referencePavingMaterial);
            }
            SmallWorldGardenSurface.ConfigureMeadowMaterial(referencePavingMaterial,game.expansion.finalPosition);
            // Respect the player's saved ground style. A scene refresh must not
            // repaint an active meadow as stone or switch off its fine grass.
            var surface=game.expansion.land.GetComponent<SmallWorldGardenSurface>();
            if(surface!=null&&surface.Active){surface.RestoreMeadowLook();return;}
            var renderer=game.expansion.land.GetComponent<Renderer>();
            if(renderer!=null){renderer.sharedMaterial=referencePavingMaterial;renderer.SetPropertyBlock(null);}
        }
        void BuildReferenceWhiteApron()
        {
            // Keep the original support and walkable outline, but use one meadow
            // shader on both sides of this seam. Soil and irregular turf carry the
            // eye into the surrounding path instead of framing a circular carpet.
            var angles=new List<float>();
            for(int i=0;i<144;i++)angles.Add(i*Mathf.PI*2/144);
            foreach(float x in new[]{5.3f,18.3f})foreach(float z in new[]{-5.6f,5.6f})
            {float angle=Mathf.Atan2(z,x-11.7f);if(angle<0)angle+=Mathf.PI*2;angles.Add(angle);}
            angles.Sort();int columns=angles.Count;const int rings=8;
            var v=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();var uv2=new List<Vector2>();
            for(int ring=0;ring<=rings;ring++)foreach(float a in angles)
            {
                float dx=Mathf.Cos(a),dz=Mathf.Sin(a),edge=1+.02f*Mathf.Sin(a*3)+.012f*Mathf.Cos(a*7);
                var inside=new Vector2(11.7f+dx*6.15f*edge,dz*5.15f*edge);
                var direction=inside-new Vector2(11.7f,0);
                float tx=direction.x>=0?(18.3f-11.7f)/Mathf.Max(.00001f,direction.x):(5.3f-11.7f)/Mathf.Min(-.00001f,direction.x);
                float tz=direction.y>=0?5.6f/Mathf.Max(.00001f,direction.y):-5.6f/Mathf.Min(-.00001f,direction.y);
                var outside=new Vector2(11.7f,0)+direction*Mathf.Min(tx,tz);
                var p=Vector2.Lerp(inside,outside,ring/(float)rings);
                v.Add(new Vector3(p.x,.032f,p.y));colors.Add(new Color(.84f,.81f,.73f,1));uv2.Add(Vector2.zero);
            }
            for(int ring=0;ring<rings;ring++)for(int i=0;i<columns;i++)
            {
                int a=ring*columns+i,b=ring*columns+(i+1)%columns,c=a+columns,d=b+columns;
                indices.AddRange(new[]{a,b,c,b,d,c});
            }
            var apron=MeshObject("花园承接带 · 与原地块连续的草土",details,v,indices,referencePavingMaterial);
            var mesh=apron.GetComponent<MeshFilter>().sharedMesh;mesh.SetColors(colors);mesh.SetUVs(1,uv2);
        }
        void BuildReferenceRockDressing(Transform root)
        {
            var positions=new List<Vector2>();
            for(int i=0;i<ReferenceOutline.Length;i++)
            {
                var a=ReferenceOutline[i];var b=ReferenceOutline[(i+1)%ReferenceOutline.Length];
                for(int j=0;j<3;j++)
                {
                    var p=Vector2.Lerp(a,b,(j+.25f)/3);if(p.x>19&&p.y<-15)continue;
                    if(p.x>-3&&p.x<13&&p.y<-28)continue;positions.Add(p);
                }
            }
            for(float x=-24;x<36;x+=4.1f)for(float z=-33;z<31;z+=4.2f)
            {
                if(x>-6&&x<12&&z>-27&&z<-15)continue;
                if(x>4&&x<20&&z>-6&&z<6)continue;
                float h=ReferenceHeight(x,z),dx=ReferenceHeight(x+.7f,z)-ReferenceHeight(x-.7f,z),dz=ReferenceHeight(x,z+.7f)-ReferenceHeight(x,z-.7f);
                if(h>ReferenceSeaLevel+.6f&&dx*dx+dz*dz>7.5f)positions.Add(new Vector2(x,z));
            }
            for(int n=0;n<22;n++)
            {
                float a=n*Mathf.PI*2/22;
                var p=new Vector3(5+Mathf.Cos(a)*29,ReferenceSeaLevel-.65f,-7+Mathf.Sin(a)*29);
                if(p.z>-8||p.x>25)continue;
                var reef=ReferenceAsset("Geology/coast_reef",p,n*71);
                if(reef!=null){reef.transform.localScale=Vector3.one*(1.2f+.22f*(n%4));SetReferenceRockMap(reef,"coast_reef");}
            }
            int index=0;
            foreach(var p in positions)
            {
                if(ReferenceRidgePathContains(p.x,p.y,1.8f))continue;
                float y=ReferenceHeight(p.x,p.y);var id=index%5==0?"coast_crag":"coast_rock";
                var rock=ReferenceAsset("Geology/"+id,new Vector3(p.x,y-1.40f,p.y),index*73);
                if(rock!=null){rock.transform.localScale=Vector3.one*(.85f+(index%4)*.15f);SetReferenceRockMap(rock,id);}
                index++;
            }
        }
        void SetReferenceRockMap(GameObject obj,string id)
        {
            var map=Resources.Load<Texture2D>("CoastReference/Geology/"+id+"_basecolor");
            var normal=Resources.Load<Texture2D>("CoastReference/Geology/"+id+"_normal");
            foreach(var r in obj.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)
            {
                m.color=new Color(1.10f,1.04f,1.02f);if(map!=null)m.SetTexture("_BaseMap",map);
                if(normal!=null){m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",.45f);m.EnableKeyword("_NORMALMAP");}
            }
        }
        void BuildReferenceShallowReefs()
        {
            var root=Group("浅湾内可见的低礁群",details);
            var random=new System.Random(62853);int count=0;
            for(int i=0;i<38&&count<20;i++)
            {
                float x=-1.5f+(float)random.NextDouble()*17.5f;
                float z=-28.6f-(float)random.NextDouble()*8.4f;
                float floor=ReferenceHeight(x,z);
                if(floor>ReferenceSeaLevel-.24f||floor<ReferenceSeaLevel-2.0f)continue;
                float scale=.52f+(float)random.NextDouble()*.48f;
                var reef=ReferenceAsset("Geology/coast_reef",new Vector3(x,floor-.12f,z),random.Next(360));
                if(reef==null)continue;
                reef.transform.SetParent(root,true);reef.transform.localScale=new Vector3(scale,scale*.48f,scale*.85f);
                SetReferenceRockMap(reef,"coast_reef");count++;
            }
            var bedSource=Resources.Load<GameObject>("CoastReference/Geology/coast_reef");
            var bedShape=bedSource==null?null:ReferenceForeshoreGeometry.Read(bedSource);
            if(bedShape!=null)
            {
                var centers=new[]{new Vector2(-1,-33.5f),new Vector2(5,-36),new Vector2(11,-34),new Vector2(17,-32.8f)};
                for(int i=0;i<centers.Length;i++)
                {
                    var p=centers[i];float bed=ReferenceHeight(p.x,p.y);
                    if(bed>ReferenceSeaLevel-.25f||bed<ReferenceSeaLevel-3.1f)continue;
                    float height=Mathf.Min(.68f,ReferenceSeaLevel-bed-.16f);
                    var scale=new Vector3((4.4f+i*.23f)/bedShape.bounds.size.x,height/bedShape.bounds.size.y,(2.4f+(i%2)*.5f)/bedShape.bounds.size.z);
                    var rotation=Quaternion.Euler(0,19+i*43,0);
                    var pivot=new Vector3(bedShape.bounds.center.x,bedShape.bounds.min.y,bedShape.bounds.center.z);
                    var at=new Vector3(p.x,bed-.12f,p.y)-rotation*Vector3.Scale(scale,pivot);
                    var reef=ReferenceAsset("Geology/coast_reef",at,19+i*43);
                    if(reef==null)continue;
                    reef.name="浅湾水下连片岩床 · "+i;reef.transform.SetParent(root,true);reef.transform.localScale=scale;
                    SetReferenceRockMap(reef,"coast_reef");
                }
            }
        }
        void BuildReferenceWater()
        {
            EnsureSampleWaterMaterials();var root=Group("连通阶梯水渠",details);
            for(int k=0;k<ReferenceStream.Length-3;k++)
            {
                var a=ReferenceStream[k];var b=ReferenceStream[k+1];var side=Vector3.Cross(Vector3.up,(b-a).normalized).normalized;
                if(a.y-b.y>3)
                {
                    BuildReferenceCascade(root,a,b,side,ReferenceStreamHalfWidths[k],ReferenceStreamHalfWidths[k+1]);
                    continue;
                }
                if(a.y-b.y>.5f)
                {
                    const int columns=14,rows=26;
                    var sheet=new List<Vector3>();var mapping=new List<Vector2>();var faces=new List<int>();
                    var direction=HorizontalDirection(b-a);
                    float length=Vector3.Distance(a,b);
                    for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++)
                    {
                        float t=row/(float)rows,u=col/(float)columns;
                        float edge=Mathf.Lerp(ReferenceStreamHalfWidths[k],ReferenceStreamHalfWidths[k+1],t);
                        edge*=1-.19f*Mathf.Sin(t*Mathf.PI)+.07f*t*t;
                        float span=(u*2-1)*edge;
                        var point=Vector3.Lerp(a,b,t)+side*(span+.035f*Mathf.Sin(t*9)*Mathf.Sin(t*Mathf.PI));
                        // A curved lip accelerates into the fall. The endpoints
                        // stay on the surveyed channel and receiving pool.
                        point.y=Mathf.Lerp(a.y,b.y,t*t*.35f+t*.65f);
                        point+=direction*((a.y-b.y>3?.24f:.13f)*Mathf.Sin(t*Mathf.PI)+.035f*Mathf.Sin(u*11+t*8)*Mathf.Sin(t*Mathf.PI));
                        point.y+=.025f*Mathf.Sin(u*17+t*11)*Mathf.Sin(t*Mathf.PI);
                        sheet.Add(point);mapping.Add(new Vector2(u,t*length));
                        if(row<rows&&col<columns){int n=row*(columns+1)+col;faces.AddRange(new[]{n,n+1,n+columns+1,n+1,n+columns+2,n+columns+1});}
                    }
                    MeshObject("收束后散开的弧形跌水",root,sheet,faces,a.y-b.y>3?caveFallSurface:fallingSurface,false,mapping);
                    FoamingRunoff(root,b,direction,ReferenceStreamHalfWidths[k+1],a.y-b.y>3);
                    continue;
                }
                var v=new List<Vector3>{a-side*ReferenceStreamHalfWidths[k],a+side*ReferenceStreamHalfWidths[k],b-side*ReferenceStreamHalfWidths[k+1],b+side*ReferenceStreamHalfWidths[k+1]};
                var uv=new List<Vector2>{new(0,0),new(1,0),new(0,Vector3.Distance(a,b)),new(1,Vector3.Distance(a,b))};
                MeshObject("溪流 "+k,root,v,new List<int>{0,1,2,1,3,2},a.y-b.y>3?caveFallSurface:a.y-b.y>.5f?fallingSurface:riverSurface,false,uv);
                if(a.y-b.y>.5f)FoamingRunoff(root,b,HorizontalDirection(b-a),ReferenceStreamHalfWidths[k+1],a.y-b.y>3);
            }
        }
    }
}

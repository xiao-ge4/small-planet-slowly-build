using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DawnGarden
{
    public sealed class SmallWorldMapTarget : MonoBehaviour {public int continent,planet;public bool planetTarget;}
    public sealed partial class SmallWorldApp
    {
        GameObject worldRoot,planetRoot,starRoot;Camera mapCamera;
        Transform[] continentRoots=new Transform[4];Vector3[] centers=new Vector3[4];
        Vector3[] mapMarkers=new Vector3[4];
        float mapYaw=30,mapPitch=22,mapDistance=10,dragDistance;bool stars;
        int mapPlanet,selectedContinent;
        const int MapLayer=30;
        Material MapMaterial(Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.28f);return material;
        }
        void CreateWorldMap()
        {
            worldRoot=new GameObject("Small World Atlas");DontDestroyOnLoad(worldRoot);
            mapCamera=new GameObject("Atlas Camera").AddComponent<Camera>();mapCamera.transform.SetParent(worldRoot.transform);mapCamera.cullingMask=1<<MapLayer;mapCamera.clearFlags=CameraClearFlags.SolidColor;mapCamera.backgroundColor=new Color(.075f,.16f,.20f);mapCamera.nearClipPlane=.05f;mapCamera.farClipPlane=80;
            var light=new GameObject("Atlas dawn").AddComponent<Light>();light.transform.SetParent(worldRoot.transform);light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(40,-30,0);light.cullingMask=1<<MapLayer;
            planetRoot=new GameObject("Four continent planet");planetRoot.transform.SetParent(worldRoot.transform);
            var ocean=GameObject.CreatePrimitive(PrimitiveType.Sphere);ocean.name="Ocean globe";ocean.layer=MapLayer;ocean.transform.SetParent(planetRoot.transform,false);ocean.transform.localScale=Vector3.one*6.8f;ocean.GetComponent<Renderer>().sharedMaterial=MapMaterial(new Color(.10f,.48f,.58f));Destroy(ocean.GetComponent<Collider>());
            var random=new System.Random(Profile.worldSeed);var rotation=Quaternion.Euler((float)random.NextDouble()*360,(float)random.NextDouble()*360,(float)random.NextDouble()*360);
            var tetra=new[]{new Vector3(1,1,1),new Vector3(1,-1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1)};
            for(var i=0;i<4;i++)
            {
                centers[i]=rotation*tetra[i].normalized;
                var go=new GameObject("Continent "+i);go.layer=MapLayer;go.transform.SetParent(planetRoot.transform,false);continentRoots[i]=go.transform;
                go.AddComponent<MeshFilter>().sharedMesh=ContinentMesh(i,centers[i]);
                var color=i==Profile.ownContinent?(Profile.theme=="ember"?new Color(.76f,.44f,.20f):new Color(.43f,.64f,.34f)):i%2==0?new Color(.55f,.67f,.38f):new Color(.74f,.51f,.31f);
                go.AddComponent<MeshRenderer>().sharedMaterial=MapMaterial(color);
                go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
                var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Home arrival "+i;marker.layer=MapLayer;marker.transform.SetParent(go.transform,false);
                var tangent=Vector3.Cross(centers[i],Vector3.up).normalized;if(tangent.sqrMagnitude<.1f)tangent=Vector3.right;
                var offset=(float)(random.NextDouble()-.5)*.32f;
                mapMarkers[i]=(centers[i]+tangent*offset).normalized*3.55f;marker.transform.localPosition=mapMarkers[i];marker.transform.localScale=Vector3.one*.17f;
                marker.GetComponent<Renderer>().sharedMaterial=MapMaterial(i==Profile.ownContinent?new Color(1,.83f,.32f):new Color(.95f,.92f,.78f));
                var target=marker.AddComponent<SmallWorldMapTarget>();target.continent=i;target.planet=0;
                go.AddComponent<SmallWorldMapTarget>().continent=i;
            }
            starRoot=new GameObject("Demo star routes");starRoot.transform.SetParent(worldRoot.transform);
            for(var i=0;i<3;i++)
            {
                var p=GameObject.CreatePrimitive(PrimitiveType.Sphere);p.name="Planet "+i;p.layer=MapLayer;p.transform.SetParent(starRoot.transform,false);p.transform.localPosition=new Vector3((i-1)*5.2f,0,0);p.transform.localScale=Vector3.one*(i==0?2.8f:2.3f);
                p.GetComponent<Renderer>().sharedMaterial=MapMaterial(i==0?new Color(.15f,.56f,.64f):i==1?new Color(.68f,.39f,.24f):new Color(.42f,.52f,.68f));
                var target=p.AddComponent<SmallWorldMapTarget>();target.planet=i;target.planetTarget=true;
            }
            starRoot.SetActive(false);mapCamera.enabled=false;worldRoot.SetActive(false);
        }
        Mesh ContinentMesh(int index,Vector3 center)
        {
            var tangent=Vector3.Cross(center,Vector3.up).normalized;if(tangent.sqrMagnitude<.1f)tangent=Vector3.right;var bitangent=Vector3.Cross(center,tangent);
            var v=new List<Vector3>{center*3.46f};var tris=new List<int>();const int rays=64,rings=5;
            for(var ring=1;ring<=rings;ring++)for(var j=0;j<rays;j++)
            {
                var a=j*Mathf.PI*2/rays;var radius=(.43f+.065f*Mathf.Sin(a*3+index*2)+.04f*Mathf.Cos(a*5+index))*(ring/(float)rings);
                v.Add((center*Mathf.Cos(radius)+(tangent*Mathf.Cos(a)+bitangent*Mathf.Sin(a))*Mathf.Sin(radius))*3.46f);
            }
            for(var j=0;j<rays;j++)tris.AddRange(new[]{0,1+j,1+(j+1)%rays});
            for(var ring=1;ring<rings;ring++)for(var j=0;j<rays;j++)
            {var a=1+(ring-1)*rays+j;var b=1+(ring-1)*rays+(j+1)%rays;var c=1+ring*rays+j;var d=1+ring*rays+(j+1)%rays;tris.AddRange(new[]{a,c,d,a,d,b});}
            var mesh=new Mesh {name="Irregular isolated continent "+index};mesh.SetVertices(v);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();return mesh;
        }
        void SetWorldVisible(bool value)
        {
            if(worldRoot!=null)worldRoot.SetActive(value);if(mapCamera!=null)mapCamera.enabled=value;
            if(Game?.worldCamera!=null)Game.worldCamera.enabled=!value;
        }
        void OpenWorld()
        {
            if(GuardianBusy)return;
            if(Game!=null&&Game.Busy){Toast("等当前照料动作结束，再出发吧。");return;}
            gameSettingsOpen=false;WorldOpen=true;ChatOpen=false;creatorOpen=false;editorOpen=false;selectedContinent=continentIndex;mapPlanet=planetIndex;stars=false;mapDistance=10;
            var normal=continentRoots[selectedContinent].TransformPoint(mapMarkers[selectedContinent]).normalized;mapPitch=Mathf.Asin(normal.y)*Mathf.Rad2Deg;mapYaw=Mathf.Atan2(-normal.x,-normal.z)*Mathf.Rad2Deg;
            if(planetRoot!=null)planetRoot.SetActive(true);if(starRoot!=null)starRoot.SetActive(false);SetWorldVisible(true);UpdateControls();
        }
        void FocusMapContinent(int index)
        {
            selectedContinent=index;mapDistance=10;var normal=continentRoots[index].TransformPoint(mapMarkers[index]).normalized;
            mapPitch=Mathf.Asin(normal.y)*Mathf.Rad2Deg;mapYaw=Mathf.Atan2(-normal.x,-normal.z)*Mathf.Rad2Deg;
        }
        void UpdateWorldMap()
        {
            if(worldRoot==null||!worldRoot.activeSelf)return;
            var age=(float)(DateTime.UtcNow.TimeOfDay.TotalSeconds/3600);
            for(var i=0;i<4;i++)
            {
                var tangent=Vector3.Cross(centers[i],Vector3.up).normalized;
                var drift=Vector3.Cross(centers[i],tangent)*Mathf.Sin(age*Mathf.PI*2/(9+i*2)+i)*.022f;
                continentRoots[i].rotation=Quaternion.FromToRotation(centers[i],(centers[i]+drift).normalized);
            }
            var mouse=Mouse.current;
            if(WorldOpen&&mouse!=null)
            {
                var screen=mouse.position.ReadValue();var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);var ui=new Vector2(screen.x/scale,(Screen.height-screen.y)/scale);
                var overUI=new Rect(20,20,300,530).Contains(ui)||new Rect(938,565,320,140).Contains(ui);
                if(mouse.leftButton.wasPressedThisFrame)dragDistance=0;
                if(!overUI&&(mouse.rightButton.isPressed||mouse.leftButton.isPressed))
                {var d=mouse.delta.ReadValue();mapYaw+=d.x*.28f;mapPitch-=d.y*.28f;dragDistance+=d.magnitude;}
                if(!overUI)
                {
                    var scroll=mouse.scroll.ReadValue().y;mapDistance=Mathf.Clamp(mapDistance-scroll*.012f,4.15f,18);
                    if(scroll>0&&!stars&&Physics.Raycast(mapCamera.ScreenPointToRay(screen),out var underPointer,100,1<<MapLayer))
                    {var target=underPointer.collider.GetComponent<SmallWorldMapTarget>();if(target!=null&&!target.planetTarget)selectedContinent=target.continent;}
                    if(mouse.leftButton.wasReleasedThisFrame&&dragDistance<8 && Physics.Raycast(mapCamera.ScreenPointToRay(screen),out var hit,100,1<<MapLayer))
                    {var target=hit.collider.GetComponent<SmallWorldMapTarget>();if(target!=null){if(target.planetTarget){mapPlanet=target.planet;stars=false;starRoot.SetActive(false);planetRoot.SetActive(true);mapDistance=10;}else selectedContinent=target.continent;}}
                    if(!stars&&scroll>0 && mapDistance<4.35f && !loading)
                    {var normal=continentRoots[selectedContinent].TransformPoint(mapMarkers[selectedContinent]).normalized;if(Vector3.Dot(normal,(mapCamera.transform.position).normalized)>.35f)StartCoroutine(Visit(mapPlanet,selectedContinent));}
                }
            }
            if(stars){mapCamera.transform.position=new Vector3(0,4,-16);mapCamera.transform.LookAt(Vector3.zero);}
            else
            {
                var focus=mapDistance<7?continentRoots[selectedContinent].TransformPoint(mapMarkers[selectedContinent])*Mathf.InverseLerp(7,4.15f,mapDistance)*.65f:Vector3.zero;
                mapCamera.transform.position=Quaternion.Euler(mapPitch,mapYaw,0)*Vector3.back*mapDistance;mapCamera.transform.LookAt(focus);
            }
        }
        IEnumerator Visit(int planet,int continent)
        {
            if(SubmissionSwitchBusy){Toast("等当前家园加载完成后再出发。");yield break;}var own=planet==0&&continent==Profile.ownContinent;
            if(planet==planetIndex&&continent==continentIndex&&Game!=null){WorldOpen=false;SetWorldVisible(false);UpdateControls();yield break;}
            yield return LoadNeighbors();if(loading||GuardianBusy)yield break;var destination=AtResident(planet,continent);
            if(!own&&destination==null){Toast("这块大陆暂时还没有住户。");yield break;}
            if(!SaveBeforeWorldTransition())yield break;
            loading=true;yield return SyncState();Profile.Save();
            planetIndex=planet;continentIndex=continent;visitTheme=own?Profile.theme:destination.theme;
            yield return LoadWorldScene(own?(Profile.theme=="ember"?"EmberHomeV1":"DawnHomeV1"):destination.scene);loading=false;
        }
        string MapHomeName(int p,int c)=>p==0&&c==Profile.ownContinent?"我的家园":AtResident(p,c)!=null?AtResident(p,c).name+"的家园":"尚未入住的小岛";
        void DrawWorldInterface()
        {
            GUILayout.BeginArea(new Rect(22,20,295,stars?280:260),panel);GUILayout.Label(stars?"星际航线":mapPlanet==0?"你的星球":mapPlanet==1?"星露之星":"风铃之星",title);
            GUILayout.Label(stars?"星球层 · 选择星球，再选择大陆与朋友":"大陆层 · 四块大陆，四个小世界",muted);
            GUILayout.Label(stars?"选择一颗星球，继续查看大陆与家园":"拖动自由转动 · 滚轮拉近家园",label);GUILayout.Label("拜访与回家均免费",muted);
            if(GUILayout.Button(stars?"回到星球":"查看其他星球",button)){stars=!stars;starRoot.SetActive(stars);planetRoot.SetActive(!stars);}
            if(Game!=null&&GUILayout.Button("关闭地图",button)){WorldOpen=false;SetWorldVisible(false);UpdateControls();}GUILayout.EndArea();
            if(!stars)
            {
                GUILayout.BeginArea(new Rect(22,296,295,139),panel);GUILayout.Label("住在这里的朋友",muted);
                for(var row=0;row<2;row++){GUILayout.BeginHorizontal();for(var col=0;col<2;col++){var index=row*2+col;if(GUILayout.Button(mapPlanet==0&&index==Profile.ownContinent?"我的家园":AtResident(mapPlanet,index)?.name??"未入住",button))FocusMapContinent(index);}GUILayout.EndHorizontal();}GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(22,451,295,70),panel);GUILayout.BeginHorizontal();if(GUILayout.Button("←",button))mapYaw-=45;if(GUILayout.Button("→",button))mapYaw+=45;if(GUILayout.Button("↑",button))mapPitch+=45;if(GUILayout.Button("↓",button))mapPitch-=45;GUILayout.EndHorizontal();GUILayout.EndArea();
                for(var i=0;i<4;i++)
                {
                    var point=continentRoots[i].TransformPoint(mapMarkers[i]);
                    if(Vector3.Dot(point.normalized,mapCamera.transform.position.normalized)<.1f)continue;
                    var p=mapCamera.WorldToScreenPoint(point);var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);if(p.z<=0)continue;
                    var own=mapPlanet==0&&i==Profile.ownContinent;
                    if(!own&&AtResident(mapPlanet,i)==null)continue;
                    if(GUI.Button(new Rect(p.x/scale-85,(Screen.height-p.y)/scale-15,170,36),own?"我的家园":AtResident(mapPlanet,i).name,button))selectedContinent=i;
                }
            }
            else for(var i=0;i<3;i++)
            {
                var point=mapCamera.WorldToScreenPoint(starRoot.transform.GetChild(i).position);var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
                if(GUI.Button(new Rect(point.x/scale-80,(Screen.height-point.y)/scale+70,160,40),i==0?"我的星球":i==1?"星露之星":"风铃之星",button))
                {mapPlanet=i;stars=false;starRoot.SetActive(false);planetRoot.SetActive(true);FocusMapContinent(i==0?Profile.ownContinent:neighborWorld.residents.FirstOrDefault(r=>r.planet==i)?.continent??0);}
            }
            GUILayout.BeginArea(new Rect(938,565,320,140),panel);
            GUILayout.Label(stars?"下一站，去哪里？":MapHomeName(mapPlanet,selectedContinent)??"还没有住户的小岛",title);
            var ownTarget=mapPlanet==0&&selectedContinent==Profile.ownContinent;var resident=AtResident(mapPlanet,selectedContinent);
            if(!stars&&mapPlanet==planetIndex&&selectedContinent==continentIndex)GUILayout.Label("你正在这里 · 慢慢享受今天",muted);
            GUI.enabled=ownTarget||resident!=null;
            if(!stars&&!(mapPlanet==planetIndex&&selectedContinent==continentIndex)&&GUILayout.Button(mapPlanet==planetIndex&&selectedContinent==continentIndex?"关闭地图":ownTarget?"进入我的家园":resident!=null?"拜访 "+resident.name:"这块大陆尚未入住",button))StartCoroutine(Visit(mapPlanet,selectedContinent));GUI.enabled=true;
            if(IsVisitor&&GUILayout.Button("回到自己的家园",button))StartCoroutine(Visit(0,Profile.ownContinent));GUILayout.EndArea();
        }
    }
}

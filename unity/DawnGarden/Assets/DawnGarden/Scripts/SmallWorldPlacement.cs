using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        bool editorOpen,editorBusy,previewValid,editSeamSoftened;int selectedItem=-1;
        string editSurfaceStyle="";SmallWorldGardenSurface gardenSurface;
        bool restoringOwnedLayout;
        List<SmallWorldItem> renderedItems=new List<SmallWorldItem>();
        List<SmallWorldItem> editItems=new List<SmallWorldItem>();List<string> placementUndo=new List<string>(),placementRedo=new List<string>();
        SmallWorldAsset[] availableAssets;SmallWorldLayout committedLayout=new SmallWorldLayout();Transform placedRoot;
        Vector2 paletteScroll,dragPointer;string placementError="";bool draggingItem,dragMoved;Vector3 dragStart,dragOffset;string dragBefore;
        [Serializable] public class PreviewChecks {public bool groundConnected,westRoute,boundsValid,materialsValid,collisionsValid,actorGrounded;}
        [Serializable] public class PreviewRequest
        {
            public string clientId,runId,buildId,image;
            public int expectedVersion,lightingHour=12;
            public bool seamNatural=true;
            public bool seamSoftened;
            public string surfaceStyle="";
            public SmallWorldItem[] instances;
            public PreviewChecks checks;
        }
        [Serializable] public class PreviewReply {public string runId,previewToken;public int version;}
        [Serializable] public class FinalRequest {public string clientId,runId,previewToken;public bool visualAccepted=true,visualOverrideAccepted;}
        public string LayoutFile=>Profile.HomeSavePath("committed-layout");
        SmallWorldAsset FindAsset(SmallWorldItem i)=>(availableAssets??library.assets).FirstOrDefault(a=>a.id==i.assetId && a.version==i.assetVersion && a.hash==i.assetHash);
        bool ReadLayoutSave(string path,out SmallWorldLayout value)
        {
            value=null;
            foreach(var candidate in new[]{path,path+".bak"})
            {
                if(!File.Exists(candidate))continue;
                try
                {
                    var saved=JsonUtility.FromJson<SmallWorldLayout>(File.ReadAllText(candidate));
                    if(saved==null||saved.version<0||saved.instances==null||saved.instances.Length>50||saved.instances.Any(i=>i==null||string.IsNullOrEmpty(i.instanceId)))continue;
                    value=saved;if(candidate!=path)Toast("已从备份恢复上次保存的布局。");return true;
                }
                catch(Exception){ }
            }
            return false;
        }
        bool WriteLayoutSave(string path,string json)
        {
            try{var temporary=path+".tmp";File.WriteAllText(temporary,json);SmallWorldBrowserStorage.Commit(temporary,path,path+".bak");return true;}
            catch(Exception){Toast("本机布局文件未能写入，已有文件与服务端存档已保留。");return false;}
        }
        IEnumerator RestoreOwnedLayout()
        {
            var epoch=worldSceneEpoch;
            var layoutPath=LayoutFile;
            restoringOwnedLayout=true;
            // This component survives scene and profile switches. A profile
            // without a layout must start at its own version zero.
            committedLayout=new SmallWorldLayout();
            editItems.Clear();renderedItems.Clear();
            editSeamSoftened=false;editSurfaceStyle="";savedDraft="";
            availableAssets=library.assets;
            yield return SmallWorldApi.Request("assets",null,json=>{if(epoch==worldSceneEpoch)availableAssets=JsonUtility.FromJson<SmallWorldAssetList>(json).assets;},_=>{});
            if(epoch!=worldSceneEpoch||Game==null)yield break;
            if(ReadLayoutSave(layoutPath,out var local))committedLayout=local;
            else if(File.Exists(layoutPath))Toast("旧布局未能读取，文件已保留。");
            yield return SmallWorldApi.Request("layout/"+Profile.clientId,null,json=>{
                if(epoch!=worldSceneEpoch)return;
                var remote=JsonUtility.FromJson<SmallWorldLayout>(json);
                if(remote?.instances==null)return;
                if(remote.version>=committedLayout.version){committedLayout=remote;WriteLayoutSave(layoutPath,json);}
                else Toast("服务端布局早于本机存档，已保留本机家园。请恢复对应的服务端数据后再应用修改。");
            },_=>{});
            if(epoch!=worldSceneEpoch||Game==null)yield break;
            if(Game.Progress.firstExpansionUnlocked && (committedLayout.instances.Length>0||committedLayout.seamSoftened||!string.IsNullOrEmpty(committedLayout.surfaceStyle)))
            {editItems=committedLayout.instances.ToList();editSeamSoftened=committedLayout.seamSoftened;editSurfaceStyle=committedLayout.surfaceStyle;yield return PreviewPlacement(false);}
            if(epoch==worldSceneEpoch)restoringOwnedLayout=false;
        }
        public bool OpenPlacement(bool autoPreview=true)
        {
            if(GuardianBusy)return false;
            if(restoringOwnedLayout){Toast("正在恢复家园布局，稍等一下。");return false;}
            if(editorBusy){Toast("正在载入物件，稍等一下。");return false;}
            if(IsVisitor || Game?.Progress==null || !GardenGuardianLand.CanCreate(Game.Progress) || Game.expansion.IsRising){Toast("等自己的新土地升起后，再来一起布置。");return false;}
            Game.CancelMovement();visualReviewMessage="";visualReviewFinished=false;visualReviewPassed=false;visualOverrideAccepted=false;editorOpen=true;ChatOpen=false;creatorOpen=false;WorldOpen=false;SetWorldVisible(false);UpdateControls();
            editItems=JsonUtility.FromJson<SmallWorldLayout>(JsonUtility.ToJson(committedLayout)).instances.ToList();editSeamSoftened=committedLayout.seamSoftened;editSurfaceStyle=committedLayout.surfaceStyle;selectedItem=-1;placementUndo.Clear();placementRedo.Clear();previewValid=false;
            var draft=Profile.HomeSavePath("layout-draft");if(ReadLayoutSave(draft,out var d)){if(d.version==committedLayout.version){editItems=d.instances.ToList();editSeamSoftened=d.seamSoftened;editSurfaceStyle=d.surfaceStyle;}else Toast("旧草稿来自更早布局，保留文件并打开当前家园。");}
            Game.orbit.FramePlacement(Game.expansion.finalPosition);
            if(autoPreview)StartCoroutine(PreviewPlacement(false));return true;
        }
        string DraftJson()=>JsonUtility.ToJson(new SmallWorldLayout {version=committedLayout.version,instances=editItems.ToArray(),seamSoftened=editSeamSoftened,surfaceStyle=editSurfaceStyle});
        void ReadDraft(string json){var d=JsonUtility.FromJson<SmallWorldLayout>(json);editItems=d.instances.ToList();editSeamSoftened=d.seamSoftened;editSurfaceStyle=d.surfaceStyle;}
        void RememberUndo(){placementUndo.Add(DraftJson());if(placementUndo.Count>60)placementUndo.RemoveAt(0);placementRedo.Clear();previewValid=false;visualOverrideAccepted=false;}
        void DrawPlacement()
        {
            if(!string.IsNullOrEmpty(visualReviewMessage)){GUI.Box(new Rect(22,80,820,72),GUIContent.none,panel);GUI.Label(new Rect(38,90,785,58),visualReviewMessage,muted);}
            if(visualReviewFinished&&!visualReviewBusy&&!visualReviewPassed)visualOverrideAccepted=IllustratedToggle(new Rect(28,158,800,35),visualOverrideAccepted,"以我看到的效果保存，保留检查意见");
            if(GUI.Button(new Rect(22,30,130,40),Game.orbit.distance>16?"近看景观":"查看全貌",button))
                Game.orbit.FramePlacement(Game.expansion.finalPosition, Game.orbit.distance>16);
            HandlePlacementGuiEvent();GUILayout.BeginArea(new Rect(910,60,350,550),panel);GUILayout.Label("布置我的花园",title);GUILayout.Label("选一件喜欢的物件，再拖到合适的位置",muted);
            GUI.enabled=!editorBusy&&!draggingItem&&!visualReviewBusy;
            paletteScroll=GUILayout.BeginScrollView(paletteScroll,GUILayout.Height(selectedItem>=0&&selectedItem<editItems.Count?120:260));
            foreach(var gift in neighborWorld.gifts??new Gift[0])
            {
                var asset=(availableAssets??library.assets).FirstOrDefault(a=>a.id==gift.assetId);if(asset==null)continue;
                if(DrawPaletteAsset(asset,gift.name,"朋友的礼物")){RememberUndo();editItems.Add(new SmallWorldItem {instanceId="gift-"+Guid.NewGuid().ToString("N").Substring(0,12),assetId=asset.id,assetHash=asset.hash,assetVersion=asset.version,giftId=gift.id,tint=gift.tint,localScale=Vector3.one*gift.scale,localPosition=new Vector3(1.7f,0,-1.8f)});selectedItem=editItems.Count-1;StartCoroutine(PreviewPlacement());}
            }
            foreach(var asset in availableAssets??library.assets)
                if(DrawPaletteAsset(asset,asset.name,"可重复摆放"))
                {RememberUndo();editItems.Add(new SmallWorldItem {instanceId=asset.id+"-"+Guid.NewGuid().ToString("N").Substring(0,8),assetId=asset.id,assetVersion=asset.version,assetHash=asset.hash,localPosition=new Vector3(1.7f,0,-1.8f)});selectedItem=editItems.Count-1;StartCoroutine(PreviewPlacement());}
            GUILayout.EndScrollView();
            var selected=selectedItem>=0&&selectedItem<editItems.Count?editItems[selectedItem]:null;
            if(selected!=null)
            {
                GUILayout.Label(FindAsset(selected)?.name??selected.assetId,label);
                GUILayout.BeginHorizontal();if(GUILayout.Button("←",button))ChangeSelected(-.25f,0,0,0);if(GUILayout.Button("→",button))ChangeSelected(.25f,0,0,0);if(GUILayout.Button("前",button))ChangeSelected(0,.25f,0,0);if(GUILayout.Button("后",button))ChangeSelected(0,-.25f,0,0);GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();if(GUILayout.Button("左转",button))ChangeSelected(0,0,-15,0);if(GUILayout.Button("右转",button))ChangeSelected(0,0,15,0);if(GUILayout.Button("缩小",button))ChangeSelected(0,0,0,-.1f);if(GUILayout.Button("放大",button))ChangeSelected(0,0,0,.1f);GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();foreach(var tint in new[]{"#ffffff","#e0d7bd","#b9c796","#daac87"})
                    if(GUILayout.Button(tint=="#ffffff"?"原色":tint=="#e0d7bd"?"暖白":tint=="#b9c796"?"苔绿":"暖橙",button)){RememberUndo();selected.tint=tint;StartCoroutine(PreviewPlacement());}GUILayout.EndHorizontal();
                if(GUILayout.Button("移除物件",button)){RememberUndo();editItems.RemoveAt(selectedItem);selectedItem=-1;StartCoroutine(PreviewPlacement());}
            }
            else GUILayout.Label("点击场景里的已摆放物件来选择。",muted);
            var soft=IllustratedToggle(editSeamSoftened,"连接处更宽缓");if(soft!=editSeamSoftened){RememberUndo();editSeamSoftened=soft;StartCoroutine(PreviewPlacement());}
            var meadow=IllustratedToggle(editSurfaceStyle==SmallWorldGardenSurface.Style,"草土小径与自然岸沿");if(meadow!=(editSurfaceStyle==SmallWorldGardenSurface.Style)){RememberUndo();editSurfaceStyle=meadow?SmallWorldGardenSurface.Style:"";StartCoroutine(PreviewPlacement());}
            GUILayout.BeginHorizontal();GUI.enabled=!editorBusy&&placementUndo.Count>0;if(GUILayout.Button("撤销",button)){placementRedo.Add(DraftJson());var saved=placementUndo[placementUndo.Count-1];placementUndo.RemoveAt(placementUndo.Count-1);ReadDraft(saved);selectedItem=-1;StartCoroutine(PreviewPlacement());}
            GUI.enabled=!editorBusy&&placementRedo.Count>0;if(GUILayout.Button("重做",button)){placementUndo.Add(DraftJson());var saved=placementRedo[placementRedo.Count-1];placementRedo.RemoveAt(placementRedo.Count-1);ReadDraft(saved);selectedItem=-1;StartCoroutine(PreviewPlacement());}GUILayout.EndHorizontal();
            GUI.enabled=true;if(!string.IsNullOrEmpty(placementError))GUILayout.Label(placementError,muted);GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(350,615,910,88),panel);GUILayout.BeginHorizontal();
            GUI.enabled=!editorBusy&&previewValid&&!visualReviewBusy;
            if(GUILayout.Button(visualReviewBusy?"正在检查画面…":"对照设计图检查",button))StartCoroutine(ReviewPlacement());
            if(GUILayout.Button("应用到家园",button))StartCoroutine(FinalizePlacement());GUI.enabled=!editorBusy&&!visualReviewBusy;
            if(GUILayout.Button("稍后继续",button))StartCoroutine(ClosePlacement());GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.EndArea();
        }
        bool DrawPaletteAsset(SmallWorldAsset asset,string name,string hint)
        {
            var rect=GUILayoutUtility.GetRect(280,60,GUILayout.ExpandWidth(true));
            bool pressed=GUI.Button(rect,GUIContent.none,secondaryButton);
            var thumb=Resources.Load<Texture2D>("UIAssetThumbs/"+asset.id);
            GUI.DrawTexture(new Rect(rect.x+6,rect.y+3,54,54),thumb!=null?thumb:SmallWorldUiArt.Icon(asset.id.Contains("lamp")||asset.id.Contains("lantern")?"lamp":"leaf"),ScaleMode.ScaleToFit);
            GUI.Label(new Rect(rect.x+69,rect.y+5,rect.width-78,26),name,new GUIStyle(label){fontSize=15,wordWrap=false});
            GUI.Label(new Rect(rect.x+70,rect.y+33,rect.width-80,22),"＋ "+hint,small);return pressed;
        }
        void ChangeSelected(float x,float z,float angle,float size)
        {
            if(selectedItem<0||selectedItem>=editItems.Count)return;RememberUndo();var i=editItems[selectedItem];i.localPosition+=new Vector3(x,0,z);i.localEulerAngles.y=Mathf.Repeat(i.localEulerAngles.y+angle,360);i.localScale=Vector3.one*Mathf.Clamp(i.localScale.x+size,.1f,4);StartCoroutine(PreviewPlacement());
        }
        int dragBeginFrame;bool dragCandidateValid=true;Vector3 dragCandidate;LineRenderer placementOutline;
        public int SuccessfulPointerDrags {get;private set;}
        void HandlePlacementGuiEvent()
        {
            var e=Event.current;if(editorBusy||visualReviewBusy||e==null)return;
            // IMGUI coordinates follow GUI.matrix; use this event's position rather
            // than the Input System sample, which may already belong to another event.
            var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            var screen=new Vector2(e.mousePosition.x*scale,Screen.height-e.mousePosition.y*scale);
            if(e.type==EventType.MouseDown&&e.button==0&&!draggingItem){if(BeginPlacementDrag(screen))e.Use();}
            else if(e.type==EventType.MouseDrag&&e.button==0&&draggingItem){ContinuePlacementDrag(screen);e.Use();}
            else if(e.type==EventType.MouseUp&&e.button==0&&draggingItem){ContinuePlacementDrag(screen);EndPlacementDrag();e.Use();}
            else if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape&&draggingItem){CancelPlacementDrag();e.Use();}
        }
        bool BeginPlacementDrag(Vector2 screen)
        {
            if(!editorOpen||editorBusy||draggingItem)return false;
            var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);var ui=new Vector2(screen.x/scale,(Screen.height-screen.y)/scale);if(PointerOverUI(ui))return false;
            if(!Physics.Raycast(Game.worldCamera.ScreenPointToRay(screen),out var hit,100,~0,QueryTriggerInteraction.Collide))return false;
            var marker=hit.collider.GetComponentInParent<HomeLayoutInstance>();if(marker==null)return false;
            selectedItem=editItems.FindIndex(i=>i.instanceId==marker.instanceId);if(selectedItem<0)return false;
            draggingItem=true;dragBefore=DraftJson();dragPointer=screen;dragMoved=false;dragBeginFrame=Time.frameCount;dragCandidateValid=true;placementError="";
            dragCandidate=dragStart=editItems[selectedItem].localPosition;var plane=new Plane(Vector3.up,Game.expansion.land.position+Vector3.up*.025f);var ray=Game.worldCamera.ScreenPointToRay(screen);
            dragOffset=plane.Raycast(ray,out var distance)?dragStart-Game.expansion.land.InverseTransformPoint(ray.GetPoint(distance)):Vector3.zero;return true;
        }
        void ContinuePlacementDrag(Vector2 screen)
        {
            if(!draggingItem||selectedItem<0||selectedItem>=editItems.Count||(!dragMoved&&(screen-dragPointer).sqrMagnitude<16))return;
            dragMoved=true;var scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);if(PointerOverUI(new Vector2(screen.x/scale,(Screen.height-screen.y)/scale)))return;
            var plane=new Plane(Vector3.up,Game.expansion.land.position+Vector3.up*.025f);var ray=Game.worldCamera.ScreenPointToRay(screen);if(!plane.Raycast(ray,out var distance))return;
            var local=Game.expansion.land.InverseTransformPoint(ray.GetPoint(distance))+dragOffset;local.y=0;
            bool free=Keyboard.current!=null&&(Keyboard.current.leftShiftKey.isPressed||Keyboard.current.rightShiftKey.isPressed);
            if(!free){local.x=Mathf.Round(local.x*10)/10;local.z=Mathf.Round(local.z*10)/10;}
            var item=editItems[selectedItem];var before=item.localPosition;item.localPosition=local;dragCandidate=local;dragCandidateValid=FindAsset(item)!=null&&Fits(item,FindAsset(item));
            if(dragCandidateValid){var go=placedRoot?.Find(item.instanceId);if(go!=null)go.localPosition=local;previewValid=false;}else item.localPosition=before;
        }
        void EndPlacementDrag()
        {
            if(!draggingItem)return;draggingItem=false;
            if(!dragCandidateValid){var rejected=dragCandidate;CancelPlacementDrag();WriteDragProof(rejected,false);Toast("那里超出地块或挡住入口，已回到原处。");return;}
            if(selectedItem>=0&&selectedItem<editItems.Count&&(editItems[selectedItem].localPosition-dragStart).sqrMagnitude>.0001f)StartCoroutine(CommitPlacementDrag(dragBefore));
        }
        IEnumerator CommitPlacementDrag(string before)
        {
            var origin=dragStart;var destination=editItems[selectedItem].localPosition;yield return PreviewPlacement(false);
            if(previewValid){placementUndo.Add(before);if(placementUndo.Count>60)placementUndo.RemoveAt(0);placementRedo.Clear();SuccessfulPointerDrags++;WriteDragProof(destination,true,origin);}
            else{var error=placementError;ReadDraft(before);yield return PreviewPlacement(false);placementError=error;Toast("这次移动挡住了通路，已恢复原处。");WriteDragProof(destination,false);}
        }
        void CancelPlacementDrag()
        {
            draggingItem=false;if(!string.IsNullOrEmpty(dragBefore)){ReadDraft(dragBefore);foreach(var item in editItems){var t=placedRoot?.Find(item.instanceId);if(t!=null)t.localPosition=item.localPosition;}}
            dragCandidateValid=true;dragMoved=false;Physics.SyncTransforms();var check=PlacementChecks();previewValid=check.boundsValid&&check.groundConnected&&check.westRoute&&check.materialsValid&&check.collisionsValid;
        }
        [Serializable] class DragProof {public string source="native-pointer",utc;public bool accepted;public int count;public Vector3 from,to;}
        void WriteDragProof(Vector3 destination,bool accepted,Vector3? origin=null)
        {
            if(string.IsNullOrEmpty(SmallWorldProfile.TestRoot))return;
            File.WriteAllText(Path.Combine(SmallWorldProfile.TestRoot,"pointer-drag.json"),JsonUtility.ToJson(new DragProof {utc=DateTime.UtcNow.ToString("o"),accepted=accepted,count=SuccessfulPointerDrags,from=origin??dragStart,to=destination},true));
        }
        void UpdatePlacementInput()
        {
            if(!editorOpen||editorBusy||Game==null){if(placementOutline!=null)placementOutline.gameObject.SetActive(false);return;}
            // Pointer gestures have one owner (IMGUI). Processing the same press
            // in Update as well can start a second drag after MouseUp.
            if(draggingItem&&Keyboard.current?.escapeKey.wasPressedThisFrame==true)CancelPlacementDrag();
            UpdatePlacementOutline();
        }
        void UpdatePlacementOutline()
        {
            if(selectedItem<0||selectedItem>=editItems.Count){if(placementOutline!=null)placementOutline.gameObject.SetActive(false);return;}
            var item=editItems[selectedItem];var asset=FindAsset(item);if(asset==null)return;
            if(placementOutline==null){var go=new GameObject("选中物件落地轮廓");go.layer=2;placementOutline=go.AddComponent<LineRenderer>();placementOutline.useWorldSpace=true;placementOutline.positionCount=5;placementOutline.widthMultiplier=.028f;var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Sprites/Default");if(shader!=null)placementOutline.material=new Material(shader);}
            placementOutline.gameObject.SetActive(true);var color=draggingItem&&!dragCandidateValid?new Color(.95f,.28f,.18f):new Color(.2f,.7f,.5f);placementOutline.startColor=placementOutline.endColor=color;
            var pos=draggingItem&&!dragCandidateValid?dragCandidate:item.localPosition;var center=pos+Quaternion.Euler(item.localEulerAngles)*Vector3.Scale(asset.center,item.localScale);var half=Vector3.Scale(asset.size,item.localScale)*.5f;
            var corners=new[]{new Vector3(-half.x,0,-half.z),new Vector3(half.x,0,-half.z),new Vector3(half.x,0,half.z),new Vector3(-half.x,0,half.z),new Vector3(-half.x,0,-half.z)};
            for(int i=0;i<5;i++){var p=center+Quaternion.Euler(item.localEulerAngles)*corners[i];p.y=.07f;placementOutline.SetPosition(i,Game.expansion.land.TransformPoint(p));}
        }
        bool Fits(SmallWorldItem item,SmallWorldAsset asset)
        {
            var r=item.localEulerAngles.y*Mathf.Deg2Rad;var sx=asset.size.x*item.localScale.x*.5f;var sz=asset.size.z*item.localScale.z*.5f;
            var ex=Mathf.Abs(Mathf.Cos(r))*sx+Mathf.Abs(Mathf.Sin(r))*sz;var ez=Mathf.Abs(Mathf.Sin(r))*sx+Mathf.Abs(Mathf.Cos(r))*sz;
            var center=item.localPosition+Quaternion.Euler(0,item.localEulerAngles.y,0)*Vector3.Scale(asset.center,item.localScale);
            foreach(var x in new[]{center.x-ex,center.x+ex})foreach(var z in new[]{center.z-ez,center.z+ez})if((x*x/36)+(z*z/25)>1)return false;
            return !(center.x-ex<-.5f&&center.z-ez<.8f&&center.z+ez>-.8f);
        }
        IEnumerator PreviewPlacement(bool showNotice=true)
        {
            if(editorBusy||Game==null)yield break;if(editItems.Count>50){placementError="一块地最多放 50 件装饰。";yield break;}editorBusy=true;previewValid=false;placementError="";
            if(placedRoot!=null&&renderedItems.Count==editItems.Count&&editItems.All(i=>renderedItems.Any(old=>old.instanceId==i.instanceId&&old.assetId==i.assetId&&old.assetHash==i.assetHash&&old.tint==i.tint&&old.giftId==i.giftId)))
            {
                var valid=editItems.All(i=>FindAsset(i)!=null&&Fits(i,FindAsset(i)));
                if(valid)
                {
                    foreach(var i in editItems){var t=placedRoot.Find(i.instanceId);if(t!=null){t.localPosition=i.localPosition;t.localEulerAngles=i.localEulerAngles;t.localScale=i.localScale;}}
                    var seam=Game.expansion.land.Find("Natural continent connection");var previousScale=seam!=null?seam.localScale:Vector3.one;if(seam!=null)seam.localScale=new Vector3(1,1,editSeamSoftened?1.16f:1);
                    Physics.SyncTransforms();RebuildHomeNavigation();var check=PlacementChecks();valid=check.groundConnected&&check.westRoute&&check.materialsValid&&check.collisionsValid;
                    if(!valid){foreach(var i in renderedItems){var t=placedRoot.Find(i.instanceId);if(t!=null){t.localPosition=i.localPosition;t.localEulerAngles=i.localEulerAngles;t.localScale=i.localScale;}}if(seam!=null)seam.localScale=previousScale;RebuildHomeNavigation();}
                }
                if(valid){previewValid=true;CacheRendered();ApplyGardenSurface();SaveDraft();}else placementError="请把物件移回地块内，并保留入口通路。";
                editorBusy=false;yield break;
            }
            var staging=new GameObject("Layout preview staging").transform;staging.SetParent(Game.expansion.land,false);staging.localPosition=Vector3.down*100;
            string error=null;
            foreach(var item in editItems)
            {
                var a=FindAsset(item);if(a==null||!Fits(item,a)){error="物件超出地块或挡住连接通路，请向内移动、缩小或撤销。";break;}
                GameObject go=null;yield return SmallWorldAssetLoader.Instantiate(a,library,staging,value=>go=value,message=>error=message);
                if(go==null||error!=null)break;
                go.name=item.instanceId;var mark=go.AddComponent<HomeLayoutInstance>();mark.instanceId=item.instanceId;mark.assetId=item.assetId;mark.assetVersion=item.assetVersion;
                go.transform.localPosition=item.localPosition;go.transform.localEulerAngles=item.localEulerAngles;go.transform.localScale=item.localScale;
                SmallWorldAssetLoader.Tint(go,item.tint);
                if (item.giftId == HomecomingGift) go.AddComponent<SmallWorldWarmLantern>().Build();
            }
            if(error==null)
            {
                var seam=Game.expansion.land.Find("Natural continent connection");var previousScale=seam!=null?seam.localScale:Vector3.one;
                if(seam!=null)seam.localScale=new Vector3(1,1,editSeamSoftened?1.16f:1);
                if(placedRoot!=null)placedRoot.gameObject.SetActive(false);staging.localPosition=Vector3.zero;Physics.SyncTransforms();
                RebuildHomeNavigation();
                var check=PlacementChecks();
                if(check.groundConnected&&check.westRoute&&check.materialsValid&&check.collisionsValid)
                {
                    if(placedRoot!=null)Destroy(placedRoot.gameObject);placedRoot=staging;placedRoot.name="Editable Home Layout";previewValid=true;CacheRendered();
                    ApplyGardenSurface();SaveDraft();
                }
                else {error="预览没有通过通路或物件检查，原有布局已保留。";staging.gameObject.SetActive(false);if(seam!=null)seam.localScale=previousScale;if(placedRoot!=null)placedRoot.gameObject.SetActive(true);RebuildHomeNavigation();}
            }
            if(error!=null){Destroy(staging.gameObject);placementError=error;}
            editorBusy=false;
        }
        void CacheRendered(){renderedItems=JsonUtility.FromJson<SmallWorldLayout>(JsonUtility.ToJson(new SmallWorldLayout {instances=editItems.ToArray()})).instances.ToList();}
        void ApplyGardenSurface()
        {
            if(gardenSurface==null)gardenSurface=Game.expansion.land.GetComponent<SmallWorldGardenSurface>()??Game.expansion.land.gameObject.AddComponent<SmallWorldGardenSurface>();
            gardenSurface.Apply(Game.expansion.land,editSurfaceStyle,editItems);
            if(coast!=null && !storyActive && playableLand==null)coast.RefreshReferenceSurface();
            else if(coast!=null)
            {
                // The authored story requests a meadow garden inside the existing
                // stone apron. Keep its saved meadow style instead of repainting it.
                var sea=GameObject.Find("Ocean");
                if(sea!=null){var position=sea.transform.position;position.y=DawnCoast.ReferenceSeaLevel;sea.transform.position=position;}
            }
        }
        void RebuildHomeNavigation()
        {
            var exp=Game.expansion;var enabled=Game.navigator.enabled;Game.navigator.enabled=false;if(coast!=null)coast.RebuildPlayableNavigation();else exp.surface.BuildNavMesh();exp.expandedNavigation=exp.surface.navMeshData;coast?.ReleaseUnusedNavigation();
            if(enabled){Game.navigator.enabled=true;if(NavMesh.SamplePosition(Game.actor.position,out var sample,.6f,NavMesh.AllAreas))Game.navigator.Warp(sample.position);else Game.PlaceActor(Game.spawn.position,180);}
        }
        PreviewChecks PlacementChecks()
        {
            var exp=Game.expansion;var path=new NavMeshPath();var ground=NavMesh.CalculatePath(Game.spawn.position,exp.finalPosition+Vector3.right*.5f,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
            var west=NavMesh.CalculatePath(Game.spawn.position,exp.finalPosition+Vector3.left*4.5f,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
            var roots=placedRoot;
            // During staging the candidate root is the last active layout under the land.
            foreach(Transform t in exp.land)if(t.name=="Layout preview staging"&&t.gameObject.activeInHierarchy)roots=t;
            var renderers=roots!=null?roots.GetComponentsInChildren<Renderer>(true):new Renderer[0];
            var mats=renderers.All(r=>r.sharedMaterials.Length>0&&r.sharedMaterials.All(m=>m!=null&&m.shader!=null&&m.shader.isSupported));
            var colliders=true;
            if(roots!=null)foreach(var marker in roots.GetComponentsInChildren<HomeLayoutInstance>())
            {var c=marker.GetComponentInChildren<Collider>();var nonblocking=marker.assetId=="meadow-flower"||marker.assetId=="stepping-stone"||(availableAssets??library.assets).Any(a=>a.id==marker.assetId&&a.decorationOnly);if(!nonblocking&&(c==null||c.isTrigger))colliders=false;}
            return new PreviewChecks {groundConnected=ground,westRoute=west,boundsValid=editItems.All(i=>FindAsset(i)!=null&&Fits(i,FindAsset(i))),materialsValid=mats,collisionsValid=colliders,actorGrounded=NavMesh.SamplePosition(Game.actor.position,out _,.8f,NavMesh.AllAreas)};
        }
        byte[] CaptureHome(Camera camera = null, int width = 1280, int height = 720)
        {
            var cam=camera!=null?camera:Game.worldCamera;var previous=cam.targetTexture;var rect=cam.rect;var active=RenderTexture.active;var rt=new RenderTexture(width,height,24);var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{cam.rect=new Rect(0,0,1,1);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();return texture.EncodeToPNG();}
            finally{cam.targetTexture=previous;cam.rect=rect;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(texture);}
        }
        IEnumerator FinalizePlacement()
        {
            if(editorBusy||visualReviewBusy||!previewValid)yield break;
            if(creation?.visualReviewRequired==true&&!visualOverrideAccepted&&(!visualReviewPassed||reviewedDraftId!=DraftContextId()||reviewedHour!=Mathf.FloorToInt(Game.atmosphere?.CurrentHour??12))){yield return ReviewPlacement();if(!visualReviewPassed||visualReviewChanged)yield break;}
            editorBusy=true;yield return SyncState();PreviewReply preview=null;bool success=false;var request=PreviewBody(CaptureVisualHome());
            yield return SmallWorldApi.Request("preview",request,json=>preview=JsonUtility.FromJson<PreviewReply>(json),Toast);
            if(preview!=null)yield return SmallWorldApi.Request("finalize",new FinalRequest {clientId=Profile.clientId,runId=preview.runId,previewToken=preview.previewToken,visualOverrideAccepted=visualOverrideAccepted},json=>{
                committedLayout=JsonUtility.FromJson<SmallWorldLayout>(json);success=true;WriteLayoutSave(LayoutFile,json);Profile.creationRunId=preview.runId;Profile.Save();
            },Toast);
            editorBusy=false;if(success){WriteLayoutSave(Profile.HomeSavePath("layout-draft"),JsonUtility.ToJson(committedLayout));editorOpen=false;EndPlacementView();UpdateControls();bubble="这片土地已经定稿了。我会记住你怎样把它一点点布置起来。";Toast("家园已保存，创作经过已写入记忆。");}
        }
        float nextDraftSave;string savedDraft="";
        bool HasUnappliedDraft()
        {
            var path=Profile.HomeSavePath("layout-draft");if(!File.Exists(path))return false;
            try{var draft=JsonUtility.FromJson<SmallWorldLayout>(File.ReadAllText(path));if(draft.version!=committedLayout.version)return false;return JsonUtility.ToJson(new SmallWorldLayout {instances=draft.instances,seamSoftened=draft.seamSoftened,surfaceStyle=draft.surfaceStyle})!=JsonUtility.ToJson(new SmallWorldLayout {instances=committedLayout.instances,seamSoftened=committedLayout.seamSoftened,surfaceStyle=committedLayout.surfaceStyle});}catch(Exception){return false;}
        }
        void SaveDraft()
        {
            if(!editorOpen||draggingItem)return;var json=DraftJson();if(json==savedDraft)return;
            var path=Profile.HomeSavePath("layout-draft");if(WriteLayoutSave(path,json))savedDraft=json;
        }
        void UpdateDraftAutosave(){if(editorOpen&&!draggingItem&&Time.unscaledTime>nextDraftSave){nextDraftSave=Time.unscaledTime+1;SaveDraft();}}
        void EndPlacementView()
        {
            Game.orbit.placementView = false;
            Game.worldCamera.rect = new Rect(0,0,1,1);
            Game.orbit.expandedOverviewCenter = coast != null ? DawnCoast.Overview : new Vector3(5.8f,.5f,0);
            if (coast != null) Game.orbit.FocusPlayer();
            else {Game.orbit.overview=true;Game.orbit.distance=Game.orbit.expandedOverviewDistance;}
        }
        IEnumerator ClosePlacement()
        {
            if(editorBusy)yield break;
            SaveDraft();editorOpen=false;
            editItems=committedLayout.instances.ToList();editSeamSoftened=committedLayout.seamSoftened;editSurfaceStyle=committedLayout.surfaceStyle;yield return PreviewPlacement(false);editorOpen=false;EndPlacementView();OpenChat();Toast("草稿已保留，家园恢复到上次应用的布局。");
        }
    }
}

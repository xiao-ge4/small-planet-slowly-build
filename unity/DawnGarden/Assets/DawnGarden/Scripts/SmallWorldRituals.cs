using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DawnGarden
{
    public sealed partial class SmallWorldApp
    {
        bool ritualOpen,ritualSubmitting,ritualWasHeld,ritualPointerHeld,ritualHints;
        float ritualHeat,ritualStarted,ritualDemoUntil,ritualHeatStarted;int ritualInput;
        int[] ritualTuning=new[]{0,0,0};string ritualMessage="";SmallWorldWorldTarget ritualTarget;
        Vector3 ritualAnchor;
        void CreateRitualTarget()
        {
            var r=CurrentResident;if(r==null)return;
            var name=r.ritualKind=="heat"?"Campfire":r.ritualKind=="stars"?"Crystal Monument":"Central Boulder";
            var sceneObject=GameObject.Find(name);
            if(sceneObject==null)sceneObject=GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t=>t.name.Contains(r.ritualKind=="stars"?"Monument":"Boulder"))?.gameObject;
            if(sceneObject==null){sceneObject=new GameObject(r.ritualTitle);sceneObject.transform.position=new Vector3(0,0,-2.8f);}
            ritualTarget=sceneObject.AddComponent<SmallWorldWorldTarget>();ritualTarget.kind="ritual";
            var inward=-sceneObject.transform.position;inward.y=0;if(inward.sqrMagnitude<.1f)inward=Vector3.forward;
            ritualAnchor=Reachable(sceneObject.transform.position+(r.ritualKind=="heat"?Vector3.forward:inward.normalized)*1.1f);
            ritualTarget.approach=ritualAnchor;worldTargets.Add(ritualTarget);
        }
        void OpenRitual()
        {
            if(CurrentResident==null||CurrentResident.collected.Length!=3){Toast("先和邻居聊聊，找到三件小物后再来。");return;}
            ChatOpen=false;ritualOpen=true;ritualSubmitting=false;Game.CancelMovement();UpdateControls();
            ritualStarted=Time.unscaledTime;ritualHeat=0;ritualWasHeld=false;ritualPointerHeld=false;ritualInput=0;ritualTuning=new[]{0,0,0};ritualHints=Game.atmosphere?.Muted==true;ritualMessage="";
            ReplayRitual();
        }
        void ReplayRitual()
        {
            ritualDemoUntil=Time.unscaledTime+2.4f;ritualInput=0;
            if(CurrentResident.ritualKind=="tune")PlayRitualNotes(CurrentResident.ritualPattern);
        }
        void PlayRitualNotes(int[] notes)
        {
            if(Game.atmosphere?.Muted==true)return;
            float[] frequencies={392f,440f,523.25f,587.33f,659.25f};var clip=AudioClip.Create("风铃的示范",26460,1,22050,false);var samples=new float[26460];
            for(int i=0;i<samples.Length;i++){float t=i/22050f;int n=Mathf.Min(notes.Length-1,(int)(t/.38f));float u=t-n*.38f;samples[i]=Mathf.Sin(2*Mathf.PI*frequencies[notes[n]]*t)*Mathf.Exp(-u*12)*.12f;}
            clip.SetData(samples,0);var a=Game.gameObject.AddComponent<AudioSource>();a.clip=clip;a.volume=Game.atmosphere?.MusicVolume??.35f;a.Play();Destroy(a,1.5f);Destroy(clip,1.6f);
        }
        void UpdateRitual()
        {
            if(!ritualOpen||ritualSubmitting||CurrentResident==null)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true){ritualOpen=false;UpdateControls();return;}
            if(CurrentResident.ritualKind!="heat")return;
            bool held=(showcaseActive||storyActive)?showcaseHeatHeld:ritualPointerHeld||Keyboard.current?.spaceKey.isPressed==true;
            AdvanceHeat(held,Time.unscaledDeltaTime);
        }
        void AdvanceHeat(bool held,float dt)
        {
            if(held){if(!ritualWasHeld)ritualHeatStarted=Time.unscaledTime;ritualHeat=Mathf.Min(1,ritualHeat+dt*.25f);}
            if(!held&&ritualWasHeld)
            {
                if(ritualHeat>=.55f&&ritualHeat<=.8f)StartCoroutine(SubmitRitual());
                else{ritualMessage=ritualHeat>.8f?"有点烫，等它凉一凉。再试一次，不会丢失暖石。":"还不够温暖，再慢慢添一点火。";ritualHeat=0;}
            }
            ritualWasHeld=held;
        }
        IEnumerator SubmitRitual()
        {
            if(ritualSubmitting)yield break;ritualSubmitting=true;yield return SyncState();
            var request=new NeighborRequest {clientId=Profile.clientId,residentId=CurrentResident.id,requestId=Guid.NewGuid().ToString("N"),action="ritual",value=ritualHeat,duration=Time.unscaledTime-ritualHeatStarted,pattern=CurrentResident.ritualKind=="tune"?ritualTuning:CurrentResident.ritualPattern};
            yield return SmallWorldApi.Request("neighbor-action",request,j=>{
                var result=JsonUtility.FromJson<NeighborReply>(j);UpdateResident(result.resident);neighborWorld.gifts=result.gifts;ritualMessage=result.reply;ritualOpen=false;
                Game.actor.GetComponent<SmallWorldPetResponse>()?.Respond("thanks");SmallWorldPetResponse.Chime(Game,CurrentResident.theme,"ritual");Toast(result.reply);UpdateControls();
            },e=>ritualMessage=e);ritualSubmitting=false;
        }
        void ChooseStar(int index)
        {
            if(ritualSubmitting||ritualInput>=3||Time.unscaledTime<ritualDemoUntil)return;
            if(index==CurrentResident.ritualPattern[ritualInput]){ritualInput++;if(ritualInput==3)StartCoroutine(SubmitRitual());}
            else{ritualInput=0;ritualMessage="没关系，再看一次也可以。";}
        }
        bool TryRitualTuning()
        {
            if(ritualSubmitting)return false;
            if(ritualTuning.SequenceEqual(CurrentResident.ritualPattern)){StartCoroutine(SubmitRitual());return true;}
            ritualMessage="还差一点。可以分别试听，也可以打开音符提示。";return false;
        }
        void DrawRitual()
        {
            var r=CurrentResident;if(r==null){ritualOpen=false;return;}
            var area=new Rect(322,358,636,334);GUI.Box(area,GUIContent.none,panel);
            SmallWorldUiArt.Portrait(new Rect(344,376,48,48),r.id);
            GUI.Label(new Rect(408,378,365,33),r.ritualTitle,title);
            GUI.enabled=!ritualSubmitting;
            if(GUI.Button(new Rect(817,379,117,34),"稍后再来",secondaryButton)){ritualOpen=false;UpdateControls();}
            GUI.Label(new Rect(346,433,588,44),r.ritualHint,new GUIStyle(label){fontSize=16});
            if(r.ritualKind=="heat")
            {
                GUI.DrawTexture(new Rect(349,488,65,65),SmallWorldUiArt.Icon("fire"));
                var track=new Rect(431,502,479,24);var previous=GUI.color;
                GUI.color=new Color(.30f,.31f,.23f);GUI.DrawTexture(track,Texture2D.whiteTexture);
                GUI.color=new Color(.86f,.60f,.25f);GUI.DrawTexture(new Rect(track.x+track.width*.55f,track.y,track.width*.25f,track.height),Texture2D.whiteTexture);
                float cursor=Mathf.Clamp(track.x+track.width*ritualHeat,track.x+4,track.xMax-4);GUI.color=SmallWorldUiArt.Ink;GUI.DrawTexture(new Rect(cursor-4,track.y-6,8,track.height+12),Texture2D.whiteTexture);GUI.color=new Color(1,.95f,.75f);GUI.DrawTexture(new Rect(cursor-1.5f,track.y-4,3,track.height+8),Texture2D.whiteTexture);GUI.color=previous;
                var centered=new GUIStyle(muted){alignment=TextAnchor.MiddleCenter};GUI.Label(new Rect(track.x,537,40,24),"凉",centered);GUI.Label(new Rect(track.x+track.width*.675f-55,537,110,24),"温暖 55–80%",centered);GUI.Label(new Rect(track.xMax-40,537,40,24),"烫",centered);GUI.Label(new Rect(349,553,70,25),Mathf.RoundToInt(ritualHeat*100)+"%",centered);
                bool held=GUI.RepeatButton(new Rect(432,574,477,43),"按住添火 · 进入暖色区域后松开",button);
                if(Event.current.type==EventType.Repaint)ritualPointerHeld=held;
            }
            else if(r.ritualKind=="stars")
            {
                bool showing=Time.unscaledTime<ritualDemoUntil;int index=Mathf.FloorToInt((Time.unscaledTime-(ritualDemoUntil-2.4f))/.8f);
                int lit=showing&&index>=0&&index<3&&Mathf.Repeat(Time.unscaledTime-(ritualDemoUntil-2.4f),.8f)<.55f?r.ritualPattern[index]:-1;
                GUI.Label(new Rect(348,475,350,23),showing?"看，星光正在回应…":"轮到你了 · "+ritualInput+" / 3",muted);
                for(int i=0;i<3;i++)
                {
                    Rect card=new Rect(372+i*190,510,156,96);var c=GUI.color;
                    GUI.color=i==lit?new Color(1,.87f,.48f):Color.white;GUI.Box(card,GUIContent.none,i==lit?button:secondaryButton);GUI.color=c;
                    GUI.DrawTexture(new Rect(card.x+45,card.y+9,66,66),SmallWorldUiArt.Icon("star"));
                    GUI.Label(new Rect(card.x,card.y+72,card.width,20),new[]{"露光","星芽","月滴"}[i],new GUIStyle(muted){alignment=TextAnchor.MiddleCenter});
                    GUI.enabled=!showing&&!ritualSubmitting;if(GUI.Button(card,GUIContent.none,GUIStyle.none))ChooseStar(i);
                }
                GUI.enabled=!ritualSubmitting;if(GUI.Button(new Rect(753,628,179,34),"再看一次",secondaryButton))ReplayRitual();
            }
            else
            {
                for(int i=0;i<3;i++)
                {
                    var x=364+i*192;GUI.DrawTexture(new Rect(x+50,485,54,54),SmallWorldUiArt.Icon("wind"));
                    GUI.Label(new Rect(x+42,535,70,25),(ritualTuning[i]+1).ToString(),new GUIStyle(label){alignment=TextAnchor.MiddleCenter});
                    if(GUI.Button(new Rect(x+5,532,33,32),"−",secondaryButton)){ritualTuning[i]=(ritualTuning[i]+4)%5;PlayRitualNotes(new[]{ritualTuning[i]});}
                    if(GUI.Button(new Rect(x+118,532,33,32),"+",secondaryButton)){ritualTuning[i]=(ritualTuning[i]+1)%5;PlayRitualNotes(new[]{ritualTuning[i]});}
                }
                if(GUI.Button(new Rect(347,581,176,36),"聆听示范",secondaryButton))PlayRitualNotes(r.ritualPattern);
                if(GUI.Button(new Rect(535,581,176,36),"试听风铃",secondaryButton))PlayRitualNotes(ritualTuning);
                if(GUI.Button(new Rect(724,581,207,36),"一起奏响",button))TryRitualTuning();
                ritualHints=IllustratedToggle(new Rect(347,632,180,27),ritualHints,"显示音符提示");
                if(ritualHints)GUI.Label(new Rect(540,632,360,27),"示范："+string.Join(" → ",r.ritualPattern.Select(v=>(v+1).ToString())),muted);
            }
            GUI.enabled=true;
            if(ritualSubmitting)GUI.Label(new Rect(348,663,580,25),"正在记下这次合作…",muted);
            else if(!string.IsNullOrEmpty(ritualMessage))GUI.Label(new Rect(348,663,580,25),ritualMessage,new GUIStyle(small){wordWrap=true});
        }

    }
}

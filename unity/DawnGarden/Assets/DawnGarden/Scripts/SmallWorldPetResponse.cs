using System;
using System.Linq;
using UnityEngine;
namespace DawnGarden
{
    // An additive pose over the existing rig. Collision, movement, and the original animation clips stay authoritative.
    [DefaultExecutionOrder(1000)]
    public sealed class SmallWorldPetResponse:MonoBehaviour
    {
        DawnGardenGame game;Animator animator;Transform visual,head,leftArm,rightArm;
        Vector3 originalPosition;Quaternion originalRotation,headRotation,leftRotation,rightRotation;
        bool applied,npc;float age,duration;string queued,kind,caption;
        public int ResponseCount {get;private set;}public bool Responding=>duration>age;
        public string Caption=>Responding?caption:"";
        public void Bind(DawnGardenGame owner,Animator rig,bool resident=false)
        {
            game=owner;animator=rig;npc=resident;visual=rig.transform;
            head=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="mixamorig:Head");
            leftArm=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="mixamorig:LeftArm");rightArm=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="mixamorig:RightArm");
        }
        public void Respond(string response)
        {queued=response;if(CanPose())Begin();}
        bool CanPose()=>game!=null&&animator!=null&&(npc||(!game.Busy&&game.Motion=="Idle"));
        void Begin(){kind=queued;queued=null;age=0;duration=kind=="greet"?1.6f:2.1f;ResponseCount++;caption=kind=="greet"?"你来啦":kind=="thanks"?"谢谢你！":kind=="newland"?"一起布置吧":kind=="gift"?"喜欢这份礼物":"又长大一点！";}
        void Restore()
        {
            if(!applied||visual==null)return;visual.localPosition=originalPosition;visual.localRotation=originalRotation;
            if(head!=null)head.localRotation=headRotation;if(leftArm!=null)leftArm.localRotation=leftRotation;if(rightArm!=null)rightArm.localRotation=rightRotation;applied=false;
        }
        void Update(){Restore();if(queued!=null&&CanPose())Begin();age+=Time.deltaTime;}
        void LateUpdate()
        {
            if(!Responding||!CanPose())return;
            originalPosition=visual.localPosition;originalRotation=visual.localRotation;if(head!=null)headRotation=head.localRotation;if(leftArm!=null)leftRotation=leftArm.localRotation;if(rightArm!=null)rightRotation=rightArm.localRotation;
            float envelope=Mathf.Sin(Mathf.PI*Mathf.Clamp01(age/duration));float wave=Mathf.Sin(age*Mathf.PI*4)*envelope;
            // Only the animated visual child moves. The CharacterController's world transform is never displaced.
            if(visual!=game.actor){visual.localPosition+=Vector3.up*Mathf.Abs(wave)*.07f;visual.localRotation*=Quaternion.Euler(0,0,wave*3);}
            if(head!=null)head.localRotation*=Quaternion.Euler(wave*8,0,envelope*(kind=="thanks"?-8:8));
            if(leftArm!=null)leftArm.localRotation*=Quaternion.Euler(0,wave*9,-envelope*(kind=="greet"?25:16));
            if(rightArm!=null)rightArm.localRotation*=Quaternion.Euler(0,-wave*9,envelope*16);
            applied=true;
        }
        void OnDisable(){Restore();}
        public static void Chime(DawnGardenGame game,string theme,string response)
        {
            if(game.atmosphere!=null&&game.atmosphere.Muted)return;
            var notes=theme=="ember"?new[]{392f,493.88f,587.33f}:theme=="wind"?new[]{523.25f,783.99f,659.25f}:new[]{659.25f,880f,987.77f};
            var clip=AudioClip.Create("精灵的回应 · "+theme,26460,1,22050,false);var data=new float[26460];
            for(int i=0;i<data.Length;i++){float t=i/22050f;int n=Mathf.Min(2,(int)(t/.24f));float u=t-n*.24f;data[i]=(Mathf.Sin(2*Mathf.PI*notes[n]*t)+.18f*Mathf.Sin(4*Mathf.PI*notes[n]*t))*Mathf.Exp(-u*10)*.055f;}
            clip.SetData(data,0);var source=game.gameObject.AddComponent<AudioSource>();source.spatialBlend=0;source.volume=game.atmosphere?.MusicVolume??.35f;source.clip=clip;source.Play();Destroy(source,1.5f);Destroy(clip,1.6f);
        }
    }
}

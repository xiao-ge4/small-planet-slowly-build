using UnityEngine;

namespace DawnGarden
{
    public sealed class EmberCampEffects : MonoBehaviour
    {
        public GardenAtmosphere atmosphere;
        public Transform[] flameTongues, embers;
        public Light firelight;
        public ParticleSystem springSteam;
        Vector3[] bases;
        MaterialPropertyBlock steamProperties;
        Renderer steamRenderer;
        void Awake()
        {
            bases=new Vector3[flameTongues.Length];
            for(var i=0;i<bases.Length;i++) bases[i]=flameTongues[i].localScale;
            if(springSteam!=null)steamRenderer=springSteam.GetComponent<Renderer>();
            steamProperties=new MaterialPropertyBlock();
        }
        void Update()
        {
            var time=Time.time;
            if(steamRenderer!=null)
            {
                steamRenderer.GetPropertyBlock(steamProperties);
                steamProperties.SetFloat("_Daylight",atmosphere!=null?atmosphere.Daylight:1);
                steamRenderer.SetPropertyBlock(steamProperties);
            }
            for(var i=0;i<flameTongues.Length;i++)
            {
                var f=flameTongues[i];
                f.localScale=Vector3.Scale(bases[i],new Vector3(1+.08f*Mathf.Sin(time*2.1f+i),1+.12f*Mathf.Sin(time*3.3f+i*1.8f),1));
                f.localRotation=Quaternion.Euler(Mathf.Sin(time*1.9f+i)*6, i*67,Mathf.Sin(time*2.4f+i)*7);
            }
            if(firelight != null) firelight.intensity=(atmosphere == null ? .9f : Mathf.Lerp(2.0f,.55f,atmosphere.Daylight))*(1+.07f*Mathf.Sin(time*5.1f)+.03f*Mathf.Sin(time*8.7f));
            for(var i=0;i<embers.Length;i++)
            {
                var u=Mathf.Repeat(time*.18f+i*.137f,1);
                embers[i].localPosition=new Vector3(Mathf.Sin(i*2.4f+time*.35f)*(.15f+u*.2f),.26f+u*1.2f,Mathf.Cos(i*2.4f+time*.3f)*.17f);
                embers[i].localScale=Vector3.one*(Mathf.Sin(u*Mathf.PI)*.023f);
            }
        }
    }
}

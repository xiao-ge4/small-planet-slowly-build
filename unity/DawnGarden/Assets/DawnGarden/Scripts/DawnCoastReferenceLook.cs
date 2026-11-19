using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool referenceLookConfigured;
        readonly Dictionary<Light,Vector2> referenceLampLevels=new();

        // Call once after the complete reference home has been dressed. The list
        // contains only materials created by this DawnCoast and disposed with it.
        void ApplyReferenceLook()
        {
            if(referenceLookConfigured)return;
            referenceLookConfigured=true;
            var timberGrain=ReferenceSurfaceGrain(true);
            var clayGrain=ReferenceSurfaceGrain(false);
            foreach(var material in materials)
            {
                if(material==null)continue;
                string name=material.name;
                bool timber=name.StartsWith("Timber")||name.StartsWith("工坊陈列 · 深色橡木")||name.StartsWith("工坊陈列 · 蜜色端木");
                bool clay=name.StartsWith("Terracotta");
                bool masonry=name.StartsWith("Limestone")||name.StartsWith("阶地")&&name.Contains("石")||name=="工坊入口完整石阶";
                bool geology=name.Contains("cave_")||name.Contains("central_ridge")||name.Contains("coast_crag")||name.Contains("海岸母岩");
                if(timber||clay)
                {
                    material.SetFloat("_Smoothness",timber?.10f:.08f);
                    material.SetFloat("_Metallic",0);
                    // Keep authored UVs and pigment. A quiet grain replaces the
                    // uniformly polished highlight without adding new geometry.
                    if(material.GetTexture("_BaseMap")==null)
                        material.SetTexture("_BaseMap",timber?timberGrain:clayGrain);
                    var c=material.color;
                    material.color=new Color(c.r*.97f,c.g*.98f,c.b*1.035f,c.a);
                }
                if(masonry)
                {
                    var c=material.color;
                    material.color=new Color(c.r*.98f,c.g*.985f,Mathf.Min(1,c.b*1.065f),c.a);
                    if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.08f);
                }
                if(geology)
                {
                    if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.09f);
                    if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",.53f);
                    if(name.Contains("cave_vault"))material.color=new Color(.86f,.95f,1.07f);
                }
                if(name.StartsWith("Hero foliage"))
                {
                    var c=material.color;
                    material.color=Color.Lerp(c,new Color(c.grayscale,c.grayscale,c.grayscale,c.a),.075f);
                }
            }
            warmGlass.SetColor("_EmissionColor",new Color(.85f,.34f,.075f));
            warmGlass.SetFloat("_Smoothness",.20f);
            Transform workshop=null;
            // Imported root names contain '/', so Transform.Find would treat the
            // name as a hierarchy path and silently miss the workshop.
            foreach(Transform child in details)
                if(child.name=="参考资产 · Buildings/workshop"){workshop=child;break;}
            if(workshop!=null)
            {
                foreach(var renderer in workshop.GetComponentsInChildren<Renderer>())
                {
                    string name=renderer.name;
                    // The imported shelves already contain a second regimented
                    // display. Retire those loose props, keeping the solid furniture.
                    if(name.StartsWith("Shelf glazed earthen jar")||name.StartsWith("Jar pale cork lid")||
                        name.StartsWith("Supplies wrapped in pale paper")||name.StartsWith("Worked plank on bench")||
                        name.StartsWith("Mallet oak handle")||name.StartsWith("Joiner's mallet head")||
                        name.StartsWith("Round tool cup")||name.StartsWith("Brush handle in cup"))renderer.enabled=false;
                }
            }
            foreach(var lamp in nightLamps)
            {
                if(lamp==null)continue;
                if(lamp.name=="灯前暖光")
                {
                    // A short, gentle point light connects the luminous shade to
                    // its rear wall without consuming six shadow-atlas faces.
                    lamp.type=LightType.Point;lamp.shadows=LightShadows.None;
                    lamp.range=3.0f;lamp.color=new Color(1,.72f,.47f);
                    referenceLampLevels[lamp]=new Vector2(.24f,.95f);
                }
                else if(lamp.name=="柱灯暖光")
                {
                    lamp.range=2.8f;lamp.color=new Color(1,.66f,.37f);
                    referenceLampLevels[lamp]=new Vector2(.16f,.85f);
                }
                else if(lamp.name=="檐口琥珀暖灯")
                {
                    lamp.range=2.9f;lamp.color=new Color(1,.69f,.40f);
                    referenceLampLevels[lamp]=new Vector2(.13f,.72f);
                }
            }
            game.atmosphere.daytimeSunColor=new Color(1,.905f,.79f);
        }

        Texture2D ReferenceSurfaceGrain(bool timber)
        {
            const int size=128;
            var texture=new Texture2D(size,size,TextureFormat.RGB24,true)
                {name=timber?"海岸木作 · 细缓木纹":"海岸陶瓦 · 细砂面",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size;
                float large=Mathf.PerlinNoise(u*5.1f+2.4f,v*5.1f+7.6f);
                float fine=Mathf.PerlinNoise(u*43.1f+7.6f,v*(timber?2.7f:43.1f)+2.4f);
                float value=timber?.89f+.10f*fine+.035f*large:.91f+.055f*fine+.035f*large;
                pixels[y*size+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);textures.Add(texture);return texture;
        }

        void UpdateReferenceLook(float daylight)
        {
            if(!referenceLookConfigured)return;
            foreach(var entry in referenceLampLevels)
                if(entry.Key!=null)entry.Key.intensity=Mathf.Lerp(entry.Value.y,entry.Value.x,daylight);
            if(fill!=null)fill.intensity=Mathf.Lerp(.045f,.065f,daylight);
        }
    }
}

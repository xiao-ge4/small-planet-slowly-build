using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DawnGarden
{
    public sealed partial class DawnCoast
    {
        bool sampleAtmosphereConfigured, savedFog;
        Material savedSkybox;
        FogMode savedFogMode;
        Color savedFogColor;
        float savedFogDensity, savedShadowDistance;
        Vector3 savedCascadeSplit;
        UnityEngine.Rendering.SphericalHarmonicsL2 savedAmbientProbe;
        UniversalRenderPipelineAsset samplePipeline;
        readonly List<Action> sampleAtmosphereRestores=new();
        Color savedAmbientSky,savedAmbientEquator,savedAmbientGround,savedDaytimeSun;
        UnityEngine.Rendering.AmbientMode savedAmbientMode;
        float savedDayExposure,savedNightExposure;
        bool savedShowSkyOrbs;

        void SaveSampleParameter<T>(UnityEngine.Rendering.VolumeParameter<T> parameter)
        {
            T value=parameter.value;bool overridden=parameter.overrideState;
            sampleAtmosphereRestores.Add(()=>{parameter.value=value;parameter.overrideState=overridden;});
        }

        void ConfigureSampleCamera()
        {
            if(sampleAtmosphereConfigured)return;
            savedSkybox=RenderSettings.skybox;
            savedFog=RenderSettings.fog;savedFogMode=RenderSettings.fogMode;
            savedFogColor=RenderSettings.fogColor;savedFogDensity=RenderSettings.fogDensity;
            savedAmbientProbe=RenderSettings.ambientProbe;
            savedAmbientMode=RenderSettings.ambientMode;
            savedAmbientSky=RenderSettings.ambientSkyColor;savedAmbientEquator=RenderSettings.ambientEquatorColor;
            savedAmbientGround=RenderSettings.ambientGroundColor;
            savedDaytimeSun=game.atmosphere.daytimeSunColor;
            savedDayExposure=game.atmosphere.daylightExposure;savedNightExposure=game.atmosphere.nightExposure;
            savedShowSkyOrbs=game.atmosphere.showSkyOrbs;
            sampleAtmosphereConfigured=true;
            game.atmosphere.showSkyOrbs = false;
            var volume = game.atmosphere.atmosphereVolume;
            if (volume != null && volume.profile.TryGet(out Bloom bloom))
            {
                SaveSampleParameter(bloom.threshold);SaveSampleParameter(bloom.intensity);
                bloom.threshold.Override(1.55f);
                bloom.intensity.Override(.12f);
            }
            var cameraData=game.worldCamera.GetUniversalAdditionalCameraData();
            bool stopNaN=cameraData.stopNaN,post=cameraData.renderPostProcessing;
            var antialiasing=cameraData.antialiasing;
            sampleAtmosphereRestores.Add(()=>{if(cameraData!=null){cameraData.stopNaN=stopNaN;cameraData.renderPostProcessing=post;cameraData.antialiasing=antialiasing;}});
            game.worldCamera.GetUniversalAdditionalCameraData().stopNaN = true;
            game.worldCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            game.worldCamera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            if(volume != null)
            {
                if(volume.profile.TryGet(out ColorAdjustments colors))
                {
                    SaveSampleParameter(colors.contrast);SaveSampleParameter(colors.saturation);
                    colors.contrast.Override(7);
                    colors.saturation.Override(6);
                }
                if(volume.profile.TryGet(out Tonemapping tone))
                {SaveSampleParameter(tone.mode);tone.mode.Override(TonemappingMode.ACES);}
            }
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline != null) { samplePipeline=pipeline;savedShadowDistance=pipeline.shadowDistance;savedCascadeSplit=pipeline.cascade4Split;pipeline.shadowDistance = 155; }
            if(game.atmosphere.sun != null)
            {
                var sun=game.atmosphere.sun;var shadows=sun.shadows;
                float strength=sun.shadowStrength,bias=sun.shadowBias,normalBias=sun.shadowNormalBias;
                sampleAtmosphereRestores.Add(()=>{if(sun!=null){sun.shadows=shadows;sun.shadowStrength=strength;sun.shadowBias=bias;sun.shadowNormalBias=normalBias;}});
                game.atmosphere.sun.shadows=LightShadows.Soft;
                game.atmosphere.sun.shadowStrength=.85f;
                game.atmosphere.sun.shadowBias=.035f;
                game.atmosphere.sun.shadowNormalBias=.10f;
            }
            // The transparent panes have no physics collision. This camera-only proxy
            // leaves the greenhouse walkable while keeping the chase view outside the glazing.
            var nursery = details.Find("02 · 水渠苗圃");
            var greenhouse = nursery == null ? null : nursery.Find("暖铜弧肋玻璃温室");
            if (greenhouse == null) return;
            var hull = Group("温室镜头避障代理",greenhouse);
            hull.gameObject.layer = 2;
            var bounds = hull.gameObject.AddComponent<BoxCollider>();
            bounds.center = new Vector3(0,1.9f,0);
            bounds.size = new Vector3(6.7f,3.9f,8.9f);
            bounds.isTrigger = true;
            game.orbit.sightObstruction = hull;
        }

        void RestoreSampleAtmosphere()
        {
            if(!sampleAtmosphereConfigured)return;
            sampleAtmosphereConfigured=false;
            foreach(var restore in sampleAtmosphereRestores)restore();
            sampleAtmosphereRestores.Clear();
            if(game!=null&&game.atmosphere!=null)
            {
                game.atmosphere.daytimeSunColor=savedDaytimeSun;
                game.atmosphere.daylightExposure=savedDayExposure;game.atmosphere.nightExposure=savedNightExposure;
                game.atmosphere.showSkyOrbs=savedShowSkyOrbs;
                game.atmosphere.RefreshNow();
            }
            RenderSettings.skybox=savedSkybox;
            RenderSettings.fog=savedFog;RenderSettings.fogMode=savedFogMode;
            RenderSettings.fogColor=savedFogColor;RenderSettings.fogDensity=savedFogDensity;
            RenderSettings.ambientMode=savedAmbientMode;
            RenderSettings.ambientSkyColor=savedAmbientSky;RenderSettings.ambientEquatorColor=savedAmbientEquator;
            RenderSettings.ambientGroundColor=savedAmbientGround;
            RenderSettings.ambientProbe=savedAmbientProbe;
            if(samplePipeline!=null){samplePipeline.shadowDistance=savedShadowDistance;samplePipeline.cascade4Split=savedCascadeSplit;}
        }

        void LateUpdate()
        {
            if(game == null || game.atmosphere == null)return;
            float light=game.atmosphere.Daylight;
            foreach(var glass in referenceGlassMaterials)glass.SetFloat("_Daylight",light);
            if(referencePondMaterial!=null)referencePondMaterial.SetFloat("_Daylight",light);
            if(samplePipeline!=null)samplePipeline.cascade4Split=game.orbit.overview?new Vector3(.40f,.65f,.85f):savedCascadeSplit;
            if(distantShoreMaterial!=null)distantShoreMaterial.SetFloat("_Day",light);
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.17f,.22f,.36f),new Color(.48f,.62f,.76f),light);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.13f,.18f,.29f),new Color(.43f,.47f,.60f),light);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.09f,.12f,.21f),new Color(.30f,.31f,.39f),light);
            var harmonics=new UnityEngine.Rendering.SphericalHarmonicsL2();
            // Preserve the night fill, but leave room for the moving sun to model
            // the limestone at daytime. Raising exposure flattened those faces.
            float skyFill=Mathf.Lerp(1,referenceLookConfigured?.75f:.84f,light);
            harmonics.AddAmbientLight(Color.Lerp(new Color(.08f,.12f,.19f),new Color(.18f,.22f,.29f),light)*skyFill);
            harmonics.AddDirectionalLight(Vector3.up,new Color(.34f,.46f,.66f),.16f*light*skyFill);
            RenderSettings.ambientProbe=harmonics;
            if(game.atmosphere.sun!=null)
            {
                float solar=Mathf.Sin((game.atmosphere.CurrentHour-6)*Mathf.PI/12);
                // Recompute from the clock, not by multiplying last frame's value.
                game.atmosphere.sun.intensity=light*Mathf.Lerp(.65f,1.3f,Mathf.Sqrt(Mathf.Clamp01(solar)))*(referenceLookConfigured?1.10f:1.12f);
            }
            if(RenderSettings.skybox!=null && RenderSettings.skybox.HasProperty("_Day"))
            {
                RenderSettings.skybox.SetFloat("_Day",light);
                if(game.atmosphere.sun!=null)
                    RenderSettings.skybox.SetVector("_SunDir",-game.atmosphere.sun.transform.forward);
            }
            RenderSettings.fog=true;
            RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=Color.Lerp(new Color(.055f,.10f,.19f),new Color(.63f,.76f,.78f),light);
            RenderSettings.fogDensity=.0018f;
            UpdateReferenceLook(light);
        }
    }

}

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGarden
{
    [DefaultExecutionOrder(-50)]
    public sealed class GardenAtmosphere : MonoBehaviour
    {
        [Header("Clock — one real day per cycle")]
        public bool followRealTime = true;
        [Range(-12,14)] public float utcOffsetHours = 8;
        [Range(0,24)] public float previewHour = 8;
        [Header("Scene")]
        public DawnGardenGame game;
        public Light sun, moon, crystalGlow;
        public Color daytimeSunColor = new Color(1,.96f,.87f);
        public Transform sunDisc, moonDisc;
        public bool showSkyOrbs = true;
        public Renderer[] water;
        public Transform[] fireflies;
        public Volume atmosphereVolume;
        [Header("Music")]
        public AudioSource morningMusic, nightMusic;
        [Range(0,1)] public float defaultVolume = .35f;
        [Header("Home lighting")]
        public float daylightExposure = .15f, nightExposure = .55f;
        public float CurrentHour { get; private set; }
        public float Daylight { get; private set; }
        public float MusicVolume { get; private set; }
        public bool Muted { get; private set; }
        public bool ShowControls { get; private set; }
        public string Phase => CurrentHour < 5.5f || CurrentHour >= 19 ? "月夜" : CurrentHour < 8 ? "晨曦" : CurrentHour < 16.5f ? "白昼" : "黄昏";
        public string ClockLabel => $"{Mathf.FloorToInt(CurrentHour):00}:{Mathf.FloorToInt(CurrentHour * 60) % 60:00}";
        VolumeProfile runtimeProfile;
        ColorAdjustments adjustments;
        MaterialPropertyBlock waterProperties;
        float fade;
        GUIStyle panel, label, title, button;
        Texture2D panelTexture;
        const string VolumeKey = "DawnGarden.Music.Volume", MuteKey = "DawnGarden.Music.Muted";
        public static float HourAt(DateTimeOffset utc, float offset) => (float)utc.ToOffset(TimeSpan.FromMinutes(Math.Round(Mathf.Clamp(offset,-12,14)*60))).TimeOfDay.TotalHours;

        void Awake()
        {
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, defaultVolume));
            Muted = PlayerPrefs.GetInt(MuteKey,0) == 1;
            waterProperties = new MaterialPropertyBlock();
            if (atmosphereVolume != null)
            {
                runtimeProfile = atmosphereVolume.profile;
                runtimeProfile.TryGet(out adjustments);
            }
            foreach (var source in new[] { morningMusic, nightMusic })
                if (source != null && source.clip != null) { source.loop = true; source.spatialBlend = 0; source.volume = 0; source.Play(); }
            RefreshNow();
        }
        void Update()
        {
            RefreshNow();
            fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime / 2.5f);
            var volume = Muted ? 0 : MusicVolume * fade;
            if (morningMusic != null) morningMusic.volume = volume * Mathf.Sqrt(Daylight);
            if (nightMusic != null) nightMusic.volume = volume * Mathf.Sqrt(1-Daylight) * .80f;
            if (fireflies == null) return;
            for (var i=0;i<fireflies.Length;i++)
            {
                var f = fireflies[i]; if (f == null) continue;
                f.gameObject.SetActive(Daylight < .4f);
                var p = f.localPosition; p.y = .6f + .3f * Mathf.Sin(Time.unscaledTime * .7f + i*2.4f); f.localPosition = p;
            }
        }
        public void Preview(float hour) { followRealTime = false; previewHour = Mathf.Repeat(hour,24); RefreshNow(); }
        public void UseRealTime() { followRealTime = true; RefreshNow(); }
        public void SetVolume(float value, bool persist = true)
        {
            MusicVolume = Mathf.Clamp01(value);
            if (persist) { PlayerPrefs.SetFloat(VolumeKey,MusicVolume); PlayerPrefs.Save(); }
        }
        public void SetMuted(bool value, bool persist = true)
        {
            Muted = value;
            if (persist) { PlayerPrefs.SetInt(MuteKey,value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public void RefreshNow()
        {
            CurrentHour = followRealTime ? HourAt(DateTimeOffset.UtcNow,utcOffsetHours) : Mathf.Repeat(previewHour,24);
            var solar = Mathf.Sin((CurrentHour-6)*Mathf.PI/12);
            Daylight = Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.14f,.30f,solar));
            var twilight = Mathf.Exp(-Mathf.Pow(solar/.24f,2)) * .85f;
            var elevation = solar*67;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(elevation, CurrentHour*15-90,0);
                sun.intensity = Daylight * Mathf.Lerp(.65f,1.3f,Mathf.Sqrt(Mathf.Clamp01(solar)));
                sun.color = Color.Lerp(new Color(1,.52f,.28f),daytimeSunColor,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.65f,solar)));
                sun.enabled = solar > -.14f;
            }
            if (moon != null)
            {
                moon.transform.rotation = Quaternion.Euler(-elevation,CurrentHour*15+90,0);
                moon.color = new Color(.55f,.68f,1);
                moon.intensity = (1-Daylight)*.50f; moon.enabled = Daylight < .98f;
            }
            RenderSettings.sun = Daylight > .35f ? sun : moon;
            var nightSky = new Color(.17f,.22f,.36f);
            var daySky = new Color(.63f,.75f,.85f);
            var sky = Color.Lerp(nightSky,daySky,Daylight);
            sky = Color.Lerp(sky,new Color(.72f,.48f,.38f),twilight*.48f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.16f,.21f,.31f),new Color(.53f,.61f,.53f),Daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.10f,.14f,.22f),new Color(.34f,.39f,.33f),Daylight);
            if (game != null && game.worldCamera != null)
                game.worldCamera.backgroundColor = Color.Lerp(new Color(.025f,.055f,.13f),new Color(.51f,.72f,.80f),Daylight);
            if (adjustments != null)
            {
                adjustments.postExposure.Override(Mathf.Lerp(nightExposure,daylightExposure,Daylight));
            }
            if (crystalGlow != null) crystalGlow.intensity = Mathf.Lerp(1.15f,.05f,Daylight);
            if (waterProperties == null) waterProperties = new MaterialPropertyBlock();
            if (water != null) foreach (var renderer in water)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(waterProperties); waterProperties.SetFloat("_Daylight",Daylight); renderer.SetPropertyBlock(waterProperties);
            }
            if (sunDisc != null && sun != null)
            {
                sunDisc.position = -sun.transform.forward*62; sunDisc.gameObject.SetActive(showSkyOrbs && solar > -.05f);
            }
            if (moonDisc != null && moon != null)
            {
                moonDisc.position = -moon.transform.forward*62; moonDisc.gameObject.SetActive(showSkyOrbs && solar < .05f);
            }
        }
        public bool PointerOverHud(Vector2 p) => new Rect(1050,108,208,ShowControls ? 435 : SmallWorldApp.Instance!=null ? 42 : 130).Contains(p);
        void PrepareStyles()
        {
            if (panel != null) return;
            var font = game.interfaceFont;
            panelTexture = new Texture2D(1,1); panelTexture.SetPixel(0,0,new Color(.09f,.17f,.16f,.92f)); panelTexture.Apply();
            panel = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, padding = new RectOffset(15,15,12,12) };
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, wordWrap = true, normal = { textColor = new Color(.87f,.93f,.88f) } };
            title = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { font = font,fontSize=15,padding=new RectOffset(6,6,8,8) };
            button.normal.textColor=button.hover.textColor=button.active.textColor=button.focused.textColor=new Color(.13f,.23f,.18f);
        }
        void OnGUI()
        {
            if (game == null || !game.showHud || SmallWorldApp.Instance != null) return;
            PrepareStyles();
            float scale = Mathf.Min(Screen.width/1280f,Screen.height/720f);
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            bool compact=SmallWorldApp.Instance!=null;
            GUILayout.BeginArea(new Rect(1050,108,208,ShowControls ? 435 : compact ? 42 : 130),compact&&!ShowControls ? GUIStyle.none : panel);
            if(!compact||ShowControls)
            {
                GUILayout.Label(ClockLabel+" · "+Phase,title);
                GUILayout.Label(followRealTime ? $"现实时间 UTC{utcOffsetHours:+0.#;-0.#;0}" : "时间预览中",label);
            }
            if (GUILayout.Button(ShowControls ? "收起声音与昼夜" : "声音与昼夜",button)) ShowControls = !ShowControls;
            if (ShowControls)
            {
                GUILayout.Space(10); GUILayout.Label("背景音乐  "+Mathf.RoundToInt(MusicVolume*100)+"%",label);
                var volume = GUILayout.HorizontalSlider(MusicVolume,0,1);
                if (Mathf.Abs(volume-MusicVolume) > .001f) SetVolume(volume);
                if (GUILayout.Button(Muted ? "开启音乐" : "静音",button)) SetMuted(!Muted);
                GUILayout.Space(14); GUILayout.Label("预览一天的光影",label);
                var hour = GUILayout.HorizontalSlider(CurrentHour,0,23.999f);
                if (Mathf.Abs(hour-CurrentHour) > .02f) Preview(hour);
                GUILayout.Label("00:00          12:00          24:00",new GUIStyle(label){fontSize=11});
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("晨光",button)) Preview(7.5f);
                if (GUILayout.Button("月夜",button)) Preview(22);
                GUILayout.EndHorizontal();
                GUI.enabled = !followRealTime;
                if (GUILayout.Button("回到实时",button)) UseRealTime();
                GUI.enabled = true;
                GUILayout.Label("按现实时间走满 24 小时",label);
            }
            GUILayout.EndArea(); GUI.matrix = matrix;
        }
        void OnApplicationFocus(bool focused) { if (focused) RefreshNow(); }
        void OnDestroy()
        {
            if (panelTexture != null) Destroy(panelTexture);
            if (runtimeProfile != null)
            {
                foreach (var component in runtimeProfile.components) if (component != null) Destroy(component);
                Destroy(runtimeProfile);
            }
        }
    }
}

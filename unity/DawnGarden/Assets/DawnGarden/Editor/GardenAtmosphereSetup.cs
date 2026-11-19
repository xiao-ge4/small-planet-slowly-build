using System;
using System.IO;
using System.Linq;
using DawnGarden;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden.Editor
{
    public static class GardenAtmosphereSetup
    {
        const string Root = "Assets/DawnGarden";
        [MenuItem("Dawn Garden/Atmosphere/Install Music And Day Night")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene first");
            GardenBootstrap.Open();
            var game = UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
            var go = GameObject.Find("Garden Atmosphere") ?? new GameObject("Garden Atmosphere");
            var atmosphere = go.GetComponent<GardenAtmosphere>();
            if (atmosphere == null) atmosphere = go.AddComponent<GardenAtmosphere>();
            atmosphere.game = game; game.atmosphere = atmosphere;
            atmosphere.sun = GameObject.Find("Morning Sun").GetComponent<Light>();
            atmosphere.moon = Directional("Garden Moonlight",go.transform);
            atmosphere.moon.shadowStrength = .55f; atmosphere.moon.shadowBias = .04f; atmosphere.moon.shadowNormalBias = .22f;
            atmosphere.atmosphereVolume = GameObject.Find("Garden Morning Volume").GetComponent<Volume>();
            atmosphere.water = new[] { "Ocean", "Pond Water" }.Select(n=>GameObject.Find(n).GetComponent<Renderer>()).ToArray();
            atmosphere.morningMusic = Music("Morning Music","MorningDew.wav",go.transform);
            atmosphere.nightMusic = Music("Moonlight Music","MoonlitDew.wav",go.transform);
            atmosphere.sunDisc = Orb("Garden Sun",new Color(1,.87f,.54f),Vector3.one*3,go.transform);
            atmosphere.moonDisc = Orb("Garden Moon",new Color(.74f,.84f,1),Vector3.one*2,go.transform);
            var monument = GameObject.Find("Formal Travel Monument").transform;
            var glow = GameObject.Find("Crystal Night Glow") ?? new GameObject("Crystal Night Glow");
            glow.transform.SetParent(go.transform); glow.transform.position = monument.position + Vector3.up*1.3f;
            atmosphere.crystalGlow = glow.GetComponent<Light>();
            if (atmosphere.crystalGlow == null) atmosphere.crystalGlow = glow.AddComponent<Light>();
            atmosphere.crystalGlow.type = LightType.Point; atmosphere.crystalGlow.range = 3.5f;
            atmosphere.crystalGlow.color = new Color(.36f,.78f,1); atmosphere.crystalGlow.shadows = LightShadows.None;
            var flies = new Transform[16];
            for (var i=0;i<flies.Length;i++)
            {
                var t = Orb("Garden Firefly "+i,new Color(1,.91f,.40f),Vector3.one*.028f,go.transform);
                float a=i*2.39996f; t.localPosition = new Vector3(Mathf.Cos(a)*3.7f,.7f,Mathf.Sin(a)*3.1f); flies[i]=t;
            }
            atmosphere.fireflies = flies;
            foreach (var fly in flies) fly.gameObject.SetActive(false);
            atmosphere.followRealTime = true; atmosphere.utcOffsetHours = 8;
            atmosphere.RefreshNow();
            EditorSceneManager.MarkSceneDirty(go.scene); EditorSceneManager.SaveScene(go.scene); AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(GardenBootstrap.Output,"garden-atmosphere-installed.json"),
                "{\"success\":true,\"realTime\":true,\"utcOffsetHours\":8,\"musicTracks\":2,\"looping\":true}");
        }
        static Light Directional(string name,Transform parent)
        {
            var go=GameObject.Find(name) ?? new GameObject(name); go.transform.SetParent(parent);
            var light=go.GetComponent<Light>(); if (light == null) light=go.AddComponent<Light>(); light.type=LightType.Directional; light.shadows=LightShadows.Soft;
            return light;
        }
        static AudioSource Music(string name,string file,Transform parent)
        {
            var path=Root+"/Audio/"+file;
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer==null) throw new InvalidOperationException("Audio not imported: "+path);
            var sample=importer.defaultSampleSettings;
            sample.loadType=AudioClipLoadType.DecompressOnLoad; sample.compressionFormat=AudioCompressionFormat.PCM;
            sample.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            sample.preloadAudioData=true; importer.defaultSampleSettings=sample; importer.SaveAndReimport();
            var go=GameObject.Find(name) ?? new GameObject(name); go.transform.SetParent(parent);
            var audio=go.GetComponent<AudioSource>(); if (audio == null) audio=go.AddComponent<AudioSource>();
            audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path); audio.loop=true; audio.playOnAwake=false;
            audio.spatialBlend=0; audio.volume=0; audio.priority=128;
            return audio;
        }
        static Transform Orb(string name,Color color,Vector3 scale,Transform parent)
        {
            var go=GameObject.Find(name);
            if (go==null) { go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name=name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); }
            go.transform.SetParent(parent); go.transform.localScale=scale;
            const string folder=Root+"/Art/Atmosphere"; Directory.CreateDirectory(folder);
            var matPath=folder+"/"+(name.StartsWith("Garden Firefly") ? "Firefly" : name)+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat,matPath); }
            mat.SetColor("_BaseColor",color); mat.enableInstancing=true; EditorUtility.SetDirty(mat);
            var renderer=go.GetComponent<Renderer>(); renderer.sharedMaterial=mat; renderer.shadowCastingMode=ShadowCastingMode.Off;
            return go.transform;
        }
    }
}

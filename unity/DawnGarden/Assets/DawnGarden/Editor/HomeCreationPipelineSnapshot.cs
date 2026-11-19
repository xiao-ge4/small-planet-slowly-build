using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DawnGarden.Editor
{
    // Retrospective evidence, not a scene importer or a production layout format.
    public static class HomeCreationPipelineSnapshot
    {
        [Serializable] public class Instance
        {
            public string id, name, parentId, prefabPath;
            public Vector3 localPosition, localEuler, localScale;
            public bool activeSelf;
            public string[] colliderTypes;
        }
        [Serializable] public class Interaction
        {
            public string instanceId, stableId, kind, visualInstanceId;
            public Vector3 approachWorldPosition;
        }
        [Serializable] public class Home
        {
            public string homeId, title, scenePath, sceneSha256;
            public Vector3 spawnWorldPosition;
            public string[] clips;
            public List<Instance> instances = new List<Instance>();
            public List<Interaction> interactions = new List<Interaction>();
        }
        [Serializable] public class Report
        {
            public string utc, mode = "retrospective-hierarchy-snapshot";
            public bool sourceScenesUnchanged, activeScenePreserved;
            public List<Home> homes = new List<Home>();
            public string[] limitations = {
                "Snapshot IDs use hierarchy indices and are only stable inside this snapshot; future authored layouts need permanent UUIDs.",
                "Contains inactive children and LODs; parent references and activeSelf must be respected.",
                "World positions on interactions are evidence from saved source scenes; new layouts require plot-local bindings.",
                "Does not assert player confirmation, visual quality, navigation rebake, or readiness to publish."
            };
        }
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        static string Hash(string path)
        {
            using(var stream=File.OpenRead(path)) using(var sha=SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        static string Key(Transform t) => t.parent == null ? t.GetSiblingIndex().ToString() : Key(t.parent)+"/"+t.GetSiblingIndex();
        [MenuItem("Dawn Garden/Creation/Export Pipeline Source Snapshots")]
        public static void FromMenu() { Run(); }
        public static string Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Export in Edit Mode after compilation.");
            var active=SceneManager.GetActiveScene(); var dirty=active.isDirty;
            var report=new Report {utc=DateTime.UtcNow.ToString("o"),sourceScenesUnchanged=true};
            foreach(var path in new[]{"Assets/DawnGarden/Scenes/DawnGarden.unity","Assets/DawnGarden/Scenes/EmberCamp.unity"})
            {
                var before=Hash(path);
                var scene=EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var transforms=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                    var game=transforms.Select(t=>t.GetComponent<DawnGardenGame>()).Single(g=>g!=null);
                    var home=new Home {homeId=game.homeId,title=game.homeTitle,scenePath=path,sceneSha256=before,
                        spawnWorldPosition=game.spawn.position,clips=game.clips.Select(c=>c.name).ToArray()};
                    foreach(var t in transforms)
                    {
                        home.instances.Add(new Instance {id=Key(t),name=t.name,parentId=t.parent==null?null:Key(t.parent),
                            prefabPath=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),
                            localPosition=t.localPosition,localEuler=t.localEulerAngles,localScale=t.localScale,activeSelf=t.gameObject.activeSelf,
                            colliderTypes=t.GetComponents<Collider>().Select(c=>c.GetType().Name).ToArray()});
                    }
                    foreach(var i in game.interactions)
                        home.interactions.Add(new Interaction {instanceId=Key(i.transform),stableId=i.stableId,kind=i.kind.ToString(),
                            visualInstanceId=Key(i.visual),approachWorldPosition=i.approach.position});
                    report.homes.Add(home);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
                report.sourceScenesUnchanged &= before==Hash(path);
            }
            report.activeScenePreserved=SceneManager.GetActiveScene()==active && active.isDirty==dirty;
            if(!report.sourceScenesUnchanged || !report.activeScenePreserved) throw new InvalidOperationException("Source scene preservation check failed.");
            var output=Path.Combine(Root,"artifacts/home-creation/pipeline/source-layouts.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
            return output;
        }
    }
}

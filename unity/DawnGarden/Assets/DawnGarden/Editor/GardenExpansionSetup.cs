using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace DawnGarden.Editor
{
    public static class GardenExpansionSetup
    {
        const string Folder="Assets/DawnGarden/Art/Expansion";
        [MenuItem("Dawn Garden/Expansion/Install First Land Expansion")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first");
            GardenBootstrap.Open(); Directory.CreateDirectory(Folder);
            var game=UnityEngine.Object.FindFirstObjectByType<DawnGardenGame>();
            var world=GameObject.Find("Garden World"); var surface=world.GetComponent<NavMeshSurface>();
            var old=UnityEngine.Object.FindFirstObjectByType<GardenExpansion>();
            var original=old != null ? old.originalNavigation : surface.navMeshData;
            if (old != null)
            {
                foreach (var stone in old.seamStones) if (stone != null) stone.SetActive(true);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var root=new GameObject("First Land Expansion"); root.transform.SetParent(world.transform,false);
            var expansion=root.AddComponent<GardenExpansion>(); expansion.game=game; game.expansion=expansion;
            expansion.surface=surface; expansion.originalNavigation=original;
            var land=new GameObject("New Meadow Land"); land.layer=8; land.transform.SetParent(root.transform,false);
            land.transform.position=expansion.finalPosition;
            expansion.land=land.transform;
            var mesh=CreateMesh(); land.AddComponent<MeshFilter>().sharedMesh=mesh;
            land.AddComponent<MeshRenderer>().sharedMaterials=new[] {
                AssetDatabase.LoadAssetAtPath<Material>("Assets/DawnGarden/Art/Polish/Soft Meadow.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/DawnGarden/Art/Polish/Warm Limestone Cliff.mat") };
            expansion.landCollider=land.AddComponent<MeshCollider>(); expansion.landCollider.sharedMesh=mesh;
            var stoneMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/DawnGarden/Art/Polish/Shore Limestone.mat");
            for (var i=0;i<48;i++)
            {
                var a=i*Mathf.PI*2/48; var edge=Edge(a);
                if (InsideOldIsland(expansion.finalPosition+edge)) continue;
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name="New meadow shore stone";
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(land.transform,false); go.transform.localPosition=edge+Vector3.down*.08f;
                go.transform.localScale=new Vector3(.38f,.23f,.3f); go.transform.localRotation=Quaternion.Euler(0,i*31,7);
                go.GetComponent<Renderer>().sharedMaterial=stoneMat;
            }
            var flower=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DawnGarden/Prefabs/Garden/meadow-flower.prefab");
            for (var i=0;i<13;i++)
            {
                var a=i*2.39996f; var p=Edge(a)*.79f;
                if (p.x < -.9f) continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(flower,land.transform);
                go.transform.localPosition=p+Vector3.up*.015f; go.transform.localScale*=.65f;
                go.transform.localRotation=Quaternion.Euler(0,i*37,0);
            }
            expansion.seamStones=UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(g=>g.name=="Shore rim stone" && InsideNewLand(g.transform.position,expansion.finalPosition)).ToArray();
            foreach (var stone in expansion.seamStones) stone.SetActive(false);
            Physics.SyncTransforms(); surface.BuildNavMesh();
            var generated=surface.navMeshData;
            if (generated == null) throw new InvalidOperationException("Expanded navigation failed");
            surface.RemoveData(); var path=Folder+"/ExpandedNavigation.asset";
            var stored=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (stored == null) { AssetDatabase.CreateAsset(generated,path); stored=generated; }
            else { EditorUtility.CopySerialized(generated,stored); UnityEngine.Object.DestroyImmediate(generated); }
            expansion.expandedNavigation=stored;
            surface.navMeshData=original; surface.AddData();
            foreach (var stone in expansion.seamStones) stone.SetActive(true);
            land.SetActive(false);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Dawn Seafoam.mat");
            if (material == null)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.SetColor("_BaseColor",new Color(1,.96f,.72f,.85f));
                material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetFloat("_ZWrite",0);
                material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
                AssetDatabase.CreateAsset(material,Folder+"/Dawn Seafoam.mat");
            }
            expansion.effectMaterial=material;
            EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene); AssetDatabase.SaveAssets();
        }
        static Vector3 Edge(float a)
        {
            var r=1+.032f*Mathf.Sin(3*a+.4f)+.018f*Mathf.Cos(7*a);
            return new Vector3(Mathf.Cos(a)*3.25f*r,0,Mathf.Sin(a)*2.75f*r);
        }
        static bool InsideOldIsland(Vector3 p) => p.x*p.x/(6.12f*6.12f)+p.z*p.z/(5.12f*5.12f)<1.01f;
        static bool InsideNewLand(Vector3 p,Vector3 center)
        { var d=p-center; return d.x*d.x/(3.30f*3.30f)+d.z*d.z/(2.80f*2.80f)<1; }
        static Mesh CreateMesh()
        {
            const int n=96; var v=new List<Vector3>(); var uv=new List<Vector2>();
            var top=new List<int>(); var sides=new List<int>();
            v.Add(Vector3.zero); uv.Add(Vector2.zero);
            for (var i=0;i<n;i++) { var e=Edge(i*Mathf.PI*2/n); v.Add(e); uv.Add(new Vector2((e.x+7.5f)*.22f,e.z*.22f)); }
            uv[0]=new Vector2(7.5f*.22f,0);
            for (var i=0;i<n;i++) { top.Add(0); top.Add(1+(i+1)%n); top.Add(1+i); }
            for (var i=0;i<n;i++)
            {
                var e=Edge(i*Mathf.PI*2/n); var f=Edge((i+1)*Mathf.PI*2/n); var b=v.Count;
                v.AddRange(new[] { e,f,f*.89f+Vector3.down*1.45f,e*.89f+Vector3.down*1.45f });
                uv.AddRange(new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up });
                sides.AddRange(new[] {b,b+1,b+2,b,b+2,b+3});
            }
            var mesh=new Mesh {name="New meadow with connected shore",subMeshCount=2};
            mesh.SetVertices(v); mesh.SetUVs(0,uv); mesh.SetTriangles(top,0); mesh.SetTriangles(sides,1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var path=Folder+"/NewMeadow.asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh,path);
            else { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; }
            return mesh;
        }
    }
}

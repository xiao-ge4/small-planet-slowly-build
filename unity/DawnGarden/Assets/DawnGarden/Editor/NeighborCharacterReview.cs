using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGarden.Editor
{
    /// <summary>Isolated Mecanim sampling, geometry checks and actual model portraits.</summary>
    public static class NeighborCharacterReview
    {
        static readonly string[] States={"Idle","Walk","Jog","Greet","Water"};
        static readonly float[] Phases={0,.25f,.5f,.75f};
        const float ExpectedHeight=1.45f;
        [Serializable] public sealed class Pose
        {
            public string state,image,error;
            public float phase,actualNormalizedTime;
            public bool geometryValid,stateMatched;
            public int vertices;
            public Vector3 min,max,size;
            public string[] warnings;
        }
        [Serializable] public sealed class MotionCheck
        {
            public string state;
            public int samples;
            public bool motionDetected;
            public float maximumPositionDelta,maximumRotationDegrees;
        }
        [Serializable] public sealed class Report
        {
            public int schemaVersion=1;
            public string character,prefab,prefabSha256,createdUtc;
            public bool structuralChecksPassed,visualReviewRequired=true;
            public string method="Unity Mecanim Animator.Play/Update; actual SkinnedMeshRenderer.BakeMesh at four phases per motion";
            public string[] manualReview={"Face, silhouette and materials remain faithful to the approved views",
                "Armpit/crotch deformation, self-intersection and stretched weights",
                "Feet slide, penetrate or float between sampled frames",
                "Watering hand reaches the actual tool and plant; props are not simulated here"};
            public Pose[] poses;
            public MotionCheck[] motions;
        }
        [Serializable] sealed class PortraitEntry
        {
            public string character,prefab,output,status;
        }
        [Serializable] sealed class PortraitReport
        {
            public string createdUtc,method="Front upper-body renders of local prefabs; no generated imagery";
            public PortraitEntry[] characters;
        }

        public static string Inspect(string character,string output)
        {
            GuardEditor();
            if(character!="lumi"&&character!="lanlan")throw new ArgumentException("Expected lumi or lanlan.");
            string resource="Neighbors/"+(character=="lumi"?"Lumi":"Lanlan");
            var prefab=Resources.Load<GameObject>(resource);
            if(prefab==null)throw new FileNotFoundException("Missing neighbor prefab: "+resource);
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);
            var report=new Report {character=character,prefab=AssetDatabase.GetAssetPath(prefab),createdUtc=DateTime.UtcNow.ToString("O")};
            using(var hash=SHA256.Create())report.prefabSha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(report.prefab))).Replace("-","").ToLowerInvariant();
            var preview=new PreviewRenderUtility();
            var poses=new List<Pose>();
            try
            {
                var actor=UnityEngine.Object.Instantiate(prefab);actor.name=character+" · isolated motion review";
                actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);preview.AddSingleGO(actor);
                DisableBehaviours(actor);
                var animator=actor.GetComponentInChildren<Animator>(true);
                if(animator==null||animator.runtimeAnimatorController==null)throw new InvalidOperationException("Prefab has no Mecanim controller.");
                animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(skins.Length==0||skins.Any(s=>s.sharedMesh==null||s.bones.Length<12||s.bones.Any(b=>b==null)))
                    throw new InvalidOperationException("Missing skin mesh or usable bone hierarchy.");
                if(skins.SelectMany(s=>s.sharedMaterials).Any(m=>m==null||m.shader==null||!m.shader.isSupported))
                    throw new InvalidOperationException("Missing or unsupported character material.");
                foreach(var skin in skins)skin.updateWhenOffscreen=true;
                var bones=skins.SelectMany(s=>s.bones).Distinct().ToArray();
                var firstPositions=new Dictionary<string,Vector3[]>();
                var firstRotations=new Dictionary<string,Quaternion[]>();
                var motions=States.ToDictionary(s=>s,s=>new MotionCheck {state=s});
                SetupLight(preview,new Color(.88f,.90f,.86f));
                using var visiblePose = new VisibleSkinPose(skins);
                foreach(var state in States)
                foreach(float phase in Phases)
                {
                    var sample=new Pose {state=state,phase=phase,image=state.ToLowerInvariant()+"-"+Mathf.RoundToInt(phase*100).ToString("D2")+".png"};
                    var warnings=new List<string>();
                    try
                    {
                        animator.Rebind();
                        if(!animator.HasState(0,Animator.StringToHash(state)))throw new InvalidOperationException("Missing Mecanim state "+state);
                        animator.Play(state,0,phase);animator.Update(0);
                        var actual=animator.GetCurrentAnimatorStateInfo(0);
                        sample.actualNormalizedTime=actual.normalizedTime;
                        sample.stateMatched=actual.shortNameHash==Animator.StringToHash(state);
                        if(!sample.stateMatched)throw new InvalidOperationException("Mecanim did not enter requested state "+state);
                        var motion=motions[state];motion.samples++;
                        if(!firstPositions.ContainsKey(state))
                        {
                            firstPositions[state]=bones.Select(b=>b.localPosition).ToArray();
                            firstRotations[state]=bones.Select(b=>b.localRotation).ToArray();
                        }
                        else for(int b=0;b<bones.Length;b++)
                        {
                            motion.maximumPositionDelta=Mathf.Max(motion.maximumPositionDelta,Vector3.Distance(firstPositions[state][b],bones[b].localPosition));
                            motion.maximumRotationDegrees=Mathf.Max(motion.maximumRotationDegrees,Quaternion.Angle(firstRotations[state][b],bones[b].localRotation));
                        }
                        var bounds=BakeBounds(skins,out int vertices);sample.vertices=vertices;
                        sample.min=bounds.min;sample.max=bounds.max;sample.size=bounds.size;
                        if(bounds.size.y<ExpectedHeight*.22f||bounds.size.y>ExpectedHeight*2.5f||
                            bounds.size.x>ExpectedHeight*3||bounds.size.z>ExpectedHeight*3||bounds.center.magnitude>ExpectedHeight*3)
                            throw new InvalidOperationException("Abnormal sampled bounds; inspect skin deformation before use.");
                        if(bounds.min.y<-.10f)warnings.Add("Visible skin extends below the prefab ground; inspect feet/tool contact.");
                        if(bounds.min.y>.12f)warnings.Add("Visible skin is above ground in this sample; verify intended motion rather than floating.");
                        if(state=="Idle"&&phase==0&&Mathf.Abs(bounds.size.y-ExpectedHeight)>.03f)
                            warnings.Add("Idle rendered height differs from the 1.45 m import target.");
                        sample.geometryValid=true;
                    }
                    catch(Exception error){sample.geometryValid=false;sample.error=error.Message;}
                    sample.warnings=warnings.ToArray();
                    // Fixed framing keeps shape/foot comparisons meaningful. An
                    // exploded skin does not silently zoom the camera far away.
                    var view=(state=="Walk"||state=="Jog"||state=="Water") ? new Vector3(1,.10f,.38f) : new Vector3(.28f,.10f,1);
                    Frame(preview,new Vector3(0,ExpectedHeight*.52f,0),ExpectedHeight*.76f,view);
                    visiblePose.Refresh();
                    Render(preview,Path.Combine(output,sample.image),640,800,false);
                    poses.Add(sample);
                }
                foreach(var motion in motions.Values)motion.motionDetected=motion.samples==4&&
                    (motion.maximumPositionDelta>.00001f||motion.maximumRotationDegrees>.01f);
                report.poses=poses.ToArray();report.motions=States.Select(s=>motions[s]).ToArray();
                report.structuralChecksPassed=poses.Count==20&&poses.All(p=>p.geometryValid&&p.stateMatched)&&report.motions.All(m=>m.motionDetected);
                File.WriteAllText(Path.Combine(output,"unity-motion-review.json"),JsonUtility.ToJson(report,true));
                return character+": "+poses.Count(p=>p.geometryValid)+"/20 structural pose checks; "+report.motions.Count(m=>m.motionDetected)+"/5 motions change bone transforms; visual review is still required.";
            }
            finally {preview.Cleanup();}
        }

        // Multiple Animator.Update calls can happen within one editor frame.
        // Display CPU-baked vertices so every rendered frame shows its own pose.
        internal sealed class VisibleSkinPose : IDisposable
        {
            readonly (SkinnedMeshRenderer Skin, Mesh Mesh, GameObject Object)[] entries;

            public VisibleSkinPose(IEnumerable<SkinnedMeshRenderer> skins)
            {
                entries = skins.Select(skin =>
                {
                    var proxy = new GameObject("Visible sampled pose");
                    proxy.transform.SetParent(skin.transform, false);
                    var mesh = new Mesh { indexFormat = skin.sharedMesh.indexFormat };
                    mesh.MarkDynamic();
                    proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                    proxy.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.forceRenderingOff = true;
                    return (skin, mesh, proxy);
                }).ToArray();
            }

            public void Refresh()
            {
                foreach (var entry in entries) entry.Skin.BakeMesh(entry.Mesh, true);
            }

            public void Dispose()
            {
                foreach (var entry in entries)
                {
                    entry.Skin.forceRenderingOff = false;
                    UnityEngine.Object.DestroyImmediate(entry.Object);
                    UnityEngine.Object.DestroyImmediate(entry.Mesh);
                }
            }
        }

        static Bounds BakeBounds(IEnumerable<SkinnedMeshRenderer> skins,out int count)
        {
            var mesh=new Mesh();var vertices=new List<Vector3>();var normals=new List<Vector3>();
            Bounds bounds=default;bool found=false;count=0;
            try
            {
                foreach(var skin in skins)
                {
                    if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                    mesh.Clear();skin.BakeMesh(mesh,true);mesh.GetVertices(vertices);mesh.GetNormals(normals);
                    foreach(var normal in normals)if(!Finite(normal))throw new InvalidOperationException("Non-finite baked normal.");
                    foreach(var v in vertices)
                    {
                        var world=skin.localToWorldMatrix.MultiplyPoint3x4(v);
                        if(!Finite(world))throw new InvalidOperationException("Non-finite baked vertex.");
                        if(!found){bounds=new Bounds(world,Vector3.zero);found=true;}else bounds.Encapsulate(world);
                        count++;
                    }
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(mesh);}
            if(!found||count<3)throw new InvalidOperationException("No visible skinned vertices.");
            return bounds;
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);

        public static string BuildPortraits()
        {
            GuardEditor();
            const string folder="Assets/SmallWorldV1/Resources/CharacterPortraits";
            Directory.CreateDirectory(folder);var entries=new List<PortraitEntry>();
            var paths=new Dictionary<string,string> {
                {"player","Assets/DawnGarden/Prefabs/Pet.prefab"},
                {"ajin","Assets/SmallWorldV1/Resources/Homecoming/Ajin.prefab"},
                {"lumi","Assets/SmallWorldV1/Resources/Neighbors/Lumi.prefab"},
                {"lanlan","Assets/SmallWorldV1/Resources/Neighbors/Lanlan.prefab"},
                {"guardian","Assets/SmallWorldV1/Resources/Guardian/MossstoneGuardian.prefab"}
            };
            foreach(var pair in paths)
            {
                var entry=new PortraitEntry {character=pair.Key,prefab=pair.Value,output=folder+"/"+pair.Key+".png"};entries.Add(entry);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
                if(prefab==null){entry.status="skipped: prefab does not exist";continue;}
                var preview=new PreviewRenderUtility();
                try
                {
                    var actor=UnityEngine.Object.Instantiate(prefab);preview.AddSingleGO(actor);actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);DisableBehaviours(actor);
                    var animator=actor.GetComponentInChildren<Animator>(true);
                    if(animator!=null&&animator.runtimeAnimatorController!=null)
                    {animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Play("Idle",0,0);animator.Update(0);}
                    var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                    Bounds bounds;
                    if(skins.Length>0)bounds=BakeBounds(skins,out _);
                    else
                    {
                        var renderers=actor.GetComponentsInChildren<Renderer>().Where(r=>!(r is ParticleSystemRenderer)).ToArray();
                        if(renderers.Length==0)throw new InvalidOperationException("No visible portrait mesh.");
                        bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                    }
                    SetupLight(preview,new Color(.91f,.94f,.87f,0));
                    // Bounds of some imported skins contain invisible or padded
                    // vertices. Fit the final portrait to the *rendered* alpha
                    // silhouette, then render afresh at full output resolution.
                    Frame(preview,bounds.center,Mathf.Max(bounds.extents.y,bounds.extents.x)*1.25f,new Vector3(.10f,.025f,1));
                    FitPortraitToVisiblePixels(preview);
                    Render(preview,entry.output,384,384,true);
                    AssetDatabase.ImportAsset(entry.output,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(entry.output);
                    importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaIsTransparency=true;
                    importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                    entry.status="rendered from prefab";
                }
                catch(Exception error){entry.status="failed: "+error.Message;}
                finally {preview.Cleanup();}
            }
            var report=new PortraitReport {createdUtc=DateTime.UtcNow.ToString("O"),characters=entries.ToArray()};
            File.WriteAllText(Path.Combine(folder,"portrait-build.json"),JsonUtility.ToJson(report,true));
            AssetDatabase.Refresh();
            return "Character portraits: "+entries.Count(e=>e.status=="rendered from prefab")+" rendered; "+
                   string.Join("; ",entries.Where(e=>e.status!="rendered from prefab").Select(e=>e.character+" "+e.status));
        }

        static void GuardEditor()
        {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play Mode before character review.");}
        static void DisableBehaviours(GameObject actor)
        {foreach(var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))if(behaviour!=null)behaviour.enabled=false;}
        static void SetupLight(PreviewRenderUtility preview,Color background)
        {
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=background;
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
            preview.camera.allowHDR=false;preview.camera.allowMSAA=true;
            preview.lights[0].intensity=1.05f;preview.lights[0].color=new Color(1,.94f,.86f);preview.lights[0].transform.rotation=Quaternion.Euler(32,210,0);
            preview.lights[1].intensity=.55f;preview.lights[1].color=new Color(.83f,.91f,1);preview.lights[1].transform.rotation=Quaternion.Euler(22,35,0);
            preview.ambientColor=new Color(.44f,.46f,.43f);
        }
        static void Frame(PreviewRenderUtility preview,Vector3 center,float halfHeight,Vector3 direction)
        {
            preview.camera.orthographic=true;preview.camera.orthographicSize=halfHeight;
            preview.camera.transform.position=center+direction.normalized*Mathf.Max(4,halfHeight*4);
            preview.camera.transform.LookAt(center);
        }
        static void Render(PreviewRenderUtility preview,string path,int width,int height,bool alpha)
        {
            var image=Capture(preview,width,height,alpha);
            try {File.WriteAllBytes(path,image.EncodeToPNG());}
            finally {UnityEngine.Object.DestroyImmediate(image);}
        }
        static void FitPortraitToVisiblePixels(PreviewRenderUtility preview)
        {
            const int size=512;
            for(int attempt=0;attempt<5;attempt++)
            {
                var image=Capture(preview,size,size,true);
                try
                {
                    var pixels=image.GetPixels32();int minX=size,minY=size,maxX=-1,maxY=-1;
                    for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                        if(pixels[y*size+x].a>32){minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                    if(maxX-minX<8||maxY-minY<12)throw new InvalidOperationException("Character silhouette is not visible enough for a portrait.");
                    if(minX<2||minY<2||maxX>size-3||maxY>size-3)
                    {
                        // Imported skin bounds can also under-estimate a posed
                        // character. Back off before fitting; never crop its ears.
                        preview.camera.orthographicSize*=1.65f;
                        continue;
                    }
                    float bodyHeight=maxY-minY+1,bodyWidth=maxX-minX+1;
                    float xCenter=(minX+maxX+1)*.5f,yFocus=minY+bodyHeight*.72f;
                    float unitsPerPixel=preview.camera.orthographicSize*2/size;
                    preview.camera.transform.position+=preview.camera.transform.right*((xCenter-size*.5f)*unitsPerPixel)+
                        preview.camera.transform.up*((yFocus-size*.5f)*unitsPerPixel);
                    preview.camera.orthographicSize=Mathf.Max(bodyHeight*.39f,bodyWidth*.57f)*unitsPerPixel;
                    return;
                }
                finally {UnityEngine.Object.DestroyImmediate(image);}
            }
            throw new InvalidOperationException("Full-body framing still clips after five views; inspect the prefab before making a portrait.");
        }
        static Texture2D Capture(PreviewRenderUtility preview,int width,int height,bool alpha)
        {
            preview.BeginPreview(new Rect(0,0,width,height),GUIStyle.none);preview.Render(true);var texture=preview.EndPreview();
            var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            Texture2D image=null;
            try
            {
                Graphics.Blit(texture,target);RenderTexture.active=target;
                image=new Texture2D(width,height,alpha?TextureFormat.RGBA32:TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();return image;
            }
            catch {if(image!=null)UnityEngine.Object.DestroyImmediate(image);throw;}
            finally
            {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
    }
}

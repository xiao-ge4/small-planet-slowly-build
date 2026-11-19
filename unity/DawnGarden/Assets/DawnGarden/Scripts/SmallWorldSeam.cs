using System.Collections.Generic;
using UnityEngine;

namespace DawnGarden
{
    public static class SmallWorldSeam
    {
        public static GameObject Build(Transform land,Material top,Material cliff)
        {
            var root=new GameObject("Natural continent connection");root.layer=LayerMask.NameToLayer("GardenGround");root.transform.SetParent(land,false);
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();var walls=new List<int>();
            const int n=24;
            // A broad curved waist joins the two plots. It rises with the extension,
            // while the starter shore stays intact before expansion.
            for(var i=0;i<=n;i++)
            {
                var t=i/(float)n;var x=4.55f+t*2.6f-land.position.x;
                var width=2.9f-.32f*Mathf.Sin(t*Mathf.PI);
                vertices.Add(new Vector3(x,.024f,-width));vertices.Add(new Vector3(x,.024f,width));
                uv.Add(new Vector2(t*1.2f,0));uv.Add(new Vector2(t*1.2f,1.8f));
                if(i>0){var k=i*2;tris.AddRange(new[]{k-2,k-1,k+1,k-2,k+1,k});}
            }
            for(var side=0;side<2;side++)for(var i=0;i<n;i++)
            {
                var a=vertices[i*2+side];var b=vertices[(i+1)*2+side];var index=vertices.Count;
                vertices.AddRange(new[]{a,b,b+Vector3.down*1.2f,a+Vector3.down*1.2f});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                if(side==0)walls.AddRange(new[]{index,index+2,index+1,index,index+3,index+2});else walls.AddRange(new[]{index,index+1,index+2,index,index+2,index+3});
            }
            var mesh=new Mesh {name="Continuous land waist",subMeshCount=2};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.SetTriangles(walls,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterials=new[]{top,cliff};root.AddComponent<MeshCollider>().sharedMesh=mesh;
            return root;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkVisual : MonoBehaviour
{
    private MeshFilter _mf;
    private MeshRenderer _mr;

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
    }

    public void ApplyMesh(ChunkMeshBuilder.MeshBuildResult r, bool flatShading, Material material, RegionData.Region[] regions)
    {
        Mesh mesh = MeshPool.Get();
        mesh.Clear();

        int vs = r.vertsPerSide;
        float step = r.step;

        if (!flatShading)
        {
            Vector3[] verts = new Vector3[vs * vs];
            Vector2[] uvs = new Vector2[vs * vs];

            for (int z = 0; z < vs; z++)
            {
                for (int x = 0; x < vs; x++)
                {
                    int idx = z * vs + x;
                    verts[idx] = new Vector3(x * step, r.heights[idx], z * step);
                    uvs[idx] = new Vector2((float)x / (vs - 1), (float)z / (vs - 1));
                }
            }

            mesh.indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.triangles = r.triangles;
            mesh.uv = uvs;

            // assign precomputed normals (no RecalculateNormals)
            if (r.normals != null && r.normals.Length == verts.Length)
                mesh.normals = r.normals;

            Color[] cols = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                cols[i] = RegionData.EvaluateColor(regions, r.normalized[i]);
            }
            mesh.colors = cols;
        }
        else
        {
            // Flat shading: duplicate vertices per triangle and compute per-triangle normals
            int triCount = r.triangles.Length / 3;
            Vector3[] fVerts = new Vector3[triCount * 3];
            Vector2[] fUvs = new Vector2[triCount * 3];
            int[] fTris = new int[triCount * 3];
            Color[] cols = new Color[triCount * 3];
            Vector3[] fNormals = new Vector3[triCount * 3];

            for (int t = 0; t < triCount; t++)
            {
                int ia = r.triangles[t * 3 + 0];
                int ib = r.triangles[t * 3 + 1];
                int ic = r.triangles[t * 3 + 2];

                Vector3 va = new Vector3((ia % vs) * step, r.heights[ia], (ia / vs) * step);
                Vector3 vb = new Vector3((ib % vs) * step, r.heights[ib], (ib / vs) * step);
                Vector3 vc = new Vector3((ic % vs) * step, r.heights[ic], (ic / vs) * step);

                int o = t * 3;
                fVerts[o + 0] = va;
                fVerts[o + 1] = vb;
                fVerts[o + 2] = vc;

                fUvs[o + 0] = new Vector2((float)(ia % vs) / (vs - 1), (float)(ia / vs) / (vs - 1));
                fUvs[o + 1] = new Vector2((float)(ib % vs) / (vs - 1), (float)(ib / vs) / (vs - 1));
                fUvs[o + 2] = new Vector2((float)(ic % vs) / (vs - 1), (float)(ic / vs) / (vs - 1));

                Color ca = RegionData.EvaluateColor(regions, r.normalized[ia]);
                Color cb = RegionData.EvaluateColor(regions, r.normalized[ib]);
                Color cc = RegionData.EvaluateColor(regions, r.normalized[ic]);
                cols[o + 0] = ca; cols[o + 1] = cb; cols[o + 2] = cc;

                // compute flat normal
                Vector3 triNormal = Vector3.Cross(vb - va, vc - va);
                if (triNormal.sqrMagnitude > 1e-9f) triNormal.Normalize();
                else triNormal = Vector3.up;

                fNormals[o + 0] = triNormal;
                fNormals[o + 1] = triNormal;
                fNormals[o + 2] = triNormal;

                fTris[o + 0] = o + 0;
                fTris[o + 1] = o + 1;
                fTris[o + 2] = o + 2;
            }

            mesh.indexFormat = fVerts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = fVerts;
            mesh.triangles = fTris;
            mesh.uv = fUvs;
            mesh.normals = fNormals; // precomputed
            mesh.colors = cols;
        }

        // swap meshes (return previous to pool)
        var previous = _mf.sharedMesh;
        _mf.sharedMesh = mesh;
        _mr.sharedMaterial = material;

        if (previous != null && previous != mesh)
        {
            MeshPool.Release(previous);
        }
    }

    void OnDestroy()
    {
        if (_mf != null && _mf.sharedMesh != null)
        {
            MeshPool.Release(_mf.sharedMesh);
            _mf.sharedMesh = null;
        }
    }
}

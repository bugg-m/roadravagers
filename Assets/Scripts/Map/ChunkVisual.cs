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
            mesh.RecalculateNormals();

            Color[] cols = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                cols[i] = RegionData.EvaluateColor(regions, r.normalized[i]);
            }
            mesh.colors = cols;
        }
        else
        {
            int triCount = r.triangles.Length / 3;
            Vector3[] fVerts = new Vector3[triCount * 3];
            Vector2[] fUvs = new Vector2[triCount * 3];
            int[] fTris = new int[triCount * 3];
            Color[] cols = new Color[triCount * 3];

            for (int t = 0; t < triCount; t++)
            {
                int a = r.triangles[t * 3 + 0];
                int b = r.triangles[t * 3 + 1];
                int c = r.triangles[t * 3 + 2];

                Vector3 va = new Vector3((a % vs) * step, r.heights[a], (a / vs) * step);
                Vector3 vb = new Vector3((b % vs) * step, r.heights[b], (b / vs) * step);
                Vector3 vc = new Vector3((c % vs) * step, r.heights[c], (c / vs) * step);

                fVerts[t * 3 + 0] = va; fVerts[t * 3 + 1] = vb; fVerts[t * 3 + 2] = vc;
                fUvs[t * 3 + 0] = new Vector2((float)(a % vs) / (vs - 1), (float)(a / vs) / (vs - 1));
                fUvs[t * 3 + 1] = new Vector2((float)(b % vs) / (vs - 1), (float)(b / vs) / (vs - 1));
                fUvs[t * 3 + 2] = new Vector2((float)(c % vs) / (vs - 1), (float)(c / vs) / (vs - 1));

                cols[t * 3 + 0] = RegionData.EvaluateColor(regions, r.normalized[a]);
                cols[t * 3 + 1] = RegionData.EvaluateColor(regions, r.normalized[b]);
                cols[t * 3 + 2] = RegionData.EvaluateColor(regions, r.normalized[c]);

                fTris[t * 3 + 0] = t * 3 + 0;
                fTris[t * 3 + 1] = t * 3 + 1;
                fTris[t * 3 + 2] = t * 3 + 2;
            }

            mesh.indexFormat = fVerts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = fVerts;
            mesh.triangles = fTris;
            mesh.uv = fUvs;
            mesh.RecalculateNormals();
            mesh.colors = cols;
        }

        _mf.sharedMesh = mesh;
        _mr.sharedMaterial = material;
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

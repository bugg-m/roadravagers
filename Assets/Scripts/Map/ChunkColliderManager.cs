using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChunkColliderManager : MonoBehaviour
{
    [SerializeField] TerrainStreamController _controller;
    [SerializeField] int _colliderVertsPerSide = 9;
    [SerializeField] int _maxCollidersPerFrame = 1;
    [SerializeField] float _smoothing = 0.6f;
    [SerializeField] int _chunkSize = 32;

    readonly Queue<TerrainStreamController.ChunkInfo> _queue = new Queue<TerrainStreamController.ChunkInfo>();

    void Awake()
    {
        if (_controller == null) _controller = FindFirstObjectByType<TerrainStreamController>();
    }

    void OnEnable()
    {
        if (_controller != null) _controller.OnChunkReady += Enqueue;
    }

    void OnDisable()
    {
        if (_controller != null) _controller.OnChunkReady -= Enqueue;
    }

    void Enqueue(TerrainStreamController.ChunkInfo info) => _queue.Enqueue(info);

    void Update()
    {
        int assigned = 0;
        while (_queue.Count > 0 && assigned < _maxCollidersPerFrame)
        {
            var info = _queue.Dequeue();
            AssignCollider(info);
            assigned++;
        }
    }

    void AssignCollider(TerrainStreamController.ChunkInfo info)
    {
        if (info.chunkObject == null) return;
        MeshCollider mc = info.chunkObject.GetComponent<MeshCollider>();
        if (mc == null) mc = info.chunkObject.AddComponent<MeshCollider>();

        Mesh m = MeshPool.Get();
        BuildColliderMesh(m, info.coord, _chunkSize, _colliderVertsPerSide, _smoothing, _controller != null ? _controller.HeightMultiplier : 1f);
        mc.sharedMesh = m;
        mc.convex = false;

        // Avoid calling Physics.SyncTransforms() here — heavy and can cause job/temporary allocation pressure.
    }

    static void BuildColliderMesh(Mesh mesh, Vector2Int coord, int chunkSize, int vs, float smoothing, float heightMultiplier)
    {
        vs = Mathf.Max(3, vs);
        mesh.Clear();
        Vector3[] verts = new Vector3[vs * vs];
        Vector2[] uvs = new Vector2[vs * vs];
        float step = (float)chunkSize / (vs - 1);
        float chunkWx = coord.x * (float)chunkSize;
        float chunkWz = coord.y * (float)chunkSize;

        int i = 0;
        for (int z = 0; z < vs; z++)
        {
            for (int x = 0; x < vs; x++)
            {
                float worldX = chunkWx + x * step;
                float worldZ = chunkWz + z * step;
                float h = NoiseProvider.GetNormalized(worldX, worldZ) * heightMultiplier;
                h = Mathf.Lerp(h, NoiseProvider.GetNormalized(worldX + 0.1f, worldZ + 0.1f) * heightMultiplier, smoothing);
                verts[i] = new Vector3(x * step, h, z * step);
                uvs[i] = new Vector2((float)x / (vs - 1), (float)z / (vs - 1));
                i++;
            }
        }

        List<int> tris = new List<int>((vs - 1) * (vs - 1) * 6);
        for (int z = 0; z < vs - 1; z++)
            for (int x = 0; x < vs - 1; x++)
            {
                int a = z * vs + x;
                int b = a + 1;
                int c = (z + 1) * vs + x;
                int d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}

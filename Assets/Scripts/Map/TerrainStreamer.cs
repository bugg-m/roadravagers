using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class TerrainStreamer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Material baseMaterial;

    [Header("Chunk / LOD")]
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private int vertsPerSideNear = 33;
    [SerializeField] private int vertsPerSideFar = 17;
    [SerializeField] private int renderDistance = 1;
    [SerializeField] private int maxChunksPerFrame = 1;

    [Header("Noise / Height")]
    [SerializeField] private int seed = 1337;
    [SerializeField] private int octaves = 4;
    [SerializeField][Range(0.01f, 1f)] private float persistence = 0.5f;
    [SerializeField] private float lacunarity = 2f;
    [SerializeField] private float noiseScale = 0.006f;
    [SerializeField] private Vector2 globalOffset = Vector2.zero;
    [SerializeField] private float heightMultiplier = 12f;

    [Header("Visual")]
    [SerializeField][Range(8, 32)] private int textureResolution = 16;
    [SerializeField] private Color waterColor = new Color(0.05f, 0.2f, 0.6f);
    [SerializeField] private Color groundColor = new Color(0.12f, 0.6f, 0.14f);
    [SerializeField] private Color mountainColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color snowColor = Color.white;
    [SerializeField][Range(0f, 1f)] private float waterLevel = 0.35f;
    [SerializeField][Range(0f, 1f)] private float groundLevel = 0.6f;
    [SerializeField][Range(0f, 1f)] private float mountainLevel = 0.85f;

    public float HeightMultiplier => heightMultiplier;
    public int Seed => seed;
    public int Octaves => octaves;
    public float Persistence => persistence;
    public float Lacunarity => lacunarity;
    public float NoiseScale => noiseScale;
    public Vector2 GlobalOffset => globalOffset;

    Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();
    Queue<Vector2Int> genQueue = new Queue<Vector2Int>();
    Vector2Int currentPlayerChunk;
    MaterialPropertyBlock mpb;

    void Awake()
    {
        if (player == null)
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo) player = pgo.transform;
        }

        if (baseMaterial == null)
        {
            baseMaterial = new Material(Shader.Find("Standard"));
        }

        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        Noise.Initialize(seed, octaves, persistence, lacunarity, noiseScale, globalOffset);
        UpdateChunksImmediate();
    }

    void Update()
    {
        if (player == null) return;
        Vector2Int pc = WorldToChunk(player.position);
        if (pc != currentPlayerChunk)
        {
            currentPlayerChunk = pc;
            UpdateGenerationQueue(pc);
        }

        int did = 0;
        while (genQueue.Count > 0 && did < maxChunksPerFrame)
        {
            var c = genQueue.Dequeue();
            if (!activeChunks.ContainsKey(c))
            {
                StartCoroutine(GenerateChunkCoroutine(c));
            }
            did++;
        }

        RemoveFarChunks(currentPlayerChunk);
    }

    void UpdateGenerationQueue(Vector2Int center)
    {
        genQueue.Clear();
        for (int dx = -renderDistance; dx <= renderDistance; dx++)
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                var coord = new Vector2Int(center.x + dx, center.y + dz);
                if (!activeChunks.ContainsKey(coord))
                    genQueue.Enqueue(coord);
            }
    }

    void UpdateChunksImmediate()
    {
        if (player == null) return;
        currentPlayerChunk = WorldToChunk(player.position);
        UpdateGenerationQueue(currentPlayerChunk);

        int generated = 0;
        int needed = (2 * renderDistance + 1) * (2 * renderDistance + 1);
        while (genQueue.Count > 0 && generated < needed)
        {
            var coord = genQueue.Dequeue();
            if (!activeChunks.ContainsKey(coord))
            {
                var task = BuildChunkData(coord, vertsPerSideNear);
                CreateChunkFromData(coord, task);
                generated++;
            }
        }
    }

    IEnumerator GenerateChunkCoroutine(Vector2Int coord)
    {
        int distSq = (int)DistanceSquared(coord, currentPlayerChunk);
        int vs = (distSq <= 1) ? vertsPerSideNear : vertsPerSideFar;

        Task<MeshBuildResult> t = Task.Run(() => BuildChunkData(coord, vs));
        while (!t.IsCompleted) yield return null;
        if (t.IsFaulted)
        {
            yield break;
        }

        CreateChunkFromData(coord, t.Result);
    }

    MeshBuildResult BuildChunkData(Vector2Int coord, int vertsPerSideLocal)
    {
        int vs = Mathf.Max(3, vertsPerSideLocal);
        int vertCount = vs * vs;
        Vector3[] verts = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        float[] norm = new float[vertCount];

        float step = (float)chunkSize / (vs - 1);
        float chunkWx = coord.x * (float)chunkSize;
        float chunkWz = coord.y * (float)chunkSize;

        int vi = 0;
        for (int z = 0; z < vs; z++)
        {
            for (int x = 0; x < vs; x++)
            {
                float worldX = chunkWx + x * step;
                float worldZ = chunkWz + z * step;
                float n = Noise.GetHeightNormalized(worldX, worldZ);
                float y = n * heightMultiplier;
                verts[vi] = new Vector3(x * step, y, z * step);
                uvs[vi] = new Vector2((float)x / (vs - 1), (float)z / (vs - 1));
                norm[vi] = n;
                vi++;
            }
        }

        int[] tris = GenerateTriangles(vs);
        return new MeshBuildResult() { vertices = verts, uvs = uvs, triangles = tris, normalizedNoise = norm, vertsPerSide = vs };
    }

    void CreateChunkFromData(Vector2Int coord, MeshBuildResult r)
    {
        if (activeChunks.ContainsKey(coord)) { return; }

        GameObject go = new GameObject($"vis_chunk_{coord.x}_{coord.y}");
        go.transform.parent = transform;
        go.transform.localScale = Vector3.one;
        go.transform.position = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = baseMaterial;

        Mesh mesh = new Mesh();
        mesh.name = $"terrain_vis_{coord.x}_{coord.y}";
        mesh.indexFormat = (r.vertices.Length > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = r.vertices;
        mesh.triangles = r.triangles;
        mesh.uv = r.uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;

        Texture2D tex = CreateTextureFromNoise(r.normalizedNoise, r.vertsPerSide, textureResolution);
        mpb.Clear();
        mpb.SetTexture("_MainTex", tex);
        mpb.SetTexture("_BaseMap", tex);
        mr.SetPropertyBlock(mpb);

        activeChunks.Add(coord, go);
    }

    int[] GenerateTriangles(int vs)
    {
        int quad = (vs - 1) * (vs - 1);
        int[] tris = new int[quad * 6];
        int ti = 0;
        for (int z = 0; z < vs - 1; z++)
        {
            for (int x = 0; x < vs - 1; x++)
            {
                int a = z * vs + x;
                int b = a + 1;
                int c = (z + 1) * vs + x;
                int d = c + 1;

                tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
            }
        }
        return tris;
    }

    Texture2D CreateTextureFromNoise(float[] normNoise, int sourceRes, int texRes)
    {
        int res = Mathf.Max(8, texRes);
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[res * res];

        float scale = (float)sourceRes / res;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                int sx = Mathf.Min(sourceRes - 1, Mathf.FloorToInt(x * scale));
                int sy = Mathf.Min(sourceRes - 1, Mathf.FloorToInt(y * scale));
                float n = normNoise[sy * sourceRes + sx];
                pixels[y * res + x] = ColorByHeight(n);
            }
        }
        tex.SetPixels(pixels);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply(false);
        return tex;
    }

    Color ColorByHeight(float n)
    {
        if (n <= waterLevel) return waterColor;
        if (n <= groundLevel) return groundColor;
        if (n <= mountainLevel) return mountainColor;
        return snowColor;
    }

    Vector2Int WorldToChunk(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x / chunkSize), Mathf.FloorToInt(worldPos.z / chunkSize));
    }

    long DistanceSquared(Vector2Int a, Vector2Int b)
    {
        long dx = (long)a.x - b.x;
        long dy = (long)a.y - b.y;
        return dx * dx + dy * dy;
    }

    void RemoveFarChunks(Vector2Int center)
    {
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var kv in activeChunks)
        {
            Vector2Int coord = kv.Key;
            if (Mathf.Abs(coord.x - center.x) > renderDistance || Mathf.Abs(coord.y - center.y) > renderDistance)
            {
                toRemove.Add(coord);
            }
        }
        foreach (var coord in toRemove)
        {
            var go = activeChunks[coord];
            if (go != null)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Destroy(mf.sharedMesh);
                }
                Destroy(go);
            }
            activeChunks.Remove(coord);
        }
    }

    struct MeshBuildResult
    {
        public Vector3[] vertices;
        public Vector2[] uvs;
        public int[] triangles;
        public float[] normalizedNoise;
        public int vertsPerSide;
    }

    public void ClearAllChunks()
    {
        foreach (var kv in activeChunks)
            if (kv.Value != null) Destroy(kv.Value);
        activeChunks.Clear();
    }
}

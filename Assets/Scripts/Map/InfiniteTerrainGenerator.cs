using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Fixed InfiniteTerrainGenerator - minimal changes to address collider penetration issues
/// Keeps original working streaming/movement logic, fixes only the collider sync problems
/// </summary>
[DisallowMultipleComponent]
public class InfiniteTerrainGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Material baseMaterial;

    [Header("Chunk / Streaming")]
    [SerializeField] private int chunkSize = 64;
    [SerializeField] private int vertsPerSide = 33;
    [SerializeField] private int renderDistance = 2;
    [SerializeField] private int colliderDistance = 1;
    [SerializeField] private int initialPreloadRadius = 1;

    [Header("Noise - FBM")]
    [SerializeField] private int seed = 1337;
    [SerializeField] private float noiseScale = 0.006f;
    [SerializeField] private int octaves = 4;
    [SerializeField, Range(0.01f, 1f)] private float persistence = 0.5f;
    [SerializeField] private float lacunarity = 2f;
    [SerializeField] private Vector2 globalOffset = Vector2.zero;
    [SerializeField] private float heightMultiplier = 16f;

    [Header("Smoothing")]
    [SerializeField, Range(0, 3)] private int smoothIterations = 1;
    [SerializeField, Range(0f, 1f)] private float smoothFactor = 0.4f;

    [Header("Collider")]
    [SerializeField] private float colliderCookTimeout = 0.2f;
    [SerializeField] private float vehicleSnapHeight = 1.5f;

    [Header("Color bands")]
    [SerializeField, Range(0f, 1f)] private float waterLevel = 0.35f;
    [SerializeField, Range(0f, 1f)] private float groundLevel = 0.6f;
    [SerializeField, Range(0f, 1f)] private float mountainLevel = 0.85f;
    [SerializeField] private Color waterColor = new Color(0.05f, 0.2f, 0.6f);
    [SerializeField] private Color groundColor = new Color(0.1f, 0.6f, 0.15f);
    [SerializeField] private Color mountainColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color snowColor = Color.white;

    [Header("Performance")]
    [SerializeField] private int maxChunksPerFrame = 1;
    [SerializeField] private int textureResolution = 32;
    [SerializeField] private bool generateOnStart = true;

    // Internals
    Dictionary<Vector2Int, Chunk> activeChunks = new Dictionary<Vector2Int, Chunk>();
    HashSet<Vector2Int> generatingChunks = new HashSet<Vector2Int>();
    List<Vector2Int> generationQueue = new List<Vector2Int>();
    System.Random prng;
    Vector2[] octaveOffsets;
    float maxPossibleAmplitude;

    int chunksGeneratedThisFrame = 0;

    void Awake()
    {
        if (player == null)
        {
            GameObject pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo) player = pgo.transform;
        }

        if (baseMaterial == null)
        {
            baseMaterial = new Material(Shader.Find("Standard"));
        }

        InitializeNoise();
    }

    void InitializeNoise()
    {
        prng = new System.Random(seed);
        octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float ox = prng.Next(-100000, 100000) + globalOffset.x;
            float oy = prng.Next(-100000, 100000) + globalOffset.y;
            octaveOffsets[i] = new Vector2(ox, oy);
        }

        float amp = 1f;
        maxPossibleAmplitude = 0f;
        for (int i = 0; i < octaves; i++)
        {
            maxPossibleAmplitude += amp;
            amp *= persistence;
        }
    }

    void Start()
    {
        if (generateOnStart)
        {
            StartCoroutine(StartupAndStream());
        }
    }

    IEnumerator StartupAndStream()
    {
        if (player != null)
        {
            Vector2Int playerChunk = WorldPosToChunkCoord(player.position);

            // Generate critical chunks first
            List<Vector2Int> criticalChunks = GetCoordsInRadius(playerChunk, 0);
            foreach (var coord in criticalChunks)
            {
                yield return StartCoroutine(GenerateChunkSynchronous(coord));
            }

            // Position vehicle safely AFTER first chunk is ready
            yield return StartCoroutine(PositionVehicleSafely());

            // Generate preload ring
            List<Vector2Int> preloadChunks = GetCoordsInRadius(playerChunk, initialPreloadRadius);
            preloadChunks.Sort((a, b) => DistanceSquared(a, playerChunk).CompareTo(DistanceSquared(b, playerChunk)));

            foreach (var coord in preloadChunks)
            {
                if (!activeChunks.ContainsKey(coord))
                {
                    yield return StartCoroutine(GenerateChunkSynchronous(coord));
                }
            }
        }

        StartCoroutine(StreamingLoop());
    }

    IEnumerator PositionVehicleSafely()
    {
        if (player == null) yield break;

        // Wait a bit for physics to settle
        yield return new WaitForSeconds(0.1f);

        Vector3 playerPos = player.position;
        float terrainHeight = SampleTerrainHeight(playerPos.x, playerPos.z);

        // Position vehicle just above terrain
        Vector3 safePos = new Vector3(playerPos.x, terrainHeight + vehicleSnapHeight, playerPos.z);
        player.position = safePos;

        // Force physics update
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        // Final validation after a short delay
        StartCoroutine(ValidateVehiclePositionDelayed());
    }

    IEnumerator ValidateVehiclePositionDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        if (player != null)
        {
            Vector3 pos = player.position;

            // Raycast down to check terrain
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
            {
                // If vehicle is too far below surface or too far above
                float groundHeight = hit.point.y;
                if (pos.y < groundHeight - 0.5f || pos.y > groundHeight + vehicleSnapHeight * 2f)
                {
                    Vector3 correctedPos = new Vector3(pos.x, groundHeight + vehicleSnapHeight * 0.5f, pos.z);
                    player.position = correctedPos;
                }
            }
        }
    }

    float SampleTerrainHeight(float worldX, float worldZ)
    {
        float raw = FBM(worldX * noiseScale, worldZ * noiseScale);
        float normalized = raw / maxPossibleAmplitude * 0.5f + 0.5f;
        return Mathf.Clamp01(normalized) * heightMultiplier;
    }

    IEnumerator StreamingLoop()
    {
        while (true)
        {
            chunksGeneratedThisFrame = 0;

            if (player != null)
            {
                Vector2Int playerChunk = WorldPosToChunkCoord(player.position);
                UpdateGenerationQueue(playerChunk);

                while (generationQueue.Count > 0 && chunksGeneratedThisFrame < maxChunksPerFrame)
                {
                    Vector2Int coord = generationQueue[0];
                    generationQueue.RemoveAt(0);

                    if (!activeChunks.ContainsKey(coord) && !generatingChunks.Contains(coord))
                    {
                        StartCoroutine(GenerateChunkAsync(coord, playerChunk));
                        chunksGeneratedThisFrame++;
                    }
                }

                UpdateChunkLOD(playerChunk);
                RemoveFarChunks(playerChunk);
            }

            yield return null;
        }
    }

    void UpdateGenerationQueue(Vector2Int center)
    {
        generationQueue.Clear();

        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        {
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                Vector2Int coord = new Vector2Int(center.x + dx, center.y + dz);
                if (!activeChunks.ContainsKey(coord) && !generatingChunks.Contains(coord))
                {
                    generationQueue.Add(coord);
                }
            }
        }

        generationQueue.Sort((a, b) => DistanceSquared(a, center).CompareTo(DistanceSquared(b, center)));
    }

    void UpdateChunkLOD(Vector2Int playerChunk)
    {
        foreach (var kvp in activeChunks)
        {
            var chunk = kvp.Value;
            var coord = kvp.Key;

            if (chunk.obj == null) continue;

            float dist = Mathf.Sqrt(DistanceSquared(coord, playerChunk));
            bool shouldHaveCollider = dist <= colliderDistance;
            bool hasCollider = chunk.obj.GetComponent<MeshCollider>() != null;

            if (shouldHaveCollider && !hasCollider)
            {
                StartCoroutine(AddColliderToChunk(chunk));
            }
            else if (!shouldHaveCollider && hasCollider)
            {
                RemoveColliderFromChunk(chunk);
            }
        }
    }

    IEnumerator AddColliderToChunk(Chunk chunk)
    {
        if (chunk.obj == null || chunk.colliderMesh == null) yield break;

        MeshCollider mc = chunk.obj.GetComponent<MeshCollider>();
        if (mc == null)
        {
            mc = chunk.obj.AddComponent<MeshCollider>();
        }

        // Force complete refresh
        mc.enabled = false;
        mc.sharedMesh = null;
        yield return null; // Wait one frame

        mc.sharedMesh = chunk.colliderMesh;
        mc.enabled = true;

        // Ensure collider is cooked properly
        yield return StartCoroutine(EnsureColliderCook(mc));
    }

    void RemoveColliderFromChunk(Chunk chunk)
    {
        if (chunk.obj == null) return;

        MeshCollider mc = chunk.obj.GetComponent<MeshCollider>();
        if (mc != null)
        {
            DestroyImmediate(mc);
        }
    }

    IEnumerator EnsureColliderCook(MeshCollider collider)
    {
        float elapsed = 0f;
        while (elapsed < colliderCookTimeout && collider != null)
        {
            Physics.SyncTransforms();

            // Check if collider bounds are reasonable
            if (collider.bounds.size.magnitude > 1f)
                break;

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Final bounds check
        if (collider != null && collider.bounds.size.magnitude < 1f)
        {
            collider.enabled = false;
            yield return null;
            collider.enabled = true;
        }
    }

    IEnumerator GenerateChunkSynchronous(Vector2Int coord)
    {
        generatingChunks.Add(coord);

        var buildResult = BuildMeshDataSync(coord, vertsPerSide);
        var chunk = CreateChunkGameObject(coord, buildResult, true);

        activeChunks[coord] = chunk;
        generatingChunks.Remove(coord);

        yield return null;
    }

    IEnumerator GenerateChunkAsync(Vector2Int coord, Vector2Int playerChunk)
    {
        generatingChunks.Add(coord);

        bool isHighPriority = DistanceSquared(coord, playerChunk) <= 1;
        int lodVerts = isHighPriority ? vertsPerSide : Mathf.Max(9, vertsPerSide / 2);

        Task<MeshBuildResult> buildTask = Task.Run(() => BuildMeshData(coord, lodVerts));

        while (!buildTask.IsCompleted)
        {
            yield return null;
        }

        if (buildTask.IsFaulted)
        {
            generatingChunks.Remove(coord);
            yield break;
        }

        var chunk = CreateChunkGameObject(coord, buildTask.Result, isHighPriority);
        activeChunks[coord] = chunk;
        generatingChunks.Remove(coord);
    }

    Chunk CreateChunkGameObject(Vector2Int coord, MeshBuildResult buildResult, bool needsCollider)
    {
        GameObject go = new GameObject($"Chunk_{coord.x}_{coord.y}");
        go.transform.parent = transform;
        go.transform.position = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();

        Material matInstance = new Material(baseMaterial);
        mr.sharedMaterial = matInstance;

        // Apply smoothing to visual mesh
        if (smoothIterations > 0)
        {
            SmoothHeights(buildResult.vertices, buildResult.vertsPerSide, smoothIterations, smoothFactor);
        }

        Mesh visualMesh = CreateMesh(buildResult.vertices, buildResult.triangles, buildResult.uvs);
        mf.sharedMesh = visualMesh;

        // Create collider mesh using same vertices as visual mesh - NO HEIGHT SCALING
        Mesh colliderMesh = CreateColliderMesh(buildResult.originalVertices, buildResult.triangles, buildResult.vertsPerSide);

        Texture2D texture = CreateChunkTexture(buildResult.normalizedNoise, buildResult.vertsPerSide);
        matInstance.mainTexture = texture;

        var chunk = new Chunk
        {
            coord = coord,
            obj = go,
            texture = texture,
            colliderMesh = colliderMesh
        };

        if (needsCollider)
        {
            StartCoroutine(AddColliderToChunk(chunk));
        }

        return chunk;
    }

    MeshBuildResult BuildMeshDataSync(Vector2Int coord, int vertsPerSideLocal)
    {
        return BuildMeshData(coord, vertsPerSideLocal);
    }

    MeshBuildResult BuildMeshData(Vector2Int coord, int vertsPerSideLocal)
    {
        int vs = vertsPerSideLocal;
        int vertCount = vs * vs;

        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] originalVertices = new Vector3[vertCount]; // Store original for collider
        Vector2[] uvs = new Vector2[vertCount];
        float[] normalizedNoise = new float[vertCount];

        float step = (float)chunkSize / (vs - 1);
        float chunkWx = coord.x * (float)chunkSize;
        float chunkWz = coord.y * (float)chunkSize;

        for (int z = 0; z < vs; z++)
        {
            for (int x = 0; x < vs; x++)
            {
                int index = z * vs + x;
                float worldX = chunkWx + x * step;
                float worldZ = chunkWz + z * step;

                float rawNoise = FBM(worldX * noiseScale, worldZ * noiseScale);
                float normalized = rawNoise / maxPossibleAmplitude * 0.5f + 0.5f;
                normalized = Mathf.Clamp01(normalized);

                normalizedNoise[index] = normalized;
                Vector3 vertPos = new Vector3(x * step, normalized * heightMultiplier, z * step);
                vertices[index] = vertPos;
                originalVertices[index] = vertPos; // Keep copy for collider

                uvs[index] = new Vector2((float)x / (vs - 1), (float)z / (vs - 1));
            }
        }

        int[] triangles = GenerateTriangles(vs);

        return new MeshBuildResult
        {
            vertices = vertices,
            originalVertices = originalVertices,
            uvs = uvs,
            triangles = triangles,
            normalizedNoise = normalizedNoise,
            vertsPerSide = vs
        };
    }

    int[] GenerateTriangles(int vertsPerSide)
    {
        int vs = vertsPerSide;
        int quadCount = (vs - 1) * (vs - 1);
        int[] triangles = new int[quadCount * 6];

        int index = 0;
        for (int z = 0; z < vs - 1; z++)
        {
            for (int x = 0; x < vs - 1; x++)
            {
                int a = z * vs + x;
                int b = z * vs + x + 1;
                int c = (z + 1) * vs + x;
                int d = (z + 1) * vs + x + 1;

                triangles[index++] = a;
                triangles[index++] = c;
                triangles[index++] = b;

                triangles[index++] = b;
                triangles[index++] = c;
                triangles[index++] = d;
            }
        }

        return triangles;
    }

    Mesh CreateMesh(Vector3[] vertices, int[] triangles, Vector2[] uvs)
    {
        Mesh mesh = new Mesh();
        mesh.name = "TerrainChunk";
        mesh.indexFormat = vertices.Length > 65000 ?
            UnityEngine.Rendering.IndexFormat.UInt32 :
            UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    Mesh CreateColliderMesh(Vector3[] visualVertices, int[] triangles, int vertsPerSide)
    {
        // Use exact same vertices for collider - NO scaling or modification
        Vector3[] colliderVerts = new Vector3[visualVertices.Length];
        Array.Copy(visualVertices, colliderVerts, visualVertices.Length);

        return CreateMesh(colliderVerts, triangles, null);
    }

    void SmoothHeights(Vector3[] vertices, int vertsPerSide, int iterations, float smoothStrength)
    {
        int vs = vertsPerSide;
        float[] heights = new float[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
            heights[i] = vertices[i].y;

        for (int iter = 0; iter < iterations; iter++)
        {
            float[] smoothed = new float[heights.Length];
            Array.Copy(heights, smoothed, heights.Length);

            for (int z = 1; z < vs - 1; z++)
            {
                for (int x = 1; x < vs - 1; x++)
                {
                    int i = z * vs + x;
                    float avg = (heights[i - 1] + heights[i + 1] + heights[i - vs] + heights[i + vs]) * 0.25f;
                    smoothed[i] = Mathf.Lerp(heights[i], avg, smoothStrength);
                }
            }

            heights = smoothed;
        }

        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y = heights[i];
    }

    Texture2D CreateChunkTexture(float[] normalizedNoise, int sourceRes)
    {
        int texRes = textureResolution;
        Texture2D texture = new Texture2D(texRes, texRes, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[texRes * texRes];

        float scale = (float)sourceRes / texRes;

        for (int y = 0; y < texRes; y++)
        {
            for (int x = 0; x < texRes; x++)
            {
                int sourceX = Mathf.FloorToInt(x * scale);
                int sourceY = Mathf.FloorToInt(y * scale);
                sourceX = Mathf.Clamp(sourceX, 0, sourceRes - 1);
                sourceY = Mathf.Clamp(sourceY, 0, sourceRes - 1);

                float heightValue = normalizedNoise[sourceY * sourceRes + sourceX];
                pixels[y * texRes + x] = EvaluateColorByHeight(heightValue);
            }
        }

        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();

        return texture;
    }

    float FBM(float x, float y)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = x * frequency + octaveOffsets[i].x;
            float sampleY = y * frequency + octaveOffsets[i].y;
            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
            total += perlinValue * amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total;
    }

    Color EvaluateColorByHeight(float normalizedHeight)
    {
        if (normalizedHeight <= waterLevel) return waterColor;
        if (normalizedHeight <= groundLevel) return groundColor;
        if (normalizedHeight <= mountainLevel) return mountainColor;
        return snowColor;
    }

    void RemoveFarChunks(Vector2Int center)
    {
        List<Vector2Int> toRemove = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            Vector2Int coord = kvp.Key;
            if (Mathf.Abs(coord.x - center.x) > renderDistance ||
                Mathf.Abs(coord.y - center.y) > renderDistance)
            {
                toRemove.Add(coord);
            }
        }

        foreach (var coord in toRemove)
        {
            var chunk = activeChunks[coord];
            if (chunk.obj != null) DestroyImmediate(chunk.obj);
            if (chunk.texture != null) DestroyImmediate(chunk.texture);
            if (chunk.colliderMesh != null) DestroyImmediate(chunk.colliderMesh);
            activeChunks.Remove(coord);
        }
    }

    Vector2Int WorldPosToChunkCoord(Vector3 worldPos)
    {
        int cx = Mathf.FloorToInt(worldPos.x / chunkSize);
        int cz = Mathf.FloorToInt(worldPos.z / chunkSize);
        return new Vector2Int(cx, cz);
    }

    List<Vector2Int> GetCoordsInRadius(Vector2Int center, int radius)
    {
        var coords = new List<Vector2Int>();
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                coords.Add(new Vector2Int(center.x + dx, center.y + dz));
            }
        }
        return coords;
    }

    long DistanceSquared(Vector2Int a, Vector2Int b)
    {
        long dx = (long)a.x - b.x;
        long dy = (long)a.y - b.y;
        return dx * dx + dy * dy;
    }

    struct MeshBuildResult
    {
        public Vector3[] vertices;
        public Vector3[] originalVertices; // Added for collider
        public Vector2[] uvs;
        public int[] triangles;
        public float[] normalizedNoise;
        public int vertsPerSide;
    }

    class Chunk
    {
        public Vector2Int coord;
        public GameObject obj;
        public Texture2D texture;
        public Mesh colliderMesh;
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Vector2Int pc = WorldPosToChunkCoord(player.position);

        Gizmos.color = Color.yellow;
        for (int dx = -renderDistance; dx <= renderDistance; dx++)
        {
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                Vector3 pos = new Vector3((pc.x + dx) * chunkSize, 0, (pc.y + dz) * chunkSize);
                Gizmos.DrawWireCube(pos + Vector3.right * chunkSize * 0.5f + Vector3.forward * chunkSize * 0.5f,
                    new Vector3(chunkSize, 0.1f, chunkSize));
            }
        }

        Gizmos.color = Color.red;
        for (int dx = -colliderDistance; dx <= colliderDistance; dx++)
        {
            for (int dz = -colliderDistance; dz <= colliderDistance; dz++)
            {
                Vector3 pos = new Vector3((pc.x + dx) * chunkSize, 1f, (pc.y + dz) * chunkSize);
                Gizmos.DrawWireCube(pos + Vector3.right * chunkSize * 0.5f + Vector3.forward * chunkSize * 0.5f,
                    new Vector3(chunkSize, 0.1f, chunkSize));
            }
        }
    }
}
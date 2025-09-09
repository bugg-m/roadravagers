using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class TerrainStreamController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform _player;
    [SerializeField] Material _sharedMaterial;
    [SerializeField] RegionData _regionData;

    [Header("Chunk")]
    [SerializeField] int _chunkSize = 32;
    [SerializeField] int _vertsNear = 33;
    [SerializeField] int _vertsFar = 17;
    [SerializeField] int _renderDistance = 2;
    [SerializeField] int _colliderDistance = 1;

    [Header("Noise / Height")]
    [SerializeField] int _seed = 1337;
    [SerializeField] int _octaves = 4;
    [SerializeField][Range(0.01f, 1f)] float _persistence = 0.5f;
    [SerializeField] float _lacunarity = 2f;
    [SerializeField] float _noiseScale = 0.01f;
    [SerializeField] Vector2 _globalOffset = default;
    [SerializeField] float _heightMultiplier = 12f;
    [SerializeField][Range(0, 32)] int _quantizeBands = 0;
    [SerializeField] bool _flatShading = true;

    [Header("Performance")]
    [SerializeField] int _maxChunksPerFrame = 1;
    [SerializeField] int _maxConcurrentBuilds = 2;

    public event Action<ChunkInfo> OnChunkReady;
    public event Action<Vector2Int> OnChunkRemoved;

    public int Seed => _seed;
    public float HeightMultiplier => _heightMultiplier;
    public int ChunkSize => _chunkSize;

    readonly Dictionary<Vector2Int, ChunkInfo> _active = new Dictionary<Vector2Int, ChunkInfo>();
    readonly ConcurrentQueue<KeyValuePair<Vector2Int, ChunkMeshBuilder.MeshBuildResult>> _finishedQueue = new ConcurrentQueue<KeyValuePair<Vector2Int, ChunkMeshBuilder.MeshBuildResult>>();
    readonly HashSet<Vector2Int> _building = new HashSet<Vector2Int>();
    readonly Queue<Vector2Int> _genQueue = new Queue<Vector2Int>();
    SemaphoreSlim _buildSemaphore;

    Vector2Int _currentPlayerChunk;

    void Awake()
    {
        if (_player == null) _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (_sharedMaterial == null) _sharedMaterial = new Material(Shader.Find("Standard"));
        NoiseProvider.Initialize(_seed, _octaves, _persistence, _lacunarity, _noiseScale, _globalOffset);
        _buildSemaphore = new SemaphoreSlim(Math.Max(1, _maxConcurrentBuilds));
    }

    void Start()
    {
        if (_player == null) { Debug.LogError("[TerrainStreamController] Player not assigned."); enabled = false; return; }

        _currentPlayerChunk = WorldToChunk(_player.position);
        GenerateAndApplyImmediate(_currentPlayerChunk);

        for (int dx = -_renderDistance; dx <= _renderDistance; dx++)
            for (int dz = -_renderDistance; dz <= _renderDistance; dz++)
            {
                var c = _currentPlayerChunk + new Vector2Int(dx, dz);
                if (!_active.ContainsKey(c)) GenerateAndApplyImmediate(c);
            }
    }

    void Update()
    {
        if (_player == null) return;

        var pc = WorldToChunk(_player.position);
        if (pc != _currentPlayerChunk)
        {
            _currentPlayerChunk = pc;
            EnqueueSurrounding(pc);
        }

        int started = 0;
        while (_genQueue.Count > 0 && started < _maxChunksPerFrame)
        {
            var coord = _genQueue.Dequeue();
            if (!_active.ContainsKey(coord) && !_building.Contains(coord))
            {
                StartBuild(coord);
                started++;
            }
        }

        while (_finishedQueue.TryDequeue(out var kv))
        {
            CreateChunkObject(kv.Key, kv.Value);
        }

        RemoveFarChunks(_currentPlayerChunk);
    }

    void EnqueueSurrounding(Vector2Int center)
    {
        for (int dx = -_renderDistance; dx <= _renderDistance; dx++)
            for (int dz = -_renderDistance; dz <= _renderDistance; dz++)
            {
                var coord = center + new Vector2Int(dx, dz);
                if (!_active.ContainsKey(coord) && !_building.Contains(coord))
                    _genQueue.Enqueue(coord);
            }
    }

    void StartBuild(Vector2Int coord)
    {
        _building.Add(coord);
        _ = Task.Run(async () =>
        {
            await _buildSemaphore.WaitAsync();
            try
            {
                int distSq = (int)DistanceSquared(coord, _currentPlayerChunk);
                int verts = distSq <= 1 ? _vertsNear : _vertsFar;
                var build = ChunkMeshBuilder.Build(_chunkSize, coord, verts, _octaves, _noiseScale, _persistence, _lacunarity, NoiseProvider.GetOffsets(), NoiseProvider.GetMaxPossibleAmplitude(), _heightMultiplier, _quantizeBands);
                _finishedQueue.Enqueue(new KeyValuePair<Vector2Int, ChunkMeshBuilder.MeshBuildResult>(coord, build));
            }
            catch (Exception ex) { Debug.LogException(ex); }
            finally { _building.Remove(coord); _buildSemaphore.Release(); }
        });
    }

    void GenerateAndApplyImmediate(Vector2Int coord)
    {
        var build = ChunkMeshBuilder.Build(_chunkSize, coord, _vertsNear, _octaves, _noiseScale, _persistence, _lacunarity, NoiseProvider.GetOffsets(), NoiseProvider.GetMaxPossibleAmplitude(), _heightMultiplier, _quantizeBands);
        CreateChunkObject(coord, build);
    }

    void CreateChunkObject(Vector2Int coord, ChunkMeshBuilder.MeshBuildResult build)
    {
        if (_active.ContainsKey(coord)) return;

        GameObject go = new GameObject($"chunk_{coord.x}_{coord.y}");
        go.transform.parent = transform;
        go.transform.position = new Vector3(coord.x * _chunkSize, 0f, coord.y * _chunkSize);

        var cv = go.AddComponent<ChunkVisual>();
        var mat = new Material(_sharedMaterial);
        cv.ApplyMesh(build, _flatShading, mat, _regionData?.regions);

        var info = new ChunkInfo { coord = coord, chunkObject = go, meshBuild = build };
        _active.Add(coord, info);

        OnChunkReady?.Invoke(info);
    }

    void RemoveFarChunks(Vector2Int center)
    {
        var toRemove = new List<Vector2Int>();
        foreach (var kv in _active)
        {
            var c = kv.Key;
            if (Mathf.Abs(c.x - center.x) > _renderDistance || Mathf.Abs(c.y - center.y) > _renderDistance) toRemove.Add(c);
        }

        foreach (var coord in toRemove)
        {
            var info = _active[coord];
            if (info.chunkObject != null) Destroy(info.chunkObject);
            _active.Remove(coord);
            OnChunkRemoved?.Invoke(coord);
        }
    }

    Vector2Int WorldToChunk(Vector3 pos) => new Vector2Int(Mathf.FloorToInt(pos.x / _chunkSize), Mathf.FloorToInt(pos.z / _chunkSize));
    long DistanceSquared(Vector2Int a, Vector2Int b) { long dx = (long)a.x - b.x; long dy = (long)a.y - b.y; return dx * dx + dy * dy; }

    public float GetHeightAtWorldPos(float worldX, float worldZ) => NoiseProvider.GetHeight(worldX, worldZ, _heightMultiplier);

    public struct ChunkInfo { public Vector2Int coord; public GameObject chunkObject; public ChunkMeshBuilder.MeshBuildResult meshBuild; }
}

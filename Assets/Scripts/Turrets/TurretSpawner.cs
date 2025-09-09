// TurretSpawner.cs
// Deterministic turret placement per chunk; listens to OnChunkReady/OnChunkRemoved and pools turrets.
using System;
using System.Collections.Generic;
using UnityEngine;

public class TurretSpawner : MonoBehaviour
{
    [SerializeField] TerrainStreamController _controller;
    [SerializeField] GameObject _turretPrefab;
    [SerializeField] Transform _parent;
    [SerializeField, Range(0, 8)] int _turretsPerChunk = 3;
    [SerializeField] float _minSpacing = 6f;
    [SerializeField] float _maxSlope = 25f;
    [SerializeField] int _initialPool = 32;

    GameObjectPool _pool;
    readonly Dictionary<Vector2Int, List<GameObject>> _spawned = new Dictionary<Vector2Int, List<GameObject>>();

    void Awake()
    {
        if (_controller == null) _controller = FindFirstObjectByType<TerrainStreamController>();
        if (_controller == null) { Debug.LogError("TerrainStreamController not found"); enabled = false; return; }
        if (_turretPrefab == null) { Debug.LogError("Turret prefab missing"); enabled = false; return; }

        _pool = new GameObjectPool(_turretPrefab, _initialPool, _parent);
    }

    void OnEnable()
    {
        _controller.OnChunkReady += HandleChunkReady;
        _controller.OnChunkRemoved += HandleChunkRemoved;
    }

    void OnDisable()
    {
        if (_controller != null)
        {
            _controller.OnChunkReady -= HandleChunkReady;
            _controller.OnChunkRemoved -= HandleChunkRemoved;
        }
    }

    void HandleChunkReady(TerrainStreamController.ChunkInfo info)
    {
        var coord = info.coord;
        if (_spawned.ContainsKey(coord)) return;

        var placements = GeneratePositions(coord);
        var list = new List<GameObject>(placements.Count);
        foreach (var p in placements)
        {
            var go = _pool.Get();
            go.transform.position = p;
            go.transform.rotation = Quaternion.identity;
            if (_parent != null) go.transform.SetParent(_parent, true);
            list.Add(go);
        }

        _spawned[coord] = list;
    }

    void HandleChunkRemoved(Vector2Int coord)
    {
        if (_spawned.TryGetValue(coord, out var list))
        {
            foreach (var go in list) _pool.Release(go);
            _spawned.Remove(coord);
        }
    }

    List<Vector3> GeneratePositions(Vector2Int coord)
    {
        var result = new List<Vector3>();
        int localSeed = HashInts(_controller.Seed, coord.x, coord.y);
        var rng = new System.Random(localSeed);
        int attempts = 0;
        while (result.Count < _turretsPerChunk && attempts < 64)
        {
            attempts++;
            float lx = (float)rng.NextDouble() * _controller.ChunkSize;
            float lz = (float)rng.NextDouble() * _controller.ChunkSize;
            float wx = coord.x * _controller.ChunkSize + lx;
            float wz = coord.y * _controller.ChunkSize + lz;
            float wy = NoiseProvider.GetHeight(wx, wz, _controller.HeightMultiplier);

            // slope sample
            float h1 = NoiseProvider.GetHeight(wx + 0.5f, wz, _controller.HeightMultiplier);
            float h2 = NoiseProvider.GetHeight(wx - 0.5f, wz, _controller.HeightMultiplier);
            float h3 = NoiseProvider.GetHeight(wx, wz + 0.5f, _controller.HeightMultiplier);
            float h4 = NoiseProvider.GetHeight(wx, wz - 0.5f, _controller.HeightMultiplier);
            float dx = (h1 - h2);
            float dz = (h3 - h4);
            float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
            if (slope > _maxSlope) continue;

            var pos = new Vector3(wx, wy, wz);
            bool ok = true;
            foreach (var ex in result) if (Vector3.Distance(ex, pos) < _minSpacing) { ok = false; break; }
            if (!ok) continue;
            result.Add(pos);
        }
        return result;
    }

    static int HashInts(params int[] xs)
    {
        unchecked { int h = 17; foreach (var v in xs) h = h * 31 + v; return h; }
    }
}

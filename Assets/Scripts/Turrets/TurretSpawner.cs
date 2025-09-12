using System.Collections.Generic;
using UnityEngine;

public class TurretSpawner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private TurretPoolManager poolManager;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float spawnRadius = 80f;
    [SerializeField] private float spacing = 12f;
    [SerializeField] private float spawnHeightOffset = 2f;
    [SerializeField] private float maxSlopeDegrees = 35f;
    [SerializeField] private float updateInterval = 0.6f;
    [SerializeField] private int maxAttemptsPerCell = 6;
    [SerializeField] private int rngSeed = 1234567;

    readonly Dictionary<Vector2Int, GameObject> active = new Dictionary<Vector2Int, GameObject>();
    float timeAcc = 0f;

    void Awake()
    {
        if (player == null)
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo) player = pgo.transform;
        }

        if (poolManager == null)
        {
            poolManager = FindFirstObjectByType<TurretPoolManager>();
        }

        if (poolManager != null) poolManager.OnTurretReleased += OnTurretReleased;
    }

    void OnDestroy()
    {
        if (poolManager != null) poolManager.OnTurretReleased -= OnTurretReleased;
    }

    void Update()
    {
        if (player == null || poolManager == null) return;

        timeAcc += Time.deltaTime;
        if (timeAcc < updateInterval) return;
        timeAcc = 0f;

        Vector2Int center = WorldToCell(player.position);
        int radiusCells = Mathf.CeilToInt(spawnRadius / spacing);

        // spawn cells
        for (int dz = -radiusCells; dz <= radiusCells; dz++)
        {
            for (int dx = -radiusCells; dx <= radiusCells; dx++)
            {
                var cell = center + new Vector2Int(dx, dz);
                var worldCellCenter = CellToWorld(cell) + new Vector3(spacing * 0.5f, 0f, spacing * 0.5f);
                float dist = Vector3.Distance(worldCellCenter.WithY(player.position.y), player.position);
                if (dist > spawnRadius) continue;

                if (!active.ContainsKey(cell))
                {
                    var p = FindSpawnPointInCell(cell, maxAttemptsPerCell);
                    if (p.HasValue)
                    {
                        var go = poolManager.Spawn(p.Value, Quaternion.identity, cell);
                        var turret = go.GetComponent<TurretAI>();
                        turret?.OnSpawned(player);
                        active[cell] = go;
                    }
                }
            }
        }

        var toRemove = new List<Vector2Int>();
        foreach (var kv in active)
        {
            var cell = kv.Key;
            var worldCellCenter = CellToWorld(cell) + new Vector3(spacing * 0.5f, 0f, spacing * 0.5f);
            float dist = Vector3.Distance(worldCellCenter.WithY(player.position.y), player.position);
            if (dist > spawnRadius * 1.15f) toRemove.Add(cell);
        }

        foreach (var cell in toRemove)
        {
            if (active.TryGetValue(cell, out var turret))
            {
                var pt = turret.GetComponent<TurretAI>();
                pt?.OnDespawned();
                poolManager.Release(turret, cell);
                active.Remove(cell);
            }
        }
    }

    Vector3? FindSpawnPointInCell(Vector2Int cell, int attempts)
    {
        var rng = new System.Random(rngSeed ^ (cell.x * 73856093) ^ (cell.y * 19349663));
        for (int i = 0; i < attempts; i++)
        {
            float rx = (float)rng.NextDouble() * spacing;
            float rz = (float)rng.NextDouble() * spacing;
            Vector3 pos = CellToWorld(cell) + new Vector3(rx, spawnHeightOffset, rz) + Vector3.up * 20f;
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 50f, groundMask))
            {
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope <= maxSlopeDegrees)
                {
                    return hit.point;
                }
            }
        }
        return null;
    }

    Vector2Int WorldToCell(Vector3 w)
    {
        int cx = Mathf.FloorToInt(w.x / spacing);
        int cz = Mathf.FloorToInt(w.z / spacing);
        return new Vector2Int(cx, cz);
    }

    Vector3 CellToWorld(Vector2Int c) => new Vector3(c.x * spacing, 0f, c.y * spacing);

    void OnTurretReleased(Vector2Int cell)
    {
        if (active.ContainsKey(cell)) active.Remove(cell);
    }
}

static class Vec3Ext
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}

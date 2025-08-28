using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance;

    [Header("Prefabs & Pool")]
    public GameObject turretPrefab;             // assign a prefab FROM the Project (not a scene object)
    public int initialPool = 30;                // number of pooled turrets to create at start

    [Header("Spawn Area")]
    public float spawnRadius = 80f;
    public float minSpawnDistanceFromPlayer = 12f;
    public int maxActiveTurrets = 18;
    public float spawnInterval = 0.6f;
    public float turretSpawnY = 0.5f;           // Y position where turret should sit (adjust to turret half-height)

    [Header("Debug")]
    public bool drawSpawnRadiusGizmo = true;
    public bool verboseLogs = false;

    List<GameObject> pool = new List<GameObject>();
    Transform player;
    float lastSpawn;
    GameObject poolParent;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
        {
            Debug.LogWarning("[SpawnManager] Player with tag 'Player' not found in scene.");
        }

        if (turretPrefab == null)
        {
            Debug.LogError("[SpawnManager] turretPrefab is NOT assigned in the Inspector. Spawner disabled.");
            enabled = false;
            return;
        }

        // Create a parent in the Hierarchy to keep pooled objects organized
        poolParent = new GameObject("TurretPool");
        poolParent.transform.SetParent(transform);

        // Build initial pool with unique instances
        for (int i = 0; i < initialPool; i++)
        {
            GameObject go = Instantiate(turretPrefab, Vector3.zero, Quaternion.identity, poolParent.transform);
            go.name = $"{turretPrefab.name}_pooled_{i}";
            go.SetActive(false);
            pool.Add(go);
        }

        if (verboseLogs) Debug.Log($"[SpawnManager] Pool created with {pool.Count} items.");
    }

    void Update()
    {
        if (player == null) return;                 // nothing to do without a player
        if (Time.time < lastSpawn + spawnInterval) return;

        // count active
        int active = 0;
        for (int i = 0; i < pool.Count; i++) if (pool[i] != null && pool[i].activeInHierarchy) active++;

        if (active >= maxActiveTurrets) return;

        // get a spawn pos
        Vector3 pos = RandomPositionAround(player.position, spawnRadius, minSpawnDistanceFromPlayer);
        pos.y = turretSpawnY;

        GameObject turret = GetFromPool();
        if (turret == null)
        {
            // expand pool as fallback
            turret = Instantiate(turretPrefab, pos, Quaternion.identity, poolParent.transform);
            turret.name = $"{turretPrefab.name}_pooled_expanded";
            pool.Add(turret);
            if (verboseLogs) Debug.Log("[SpawnManager] Pool expanded by 1.");
        }
        else
        {
            turret.transform.position = pos;
            turret.transform.rotation = Quaternion.identity;
            turret.SetActive(true);
        }

        lastSpawn = Time.time;
    }

    GameObject GetFromPool()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var g = pool[i];
            if (g == null) continue; // skip destroyed entries
            if (!g.activeInHierarchy) return g;
        }
        return null;
    }

    Vector3 RandomPositionAround(Vector3 center, float radius, float minDist)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(r.x, 0, r.y);
            if (Vector3.Distance(candidate, center) >= minDist)
            {
                return candidate;
            }
        }
        // fallback directly in front
        return center + Vector3.forward * (minDist + 4f);
    }

    // Allow turrets to be returned to pool
    public void ReturnToPool(GameObject g)
    {
        if (g == null) return;
        g.SetActive(false);
        g.transform.SetParent(poolParent.transform);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawSpawnRadiusGizmo || player == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, spawnRadius);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject turretPrefab;
    [SerializeField] private int initialTurretCount = 10;
    [SerializeField] private float mapRadius = 50f;
    [SerializeField] private float minDistanceBetweenTurrets = 5f;
    [SerializeField] private float minDistanceFromPlayer = 8f;

    [Header("Spawn Constraints")]
    [SerializeField] private LayerMask groundLayerMask = 1;
    [SerializeField] private float maxGroundCheckDistance = 10f;
    [SerializeField] private float turretHeightOffset = 0f;

    [Header("Dynamic Spawning")]
    [SerializeField] private bool enableDynamicSpawning = false;
    [SerializeField] private int maxTotalTurrets = 15;
    [SerializeField] private float respawnDelay = 10f;

    [Header("Exclusion Zones")]
    [SerializeField] private Transform[] noSpawnZones;
    [SerializeField] private float noSpawnRadius = 10f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private List<GameObject> spawnedTurrets = new List<GameObject>();
    private Transform playerTransform;
    private Vector3 mapCenter;
    private Coroutine dynamicSpawningCoroutine;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        mapCenter = transform.position;
        FindPlayer();

        if (turretPrefab == null)
        {
            Debug.LogError("Turret prefab is not assigned!");
            return;
        }

        SpawnInitialTurrets();

        if (enableDynamicSpawning)
        {
            dynamicSpawningCoroutine = StartCoroutine(DynamicSpawningLoop());
        }
    }

    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("Player not found for turret spawning distance calculations.");
        }
    }

    void SpawnInitialTurrets()
    {
        int attempts = 0;
        int spawned = 0;
        int maxAttempts = initialTurretCount * 10;

        while (spawned < initialTurretCount && attempts < maxAttempts)
        {
            Vector3 spawnPosition = GetRandomSpawnPosition();

            if (IsValidSpawnPosition(spawnPosition))
            {
                if (SpawnTurret(spawnPosition) != null)
                {
                    spawned++;
                }
            }
            attempts++;
        }

        Debug.Log($"Spawned {spawned}/{initialTurretCount} turrets after {attempts} attempts");
    }

    IEnumerator DynamicSpawningLoop()
    {
        while (enableDynamicSpawning)
        {
            yield return new WaitForSeconds(2f);

            CleanupDestroyedTurrets();

            int needed = maxTotalTurrets - spawnedTurrets.Count;
            if (needed > 0)
            {
                yield return StartCoroutine(SpawnBatch(needed));
            }
        }
    }

    IEnumerator SpawnBatch(int count)
    {
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = count * 5;

        while (spawned < count && attempts < maxAttempts)
        {
            Vector3 spawnPosition = GetRandomSpawnPosition();

            if (IsValidSpawnPosition(spawnPosition))
            {
                if (SpawnTurret(spawnPosition) != null)
                {
                    spawned++;
                    yield return new WaitForSeconds(respawnDelay / count);
                }
            }
            attempts++;
        }
    }

    Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * mapRadius;
        Vector3 randomPosition = mapCenter + new Vector3(randomCircle.x, 0, randomCircle.y);
        return AdjustToGround(randomPosition);
    }

    Vector3 AdjustToGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * maxGroundCheckDistance;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, maxGroundCheckDistance * 2, groundLayerMask))
        {
            return hit.point + Vector3.up * turretHeightOffset;
        }

        return new Vector3(position.x, mapCenter.y + turretHeightOffset, position.z);
    }

    bool IsValidSpawnPosition(Vector3 position)
    {
        if (Vector3.Distance(position, mapCenter) > mapRadius) return false;

        if (playerTransform != null && Vector3.Distance(position, playerTransform.position) < minDistanceFromPlayer)
            return false;

        foreach (GameObject turret in spawnedTurrets)
        {
            if (turret != null && Vector3.Distance(position, turret.transform.position) < minDistanceBetweenTurrets)
                return false;
        }

        if (noSpawnZones != null)
        {
            foreach (Transform zone in noSpawnZones)
            {
                if (zone != null && Vector3.Distance(position, zone.position) < noSpawnRadius)
                    return false;
            }
        }

        return true;
    }

    GameObject SpawnTurret(Vector3 position)
    {
        try
        {
            GameObject turret = Instantiate(turretPrefab, position, Quaternion.identity, transform);
            turret.name = $"Turret_{spawnedTurrets.Count + 1}";

            if (!turret.CompareTag("Turret")) turret.tag = "Turret";

            spawnedTurrets.Add(turret);
            return turret;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to spawn turret: {e.Message}");
            return null;
        }
    }

    void CleanupDestroyedTurrets()
    {
        spawnedTurrets.RemoveAll(turret => turret == null);
    }

    void OnDestroy()
    {
        if (dynamicSpawningCoroutine != null)
        {
            StopCoroutine(dynamicSpawningCoroutine);
        }
    }

    public int GetActiveTurretCount()
    {
        CleanupDestroyedTurrets();
        return spawnedTurrets.Count;
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Vector3 center = Application.isPlaying ? mapCenter : transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, mapRadius);

        if (noSpawnZones != null)
        {
            Gizmos.color = Color.red;
            foreach (Transform zone in noSpawnZones)
            {
                if (zone != null) Gizmos.DrawWireSphere(zone.position, noSpawnRadius);
            }
        }

        if (playerTransform != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(playerTransform.position, minDistanceFromPlayer);
        }
    }
}

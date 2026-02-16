using UnityEngine;
using System.Collections.Generic;

public class ChunkTerrainManager : MonoBehaviour
{
    [Header("Chunk Settings")]
    [SerializeField] private GameObject[] terrainChunkPrefabs;
    [SerializeField] private int chunkSize = 50;
    [SerializeField] private int renderDistance = 2;

    private Transform player;
    private Vector2Int currentPlayerChunk;
    private Dictionary<Vector2Int, GameObject> activeChunks = new Dictionary<Vector2Int, GameObject>();

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        UpdateChunks();
    }

    void Update()
    {
        if (player == null) return;

        Vector2Int playerChunk = GetChunkCoordinate(player.position);
        if (playerChunk != currentPlayerChunk)
        {
            currentPlayerChunk = playerChunk;
            UpdateChunks();
        }
    }

    Vector2Int GetChunkCoordinate(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / chunkSize),
            Mathf.FloorToInt(worldPosition.z / chunkSize)
        );
    }

    void UpdateChunks()
    {
        HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();

        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                Vector2Int chunkCoord = currentPlayerChunk + new Vector2Int(x, z);
                neededChunks.Add(chunkCoord);

                if (!activeChunks.ContainsKey(chunkCoord))
                {
                    CreateChunk(chunkCoord);
                }
            }
        }

        List<Vector2Int> chunksToRemove = new List<Vector2Int>();
        foreach (var chunk in activeChunks)
        {
            if (!neededChunks.Contains(chunk.Key))
            {
                chunksToRemove.Add(chunk.Key);
            }
        }

        foreach (var chunkCoord in chunksToRemove)
        {
            if (activeChunks.ContainsKey(chunkCoord))
            {
                Destroy(activeChunks[chunkCoord]);
                activeChunks.Remove(chunkCoord);
            }
        }
    }

    void CreateChunk(Vector2Int chunkCoord)
    {
        Vector3 worldPosition = new Vector3(chunkCoord.x * chunkSize, 0, chunkCoord.y * chunkSize);

        GameObject chunkPrefab = terrainChunkPrefabs[Random.Range(0, terrainChunkPrefabs.Length)];
        GameObject chunk = Instantiate(chunkPrefab, worldPosition, Quaternion.identity, transform);
        chunk.name = $"Chunk_{chunkCoord.x}_{chunkCoord.y}";

        activeChunks[chunkCoord] = chunk;
    }
}

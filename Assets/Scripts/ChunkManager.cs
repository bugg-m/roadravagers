using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public Transform player;
    public int chunkSize = 50;
    public int viewDistanceInChunks = 2; // creates (2*view+1)^2 chunks
    public Material chunkMaterial;

    Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();

    void Start()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        UpdateChunks();
    }

    void Update()
    {
        UpdateChunks();
    }

    void UpdateChunks()
    {
        if (player == null) return;
        Vector2Int playerChunk = new Vector2Int(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize)
        );

        // Keep set of needed coordinates
        HashSet<Vector2Int> needed = new HashSet<Vector2Int>();
        for (int x = -viewDistanceInChunks; x <= viewDistanceInChunks; x++)
        {
            for (int z = -viewDistanceInChunks; z <= viewDistanceInChunks; z++)
            {
                needed.Add(new Vector2Int(playerChunk.x + x, playerChunk.y + z));
            }
        }

        // Create missing
        foreach (var coord in needed)
        {
            if (!chunks.ContainsKey(coord))
            {
                CreateChunk(coord);
            }
        }

        // Remove far chunks
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var kv in chunks)
        {
            if (!needed.Contains(kv.Key))
            {
                Destroy(kv.Value);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var r in toRemove) chunks.Remove(r);
    }

    void CreateChunk(Vector2Int coord)
    {
        GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Plane);
        chunk.name = $"Chunk_{coord.x}_{coord.y}";
        chunk.transform.localScale = Vector3.one * (chunkSize / 10f); // plane is 10x10 by default
        float px = coord.x * chunkSize + chunkSize / 2f;
        float pz = coord.y * chunkSize + chunkSize / 2f;
        chunk.transform.position = new Vector3(px, 0f, pz);
        if (chunkMaterial != null) chunk.GetComponent<MeshRenderer>().material = chunkMaterial;
        // optionally disable collider
        Destroy(chunk.GetComponent<Collider>());
        chunks.Add(coord, chunk);
    }
}

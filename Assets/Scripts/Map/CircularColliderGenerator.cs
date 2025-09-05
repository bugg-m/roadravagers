using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CircularColliderGenerator : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float radius = 48f;
    [SerializeField] private int gridResolution = 33;
    [SerializeField] private float heightMultiplier = 12f;
    [SerializeField] private int updateIntervalFrames = 6;
    [SerializeField] private float moveThreshold = 1.0f;

    Mesh mesh;
    MeshCollider meshCollider;
    Vector3 lastPosition;
    int frameCounter = 0;

    void Awake()
    {
        gameObject.name = "CircularTerrainCollider";
        gameObject.transform.localScale = Vector3.one;

        mesh = new Mesh();
        mesh.name = "circular_collider_mesh";
        var mf = GetComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        mr.enabled = false;

        meshCollider = gameObject.GetComponent<MeshCollider>();
        if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.convex = false;
        meshCollider.sharedMesh = null;

        if (target == null)
        {
            var pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo) target = pgo.transform;
        }
    }

    IEnumerator Start()
    {
        if (Noise.octaveOffsets == null)
        {
            var streamer = FindFirstObjectByType<TerrainStreamer>();
            if (streamer != null)
            {
                Noise.Initialize(streamer.Seed, streamer.Octaves, streamer.Persistence, streamer.Lacunarity, streamer.NoiseScale, streamer.GlobalOffset);
            }
            else
            {
                float timeout = 5f;
                float elapsed = 0f;
                while (Noise.octaveOffsets == null && elapsed < timeout)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }
            }
        }

        lastPosition = target != null ? target.position : Vector3.zero;
        BuildAndAssign();
    }

    void Update()
    {
        if (target == null) return;
        frameCounter++;
        if (frameCounter < updateIntervalFrames) return;
        frameCounter = 0;

        if (Vector3.SqrMagnitude((Vector3)target.position - lastPosition) > (moveThreshold * moveThreshold))
        {
            BuildAndAssign();
            lastPosition = target.position;
        }
    }

    public void BuildAndAssign()
    {
        if (target == null) return;

        int vs = Mathf.Max(3, gridResolution);
        float step = radius * 2f / (vs - 1);
        Vector3 origin = target.position - new Vector3(radius, 0f, radius);

        List<Vector3> verts = new List<Vector3>(vs * vs);
        List<Vector2> uvs = new List<Vector2>(vs * vs);

        bool[] inside = new bool[vs * vs];
        for (int z = 0; z < vs; z++)
        {
            for (int x = 0; x < vs; x++)
            {
                int i = z * vs + x;
                float worldX = origin.x + x * step;
                float worldZ = origin.z + z * step;
                float dx = worldX - target.position.x;
                float dz = worldZ - target.position.z;
                float dist2 = dx * dx + dz * dz;
                inside[i] = dist2 <= radius * radius;

                float n = Noise.GetHeightNormalized(worldX, worldZ);
                float y = n * heightMultiplier;
                verts.Add(new Vector3(x * step - radius, y, z * step - radius));
                uvs.Add(new Vector2((float)x / (vs - 1), (float)z / (vs - 1)));
            }
        }

        List<int> tris = new List<int>((vs - 1) * (vs - 1) * 6);
        for (int z = 0; z < vs - 1; z++)
        {
            for (int x = 0; x < vs - 1; x++)
            {
                int a = z * vs + x;
                int b = a + 1;
                int c = (z + 1) * vs + x;
                int d = c + 1;

                if (inside[a] || inside[b] || inside[c] || inside[d])
                {
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
        }

        mesh.Clear();
        mesh.indexFormat = (verts.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        transform.position = new Vector3(target.position.x, 0f, target.position.z);

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = false;
        meshCollider.isTrigger = false;
        Physics.SyncTransforms();
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target.position, radius);
    }
}

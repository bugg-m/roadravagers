using UnityEngine;

/// <summary>
/// Small helper: keeps a Rigidbody vehicle above procedural terrain using Noise sampler.
/// Works even if collider not yet perfectly cooked. Gentle correction (not teleport).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleSurfaceSnapper : MonoBehaviour
{
    [SerializeField] private float verticalOffset = 0.7f;
    [SerializeField] private float snapSpeed = 10f;
    [SerializeField] private bool enableSnap = true;


    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (!enableSnap) return;
        Vector3 pos = rb.position;
        float ground = Noise.GetHeightAtWorldPos(pos.x, pos.z, FindFirstObjectByType<TerrainStreamer>()?.HeightMultiplier ?? 12f);
        float desiredY = ground + verticalOffset;

        if (pos.y < desiredY)
        {
            Vector3 newPos = Vector3.Lerp(pos, new Vector3(pos.x, desiredY, pos.z), Mathf.Clamp01(snapSpeed * Time.fixedDeltaTime));
            rb.MovePosition(newPos);

            Vector3 v = rb.linearVelocity;
            if (v.y < 0f) { v.y = 0f; rb.linearVelocity = v; }
        }
    }
}

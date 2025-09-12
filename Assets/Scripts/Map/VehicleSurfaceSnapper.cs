using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VehicleSurfaceSnapper : MonoBehaviour
{
    [SerializeField] bool enableSnap = false;
    [SerializeField] float penetrationThreshold = 0.08f;
    [SerializeField] float maxCorrectionPerSecond = 3f;
    [SerializeField] float sampleYOffset = 0.5f;

    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        if (!enableSnap || rb == null) return;

        Vector3 pos = rb.position;

        Vector3 rayOrigin = pos + Vector3.up * 5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 20f))
        {
            float groundY = hit.point.y;
            float penetration = groundY - pos.y;
            if (penetration > penetrationThreshold)
            {
                float desiredY = groundY + sampleYOffset;
                float maxDelta = maxCorrectionPerSecond * Time.fixedDeltaTime;
                float newY = Mathf.MoveTowards(pos.y, desiredY, maxDelta);
                Vector3 newPos = new Vector3(pos.x, newY, pos.z);
                rb.MovePosition(newPos);
                if (rb.linearVelocity.y < -0.1f)
                {
                    var v = rb.linearVelocity;
                    v.y = Mathf.Lerp(v.y, 0f, 0.3f);
                    rb.linearVelocity = v;
                }
            }
        }

    }
}

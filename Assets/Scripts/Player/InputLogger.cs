using UnityEngine;

/// <summary>
/// Aggressive input logger — set interval small to sample frequently.
/// </summary>
public class InputLogger : MonoBehaviour
{
    [SerializeField] private float interval = 0.25f;
    float t = 0f;

    void Update()
    {
        t += Time.deltaTime;
        if (t < interval) return;
        t = 0f;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool fire = Input.GetButton("Fire1");
        bool brake = Input.GetKey(KeyCode.Space);

        Debug.Log($"[InputLogger] H={h:0.000} V={v:0.000} Fire={fire} Brake={brake}");
    }
}

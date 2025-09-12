using System;
using UnityEngine;

/// <summary>
/// Debug helper: monitors player/car state and prints detailed diagnostics when control is lost.
/// Optional auto-recover tries to re-enable CarController if it was accidentally disabled.
/// Attach to an always-active object (GameManager) and assign playerRoot.
/// </summary>
public class ControlDebugger : MonoBehaviour
{
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private bool autoRecover = false;
    [SerializeField] private bool verbose = true;

    private float timer;
    private CarController car;
    private HealthSystem health;

    private float lastRecoverTime = -10f;
    private const float recoverCooldown = 2f;

    void Start()
    {
        if (playerRoot == null)
            playerRoot = GameObject.FindGameObjectWithTag("Player");

        if (playerRoot != null)
        {
            car = playerRoot.GetComponent<CarController>();
            health = playerRoot.GetComponent<HealthSystem>();
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < checkInterval) return;
        timer = 0f;

        if (playerRoot == null)
        {
            Debug.LogError("[ControlDebugger] playerRoot not assigned and no GameObject with tag Player found.");
            return;
        }

        // 1) Active / enabled checks
        if (!playerRoot.activeInHierarchy)
        {
            Debug.LogError("[ControlDebugger] Player GameObject is not active in hierarchy! This will disable all components.");
            return;
        }

        if (car == null)
        {
            Debug.LogError("[ControlDebugger] CarController component missing on playerRoot.");
            return;
        }

        if (!car.enabled)
        {
            Debug.LogWarning("[ControlDebugger] CarController component is disabled.");
            if (autoRecover && Time.time > lastRecoverTime + recoverCooldown && (health == null || !health.IsDestroyed()))
            {
                Debug.LogWarning("[ControlDebugger] AutoRecover: re-enabling CarController.");
                car.enabled = true;
                car.Invoke("EnableMovement", 0f); // call enable movement
                lastRecoverTime = Time.time;
            }
            return;
        }

        // 2) canMove flag check
        bool canMove = true;
        try { canMove = typeof(CarController).GetMethod("IsMoving") != null ? true : canMove; } catch { }
        // better: reflectively read private field? fallback to safe check via API
        // We'll call the public methods that exist:
        // If CarController implements GetCurrentSpeed, we already call it in the car; we check speed.
        float spd = car.GetCurrentSpeed();
        if (!car.IsMoving() && (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f))
        {
            Debug.LogWarning($"[ControlDebugger] Input present (H={Input.GetAxis("Horizontal"):0.00}, V={Input.GetAxis("Vertical"):0.00}) but car not responding. Speed={spd:0.00} km/h");
        }

        // 3) Rigidbody sanity checks
        var rb = playerRoot.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (rb.isKinematic)
                Debug.LogWarning("[ControlDebugger] Player Rigidbody is kinematic (will not respond to physics).");
            if (rb.linearVelocity.sqrMagnitude < 0.0001f && Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f)
                Debug.LogWarning($"[ControlDebugger] Rigidbody velocity near zero while throttle input present. vel={rb.linearVelocity}.");
        }

        // 4) Time scale
        if (Mathf.Approximately(Time.timeScale, 0f))
            Debug.LogWarning("[ControlDebugger] Time.timeScale is zero. Input and physics paused.");

        // 5) Window focus
        if (!Application.isFocused)
            Debug.LogWarning("[ControlDebugger] Application not focused; input may be blocked.");

        if (verbose)
        {
            // general health
            if (health != null)
            {
                Debug.Log($"[ControlDebugger] Health: {health.GetHealth():0.0}/{health.GetMaxHealth():0.0}");
            }
        }
    }
}

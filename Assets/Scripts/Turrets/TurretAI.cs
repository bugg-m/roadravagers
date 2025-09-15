using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(RaycastWeaponSystem))]
public class TurretAI : MonoBehaviour
{
    // Pool wiring
    public TurretPoolManager PoolOwner { get; set; }
    public Vector2Int CellCoord { get; set; }

    [Header("Ranges & Timings")]
    [SerializeField] private float detectionRange = 28f;
    [SerializeField] private float firingRange = 16f;
    [SerializeField] private float riseDuration = 0.9f;
    [SerializeField] private float aimDuration = 0.45f;

    [Header("Transforms")]
    [SerializeField] private Transform gunPivot;

    [Header("Motion")]
    [SerializeField] private float rotationSpeed = 160f;
    [SerializeField] private float hiddenYOffset = -1.6f;

    [Header("Firing")]
    [SerializeField] private bool requireLOS = true;

    [Header("References")]
    [SerializeField] private RaycastWeaponSystem weaponSystem;

    // Internal state
    enum State { Hidden, Arise, Aim, Idle, Fire, Hiding, Destroyed }
    State state = State.Hidden;

    Transform player;
    Vector3 originalPosition;
    Vector3 hiddenPosition;

    float stateTimer = 0f;
    Coroutine detectionCoroutine;

    void Awake()
    {
        if (gunPivot == null) gunPivot = transform;
        if (weaponSystem == null) weaponSystem = GetComponent<RaycastWeaponSystem>();
    }

    /// <summary>
    /// Call after pool spawn
    /// </summary>
    public void OnSpawned(Transform playerTransform)
    {
        player = playerTransform;
        originalPosition = transform.position;
        hiddenPosition = originalPosition + Vector3.up * hiddenYOffset;
        transform.position = hiddenPosition;

        state = State.Hidden;
        stateTimer = 0f;

        weaponSystem.ResetCooldown();

        // Start detection
        if (detectionCoroutine != null)
        {
            StopCoroutine(detectionCoroutine);
            detectionCoroutine = null;
        }

        if (gameObject.activeInHierarchy)
        {
            detectionCoroutine = StartCoroutine(DetectionLoop());
        }
        else
        {
            StartCoroutine(StartDetectionNextFrame());
        }
    }

    IEnumerator StartDetectionNextFrame()
    {
        yield return null;
        if (gameObject.activeInHierarchy)
            detectionCoroutine = StartCoroutine(DetectionLoop());
    }

    /// <summary>
    /// Call before returning to pool
    /// </summary>
    public void OnDespawned()
    {
        player = null;
        if (detectionCoroutine != null)
        {
            StopCoroutine(detectionCoroutine);
            detectionCoroutine = null;
        }
        transform.position = hiddenPosition;
        state = State.Hidden;
    }

    /// <summary>
    /// External call when turret is destroyed
    /// </summary>
    public void MarkDestroyed()
    {
        if (state == State.Destroyed) return;
        state = State.Destroyed;

        // Optional: play destroy VFX here
        PoolOwner.ReleaseFromTurret(gameObject, CellCoord);
    }

    /// <summary>
    /// Called by external damage system
    /// </summary>
    public void ExternalDestroyed()
    {
        MarkDestroyed();
    }

    void Update()
    {
        if (state == State.Hidden || state == State.Destroyed) return;

        stateTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Arise:
                UpdateAriseState();
                break;

            case State.Aim:
                AimAtPlayerSmooth();
                if (stateTimer <= 0f) state = State.Idle;
                break;

            case State.Idle:
                UpdateIdleState();
                break;

            case State.Fire:
                UpdateFireState();
                break;

            case State.Hiding:
                UpdateHidingState();
                break;
        }
    }

    private void UpdateAriseState()
    {
        float t = Mathf.Clamp01(1f - (stateTimer / riseDuration));
        transform.position = Vector3.Lerp(hiddenPosition, originalPosition, t);
        AimAtPlayerSmooth();

        if (stateTimer <= 0f)
        {
            state = State.Aim;
            stateTimer = aimDuration;
        }
    }

    private void UpdateIdleState()
    {
        AimAtPlayerSmooth();

        if (PlayerWithin(firingRange) && (!requireLOS || weaponSystem.HasLineOfSight(gunPivot.position, player)))
        {
            state = State.Fire;
        }
        else if (!PlayerWithin(detectionRange * 1.2f))
        {
            StartHiding();
        }
    }

    private void UpdateFireState()
    {
        AimAtPlayerSmooth();

        if (PlayerWithin(firingRange) && (!requireLOS || weaponSystem.HasLineOfSight(gunPivot.position, player)))
        {
            // Fire using the new weapon system
            if (weaponSystem != null && weaponSystem.CanFire)
            {
                weaponSystem.FireAt(player);
            }
        }
        else
        {
            state = State.Idle; // Lost target or range
        }

        if (!PlayerWithin(detectionRange * 1.2f))
        {
            StartHiding();
        }
    }

    private void UpdateHidingState()
    {
        float t = Mathf.Clamp01(1f - (stateTimer / riseDuration));
        transform.position = Vector3.Lerp(originalPosition, hiddenPosition, t);

        if (stateTimer <= 0f)
        {
            state = State.Hidden;
            transform.position = hiddenPosition;
        }
    }

    private void StartHiding()
    {
        state = State.Hiding;
        stateTimer = riseDuration;
    }

    IEnumerator DetectionLoop()
    {
        var wait = new WaitForSeconds(0.18f);

        while (true)
        {
            if (player == null)
            {
                yield return wait;
                continue;
            }

            if (state == State.Hidden)
            {
                if (PlayerWithin(detectionRange))
                {
                    state = State.Arise;
                    stateTimer = riseDuration;
                }
            }
            else if (state != State.Hiding && state != State.Hidden)
            {
                if (!PlayerWithin(detectionRange * 1.2f))
                {
                    StartHiding();
                }
            }

            yield return wait;
        }
    }

    private bool PlayerWithin(float range)
    {
        if (player == null) return false;
        return Vector3.Distance(player.position, transform.position) <= range;
    }

    private void AimAtPlayerSmooth()
    {
        if (player == null || gunPivot == null) return;

        Vector3 direction = player.position - gunPivot.position;
        direction.y = 0f; // Keep horizontal only

        if (direction.sqrMagnitude < 1e-6f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        gunPivot.rotation = Quaternion.RotateTowards(gunPivot.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Firing range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, firingRange);
    }
}
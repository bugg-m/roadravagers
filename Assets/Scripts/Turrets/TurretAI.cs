using UnityEngine;
using System.Collections;

/// <summary>
/// Pooled turret AI with 6 phases:
/// Hidden -> Arise -> Aim -> Idle -> Fire -> Destroyed (or Hiding -> Hidden)
/// - OnSpawned(player) must be called right after pool.Spawn(...)
/// - OnDespawned() must be called before pool.Release(...)
/// - When MarkDestroyed() is called (e.g. HealthSystem), turret returns to pool via PoolOwner
/// </summary>
[RequireComponent(typeof(Collider))]
public class TurretAI : MonoBehaviour
{
    // pool wiring
    public TurretPoolManager PoolOwner { get; set; }
    public Vector2Int CellCoord { get; set; }

    [Header("Ranges & timings")]
    [SerializeField] private float detectionRange = 28f;
    [SerializeField] private float firingRange = 16f;
    [SerializeField] private float riseDuration = 0.9f;
    [SerializeField] private float aimDuration = 0.45f;

    [Header("Transforms")]
    [SerializeField] private Transform gunPivot;          // rotates horizontally to aim
    [SerializeField] private Transform firePoint;         // origin used by WeaponSystem

    [Header("Motion")]
    [SerializeField] private float rotationSpeed = 160f;
    [SerializeField] private float hiddenYOffset = -1.6f;

    [Header("Firing gating")]
    [SerializeField] private bool requireLOS = true;      // if true, turret checks LOS before firing

    [Header("References")]
    [SerializeField] private WeaponSystem weaponSystem;   // must be on same prefab (auto assign in Awake)

    // internal state
    enum State { Hidden, Arise, Aim, Idle, Fire, Hiding, Destroyed }
    State state = State.Hidden;

    Transform player;
    Vector3 originalPosition;
    Vector3 hiddenPosition;

    float stateTimer = 0f;
    float fireCooldown = 0f;

    Coroutine detectionCoroutine;

    void Awake()
    {
        if (gunPivot == null) gunPivot = transform;
        if (firePoint == null) firePoint = gunPivot;
        if (weaponSystem == null) weaponSystem = GetComponent<WeaponSystem>();
    }

    // Call after Spawn (poolManager.Spawn)
    public void OnSpawned(Transform playerTransform)
    {
        player = playerTransform;
        originalPosition = transform.position;
        hiddenPosition = originalPosition + Vector3.up * hiddenYOffset;
        transform.position = hiddenPosition;

        state = State.Hidden;
        stateTimer = 0f;
        fireCooldown = 0f;

        weaponSystem?.ResetCooldown();

        if (detectionCoroutine != null) StopCoroutine(detectionCoroutine);
        detectionCoroutine = StartCoroutine(DetectionLoop());
    }

    // Call before returning to pool
    public void OnDespawned()
    {
        player = null;
        if (detectionCoroutine != null) { StopCoroutine(detectionCoroutine); detectionCoroutine = null; }
        transform.position = hiddenPosition;
        state = State.Hidden;
    }

    /// <summary>
    /// External call when turret is destroyed (health 0). Will notify PoolOwner to release it.
    /// </summary>
    public void MarkDestroyed()
    {
        if (state == State.Destroyed) return;
        state = State.Destroyed;

        // optional: play destroy VFX / sound here
        PoolOwner?.ReleaseFromTurret(gameObject, CellCoord);
    }

    void Update()
    {
        if (state == State.Hidden || state == State.Destroyed) return;

        stateTimer -= Time.deltaTime;
        fireCooldown -= Time.deltaTime;

        switch (state)
        {
            case State.Arise:
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
                break;

            case State.Aim:
                AimAtPlayerSmooth();
                if (stateTimer <= 0f) state = State.Idle;
                break;

            case State.Idle:
                AimAtPlayerSmooth();
                if (PlayerWithin(firingRange) && (!requireLOS || HasLineOfSight()))
                {
                    state = State.Fire;
                    fireCooldown = 0f;
                }
                else if (!PlayerWithin(detectionRange * 1.2f))
                {
                    state = State.Hiding;
                    stateTimer = riseDuration;
                }
                break;

            case State.Fire:
                AimAtPlayerSmooth();
                if (PlayerWithin(firingRange) && (!requireLOS || HasLineOfSight()))
                {
                    if (weaponSystem != null && weaponSystem.CanFireNow())
                    {
                        weaponSystem.FireAt(player);
                    }
                }
                else
                {
                    state = State.Idle; // player moved out of firing range or LOS lost
                }

                if (!PlayerWithin(detectionRange * 1.2f))
                {
                    state = State.Hiding;
                    stateTimer = riseDuration;
                }
                break;

            case State.Hiding:
                {
                    float tt = Mathf.Clamp01(1f - (stateTimer / riseDuration));
                    transform.position = Vector3.Lerp(originalPosition, hiddenPosition, tt);
                    if (stateTimer <= 0f)
                    {
                        state = State.Hidden;
                        transform.position = hiddenPosition;
                    }
                }
                break;
        }
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
            else
            {
                if (!PlayerWithin(detectionRange * 1.2f) && state != State.Hiding && state != State.Hidden)
                {
                    state = State.Hiding;
                    stateTimer = riseDuration;
                }
            }
            yield return wait;
        }
    }

    // helper checks
    bool PlayerWithin(float r)
    {
        if (player == null) return false;
        return (player.position - transform.position).sqrMagnitude <= (r * r);
    }

    void AimAtPlayerSmooth()
    {
        if (player == null || gunPivot == null) return;
        Vector3 dir = player.position - gunPivot.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        Quaternion tgt = Quaternion.LookRotation(dir.normalized, Vector3.up);
        gunPivot.rotation = Quaternion.RotateTowards(gunPivot.rotation, tgt, rotationSpeed * Time.deltaTime);
    }

    // inside TurretAI (replace HasLineOfSight and firing code block)
    bool HasLineOfSight()
    {
        if (player == null) return false;
        Vector3 origin = (firePoint != null) ? firePoint.position : gunPivot.position;
        // use weaponSystem helper if available, otherwise do a local raycast
        if (weaponSystem != null) return weaponSystem.CanSee(origin, player);
        Vector3 dir = (player.position + Vector3.up * 0.5f) - origin;
        float dist = Mathf.Min(60f, dir.magnitude + 0.5f);
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist))
        {
            if (hit.collider != null && (hit.collider.transform == player || hit.collider.transform.IsChildOf(player)))
                return true;
            return false;
        }
        return true;
    }

    // Called by external damage system when the turret is reduced to zero HP (for example)
    public void ExternalDestroyed()
    {
        // optional explosion VFX / sound here
        MarkDestroyed();
    }
}

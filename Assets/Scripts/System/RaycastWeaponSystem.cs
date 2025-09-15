using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RaycastWeaponSystem : MonoBehaviour
{
    [Header("Fire Points")]
    [SerializeField] private Transform[] firePoints;
    [SerializeField] private bool autoFindFirePoints = true;

    [Header("Weapon Settings")]
    [SerializeField, Min(0.01f)] private float fireRate = 2f;
    [SerializeField, Min(0.1f)] private float range = 100f;
    [SerializeField] private LayerMask targetLayers = -1;
    [SerializeField] private LayerMask obstacleLayers = -1;

    [Header("VFX Settings")]
    [SerializeField] private ParticleSystem muzzleFlashPrefab;
    [SerializeField] private ParticleSystem impactEffectPrefab;
    [SerializeField] private float muzzleLifetime = 1f;
    [SerializeField] private float impactLifetime = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = false;

    private float nextFireTime;
    private VFXPool vfxPool;

    // Public properties
    public float FireRate => fireRate;
    public float Range => range;
    public bool CanFire => Time.time >= nextFireTime;

    void Awake()
    {
        InitializeFirePoints();
        vfxPool = GetComponent<VFXPool>() ?? gameObject.AddComponent<VFXPool>();
    }

    void InitializeFirePoints()
    {
        if (autoFindFirePoints && (firePoints == null || firePoints.Length == 0))
        {
            // Try to find FirePoints container
            Transform firePointsContainer = transform.Find("FirePoints");
            if (firePointsContainer != null)
            {
                firePoints = new Transform[firePointsContainer.childCount];
                for (int i = 0; i < firePointsContainer.childCount; i++)
                {
                    firePoints[i] = firePointsContainer.GetChild(i);
                }
            }
        }

        // Fallback to self if no fire points found
        if (firePoints == null || firePoints.Length == 0)
        {
            firePoints = new Transform[] { transform };
        }
    }

    /// <summary>
    /// Fire at a specific target transform
    /// </summary>
    public bool FireAt(Transform target)
    {
        if (target == null || !CanFire) return false;

        Vector3 targetPos = target.position + Vector3.up * 0.5f; // Aim slightly up
        bool hitSomething = false;

        foreach (var firePoint in firePoints)
        {
            if (firePoint == null) continue;

            Vector3 direction = (targetPos - firePoint.position).normalized;
            float distance = Vector3.Distance(firePoint.position, targetPos);
            distance = Mathf.Min(distance, range);

            if (FireRaycast(firePoint.position, direction, distance))
                hitSomething = true;

            SpawnMuzzleFlash(firePoint.position, Quaternion.LookRotation(direction));
        }

        SetCooldown();
        return hitSomething;
    }

    /// <summary>
    /// Fire in a specific direction from origin
    /// </summary>
    public bool FireDirection(Vector3 origin, Vector3 direction, float? overrideRange = null)
    {
        if (!CanFire) return false;

        float fireRange = overrideRange ?? range;
        bool hit = FireRaycast(origin, direction.normalized, fireRange);
        SpawnMuzzleFlash(origin, Quaternion.LookRotation(direction));

        SetCooldown();
        return hit;
    }

    /// <summary>
    /// Fire forward from all fire points
    /// </summary>
    public bool FireForward()
    {
        if (!CanFire) return false;

        bool hitSomething = false;
        foreach (var firePoint in firePoints)
        {
            if (firePoint == null) continue;

            if (FireRaycast(firePoint.position, firePoint.forward, range))
                hitSomething = true;

            SpawnMuzzleFlash(firePoint.position, firePoint.rotation);
        }

        SetCooldown();
        return hitSomething;
    }

    /// <summary>
    /// Check if target is visible from origin
    /// </summary>
    public bool HasLineOfSight(Vector3 origin, Transform target)
    {
        if (target == null) return false;

        Vector3 targetPos = target.position + Vector3.up * 0.5f;
        Vector3 direction = targetPos - origin;
        float distance = direction.magnitude;

        if (distance > range) return false;

        int layerMask = targetLayers | obstacleLayers;
        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, layerMask))
        {
            // Check if we hit the target or something belonging to the target
            Transform hitTransform = hit.collider.transform;
            return hitTransform == target || hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform);
        }

        return true; // No obstacles in the way
    }

    private bool FireRaycast(Vector3 origin, Vector3 direction, float distance)
    {
        int layerMask = targetLayers | obstacleLayers;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, layerMask))
        {
            SpawnImpactEffect(hit.point, Quaternion.LookRotation(hit.normal));

            // Check if we hit a valid target
            if (IsValidTarget(hit.collider))
            {
                // Notify target of hit
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                damageable?.OnHit(hit.point, direction);

                if (showDebugRays)
                    Debug.DrawRay(origin, direction * hit.distance, Color.red, 0.5f);

                return true;
            }

            if (showDebugRays)
                Debug.DrawRay(origin, direction * hit.distance, Color.yellow, 0.5f);
        }
        else if (showDebugRays)
        {
            Debug.DrawRay(origin, direction * distance, Color.white, 0.1f);
        }

        return false;
    }

    private bool IsValidTarget(Collider hitCollider)
    {
        return ((1 << hitCollider.gameObject.layer) & targetLayers) != 0;
    }

    private void SpawnMuzzleFlash(Vector3 position, Quaternion rotation)
    {
        if (muzzleFlashPrefab != null && vfxPool != null)
        {
            vfxPool.PlayEffect(muzzleFlashPrefab, position, rotation, muzzleLifetime);
        }
    }

    private void SpawnImpactEffect(Vector3 position, Quaternion rotation)
    {
        if (impactEffectPrefab != null && vfxPool != null)
        {
            vfxPool.PlayEffect(impactEffectPrefab, position, rotation, impactLifetime);
        }
    }

    private void SetCooldown()
    {
        nextFireTime = Time.time + (1f / Mathf.Max(0.001f, fireRate));
    }

    /// <summary>
    /// Reset weapon cooldown
    /// </summary>
    public void ResetCooldown()
    {
        nextFireTime = 0f;
    }

    void OnDrawGizmosSelected()
    {
        if (firePoints == null) return;

        Gizmos.color = Color.cyan;
        foreach (var firePoint in firePoints)
        {
            if (firePoint == null) continue;

            Gizmos.DrawWireSphere(firePoint.position, 0.1f);
            Gizmos.DrawRay(firePoint.position, firePoint.forward * range);
        }
    }
}
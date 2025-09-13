using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponSystem : MonoBehaviour
{
    [Header("Fire points")]
    [SerializeField] private Transform[] firePoints;

    [Header("Raycast settings")]
    [SerializeField, Min(0.01f)] private float rayRange = 60f;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Rate & Pools")]
    [SerializeField, Min(0.01f)] private float fireRate = 2f;

    [Header("Pool lookup (runtime)")]
    [SerializeField] private string poolManagerName = "VfxPoolManager";
    [SerializeField] private string muzzlePoolObjectName = "MuzzleVfxPool";
    [SerializeField] private string impactPoolObjectName = "ImpactVfxPool";
    [SerializeField] private string damagePoolObjectName = "DamageVfxPool";

    [Header("VFX lifetimes")]
    [SerializeField] private float defaultMuzzleLifetime = 1.2f;
    [SerializeField] private float defaultImpactLifetime = 1.2f;
    [SerializeField] private float defaultDamageLifetime = 1.2f;

    float nextFireTime = 0f;

    private ParticleVfxPool muzzlePool;
    private ParticleVfxPool impactPool;
    private ParticleVfxPool damagePool;

    public float RayRange => rayRange;
    public LayerMask TargetMask => targetMask;
    public LayerMask ObstacleMask => obstacleMask;
    public float FireRate => fireRate;

    void Awake()
    {
        if ((firePoints == null || firePoints.Length == 0) && transform.childCount > 0)
        {
            var fp = transform.Find("FirePoints");
            if (fp != null)
            {
                int c = fp.childCount;
                firePoints = new Transform[c];
                for (int i = 0; i < c; i++) firePoints[i] = fp.GetChild(i);
            }
        }
    }

    void Start()
    {
        ResolvePools();
    }

    void ResolvePools()
    {
        var manager = GameObject.Find(poolManagerName);
        if (manager != null)
        {
            var cMuzzle = manager.transform.Find(muzzlePoolObjectName);
            var cImpact = manager.transform.Find(impactPoolObjectName);
            var cDamage = manager.transform.Find(damagePoolObjectName);

            if (cMuzzle != null) muzzlePool = cMuzzle.GetComponent<ParticleVfxPool>();
            if (cImpact != null) impactPool = cImpact.GetComponent<ParticleVfxPool>();
            if (cDamage != null) damagePool = cDamage.GetComponent<ParticleVfxPool>();
        }

        if (muzzlePool == null || impactPool == null || damagePool == null)
        {
            var all = FindObjectsByType<ParticleVfxPool>(FindObjectsSortMode.None);
            foreach (var p in all)
            {
                string n = p.gameObject.name.ToLowerInvariant();
                if (muzzlePool == null && n.Contains("muzzle")) muzzlePool = p;
                else if (impactPool == null && (n.Contains("impact") || n.Contains("hit"))) impactPool = p;
                else if (damagePool == null && (n.Contains("damage") || n.Contains("hit"))) damagePool = p;
            }
        }

        if (muzzlePool == null || impactPool == null || damagePool == null)
        {
            var all = FindObjectsByType<ParticleVfxPool>(FindObjectsSortMode.None);
            if (all.Length > 0)
            {
                if (muzzlePool == null) muzzlePool = all[0];
                if (impactPool == null && all.Length > 1) impactPool = all[Mathf.Min(1, all.Length - 1)];
                if (damagePool == null && all.Length > 2) damagePool = all[Mathf.Min(2, all.Length - 1)];
            }
        }
    }

    int CombinedMask() => targetMask.value | obstacleMask.value;

    public bool CanSee(Vector3 origin, Transform target)
    {
        if (target == null) return false;
        Vector3 dir = (target.position + Vector3.up * 0.5f) - origin;
        float dist = dir.magnitude;
        if (dist > rayRange) return false;

        int mask = CombinedMask();
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, mask))
        {
            if (hit.collider != null && (hit.collider.transform == target || hit.collider.transform.IsChildOf(target)))
                return true;
            return false;
        }
        return true;
    }

    public bool FireAt(Transform target)
    {
        if (target == null) return false;
        if (Time.time < nextFireTime) return false;
        nextFireTime = Time.time + (1f / Mathf.Max(0.0001f, fireRate));
        if (firePoints == null || firePoints.Length == 0) return false;

        foreach (var fp in firePoints)
        {
            if (fp == null) continue;
            Vector3 dir = (target.position + Vector3.up * 0.5f) - fp.position;
            float dist = Mathf.Min(rayRange, dir.magnitude);
            FireRayFromPoint(fp.position, dir.normalized, dist, true);
            SpawnMuzzle(fp.position, Quaternion.LookRotation(dir.normalized));
        }
        return true;
    }

    public bool FireForward(Vector3 origin, Vector3 direction, float distance = -1f)
    {
        if (Time.time < nextFireTime) return false;
        nextFireTime = Time.time + (1f / Mathf.Max(0.0001f, fireRate));

        Vector3 dir = direction.normalized;
        float dist = (distance > 0f) ? Mathf.Min(distance, rayRange) : rayRange;

        SpawnMuzzle(origin, Quaternion.LookRotation(dir));
        FireRayFromPoint(origin, dir, dist, false);
        return true;
    }

    private void FireRayFromPoint(Vector3 origin, Vector3 direction, float dist, bool inferTargetAsHit)
    {
        int mask = CombinedMask();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, dist, mask))
        {
            SpawnImpact(hit.point, Quaternion.LookRotation(hit.normal));

            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                // NOTE: this WeaponSystem no longer automatically applies damage by default
                // to avoid interfering with your testing. If you want it to apply damage here,
                // enable the lines below or call dmg.TakeDamage(damageAmount).
                // dmg.TakeDamage(damageAmount);

                // spawn damage VFX
                SpawnDamage(hit.point, Quaternion.identity);
            }
        }
    }

    #region VFX spawn helpers

    void SpawnMuzzle(Vector3 pos, Quaternion rot)
    {
        if (muzzlePool != null)
        {
            if (muzzlePool.gameObject.activeInHierarchy)
                muzzlePool.Spawn(pos, rot, defaultMuzzleLifetime);
            else
                TryDirectFallback(pos, rot, defaultMuzzleLifetime);
        }
        else
        {
            TryDirectFallback(pos, rot, defaultMuzzleLifetime);
        }
    }

    void SpawnImpact(Vector3 pos, Quaternion rot)
    {
        if (impactPool != null)
        {
            if (impactPool.gameObject.activeInHierarchy)
                impactPool.Spawn(pos, rot, defaultImpactLifetime);
            else
                TryDirectFallback(pos, rot, defaultImpactLifetime);
        }
        else
        {
            TryDirectFallback(pos, rot, defaultImpactLifetime);
        }
    }

    void SpawnDamage(Vector3 pos, Quaternion rot)
    {
        if (damagePool != null)
        {
            if (damagePool.gameObject.activeInHierarchy)
                damagePool.Spawn(pos, rot, defaultDamageLifetime);
            else
                TryDirectFallback(pos, rot, defaultDamageLifetime);
        }
        else
        {
            TryDirectFallback(pos, rot, defaultDamageLifetime);
        }
    }

    public bool CanFireNow() => Time.time >= nextFireTime;
    public void ResetCooldown() => nextFireTime = 0f;
    void TryDirectFallback(Vector3 pos, Quaternion rot, float life)
    {

    }

    #endregion
}

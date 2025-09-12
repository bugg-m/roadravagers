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

    [Header("Rate & VFX")]
    [SerializeField, Min(0.01f)] private float fireRate = 2f;
    [SerializeField] private GameObject muzzleVfxPrefab;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private GameObject damageVfxPrefab;
    [SerializeField] private float impactVfxLifetime = 1.2f;

    [Header("Damage")]
    [SerializeField] private float damagePerHit = 10f;
    [SerializeField, Range(0.01f, 0.5f)] private float minDamageIntervalPerTarget = 0.06f;

    float nextFireTime = 0f;

    static readonly Dictionary<int, float> s_lastDamagedTime = new Dictionary<int, float>();

    public float RayRange => rayRange;
    public LayerMask TargetMask => targetMask;
    public LayerMask ObstacleMask => obstacleMask;
    public float FireRate => fireRate;

    void Awake()
    {
        if (firePoints == null || firePoints.Length == 0)
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
            FireRayFromPoint(fp.position, dir.normalized, dist);
        }
        return true;
    }

    public bool FireForward(Vector3 origin, Vector3 direction, float distance = -1f)
    {
        if (Time.time < nextFireTime) return false;
        nextFireTime = Time.time + (1f / Mathf.Max(0.0001f, fireRate));

        Vector3 dir = direction.normalized;
        float dist = (distance > 0f) ? Mathf.Min(distance, rayRange) : rayRange;

        if (muzzleVfxPrefab != null)
        {
            var mv = Instantiate(muzzleVfxPrefab, origin, Quaternion.LookRotation(dir));
            Destroy(mv, 2f);
        }

        FireRayFromPoint(origin, dir, dist);
        return true;
    }

    private void FireRayFromPoint(Vector3 origin, Vector3 direction, float dist)
    {
        int mask = CombinedMask();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, dist, mask))
        {
            if (impactVfxPrefab != null)
            {
                var iv = Instantiate(impactVfxPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(iv, impactVfxLifetime);
            }

            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                int id = hit.collider.gameObject.GetInstanceID();
                float last = 0f;
                s_lastDamagedTime.TryGetValue(id, out last);
                if (Time.time - last >= minDamageIntervalPerTarget)
                {
                    s_lastDamagedTime[id] = Time.time;
                    dmg.TakeDamage(damagePerHit);
                    if (damageVfxPrefab != null)
                    {
                        var dv = Instantiate(damageVfxPrefab, hit.point, Quaternion.identity);
                        Destroy(dv, impactVfxLifetime);
                    }
                }
            }
        }
    }

    public bool CanFireNow() => Time.time >= nextFireTime;
    public void ResetCooldown() => nextFireTime = 0f;
}

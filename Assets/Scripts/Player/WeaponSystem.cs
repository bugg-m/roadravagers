using UnityEngine;

public class WeaponSystem : MonoBehaviour, IWeapon
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform[] firePoints;
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private float bulletSpeed = 50f;
    [SerializeField] private string ownerTag = "Player";

    [Header("Ammo System")]
    [SerializeField] private bool useAmmo = false;
    [SerializeField] private int maxAmmo = 100;
    [SerializeField] private int currentAmmo;

    [Header("Effects")]
    [SerializeField] private ParticleSystem[] muzzleFlashes;

    private float nextFireTime;
    private bool canFire = true;

    void Start()
    {
        Initialize();
        SubscribeToEvents();
    }

    void Initialize()
    {
        if (useAmmo)
        {
            currentAmmo = maxAmmo;
            GameEvents.OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        }

        SetupFirePoints();
    }

    void SubscribeToEvents()
    {
        GameEvents.OnPlayerDied += DisableWeapon;
        GameEvents.OnGamePaused += DisableWeapon;
        GameEvents.OnGameStarted += EnableWeapon;
    }

    void SetupFirePoints()
    {
        if (firePoints == null || firePoints.Length == 0)
        {
            Transform firePointParent = transform.Find("FirePoints");
            if (firePointParent != null)
            {
                firePoints = new Transform[firePointParent.childCount];
                for (int i = 0; i < firePointParent.childCount; i++)
                {
                    firePoints[i] = firePointParent.GetChild(i);
                }
            }
        }
    }

    public bool CanFire()
    {
        return Time.time >= nextFireTime && (!useAmmo || currentAmmo > 0) && canFire;
    }

    public void Fire()
    {
        if (!CanFire()) return;

        if (useAmmo)
        {
            currentAmmo--;
            GameEvents.OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        }

        foreach (Transform firePoint in firePoints)
        {
            if (firePoint != null)
            {
                FireBullet(firePoint);
            }
        }

        PlayEffects();
        GameEvents.OnWeaponFired?.Invoke();
        nextFireTime = Time.time + fireRate;
    }

    void FireBullet(Transform firePoint)
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetOwner(ownerTag);
        }

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = firePoint.forward * bulletSpeed;
        }
    }

    void PlayEffects()
    {
        if (muzzleFlashes != null)
        {
            foreach (var flash in muzzleFlashes)
            {
                if (flash != null) flash.Play();
            }
        }
    }

    public void Reload()
    {
        if (!useAmmo) return;

        currentAmmo = maxAmmo;
        GameEvents.OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
        GameEvents.OnWeaponReloaded?.Invoke();
    }

    public void AddAmmo(int amount)
    {
        if (!useAmmo) return;

        currentAmmo = Mathf.Clamp(currentAmmo + amount, 0, maxAmmo);
        GameEvents.OnAmmoChanged?.Invoke(currentAmmo, maxAmmo);
    }

    void EnableWeapon() => canFire = true;
    void DisableWeapon() => canFire = false;

    void OnDestroy()
    {
        GameEvents.OnPlayerDied -= DisableWeapon;
        GameEvents.OnGamePaused -= DisableWeapon;
        GameEvents.OnGameStarted -= EnableWeapon;
    }
}

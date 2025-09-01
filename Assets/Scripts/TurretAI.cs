using System.Collections;
using UnityEngine;

public class TurretAI : MonoBehaviour, IDamageable
{
    [Header("Detection & Combat")]
    [SerializeField] private float fireRange = 15f;
    [SerializeField] private float fireRate = 0.5f;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float maxHealth = 100f;

    [Header("Components")]
    [SerializeField] private Transform rotatingPart;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("Animation")]
    [SerializeField] private float riseSpeed = 2f;
    [SerializeField] private float hiddenYOffset = -2f;

    [Header("Prediction")]
    [SerializeField] private bool usePredictiveAiming = true;
    [SerializeField] private float predictionMultiplier = 1f;

    public enum TurretState { Hidden, Rising, Active, Hiding, Destroyed }

    private TurretState currentState = TurretState.Hidden;
    private Transform carTarget;
    private Rigidbody carRigidbody;
    private Vector3 originalPosition;
    private Vector3 hiddenPosition;
    private float nextFireTime;
    private float currentHealth;
    private bool canFire = false;
    private Coroutine detectionCoroutine;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        originalPosition = transform.position;
        hiddenPosition = originalPosition + Vector3.up * hiddenYOffset;
        transform.position = hiddenPosition;

        currentHealth = maxHealth;
        SetupComponents();

        detectionCoroutine = StartCoroutine(DetectionLoop());
    }

    void SetupComponents()
    {
        if (firePoint == null)
        {
            firePoint = FindOrCreateFirePoint();
        }

        if (rotatingPart == null && transform.childCount > 0)
        {
            rotatingPart = transform.GetChild(0);
        }
    }

    Transform FindOrCreateFirePoint()
    {
        Transform found = transform.Find("FirePoint");
        if (found != null) return found;

        GameObject firePointObj = new GameObject("FirePoint");
        Transform parent = rotatingPart != null ? rotatingPart : transform;
        firePointObj.transform.SetParent(parent);
        firePointObj.transform.localPosition = Vector3.forward * 2f;

        return firePointObj.transform;
    }

    void Update()
    {
        switch (currentState)
        {
            case TurretState.Rising:
                HandleRising();
                break;
            case TurretState.Active:
                HandleActive();
                break;
            case TurretState.Hiding:
                HandleHiding();
                break;
        }
    }

    IEnumerator DetectionLoop()
    {
        while (currentState != TurretState.Destroyed)
        {
            if (currentState == TurretState.Hidden)
            {
                DetectCar();
            }
            else if (currentState == TurretState.Active)
            {
                CheckCarInRange();
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    void DetectCar()
    {
        GameObject car = GameObject.FindGameObjectWithTag("Player");
        if (car != null && Vector3.Distance(transform.position, car.transform.position) <= fireRange)
        {
            carTarget = car.transform;
            carRigidbody = carTarget.GetComponent<Rigidbody>();
            ActivateTurret();
        }
    }

    void CheckCarInRange()
    {
        if (carTarget == null || Vector3.Distance(transform.position, carTarget.position) > fireRange)
        {
            DeactivateTurret();
        }
    }

    void ActivateTurret()
    {
        if (currentState == TurretState.Hidden)
        {
            currentState = TurretState.Rising;
            canFire = false;
        }
    }

    void HandleRising()
    {
        transform.position = Vector3.MoveTowards(transform.position, originalPosition, riseSpeed * Time.deltaTime);

        if (carTarget != null) AimAtTarget();

        if (Vector3.Distance(transform.position, originalPosition) < 0.1f)
        {
            transform.position = originalPosition;
            currentState = TurretState.Active;
            canFire = true;
        }
    }

    void HandleActive()
    {
        if (carTarget == null)
        {
            DeactivateTurret();
            return;
        }

        AimAtTarget();

        if (canFire && Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + fireRate;
        }
    }

    void AimAtTarget()
    {
        if (carTarget == null || rotatingPart == null) return;

        Vector3 targetPosition = carTarget.position;

        if (usePredictiveAiming && carRigidbody != null)
        {
            float timeToTarget = Vector3.Distance(transform.position, carTarget.position) / bulletSpeed;
            targetPosition += carRigidbody.linearVelocity * timeToTarget * predictionMultiplier;
        }

        Vector3 direction = (targetPosition - rotatingPart.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rotatingPart.rotation = Quaternion.RotateTowards(rotatingPart.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void Fire()
    {
        if (firePoint == null || bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetOwner("Turret");
        }

        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = firePoint.forward * bulletSpeed;
        }

        if (muzzleFlash != null) muzzleFlash.Play();

        Destroy(bullet, 5f);
    }

    void HandleHiding()
    {
        transform.position = Vector3.MoveTowards(transform.position, hiddenPosition, riseSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, hiddenPosition) < 0.1f)
        {
            transform.position = hiddenPosition;
            currentState = TurretState.Hidden;
            canFire = false;
            carTarget = null;
            carRigidbody = null;
        }
    }

    void DeactivateTurret()
    {
        if (currentState == TurretState.Active || currentState == TurretState.Rising)
        {
            currentState = TurretState.Hiding;
            canFire = false;
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentState == TurretState.Destroyed) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);

        if (currentHealth <= 0)
        {
            DestroyTurret();
        }
    }

    public float GetHealth() => currentHealth;
    public bool IsDestroyed() => currentState == TurretState.Destroyed;

    void DestroyTurret()
    {
        currentState = TurretState.Destroyed;
        canFire = false;

        if (detectionCoroutine != null)
        {
            StopCoroutine(detectionCoroutine);
        }

        enabled = false;
        Destroy(gameObject, 2f);
    }

    void OnDestroy()
    {
        if (detectionCoroutine != null)
        {
            StopCoroutine(detectionCoroutine);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fireRange);

        if (firePoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(firePoint.position, 0.2f);
        }

        if (carTarget != null && (currentState == TurretState.Active || currentState == TurretState.Rising))
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, carTarget.position);
        }
    }
}

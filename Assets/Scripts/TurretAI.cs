using System.Collections;
using UnityEngine;

public class TurretAI : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float attackRange = 12f;
    [SerializeField] private LayerMask carLayerMask = 1 << 8; // Assumes car is on layer 8

    [Header("Turret Components")]
    [SerializeField] private Transform rotatingPart; // The rotating part of the turret
    [SerializeField] private Transform firePoint; // Where bullets spawn from
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private AudioSource fireAudioSource;
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("Combat Settings")]
    [SerializeField] private float fireRate = 0.5f; // Time between shots
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float rotationSpeed = 90f; // Degrees per second
    [SerializeField] private float maxHealth = 100f;

    [Header("Animation Settings")]
    [SerializeField] private float riseSpeed = 2f;
    [SerializeField] private float hiddenYOffset = -2f; // How far below ground when hidden

    [Header("Prediction Settings")]
    [SerializeField] private bool usePredictiveAiming = true;
    [SerializeField] private float predictionMultiplier = 1f;

    // Private variables
    private Transform carTarget;
    private Vector3 originalPosition;
    private Vector3 hiddenPosition;
    private bool isActive = false;
    private bool isHidden = true;
    private float nextFireTime = 0f;
    private float currentHealth;
    private Rigidbody carRigidbody;

    // State management
    private enum TurretState
    {
        Hidden,
        Rising,
        Active,
        Hiding,
        Destroyed
    }
    private TurretState currentState = TurretState.Hidden;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        // Store original position
        originalPosition = transform.position;
        hiddenPosition = originalPosition + Vector3.up * hiddenYOffset;

        // Set initial position to hidden
        transform.position = hiddenPosition;

        // Initialize health
        currentHealth = maxHealth;

        // Auto-find fire point if not assigned
        if (firePoint == null)
        {
            firePoint = FindFirePoint();
        }

        // Auto-find turret base if not assigned
        if (rotatingPart == null)
        {
            rotatingPart = transform.GetChild(0); // Assumes first child is the rotating part
        }

        // Start detection coroutine
        StartCoroutine(DetectionLoop());
    }

    Transform FindFirePoint()
    {
        // Try to find a child object named "FirePoint"
        Transform foundFirePoint = transform.Find("FirePoint");
        if (foundFirePoint != null)
            return foundFirePoint;

        // If not found, create one at the front of the turret base
        GameObject firePointObj = new GameObject("FirePoint");
        firePointObj.transform.SetParent(rotatingPart != null ? rotatingPart : transform);
        firePointObj.transform.localPosition = Vector3.forward * 2f; // Adjust as needed
        return firePointObj.transform;
    }

    void Update()
    {
        switch (currentState)
        {
            case TurretState.Hidden:
                // Do nothing, waiting for detection
                break;

            case TurretState.Rising:
                HandleRising();
                break;

            case TurretState.Active:
                HandleActive();
                break;

            case TurretState.Hiding:
                HandleHiding();
                break;

            case TurretState.Destroyed:
                // Handle destruction effects if needed
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
            yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds
        }
    }

    void DetectCar()
    {
        Collider[] detectedObjects = Physics.OverlapSphere(transform.position, detectionRange, carLayerMask);

        if (detectedObjects.Length > 0)
        {
            // Found a car, activate turret
            carTarget = detectedObjects[0].transform;
            carRigidbody = carTarget.GetComponent<Rigidbody>();
            ActivateTurret();
        }
    }

    void ActivateTurret()
    {
        if (currentState == TurretState.Hidden)
        {
            currentState = TurretState.Rising;
            isActive = true;
        }
    }

    void HandleRising()
    {
        // Move turret up from hidden position
        transform.position = Vector3.MoveTowards(transform.position, originalPosition, riseSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, originalPosition) < 0.1f)
        {
            transform.position = originalPosition;
            currentState = TurretState.Active;
            isHidden = false;
        }
    }

    void HandleActive()
    {
        if (carTarget == null)
        {
            DeactivateTurret();
            return;
        }

        float distanceToCar = Vector3.Distance(transform.position, carTarget.position);

        // Check if car is still in range
        if (distanceToCar > attackRange)
        {
            DeactivateTurret();
            return;
        }

        // Aim at the car
        AimAtTarget();

        // Fire at the car
        if (Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + fireRate;
        }
    }

    void AimAtTarget()
    {
        if (carTarget == null || rotatingPart == null) return;

        Vector3 targetPosition = carTarget.position;

        // Use predictive aiming if enabled
        if (usePredictiveAiming && carRigidbody != null)
        {
            Vector3 carVelocity = carRigidbody.linearVelocity;
            float timeToTarget = Vector3.Distance(transform.position, carTarget.position) / bulletSpeed;
            targetPosition += carVelocity * timeToTarget * predictionMultiplier;
        }

        // Calculate direction to target
        Vector3 direction = (targetPosition - rotatingPart.position).normalized;
        direction.y = 0; // Keep turret level (only rotate on Y-axis)

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rotatingPart.rotation = Quaternion.RotateTowards(rotatingPart.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void Fire()
    {
        if (firePoint == null || bulletPrefab == null) return;

        // Instantiate bullet
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // Add velocity to bullet
        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = firePoint.forward * bulletSpeed;
        }

        // Play effects
        if (fireAudioSource != null)
        {
            fireAudioSource.Play();
        }

        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        // Destroy bullet after some time to prevent memory leaks
        Destroy(bullet, 5f);
    }

    void HandleHiding()
    {
        // Move turret down to hidden position
        transform.position = Vector3.MoveTowards(transform.position, hiddenPosition, riseSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, hiddenPosition) < 0.1f)
        {
            transform.position = hiddenPosition;
            currentState = TurretState.Hidden;
            isHidden = true;
            isActive = false;
            carTarget = null;
            carRigidbody = null;
        }
    }

    void DeactivateTurret()
    {
        if (currentState == TurretState.Active)
        {
            currentState = TurretState.Hiding;
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentState == TurretState.Destroyed) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            DestroyTurret();
        }
    }

    void DestroyTurret()
    {
        currentState = TurretState.Destroyed;

        // Add destruction effects here (particles, sound, etc.)

        // Disable the turret
        enabled = false;

        // Optionally destroy the game object after a delay
        Destroy(gameObject, 2f);
    }

    // Public methods for external access
    public bool IsActive() { return isActive; }
    public bool IsHidden() { return isHidden; }
    public float GetHealth() { return currentHealth; }
    public float GetMaxHealth() { return maxHealth; }

    // Gizmos for debugging in Scene view
    void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw fire point
        if (firePoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(firePoint.position, 0.2f);
            Gizmos.DrawLine(firePoint.position, firePoint.position + firePoint.forward * 3f);
        }
    }
}
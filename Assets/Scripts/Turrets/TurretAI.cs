using UnityEngine;
using System.Collections;

[RequireComponent(typeof(WeaponSystem))]
[RequireComponent(typeof(HealthSystem))]
public class TurretAI : MonoBehaviour
{
    [Header("Detection & Combat")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Animation")]
    [SerializeField] private float riseSpeed = 2f;
    [SerializeField] private float hiddenYOffset = -2f;

    [Header("Components")]
    [SerializeField] private Transform rotatingPart;
    [SerializeField] private Transform firePoint;

    public enum TurretState { Hidden, Rising, Active, Hiding, Destroyed }

    private TurretState currentState = TurretState.Hidden;
    private Transform playerTarget;
    private Rigidbody playerRigidbody;
    private Vector3 originalPosition;
    private Vector3 hiddenPosition;
    private WeaponSystem weaponSystem;
    private HealthSystem healthSystem;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        originalPosition = transform.position;
        hiddenPosition = originalPosition + Vector3.up * hiddenYOffset;
        transform.position = hiddenPosition;

        weaponSystem = GetComponent<WeaponSystem>();
        healthSystem = GetComponent<HealthSystem>();

        SetupComponents();
        StartCoroutine(DetectionLoop());

        // Subscribe to health system death event instead of polling
        if (healthSystem != null)
        {
            // Listen for when THIS specific turret dies via the GameEvents system
            GameEvents.OnTurretDestroyed += OnAnyTurretDestroyed;
        }
    }

    void OnAnyTurretDestroyed(Vector3 destroyedPosition)
    {
        // Check if the destroyed turret is this turret (by position)
        if (Vector3.Distance(transform.position, destroyedPosition) < 0.1f)
        {
            currentState = TurretState.Destroyed;
            StopAllCoroutines();
            Destroy(gameObject, 1f);
        }
    }

    void SetupComponents()
    {
        if (rotatingPart == null && transform.childCount > 0)
            rotatingPart = transform.GetChild(0);
    }

    IEnumerator DetectionLoop()
    {
        while (currentState != TurretState.Destroyed)
        {
            if (currentState == TurretState.Hidden)
                DetectPlayer();
            else if (currentState == TurretState.Active)
                CheckPlayerInRange();

            yield return new WaitForSeconds(0.2f);
        }
    }

    void DetectPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && Vector3.Distance(transform.position, player.transform.position) <= detectionRange)
        {
            playerTarget = player.transform;
            playerRigidbody = playerTarget.GetComponent<Rigidbody>();
            ActivateTurret();
        }
    }

    void CheckPlayerInRange()
    {
        if (playerTarget == null || Vector3.Distance(transform.position, playerTarget.position) > detectionRange * 1.5f)
        {
            DeactivateTurret();
        }
    }

    void Update()
    {
        HandleState();
    }

    void HandleState()
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

    void ActivateTurret()
    {
        if (currentState == TurretState.Hidden)
            currentState = TurretState.Rising;
    }

    void HandleRising()
    {
        transform.position = Vector3.MoveTowards(transform.position, originalPosition, riseSpeed * Time.deltaTime);
        if (playerTarget != null) AimAtTarget();

        if (Vector3.Distance(transform.position, originalPosition) < 0.1f)
        {
            transform.position = originalPosition;
            currentState = TurretState.Active;
        }
    }

    void HandleActive()
    {
        if (playerTarget == null)
        {
            DeactivateTurret();
            return;
        }

        AimAtTarget();

        if (weaponSystem != null && weaponSystem.CanFire())
        {
            weaponSystem.Fire();
        }
    }

    void AimAtTarget()
    {
        if (playerTarget == null || rotatingPart == null) return;

        Vector3 direction = (playerTarget.position - rotatingPart.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rotatingPart.rotation = Quaternion.RotateTowards(rotatingPart.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void HandleHiding()
    {
        transform.position = Vector3.MoveTowards(transform.position, hiddenPosition, riseSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, hiddenPosition) < 0.1f)
        {
            transform.position = hiddenPosition;
            currentState = TurretState.Hidden;
            playerTarget = null;
            playerRigidbody = null;
        }
    }

    void DeactivateTurret()
    {
        if (currentState == TurretState.Active || currentState == TurretState.Rising)
            currentState = TurretState.Hiding;
    }

    void OnDestroy()
    {
        GameEvents.OnTurretDestroyed -= OnAnyTurretDestroyed;
    }
}

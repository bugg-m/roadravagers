using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(WeaponSystem))]
[RequireComponent(typeof(HealthSystem))]
public class CarController : MonoBehaviour
{
    [Header("Car Performance")]
    [SerializeField] private float motorForce = 2000f;
    [SerializeField] private float brakeForce = 6000f;
    [SerializeField] private float maxSteerAngle = 60f;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider flWheel;
    [SerializeField] private WheelCollider frWheel;
    [SerializeField] private WheelCollider blWheel;
    [SerializeField] private WheelCollider brWheel;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform flWheelTransform;
    [SerializeField] private Transform frWheelTransform;
    [SerializeField] private Transform blWheelTransform;
    [SerializeField] private Transform brWheelTransform;

    [Header("Physics")]
    [SerializeField] private Transform centerOfMass;

    [Header("Firing")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float fireRayDistance = 250f;

    private WeaponSystem weaponSystem;
    private HealthSystem healthSystem;
    private Rigidbody carRigidbody;

    private float horizontalInput;
    private float verticalInput;
    private bool isBreaking;
    private bool fireInput;
    private float currentSteerAngle;
    private float currentBrakeForce;
    private float currentSpeed;
    private bool canMove = true;

    void Start()
    {
        Initialize();
        SubscribeToEvents();
    }

    void Initialize()
    {
        carRigidbody = GetComponent<Rigidbody>();
        weaponSystem = GetComponent<WeaponSystem>();
        healthSystem = GetComponent<HealthSystem>();

        if (centerOfMass != null)
        {
            carRigidbody.centerOfMass = centerOfMass.localPosition;
        }
        else
        {
            carRigidbody.centerOfMass = new Vector3(0f, -0.5f, 0.5f);
        }

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void SubscribeToEvents()
    {
        GameEvents.OnPlayerDied += DisableMovement;
        GameEvents.OnGamePaused += DisableMovement;
        GameEvents.OnGameStarted += EnableMovement;
    }

    void Update()
    {
        GetInput();
    }

    void FixedUpdate()
    {
        currentSpeed = carRigidbody != null ? carRigidbody.linearVelocity.magnitude * 3.6f : 0f;
        GameEvents.OnPlayerSpeedChanged?.Invoke(currentSpeed);

        if (!canMove)
        {
            ZeroWheels();
            return;
        }

        HandleMotor();
        HandleSteering();
        HandleBraking();
        UpdateWheelPoses();
        HandleFireInput();
    }

    void GetInput()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
        isBreaking = Input.GetKey(KeyCode.Space);
        fireInput = Input.GetButton("Fire1") || Input.GetKey(KeyCode.RightShift);
    }

    void HandleFireInput()
    {
        if (!fireInput || weaponSystem == null) return;

        if (playerCamera != null)
        {
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Ray ray = playerCamera.ScreenPointToRay(screenCenter);
            if (Physics.Raycast(ray, out RaycastHit hit, fireRayDistance))
            {
                Vector3 dir = (hit.point - transform.position).normalized;
                weaponSystem.FireForward(transform.position + Vector3.up * 1.0f, dir, hit.distance);
            }
            else
            {
                Vector3 forwardOrigin = transform.position + transform.up * 1f + transform.forward * 1f;
                Vector3 forwardDir = playerCamera.transform.forward;
                weaponSystem.FireForward(forwardOrigin, forwardDir, fireRayDistance);
            }
        }
        else
        {
            Vector3 forwardOrigin = transform.position + transform.up * 1f + transform.forward * 1f;
            Vector3 forwardDir = transform.forward;
            weaponSystem.FireForward(forwardOrigin, forwardDir, fireRayDistance);
        }
    }

    void HandleMotor()
    {
        float motor = Mathf.Clamp(verticalInput, -1f, 1f) * motorForce;
        if (blWheel != null) blWheel.motorTorque = motor;
        if (brWheel != null) brWheel.motorTorque = motor;
    }

    void HandleSteering()
    {
        currentSteerAngle = maxSteerAngle * Mathf.Clamp(horizontalInput, -1f, 1f);
        if (flWheel != null) flWheel.steerAngle = currentSteerAngle;
        if (frWheel != null) frWheel.steerAngle = currentSteerAngle;
    }

    void HandleBraking()
    {
        currentBrakeForce = isBreaking ? brakeForce : 0f;
        if (flWheel != null) flWheel.brakeTorque = currentBrakeForce;
        if (frWheel != null) frWheel.brakeTorque = currentBrakeForce;
        if (blWheel != null) blWheel.brakeTorque = currentBrakeForce;
        if (brWheel != null) brWheel.brakeTorque = currentBrakeForce;
    }

    void UpdateWheelPoses()
    {
        UpdateWheelPose(flWheel, flWheelTransform);
        UpdateWheelPose(frWheel, frWheelTransform);
        UpdateWheelPose(blWheel, blWheelTransform);
        UpdateWheelPose(brWheel, brWheelTransform);
    }

    void UpdateWheelPose(WheelCollider collider, Transform wheelTransform)
    {
        if (collider == null || wheelTransform == null) return;
        collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    void ZeroWheels()
    {
        if (flWheel != null) { flWheel.motorTorque = 0f; flWheel.brakeTorque = brakeForce; flWheel.steerAngle = 0f; }
        if (frWheel != null) { frWheel.motorTorque = 0f; frWheel.brakeTorque = brakeForce; frWheel.steerAngle = 0f; }
        if (blWheel != null) { blWheel.motorTorque = 0f; blWheel.brakeTorque = brakeForce; }
        if (brWheel != null) { brWheel.motorTorque = 0f; brWheel.brakeTorque = brakeForce; }
    }

    void EnableMovement() => canMove = true;

    void DisableMovement()
    {
        canMove = false;
        ZeroWheels();
    }

    public float GetCurrentSpeed() => currentSpeed;
    public bool IsMoving() => carRigidbody != null && carRigidbody.linearVelocity.magnitude > 0.1f;

    void OnDestroy()
    {
        GameEvents.OnPlayerDied -= DisableMovement;
        GameEvents.OnGamePaused -= DisableMovement;
        GameEvents.OnGameStarted -= EnableMovement;
    }
}

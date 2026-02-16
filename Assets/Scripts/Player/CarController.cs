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

    private WeaponSystem weaponSystem;
    private HealthSystem healthSystem;
    private Rigidbody carRigidbody;
    private float horizontalInput;
    private float verticalInput;
    private float currentSteerAngle;
    private float currentBreakForce;
    private bool isBreaking;
    private bool fireInput;
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
            carRigidbody.centerOfMass = new Vector3(0, -0.5f, 0.5f);
        }
    }

    void SubscribeToEvents()
    {
        GameEvents.OnPlayerDied += DisableMovement;
        GameEvents.OnGamePaused += DisableMovement;
        GameEvents.OnGameStarted += EnableMovement;
    }

    void Update()
    {
        if (!canMove) return;
        GetInput();
    }

    void FixedUpdate()
    {
        if (!canMove) return;

        currentSpeed = carRigidbody.linearVelocity.magnitude * 3.6f;
        GameEvents.OnPlayerSpeedChanged?.Invoke(currentSpeed);

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
        if (fireInput)
        {
            weaponSystem.Fire();
        }
    }

    void HandleMotor()
    {
        float motor = verticalInput * motorForce;
        if (blWheel != null) blWheel.motorTorque = motor;
        if (brWheel != null) brWheel.motorTorque = motor;
    }

    void HandleSteering()
    {
        currentSteerAngle = maxSteerAngle * horizontalInput;
        if (flWheel != null) flWheel.steerAngle = currentSteerAngle;
        if (frWheel != null) frWheel.steerAngle = currentSteerAngle;
    }

    void HandleBraking()
    {
        currentBreakForce = isBreaking ? brakeForce : 0f;
        if (flWheel != null) flWheel.brakeTorque = currentBreakForce;
        if (frWheel != null) frWheel.brakeTorque = currentBreakForce;
        if (blWheel != null) blWheel.brakeTorque = currentBreakForce;
        if (brWheel != null) brWheel.brakeTorque = currentBreakForce;
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
        if (collider != null && wheelTransform != null)
        {
            collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            wheelTransform.position = pos;
            wheelTransform.rotation = rot;
        }
    }

    void EnableMovement() => canMove = true;
    void DisableMovement() => canMove = false;

    public float GetCurrentSpeed() => currentSpeed;
    public bool IsMoving() => carRigidbody.linearVelocity.magnitude > 0.1f;

    void OnDestroy()
    {
        GameEvents.OnPlayerDied -= DisableMovement;
        GameEvents.OnGamePaused -= DisableMovement;
        GameEvents.OnGameStarted -= EnableMovement;
    }
}

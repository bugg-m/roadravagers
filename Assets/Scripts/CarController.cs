using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour, IDamageable
{
    [Header("Car Performance")]
    [SerializeField] private float motorForce = 1500f;
    [SerializeField] private float brakeForce = 3000f;
    [SerializeField] private float maxSteerAngle = 30f;
    [SerializeField] private float maxSpeed = 200f;

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

    [Header("Weapon System")]
    [SerializeField] private Transform[] firePoints;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private ParticleSystem[] muzzleFlashes;

    [Header("Car Health")]
    [SerializeField] private float maxHealth = 100f;

    private Rigidbody carRigidbody;
    private float horizontalInput;
    private float verticalInput;
    private float currentSteerAngle;
    private float currentBreakForce;
    private bool isBreaking;
    private bool isFiring;
    private float nextFireTime;
    private float currentSpeed;
    private float currentHealth;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        currentHealth = maxHealth;
        carRigidbody = GetComponent<Rigidbody>();

        if (carRigidbody == null)
        {
            Debug.LogError("CarController requires a Rigidbody component!");
            return;
        }

        if (centerOfMass != null)
        {
            carRigidbody.centerOfMass = centerOfMass.localPosition;
        }
        else
        {
            carRigidbody.centerOfMass = new Vector3(0, -0.5f, 0.5f);
        }

        SetupFirePoints();
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

        if (muzzleFlashes == null && firePoints != null)
        {
            muzzleFlashes = new ParticleSystem[firePoints.Length];
            for (int i = 0; i < firePoints.Length; i++)
            {
                if (firePoints[i] != null)
                {
                    muzzleFlashes[i] = firePoints[i].GetComponentInChildren<ParticleSystem>();
                }
            }
        }
    }

    void Update()
    {
        GetInput();
        HandleFiring();
    }

    void FixedUpdate()
    {
        currentSpeed = carRigidbody.linearVelocity.magnitude * 3.6f; // Convert to km/h

        HandleMotor();
        HandleSteering();
        HandleBraking();
        UpdateWheelPoses();
    }

    void GetInput()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");
        isBreaking = Input.GetKey(KeyCode.Space);
        isFiring = Input.GetKey(KeyCode.RightShift);
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

    void HandleFiring()
    {
        if (isFiring && Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Fire()
    {
        if (firePoints == null || bulletPrefab == null) return;

        for (int i = 0; i < firePoints.Length; i++)
        {
            if (firePoints[i] != null)
            {
                GameObject bullet = Instantiate(bulletPrefab, firePoints[i].position, firePoints[i].rotation);

                Bullet bulletScript = bullet.GetComponent<Bullet>();
                if (bulletScript != null)
                {
                    bulletScript.SetOwner("Player");
                }

                if (muzzleFlashes != null && i < muzzleFlashes.Length && muzzleFlashes[i] != null)
                {
                    muzzleFlashes[i].Play();
                }
            }
        }
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

    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);
        if (currentHealth <= 0)
        {
            DestroyCar();
        }
    }

    void DestroyCar()
    {
        enabled = false;
        Debug.Log("Car destroyed!");
    }

    public float GetHealth() => currentHealth;
    public bool IsDestroyed() => currentHealth <= 0;

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), $"Speed: {currentSpeed:F1} km/h");
        GUI.Label(new Rect(10, 30, 200, 20), $"Health: {currentHealth:F1}/{maxHealth}");
    }
}
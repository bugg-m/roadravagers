using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Car Settings")]
    [SerializeField] private float speed = 200f;
    [SerializeField] private float steeringAngle = 30f;
    [SerializeField] private float brakeTorque = 4000f;

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

    [Header("Car Center of Mass")]
    [SerializeField] private Transform carCenterOfMassTransform;

    [SerializeField] private Rigidbody carRigidbody;

    private float moveInput;
    private float turnInput;
    private bool isBraking;
    private string HORIZONTAL = "Horizontal";
    private string VERTICAL = "Vertical";

    void Start()
    {
        carRigidbody.centerOfMass = carCenterOfMassTransform.localPosition;
    }

    void FixedUpdate()
    {
        GetInput();
        MotorForce();
        SteerWheels();
        UpdateWheelPoses();
        ApplyBrakes();
    }

    void GetInput()
    {
        moveInput = Input.GetAxis(VERTICAL);
        turnInput = Input.GetAxis(HORIZONTAL);
        isBraking = Input.GetKey(KeyCode.Space);
    }
    void MotorForce()
    {
        blWheel.motorTorque = moveInput * speed;
        brWheel.motorTorque = moveInput * speed;
    }

    void SteerWheels()
    {
        flWheel.steerAngle = turnInput * steeringAngle;
        frWheel.steerAngle = turnInput * steeringAngle;
    }

    void ApplyBrakes()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            flWheel.brakeTorque = brakeTorque;
            frWheel.brakeTorque = brakeTorque;
            blWheel.brakeTorque = brakeTorque;
            brWheel.brakeTorque = brakeTorque;
        }
        else
        {
            flWheel.brakeTorque = 0;
            frWheel.brakeTorque = 0;
            blWheel.brakeTorque = 0;
            brWheel.brakeTorque = 0;
        }
    }

    void UpdateWheelPoses()
    {
        UpdateWheelPose(flWheel, flWheelTransform);
        UpdateWheelPose(frWheel, frWheelTransform);
        UpdateWheelPose(blWheel, blWheelTransform);
        UpdateWheelPose(brWheel, brWheelTransform);
    }

    void UpdateWheelPose(WheelCollider collider, Transform transform)
    {
        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        transform.SetPositionAndRotation(position, rotation);
    }
}

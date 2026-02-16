using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform cameraPoint;
    [SerializeField] private float smoothSpeed = 0.125f;
    [SerializeField] private float lookSmoothSpeed = 2f;

    private Transform target;
    private Vector3 velocity = Vector3.zero;

    void Start()
    {
        target = GameObject.FindGameObjectWithTag("Player")?.transform;
    }


    void FixedUpdate()
    {
        if (target == null) return;

        transform.position = Vector3.SmoothDamp(transform.position, cameraPoint.position, ref velocity, smoothSpeed);

        Vector3 lookDirection = target.position - transform.position;
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lookSmoothSpeed * Time.deltaTime);
        }
    }
}

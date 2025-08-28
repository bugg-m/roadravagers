using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform carTransform;
    [SerializeField] private Transform cameraPoint;
    private Vector3 velocity = Vector3.zero;
    private readonly float delayTime = 0.5f;

    void FixedUpdate()
    {
        if (carTransform == null || cameraPoint == null) return;
        Follow();
    }

    void Follow()
    {
        transform.LookAt(carTransform);
        transform.position = Vector3.SmoothDamp(transform.position, cameraPoint.position, ref velocity, Time.deltaTime * delayTime);
    }


}


using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float forwardSpeed = 20f;
    public float turnSpeed = 120f;
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float fireCooldown = 0.25f;

    Rigidbody rb;
    float lastFireTime;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        if (firePoint == null)
        {
            // create a default fire point in front if not assigned
            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(transform);
            fp.transform.localPosition = new Vector3(0, 0.5f, 1.8f);
            firePoint = fp.transform;
        }
    }

    void Update()
    {
        // Simple driving using Input axes (works in Editor)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Forward/backward by applying forward velocity
        Vector3 forward = transform.forward * v * forwardSpeed;
        Vector3 newVel = new Vector3(forward.x, rb.linearVelocity.y, forward.z);
        rb.linearVelocity = newVel;

        // Rotation
        float turn = h * turnSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, turn);

        // Shooting
        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= lastFireTime + fireCooldown)
        {
            Shoot();
            lastFireTime = Time.time;
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null) return;
        GameObject b = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bl = b.GetComponent<Bullet>();
        if (bl != null) bl.OwnerTag = gameObject.tag;
    }

    // Called by GameManager when hit by turret projectile
    public void ApplyDamage(int amount)
    {
        GameManager.Instance.ApplyDamage(amount);
    }
}

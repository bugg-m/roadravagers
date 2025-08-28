using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 40f;
    public int damage = 1;
    public float lifeTime = 4f;
    public string OwnerTag = "Player"; // set by spawner: "Player" or "Turret"

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            // if kinematic, we may move transform directly; if dynamic, set velocity
            if (!rb.isKinematic)
            {
                rb.linearVelocity = transform.forward * speed;
            }
        }
        // if using kinematic rigidbody or no rigidbody, move in Update
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // if kinematic or no rigidbody, move manually
        if (rb == null || rb.isKinematic)
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // ignore collisions with owner
        if (other.CompareTag(OwnerTag)) return;

        // player bullet hits turret
        if (OwnerTag == "Player" && other.CompareTag("Turret"))
        {
            TurretAI tc = other.GetComponentInParent<TurretAI>();
            if (tc != null) tc.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // turret bullet hits player
        if (OwnerTag == "Turret" && other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null) pc.ApplyDamage(damage);
            Destroy(gameObject);
            return;
        }

        // otherwise hit environment / other: destroy
        Destroy(gameObject);
    }
}

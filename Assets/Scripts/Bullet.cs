using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Properties")]
    [SerializeField] private float speed = 50f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float lifeTime = 5f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject impactEffect;

    private string ownerTag = "Player";
    private Rigidbody bulletRigidbody;
    private bool hasHit = false;

    void Start()
    {
        bulletRigidbody = GetComponent<Rigidbody>();

        if (bulletRigidbody != null)
        {
            bulletRigidbody.linearVelocity = transform.forward * speed;
        }

        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit || other.CompareTag(ownerTag)) return;

        hasHit = true;
        ProcessHit(other);
        DestroyBullet();
    }

    void ProcessHit(Collider hitCollider)
    {
        if (!hitCollider.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable = hitCollider.GetComponentInParent<IDamageable>();
        }

        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        if (impactEffect != null)
        {
            GameObject effect = Instantiate(impactEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    void DestroyBullet()
    {
        Destroy(gameObject);
    }

    public void SetOwner(string owner)
    {
        ownerTag = owner;
    }

    public string GetOwner() => ownerTag;
    public float GetDamage() => damage;
}

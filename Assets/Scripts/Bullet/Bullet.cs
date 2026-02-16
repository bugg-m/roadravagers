using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Properties")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float lifeTime = 5f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject impactEffectPrefab;

    private string ownerTag = "Player";
    private bool hasHit = false;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit || other.CompareTag(ownerTag) || other.CompareTag("Projectile"))
            return;

        hasHit = true;
        ProcessHit(other);
        CreateImpactEffect();
        Destroy(gameObject);
    }

    void ProcessHit(Collider hitCollider)
    {
        IDamageable damageable = hitCollider.GetComponent<IDamageable>() ??
                                hitCollider.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }

    void CreateImpactEffect()
    {
        if (impactEffectPrefab != null)
        {
            GameObject effect = Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    public void SetOwner(string owner) => ownerTag = owner;
}
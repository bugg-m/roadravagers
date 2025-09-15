using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float damage);
    float GetHealth();
    float GetMaxHealth();
    bool IsDestroyed();
    void OnHit(Vector3 hitPoint, Vector3 direction);
}
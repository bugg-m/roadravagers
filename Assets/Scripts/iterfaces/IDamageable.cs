public interface IDamageable
{
    void TakeDamage(float damage);
    float GetHealth();
    float GetMaxHealth();
    bool IsDestroyed();
}
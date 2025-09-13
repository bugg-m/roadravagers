using UnityEngine;

public class HealthSystem : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool isPlayer = false;

    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;

        if (isPlayer)
        {
            GameEvents.OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    /// <summary>Pool-friendly reset: restore full health.</summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        if (isPlayer)
        {
            GameEvents.OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0f) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);

        if (isPlayer)
        {
            GameEvents.OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
    }

    public void Heal(float healAmount)
    {
        if (currentHealth <= 0f) return;

        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0f, maxHealth);

        if (isPlayer)
        {
            GameEvents.OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    void HandleDeath()
    {
        if (isPlayer)
        {
            GameEvents.OnPlayerDied?.Invoke();
        }
        else if (CompareTag("Turret"))
        {
            GameEvents.OnTurretDestroyed?.Invoke(transform.position);
            GameEvents.OnEnemyKilled?.Invoke(100); // Score value
        }
    }

    public float GetHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public bool IsDestroyed() => currentHealth <= 0;
    public float GetHealthPercentage() => currentHealth / maxHealth;

    void OnDestroy()
    {
        // nothing extra to trim here (keeps behaviour predictable)
    }
}

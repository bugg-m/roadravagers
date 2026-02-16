// using UnityEngine;

// public class PowerUp : MonoBehaviour
// {
//     [Header("Power-up Settings")]
//     [SerializeField] private PowerUpType powerUpType;
//     [SerializeField] private float effectValue = 20f;
//     [SerializeField] private float effectDuration = 10f;
//     [SerializeField] private float rotationSpeed = 90f;

//     public enum PowerUpType
//     {
//         Health,
//         Ammo,
//         SpeedBoost,
//         Damage,
//         Shield
//     }

//     void Update()
//     {
//         transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
//     }

//     void OnTriggerEnter(Collider other)
//     {
//         if (other.CompareTag("Player"))
//         {
//             ApplyPowerUp(other.gameObject);
//             Destroy(gameObject);
//         }
//     }

//     void ApplyPowerUp(GameObject player)
//     {
//         switch (powerUpType)
//         {
//             case PowerUpType.Health:
//                 HealthSystem health = player.GetComponent<HealthSystem>();
//                 if (health != null) health.Heal(effectValue);
//                 break;

//             case PowerUpType.Ammo:
//                 WeaponSystem weapon = player.GetComponent<WeaponSystem>();
//                 if (weapon != null) weapon.AddAmmo((int)effectValue);
//                 break;

//                 // Add other power-up effects here
//         }
//     }
// }

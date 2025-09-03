using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Slider healthSlider;

    [Header("Panels")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;

    void Start()
    {
        SubscribeToEvents();
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    void SubscribeToEvents()
    {
        GameEvents.OnScoreChanged += UpdateScore;
        GameEvents.OnPlayerHealthChanged += UpdateHealth;
        GameEvents.OnAmmoChanged += UpdateAmmo;
        GameEvents.OnPlayerSpeedChanged += UpdateSpeed;
        GameEvents.OnPlayerDied += ShowGameOver;
        GameEvents.OnTimeChanged += UpdateTime;
    }

    void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score:N0}";
    }

    void UpdateHealth(float current, float max)
    {
        if (healthSlider != null)
            healthSlider.value = current / max;
    }

    void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
            ammoText.text = $"Ammo: {current}/{max}";
    }

    void UpdateSpeed(float speed)
    {
        if (speedText != null)
            speedText.text = $"Speed: {speed:F0} km/h";
    }

    void UpdateTime(float gameTime)
    {
        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(gameTime / 60f);
            int seconds = Mathf.FloorToInt(gameTime % 60f);
            timeText.text = $"Time: {minutes:00}:{seconds:00}";
        }
    }

    void ShowGameOver()
    {
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    void OnDestroy()
    {
        GameEvents.OnScoreChanged -= UpdateScore;
        GameEvents.OnPlayerHealthChanged -= UpdateHealth;
        GameEvents.OnAmmoChanged -= UpdateAmmo;
        GameEvents.OnPlayerSpeedChanged -= UpdateSpeed;
        GameEvents.OnPlayerDied -= ShowGameOver;
        GameEvents.OnTimeChanged -= UpdateTime;
    }
}

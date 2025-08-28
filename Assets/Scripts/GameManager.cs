using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int playerMaxHealth = 10;
    public int playerCurrentHealth;
    public TextMeshProUGUI scoreText;
    public Slider healthSlider;
    public GameObject gameOverPanel;

    int score = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
        playerCurrentHealth = playerMaxHealth;
        if (healthSlider) healthSlider.maxValue = playerMaxHealth;
        UpdateUI();
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();
    }

    public void ApplyDamage(int amount)
    {
        playerCurrentHealth -= amount;
        if (playerCurrentHealth <= 0)
        {
            playerCurrentHealth = 0;
            GameOver();
        }
        UpdateUI();
    }

    void UpdateUI()
    {
        if (scoreText) scoreText.text = $"Score: {score}";
        if (healthSlider) healthSlider.value = playerCurrentHealth;
    }

    void GameOver()
    {
        if (gameOverPanel) gameOverPanel.SetActive(true);
        // stop time or disable player controls
        Time.timeScale = 0f;
    }

    // optional restart
    public void Restart()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}

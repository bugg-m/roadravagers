using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game State")]
    [SerializeField] private int score = 0;
    [SerializeField] private float gameTime = 0f;
    [SerializeField] private bool isGameActive = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        SubscribeToEvents();
        GameEvents.OnGameStarted?.Invoke();
    }

    void SubscribeToEvents()
    {
        GameEvents.OnEnemyKilled += AddScore;
        GameEvents.OnPlayerDied += HandleGameOver;
    }

    void Update()
    {
        if (isGameActive)
        {
            gameTime += Time.deltaTime;
            GameEvents.OnTimeChanged?.Invoke(gameTime);
        }
    }

    void AddScore(int points)
    {
        if (!isGameActive) return;

        score += points;
        GameEvents.OnScoreChanged?.Invoke(score);
    }

    void HandleGameOver()
    {
        isGameActive = false;
        GameEvents.OnGameOver?.Invoke();
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        GameEvents.OnEnemyKilled -= AddScore;
        GameEvents.OnPlayerDied -= HandleGameOver;
    }
}

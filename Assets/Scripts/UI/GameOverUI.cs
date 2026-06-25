using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI flavourText;
    public Button playAgainButton;

    [Header("Army Morale Bars")]
    public Image playerArmyMoraleBar;
    public Image enemyArmyMoraleBar;
    public TextMeshProUGUI playerMoraleText;
    public TextMeshProUGUI enemyMoraleText;

    [Header("Colors")]
    public Color victoryColor = new Color(0.20f, 0.85f, 0.20f);
    public Color defeatColor  = new Color(0.85f, 0.20f, 0.20f);

    void Start()
    {
        // Hide panel at start
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // Wire play again button
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);
    }

    // ── Game Over Screen ──────────────────────────────────────────

    public void ShowGameOver(bool playerWon)
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (resultText != null)
        {
            resultText.text  = playerWon ? "VICTORY!" : "DEFEAT!";
            resultText.color = playerWon ? victoryColor : defeatColor;
        }

        if (flavourText != null)
        {
            flavourText.text = playerWon
                ? "The enemy army has broken!\nThe field is yours."
                : "Your army has broken!\nRetreat and regroup.";
        }

        // Pause the game
        Time.timeScale = 0f;

        Debug.Log(playerWon ? "=== VICTORY ===" : "=== DEFEAT ===");
    }

    // ── Army Morale Bars ──────────────────────────────────────────

    public void UpdateArmyMoraleBars(float playerMorale, float enemyMorale)
    {
        float playerPercent = Mathf.Clamp01(playerMorale / 100f);
        float enemyPercent  = Mathf.Clamp01(enemyMorale  / 100f);

        if (playerArmyMoraleBar != null)
            playerArmyMoraleBar.fillAmount = playerPercent;

        if (enemyArmyMoraleBar != null)
            enemyArmyMoraleBar.fillAmount = enemyPercent;

        if (playerMoraleText != null)
            playerMoraleText.text = $"Allied Morale: {playerMorale:0}";

        if (enemyMoraleText != null)
            enemyMoraleText.text = $"Enemy Morale: {enemyMorale:0}";
    }

    // ── Play Again ────────────────────────────────────────────────

    void OnPlayAgainClicked()
    {
        // Resume time before reloading
        Time.timeScale = 1f;

        // Reload current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
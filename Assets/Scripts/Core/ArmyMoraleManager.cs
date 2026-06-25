using UnityEngine;
using System.Collections.Generic;

public class ArmyMoraleManager : MonoBehaviour
{
    public static ArmyMoraleManager Instance;

    [Header("Army Morale")]
    public float playerArmyMorale = 100f;
    public float enemyArmyMorale  = 100f;

    [Header("Morale Loss Per Event")]
    public float moraleOnInfantryRouted   = 8f;
    public float moraleOnCavalryRouted    = 10f;
    public float moraleOnArtilleryRouted  = 15f;
    public float moraleOnAnyRouted        = 20f;

    [Header("References")]
    public BattleManager battleManager;
    public GameOverUI    gameOverUI;

    private bool gameEnded = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log(" ArmyMoraleManager instance GET.");
        }
        else Destroy(gameObject);
    }

    // ── Called When Any Unit Routes ───────────────────────────────

    public void OnUnitRouted(UnitController unit)
    {
        Debug.Log($"OnUnitRouted called — {unit.data.unitName} " +
              $"faction={unit.faction} gameEnded={gameEnded}");

        if (gameEnded) return;

        float moraleLoss = GetMoraleLoss(unit);

        if (unit.faction == Faction.Player)
        {
            playerArmyMorale -= moraleLoss;
            playerArmyMorale  = Mathf.Max(0, playerArmyMorale);

            Debug.Log($"PLAYER army morale: {playerArmyMorale} " +
                      $"(-{moraleLoss} from {unit.data.unitName} routing)");
        }
        else
        {
            enemyArmyMorale -= moraleLoss;
            enemyArmyMorale  = Mathf.Max(0, enemyArmyMorale);

            Debug.Log($"ENEMY army morale: {enemyArmyMorale} " +
                      $"(-{moraleLoss} from {unit.data.unitName} routing)");
        }

        // Update UI bars
        gameOverUI?.UpdateArmyMoraleBars(playerArmyMorale, enemyArmyMorale);

        // Check win condition
        CheckWinCondition();
    }

    float GetMoraleLoss(UnitController unit)
    {
        // Base loss from unit type
        float typeLoss = unit.data.unitType switch
        {
            UnitType.Artillery    => moraleOnArtilleryRouted,
            UnitType.Cavalry      => moraleOnCavalryRouted,
            UnitType.LineInfantry => moraleOnInfantryRouted,
            UnitType.Skirmisher   => moraleOnInfantryRouted * 0.5f,
            _                     => moraleOnAnyRouted
        };

        return typeLoss;
    }

    void CheckWinCondition()
    {
        if (gameEnded) return;

        Debug.Log($"Checking win condition — " +
                $"Player: {playerArmyMorale} " +
                $"Enemy: {enemyArmyMorale}");

        if (playerArmyMorale <= 0)
        {
            gameEnded = true;
            Debug.Log("DEFEAT — Player army has broken!");
            gameOverUI?.ShowGameOver(false);
        }
        else if (enemyArmyMorale <= 0)
        {
            gameEnded = true;
            Debug.Log("VICTORY — Enemy army has broken!");
            gameOverUI?.ShowGameOver(true);
        }
    }

    // Call this at the END of each turn execution
    // as a safety net to catch any missed routing events
    public void RecalculateArmyMorale()
    {
        if (gameEnded) return;

        // Count active units per side
        int playerActive = 0;
        int enemyActive  = 0;
        int playerTotal  = battleManager.playerUnits.Count;
        int enemyTotal   = battleManager.enemyUnits.Count;

        foreach (UnitController u in battleManager.playerUnits)
            if (u != null && !u.isDefeated && !u.isRouting)
                playerActive++;

        foreach (UnitController u in battleManager.enemyUnits)
            if (u != null && !u.isDefeated && !u.isRouting)
                enemyActive++;

        // Recalculate army morale from surviving units
        float newPlayerMorale = playerTotal > 0
            ? ((float)playerActive / playerTotal) * 100f
            : 0f;

        float newEnemyMorale = enemyTotal > 0
            ? ((float)enemyActive / enemyTotal) * 100f
            : 0f;

        Debug.Log($"Recalculate — Player active: {playerActive}/{playerTotal} " +
                $"Enemy active: {enemyActive}/{enemyTotal}");

        playerArmyMorale = newPlayerMorale;
        enemyArmyMorale  = newEnemyMorale;

        gameOverUI?.UpdateArmyMoraleBars(playerArmyMorale, enemyArmyMorale);
        CheckWinCondition();
    }

    public bool IsGameEnded() => gameEnded;
}
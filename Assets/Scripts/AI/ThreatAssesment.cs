using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ThreatAssessment : MonoBehaviour
{
    [Header("References")]
    public HexGrid      hexGrid;
    public BattleManager battleManager;

    [Header("Thresholds")]
    public int lowMoraleThreshold    = 40;
    public int dangerMoraleThreshold = 35;
    public int exposedDistThreshold  = 3;

    // ── Main Entry Point ──────────────────────────────────────────

    public void ExecuteAITurn()
    {
        Debug.Log("=== AI THREAT ASSESSMENT ===");

        // Step 0 — Handle formation changes FIRST
        // before any movement or combat orders
        HandleFormationChanges();

        // Step 1 — Self preservation
        HandleSelfPreservation();

        // Step 2 — Offensive orders
        AssignOffensiveOrders();

        Debug.Log("=== AI ASSESSMENT COMPLETE ===");
    }

    // ── Priority 1: Self Preservation ────────────────────────────

    void HandleSelfPreservation()
    {
        // Check artillery — most valuable, most vulnerable
        foreach (UnitController unit in battleManager.enemyUnits)
        {
            if (unit == null || unit.isDefeated || unit.isRouting) continue;
            if (unit.data.unitType != UnitType.Artillery) continue;
            if (unit.currentCell == null) continue;

            // Is artillery exposed? (player unit within 3 hexes)
            bool isExposed = IsUnitExposed(unit, battleManager.playerUnits);

            if (isExposed)
            {
                Debug.Log($"AI: Artillery at risk — finding infantry screen");
                ScreenUnit(unit);
            }
        }

        // Pull back dangerously low morale units
        foreach (UnitController unit in battleManager.enemyUnits)
        {
            if (unit == null || unit.isDefeated || unit.isRouting) continue;
            if (unit.currentCell == null) continue;
            if (unit.currentOrder != OrderType.None) continue;

            if (unit.currentMorale <= dangerMoraleThreshold)
            {
                Debug.Log($"AI: {unit.data.unitName} at {unit.currentMorale} " +
                          $"morale — pulling back");
                PullBack(unit);
            }
        }
    }

    // ── Priority 2 & 3: Offensive Orders ─────────────────────────

    void AssignOffensiveOrders()
    {
        List<TargetScore> scores = ScoreAllTargets();

        if (scores.Count == 0) return;

        scores.Sort((a, b) => b.score.CompareTo(a.score));

        foreach (UnitController aiUnit in battleManager.enemyUnits)
        {
            if (aiUnit == null || aiUnit.isDefeated || aiUnit.isRouting) continue;
            if (aiUnit.currentCell == null) continue;
            if (aiUnit.currentOrder != OrderType.None) continue;

            // Units in square hold position — don't move
            // but CAN still fire
            if (aiUnit.currentFormation == FormationType.Square)
            {
                // Only assign fire orders for square units
                UnitController fireTarget = FindBestFireTarget(
                    aiUnit, scores
                );

                if (fireTarget != null)
                {
                    aiUnit.AssignFireOrder(fireTarget);
                    Debug.Log($"AI: {aiUnit.data.unitName} fires from " +
                            $"square at {fireTarget.data.unitName}");
                }
                continue; // skip movement
            }

            // Normal offensive orders for non-square units
            UnitController bestTarget = FindBestTargetFor(aiUnit, scores);

            if (bestTarget != null)
                AssignBestOrderFor(aiUnit, bestTarget);
            else
                AdvanceTowardNearestEnemy(aiUnit);
        }
    }

    // Find best target in fire range only
    UnitController FindBestFireTarget(
        UnitController aiUnit,
        List<TargetScore> scores)
    {
        if (aiUnit.data.fireRange <= 0) return null;

        foreach (TargetScore ts in scores)
        {
            if (ts.target == null || ts.target.currentCell == null) continue;
            if (ts.target.isDefeated || ts.target.isRouting) continue;

            int dist = aiUnit.currentCell.DistanceTo(ts.target.currentCell);

            if (dist <= aiUnit.data.fireRange)
                return ts.target;
        }

        return null;
    }

    void AdvanceTowardNearestEnemy(UnitController aiUnit)
{
    if (aiUnit.currentCell == null) return;

    UnitController nearest = FindNearest(aiUnit, battleManager.playerUnits);

    if (nearest == null || nearest.currentCell == null)
    {
        Debug.Log($"AI: {aiUnit.data.unitName} has no valid target to advance toward");
        return;
    }

    HexCell advanceCell = GetBestAdvanceCell(aiUnit, nearest);

    if (advanceCell != null)
    {
        aiUnit.AssignMoveOrder(advanceCell);
        Debug.Log($"AI: {aiUnit.data.unitName} advances toward nearest enemy");
    }
    else
    {
        Debug.Log($"AI: {aiUnit.data.unitName} has no valid advance cell");
    }
}
    // ── Target Scoring ────────────────────────────────────────────

    List<TargetScore> ScoreAllTargets()
    {
        List<TargetScore> scores = new List<TargetScore>();

        foreach (UnitController player in battleManager.playerUnits)
        {
            if (player == null || player.isDefeated || player.isRouting) continue;
            if (player.currentCell == null) continue;

            float score = 0f;

            // Davout trait — low morale targets are priority
            float moralePercent = (float)player.currentMorale / 100f;
            float moraleScore   = (1f - moralePercent) * 40f;
            score += moraleScore;

            // Ney trait — exposed/isolated targets
            if (IsUnitExposed(player, battleManager.enemyUnits))
            {
                score += 30f;
                Debug.Log($"  {player.data.unitName} is EXPOSED +30");
            }

            // Bonus for high value targets
            score += player.data.unitType switch
            {
                UnitType.Artillery   => 20f, // highest priority
                UnitType.Skirmisher  => 10f, // screen units
                UnitType.LineInfantry => 5f,
                UnitType.Cavalry     => 5f,
                _                    => 0f
            };

            // Wrong formation bonus — Ney exploits this
            score += GetFormationVulnerabilityScore(player);

            scores.Add(new TargetScore(player, score));
        }

        return scores;
    }

    float GetFormationVulnerabilityScore(UnitController target)
    {
        // Check if any AI cavalry is nearby
        bool cavalryNearby = battleManager.enemyUnits.Any(u =>
            u != null &&
            !u.isDefeated &&
            !u.isRouting &&
            u.data.unitType == UnitType.Cavalry &&
            u.currentCell != null &&
            u.currentCell.DistanceTo(target.currentCell) <= 4
        );

        // Infantry NOT in square when cavalry is nearby = vulnerable
        if (cavalryNearby &&
            target.data.unitType == UnitType.LineInfantry &&
            target.currentFormation != FormationType.Square)
        {
            Debug.Log($"  {target.data.unitName} vulnerable to cavalry +25");
            return 25f;
        }

        // Column formation = easy target for anyone
        if (target.currentFormation == FormationType.Column)
            return 15f;

        return 0f;
    }

    // ── Formation Management ──────────────────────────────────────────

    void HandleFormationChanges()
    {
        foreach (UnitController unit in battleManager.enemyUnits)
        {
            if (unit == null || unit.isDefeated || unit.isRouting) continue;
            if (unit.currentCell == null) continue;
            if (unit.data.unitType != UnitType.LineInfantry) continue;

            bool playerCavalryNearby = IsCavalryNearby(
                unit,
                battleManager.playerUnits
            );

            if (playerCavalryNearby &&
                unit.currentFormation != FormationType.Square)
            {
                // Form square to defend against cavalry
                unit.AssignFormationChange(FormationType.Square);
                Debug.Log($"AI: {unit.data.unitName} forms SQUARE " +
                        $"— player cavalry nearby!");
            }
            else if (!playerCavalryNearby &&
                    unit.currentFormation == FormationType.Square)
            {
                // No cavalry threat — return to line
                unit.AssignFormationChange(FormationType.Line);
                Debug.Log($"AI: {unit.data.unitName} returns to LINE " +
                        $"— cavalry threat gone");
            }
        }
    }

    bool IsCavalryNearby(UnitController unit, List<UnitController> enemies)
    {
        foreach (UnitController enemy in enemies)
        {
            if (enemy == null || enemy.isDefeated || enemy.isRouting) continue;
            if (enemy.currentCell == null) continue;
            if (enemy.data.unitType != UnitType.Cavalry) continue;

            int dist = unit.currentCell.DistanceTo(enemy.currentCell);

            // Cavalry within charge range = threat
            if (dist <= enemy.data.movementRange - 2)
                return true;
        }

        return false;
    }

    // ── Order Assignment ──────────────────────────────────────────

    UnitController FindBestTargetFor(
        UnitController aiUnit,
        List<TargetScore> scores)
    {
        foreach (TargetScore ts in scores)
        {
            UnitController target = ts.target;
            if (target == null || target.currentCell == null) continue;

            int dist = aiUnit.currentCell.DistanceTo(target.currentCell);

            // Can fire at it?
            if (aiUnit.data.fireRange > 0 && dist <= aiUnit.data.fireRange)
                return target;

            // Can charge it?
            if (aiUnit.data.chargeDamage > 0 && dist <= aiUnit.data.movementRange)
                return target;

            // Can advance toward it?
            if (dist <= aiUnit.data.movementRange + 2)
                return target;
        }

        return null;
    }

    void AssignBestOrderFor(UnitController aiUnit, UnitController target)
    {
        int dist = aiUnit.currentCell.DistanceTo(target.currentCell);

        // Wellington trait — don't charge if our morale is low
        bool lowMorale = aiUnit.currentMorale < lowMoraleThreshold;

        // Can fire?
        if (aiUnit.data.fireRange > 0 && dist <= aiUnit.data.fireRange)
        {
            aiUnit.AssignFireOrder(target);
            Debug.Log($"AI: {aiUnit.data.unitName} fires at " +
                      $"{target.data.unitName} (score priority)");
            return;
        }

        // Can charge AND not low morale?
        if (aiUnit.data.chargeDamage > 0 &&
            dist <= aiUnit.data.movementRange &&
            !lowMorale)
        {
            aiUnit.AssignChargeOrder(target);
            Debug.Log($"AI: {aiUnit.data.unitName} CHARGES " +
                      $"{target.data.unitName}!");
            return;
        }

        // Advance toward target
        HexCell advanceCell = GetBestAdvanceCell(aiUnit, target);
        if (advanceCell != null)
        {
            aiUnit.AssignMoveOrder(advanceCell);
            Debug.Log($"AI: {aiUnit.data.unitName} advances toward " +
                      $"{target.data.unitName}");
        }
    }

    // ── Self Preservation Helpers ─────────────────────────────────

    void ScreenUnit(UnitController unitToScreen)
    {
        // Find nearest friendly infantry to act as screen
        UnitController screener = null;
        int nearestDist = int.MaxValue;

        foreach (UnitController u in battleManager.enemyUnits)
        {
            if (u == null || u.isDefeated || u.isRouting) continue;
            if (u == unitToScreen) continue;
            if (u.currentOrder != OrderType.None) continue;
            if (u.data.unitType != UnitType.LineInfantry) continue;
            if (u.currentCell == null) continue;

            int dist = u.currentCell.DistanceTo(unitToScreen.currentCell);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                screener    = u;
            }
        }

        if (screener == null) return;

        // Move screener adjacent to artillery
        HexCell screenCell = GetCellAdjacentTo(screener, unitToScreen);
        if (screenCell != null)
        {
            screener.AssignMoveOrder(screenCell);
            Debug.Log($"AI: {screener.data.unitName} screens artillery");
        }
    }

    void PullBack(UnitController unit)
    {
        // Move away from nearest player unit
        UnitController nearestPlayer = FindNearest(
            unit, battleManager.playerUnits
        );

        if (nearestPlayer == null || nearestPlayer.currentCell == null) return;

        // Find cell that moves AWAY from nearest player
        List<HexCell> inRange = hexGrid.GetCellsInRange(
            unit.currentCell,
            unit.data.movementRange
        );

        HexCell bestCell  = null;
        int     bestDist  = 0;

        foreach (HexCell cell in inRange)
        {
            if (cell == null || cell.isOccupied) continue;

            int dist = cell.DistanceTo(nearestPlayer.currentCell);
            if (dist > bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        if (bestCell != null)
        {
            unit.AssignMoveOrder(bestCell);
            Debug.Log($"AI: {unit.data.unitName} pulling back to safety");
        }
    }

    // ── Movement Helpers ──────────────────────────────────────────

    HexCell GetBestAdvanceCell(UnitController mover, UnitController target)
    {
        if (mover.currentCell == null || target.currentCell == null)
            return null;

        List<HexCell> inRange = hexGrid.GetCellsInRange(
            mover.currentCell,
            mover.data.movementRange
        );

        HexCell bestCell = null;
        int     bestDist = int.MaxValue;

        foreach (HexCell cell in inRange)
        {
            if (cell == null || cell.isOccupied) continue;

            int dist = cell.DistanceTo(target.currentCell);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    HexCell GetCellAdjacentTo(UnitController mover, UnitController target)
    {
        if (mover.currentCell == null || target.currentCell == null)
            return null;

        List<HexCell> adjacent = hexGrid.GetCellsInRange(
            target.currentCell, 1
        );

        HexCell bestCell = null;
        int     bestDist = int.MaxValue;

        foreach (HexCell cell in adjacent)
        {
            if (cell == null || cell.isOccupied) continue;

            int dist = cell.DistanceTo(mover.currentCell);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    bool IsUnitExposed(UnitController unit, List<UnitController> enemies)
    {
        if (unit.currentCell == null) return false;

        foreach (UnitController enemy in enemies)
        {
            if (enemy == null || enemy.isDefeated || enemy.isRouting) continue;
            if (enemy.currentCell == null) continue;

            if (unit.currentCell.DistanceTo(enemy.currentCell)
                <= exposedDistThreshold)
                return true;
        }

        return false;
    }

    UnitController FindNearest(
        UnitController unit,
        List<UnitController> others)
    {
        UnitController nearest  = null;
        int            bestDist = int.MaxValue;

        foreach (UnitController other in others)
        {
            if (other == null || other.isDefeated || other.isRouting) continue;
            if (other.currentCell == null) continue;

            int dist = unit.currentCell.DistanceTo(other.currentCell);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest  = other;
            }
        }

        return nearest;
    }
}


// ── Helper Class ──────────────────────────────────────────────────

public class TargetScore
{
    public UnitController target;
    public float          score;

    public TargetScore(UnitController t, float s)
    {
        target = t;
        score  = s;
    }
}
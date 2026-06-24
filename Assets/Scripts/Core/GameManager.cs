using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("References")]
    public BattleManager battleManager;
    public HexGrid hexGrid;

    [Header("Turn State")]
    public TurnPhase currentPhase;
    public int turnNumber = 1;

    [Header("Selection")]
    public UnitController selectedUnit;
    public List<HexCell> highlightedCells = new List<HexCell>();

    void Awake()
    {
        // Singleton pattern — one GameManager exists at all times
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartPlanningPhase();
    }

    // ── Phase Control ─────────────────────────────────────────────

    public void StartPlanningPhase()
    {
        currentPhase = TurnPhase.Planning;
        selectedUnit = null;
        ClearHighlights();
        Debug.Log($"Turn {turnNumber} — Planning Phase");
    }

    public void ExecuteOrders()
    {
        if (currentPhase != TurnPhase.Planning) return;

        currentPhase = TurnPhase.Execution;
        ClearHighlights();
        DeselectUnit();

        Debug.Log("Executing orders...");

        ResolveMovement(battleManager.playerUnits);
        ResolveFire(battleManager.playerUnits);
        ResolveCharge(battleManager.playerUnits);

        ResolveAITurn();
        ResolveFire(battleManager.enemyUnits);
        ResolveCharge(battleManager.enemyUnits);

        ClearAllOrders();

        turnNumber++;           // ← increment first
        StartPlanningPhase();   // ← then announce new turn
    }
    // ── Movement Resolution ───────────────────────────────────────────

    void ResolveMovement(List<UnitController> units)
    {
        foreach (UnitController unit in units)
        {
            if (unit.isDefeated || unit.isRouting) continue;
            if (unit.currentOrder != OrderType.Move) continue;
            if (unit.orderTargetCell == null) continue;

            if (unit.orderTargetCell.isOccupied)
            {
                Debug.Log($"{unit.data.unitName} move blocked");
                unit.ClearOrder();
                continue;
            }

            Debug.Log($"{unit.data.unitName} marching to " +
                    $"({unit.orderTargetCell.q}," +
                    $"{unit.orderTargetCell.r}," +
                    $"{unit.orderTargetCell.s})");

            unit.MoveToCell(unit.orderTargetCell, () =>
            {
                Debug.Log($"{unit.data.unitName} arrived!");
            });
        }
    }

// ── Fire Resolution ───────────────────────────────────────────────

    void ResolveFire(List<UnitController> units)
{
    foreach (UnitController unit in units)
    {
        if (unit.isDefeated || unit.isRouting) continue;
        if (unit.currentOrder != OrderType.Fire) continue;
        if (unit.orderTargetUnit == null) continue;

        UnitController target = unit.orderTargetUnit;

        // Check target still exists and is in range
        if (target.isDefeated)
        {
            Debug.Log($"{unit.data.unitName} fire cancelled — target defeated");
            unit.ClearOrder();
            continue;
        }

        if (!unit.CanFireAt(target))
        {
            Debug.Log($"{unit.data.unitName} fire cancelled — target out of range");
            unit.ClearOrder();
            continue;
        }

        // Calculate morale damage
        int damage = CalculateFireDamage(unit, target);

        Debug.Log($"{unit.data.unitName} fires at {target.data.unitName} " +
                  $"— {damage} morale damage");

        // Apply damage
        target.TakeMoraleDamage(damage, unit);

        // Kill some soldiers visually based on damage
        int soldiersKilled = Mathf.RoundToInt(damage / 10f);
        KillSoldiers(target, soldiersKilled, unit);
    }
}

    void ResolveCharge(List<UnitController> units)
    {
        foreach (UnitController unit in units)
        {
            if (unit.isDefeated || unit.isRouting) continue;
            if (unit.currentOrder != OrderType.Charge) continue;
            if (unit.orderTargetUnit == null) continue;

            UnitController target = unit.orderTargetUnit;

            if (target.isDefeated)
            {
                Debug.Log($"{unit.data.unitName} charge cancelled — target defeated");
                unit.ClearOrder();
                continue;
            }

            int distToTarget = unit.currentCell.DistanceTo(target.currentCell);

            if (distToTarget > unit.data.movementRange)
            {
                Debug.Log($"{unit.data.unitName} charge cancelled — target too far");
                unit.ClearOrder();
                continue;
            }

            // Calculate charge damage
            int damage = CalculateChargeDamage(unit, target);

            Debug.Log($"{unit.data.unitName} CHARGES {target.data.unitName} " +
                    $"— {damage} morale damage!");

            // Apply charge damage
            target.TakeMoraleDamage(damage, unit);

            // Kill more soldiers than fire (charge is brutal)
            int soldiersKilled = Mathf.RoundToInt(damage / 8f);
            KillSoldiers(target, soldiersKilled, unit);

            // Move cavalry to adjacent cell of target
            HexCell chargeDestination = GetCellAdjacentTo(unit, target);
            if (chargeDestination != null)
                unit.MoveToCell(chargeDestination);
        }
    }

    int CalculateChargeDamage(UnitController attacker, UnitController target)
    {
        int baseDamage = attacker.data.chargeDamage;

        // Formation counter — square formation nullifies cavalry charge
        float formationMultiplier = target.currentFormation switch
        {
            FormationType.Square    => 0.1f,  // square stops cavalry cold
            FormationType.Line      => 1.0f,  // line is vulnerable
            FormationType.Column    => 1.5f,  // column is devastated
            FormationType.Dispersed => 0.6f,  // skirmishers scatter
            _                       => 1.0f
        };

        // Attacker morale affects charge power
        float moraleMultiplier = attacker.moraleState switch
        {
            MoraleState.Steady   => 1.0f,
            MoraleState.Shaken   => 0.7f,
            MoraleState.Wavering => 0.4f,
            MoraleState.Breaking => 0.1f,
            _                    => 1.0f
        };

        // Random variance ±15%
        float variance = UnityEngine.Random.Range(0.85f, 1.15f);

        int finalDamage = Mathf.RoundToInt(
            baseDamage * formationMultiplier * moraleMultiplier * variance
        );

        return Mathf.Max(1, finalDamage);
    }

    HexCell GetCellAdjacentTo(UnitController mover, UnitController target)
    {
        // Find an empty cell next to the target to move into
        List<HexCell> adjacentCells = hexGrid.GetCellsInRange(
            target.currentCell, 1
        );

        HexCell bestCell = null;
        int bestDist = int.MaxValue;

        foreach (HexCell cell in adjacentCells)
        {
            if (cell.isOccupied) continue;

            // Pick the cell closest to where the charger started
            int dist = cell.DistanceTo(mover.currentCell);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        return bestCell;
    }

// ── Damage Calculation ────────────────────────────────────────────

    int CalculateFireDamage(UnitController attacker, UnitController target)
{
    int baseDamage = attacker.data.fireDamage;

    // Morale state of attacker affects accuracy
    float moraleMultiplier = attacker.moraleState switch
    {
        MoraleState.Steady   => 1.0f,
        MoraleState.Shaken   => 0.8f,
        MoraleState.Wavering => 0.5f,
        MoraleState.Breaking => 0.2f,
        _                    => 1.0f
    };

    // Formation bonus — line formation fires more effectively
    float formationMultiplier = target.currentFormation switch
    {
        FormationType.Line    => 1.0f,   // normal
        FormationType.Column  => 1.3f,   // column is vulnerable
        FormationType.Square  => 0.7f,   // square is tight, less impact
        FormationType.Dispersed => 0.5f, // skirmishers hard to hit
        _                     => 1.0f
    };

    // Distance penalty — further away = less accurate
    int distance = attacker.currentCell.DistanceTo(target.currentCell);
    float distancePenalty = 1.0f - (distance * 0.1f);
    distancePenalty = Mathf.Max(0.3f, distancePenalty); // min 30% effectiveness

    int finalDamage = Mathf.RoundToInt(
        baseDamage * moraleMultiplier * formationMultiplier * distancePenalty
    );

    // Add small random variance ±20%
    float variance = Random.Range(0.8f, 1.2f);
    finalDamage = Mathf.RoundToInt(finalDamage * variance);

    return Mathf.Max(1, finalDamage); // minimum 1 damage
}

// ── Soldier Casualties ────────────────────────────────────────────

    void KillSoldiers(UnitController target, int count, UnitController attacker)
{
    int killed = 0;

    foreach (SoldierCircle soldier in target.soldiers)
    {
        if (killed >= count) break;
        if (soldier.state != SoldierState.Alive) continue;

        // First wound the soldier, then kill on next hit
        // For now: instantly kill for visual feedback
        soldier.SetState(SoldierState.Dead);
        killed++;
        target.aliveSoldierCount--;
    }

    Debug.Log($"{killed} soldiers killed in {target.data.unitName}");

    // Trigger witness effect on nearby soldiers
    TriggerWitnessEffect(target, killed);
}

// ── Witness Effect ────────────────────────────────────────────────

    void TriggerWitnessEffect(UnitController unit, int deathsThisTurn)
{
    if (deathsThisTurn <= 0) return;

    // Extra morale damage based on how many comrades died
    int witnessDamage = deathsThisTurn * 3;

    Debug.Log($"Witness Effect: {unit.data.unitName} takes " +
              $"{witnessDamage} extra morale damage from witnessing deaths");

    unit.TakeMoraleDamage(witnessDamage, null);
}

// ── AI Turn ───────────────────────────────────────────────────────

    void ResolveAITurn()
{
    Debug.Log("--- AI Turn ---");

    foreach (UnitController aiUnit in battleManager.enemyUnits)
    {
        if (aiUnit.isDefeated || aiUnit.isRouting) continue;

        // Simple AI for now — find nearest player unit and act
        UnitController nearestPlayer = FindNearestEnemy(
            aiUnit,
            battleManager.playerUnits
        );

        if (nearestPlayer == null) continue;

        int distToPlayer = aiUnit.currentCell.DistanceTo(
            nearestPlayer.currentCell
        );

        // Can fire? Fire.
        if (aiUnit.data.fireRange > 0 && distToPlayer <= aiUnit.data.fireRange)
        {
            aiUnit.AssignFireOrder(nearestPlayer);
            Debug.Log($"AI {aiUnit.data.unitName} fires at " +
                      $"{nearestPlayer.data.unitName}");
        }
        // Can't fire but can move? Advance.
        else if (distToPlayer > 1)
        {
            HexCell advanceCell = GetCellToward(aiUnit, nearestPlayer);
            if (advanceCell != null)
            {
                aiUnit.AssignMoveOrder(advanceCell);
                Debug.Log($"AI {aiUnit.data.unitName} advances toward " +
                          $"{nearestPlayer.data.unitName}");
            }
        }
    }

    // Resolve AI movement
    ResolveMovement(battleManager.enemyUnits);
}

    UnitController FindNearestEnemy(
    UnitController unit,
        List<UnitController> enemies)
{
    UnitController nearest = null;
    int nearestDist = int.MaxValue;

    foreach (UnitController enemy in enemies)
    {
        if (enemy.isDefeated || enemy.isRouting) continue;

        int dist = unit.currentCell.DistanceTo(enemy.currentCell);
        if (dist < nearestDist)
        {
            nearestDist = dist;
            nearest = enemy;
        }
    }

    return nearest;
}

    HexCell GetCellToward(UnitController mover, UnitController target)
{
    // Get all cells in movement range
    List<HexCell> inRange = hexGrid.GetCellsInRange(
        mover.currentCell,
        mover.data.movementRange
    );

    // Find the empty cell closest to the target
    HexCell bestCell = null;
    int bestDist = int.MaxValue;

    foreach (HexCell cell in inRange)
    {
        if (cell.isOccupied) continue;

        int dist = cell.DistanceTo(target.currentCell);
        if (dist < bestDist)
        {
            bestDist = dist;
            bestCell = cell;
        }
    }

    return bestCell;
}

    // ── Clear Orders ──────────────────────────────────────────────────

    void ClearAllOrders()
{
    foreach (UnitController unit in battleManager.playerUnits)
        unit.ClearOrder();

    foreach (UnitController unit in battleManager.enemyUnits)
        unit.ClearOrder();
}


    // ── Selection ─────────────────────────────────────────────────
    public void SelectUnit(UnitController unit)
    {
        // Can only select player units during planning
        if (currentPhase != TurnPhase.Planning) return;
        if (unit.faction != Faction.Player) return;
        if (unit.isRouting || unit.isDefeated) return;

        // Deselect previous
        DeselectUnit();

        selectedUnit = unit;
        unit.SetSelected(true);

        // Show valid movement hexes
        ShowMovementRange(unit);

        Debug.Log($"Selected: {unit.data.unitName}");
    }

    public void DeselectUnit()
    {
        if (selectedUnit != null)
        {
            selectedUnit.SetSelected(false);
            selectedUnit = null;
        }
        ClearHighlights();
    }

    // ── Movement Range Display ────────────────────────────────────

    void ShowMovementRange(UnitController unit)
    {
        ClearHighlights();

        // Show movement range in blue
        List<HexCell> inRange = hexGrid.GetCellsInRange(
            unit.currentCell,
            unit.data.movementRange
        );

        foreach (HexCell cell in inRange)
        {
            if (!cell.isOccupied)
            {
                cell.SetHighlight(HexCell.colorMovement);
                highlightedCells.Add(cell);
            }

            // If enemy in movement range AND unit can charge → show as red
            if (cell.isOccupied && cell.occupyingUnit != null &&
                cell.occupyingUnit.faction != unit.faction &&
                unit.data.chargeDamage > 0)
            {
                cell.SetHighlight(HexCell.colorAttack);
                highlightedCells.Add(cell);
            }
        }

        // Fire range shown separately for ranged units
        if (unit.data.fireRange > 0)
        {
            List<HexCell> fireRange = hexGrid.GetCellsInRange(
                unit.currentCell,
                unit.data.fireRange
            );

            foreach (HexCell cell in fireRange)
            {
                if (cell.isOccupied && cell.occupyingUnit != null &&
                    cell.occupyingUnit.faction != unit.faction)
                {
                    cell.SetHighlight(HexCell.colorAttack);
                    highlightedCells.Add(cell);
                }
            }
        }
    }

    // ── Cell Click Handler ────────────────────────────────────────

    public void OnCellClicked(HexCell cell)
    {
        if (currentPhase != TurnPhase.Planning) return;

        // If cell has a friendly unit — select it
        if (cell.isOccupied && cell.occupyingUnit != null)
        {
            if (cell.occupyingUnit.faction == Faction.Player)
            {
                SelectUnit(cell.occupyingUnit);
                return;
            }

            // If cell has enemy unit and we have a unit selected — fire order
            if (selectedUnit != null && cell.isOccupied && 
                cell.occupyingUnit != null &&
                cell.occupyingUnit.faction == Faction.Enemy)
            {
                AssignBestCombatOrder(selectedUnit, cell.occupyingUnit);
                return;
            }
        }

        // If cell is empty and we have a unit selected — move order
        if (selectedUnit != null && !cell.isOccupied)
        {
            // Check cell is in movement range
            if (highlightedCells.Contains(cell))
            {
                AssignMoveOrder(cell);
            }
        }
    }

    // ── Order Assignment ──────────────────────────────────────────

    void AssignMoveOrder(HexCell targetCell)
    {
        selectedUnit.AssignMoveOrder(targetCell);

        // Show the assigned order visually
        targetCell.SetHighlight(HexCell.colorSelected);

        Debug.Log($"{selectedUnit.data.unitName} → Move to " +
                  $"({targetCell.q},{targetCell.r},{targetCell.s})");

        DeselectUnit();
    }

    void AssignFireOrder(UnitController target)
    {
        if (!selectedUnit.CanFireAt(target))
        {
            Debug.Log("Target out of range!");
            return;
        }

        selectedUnit.AssignFireOrder(target);
        target.currentCell.SetHighlight(HexCell.colorAttack);

        Debug.Log($"{selectedUnit.data.unitName} → Fire at {target.data.unitName}");

        DeselectUnit();
    }

    

    void AssignBestCombatOrder(UnitController attacker, UnitController target)
    {
        int distToTarget = attacker.currentCell.DistanceTo(target.currentCell);

        // Can fire at this range?
        if (attacker.data.fireRange > 0 && 
            distToTarget <= attacker.data.fireRange)
        {
            AssignFireOrder(target);
            return;
        }

        // Can charge? (cavalry + infantry with movement range)
        if (attacker.data.chargeDamage > 0 && 
            distToTarget <= attacker.data.movementRange)
        {
            AssignChargeOrder(target);
            return;
        }

        // Target out of range entirely
        Debug.Log($"{attacker.data.unitName} cannot reach " +
                $"{target.data.unitName} — too far!");
    }

    void AssignChargeOrder(UnitController target)
    {
        selectedUnit.AssignChargeOrder(target);
        target.currentCell.SetHighlight(HexCell.colorAttack);

        Debug.Log($"{selectedUnit.data.unitName} → CHARGE at " +
                $"{target.data.unitName}!");

        DeselectUnit();
    }

    // ── Highlight Management ──────────────────────────────────────

    void ClearHighlights()
    {
        foreach (HexCell cell in highlightedCells)
            if (cell != null) cell.ResetHighlight();

        highlightedCells.Clear();
    }
}


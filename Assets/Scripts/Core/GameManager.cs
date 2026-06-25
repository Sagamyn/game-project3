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

    [Header("AI")]
    public ThreatAssessment threatAssessment;

    [Header("UI")]
    public FormationUI formationUI;

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

    // Change private to public
public void KillSoldiersPublic(
    UnitController target,
    int count,
    UnitController attacker)
{
    KillSoldiers(target, count, attacker);
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
        if (ArmyMoraleManager.Instance != null &&
            ArmyMoraleManager.Instance.IsGameEnded()) return;

        currentPhase = TurnPhase.Execution;
        ClearHighlights();
        DeselectUnit();

        Debug.Log($"=== EXECUTING TURN {turnNumber} ===");

        // Resolve existing melees first
        MeleeManager.Instance?.ResolveMeleeEngagements(this);

        // Then resolve new orders
        ResolveMovement(battleManager.playerUnits);
        ResolveFire(battleManager.playerUnits);
        ResolveCharge(battleManager.playerUnits);

        ResolveAITurn();

        ClearAllOrders();
        turnNumber++;

        ArmyMoraleManager.Instance?.RecalculateArmyMorale();
        StartPlanningPhase();
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

        // Guard — target may have routed this turn
        if (target.isDefeated || target.isRouting)
        {
            Debug.Log($"{unit.data.unitName} fire cancelled " +
                      $"— target already routing");
            unit.ClearOrder();
            continue;
        }

        if (target.currentCell == null)
        {
            Debug.Log($"{unit.data.unitName} fire cancelled " +
                      $"— target cell null");
            unit.ClearOrder();
            continue;
        }

        if (!unit.CanFireAt(target))
        {
            Debug.Log($"{unit.data.unitName} fire cancelled — out of range");
            unit.ClearOrder();
            continue;
        }

        int damage = CalculateFireDamage(unit, target);

        Debug.Log($"{unit.data.unitName} fires at {target.data.unitName} " +
                  $"— {damage} morale damage");

        target.TakeMoraleDamage(damage, unit);

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

            if (target.isDefeated || target.isRouting)
            {
                unit.ClearOrder();
                continue;
            }

            if (target.currentCell == null || unit.currentCell == null)
            {
                unit.ClearOrder();
                continue;
            }

            int distToTarget = unit.currentCell.DistanceTo(target.currentCell);
            if (distToTarget > unit.data.movementRange)
            {
                Debug.Log($"{unit.data.unitName} charge cancelled — too far");
                unit.ClearOrder();
                continue;
            }

            // Calculate impact direction
            Vector3 impactDir = (
                target.transform.position -
                unit.transform.position
            ).normalized;

            // Start the full charge sequence
            // MarchIntoEnemy handles the rush animation now
            StartCoroutine(DelayedChargeImpact(unit, target, impactDir));
        }
    }

    System.Collections.IEnumerator DelayedChargeImpact(
    UnitController attacker,
    UnitController target,
    Vector3 impactDir)
    {
        if (attacker == null || target == null) yield break;
        if (target.isDefeated || target.isRouting) yield break;

        SoldierSpawner attackerSpawner =
            attacker.GetComponent<SoldierSpawner>();
        SoldierSpawner targetSpawner =
            target.GetComponent<SoldierSpawner>();

        // ── PHASE 1: Cavalry Gallops Toward Enemy ─────────────────
        Debug.Log("CHARGE: Cavalry galloping toward enemy...");

        bool cavalryArrived = false;

        attackerSpawner?.MarchIntoEnemy(target, () =>
        {
            cavalryArrived = true;
        });

        // Wait until cavalry soldiers reach enemy
        float timeout = 0f;
        while (!cavalryArrived && timeout < 3f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (attacker == null || target == null) yield break;
        if (target.isDefeated || target.isRouting) yield break;

        // ── PHASE 2: COLLISION ────────────────────────────────────
        Debug.Log("CHARGE: COLLISION!");

        // Apply charge damage
        int damage = CalculateChargeDamage(attacker, target);
        Debug.Log($"{attacker.data.unitName} COLLIDES with " +
                $"{target.data.unitName} — {damage} morale damage!");

        target.TakeMoraleDamage(damage, attacker);

        int soldiersKilled = Mathf.RoundToInt(damage / 8f);
        KillSoldiers(target, soldiersKilled, attacker);

        // ── PHASE 3: Knockback ────────────────────────────────────
        // Enemy soldiers fly outward from impact
        targetSpawner?.ApplyChargeImpact(impactDir);

        // Cavalry soldiers also bounce back slightly
        // (collision affects both sides)
        attackerSpawner?.ApplyChargeImpact(-impactDir * 0.4f);

        // Move cavalry unit root to adjacent cell
        if (target.currentCell != null)
        {
            HexCell dest = GetCellAdjacentTo(attacker, target);
            if (dest != null)
                attacker.MoveToCell(dest);
        }

        // ── PHASE 4: Settle ───────────────────────────────────────
        // Let knockback animation play out
        yield return new WaitForSeconds(0.6f);

        if (attacker == null || target == null) yield break;
        if (attacker.isRouting || target.isRouting) yield break;
        if (attacker.isDefeated || target.isDefeated) yield break;

        // ── PHASE 5: Melee Begins ─────────────────────────────────
        Debug.Log("CHARGE: Melee begins!");
        MeleeManager.Instance?.StartMelee(attacker, target);
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
        // Guard against null cells
        if (mover == null || target == null) return null;
        if (mover.currentCell == null || target.currentCell == null) return null;

        List<HexCell> adjacentCells = hexGrid.GetCellsInRange(
            target.currentCell, 1
        );

        HexCell bestCell  = null;
        int     bestDist  = int.MaxValue;

        foreach (HexCell cell in adjacentCells)
        {
            if (cell == null) continue;
            if (cell.isOccupied) continue;

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
        List<SoldierCircle> living = new List<SoldierCircle>();
        foreach (SoldierCircle sc in target.soldiers)
        {
            if (sc != null && sc.state == SoldierState.Alive)
                living.Add(sc);
        }

        // Shuffle for random deaths
        for (int i = living.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (living[i], living[j]) = (living[j], living[i]);
        }

        int killed = 0;
        foreach (SoldierCircle soldier in living)
        {
            if (killed >= count) break;

            // Capture world position
            Vector3 worldPos = soldier.transform.position;

            // Detach preserving world position
            soldier.transform.SetParent(null, true);
            soldier.transform.position = worldPos;

            // Set dead state — this now stops all movement
            soldier.SetState(SoldierState.Dead);

            // Force stop any remaining velocity
            soldier.StopAllMovement();

            killed++;
            target.aliveSoldierCount--;
        }

        Debug.Log($"{killed} soldiers killed in {target.data.unitName}");
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
    if (threatAssessment != null)
    {
        // Use smart AI
        threatAssessment.ExecuteAITurn();

        // Resolve movement and fire for AI units
        ResolveMovement(battleManager.enemyUnits);
        ResolveFire(battleManager.enemyUnits);
        ResolveCharge(battleManager.enemyUnits);
    }
    else
    {
        // Fallback to old simple AI if not wired up
        Debug.LogWarning("ThreatAssessment not assigned — using simple AI");
        SimpleAIFallback();
    }
}

void SimpleAIFallback()
{
    foreach (UnitController aiUnit in battleManager.enemyUnits)
    {
        if (aiUnit == null || aiUnit.isDefeated || aiUnit.isRouting) continue;
        if (aiUnit.currentCell == null) continue;

        UnitController nearest = FindNearestEnemy(
            aiUnit, battleManager.playerUnits
        );

        if (nearest == null) continue;

        int dist = aiUnit.currentCell.DistanceTo(nearest.currentCell);

        if (aiUnit.data.fireRange > 0 && dist <= aiUnit.data.fireRange)
            aiUnit.AssignFireOrder(nearest);
        else if (dist > 1)
        {
            HexCell cell = GetCellToward(aiUnit, nearest);
            if (cell != null) aiUnit.AssignMoveOrder(cell);
        }
    }

    ResolveMovement(battleManager.enemyUnits);
    ResolveFire(battleManager.enemyUnits);
    ResolveCharge(battleManager.enemyUnits);
}

    UnitController FindNearestEnemy(
    UnitController unit,
    List<UnitController> enemies)
    {
        UnitController nearest     = null;
        int            nearestDist = int.MaxValue;

        foreach (UnitController enemy in enemies)
        {
            // Skip routed, defeated, or null cell units
            if (enemy.isDefeated || enemy.isRouting) continue;
            if (enemy.currentCell == null) continue;

            int dist = unit.currentCell.DistanceTo(enemy.currentCell);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest     = enemy;
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
    ClearOrdersForArmy(battleManager.playerUnits);
    ClearOrdersForArmy(battleManager.enemyUnits);
}

    void ClearOrdersForArmy(List<UnitController> units)
    {
        foreach (UnitController unit in units)
        {
            if (unit == null || unit.isDefeated || unit.isRouting) continue;

            // Don't clear orders for units in melee
            // their "order" is the melee itself
            if (MeleeManager.Instance != null &&
                MeleeManager.Instance.IsInMelee(unit))
                continue;

            if (unit.persistOrder)
            {
                if (unit.currentOrder == OrderType.Fire ||
                    unit.currentOrder == OrderType.Charge)
                {
                    if (unit.orderTargetUnit == null ||
                        unit.orderTargetUnit.isDefeated ||
                        unit.orderTargetUnit.isRouting)
                    {
                        unit.ClearOrder();
                    }
                }
                continue;
            }

            unit.ClearOrder();
        }
    }


    // ── Selection ─────────────────────────────────────────────────
    public void SelectUnit(UnitController unit)
    {
        if (currentPhase != TurnPhase.Planning) return;
        if (unit.faction != Faction.Player) return;
        if (unit.isRouting || unit.isDefeated) return;

        // Units in melee can't receive orders
        if (MeleeManager.Instance != null &&
            MeleeManager.Instance.IsInMelee(unit))
        {
            Debug.Log($"{unit.data.unitName} is in melee — no orders!");
            return;
        }

        DeselectUnit();
        selectedUnit = unit;
        unit.SetSelected(true);
        ShowMovementRange(unit);
        formationUI?.ShowForUnit(unit);

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

        // Hide formation panel
        formationUI?.Hide();
    }

    // ── Movement Range Display ────────────────────────────────────

    void ShowMovementRange(UnitController unit)
    {
        ClearHighlights();

        // Don't show movement range if in square formation
        if (unit.currentFormation == FormationType.Square)
        {
            Debug.Log($"{unit.data.unitName} is in Square — cannot move");

            // Still show fire range
            if (unit.data.fireRange > 0)
                ShowFireRange(unit);

            return;
        }

        // Normal movement range
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

            if (cell.isOccupied && cell.occupyingUnit != null &&
                cell.occupyingUnit.faction != unit.faction &&
                unit.data.chargeDamage > 0)
            {
                cell.SetHighlight(HexCell.colorAttack);
                highlightedCells.Add(cell);
            }
        }

        // Show fire range
        if (unit.data.fireRange > 0)
            ShowFireRange(unit);
    }

    void ShowFireRange(UnitController unit)
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
        // Block movement if in square formation
        if (selectedUnit.currentFormation == FormationType.Square)
        {
            Debug.Log("Cannot move in Square formation!");

            // Blink the Line Formation button to tell player
            formationUI?.BlinkLineButton();
            return;
        }

        selectedUnit.AssignMoveOrder(targetCell);
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


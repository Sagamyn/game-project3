using UnityEngine;
using System.Collections.Generic;

public class UnitController : MonoBehaviour
{
    [Header("Unit Identity")]
    public UnitData data;
    public Faction faction;
    public int unitID;

    [Header("Battlefield State")]
    public HexCell currentCell;
    public FormationType currentFormation;
    public OrderType currentOrder;
    public HexCell orderTargetCell;
    public UnitController orderTargetUnit;

    [Header("Morale")]
    public int currentMorale;
    public MoraleState moraleState;
    public bool isRouting;
    public bool isDefeated;

    [Header("Soldiers")]
    public List<SoldierCircle> soldiers = new List<SoldierCircle>();
    public int aliveSoldierCount;

    [Header("Visual")]
    public GameObject soldierContainer;
    public SpriteRenderer selectionRing;

    // ── Events other systems listen to ──────────────────────────
    public System.Action<UnitController> OnMoraleChanged;
    public System.Action<UnitController> OnUnitRouted;
    public System.Action<UnitController> OnUnitDefeated;

    // ── Private ─────────────────────────────────────────────────
    private HexGrid hexGrid;

    void Awake()
    {
        hexGrid = FindObjectOfType<HexGrid>();
    }

    public void Initialize(UnitData unitData, Faction unitFaction, HexCell spawnCell)
    {
        data            = unitData;
        faction         = unitFaction;
        currentCell     = spawnCell;
        currentMorale   = data.startingMorale;
        moraleState     = MoraleState.Steady;
        currentFormation = FormationType.Line;
        isRouting       = false;
        isDefeated      = false;
        aliveSoldierCount = data.soldierCount;

        // Mark cell as occupied
        spawnCell.isOccupied    = true;
        spawnCell.occupyingUnit = this;

        // Position this GameObject on the cell
        transform.position = spawnCell.transform.position;
        //to disable selection ring at start
        if (selectionRing != null)
        selectionRing.enabled = false;

        UpdateMoraleState();
    }

    // ── Morale System ────────────────────────────────────────────

    public void TakeMoraleDamage(int amount, UnitController source)
    {
        if (isDefeated) return;

        currentMorale = Mathf.Max(0, currentMorale - amount);
        UpdateMoraleState();
        OnMoraleChanged?.Invoke(this);

        if (currentMorale <= 0)
            StartRouting();
    }

    void UpdateMoraleState()
    {
        MoraleState previous = moraleState;

        if      (currentMorale >= 75) moraleState = MoraleState.Steady;
        else if (currentMorale >= 50) moraleState = MoraleState.Shaken;
        else if (currentMorale >= 25) moraleState = MoraleState.Wavering;
        else                          moraleState = MoraleState.Breaking;

        // If state changed, update soldier visuals
        if (moraleState != previous)
            UpdateFormationTightness();
    }

    void UpdateFormationTightness()
    {
        // Soldiers spread out as morale drops
        // SoldierSpawner handles this — we just signal it
        GetComponent<SoldierSpawner>()?.RefreshFormation(moraleState);
    }

    public void StartRouting()
    {
        if (isRouting) return;
        isRouting = true;
        OnUnitRouted?.Invoke(this);

        // Free the current cell
        if (currentCell != null)
        {
            currentCell.isOccupied    = false;
            currentCell.occupyingUnit = null;
        }
    }

    // ── Order Assignment (Planning Phase) ────────────────────────

    public void AssignMoveOrder(HexCell targetCell)
    {
        currentOrder     = OrderType.Move;
        orderTargetCell  = targetCell;
        orderTargetUnit  = null;
    }

    public void AssignFireOrder(UnitController target)
    {
        currentOrder     = OrderType.Fire;
        orderTargetUnit  = target;
        orderTargetCell  = null;
    }

    public void AssignChargeOrder(UnitController target)
    {
        currentOrder     = OrderType.Charge;
        orderTargetUnit  = target;
        orderTargetCell  = target.currentCell;
    }

    public void AssignHoldOrder()
    {
        currentOrder     = OrderType.Hold;
        orderTargetCell  = null;
        orderTargetUnit  = null;
    }



    public void ClearOrder()
    {
        currentOrder     = OrderType.None;
        orderTargetCell  = null;
        orderTargetUnit  = null;
    }

    // ── Movement ─────────────────────────────────────────────────

    public void MoveToCell(HexCell targetCell, System.Action onComplete = null)
    {
        if (currentCell != null)
        {
            currentCell.isOccupied    = false;
            currentCell.occupyingUnit = null;
        }

        currentCell              = targetCell;
        targetCell.isOccupied    = true;
        targetCell.occupyingUnit = this;

        Vector3 newWorldPos = targetCell.transform.position;

        // Step 1: Capture BEFORE moving root
        SoldierSpawner spawner = GetComponent<SoldierSpawner>();
        List<Vector3> startPositions = spawner?.CaptureSoldierWorldPositions();

        // Step 2: Move root
        transform.position = newWorldPos;

        // Step 3: March — note the explicit parameter names
        spawner?.MarchToHex(
            newHexWorldPos:       newWorldPos,
            soldierStartPositions: startPositions,
            onAllArrived:         () => { onComplete?.Invoke(); }
        );
    }

    // Also fix AssignFormationChange — remove AnimateToFormation call
    public void AssignFormationChange(FormationType newFormation)
    {
        currentOrder     = OrderType.ChangeFormation;
        currentFormation = newFormation;

        // Use RefreshFormation instead
        GetComponent<SoldierSpawner>()?.RefreshFormation(moraleState);
    }

    // ── Selection Visual ─────────────────────────────────────────

    public void SetSelected(bool selected)
    {
        if (selectionRing != null)
            selectionRing.enabled = selected;
        else
            Debug.LogWarning("SelectionRing is not assigned on " + data.unitName);
    }

    // ── Utility ──────────────────────────────────────────────────

    public bool CanFireAt(UnitController target)
    {
        if (data.fireRange == 0) return false;
        return currentCell.DistanceTo(target.currentCell) <= data.fireRange;
    }

    public bool CanCharge(UnitController target)
    {
        return currentCell.DistanceTo(target.currentCell) <= data.movementRange;
    }

    public bool IsEnemy(UnitController other)
    {
        return other.faction != faction;
    }
}


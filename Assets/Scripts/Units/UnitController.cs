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

    [Header("UI")]
    public GameObject moraleBarPrefab;
    private MoraleBar moraleBar;

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

    [Header("Orders")]
    public bool persistOrder = false; // if true, repeat order next turn

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

        if (moraleBarPrefab != null)
        {
            GameObject barObj = Instantiate(
                moraleBarPrefab,
                transform
            );
            moraleBar = barObj.GetComponent<MoraleBar>();
            moraleBar.Initialize(data.startingMorale);
        }
    }

    // ── Morale System ────────────────────────────────────────────

    public void TakeMoraleDamage(int amount, UnitController source)
    {
        if (isDefeated) return;

        currentMorale = Mathf.Max(0, currentMorale - amount);

        // Update morale bar visual
        moraleBar?.UpdateMorale(currentMorale);

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

        Debug.Log($"{data.unitName} is ROUTING! " +
                $"ArmyMoraleManager exists: " +
                $"{ArmyMoraleManager.Instance != null}");

        moraleBar?.UpdateMorale(0);

        if (currentCell != null)
        {
            currentCell.isOccupied    = false;
            currentCell.occupyingUnit = null;
            currentCell               = null;
        }

        // Notify army morale system
        if (ArmyMoraleManager.Instance != null)
            ArmyMoraleManager.Instance.OnUnitRouted(this);
        else
            Debug.LogError("ArmyMoraleManager.Instance is NULL " +
                        "— routing not counted!");

        OnUnitRouted?.Invoke(this);
        StartCoroutine(RoutingSequence());
    }

 System.Collections.IEnumerator RoutingSequence()
{
    SoldierSpawner spawner = GetComponent<SoldierSpawner>();

    // Collect living soldiers with their CURRENT world positions
    // captured RIGHT NOW before anything moves
    List<(SoldierCircle sc, Vector3 worldPos)> livingSoldiers =
        new List<(SoldierCircle, Vector3)>();

    foreach (SoldierCircle sc in soldiers)
    {
        if (sc == null) continue;
        if (sc.state == SoldierState.Dead) continue;

        // Capture world position immediately
        livingSoldiers.Add((sc, sc.transform.position));
    }

    // Blink loop — only changes COLOR, never touches transform
    float blinkDuration = 5f;
    float blinkSpeed    = 0.3f;
    float elapsed       = 0f;
    bool  isWhite       = false;

    while (elapsed < blinkDuration)
    {
        isWhite = !isWhite;

        foreach (var (sc, _) in livingSoldiers)
        {
            if (sc == null) continue;
            SpriteRenderer sr = sc.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            sr.color = isWhite
                ? Color.white
                : (faction == Faction.Player
                    ? sc.playerAlive
                    : sc.enemyAlive);
        }

        yield return new WaitForSeconds(blinkSpeed);
        elapsed += blinkSpeed;
    }

    // Reset colors
    foreach (var (sc, _) in livingSoldiers)
    {
        if (sc != null) sc.SetState(SoldierState.Alive);
    }

    // NOW flee — soldiers detach from here with correct positions
    Vector3 enemyDirection = GetEnemyDirection();
    if (spawner != null)
        FleeOffMap(transform.position, enemyDirection);

    yield return new WaitForSeconds(3.0f);

    isDefeated = true;
    OnUnitDefeated?.Invoke(this);

    if (moraleBar != null)
        moraleBar.gameObject.SetActive(false);

    Destroy(gameObject);
}

    Vector3 GetEnemyDirection()
    {
        // Player units flee downward (away from enemy at top)
        // Enemy units flee upward (away from player at bottom)
        if (faction == Faction.Player)
            return new Vector3(0, -1, 0); // flee south

        return new Vector3(0, 1, 0); // flee north
    }

    public void FleeOffMap(Vector3 unitWorldPos, Vector3 enemyDirection)
    {
        // Flee direction = away from enemy
        Vector3 fleeDir = -enemyDirection.normalized;

        // Stagger each soldier's flee start
        // so they don't all bolt at exactly the same time
        for (int i = 0; i < soldiers.Count; i++)
        {
            SoldierCircle sc = soldiers[i];
            if (sc == null) continue;
            if (sc.state == SoldierState.Dead) continue;

            // Random delay before each soldier starts fleeing
            float delay = UnityEngine.Random.Range(0f, 0.4f);

            // Random flee speed — panic isn't uniform
            float speed = UnityEngine.Random.Range(0.1f, 0.3f);

            StartCoroutine(DelayedFlee(sc, fleeDir, speed, delay));
        }
    }

    System.Collections.IEnumerator DelayedFlee(
        SoldierCircle soldier,
        Vector3 direction,
        float speed,
        float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);

        if (soldier != null)
            soldier.FleeOffMap(direction, speed);
    }

    // ── Order Assignment (Planning Phase) ────────────────────────

    public void AssignMoveOrder(HexCell targetCell)
    {
        currentOrder     = OrderType.Move;
        orderTargetCell  = targetCell;
        orderTargetUnit  = null;
        persistOrder     = false;

    }

    public void AssignFireOrder(UnitController target)
    {
        currentOrder     = OrderType.Fire;
        orderTargetUnit  = target;
        orderTargetCell  = null;
        persistOrder     = true;
    }

    public void AssignChargeOrder(UnitController target)
    {
        currentOrder     = OrderType.Charge;
        orderTargetUnit  = target;
        orderTargetCell  = target.currentCell;
        persistOrder     = true;
    }

    public void AssignHoldOrder()
    {
        currentOrder     = OrderType.Hold;
        orderTargetCell  = null;
        orderTargetUnit  = null;
        persistOrder     = false;
    }



    public void ClearOrder()
    {
        currentOrder     = OrderType.None;
        orderTargetCell  = null;
        orderTargetUnit  = null;
        persistOrder     = false;
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


using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SoldierSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject soldierPrefab;
    public UnitController unit;

    [Header("Spacing")]
    public float soldierSpacing = 0.18f;

    [Header("March Settings")]
    public float staggerDelay = 0.02f; // delay between each soldier starting

    private List<SoldierCircle> spawnedSoldiers = new List<SoldierCircle>();
    private int soldiersArrived = 0;

    // ── Spawn ─────────────────────────────────────────────────────

    public void SpawnSoldiers()
    {
        if (unit == null)
        {
            Debug.LogError("SoldierSpawner: unit is NULL!");
            return;
        }
        if (unit.data == null)
        {
            Debug.LogError("SoldierSpawner: unit.data is NULL!");
            return;
        }
        if (soldierPrefab == null)
        {
            Debug.LogError("SoldierSpawner: soldierPrefab is NULL!");
            return;
        }

        foreach (var s in spawnedSoldiers)
            if (s != null) Destroy(s.gameObject);

        spawnedSoldiers.Clear();
        unit.soldiers.Clear();

        List<Vector2> positions = GetFormationPositions(
            unit.data.unitType,
            unit.data.soldierCount,
            FormationType.Line
        );

        Transform container = unit.soldierContainer != null
            ? unit.soldierContainer.transform
            : transform;

        for (int i = 0; i < unit.data.soldierCount; i++)
        {
            if (i >= positions.Count) break;

            GameObject obj = Instantiate(soldierPrefab, container);

            float scale = unit.data.soldierCircleSize;
            obj.transform.localScale = new Vector3(scale, scale, 1);

            SoldierCircle sc = obj.GetComponent<SoldierCircle>();
            if (sc == null)
            {
                Debug.LogError("SoldierCircle component missing!");
                continue;
            }

            // Snap into position on spawn — no march animation
            sc.SnapTo(new Vector3(positions[i].x, positions[i].y, 0));
            sc.Initialize(unit.faction);

            spawnedSoldiers.Add(sc);
            unit.soldiers.Add(sc);
        }

        Debug.Log($"Spawned {spawnedSoldiers.Count} soldiers " +
                  $"for {unit.data.unitName}");
    }

    // ── March To New Hex ──────────────────────────────────────────

    // Called when unit moves to a new hex
    // newHexWorldPos = world position of destination hex
    public void MarchToHex(
    Vector3 newHexWorldPos,
    List<Vector3> soldierStartPositions,  // ← pre-captured
    System.Action onAllArrived = null)
    {
        List<Vector2> formationPositions = GetFormationPositions(
            unit.data.unitType,
            unit.data.soldierCount,
            unit.currentFormation
        );

        float spread = GetMoraleSpread(unit.moraleState);

        soldiersArrived = 0;
        int activeSoldiers = 0;

        Transform container = unit.soldierContainer != null
            ? unit.soldierContainer.transform
            : transform;

        for (int i = 0; i < spawnedSoldiers.Count; i++)
        {
            SoldierCircle sc = spawnedSoldiers[i];
            if (sc == null) continue;
            if (sc.state == SoldierState.Dead) continue;
            if (i >= formationPositions.Count) break;

            activeSoldiers++;

            Vector2 localOffset = formationPositions[i] * spread;

            Vector3 worldDest = newHexWorldPos + new Vector3(
                localOffset.x,
                localOffset.y,
                0
            );

            Vector3 localDest = new Vector3(
                localOffset.x,
                localOffset.y,
                0
            );

            // Use pre-captured start position
            Vector3 startPos = (soldierStartPositions != null && i < soldierStartPositions.Count)
                ? soldierStartPositions[i]
                : sc.transform.position;

            float minDelay, maxDelay, minSpeed, maxSpeed;

            switch (unit.data.unitType)
            {
                case UnitType.Cavalry:
                    minDelay = 0f;   maxDelay = 0.08f;
                    minSpeed = 3.0f; maxSpeed = 4.5f;
                    break;
                case UnitType.Skirmisher:
                    minDelay = 0f;   maxDelay = 0.4f;
                    minSpeed = 2.0f; maxSpeed = 3.5f;
                    break;
                case UnitType.Artillery:
                    minDelay = 0f;   maxDelay = 0.2f;
                    minSpeed = 0.8f; maxSpeed = 1.2f;
                    break;
                default: // LineInfantry
                    minDelay = 0f;   maxDelay = 0.15f;
                    minSpeed = 1.5f; maxSpeed = 2.0f;
                    break;
            }

            float randomDelay = UnityEngine.Random.Range(minDelay, maxDelay);
            float randomSpeed = UnityEngine.Random.Range(minSpeed, maxSpeed);

            StartCoroutine(StaggeredMarch(
                sc,
                startPos,    // ← pre-captured world position
                worldDest,
                localDest,
                container,
                randomDelay,
                randomSpeed,
                () =>
                {
                    soldiersArrived++;
                    if (soldiersArrived >= activeSoldiers)
                        onAllArrived?.Invoke();
                }
            ));
        }

        if (activeSoldiers == 0)
            onAllArrived?.Invoke();
    }

    IEnumerator StaggeredMarch(
    SoldierCircle soldier,
    Vector3 worldStart,
    Vector3 worldDest,
    Vector3 localDest,
    Transform container,
    float delay,
    float speed,
    System.Action onArrived)
    {
        // Detach immediately so world position is preserved
        soldier.transform.SetParent(null);

        // Force to captured start position RIGHT NOW
        // before any delay
        soldier.transform.position = worldStart;

        if (delay > 0)
            yield return new WaitForSeconds(delay);

        if (soldier == null) yield break;

        soldier.OnArrived = onArrived;
        soldier.MarchTo(
            worldDest,
            localDest,
            container,
            customMarchSpeed: speed,
            customFormSpeed:  speed * 1.5f
        );
    }

    // ── Formation Refresh (morale change, no hex change) ─────────

   public void RefreshFormation(MoraleState moraleState)
    {
        List<Vector2> positions = GetFormationPositions(
            unit.data.unitType,
            unit.data.soldierCount,
            unit.currentFormation
        );

        float spread = GetMoraleSpread(moraleState);

        for (int i = 0; i < spawnedSoldiers.Count; i++)
        {
            if (spawnedSoldiers[i] == null) continue;
            if (spawnedSoldiers[i].state == SoldierState.Dead) continue;
            if (i >= positions.Count) break;

            Vector2 targetPos = positions[i] * spread;
            Vector3 localDest = new Vector3(targetPos.x, targetPos.y, 0);

            // Smooth drift into new formation — no snap
            spawnedSoldiers[i].FormUpTo(localDest);
        }
    }

    IEnumerator StaggeredLocalMove(
        SoldierCircle soldier,
        Vector3 localDest,
        float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);

        if (soldier != null)
            soldier.SnapTo(localDest); // instant for morale shifts
    }

    public List<Vector3> CaptureSoldierWorldPositions()
    {
        List<Vector3> positions = new List<Vector3>();

        foreach (SoldierCircle sc in spawnedSoldiers)
        {
            if (sc == null)
            {
                positions.Add(Vector3.zero);
                continue;
            }
            // Capture TRUE world position right now
            positions.Add(sc.transform.position);
        }

        return positions;
    }

    // ── Helpers ───────────────────────────────────────────────────

    float GetMoraleSpread(MoraleState moraleState)
    {
        return moraleState switch
        {
            MoraleState.Steady   => 1.0f,
            MoraleState.Shaken   => 1.2f,
            MoraleState.Wavering => 1.5f,
            MoraleState.Breaking => 2.0f,
            _                    => 1.0f
        };
    }

    // ── Formation Layouts ─────────────────────────────────────────

    List<Vector2> GetFormationPositions(
        UnitType type,
        int count,
        FormationType formation)
    {
        return type switch
        {
            UnitType.LineInfantry => formation switch
            {
                FormationType.Line    => GetLinePositions(count, 8),
                FormationType.Column  => GetLinePositions(count, 2),
                FormationType.Square  => GetSquarePositions(count),
                _                     => GetLinePositions(count, 8)
            },
            UnitType.Skirmisher   => GetScatteredPositions(count),
            UnitType.Cavalry      => formation switch
            {
                FormationType.Line   => GetLinePositions(count, 8),
                FormationType.Column => GetLinePositions(count, 2),
                _                    => GetLinePositions(count, 8)
            },
            UnitType.Artillery    => GetArtilleryPositions(count),
            _                     => GetLinePositions(count, 8)
        };
    }

    List<Vector2> GetLinePositions(int count, int columns)
    {
        List<Vector2> pos = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            int col = i % columns;
            int row = i / columns;
            pos.Add(new Vector2(
                (col - columns / 2f + 0.5f) * soldierSpacing,
                (row - 0.5f) * soldierSpacing
            ));
        }
        return pos;
    }

    List<Vector2> GetSquarePositions(int count)
    {
        List<Vector2> pos = new List<Vector2>();
        int side = Mathf.CeilToInt(Mathf.Sqrt(count));

        for (int i = 0; i < count; i++)
        {
            int col = i % side;
            int row = i / side;
            bool onEdge = col == 0 || col == side - 1
                       || row == 0 || row == side - 1;

            if (onEdge)
                pos.Add(new Vector2(
                    (col - side / 2f + 0.5f) * soldierSpacing,
                    (row - side / 2f + 0.5f) * soldierSpacing
                ));
        }
        return pos;
    }

    List<Vector2> GetScatteredPositions(int count)
    {
        List<Vector2> pos = new List<Vector2>();
        UnityEngine.Random.InitState(unit.GetInstanceID());

        for (int i = 0; i < count; i++)
            pos.Add(new Vector2(
                UnityEngine.Random.Range(-0.4f, 0.4f),
                UnityEngine.Random.Range(-0.3f, 0.3f)
            ));

        return pos;
    }

    List<Vector2> GetArtilleryPositions(int count)
    {
        return new List<Vector2>
        {
            new Vector2(-0.15f,  0.15f),
            new Vector2( 0.15f,  0.15f),
            new Vector2(-0.15f, -0.15f),
            new Vector2( 0.15f, -0.15f),
        };
    }
}
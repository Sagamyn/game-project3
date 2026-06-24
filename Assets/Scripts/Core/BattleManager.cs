using UnityEngine;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    public HexGrid hexGrid;
    public GameObject unitPrefab;

    [Header("Unit Data")]
    public UnitData lineInfantryData;
    public UnitData skirmisherData;
    public UnitData cavalryData;
    public UnitData artilleryData;

    [Header("Soldiers")]
    public GameObject soldierPrefab;

    // Tracked units
    public List<UnitController> playerUnits = new List<UnitController>();
    public List<UnitController> enemyUnits  = new List<UnitController>();

    void Start()
    {
        SpawnArmy(Faction.Player);
        SpawnArmy(Faction.Enemy);
    }

    
    void SpawnArmy(Faction faction)
    {
        List<UnitData> composition = new List<UnitData>
        {
            lineInfantryData,
            lineInfantryData,
            skirmisherData,
            cavalryData,
            artilleryData
        };

        List<HexCell> spawnCells = GetDeploymentZone(faction);

        // ADD THIS — tells us exactly what's happening
        Debug.Log($"[{faction}] Deployment zone found {spawnCells.Count} cells");

        if (spawnCells.Count == 0)
        {
            Debug.LogError($"[{faction}] NO SPAWN CELLS FOUND — check GetDeploymentZone");
            return;
        }

        for (int i = 0; i < composition.Count; i++)
        {
            if (i >= spawnCells.Count)
            {
                Debug.LogError($"Not enough spawn cells! Only {spawnCells.Count} available");
                break;
            }

            HexCell cell = spawnCells[i];
            if (cell == null)
            {
                Debug.LogError($"Cell {i} is null!");
                continue;
            }

            if (cell.isOccupied)
            {
                Debug.LogWarning($"Cell {i} already occupied, skipping");
                continue;
            }

            Debug.Log($"Spawning {composition[i].unitName} for {faction} at cell ({cell.q},{cell.r},{cell.s})");

            GameObject unitObj = Instantiate(unitPrefab);
            UnitController unit = unitObj.GetComponent<UnitController>();

            if (unit == null)
            {
                Debug.LogError("UnitController not found on unitPrefab!");
                continue;
            }

            SoldierSpawner spawner = unitObj.GetComponent<SoldierSpawner>();
            if (spawner == null)
            {
                Debug.LogError("SoldierSpawner not found on unitPrefab!");
                continue;
            }

            spawner.soldierPrefab = soldierPrefab;
            spawner.unit          = unit;

            unit.Initialize(composition[i], faction, cell);
            spawner.SpawnSoldiers();

            if (faction == Faction.Player)
                playerUnits.Add(unit);
            else
                enemyUnits.Add(unit);
        }
    }

    List<HexCell> GetDeploymentZone(Faction faction)
    {
        List<HexCell> zone = new List<HexCell>();

        // Ask HexGrid for ALL cells then filter by world position
        // This avoids coordinate conversion issues entirely
        HexCell[] allCells = FindObjectsOfType<HexCell>();

        Debug.Log($"Total cells found in scene: {allCells.Length}");

        // Sort cells by Y position to find top and bottom rows
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (HexCell cell in allCells)
        {
            float y = cell.transform.position.y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        float gridHeight = maxY - minY;

        // Player = bottom 25% of grid, Enemy = top 25% of grid
        float playerThreshold = minY + gridHeight * 0.30f;
        float enemyThreshold  = maxY - gridHeight * 0.30f;

        foreach (HexCell cell in allCells)
        {
            float y = cell.transform.position.y;

            bool inPlayerZone = y <= playerThreshold;
            bool inEnemyZone  = y >= enemyThreshold;

            if (faction == Faction.Player && inPlayerZone && !cell.isOccupied)
                zone.Add(cell);
            else if (faction == Faction.Enemy && inEnemyZone && !cell.isOccupied)
                zone.Add(cell);
        }

        Debug.Log($"[{faction}] Zone has {zone.Count} valid cells");

        // Shuffle for random placement
        for (int i = zone.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (zone[i], zone[j]) = (zone[j], zone[i]);
        }

        return zone;
    }
}
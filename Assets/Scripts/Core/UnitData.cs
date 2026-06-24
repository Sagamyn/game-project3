using UnityEngine;

[CreateAssetMenu(fileName = "NewUnitData", menuName = "UnitData")]
public class UnitData : ScriptableObject
{
    [Header("Identity")]
    public string unitName;
    public UnitType unitType;

    [Header("Soldiers")]
    public int soldierCount;        // 24, 12, 16, or 4
    public float soldierCircleSize; // 0.15f infantry, 0.22f cavalry

    [Header("Movement")]
    public int movementRange;       // hexes per turn

    [Header("Combat")]
    public int fireRange;           // hexes (0 = melee only)
    public int fireDamage;          // morale damage per volley
    public int chargeDamage;        // morale damage on charge

    [Header("Morale")]
    public int startingMorale;      // always 100 to start
    public int witnessEffectRadius; // soldiers affected by nearby death

    [Header("Formation")]
    public bool canFormSquare;      // infantry only
    public bool canSkirmish;        // skirmisher only
}


using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MeleeManager : MonoBehaviour
{
    public static MeleeManager Instance;

    [Header("Melee Settings")]
    public float meleeCasualtiesPerTurn = 0.3f;
    public float meleeEndMoraleThreshold = 20f;


    // Active melees
    private List<MeleeEngagement> activeEngagements
        = new List<MeleeEngagement>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ── Start A Melee ─────────────────────────────────────────────

    public void StartMelee(UnitController attacker, UnitController defender)
    {
        // Check not already in melee together
        foreach (MeleeEngagement eng in activeEngagements)
        {
            if ((eng.attacker == attacker && eng.defender == defender) ||
                (eng.attacker == defender && eng.defender == attacker))
                return;
        }

        Debug.Log($"MELEE: {attacker.data.unitName} vs " +
                  $"{defender.data.unitName}");

        MeleeEngagement engagement = new MeleeEngagement(
            attacker, defender
        );
        activeEngagements.Add(engagement);

        // Put all soldiers of both units into melee state
        EnterMeleeState(attacker);
        EnterMeleeState(defender);
    }

    void EnterMeleeState(UnitController unit)
    {
        foreach (SoldierCircle sc in unit.soldiers)
        {
            if (sc == null) continue;
            if (sc.state == SoldierState.Dead) continue;
            sc.EnterMelee();
        }
    }

    // ── Resolve Melee Each Turn ───────────────────────────────────

    public void ResolveMeleeEngagements(GameManager gameManager)
    {
        if (activeEngagements.Count == 0) return;

        List<MeleeEngagement> toRemove = new List<MeleeEngagement>();

        foreach (MeleeEngagement eng in activeEngagements)
        {
            if (!eng.isActive)
            {
                toRemove.Add(eng);
                continue;
            }

            if (!IsEngagementValid(eng))
            {
                Debug.Log("Melee engagement invalid — cleaning up");
                EndMelee(eng);
                toRemove.Add(eng);
                continue;
            }

            // Resolve this turn's melee casualties
            ResolveMeleeTurn(eng, gameManager);

            // Check if melee should end
            if (ShouldMeleeEnd(eng))
            {
                EndMelee(eng);
                toRemove.Add(eng);
            }
            else
            {
                Debug.Log($"Melee continues: " +
                        $"{eng.attacker.data.unitName} " +
                        $"({eng.attacker.currentMorale}) vs " +
                        $"{eng.defender.data.unitName} " +
                        $"({eng.defender.currentMorale})");
            }
        }

        foreach (MeleeEngagement eng in toRemove)
            activeEngagements.Remove(eng);
    }

    bool IsEngagementValid(MeleeEngagement eng)
    {
        if (eng.attacker == null || eng.defender == null) return false;
        if (eng.attacker.isDefeated || eng.attacker.isRouting) return false;
        if (eng.defender.isDefeated || eng.defender.isRouting) return false;
        return true;
    }

    void ResolveMeleeTurn(MeleeEngagement eng, GameManager gameManager)
    {
        // Both sides take morale damage
        int attackerDamage = Mathf.RoundToInt(
            eng.defender.data.fireDamage * 0.5f
        );
        int defenderDamage = Mathf.RoundToInt(
            eng.attacker.data.chargeDamage * 0.3f
        );

        // Attacker (cavalry) also takes some damage — melee hurts both
        eng.attacker.TakeMoraleDamage(attackerDamage, eng.defender);
        eng.defender.TakeMoraleDamage(defenderDamage, eng.attacker);

        // Kill some soldiers on both sides
        int attackerKills = Mathf.RoundToInt(meleeCasualtiesPerTurn);
        int defenderKills = Mathf.RoundToInt(meleeCasualtiesPerTurn * 1.5f);

        gameManager.KillSoldiersPublic(eng.attacker, attackerKills, eng.defender);
        gameManager.KillSoldiersPublic(eng.defender, defenderKills, eng.attacker);

        Debug.Log($"MELEE turn — {eng.attacker.data.unitName} " +
                  $"vs {eng.defender.data.unitName} " +
                  $"continuing...");
    }

    bool ShouldMeleeEnd(MeleeEngagement eng)
    {
        // Melee ends when one side is nearly broken
        return eng.attacker.currentMorale <= meleeEndMoraleThreshold ||
               eng.defender.currentMorale <= meleeEndMoraleThreshold;
    }

    void EndMelee(MeleeEngagement eng)
    {
        if (!eng.isActive) return;

        eng.isActive = false;

        Debug.Log($"MELEE ENDS — " +
                $"{eng.attacker?.data?.unitName} vs " +
                $"{eng.defender?.data?.unitName}");

        // Exit melee state for both units if still alive
        if (eng.attacker != null &&
            !eng.attacker.isDefeated &&
            !eng.attacker.isRouting)
        {
            ExitMeleeState(eng.attacker);
            Debug.Log($"{eng.attacker.data.unitName} exits melee");
        }

        if (eng.defender != null &&
            !eng.defender.isDefeated &&
            !eng.defender.isRouting)
        {
            ExitMeleeState(eng.defender);
            Debug.Log($"{eng.defender.data.unitName} exits melee");
        }
    }

    void ExitMeleeState(UnitController unit)
    {
        SoldierSpawner spawner = unit.GetComponent<SoldierSpawner>();
        if (spawner == null) return;

        Transform container = unit.soldierContainer != null
            ? unit.soldierContainer.transform
            : unit.transform;

        List<Vector2> positions = spawner.GetCurrentFormationPositions();

        for (int i = 0; i < unit.soldiers.Count; i++)
        {
            SoldierCircle sc = unit.soldiers[i];
            if (sc == null) continue;
            if (sc.state == SoldierState.Dead) continue;
            if (i >= positions.Count) break;

            Vector3 localTarget = new Vector3(
                positions[i].x,
                positions[i].y,
                0
            );

            sc.ExitMelee(container, localTarget);
        }
    }

    public bool IsInMelee(UnitController unit)
    {
        foreach (MeleeEngagement eng in activeEngagements)
        {
            if (!eng.isActive) continue;  // ← skip ended engagements

            if (eng.attacker == unit || eng.defender == unit)
                return true;
        }
        return false;
    }

    public void EndEngagementsForUnit(UnitController unit)
    {
        foreach (MeleeEngagement eng in activeEngagements)
        {
            if (!eng.isActive) continue;

            if (eng.attacker == unit || eng.defender == unit)
            {
                Debug.Log($"Ending previous melee for {unit.data.unitName}");
                EndMelee(eng);
            }
        }
    }

    public List<MeleeEngagement> GetActiveEngagements()
        => activeEngagements;
}

// ── Melee Engagement Data ─────────────────────────────────────────

public class MeleeEngagement
{
    public UnitController attacker;
    public UnitController defender;
    public bool           isActive = true;  // ← ADD THIS

    public MeleeEngagement(UnitController a, UnitController d)
    {
        attacker = a;
        defender = d;
        isActive = true;
    }
}
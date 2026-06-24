using UnityEngine;

public class SoldierCircle : MonoBehaviour
{
    [Header("State")]
    public SoldierState state = SoldierState.Alive;

    [Header("Colors")]
    public Color playerAlive  = new Color(0.20f, 0.45f, 0.85f);
    public Color enemyAlive   = new Color(0.85f, 0.20f, 0.20f);
    public Color woundedColor = new Color(0.55f, 0.55f, 0.55f);
    public Color deadColor    = new Color(0.15f, 0.15f, 0.15f);

    [Header("Movement")]
    public float marchSpeed    = 2.0f;
    public float formUpSpeed   = 3.5f; // slightly faster for form-up

    // Movement state machine
    private enum MovePhase { Idle, Marching, FormingUp }
    private MovePhase movePhase = MovePhase.Idle;

    // Phase 1 — march to hex center in world space
    private Vector3 worldMarchTarget;

    // Phase 2 — drift into formation in local space
    private Vector3 localFormTarget;
    private Transform formationParent;

    private SpriteRenderer sr;
    private Faction faction;

    public System.Action OnArrived;

    public void Initialize(Faction unitFaction)
    {
        sr      = GetComponent<SpriteRenderer>();
        faction = unitFaction;
        SetState(SoldierState.Alive);
    }

    void Update()
    {
        switch (movePhase)
        {
            case MovePhase.Marching:
                UpdateMarching();
                break;

            case MovePhase.FormingUp:
                UpdateFormingUp();
                break;
        }
    }

    void UpdateMarching()
    {
        // Move toward hex center in world space
        transform.position = Vector3.MoveTowards(
            transform.position,
            worldMarchTarget,
            marchSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, worldMarchTarget) < 0.01f)
        {
            // Arrived at hex center — now form up
            transform.position = worldMarchTarget;
            StartFormingUp();
        }
    }

    void StartFormingUp()
    {
        // Re-parent to unit container
        transform.SetParent(formationParent);

        // Begin phase 2 — drift to formation offset
        movePhase = MovePhase.FormingUp;
    }

    void UpdateFormingUp()
    {
        // Smoothly move to formation position in local space
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            localFormTarget,
            formUpSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.localPosition, localFormTarget) < 0.005f)
        {
            transform.localPosition = localFormTarget;
            movePhase = MovePhase.Idle;
            OnArrived?.Invoke();
        }
    }

    // Called to start the full two-phase march
    public void MarchTo(
        Vector3 hexCenterWorld,   // world pos of destination hex
        Vector3 localFormation,   // local offset in formation
        Transform parent,         // unit container to re-parent to
        float customMarchSpeed = -1f,
        float customFormSpeed  = -1f)
    {
        if (state == SoldierState.Dead) return;

        worldMarchTarget  = hexCenterWorld;
        localFormTarget   = localFormation;
        formationParent   = parent;

        if (customMarchSpeed > 0) marchSpeed  = customMarchSpeed;
        if (customFormSpeed  > 0) formUpSpeed = customFormSpeed;

        // Detach from parent to march in world space
        transform.SetParent(null);
        movePhase = MovePhase.Marching;
    }

    // Instant placement — used on spawn only
    public void SnapTo(Vector3 localPosition)
    {
        localFormTarget         = localPosition;
        transform.localPosition = localPosition;
        movePhase               = MovePhase.Idle;
    }

    // Smooth form-up only — no marching (used for morale shifts)
    public void FormUpTo(Vector3 localPosition)
    {
        localFormTarget = localPosition;
        movePhase       = MovePhase.FormingUp;
    }

    public void SetState(SoldierState newState)
    {
        state = newState;

        if (sr == null) sr = GetComponent<SpriteRenderer>();

        sr.color = state switch
        {
            SoldierState.Alive   => faction == Faction.Player
                                        ? playerAlive
                                        : enemyAlive,
            SoldierState.Wounded => woundedColor,
            SoldierState.Dead    => deadColor,
            _                    => sr.color
        };
    }

    public bool IsMoving => movePhase != MovePhase.Idle;
}
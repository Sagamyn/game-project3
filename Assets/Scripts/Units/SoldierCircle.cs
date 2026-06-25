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
    public float marchSpeed  = 2.0f;
    public float formUpSpeed = 3.5f;

    // Replace knockback fields with these
    private Vector3 knockbackVelocity;
    private float   knockbackDecay        = 8f;
    private Vector3 knockbackReturnWorld; // world space return point
    private bool    isKnockedBack         = false;

    private enum MovePhase { Idle, Marching, FormingUp, Fleeing, KnockedBack, Melee }
    private MovePhase movePhase = MovePhase.Idle;

    private Vector3    worldMarchTarget;
    private Vector3    localFormTarget;
    public Transform  formationParent;

    private float meleeTimer      = 0f;
    private float meleeMoveCooldown = 0f;
    private Vector3 meleeTargetPos;
    private float meleeMoveInterval = 0.3f;

    private Vector3 fleeDirection;
    private float   fleeSpeed;
    private float   fleeDistance;
    private float   distanceFled;

    private SpriteRenderer sr;
    private Faction        faction;

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
            case MovePhase.Marching:  UpdateMarching();  break;
            case MovePhase.FormingUp: UpdateFormingUp(); break;
            case MovePhase.Fleeing:   UpdateFleeing();   break;
            case MovePhase.KnockedBack: UpdateKnockedBack(); break;
            case MovePhase.Melee:       UpdateMelee();       break;
        }
    }

    // ── Marching ──────────────────────────────────────────────────

    void UpdateMarching()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            worldMarchTarget,
            marchSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, worldMarchTarget) < 0.01f)
        {
            transform.position = worldMarchTarget;
            StartFormingUp();
        }
    }

    void StartFormingUp()
    {
        if (formationParent != null)
                transform.SetParent(formationParent, true);

            movePhase = MovePhase.FormingUp;
    }

    void UpdateFormingUp()
    {
        // If we have no parent yet just idle
        if (transform.parent == null)
        {
            // Try to re-parent
            if (formationParent != null)
                transform.SetParent(formationParent, true);
            else
            {
                movePhase = MovePhase.Idle;
                return;
            }
        }

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            localFormTarget,
            formUpSpeed * Time.deltaTime
        );

        if (Vector3.Distance(
            transform.localPosition, localFormTarget) < 0.005f)
        {
            transform.localPosition = localFormTarget;
            movePhase = MovePhase.Idle;
            OnArrived?.Invoke();
        }
    }

    // ── Melee ────────────────────────────────────────────────────

    void UpdateMelee()
    {
        // Stop immediately if soldier died during melee
        if (state == SoldierState.Dead)
        {
            movePhase = MovePhase.Idle;
            return;
        }

        meleeMoveCooldown -= Time.deltaTime;

        if (meleeMoveCooldown <= 0f)
        {
            meleeTargetPos = transform.position + new Vector3(
                UnityEngine.Random.Range(-0.2f, 0.2f),
                UnityEngine.Random.Range(-0.2f, 0.2f),
                0
            );

            meleeMoveCooldown = meleeMoveInterval +
                UnityEngine.Random.Range(-0.1f, 0.1f);
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            meleeTargetPos,
            1.5f * Time.deltaTime
        );
    }

    public void EnterMelee()
    {
        if (state == SoldierState.Dead) return;

        // Only detach if still parented
        // (knockback already detached them)
        if (transform.parent != null)
            transform.SetParent(null, true);

        meleeTargetPos    = transform.position;
        meleeMoveCooldown = UnityEngine.Random.Range(0f, 0.5f);
        movePhase         = MovePhase.Melee;
    }

    public void ExitMelee(Transform parent, Vector3 localTarget)
    {
        // Re-parent and return to formation
        formationParent = parent;
        localFormTarget = localTarget;
        transform.SetParent(parent, true);
        movePhase = MovePhase.FormingUp;
    }

    public bool IsInMelee => movePhase == MovePhase.Melee;

    // ── Fleeing ───────────────────────────────────────────────────

    void UpdateFleeing()
    {
        transform.position += fleeDirection * fleeSpeed * Time.deltaTime;
        distanceFled       += fleeSpeed * Time.deltaTime;

        if (distanceFled >= fleeDistance)
            Destroy(gameObject);
    }

    // ── Public API ────────────────────────────────────────────────

    public void MarchTo(
        Vector3   hexCenterWorld,
        Vector3   localFormation,
        Transform parent,
        float     customMarchSpeed = -1f,
        float     customFormSpeed  = -1f)
    {
        if (state == SoldierState.Dead) return;

        // Capture world position before ANY parent change
        Vector3 currentWorldPos = transform.position;

        worldMarchTarget = hexCenterWorld;
        localFormTarget  = localFormation;
        formationParent  = parent;

        if (customMarchSpeed > 0) marchSpeed  = customMarchSpeed;
        if (customFormSpeed  > 0) formUpSpeed = customFormSpeed;

        // Detach preserving world position
        transform.SetParent(null, true);

        // Force position — belt and suspenders
        transform.position = currentWorldPos;

        movePhase = MovePhase.Marching;
    }

    public void FleeOffMap(Vector3 direction, float speed)
    {
        if (state == SoldierState.Dead) return;

        // Capture world position before detaching
        Vector3 currentWorldPos = transform.position;

        // Detach preserving world position
        transform.SetParent(null, true);

        // Force exact position after detach
        transform.position = currentWorldPos;

        float wobble  = UnityEngine.Random.Range(-0.5f, 0.5f);
        fleeDirection = new Vector3(
            direction.x + wobble,
            direction.y + wobble,
            0
        ).normalized;

        fleeSpeed    = speed;
        fleeDistance = UnityEngine.Random.Range(8f, 12f);
        distanceFled = 0f;
        movePhase    = MovePhase.Fleeing;
    }

    public void SnapTo(Vector3 localPosition)
    {
        localFormTarget         = localPosition;
        transform.localPosition = localPosition;
        movePhase               = MovePhase.Idle;
    }

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

        // Stop all movement when soldier dies
        if (state == SoldierState.Dead)
        {
            movePhase         = MovePhase.Idle;
            knockbackVelocity = Vector3.zero;
        }
    }

    public bool IsMoving => movePhase != MovePhase.Idle;

    public void StopAllMovement()
    {
        movePhase         = MovePhase.Idle;
        knockbackVelocity = Vector3.zero;
        meleeMoveCooldown = 0f;
    }

    void UpdateKnockedBack()
    {
        // Move in world space
        transform.position += knockbackVelocity * Time.deltaTime;

        // Decay velocity
        knockbackVelocity = Vector3.Lerp(
            knockbackVelocity,
            Vector3.zero,
            knockbackDecay * Time.deltaTime
        );

        // Once stopped — go IDLE and wait
        // MeleeManager will call EnterMelee() shortly after
        if (knockbackVelocity.magnitude < 0.05f)
        {
            transform.position = transform.position; // stay put
            movePhase          = MovePhase.Idle;     // wait for melee
        }
    }

    public void ApplyKnockback(Vector3 impactDirection, float force)
    {
        if (state == SoldierState.Dead) return;

        // Capture current WORLD position as return point
        knockbackReturnWorld = transform.position;

        // Random spread on impact direction
        float spread = UnityEngine.Random.Range(-0.3f, 0.3f);
        Vector3 knockDir = new Vector3(
            impactDirection.x + spread,
            impactDirection.y + spread,
            0
        ).normalized;

        // Detach to move freely in world space
        transform.SetParent(null, true);

        knockbackVelocity = knockDir * force;
        isKnockedBack     = true;
        movePhase         = MovePhase.KnockedBack;
    }
}
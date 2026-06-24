using UnityEngine;

public class HexCell : MonoBehaviour
{
    [Header("Grid Position")]
    public int q;           // cube coordinate
    public int r;           // cube coordinate
    public int s;           // cube coordinate (= -q - r)

    [Header("State")]
    public bool isOccupied;
    public UnitController occupyingUnit;
    public TerrainType terrain;

    [Header("Visual")]
    private SpriteRenderer spriteRenderer;
    private Color defaultColor;

    // Highlight states
    public static Color colorDefault    = new Color(0.76f, 0.70f, 0.50f); // sandy
    public static Color colorMovement   = new Color(0.40f, 0.70f, 1.00f); // blue
    public static Color colorAttack     = new Color(1.00f, 0.30f, 0.30f); // red
    public static Color colorSelected   = new Color(1.00f, 0.85f, 0.20f); // gold
    public static Color colorHover      = new Color(0.90f, 0.90f, 0.90f); // white

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        defaultColor = colorDefault;
        spriteRenderer.color = defaultColor;
        s = -q - r; // enforce cube coordinate constraint
    }

    public void SetHighlight(Color color)
    {
        spriteRenderer.color = color;
    }

    public void ResetHighlight()
    {
        spriteRenderer.color = defaultColor;
    }

    // Get all 6 neighbor coordinates from this cell
    public Vector3Int[] GetNeighborCoordinates()
    {
        return new Vector3Int[]
        {
            new Vector3Int(q+1, r-1, s),   // East
            new Vector3Int(q+1, r,   s-1), // North East
            new Vector3Int(q,   r+1, s-1), // North West
            new Vector3Int(q-1, r+1, s),   // West
            new Vector3Int(q-1, r,   s+1), // South West
            new Vector3Int(q,   r-1, s+1)  // South East
        };
    }

    // Distance between this cell and another in cube coordinates
    public int DistanceTo(HexCell other)
    {
        return Mathf.Max(
            Mathf.Abs(q - other.q),
            Mathf.Abs(r - other.r),
            Mathf.Abs(s - other.s)
        );
    }
}

using UnityEngine;
using System.Collections.Generic;

public class HexGrid : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int width = 12;
    public int height = 8;

    [Header("Cell Settings")]
    public GameObject hexCellPrefab;

    private float spriteWidth;

    [Tooltip("Match this to your sprite's visual width in Unity units")]
    public float cellSize = 1;

    [Header("Runtime")]
    private Dictionary<Vector3Int, HexCell> cells = new Dictionary<Vector3Int, HexCell>();

    void Awake()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        cells.Clear();

        foreach (Transform child in transform)
            Destroy(child.gameObject);

        // ── Measure the actual sprite size automatically ──────────
        SpriteRenderer sr = hexCellPrefab.GetComponent<SpriteRenderer>();
        float spriteWidth  = sr.sprite.bounds.size.x;
        Debug.Log($"RAW spriteWidth = {spriteWidth}");
        float spriteHeight = sr.sprite.bounds.size.y;

        // For flat-top hex: use sprite width to drive horizontal spacing
        // and sprite height to drive vertical spacing
        float horizSpacing = spriteWidth  * 0.75f;   // columns overlap by 25%
        float vertSpacing  = spriteHeight * 0.8828125f;     // rows stack flush

        Debug.Log($"Sprite size: {spriteWidth} x {spriteHeight}");
        Debug.Log($"Spacing: horiz={horizSpacing}, vert={vertSpacing}");

        for (int col = 0; col < width; col++)
        {
            for (int row = 0; row < height; row++)
            {
                // Flat-top hex offset layout
                float x = col * horizSpacing;
                float y = row * vertSpacing;

                // Every odd column shifts down by half a cell
                if (col % 2 != 0)
                    y += vertSpacing * 0.5f;

                Vector3 worldPos = new Vector3(x, -y, 0);

                GameObject cellObj = Instantiate(
                    hexCellPrefab,
                    worldPos,
                    Quaternion.identity,
                    transform
                );

                // Cube coordinate conversion
                int cubeQ = col;
                int cubeR = row - (col - (col & 1)) / 2;
                int cubeS = -cubeQ - cubeR;

                cellObj.name = $"Hex ({cubeQ},{cubeR},{cubeS})";

                HexCell cell = cellObj.GetComponent<HexCell>();
                cell.q = cubeQ;
                cell.r = cubeR;
                cell.s = cubeS;

                cells[new Vector3Int(cubeQ, cubeR, cubeS)] = cell;
            }
        }

        CenterGridOnCamera(horizSpacing, vertSpacing);
    }

    void CenterGridOnCamera(float horizSpacing, float vertSpacing)
    {
        float totalWidth  = (width  - 1) * horizSpacing;
        float totalHeight = (height - 1) * vertSpacing;

        transform.position = new Vector3(
            -totalWidth  / 2f,
            totalHeight / 2f,
            0f
        );
    }

    Vector3 ColRowToWorld(int col, int row)
    {
        // Flat-top hexagon spacing — this is the key fix
        float horizSpacing = cellSize * 1.5f;
        float vertSpacing  = cellSize * Mathf.Sqrt(3);

        float x = col * horizSpacing;

        // Offset every odd column downward by half a cell height
        float y = row * vertSpacing;
        if (col % 2 != 0)
            y += vertSpacing * 0.5f;

        return new Vector3(x, -y, 0);
    }



    // ── Public API used by all other systems ──────────────────────

    public HexCell GetCell(int q, int r, int s)
    {
        return cells.TryGetValue(new Vector3Int(q, r, s), out HexCell cell)
            ? cell : null;
    }

    public HexCell GetCell(Vector3Int cube)
    {
        return GetCell(cube.x, cube.y, cube.z);
    }

    public List<HexCell> GetCellsInRange(HexCell origin, int range)
    {
        List<HexCell> result = new List<HexCell>();

        for (int dq = -range; dq <= range; dq++)
        {
            int rMin = Mathf.Max(-range, -dq - range);
            int rMax = Mathf.Min( range, -dq + range);

            for (int dr = rMin; dr <= rMax; dr++)
            {
                int ds = -dq - dr;
                HexCell cell = GetCell(
                    origin.q + dq,
                    origin.r + dr,
                    origin.s + ds
                );
                if (cell != null && cell != origin)
                    result.Add(cell);
            }
        }

        return result;
    }

    public HexCell GetCellFromWorldPosition(Vector3 worldPos)
    {
        HexCell closest = null;
        float closestDist = float.MaxValue;

        // Find the nearest cell by actual distance
        // This is reliable regardless of coordinate math
        foreach (HexCell cell in cells.Values)
        {
            float dist = Vector3.Distance(worldPos, cell.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = cell;
            }
        }

        // Only return if click is within reasonable range of a cell
        // Use spriteWidth as threshold so clicking empty space returns null
        if (closestDist <= spriteWidth * 0.6f)
            return closest;

        return null;
    }

    Vector3Int CubeRound(float q, float r)
    {
        float s = -q - r;

        int rq = Mathf.RoundToInt(q);
        int rr = Mathf.RoundToInt(r);
        int rs = Mathf.RoundToInt(s);

        float dq = Mathf.Abs(rq - q);
        float dr = Mathf.Abs(rr - r);
        float ds = Mathf.Abs(rs - s);

        if      (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds)            rr = -rq - rs;
        else                         rs = -rq - rr;

        return new Vector3Int(rq, rr, rs);
    }
}
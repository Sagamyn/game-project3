using UnityEngine;

public class OrderArrow : MonoBehaviour
{
    [Header("Appearance")]
    public float lineWidth      = 0.06f;
    public float arrowheadSize  = 0.15f;
    public Color moveColor      = new Color(0.3f, 0.6f, 1f, 0.8f);
    public Color attackColor    = new Color(1f, 0.3f, 0.3f, 0.8f);
    public Color chargeColor    = new Color(1f, 0.7f, 0.1f, 0.9f);

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth   = lineWidth;
        lineRenderer.sortingOrder = 5; // above hex grid, below soldiers
    }

    public void DrawArrow(Vector3 start, Vector3 end, OrderType orderType)
    {
        Color color = orderType switch
        {
            OrderType.Move   => moveColor,
            OrderType.Fire   => attackColor,
            OrderType.Charge => chargeColor,
            _                => moveColor
        };

        lineRenderer.startColor = color;
        lineRenderer.endColor   = color;

        // Pull back the end point slightly so arrow doesn't
        // overlap the target unit sprite
        Vector3 direction = (end - start).normalized;
        Vector3 adjustedEnd = end - direction * 0.3f;

        // Build the arrow shape — shaft + arrowhead
        Vector3 perpendicular = new Vector3(
            -direction.y, direction.x, 0
        ) * arrowheadSize;

        Vector3 arrowBase = adjustedEnd - direction * arrowheadSize;

        // 5 points: start -> arrow base -> left wing -> tip -> right wing -> back to base
        Vector3[] points = new Vector3[]
        {
            start,
            arrowBase,
            arrowBase + perpendicular * 0.6f,
            adjustedEnd,
            arrowBase - perpendicular * 0.6f,
            arrowBase
        };

        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
    }

    public void Hide()
    {
        lineRenderer.positionCount = 0;
    }
}
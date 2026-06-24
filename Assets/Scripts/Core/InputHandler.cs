using UnityEngine;

public class InputHandler : MonoBehaviour
{
    [Header("References")]
    public HexGrid hexGrid;
    public GameManager gameManager;
    public Camera mainCamera;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleLeftClick();

        if (Input.GetMouseButtonDown(1))
            HandleRightClick();

        if (Input.GetKeyDown(KeyCode.Escape))
            gameManager.DeselectUnit();
    }

    void HandleLeftClick()
    {
        // Cast a ray from mouse position into the scene
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;

        // Use Physics2D to detect what was clicked
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null)
        {
            Debug.Log($"Hit object: {hit.collider.gameObject.name}");

            // Check if we hit a HexCell
            HexCell clickedCell = hit.collider.GetComponent<HexCell>();

            if (clickedCell != null)
            {
                Debug.Log($"Clicked cell: ({clickedCell.q},{clickedCell.r},{clickedCell.s})");
                gameManager.OnCellClicked(clickedCell);
            }
        }
        else
        {
            Debug.Log("Raycast hit nothing");
        }
    }

    void HandleRightClick()
    {
        // Right click = deselect
        gameManager.DeselectUnit();
    }
}

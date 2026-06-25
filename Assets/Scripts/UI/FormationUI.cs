using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FormationUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject formationPanel;

    [Header("Buttons")]
    public Button lineButton;
    public Button columnButton;
    public Button squareButton;

    [Header("Labels")]
    public TextMeshProUGUI unitNameText;
    public TextMeshProUGUI currentFormationText;

    private GameManager gameManager;
    private UnitController currentUnit;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();

        // Wire button listeners
        lineButton?.onClick.AddListener(() =>
            ChangeFormation(FormationType.Line));

        columnButton?.onClick.AddListener(() =>
            ChangeFormation(FormationType.Column));

        squareButton?.onClick.AddListener(() =>
            ChangeFormation(FormationType.Square));

        // Hide panel at start
        formationPanel?.SetActive(false);
    }

    // Called by GameManager when a unit is selected
    public void ShowForUnit(UnitController unit)
    {
        currentUnit = unit;

        if (unit == null)
        {
            formationPanel?.SetActive(false);
            return;
        }

        // Only show for units that can change formation
        bool canChange = unit.data.unitType == UnitType.LineInfantry ||
                         unit.data.unitType == UnitType.Cavalry;

        if (!canChange)
        {
            formationPanel?.SetActive(false);
            return;
        }

        formationPanel?.SetActive(true);

        // Update unit name
        if (unitNameText != null)
            unitNameText.text = unit.data.unitName;

        // Update current formation label
        if (currentFormationText != null)
            currentFormationText.text = $"Formation: {unit.currentFormation}";

        // Show/hide square button based on unit type
        // Only infantry can form square
        if (squareButton != null)
            squareButton.gameObject.SetActive(
                unit.data.unitType == UnitType.LineInfantry
            );

        // Highlight current formation button
        UpdateButtonHighlights(unit.currentFormation);
    }

    public void Hide()
    {
        currentUnit = null;
        formationPanel?.SetActive(false);
    }

    void ChangeFormation(FormationType newFormation)
    {
        if (currentUnit == null) return;
        if (gameManager.currentPhase != TurnPhase.Planning) return;

        // Can't form square if not infantry
        if (newFormation == FormationType.Square &&
            currentUnit.data.unitType != UnitType.LineInfantry)
            return;

        currentUnit.AssignFormationChange(newFormation);

        // Update label
        if (currentFormationText != null)
            currentFormationText.text = $"Formation: {newFormation}";

        UpdateButtonHighlights(newFormation);

        Debug.Log($"{currentUnit.data.unitName} → {newFormation} formation");
    }

    void UpdateButtonHighlights(FormationType current)
    {
        // Highlight active formation button
        Color activeColor   = new Color(0.3f, 0.8f, 0.3f);
        Color inactiveColor = new Color(0.8f, 0.8f, 0.8f);

        if (lineButton != null)
            lineButton.image.color = current == FormationType.Line
                ? activeColor : inactiveColor;

        if (columnButton != null)
            columnButton.image.color = current == FormationType.Column
                ? activeColor : inactiveColor;

        if (squareButton != null)
            squareButton.image.color = current == FormationType.Square
                ? activeColor : inactiveColor;
    }

    // Blink the Line button to tell player they must
    // change formation before moving
    public void BlinkLineButton()
    {
        if (lineButton == null) return;
        StartCoroutine(BlinkButton(lineButton));
    }

    System.Collections.IEnumerator BlinkButton(Button button)
    {
        Color originalColor = button.image.color;
        Color blinkColor    = new Color(1f, 0.8f, 0f); // gold flash

        for (int i = 0; i < 4; i++)
        {
            button.image.color = blinkColor;
            yield return new WaitForSeconds(0.12f);
            button.image.color = originalColor;
            yield return new WaitForSeconds(0.12f);
        }
    }
}
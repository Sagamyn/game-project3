using UnityEngine;

public class MoraleBar : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer background;
    public SpriteRenderer foreground;

    [Header("Settings")]
    public float barWidth    = 0.8f;
    public float barHeight   = 0.08f;
    public float yOffset     = 0.55f; // how far above unit center

    public float xOffset     = -0.55f;

    [Header("Colors")]
    public Color colorHigh   = new Color(0.20f, 0.85f, 0.20f); // green
    public Color colorMid    = new Color(0.95f, 0.80f, 0.10f); // yellow
    public Color colorLow    = new Color(0.85f, 0.20f, 0.20f); // red
    public Color colorBG     = new Color(0.10f, 0.10f, 0.10f); // dark

    private float maxMorale = 100f;

    public void Initialize(float maxMoraleValue)
    {
        maxMorale = maxMoraleValue;

        // Set background size
        background.transform.localScale = new Vector3(
            barWidth,
            barHeight,
            1f
        );
        background.color = colorBG;

        // Set foreground size (starts full)
        foreground.transform.localScale = new Vector3(
            barWidth,
            barHeight,
            1f
        );
        foreground.color = colorHigh;

        // Position above unit
        transform.localPosition = new Vector3(xOffset, yOffset, -0.1f);
    

    }

    public void UpdateMorale(float currentMorale)
    {
        float percent = Mathf.Clamp01(currentMorale / maxMorale);

        // Scale foreground on X axis — shrinks from right to left
        foreground.transform.localScale = new Vector3(
            barWidth * percent,
            barHeight,
            1f
        );

        // Offset foreground so it shrinks from right
        // not from center
        float xOffset = (barWidth - barWidth * percent) / -2f;
        foreground.transform.localPosition = new Vector3(
            xOffset,
            0,
            -0.01f  // slightly in front of background
        );

        // Color shifts green → yellow → red
        foreground.color = percent switch
        {
            > 0.75f => colorHigh,
            > 0.50f => Color.Lerp(colorMid,  colorHigh,
                                  (percent - 0.50f) / 0.25f),
            > 0.25f => Color.Lerp(colorLow,  colorMid,
                                  (percent - 0.25f) / 0.25f),
            _       => colorLow
        };
    }
}
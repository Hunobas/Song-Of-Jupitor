// Assets/Scripts/Localization/LayoutProfile.cs
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Localization/Layout Profile", fileName = "LayoutProfile")]
public sealed class LayoutProfile : ScriptableObject
{
    [Header("Font & Size")]
    public TMP_FontAsset font;
    public bool autoSize = false;
    [Min(1)] public int fontSize = 36;
    [Min(1)] public int minSize = 18;
    [Min(1)] public int maxSize = 48;

    [Header("Spacing")]
    public float lineSpacing = 0f;
    public float characterSpacing = 0f;
    public Vector4 margins;

    [Header("Layout")]
    public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;
    public bool wordWrapping = true;
    public TextOverflowModes overflowMode = TextOverflowModes.Overflow;

    [Header("Style (optional)")]
    public FontStyles fontStyle = FontStyles.Normal;

    public void ApplyTo(TMP_Text t)
    {
        if (!t) return;

        if (font) t.font = font;
        t.enableAutoSizing = autoSize;

        if (!autoSize)
            t.fontSize = fontSize;
        else
        {
            t.fontSizeMin = minSize;
            t.fontSizeMax = maxSize;
        }

        t.lineSpacing = lineSpacing;
        t.characterSpacing = characterSpacing;
        t.margin = margins;
        t.alignment = alignment;
        t.enableWordWrapping = wordWrapping;
        t.overflowMode = overflowMode;
        t.fontStyle = fontStyle;
    }
}
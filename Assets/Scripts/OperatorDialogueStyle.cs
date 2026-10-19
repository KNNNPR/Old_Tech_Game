using TMPro;
using UnityEngine;

/// <summary>Neutral dialogue colours for Operator C; Helpy keeps his green terminal palette.</summary>
public static class OperatorDialogueStyle
{
    public static readonly Color Background = new Color(.07f, .07f, .07f, .96f);
    public static readonly Color Speaker = new Color(.78f, .78f, .78f, 1);
    public static readonly Color Secondary = new Color(.55f, .55f, .55f, 1);
    public static readonly Color TopRule = new Color(.68f, .68f, .68f, .5f);
    public static readonly Color BottomRule = new Color(.4f, .4f, .4f, .3f);
    public static readonly VertexGradient Gradient = new VertexGradient(
        new Color(.86f, .86f, .86f), new Color(.82f, .82f, .82f),
        new Color(.61f, .61f, .61f), new Color(.69f, .69f, .69f));

    public static void ApplyText(TMP_Text text)
    {
        text.color = Color.white;
        text.enableVertexGradient = true;
        text.colorGradient = Gradient;
        // Keep bold / italic emphasis, while ignoring green colour tags in existing scene dialogue.
        text.overrideColorTags = true;
    }
}

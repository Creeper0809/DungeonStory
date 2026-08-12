using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class ResearchTreeNodeVisual
{
    public ResearchTreeNodeVisual(
        Image background,
        TMP_Text stateText,
        RectTransform progressFill,
        Outline selectionOutline)
    {
        Background = background;
        StateText = stateText;
        ProgressFill = progressFill;
        SelectionOutline = selectionOutline;
    }

    public Image Background { get; }
    public TMP_Text StateText { get; }
    public RectTransform ProgressFill { get; }
    public Outline SelectionOutline { get; }
}

using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BuildingInfoResponsiveLayout
{
    private readonly RectTransform root;
    private readonly Vector2 baseSizeDelta;
    private GameObject legacyButtonSelection;

    public BuildingInfoResponsiveLayout(RectTransform root)
    {
        this.root = root;
        baseSizeDelta = root != null ? root.sizeDelta : Vector2.zero;
        legacyButtonSelection = root != null
            ? root.Find("ButtonSelection")?.gameObject
            : null;
    }

    public void BringToFront()
    {
        root?.SetAsLastSibling();
    }

    public void SetLegacyChromeVisible(bool visible)
    {
        if (legacyButtonSelection == null && root != null)
        {
            legacyButtonSelection =
                root.Find("ButtonSelection")?.gameObject;
        }
        if (legacyButtonSelection != null
            && legacyButtonSelection.activeSelf != visible)
        {
            legacyButtonSelection.SetActive(visible);
        }
    }

    public void ApplyWidth(bool portrait)
    {
        if (root == null
            || root.GetComponentInParent<Canvas>()?.transform
                is not RectTransform canvasRect)
        {
            return;
        }

        float targetWidth = baseSizeDelta.x;
        if (portrait && targetWidth > 0f)
        {
            targetWidth = Mathf.Min(
                targetWidth,
                canvasRect.rect.width * 0.92f);
        }
        root.sizeDelta = new Vector2(targetWidth, baseSizeDelta.y);
    }
}

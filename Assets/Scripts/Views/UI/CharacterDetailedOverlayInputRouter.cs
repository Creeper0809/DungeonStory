using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Routes pointer clicks from the modal background to runtime-created controls.
/// Unity can leave graphics created below an initially hidden runtime hierarchy
/// without a raycast depth even though they render normally.
/// </summary>
public sealed class CharacterDetailedOverlayInputRouter : MonoBehaviour,
    IPointerClickHandler
{
    private Transform modalRoot;

    public void Bind(Transform root)
    {
        modalRoot = root;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null
            || modalRoot == null
            || !modalRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        Button target = modalRoot.GetComponentsInChildren<Button>(false)
            .Where(button => button != null
                && button.interactable
                && button.gameObject.activeInHierarchy
                && Contains(button.transform as RectTransform, eventData))
            .OrderBy(button => RectArea(button.transform as RectTransform))
            .ThenByDescending(button => HierarchyDepth(button.transform))
            .FirstOrDefault();
        target?.onClick.Invoke();
    }

    private static bool Contains(
        RectTransform rect,
        PointerEventData eventData)
    {
        if (rect == null)
        {
            return false;
        }
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null
            && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            eventData.position,
            camera);
    }

    private static float RectArea(RectTransform rect) =>
        rect == null ? float.MaxValue : rect.rect.width * rect.rect.height;

    private static int HierarchyDepth(Transform value)
    {
        int depth = 0;
        for (Transform current = value; current != null; current = current.parent)
        {
            depth++;
        }
        return depth;
    }
}

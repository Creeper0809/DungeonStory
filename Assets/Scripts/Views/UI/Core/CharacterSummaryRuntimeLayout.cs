using UnityEngine;
using UnityEngine.UI;

public static class CharacterSummaryRuntimeLayout
{
    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    public static void ConfigurePanelBounds(GameObject uiRoot)
    {
        RectTransform wrapper = uiRoot.transform.parent as RectTransform;
        if (wrapper != null)
        {
            wrapper.anchorMin = Vector2.zero;
            wrapper.anchorMax = Vector2.zero;
            wrapper.pivot = Vector2.zero;
            wrapper.anchoredPosition = new Vector2(24f, 80f);
            wrapper.sizeDelta = new Vector2(500f, 700f);
        }

        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            SetStretch(rootRect, Vector2.zero, Vector2.zero);
        }

        Image background = uiRoot.GetComponent<Image>();
        if (background == null)
        {
            background = uiRoot.AddComponent<Image>();
        }

        background.color = DungeonUiThemePalette.Panel(false);
    }

    public static void DisableLegacyChildren(Transform root)
    {
        foreach (Transform child in root)
        {
            child.gameObject.SetActive(false);
        }
    }

    public static void SetStretch(
        RectTransform rect,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}

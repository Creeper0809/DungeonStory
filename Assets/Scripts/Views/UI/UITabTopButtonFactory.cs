using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IUITabTopButtonFactory
{
    Button CreateButton(Button template, Transform parent, TabId id, string title);
    HorizontalLayoutGroup EnsureSingleRowLayout(Transform root, float barHeight, params string[] rowNames);
}

public sealed class UITabTopButtonFactory : IUITabTopButtonFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    public UITabTopButtonFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public Button CreateButton(Button template, Transform parent, TabId id, string title)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        Button button = UnityEngine.Object.Instantiate(template, parent);
        button.name = $"TopTabButton_{id}_{SanitizeName(title)}";
        button.onClick = new Button.ButtonClickedEvent();
        UITabButtonBinding binding = button.GetComponent<UITabButtonBinding>();
        if (binding == null)
        {
            binding = button.gameObject.AddComponent<UITabButtonBinding>();
        }

        binding.Set(id);
        SetLabel(button, title);
        return button;
    }

    public HorizontalLayoutGroup EnsureSingleRowLayout(Transform root, float barHeight, params string[] rowNames)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (root is RectTransform rootRect)
        {
            rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, barHeight);
            rootRect.anchoredPosition = new Vector2(rootRect.anchoredPosition.x, barHeight * 0.5f);
        }

        if (rowNames != null)
        {
            foreach (string rowName in rowNames)
            {
                MoveRowChildrenBackToRoot(root, rowName);
            }
        }

        VerticalLayoutGroup oldVertical = root.GetComponent<VerticalLayoutGroup>();
        if (oldVertical != null)
        {
            oldVertical.enabled = false;
        }

        HorizontalLayoutGroup horizontal = root.GetComponent<HorizontalLayoutGroup>();
        if (horizontal == null)
        {
            horizontal = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        horizontal.enabled = true;
        horizontal.padding = new RectOffset(0, 0, 0, 0);
        horizontal.spacing = 1f;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.transform.parent != root)
            {
                continue;
            }

            LayoutElement layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = button.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = 0f;
            layoutElement.preferredWidth = 0f;
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = 0f;
            layoutElement.preferredHeight = 0f;
            layoutElement.flexibleHeight = 1f;
        }

        return horizontal;
    }

    private void SetLabel(Button button, string title)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            return;
        }

        tmpKoreanFontService.Apply(label);
        label.text = title;
    }

    private static void MoveRowChildrenBackToRoot(Transform root, string rowName)
    {
        if (string.IsNullOrWhiteSpace(rowName))
        {
            return;
        }

        Transform row = root.Find(rowName);
        if (row == null)
        {
            return;
        }

        while (row.childCount > 0)
        {
            row.GetChild(0).SetParent(root, false);
        }

        row.gameObject.SetActive(false);
    }

    private static string SanitizeName(string title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? "Untitled"
            : title.Replace("/", string.Empty);
    }
}

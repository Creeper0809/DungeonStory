using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;

public interface IGridConstructButtonFactory
{
    UITab CreateCategoryPanel(GameObject panelPrefab, Transform parent, BuildingCategory category);
    Button CreateCategoryButton(GameObject buttonPrefab, Transform parent, string labelText, UnityAction onClick);
    UIBuildingSelectButton CreateBuildingSelectButton(GameObject buttonPrefab, Transform parent, BuildingSO buildingData);
}

public sealed class GridConstructButtonFactory : IGridConstructButtonFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;
    private readonly IObjectResolver objectResolver;

    public GridConstructButtonFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    [Inject]
    public GridConstructButtonFactory(
        ITmpKoreanFontService tmpKoreanFontService,
        IObjectResolver objectResolver)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public UITab CreateCategoryPanel(GameObject panelPrefab, Transform parent, BuildingCategory category)
    {
        if (panelPrefab == null)
        {
            throw new ArgumentNullException(nameof(panelPrefab));
        }

        UITab panel = UnityEngine.Object.Instantiate(panelPrefab, parent).GetComponent<UITab>();
        if (panel == null)
        {
            throw new InvalidOperationException("Building select panel prefab requires a UITab component.");
        }

        objectResolver?.Inject(panel);
        panel.id = (int)category;
        panel.gameObject.name = "BuildingSelect" + category;
        return panel;
    }

    public Button CreateCategoryButton(
        GameObject buttonPrefab,
        Transform parent,
        string labelText,
        UnityAction onClick)
    {
        if (buttonPrefab == null)
        {
            throw new ArgumentNullException(nameof(buttonPrefab));
        }

        Button button = UnityEngine.Object.Instantiate(buttonPrefab, parent).GetComponent<Button>();
        if (button == null)
        {
            throw new InvalidOperationException("Building category button prefab requires a Button component.");
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            tmpKoreanFontService.Apply(label);
            label.text = labelText;
        }

        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        return button;
    }

    public UIBuildingSelectButton CreateBuildingSelectButton(
        GameObject buttonPrefab,
        Transform parent,
        BuildingSO buildingData)
    {
        if (buttonPrefab == null)
        {
            throw new ArgumentNullException(nameof(buttonPrefab));
        }

        if (buildingData == null)
        {
            throw new ArgumentNullException(nameof(buildingData));
        }

        UIBuildingSelectButton button = UnityEngine.Object.Instantiate(buttonPrefab, parent)
            .GetComponent<UIBuildingSelectButton>();
        if (button == null)
        {
            throw new InvalidOperationException("Building select button prefab requires a UIBuildingSelectButton component.");
        }

        objectResolver?.Inject(button);
        button.Initialization(buildingData);
        AddBuildingLabel(button, buildingData);

        Button unityButton = button.GetComponent<Button>();
        if (unityButton != null)
        {
            unityButton.onClick.RemoveListener(button.OnClick);
            unityButton.onClick.AddListener(button.OnClick);
        }

        return button;
    }

    private void AddBuildingLabel(UIBuildingSelectButton button, BuildingSO buildingData)
    {
        if (button == null || buildingData == null)
        {
            return;
        }

        if (button.transform.childCount > 0
            && button.transform.GetChild(0) is RectTransform iconRect)
        {
            iconRect.anchoredPosition = new Vector2(0f, 10f);
        }

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(button.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 4f);
        labelRect.sizeDelta = new Vector2(-6f, 30f);

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        tmpKoreanFontService.Apply(label);
        int unlockPhase = buildingData.GetUnlockPhase();
        string unlockText = unlockPhase > 1 ? $"P{unlockPhase} / 연구" : "P1";
        label.text = buildingData.GetAbility<BuildingEconomyAbility>() != null
            ? $"{buildingData.objectName}\n{buildingData.GetConstructionCost()}G - {unlockText}"
            : buildingData.objectName;
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 14f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 7f;
        label.fontSizeMax = 14f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.outlineWidth = 0.12f;
        label.outlineColor = Color.black;
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OffenseWorldMapStrategicViewFactory
{
    private const float HexWidth = 39f;
    private const float HexHeight = 34f;

    private readonly RectTransform mapRoot;
    private readonly RectTransform actionRoot;
    private readonly Func<
        Transform,
        string,
        float,
        TextAlignmentOptions,
        GameObject> textCreator;
    private readonly Func<
        RectTransform,
        string,
        float,
        Action,
        GameObject> buttonCreator;
    private readonly ICollection<GameObject> spawnedMapObjects;
    private readonly ICollection<GameObject> spawnedActionButtons;

    public OffenseWorldMapStrategicViewFactory(
        RectTransform mapRoot,
        RectTransform actionRoot,
        Func<Transform, string, float, TextAlignmentOptions, GameObject>
            textCreator,
        Func<RectTransform, string, float, Action, GameObject> buttonCreator,
        ICollection<GameObject> spawnedMapObjects,
        ICollection<GameObject> spawnedActionButtons)
    {
        this.mapRoot = mapRoot ?? throw new ArgumentNullException(nameof(mapRoot));
        this.actionRoot = actionRoot ?? throw new ArgumentNullException(nameof(actionRoot));
        this.textCreator = textCreator
            ?? throw new ArgumentNullException(nameof(textCreator));
        this.buttonCreator = buttonCreator
            ?? throw new ArgumentNullException(nameof(buttonCreator));
        this.spawnedMapObjects = spawnedMapObjects
            ?? throw new ArgumentNullException(nameof(spawnedMapObjects));
        this.spawnedActionButtons = spawnedActionButtons
            ?? throw new ArgumentNullException(nameof(spawnedActionButtons));
    }

    public void CreateHexCell(
        string objectName,
        bool blocked,
        string label,
        Vector2 position,
        Color terrainColor,
        Color labelColor,
        Action onSelected)
    {
        GameObject cell = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(OffenseHexTileGraphic),
            typeof(Button));
        cell.transform.SetParent(mapRoot, false);
        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(HexWidth, HexHeight);
        rect.anchoredPosition = position;

        OffenseHexTileGraphic graphic = cell.GetComponent<OffenseHexTileGraphic>();
        graphic.color = terrainColor;
        Button button = cell.GetComponent<Button>();
        button.targetGraphic = graphic;
        button.interactable = !blocked;
        button.onClick.AddListener(() => onSelected());
        spawnedMapObjects.Add(cell);

        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        TMP_Text text = CreateChildLabel(cell.transform, label, 10f);
        text.color = labelColor;
        text.raycastTarget = false;
        text.transform.SetAsLastSibling();
    }

    private TMP_Text CreateChildLabel(
        Transform parent,
        string text,
        float fontSize)
    {
        GameObject labelObject = CreateText(
            parent,
            "HexLabel",
            fontSize,
            TextAlignmentOptions.Center);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(-0.45f, -0.55f);
        rect.anchorMax = new Vector2(1.45f, 1.55f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = text;
        label.enableAutoSizing = true;
        label.fontSizeMin = 7f;
        label.fontSizeMax = fontSize;
        return label;
    }

    public TMP_Text CreateMapText(
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = CreateText(
            mapRoot,
            name,
            fontSize,
            alignment);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.text = text;
        spawnedMapObjects.Add(textObject);
        return label;
    }

    public GameObject CreateMapButton(
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Action callback,
        Color color)
    {
        GameObject buttonObject = CreateButton(
            mapRoot,
            label,
            15f,
            callback);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);
        buttonObject.GetComponent<Image>().color = color;
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.ignoreLayout = true;
        }

        spawnedMapObjects.Add(buttonObject);
        return buttonObject;
    }

    public void AddRightButton(
        string label,
        Action callback,
        Color? color = null)
    {
        GameObject button = CreateButton(
            actionRoot,
            label,
            15f,
            callback);
        button.GetComponent<LayoutElement>().preferredHeight = 40f;
        if (color.HasValue)
        {
            button.GetComponent<Image>().color = color.Value;
        }

        spawnedActionButtons.Add(button);
    }

    private GameObject CreateText(
        Transform parent,
        string name,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        return textCreator(parent, name, fontSize, alignment);
    }

    private GameObject CreateButton(
        RectTransform parent,
        string label,
        float fontSize,
        Action callback)
    {
        return buttonCreator(parent, label, fontSize, callback);
    }
}

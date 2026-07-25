using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public interface IOwnerSelectionOptionButtonFactory
{
    Button Create(Button prefab, Transform parent, string objectName, string label, UnityAction onClick);
    void Release(GameObject optionObject);
}

public sealed class OwnerSelectionOptionButtonFactory : IOwnerSelectionOptionButtonFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    public OwnerSelectionOptionButtonFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public Button Create(Button prefab, Transform parent, string objectName, string label, UnityAction onClick)
    {
        if (prefab == null)
        {
            throw new ArgumentNullException(nameof(prefab));
        }

        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        Button button = UnityEngine.Object.Instantiate(prefab, parent);
        button.name = objectName;
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        DungeonUiTheme.StyleButton(button);

        TMP_Text labelText = button.GetComponentInChildren<TMP_Text>();
        if (labelText != null)
        {
            tmpKoreanFontService.Apply(labelText);
            labelText.text = label;
            labelText.fontSize = 20f;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 13f;
            labelText.fontSizeMax = 20f;
            labelText.textWrappingMode = TextWrappingModes.Normal;
            labelText.overflowMode = TextOverflowModes.Truncate;
        }

        return button;
    }

    public void Release(GameObject optionObject)
    {
        if (optionObject == null)
        {
            return;
        }

        optionObject.SetActive(false);
        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(optionObject);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(optionObject);
        }
    }
}

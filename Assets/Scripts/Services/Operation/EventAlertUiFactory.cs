using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class EventAlertUiFactory
{
    public static Button CreateAlertButton(
        Transform buttonRoot,
        EventAlertRecord record,
        UnityAction onClick,
        ITmpKoreanFontService tmpKoreanFontService)
    {
        if (buttonRoot == null || record == null)
        {
            return null;
        }

        GameObject buttonObject = new GameObject($"EventAlertButton_{record.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(buttonRoot, false);
        buttonObject.transform.SetAsFirstSibling();

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(EventAlertLayout.AlertButtonWidth, EventAlertLayout.AlertButtonHeight);

        Image image = buttonObject.GetComponent<Image>();
        image.color = GetImportanceColor(record.Importance);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        DungeonUiTheme.StyleButton(button);
        image.color = GetImportanceColor(record.Importance);
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        TMP_Text label = CreateText(buttonObject.transform, "Label", record.ButtonText, 15, TextAlignmentOptions.Center, tmpKoreanFontService);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        return button;
    }

    public static void UpdateAlertButton(Button button, EventAlertRecord record)
    {
        if (button == null || record == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = record.ButtonText;
        }
    }

    public static Button CreateChoiceButton(
        Transform parent,
        EventAlertChoice choice,
        int choiceIndex,
        UnityAction onClick,
        ITmpKoreanFontService tmpKoreanFontService)
    {
        if (parent == null || choice == null)
        {
            return null;
        }

        GameObject buttonObject = new GameObject($"EventChoice_{choiceIndex + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(18f + (choiceIndex * 138f), 18f);
        rect.sizeDelta = new Vector2(128f, 34f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = DungeonUiTheme.Accent;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        DungeonUiTheme.StyleButton(button, selected: true);
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        TMP_Text label = CreateText(buttonObject.transform, "Label", choice.Label, 14, TextAlignmentOptions.Center, tmpKoreanFontService);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return button;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        int fontSize,
        TextAlignmentOptions alignment,
        ITmpKoreanFontService tmpKoreanFontService)
    {
        if (tmpKoreanFontService == null)
        {
            throw new System.ArgumentNullException(nameof(tmpKoreanFontService));
        }

        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmpKoreanFontService.Apply(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Color GetImportanceColor(EventAlertImportance importance)
    {
        return importance switch
        {
            EventAlertImportance.Low => DungeonUiTheme.SurfaceRaised,
            EventAlertImportance.Medium => Color.Lerp(DungeonUiTheme.SurfaceRaised, DungeonUiTheme.Warning, 0.55f),
            EventAlertImportance.High => Color.Lerp(DungeonUiTheme.SurfaceRaised, DungeonUiTheme.Danger, 0.65f),
            _ => DungeonUiTheme.SurfaceRaised
        };
    }
}

public interface IEventAlertButtonFactory
{
    Button CreateAlertButton(Transform buttonRoot, EventAlertRecord record, UnityAction onClick);
    void UpdateAlertButton(Button button, EventAlertRecord record);
    Button CreateChoiceButton(Transform parent, EventAlertChoice choice, int choiceIndex, UnityAction onClick);
    void Release(Button button);
}

public sealed class EventAlertButtonFactory : IEventAlertButtonFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    public EventAlertButtonFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new System.ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public Button CreateAlertButton(Transform buttonRoot, EventAlertRecord record, UnityAction onClick)
    {
        return EventAlertUiFactory.CreateAlertButton(
            buttonRoot,
            record,
            onClick,
            tmpKoreanFontService);
    }

    public void UpdateAlertButton(Button button, EventAlertRecord record)
    {
        EventAlertUiFactory.UpdateAlertButton(button, record);
    }

    public Button CreateChoiceButton(
        Transform parent,
        EventAlertChoice choice,
        int choiceIndex,
        UnityAction onClick)
    {
        return EventAlertUiFactory.CreateChoiceButton(
            parent,
            choice,
            choiceIndex,
            onClick,
            tmpKoreanFontService);
    }

    public void Release(Button button)
    {
        if (button == null)
        {
            return;
        }

        GameObject buttonObject = button.gameObject;
        buttonObject.SetActive(false);
        buttonObject.transform.SetParent(null, false);

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(buttonObject);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(buttonObject);
        }
    }
}

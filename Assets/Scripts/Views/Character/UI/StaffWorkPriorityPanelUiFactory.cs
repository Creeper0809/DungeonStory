using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IStaffWorkPriorityPanelUiFactory
{
    RectTransform EnsureRectTransform(GameObject target);
    GameObject CreateUiObject(string name, Transform parent);
    Image AddImage(GameObject target, Color color);
    ScrollRect AddScrollRect(GameObject target);
    Mask AddMask(GameObject target, bool showMaskGraphic);
    VerticalLayoutGroup AddVerticalLayoutGroup(GameObject target);
    HorizontalLayoutGroup AddHorizontalLayoutGroup(GameObject target);
    ContentSizeFitter AddContentSizeFitter(GameObject target);
    LayoutElement AddLayoutElement(GameObject target, float width, float height);
    Button AddButton(GameObject target, Graphic targetGraphic);
    TMP_Text AddText(GameObject target);
    Shadow AddShadow(GameObject target, Color effectColor, Vector2 effectDistance);
    void ApplyFonts(Transform root);
    void Release(GameObject target);
}

public sealed class StaffWorkPriorityPanelUiFactory : IStaffWorkPriorityPanelUiFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    public StaffWorkPriorityPanelUiFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public RectTransform EnsureRectTransform(GameObject target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        RectTransform rectTransform = target.GetComponent<RectTransform>();
        return rectTransform != null
            ? rectTransform
            : target.AddComponent<RectTransform>();
    }

    public GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    public Image AddImage(GameObject target, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public ScrollRect AddScrollRect(GameObject target)
    {
        ScrollRect scrollRect = target.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 18f;
        return scrollRect;
    }

    public Mask AddMask(GameObject target, bool showMaskGraphic)
    {
        Mask mask = target.AddComponent<Mask>();
        mask.showMaskGraphic = showMaskGraphic;
        return mask;
    }

    public VerticalLayoutGroup AddVerticalLayoutGroup(GameObject target)
    {
        VerticalLayoutGroup vertical = target.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 1f;
        vertical.childControlWidth = false;
        vertical.childControlHeight = false;
        vertical.childForceExpandWidth = false;
        vertical.childForceExpandHeight = false;
        return vertical;
    }

    public HorizontalLayoutGroup AddHorizontalLayoutGroup(GameObject target)
    {
        HorizontalLayoutGroup horizontal = target.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 1f;
        horizontal.childControlWidth = false;
        horizontal.childControlHeight = false;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;
        return horizontal;
    }

    public ContentSizeFitter AddContentSizeFitter(GameObject target)
    {
        ContentSizeFitter fitter = target.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return fitter;
    }

    public LayoutElement AddLayoutElement(GameObject target, float width, float height)
    {
        LayoutElement layout = target.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
        layout.minWidth = width;
        layout.minHeight = height;
        return layout;
    }

    public Button AddButton(GameObject target, Graphic targetGraphic)
    {
        Button button = target.AddComponent<Button>();
        button.targetGraphic = targetGraphic;
        return button;
    }

    public TMP_Text AddText(GameObject target)
    {
        TMP_Text text = target.AddComponent<TextMeshProUGUI>();
        tmpKoreanFontService.Apply(text);
        return text;
    }

    public Shadow AddShadow(GameObject target, Color effectColor, Vector2 effectDistance)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = effectColor;
        shadow.effectDistance = effectDistance;
        return shadow;
    }

    public void ApplyFonts(Transform root)
    {
        tmpKoreanFontService.ApplyToChildren(root);
    }

    public void Release(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(false);

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

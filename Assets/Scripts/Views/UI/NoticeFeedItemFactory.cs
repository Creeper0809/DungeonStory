using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public interface INoticeFeedItemFactory
{
    GameObject Create(GameObject prefab);
    bool TryPrepare(GameObject noticeObject, Transform parent, NoticeFeedEvent notice, out TMP_Text text);
    void OnTake(GameObject noticeObject);
    void OnReturn(GameObject noticeObject);
    void DestroyItem(GameObject noticeObject);
}

public sealed class NoticeFeedItemFactory : INoticeFeedItemFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    [Inject]
    public NoticeFeedItemFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public GameObject Create(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("NoticeFeed requires textPrefab.");
            return null;
        }

        return UnityEngine.Object.Instantiate(prefab);
    }

    public bool TryPrepare(GameObject noticeObject, Transform parent, NoticeFeedEvent notice, out TMP_Text text)
    {
        text = null;
        if (noticeObject == null || parent == null)
        {
            return false;
        }

        noticeObject.transform.SetParent(parent, false);
        noticeObject.transform.SetAsFirstSibling();
        noticeObject.transform.localScale = Vector3.one;
        SetLayerRecursively(noticeObject, parent.gameObject.layer);

        RectTransform rootRect = noticeObject.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = new Vector2(0f, 0.5f);
            rootRect.anchorMax = new Vector2(1f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(0f, 56f);
        }

        LayoutElement layoutElement = noticeObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = noticeObject.AddComponent<LayoutElement>();
        }
        layoutElement.minHeight = 56f;
        layoutElement.preferredHeight = 56f;
        layoutElement.flexibleWidth = 1f;

        Image background = noticeObject.GetComponent<Image>();
        if (background != null)
        {
            background.color = DungeonUiTheme.SurfaceRaised;
            background.raycastTarget = false;
        }

        CanvasGroup canvasGroup = noticeObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = noticeObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        text = noticeObject.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
        {
            return false;
        }

        tmpKoreanFontService.Apply(text);
        text.text = notice.notice;
        text.color = GetColor(notice.grade);
        text.fontSize = 18f;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;

        ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.enabled = false;
        }

        if (text.rectTransform != null)
        {
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = new Vector2(-28f, -12f);
        }
        return true;
    }

    public void OnTake(GameObject noticeObject)
    {
        if (noticeObject != null)
        {
            CanvasGroup canvasGroup = noticeObject.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            noticeObject.SetActive(true);
        }
    }

    public void OnReturn(GameObject noticeObject)
    {
        if (noticeObject != null)
        {
            CanvasGroup canvasGroup = noticeObject.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            noticeObject.SetActive(false);
        }
    }

    public void DestroyItem(GameObject noticeObject)
    {
        if (noticeObject != null)
        {
            UnityEngine.Object.Destroy(noticeObject);
        }
    }

    private static Color GetColor(NoticeFeedEvent.Grade grade)
    {
        return grade switch
        {
            NoticeFeedEvent.Grade.DANGER => DungeonUiTheme.Danger,
            NoticeFeedEvent.Grade.WARNING => DungeonUiTheme.Warning,
            _ => DungeonUiTheme.TextPrimary
        };
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}

using System;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class NoticeFeed : MonoBehaviour
{
    public GameObject textPrefab;
    private INoticeFeedPresenter presenter;
    private IGameEventBus gameEventBus;
    private IDisposable noticeSubscription;

    private void Awake()
    {
        ConfigureLayout();
    }

    [Inject]
    public void ConstructNoticeFeed(
        INoticeFeedPresenter presenter,
        IGameEventBus gameEventBus)
    {
        this.presenter = presenter
            ?? throw new System.ArgumentNullException(nameof(presenter));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToNotices();
    }

    public virtual void OnTriggerEvent(NoticeFeedEvent e)
    {
        RequirePresenter().Present(textPrefab, transform, e);
    }

    private void OnEnable()
    {
        ConfigureLayout();
        SubscribeToNotices();
    }
    private void OnDisable()
    {
        noticeSubscription?.Dispose();
        noticeSubscription = null;
    }

    private void SubscribeToNotices()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        noticeSubscription ??=
            gameEventBus.Subscribe<NoticeFeedEvent>(OnTriggerEvent);
    }

    private INoticeFeedPresenter RequirePresenter()
    {
        return presenter
            ?? throw new System.InvalidOperationException(
                $"{nameof(NoticeFeed)} requires VContainer injection of {nameof(INoticeFeedPresenter)}.");
    }

    private void ConfigureLayout()
    {
        if (transform is RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(520f, 360f);
        }

        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }
}

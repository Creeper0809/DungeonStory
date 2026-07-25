using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;

public interface INoticeFeedPresenter
{
    void Present(GameObject prefab, Transform parent, NoticeFeedEvent notice);
}

public sealed class NoticeFeedPresenter : INoticeFeedPresenter
{
    private const float VisibleSeconds = 2f;
    private const float FadeSeconds = 1f;
    private const int DefaultPoolCapacity = 5;
    private const int MaxPoolSize = 15;
    private const int MaxVisibleItems = 6;

    private readonly INoticeFeedItemFactory itemFactory;
    private readonly Dictionary<GameObject, IObjectPool<GameObject>> poolsByPrefab =
        new Dictionary<GameObject, IObjectPool<GameObject>>();

    public NoticeFeedPresenter(INoticeFeedItemFactory itemFactory)
    {
        this.itemFactory = itemFactory
            ?? throw new ArgumentNullException(nameof(itemFactory));
    }

    public void Present(GameObject prefab, Transform parent, NoticeFeedEvent notice)
    {
        if (prefab == null || parent == null)
        {
            return;
        }

        IObjectPool<GameObject> pool = GetPool(prefab);
        GameObject noticeObject = pool.Get();
        if (noticeObject == null)
        {
            return;
        }

        if (!itemFactory.TryPrepare(noticeObject, parent, notice, out _))
        {
            pool.Release(noticeObject);
            return;
        }

        TrimVisibleNotices(parent, pool, noticeObject);
        CanvasGroup canvasGroup = noticeObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = noticeObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 1f;

        DOTween.Sequence()
            .PrependInterval(VisibleSeconds)
            .Append(canvasGroup.DOFade(0f, FadeSeconds))
            .SetTarget(noticeObject)
            .SetLink(noticeObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() => pool.Release(noticeObject))
            .Play();
    }

    private static void TrimVisibleNotices(
        Transform parent,
        IObjectPool<GameObject> pool,
        GameObject current)
    {
        while (CountVisibleNotices(parent, current) >= MaxVisibleItems)
        {
            GameObject oldest = FindOldestVisibleNotice(parent, current);
            if (oldest == null)
            {
                return;
            }

            DOTween.Kill(oldest, complete: false);
            pool.Release(oldest);
        }
    }

    private static int CountVisibleNotices(Transform parent, GameObject current)
    {
        int count = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (child != current && child.activeSelf)
            {
                count++;
            }
        }

        return count;
    }

    private static GameObject FindOldestVisibleNotice(Transform parent, GameObject current)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            if (child != current && child.activeSelf)
            {
                return child;
            }
        }

        return null;
    }

    private IObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (!poolsByPrefab.TryGetValue(prefab, out IObjectPool<GameObject> pool) || pool == null)
        {
            pool = CreatePool(prefab);
            poolsByPrefab[prefab] = pool;
        }

        return pool;
    }

    private IObjectPool<GameObject> CreatePool(GameObject prefab)
    {
        return new ObjectPool<GameObject>(
            () => itemFactory.Create(prefab),
            itemFactory.OnTake,
            itemFactory.OnReturn,
            itemFactory.DestroyItem,
            true,
            DefaultPoolCapacity,
            MaxPoolSize);
    }
}

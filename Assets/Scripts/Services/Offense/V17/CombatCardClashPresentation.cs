using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public sealed class CombatCardPresentationRecipe
{
    public string allyName;
    public string enemyName;
    public string commandName;
    public OffenseTacticalTag tacticalTag;
    public CombatDamageType damageType;
    public int allyStages;
    public int enemyStages;
    public int allyStagesRemaining;
    public int enemyStagesRemaining;
    public bool ultimate;
}

public interface ICombatCardPresentationService
{
    event Action<IReadOnlyList<CombatCardPresentationRecipe>>
        PresentationRequested;
    void Present(IEnumerable<CombatCardPresentationRecipe> recipes);
}

public sealed class CombatCardPresentationService :
    ICombatCardPresentationService
{
    public event Action<IReadOnlyList<CombatCardPresentationRecipe>>
        PresentationRequested;

    public void Present(IEnumerable<CombatCardPresentationRecipe> recipes)
    {
        CombatCardPresentationRecipe[] snapshot = (recipes
                ?? Array.Empty<CombatCardPresentationRecipe>())
            .Where(recipe => recipe != null)
            .Select(Clone)
            .ToArray();
        if (snapshot.Length > 0)
        {
            PresentationRequested?.Invoke(snapshot);
        }
    }

    private static CombatCardPresentationRecipe Clone(
        CombatCardPresentationRecipe source)
    {
        return new CombatCardPresentationRecipe
        {
            allyName = source.allyName ?? string.Empty,
            enemyName = source.enemyName ?? string.Empty,
            commandName = source.commandName ?? string.Empty,
            tacticalTag = source.tacticalTag,
            damageType = source.damageType,
            allyStages = Mathf.Clamp(source.allyStages, 0, 3),
            enemyStages = Mathf.Clamp(source.enemyStages, 0, 3),
            allyStagesRemaining = Mathf.Clamp(source.allyStagesRemaining, 0, 3),
            enemyStagesRemaining = Mathf.Clamp(source.enemyStagesRemaining, 0, 3),
            ultimate = source.ultimate
        };
    }
}

public sealed class CombatCardClashPresenter : MonoBehaviour
{
    private readonly Queue<CombatCardPresentationRecipe> queue =
        new Queue<CombatCardPresentationRecipe>();
    private ICombatCardPresentationService service;
    private IGameClock gameClock;
    private RectTransform layer;
    private RectTransform allyCard;
    private RectTransform enemyCard;
    private Image allyImage;
    private Image enemyImage;
    private Image flash;
    private TMP_Text allyText;
    private TMP_Text enemyText;
    private CanvasGroup canvasGroup;
    private CombatCardPresentationRecipe current;
    private float elapsed;

    public void Bind(
        RectTransform parent,
        ITmpKoreanFontService font)
    {
        if (parent == null || font == null)
        {
            throw new ArgumentNullException(
                parent == null ? nameof(parent) : nameof(font));
        }

        GameObject layerObject = new GameObject(
            "CombatCardClashLayer",
            typeof(RectTransform),
            typeof(CanvasGroup));
        layerObject.transform.SetParent(parent, false);
        layer = layerObject.GetComponent<RectTransform>();
        layer.anchorMin = new Vector2(0f, 0f);
        layer.anchorMax = new Vector2(0.72f, 0.9f);
        layer.offsetMin = new Vector2(20f, 16f);
        layer.offsetMax = new Vector2(-8f, -10f);
        canvasGroup = layerObject.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        (allyCard, allyImage, allyText) = CreateCard(
            layer,
            "AllyCommandCard",
            font);
        (enemyCard, enemyImage, enemyText) = CreateCard(
            layer,
            "EnemyIntentCard",
            font);

        GameObject flashObject = new GameObject(
            "ClashFlash",
            typeof(RectTransform),
            typeof(Image));
        flashObject.transform.SetParent(layer, false);
        RectTransform flashRect = flashObject.GetComponent<RectTransform>();
        flashRect.anchorMin = flashRect.anchorMax = new Vector2(0.5f, 0.5f);
        flashRect.sizeDelta = new Vector2(46f, 46f);
        flash = flashObject.GetComponent<Image>();
        flash.color = new Color(1f, 0.78f, 0.35f, 0f);
        SetCardsAtRest();
    }

    [Inject]
    public void Construct(
        ICombatCardPresentationService service,
        IGameClock gameClock)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.service.PresentationRequested += OnPresentationRequested;
    }

    private void Update()
    {
        if (current == null || layer == null || gameClock == null)
        {
            return;
        }

        float delta = gameClock.DeltaTime;
        if (delta <= 0f)
        {
            return;
        }

        elapsed += delta;
        AnimateCurrent();
        if (elapsed < 0.82f)
        {
            return;
        }

        current = null;
        if (queue.Count > 0)
        {
            PlayNext();
        }
        else
        {
            canvasGroup.alpha = 0f;
            SetCardsAtRest();
        }
    }

    private void OnDestroy()
    {
        if (service != null)
        {
            service.PresentationRequested -= OnPresentationRequested;
        }
    }

    private void OnPresentationRequested(
        IReadOnlyList<CombatCardPresentationRecipe> recipes)
    {
        foreach (CombatCardPresentationRecipe recipe in recipes
                     ?? Array.Empty<CombatCardPresentationRecipe>())
        {
            if (recipe != null)
            {
                queue.Enqueue(recipe);
            }
        }

        if (current == null && queue.Count > 0)
        {
            PlayNext();
        }
    }

    private void PlayNext()
    {
        current = queue.Dequeue();
        elapsed = 0f;
        canvasGroup.alpha = 1f;
        allyImage.color = ResolveTagColor(current.tacticalTag);
        enemyImage.color = new Color(0.42f, 0.13f, 0.16f, 0.98f);
        allyText.text =
            $"{current.allyName}\n{current.commandName}\n"
            + $"{GetTagLabel(current.tacticalTag)} "
            + BuildSeals(current.allyStages, current.allyStagesRemaining);
        enemyText.text =
            $"{current.enemyName}\n적 의도\n"
            + BuildSeals(current.enemyStages, current.enemyStagesRemaining);
        SetCardsAtRest();
    }

    private void AnimateCurrent()
    {
        float approach = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Clamp01(elapsed / 0.3f));
        float recoil = Mathf.Clamp01((elapsed - 0.46f) / 0.3f);
        float allyOutcome = current.allyStagesRemaining > 0 ? 1f : -1f;
        float enemyOutcome = current.enemyStagesRemaining > 0 ? -1f : 1f;

        Vector2 allyStart = new Vector2(-310f, -20f);
        Vector2 enemyStart = new Vector2(310f, 20f);
        Vector2 allyCenter = new Vector2(-104f, -6f);
        Vector2 enemyCenter = new Vector2(104f, 6f);
        allyCard.anchoredPosition = Vector2.Lerp(
            allyStart,
            allyCenter,
            approach) + Vector2.right * allyOutcome * recoil * 75f;
        enemyCard.anchoredPosition = Vector2.Lerp(
            enemyStart,
            enemyCenter,
            approach) + Vector2.right * enemyOutcome * recoil * 75f;

        float flashAlpha = elapsed is >= 0.28f and <= 0.52f
            ? 1f - Mathf.Abs(elapsed - 0.4f) / 0.12f
            : 0f;
        Color flashColor = ResolveDamageColor(current.damageType);
        flashColor.a = Mathf.Clamp01(flashAlpha);
        flash.color = flashColor;
        flash.rectTransform.localScale =
            Vector3.one * Mathf.Lerp(0.65f, current.ultimate ? 2f : 1.35f, flashAlpha);
        canvasGroup.alpha = elapsed > 0.68f
            ? Mathf.Clamp01((0.82f - elapsed) / 0.14f)
            : 1f;
    }

    private void SetCardsAtRest()
    {
        if (allyCard == null || enemyCard == null)
        {
            return;
        }

        allyCard.anchoredPosition = new Vector2(-310f, -20f);
        enemyCard.anchoredPosition = new Vector2(310f, 20f);
        allyCard.localRotation = Quaternion.Euler(0f, 0f, -2f);
        enemyCard.localRotation = Quaternion.Euler(0f, 0f, 2f);
    }

    private static (
        RectTransform rect,
        Image image,
        TMP_Text text) CreateCard(
        RectTransform parent,
        string name,
        ITmpKoreanFontService font)
    {
        GameObject cardObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));
        cardObject.transform.SetParent(parent, false);
        RectTransform rect = cardObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(190f, 116f);
        Image image = cardObject.GetComponent<Image>();

        GameObject textObject = OffensePanelUiFactory.CreateText(
            cardObject.transform,
            "CardText",
            17f,
            TextAlignmentOptions.Center,
            font);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 8f);
        textRect.offsetMax = new Vector2(-10f, -8f);
        return (rect, image, textObject.GetComponent<TMP_Text>());
    }

    private static string BuildSeals(int total, int remaining)
    {
        total = Mathf.Clamp(total, 0, 3);
        remaining = Mathf.Clamp(remaining, 0, total);
        return new string('◆', remaining) + new string('◇', total - remaining);
    }

    private static string GetTagLabel(OffenseTacticalTag tag)
    {
        return tag switch
        {
            OffenseTacticalTag.Intercept => "저지",
            OffenseTacticalTag.Maneuver => "기동",
            OffenseTacticalTag.Break => "파쇄",
            OffenseTacticalTag.Support => "지원",
            OffenseTacticalTag.Execute => "집행",
            _ => "일반"
        };
    }

    private static Color ResolveTagColor(OffenseTacticalTag tag)
    {
        return tag switch
        {
            OffenseTacticalTag.Intercept => new Color(0.22f, 0.38f, 0.46f, 0.98f),
            OffenseTacticalTag.Maneuver => new Color(0.2f, 0.42f, 0.3f, 0.98f),
            OffenseTacticalTag.Break => new Color(0.5f, 0.27f, 0.15f, 0.98f),
            OffenseTacticalTag.Support => new Color(0.33f, 0.28f, 0.48f, 0.98f),
            OffenseTacticalTag.Execute => new Color(0.5f, 0.13f, 0.18f, 0.98f),
            _ => new Color(0.23f, 0.25f, 0.28f, 0.98f)
        };
    }

    private static Color ResolveDamageColor(CombatDamageType type)
    {
        return type switch
        {
            CombatDamageType.Slash => new Color(0.95f, 0.3f, 0.22f, 1f),
            CombatDamageType.Pierce => new Color(0.95f, 0.78f, 0.28f, 1f),
            CombatDamageType.Blunt => new Color(0.72f, 0.72f, 0.78f, 1f),
            _ => new Color(0.46f, 0.72f, 0.95f, 1f)
        };
    }
}

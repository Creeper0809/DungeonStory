using System;
using DamageNumbersPro;
using UnityEngine;

public interface IFloatingIconFeedbackService
{
    bool Show(Component target, Sprite sprite, float maxWorldSize);
}

public static class FloatingIconFeedbackDefaults
{
    public const float DefaultMaxWorldSize = 0.45f;
}

public sealed class GameManagerFloatingIconFeedbackService : IFloatingIconFeedbackService
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public GameManagerFloatingIconFeedbackService(
        DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public bool Show(Component target, Sprite sprite, float maxWorldSize)
    {
        if (target == null || sprite == null)
        {
            return false;
        }

        DamageNumber iconNumber = ResolveNumber(NumberCondition.ONBUYINGITEM);
        DamageNumber number = iconNumber.Spawn(target.transform.position + Vector3.up);
        SpriteRenderer iconRenderer = number != null ? number.GetComponent<SpriteRenderer>() : null;
        if (iconRenderer == null)
        {
            throw new InvalidOperationException($"{nameof(NumberCondition.ONBUYINGITEM)} feedback prefab must provide a {nameof(SpriteRenderer)}.");
        }

        iconRenderer.sprite = sprite;
        FitIcon(iconRenderer, maxWorldSize);
        return true;
    }

    private DamageNumber ResolveNumber(NumberCondition condition)
    {
        GameManager manager = ResolveGameManager();
        if (manager.numbers == null)
        {
            throw new InvalidOperationException($"{nameof(GameManager)} has no feedback number dictionary.");
        }

        if (!manager.numbers.TryGetValue(condition, out DamageNumber number) || number == null)
        {
            throw new InvalidOperationException($"{nameof(GameManager)} is missing feedback number prefab for {condition}.");
        }

        return number;
    }

    private GameManager ResolveGameManager()
    {
        return sceneReferences.GameManager != null
            ? sceneReferences.GameManager
            : throw new InvalidOperationException($"{nameof(IFloatingIconFeedbackService)} requires a loaded {nameof(GameManager)}.");
    }

    private static void FitIcon(SpriteRenderer iconRenderer, float maxWorldSize)
    {
        if (iconRenderer == null || iconRenderer.sprite == null)
        {
            return;
        }

        Vector2 spriteSize = iconRenderer.sprite.bounds.size;
        float longestSide = Mathf.Max(spriteSize.x, spriteSize.y);
        if (longestSide <= Mathf.Epsilon)
        {
            return;
        }

        float safeMaxWorldSize = maxWorldSize > 0f
            ? maxWorldSize
            : FloatingIconFeedbackDefaults.DefaultMaxWorldSize;
        float fittedScale = Mathf.Min(1f, safeMaxWorldSize / longestSide);
        iconRenderer.drawMode = SpriteDrawMode.Simple;
        iconRenderer.transform.localScale = new Vector3(
            fittedScale,
            fittedScale,
            iconRenderer.transform.localScale.z);
    }
}

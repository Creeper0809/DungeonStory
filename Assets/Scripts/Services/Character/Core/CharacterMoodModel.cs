using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterMoodFactorKind
{
    Need,
    Interaction
}

public sealed class CharacterMoodFactorSnapshot
{
    public CharacterMoodFactorSnapshot(
        string id,
        string label,
        float value,
        CharacterMoodFactorKind kind,
        float remainingSeconds)
    {
        Id = id ?? string.Empty;
        Label = label ?? string.Empty;
        Value = value;
        Kind = kind;
        RemainingSeconds = Mathf.Max(0f, remainingSeconds);
    }

    public string Id { get; }
    public string Label { get; }
    public float Value { get; }
    public CharacterMoodFactorKind Kind { get; }
    public float RemainingSeconds { get; }
}

public sealed class CharacterMoodSnapshot
{
    public CharacterMoodSnapshot(
        float value,
        float baseValue,
        IReadOnlyList<CharacterMoodFactorSnapshot> factors)
    {
        Value = Mathf.Clamp(value, 0f, 100f);
        BaseValue = Mathf.Clamp(baseValue, 0f, 100f);
        Factors = factors ?? Array.Empty<CharacterMoodFactorSnapshot>();
    }

    public float Value { get; }
    public float BaseValue { get; }
    public IReadOnlyList<CharacterMoodFactorSnapshot> Factors { get; }
    public float TotalOffset => Value - BaseValue;
}

[Serializable]
public sealed class CharacterMoodMemory
{
    [SerializeField] private string id;
    [SerializeField] private string label;
    [SerializeField] private float valuePerStack;
    [SerializeField] private int stacks = 1;
    [SerializeField] private int maxStacks = 1;
    [SerializeField] private float expiresAt;

    public string Id => id;
    public float ExpiresAt => expiresAt;

    public CharacterMoodMemory(
        string factorId,
        string factorLabel,
        float value,
        float durationSeconds,
        int stackLimit,
        float now)
    {
        id = factorId;
        Apply(factorLabel, value, durationSeconds, stackLimit, now, resetStacks: true);
    }

    public void Apply(
        string factorLabel,
        float value,
        float durationSeconds,
        int stackLimit,
        float now,
        bool resetStacks = false)
    {
        int nextLimit = Mathf.Clamp(stackLimit, 1, 8);
        bool signChanged = !Mathf.Approximately(valuePerStack, 0f)
            && Mathf.Sign(valuePerStack) != Mathf.Sign(value);
        if (resetStacks || signChanged)
        {
            stacks = 1;
        }
        else if (nextLimit > 1)
        {
            stacks = Mathf.Min(stacks + 1, nextLimit);
        }
        else
        {
            stacks = 1;
        }

        label = factorLabel;
        valuePerStack = Mathf.Clamp(value, -25f, 25f);
        maxStacks = nextLimit;
        stacks = Mathf.Clamp(stacks, 1, maxStacks);
        expiresAt = now + Mathf.Max(0.25f, durationSeconds);
    }

    public bool IsExpired(float now)
    {
        return now >= expiresAt;
    }

    public CharacterMoodFactorSnapshot CreateSnapshot(float now)
    {
        float totalValue = Mathf.Clamp(valuePerStack * stacks, -25f, 25f);
        string displayLabel = stacks > 1 ? $"{label} x{stacks}" : label;
        return new CharacterMoodFactorSnapshot(
            id,
            displayLabel,
            totalValue,
            CharacterMoodFactorKind.Interaction,
            expiresAt - now);
    }
}

public static class CharacterMoodRules
{
    public const float DefaultBaseMood = 55f;

    public static List<CharacterMoodFactorSnapshot> BuildNeedFactors(
        IReadOnlyDictionary<CharacterCondition, float> stats)
    {
        List<CharacterMoodFactorSnapshot> factors = new List<CharacterMoodFactorSnapshot>();
        foreach (CharacterNeedDefinition definition in CharacterNeedCatalog.All)
        {
            if (definition.HasTag(CharacterNeedTag.MoodInteraction)
                && definition.TryCreateMoodFactor(stats, out CharacterMoodFactorSnapshot factor))
            {
                factors.Add(factor);
            }
        }

        return factors;
    }

    public static string GetMoodLabel(float mood)
    {
        if (mood < 15f) return "절망적";
        if (mood < 30f) return "불쾌함";
        if (mood < 45f) return "가라앉음";
        if (mood < 60f) return "평온함";
        if (mood < 80f) return "만족함";
        return "아주 좋음";
    }

}

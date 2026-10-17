using System;
using System.Collections.Generic;
using UnityEngine;

public static class DefenseEffectIds
{
    public const string Damage = "defense.damage";
    public const string Corrosion = "defense.corrosion";
    public const string Burn = "defense.burn";
    public const string Charge = "defense.charge";
    public const string Slow = "defense.slow";
    public const string GuardAttack = "defense.guard-attack";
}

public abstract class DefenseEffectSO : ScriptableObject
{
    [SerializeField, Min(0f)] private float amount;
    [SerializeField, Min(0f)] private float duration;
    [SerializeField, Min(1)] private int stacks = 1;
    [SerializeField] private string logTag;

    public abstract string EffectId { get; }
    public abstract string DisplayName { get; }

    public float Amount
    {
        get => amount;
        set => amount = Mathf.Max(0f, value);
    }

    public float Duration
    {
        get => duration;
        set => duration = Mathf.Max(0f, value);
    }

    public int Stacks
    {
        get => stacks;
        set => stacks = Mathf.Max(1, value);
    }

    public string LogTag
    {
        get => logTag;
        set => logTag = value;
    }

    protected string EffectiveLogTag => string.IsNullOrWhiteSpace(logTag) ? DisplayName : logTag;

    public void Configure(float configuredAmount, float configuredDuration, int configuredStacks, string configuredLogTag)
    {
        Amount = configuredAmount;
        Duration = configuredDuration;
        Stacks = configuredStacks;
        LogTag = configuredLogTag;
    }

    public abstract void Apply(IDefenseEffectCommands context);

    public virtual string FormatSummary()
    {
        List<string> parts = new List<string> { DisplayName };
        if (amount > 0f)
        {
            parts.Add($"{amount:0.#}");
        }

        if (duration > 0f)
        {
            parts.Add($"{duration:0.#}초");
        }

        if (stacks > 1)
        {
            parts.Add($"{stacks}중첩");
        }

        return string.Join(" ", parts);
    }

    protected virtual void OnValidate()
    {
        amount = Mathf.Max(0f, amount);
        duration = Mathf.Max(0f, duration);
        stacks = Mathf.Max(1, stacks);
    }
}

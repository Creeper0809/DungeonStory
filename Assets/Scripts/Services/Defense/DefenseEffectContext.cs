using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DefenseEffectContext : IDefenseEffectCommands
{
    private readonly ICharacterBodyHealthMutationTransaction bodyHealthMutation;
    private readonly List<CharacterPreparedAggregateDamageReceipt>
        preparedDamageReceipts;

    public DefenseEffectContext(
        CharacterActor target,
        DefenseStatusRuntime statusRuntime,
        DefenseActivationReport report,
        DefenseFacilityData defense,
        float effectMultiplier = 1f)
        : this(
            target,
            statusRuntime,
            report,
            defense,
            effectMultiplier,
            null,
            null)
    {
    }

    internal DefenseEffectContext(
        CharacterActor target,
        DefenseStatusRuntime statusRuntime,
        DefenseActivationReport report,
        DefenseFacilityData defense,
        float effectMultiplier,
        ICharacterBodyHealthMutationTransaction bodyHealthMutation,
        List<CharacterPreparedAggregateDamageReceipt> preparedDamageReceipts)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        StatusRuntime = statusRuntime ?? throw new ArgumentNullException(nameof(statusRuntime));
        Report = report ?? throw new ArgumentNullException(nameof(report));
        Defense = defense ?? throw new ArgumentNullException(nameof(defense));
        EffectMultiplier = Mathf.Clamp(effectMultiplier, 0f, 2f);
        this.bodyHealthMutation = bodyHealthMutation;
        this.preparedDamageReceipts = preparedDamageReceipts;
    }

    public CharacterActor Target { get; }
    public DefenseStatusRuntime StatusRuntime { get; }
    public DefenseActivationReport Report { get; }
    public DefenseFacilityData Defense { get; }
    public float EffectMultiplier { get; }
    public string CombatLogText => Defense.combatLogText;

    public void ApplyDamage(float amount, string reason)
    {
        if (amount <= 0f || Target.IsDead)
        {
            return;
        }

        float finalDamage = amount
            * EffectMultiplier
            * StatusRuntime.GetIncomingDamageMultiplier();
        if (finalDamage <= 0f)
        {
            return;
        }
        if (bodyHealthMutation != null)
        {
            CharacterPreparedAggregateDamageReceipt receipt =
                bodyHealthMutation.ApplyPreparedAggregateDamage(
                    Target,
                    finalDamage,
                    CharacterDeathCauseCode.Combat,
                    string.IsNullOrWhiteSpace(reason)
                        ? "defense-facility-effect"
                        : reason,
                    allowDeath: true);
            preparedDamageReceipts?.Add(receipt);
            Report.AddDamage(receipt.AppliedDamage);
            return;
        }

        Target.ApplyDamage(finalDamage, reason);
        Report.AddDamage(finalDamage);
    }

    public int ApplyStatus(DefenseStatusKind kind, float value, float duration, int stacks)
    {
        return StatusRuntime.ApplyStatus(
            kind,
            value * EffectMultiplier,
            duration * EffectMultiplier,
            stacks);
    }

    public void ClearStatus(DefenseStatusKind kind)
    {
        StatusRuntime.ClearStatus(kind);
    }

    public void AddMovementDelay(float seconds)
    {
        Report.AddMovementDelay(seconds * EffectMultiplier);
    }

    public void AddEffectTag(string tag)
    {
        Report.AddEffectTag(tag);
    }

    public void RecordEnvironmentalIgnition(float ignitionIntensity)
    {
        Report.AddEnvironmentalIgnition(
            Mathf.Clamp01(ignitionIntensity * EffectMultiplier));
    }
}


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static ExtremeTraitRuleAccess;
using static ExtremeTraitRuleIds;

internal sealed class ArcaneOverchargeTraitRuntime
{
    private readonly CharacterIdentityStateStore states;

    internal ArcaneOverchargeTraitRuntime(CharacterIdentityStateStore states) =>
        this.states = states ?? throw new ArgumentNullException(nameof(states));

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryActivateArcaneOvercharge(
        CharacterActor actor,
        string activationId,
        float manaRatio,
        float elapsedSeconds,
        out ArcaneOverchargeActivation activation)
    {
        activation = default;
        ArcaneOverchargeRule rule = ResolveRule<ArcaneOverchargeRule>(
            actor, 306, ArcaneOverchargeRuleId, out CharacterTraitSO trait);
        if (rule == null || Mathf.Clamp01(manaRatio) >= rule.manaThreshold) return false;
        ArcaneOverchargeRuntimeState state = Read<ArcaneOverchargeRuntimeState>(states,
            actor, trait, ArcaneOverchargeRuleId);
        float now = Mathf.Max(0f, elapsedSeconds);
        if (state.activeUntilSeconds > now || state.aftermathUntilSeconds > now) return false;
        state.activationId = Required(activationId, nameof(activationId));
        state.activeUntilSeconds = now + Mathf.Max(1, rule.durationSeconds);
        state.aftermathUntilSeconds = state.activeUntilSeconds
            + DaysToSeconds(rule.aftermathDays);
        ResetArcaneOperation(state);
        state.selfDamageFraction = rule.selfDamageFraction;
        state.equipmentDurabilityFraction =
            rule.equipmentDurabilityFraction;
        state.effectsCompleted = true;
        activation = new ArcaneOverchargeActivation(
            rule.selfDamageFraction,
            rule.equipmentDurabilityFraction);
        Write(states, actor, trait, ArcaneOverchargeRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryBeginOrResumeArcaneOvercharge(
        CharacterActor actor,
        string activationId,
        string equipmentInstanceId,
        float manaRatio,
        float elapsedSeconds,
        out ArcaneOverchargeOperationSnapshot operation)
    {
        operation = default;
        ArcaneOverchargeRule rule = ResolveRule<ArcaneOverchargeRule>(
            actor, 306, ArcaneOverchargeRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;

        string equipmentId = equipmentInstanceId?.Trim() ?? string.Empty;
        if (equipmentId.Length == 0)
            return false;

        ArcaneOverchargeRuntimeState state = Read<ArcaneOverchargeRuntimeState>(states,
            actor, trait, ArcaneOverchargeRuleId);
        if (IsPendingArcaneOperation(state))
        {
            if (!string.Equals(
                    state.equipmentInstanceId,
                    equipmentId,
                    StringComparison.Ordinal))
                return false;
            operation = CaptureArcaneOperation(state);
            return true;
        }

        float now = Mathf.Max(0f, elapsedSeconds);
        if (Mathf.Clamp01(manaRatio) >= rule.manaThreshold
            || state.activeUntilSeconds > now
            || state.aftermathUntilSeconds > now)
            return false;

        state.activationId = Required(activationId, nameof(activationId));
        state.activeUntilSeconds = now + Mathf.Max(1, rule.durationSeconds);
        state.aftermathUntilSeconds = state.activeUntilSeconds
            + DaysToSeconds(rule.aftermathDays);
        ResetArcaneOperation(state);
        state.equipmentInstanceId = equipmentId;
        state.selfDamageFraction = rule.selfDamageFraction;
        state.equipmentDurabilityFraction =
            rule.equipmentDurabilityFraction;
        Write(states, actor, trait, ArcaneOverchargeRuleId, state);
        operation = CaptureArcaneOperation(state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryRecordArcaneOverchargeFailure(
        CharacterActor actor,
        string activationId,
        string failureCode)
    {
        if (!TryReadArcaneOperation(
                actor,
                activationId,
                out CharacterTraitSO trait,
                out ArcaneOverchargeRuntimeState state)
            || state.effectsCompleted)
            return false;
        string code = failureCode?.Trim() ?? string.Empty;
        if (code.Length == 0)
            return false;
        state.failureCount++;
        state.lastFailureCode = code;
        Write(states, actor, trait, ArcaneOverchargeRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryRecordArcaneOverchargeSelfDamage(
        CharacterActor actor,
        string activationId,
        float healthBefore,
        float healthAfter)
    {
        if (!TryReadArcaneOperation(
                actor,
                activationId,
                out CharacterTraitSO trait,
                out ArcaneOverchargeRuntimeState state)
            || state.effectsCompleted
            || state.equipmentDurabilityApplied
            || !FiniteNonNegative(healthAfter)
            || !FinitePositive(healthBefore)
            || healthAfter >= healthBefore)
            return false;
        if (state.selfDamageApplied)
            return Mathf.Approximately(state.healthBefore, healthBefore)
                && Mathf.Approximately(state.healthAfter, healthAfter);
        state.selfDamageApplied = true;
        state.healthBefore = healthBefore;
        state.healthAfter = healthAfter;
        Write(states, actor, trait, ArcaneOverchargeRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryRecordArcaneOverchargeDurability(
        CharacterActor actor,
        string activationId,
        float durabilityBefore,
        float durabilityAfter)
    {
        if (!TryReadArcaneOperation(
                actor,
                activationId,
                out CharacterTraitSO trait,
                out ArcaneOverchargeRuntimeState state)
            || state.effectsCompleted
            || !state.selfDamageApplied
            || !FiniteNonNegative(durabilityAfter)
            || !FinitePositive(durabilityBefore)
            || durabilityAfter >= durabilityBefore)
            return false;
        if (state.equipmentDurabilityApplied)
            return Mathf.Approximately(
                    state.durabilityBefore,
                    durabilityBefore)
                && Mathf.Approximately(
                    state.durabilityAfter,
                    durabilityAfter);
        state.equipmentDurabilityApplied = true;
        state.durabilityBefore = durabilityBefore;
        state.durabilityAfter = durabilityAfter;
        Write(states, actor, trait, ArcaneOverchargeRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryCompleteArcaneOvercharge(
        CharacterActor actor,
        string activationId)
    {
        if (!TryReadArcaneOperation(
                actor,
                activationId,
                out CharacterTraitSO trait,
                out ArcaneOverchargeRuntimeState state)
            || state.effectsCompleted
            || !state.selfDamageApplied
            || !state.equipmentDurabilityApplied)
            return false;
        state.effectsCompleted = true;
        Write(states, actor, trait, ArcaneOverchargeRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryGetArcaneOverchargeOperation(
        CharacterActor actor,
        out ArcaneOverchargeOperationSnapshot operation)
    {
        operation = default;
        ArcaneOverchargeRule rule = ResolveRule<ArcaneOverchargeRule>(
            actor, 306, ArcaneOverchargeRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ArcaneOverchargeRuntimeState state = Read<ArcaneOverchargeRuntimeState>(states,
            actor, trait, ArcaneOverchargeRuleId);
        if (string.IsNullOrWhiteSpace(state.activationId))
            return false;
        operation = CaptureArcaneOperation(state);
        return operation.IsValid;
    }

    internal void AddConditions(CharacterActor actor, float now, ICollection<string> result)
    {
        ArcaneOverchargeRule rule = ResolveRule<ArcaneOverchargeRule>(
            actor, 306, ArcaneOverchargeRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        ArcaneOverchargeRuntimeState state = Read<ArcaneOverchargeRuntimeState>(states,
            actor, trait, ArcaneOverchargeRuleId);
        if (state.activeUntilSeconds > now) result.Add("state:arcane-overcharge");
        else if (state.aftermathUntilSeconds > now)
            result.Add("state:arcane-overcharge-aftermath");
    }

    private static bool FiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private bool TryReadArcaneOperation(
        CharacterActor actor,
        string activationId,
        out CharacterTraitSO trait,
        out ArcaneOverchargeRuntimeState state)
    {
        state = null;
        ArcaneOverchargeRule rule = ResolveRule<ArcaneOverchargeRule>(
            actor, 306, ArcaneOverchargeRuleId, out trait);
        if (rule == null)
            return false;
        state = Read<ArcaneOverchargeRuntimeState>(states,
            actor, trait, ArcaneOverchargeRuleId);
        return IsPendingArcaneOperation(state)
            && string.Equals(
                state.activationId,
                activationId?.Trim(),
                StringComparison.Ordinal);
    }

    private static bool IsPendingArcaneOperation(
        ArcaneOverchargeRuntimeState state) => state != null
        && !state.effectsCompleted
        && !string.IsNullOrWhiteSpace(state.activationId)
        && !string.IsNullOrWhiteSpace(state.equipmentInstanceId);

    private static ArcaneOverchargeOperationSnapshot CaptureArcaneOperation(
        ArcaneOverchargeRuntimeState state) => new(
        state.activationId,
        state.equipmentInstanceId,
        state.selfDamageFraction,
        state.equipmentDurabilityFraction,
        state.selfDamageApplied,
        state.healthBefore,
        state.healthAfter,
        state.equipmentDurabilityApplied,
        state.durabilityBefore,
        state.durabilityAfter,
        state.effectsCompleted,
        state.failureCount,
        state.lastFailureCode);

    private static void ResetArcaneOperation(
        ArcaneOverchargeRuntimeState state)
    {
        state.equipmentInstanceId = string.Empty;
        state.selfDamageFraction = 0f;
        state.equipmentDurabilityFraction = 0f;
        state.selfDamageApplied = false;
        state.healthBefore = 0f;
        state.healthAfter = 0f;
        state.equipmentDurabilityApplied = false;
        state.durabilityBefore = 0f;
        state.durabilityAfter = 0f;
        state.effectsCompleted = false;
        state.failureCount = 0;
        state.lastFailureCode = string.Empty;
    }
}

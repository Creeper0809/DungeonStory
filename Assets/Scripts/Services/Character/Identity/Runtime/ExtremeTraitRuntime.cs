using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public enum ExtremeRiskOutcome
{
    Normal,
    Breakthrough,
    Setback,
    Miracle,
    Complication,
    Jackpot,
    Loss
}

public readonly struct ExtremeRiskResolution
{
    public ExtremeRiskResolution(
        ExtremeRiskOutcome outcome,
        float primaryMultiplier,
        float secondaryMultiplier,
        float progressDelta,
        ulong fixedRollHash)
    {
        Outcome = outcome;
        PrimaryMultiplier = primaryMultiplier;
        SecondaryMultiplier = secondaryMultiplier;
        ProgressDelta = progressDelta;
        FixedRollHash = fixedRollHash;
    }

    public ExtremeRiskOutcome Outcome { get; }
    public float PrimaryMultiplier { get; }
    public float SecondaryMultiplier { get; }
    public float ProgressDelta { get; }
    public ulong FixedRollHash { get; }
}

public readonly struct ArcaneOverchargeActivation
{
    public ArcaneOverchargeActivation(
        float selfDamageFraction,
        float equipmentDurabilityFraction)
    {
        SelfDamageFraction = selfDamageFraction;
        EquipmentDurabilityFraction = equipmentDurabilityFraction;
    }

    public float SelfDamageFraction { get; }
    public float EquipmentDurabilityFraction { get; }
}

[Serializable]
public sealed class LastStandRuntimeState
{
    public string encounterId = string.Empty;
    public bool used;
    public bool active;
    public float aftermathUntilSeconds;
}

[Serializable]
public sealed class ForbiddenResearchLeapRuntimeState
{
    public List<string> usedProjectIds = new();
    public float aftermathUntilSeconds;
}

[Serializable]
public sealed class MiracleSurgeryRuntimeState
{
    public List<string> usedSurgeryIds = new();
    public float aftermathUntilSeconds;
}

[Serializable]
public sealed class GoldenHarvestRuntimeState
{
    public string fieldId = string.Empty;
    public int attemptIndex;
    public float resolveAfterSeconds;
    public bool pending;
}

[Serializable]
public sealed class ProductionLimitBreakRuntimeState
{
    public string batchId = string.Empty;
    public bool active;
    public float activeLeaseUntilSeconds;
    public float aftermathUntilSeconds;
}

[Serializable]
public sealed class ArcaneOverchargeRuntimeState
{
    public string activationId = string.Empty;
    public float activeUntilSeconds;
    public float aftermathUntilSeconds;
}

public sealed class ExtremeTraitRuntime
{
    public const string LastStandRuleId = "extreme:last-stand";
    public const string ForbiddenLeapRuleId = "extreme:forbidden-leap";
    public const string MiracleSurgeryRuleId = "extreme:miracle-surgery";
    public const string GoldenHarvestRuleId = "extreme:golden-harvest";
    public const string ProductionLimitBreakRuleId = "extreme:production-limit-break";
    public const string ArcaneOverchargeRuleId = "extreme:arcane-overcharge";

    private readonly CharacterIdentityStateStore states;

    public ExtremeTraitRuntime(CharacterIdentityStateStore states) =>
        this.states = states ?? throw new ArgumentNullException(nameof(states));

    [GameplayInternalOnly(
        "Health-threshold orchestration owns last-stand activation.",
        "CombatRuntimeStatFactory")]
    public bool TryActivateLastStand(
        CharacterActor actor,
        string encounterId,
        float healthRatio,
        bool coreOrganCritical,
        float elapsedSeconds,
        out LastStandRule rule)
    {
        rule = ResolveRule<LastStandRule>(actor, 301, LastStandRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        string requiredEncounter = Required(encounterId, nameof(encounterId));
        LastStandRuntimeState state = Read<LastStandRuntimeState>(actor, trait, LastStandRuleId);
        if (!string.Equals(state.encounterId, requiredEncounter, StringComparison.Ordinal))
        {
            state.encounterId = requiredEncounter;
            state.used = false;
            state.active = false;
        }
        if (state.used
            || (!coreOrganCritical && Mathf.Clamp01(healthRatio) >= rule.healthThreshold))
        {
            return false;
        }
        state.used = true;
        state.active = true;
        Write(actor, trait, LastStandRuleId, state);
        return true;
    }

    [GameplayInternalOnly(
        "Combat lifecycle owns last-stand cleanup and aftermath.",
        "CharacterCombatCommandRuntime")]
    public void EndLastStand(CharacterActor actor, string encounterId, float elapsedSeconds)
    {
        LastStandRule rule = ResolveRule<LastStandRule>(actor, 301, LastStandRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        LastStandRuntimeState state = Read<LastStandRuntimeState>(actor, trait, LastStandRuleId);
        if (!state.active || !string.Equals(state.encounterId, encounterId?.Trim(), StringComparison.Ordinal))
            return;
        state.active = false;
        state.aftermathUntilSeconds = Mathf.Max(0f, elapsedSeconds)
            + DaysToSeconds(rule.aftermathDays);
        Write(actor, trait, LastStandRuleId, state);
    }

    [GameplayInternalOnly(
        "Research work completion owns the fixed project attempt.",
        "BlueprintResearchRuntime")]
    public bool TryResolveForbiddenResearchLeap(
        CharacterActor actor,
        string projectId,
        ulong runSeed,
        float elapsedSeconds,
        out ExtremeRiskResolution resolution)
    {
        resolution = default;
        ForbiddenResearchLeapRule rule = ResolveRule<ForbiddenResearchLeapRule>(
            actor, 302, ForbiddenLeapRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        string project = Required(projectId, nameof(projectId));
        ForbiddenResearchLeapRuntimeState state = Read<ForbiddenResearchLeapRuntimeState>(
            actor, trait, ForbiddenLeapRuleId);
        state.usedProjectIds ??= new List<string>();
        if (state.usedProjectIds.Contains(project, StringComparer.Ordinal)) return false;
        state.usedProjectIds.Add(project);
        state.usedProjectIds.Sort(StringComparer.Ordinal);
        state.aftermathUntilSeconds = Mathf.Max(0f, elapsedSeconds)
            + DaysToSeconds(rule.aftermathDays);
        ulong hash = FixedHash(runSeed, "research", project, ActorId(actor), "302");
        float roll = Roll01(hash);
        resolution = roll < rule.breakthroughChance
            ? new ExtremeRiskResolution(
                ExtremeRiskOutcome.Breakthrough, 1f, 1f,
                Mathf.Abs(rule.breakthroughProgress), hash)
            : roll < rule.breakthroughChance + rule.setbackChance
                ? new ExtremeRiskResolution(
                    ExtremeRiskOutcome.Setback, 1f, 1f,
                    -Mathf.Abs(rule.setbackProgress), hash)
                : new ExtremeRiskResolution(ExtremeRiskOutcome.Normal, 1f, 1f, 0f, hash);
        Write(actor, trait, ForbiddenLeapRuleId, state);
        return true;
    }

    [GameplayInternalOnly(
        "Surgery resolution owns the fixed operation attempt.",
        "SurgeryRuntime")]
    public bool TryResolveMiracleSurgery(
        CharacterActor actor,
        string surgeryId,
        bool isCritical,
        ulong runSeed,
        float elapsedSeconds,
        out ExtremeRiskResolution resolution)
    {
        resolution = default;
        MiracleSurgeryRule rule = ResolveRule<MiracleSurgeryRule>(
            actor, 303, MiracleSurgeryRuleId, out CharacterTraitSO trait);
        if (rule == null || !isCritical) return false;
        string surgery = Required(surgeryId, nameof(surgeryId));
        MiracleSurgeryRuntimeState state = Read<MiracleSurgeryRuntimeState>(
            actor, trait, MiracleSurgeryRuleId);
        state.usedSurgeryIds ??= new List<string>();
        if (state.usedSurgeryIds.Contains(surgery, StringComparer.Ordinal)) return false;
        state.usedSurgeryIds.Add(surgery);
        state.usedSurgeryIds.Sort(StringComparer.Ordinal);
        state.aftermathUntilSeconds = Mathf.Max(0f, elapsedSeconds)
            + DaysToSeconds(rule.aftermathDays);
        ulong hash = FixedHash(runSeed, "surgery", surgery, ActorId(actor), "303");
        float roll = Roll01(hash);
        resolution = roll < rule.miracleChance
            ? new ExtremeRiskResolution(ExtremeRiskOutcome.Miracle, 1f, 1f, 0f, hash)
            : roll < rule.miracleChance + rule.complicationChance
                ? new ExtremeRiskResolution(ExtremeRiskOutcome.Complication, 1f, 1f, 0f, hash)
                : new ExtremeRiskResolution(ExtremeRiskOutcome.Normal, 1f, 1f, 0f, hash);
        Write(actor, trait, MiracleSurgeryRuleId, state);
        return true;
    }

    [GameplayInternalOnly(
        "Crop-plot command validates and persists the scheduled harvester first.",
        "CropPlotRuntime")]
    public bool TryScheduleGoldenHarvest(
        CharacterActor actor,
        string fieldId,
        int attemptIndex,
        float elapsedSeconds)
    {
        GoldenHarvestRule rule = ResolveRule<GoldenHarvestRule>(
            actor, 304, GoldenHarvestRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        GoldenHarvestRuntimeState state = Read<GoldenHarvestRuntimeState>(
            actor, trait, GoldenHarvestRuleId);
        if (state.pending) return false;
        state.fieldId = Required(fieldId, nameof(fieldId));
        state.attemptIndex = Mathf.Max(0, attemptIndex);
        state.resolveAfterSeconds = Mathf.Max(0f, elapsedSeconds)
            + HoursToSeconds(rule.delayHours);
        state.pending = true;
        Write(actor, trait, GoldenHarvestRuleId, state);
        return true;
    }

    [GameplayInternalOnly(
        "Crop harvest completion owns the fixed delayed outcome.",
        "CropPlotRuntime")]
    public bool TryResolveGoldenHarvest(
        CharacterActor actor,
        string fieldId,
        ulong runSeed,
        float elapsedSeconds,
        out ExtremeRiskResolution resolution)
    {
        resolution = default;
        GoldenHarvestRule rule = ResolveRule<GoldenHarvestRule>(
            actor, 304, GoldenHarvestRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        GoldenHarvestRuntimeState state = Read<GoldenHarvestRuntimeState>(
            actor, trait, GoldenHarvestRuleId);
        string field = Required(fieldId, nameof(fieldId));
        if (!state.pending
            || elapsedSeconds < state.resolveAfterSeconds
            || !string.Equals(state.fieldId, field, StringComparison.Ordinal))
            return false;
        ulong hash = FixedHash(
            runSeed, "harvest", field,
            state.attemptIndex.ToString(CultureInfo.InvariantCulture),
            ActorId(actor), "304");
        float roll = Roll01(hash);
        resolution = roll < rule.jackpotChance
            ? new ExtremeRiskResolution(
                ExtremeRiskOutcome.Jackpot,
                1f,
                1f,
                0f,
                hash)
            : roll < rule.jackpotChance + rule.lossChance
                ? new ExtremeRiskResolution(
                    ExtremeRiskOutcome.Loss,
                    rule.failureYieldMultiplier,
                    rule.failureYieldMultiplier,
                    0f,
                    hash)
                : new ExtremeRiskResolution(ExtremeRiskOutcome.Normal, 1f, 1f, 0f, hash);
        state.pending = false;
        state.fieldId = string.Empty;
        Write(actor, trait, GoldenHarvestRuleId, state);
        return true;
    }

    public bool TryGetGoldenHarvestDelay(
        CharacterActor actor,
        string fieldId,
        float elapsedSeconds,
        out float remainingSeconds)
    {
        remainingSeconds = 0f;
        GoldenHarvestRule rule = ResolveRule<GoldenHarvestRule>(
            actor, 304, GoldenHarvestRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        GoldenHarvestRuntimeState state = Read<GoldenHarvestRuntimeState>(
            actor, trait, GoldenHarvestRuleId);
        if (!state.pending
            || !string.Equals(state.fieldId, fieldId?.Trim(), StringComparison.Ordinal))
            return false;
        remainingSeconds = Mathf.Max(0f, state.resolveAfterSeconds - elapsedSeconds);
        return remainingSeconds > 0f;
    }

    [GameplayInternalOnly(
        "Production bill facade prevalidates the exact runnable bill before mutation.",
        "ProductionBillSceneFacade")]
    public bool TryBeginProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds,
        out ProductionLimitBreakRule rule)
    {
        rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(
            actor, trait, ProductionLimitBreakRuleId);
        float now = Mathf.Max(0f, elapsedSeconds);
        if (state.active || state.aftermathUntilSeconds > now) return false;
        state.batchId = Required(batchId, nameof(batchId));
        state.active = true;
        state.activeLeaseUntilSeconds = now + 5f;
        Write(actor, trait, ProductionLimitBreakRuleId, state);
        return true;
    }

    public bool CanBeginProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(
            actor, trait, ProductionLimitBreakRuleId);
        return !string.IsNullOrWhiteSpace(batchId)
            && !state.active
            && state.aftermathUntilSeconds <= Mathf.Max(0f, elapsedSeconds);
    }

    public bool CanConfigureProductionLimitBreak(
        CharacterActor actor,
        float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(
            actor, trait, ProductionLimitBreakRuleId);
        return !state.active
            && state.aftermathUntilSeconds <= Mathf.Max(0f, elapsedSeconds);
    }

    [GameplayInternalOnly(
        "The authoritative production-bill facade refreshes the active batch lease while work remains live.",
        "ProductionBillSceneFacade")]
    public bool RefreshProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(
            actor, trait, ProductionLimitBreakRuleId);
        if (!state.active
            || !string.Equals(
                state.batchId,
                batchId?.Trim(),
                StringComparison.Ordinal))
            return false;
        state.activeLeaseUntilSeconds = Mathf.Max(0f, elapsedSeconds) + 5f;
        Write(actor, trait, ProductionLimitBreakRuleId, state);
        return true;
    }

    [GameplayInternalOnly(
        "The registered extreme-trait lease clock expires abandoned batch leases.",
        "ExtremeTraitLeaseClock")]
    public bool ExpireProductionLimitBreak(
        CharacterActor actor,
        float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(
            actor, trait, ProductionLimitBreakRuleId);
        float now = Mathf.Max(0f, elapsedSeconds);
        if (!state.active || state.activeLeaseUntilSeconds >= now)
            return false;
        state.active = false;
        state.activeLeaseUntilSeconds = 0f;
        state.aftermathUntilSeconds = now + DaysToSeconds(rule.aftermathDays);
        Write(actor, trait, ProductionLimitBreakRuleId, state);
        return true;
    }

    [GameplayInternalOnly(
        "Production completion owns limit-break cleanup and aftermath.",
        "ProductionBillSceneFacade")]
    public void EndProductionLimitBreak(CharacterActor actor, string batchId, float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(
            actor, trait, ProductionLimitBreakRuleId);
        if (!state.active || !string.Equals(state.batchId, batchId?.Trim(), StringComparison.Ordinal))
            return;
        state.active = false;
        state.activeLeaseUntilSeconds = 0f;
        state.aftermathUntilSeconds = Mathf.Max(0f, elapsedSeconds)
            + DaysToSeconds(rule.aftermathDays);
        Write(actor, trait, ProductionLimitBreakRuleId, state);
    }

    [GameplayInternalOnly(
        "Arcane command validates mana, health and equipment before activation.",
        "ArcaneOverchargeCommandRuntime")]
    public bool TryActivateArcaneOvercharge(
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
        ArcaneOverchargeRuntimeState state = Read<ArcaneOverchargeRuntimeState>(
            actor, trait, ArcaneOverchargeRuleId);
        float now = Mathf.Max(0f, elapsedSeconds);
        if (state.activeUntilSeconds > now || state.aftermathUntilSeconds > now) return false;
        state.activationId = Required(activationId, nameof(activationId));
        state.activeUntilSeconds = now + Mathf.Max(1, rule.durationSeconds);
        state.aftermathUntilSeconds = state.activeUntilSeconds
            + DaysToSeconds(rule.aftermathDays);
        activation = new ArcaneOverchargeActivation(
            rule.selfDamageFraction,
            rule.equipmentDurabilityFraction);
        Write(actor, trait, ArcaneOverchargeRuleId, state);
        return true;
    }

    public IReadOnlyList<string> GetActiveConditionIds(
        CharacterActor actor,
        float elapsedSeconds)
    {
        if (actor == null) return Array.Empty<string>();
        float now = Mathf.Max(0f, elapsedSeconds);
        List<string> result = new();
        AddLastStandConditions(actor, now, result);
        AddTimedAftermath<ForbiddenResearchLeapRule, ForbiddenResearchLeapRuntimeState>(
            actor, 302, ForbiddenLeapRuleId, "state:forbidden-leap-aftermath",
            value => value.aftermathUntilSeconds, now, result);
        AddTimedAftermath<MiracleSurgeryRule, MiracleSurgeryRuntimeState>(
            actor, 303, MiracleSurgeryRuleId, "state:miracle-surgery-aftermath",
            value => value.aftermathUntilSeconds, now, result);
        AddProductionConditions(actor, now, result);
        AddArcaneConditions(actor, now, result);
        return result.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private void AddLastStandConditions(CharacterActor actor, float now, ICollection<string> result)
    {
        LastStandRule rule = ResolveRule<LastStandRule>(actor, 301, LastStandRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        LastStandRuntimeState state = Read<LastStandRuntimeState>(actor, trait, LastStandRuleId);
        if (state.active) result.Add("state:last-stand");
        if (state.aftermathUntilSeconds > now) result.Add("state:last-stand-aftermath");
    }

    private void AddProductionConditions(CharacterActor actor, float now, ICollection<string> result)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(
            actor, trait, ProductionLimitBreakRuleId);
        if (state.active && state.activeLeaseUntilSeconds >= now)
            result.Add("state:production-limit-break");
        if (state.aftermathUntilSeconds > now)
            result.Add("state:production-limit-break-aftermath");
    }

    private void AddArcaneConditions(CharacterActor actor, float now, ICollection<string> result)
    {
        ArcaneOverchargeRule rule = ResolveRule<ArcaneOverchargeRule>(
            actor, 306, ArcaneOverchargeRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        ArcaneOverchargeRuntimeState state = Read<ArcaneOverchargeRuntimeState>(
            actor, trait, ArcaneOverchargeRuleId);
        if (state.activeUntilSeconds > now) result.Add("state:arcane-overcharge");
        else if (state.aftermathUntilSeconds > now)
            result.Add("state:arcane-overcharge-aftermath");
    }

    private void AddTimedAftermath<TRule, TState>(
        CharacterActor actor,
        int traitId,
        string ruleId,
        string conditionId,
        Func<TState, float> deadline,
        float now,
        ICollection<string> result)
        where TRule : CharacterIdentityRule
        where TState : class, new()
    {
        TRule rule = ResolveRule<TRule>(actor, traitId, ruleId, out CharacterTraitSO trait);
        if (rule == null) return;
        if (deadline(Read<TState>(actor, trait, ruleId)) > now) result.Add(conditionId);
    }

    private TState Read<TState>(CharacterActor actor, CharacterTraitSO trait, string ruleId)
        where TState : class, new()
    {
        if (states.TryGet(
                ActorId(actor),
                trait.DefinitionId.Value,
                ruleId,
                out CharacterIdentityRuleStateSaveData saved)
            && !string.IsNullOrWhiteSpace(saved.statePayload))
            return JsonUtility.FromJson<TState>(saved.statePayload) ?? new TState();
        return new TState();
    }

    private void Write<TState>(
        CharacterActor actor,
        CharacterTraitSO trait,
        string ruleId,
        TState state)
        where TState : class =>
        states.Set(
            ActorId(actor),
            trait.DefinitionId.Value,
            ruleId,
            1,
            JsonUtility.ToJson(state));

    private static TRule ResolveRule<TRule>(
        CharacterActor actor,
        int traitId,
        string ruleId,
        out CharacterTraitSO trait)
        where TRule : CharacterIdentityRule
    {
        trait = actor?.Progression?.ResolveSelectedTraits()
            .FirstOrDefault(value => value != null && value.id == traitId);
        return trait?.identityRules?
            .OfType<TRule>()
            .FirstOrDefault(value => string.Equals(value.ruleId, ruleId, StringComparison.Ordinal));
    }

    private static string ActorId(CharacterActor actor) =>
        Required(actor?.Identity?.PersistentId, "actor");

    private static float DaysToSeconds(int days) =>
        Mathf.Max(1, days) * GameCalendarRules.SecondsPerDay;

    private static float HoursToSeconds(int hours) =>
        Mathf.Max(1, hours) * (GameCalendarRules.SecondsPerDay / 24f);

    private static float Roll01(ulong hash) =>
        (hash % 1_000_000UL) / 1_000_000f;

    private static ulong FixedHash(ulong runSeed, params string[] parts)
    {
        ulong hash = 14695981039346656037UL;
        Append(ref hash, runSeed.ToString(CultureInfo.InvariantCulture));
        foreach (string part in parts) Append(ref hash, part);
        return hash;
    }

    private static void Append(ref ulong hash, string value)
    {
        foreach (char character in value?.Trim() ?? string.Empty)
        {
            unchecked
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
        }
        unchecked
        {
            hash ^= 0x1FUL;
            hash *= 1099511628211UL;
        }
    }

    private static string Required(string value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Extreme trait {label} is required.")
            : value.Trim();
}

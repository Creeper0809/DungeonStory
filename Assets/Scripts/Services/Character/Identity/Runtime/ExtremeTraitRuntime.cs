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
    public string preparedOperationId = string.Empty;
    public string preparedFieldId = string.Empty;
    public ExtremeRiskOutcome preparedOutcome;
    public float preparedPrimaryMultiplier;
    public float preparedSecondaryMultiplier;
    public float preparedProgressDelta;
    public ulong preparedRollHash;
    public string preparedFingerprint = string.Empty;
    public bool preparedCommitted;
    public bool preparedOwnerDead;
}

public readonly struct GoldenHarvestPreparedResolution
{
    public GoldenHarvestPreparedResolution(
        string operationId,
        string characterId,
        string traitDefinitionId,
        string fieldId,
        string fingerprint,
        bool committed,
        ExtremeRiskResolution resolution)
    {
        OperationId = operationId ?? string.Empty;
        CharacterId = characterId ?? string.Empty;
        TraitDefinitionId = traitDefinitionId ?? string.Empty;
        FieldId = fieldId ?? string.Empty;
        Fingerprint = fingerprint ?? string.Empty;
        Committed = committed;
        Resolution = resolution;
    }

    public string OperationId { get; }
    public string CharacterId { get; }
    public string TraitDefinitionId { get; }
    public string FieldId { get; }
    public string Fingerprint { get; }
    public bool Committed { get; }
    public ExtremeRiskResolution Resolution { get; }
}

public interface IGoldenHarvestPreparedResolutionQuery
{
    IReadOnlyList<GoldenHarvestPreparedResolution>
        CapturePreparedGoldenHarvests();
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

public sealed class ExtremeTraitRuntime :
    IGoldenHarvestPreparedResolutionQuery,
    ICharacterIdentityDeathStateRetentionPolicy
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
        ulong hash = GoldenHarvestDeterministicOutcomeAuthority.CaptureRollHash(
            runSeed,
            field,
            state.attemptIndex,
            ActorId(actor));
        float roll = GoldenHarvestDeterministicOutcomeAuthority
            .CaptureRoll01(hash);
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

    [GameplayInternalOnly(
        "Crop harvest prepares one stable outcome before physical publication.",
        "CropPlotRuntime")]
    public bool TryPrepareGoldenHarvest(
        CharacterActor actor,
        string fieldId,
        string operationId,
        ulong runSeed,
        float elapsedSeconds,
        out GoldenHarvestPreparedResolution prepared)
    {
        prepared = default;
        GoldenHarvestRule rule = ResolveRule<GoldenHarvestRule>(
            actor,
            304,
            GoldenHarvestRuleId,
            out CharacterTraitSO trait);
        if (rule == null)
            return false;
        string operation = Required(operationId, nameof(operationId));
        string field = Required(fieldId, nameof(fieldId));
        string characterId = ActorId(actor);
        GoldenHarvestRuntimeState state = Read<GoldenHarvestRuntimeState>(
            actor,
            trait,
            GoldenHarvestRuleId);
        if (!string.IsNullOrEmpty(state.preparedOperationId))
        {
            if (!string.Equals(
                    state.preparedOperationId,
                    operation,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Golden harvest already has another prepared operation.");
            prepared = CapturePrepared(
                state,
                characterId,
                trait.DefinitionId.Value);
            return true;
        }
        if (!state.pending
            || elapsedSeconds < state.resolveAfterSeconds
            || !string.Equals(state.fieldId, field, StringComparison.Ordinal))
            return false;
        ulong hash = GoldenHarvestDeterministicOutcomeAuthority.CaptureRollHash(
            runSeed,
            field,
            state.attemptIndex,
            characterId);
        float roll = GoldenHarvestDeterministicOutcomeAuthority
            .CaptureRoll01(hash);
        ExtremeRiskResolution resolution = roll < rule.jackpotChance
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
                : new ExtremeRiskResolution(
                    ExtremeRiskOutcome.Normal,
                    1f,
                    1f,
                    0f,
                    hash);
        state.preparedOperationId = operation;
        state.preparedFieldId = field;
        state.preparedOutcome = resolution.Outcome;
        state.preparedPrimaryMultiplier = resolution.PrimaryMultiplier;
        state.preparedSecondaryMultiplier = resolution.SecondaryMultiplier;
        state.preparedProgressDelta = resolution.ProgressDelta;
        state.preparedRollHash = resolution.FixedRollHash;
        state.preparedFingerprint = CapturePreparedFingerprint(
            operation,
            characterId,
            trait.DefinitionId.Value,
            field,
            resolution);
        state.preparedCommitted = false;
        Write(actor, trait, GoldenHarvestRuleId, state);
        prepared = CapturePrepared(
            state,
            characterId,
            trait.DefinitionId.Value);
        return true;
    }

    [GameplayInternalOnly(
        "Crop harvest commits a prepared outcome after physical output commit.",
        "CropPlotRuntime")]
    public bool TryCommitPreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId,
        out GoldenHarvestPreparedResolution prepared)
    {
        prepared = default;
        if (!TryReadPreparedByIdentity(
                characterId,
                traitDefinitionId,
                operationId,
                out GoldenHarvestRuntimeState state))
            return false;
        if (!state.preparedCommitted)
        {
            state.pending = false;
            state.fieldId = string.Empty;
            state.preparedCommitted = true;
            WriteByIdentity(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId,
                state);
        }
        prepared = CapturePrepared(state, characterId, traitDefinitionId);
        return true;
    }

    [GameplayInternalOnly(
        "Crop harvest clears a committed stable outcome after domain finalization.",
        "CropPlotRuntime")]
    public bool TryAcknowledgePreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId)
    {
        if (!TryReadPreparedByIdentity(
                characterId,
                traitDefinitionId,
                operationId,
                out GoldenHarvestRuntimeState state)
            || !state.preparedCommitted)
            return false;
        bool retireDeathRetainedState = state.preparedOwnerDead;
        ClearPrepared(state);
        if (retireDeathRetainedState)
            states.RemoveRule(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId);
        else
            WriteByIdentity(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId,
                state);
        return true;
    }

    [GameplayInternalOnly(
        "Crop harvest aborts an uncommitted stable outcome with its work owner.",
        "CropPlotRuntime")]
    public bool TryAbortPreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId)
    {
        if (!TryReadPreparedByIdentity(
                characterId,
                traitDefinitionId,
                operationId,
                out GoldenHarvestRuntimeState state)
            || state.preparedCommitted)
            return false;
        bool retireDeathRetainedState = state.preparedOwnerDead;
        ClearPrepared(state);
        if (retireDeathRetainedState)
            states.RemoveRule(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId);
        else
            WriteByIdentity(
                characterId,
                traitDefinitionId,
                GoldenHarvestRuleId,
                state);
        return true;
    }

    public bool TryRetainForPendingExternalOwner(
        string characterId,
        CharacterIdentityRuleStateSaveData saved)
    {
        if (saved == null
            || !string.Equals(
                saved.ruleId,
                GoldenHarvestRuleId,
                StringComparison.Ordinal))
            return false;
        CharacterIdentityRuntimeStateSaveData owner = new()
        {
            characterId = Required(characterId, nameof(characterId)),
            rules = new List<CharacterIdentityRuleStateSaveData>
            {
                saved.Clone()
            }
        };
        GoldenHarvestPreparedResolution[] prepared =
            CapturePreparedGoldenHarvests(new[] { owner }).ToArray();
        if (prepared.Length == 0)
            return false;
        GoldenHarvestRuntimeState state = JsonUtility.FromJson<
            GoldenHarvestRuntimeState>(saved.statePayload)
            ?? throw new InvalidOperationException(
                "Golden Harvest death retention payload is invalid.");
        state.preparedOwnerDead = true;
        WriteByIdentity(
            characterId,
            saved.traitDefinitionId,
            GoldenHarvestRuleId,
            state);
        return true;
    }

    public IReadOnlyList<GoldenHarvestPreparedResolution>
        CapturePreparedGoldenHarvests() => CapturePreparedGoldenHarvests(
            states.Capture());

    public static IReadOnlyList<GoldenHarvestPreparedResolution>
        CapturePreparedGoldenHarvests(
            IEnumerable<CharacterIdentityRuntimeStateSaveData> source)
    {
        List<GoldenHarvestPreparedResolution> result = new();
        HashSet<string> operations = new(StringComparer.Ordinal);
        foreach (CharacterIdentityRuntimeStateSaveData character in
                 source ?? Array.Empty<CharacterIdentityRuntimeStateSaveData>())
        {
            if (character == null)
                throw new InvalidOperationException(
                    "Golden Harvest state census contains a null character owner.");
            foreach (CharacterIdentityRuleStateSaveData saved in
                     character.rules ?? new List<CharacterIdentityRuleStateSaveData>())
            {
                if (!string.Equals(
                        saved.ruleId,
                        GoldenHarvestRuleId,
                        StringComparison.Ordinal))
                    continue;
                GoldenHarvestRuntimeState state = JsonUtility.FromJson<
                    GoldenHarvestRuntimeState>(saved.statePayload)
                    ?? new GoldenHarvestRuntimeState();
                bool hasPreparedProvenance =
                    !string.IsNullOrEmpty(state.preparedOperationId)
                    || !string.IsNullOrEmpty(state.preparedFieldId)
                    || !string.IsNullOrEmpty(state.preparedFingerprint)
                    || state.preparedOutcome != default
                    || state.preparedPrimaryMultiplier != 0f
                    || state.preparedSecondaryMultiplier != 0f
                    || state.preparedProgressDelta != 0f
                    || state.preparedRollHash != 0UL
                    || state.preparedCommitted;
                if (!hasPreparedProvenance)
                    continue;
                if (!Canonical(state.preparedOperationId)
                    || !Canonical(character.characterId)
                    || !Canonical(saved.traitDefinitionId)
                    || !Canonical(state.preparedFieldId)
                    || !Enum.IsDefined(
                        typeof(ExtremeRiskOutcome),
                        state.preparedOutcome)
                    || !FinitePositive(state.preparedPrimaryMultiplier)
                    || !FinitePositive(state.preparedSecondaryMultiplier)
                    || !state.preparedCommitted
                        && (!state.pending
                            || !string.Equals(
                                state.fieldId,
                                state.preparedFieldId,
                                StringComparison.Ordinal))
                    || state.preparedCommitted
                        && (state.pending
                            || !string.IsNullOrEmpty(state.fieldId))
                    || !string.Equals(
                        state.preparedFingerprint,
                        CapturePreparedFingerprint(
                            state.preparedOperationId,
                            character.characterId,
                            saved.traitDefinitionId,
                            state.preparedFieldId,
                            PreparedResolution(state)),
                        StringComparison.Ordinal)
                    || !operations.Add(state.preparedOperationId))
                    throw new InvalidOperationException(
                        "Golden Harvest prepared resolution census is invalid or duplicated.");
                result.Add(CapturePrepared(
                    state,
                    character.characterId,
                    saved.traitDefinitionId));
            }
        }
        return result
            .OrderBy(value => value.OperationId, StringComparer.Ordinal)
            .ToArray();
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

    private bool TryReadPreparedByIdentity(
        string characterId,
        string traitDefinitionId,
        string operationId,
        out GoldenHarvestRuntimeState state)
    {
        state = null;
        string character = Required(characterId, nameof(characterId));
        string trait = Required(traitDefinitionId, nameof(traitDefinitionId));
        string operation = Required(operationId, nameof(operationId));
        if (!states.TryGet(
                character,
                trait,
                GoldenHarvestRuleId,
                out CharacterIdentityRuleStateSaveData saved)
            || string.IsNullOrWhiteSpace(saved.statePayload))
            return false;
        state = JsonUtility.FromJson<GoldenHarvestRuntimeState>(saved.statePayload)
            ?? new GoldenHarvestRuntimeState();
        return string.Equals(
                state.preparedOperationId,
                operation,
                StringComparison.Ordinal)
            && string.Equals(
                state.preparedFingerprint,
                CapturePreparedFingerprint(
                    operation,
                    character,
                    trait,
                    state.preparedFieldId,
                    PreparedResolution(state)),
                StringComparison.Ordinal);
    }

    private void WriteByIdentity<TState>(
        string characterId,
        string traitDefinitionId,
        string ruleId,
        TState state)
        where TState : class => states.Set(
        Required(characterId, nameof(characterId)),
        Required(traitDefinitionId, nameof(traitDefinitionId)),
        Required(ruleId, nameof(ruleId)),
        1,
        JsonUtility.ToJson(state));

    private static GoldenHarvestPreparedResolution CapturePrepared(
        GoldenHarvestRuntimeState state,
        string characterId,
        string traitDefinitionId) => new(
        state.preparedOperationId,
        characterId,
        traitDefinitionId,
        state.preparedFieldId,
        state.preparedFingerprint,
        state.preparedCommitted,
        PreparedResolution(state));

    private static ExtremeRiskResolution PreparedResolution(
        GoldenHarvestRuntimeState state) => new(
        state.preparedOutcome,
        state.preparedPrimaryMultiplier,
        state.preparedSecondaryMultiplier,
        state.preparedProgressDelta,
        state.preparedRollHash);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    private static string CapturePreparedFingerprint(
        string operationId,
        string characterId,
        string traitDefinitionId,
        string fieldId,
        ExtremeRiskResolution resolution)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("golden-harvest-prepared@1");
        digest.Append(operationId);
        digest.Append(characterId);
        digest.Append(traitDefinitionId);
        digest.Append(fieldId);
        digest.Append((int)resolution.Outcome);
        digest.Append(resolution.PrimaryMultiplier.ToString(
            "R",
            CultureInfo.InvariantCulture));
        digest.Append(resolution.SecondaryMultiplier.ToString(
            "R",
            CultureInfo.InvariantCulture));
        digest.Append(resolution.ProgressDelta.ToString(
            "R",
            CultureInfo.InvariantCulture));
        digest.Append(resolution.FixedRollHash.ToString(
            CultureInfo.InvariantCulture));
        return digest.ComputeSha256();
    }

    private static void ClearPrepared(GoldenHarvestRuntimeState state)
    {
        state.preparedOperationId = string.Empty;
        state.preparedFieldId = string.Empty;
        state.preparedOutcome = default;
        state.preparedPrimaryMultiplier = 0f;
        state.preparedSecondaryMultiplier = 0f;
        state.preparedProgressDelta = 0f;
        state.preparedRollHash = 0UL;
        state.preparedFingerprint = string.Empty;
        state.preparedCommitted = false;
        state.preparedOwnerDead = false;
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

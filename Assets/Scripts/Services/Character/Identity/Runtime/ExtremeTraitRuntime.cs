using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ExtremeTraitRuntime :
    IGoldenHarvestPreparedResolutionQuery,
    ICharacterIdentityDeathStateRetentionPolicy
{
    public const string LastStandRuleId = ExtremeTraitRuleIds.LastStandRuleId;
    public const string ForbiddenLeapRuleId = ExtremeTraitRuleIds.ForbiddenLeapRuleId;
    public const string MiracleSurgeryRuleId = ExtremeTraitRuleIds.MiracleSurgeryRuleId;
    public const string GoldenHarvestRuleId = ExtremeTraitRuleIds.GoldenHarvestRuleId;
    public const string ProductionLimitBreakRuleId = ExtremeTraitRuleIds.ProductionLimitBreakRuleId;
    public const string ArcaneOverchargeRuleId = ExtremeTraitRuleIds.ArcaneOverchargeRuleId;

    private readonly LastStandTraitRuntime lastStand;
    private readonly ForbiddenResearchLeapTraitRuntime researchLeap;
    private readonly MiracleSurgeryTraitRuntime miracleSurgery;
    private readonly GoldenHarvestTraitRuntime goldenHarvest;
    private readonly ProductionLimitBreakTraitRuntime production;
    private readonly ArcaneOverchargeTraitRuntime arcaneOvercharge;

    public ExtremeTraitRuntime(CharacterIdentityStateStore states)
    {
        if (states == null) throw new ArgumentNullException(nameof(states));
        lastStand = new LastStandTraitRuntime(states);
        researchLeap = new ForbiddenResearchLeapTraitRuntime(states);
        miracleSurgery = new MiracleSurgeryTraitRuntime(states);
        goldenHarvest = new GoldenHarvestTraitRuntime(states);
        production = new ProductionLimitBreakTraitRuntime(states);
        arcaneOvercharge = new ArcaneOverchargeTraitRuntime(states);
    }

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
        => lastStand.TryActivateLastStand(actor, encounterId, healthRatio, coreOrganCritical, elapsedSeconds, out rule);

    [GameplayInternalOnly(
        "Combat lifecycle owns last-stand cleanup and aftermath.",
        "CharacterCombatCommandRuntime")]
    public void EndLastStand(CharacterActor actor, string encounterId, float elapsedSeconds)
        => lastStand.EndLastStand(actor, encounterId, elapsedSeconds);

    [GameplayInternalOnly(
        "Research jointly commits the prepared trait attempt, progress and outcome.",
        "BlueprintResearchOutcomeTransaction")]
    public bool TryPrepareForbiddenResearchLeap(
        CharacterActor actor,
        string projectId,
        ulong runSeed,
        float elapsedSeconds,
        out ForbiddenResearchLeapPreparation preparation)
        => researchLeap.TryPrepareForbiddenResearchLeap(actor, projectId, runSeed, elapsedSeconds, out preparation);

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
        => miracleSurgery.TryResolveMiracleSurgery(actor, surgeryId, isCritical, runSeed, elapsedSeconds, out resolution);

    [GameplayInternalOnly(
        "Crop-plot command validates and persists the scheduled harvester first.",
        "CropPlotRuntime")]
    public bool TryScheduleGoldenHarvest(
        CharacterActor actor,
        string fieldId,
        int attemptIndex,
        float elapsedSeconds)
        => goldenHarvest.TryScheduleGoldenHarvest(actor, fieldId, attemptIndex, elapsedSeconds);

    [GameplayInternalOnly(
        "Crop harvest completion owns the fixed delayed outcome.",
        "CropPlotRuntime")]
    public bool TryResolveGoldenHarvest(
        CharacterActor actor,
        string fieldId,
        ulong runSeed,
        float elapsedSeconds,
        out ExtremeRiskResolution resolution)
        => goldenHarvest.TryResolveGoldenHarvest(actor, fieldId, runSeed, elapsedSeconds, out resolution);

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
        => goldenHarvest.TryPrepareGoldenHarvest(actor, fieldId, operationId, runSeed, elapsedSeconds, out prepared);

    [GameplayInternalOnly(
        "Crop harvest commits a prepared outcome after physical output commit.",
        "CropPlotRuntime")]
    public bool TryCommitPreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId,
        out GoldenHarvestPreparedResolution prepared)
        => goldenHarvest.TryCommitPreparedGoldenHarvest(characterId, traitDefinitionId, operationId, out prepared);

    [GameplayInternalOnly(
        "Crop harvest clears a committed stable outcome after domain finalization.",
        "CropPlotRuntime")]
    public bool TryAcknowledgePreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId)
        => goldenHarvest.TryAcknowledgePreparedGoldenHarvest(characterId, traitDefinitionId, operationId);

    [GameplayInternalOnly(
        "Crop harvest aborts an uncommitted stable outcome with its work owner.",
        "CropPlotRuntime")]
    public bool TryAbortPreparedGoldenHarvest(
        string characterId,
        string traitDefinitionId,
        string operationId)
        => goldenHarvest.TryAbortPreparedGoldenHarvest(characterId, traitDefinitionId, operationId);

    public bool TryRetainForPendingExternalOwner(
        string characterId,
        CharacterIdentityRuleStateSaveData saved)
        => goldenHarvest.TryRetainForPendingExternalOwner(characterId, saved);

    public IReadOnlyList<GoldenHarvestPreparedResolution>
        CapturePreparedGoldenHarvests()
        => goldenHarvest.CapturePreparedGoldenHarvests();

    public static IReadOnlyList<GoldenHarvestPreparedResolution>
        CapturePreparedGoldenHarvests(
            IEnumerable<CharacterIdentityRuntimeStateSaveData> source)
        => GoldenHarvestTraitRuntime.CapturePreparedGoldenHarvests(source);

    public bool TryGetGoldenHarvestDelay(
        CharacterActor actor,
        string fieldId,
        float elapsedSeconds,
        out float remainingSeconds)
        => goldenHarvest.TryGetGoldenHarvestDelay(actor, fieldId, elapsedSeconds, out remainingSeconds);

    [GameplayInternalOnly(
        "Production bill facade prevalidates the exact runnable bill before mutation.",
        "ProductionBillSceneFacade")]
    public bool TryBeginProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds,
        out ProductionLimitBreakRule rule)
        => production.TryBeginProductionLimitBreak(actor, batchId, elapsedSeconds, out rule);

    public bool CanBeginProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds)
        => production.CanBeginProductionLimitBreak(actor, batchId, elapsedSeconds);

    public bool CanConfigureProductionLimitBreak(
        CharacterActor actor,
        float elapsedSeconds)
        => production.CanConfigureProductionLimitBreak(actor, elapsedSeconds);

    [GameplayInternalOnly(
        "The authoritative production-bill facade refreshes the active batch lease while work remains live.",
        "ProductionBillSceneFacade")]
    public bool RefreshProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds)
        => production.RefreshProductionLimitBreak(actor, batchId, elapsedSeconds);

    [GameplayInternalOnly(
        "The registered extreme-trait lease clock expires abandoned batch leases.",
        "ExtremeTraitLeaseClock")]
    public bool ExpireProductionLimitBreak(
        CharacterActor actor,
        float elapsedSeconds)
        => production.ExpireProductionLimitBreak(actor, elapsedSeconds);

    [GameplayInternalOnly(
        "Production completion owns limit-break cleanup and aftermath.",
        "ProductionBillSceneFacade")]
    public void EndProductionLimitBreak(CharacterActor actor, string batchId, float elapsedSeconds)
        => production.EndProductionLimitBreak(actor, batchId, elapsedSeconds);

    [GameplayInternalOnly(
        "Arcane command validates mana, health and equipment before activation.",
        "ArcaneOverchargeCommandRuntime")]
    public bool TryActivateArcaneOvercharge(
        CharacterActor actor,
        string activationId,
        float manaRatio,
        float elapsedSeconds,
        out ArcaneOverchargeActivation activation)
        => arcaneOvercharge.TryActivateArcaneOvercharge(actor, activationId, manaRatio, elapsedSeconds, out activation);

    [GameplayInternalOnly(
        "Arcane command owns the resumable self-damage and equipment-durability operation.",
        "ArcaneOverchargeCommandRuntime")]
    public bool TryBeginOrResumeArcaneOvercharge(
        CharacterActor actor,
        string activationId,
        string equipmentInstanceId,
        float manaRatio,
        float elapsedSeconds,
        out ArcaneOverchargeOperationSnapshot operation)
        => arcaneOvercharge.TryBeginOrResumeArcaneOvercharge(actor, activationId, equipmentInstanceId, manaRatio, elapsedSeconds, out operation);

    [GameplayInternalOnly(
        "Arcane command records an explicit rejected effect before returning failure.",
        "ArcaneOverchargeCommandRuntime")]
    public bool TryRecordArcaneOverchargeFailure(
        CharacterActor actor,
        string activationId,
        string failureCode)
        => arcaneOvercharge.TryRecordArcaneOverchargeFailure(actor, activationId, failureCode);

    [GameplayInternalOnly(
        "Arcane command records the exact body-health mutation before publishing observers.",
        "ArcaneOverchargeCommandRuntime")]
    public bool TryRecordArcaneOverchargeSelfDamage(
        CharacterActor actor,
        string activationId,
        float healthBefore,
        float healthAfter)
        => arcaneOvercharge.TryRecordArcaneOverchargeSelfDamage(actor, activationId, healthBefore, healthAfter);

    [GameplayInternalOnly(
        "Arcane command records the exact equipment mutation before completing the lease.",
        "ArcaneOverchargeCommandRuntime")]
    public bool TryRecordArcaneOverchargeDurability(
        CharacterActor actor,
        string activationId,
        float durabilityBefore,
        float durabilityAfter)
        => arcaneOvercharge.TryRecordArcaneOverchargeDurability(actor, activationId, durabilityBefore, durabilityAfter);

    [GameplayInternalOnly(
        "Arcane command completes only after both authoritative effects are recorded.",
        "ArcaneOverchargeCommandRuntime")]
    public bool TryCompleteArcaneOvercharge(
        CharacterActor actor,
        string activationId)
        => arcaneOvercharge.TryCompleteArcaneOvercharge(actor, activationId);

    public bool TryGetArcaneOverchargeOperation(
        CharacterActor actor,
        out ArcaneOverchargeOperationSnapshot operation)
        => arcaneOvercharge.TryGetArcaneOverchargeOperation(actor, out operation);

    public IReadOnlyList<string> GetActiveConditionIds(
        CharacterActor actor,
        float elapsedSeconds)
    {
        if (actor == null) return Array.Empty<string>();
        float now = Mathf.Max(0f, elapsedSeconds);
        List<string> result = new();
        lastStand.AddConditions(actor, now, result);
        researchLeap.AddConditions(actor, now, result);
        miracleSurgery.AddConditions(actor, now, result);
        production.AddConditions(actor, now, result);
        arcaneOvercharge.AddConditions(actor, now, result);
        return result.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}

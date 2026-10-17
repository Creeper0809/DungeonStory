using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DungeonSaveReferenceRules;

/// <summary>
/// Validates identities and authored-content references across aggregate payloads before
/// any save section is allowed to mutate the live world.
/// </summary>
public sealed class DungeonAggregateReferencePreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    private readonly IBuildingDefinitionLookup buildingDefinitions;
    private readonly DungeonSaveReferenceRules references;
    private readonly DungeonPhysicalSaveReferenceValidation physicalSaveReference;
    private readonly DungeonCommerceSaveReferenceValidation commerceSaveReference;
    private readonly DungeonExpeditionSaveReferenceValidation expeditionSaveReference;
    private readonly DungeonEquipmentSaveReferenceValidation equipmentSaveReference;
    private readonly DungeonCharacterLifeSaveReferenceValidation characterLifeSaveReference;
    private readonly DungeonHealthSaveReferenceValidation healthSaveReference;
    private readonly DungeonCropSaveReferenceValidation cropSaveReference;

    public DungeonAggregateReferencePreflight(
        IItemDefinitionCatalog itemDefinitions,
        IBuildingDefinitionLookup buildingDefinitions,
        ICombatEquipmentCatalog combatEquipmentDefinitions,
        IResourceEconomyContentCatalog economyContent,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        IDiseaseDefinitionCatalog diseaseDefinitions)
    {
        if (itemDefinitions == null) throw new ArgumentNullException(nameof(itemDefinitions));
        if (buildingDefinitions == null) throw new ArgumentNullException(nameof(buildingDefinitions));
        if (combatEquipmentDefinitions == null) throw new ArgumentNullException(nameof(combatEquipmentDefinitions));
        if (economyContent == null) throw new ArgumentNullException(nameof(economyContent));
        if (lifeDefinitions == null) throw new ArgumentNullException(nameof(lifeDefinitions));
        if (diseaseDefinitions == null) throw new ArgumentNullException(nameof(diseaseDefinitions));
        this.buildingDefinitions = buildingDefinitions;
        references = new DungeonSaveReferenceRules(itemDefinitions);
        physicalSaveReference = new DungeonPhysicalSaveReferenceValidation(references, buildingDefinitions, combatEquipmentDefinitions);
        commerceSaveReference = new DungeonCommerceSaveReferenceValidation();
        expeditionSaveReference = new DungeonExpeditionSaveReferenceValidation(references);
        equipmentSaveReference = new DungeonEquipmentSaveReferenceValidation(combatEquipmentDefinitions);
        characterLifeSaveReference = new DungeonCharacterLifeSaveReferenceValidation(references, lifeDefinitions);
        healthSaveReference = new DungeonHealthSaveReferenceValidation(references, diseaseDefinitions);
        cropSaveReference = new DungeonCropSaveReferenceValidation(economyContent);
    }

    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (saveData == null) throw new ArgumentNullException(nameof(saveData));
        if (report == null) throw new ArgumentNullException(nameof(report));

        GameSessionSaveData session =
            DungeonSaveSectionPayload.ReadOrNew<GameSessionSaveData>(
                saveData,
                FoundationSessionSaveSection.Id);
        SeasonalEventWorldSaveData seasonal =
            DungeonSaveSectionPayload.ReadOrNew<SeasonalEventWorldSaveData>(
                saveData,
                SeasonalWorldEventsSaveSection.Id);
        DungeonWildlifeSaveData wildlife =
            DungeonSaveSectionPayload.ReadOrNew<DungeonWildlifeSaveData>(
                saveData,
                WildlifeSaveSection.Id);
        DungeonPhysicalItemSaveData items =
            DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                saveData,
                PhysicalItemsSaveSection.Id);
        DungeonCharacterWorldSaveData characters =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                saveData,
                CharacterWorldSaveSection.Id);
        CharacterNarrativeWorldSaveData characterNarrative =
            DungeonSaveSectionPayload.ReadOrNew<CharacterNarrativeWorldSaveData>(
                saveData,
                CharacterNarrativeSaveSection.Id);
        SocietyEventWorldSaveData societyEvents =
            DungeonSaveSectionPayload.ReadOrNew<SocietyEventWorldSaveData>(
                saveData,
                SocietyEventsSaveSection.Id);
        ModularFacilityWorldSaveData buildings =
            DungeonSaveSectionPayload.ReadOrNew<ModularFacilityWorldSaveData>(
                saveData,
                ModularFacilityWorldSaveSection.Id);
        DungeonOffenseAggregateSaveData offense =
            DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(
                saveData,
                OffenseAggregateSaveSection.Id);
        DungeonCombatEquipmentSaveData combat =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCombatEquipmentSaveData>(
                saveData,
                CombatEquipmentSaveSection.Id);
        DungeonInvasionSaveData invasion =
            DungeonSaveSectionPayload.ReadOrNew<DungeonInvasionSaveData>(
                saveData,
                InvasionSaveSection.Id);
        EquipmentEvolutionSaveData evolution =
            DungeonSaveSectionPayload.ReadOrNew<EquipmentEvolutionSaveData>(
                saveData,
                EquipmentEvolutionSaveSection.Id);
        DungeonMetaProgressionSaveData meta =
            DungeonSaveSectionPayload.ReadOrNew<DungeonMetaProgressionSaveData>(
                saveData,
                MetaProgressionSaveSection.Id);
        DungeonRunFlowSaveData runFlow =
            DungeonSaveSectionPayload.ReadOrNew<DungeonRunFlowSaveData>(
                saveData,
                RunFlowSaveSection.Id);
        DungeonResearchSaveData research =
            DungeonSaveSectionPayload.ReadOrNew<DungeonResearchSaveData>(
                saveData,
                BlueprintResearchSaveSection.Id);
        DungeonCropPlotSaveData cropPlots =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCropPlotSaveData>(
                saveData,
                CropPlotSaveSection.Id);
        CertifiedSeedWorldSaveData certifiedSeeds =
            DungeonSaveSectionPayload.ReadOrNew<CertifiedSeedWorldSaveData>(
                saveData,
                CertifiedSeedSaveSection.Id);
        TreasuryEconomySaveData treasury =
            DungeonSaveSectionPayload.ReadOrNew<TreasuryEconomySaveData>(
                saveData,
                TreasuryEconomySaveSection.Id);
        DungeonResourceStockPolicySaveData stockPolicies =
            DungeonSaveSectionPayload.ReadOrNew<
                DungeonResourceStockPolicySaveData>(
                saveData,
                ResourceStockPolicySaveSection.Id);
        CharacterLifeWorldSaveData life =
            DungeonSaveSectionPayload.ReadOrNew<CharacterLifeWorldSaveData>(
                saveData,
                CharacterLifeSaveSection.Id);
        KinshipHouseholdWorldSaveData kinshipHouseholds =
            DungeonSaveSectionPayload.ReadOrNew<KinshipHouseholdWorldSaveData>(
                saveData,
                KinshipHouseholdSaveSection.Id);
        ReproductionWorldSaveData reproduction =
            DungeonSaveSectionPayload.ReadOrNew<ReproductionWorldSaveData>(
                saveData,
                ReproductionSaveSection.Id);
        PopulationHealthWorldSaveData populationHealth =
            DungeonSaveSectionPayload.ReadOrNew<PopulationHealthWorldSaveData>(
                saveData,
                PopulationHealthSaveSection.Id);
        DungeonCharacterMedicalSaveData characterMedical =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterMedicalSaveData>(
                saveData,
                CharacterMedicalSaveSection.Id);
        CharacterCareerWorldSaveData careers =
            DungeonSaveSectionPayload.ReadOrNew<CharacterCareerWorldSaveData>(
                saveData,
                CharacterCareerSaveSection.Id);
        CharacterPsychosocialWorldSaveData psychosocial =
            DungeonSaveSectionPayload.ReadOrNew<CharacterPsychosocialWorldSaveData>(
                saveData,
                CharacterPsychosocialSaveSection.Id);
        CropEcologyWorldSaveData cropEcology =
            DungeonSaveSectionPayload.ReadOrNew<CropEcologyWorldSaveData>(
                saveData,
                CropEcologySaveSection.Id);
        DungeonPowerInfrastructureSaveData powerInfrastructure =
            DungeonSaveSectionPayload.ReadOrNew<
                DungeonPowerInfrastructureSaveData>(
                saveData,
                PowerInfrastructureSaveSection.Id);
        DungeonInfrastructureObservationSaveData infrastructureOutcomes =
            DungeonSaveSectionPayload.ReadOrNew<
                DungeonInfrastructureObservationSaveData>(
                saveData,
                InfrastructureCommandOutcomeSaveSection.Id);
        GameplayOutcomeLedgerSaveData outcomeLedger =
            DungeonSaveSectionPayload.ReadOrNew<
                GameplayOutcomeLedgerSaveData>(
                saveData,
                GameplayOutcomeLedgerSaveSection.Id);
        ValidateOutcomeCalendar(session, outcomeLedger, report);
        try
        {
            AnimalHusbandryCompletionObservation.ValidateCompletionRestoreJoin(
                DungeonSaveSectionPayload.ReadOrNew<DungeonAnimalHusbandrySaveData>(
                    saveData, AnimalHusbandrySaveSection.Id).pendingOutcomes,
                outcomeLedger);
        }
        catch (Exception exception)
        {
            report.AddError("Husbandry completion identity restore join is invalid: " + exception.Message);
        }
        try
        {
            SurgeryCompletionIdentityRestoreJoin.Validate(
                DungeonSaveSectionPayload.ReadOrNew<DungeonSurgerySaveData>(
                    saveData, SurgerySaveSection.Id).orders,
                characterNarrative.workCompletionDeliveries, outcomeLedger);
        }
        catch (Exception exception)
        {
            report.AddError("Surgery completion identity restore join is invalid: " + exception.Message);
        }
        try
        {
            RepairWorkCompletionObservation.ValidateCompletionRestoreJoin(
                DungeonSaveSectionPayload.ReadOrNew<DungeonWorkOrderSaveData>(
                    saveData, WorkOrdersSaveSection.Id).repairOutcomes,
                characterNarrative.workCompletionDeliveries, outcomeLedger,
                DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(
                    saveData, MigratedProducerOutcomeSaveSection.Id));
        }
        catch (Exception exception)
        {
            report.AddError("Repair completion identity restore join is invalid: " + exception.Message);
        }

        try
        {
            EnvironmentalFireCompletionIdentity.ValidateRestoreJoin(
                DungeonSaveSectionPayload.ReadOrNew<DungeonStory.Environment.DungeonEnvironmentalFireSaveData>(
                    saveData, EnvironmentalFireSaveSection.Id),
                characterNarrative.workCompletionDeliveries, outcomeLedger);
        }
        catch (Exception exception)
        {
            report.AddError("Fire completion identity restore join is invalid: " + exception.Message);
        }

        // ReadOrNew deserializes detached DTO graphs. Normalize only the explicitly
        // typed early-V18 character-reference paths on those graphs before building
        // the cross-section indexes. The source envelopes and live runtime remain
        // untouched; section preflight still owns warning emission and publication.
        NormalizeEarlyV18CharacterReferences(
            characters,
            offense,
            combat,
            treasury);

        DungeonSavePhysicalReferenceIndex physical = physicalSaveReference.ValidatePhysicalItems(items, report);
        DungeonSaveCharacterReferenceIndex characterReferences =
            physicalSaveReference.ValidateCharacters(characters, report);
        DungeonExpeditionSaveReferenceValidation.AddActiveInvasionCharacterIds(invasion, characterReferences, report);
        HashSet<string> characterIds = characterReferences.KnownCharacterIds;
        HashSet<string> combatCharacterIds = DungeonExpeditionSaveReferenceValidation.BuildCombatCharacterIds(
            characterIds, offense?.expedition?.activeBattle, report);
        DungeonSaveBuildingReferenceIndex buildingIds = physicalSaveReference.ValidateBuildings(buildings, report);
        DungeonCommerceSaveReferenceValidation.ValidateRetailStockJoins(buildings, physical, report);
        DungeonCommerceSaveReferenceValidation.ValidateQualityRejectedSaleJoins(
            stockPolicies,
            treasury,
            physical,
            report);
        IndustrialTransitionFactObservation.ValidateSaveJoin(infrastructureOutcomes, outcomeLedger, report);
        ValidatePowerFuelPhysicalReceipts(
            powerInfrastructure,
            buildingIds.InstanceIds,
            physical,
            infrastructureOutcomes,
            outcomeLedger,
            report);
        DungeonExpeditionSaveReferenceValidation.ValidateOffenseMembers(offense, characterIds, report);
        expeditionSaveReference.ValidateOffenseReferences(
            offense,
            characterIds,
            combatCharacterIds,
            physical,
            buildingIds.InstanceIds,
            report);
        DungeonExpeditionSaveReferenceValidation.ValidateSeasonalOffenseOfferJoins(
            session,
            seasonal,
            offense,
            report);
        DungeonExpeditionSaveReferenceValidation.ValidateOffenseArrivalResultJoins(offense, report);
        equipmentSaveReference.ValidateCombat(
            combat,
            combatCharacterIds,
            physical,
            buildingIds.InstanceIds,
            research,
            report);
        DungeonEquipmentSaveReferenceValidation.ValidateEvolution(
            evolution,
            physical.EquipmentInstanceIds,
            buildingIds.InstanceIds,
            report);
        ValidateMeta(meta, report);
        ValidateResearch(research, buildings, offense, report);
        cropSaveReference.ValidateCropPlots(cropPlots, buildingIds, report);
        cropSaveReference.ValidateCertifiedSeeds(
            certifiedSeeds,
            buildingIds.InstanceIds,
            physical,
            report);
        DungeonCommerceSaveReferenceValidation.ValidateTreasury(
            treasury,
            characterIds,
            buildingIds.InstanceIds,
            physical.EquipmentInstanceIds,
            report);
        HashSet<string> lifeCharacterIds = characterLifeSaveReference.ValidateCharacterLife(
            life,
            characterIds,
            buildingIds.InstanceIds,
            physical,
            report);
        HashSet<string> retainedDeadActorIds =
            DungeonCharacterLifeSaveReferenceValidation.ValidateRetainedDeadActors(
                characters,
                offense,
                kinshipHouseholds,
                meta,
                runFlow,
                report);
        DungeonSaveKinshipReferenceIndex kinship = DungeonCharacterLifeSaveReferenceValidation.ValidateKinshipHouseholds(
            kinshipHouseholds,
            characterReferences.LivingCharacterIds,
            characterReferences.ActorIds,
            retainedDeadActorIds,
            buildingIds.InstanceIds,
            report);
        characterLifeSaveReference.ValidateReproduction(
            reproduction,
            characterIds,
            kinship.AllCharacterIds,
            report);
        healthSaveReference.ValidatePopulationHealth(
            populationHealth,
            outcomeLedger,
            characterIds,
            new HashSet<string>(kinship.AllCharacterIds.Concat(retainedDeadActorIds), StringComparer.Ordinal),
            buildingIds.InstanceIds,
            physical,
            report);
        healthSaveReference.ValidateCharacterMedicalPhysicalReceipts(
            characterMedical,
            characterIds,
            buildingIds.InstanceIds,
            physical,
            report);
        DungeonCharacterLifeSaveReferenceValidation.ValidateCareers(
            careers,
            characterIds,
            kinship.TombstoneIds,
            buildingIds.InstanceIds,
            report);
        DungeonCharacterLifeSaveReferenceValidation.ValidatePsychosocial(
            psychosocial,
            characterIds,
            kinship.AllCharacterIds,
            report);
        DungeonCharacterLifeSaveReferenceValidation.ValidateObservedPromotionNarrativeJoins(
            societyEvents,
            characterNarrative,
            report);
        cropSaveReference.ValidateCropEcology(
            cropEcology,
            cropPlots,
            characterNarrative,
            outcomeLedger,
            buildingIds.InstanceIds,
            physical.SeedLots,
            report);
        try
        {
            SeasonalWildlifeArrivalRules.ValidateRestoreJoin(
                seasonal,
                wildlife.wildlife);
        }
        catch (Exception exception)
        {
            report.AddError(
                "Seasonal wildlife restore join is invalid: "
                + exception.Message);
        }

        foreach (string characterId in characterIds.Except(lifeCharacterIds))
        {
            report.AddError(
                $"Character '{characterId}' has no V19 life record.");
        }
    }

    public static void ValidateOutcomeCalendar(GameSessionSaveData session,
        GameplayOutcomeLedgerSaveData ledger, DungeonGameRestoreReport report)
    {
        if (session == null || ledger == null || report == null)
            throw new ArgumentNullException("Outcome/calendar join requires detached save sections.");
        if (session.absoluteDay < 1 || session.hour < 0 || session.hour >= GameCalendarRules.HoursPerDay)
        {
            report.AddError("outcome-calendar-source-invalid");
            return;
        }
        long capturedHour = GameplayOutcomeDeliveryTime.OccurrenceStartHour(session.absoluteDay) + session.hour;
        ValidateRows(ledger.outbox);
        ValidateRows(ledger.exactOutcomes);
        if (ledger.tombstones == null)
            report.AddError("outcome-calendar-tombstones-missing");
        else
            foreach (GameplayOutcomeTombstoneSnapshot tombstone in ledger.tombstones)
            {
                try { GameplayOutcomeDeliveryTime.RequireCapturedAt(tombstone, capturedHour, false); }
                catch (InvalidOperationException error)
                {
                    report.AddError("outcome-calendar-tombstone-invalid: " + tombstone?.operationId + ": " + error.Message);
                }
            }
        if (ledger.compactedMemories == null)
            report.AddError("outcome-calendar-compacted-missing");
        else
            foreach (CompactedNarrativeMemorySnapshot memory in ledger.compactedMemories)
            {
                try { GameplayOutcomeDeliveryTime.RequireCapturedAt(memory, capturedHour); }
                catch (InvalidOperationException error)
                {
                    report.AddError("outcome-calendar-compacted-invalid: " + memory?.memoryId + ": " + error.Message);
                }
            }
        void ValidateRows(List<GameplayOutcomeSnapshot> rows)
        {
            if (rows == null) { report.AddError("outcome-calendar-rows-missing"); return; }
            foreach (GameplayOutcomeSnapshot row in rows)
            {
                try { GameplayOutcomeDeliveryTime.RequireCapturedAt(row, capturedHour, false); }
                catch (InvalidOperationException error)
                {
                    report.AddError("outcome-calendar-join-invalid: " + row?.operationId + ": " + error.Message);
                }
            }
        }
    }

    private static void NormalizeEarlyV18CharacterReferences(
        DungeonCharacterWorldSaveData characters,
        DungeonOffenseAggregateSaveData offense,
        DungeonCombatEquipmentSaveData combat,
        TreasuryEconomySaveData treasury)
    {
        NormalizeCharacterWorldReferences(characters);
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(
            offense,
            (value, _) => NormalizeCharacterReference(value));
        V18TypedCharacterReferenceRestoreNormalizer.Normalize(
            combat,
            (value, _) => NormalizeCharacterReference(value));
        NormalizeTreasuryCharacterReferences(treasury);
    }

    private static void NormalizeCharacterWorldReferences(
        DungeonCharacterWorldSaveData source)
    {
        if (source?.actors != null)
        {
            foreach (DungeonCharacterSaveData actor in source.actors)
            {
                if (actor != null)
                {
                    actor.persistentId = NormalizeCharacterReference(
                        actor.persistentId);
                }
            }
        }

        if (source?.populationProfiles == null)
        {
            return;
        }

        foreach (WorldCharacterProfile profile in source.populationProfiles)
        {
            if (profile != null)
            {
                profile.persistentId = NormalizeCharacterReference(
                    profile.persistentId);
            }
        }
    }

    private static void NormalizeTreasuryCharacterReferences(
        TreasuryEconomySaveData source)
    {
        if (source?.employment?.wageStates != null)
        {
            foreach (EmployeeWageState wage in source.employment.wageStates)
            {
                if (wage != null)
                {
                    wage.characterId = NormalizeCharacterReference(
                        wage.characterId);
                }
            }
        }

        if (source?.employment?.mercenaryContracts == null)
        {
            return;
        }

        foreach (MercenaryContract contract in
                 source.employment.mercenaryContracts)
        {
            if (contract != null)
            {
                contract.characterId = NormalizeCharacterReference(
                    contract.characterId);
            }
        }
    }

    private static string NormalizeCharacterReference(string value)
    {
        if (value == null
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return value;
        }

        return CharacterId.TryCanonicalizeV18Restore(
                value,
                out CharacterId canonical,
                out bool wasLegacy)
            && wasLegacy
                ? canonical.Value
                : value;
    }

    private void ValidateMeta(
        DungeonMetaProgressionSaveData source,
        DungeonGameRestoreReport report)
    {
        foreach (int buildingId in source?.runProgress?.discoveredFacilityIds
                     ?? new List<int>())
        {
            try
            {
                buildingDefinitions.GetBuilding(buildingId);
            }
            catch (Exception)
            {
                report.AddError(
                    $"Meta progression references unknown discovered facility definition {buildingId}.");
            }
        }
    }

    private static void ValidateResearch(
        DungeonResearchSaveData source,
        ModularFacilityWorldSaveData buildings,
        DungeonOffenseAggregateSaveData offense,
        DungeonGameRestoreReport report)
    {
        ValidateDungeonExpansionResearch(source, buildings, report);

        HashSet<string> regionIds = (offense?.regions?.regions
                ?? new List<OffenseRegionState>())
            .Where(region => region != null)
            .Select(region => region.regionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (KnowledgeResidueTaskSaveData task in source?.knowledgeTasks
                     ?? new List<KnowledgeResidueTaskSaveData>())
        {
            if (task == null)
            {
                continue;
            }
            if (task.facilityId != 0
                && !(buildings?.buildings
                    ?? new List<ModularFacilityBuildingSaveData>()).Any(building =>
                        building != null
                        && building.buildingId == task.facilityId
                        && building.centerX == task.facilityX
                        && building.centerY == task.facilityY))
            {
                report.AddError(
                    $"Knowledge task '{task.taskId}' references missing facility {task.facilityId} at ({task.facilityX},{task.facilityY}).");
            }
            if (task.use == KnowledgeResidueUse.RegionReconnaissance
                && !regionIds.Contains(task.regionId))
            {
                report.AddError(
                    $"Knowledge task '{task.taskId}' references missing offense region '{task.regionId}'.");
            }
        }
    }

    internal static void ValidateDungeonExpansionResearch(
        DungeonResearchSaveData research,
        ModularFacilityWorldSaveData facilities,
        DungeonGameRestoreReport report)
    {
        if (!DungeonSpaceGridLayout.TryCapture(
                facilities,
                out DungeonInteriorLayoutSnapshot layout,
                out string failureReason))
        {
            report.AddError(
                $"Dungeon expansion save layout is invalid: {failureReason}");
            return;
        }

        int expectedColumns =
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                research?.completedProjectIds);
        if (layout.ColumnCount != expectedColumns)
        {
            report.AddError(
                $"Dungeon expansion research/layout mismatch: completed tier expects {expectedColumns} interior columns, save contains {layout.ColumnCount}.");
        }
    }

    private void ValidatePowerFuelPhysicalReceipts(
        DungeonPowerInfrastructureSaveData source,
        ISet<string> buildingInstanceIds,
        DungeonSavePhysicalReferenceIndex physical,
        DungeonInfrastructureObservationSaveData infrastructureOutcomes,
        GameplayOutcomeLedgerSaveData outcomeLedger,
        DungeonGameRestoreReport report)
    {
        foreach (PowerNodeSaveData node in source?.nodes
                     ?? new List<PowerNodeSaveData>())
        {
            if (node == null)
            {
                continue;
            }
            RequireReference(
                node.buildingInstanceId,
                buildingInstanceIds,
                "Power infrastructure node",
                report);
            PowerFuelCommitSaveData pending = node.pendingFuel;
            PowerFuelCommitPhase phase =
                (PowerFuelCommitPhase)(pending?.phase ?? 0);
            if (pending == null || phase == PowerFuelCommitPhase.None)
            {
                continue;
            }

            references.RequireItemDefinition(pending.itemId, report);
            bool hasPending = physical.PendingBatchByOperation.TryGetValue(
                pending.operationId ?? string.Empty,
                out PhysicalItemBatchDispositionSaveData receipt);
            if (!hasPending)
            {
                if (phase == PowerFuelCommitPhase.OutcomePublished)
                {
                    report.AddError(
                        $"Power fuel commit '{pending.operationId}' lost its published physical Sink receipt.");
                }
                continue;
            }

            bool exact = receipt != null
                && receipt.kind == (int)PhysicalItemDispositionKind.Sink
                && string.Equals(
                    receipt.operationId,
                    pending.operationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    receipt.reasonCode,
                    pending.reasonCode,
                    StringComparison.Ordinal)
                && receipt.quantity == pending.quantity;
            if (phase == PowerFuelCommitPhase.OutcomePublished)
            {
                exact = exact
                    && string.Equals(
                        receipt.commitId,
                        pending.commitId,
                        StringComparison.Ordinal)
                    && receipt.inputMassGrams == pending.inputMassGrams
                    && receipt.sourceStackIds.SequenceEqual(
                        pending.sourceStackIds,
                        StringComparer.Ordinal);
                ValidatePowerFuelOutcomeJoin(
                    node,
                    pending,
                    infrastructureOutcomes,
                    outcomeLedger,
                    report);
            }
            if (!exact)
            {
                report.AddError(
                    $"Power fuel commit '{pending.operationId}' does not join its exact pending physical Sink receipt.");
            }
        }
    }

    internal static void ValidatePowerFuelOutcomeJoin(
        PowerNodeSaveData node, PowerFuelCommitSaveData pending, DungeonInfrastructureObservationSaveData infrastructureOutcomes,
        GameplayOutcomeLedgerSaveData outcomeLedger, DungeonGameRestoreReport report) =>
        IndustrialTransitionFactObservation.ValidatePowerFuelJoin(node, pending, infrastructureOutcomes, outcomeLedger, report);
}

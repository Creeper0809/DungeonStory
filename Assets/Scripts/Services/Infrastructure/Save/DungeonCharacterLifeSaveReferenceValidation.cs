using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DungeonSaveReferenceRules;

/// <summary>Validates detached life, household and social references.</summary>
internal sealed class DungeonCharacterLifeSaveReferenceValidation
{
    private readonly DungeonSaveReferenceRules references;
    private readonly ICharacterLifeDefinitionCatalog lifeDefinitions;

    internal DungeonCharacterLifeSaveReferenceValidation(DungeonSaveReferenceRules references, ICharacterLifeDefinitionCatalog lifeDefinitions)
    {
        this.references = references ?? throw new ArgumentNullException(nameof(references));
        this.lifeDefinitions = lifeDefinitions ?? throw new ArgumentNullException(nameof(lifeDefinitions));
    }

    internal HashSet<string> ValidateCharacterLife(
        CharacterLifeWorldSaveData source,
        ISet<string> characterIds,
        ISet<string> buildingInstanceIds,
        DungeonSavePhysicalReferenceIndex physical,
        DungeonGameRestoreReport report)
    {
        HashSet<string> lifeIds = new(StringComparer.Ordinal);
        foreach (CharacterLifeRecordSaveData record in source?.characters
                     ?? new List<CharacterLifeRecordSaveData>())
        {
            if (record == null)
            {
                report.AddError("Character-life aggregate contains a null record.");
                continue;
            }

            RequireUniqueId(record.characterId, "character-life record", lifeIds, report);
            RequireReference(record.characterId, characterIds, "Character-life record", report);
            CharacterSpeciesId speciesId = new(record.phenotypeSpeciesId);
            if (!speciesId.IsValid)
            {
                report.AddError(
                    $"Character-life record '{record.characterId}' has invalid phenotype species '{record.phenotypeSpeciesId}'.");
            }
            else
            {
                try
                {
                    SpeciesLifeHistoryDefinition history =
                        lifeDefinitions.RequireLifeHistory(speciesId);
                    CharacterLifeStage expected = history.ResolveStage(
                        record.biologicalAgeDayUnits);
                    if (expected != record.lifeStage)
                    {
                        report.AddError(
                            $"Character-life record '{record.characterId}' has life stage {record.lifeStage}, expected {expected}.");
                    }
                }
                catch (Exception)
                {
                    report.AddError(
                        $"Character-life record '{record.characterId}' references unknown phenotype species '{record.phenotypeSpeciesId}'.");
                }
            }

            HashSet<string> conditionIds = new(StringComparer.Ordinal);
            foreach (CharacterAgeConditionSaveData condition in record.ageConditions
                         ?? new List<CharacterAgeConditionSaveData>())
            {
                if (condition == null
                    || string.IsNullOrWhiteSpace(condition.conditionId)
                    || !conditionIds.Add(condition.conditionId))
                {
                    report.AddError(
                        $"Character-life record '{record.characterId}' contains an invalid or duplicate aging condition.");
                }
            }
        }

        foreach (var row in source?.pendingDailyObservation?.entries ?? new List<CharacterLifeTransitionPublicationSaveData>())
        {
            if (row?.fact == null) { report.AddError("Character-life pending observation is null."); continue; }
            RequireReference(row.fact.characterId, characterIds, "Pending life transition character", report);
        }
        foreach (var projection in source?.pendingAgeConditionAnatomyProjections ?? new List<AgeConditionAnatomyProjectionSaveData>())
        {
            if (projection == null) { report.AddError("Character-life body projection is null."); continue; }
            RequireReference(projection.characterId, characterIds, "Age-condition body projection character", report);
            try { _ = lifeDefinitions.RequireAgeCondition(projection.conditionId); }
            catch (Exception) { report.AddError("Age-condition body projection definition is unavailable: " + projection.conditionId); }
        }

        TemporalStasisMaintenanceCommitSaveData maintenance =
            source?.pendingTemporalStasisMaintenance;
        TemporalStasisMaintenanceCommitPhase phase =
            (TemporalStasisMaintenanceCommitPhase)(maintenance?.phase ?? 0);
        if (maintenance != null
            && phase != TemporalStasisMaintenanceCommitPhase.None)
        {
            RequireReference(
                maintenance.characterId,
                characterIds,
                "Temporal-stasis maintenance character",
                report);
            RequireReference(
                maintenance.facilityInstanceId,
                buildingInstanceIds,
                "Temporal-stasis maintenance facility",
                report);
            references.RequireItemDefinition(maintenance.runeConductorItemId, report);
            references.RequireItemDefinition(maintenance.manaCrystalItemId, report);

            bool hasPending = physical.PendingBatchByOperation.TryGetValue(
                maintenance.operationId ?? string.Empty,
                out PhysicalItemBatchDispositionSaveData receipt);
            if (hasPending)
            {
                int expectedQuantity = checked(
                    maintenance.runeConductorQuantity
                    + maintenance.manaCrystalQuantity);
                bool exact = receipt != null
                    && receipt.kind
                        == (int)PhysicalItemDispositionKind.Sink
                    && string.Equals(
                        receipt.operationId,
                        maintenance.operationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        receipt.reasonCode,
                        maintenance.reasonCode,
                        StringComparison.Ordinal)
                    && receipt.quantity == expectedQuantity;
                if (phase
                    == TemporalStasisMaintenanceCommitPhase.OutcomePublished)
                {
                    exact = exact
                        && string.Equals(
                            receipt.commitId,
                            maintenance.commitId,
                            StringComparison.Ordinal)
                        && receipt.inputMassGrams
                            == maintenance.inputMassGrams
                        && receipt.sourceStackIds.SequenceEqual(
                            maintenance.sourceStackIds,
                            StringComparer.Ordinal);
                }
                if (!exact)
                {
                    report.AddError(
                        $"Temporal-stasis maintenance '{maintenance.operationId}' does not join its exact pending physical Sink receipt.");
                }
            }
        }

        return lifeIds;
    }

    internal static HashSet<string> ValidateRetainedDeadActors(
        DungeonCharacterWorldSaveData characters,
        DungeonOffenseAggregateSaveData offense,
        KinshipHouseholdWorldSaveData kinshipHouseholds,
        DungeonMetaProgressionSaveData meta,
        DungeonRunFlowSaveData runFlow,
        DungeonGameRestoreReport report)
    {
        HashSet<string> activeExpeditionActorIds =
            (offense?.expedition?.activeExpeditions
                 ?? new List<DungeonOffenseExpeditionRunSaveData>())
            .Where(run => run != null)
            .SelectMany(run =>
                (run.memberPersistentIds ?? new List<string>())
                .Concat(run.protectedRescueMemberPersistentIds
                    ?? new List<string>()))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> tombstoneIds =
            (kinshipHouseholds?.kinship?.tombstones
                 ?? new List<CharacterTombstoneSaveData>())
            .Where(tombstone => tombstone != null)
            .Select(tombstone => tombstone.characterId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> retained = new(StringComparer.Ordinal);

        foreach (DungeonCharacterSaveData actor in characters?.actors
                     ?? new List<DungeonCharacterSaveData>())
        {
            if (actor == null)
            {
                continue;
            }

            bool deadByLifecycle = actor.lifecycleState
                == CharacterLifecycleState.Despawned;
            bool deadByHealth = actor.currentHealth <= 0f;
            if (!deadByLifecycle && !deadByHealth)
            {
                continue;
            }

            string actorId = actor.persistentId ?? string.Empty;
            if (deadByLifecycle != deadByHealth)
            {
                report.AddError(
                    $"Retained character actor '{actorId}' has inconsistent death lifecycle and health state.");
                continue;
            }
            bool terminalOwner = actor.isOwner
                && string.Equals(actorId, CharacterId.Owner.Value, StringComparison.Ordinal)
                && (meta?.ended == true && meta.hasLatestResult
                    && meta.latestResult?.outcome == DungeonRunOutcome.Defeat
                    // The exact completion fact is cross-validated by its
                    // preflight before replay can deliver the settlement event.
                    || meta?.ended == false && !meta.hasLatestResult
                    && runFlow?.pendingCompletion?.fact?.ownerId == actorId
                    && runFlow.pendingCompletion.fact.outcome == DungeonRunOutcome.Defeat)
                && runFlow?.phase == DungeonRunPhase.Finished
                && runFlow.outcome == DungeonRunOutcome.Defeat;
            if (!terminalOwner && !activeExpeditionActorIds.Contains(actorId))
            {
                report.AddError(
                    $"Dead character actor '{actorId}' has no exact terminal-owner or active-expedition ownership.");
                continue;
            }
            if (!tombstoneIds.Contains(actorId))
            {
                report.AddError(
                    $"Retained dead character '{actorId}' has no matching tombstone.");
                continue;
            }

            retained.Add(actorId);
        }

        return retained;
    }

    internal static DungeonSaveKinshipReferenceIndex ValidateKinshipHouseholds(
        KinshipHouseholdWorldSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> actorIds,
        ISet<string> retainedDeadActorIds,
        ISet<string> buildingIds,
        DungeonGameRestoreReport report)
    {
        DungeonSaveKinshipReferenceIndex result = new(livingCharacterIds);
        HashSet<string> tombstoneIds = result.TombstoneIds;
        foreach (CharacterTombstoneSaveData tombstone in source?.kinship?.tombstones
                     ?? new List<CharacterTombstoneSaveData>())
        {
            if (tombstone == null)
            {
                report.AddError("Kinship aggregate contains a null tombstone.");
                continue;
            }

            RequireUniqueId(tombstone.characterId, "tombstone", tombstoneIds, report);
            if (livingCharacterIds.Contains(tombstone.characterId))
            {
                report.AddError(
                    $"Character '{tombstone.characterId}' is both living and archived as a tombstone.");
            }
            else if (actorIds.Contains(tombstone.characterId)
                     && !retainedDeadActorIds.Contains(
                         tombstone.characterId))
            {
                report.AddError(
                    $"Character '{tombstone.characterId}' retains an archived actor without exact terminal-owner or active-expedition death ownership.");
            }
            if (!new CharacterSpeciesId(tombstone.phenotypeSpeciesId).IsValid)
            {
                report.AddError(
                    $"Tombstone '{tombstone.characterId}' has invalid phenotype species '{tombstone.phenotypeSpeciesId}'.");
            }
            if (tombstone.deathAbsoluteDay < 1
                || tombstone.deathAbsoluteDay < tombstone.birthAbsoluteDay
                || tombstone.generation < 0
                || !string.IsNullOrWhiteSpace(tombstone.householdId)
                && !new HouseholdId(tombstone.householdId).IsValid)
            {
                report.AddError(
                    $"Tombstone '{tombstone.characterId}' has invalid temporal, household, or generation data.");
            }
        }

        result.AllCharacterIds.UnionWith(tombstoneIds);
        HashSet<string> linkKeys = new(StringComparer.Ordinal);
        foreach (CharacterKinshipLinkSaveData link in source?.kinship?.links
                     ?? new List<CharacterKinshipLinkSaveData>())
        {
            if (link == null)
            {
                report.AddError("Kinship aggregate contains a null relationship link.");
                continue;
            }
            RequireReference(link.sourceCharacterId, result.AllCharacterIds, "Kinship source", report);
            RequireReference(link.targetCharacterId, result.AllCharacterIds, "Kinship target", report);
            string key = $"{(int)link.kind}:{link.sourceCharacterId}:{link.targetCharacterId}";
            if (!linkKeys.Add(key))
            {
                report.AddError($"Kinship aggregate contains duplicate relationship '{key}'.");
            }
        }

        HashSet<string> assignedCharacters = new(StringComparer.Ordinal);
        HashSet<string> assignedBedSlots = new(StringComparer.Ordinal);
        foreach (CharacterRoomAssignmentSaveData assignment in source?.households?.assignments
                     ?? new List<CharacterRoomAssignmentSaveData>())
        {
            if (assignment == null)
            {
                report.AddError("Household aggregate contains a null room assignment.");
                continue;
            }
            RequireReference(assignment.characterId, livingCharacterIds, "Household character", report);
            RequireReference(assignment.roomBuildingId, buildingIds, "Household room", report);
            RequireReference(assignment.bedBuildingId, buildingIds, "Household bed", report);
            if (!new HouseholdId(assignment.householdId).IsValid)
            {
                report.AddError(
                    $"Household assignment for '{assignment.characterId}' has invalid household ID '{assignment.householdId}'.");
            }
            if (!assignedCharacters.Add(assignment.characterId))
            {
                report.AddError(
                    $"Character '{assignment.characterId}' has multiple room assignments.");
            }
            if (assignment.bedSlotIndex < 0)
            {
                report.AddError(
                    $"Bed '{assignment.bedBuildingId}' has invalid slot index {assignment.bedSlotIndex}.");
            }
            string bedSlotKey = assignment.bedBuildingId
                + ":"
                + assignment.bedSlotIndex;
            if (!assignedBedSlots.Add(bedSlotKey))
            {
                report.AddError(
                    $"Bed slot '{bedSlotKey}' is assigned to multiple characters.");
            }
            result.HouseholdIds.Add(assignment.householdId);
        }

        HashSet<string> pendingCharacters = new(StringComparer.Ordinal);
        foreach (HouseholdPendingAssignmentSaveData pending in
                 source?.households?.pendingAssignments
                 ?? new List<HouseholdPendingAssignmentSaveData>())
        {
            if (pending == null)
            {
                report.AddError(
                    "Household aggregate contains a null pending assignment.");
                continue;
            }
            RequireReference(
                pending.characterId,
                livingCharacterIds,
                "Pending household character",
                report);
            RequireReference(
                pending.firstParentId,
                result.AllCharacterIds,
                "Pending household first parent",
                report);
            if (!string.IsNullOrWhiteSpace(pending.secondParentId))
                RequireReference(
                    pending.secondParentId,
                    result.AllCharacterIds,
                    "Pending household second parent",
                    report);
            if (!string.IsNullOrWhiteSpace(pending.carrierId))
                RequireReference(
                    pending.carrierId,
                    result.AllCharacterIds,
                    "Pending household carrier",
                    report);
            if (!string.IsNullOrWhiteSpace(pending.preferredHouseholdId)
                && !new HouseholdId(pending.preferredHouseholdId).IsValid)
                report.AddError(
                    $"Pending household assignment for '{pending.characterId}' has invalid preferred household ID '{pending.preferredHouseholdId}'.");
            if (!pendingCharacters.Add(pending.characterId)
                || assignedCharacters.Contains(pending.characterId))
                report.AddError(
                    $"Character '{pending.characterId}' has conflicting household assignment state.");
            if (pending.requestedAbsoluteDay < 1
                || pending.attemptRevision < 1L
                || string.IsNullOrWhiteSpace(pending.reasonCode))
                report.AddError(
                    $"Pending household assignment for '{pending.characterId}' has invalid request metadata.");
            if (!string.IsNullOrWhiteSpace(pending.preferredHouseholdId))
                result.HouseholdIds.Add(pending.preferredHouseholdId);
        }

        foreach (LineageSummarySaveData summary in source?.kinship?.lineageSummaries
                     ?? new List<LineageSummarySaveData>())
        {
            if (summary == null || !new HouseholdId(summary.householdId).IsValid)
            {
                report.AddError("Kinship aggregate contains an invalid lineage summary.");
            }
            else
            {
                result.HouseholdIds.Add(summary.householdId);
            }
        }

        return result;
    }

    internal void ValidateReproduction(
        ReproductionWorldSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> knownCharacterIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> processIds = new(StringComparer.Ordinal);
        foreach (ReproductionProcessSaveData process in source?.processes
                     ?? new List<ReproductionProcessSaveData>())
        {
            if (process == null)
            {
                report.AddError("Reproduction aggregate contains a null process.");
                continue;
            }
            RequireUniqueId(process.processId, "reproduction process", processIds, report);
            RequireReference(process.firstParentId, knownCharacterIds, "Reproduction first parent", report);
            if (process.mode != ReproductionMode.GolemAssembly)
            {
                RequireReference(process.secondParentId, knownCharacterIds, "Reproduction second parent", report);
            }
            if (!string.IsNullOrWhiteSpace(process.carrierId))
            {
                RequireReference(process.carrierId, knownCharacterIds, "Reproduction carrier", report);
                if (process.status is ReproductionProcessStatus.Active
                        or ReproductionProcessStatus.WaitingForEnvironment
                        or ReproductionProcessStatus.WaitingForEmergencyExtraction
                    && process.carrierDeathAbsoluteDay <= 0
                    && !livingCharacterIds.Contains(process.carrierId))
                {
                    report.AddError(
                        $"Active reproduction carrier '{process.carrierId}' is not a living character.");
                }
            }
            if (process.resultPublished)
            {
                RequireReference(
                    process.resultCharacterId,
                    livingCharacterIds,
                    "Published reproduction result",
                    report);
            }
            else if (!string.IsNullOrWhiteSpace(process.resultCharacterId))
            {
                report.AddError(
                    $"Unpublished reproduction process '{process.processId}' contains a result character ID.");
            }
            CharacterSpeciesId speciesId = new(process.phenotypeSpeciesId);
            try
            {
                if (!speciesId.IsValid)
                    throw new InvalidOperationException();
                lifeDefinitions.RequireLifeHistory(speciesId);
            }
            catch (Exception)
            {
                report.AddError(
                    $"Reproduction process '{process.processId}' references unknown phenotype species '{process.phenotypeSpeciesId}'.");
            }
        }
    }

    internal static void ValidateCareers(
        CharacterCareerWorldSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> tombstoneCharacterIds,
        ISet<string> buildingIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> careerIds = new(StringComparer.Ordinal);
        foreach (CharacterCareerSaveData career in source?.characters
                     ?? new List<CharacterCareerSaveData>())
        {
            if (career == null)
            {
                report.AddError("Career aggregate contains a null character record.");
                continue;
            }
            RequireUniqueId(career.characterId, "career character", careerIds, report);
            if (tombstoneCharacterIds.Contains(career.characterId))
            {
                if (career.position != CareerPositionKind.None
                    || !string.IsNullOrEmpty(career.positionScopeId)
                    || career.retirementScheduleStatus ==
                        RetirementScheduleStatus.Pending)
                {
                    report.AddError(
                        $"Tombstoned career character '{career.characterId}' retains an active position, scope, or retirement schedule.");
                }
            }
            else
            {
                RequireReference(
                    career.characterId,
                    livingCharacterIds,
                    "Career character",
                    report);
            }
            if (career.retiredWorkAbsoluteDay < 0
                || career.retiredWorkSeconds < 0f
                || float.IsNaN(career.retiredWorkSeconds)
                || float.IsInfinity(career.retiredWorkSeconds)
                || career.retiredWorkSeconds
                    > CareerRules.RetireeMaximumSafeWorkSeconds + 0.001f)
            {
                report.AddError(
                    $"Career character '{career.characterId}' has invalid retiree work time.");
            }
        }
        HashSet<string> mentoredStudents = new(StringComparer.Ordinal);
        foreach (CareerMentorshipSaveData mentorship in source?.mentorships
                     ?? new List<CareerMentorshipSaveData>())
        {
            if (mentorship == null)
            {
                report.AddError("Career aggregate contains a null mentorship.");
                continue;
            }
            RequireReference(
                mentorship.mentorCharacterId,
                livingCharacterIds,
                "Mentorship mentor",
                report);
            RequireReference(
                mentorship.studentCharacterId,
                livingCharacterIds,
                "Mentorship student",
                report);
            RequireReference(
                mentorship.academyBuildingId,
                buildingIds,
                "Mentorship academy",
                report);
            if (!mentoredStudents.Add(mentorship.studentCharacterId)
                || string.Equals(
                    mentorship.mentorCharacterId,
                    mentorship.studentCharacterId,
                    StringComparison.Ordinal)
                || mentorship.lastAwardAbsoluteDay < 0)
            {
                report.AddError(
                    $"Mentorship for '{mentorship.studentCharacterId}' is invalid or duplicated.");
            }
        }
    }

    internal static void ValidatePsychosocial(
        CharacterPsychosocialWorldSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> knownCharacterIds,
        DungeonGameRestoreReport report)
    {
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CharacterPsychosocialRecordSaveData record in source?.characters
                     ?? new List<CharacterPsychosocialRecordSaveData>())
        {
            if (record == null)
            {
                report.AddError("Psychosocial aggregate contains a null character record.");
                continue;
            }
            RequireUniqueId(record.characterId, "psychosocial character", recordIds, report);
            RequireReference(record.characterId, livingCharacterIds, "Psychosocial character", report);
            if (record.lastLongNightMemorialYear < 0)
            {
                report.AddError(
                    $"Psychosocial record '{record.characterId}' has an invalid long-night year.");
            }
            HashSet<string> festivals = new(StringComparer.Ordinal);
            foreach (FestivalAttendanceSaveData attendance in record.festivalAttendance
                         ?? new List<FestivalAttendanceSaveData>())
            {
                if (attendance == null
                    || string.IsNullOrWhiteSpace(attendance.festivalId)
                    || attendance.year < 1
                    || !festivals.Add(attendance.festivalId))
                {
                    report.AddError(
                        $"Psychosocial record '{record.characterId}' has invalid festival attendance.");
                }
            }
            foreach (GriefIncidentSaveData incident in record.grief
                         ?? new List<GriefIncidentSaveData>())
            {
                if (incident == null)
                {
                    report.AddError(
                        $"Psychosocial record '{record.characterId}' contains a null grief incident.");
                    continue;
                }
                RequireReference(
                    incident.deceasedCharacterId,
                    knownCharacterIds,
                    "Grief deceased character",
                    report);
            }
        }
    }

    internal static void ValidateObservedPromotionNarrativeJoins(
        SocietyEventWorldSaveData society,
        CharacterNarrativeWorldSaveData narrative,
        DungeonGameRestoreReport report)
    {
        const string definitionId = "life-event:quiet-promotion";
        List<ObservedLifeEventReceiptSaveData> receipts =
            (society?.successfulLifeEventOperations
                ?? new List<ObservedLifeEventReceiptSaveData>())
            .Where(value => value?.sourceKind
                == ObservedLifeEventSourceKind.ProficiencyPromotion)
            .ToList();
        List<V20ActiveEventSaveData> occurrences =
            (society?.activeEvents ?? new List<V20ActiveEventSaveData>())
            .Concat(society?.recentResolvedEvents
                ?? new List<V20ActiveEventSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.definitionId,
                    definitionId,
                    StringComparison.Ordinal))
            .ToList();

        foreach (ObservedLifeEventReceiptSaveData saved in receipts)
        {
            if (!ObservedQuietPromotionLifeEventReceipt.TryParse(
                    saved.canonicalPayload,
                    out ObservedQuietPromotionLifeEventReceipt receipt)
                || !string.Equals(
                    saved.sourceOperationId,
                    receipt.SourceOperationId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    "Quiet-promotion receipt has an invalid exact source payload.");
                continue;
            }

            CharacterNarrativeSaveData[] characterMatches =
                (narrative?.characters
                    ?? new List<CharacterNarrativeSaveData>())
                .Where(value => value != null
                    && string.Equals(
                        value.characterId,
                        receipt.CharacterId.Value,
                        StringComparison.Ordinal))
                .ToArray();
            NarrativeSkillExperienceSaveData[] proficiencyMatches =
                characterMatches.Length == 1
                    ? (characterMatches[0].skillExperience
                            ?? new List<NarrativeSkillExperienceSaveData>())
                        .Where(value => value != null
                            && string.Equals(
                                value.proficiencyId,
                                receipt.ProficiencyId.Value,
                                StringComparison.Ordinal))
                        .ToArray()
                    : Array.Empty<NarrativeSkillExperienceSaveData>();
            if (characterMatches.Length != 1
                || proficiencyMatches.Length != 1
                || proficiencyMatches[0].lifetimeMilliExperience
                    < receipt.AfterLifetimeMilliExperience)
            {
                report.AddError(
                    $"Quiet-promotion receipt '{receipt.SourceOperationId}' has no exact character/proficiency lifetime join.");
            }

            V20ActiveEventSaveData[] occurrenceMatches = occurrences
                .Where(value => string.Equals(
                    value.instanceId,
                    saved.occurrenceInstanceId,
                    StringComparison.Ordinal))
                .ToArray();
            if (occurrenceMatches.Length != 1
                || !occurrenceMatches[0].resolved
                || !string.Equals(
                    occurrenceMatches[0].resolutionId,
                    "automatic",
                    StringComparison.Ordinal)
                || occurrenceMatches[0].participantCharacterIds == null
                || occurrenceMatches[0].participantCharacterIds.Count != 1
                || !string.Equals(
                    occurrenceMatches[0].participantCharacterIds[0],
                    receipt.CharacterId.Value,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Quiet-promotion receipt '{receipt.SourceOperationId}' has no exact automatic Society occurrence join.");
            }
        }

        HashSet<string> receiptOccurrenceIds = receipts
            .Where(value => value != null)
            .Select(value => value.occurrenceInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (V20ActiveEventSaveData occurrence in occurrences)
        {
            if (!receiptOccurrenceIds.Contains(occurrence.instanceId))
            {
                report.AddError(
                    $"Quiet-promotion occurrence '{occurrence.instanceId}' has no exact proficiency source receipt.");
            }
        }
    }
}

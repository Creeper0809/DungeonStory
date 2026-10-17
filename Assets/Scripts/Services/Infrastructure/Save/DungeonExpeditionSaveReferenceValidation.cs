using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DungeonSaveReferenceRules;

/// <summary>Validates detached expedition and invasion joins.</summary>
internal sealed class DungeonExpeditionSaveReferenceValidation
{
    private readonly DungeonSaveReferenceRules references;

    internal DungeonExpeditionSaveReferenceValidation(DungeonSaveReferenceRules references)
    {
        this.references = references ?? throw new ArgumentNullException(nameof(references));
    }

    internal static HashSet<string> BuildCombatCharacterIds(
        ISet<string> worldCharacterIds,
        OffenseBattlePersistenceState battle,
        DungeonGameRestoreReport report)
    {
        // Battle-owned enemy individuals are not resident CharacterActors. Their
        // equipment authority is admitted only by the persisted active roster,
        // joined exactly to battle combatants; never by an ID prefix or fallback.
        HashSet<string> owners = new(worldCharacterIds, StringComparer.Ordinal);
        if (battle == null) return owners;
        HashSet<string> combatants = new(StringComparer.Ordinal);
        foreach (OffenseBattleCombatantPersistenceState combatant in battle.combatants
                 ?? new List<OffenseBattleCombatantPersistenceState>())
        {
            string rawId = combatant?.persistentId;
            string id = rawId?.Trim() ?? string.Empty;
            if (combatant == null || id.Length == 0
                || !string.Equals(rawId, id, StringComparison.Ordinal)
                || !combatants.Add(id))
                report.AddError($"Active offense battle has an invalid or duplicate combatant '{rawId ?? string.Empty}'.");
        }
        HashSet<string> enemies = new(StringComparer.Ordinal);
        foreach (EnemyIndividualSaveData enemy in battle.enemyIndividuals
                 ?? new List<EnemyIndividualSaveData>())
        {
            string rawId = enemy?.characterId;
            CharacterId id = (CharacterId)rawId;
            if (enemy == null || !id.IsValid
                || !string.Equals(rawId, id.Value, StringComparison.Ordinal)
                || !enemies.Add(id.Value))
            {
                report.AddError($"Active offense battle has an invalid or duplicate enemy individual '{rawId ?? string.Empty}'.");
                continue;
            }
            if (worldCharacterIds.Contains(id.Value))
                report.AddError($"Active offense battle enemy '{id.Value}' collides with a world character.");
            else if (!combatants.Contains(id.Value))
                report.AddError($"Active offense battle enemy '{id.Value}' has no matching combatant.");
            else
                owners.Add(id.Value);
        }
        // Authored objectives are combatants, not characters or equipment owners.
        // Their composition remains the offense section's validation authority.
        return owners;
    }

    internal static void AddActiveInvasionCharacterIds(
        DungeonInvasionSaveData invasion,
        DungeonSaveCharacterReferenceIndex characterReferences,
        DungeonGameRestoreReport report)
    {
        HashSet<string> activeIntruderIds = new(StringComparer.Ordinal);
        foreach (DungeonInvasionIntruderSaveData intruder in
                 invasion?.activeIntruders
                 ?? new List<DungeonInvasionIntruderSaveData>())
        {
            string rawId = intruder?.enemyIndividual?.characterId;
            CharacterId characterId = (CharacterId)rawId;
            if (intruder == null
                || !characterId.IsValid
                || !string.Equals(rawId, characterId.Value, StringComparison.Ordinal)
                || !activeIntruderIds.Add(characterId.Value))
            {
                report.AddError(
                    $"Active invasion intruder has an invalid or duplicate character ID '{rawId ?? string.Empty}'.");
                continue;
            }

            if (!characterReferences.KnownCharacterIds.Add(characterId.Value))
            {
                report.AddError(
                    $"Active invasion intruder character '{characterId.Value}' collides with a resident character.");
                continue;
            }

            characterReferences.LivingCharacterIds.Add(characterId.Value);
        }
    }

    internal static void ValidateOffenseMembers(
        DungeonOffenseAggregateSaveData source,
        ISet<string> characterIds,
        DungeonGameRestoreReport report)
    {
        foreach (DungeonOffenseExpeditionRunSaveData run in source?.expedition?.activeExpeditions
                     ?? new List<DungeonOffenseExpeditionRunSaveData>())
        {
            if (run == null || string.IsNullOrWhiteSpace(run.expeditionId))
            {
                report.AddError("Offense aggregate contains an expedition without an ID.");
                continue;
            }

            foreach (string memberId in (run.memberPersistentIds ?? new List<string>())
                         .Concat(run.protectedRescueMemberPersistentIds ?? new List<string>())
                         .Concat((run.memberStates ?? new List<DungeonOffenseExpeditionMemberStateSaveData>())
                             .Where(state => state != null)
                             .Select(state => state.persistentId)))
            {
                string id = memberId?.Trim() ?? string.Empty;
                if (id.Length == 0 || !characterIds.Contains(id))
                {
                    report.AddError(
                        $"Expedition '{run.expeditionId}' references missing character '{id}'.");
                }
            }
        }
    }

    internal static void ValidateSeasonalOffenseOfferJoins(
        GameSessionSaveData session,
        SeasonalEventWorldSaveData seasonal,
        DungeonOffenseAggregateSaveData offense,
        DungeonGameRestoreReport report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        IReadOnlyList<V20ActiveEventSaveData> activeOccurrences =
            seasonal?.activeEvents ?? new List<V20ActiveEventSaveData>();
        foreach (OffenseWorldSiteStateData site in offense?.world?.sites
                     ?? new List<OffenseWorldSiteStateData>())
        {
            if (site?.state != OffenseWorldSiteState.Revealed
                || site.seasonalOffer?.IsConfigured != true)
            {
                continue;
            }

            OffenseSeasonalExpeditionOfferData offer = site.seasonalOffer;
            V20ActiveEventSaveData[] matches = activeOccurrences
                .Where(value => value?.seasonalSpecialExpedition?.configured == true
                    && string.Equals(
                        value.instanceId,
                        offer.occurrenceInstanceId,
                        StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (matches.Length != 1)
            {
                report.AddError(
                    $"Revealed seasonal offer '{site.siteId}' does not join exactly one active occurrence '{offer.occurrenceInstanceId}'.");
                continue;
            }

            V20ActiveEventSaveData occurrence = matches[0];
            bool exactLifetime = session != null
                && session.absoluteDay >= 1
                && session.absoluteDay <= occurrence.deadlineAbsoluteDay
                && occurrence.deadlineAbsoluteDay < int.MaxValue
                && string.Equals(
                    site.siteId,
                    "seasonal-expedition:" + occurrence.instanceId,
                    StringComparison.Ordinal)
                && site.createdDay == Math.Max(
                    1,
                    occurrence.startedAbsoluteDay)
                && site.expiresDay == occurrence.deadlineAbsoluteDay + 1;
            if (!exactLifetime)
            {
                report.AddError(
                    $"Revealed seasonal offer '{site.siteId}' has a stale or mismatched occurrence lifetime.");
                continue;
            }

            try
            {
                SeasonalArcaneEventRules.RequireMatchingOffer(
                    site,
                    occurrence);
            }
            catch (InvalidOperationException exception)
            {
                report.AddError(exception.Message);
            }
        }
    }

    internal static void ValidateOffenseArrivalResultJoins(
        DungeonOffenseAggregateSaveData source,
        DungeonGameRestoreReport report)
    {
        IReadOnlyList<OffenseReturnArrivalState> arrivals =
            source?.returnArrivals?.arrivals
            ?? new List<OffenseReturnArrivalState>();
        IReadOnlyList<DungeonOffenseExpeditionResultSaveData> results =
            source?.expedition?.resultHistory
            ?? new List<DungeonOffenseExpeditionResultSaveData>();
        Dictionary<string, string> arrivalOwners =
            new(StringComparer.Ordinal);
        Dictionary<string, int> receiptOccurrences =
            new(StringComparer.Ordinal);
        foreach (DungeonOffenseExpeditionResultSaveData result in results)
        {
            foreach (DungeonOffenseArrivalReceiptSaveData receipt in
                     result?.arrivalReceipts
                     ?? new List<DungeonOffenseArrivalReceiptSaveData>())
            {
                string receiptId = receipt?.arrivalId ?? string.Empty;
                if (receiptId.Length == 0) continue;
                receiptOccurrences.TryGetValue(receiptId, out int current);
                receiptOccurrences[receiptId] = checked(current + 1);
            }
        }

        foreach (OffenseReturnArrivalState arrival in arrivals)
        {
            if (arrival == null
                || string.IsNullOrWhiteSpace(arrival.arrivalId)
                || arrivalOwners.ContainsKey(arrival.arrivalId))
            {
                report.AddError(
                    "Offense return-arrival capture contains a missing or duplicate arrival ID.");
                continue;
            }
            arrivalOwners.Add(arrival.arrivalId, arrival.expeditionId);

            DungeonOffenseExpeditionResultSaveData[] matchingResults = results
                .Where(value => value != null
                    && string.Equals(
                        value.expeditionId,
                        arrival.expeditionId,
                        StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            DungeonOffenseExpeditionResultSaveData result =
                matchingResults.Length == 1 ? matchingResults[0] : null;
            DungeonOffenseArrivalReceiptSaveData[] receipts = result
                ?.arrivalReceipts
                ?.Where(value => value != null
                    && string.Equals(
                        value.arrivalId,
                        arrival.arrivalId,
                        StringComparison.Ordinal))
                .ToArray()
                ?? Array.Empty<DungeonOffenseArrivalReceiptSaveData>();
            OffenseExpeditionArrivalResolution expectedResolution =
                arrival.stage switch
                {
                    OffenseReturnArrivalStage.Secured =>
                        OffenseExpeditionArrivalResolution.Secured,
                    OffenseReturnArrivalStage.Escaped =>
                        OffenseExpeditionArrivalResolution.Escaped,
                    _ => OffenseExpeditionArrivalResolution.Pending
                };
            bool frozenCountsMatch = expectedResolution
                    == OffenseExpeditionArrivalResolution.Pending
                || receipts.Length == 1
                    && receipts[0].materializedAmount
                        == arrival.settledMaterializedAmount
                    && receipts[0].securedAmount
                        == arrival.settledSecuredAmount
                    && receipts[0].escapedAmount
                        == arrival.settledEscapedAmount;
            if (matchingResults.Length != 1
                || receipts.Length != 1
                || !receiptOccurrences.TryGetValue(
                    arrival.arrivalId,
                    out int receiptOccurrence)
                || receiptOccurrence != 1
                || !string.Equals(
                    receipts[0].kind,
                    arrival.kind.ToString(),
                    StringComparison.Ordinal)
                || receipts[0].requestedAmount != arrival.requestedAmount
                || receipts[0].resolution != expectedResolution
                || !frozenCountsMatch)
            {
                report.AddError(
                    $"Offense return-arrival '{arrival.arrivalId}' is torn from its committed result receipt.");
            }
        }

        foreach (DungeonOffenseExpeditionResultSaveData result in results
                     .Where(value => value != null))
        {
            foreach (DungeonOffenseArrivalReceiptSaveData receipt in
                     result.arrivalReceipts
                     ?? new List<DungeonOffenseArrivalReceiptSaveData>())
            {
                string receiptId = receipt?.arrivalId ?? string.Empty;
                if (receipt == null
                    || !arrivalOwners.TryGetValue(
                        receiptId,
                        out string ownerExpeditionId)
                    || !string.Equals(
                        ownerExpeditionId,
                        result.expeditionId,
                        StringComparison.Ordinal)
                    || !receiptOccurrences.TryGetValue(
                        receiptId,
                        out int receiptOccurrence)
                    || receiptOccurrence != 1)
                {
                    report.AddError(
                        $"Offense expedition result '{result.expeditionId}' contains an orphan, foreign-owned, or duplicate return-arrival receipt '{receiptId}'.");
                }
            }
        }
    }

    internal void ValidateOffenseReferences(
        DungeonOffenseAggregateSaveData source,
        ISet<string> characterIds,
        ISet<string> combatCharacterIds,
        DungeonSavePhysicalReferenceIndex physical,
        ISet<string> buildingIds,
        DungeonGameRestoreReport report)
    {
        OffenseWorldSaveData world = source?.world;
        foreach (FieldStabilizationState stabilization in
                 world?.fieldStabilizations
                 ?? new List<FieldStabilizationState>())
        {
            if (stabilization == null)
            {
                continue;
            }
            RequireReference(
                stabilization.characterId,
                characterIds,
                $"Field stabilization '{stabilization.expeditionId}' character",
                report);
            string kitId = stabilization.consumedKitInstanceId ?? string.Empty;
            if (!kitId.StartsWith("packed:", StringComparison.Ordinal)
                && !physical.ItemInstanceIds.Contains(kitId)
                && !physical.StackIds.Contains(kitId))
            {
                report.AddError(
                    $"Field stabilization '{stabilization.expeditionId}' references missing consumed kit '{kitId}'.");
            }
        }

        foreach (OffenseCasualtyCarryState carry in world?.casualtyCarries
                     ?? new List<OffenseCasualtyCarryState>())
        {
            if (carry == null)
            {
                continue;
            }
            RequireReference(
                carry.casualtyCharacterId,
                characterIds,
                $"Casualty carry '{carry.expeditionId}' casualty",
                report);
            RequireReference(
                carry.carrierCharacterId,
                characterIds,
                $"Casualty carry '{carry.expeditionId}' carrier",
                report);
        }

        foreach (RescueConvoyState convoy in world?.rescueConvoys
                     ?? new List<RescueConvoyState>())
        {
            if (convoy == null)
            {
                continue;
            }
            foreach (string characterId in
                     (convoy.rescuerCharacterIds ?? new List<string>())
                     .Concat(convoy.protectedCasualtyIds ?? new List<string>()))
            {
                RequireReference(
                    characterId,
                    characterIds,
                    $"Rescue convoy '{convoy.rescueExpeditionId}' character",
                    report);
            }
        }

        foreach (OffenseUrgentMitigationOrderStateData order in
                 world?.mitigationOrders
                 ?? new List<OffenseUrgentMitigationOrderStateData>())
        {
            if (order != null
                && !string.IsNullOrEmpty(order.facilityPersistentId))
            {
                RequireReference(
                    order.facilityPersistentId,
                    buildingIds,
                    $"Offense mitigation order '{order.orderId}' facility",
                    report);
            }
        }

        foreach (OffenseUrgentMitigationOrderStateData order in
                 world?.mitigationOrders
                 ?? new List<OffenseUrgentMitigationOrderStateData>())
        {
            if (order == null
                || (OffenseUrgentMitigationCommitPhase)order.physicalCommitPhase
                    == OffenseUrgentMitigationCommitPhase.None)
            {
                continue;
            }
            bool hasPending = physical.PendingBatchByOperation.TryGetValue(
                order.physicalOperationId ?? string.Empty,
                out PhysicalItemBatchDispositionSaveData pending);
            if (order.physicalReceiptAcknowledged)
            {
                if (hasPending)
                {
                    report.AddError(
                        $"Acknowledged mitigation order '{order.orderId}' still has a pending physical receipt.");
                }
                continue;
            }
            if (!hasPending
                || pending == null
                || !string.Equals(
                    pending.commitId,
                    order.physicalCommitId,
                    StringComparison.Ordinal)
                || pending.quantity != order.inputQuantity
                || pending.inputMassGrams != order.inputMassGrams)
            {
                report.AddError(
                    $"Mitigation order '{order.orderId}' does not join its exact pending physical receipt.");
            }
        }

        foreach (OffenseSupplyPackingItemStateData cost in
                 (world?.supplyPackages
                      ?? new List<OffenseSupplyPackingStateData>())
                  .Where(package => package != null)
                  .SelectMany(package =>
                      (package.costs
                          ?? new List<OffenseSupplyPackingItemStateData>())
                      .Concat(package.returnedCosts
                          ?? new List<OffenseSupplyPackingItemStateData>())))
        {
            if (cost != null)
            {
                references.RequireItemDefinition(cost.itemId, report);
            }
        }

        foreach (OffenseThrownEquipmentPersistenceState thrown in
                 source?.expedition?.activeBattle?.thrownEquipment
                 ?? new List<OffenseThrownEquipmentPersistenceState>())
        {
            if (thrown == null)
            {
                continue;
            }
            RequireReference(
                thrown.instanceId,
                physical.EquipmentInstanceIds,
                "Thrown equipment",
                report);
            RequireReference(
                thrown.ownerCharacterId,
                combatCharacterIds,
                $"Thrown equipment '{thrown.instanceId}' owner",
                report);
        }

        foreach (DungeonOffenseExpeditionRunSaveData run in
                 source?.expedition?.activeExpeditions
                 ?? new List<DungeonOffenseExpeditionRunSaveData>())
        {
            foreach (DungeonOffenseEquipmentBaselineSaveData baseline in
                     run?.equipmentBaselines
                     ?? new List<DungeonOffenseEquipmentBaselineSaveData>())
            {
                if (baseline != null)
                {
                    if (!physical.EquipmentInstances.TryGetValue(
                            baseline.instanceId,
                            out CombatEquipmentInstance equipment))
                    {
                        report.AddError(
                            $"Expedition '{run.expeditionId}' equipment baseline references missing equipment '{baseline.instanceId}'.");
                    }
                    else if (!string.Equals(
                                 equipment.definitionId,
                                 baseline.definitionId,
                                 StringComparison.Ordinal))
                    {
                        report.AddError(
                            $"Expedition '{run.expeditionId}' equipment baseline '{baseline.instanceId}' does not match its staged equipment definition.");
                    }
                }
            }
        }
    }
}

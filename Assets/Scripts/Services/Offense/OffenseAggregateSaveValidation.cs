using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[Serializable]
public sealed class DungeonOffenseAggregateSaveData
{
    public const int CurrentVersion = 6;

    public int version = CurrentVersion;
    public DungeonOffenseCampaignSaveData campaign =
        new DungeonOffenseCampaignSaveData();
    public DungeonOffenseSaveData expedition = new DungeonOffenseSaveData();
    public OffenseWorldSaveData world = new OffenseWorldSaveData();
    public DungeonOffenseRegionSaveData regions =
        new DungeonOffenseRegionSaveData();
    public DungeonOffenseReturnArrivalSaveData returnArrivals =
        new DungeonOffenseReturnArrivalSaveData();
}

/// <summary>
/// Immutable, fully validated transport plan for an offense restore.  The plan is
/// deliberately detached from every live offense runtime; consumers may only bind
/// its IDs to already-staged world candidates after all section preflights pass.
/// </summary>
public sealed class OffenseAggregateRestorePlan
{
    internal OffenseAggregateRestorePlan(DungeonOffenseAggregateSaveData payload)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public DungeonOffenseAggregateSaveData Payload { get; }
}

/// <summary>
/// Resolves every authored definition referenced by an offense candidate while the
/// live world is still untouched. Cross-aggregate instance IDs are handled by
/// <see cref="DungeonAggregateReferencePreflight"/>.
/// </summary>
public sealed class OffenseAggregateAuthoredReferenceValidator
{
    private readonly IOffenseContentCatalog content;
    private readonly IItemDefinitionCatalog itemDefinitions;
    private readonly IOffenseCampaignCatalog campaigns;
    private readonly IEncounterCatalog encounters;

    public OffenseAggregateAuthoredReferenceValidator(
        IOffenseContentCatalog content,
        IItemDefinitionCatalog itemDefinitions,
        IOffenseCampaignCatalog campaigns,
        IEncounterCatalog encounters = null)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.itemDefinitions = itemDefinitions
            ?? throw new ArgumentNullException(nameof(itemDefinitions));
        this.campaigns = campaigns
            ?? throw new ArgumentNullException(nameof(campaigns));
        this.encounters = encounters;
    }

    public void Validate(OffenseAggregateRestorePlan plan)
    {
        DungeonOffenseAggregateSaveData payload = (plan
                ?? throw new ArgumentNullException(nameof(plan)))
            .Payload;
        HashSet<string> campaignTargets = campaigns.Targets
            .Where(value => value != null && value.IsValid)
            .Select(value => value.id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string targetId in payload.campaign.knownTargetIds
                     .Concat(payload.campaign.completedTargetIds)
                     .Concat(new[]
                     {
                         payload.campaign.selectedTargetId,
                         payload.campaign.revealedTruthTargetId
                     })
                     .Where(value => !string.IsNullOrEmpty(value)))
        {
            Require(campaignTargets.Contains(targetId),
                $"Offense campaign references unknown target '{targetId}'.");
        }
        foreach (DungeonOffenseExpeditionRunSaveData run in
                 payload.expedition.activeExpeditions.Where(value =>
                     !value.usesWorldTravel))
        {
            Require(campaignTargets.Contains(run.targetId),
                $"Expedition '{run.expeditionId}' references unknown target '{run.targetId}'.");
        }

        HashSet<string> archetypes = content.SiteArchetypes
            .Where(value => value != null)
            .Select(value => value.siteTypeId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (OffenseWorldSiteStateData site in payload.world.sites)
        {
            Require(archetypes.Contains(site.archetypeId),
                $"Offense site '{site.siteId}' references unknown archetype '{site.archetypeId}'.");
            OffenseSeasonalExpeditionOfferData offer = site.seasonalOffer;
            if (offer?.IsConfigured == true)
            {
                if (encounters != null)
                    encounters.Require(offer.authoredEncounterId);
                foreach (OffenseSeasonalPhysicalRewardData reward in
                         offer.physicalRewards)
                {
                    ItemDefinitionId itemId = new(reward.itemId);
                    Require(itemId.IsValid
                            && itemDefinitions.TryGet(itemId, out _),
                        $"Seasonal expedition site '{site.siteId}' references unknown reward item '{reward.itemId}'.");
                }
            }
        }

        HashSet<string> urgentDefinitions = content.UrgentSites
            .Where(value => value != null)
            .Select(value => value.urgentSiteId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (OffenseUrgentSiteStateData site in payload.world.urgentSites)
        {
            Require(urgentDefinitions.Contains(site.definitionId),
                $"Urgent site '{site.siteId}' references unknown definition '{site.definitionId}'.");
        }
        foreach (OffenseUrgentMitigationOrderStateData order in
                 payload.world.mitigationOrders)
        {
            Require(urgentDefinitions.Contains(order.definitionId),
                $"Mitigation order '{order.orderId}' references unknown definition '{order.definitionId}'.");
            OffenseUrgentSiteStateData site = payload.world.urgentSites.Single(
                value => string.Equals(value.siteId, order.siteId,
                    StringComparison.Ordinal));
            Require(string.Equals(site.definitionId, order.definitionId,
                    StringComparison.Ordinal),
                $"Mitigation order '{order.orderId}' definition does not match site '{order.siteId}'.");
            OffenseUrgentSiteDefinitionSO definition = content.UrgentSites
                .Single(value => value != null
                    && string.Equals(
                        value.urgentSiteId,
                        order.definitionId,
                        StringComparison.Ordinal));
            OffenseUrgentMitigationCommitPhase phase =
                (OffenseUrgentMitigationCommitPhase)order.physicalCommitPhase;
            if (phase != OffenseUrgentMitigationCommitPhase.None)
            {
                Require(order.inputQuantity == definition.mitigationItemAmount,
                    $"Mitigation order '{order.orderId}' physical quantity does not match authored cost.");
                Require(phase == OffenseUrgentMitigationCommitPhase.MaterialsCommitted
                        ? Mathf.Abs(site.mitigation - order.mitigationBefore)
                                <= 0.0001f
                            || Mathf.Abs(site.mitigation - order.mitigationAfter)
                                <= 0.0001f
                        : Mathf.Abs(site.mitigation - order.mitigationAfter)
                            <= 0.0001f,
                    $"Mitigation order '{order.orderId}' outcome does not join the urgent-site state.");
            }
        }

        Dictionary<string, OffenseDecisionCardSO> decisionCards =
            content.DecisionCards
                .Where(value => value != null
                    && !string.IsNullOrWhiteSpace(value.cardId))
                .ToDictionary(value => value.cardId, StringComparer.Ordinal);
        foreach (OffenseDecisionStateData decision in payload.world.decisions)
        {
            Require(decisionCards.TryGetValue(decision.cardId,
                    out OffenseDecisionCardSO card),
                $"Decision '{decision.expeditionId}' references unknown card '{decision.cardId}'.");
            if (decision.resolved)
            {
                Require(card.choices.Any(choice => choice != null
                        && string.Equals(choice.choiceId,
                            decision.selectedChoiceId,
                            StringComparison.Ordinal)),
                    $"Decision '{decision.expeditionId}' references unknown choice '{decision.selectedChoiceId}'.");
            }
        }

        foreach (OffenseSupplyPackingItemStateData item in
                 payload.world.supplyPackages.SelectMany(value =>
                     value.costs.Concat(value.returnedCosts)))
        {
            ItemDefinitionId id = new(item.itemId);
            Require(id.IsValid && itemDefinitions.TryGet(id, out _),
                $"Offense supply package references unknown item definition '{item.itemId}'.");
        }

        IEnumerable<string> settlementItemIds = payload.expedition
            .activeExpeditions
            .SelectMany(run => run.consumedSupplies.Select(value =>
                    OffenseSupplyCatalog.GetPhysicalItemId(value.type))
                .Concat(run.ammunitionConsumptions
                    .Select(value => value.itemId)))
            .Concat(payload.expedition.resultHistory
                .SelectMany(result => result.itemReceipts
                    .Select(value => value.itemId)
                    .Concat(result.grantedRewards.SelectMany(reward =>
                        reward.physicalItems.Select(value => value.itemId)))))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal);
        foreach (string itemId in settlementItemIds)
        {
            ItemDefinitionId id = new(itemId);
            Require(id.IsValid && itemDefinitions.TryGet(id, out _),
                $"Expedition settlement references unknown item definition '{itemId}'.");
        }
        foreach (DungeonOffenseExpeditionRunSaveData run in payload.expedition
                     .activeExpeditions.Where(value =>
                         value.worldTarget != null))
        {
            foreach (OffensePhysicalItemRewardSpec reward in
                     run.worldTarget.rewards
                         .Select(value => value?.GrantSpec)
                         .OfType<OffensePhysicalItemRewardSpec>())
            {
                ItemDefinitionId itemId = new(reward.ItemId);
                Require(itemId.IsValid
                        && itemDefinitions.TryGet(itemId, out _),
                    $"Expedition '{run.expeditionId}' references unknown physical reward item '{reward.ItemId}'.");
            }
            if (!string.IsNullOrEmpty(run.worldTarget.authoredEncounterId)
                && encounters != null)
                encounters.Require(run.worldTarget.authoredEncounterId);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

/// <summary>
/// Strict V18 codec for the complete offense aggregate.  It never repairs, clamps,
/// skips, or defaults persisted state.  Canonicalization is a capture concern; a
/// non-canonical restore is rejected before a runtime candidate is constructed.
/// </summary>
public static class OffenseAggregateSaveValidation
{
    private const int MaximumRecordsPerCollection = 10_000;
    private static readonly BindingFlags SerializableFields =
        BindingFlags.Instance | BindingFlags.Public;

    public static OffenseAggregateRestorePlan BuildRestorePlan(
        DungeonOffenseAggregateSaveData source)
    {
        Require(source != null, "Offense aggregate payload is null.");
        Require(source.version == DungeonOffenseAggregateSaveData.CurrentVersion,
            $"Unsupported offense aggregate payload version {source.version}; expected {DungeonOffenseAggregateSaveData.CurrentVersion}.");
        Require(source.campaign != null, "Offense campaign payload is missing.");
        Require(source.expedition != null, "Offense expedition payload is missing.");
        Require(source.world != null, "Offense world payload is missing.");
        Require(source.regions != null, "Offense region payload is missing.");
        Require(source.returnArrivals != null,
            "Offense return-arrival payload is missing.");
        Require(source.campaign.version == DungeonOffenseCampaignSaveData.CurrentVersion,
            $"Unsupported offense campaign payload version {source.campaign.version}; expected {DungeonOffenseCampaignSaveData.CurrentVersion}.");
        Require(source.expedition.version == DungeonOffenseSaveData.CurrentVersion,
            $"Unsupported offense expedition payload version {source.expedition.version}; expected {DungeonOffenseSaveData.CurrentVersion}.");
        Require(source.world.version == OffenseWorldSaveData.CurrentVersion,
            $"Unsupported offense world payload version {source.world.version}; expected {OffenseWorldSaveData.CurrentVersion}.");
        Require(source.regions.version == DungeonOffenseRegionSaveData.CurrentVersion,
            $"Unsupported offense region payload version {source.regions.version}; expected {DungeonOffenseRegionSaveData.CurrentVersion}.");
        Require(source.returnArrivals.version ==
                DungeonOffenseReturnArrivalSaveData.CurrentVersion,
            $"Unsupported offense return-arrival payload version {source.returnArrivals.version}; expected {DungeonOffenseReturnArrivalSaveData.CurrentVersion}.");

        ValidateObjectGraph(source, "offense", new HashSet<object>(
            ReferenceEqualityComparer.Instance));
        ValidateCampaign(source.campaign);
        ValidateExpedition(source.expedition);
        ValidateWorld(source.world);
        ValidateRegions(source.regions);
        ValidateReturnArrivals(source.returnArrivals);
        ValidateCrossModuleLinks(source);

        // JSON cloning detaches every mutable DTO/list from the caller.  Validate the
        // clone too so Unity serialization omissions can never become an implicit
        // defaulting path.
        string json = JsonUtility.ToJson(source);
        Require(!string.IsNullOrWhiteSpace(json),
            "Offense aggregate payload could not be serialized.");
        DungeonOffenseAggregateSaveData detached =
            JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(json);
        Require(detached != null,
            "Offense aggregate payload could not be detached.");
        ValidateObjectGraph(detached, "offense", new HashSet<object>(
            ReferenceEqualityComparer.Instance));
        ValidateCampaign(detached.campaign);
        ValidateExpedition(detached.expedition);
        ValidateWorld(detached.world);
        ValidateRegions(detached.regions);
        ValidateReturnArrivals(detached.returnArrivals);
        ValidateCrossModuleLinks(detached);
        if (!detached.expedition.hasActiveBattle)
        {
            // Unity JsonUtility materializes a default class instance for a serialized
            // null field. The explicit presence bit is the authority; discard only the
            // verified empty placeholder so the restore plan remains canonical.
            detached.expedition.activeBattle = null;
        }
        foreach (DungeonOffenseExpeditionRunSaveData run in
                 detached.expedition.activeExpeditions.Where(run =>
                     !run.usesWorldTravel))
        {
            run.worldTarget = null;
        }
        return new OffenseAggregateRestorePlan(detached);
    }

    private static void ValidateCampaign(DungeonOffenseCampaignSaveData data)
    {
        Require(data.reconLevel >= 0,
            "Offense reconnaissance level cannot be negative.");
        RequireUniqueNonEmpty(data.knownTargetIds, "known target");
        RequireUniqueNonEmpty(data.completedTargetIds, "completed target");
        Require(data.completedTargetIds.All(data.knownTargetIds.Contains),
            "Every completed offense target must also be known.");
        if (!string.IsNullOrWhiteSpace(data.selectedTargetId))
        {
            Require(data.knownTargetIds.Contains(data.selectedTargetId),
                $"Selected offense target '{data.selectedTargetId}' is not known.");
        }
        if (!string.IsNullOrWhiteSpace(data.revealedTruthTargetId))
        {
            Require(data.completedTargetIds.Contains(data.revealedTruthTargetId),
                $"Revealed truth target '{data.revealedTruthTargetId}' is not completed.");
        }
    }

    private static void ValidateExpedition(DungeonOffenseSaveData data)
    {
        Require(data.rewards.moneyEarned >= 0,
            "Offense earned reward money cannot be negative.");
        RequireUnique(data.rewards.stockGranted, value => value.category,
            "reward stock category");
        foreach (DungeonOffenseStockRewardSaveData reward in
                 data.rewards.stockGranted)
        {
            Require(reward.amount > 0,
                "Offense stock reward amounts must be positive.");
        }
        RequireUnique(data.rewards.rareFacilityBuildingIds, value => value,
            "rare facility definition");
        Require(data.rewards.rareFacilityBuildingIds.All(value => value > 0),
            "Rare facility definition IDs must be positive.");
        RequireUnique(data.rewards.acquiredBlueprintIds, value => value,
            "acquired blueprint");
        Require(data.rewards.acquiredBlueprintIds.All(value => value > 0),
            "Acquired blueprint IDs must be positive.");

        RequireUnique(data.activeExpeditions, value => value.expeditionId,
            "active expedition");
        foreach (DungeonOffenseExpeditionRunSaveData run in
                 data.activeExpeditions)
        {
            ValidateActiveExpedition(run);
        }

        RequireUnique(data.resultHistory, value => value.expeditionId,
            "expedition result");
        foreach (DungeonOffenseExpeditionResultSaveData result in
                 data.resultHistory)
        {
            RequireId(result.expeditionId, "expedition result ID");
            RequireId(result.targetId, "expedition result target ID");
            Require(result.totalPower >= 0f
                    && result.requiredPower >= 0f
                    && result.danger >= 0f
                    && result.elapsedSeconds >= 0f,
                $"Expedition result '{result.expeditionId}' has negative values.");
            Require(result.members.All(value =>
                    value.power >= 0f && value.damageTaken >= 0f),
                $"Expedition result '{result.expeditionId}' has invalid member values.");
            Require(result.grantedRewards != null
                    && result.itemReceipts != null
                    && result.treatmentReceipts != null
                    && result.arrivalReceipts != null
                    && result.currencyReceipts != null,
                $"Expedition result '{result.expeditionId}' is missing current settlement lists.");
            foreach (DungeonOffenseRewardGrantSaveData reward in result.grantedRewards)
            {
                Require(reward != null
                        && Enum.IsDefined(typeof(OffenseRewardCategory), reward.category)
                        && reward.requestedAmount >= 0
                        && reward.grantedAmount >= 0
                        && reward.grantedAmount <= reward.requestedAmount
                        && reward.label != null
                        && reward.detail != null
                        && reward.physicalItems != null,
                    $"Expedition result '{result.expeditionId}' has an invalid reward grant receipt.");
                RequireUnique(reward.physicalItems, value => value.itemId,
                    $"expedition result '{result.expeditionId}' reward physical item");
                Require(reward.physicalItems.All(value => value != null
                        && !string.IsNullOrWhiteSpace(value.itemId)
                        && value.quantity > 0)
                    && (reward.success || reward.physicalItems.Count == 0),
                    $"Expedition result '{result.expeditionId}' has an invalid physical reward receipt.");
            }
            foreach (DungeonOffenseItemReceiptSaveData receipt in result.itemReceipts)
            {
                ValidateItemReceipt(receipt, result.expeditionId);
            }
            RequireUnique(result.itemReceipts, ItemReceiptCompositeKey,
                $"expedition result '{result.expeditionId}' item receipt");
            Dictionary<string, int> expectedRewardItems = result.grantedRewards
                .Where(value => value.success)
                .SelectMany(value => value.physicalItems)
                .GroupBy(value => value.itemId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(value => value.quantity),
                    StringComparer.Ordinal);
            Dictionary<string, int> actualRewardItems = result.itemReceipts
                .Where(value => value.kind
                    == OffenseExpeditionItemReceiptKind.RewardGranted)
                .ToDictionary(
                    value => value.itemId,
                    value => value.quantity,
                    StringComparer.Ordinal);
            Require(expectedRewardItems.Count == actualRewardItems.Count
                    && expectedRewardItems.All(pair =>
                        actualRewardItems.TryGetValue(pair.Key, out int amount)
                        && amount == pair.Value),
                $"Expedition result '{result.expeditionId}' physical rewards do not match their successful grants.");
            ValidateTreatments(result.treatmentReceipts, result.expeditionId);
            RequireUnique(result.arrivalReceipts, value => value.arrivalId,
                $"expedition result '{result.expeditionId}' arrival receipt");
            foreach (DungeonOffenseArrivalReceiptSaveData arrival in result.arrivalReceipts)
            {
                Require(arrival != null
                        && !string.IsNullOrWhiteSpace(arrival.arrivalId)
                        && !string.IsNullOrWhiteSpace(arrival.kind)
                        && Enum.IsDefined(
                            typeof(OffenseExpeditionArrivalResolution),
                            arrival.resolution)
                        && arrival.requestedAmount > 0
                        && arrival.materializedAmount >= 0
                        && arrival.materializedAmount <= arrival.requestedAmount
                        && arrival.securedAmount >= 0
                        && arrival.escapedAmount >= 0
                        && arrival.securedAmount + arrival.escapedAmount
                            <= arrival.materializedAmount,
                    $"Expedition result '{result.expeditionId}' has an invalid return-arrival receipt.");
                Require(arrival.resolution switch
                    {
                        OffenseExpeditionArrivalResolution.Pending => true,
                        OffenseExpeditionArrivalResolution.Secured =>
                            arrival.securedAmount == arrival.requestedAmount
                            && arrival.escapedAmount == 0,
                        OffenseExpeditionArrivalResolution.Escaped =>
                            arrival.escapedAmount > 0
                            && arrival.securedAmount + arrival.escapedAmount
                                == arrival.requestedAmount,
                        _ => false
                    },
                    $"Expedition result '{result.expeditionId}' has inconsistent arrival resolution counts.");
            }
            RequireUnique(result.currencyReceipts, value => value.operationId,
                $"expedition result '{result.expeditionId}' currency receipt");
            Require(result.currencyReceipts.All(value => value != null
                    && string.Equals(
                        value.currencyId,
                        OffenseSettlementCurrencyIds.Gold,
                        StringComparison.Ordinal)
                    && value.amount > 0
                    && !string.IsNullOrWhiteSpace(value.operationId)),
                $"Expedition result '{result.expeditionId}' has an invalid currency receipt.");
        }

        if (!data.hasActiveBattle)
        {
            Require(!HasBattlePayload(data.activeBattle),
                "Offense payload contains hidden battle state while hasActiveBattle is false.");
        }
        else
        {
            Require(data.activeBattle != null,
                "Offense payload marks an active battle but has no battle state.");
            ValidatePersistentBattle(data.activeBattle);
            DungeonOffenseExpeditionRunSaveData run = data.activeExpeditions
                .SingleOrDefault(candidate => string.Equals(
                    candidate.expeditionId,
                    data.activeBattle.expeditionId,
                    StringComparison.Ordinal));
            Require(run != null,
                $"Offense battle '{data.activeBattle.battleId}' has no active expedition.");
            Require(run.phase == OffenseExpeditionPhase.InBattle,
                $"Offense battle expedition '{run.expeditionId}' is not in battle phase.");
            Require(string.Equals(run.targetId, data.activeBattle.targetId,
                    StringComparison.Ordinal),
                $"Offense battle '{data.activeBattle.battleId}' target does not match its expedition.");
        }
    }

    private static void ValidateActiveExpedition(
        DungeonOffenseExpeditionRunSaveData run)
    {
        Require(run.journeyVersion == DungeonOffenseExpeditionRunSaveData.CurrentVersion,
            $"Expedition '{run.expeditionId}' has unsupported journey version {run.journeyVersion}; expected {DungeonOffenseExpeditionRunSaveData.CurrentVersion}.");
        RequireId(run.expeditionId, "expedition ID");
        RequireId(run.targetId, $"expedition '{run.expeditionId}' target ID");
        Require(run.totalPower >= 0f && run.remainingSeconds >= 0f,
            $"Expedition '{run.expeditionId}' has negative power or time.");
        Require(run.memberPersistentIds.Count is >= 1 and <= 5,
            $"Expedition '{run.expeditionId}' must contain one to five members.");
        RequireUniqueNonEmpty(run.memberPersistentIds,
            $"expedition '{run.expeditionId}' member");
        Require(run.returnProgress != null && run.returnMessage != null,
            "Expedition return state is missing.");
        Require(run.returnProgress.All(value => value != null
                && Enum.IsDefined(typeof(ExpeditionReturnStage), value.stage)),
            "Expedition return has an invalid stage.");
        RequireUnique(run.returnProgress, value => value.characterId, "expedition return member");
        var returnMembers = run.memberPersistentIds.Concat(run.protectedRescueMemberPersistentIds)
            .OrderBy(value => value, StringComparer.Ordinal);
        Require(run.returnPending
                ? run.returnProgress.Select(value => value.characterId).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(returnMembers)
                : run.returnProgress.Count == 0 && !run.returnSuccess && run.returnMessage.Length == 0,
            "Expedition return progress does not exactly match its pending party.");
        RequireUniqueNonEmpty(run.protectedRescueMemberPersistentIds,
            $"expedition '{run.expeditionId}' protected rescue member");
        Require(!run.memberPersistentIds.Intersect(
                run.protectedRescueMemberPersistentIds,
                StringComparer.Ordinal).Any(),
            $"Expedition '{run.expeditionId}' contains a member in both party lists.");
        RequireUnique(run.memberStates, value => value.persistentId,
            $"expedition '{run.expeditionId}' member state");
        Require(run.memberStates.Select(value => value.persistentId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(run.memberPersistentIds
                    .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            $"Expedition '{run.expeditionId}' member-state IDs do not exactly match its party.");
        Require(run.memberStates.All(value =>
                value.stress is >= 0f and <= 100f
                && value.totalDamageTaken >= 0f),
            $"Expedition '{run.expeditionId}' has invalid member stress or damage state.");
        RequireUniqueNonEmpty(run.completedNodeIds,
            $"expedition '{run.expeditionId}' completed route node");
        RequireUnique(run.supplies, value => value.type,
            $"expedition '{run.expeditionId}' supply type");
        Require(run.supplies.All(value => value.amount > 0),
            $"Expedition '{run.expeditionId}' has a non-positive supply amount.");
        Require(run.consumedSupplies != null
                && run.treatmentReceipts != null
                && run.equipmentBaselines != null
                && run.ammunitionConsumptions != null
                && run.returnResourceFailure != null
                && run.returnItemReceipts != null
                && run.returnCurrencyReceipts != null,
            $"Expedition '{run.expeditionId}' is missing current settlement receipt state.");
        RequireUnique(run.consumedSupplies, value => value.type,
            $"expedition '{run.expeditionId}' consumed supply type");
        Require(run.consumedSupplies.All(value => value != null
                && Enum.IsDefined(typeof(OffenseSupplyType), value.type)
                && value.amount > 0),
            $"Expedition '{run.expeditionId}' has an invalid consumed supply receipt.");
        ValidateTreatments(run.treatmentReceipts, run.expeditionId);
        HashSet<string> participantIds = run.memberPersistentIds
            .Concat(run.protectedRescueMemberPersistentIds)
            .ToHashSet(StringComparer.Ordinal);
        Require(run.treatmentReceipts.All(value =>
                participantIds.Contains(value.characterId)),
            $"Expedition '{run.expeditionId}' has a treatment receipt for a non-participant.");
        RequireUnique(run.equipmentBaselines, value => value.instanceId,
            $"expedition '{run.expeditionId}' equipment baseline");
        Require(run.equipmentBaselines.All(value => value != null
                && !string.IsNullOrWhiteSpace(value.instanceId)
                && !string.IsNullOrWhiteSpace(value.definitionId)
                && value.durabilityRatio is >= 0f and <= 1f),
            $"Expedition '{run.expeditionId}' has an invalid equipment baseline.");
        RequireUnique(run.ammunitionConsumptions,
            value => (value.instanceId, value.itemId),
            $"expedition '{run.expeditionId}' ammunition consumption");
        HashSet<string> baselineIds = run.equipmentBaselines
            .Select(value => value.instanceId)
            .ToHashSet(StringComparer.Ordinal);
        Require(run.ammunitionConsumptions.All(value => value != null
                && !string.IsNullOrWhiteSpace(value.instanceId)
                && !string.IsNullOrWhiteSpace(value.itemId)
                && value.quantity > 0
                && baselineIds.Contains(value.instanceId)),
            $"Expedition '{run.expeditionId}' has invalid committed ammunition consumption.");
        foreach (DungeonOffenseItemReceiptSaveData receipt in
                 run.returnItemReceipts)
        {
            ValidateItemReceipt(receipt, run.expeditionId);
            Require(receipt.kind is
                    OffenseExpeditionItemReceiptKind.SupplyReturned
                    or OffenseExpeditionItemReceiptKind.LootRecovered
                    or OffenseExpeditionItemReceiptKind.EquipmentRecovered,
                $"Expedition '{run.expeditionId}' contains a non-return resource receipt in its retry state.");
        }
        RequireUnique(run.returnItemReceipts, ItemReceiptCompositeKey,
            $"expedition '{run.expeditionId}' committed return item receipt");
        RequireUnique(run.returnCurrencyReceipts,
            value => value.operationId,
            $"expedition '{run.expeditionId}' committed return currency receipt");
        Require(run.returnCurrencyReceipts.All(value => value != null
                && string.Equals(
                    value.currencyId,
                    OffenseSettlementCurrencyIds.Gold,
                    StringComparison.Ordinal)
                && value.amount > 0
                && string.Equals(
                    value.operationId,
                    run.expeditionId,
                    StringComparison.Ordinal)),
            $"Expedition '{run.expeditionId}' has an invalid committed return currency receipt.");
        Require(run.returnPending
                || !run.returnResourcesCommitted
                    && run.returnResourceFailure.Length == 0
                    && run.returnItemReceipts.Count == 0
                    && run.returnCurrencyReceipts.Count == 0,
            $"Expedition '{run.expeditionId}' retains return settlement state before return begins.");
        Require(!run.returnResourcesCommitted
                || run.returnResourceFailure.Length == 0,
            $"Expedition '{run.expeditionId}' marks both committed and failed return resources.");
        Require(run.returnCurrencyReceipts.Count == 0
                || run.fieldFundsReturned && run.fieldFunds == 0,
            $"Expedition '{run.expeditionId}' currency receipt is torn from its returned field funds.");
        RequireUnique(run.carriedStock, value => value.category,
            $"expedition '{run.expeditionId}' carried stock category");
        Require(run.carriedStock.All(value => value.amount > 0),
            $"Expedition '{run.expeditionId}' has a non-positive carried stock amount.");
        RequireUniqueNonEmpty(
            run.recoveredEquipmentInstanceIds,
            $"expedition '{run.expeditionId}' recovered equipment instance");
        Require(run.supplyCapacity >= 0
                && run.startingLight is >= 0f and <= 100f
                && run.light is >= 0f and <= 100f
                && run.campHealRatio is >= 0f and <= 1f
                && run.campStressRecovery >= 0f
                && run.medicineHealRatio is >= 0f and <= 1f
                && run.scouting >= 0
                && run.fieldFunds >= 0,
            $"Expedition '{run.expeditionId}' has invalid preparation values.");
        if (run.usesWorldTravel)
        {
            RequireId(run.worldSiteId,
                $"strategic expedition '{run.expeditionId}' world site ID");
            Require(run.worldTarget != null && run.worldTarget.IsValid,
                $"Strategic expedition '{run.expeditionId}' has no valid authored target snapshot.");
            if (!string.IsNullOrEmpty(
                    run.worldTarget.seasonalOccurrenceInstanceId))
            {
                Require(Canonical(
                            run.worldTarget.seasonalOccurrenceInstanceId)
                        && Canonical(run.worldTarget.authoredEncounterId)
                        && Canonical(run.worldTarget.encounterPreviewText)
                        && Canonical(
                            run.worldTarget.encounterRewardPreviewText)
                        && run.worldTarget.rewards != null
                        && run.worldTarget.rewards.Length > 0
                        && run.worldTarget.rewards.All(value =>
                            value?.GrantSpec is
                                OffensePhysicalItemRewardSpec physical
                            && Canonical(physical.ItemId)
                            && value.amount > 0),
                    $"Seasonal expedition '{run.expeditionId}' has an invalid frozen encounter or reward.");
            }
        }
        else
        {
            Require(string.IsNullOrEmpty(run.worldSiteId)
                    && !HasWorldTargetPayload(run.worldTarget)
                    && !run.worldObjectiveCompleted
                    && !run.worldObjectiveBattleActive,
                $"Campaign expedition '{run.expeditionId}' contains strategic-world state.");
        }
    }

    private static void ValidateTreatments(
        IEnumerable<DungeonOffenseTreatmentReceiptSaveData> treatments,
        string expeditionId)
    {
        foreach (DungeonOffenseTreatmentReceiptSaveData treatment in treatments)
        {
            Require(treatment != null
                    && Enum.IsDefined(
                        typeof(OffenseExpeditionTreatmentKind),
                        treatment.kind)
                    && !string.IsNullOrWhiteSpace(treatment.characterId)
                    && treatment.healedAmount >= 0f
                    && (treatment.kind == OffenseExpeditionTreatmentKind.Healing
                        ? treatment.healedAmount > 0f
                            && string.IsNullOrEmpty(treatment.anatomyNodeId)
                        : treatment.healedAmount == 0f
                            && !string.IsNullOrWhiteSpace(treatment.anatomyNodeId)),
                $"Expedition '{expeditionId}' has an invalid treatment receipt.");
        }
    }

    private static void ValidateItemReceipt(
        DungeonOffenseItemReceiptSaveData receipt,
        string expeditionId)
    {
        Require(receipt != null
                && Enum.IsDefined(
                    typeof(OffenseExpeditionItemReceiptKind),
                    receipt.kind)
                && !string.IsNullOrWhiteSpace(receipt.itemId)
                && string.Equals(
                    receipt.itemId,
                    receipt.itemId.Trim(),
                    StringComparison.Ordinal)
                && receipt.quantity > 0
                && receipt.durabilityLoss is >= 0f and <= 1f
                && receipt.valuation != null,
            $"Expedition result '{expeditionId}' has an invalid item receipt.");
        bool validShape = receipt.kind switch
        {
            OffenseExpeditionItemReceiptKind.SupplyConsumed
                or OffenseExpeditionItemReceiptKind.SupplyReturned
                or OffenseExpeditionItemReceiptKind.LootRecovered
                or OffenseExpeditionItemReceiptKind.RewardGranted =>
                string.IsNullOrEmpty(receipt.instanceId)
                && receipt.durabilityLoss == 0f,
            OffenseExpeditionItemReceiptKind.AmmunitionConsumed =>
                !string.IsNullOrWhiteSpace(receipt.instanceId)
                && string.Equals(
                    receipt.instanceId,
                    receipt.instanceId.Trim(),
                    StringComparison.Ordinal)
                && receipt.durabilityLoss == 0f,
            OffenseExpeditionItemReceiptKind.EquipmentWorn =>
                receipt.quantity == 1
                && !string.IsNullOrWhiteSpace(receipt.instanceId)
                && string.Equals(
                    receipt.instanceId,
                    receipt.instanceId.Trim(),
                    StringComparison.Ordinal)
                && receipt.durabilityLoss > 0f,
            OffenseExpeditionItemReceiptKind.EquipmentLost =>
                receipt.quantity == 1
                && !string.IsNullOrWhiteSpace(receipt.instanceId)
                && string.Equals(
                    receipt.instanceId,
                    receipt.instanceId.Trim(),
                    StringComparison.Ordinal),
            OffenseExpeditionItemReceiptKind.EquipmentRecovered =>
                receipt.quantity == 1
                && !string.IsNullOrWhiteSpace(receipt.instanceId)
                && string.Equals(
                    receipt.instanceId,
                    receipt.instanceId.Trim(),
                    StringComparison.Ordinal)
                && receipt.durabilityLoss == 0f,
            _ => false
        };
        Require(validShape,
            $"Expedition result '{expeditionId}' item receipt '{receipt.kind}' has an invalid kind-specific shape.");
        DungeonOffenseItemValuationSaveData valuation = receipt.valuation;
        Require(string.Equals(
                    valuation.itemId,
                    receipt.itemId,
                    StringComparison.Ordinal)
                && valuation.quantity == receipt.quantity
                && Enum.IsDefined(
                    typeof(OffenseSettlementValuationState),
                    valuation.state)
                && valuation.acquisitionMilliEwuPerUnit >= 0L
                && valuation.recoverableMilliEwuPerUnit >= 0L
                && valuation.recoverableMilliEwuPerUnit
                    <= valuation.acquisitionMilliEwuPerUnit,
            $"Expedition result '{expeditionId}' has an invalid item valuation receipt.");
        Require(valuation.state == OffenseSettlementValuationState.Valued
                ? !string.IsNullOrWhiteSpace(valuation.basisId)
                    && !string.IsNullOrWhiteSpace(valuation.selectedSourceId)
                : valuation.acquisitionMilliEwuPerUnit == 0L
                    && valuation.recoverableMilliEwuPerUnit == 0L
                    && string.IsNullOrEmpty(valuation.basisId)
                    && string.IsNullOrEmpty(valuation.selectedSourceId),
            $"Expedition result '{expeditionId}' has inconsistent valuation authority fields.");
    }

    private static (
        OffenseExpeditionItemReceiptKind kind,
        string itemId,
        string instanceId) ItemReceiptCompositeKey(
        DungeonOffenseItemReceiptSaveData receipt)
    {
        string instance = receipt.kind is
                OffenseExpeditionItemReceiptKind.AmmunitionConsumed
                or OffenseExpeditionItemReceiptKind.EquipmentWorn
                or OffenseExpeditionItemReceiptKind.EquipmentLost
                or OffenseExpeditionItemReceiptKind.EquipmentRecovered
            ? receipt.instanceId
            : string.Empty;
        return (receipt.kind, receipt.itemId, instance);
    }

    private static void ValidatePersistentBattle(
        OffenseBattlePersistenceState battle)
    {
        RequireId(battle.battleId, "offense battle ID");
        RequireId(battle.expeditionId,
            $"offense battle '{battle.battleId}' expedition ID");
        RequireId(battle.targetId,
            $"offense battle '{battle.battleId}' target ID");
        RequireId(battle.encounterId,
            $"offense battle '{battle.battleId}' encounter ID");
        RequireUnique(
            battle.enemyIndividuals,
            value => value.characterId,
            $"offense battle '{battle.battleId}' enemy individual");
        HashSet<string> enemyCombatantIds = battle.combatants
            .Where(value => (battle.enemyIndividuals ?? new List<EnemyIndividualSaveData>())
                .Any(individual => string.Equals(
                    individual.characterId,
                    value.persistentId,
                    StringComparison.Ordinal)))
            .Select(value => value.persistentId)
            .ToHashSet(StringComparer.Ordinal);
        Require(enemyCombatantIds.Count == battle.enemyIndividuals.Count,
            $"Offense battle '{battle.battleId}' enemy individuals do not exactly match enemy combatants.");
        Require(battle.roundNumber >= 1
                && battle.currentOrderIndex >= 0
                && battle.currentOrderIndex < battle.initiativeOrder.Count
                && battle.lastProcessedCommandId >= 0,
            $"Offense battle '{battle.battleId}' has invalid turn state.");
        RequireUniqueNonEmpty(battle.initiativeOrder,
            $"offense battle '{battle.battleId}' initiative combatant");
        RequireUnique(battle.combatants, value => value.persistentId,
            $"offense battle '{battle.battleId}' combatant");
        Require(battle.initiativeOrder.Count == battle.combatants.Count
                && battle.initiativeOrder.All(id => battle.combatants.Any(value =>
                    string.Equals(value.persistentId, id,
                        StringComparison.Ordinal))),
            $"Offense battle '{battle.battleId}' initiative does not exactly match its combatants.");
        RequireUnique(battle.thrownEquipment, value => value.instanceId,
            $"offense battle '{battle.battleId}' thrown equipment");
        foreach (OffenseThrownEquipmentPersistenceState thrown in
                 battle.thrownEquipment)
        {
            RequireId(thrown.ownerCharacterId,
                $"thrown equipment '{thrown.instanceId}' owner ID");
        }
        foreach (OffenseBattleCombatantPersistenceState combatant in
                 battle.combatants)
        {
            Require(combatant.maxHealth > 0f
                    && combatant.attack >= 0f
                    && combatant.strength >= 0f
                    && combatant.toughness >= 0f
                    && combatant.dexterity >= 0f
                    && combatant.moveSpeed >= 0f
                    && combatant.shooting >= 0f
                    && combatant.evasion >= 0f
                    && combatant.currentHealth is >= 0f
                        && combatant.currentHealth <= combatant.maxHealth
                    && combatant.totalDamageTaken >= 0f
                    && combatant.initiativePenalty >= 0f
                    && combatant.coverBlockChance is >= 0f and <= 1f
                    && combatant.turnsStarted >= 0
                    && combatant.suppression >= 0f
                    && combatant.bloodLoss >= 0f,
                $"Battle combatant '{combatant.persistentId}' has invalid persisted stats.");
            RequireUnique(combatant.bodyParts, value => value.bodyPart,
                $"battle combatant '{combatant.persistentId}' body part");
            Require(combatant.bodyParts.All(value =>
                    value.maxHealth > 0f
                    && value.currentHealth is >= 0f
                        && value.currentHealth <= value.maxHealth
                    && value.bleedingPerSecond >= 0f),
                $"Battle combatant '{combatant.persistentId}' has invalid body-part health.");
            RequireUnique(combatant.cooldowns, value => value.abilityId,
                $"battle combatant '{combatant.persistentId}' cooldown");
            Require(combatant.cooldowns.All(value => value.remainingTurns > 0),
                $"Battle combatant '{combatant.persistentId}' has a non-positive cooldown.");
            RequireUnique(combatant.statuses, value => value.id,
                $"battle combatant '{combatant.persistentId}' status");
            Require(combatant.statuses.All(value => value.remainingTurns > 0),
                $"Battle combatant '{combatant.persistentId}' has a non-positive status duration.");
        }
    }

    private static void ValidateWorld(OffenseWorldSaveData data)
    {
        Require(data.worldSeed != 0
                && data.worldDay >= 1
                && data.worldHour is >= 0f and < 24f,
            "Offense world date/time is outside its canonical range.");
        RequireUnique(data.tiles, value => $"{value.q}:{value.r}",
            "offense world tile coordinate");
        RequireUnique(data.sites, value => value.siteId, "offense world site");
        foreach (OffenseWorldSiteStateData site in data.sites)
            ValidateSeasonalOffer(site);
        RequireUnique(data.urgentSites, value => value.siteId,
            "offense urgent site");
        HashSet<string> expeditionIds = RequireUnique(
            data.travelStates,
            value => value.expeditionId,
            "offense travel state");
        RequireUnique(data.returnSafety, value => value.expeditionId,
            "offense return-safety state");
        RequireUnique(data.decisions, value => value.expeditionId,
            "offense decision state");
        Require(data.battles.Count <= 1,
            "Only one strategic command battle may be active.");
        RequireUnique(data.mitigationOrders, value => value.orderId,
            "offense mitigation order");
        RequireUnique(data.supplyPackages, value => value.packageId,
            "offense supply package");
        RequireUnique(data.fieldStabilizations,
            value => $"{value.expeditionId}:{value.characterId}:{value.anatomyNodeId}",
            "offense field stabilization");
        RequireUnique(data.casualtyCarries,
            value => $"{value.expeditionId}:{value.casualtyCharacterId}",
            "offense casualty carry");
        RequireUnique(data.strandedExpeditions, value => value.expeditionId,
            "offense stranded expedition");
        RequireUnique(data.rescueConvoys, value => value.rescueExpeditionId,
            "offense rescue convoy");

        Dictionary<string, OffenseHexTileState> tilesByCoordinate = data.tiles
            .ToDictionary(value => $"{value.q}:{value.r}",
                StringComparer.Ordinal);

        foreach (OffenseTravelStateData travel in data.travelStates)
        {
            Require(travel.progressToNextTile >= 0f
                    && travel.exposure is >= 0f and <= 100f
                    && travel.eventSequence >= 0
                    && travel.routeRoadMultiplier >= 0.1f
                    && travel.routeWeatherMultiplier >= 0.1f
                    && travel.routeLoadMultiplier >= 0.1f
                    && travel.movementTimeMultiplier is >= 1f and <= 2.5f
                    && travel.milestoneTimeMultiplier is >= 0.1f and <= 1f
                    && travel.facilityTimeMultiplier is > 0f and <= 1f
                    && travel.activeSegmentWeatherMultiplier >= 0f
                    && travel.activeSegmentTraversalCost >= 0f
                    && travel.activeSegmentDurationSeconds >= 0f,
                $"Travel state '{travel.expeditionId}' has invalid progress values.");
            if (!string.IsNullOrEmpty(travel.destinationSiteId))
            {
                Require(data.sites.Any(value => string.Equals(
                        value.siteId,
                        travel.destinationSiteId,
                        StringComparison.Ordinal))
                    || data.urgentSites.Any(value => string.Equals(
                        value.siteId,
                        travel.destinationSiteId,
                        StringComparison.Ordinal)),
                    $"Travel state '{travel.expeditionId}' references missing site '{travel.destinationSiteId}'.");
            }

            string currentCoordinate = $"{travel.currentQ}:{travel.currentR}";
            string destinationCoordinate =
                $"{travel.destinationQ}:{travel.destinationR}";
            Require(tilesByCoordinate.ContainsKey(currentCoordinate)
                    && tilesByCoordinate.ContainsKey(destinationCoordinate),
                $"Travel state '{travel.expeditionId}' references a missing current or destination tile.");
            Require(travel.remainingPath.All(value => value != null),
                $"Travel state '{travel.expeditionId}' contains a null path coordinate.");
            OffenseHexCoord previous = travel.CurrentCoord;
            foreach (OffenseHexCoordSaveData pathCoordinate in
                     travel.remainingPath)
            {
                string coordinate = $"{pathCoordinate.q}:{pathCoordinate.r}";
                Require(tilesByCoordinate.TryGetValue(coordinate,
                        out OffenseHexTileState tile)
                        && !tile.blocked,
                    $"Travel state '{travel.expeditionId}' contains a missing or blocked path tile '{coordinate}'.");
                OffenseHexCoord next = pathCoordinate.ToCoord();
                Require(previous.DistanceTo(next) == 1,
                    $"Travel state '{travel.expeditionId}' contains a non-adjacent path segment.");
                previous = next;
            }
            Require(travel.remainingPath.Count == 0
                    || previous == travel.DestinationCoord,
                $"Travel state '{travel.expeditionId}' path does not end at its destination.");

            bool hasActiveSegment = travel.activeSegmentDurationSeconds > 0f;
            if (!hasActiveSegment)
            {
                Require(travel.activeSegmentTraversalCost == 0f
                        && travel.progressToNextTile == 0f
                        && travel.ActiveSegmentCoord == travel.CurrentCoord
                        && travel.movementTimeMultiplier == 1f
                        && travel.milestoneTimeMultiplier == 1f
                        && travel.facilityTimeMultiplier == 1f
                        && string.IsNullOrEmpty(
                            travel.activeSegmentWeatherFrontId)
                        && IsLegacyNeutralWeatherMultiplier(
                            travel.activeSegmentWeatherMultiplier),
                    $"Travel state '{travel.expeditionId}' has a partial inactive segment.");
                continue;
            }

            Require(travel.remainingPath.Count > 0
                    && travel.ActiveSegmentCoord
                        == travel.remainingPath[0].ToCoord()
                    && travel.activeSegmentTraversalCost
                        >= OffenseTraversalCostRules.MinimumStepCost
                    && travel.progressToNextTile
                        <= travel.activeSegmentDurationSeconds + 0.0001f,
                $"Travel state '{travel.expeditionId}' has an inconsistent active segment.");
            Require(
                string.IsNullOrEmpty(travel.activeSegmentWeatherFrontId)
                    ? IsLegacyNeutralWeatherMultiplier(
                        travel.activeSegmentWeatherMultiplier)
                    : string.Equals(
                            travel.activeSegmentWeatherFrontId,
                            travel.activeSegmentWeatherFrontId.Trim(),
                            StringComparison.Ordinal)
                        && travel.activeSegmentWeatherFrontId.StartsWith(
                            "weather:",
                            StringComparison.Ordinal)
                        && travel.activeSegmentWeatherMultiplier >= 0.1f,
                $"Travel state '{travel.expeditionId}' has invalid active-segment weather provenance.");
            float expectedDuration = 2.5f
                * travel.activeSegmentTraversalCost
                * travel.movementTimeMultiplier
                * travel.milestoneTimeMultiplier
                * travel.facilityTimeMultiplier;
            Require(Mathf.Abs(
                    expectedDuration - travel.activeSegmentDurationSeconds)
                    <= 0.0001f,
                $"Travel state '{travel.expeditionId}' has a torn active-segment duration.");
        }
        foreach (OffenseReturnSafetyStateData safety in data.returnSafety)
        {
            Require(safety.safeStepBudget >= 0
                    && safety.protectedForcedCombatCount >= 0
                    && safety.nonCombatPitySteps >= 0,
                $"Return-safety state '{safety.expeditionId}' has negative counters.");
            Require(expeditionIds.Contains(safety.expeditionId),
                $"Return-safety state '{safety.expeditionId}' has no travel state.");
        }
        foreach (OffenseDecisionStateData decision in data.decisions)
        {
            RequireId(decision.cardId,
                $"decision '{decision.expeditionId}' card ID");
            Require(decision.sequence >= 0,
                $"Decision '{decision.expeditionId}' has a negative sequence.");
            Require(expeditionIds.Contains(decision.expeditionId),
                $"Decision '{decision.expeditionId}' has no travel state.");
            Require(decision.resolved
                    ? !string.IsNullOrWhiteSpace(decision.selectedChoiceId)
                    : string.IsNullOrEmpty(decision.selectedChoiceId),
                $"Decision '{decision.expeditionId}' has inconsistent resolution state.");
        }
        foreach (OffenseSupplyPackingStateData package in data.supplyPackages)
        {
            ValidateSupplyPackage(package);
        }

        foreach (OffenseBattleDirectorStateData battle in data.battles)
        {
            RequireId(battle.battleId, "strategic battle ID");
            Require(battle.decks.Count is >= 1 and <= 5,
                $"Strategic battle '{battle.battleId}' must contain one to five command decks.");
            foreach (OffenseCommandDeckStateData deck in battle.decks)
            {
                RequireId(deck.characterId,
                    $"strategic battle '{battle.battleId}' deck character ID");
                foreach (OffenseCommandCardStateData card in
                         (deck.drawPile ?? new List<OffenseCommandCardStateData>())
                         .Concat(deck.discardPile
                             ?? new List<OffenseCommandCardStateData>())
                         .Concat(deck.candidates
                             ?? new List<OffenseCommandCardStateData>()))
                {
                    Require(card != null
                            && Enum.IsDefined(
                                typeof(OffenseBattleActionType),
                                card.actionType),
                        $"Strategic battle '{battle.battleId}' has an invalid card action type.");
                }
            }
            foreach (OffenseEnemyIntentStateData intent in
                     battle.enemyIntents
                     ?? new List<OffenseEnemyIntentStateData>())
            {
                Require(intent != null
                        && Enum.IsDefined(
                            typeof(OffenseBattleActionType),
                            intent.actionType),
                    $"Strategic battle '{battle.battleId}' has an invalid enemy action type.");
            }
        }

        HashSet<string> mitigationSiteIds = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (OffenseUrgentMitigationOrderStateData order in
                 data.mitigationOrders)
        {
            RequireId(order.siteId,
                $"mitigation order '{order.orderId}' site ID");
            RequireId(order.definitionId,
                $"mitigation order '{order.orderId}' definition ID");
            RequireId(order.destinationId,
                $"mitigation order '{order.orderId}' destination ID");
            Require(string.Equals(
                    order.destinationId,
                    OffenseUrgentMitigationInputOwnerAuthority
                        .BuildDestinationId(order.orderId),
                    StringComparison.Ordinal),
                $"Mitigation order '{order.orderId}' has a non-canonical exact input destination.");
            Require(mitigationSiteIds.Add(order.siteId),
                $"More than one mitigation order targets site '{order.siteId}'.");
            Require(order.requiredWork > 0f
                    && order.completedWork >= 0f
                    && order.completedWork <= order.requiredWork,
                $"Mitigation order '{order.orderId}' has invalid work progress.");
            bool hasFacility = !string.IsNullOrEmpty(
                order.facilityPersistentId);
            bool hasProjection = order.inputBufferCapacityGrams > 0L
                && order.inputMassAuthorityRevision > 0L
                && !string.IsNullOrEmpty(order.inputCapacityFingerprint)
                && order.inputCapacityFingerprint.Length == 64;
            Require(hasFacility == hasProjection,
                $"Mitigation order '{order.orderId}' has a torn facility/input-capacity owner join.");
            if (!hasFacility)
            {
                Require(order.status
                        == OffenseUrgentMitigationOrderStatus.WaitingForFacility
                        && OffenseUrgentMitigationInputOwnerAuthority
                            .StoredProjectionIsEmpty(order),
                    $"Mitigation order '{order.orderId}' has invalid unbound input ownership.");
            }
            ValidateMitigationPhysicalState(order);
        }

        foreach (FieldStabilizationState stabilization in
                 data.fieldStabilizations)
        {
            RequireId(stabilization.expeditionId,
                "field stabilization expedition ID");
            RequireId(stabilization.characterId,
                "field stabilization character ID");
            RequireId(stabilization.anatomyNodeId,
                "field stabilization anatomy-node ID");
            RequireId(stabilization.consumedKitInstanceId,
                "field stabilization consumed-kit ID");
            Require(InRange(stabilization.locomotionFloor, 0f, 1f)
                    && InRange(stabilization.sustainFloor, 0f, 1f)
                    && stabilization.appliedEventSequence >= 0,
                $"Field stabilization for '{stabilization.characterId}' has invalid persisted values.");
        }
        foreach (OffenseCasualtyCarryState carry in data.casualtyCarries)
        {
            RequireId(carry.expeditionId, "casualty carry expedition ID");
            RequireId(carry.casualtyCharacterId,
                "casualty carry casualty ID");
            RequireId(carry.carrierCharacterId,
                "casualty carry carrier ID");
            Require(!string.Equals(carry.casualtyCharacterId,
                        carry.carrierCharacterId,
                        StringComparison.Ordinal)
                    && carry.casualtyBodyWeight >= 0f
                    && carry.casualtyEquipmentWeight >= 0f,
                $"Casualty carry for '{carry.casualtyCharacterId}' is invalid.");
        }
        foreach (OffenseStrandedState stranded in data.strandedExpeditions)
        {
            RequireId(stranded.expeditionId,
                "stranded expedition ID");
            Require(stranded.remainingSupply >= 0f
                    && stranded.estimatedSurvivalHours >= 0f,
                $"Stranded expedition '{stranded.expeditionId}' has invalid survival values.");
        }
        foreach (RescueConvoyState convoy in data.rescueConvoys)
        {
            RequireId(convoy.rescueExpeditionId,
                "rescue convoy expedition ID");
            RequireId(convoy.strandedExpeditionId,
                "rescue convoy stranded-expedition ID");
            RequireUniqueNonEmpty(convoy.rescuerCharacterIds,
                $"rescue convoy '{convoy.rescueExpeditionId}' rescuer");
            RequireUniqueNonEmpty(convoy.protectedCasualtyIds,
                $"rescue convoy '{convoy.rescueExpeditionId}' protected casualty");
        }
    }

    private static bool IsLegacyNeutralWeatherMultiplier(float value) =>
        value == 0f || Mathf.Abs(value - 1f) <= 0.0001f;

    private static void ValidateMitigationPhysicalState(
        OffenseUrgentMitigationOrderStateData order)
    {
        string label = $"mitigation order '{order.orderId}'";
        OffenseUrgentMitigationCommitPhase phase =
            (OffenseUrgentMitigationCommitPhase)order.physicalCommitPhase;
        Require(Enum.IsDefined(typeof(OffenseUrgentMitigationCommitPhase), phase),
            $"{label} has an unknown physical commit phase.");
        if (phase == OffenseUrgentMitigationCommitPhase.None)
        {
            Require(string.IsNullOrEmpty(order.physicalOperationId)
                    && string.IsNullOrEmpty(order.physicalCommitId)
                    && order.inputQuantity == 0
                    && order.inputMassGrams == 0L
                    && !order.physicalReceiptAcknowledged
                    && order.mitigationBefore == 0f
                    && order.mitigationAfter == 0f,
                $"{label} has orphan physical provenance.");
            return;
        }

        string operation =
            OffenseUrgentMitigationRuntime.FormatPhysicalOperationId(
                order.orderId);
        string commit =
            $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Transfer}:{operation}:{order.inputQuantity}:{order.inputMassGrams}";
        Require(string.Equals(
                    order.physicalOperationId,
                    operation,
                    StringComparison.Ordinal)
                && string.Equals(
                    order.physicalCommitId,
                    commit,
                    StringComparison.Ordinal)
                && order.inputQuantity > 0
                && order.inputMassGrams > 0L
                && order.completedWork + 0.001f >= order.requiredWork
                && InRange(order.mitigationBefore, 0f, 0.6f)
                && InRange(order.mitigationAfter, 0f, 0.6f)
                && order.mitigationAfter > order.mitigationBefore
                && (phase != OffenseUrgentMitigationCommitPhase.MaterialsCommitted
                    || !order.physicalReceiptAcknowledged),
            $"{label} physical commit provenance is invalid.");
    }

    private static void ValidateSupplyPackage(
        OffenseSupplyPackingStateData package)
    {
        string label = $"supply package '{package.packageId}'";
        RequireId(package.destinationId, label + " destination ID");
        RequireUnique(package.costs, value => value.itemId, label + " item");
        Require(package.costs.Count > 0
                && package.costs.All(value => value.amount > 0),
            $"{label} must contain positive item costs.");
        long requiredLong = package.costs.Sum(value => (long)value.amount);
        Require(requiredLong is > 0 and <= int.MaxValue,
            $"{label} item quantity is outside the supported range.");
        int required = (int)requiredLong;
        OffenseSupplyCustodyPhase phase =
            (OffenseSupplyCustodyPhase)package.custodyPhase;
        Require(Enum.IsDefined(typeof(OffenseSupplyCustodyPhase), phase)
                && package.consumed
                    == (phase != OffenseSupplyCustodyPhase.Staging),
            $"{label} has an invalid custody phase.");

        bool emptyCustody = string.IsNullOrEmpty(package.custodyOperationId)
            && string.IsNullOrEmpty(package.custodyReasonCode)
            && string.IsNullOrEmpty(package.custodyCommitId)
            && package.custodySourceStackIds.Count == 0
            && package.custodyQuantity == 0
            && package.custodyMassGrams == 0L
            && !package.custodyAcknowledged;
        bool emptyReturn = string.IsNullOrEmpty(package.returnOperationId)
            && string.IsNullOrEmpty(package.returnReasonCode)
            && package.returnX == 0
            && package.returnY == 0
            && package.returnOutputCommitIds.Count == 0
            && package.returnQuantity == 0
            && package.returnMassGrams == 0L
            && package.consumedOrLostMassGrams == 0L
            && package.returnedCosts.Count == 0;
        if (phase == OffenseSupplyCustodyPhase.Staging)
        {
            Require(emptyCustody && emptyReturn,
                $"{label} staging state contains custody provenance.");
            return;
        }

        string custodyOperation =
            DungeonOffensePreparationService.FormatCustodyOperationId(
                package.packageId);
        Require(string.Equals(
                    package.custodyOperationId,
                    custodyOperation,
                    StringComparison.Ordinal)
                && string.Equals(
                    package.custodyReasonCode,
                    "offense-expedition-supply-custody-transfer",
                    StringComparison.Ordinal)
                && package.custodyQuantity == required
                && package.custodyMassGrams > 0L,
            $"{label} has invalid custody identity or mass.");
        string expectedCommit =
            $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Transfer}:{custodyOperation}:{required}:{package.custodyMassGrams}";
        Require(string.Equals(
                package.custodyCommitId,
                expectedCommit,
                StringComparison.Ordinal),
            $"{label} custody commit is not exact.");
        RequireUniqueNonEmpty(
            package.custodySourceStackIds,
            label + " custody source stack");
        Require(package.custodySourceStackIds.SequenceEqual(
                package.custodySourceStackIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal),
            $"{label} custody source stack IDs are not ordinal sorted.");

        if (phase == OffenseSupplyCustodyPhase.CustodyOwned)
        {
            Require(emptyReturn,
                $"{label} owned state contains return provenance.");
            return;
        }
        Require(package.custodyAcknowledged,
            $"{label} terminal or returning custody is not acknowledged.");
        if (phase == OffenseSupplyCustodyPhase.Lost)
        {
            Require(string.IsNullOrEmpty(package.returnOperationId)
                    && string.IsNullOrEmpty(package.returnReasonCode)
                    && package.returnX == 0
                    && package.returnY == 0
                    && package.returnedCosts.Count == 0
                    && package.returnOutputCommitIds.Count == 0
                    && package.returnQuantity == 0
                    && package.returnMassGrams == 0L
                    && package.consumedOrLostMassGrams
                        == package.custodyMassGrams,
                $"{label} lost state does not close its physical mass.");
            return;
        }

        Require(string.Equals(
                    package.returnOperationId,
                    DungeonOffensePreparationService.FormatReturnOperationId(
                        package.packageId),
                    StringComparison.Ordinal)
                && string.Equals(
                    package.returnReasonCode,
                    "offense-expedition-supply-return",
                    StringComparison.Ordinal),
            $"{label} return identity is invalid.");
        RequireUnique(
            package.returnedCosts,
            value => value.itemId,
            label + " returned item");
        Dictionary<string, int> owned = package.costs.ToDictionary(
            value => value.itemId,
            value => value.amount,
            StringComparer.Ordinal);
        Require(package.returnedCosts.All(value => value.amount > 0
                && owned.TryGetValue(value.itemId, out int count)
                && value.amount <= count),
            $"{label} attempts to return unowned physical stock.");
        if (phase == OffenseSupplyCustodyPhase.ReturnPublishing)
        {
            Require(package.returnOutputCommitIds.Count == 0
                    && package.returnQuantity == 0
                    && package.returnMassGrams == 0L
                    && package.consumedOrLostMassGrams == 0L,
                $"{label} pending return contains terminal output provenance.");
            return;
        }

        long returnQuantity =
            package.returnedCosts.Sum(value => (long)value.amount);
        Require(returnQuantity <= int.MaxValue
                && package.returnQuantity == (int)returnQuantity
                && package.returnMassGrams >= 0L
                && package.consumedOrLostMassGrams >= 0L
                && checked(package.returnMassGrams
                    + package.consumedOrLostMassGrams)
                    == package.custodyMassGrams,
            $"{label} returned state does not close its physical mass.");
        if (returnQuantity > 0)
        {
            RequireUniqueNonEmpty(
                package.returnOutputCommitIds,
                label + " return output commit");
        }
        Require(package.returnOutputCommitIds.SequenceEqual(
                package.returnOutputCommitIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal)
                && (returnQuantity == 0
                    ? package.returnOutputCommitIds.Count == 0
                        && package.returnMassGrams == 0L
                    : package.returnOutputCommitIds.Count
                        == package.returnedCosts.Count),
            $"{label} return output provenance is not canonical.");
    }

    private static void ValidateRegions(DungeonOffenseRegionSaveData data)
    {
        RequireUnique(data.regions, value => value.regionId, "offense region");
        foreach (OffenseRegionState region in data.regions)
        {
            RequireId(region.displayName,
                $"offense region '{region.regionId}' display name");
            RequireId(region.factionId,
                $"offense region '{region.regionId}' faction ID");
            Require(InRange(region.logisticsDamage, 0f, 100f)
                    && InRange(region.armamentDamage, 0f, 100f)
                    && InRange(region.manpowerDamage, 0f, 100f)
                    && InRange(region.intelligenceDamage, 0f, 100f),
                $"Offense region '{region.regionId}' pressure is outside 0..100.");
            string awardOperation =
                region.memoryErasureSealAwardOperationId?.Trim()
                ?? string.Empty;
            Require(!region.memoryErasureSealAwardPublished
                    || awardOperation.Length > 0,
                $"Offense region '{region.regionId}' published a memory-erasure seal without an award operation.");
            Require(awardOperation.Length == 0
                    || string.Equals(
                        region.memoryErasureSealAwardOperationId,
                        awardOperation,
                        StringComparison.Ordinal)
                    && string.Equals(
                        awardOperation,
                        MemoryErasureSealBossAwardRules.BuildOperationId(
                            region.regionId),
                        StringComparison.Ordinal),
                $"Offense region '{region.regionId}' has a non-canonical memory-erasure seal award operation.");
        }
        string[] requiredRegionIds =
        {
            OffenseRegionRuntime.BorderTradeRegionId,
            OffenseRegionRuntime.RivalOutpostRegionId,
            OffenseRegionRuntime.SealedZoneRegionId
        };
        Require(requiredRegionIds.All(id => data.regions.Any(region =>
                string.Equals(region.regionId, id, StringComparison.Ordinal))),
            "Offense region payload is missing a required authored region.");
    }

    private static void ValidateReturnArrivals(
        DungeonOffenseReturnArrivalSaveData data)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        OffenseReturnArrivalSaveValidation.Validate(data, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(string.Join(" | ", report.Errors));
        }
    }

    private static void ValidateCrossModuleLinks(
        DungeonOffenseAggregateSaveData data)
    {
        HashSet<string> runIds = data.expedition.activeExpeditions
            .Select(value => value.expeditionId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> knownExpeditionIds = runIds
            .Concat(data.expedition.resultHistory.Select(value =>
                value.expeditionId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (string id in data.world.travelStates
                     .Select(value => value.expeditionId)
                     .Concat(data.world.returnSafety.Select(value => value.expeditionId))
                     .Concat(data.world.decisions.Select(value => value.expeditionId))
                     .Concat(data.world.fieldStabilizations.Select(value => value.expeditionId))
                     .Concat(data.world.casualtyCarries.Select(value => value.expeditionId))
                     .Concat(data.world.strandedExpeditions.Select(value => value.expeditionId)))
        {
            Require(runIds.Contains(id),
                $"Offense world module references missing active expedition '{id}'.");
        }

        HashSet<string> regionIds = data.regions.regions
            .Select(value => value.regionId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> worldSiteIds = data.world.sites
            .Select(value => value.siteId)
            .Concat(data.world.urgentSites.Select(value => value.siteId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (DungeonOffenseExpeditionRunSaveData run in
                 data.expedition.activeExpeditions.Where(value =>
                     value.usesWorldTravel))
        {
            Require(worldSiteIds.Contains(run.worldSiteId),
                $"Strategic expedition '{run.expeditionId}' references missing site '{run.worldSiteId}'.");
            Require(regionIds.Contains(run.worldTarget.regionId),
                $"Strategic expedition '{run.expeditionId}' references missing region '{run.worldTarget.regionId}'.");
            if (!string.IsNullOrEmpty(
                    run.worldTarget.seasonalOccurrenceInstanceId))
            {
                OffenseWorldSiteStateData site = data.world.sites.Single(
                    value => string.Equals(
                        value.siteId,
                        run.worldSiteId,
                        StringComparison.Ordinal));
                Require(site.state is OffenseWorldSiteState.Engaged
                            or OffenseWorldSiteState.Resolved
                        && string.Equals(
                            site.seasonalOffer.occurrenceInstanceId,
                            run.worldTarget.seasonalOccurrenceInstanceId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            site.seasonalOffer.authoredEncounterId,
                            run.worldTarget.authoredEncounterId,
                            StringComparison.Ordinal)
                        && SeasonalTargetMatchesOffer(
                            site,
                            run.worldTarget),
                    $"Seasonal expedition '{run.expeditionId}' does not join its engaged occurrence site.");
            }
        }
        foreach (OffenseWorldSiteStateData site in data.world.sites.Where(
                     value => value?.seasonalOffer?.IsConfigured == true
                         && value.state is OffenseWorldSiteState.Engaged
                             or OffenseWorldSiteState.Resolved))
        {
            int joinedRuns = data.expedition.activeExpeditions.Count(run =>
                run?.usesWorldTravel == true
                && string.Equals(
                    run.worldSiteId,
                    site.siteId,
                    StringComparison.Ordinal)
                && string.Equals(
                    run.worldTarget?.seasonalOccurrenceInstanceId,
                    site.seasonalOffer.occurrenceInstanceId,
                    StringComparison.Ordinal));
            Require(site.state == OffenseWorldSiteState.Engaged
                    ? joinedRuns == 1
                    : joinedRuns <= 1,
                $"Seasonal offer '{site.siteId}' has an invalid active-expedition join count {joinedRuns}.");
        }
        foreach (OffenseHexTileState tile in data.world.tiles)
        {
            Require(regionIds.Contains(tile.regionId),
                $"Offense tile ({tile.q},{tile.r}) references missing region '{tile.regionId}'.");
        }
        foreach (OffenseWorldSiteStateData site in data.world.sites)
        {
            Require(regionIds.Contains(site.regionId),
                $"Offense site '{site.siteId}' references missing region '{site.regionId}'.");
            Require(data.world.tiles.Any(tile => tile.q == site.q && tile.r == site.r),
                $"Offense site '{site.siteId}' is not on a saved world tile.");
        }
        foreach (OffenseUrgentSiteStateData site in data.world.urgentSites)
        {
            Require(data.world.tiles.Any(tile => tile.q == site.q && tile.r == site.r),
                $"Offense urgent site '{site.siteId}' is not on a saved world tile.");
        }
        foreach (OffenseUrgentMitigationOrderStateData order in
                 data.world.mitigationOrders)
        {
            Require(data.world.urgentSites.Any(site => string.Equals(
                    site.siteId,
                    order.siteId,
                    StringComparison.Ordinal)),
                $"Mitigation order '{order.orderId}' references missing urgent site '{order.siteId}'.");
        }
        HashSet<string> strandedIds = data.world.strandedExpeditions
            .Select(value => value.expeditionId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (RescueConvoyState convoy in data.world.rescueConvoys)
        {
            Require(runIds.Contains(convoy.rescueExpeditionId),
                $"Rescue convoy references missing rescue expedition '{convoy.rescueExpeditionId}'.");
            Require(strandedIds.Contains(convoy.strandedExpeditionId),
                $"Rescue convoy '{convoy.rescueExpeditionId}' references missing stranded expedition '{convoy.strandedExpeditionId}'.");
        }

        HashSet<string> knownTargetIds = data.campaign.knownTargetIds
            .Concat(data.expedition.activeExpeditions.Select(value =>
                value.targetId))
            .Concat(data.expedition.resultHistory.Select(value =>
                value.targetId))
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> arrivalOwners = data.returnArrivals.arrivals
            .ToDictionary(
                value => value.arrivalId,
                value => value.expeditionId,
                StringComparer.Ordinal);
        Dictionary<string, int> arrivalReceiptOccurrences = data.expedition
            .resultHistory
            .SelectMany(value => value.arrivalReceipts)
            .GroupBy(value => value.arrivalId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);
        foreach (OffenseReturnArrivalState arrival in data.returnArrivals.arrivals)
        {
            Require(knownExpeditionIds.Contains(arrival.expeditionId),
                $"Return arrival '{arrival.arrivalId}' references missing expedition '{arrival.expeditionId}'.");
            Require(knownTargetIds.Contains(arrival.targetId),
                $"Return arrival '{arrival.arrivalId}' references missing target '{arrival.targetId}'.");
            DungeonOffenseExpeditionResultSaveData result = data.expedition
                .resultHistory.SingleOrDefault(value => string.Equals(
                    value.expeditionId,
                    arrival.expeditionId,
                    StringComparison.Ordinal));
            Require(result != null,
                $"Return arrival '{arrival.arrivalId}' has no committed expedition result.");
            Require(arrivalReceiptOccurrences.TryGetValue(
                        arrival.arrivalId,
                        out int receiptOccurrence)
                    && receiptOccurrence == 1,
                $"Return arrival '{arrival.arrivalId}' must have exactly one globally owned result receipt.");
            DungeonOffenseArrivalReceiptSaveData receipt = result
                .arrivalReceipts.SingleOrDefault(value => string.Equals(
                    value.arrivalId,
                    arrival.arrivalId,
                    StringComparison.Ordinal));
            Require(receipt != null
                    && string.Equals(
                        receipt.kind,
                        arrival.kind.ToString(),
                        StringComparison.Ordinal)
                    && receipt.requestedAmount == arrival.requestedAmount,
                $"Return arrival '{arrival.arrivalId}' does not match its committed result receipt.");
            OffenseExpeditionArrivalResolution expectedResolution =
                arrival.stage switch
                {
                    OffenseReturnArrivalStage.Secured =>
                        OffenseExpeditionArrivalResolution.Secured,
                    OffenseReturnArrivalStage.Escaped =>
                        OffenseExpeditionArrivalResolution.Escaped,
                    _ => OffenseExpeditionArrivalResolution.Pending
                };
            Require(receipt.resolution == expectedResolution,
                $"Return arrival '{arrival.arrivalId}' terminal state is torn from its committed result.");
            if (expectedResolution != OffenseExpeditionArrivalResolution.Pending)
            {
                Require(receipt.materializedAmount
                            == arrival.settledMaterializedAmount
                        && receipt.securedAmount
                            == arrival.settledSecuredAmount
                        && receipt.escapedAmount
                            == arrival.settledEscapedAmount,
                    $"Return arrival '{arrival.arrivalId}' terminal counts are torn from its committed result.");
            }
        }
        foreach (DungeonOffenseExpeditionResultSaveData result in data.expedition
                     .resultHistory)
        {
            foreach (DungeonOffenseArrivalReceiptSaveData receipt in
                     result.arrivalReceipts)
            {
                Require(arrivalOwners.TryGetValue(
                            receipt.arrivalId,
                            out string ownerExpeditionId)
                        && string.Equals(
                            ownerExpeditionId,
                            result.expeditionId,
                            StringComparison.Ordinal)
                        && arrivalReceiptOccurrences.TryGetValue(
                            receipt.arrivalId,
                            out int receiptOccurrence)
                        && receiptOccurrence == 1,
                    $"Expedition result '{result.expeditionId}' contains an orphan, foreign-owned, or duplicate return-arrival receipt '{receipt.arrivalId}'.");
            }
        }
        foreach (OffensePrisonerCandidatePoolState pool in
            data.returnArrivals.prisonerCandidatePools)
        {
            Require(knownExpeditionIds.Contains(pool.expeditionId),
                $"Prisoner candidate pool references missing expedition '{pool.expeditionId}'.");
        }
    }

    private static void ValidateObjectGraph(
        object value,
        string path,
        ISet<object> visited)
    {
        Require(value != null, $"{path} is null.");
        Type type = value.GetType();
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum
            || type == typeof(decimal))
        {
            ValidateScalar(value, type, path);
            return;
        }
        if (!type.IsValueType && !visited.Add(value))
        {
            return;
        }
        if (value is IEnumerable enumerable)
        {
            int count = 0;
            foreach (object entry in enumerable)
            {
                Require(count++ < MaximumRecordsPerCollection,
                    $"{path} exceeds {MaximumRecordsPerCollection} records.");
                ValidateObjectGraph(entry, $"{path}[{count - 1}]", visited);
            }
            return;
        }

        foreach (FieldInfo field in type.GetFields(SerializableFields))
        {
            if (field.IsStatic || field.IsNotSerialized)
            {
                continue;
            }
            object fieldValue = field.GetValue(value);
            // Explicitly optional save fields.
            if (((type == typeof(DungeonOffenseSaveData)
                        && field.Name == nameof(DungeonOffenseSaveData.activeBattle)
                        && !((DungeonOffenseSaveData)value).hasActiveBattle)
                    || (type == typeof(DungeonOffenseExpeditionRunSaveData)
                        && field.Name == nameof(DungeonOffenseExpeditionRunSaveData.worldTarget)
                        && !((DungeonOffenseExpeditionRunSaveData)value).usesWorldTravel)))
            {
                continue;
            }
            ValidateObjectGraph(fieldValue, $"{path}.{field.Name}", visited);
        }
    }

    private static bool HasBattlePayload(OffenseBattlePersistenceState battle)
    {
        return battle != null
            && (!string.IsNullOrEmpty(battle.battleId)
                || !string.IsNullOrEmpty(battle.expeditionId)
                || !string.IsNullOrEmpty(battle.targetId)
                || !string.IsNullOrEmpty(battle.targetTitle)
                || !string.IsNullOrEmpty(battle.encounterId)
                || (battle.enemyIndividuals?.Count ?? 0) != 0
                || battle.difficulty != DungeonDifficulty.Normal
                || battle.outcome != OffenseBattleOutcome.InProgress
                || battle.roundNumber != 1
                || battle.currentOrderIndex != 0
                || battle.lastProcessedCommandId != 0
                || (battle.initiativeOrder?.Count ?? 0) != 0
                || (battle.log?.Count ?? 0) != 0
                || (battle.thrownEquipment?.Count ?? 0) != 0
                || (battle.combatants?.Count ?? 0) != 0);
    }

    private static bool HasWorldTargetPayload(OffenseTargetDefinition target)
    {
        return target != null
            && (!string.IsNullOrEmpty(target.id)
                || !string.IsNullOrEmpty(target.title)
                || !string.IsNullOrEmpty(target.description)
                || target.kind != default
                || !string.IsNullOrEmpty(target.regionId)
                || !string.IsNullOrEmpty(target.regionDisplayName)
                || !string.IsNullOrEmpty(target.factionId)
                || target.strategicPressureAxis != default
                || target.strategicPressureAmount != 15f
                || target.campaignOrder != 1
                || !string.IsNullOrEmpty(target.prerequisiteTargetId)
                || target.revealsTruth
                || !string.IsNullOrEmpty(target.truthText)
                || target.distance != 0f
                || target.danger != 0f
                || target.durationSeconds != 90f
                || target.requiredMembers != 1
                || target.requiredPower != 0f
                || !string.IsNullOrEmpty(target.seasonalOccurrenceInstanceId)
                || !string.IsNullOrEmpty(target.authoredEncounterId)
                || !string.IsNullOrEmpty(target.encounterPreviewText)
                || !string.IsNullOrEmpty(target.encounterRewardPreviewText)
                || (target.rewards?.Length ?? 0) != 0);
    }

    private static void ValidateSeasonalOffer(OffenseWorldSiteStateData site)
    {
        OffenseSeasonalExpeditionOfferData offer = site?.seasonalOffer;
        if (offer?.configured != true)
        {
            Require(offer != null
                    && string.IsNullOrEmpty(offer.occurrenceInstanceId)
                    && string.IsNullOrEmpty(offer.definitionId)
                    && offer.offerDeadlineAbsoluteDay == 0
                    && string.IsNullOrEmpty(offer.description)
                    && offer.recommendedDanger == 0f
                    && offer.durationSeconds == 0f
                    && offer.requiredMembers == 0
                    && offer.recommendedPower == 0f
                    && offer.campaignOrder == 0
                    && string.IsNullOrEmpty(offer.authoredEncounterId)
                    && string.IsNullOrEmpty(offer.encounterPreviewText)
                    && string.IsNullOrEmpty(offer.encounterRewardPreviewText)
                    && (offer.physicalRewards?.Count ?? 0) == 0,
                $"Offense site '{site?.siteId}' has partial seasonal-offer state.");
            return;
        }
        Require(offer.IsConfigured
                && Canonical(offer.occurrenceInstanceId)
                && Canonical(offer.definitionId)
                && offer.offerDeadlineAbsoluteDay >= site.createdDay
                && Canonical(offer.description)
                && offer.recommendedDanger >= 0f
                && offer.durationSeconds > 0f
                && offer.requiredMembers is >= 1 and <= 5
                && offer.recommendedPower >= 0f
                && offer.campaignOrder is >= 1 and <= 6
                && Canonical(offer.authoredEncounterId)
                && Canonical(offer.encounterPreviewText)
                && Canonical(offer.encounterRewardPreviewText)
                && offer.physicalRewards != null
                && offer.physicalRewards.Count > 0
                && offer.physicalRewards.All(value => value != null
                    && Canonical(value.itemId)
                    && Canonical(value.displayLabel)
                    && value.exactQuantity > 0)
                && offer.physicalRewards.Select(value => value.itemId)
                    .Distinct(StringComparer.Ordinal).Count()
                    == offer.physicalRewards.Count
                && !site.fixedBoss
                && site.state != OffenseWorldSiteState.Hidden,
            $"Offense site '{site?.siteId}' has invalid seasonal-offer state.");
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool SeasonalTargetMatchesOffer(
        OffenseWorldSiteStateData site,
        OffenseTargetDefinition target)
    {
        OffenseSeasonalExpeditionOfferData offer = site?.seasonalOffer;
        if (offer?.IsConfigured != true || target == null)
            return false;
        OffenseRewardPreview[] rewards = target.rewards
            ?? Array.Empty<OffenseRewardPreview>();
        if ((offer.physicalRewards?.Count ?? 0) != rewards.Length)
            return false;
        for (int index = 0; index < rewards.Length; index++)
        {
            OffenseSeasonalPhysicalRewardData offered =
                offer.physicalRewards[index];
            OffenseRewardPreview frozen = rewards[index];
            if (offered == null
                || frozen?.GrantSpec is not
                    OffensePhysicalItemRewardSpec physical
                || offered.itemId != physical.ItemId
                || offered.displayLabel != frozen.label
                || offered.exactQuantity != frozen.amount)
                return false;
        }
        return target.id == site.siteId
            && target.title == site.displayName
            && target.description == offer.description
            && target.regionId == site.regionId
            && target.factionId == site.factionId
            && target.campaignOrder == offer.campaignOrder
            && target.danger == offer.recommendedDanger
            && target.durationSeconds == offer.durationSeconds
            && target.requiredMembers == offer.requiredMembers
            && target.requiredPower == offer.recommendedPower
            && target.encounterPreviewText == offer.encounterPreviewText
            && target.encounterRewardPreviewText
                == offer.encounterRewardPreviewText;
    }

    private static void ValidateScalar(object value, Type type, string path)
    {
        if (type == typeof(float))
        {
            float number = (float)value;
            Require(!float.IsNaN(number) && !float.IsInfinity(number),
                $"{path} is not finite.");
        }
        else if (type == typeof(double))
        {
            double number = (double)value;
            Require(!double.IsNaN(number) && !double.IsInfinity(number),
                $"{path} is not finite.");
        }
        else if (type.IsEnum)
        {
            Require(Enum.IsDefined(type, value),
                $"{path} contains unknown {type.Name} value '{value}'.");
        }
    }

    private static HashSet<TKey> RequireUnique<T, TKey>(
        IEnumerable<T> values,
        Func<T, TKey> keySelector,
        string label)
    {
        HashSet<TKey> keys = new HashSet<TKey>();
        foreach (T value in values)
        {
            TKey key = keySelector(value);
            if (key is string text)
            {
                RequireId(text, label + " ID");
            }
            Require(keys.Add(key), $"Duplicate {label} '{key}'.");
        }
        return keys;
    }

    private static void RequireUniqueNonEmpty(
        IEnumerable<string> values,
        string label)
    {
        RequireUnique(values, value => value, label);
    }

    private static void RequireId(string value, string label)
    {
        Require(!string.IsNullOrWhiteSpace(value)
                && string.Equals(value, value.Trim(), StringComparison.Ordinal),
            $"{label} is empty or non-canonical.");
    }

    private static bool InRange(float value, float minimum, float maximum) =>
        value >= minimum && value <= maximum;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object value) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[Serializable]
public sealed class DungeonOffenseAggregateSaveData
{
    public const int CurrentVersion = 3;

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

    public OffenseAggregateAuthoredReferenceValidator(
        IOffenseContentCatalog content,
        IItemDefinitionCatalog itemDefinitions,
        IOffenseCampaignCatalog campaigns)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.itemDefinitions = itemDefinitions
            ?? throw new ArgumentNullException(nameof(itemDefinitions));
        this.campaigns = campaigns
            ?? throw new ArgumentNullException(nameof(campaigns));
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
                    && travel.movementTimeMultiplier >= 1f,
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
            foreach (OffenseHexCoordSaveData pathCoordinate in
                     travel.remainingPath)
            {
                string coordinate = $"{pathCoordinate.q}:{pathCoordinate.r}";
                Require(tilesByCoordinate.TryGetValue(coordinate,
                        out OffenseHexTileState tile)
                        && !tile.blocked,
                    $"Travel state '{travel.expeditionId}' contains a missing or blocked path tile '{coordinate}'.");
            }
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
            Require(mitigationSiteIds.Add(order.siteId),
                $"More than one mitigation order targets site '{order.siteId}'.");
            Require(order.requiredWork > 0f
                    && order.completedWork >= 0f
                    && order.completedWork <= order.requiredWork,
                $"Mitigation order '{order.orderId}' has invalid work progress.");
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
        foreach (OffenseReturnArrivalState arrival in data.returnArrivals.arrivals)
        {
            Require(knownExpeditionIds.Contains(arrival.expeditionId),
                $"Return arrival '{arrival.arrivalId}' references missing expedition '{arrival.expeditionId}'.");
            Require(knownTargetIds.Contains(arrival.targetId),
                $"Return arrival '{arrival.arrivalId}' references missing target '{arrival.targetId}'.");
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
                || (target.rewards?.Length ?? 0) != 0);
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

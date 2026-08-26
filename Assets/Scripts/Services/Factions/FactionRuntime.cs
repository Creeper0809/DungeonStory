using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using UnityEngine;
using VContainer.Unity;

public sealed class FactionRuntimeApplicationAdapter :
    IFactionRuntime,
    IStartable,
    ITickable,
    IDisposable
{
    private const float SecondsPerHex = 20f;
    private readonly ResourceDungeonFactionCatalogApplicationAdapter catalog;
    private readonly FactionDomainRuntime domain;
    private readonly IOffenseWorldSimulation world;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IRunCharacterCatalog characterCatalog;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterFactory;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private readonly IFactionCampaignQuery campaignQuery;
    private readonly IFactionCampaignCommand campaignCommand;
    [ApplicationAdapterTransientState]
    private IDisposable daySubscription;
    [ApplicationAdapterTransientState]
    private bool synchronizingWorldHomes;
    [ApplicationAdapterTransientState]
    private int projectedRestoreRevision;

    private IEnumerable<DungeonFactionState> factions => domain.FactionStates;
    private IReadOnlyList<FactionRouteState> routes => domain.Routes;
    private int currentDay => domain.CurrentDay;

    public FactionRuntimeApplicationAdapter(
        ResourceDungeonFactionCatalogApplicationAdapter catalog,
        IOffenseWorldSimulation world,
        FactionItemLogisticsDependencies itemLogistics,
        FactionCharacterSpawnDependencies characterSpawning,
        IGameClock clock,
        IGameEventBus events,
        V20CampaignRuntime campaign,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        itemLogistics = itemLogistics
            ?? throw new ArgumentNullException(nameof(itemLogistics));
        characterSpawning = characterSpawning
            ?? throw new ArgumentNullException(nameof(characterSpawning));
        itemSpawner = itemLogistics.ItemSpawner;
        itemRuntime = itemLogistics.ItemRuntime;
        batchDispositions = itemLogistics.BatchDispositions;
        dropZones = itemLogistics.DropZones;
        characterCatalog = characterSpawning.CharacterCatalog;
        spawnerProvider = characterSpawning.SpawnerProvider;
        characterFactory = characterSpawning.CharacterFactory;
        worldRegistry = characterSpawning.WorldRegistry;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        campaignQuery = campaign ?? throw new ArgumentNullException(nameof(campaign));
        campaignCommand = campaign;
        domain = new FactionDomainRuntime(
            aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
    }

    public IReadOnlyList<FactionDefinitionSnapshot> Definitions =>
        catalog.Definitions;
    public IReadOnlyList<DungeonFactionState> Factions =>
        catalog.Definitions
            .Select(definition => ProjectCampaignRelationship(
                factions.FirstOrDefault(value => string.Equals(
                    value.factionId,
                    definition.StableId,
                    StringComparison.Ordinal))))
            .Where(value => value != null)
            .ToArray();
    public IReadOnlyList<FactionRouteState> Routes => routes;

    public void Start()
    {
        EnsureInitialized();
        world.Changed += OnWorldChanged;
        projectedRestoreRevision = domain.PublishedRestoreRevision;
        SynchronizeWorldHomes();
        daySubscription = events.Subscribe<OperatingDayStartedEvent>(
            value => domain.SetCurrentDay(value.day));
    }

    public void Dispose()
    {
        world.Changed -= OnWorldChanged;
        daySubscription?.Dispose();
        daySubscription = null;
    }

    public void Tick()
    {
        EnsureWorldHomesProjectionCurrent();
        foreach (FactionRouteState arrived in routes.Where(value =>
                     value != null
                     && value.kind == FactionRouteKind.Reinforcement
                     && value.status == FactionRouteStatus.Arrived
                     && !value.actorsSpawned))
        {
            MaterializeReinforcements(arrived);
        }

        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        foreach (FactionRouteState route in domain.AdvanceRoutes(
                     clock.DeltaTime,
                     SecondsPerHex))
        {
            CompleteRoute(route);
        }
    }

    public bool TryGetFaction(
        string factionId,
        out DungeonFactionState faction)
    {
        EnsureInitialized();
        bool found = domain.TryGetFaction(factionId, out faction);
        if (found)
            ProjectCampaignRelationship(faction);
        return found;
    }

    public bool IsContractUnlocked(
        string factionId,
        FactionContractKind contract)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction)
            || faction.NegotiationBlocked(currentDay)
            || !campaignQuery.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData relationship))
            return false;

        return contract switch
        {
            FactionContractKind.Trade => relationship.rapport >= 20
                && relationship.grievance <= 70,
            FactionContractKind.Recruitment => relationship.rapport >= 35
                && relationship.grievance <= 55,
            FactionContractKind.Supply => relationship.rapport >= 50
                && relationship.grievance <= 40,
            FactionContractKind.Reinforcement => relationship.rapport >= 70
                && relationship.grievance <= 25
                && relationship.obligationTokens > 0
                && faction.allianceProjectCompleted,
            _ => false
        };
    }

    public bool TryAdjustTrust(
        string factionId,
        int amount,
        string reason,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            message = "세력을 찾을 수 없습니다.";
            return false;
        }

        if (amount > 0 && faction.NegotiationBlocked(currentDay))
        {
            message = $"배신 후 협상 봉쇄가 Day {faction.negotiationBlockedUntilDay}까지 유지됩니다.";
            return false;
        }

        int previous = faction.trust;
        int adjusted = amount > 0
            ? Math.Max(1, (int)MathF.Round(
                amount * MathF.Pow(0.85f, faction.betrayalScars)))
            : amount;
        campaignCommand.ApplyFactionChange(
            factionId,
            adjusted,
            amount < 0 ? Math.Max(1, -amount / 2) : 0,
            0);
        ProjectCampaignRelationship(faction);
        events.Publish(new FactionTrustChangedEvent(
            faction.factionId,
            previous,
            faction.trust,
            reason));
        message =
            $"{DisplayName(factionId)} 우호 {previous} → {faction.trust}";
        return true;
    }

    public bool TryOfferGoodwill(
        string factionId,
        int physicalValue,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            message = "호의를 보낼 세력을 찾을 수 없습니다.";
            return false;
        }

        if (FactionGoodwillOutbox.HasProvenance(faction))
        {
            int transferredValue = faction.goodwillTransferredPhysicalValue;
            if (!FactionGoodwillOutbox.TryFinalizePending(
                    faction,
                    batchDispositions,
                    campaignQuery,
                    campaignCommand,
                    domain.AcceptGoodwill,
                    out _,
                    out string replayFailure))
            {
                throw new InvalidOperationException(
                    $"Faction goodwill could not reconcile its physical transfer: {replayFailure}");
            }
            FactionGoodwillOutbox.ClearCompleted(faction);
            ProjectCampaignRelationship(faction);
            message =
                $"{DisplayName(factionId)} 호의 물자 {transferredValue} 전달 완료";
            return true;
        }

        if (faction.NegotiationBlocked(currentDay))
        {
            message =
                $"배신 후 협상 봉쇄가 Day {faction.negotiationBlockedUntilDay}까지 유지됩니다.";
            return false;
        }

        int offered = Mathf.Max(0, physicalValue);
        if (offered < 50
            || !TrySelectPhysicalGoods(
                offered,
                out PhysicalItemTransformInput[] inputs,
                out int consumedValue))
        {
            message = "예약되지 않은 실물 물자 가치 50 이상이 필요합니다.";
            return false;
        }

        if (!campaignQuery.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData campaignState)
            || campaignState == null)
        {
            throw new InvalidOperationException(
                $"Faction goodwill campaign authority '{factionId}' is missing.");
        }
        int previousRapport = campaignState.rapport;
        int rawGain = Mathf.Clamp(consumedValue / 10, 1, 10);
        int adjustedGain = Math.Max(1, (int)MathF.Round(
            rawGain * MathF.Pow(0.85f, faction.betrayalScars)));
        int rapportTarget = Math.Clamp(
            previousRapport + adjustedGain,
            -100,
            100);
        int sequence = domain.AllocateGoodwillOperationSequence();
        string operationId = FactionGoodwillOutbox.FormatOperationId(
            factionId,
            sequence);
        if (!batchDispositions.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                FactionGoodwillOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string dispositionFailure))
        {
            throw new InvalidOperationException(
                $"Faction goods changed during atomic goodwill transfer: {dispositionFailure}");
        }
        FactionGoodwillOutbox.RecordPending(
            faction,
            sequence,
            receipt,
            consumedValue,
            rapportTarget);
        if (!FactionGoodwillOutbox.TryFinalizePending(
                faction,
                batchDispositions,
                campaignQuery,
                campaignCommand,
                domain.AcceptGoodwill,
                out bool domainAppliedNow,
                out string finalizeFailure))
        {
            throw new InvalidOperationException(
                $"Faction goodwill transfer committed but did not finalize: {finalizeFailure}");
        }
        if (domainAppliedNow)
        {
            events.Publish(new FactionTrustChangedEvent(
                faction.factionId,
                previousRapport,
                rapportTarget,
                $"호의 물자 {consumedValue}"));
        }
        FactionGoodwillOutbox.ClearCompleted(faction);
        ProjectCampaignRelationship(faction);
        message =
            $"{DisplayName(factionId)} 호의 물자 {consumedValue} 전달 · 신뢰 +{adjustedGain}";
        return true;
    }

    public bool TryCompleteAllianceProject(
        string factionId,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction)
            || !campaignQuery.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData relationship)
            || relationship.rapport < 70
            || relationship.grievance > 25)
        {
            message = "우호 70 이상, 원한 25 이하여야 동맹 프로젝트를 완료할 수 있습니다.";
            return false;
        }

        domain.CompleteAllianceProject(faction);
        message = $"{DisplayName(factionId)} 동맹 프로젝트 완료";
        return true;
    }

    public bool TryRequestTrade(
        string factionId,
        out string routeId,
        out string message)
    {
        return TryCreateRoute(
            factionId,
            FactionContractKind.Trade,
            FactionRouteKind.TradeCaravan,
            FindDefinition(factionId)?.TradeCargo,
            100,
            out routeId,
            out message);
    }

    public bool TryRequestSupply(
        string factionId,
        out string routeId,
        out string message)
    {
        return TryCreateRoute(
            factionId,
            FactionContractKind.Supply,
            FactionRouteKind.SupplyCaravan,
            FindDefinition(factionId)?.SupplyCargo,
            100,
            out routeId,
            out message);
    }

    public bool TryRequestReinforcement(
        string factionId,
        out string routeId,
        out string message)
    {
        return TryCreateRoute(
            factionId,
            FactionContractKind.Reinforcement,
            FactionRouteKind.Reinforcement,
            Array.Empty<FactionCargoLine>(),
            100,
            out routeId,
            out message);
    }

    public bool TryApplyRouteAmbush(
        string routeId,
        int strengthLoss,
        float delaySeconds,
        out string message)
    {
        FactionRouteState route = domain.FindTravelingRoute(routeId);
        if (route == null)
        {
            message = "매복을 적용할 이동 중 경로가 없습니다.";
            return false;
        }

        domain.ApplyRouteAmbush(route, strengthLoss, delaySeconds);
        if (route.strength <= 0)
        {
            message = "상단 또는 지원군이 매복으로 전멸했습니다.";
            return true;
        }

        message = $"매복 발생 · 전력 {route.strength} · 지연 {route.delaySeconds:0}초";
        return true;
    }

    public bool TryBetray(
        string factionId,
        int stolenValue,
        out string message)
    {
        foreach (DungeonFactionState state in factions)
            ProjectCampaignRelationship(state);
        if (!TryGetFaction(factionId, out DungeonFactionState target))
        {
            message = "배신할 세력을 찾을 수 없습니다.";
            return false;
        }

        if (!TrySpawnBetrayalLoot(
                factionId,
                stolenValue,
                out int actualLootValue,
                out message))
        {
            return false;
        }

        foreach (FactionTrustTransition transition in
                 domain.ApplyBetrayal(target, actualLootValue))
        {
            int rapportDelta = transition.Current - transition.Previous;
            campaignCommand.ApplyFactionChange(
                transition.FactionId,
                rapportDelta,
                string.Equals(transition.FactionId, factionId, StringComparison.Ordinal)
                    ? 35
                    : 10,
                0);
            if (domain.TryGetFaction(transition.FactionId, out DungeonFactionState projected))
                ProjectCampaignRelationship(projected);
            events.Publish(new FactionTrustChangedEvent(
                transition.FactionId,
                transition.Previous,
                transition.Current,
                string.Equals(
                    transition.FactionId,
                    factionId,
                    StringComparison.Ordinal)
                    ? "동맹 던전 약탈"
                    : "다른 던전 배신 목격"));
        }

        message =
            $"{DisplayName(factionId)} 배신 · 실물 약탈 가치 {actualLootValue} · " +
            $"협상 봉쇄 Day {target.negotiationBlockedUntilDay}까지";
        SynchronizeWorldHomes();
        return true;
    }

    public bool TryPayRestitution(
        string factionId,
        int physicalValue,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            message = "배상할 세력을 찾을 수 없습니다.";
            return false;
        }

        if (FactionRestitutionOutbox.HasProvenance(faction))
        {
            if (!FactionRestitutionOutbox.TryFinalizePending(
                    faction,
                    batchDispositions,
                    campaignQuery,
                    campaignCommand,
                    domain.AcceptRestitution,
                    out string replayFailure))
            {
                throw new InvalidOperationException(
                    $"Faction restitution could not reconcile its physical transfer: {replayFailure}");
            }
            ProjectCampaignRelationship(faction);
            message =
                $"{DisplayName(factionId)} 실물 배상 "
                + $"{faction.restitutionTransferredPhysicalValue} 접수 완료";
            return true;
        }

        if (faction.betrayalScars <= 0 || faction.restitutionPaid)
        {
            message = "미납 배상 의무가 없습니다.";
            return false;
        }

        int required = domain.GetRestitutionRequired(faction);
        if (physicalValue < required
            || !TrySelectPhysicalGoods(
                required,
                out PhysicalItemTransformInput[] inputs,
                out int consumedValue))
        {
            message = $"물리 배상 가치 {required}가 필요합니다.";
            return false;
        }

        if (!campaignQuery.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData campaignState)
            || campaignState == null)
        {
            throw new InvalidOperationException(
                $"Faction restitution campaign authority '{factionId}' is missing.");
        }
        string operationId = FactionRestitutionOutbox.FormatOperationId(
            factionId,
            faction.betrayalScars);
        if (!batchDispositions.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                FactionRestitutionOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string dispositionFailure))
        {
            throw new InvalidOperationException(
                $"Faction goods changed during atomic restitution transfer: {dispositionFailure}");
        }
        FactionRestitutionOutbox.RecordPending(
            faction,
            receipt,
            consumedValue,
            Math.Max(0, campaignState.grievance - 30));
        if (!FactionRestitutionOutbox.TryFinalizePending(
                faction,
                batchDispositions,
                campaignQuery,
                campaignCommand,
                domain.AcceptRestitution,
                out string finalizeFailure))
        {
            throw new InvalidOperationException(
                $"Faction restitution transfer committed but did not finalize: {finalizeFailure}");
        }
        ProjectCampaignRelationship(faction);
        message =
            $"{DisplayName(factionId)} 실물 배상 {consumedValue} 접수 완료";
        return true;
    }

    private bool TrySpawnBetrayalLoot(
        string factionId,
        int requestedValue,
        out int actualValue,
        out string message)
    {
        actualValue = 0;
        message = string.Empty;
        FactionDefinitionSnapshot definition = FindDefinition(factionId);
        if (definition == null
            || !dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            message = "약탈품을 실물로 내릴 하차장을 찾을 수 없습니다.";
            return false;
        }

        FactionCargoLine[] warehouse = definition.TradeCargo
            .Concat(definition.SupplyCargo)
            .Where(line => line != null
                && !string.IsNullOrWhiteSpace(line.itemId)
                && line.amount > 0)
            .ToArray();
        int targetValue = Mathf.Max(1, requestedValue);
        foreach (FactionCargoLine line in warehouse)
        {
            DungeonItemDefinition item =
                itemRuntime.CatalogProvider.GetDefinition(line.itemId);
            int unitValue = Mathf.Max(1, item.UnitPrice);
            int remainingValue = Mathf.Max(0, targetValue - actualValue);
            int quantity = Mathf.Clamp(
                Mathf.CeilToInt(remainingValue / (float)unitValue),
                0,
                line.amount);
            if (quantity <= 0)
            {
                continue;
            }

            int spawned = itemSpawner.Spawn(
                line.itemId,
                quantity,
                dropoff,
                WorldItemStackState.Loose,
                $"faction-betrayal:{factionId}:{currentDay}");
            actualValue += spawned * unitValue;
            if (actualValue >= targetValue)
            {
                break;
            }
        }

        if (actualValue <= 0)
        {
            message = "약탈 가능한 팩션 창고 물자가 없습니다.";
            return false;
        }

        return true;
    }

    private bool TrySelectPhysicalGoods(
        int requiredValue,
        out PhysicalItemTransformInput[] inputs,
        out int selectedValue)
    {
        selectedValue = 0;
        List<(WorldItemStackSnapshot stack, int quantity)> selection =
            new List<(WorldItemStackSnapshot, int)>();
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks()
                     .Where(value => value != null
                         && value.AvailableQuantity > 0
                         && value.AvailableQuantity == value.Quantity
                         && !value.HasUniqueMetadata
                         && value.Quantity > 0
                         && value.UnitPrice > 0
                         && value.State is WorldItemStackState.Loose
                             or WorldItemStackState.Stored)
                     .OrderByDescending(value => value.UnitPrice)
                     .ThenBy(value => value.StackId, StringComparer.Ordinal))
        {
            int remaining = requiredValue - selectedValue;
            int quantity = Mathf.Clamp(
                Mathf.CeilToInt(remaining / (float)stack.UnitPrice),
                0,
                stack.AvailableQuantity);
            if (quantity <= 0)
            {
                continue;
            }

            selection.Add((stack, quantity));
            selectedValue += quantity * stack.UnitPrice;
            if (selectedValue >= requiredValue)
            {
                break;
            }
        }

        if (selectedValue < requiredValue)
        {
            selectedValue = 0;
            inputs = Array.Empty<PhysicalItemTransformInput>();
            return false;
        }
        inputs = selection.Select(value => new PhysicalItemTransformInput(
            value.stack.StackId,
            value.quantity)).ToArray();
        return true;
    }

    public bool TryCompleteRecoveryEvent(
        string factionId,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            message = "복구 사건의 세력을 찾을 수 없습니다.";
            return false;
        }

        domain.CompleteRecoveryEvent(faction);
        campaignCommand.ApplyFactionChange(factionId, 5, -15, 0);
        ProjectCampaignRelationship(faction);
        message = $"{DisplayName(factionId)} 구조·방어 사건 완료";
        return true;
    }

    public void RecordReinforcementLoss(
        string factionId,
        int deaths,
        int equipmentLosses)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            return;
        }

        int previous = faction.trust;
        domain.RecordReinforcementLoss(faction, deaths, equipmentLosses);
        int delta = faction.trust - previous;
        if (delta != 0)
            campaignCommand.ApplyFactionChange(
                factionId,
                delta,
                Math.Max(0, deaths * 2 + equipmentLosses),
                0);
        ProjectCampaignRelationship(faction);
    }

    private DungeonFactionState ProjectCampaignRelationship(
        DungeonFactionState faction)
    {
        if (faction != null
            && campaignQuery.TryGetFaction(
                faction.factionId,
                out FactionCampaignStateSaveData relationship))
            faction.trust = relationship.rapport;
        return faction;
    }

    public DungeonFactionSaveData Capture()
    {
        EnsureInitialized();
        return new DungeonFactionSaveData
        {
            currentDay = currentDay,
            routeSequence = domain.RouteSequence,
            goodwillOperationSequence = domain.GoodwillOperationSequence,
            factions = Factions.Select(CloneFaction).ToList(),
            routes = routes
                .Select(CloneRoute)
                .OrderBy(value => FactionPayloadValidation.RouteSequenceOf(value.routeId))
                .ToList()
        };
    }

    public FactionRestoreCandidate PrepareRestoreCandidate(
        DungeonFactionSaveData saveData)
    {
        if (saveData?.factions == null || saveData.routes == null)
        {
            throw new InvalidOperationException(
                "Faction restore payload or collections are missing.");
        }

        FactionAggregateState restored = new()
        {
            CurrentDay = saveData.currentDay,
            RouteSequence = saveData.routeSequence,
            GoodwillOperationSequence = saveData.goodwillOperationSequence
        };
        foreach (DungeonFactionState savedFaction in saveData.factions)
        {
            DungeonFactionState clone = CloneFaction(savedFaction);
            restored.Factions.Add(clone.factionId, clone);
        }
        foreach (FactionRouteState savedRoute in saveData.routes)
        {
            FactionRouteState clone = CloneRoute(savedRoute);
            clone.reinforcementActorIds =
                FactionPayloadValidation
                    .CanonicalizeReinforcementActorIdsForRestore(savedRoute)
                    .ToList();
            restored.Routes.Add(clone);
        }
        return new FactionRestoreCandidate(restored, saveData);
    }

    public void PublishRestoreCandidate(FactionRestoreCandidate candidate)
    {
        domain.ReplaceState((candidate
            ?? throw new ArgumentNullException(nameof(candidate))).State);
        foreach (DungeonFactionState faction in factions
                     .Where(value =>
                         FactionRestitutionOutbox.HasProvenance(value)
                         && !value.restitutionTransferCompleted))
        {
            if (!FactionRestitutionOutbox.TryFinalizePending(
                    faction,
                    batchDispositions,
                    campaignQuery,
                    campaignCommand,
                    domain.AcceptRestitution,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    $"Faction '{faction.factionId}' restitution transfer could not be reconciled: {failureReason}");
            }
        }
        foreach (DungeonFactionState faction in factions
                     .Where(FactionGoodwillOutbox.HasProvenance))
        {
            if (!FactionGoodwillOutbox.TryFinalizePending(
                    faction,
                    batchDispositions,
                    campaignQuery,
                    campaignCommand,
                    domain.AcceptGoodwill,
                    out _,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    $"Faction '{faction.factionId}' goodwill transfer could not be reconciled: {failureReason}");
            }
            FactionGoodwillOutbox.ClearCompleted(faction);
        }
        SynchronizeWorldHomesUnlessStaging();
    }

    public void Reset()
    {
        domain.ReplaceState(CreateDefaultState());
        SynchronizeWorldHomesUnlessStaging();
    }

    private void EnsureInitialized()
    {
        if (factions.Any())
        {
            return;
        }

        domain.ReplaceState(CreateDefaultState());
        SynchronizeWorldHomesUnlessStaging();
    }

    private void SynchronizeWorldHomesUnlessStaging()
    {
        if (!domain.IsRestoreStaging)
        {
            SynchronizeWorldHomes();
        }
    }

    private void EnsureWorldHomesProjectionCurrent()
    {
        int publishedRevision = domain.PublishedRestoreRevision;
        if (projectedRestoreRevision == publishedRevision)
        {
            return;
        }

        projectedRestoreRevision = publishedRevision;
        SynchronizeWorldHomes();
    }

    private FactionAggregateState CreateDefaultState()
    {
        FactionAggregateState created = new();

        HashSet<OffenseHexCoord> occupied = world.Sites
            .Where(site => site != null && site.IsActive)
            .Select(site => site.Coord)
            .ToHashSet();
        IReadOnlyList<OffenseHexTileState> candidates = world.Tiles
            .Where(tile => tile != null
                && !tile.blocked
                && tile.Coord.DistanceTo(world.DungeonCoord) >= 4
                && !occupied.Contains(tile.Coord))
            .OrderBy(tile => tile.Coord)
            .ToArray();
        for (int i = 0; i < catalog.Definitions.Count; i++)
        {
            FactionDefinitionSnapshot definition = catalog.Definitions[i];
            int startIndex = candidates.Count > 0
                ? StableIndex(definition.StableId, candidates.Count)
                : 0;
            OffenseHexCoord home = candidates.Count > 0
                ? FindUnusedHome(candidates, occupied, startIndex)
                : new OffenseHexCoord(i + 4, -i);
            occupied.Add(home);
            created.Factions[definition.StableId] = new DungeonFactionState
            {
                factionId = definition.StableId,
                trust = 0,
                discovered = false,
                homeQ = home.Q,
                homeR = home.R
            };
        }

        return created;
    }

    private void OnWorldChanged()
    {
        if (synchronizingWorldHomes)
        {
            return;
        }

        synchronizingWorldHomes = true;
        try
        {
            foreach (DungeonFactionState faction in factions.ToArray())
            {
                string siteId = HomeSiteId(
                    faction.factionId,
                    faction.betrayalScars);
                if (world.TryGetSite(
                        siteId,
                        out OffenseWorldSiteStateData site)
                    && site.state == OffenseWorldSiteState.Resolved)
                {
                    TryBetray(faction.factionId, 400, out _);
                }
            }
        }
        finally
        {
            synchronizingWorldHomes = false;
        }

        SynchronizeWorldHomes();
    }

    private void SynchronizeWorldHomes()
    {
        if (synchronizingWorldHomes
            || world.Tiles == null
            || world.Tiles.Count == 0)
        {
            return;
        }

        synchronizingWorldHomes = true;
        try
        {
            foreach (DungeonFactionState faction in factions)
            {
                FactionDefinitionSnapshot definition =
                    FindDefinition(faction.factionId);
                world.TryRegisterStrategicSite(new OffenseWorldSiteStateData
                {
                    siteId = HomeSiteId(
                        faction.factionId,
                        faction.betrayalScars),
                    archetypeId = HomeArchetype(definition?.SpeciesTag),
                    displayName =
                        $"{definition?.DisplayName ?? faction.factionId} 본거지",
                    q = faction.homeQ,
                    r = faction.homeR,
                    // The strategic simulation owns authored regions. Leaving this
                    // empty makes registration project the actual home tile region
                    // instead of inventing an unrestorable faction-only region ID.
                    regionId = string.Empty,
                    factionId = faction.factionId,
                    state = OffenseWorldSiteState.Revealed,
                    fixedBoss = false,
                    strength = Mathf.Clamp(
                        5 + faction.betrayalScars,
                        5,
                        9),
                    createdDay = currentDay,
                    expiresDay = int.MaxValue,
                    pressureAxis = StrategicPressureAxis.None,
                    pressureAmount = 0f
                });
            }
        }
        finally
        {
            synchronizingWorldHomes = false;
        }
    }

    private static OffenseHexCoord FindUnusedHome(
        IReadOnlyList<OffenseHexTileState> candidates,
        ISet<OffenseHexCoord> occupied,
        int startIndex)
    {
        for (int offset = 0; offset < candidates.Count; offset++)
        {
            OffenseHexCoord coord =
                candidates[(startIndex + offset) % candidates.Count].Coord;
            if (!occupied.Contains(coord))
            {
                return coord;
            }
        }

        return candidates[startIndex % candidates.Count].Coord;
    }

    private static string HomeSiteId(string factionId, int raidSequence) =>
        $"faction-home:{factionId}:{Mathf.Max(0, raidSequence)}";

    private static string HomeArchetype(string speciesTag)
    {
        return speciesTag?.Trim().ToLowerInvariant() switch
        {
            "beastkin" => "farm",
            "demon" => "ritual_site",
            "kobold" => "armory",
            "myconid" => "farm",
            "harpy" => "watchtower",
            "golem" => "quarry",
            _ => "ruin"
        };
    }

    private bool TryCreateRoute(
        string factionId,
        FactionContractKind requiredContract,
        FactionRouteKind kind,
        IEnumerable<FactionCargoLine> cargo,
        int strength,
        out string routeId,
        out string message)
    {
        routeId = string.Empty;
        if (!TryGetFaction(factionId, out DungeonFactionState faction)
            || !IsContractUnlocked(factionId, requiredContract))
        {
            message = $"{requiredContract} 계약의 신뢰 또는 동맹 조건이 부족합니다.";
            return false;
        }

        FactionDefinitionSnapshot definition = FindDefinition(factionId);
        int cooldownDays = kind switch
        {
            FactionRouteKind.TradeCaravan => definition?.TradeCooldownDays ?? 7,
            FactionRouteKind.SupplyCaravan => definition?.SupplyCooldownDays ?? 20,
            FactionRouteKind.Reinforcement => definition?.ReinforcementCooldownDays ?? 10,
            _ => 1
        };
        int latestCreatedDay = routes
            .Where(value => value != null
                && value.kind == kind
                && string.Equals(value.factionId, factionId, StringComparison.Ordinal))
            .Select(value => value.createdDay)
            .DefaultIfEmpty(int.MinValue / 2)
            .Max();
        int nextAvailableDay = latestCreatedDay + cooldownDays;
        if (currentDay < nextAvailableDay)
        {
            message = $"같은 지원 경로는 Day {nextAvailableDay}부터 다시 요청할 수 있습니다.";
            return false;
        }

        if (!world.TryFindPath(
                new OffenseHexCoord(faction.HomeCoord.Q, faction.HomeCoord.R),
                world.DungeonCoord,
                OffenseTravelProfile.Default,
                out IReadOnlyList<OffenseHexCoord> path,
                out _))
        {
            message = "팩션 거점에서 던전까지 도달 가능한 육각 경로가 없습니다.";
            return false;
        }

        int steps = Mathf.Max(1, path.Count - 1);
        FactionRouteState route = new FactionRouteState
        {
            factionId = factionId,
            kind = kind,
            status = FactionRouteStatus.Traveling,
            path = path.Select(value => FactionHexCoordSaveData.From(
                new FactionHexCoord(value.Q, value.R))).ToList(),
            strength = Mathf.Clamp(strength, 1, 100),
            createdDay = currentDay,
            estimatedArrivalDay =
                currentDay + Mathf.CeilToInt(steps * SecondsPerHex / 180f),
            cargo = (cargo ?? Array.Empty<FactionCargoLine>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList()
        };
        routeId = domain.AddRoute(route);
        if (kind == FactionRouteKind.Reinforcement)
            campaignCommand.ApplyFactionChange(factionId, 0, 0, -1);
        message = $"{DisplayName(factionId)} 경로 출발 · ETA Day {route.estimatedArrivalDay}";
        return true;
    }

    private void CompleteRoute(FactionRouteState route)
    {
        if (route.kind is FactionRouteKind.TradeCaravan
            or FactionRouteKind.SupplyCaravan
            or FactionRouteKind.Restitution)
        {
            DeliverCargo(route);
        }
        else if (route.kind == FactionRouteKind.Reinforcement)
        {
            MaterializeReinforcements(route);
        }

        events.Publish(new FactionRouteArrivedEvent(
            route.routeId,
            route.factionId,
            route.kind,
            route.strength));
    }

    private void DeliverCargo(FactionRouteState route)
    {
        if (route.cargoDelivered
            || !dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            return;
        }

        foreach (FactionCargoLine line in route.cargo)
        {
            itemSpawner.Spawn(
                line.itemId,
                Mathf.Max(1, Mathf.RoundToInt(
                    line.amount * route.strength / 100f)),
                dropoff,
                WorldItemStackState.Loose,
                $"faction-delivery:{route.routeId}");
        }
        domain.MarkCargoDelivered(route);
    }

    private void MaterializeReinforcements(FactionRouteState route)
    {
        if (route.actorsSpawned
            || !spawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            || spawner == null
            || spawner.characterPrefab == null
            || !spawner.TryGetEntryGridPosition(out Vector2Int entry))
        {
            return;
        }

        FactionDefinitionSnapshot definition = FindDefinition(route.factionId);
        CharacterSO template = characterCatalog.Characters
            .Where(value => value != null)
            .Where(value => string.Equals(
                value.SpeciesTag,
                definition?.SpeciesTag,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.id)
            .FirstOrDefault();
        if (template == null)
        {
            return;
        }

        int count = Mathf.Clamp(
            Mathf.CeilToInt(route.strength / 40f),
            1,
            3);
        for (int index = 0; index < count; index++)
        {
            GameObject instance = characterFactory.CreateInactive(
                spawner.characterPrefab);
            CharacterActor actor = instance.GetComponent<CharacterActor>();
            if (actor == null)
            {
                characterFactory.Destroy(instance);
                continue;
            }

            CharacterId actorId = CharacterId.FromStableSuffix(
                $"{route.routeId}:ally:{index + 1}");
            instance.name =
                $"{definition?.DisplayName ?? route.factionId} 지원군 {index + 1}";
            instance.transform.position =
                spawner.GetOutsideSpawnWorldPosition()
                + Vector3.right * (index * 0.35f);
            actor.Initialize(template);
            actor.characterType = CharacterType.NPC;
            actor.Identity?.SetCharacterType(CharacterType.NPC);
            actor.Identity?.SetPersistentId(actorId);
            actor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
            if (actor.TryGetAbility(out AbilityWork work))
            {
                work.WorkPriorities.SetPriority(
                    BuiltInWorkTypeIds.Guard,
                    WorkPriorityLevel.Priority1);
            }

            FactionReinforcementMarker marker =
                instance.GetComponent<FactionReinforcementMarker>()
                ?? instance.AddComponent<FactionReinforcementMarker>();
            marker.Configure(route.routeId, route.factionId, route.strength);
            domain.AddReinforcementActor(route, actorId.Value);
            characterFactory.Publish(instance);

            if (actor.TryGetAbility(out AbilityMove move))
            {
                move.StartEnterDungeon(
                    spawner.GetEntryDoorWorldPosition(),
                    entry);
            }
            else if (worldRegistry.TryGetGrid(out Grid grid))
            {
                actor.transform.position = grid.GetWorldPos(entry);
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }
        }

        domain.FinishReinforcementMaterialization(route);
    }

    private FactionDefinitionSnapshot FindDefinition(string factionId)
    {
        return catalog.Definitions.FirstOrDefault(value =>
            string.Equals(value.StableId, factionId, StringComparison.Ordinal));
    }

    private string DisplayName(string factionId)
    {
        return FindDefinition(factionId)?.DisplayName ?? factionId;
    }

    private int StableIndex(string id, int count)
    {
        unchecked
        {
            int hash = world.WorldSeed;
            foreach (char character in id ?? string.Empty)
            {
                hash = hash * 31 + character;
            }
            return Mathf.Abs(hash == int.MinValue ? 0 : hash) % Mathf.Max(1, count);
        }
    }

    private static DungeonFactionState CloneFaction(DungeonFactionState value)
    {
        return JsonUtility.FromJson<DungeonFactionState>(
            JsonUtility.ToJson(value)) ?? new DungeonFactionState();
    }

    private static FactionRouteState CloneRoute(FactionRouteState value)
    {
        return JsonUtility.FromJson<FactionRouteState>(
            JsonUtility.ToJson(value)) ?? new FactionRouteState();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class FactionRuntimeApplicationAdapter :
    IFactionRuntime,
    IDungeonSaveCaptureGuard,
    IStartable,
    ITickable,
    IDisposable
{
    private const float SecondsPerHex = 20f;
    private const string CargoSourceOwnerDomain = "faction.route-cargo";
    private const string CargoReleaseReason =
        "faction-route-cargo-delivery";
    private const string CargoOutcomeRollbackReason =
        "faction-route-cargo-outcome-rollback";
    private readonly ResourceDungeonFactionCatalogApplicationAdapter catalog;
    private readonly IReadOnlyList<FactionDefinitionSnapshot> definitions;
    private readonly FactionDomainRuntime domain;
    private readonly IOffenseWorldSimulation world;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IPhysicalItemExactSourcePublicationService exactSources;
    private readonly IRunCharacterCatalog characterCatalog;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterFactory;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private readonly IFactionRouteEconomicPolicyRegistry routeEconomicPolicies;
    private readonly ResourceFactionAllianceBenefitBudgetApplicationAdapter
        allianceBenefitBudget;
    private readonly IIdempotentGameMoneyAccount money;
    private readonly IGameSessionStateStore gameSessionState;
    private readonly TreasuryEconomyAggregateStateStore treasuryState;
    private readonly FactionTradeSettlementRecovery tradeSettlementRecovery;
    private readonly IMigratedProducerOutcomeTransaction outcomeTransactions;
    private readonly Dictionary<string, PendingFactionCargoPublication>
        pendingCargo = new(StringComparer.Ordinal);
    private readonly IFactionCampaignQuery campaignQuery;
    private readonly IFactionCampaignCommand campaignCommand;
    private readonly IV20CampaignPersistence campaignPersistence;
    [ApplicationAdapterTransientState]
    private IDisposable daySubscription;
    [ApplicationAdapterTransientState]
    private bool synchronizingWorldHomes;
    [ApplicationAdapterTransientState]
    private int projectedRestoreRevision;

    private sealed class PendingFactionCargoPublication
    {
        public PhysicalItemExactSourcePublicationPlan Plan;
        public PhysicalItemExactSourcePublicationTransaction Transaction;
        public FactionRouteCargoDeliveryReceipt ExpectedReceipt;
        public FactionRouteCargoDeliveryReceipt BeforeReceipt;
        public DungeonPhysicalItemSaveData PhysicalItemsBefore;
        public PreparedMigratedProducerOutcome Outcome;
        public bool OutcomeCommitted;
    }

    private sealed class PreparedFactionRouteArrival
    {
        public FactionRouteState Route;
        public FactionRouteState Before;
        public PreparedMigratedProducerOutcome Outcome;
    }

    private IEnumerable<DungeonFactionState> factions => domain.FactionStates;
    private IReadOnlyList<FactionRouteState> routes => domain.Routes;
    private int currentDay => domain.CurrentDay;

    [Inject]
    public FactionRuntimeApplicationAdapter(
        ResourceDungeonFactionCatalogApplicationAdapter catalog,
        IOffenseWorldSimulation world,
        FactionItemLogisticsDependencies itemLogistics,
        FactionCharacterSpawnDependencies characterSpawning,
        IGameClock clock,
        IGameEventBus events,
        IFactionRouteEconomicPolicyRegistry routeEconomicPolicies,
        ResourceFactionAllianceBenefitBudgetApplicationAdapter
            allianceBenefitBudget,
        IIdempotentGameMoneyAccount money,
        IGameSessionStateStore gameSessionState,
        TreasuryEconomyAggregateStateStore treasuryState,
        V20CampaignRuntime campaign,
        IMigratedProducerOutcomeTransaction outcomeTransactions,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        definitions = this.catalog.Definitions;
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        itemLogistics = itemLogistics
            ?? throw new ArgumentNullException(nameof(itemLogistics));
        characterSpawning = characterSpawning
            ?? throw new ArgumentNullException(nameof(characterSpawning));
        itemSpawner = itemLogistics.ItemSpawner;
        itemRuntime = itemLogistics.ItemRuntime;
        batchDispositions = itemLogistics.BatchDispositions;
        dropZones = itemLogistics.DropZones;
        exactSources = itemLogistics.ExactSources;
        characterCatalog = characterSpawning.CharacterCatalog;
        spawnerProvider = characterSpawning.SpawnerProvider;
        characterFactory = characterSpawning.CharacterFactory;
        worldRegistry = characterSpawning.WorldRegistry;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.routeEconomicPolicies = routeEconomicPolicies
            ?? throw new ArgumentNullException(nameof(routeEconomicPolicies));
        this.allianceBenefitBudget = allianceBenefitBudget
            ?? throw new ArgumentNullException(nameof(allianceBenefitBudget));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.gameSessionState = gameSessionState
            ?? throw new ArgumentNullException(nameof(gameSessionState));
        this.treasuryState = treasuryState
            ?? throw new ArgumentNullException(nameof(treasuryState));
        tradeSettlementRecovery = new FactionTradeSettlementRecovery(this.money);
        this.outcomeTransactions = outcomeTransactions
            ?? throw new ArgumentNullException(nameof(outcomeTransactions));
        campaignQuery = campaign ?? throw new ArgumentNullException(nameof(campaign));
        campaignCommand = campaign;
        campaignPersistence = campaign;
        domain = new FactionDomainRuntime(
            aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
    }

    internal FactionRuntimeApplicationAdapter(
        FactionAggregateState initialState,
        IGameClock clock,
        IGameEventBus events,
        IIdempotentGameMoneyAccount money,
        IFactionCampaignQuery campaignQuery,
        IFactionCampaignCommand campaignCommand,
        IV20CampaignPersistence campaignPersistence,
        IMigratedProducerOutcomeTransaction outcomeTransactions,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IWorldDropZoneQuery dropZones = null,
        IPhysicalItemExactSourcePublicationService exactSources = null,
        IWorldItemStackRuntime itemRuntime = null,
        IReadOnlyList<FactionDefinitionSnapshot> definitions = null,
        IOffenseWorldSimulation world = null,
        IFactionRouteEconomicPolicyRegistry routeEconomicPolicies = null,
        IGameSessionStateStore gameSessionState = null,
        TreasuryEconomyAggregateStateStore treasuryState = null)
    {
        catalog = null;
        this.definitions = definitions ?? Array.Empty<FactionDefinitionSnapshot>();
        this.world = world;
        itemSpawner = null;
        this.itemRuntime = itemRuntime;
        batchDispositions = null;
        this.dropZones = dropZones;
        this.exactSources = exactSources;
        characterCatalog = null;
        spawnerProvider = null;
        characterFactory = null;
        worldRegistry = null;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.routeEconomicPolicies = routeEconomicPolicies;
        allianceBenefitBudget = null;
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.gameSessionState = gameSessionState;
        this.treasuryState = treasuryState;
        tradeSettlementRecovery = new FactionTradeSettlementRecovery(this.money);
        this.outcomeTransactions = outcomeTransactions
            ?? throw new ArgumentNullException(nameof(outcomeTransactions));
        this.campaignQuery = campaignQuery
            ?? throw new ArgumentNullException(nameof(campaignQuery));
        this.campaignCommand = campaignCommand
            ?? throw new ArgumentNullException(nameof(campaignCommand));
        this.campaignPersistence = campaignPersistence
            ?? throw new ArgumentNullException(nameof(campaignPersistence));
        domain = new FactionDomainRuntime(
            aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
        domain.ReplaceState(initialState
            ?? throw new ArgumentNullException(nameof(initialState)));
        projectedRestoreRevision = domain.PublishedRestoreRevision;
    }

    public IReadOnlyList<FactionDefinitionSnapshot> Definitions =>
        definitions;
    public IReadOnlyList<DungeonFactionState> Factions =>
        definitions
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
            OnOperatingDayStarted);
    }

    public void Dispose()
    {
        world.Changed -= OnWorldChanged;
        daySubscription?.Dispose();
        daySubscription = null;
    }

    public void Tick()
    {
        if (!tradeSettlementRecovery.TryResolve())
        {
            return;
        }
        EnsureWorldHomesProjectionCurrent();
        foreach (FactionRouteState arrived in routes.Where(value =>
                     value != null
                     && value.status == FactionRouteStatus.Arrived
                     && value.kind is FactionRouteKind.TradeCaravan
                         or FactionRouteKind.SupplyCaravan
                         or FactionRouteKind.Restitution
                     && value.cargoDelivery != null
                     && value.cargoDelivery.state is
                         FactionRouteCargoDeliveryState.Ready
                         or FactionRouteCargoDeliveryState.Publishing))
        {
            TryDeliverCargo(arrived);
        }
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

        IReadOnlyList<FactionRouteState> predictedArrivals =
            domain.PreviewRouteArrivals(clock.DeltaTime, SecondsPerHex);
        Dictionary<string, PreparedFactionRouteArrival> preparedArrivals =
            new(StringComparer.Ordinal);
        try
        {
            foreach (FactionRouteState route in predictedArrivals)
            {
                FactionRouteState before = CloneRoute(route);
                if (!outcomeTransactions.TryReserveSingleSubject(
                        MigratedProducerOutcomeKind.FactionRouteArrivedEvent,
                        route.routeId,
                        Math.Max(1, currentDay),
                        GameplayOutcomeStatus.Succeeded,
                        out PreparedMigratedProducerOutcome prepared,
                        out _))
                {
                    foreach (PreparedFactionRouteArrival reserved in
                             preparedArrivals.Values)
                    {
                        outcomeTransactions.Cancel(reserved.Outcome);
                    }
                    return;
                }
                try
                {
                    preparedArrivals.Add(
                        route.routeId,
                        new PreparedFactionRouteArrival
                        {
                            Route = route,
                            Before = before,
                            Outcome = prepared
                        });
                }
                catch
                {
                    outcomeTransactions.Cancel(prepared);
                    throw;
                }
            }
        }
        catch
        {
            foreach (PreparedFactionRouteArrival reserved in
                     preparedArrivals.Values)
            {
                outcomeTransactions.Cancel(reserved.Outcome);
            }
            throw;
        }

        try
        {
            foreach (FactionRouteState route in domain.AdvanceRoutes(
                         clock.DeltaTime,
                         SecondsPerHex))
            {
                if (!preparedArrivals.Remove(
                        route.routeId,
                        out PreparedFactionRouteArrival prepared))
                {
                    throw new InvalidOperationException(
                        $"Faction route '{route.routeId}' arrived without a prepared outcome reservation.");
                }

                MigratedProducerOutcomeCommitResult committed;
                try
                {
                    committed = CommitRouteArrivalOutcome(
                        prepared.Outcome,
                        route);
                }
                catch
                {
                    outcomeTransactions.Cancel(prepared.Outcome);
                    RestoreRouteSnapshot(route, prepared.Before);
                    throw;
                }
                if (!committed.DurablyCommitted)
                {
                    RestoreRouteSnapshot(route, prepared.Before);
                    continue;
                }

                CompleteRoute(route);
                PublishRouteArrivedObserver(route);
            }
        }
        finally
        {
            foreach (PreparedFactionRouteArrival unused in
                     preparedArrivals.Values)
            {
                outcomeTransactions.Cancel(unused.Outcome);
                if (unused.Route != null
                    && unused.Route.status == FactionRouteStatus.Arrived
                    && unused.Before?.status != FactionRouteStatus.Arrived)
                {
                    RestoreRouteSnapshot(unused.Route, unused.Before);
                }
            }
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
        if (!outcomeTransactions.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.FactionTrustChangedEvent,
                faction.factionId,
                Math.Max(1, currentDay),
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out _))
        {
            message = "세력 관계 결과 기록을 준비하지 못했습니다.";
            return false;
        }

        FactionCampaignWorldSaveData campaignBefore =
            campaignPersistence.CaptureFactions();
        try
        {
            campaignCommand.ApplyFactionChange(
                factionId,
                adjusted,
                amount < 0 ? Math.Max(1, -amount / 2) : 0,
                0);
            ProjectCampaignRelationship(faction);
            MigratedProducerOutcomeCommitResult committed =
                CommitFactionTrustOutcome(
                    prepared,
                    faction.factionId,
                    previous,
                    faction.trust,
                    reason,
                    faction.factionId);
            if (!committed.DurablyCommitted)
            {
                RestoreCampaignFactions(campaignBefore);
                ProjectCampaignRelationship(faction);
                message = "세력 관계 결과 기록이 거절되어 변경을 복원했습니다.";
                return false;
            }
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            RestoreCampaignFactions(campaignBefore);
            ProjectCampaignRelationship(faction);
            throw;
        }

        PublishTrustChangedObserver(
            faction.factionId,
            previous,
            faction.trust,
            reason);
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

        int consumedValue;
        int previousRapport;
        int adjustedGain;
        PreparedMigratedProducerOutcome prepared;
        if (FactionGoodwillOutbox.HasProvenance(faction))
        {
            consumedValue = faction.goodwillTransferredPhysicalValue;
            if (faction.goodwillTransferCompleted)
            {
                if (!FactionGoodwillOutbox.TryApplyPending(
                        faction,
                        batchDispositions,
                        campaignQuery,
                        campaignCommand,
                        domain.AcceptGoodwill,
                        out _,
                        out string completedFailure))
                {
                    throw new InvalidOperationException(
                        $"Faction goodwill could not reconcile its completed transfer: {completedFailure}");
                }
                if (!FactionGoodwillOutbox.TryAcknowledgeCompleted(
                        faction,
                        batchDispositions,
                        out _))
                {
                    ProjectCampaignRelationship(faction);
                    message =
                        $"{DisplayName(factionId)} 호의 물자 {consumedValue} 전달 완료 · 영수증 확인 대기";
                    return true;
                }
                FactionGoodwillOutbox.ClearCompleted(faction);
                ProjectCampaignRelationship(faction);
                message =
                    $"{DisplayName(factionId)} 호의 물자 {consumedValue} 전달 완료";
                return true;
            }
            if (!campaignQuery.TryGetFaction(
                    factionId,
                    out FactionCampaignStateSaveData pendingCampaign)
                || pendingCampaign == null)
            {
                throw new InvalidOperationException(
                    $"Faction goodwill campaign authority '{factionId}' is missing.");
            }
            previousRapport = pendingCampaign.rapport;
            adjustedGain = Math.Max(
                0,
                faction.goodwillCampaignRapportTarget - previousRapport);
            if (!outcomeTransactions.TryReserveSingleSubject(
                    MigratedProducerOutcomeKind.FactionTrustChangedEvent,
                    faction.goodwillTransferOperationId,
                    Math.Max(1, currentDay),
                    GameplayOutcomeStatus.Succeeded,
                    out prepared,
                    out _))
            {
                message =
                    $"{DisplayName(factionId)} 호의 물자 {consumedValue} 전달 접수 · 관계 반영 대기";
                return true;
            }
        }
        else
        {
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
                    out consumedValue))
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
            previousRapport = campaignState.rapport;
            int rawGain = Mathf.Clamp(consumedValue / 10, 1, 10);
            adjustedGain = Math.Max(1, (int)MathF.Round(
                rawGain * MathF.Pow(0.85f, faction.betrayalScars)));
            int rapportTarget = Math.Clamp(
                previousRapport + adjustedGain,
                -100,
                100);
            if (domain.GoodwillOperationSequence == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Faction goodwill operation sequence is exhausted.");
            }
            int nextSequence = domain.GoodwillOperationSequence + 1;
            string operationId = FactionGoodwillOutbox.FormatOperationId(
                factionId,
                nextSequence);
            if (!outcomeTransactions.TryReserveSingleSubject(
                    MigratedProducerOutcomeKind.FactionTrustChangedEvent,
                    operationId,
                    Math.Max(1, currentDay),
                    GameplayOutcomeStatus.Succeeded,
                    out prepared,
                    out _))
            {
                message = "세력 관계 결과 기록을 준비하지 못했습니다.";
                return false;
            }

            try
            {
                int sequence = domain.AllocateGoodwillOperationSequence();
                if (sequence != nextSequence)
                {
                    throw new InvalidOperationException(
                        "Faction goodwill operation sequence changed after reservation.");
                }
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
            }
            catch
            {
                outcomeTransactions.Cancel(prepared);
                throw;
            }
        }

        FactionCampaignWorldSaveData campaignBefore =
            campaignPersistence.CaptureFactions();
        DungeonFactionState pendingBefore = CloneFaction(faction);
        bool domainAppliedNow;
        try
        {
            if (!FactionGoodwillOutbox.TryApplyPending(
                    faction,
                    batchDispositions,
                    campaignQuery,
                    campaignCommand,
                    domain.AcceptGoodwill,
                    out domainAppliedNow,
                    out string finalizeFailure))
            {
                throw new InvalidOperationException(
                    $"Faction goodwill transfer committed but did not finalize: {finalizeFailure}");
            }
            if (!campaignQuery.TryGetFaction(
                    factionId,
                    out FactionCampaignStateSaveData appliedCampaign)
                || appliedCampaign == null)
            {
                throw new InvalidOperationException(
                    $"Faction goodwill campaign authority '{factionId}' is missing after apply.");
            }
            int currentRapport = appliedCampaign.rapport;
            if (!domainAppliedNow)
            {
                outcomeTransactions.Cancel(prepared);
            }
            else
            {
                string reason = $"호의 물자 {consumedValue}";
                MigratedProducerOutcomeCommitResult committed =
                    CommitFactionTrustOutcome(
                        prepared,
                        faction.factionId,
                        previousRapport,
                        currentRapport,
                        reason,
                        faction.goodwillTransferOperationId);
                if (!committed.DurablyCommitted)
                {
                    RestoreCampaignFactions(campaignBefore);
                    RestoreFactionSnapshot(faction, pendingBefore);
                    ProjectCampaignRelationship(faction);
                    message =
                        $"{DisplayName(factionId)} 호의 물자 {consumedValue} 전달 접수 · 관계 반영 대기";
                    return true;
                }
                PublishTrustChangedObserver(
                    faction.factionId,
                    previousRapport,
                    currentRapport,
                    reason);
                adjustedGain = currentRapport - previousRapport;
            }
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            RestoreCampaignFactions(campaignBefore);
            RestoreFactionSnapshot(faction, pendingBefore);
            ProjectCampaignRelationship(faction);
            throw;
        }

        if (!FactionGoodwillOutbox.TryAcknowledgeCompleted(
                faction,
                batchDispositions,
                out _))
        {
            ProjectCampaignRelationship(faction);
            message =
                $"{DisplayName(factionId)} 호의 물자 {consumedValue} 전달 완료 · 영수증 확인 대기";
            return true;
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

        DungeonFactionState[] affectedFactions = factions.ToArray();
        string betrayalOperationId =
            FormatBetrayalOperationId(factionId, currentDay);
        if (!outcomeTransactions.TryReserve(
                MigratedProducerOutcomeKind.FactionTrustChangedEvent,
                betrayalOperationId,
                Math.Max(1, currentDay),
                GameplayOutcomeStatus.Succeeded,
                participantCount: affectedFactions.Length,
                metricCount: 0,
                subjectCount: affectedFactions.Length,
                additionalFactCount: 0,
                out PreparedMigratedProducerOutcome prepared,
                out _))
        {
            message = "세력 배신 결과 기록을 준비하지 못했습니다.";
            return false;
        }

        FactionCampaignWorldSaveData campaignBefore =
            campaignPersistence.CaptureFactions();
        DungeonPhysicalItemSaveData physicalItemsBefore = itemRuntime.Capture();
        Dictionary<string, DungeonFactionState> factionsBefore =
            affectedFactions.ToDictionary(
                value => value.factionId,
                CloneFaction,
                StringComparer.Ordinal);
        IReadOnlyList<FactionTrustTransition> transitions;
        int actualLootValue;
        try
        {
            if (!TrySpawnBetrayalLoot(
                    factionId,
                    stolenValue,
                    out actualLootValue,
                    out message))
            {
                outcomeTransactions.Cancel(prepared);
                return false;
            }

            transitions = domain.ApplyBetrayal(target, actualLootValue);
            foreach (FactionTrustTransition transition in transitions)
            {
                int rapportDelta = transition.Current - transition.Previous;
                campaignCommand.ApplyFactionChange(
                    transition.FactionId,
                    rapportDelta,
                    string.Equals(
                        transition.FactionId,
                        factionId,
                        StringComparison.Ordinal)
                        ? 35
                        : 10,
                    0);
                if (domain.TryGetFaction(
                        transition.FactionId,
                        out DungeonFactionState projected))
                {
                    ProjectCampaignRelationship(projected);
                }
            }

            MigratedProducerOutcomeCommitResult committed =
                CommitFactionBetrayalOutcome(
                    prepared,
                    transitions,
                    factionId,
                    betrayalOperationId,
                    actualLootValue);
            if (!committed.DurablyCommitted)
            {
                itemRuntime.Restore(physicalItemsBefore);
                RestoreCampaignFactions(campaignBefore);
                RestoreFactionSnapshots(factionsBefore);
                message = "세력 배신 결과 기록이 거절되어 관계 변경을 복원했습니다.";
                return false;
            }
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            itemRuntime.Restore(physicalItemsBefore);
            RestoreCampaignFactions(campaignBefore);
            RestoreFactionSnapshots(factionsBefore);
            throw;
        }

        foreach (FactionTrustTransition transition in transitions)
        {
            PublishTrustChangedObserver(
                transition.FactionId,
                transition.Previous,
                transition.Current,
                string.Equals(
                    transition.FactionId,
                    factionId,
                    StringComparison.Ordinal)
                    ? "동맹 던전 약탈"
                    : "다른 던전 배신 목격");
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
                FormatBetrayalOperationId(factionId, currentDay));
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
        tradeSettlementRecovery.EnsureResolved("direct faction capture");
        EnsureCargoPublicationCaptureSafe("direct faction capture");
        EnsureInitialized();
        return new DungeonFactionSaveData
        {
            currentDay = currentDay,
            routeSequence = domain.RouteSequence,
            routeSettlementOperationSequence =
                domain.RouteSettlementOperationSequence,
            goodwillOperationSequence = domain.GoodwillOperationSequence,
            allianceBenefitBalanceMilliEwu =
                domain.AllianceBenefitBalanceMilliEwu,
            allianceBenefitRefillRemainder =
                domain.AllianceBenefitRefillRemainder,
            allianceBenefitLastRefillDay =
                domain.AllianceBenefitLastRefillDay,
            allianceBenefitAuthorityDigest =
                domain.AllianceBenefitAuthorityDigest,
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
        if (!string.Equals(
                saveData.allianceBenefitAuthorityDigest,
                allianceBenefitBudget.AuthorityDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Faction alliance-benefit budget save authority is stale.");
        }
        if (saveData.allianceBenefitBalanceMilliEwu
                > allianceBenefitBudget.CapacityMilliEwu
            || saveData.allianceBenefitRefillRemainder
                >= allianceBenefitBudget.RefillDenominatorDays
            || (saveData.allianceBenefitBalanceMilliEwu
                    == allianceBenefitBudget.CapacityMilliEwu
                && saveData.allianceBenefitRefillRemainder != 0L))
        {
            throw new InvalidOperationException(
                "Faction alliance-benefit budget save state exceeds its current authority.");
        }
        foreach (FactionRouteState supplyRoute in saveData.routes.Where(
                     value => value != null
                         && value.kind == FactionRouteKind.SupplyCaravan))
        {
            FactionRouteSettlementReceipt receipt = supplyRoute.settlement;
            FactionDefinitionSnapshot currentDefinition =
                FindDefinition(supplyRoute.factionId);
            if (receipt == null
                || !allianceBenefitBudget.TryGetRoute(
                    supplyRoute.factionId,
                    out FactionAllianceBenefitRouteBudgetSnapshot budgetRoute)
                || currentDefinition == null
                || currentDefinition.SupplyCooldownDays
                    != budgetRoute.CooldownDays
                || !string.Equals(
                    receipt.allianceBenefitAuthorityDigest,
                    allianceBenefitBudget.AuthorityDigest,
                    StringComparison.Ordinal)
                || receipt.allianceBenefitDebitMilliEwu
                    != budgetRoute.DebitMilliEwu
                || !string.Equals(
                    receipt.sourceDigest,
                    budgetRoute.SupplyQuoteSourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Faction supply route '{supplyRoute.routeId}' has stale alliance-benefit budget provenance.");
            }
        }

        FactionAggregateState restored = new()
        {
            CurrentDay = saveData.currentDay,
            RouteSequence = saveData.routeSequence,
            RouteSettlementOperationSequence =
                saveData.routeSettlementOperationSequence,
            GoodwillOperationSequence = saveData.goodwillOperationSequence,
            AllianceBenefitBalanceMilliEwu =
                saveData.allianceBenefitBalanceMilliEwu,
            AllianceBenefitRefillRemainder =
                saveData.allianceBenefitRefillRemainder,
            AllianceBenefitLastRefillDay =
                saveData.allianceBenefitLastRefillDay,
            AllianceBenefitAuthorityDigest =
                saveData.allianceBenefitAuthorityDigest ?? string.Empty
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
        tradeSettlementRecovery.EnsureResolved("restore publication");
        EnsureCargoPublicationCaptureSafe("restore publication");
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
        tradeSettlementRecovery.EnsureResolved("runtime reset");
        EnsureCargoPublicationCaptureSafe("runtime reset");
        domain.ReplaceState(CreateDefaultState());
        SynchronizeWorldHomesUnlessStaging();
    }

    public void ValidateBeforeCapture()
    {
        tradeSettlementRecovery.EnsureResolved("save capture");
        EnsureCargoPublicationCaptureSafe("save capture");
    }

    private void EnsureCargoPublicationCaptureSafe(string boundary)
    {
        string[] publishingRouteIds = routes
            .Where(value => value?.cargoDelivery?.state
                == FactionRouteCargoDeliveryState.Publishing)
            .Select(value => value.routeId ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (pendingCargo.Count > 0 || publishingRouteIds.Length > 0)
        {
            throw new InvalidOperationException(
                "Faction exact cargo publication is pending during "
                + (boundary ?? "an unknown boundary")
                + ": transient="
                + pendingCargo.Count
                + ", routes="
                + string.Join(",", publishingRouteIds)
                + ".");
        }
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

    private void OnOperatingDayStarted(OperatingDayStartedEvent value)
    {
        domain.ApplyAllianceBenefitRefill(
            value.day,
            allianceBenefitBudget.AuthorityDigest,
            allianceBenefitBudget.CapacityMilliEwu,
            allianceBenefitBudget.RefillNumeratorMilliEwu,
            allianceBenefitBudget.RefillDenominatorDays);
        domain.SetCurrentDay(value.day);
    }

    private FactionAggregateState CreateDefaultState()
    {
        FactionAggregateState created = new()
        {
            CurrentDay = Math.Max(1, currentDay),
            AllianceBenefitBalanceMilliEwu =
                allianceBenefitBudget.CapacityMilliEwu,
            AllianceBenefitRefillRemainder = 0L,
            AllianceBenefitLastRefillDay = Math.Max(1, currentDay),
            AllianceBenefitAuthorityDigest =
                allianceBenefitBudget.AuthorityDigest
        };

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
        for (int i = 0; i < definitions.Count; i++)
        {
            FactionDefinitionSnapshot definition = definitions[i];
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
        int strength,
        out string routeId,
        out string message)
    {
        routeId = string.Empty;
        if (!tradeSettlementRecovery.TryResolve())
        {
            message = "이전 세력 교역 환불이 아직 복구 중입니다: "
                + tradeSettlementRecovery.LastFailure;
            return false;
        }
        if (!TryGetFaction(factionId, out DungeonFactionState faction)
            || !IsContractUnlocked(factionId, requiredContract))
        {
            message = $"{requiredContract} 계약의 신뢰 또는 동맹 조건이 부족합니다.";
            return false;
        }

        FactionDefinitionSnapshot definition = FindDefinition(factionId);
        if (definition == null)
        {
            message = "세력 콘텐츠 정의가 없습니다.";
            return false;
        }
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

        if (domain.RouteSequence == int.MaxValue)
        {
            message = "세력 경로 식별자 공간이 소진되었습니다.";
            return false;
        }

        FactionRouteSettlementReceipt settlement =
            new FactionRouteSettlementReceipt();
        IReadOnlyList<FactionCargoLine> routeCargo = kind switch
        {
            FactionRouteKind.TradeCaravan => definition.TradeCargo,
            FactionRouteKind.SupplyCaravan => definition.SupplyCargo,
            _ => Array.Empty<FactionCargoLine>()
        };
        bool hasSettlement = kind is FactionRouteKind.TradeCaravan
            or FactionRouteKind.SupplyCaravan;
        FactionRouteQuoteSnapshot quote = null;
        FactionAllianceBenefitRouteBudgetSnapshot budgetRoute = null;
        int settlementSequence = 0;
        string settlementIdentity = string.Empty;
        string expectedSettlementRouteId = string.Empty;
        EconomyTransactionContext paymentContext = default;
        EconomyTransactionContext refundContext = default;
        PreparedMigratedProducerOutcome settlementOutcome = default;
        FactionRouteCreationMutationSnapshot factionBefore = default;
        GameSessionSnapshot sessionBefore = default;
        TreasuryEconomyAggregateState treasuryBefore = null;
        if (hasSettlement)
        {
            if (!routeEconomicPolicies.TryCreateQuote(
                    definition,
                    kind,
                    out quote,
                    out string quoteFailure))
            {
                message = quoteFailure;
                return false;
            }

            if (domain.RouteSettlementOperationSequence == int.MaxValue)
            {
                message = "세력 경로 정산 식별자 공간이 소진되었습니다.";
                return false;
            }
            settlementSequence = domain.RouteSettlementOperationSequence + 1;
            if (kind == FactionRouteKind.SupplyCaravan)
            {
                if (!allianceBenefitBudget.TryGetRoute(
                        factionId,
                        out budgetRoute)
                    || budgetRoute.CooldownDays != definition.SupplyCooldownDays
                    || !string.Equals(
                        budgetRoute.SupplyQuoteSourceDigest,
                        quote.SourceDigest,
                        StringComparison.Ordinal))
                {
                    message = "세력 보급 경로가 승인된 전역 혜택 예산 원장과 일치하지 않습니다.";
                    return false;
                }
                settlementIdentity =
                    $"faction-alliance-benefit:{settlementSequence:D8}";
            }
            else
            {
                settlementIdentity =
                    $"faction-route-settlement:{settlementSequence:D8}";
                paymentContext = new EconomyTransactionContext(
                    EconomyTransactionKind.FactionTradePurchase,
                    settlementIdentity,
                    factionId,
                    $"{definition.DisplayName} 교역 화물 선결제");
                refundContext = new EconomyTransactionContext(
                    EconomyTransactionKind.FactionTradePurchaseRefund,
                    settlementIdentity + ":refund",
                    factionId,
                    "세력 교역 경로 게시 실패 환불");
                tradeSettlementRecovery.ValidateCanBegin(
                    quote.PaymentGold,
                    refundContext);
            }

            expectedSettlementRouteId =
                $"faction-route:{domain.RouteSequence + 1}";
            if (!outcomeTransactions.TryReserveSingleSubject(
                    MigratedProducerOutcomeKind.FactionRouteSettlementReceipt,
                    settlementIdentity,
                    Math.Max(1, currentDay),
                    GameplayOutcomeStatus.Succeeded,
                    out settlementOutcome,
                    out _))
            {
                message = "세력 경로 정산 결과 기록을 준비하지 못했습니다.";
                return false;
            }
            try
            {
                if (!gameSessionState.TryGetSessionState(
                        out GameSessionState session))
                {
                    throw new InvalidOperationException(
                        "Faction route settlement could not capture the game-session authority.");
                }
                factionBefore = domain.CaptureRouteCreationMutationSnapshot();
                sessionBefore = session.Capture();
                treasuryBefore = treasuryState.Current.Copy();
            }
            catch
            {
                outcomeTransactions.Cancel(settlementOutcome);
                throw;
            }
        }

        FactionRouteState route;
        try
        {
            if (hasSettlement)
            {
                int allocated =
                    domain.AllocateRouteSettlementOperationSequence();
                if (allocated != settlementSequence)
                {
                    throw new InvalidOperationException(
                        "Faction route settlement sequence changed after outcome reservation.");
                }

                if (kind == FactionRouteKind.SupplyCaravan)
                {
                    domain.ApplyAllianceBenefitRefill(
                        currentDay,
                        allianceBenefitBudget.AuthorityDigest,
                        allianceBenefitBudget.CapacityMilliEwu,
                        allianceBenefitBudget.RefillNumeratorMilliEwu,
                        allianceBenefitBudget.RefillDenominatorDays);
                    if (!domain.TryReserveAllianceBenefit(
                            allianceBenefitBudget.AuthorityDigest,
                            allianceBenefitBudget.CapacityMilliEwu,
                            budgetRoute.DebitMilliEwu,
                            out long budgetBalanceBefore,
                            out long budgetBalanceAfter,
                            out string budgetFailure))
                    {
                        RollbackRouteSettlement(
                            factionBefore,
                            sessionBefore,
                            treasuryBefore,
                            settlementOutcome);
                        message = budgetFailure;
                        return false;
                    }
                    settlement = CreateAllianceBenefitSettlementReceipt(
                        quote,
                        settlementSequence,
                        allianceBenefitBudget.AuthorityDigest,
                        budgetRoute.DebitMilliEwu,
                        budgetBalanceBefore,
                        budgetBalanceAfter);
                }
                else
                {
                    if (!money.TrySpendOnce(
                            quote.PaymentGold,
                            paymentContext,
                            out EconomyTransactionRecord paymentReceipt,
                            out string spendFailure))
                    {
                        RollbackRouteSettlement(
                            factionBefore,
                            sessionBefore,
                            treasuryBefore,
                            settlementOutcome);
                        message = spendFailure;
                        return false;
                    }
                    tradeSettlementRecovery.BeginCommittedDebit(
                        quote.PaymentGold,
                        refundContext);
                    settlement = CreatePaidSettlementReceipt(
                        quote,
                        settlementSequence,
                        paymentContext,
                        paymentReceipt);
                }
            }

            int steps = Mathf.Max(1, path.Count - 1);
            List<FactionCargoLine> frozenCargo =
                (routeCargo ?? Array.Empty<FactionCargoLine>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .OrderBy(value => value.itemId, StringComparer.Ordinal)
                .ToList();
            route = new FactionRouteState
            {
                factionId = factionId,
                kind = kind,
                status = FactionRouteStatus.Traveling,
                path = path.Select(value => FactionHexCoordSaveData.From(
                    new FactionHexCoord(value.Q, value.R))).ToList(),
                strength = Mathf.Clamp(strength, 1, 100),
                createdDay = currentDay,
                estimatedArrivalDay = currentDay
                    + Mathf.CeilToInt(steps * SecondsPerHex / 180f),
                cargo = frozenCargo,
                settlement = settlement,
                cargoDelivery = new FactionRouteCargoDeliveryReceipt
                {
                    state = frozenCargo.Count > 0
                        ? FactionRouteCargoDeliveryState.Ready
                        : FactionRouteCargoDeliveryState.NotApplicable
                }
            };
            routeId = domain.AddRoute(route);
            if (hasSettlement
                && !string.Equals(
                    routeId,
                    expectedSettlementRouteId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Faction route identity changed after settlement outcome reservation.");
            }
            tradeSettlementRecovery.CompletePublication();
            if (hasSettlement)
            {
                MigratedProducerOutcomeCommitResult committed =
                    CommitRouteSettlementOutcome(
                        settlementOutcome,
                        route,
                        settlement,
                        settlementIdentity);
                if (!committed.DurablyCommitted)
                {
                    RollbackRouteSettlement(
                        factionBefore,
                        sessionBefore,
                        treasuryBefore,
                        settlementOutcome);
                    routeId = string.Empty;
                    message = "세력 경로 정산 결과 기록이 거절되어 생성을 복원했습니다.";
                    return false;
                }
            }
        }
        catch
        {
            if (hasSettlement)
            {
                RollbackRouteSettlement(
                    factionBefore,
                    sessionBefore,
                    treasuryBefore,
                    settlementOutcome);
                routeId = string.Empty;
            }
            throw;
        }
        if (kind == FactionRouteKind.Reinforcement)
            campaignCommand.ApplyFactionChange(factionId, 0, 0, -1);
        string paymentText = settlement.state == FactionRouteSettlementState.Paid
            ? $" · 선결제 {settlement.paymentGold} gold"
            : string.Empty;
        message = $"{DisplayName(factionId)} 경로 출발 · ETA Day {route.estimatedArrivalDay}{paymentText}";
        return true;
    }

    private static FactionRouteSettlementReceipt CreatePaidSettlementReceipt(
        FactionRouteQuoteSnapshot quote,
        int operationSequence,
        EconomyTransactionContext context,
        EconomyTransactionRecord receipt)
    {
        if (quote == null
            || receipt == null
            || quote.PaymentGold <= 0
            || !receipt.succeeded
            || receipt.kind != context.kind
            || string.IsNullOrWhiteSpace(receipt.transactionId)
            || receipt.amount != -quote.PaymentGold
            || receipt.balanceAfter != receipt.balanceBefore - quote.PaymentGold
            || !string.Equals(
                receipt.sourceId,
                context.sourceId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.targetId,
                context.targetId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Faction trade debit receipt does not match its canonical quote.");
        }

        return new FactionRouteSettlementReceipt
        {
            state = FactionRouteSettlementState.Paid,
            capabilityId = quote.CapabilityId,
            capabilityVersion = quote.CapabilityVersion,
            operationSequence = operationSequence,
            cargoAuthoredGold = quote.CargoAuthoredGold,
            paymentGold = quote.PaymentGold,
            quoteLines = quote.QuoteLines
                .Select(value => value.Clone())
                .ToList(),
            sourceDigest = quote.SourceDigest,
            quoteDigest = quote.QuoteDigest,
            transactionId = receipt.transactionId ?? string.Empty,
            transactionSourceId = receipt.sourceId ?? string.Empty,
            transactionTargetId = receipt.targetId ?? string.Empty,
            balanceBefore = receipt.balanceBefore,
            balanceAfter = receipt.balanceAfter
        };
    }

    private static FactionRouteSettlementReceipt
        CreateAllianceBenefitSettlementReceipt(
            FactionRouteQuoteSnapshot quote,
            int operationSequence,
            string authorityDigest,
            long debitMilliEwu,
            long balanceBeforeMilliEwu,
            long balanceAfterMilliEwu)
    {
        if (quote == null
            || quote.RouteKind != FactionRouteKind.SupplyCaravan
            || quote.PaymentGold != 0
            || quote.CargoAuthoredGold <= 0
            || operationSequence <= 0
            || string.IsNullOrEmpty(authorityDigest)
            || debitMilliEwu <= 0
            || balanceBeforeMilliEwu < debitMilliEwu
            || balanceAfterMilliEwu
                != balanceBeforeMilliEwu - debitMilliEwu)
        {
            throw new InvalidOperationException(
                "Faction supply budget reservation does not match its canonical quote.");
        }

        return new FactionRouteSettlementReceipt
        {
            state = FactionRouteSettlementState.AllianceBenefitDebited,
            capabilityId = quote.CapabilityId,
            capabilityVersion = quote.CapabilityVersion,
            operationSequence = operationSequence,
            cargoAuthoredGold = quote.CargoAuthoredGold,
            paymentGold = 0,
            quoteLines = quote.QuoteLines
                .Select(value => value.Clone())
                .ToList(),
            sourceDigest = quote.SourceDigest,
            quoteDigest = quote.QuoteDigest,
            allianceBenefitAuthorityDigest = authorityDigest,
            allianceBenefitReservationId =
                $"faction-alliance-benefit:{operationSequence:D8}",
            allianceBenefitDebitMilliEwu = debitMilliEwu,
            allianceBenefitBalanceBeforeMilliEwu = balanceBeforeMilliEwu,
            allianceBenefitBalanceAfterMilliEwu = balanceAfterMilliEwu
        };
    }

    private void CompleteRoute(FactionRouteState route)
    {
        if (route.kind is FactionRouteKind.TradeCaravan
            or FactionRouteKind.SupplyCaravan
            or FactionRouteKind.Restitution)
        {
            TryDeliverCargo(route);
        }
        else if (route.kind == FactionRouteKind.Reinforcement)
        {
            MaterializeReinforcements(route);
        }
    }

    private bool TryDeliverCargo(FactionRouteState route)
    {
        if (route?.cargoDelivery?.state
            == FactionRouteCargoDeliveryState.Delivered)
        {
            return true;
        }
        if (route?.cargoDelivery?.state
            == FactionRouteCargoDeliveryState.Publishing)
        {
            if (!pendingCargo.TryGetValue(
                    route.routeId,
                    out PendingFactionCargoPublication pending))
            {
                throw new InvalidOperationException(
                    $"Faction route '{route.routeId}' lost its exact cargo publication transaction.");
            }
            return TryCommitCargo(route, pending);
        }
        if (route?.cargoDelivery?.state
                != FactionRouteCargoDeliveryState.Ready
            || !dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            return false;
        }

        PhysicalItemExactSourcePublicationPlan plan =
            CreateCargoPublicationPlan(route, dropoff);
        if (!outcomeTransactions.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt,
                plan.BatchCommitId,
                Math.Max(1, currentDay),
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out _))
        {
            return false;
        }
        DungeonPhysicalItemSaveData physicalItemsBefore;
        try
        {
            physicalItemsBefore = itemRuntime.Capture();
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            throw;
        }
        PhysicalItemExactSourcePublicationTransaction transaction;
        try
        {
            if (!exactSources.TryPrepare(
                    plan,
                    out transaction,
                    out _))
            {
                outcomeTransactions.Cancel(prepared);
                return false;
            }
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            throw;
        }

        PendingFactionCargoPublication created = new()
        {
            Plan = plan,
            Transaction = transaction,
            ExpectedReceipt = ProjectCargoReceipt(plan, transaction),
            BeforeReceipt = route.cargoDelivery.Clone(),
            PhysicalItemsBefore = physicalItemsBefore,
            Outcome = prepared
        };
        try
        {
            pendingCargo.Add(route.routeId, created);
            route.cargoDelivery.state =
                FactionRouteCargoDeliveryState.Publishing;
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            if (!exactSources.TryRollback(
                    transaction,
                    CargoOutcomeRollbackReason,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Faction cargo prepare failed and its physical candidate could not roll back: "
                    + rollbackFailure);
            }
            itemRuntime.Restore(physicalItemsBefore);
            route.cargoDelivery = created.BeforeReceipt.Clone();
            pendingCargo.Remove(route.routeId);
            throw;
        }
        return TryCommitCargo(route, created);
    }

    private bool TryCommitCargo(
        FactionRouteState route,
        PendingFactionCargoPublication pending)
    {
        if (pending?.ExpectedReceipt == null)
        {
            throw new InvalidOperationException(
                $"Faction route '{route?.routeId}' has no prebuilt durable cargo receipt.");
        }
        bool rollbackAttempted = false;
        try
        {
            if (!pending.OutcomeCommitted)
            {
                domain.MarkCargoDelivered(route, pending.ExpectedReceipt);
                MigratedProducerOutcomeCommitResult committed =
                    CommitRouteCargoOutcome(
                        pending.Outcome,
                        route,
                        pending.ExpectedReceipt);
                if (!committed.DurablyCommitted)
                {
                    rollbackAttempted = true;
                    RollbackPreparedCargo(route, pending);
                    return false;
                }
                pending.OutcomeCommitted = true;
                route.cargoDelivery = pending.BeforeReceipt.Clone();
                route.cargoDelivery.state =
                    FactionRouteCargoDeliveryState.Publishing;
            }

            if (!exactSources.TryCommitReleased(
                    pending.Transaction,
                    FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned,
                    CargoReleaseReason,
                    out PhysicalItemExactSourcePublicationReceipt physicalReceipt,
                    out _))
            {
                return false;
            }
            if (!CargoReceiptMatches(pending.ExpectedReceipt, physicalReceipt))
            {
                throw new InvalidOperationException(
                    $"Faction route '{route.routeId}' exact cargo receipt drifted after publication.");
            }

            domain.MarkCargoDelivered(route, pending.ExpectedReceipt);
            pendingCargo.Remove(route.routeId);
            return true;
        }
        catch
        {
            if (!pending.OutcomeCommitted && !rollbackAttempted)
            {
                outcomeTransactions.Cancel(pending.Outcome);
                RollbackPreparedCargo(route, pending);
            }
            else if (route?.cargoDelivery != null)
            {
                route.cargoDelivery = pending.BeforeReceipt.Clone();
                route.cargoDelivery.state =
                    FactionRouteCargoDeliveryState.Publishing;
            }
            throw;
        }
    }

    private static PhysicalItemExactSourcePublicationPlan
        CreateCargoPublicationPlan(
            FactionRouteState route,
            Vector2Int dropoff)
    {
        if (route == null
            || route.cargo == null
            || route.cargo.Count == 0)
        {
            throw new InvalidOperationException(
                "Faction cargo publication requires a frozen non-empty vector.");
        }

        FacilityBufferPlannedOutputSlice[] outputs = route.cargo
            .Select((line, index) =>
            {
                if (line == null
                    || string.IsNullOrWhiteSpace(line.itemId)
                    || line.amount <= 0)
                {
                    throw new InvalidOperationException(
                        "Faction cargo contains an invalid frozen line.");
                }
                ItemDefinitionId itemId = (ItemDefinitionId)line.itemId;
                return new FacilityBufferPlannedOutputSlice(
                    $"cargo:{index:D4}:{line.itemId}",
                    PhysicalItemMassSubject.ForDefinition(itemId),
                    ResolveCargoDeliveryQuantity(
                        line.amount,
                        route.strength));
            })
            .ToArray();
        return new PhysicalItemExactSourcePublicationPlan(
            CargoSourceOwnerDomain,
            route.routeId,
            dropoff,
            outputs);
    }

    private static int ResolveCargoDeliveryQuantity(int amount, int strength)
    {
        long scaled = checked((long)amount * strength);
        long whole = scaled / 100L;
        long remainder = scaled % 100L;
        if (remainder > 50L
            || (remainder == 50L && (whole & 1L) != 0L))
        {
            whole = checked(whole + 1L);
        }
        return Math.Max(1, checked((int)whole));
    }

    private static FactionRouteCargoDeliveryReceipt ProjectCargoReceipt(
        PhysicalItemExactSourcePublicationPlan plan,
        PhysicalItemExactSourcePublicationTransaction transaction)
    {
        List<ProductionDomainPublishedStackSaveData> stacks = transaction
            .PreparedStacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => new ProductionDomainPublishedStackSaveData
            {
                outputLineId = value.OutputLineId,
                itemId = value.ItemDefinitionId.Value,
                itemInstanceId = value.ItemInstanceId ?? string.Empty,
                stackId = value.StackId,
                quantity = value.Quantity,
                massGrams = value.MassGrams
            })
            .ToList();
        long totalMass = 0L;
        foreach (ProductionDomainPublishedStackSaveData stack in stacks)
        {
            totalMass = checked(totalMass + stack.massGrams);
        }
        return new FactionRouteCargoDeliveryReceipt
        {
            state = FactionRouteCargoDeliveryState.Delivered,
            batchCommitId = plan.BatchCommitId,
            destinationId = plan.DestinationId,
            outcomeFingerprint = plan.OutcomeFingerprint,
            deliveryX = plan.DropPosition.x,
            deliveryY = plan.DropPosition.y,
            totalMassGrams = totalMass,
            stacks = stacks
        };
    }

    private static bool CargoReceiptMatches(
        FactionRouteCargoDeliveryReceipt expected,
        PhysicalItemExactSourcePublicationReceipt actual)
    {
        if (expected == null
            || actual.IsRetained
            || !string.Equals(
                expected.batchCommitId,
                actual.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                expected.destinationId,
                actual.DestinationId,
                StringComparison.Ordinal)
            || expected.totalMassGrams != actual.TotalMassGrams
            || expected.stacks.Count != actual.Stacks.Count)
        {
            return false;
        }
        for (int index = 0; index < expected.stacks.Count; index++)
        {
            ProductionDomainPublishedStackSaveData left =
                expected.stacks[index];
            FacilityBufferPublishedOutputStackReceipt right =
                actual.Stacks[index];
            if (!string.Equals(
                    left.outputLineId,
                    right.OutputLineId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    left.itemId,
                    right.ItemDefinitionId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    left.itemInstanceId,
                    right.ItemInstanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    left.stackId,
                    right.StackId,
                    StringComparison.Ordinal)
                || left.quantity != right.Quantity
                || left.massGrams != right.MassGrams)
            {
                return false;
            }
        }
        return true;
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

    private MigratedProducerOutcomeCommitResult CommitRouteArrivalOutcome(
        in PreparedMigratedProducerOutcome prepared,
        FactionRouteState route) => outcomeTransactions.CommitSingleSubject(
        prepared,
        CreateRouteOutcomeSubject(route),
        "세력 경로 도착: route=" + route.routeId
        + "; faction=" + route.factionId
        + "; kind=" + route.kind
        + "; strength=" + route.strength);

    private MigratedProducerOutcomeCommitResult CommitRouteSettlementOutcome(
        in PreparedMigratedProducerOutcome prepared,
        FactionRouteState route,
        FactionRouteSettlementReceipt receipt,
        string settlementIdentity)
    {
        string quoteLines = string.Join(
            ",",
            receipt.quoteLines.Select(line =>
                line.itemId + ":" + line.amount + "@" + line.unitPriceGold));
        return outcomeTransactions.CommitSingleSubject(
            prepared,
            CreateRouteOutcomeSubject(route),
            "세력 경로 정산: route=" + route.routeId
            + "; faction=" + route.factionId
            + "; operation=" + settlementIdentity
            + "; state=" + receipt.state
            + "; capability=" + receipt.capabilityId
            + "; capability-version=" + receipt.capabilityVersion
            + "; authored-gold=" + receipt.cargoAuthoredGold
            + "; payment-gold=" + receipt.paymentGold
            + "; balance=" + receipt.balanceBefore
            + "->" + receipt.balanceAfter
            + "; transaction=" + receipt.transactionId
            + "; source=" + receipt.transactionSourceId
            + "; target=" + receipt.transactionTargetId
            + "; alliance-reservation="
            + receipt.allianceBenefitReservationId
            + "; alliance-debit-milli-ewu="
            + receipt.allianceBenefitDebitMilliEwu
            + "; alliance-balance-milli-ewu="
            + receipt.allianceBenefitBalanceBeforeMilliEwu
            + "->" + receipt.allianceBenefitBalanceAfterMilliEwu
            + "; source-digest=" + receipt.sourceDigest
            + "; quote=" + receipt.quoteDigest
            + "; lines=" + quoteLines);
    }

    private MigratedProducerOutcomeCommitResult CommitRouteCargoOutcome(
        in PreparedMigratedProducerOutcome prepared,
        FactionRouteState route,
        FactionRouteCargoDeliveryReceipt receipt)
    {
        string stacks = string.Join(
            ",",
            receipt.stacks.Select(stack =>
                stack.outputLineId
                + ":" + stack.itemId
                + ":" + stack.itemInstanceId
                + ":" + stack.stackId
                + ":" + stack.quantity
                + ":" + stack.massGrams));
        return outcomeTransactions.CommitSingleSubject(
            prepared,
            CreateRouteOutcomeSubject(route),
            "세력 경로 화물 인도: route=" + route.routeId
            + "; batch=" + receipt.batchCommitId
            + "; destination=" + receipt.destinationId
            + "; fingerprint=" + receipt.outcomeFingerprint
            + "; stacks=" + receipt.stacks.Count
            + "; mass-grams=" + receipt.totalMassGrams
            + "; position=" + receipt.deliveryX + "," + receipt.deliveryY
            + "; stack-lines=" + stacks);
    }

    private MigratedProducerOutcomeSubject CreateRouteOutcomeSubject(
        FactionRouteState route) => new(
        MigratedProducerOutcomeIds.RouteKind,
        route?.routeId
            ?? throw new ArgumentNullException(nameof(route)),
        route.routeId,
        MigratedProducerOutcomeIds.RouteRole);

    private void RollbackRouteSettlement(
        FactionRouteCreationMutationSnapshot factionSnapshot,
        GameSessionSnapshot sessionSnapshot,
        TreasuryEconomyAggregateState treasurySnapshot,
        in PreparedMigratedProducerOutcome prepared)
    {
        outcomeTransactions.Cancel(prepared);
        domain.RestoreRouteCreationMutationSnapshot(factionSnapshot);
        gameSessionState.Restore(sessionSnapshot);
        treasuryState.Replace(
            treasurySnapshot
            ?? throw new ArgumentNullException(nameof(treasurySnapshot)));
        tradeSettlementRecovery.CompletePublication();
    }

    private void RollbackPreparedCargo(
        FactionRouteState route,
        PendingFactionCargoPublication pending)
    {
        if (!exactSources.TryRollback(
                pending.Transaction,
                CargoOutcomeRollbackReason,
                out string rollbackFailure))
        {
            throw new InvalidOperationException(
                "Faction cargo outcome was rejected but its exact-source transaction could not roll back: "
                + rollbackFailure);
        }
        itemRuntime.Restore(
            pending?.PhysicalItemsBefore
            ?? throw new ArgumentNullException(nameof(pending)));
        route.cargoDelivery = (pending.BeforeReceipt
            ?? throw new InvalidOperationException(
                "Faction cargo rollback receipt is missing.")).Clone();
        pendingCargo.Remove(route.routeId);
    }

    private static void RestoreRouteSnapshot(
        FactionRouteState route,
        FactionRouteState snapshot)
    {
        JsonUtility.FromJsonOverwrite(
            JsonUtility.ToJson(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot))),
            route ?? throw new ArgumentNullException(nameof(route)));
    }

    private void PublishRouteArrivedObserver(FactionRouteState route)
    {
        try
        {
            events.Publish(new FactionRouteArrivedEvent(
                route.routeId,
                route.factionId,
                route.kind,
                route.strength));
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "faction-route-arrived-post-commit-observer:"
                + exception.GetType().Name);
        }
    }

    private MigratedProducerOutcomeCommitResult CommitFactionTrustOutcome(
        in PreparedMigratedProducerOutcome prepared,
        string factionId,
        int previous,
        int current,
        string reason,
        string ownerIdentity)
    {
        string canonicalReason = reason?.Trim() ?? string.Empty;
        return outcomeTransactions.CommitSingleSubject(
            prepared,
            CreateFactionOutcomeSubject(factionId),
            "세력 신뢰 변화: faction=" + factionId
            + "; previous=" + previous
            + "; current=" + current
            + "; reason=" + canonicalReason
            + "; owner=" + ownerIdentity);
    }

    private MigratedProducerOutcomeCommitResult CommitFactionBetrayalOutcome(
        in PreparedMigratedProducerOutcome prepared,
        IReadOnlyList<FactionTrustTransition> transitions,
        string betrayedFactionId,
        string operationId,
        int actualLootValue)
    {
        MigratedProducerOutcomePayloadBuilder builder =
            outcomeTransactions.CreatePayloadBuilder(prepared);
        for (int index = 0; index < transitions.Count; index++)
        {
            FactionTrustTransition transition = transitions[index];
            MigratedProducerOutcomeSubject subject =
                CreateFactionOutcomeSubject(transition.FactionId);
            if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                    subject.EntityId,
                    subject.Role,
                    GameplayParticipationKind.Direct,
                    true,
                    subject.DisplayName))
                || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                    subject.EntityId,
                    prepared.Salience,
                    prepared.Tier,
                    prepared.Tier == NarrativeMemoryTier.Core,
                    false,
                    0)))
            {
                outcomeTransactions.Cancel(prepared);
                return new MigratedProducerOutcomeCommitResult(
                    false,
                    prepared.ResultKey,
                    default,
                    string.Empty,
                    "faction-betrayal-outcome-capacity-invalid");
            }
        }
        string transitionSummary = string.Join(
            ",",
            transitions.Select(transition =>
                transition.FactionId
                + ":" + transition.Previous
                + "->" + transition.Current
                + ":" + (string.Equals(
                    transition.FactionId,
                    betrayedFactionId,
                    StringComparison.Ordinal)
                    ? "betrayed"
                    : "witness")));
        if (!builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.SummaryFact,
                "세력 배신 신뢰 변화: target=" + betrayedFactionId
                + "; operation=" + operationId
                + "; affected=" + transitions.Count
                + "; loot-value=" + actualLootValue
                + "; transitions=" + transitionSummary)))
        {
            outcomeTransactions.Cancel(prepared);
            return new MigratedProducerOutcomeCommitResult(
                false,
                prepared.ResultKey,
                default,
                string.Empty,
                "faction-betrayal-outcome-fact-capacity-invalid");
        }
        return outcomeTransactions.Commit(prepared, builder.Build());
    }

    private MigratedProducerOutcomeSubject CreateFactionOutcomeSubject(
        string factionId) => new(
        MigratedProducerOutcomeIds.FactionKind,
        factionId,
        DisplayName(factionId),
        MigratedProducerOutcomeIds.FactionRole);

    private static string FormatBetrayalOperationId(
        string factionId,
        int day) => $"faction-betrayal:{factionId}:{day}";

    private void RestoreCampaignFactions(
        FactionCampaignWorldSaveData snapshot)
    {
        campaignPersistence.PublishFactions(
            campaignPersistence.PrepareFactions(snapshot));
    }

    private void RestoreFactionSnapshots(
        IReadOnlyDictionary<string, DungeonFactionState> snapshots)
    {
        foreach (KeyValuePair<string, DungeonFactionState> pair in snapshots)
        {
            if (domain.TryGetFaction(pair.Key, out DungeonFactionState faction))
            {
                RestoreFactionSnapshot(faction, pair.Value);
                ProjectCampaignRelationship(faction);
            }
        }
    }

    private static void RestoreFactionSnapshot(
        DungeonFactionState faction,
        DungeonFactionState snapshot)
    {
        JsonUtility.FromJsonOverwrite(
            JsonUtility.ToJson(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot))),
            faction ?? throw new ArgumentNullException(nameof(faction)));
    }

    private void PublishTrustChangedObserver(
        string factionId,
        int previous,
        int current,
        string reason)
    {
        try
        {
            events.Publish(new FactionTrustChangedEvent(
                factionId,
                previous,
                current,
                reason));
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "faction-trust-post-commit-observer:"
                + exception.GetType().Name);
        }
    }

    private FactionDefinitionSnapshot FindDefinition(string factionId)
    {
        return definitions.FirstOrDefault(value =>
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

using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.ServiceRooms;
using VContainer.Unity;

public interface IServiceSessionRuntime :
    IServiceAvailabilityQuery,
    IServiceDemandPolicyRuntime
{
    int Version { get; }
    IReadOnlyList<ServiceSessionSnapshot> ActiveSessions { get; }
    bool IsReplacedFacility(BuildableObject facility) => false;
    bool TryGetSessionSnapshot(
        string sessionId,
        out ServiceSessionSnapshot session)
    {
        session = ActiveSessions?.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.SessionId,
                sessionId,
                StringComparison.Ordinal));
        return session != null;
    }
    ServiceHubSnapshot GetHubSnapshot(BuildableObject hub);
    ServiceModeChangeResult SetMode(
        BuildableObject hub,
        ServiceOperationMode mode);
    ServiceModeChangeResult SwitchToDirect(BuildableObject hub);
    bool TryBeginSession(
        ServiceSessionRequest request,
        out ServiceSessionSnapshot session,
        out DomainFailure failure);
    bool TrySetStage(
        string sessionId,
        ServiceSessionStage stage,
        out DomainFailure failure);
    bool TryCompleteSession(
        string sessionId,
        out ServiceSessionSnapshot completed,
        out DomainFailure failure);
    bool CancelSession(string sessionId, string reason);
    ServiceRoomsSaveData Capture();
    ServiceRoomsRestoreCandidate PrepareRestoreCandidate(
        ServiceRoomsSaveData saveData);
    void PublishRestoreCandidate(ServiceRoomsRestoreCandidate candidate);
}

public sealed class ServiceRoomsRestoreCandidate
{
    public ServiceRoomsRestoreCandidate(ServiceSessionAggregate aggregate)
    {
        Aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
    }

    public ServiceSessionAggregate Aggregate { get; }
}

public interface IServiceRoomResearchQuery
{
    bool IsCompleted(string researchId);
}

public sealed class BlueprintServiceRoomResearchQuery :
    IServiceRoomResearchQuery
{
    private readonly BlueprintResearchRuntime research;

    public BlueprintServiceRoomResearchQuery(
        ProgressionSceneRuntimeReferences progressionRuntimes)
    {
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                "Blueprint research runtime is not loaded.");
    }

    public bool IsCompleted(string researchId) =>
        !string.IsNullOrWhiteSpace(researchId)
        && research.State.Projects.IsCompleted(
            new ResearchProjectId(researchId));
}

public sealed class ServiceSessionRuntime :
    IServiceSessionRuntime,
    ISurvivalServiceSessionCapability,
    IDungeonSaveRestoreCompletedHook,
    ITickable,
    IDisposable
{
    public sealed class Dependencies
    {
        public Dependencies(
            IGameClock clock,
            IGameMoneyAccount money,
            IPowerInfrastructureQuery power,
            IServiceRoomResearchQuery research,
            ICoreSessionRulesProvider rulesProvider,
            IGameSessionStateProvider gameDataProvider,
            IDomainFactPublisher facts,
            IDomainFactRecoveryPublisher factRecovery)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Money = money ?? throw new ArgumentNullException(nameof(money));
            Power = power ?? throw new ArgumentNullException(nameof(power));
            Research = research
                ?? throw new ArgumentNullException(nameof(research));
            Rules = (rulesProvider
                    ?? throw new ArgumentNullException(nameof(rulesProvider)))
                .CoreSessionRules
                ?? throw new InvalidOperationException(
                    "Core-session rules are not authored.");
            GameDataProvider = gameDataProvider
                ?? throw new ArgumentNullException(nameof(gameDataProvider));
            Facts = facts ?? throw new ArgumentNullException(nameof(facts));
            FactRecovery = factRecovery ?? throw new ArgumentNullException(nameof(factRecovery));
        }

        public IGameClock Clock { get; }
        public IGameMoneyAccount Money { get; }
        public IPowerInfrastructureQuery Power { get; }
        public IServiceRoomResearchQuery Research { get; }
        public CoreSessionRulesDefinition Rules { get; }
        public IGameSessionStateProvider GameDataProvider { get; }
        public IDomainFactPublisher Facts { get; }
        public IDomainFactRecoveryPublisher FactRecovery { get; }
    }

    private readonly IBuildingWorldQuery buildings;
    private readonly IServiceRoomLinkRuntime links;
    private readonly IServiceProcessCatalog processCatalog;
    private readonly IGameClock clock;
    private readonly IGameMoneyAccount money;
    private readonly IIdempotentGameMoneyAccount idempotentMoney;
    private readonly IGameMoneyTransactionQuery moneyTransactions;
    private readonly IPowerInfrastructureQuery power;
    private readonly IServiceRoomResearchQuery research;
    private readonly CoreSessionRulesDefinition rules;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher factRecovery;
    private float nextFactRecoveryAt;
    private string lastModeFailure = string.Empty;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly ICharacterWorldPersistenceIdentityQuery persistentCharacters;
    private readonly ServiceHubSubscriptionRegistry<BuildableObject>
        hubSubscriptions;
    private readonly IUiClock uiClock;
    private int projectedRestoreRevision;
    private IFacilityEvolutionModifierQuery evolutionModifiers;
    private ICharacterWorldSaveService characterWorldSave;

    private ServiceSessionAggregate Aggregate => aggregateRootStore.GetOrCreate(
        () => new ServiceSessionAggregate());

    public ServiceSessionRuntime(
        IUiClock uiClock,
        IBuildingWorldQuery buildings,
        IServiceRoomLinkRuntime links,
        IServiceProcessCatalog processCatalog,
        Dependencies dependencies,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        ICharacterWorldPersistenceIdentityQuery persistentCharacters)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.processCatalog = processCatalog
            ?? throw new ArgumentNullException(nameof(processCatalog));
        dependencies = dependencies
            ?? throw new ArgumentNullException(nameof(dependencies));
        clock = dependencies.Clock;
        money = dependencies.Money;
        idempotentMoney = money as IIdempotentGameMoneyAccount;
        moneyTransactions = money as IGameMoneyTransactionQuery;
        power = dependencies.Power;
        research = dependencies.Research;
        rules = dependencies.Rules;
        gameDataProvider = dependencies.GameDataProvider;
        facts = dependencies.Facts;
        factRecovery = dependencies.FactRecovery;
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.persistentCharacters = persistentCharacters
            ?? throw new ArgumentNullException(nameof(persistentCharacters));
        hubSubscriptions = new ServiceHubSubscriptionRegistry<BuildableObject>(
            (hub, handler) => hub.OnBuildingDestroyed += handler,
            (hub, handler) => hub.OnBuildingDestroyed -= handler);
    }

    [VContainer.Inject]
    public void InjectFacilityEvolutionModifiers(
        IFacilityEvolutionModifierQuery value,
        ICharacterWorldSaveService characterWorldSave)
    {
        evolutionModifiers = value
            ?? throw new ArgumentNullException(nameof(value));
        this.characterWorldSave = characterWorldSave
            ?? throw new ArgumentNullException(nameof(characterWorldSave));
    }

    public int Version => Aggregate.Version;

    public IReadOnlyList<ServiceSessionSnapshot> ActiveSessions =>
        Aggregate.ActiveSessions;

    public bool TryGetSessionSnapshot(
        string sessionId,
        out ServiceSessionSnapshot session) =>
        Aggregate.TryGetSession(sessionId, out session);

    public bool IsReplacedFacility(BuildableObject facility) => facility != null
        && facility.PersistentInstanceId.IsValid
        && (buildings.Buildings ?? Array.Empty<BuildableObject>()).Any(value =>
            value != null && !ReferenceEquals(value, facility)
            && string.Equals(value.PersistentInstanceId.Value,
                facility.PersistentInstanceId.Value, StringComparison.Ordinal));

    public void Tick()
    {
        if (uiClock.Time >= nextFactRecoveryAt)
        {
            nextFactRecoveryAt = uiClock.Time + 0.5f;
            RecoverPendingModeFacts();
        }
        ResumePendingTerminals();
        int publishedRevision = aggregateRootStore.PublishedRestoreRevision;
        if (projectedRestoreRevision == publishedRevision)
        {
            return;
        }

        projectedRestoreRevision = publishedRevision;
        SynchronizeHubSubscriptions();
    }

    public void Dispose() => hubSubscriptions.Clear();

    public void OnRestoreCompleted()
    {
        // All detached owners have been validated/published at this point.
        // Restore occupancy for the entire set before any coroutine can admit
        // another customer, and never replay the entry charge.
        var bindings = new List<(Facility facility, CharacterActor actor, FacilityUseExecutionState state)>();
        var shopBindings = new List<(Shop shop, CharacterActor actor, ShopUseExecutionState state)>();
        foreach (Facility facility in (buildings.Buildings ?? Array.Empty<BuildableObject>()).OfType<Facility>())
        foreach (FacilityUseExecutionState state in facility.FacilityState.activeUses.ToArray())
        {
            if (characterWorldSave == null
                || !characterWorldSave.TryGetRestoredActor(state.actorId, out CharacterActor actor)
                || actor == null || !actor.TryGetAbility(out AbilityShopping _)
                || !facility.TryRestoreActiveUse(actor.BuildingVisitor, out _))
                throw new InvalidOperationException("Facility execution restore binding is unavailable: " + state.operationId);
            bindings.Add((facility, actor, state));
        }
        foreach (Shop shop in (buildings.Buildings ?? Array.Empty<BuildableObject>()).OfType<Shop>())
        foreach (ShopUseExecutionState state in shop.InteractionState.activeUses.ToArray())
        {
            if (characterWorldSave == null
                || !characterWorldSave.TryGetRestoredActor(state.actorId, out CharacterActor actor)
                || actor == null || !actor.TryGetAbility(out AbilityShopping _)
                || !(Aggregate.TryGetSession(state.serviceSessionId, out var cleanupSession)
                    && state.MatchesCancelledSession(cleanupSession, shop.RequirePersistentInstanceId().Value))
                    && !shop.TryRestoreActiveUse(actor.BuildingVisitor, out _))
                throw new InvalidOperationException("Shop execution restore binding is unavailable: " + state.operationId);
            shopBindings.Add((shop, actor, state));
        }
        SynchronizeHubSubscriptions();
        foreach (var binding in bindings)
            binding.actor.GetAbility<AbilityShopping>().ResumeFacilityUse(binding.facility, binding.state);
        foreach (var binding in shopBindings)
            binding.actor.GetAbility<AbilityShopping>().ResumeShopUse(binding.shop, binding.state);
    }

    public bool IsAdvertisingEnabled(ServiceCategory category) =>
        Aggregate.IsAdvertisingEnabled(category);

    public void SetAdvertisingEnabled(ServiceCategory category, bool enabled)
    {
        Aggregate.SetAdvertisingEnabled(category, enabled);
    }

    public ServiceAvailabilitySnapshot GetAvailability(ServiceCategory category)
    {
        ServiceHubSnapshot[] hubs = GetOperationalHubs(category)
            .Select(GetHubSnapshot)
            .ToArray();
        ServiceHubSnapshot[] accepting = hubs
            .Where(snapshot =>
                snapshot.State != ServiceOperatingState.Suspended
                && snapshot.State != ServiceOperatingState.Closed)
            .ToArray();
        return new ServiceAvailabilitySnapshot
        {
            Category = category,
            State = accepting.Length == 0
                ? hubs.Length == 0
                    ? ServiceOperatingState.Closed
                    : ServiceOperatingState.Suspended
                : HighestState(accepting),
            OperationalHubCount = accepting.Length,
            Capacity = accepting.Sum(snapshot => snapshot.Capacity),
            ActiveSessions = hubs.Sum(snapshot => snapshot.ActiveSessions),
            AdvertisingEnabled = IsAdvertisingEnabled(category),
            BlockedFailure = accepting.Length == 0 && hubs.Length > 0
                ? hubs.Select(snapshot => snapshot.BlockedFailure)
                    .FirstOrDefault(failure => failure.IsFailure)
                : DomainFailure.None
        };
    }

    public bool ShouldAcceptDemand(ServiceCategory category) =>
        GetAvailability(category).AcceptsNewDemand;

    public bool ShouldRecordUnservedDemand(
        ServiceCategory category,
        bool demandWasAdvertised) =>
        demandWasAdvertised
        && IsAdvertisingEnabled(category);

    public ServiceHubSnapshot GetHubSnapshot(BuildableObject hub)
    {
        BuildingServiceHubAbility ability = hub.GetServiceHubAbility();
        if (!IsOperational(hub) || ability == null)
        {
            return new ServiceHubSnapshot
            {
                HubId = GetHubId(hub),
                Hub = hub,
                State = ServiceOperatingState.Closed,
                BlockedFailure = new DomainFailure(
                    FailureCode.ServiceHubUnavailable)
            };
        }

        SubscribeToHub(hub);
        string hubId = GetHubId(hub);
        ServiceOperationMode mode = ResolveMode(hubId);
        bool valid = ValidateMode(hub, ability, mode, out DomainFailure failure);
        IReadOnlyList<ServiceSupportLinkSnapshot> supportLinks =
            links.GetLinks(hub);
        int capacity = ability.BaseCapacity;
        float speedMultiplier = 1f;
        int revenue = ability.directPrice;
        float satisfaction = ability.directSatisfaction;
        if (mode != ServiceOperationMode.Direct)
        {
            foreach (ServiceSupportLinkSnapshot link in supportLinks)
            {
                BuildingServiceSupportAbility support =
                    link.Support.GetServiceSupportAbility();
                if (support == null)
                {
                    continue;
                }

                capacity += Math.Max(0, support.capacity);
                speedMultiplier *= GetSupportSpeedMultiplier(link.Support, support);
                revenue += support.revenueModifier;
                satisfaction += support.satisfactionModifier;
            }
        }

        int active = CountActiveSessions(hubId);
        return new ServiceHubSnapshot
        {
            HubId = hubId,
            Hub = hub,
            Category = ability.serviceCategory,
            Mode = mode,
            State = valid ? ToOperatingState(mode) : ServiceOperatingState.Suspended,
            Capacity = Math.Max(1, capacity),
            ActiveSessions = active,
            EstimatedWaitSeconds = active <= 0
                ? 0f
                : active * Math.Max(0.1f, hub.Facility?.useDuration ?? 1f)
                    / Math.Max(0.01f, speedMultiplier),
            ExpectedRevenue = Math.Max(0, revenue),
            ExpectedSatisfaction = satisfaction,
            BlockedFailure = failure,
            Supports = supportLinks
        };
    }

    public ServiceModeChangeResult SetMode(
        BuildableObject hub,
        ServiceOperationMode mode)
    {
        BuildingServiceHubAbility ability = hub.GetServiceHubAbility();
        string hubId = GetHubId(hub);
        ServiceOperationMode previous = ResolveMode(hubId);
        if (!IsOperational(hub) || ability == null)
        {
            return Failure(
                previous,
                mode,
                new DomainFailure(FailureCode.ServiceHubUnavailable));
        }

        if (mode == previous)
        {
            return Success(previous, mode);
        }

        if (!ability.Allows(mode))
        {
            return Failure(
                previous,
                mode,
                new DomainFailure(FailureCode.ServiceModeUnsupported));
        }

        if (!ValidateResearch(
                ability.serviceCategory,
                mode,
                out DomainFailure failure)
            || !ValidateMode(hub, ability, mode, out failure))
        {
            return Failure(previous, mode, failure);
        }

        if (!TryGetCurrentAbsoluteDay(out int absoluteDay)
            || Aggregate.HasPendingModeFact(hubId)
            || Aggregate.PendingModeFacts.Count >= ServiceModeChangedFact.MaximumPending)
            return Failure(previous, mode, new DomainFailure(FailureCode.ServiceHubUnavailable,
                "service-mode-day-or-observation-unavailable"));

        var fact = new ServiceModeChangedFact(new ServiceModeChangedFactData
        {
            hubId = hubId, hubName = GetHubDisplayName(hub, hubId),
            previousMode = previous, requestedMode = mode, absoluteDay = absoluteDay
        });
        if (!facts.TryReserve(fact, out var lease, out string reserveFailure))
            return Failure(previous, mode, new DomainFailure(FailureCode.ServiceHubUnavailable, reserveFailure));
        string publicationKey = lease.PublicationKey;
        if (string.IsNullOrWhiteSpace(publicationKey))
        {
            facts.Cancel(lease);
            return Failure(previous, mode, new DomainFailure(FailureCode.ServiceHubUnavailable,
                "service-mode-publication-reference-missing"));
        }

        var before = Aggregate.CaptureModeMutationSnapshot(hubId);
        try
        {
            if (!Aggregate.SetMode(hubId, mode, out _))
            {
                facts.Cancel(lease);
                return Success(previous, mode);
            }
            Aggregate.RecordModeFact(new ServiceModePendingFactData
            { publicationKey = publicationKey, fact = fact.Snapshot() });
        }
        catch
        {
            Aggregate.ClearModeFact(hubId, publicationKey);
            Aggregate.RestoreModeMutationSnapshot(before);
            facts.Cancel(lease);
            throw;
        }

        DomainFactDelivery delivery;
        try { delivery = facts.Publish(lease, fact); }
        catch (Exception exception)
        {
            // The effect and its frozen source remain when the commit response is unknown.
            delivery = DomainFactDelivery.Uncertain("service-mode-publication-in-doubt:" + exception.GetType().Name);
        }
        finally { facts.Cancel(lease); }
        if (!delivery.MayBeCommitted)
        {
            Aggregate.ClearModeFact(hubId, publicationKey);
            Aggregate.RestoreModeMutationSnapshot(before);
            return Failure(previous, mode, new DomainFailure(FailureCode.ServiceHubUnavailable, delivery.Detail));
        }
        if (delivery.Completed && delivery.OperationKey == publicationKey)
            Aggregate.ClearModeFact(hubId, publicationKey);
        else ReportModeFactFailure(delivery.Detail);

        try
        {
            SubscribeToHub(hub);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            UnityEngine.Debug.LogError(
                "service-mode-change-post-commit-subscription:"
                + exception.GetType().Name);
        }
        return Success(previous, mode);
    }

    public ServiceModeChangeResult SwitchToDirect(BuildableObject hub) =>
        SetMode(hub, ServiceOperationMode.Direct);

    public bool TryBeginSession(
        ServiceSessionRequest request,
        out ServiceSessionSnapshot session,
        out DomainFailure failure)
    {
        session = null;
        failure = DomainFailure.None;
        BuildableObject hub = request?.Hub;
        BuildingServiceHubAbility ability = hub.GetServiceHubAbility();
        if (!IsOperational(hub) || ability == null)
        {
            failure = new DomainFailure(FailureCode.ServiceClosed);
            return false;
        }

        string processId = request?.ProcessId?.Trim() ?? string.Empty;
        if (processId.Length == 0)
        {
            failure = new DomainFailure(FailureCode.ServiceProcessIdMissing);
            return false;
        }
        if (!ability.SupportsProcess(processId)
            || !processCatalog.TryGet(processId, out ServiceProcessSO process)
            || process.ServiceCategory != ability.serviceCategory
            || !string.Equals(
                process.OwnerHubTag,
                ability.ServiceHubTag,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ServiceHubUnavailable);
            return false;
        }

        ServiceHubSnapshot hubState = GetHubSnapshot(hub);
        if (hubState.State == ServiceOperatingState.Suspended)
        {
            failure = hubState.BlockedFailure;
            return false;
        }

        string actorId = request.Actor?.BuildingCharacterId.Value
            ?? string.Empty;

        if (!process.TryGetContract(
                hubState.Mode,
                out ServiceModeProcessContract modeContract))
        {
            failure = new DomainFailure(
                FailureCode.ServiceProcessContractMissing,
                process.ProcessId,
                hubState.Mode.ToString());
            return false;
        }

        ServiceSessionContractSnapshot contract =
            CreateContract(
                hubState,
                ability,
                process,
                modeContract,
                request.IsInternalActor);
        return Aggregate.TryBegin(
            new ServiceSessionBeginCommand
            {
                HubId = hubState.HubId,
                ActorId = actorId,
                ProcessId = processId,
                Category = ability.serviceCategory,
                Capacity = hubState.Capacity,
                StartedAt = Aggregate.GetTimelineTime(clock.Time),
                AdvertisedDemand = request.AdvertisedDemand,
                Contract = contract
            },
            out session,
            out failure);
    }

    public bool TrySetStage(
        string sessionId,
        ServiceSessionStage stage,
        out DomainFailure failure)
    {
        return Aggregate.TrySetStage(
            sessionId,
            stage,
            Aggregate.GetTimelineTime(clock.Time),
            out failure);
    }

    public bool TryCompleteSession(
        string sessionId,
        out ServiceSessionSnapshot completed,
        out DomainFailure failure)
    {
        completed = null;
        if (!Aggregate.TryGetSession(
                sessionId,
                out ServiceSessionSnapshot active)
            || !active.IsActive)
        {
            failure = new DomainFailure(FailureCode.ServiceSessionMissing);
            return false;
        }
        if (!TryGetCurrentAbsoluteDay(out int absoluteDay))
        {
            failure = new DomainFailure(FailureCode.ServiceHubUnavailable);
            return false;
        }
        float completedAt = Aggregate.GetTimelineTime(clock.Time);
        string terminalHubName = ResolveHubDisplayName(active.HubId);
        if (!TryReserveTerminal(active, ServiceSessionStage.Completed, absoluteDay, terminalHubName,
                out DomainFactLease reserved, out string publicationKey, out _))
        {
            failure = new DomainFailure(FailureCode.ServiceHubUnavailable);
            return false;
        }
        ServiceSessionCompletionTransition transition;
        try
        {
            if (!Aggregate.TryComplete(
                    sessionId,
                    completedAt,
                    absoluteDay,
                    out transition,
                    out failure))
            {
                facts.Cancel(reserved);
                return false;
            }
            if (!Aggregate.TryBindTerminalObservation(sessionId, publicationKey, terminalHubName))
                throw new InvalidOperationException("service-terminal-binding-rejected-after-completion");
        }
        catch
        {
            facts.Cancel(reserved);
            throw;
        }
        if (!TryResumeTerminal(
                transition.Completed.SessionId,
                reserved,
                out string pendingReason))
        {
            UnityEngine.Debug.LogError(
                "service-session-terminal-pending:" + sessionId + ":"
                + pendingReason);
        }
        Aggregate.TryGetSession(sessionId, out completed);
        failure = DomainFailure.None;
        return true;
    }

    public bool CancelSession(string sessionId, string reason)
    {
        if (!Aggregate.TryGetSession(
                sessionId,
                out ServiceSessionSnapshot active)
            || !active.IsActive
            || !TryGetCurrentAbsoluteDay(out int absoluteDay))
        {
            return false;
        }
        float cancelledAt = Aggregate.GetTimelineTime(clock.Time);
        string terminalHubName = ResolveHubDisplayName(active.HubId);
        if (!TryReserveTerminal(active, ServiceSessionStage.Cancelled, absoluteDay, terminalHubName,
                out DomainFactLease reserved, out string publicationKey, out _))
        {
            return false;
        }
        try
        {
            if (!Aggregate.CancelSession(
                    sessionId,
                    reason,
                    cancelledAt,
                    absoluteDay))
            {
                facts.Cancel(reserved);
                return false;
            }
            if (!Aggregate.TryBindTerminalObservation(sessionId, publicationKey, terminalHubName))
                throw new InvalidOperationException("service-terminal-binding-rejected-after-cancellation");
        }
        catch
        {
            facts.Cancel(reserved);
            throw;
        }
        if (!TryResumeTerminal(
                sessionId,
                reserved,
                out string pendingReason))
        {
            UnityEngine.Debug.LogError(
                "service-session-cancellation-pending:" + sessionId + ":"
                + pendingReason);
        }
        return true;
    }

    public ServiceRoomsSaveData Capture()
    {
        ResumePendingTerminals();
        ReconcileCaptureReferences();
        var retainedCleanupSessions = new HashSet<string>(StringComparer.Ordinal);
        foreach (Shop shop in (buildings.Buildings ?? Array.Empty<BuildableObject>()).OfType<Shop>())
        foreach (ShopUseExecutionState use in shop.InteractionState.activeUses)
            if (Aggregate.TryGetSession(use.serviceSessionId, out var session)
                && use.MatchesCancelledSession(session, shop.RequirePersistentInstanceId().Value))
                retainedCleanupSessions.Add(session.SessionId);
        return Aggregate.Capture(Aggregate.GetTimelineTime(clock.Time), retainedCleanupSessions);
    }

    private void ReconcileCaptureReferences()
    {
        HashSet<string> persistentActorIds = persistentCharacters
            .GetPersistentActorIds()
            .Where(id => id.IsValid)
            .Select(id => id.Value)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, BuildableObject> hubsById = GetOperationalHubs(null)
            .ToDictionary(GetHubId, StringComparer.Ordinal);

        foreach (ServiceSessionSnapshot session in Aggregate.ActiveSessions)
        {
            bool validActor = session.ActorId.Length == 0
                || persistentActorIds.Contains(session.ActorId);
            bool validHub = hubsById.TryGetValue(
                session.HubId,
                out BuildableObject hub);
            BuildingServiceHubAbility ability = validHub
                ? hub.GetServiceHubAbility()
                : null;
            bool validProcess = ability != null
                && processCatalog.TryGet(
                    session.ProcessId,
                    out ServiceProcessSO process)
                && ability.SupportsProcess(session.ProcessId)
                && process.ServiceCategory == session.Category
                && string.Equals(
                    process.OwnerHubTag,
                    ability.ServiceHubTag,
                    StringComparison.Ordinal);
            if (validActor && validHub && validProcess)
            {
                continue;
            }

            if (!CancelSession(
                    session.SessionId,
                    "save-reference-invalidated"))
            {
                throw new InvalidOperationException(
                    "Service session could not publish its invalid-reference cancellation: "
                    + session.SessionId);
            }
        }

        Aggregate.RemoveHubModesExcept(
            hubsById.Keys.ToHashSet(StringComparer.Ordinal));
    }

    public ServiceRoomsRestoreCandidate PrepareRestoreCandidate(
        ServiceRoomsSaveData saveData)
    {
        if (saveData == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        if (!restoreWorldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> candidateBuildings)
            || !restoreWorldCandidates.TryGetCharacters(
                out IReadOnlyList<CharacterActor> candidateCharacters))
        {
            throw new InvalidOperationException(
                "Service-rooms restore requires detached facility and character candidates.");
        }

        Dictionary<string, BuildableObject> hubsById = candidateBuildings
            .Where(hub => hub != null && hub.GetServiceHubAbility() != null)
            .ToDictionary(GetHubId, StringComparer.Ordinal);
        HashSet<string> actorIds = candidateCharacters
            .Where(actor => actor != null)
            .Select(actor => actor.Identity?.PersistentId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (ServiceHubModeSaveData hub in saveData.hubs)
        {
            if (!hubsById.TryGetValue(hub.hubId, out BuildableObject building)
                || !building.GetServiceHubAbility().Allows(hub.mode))
            {
                report.AddError(
                    $"Service-rooms restore references missing or incompatible hub '{hub.hubId}'.");
            }
        }
        foreach (ServiceSessionSaveData session in saveData.sessions)
        {
            bool pendingTerminal = session != null
                && session.stage is ServiceSessionStage.Completed
                    or ServiceSessionStage.Cancelled
                && session.terminalPhase
                    is >= ServiceSessionTerminalPhase.Prepared
                    and <= ServiceSessionTerminalPhase.ObservationCommitted;
            if (pendingTerminal)
            {
                // A destroyed hub or departed actor may be the cancellation
                // cause. The frozen terminal owner is self-contained and must
                // remain restorable until its ledger acknowledgement arrives.
                continue;
            }
            bool hubFound = hubsById.TryGetValue(
                session.hubId,
                out BuildableObject hub);
            bool actorFound = session.actorId.Length == 0
                || actorIds.Contains(session.actorId);
            bool processFound = processCatalog.TryGet(
                session.processId,
                out ServiceProcessSO process);
            BuildingServiceHubAbility hubAbility = hubFound
                ? hub.GetServiceHubAbility()
                : null;
            bool processSupported = hubAbility != null
                && hubAbility.SupportsProcess(session.processId);
            bool categoryMatches = processFound
                && process.ServiceCategory == session.category;
            bool ownerTagMatches = processFound
                && hubAbility != null
                && string.Equals(
                    process.OwnerHubTag,
                    hubAbility.ServiceHubTag,
                    StringComparison.Ordinal);
            if (!hubFound
                || !actorFound
                || !processFound
                || !processSupported
                || !categoryMatches
                || !ownerTagMatches)
            {
                report.AddError(
                    $"Service session '{session.sessionId}' references a missing candidate or incompatible process: "
                    + $"hub={session.hubId}; hubFound={hubFound}; "
                    + $"actor={session.actorId}; actorFound={actorFound}; "
                    + $"process={session.processId}; processFound={processFound}; "
                    + $"supported={processSupported}; category={session.category}; "
                    + $"categoryMatches={categoryMatches}; ownerTagMatches={ownerTagMatches}.");
            }
        }
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Service-rooms restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        ValidateFacilityExecutionOwners(candidateBuildings, candidateCharacters, saveData);

        ServiceSessionAggregate restored =
            ServiceSessionAggregate.CreateRestored(
                saveData,
                unchecked(Version + 1));
        return new ServiceRoomsRestoreCandidate(restored);
    }

    private static void ValidateFacilityExecutionOwners(
        IReadOnlyList<BuildableObject> candidateBuildings,
        IReadOnlyList<CharacterActor> candidateCharacters, ServiceRoomsSaveData save)
    {
        var claimedActors = new HashSet<string>(StringComparer.Ordinal);
        var claimedSessions = new HashSet<string>(StringComparer.Ordinal);
        foreach (Facility facility in candidateBuildings.OfType<Facility>())
        {
            if (!facility.FacilityState.IsValid(out string error))
                throw new InvalidOperationException(error);
            if (facility.FacilityState.activeUses.Count > facility.EffectiveCapacity)
                throw new InvalidOperationException("Saved facility executions exceed capacity.");
            foreach (FacilityUseExecutionState state in facility.FacilityState.activeUses)
            {
                CharacterActor actor = candidateCharacters.SingleOrDefault(value => value != null
                    && value.Identity?.PersistentId == state.actorId);
                if (!claimedActors.Add(state.actorId) || actor?.Brain == null
                    || !actor.TryGetAbility(out AbilityShopping _)
                    || !actor.Brain.CanRestoreFacilityUse(state)
                    || state.operationId != "facility-visit:facility=" + facility.RequirePersistentInstanceId().Value
                        + ":use=" + state.operationSequence)
                    throw new InvalidOperationException("Invalid facility execution actor/action/operation: " + state.operationId);
                if (string.IsNullOrEmpty(state.serviceSessionId))
                {
                    if (facility.GetServiceHubAbility() != null)
                        throw new InvalidOperationException("Facility execution is missing its service session.");
                    continue;
                }
                ServiceSessionSaveData session = save.sessions.SingleOrDefault(value =>
                    value.sessionId == state.serviceSessionId);
                if (!claimedSessions.Add(state.serviceSessionId) || session == null
                    || session.actorId != state.actorId
                    || session.hubId != facility.RequirePersistentInstanceId().Value
                    || session.stage >= ServiceSessionStage.Completed)
                    throw new InvalidOperationException("Facility execution service owner mismatch.");
            }
        }
        foreach (Shop shop in candidateBuildings.OfType<Shop>())
        {
            string shopId = shop.RequirePersistentInstanceId().Value;
            if (!shop.InteractionState.IsValid(shopId, out string error))
                throw new InvalidOperationException(error);
            if (shop.InteractionState.activeUses.Count(state => !state.MatchesCancelledSession(
                    save.sessions.SingleOrDefault(value => value.sessionId == state.serviceSessionId)?.ToSnapshot(), shopId))
                > shop.EffectiveCapacity)
                throw new InvalidOperationException("Saved shop executions exceed capacity.");
            foreach (ShopUseExecutionState state in shop.InteractionState.activeUses)
            {
                CharacterActor actor = candidateCharacters.SingleOrDefault(value => value != null
                    && value.Identity?.PersistentId == state.actorId);
                if (!claimedActors.Add(state.actorId) || actor?.Brain == null
                    || !actor.TryGetAbility(out AbilityShopping _)
                    || !actor.Brain.CanRestoreShopUse(state)
                    || state.operationId != ShopUseExecutionState.OperationFor(shopId, state.sequence))
                    throw new InvalidOperationException("Invalid shop execution actor/action/operation: " + state.operationId);
                if (string.IsNullOrEmpty(state.serviceSessionId))
                {
                    if (shop.GetServiceHubAbility() != null)
                        throw new InvalidOperationException("Shop execution is missing its service session.");
                    continue;
                }
                ServiceSessionSaveData session = save.sessions.SingleOrDefault(value =>
                    value.sessionId == state.serviceSessionId);
                if (!claimedSessions.Add(state.serviceSessionId) || session == null
                    || session.actorId != state.actorId
                    || session.hubId != shopId
                    || session.stage >= ServiceSessionStage.Completed
                        && !state.MatchesCancelledSession(session.ToSnapshot(), shopId))
                    throw new InvalidOperationException("Shop execution service owner mismatch.");
            }
        }
        foreach (ServiceSessionSaveData session in save.sessions)
            if (session.terminalPhase == ServiceSessionTerminalPhase.Acknowledged
                && !claimedSessions.Contains(session.sessionId))
                throw new InvalidOperationException("Acknowledged service session has no pending cleanup owner.");
        var purchaseBindings = new HashSet<(string operationId, int cartIndex)>();
        foreach (CharacterActor actor in candidateCharacters)
        {
            if (actor == null
                || !actor.TryGetAbility(out AbilityShopping shopping))
                continue;
            foreach (RetailPurchaseRecoverySaveData receipt in
                     shopping.CapturePendingRetailPurchases())
            {
                if (string.IsNullOrEmpty(receipt.shopUseOperationId)) continue;
                if (!purchaseBindings.Add((
                        receipt.shopUseOperationId,
                        receipt.shopUseCartIndex)))
                    throw new InvalidOperationException(
                        "Duplicate shop purchase recovery binding: "
                        + receipt.shopUseOperationId + ":"
                        + receipt.shopUseCartIndex);

                Shop[] owners = candidateBuildings.OfType<Shop>()
                    .Where(value => value.RequirePersistentInstanceId().Value
                        == receipt.facilityId)
                    .ToArray();
                if (owners.Length != 1)
                    throw new InvalidOperationException(
                        "Shop purchase recovery owner cardinality mismatch: "
                        + receipt.facilityId);
                owners[0].ValidatePurchaseBinding(receipt);
            }
        }
        foreach (Shop shop in candidateBuildings.OfType<Shop>())
            foreach (ShopUseExecutionState use in shop.InteractionState.activeUses)
                for (int index = 0; index < use.cart.Count; index++)
                    if (!string.IsNullOrEmpty(use.cart[index].pendingPurchaseOperationId)
                        && !purchaseBindings.Contains((use.operationId, index)))
                        throw new InvalidOperationException("Shop cart has no matching pending purchase receipt.");
        // Reverse service coverage belongs to ServiceExecutionOwnerSaveValidation:
        // medical execution owners live in later survival sections, not here.
    }

    public void PublishRestoreCandidate(ServiceRoomsRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        candidate.Aggregate.BindRuntimeClock(clock.Time);
        aggregateRootStore.Replace(candidate.Aggregate);
    }

    private void SynchronizeHubSubscriptions()
    {
        hubSubscriptions.Synchronize(
            GetOperationalHubs(null),
            OnHubDestroyed);
    }

    internal void RecoverPendingModeFacts()
    {
        foreach (var pending in Aggregate.PendingModeFacts.Take(8))
        {
            var replies = new DomainFactDelivery[1];
            try
            {
                bool accepted = factRecovery.ReplayBatch(new[]
                {
                    DomainFactReplay.Create(pending.publicationKey, new ServiceModeChangedFact(pending.fact))
                }, replies, out string failure);
                if (accepted && replies[0].Completed && replies[0].OperationKey == pending.publicationKey)
                    Aggregate.ClearModeFact(pending.fact.hubId, pending.publicationKey);
                else ReportModeFactFailure(failure + ":" + replies[0].Detail);
            }
            catch (Exception exception) { ReportModeFactFailure("service-mode-recovery:" + exception.GetType().Name); }
            Aggregate.RotateModeFact(pending.fact.hubId);
        }
    }

    private void ReportModeFactFailure(string failure)
    {
        if (string.IsNullOrEmpty(failure)) failure = "service-mode-publication-unconfirmed";
        if (lastModeFailure == failure) return;
        lastModeFailure = failure;
        UnityEngine.Debug.LogWarning(failure);
    }

    private void ResumePendingTerminals()
    {
        ServiceSessionSnapshot[] pending = Aggregate.PendingTerminalSessions
            .ToArray();
        foreach (ServiceSessionSnapshot session in pending)
        {
            if (!TryResumeTerminal(
                    session.SessionId,
                    default,
                    out string failureReason))
            {
                UnityEngine.Debug.LogError(
                    "service-session-terminal-retry-pending:"
                    + session.SessionId + ":" + failureReason);
            }
        }
    }

    private bool TryReserveTerminal(ServiceSessionSnapshot source, ServiceSessionStage terminalStage,
        int day, string hubName, out DomainFactLease lease, out string publicationKey, out string failure)
    {
        publicationKey = string.Empty;
        if (!facts.TryReserve(ServiceSessionTerminalFact.Propose(source, terminalStage, day, hubName),
                out lease, out failure)) return false;
        publicationKey = lease.PublicationKey;
        if (!string.IsNullOrWhiteSpace(publicationKey) && publicationKey.Trim() == publicationKey) return true;
        facts.Cancel(lease);
        failure = "service-terminal-publication-reference-missing";
        return false;
    }

    private bool TryResumeTerminal(string sessionId, DomainFactLease reserved, out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            if (!Aggregate.TryGetSession(sessionId, out ServiceSessionSnapshot session)
                || session.IsActive || session.TerminalPhase == ServiceSessionTerminalPhase.None)
            { failureReason = "service-session-terminal-owner-missing"; return false; }
            if (session.TerminalPhase == ServiceSessionTerminalPhase.Acknowledged) return true;

            // A core-only prepared transition has not yet been observed. Bind its
            // frozen source before any payment. Already bound source is never rebuilt.
            if (string.IsNullOrEmpty(session.TerminalPublicationKey))
            {
                string name = ResolveHubDisplayName(session.HubId);
                if (!TryReserveTerminal(session, session.Stage, session.TerminalAbsoluteDay, name,
                        out reserved, out string key, out failureReason)
                    || !Aggregate.TryBindTerminalObservation(sessionId, key, name))
                    return false;
                Aggregate.TryGetSession(sessionId, out session);
            }

            bool requiresPayment = session.Stage == ServiceSessionStage.Completed
                && session.Contract.paymentRequired && session.Contract.price > 0;
            if (requiresPayment && session.TerminalPhase == ServiceSessionTerminalPhase.Prepared)
            {
                if (idempotentMoney == null || moneyTransactions == null)
                { failureReason = "service-session-idempotent-money-authority-missing"; return false; }
                string sourceId = ServiceSessionTerminalIdentity.PaymentSourceId(session.SessionId);
                var context = new EconomyTransactionContext(EconomyTransactionKind.GuestServiceIncome,
                    sourceId, session.ActorId, "서비스 세션 수입:" + session.HubId);
                if (!idempotentMoney.TryCreditOnce(session.Contract.price, context, out failureReason)
                    || !moneyTransactions.TryGetSuccessfulTransaction(EconomyTransactionKind.GuestServiceIncome,
                        sourceId, out EconomyTransactionRecord transaction)
                    || !Aggregate.TryRecordPayment(sessionId, transaction.transactionId, transaction.sourceId,
                        transaction.amount, transaction.balanceBefore, transaction.balanceAfter))
                {
                    if (string.IsNullOrWhiteSpace(failureReason))
                        failureReason = "service-session-payment-receipt-mismatch";
                    return false;
                }
                Aggregate.TryGetSession(sessionId, out session);
            }

            var actual = ServiceSessionTerminalFact.Actual(session);
            DomainFactDelivery delivered;
            if (reserved.IsValid)
                delivered = facts.Publish(reserved, actual);
            else
            {
                var results = new DomainFactDelivery[1];
                if (!factRecovery.ReplayBatch(new[] { DomainFactReplay.Create(session.TerminalPublicationKey, actual) },
                        results, out failureReason)) return false;
                delivered = results[0];
            }
            // Rejection or an unknown reply does not undo the terminal transition
            // or one-time credit. Retain the exact source for the next attempt.
            if (delivered.Phase is DomainFactDeliveryPhase.Rejected or DomainFactDeliveryPhase.InDoubt
                || delivered.OperationKey != session.TerminalPublicationKey)
            {
                failureReason = string.IsNullOrWhiteSpace(delivered.Detail)
                    ? "service-session-observation-pending-or-reference-mismatch" : delivered.Detail;
                return false;
            }
            if (!Aggregate.TryRecordObservationCommit(sessionId, delivered.PublicationId, delivered.Fingerprint))
            { failureReason = "service-session-observation-owner-mismatch"; return false; }
            if (!delivered.Completed)
            { failureReason = "service-session-observation-awaiting-delivery"; return false; }
            if (!Aggregate.TryAcknowledgeTerminal(sessionId, delivered.PublicationId, delivered.Fingerprint))
            { failureReason = "service-session-observation-acknowledgement-mismatch"; return false; }
            return true;
        }
        catch (Exception exception)
        {
            failureReason = "service-session-terminal-reply-unknown:" + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        finally
        {
            if (reserved.IsValid) facts.Cancel(reserved);
        }
    }

    private string ResolveHubDisplayName(string hubId)
    {
        BuildableObject hub = GetOperationalHubs(null).FirstOrDefault(
            candidate => string.Equals(
                GetHubId(candidate),
                hubId,
                StringComparison.Ordinal));
        return GetHubDisplayName(hub, hubId);
    }

    private ServiceSessionContractSnapshot CreateContract(
        ServiceHubSnapshot hub,
        BuildingServiceHubAbility ability,
        ServiceProcessSO process,
        ServiceModeProcessContract modeContract,
        bool isInternalActor)
    {
        ServicePaymentPolicy paymentPolicy = process.PaymentPolicy;
        bool paymentRequired = paymentPolicy != ServicePaymentPolicy.Free
            && !(isInternalActor
                && paymentPolicy
                    == ServicePaymentPolicy.InternalStaffFree);
        float speedMultiplier = hub.Supports
            .Select(link => new
            {
                Building = link.Support,
                Ability = link.Support?.GetServiceSupportAbility()
            })
            .Where(value => value.Ability != null)
            .Aggregate(
                1f,
                (current, value) => current * GetSupportSpeedMultiplier(
                    value.Building,
                    value.Ability));
        float serviceSeconds = Math.Max(
            0.1f,
            modeContract.serviceSeconds > 0f
                ? modeContract.serviceSeconds
                : hub.Hub.Facility?.useDuration ?? 1f);
        return new ServiceSessionContractSnapshot
        {
            mode = hub.Mode,
            activeStages = modeContract.activeStages,
            receptionSeconds = Math.Max(0f, modeContract.receptionSeconds),
            waitingSeconds = Math.Max(
                Math.Max(0f, modeContract.waitingSeconds),
                hub.EstimatedWaitSeconds),
            serviceSeconds = serviceSeconds / Math.Max(0.01f, speedMultiplier),
            paymentSeconds = Math.Max(0f, modeContract.paymentSeconds),
            cleanupSeconds = Math.Max(0f, modeContract.cleanupSeconds),
            price = ability.serviceCategory == ServiceCategory.Dining
                || ability.serviceCategory == ServiceCategory.Retail
                    ? 0
                    : Math.Max(0, modeContract.basePrice
                        + hub.ExpectedRevenue - ability.directPrice),
            satisfaction = modeContract.satisfaction
                + hub.ExpectedSatisfaction
                - ability.directSatisfaction,
            paymentRequired = paymentRequired,
            internalActor = isInternalActor,
            supportIds = hub.Supports
                .Select(link => link.SupportId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray()
        };
    }

    private bool ValidateMode(
        BuildableObject hub,
        BuildingServiceHubAbility ability,
        ServiceOperationMode mode,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!ability.Allows(mode))
        {
            failure = new DomainFailure(FailureCode.ServiceModeUnsupported);
            return false;
        }

        if (mode == ServiceOperationMode.Direct)
        {
            return true;
        }

        string[] required = mode == ServiceOperationMode.Managed
            ? ability.managedRequiredFeatureTags
            : ability.managedRequiredFeatureTags
                .Concat(ability.automatedRequiredFeatureTags
                    ?? Array.Empty<string>())
                .ToArray();
        if (!links.HasFeatures(hub, required, out failure))
        {
            return false;
        }

        foreach (string feature in required
                     .Where(feature => !string.IsNullOrWhiteSpace(feature)))
        {
            if (!links.TryResolveFeature(
                    hub,
                    feature,
                    out BuildableObject support,
                    out BuildingServiceSupportAbility supportAbility))
            {
                continue;
            }

            if (supportAbility.requiresPower && !power.IsPowered(support))
            {
                failure = new DomainFailure(
                    FailureCode.ServiceSupportUnpowered,
                    support.BuildingData.objectName ?? string.Empty);
                return false;
            }
        }

        return true;
    }

    private float GetSupportSpeedMultiplier(
        BuildableObject supportBuilding,
        BuildingServiceSupportAbility support)
    {
        float authored = Math.Max(0.01f, support?.workSpeedMultiplier ?? 1f);
        float formula = evolutionModifiers != null
            ? evolutionModifiers.GetMultiplier(
                supportBuilding,
                "service.support-speed")
            : 1f;
        return authored * Math.Max(0.1f, formula);
    }

    private bool ValidateResearch(
        ServiceCategory category,
        ServiceOperationMode mode,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (mode == ServiceOperationMode.Direct)
        {
            return true;
        }

        if (!rules.TryGetRequiredServiceResearch(
                (int)category,
                (int)mode,
                out string required))
        {
            throw new InvalidOperationException(
                $"No authored service research rule exists for "
                + $"{category}/{mode}.");
        }
        if (research.IsCompleted(required))
        {
            return true;
        }

        failure = new DomainFailure(
            FailureCode.RequiredResearchUnavailable,
            required,
            string.Empty);
        return false;
    }

    private IEnumerable<BuildableObject> GetOperationalHubs(
        ServiceCategory? category)
    {
        return (buildings.Buildings ?? Array.Empty<BuildableObject>())
            .Where(IsOperational)
            .Where(building =>
            {
                BuildingServiceHubAbility ability =
                    building.GetServiceHubAbility();
                return ability != null
                    && (!category.HasValue
                        || ability.serviceCategory == category.Value);
            });
    }

    private void SubscribeToHub(BuildableObject hub)
    {
        hubSubscriptions.Subscribe(hub, OnHubDestroyed);
    }

    private void OnHubDestroyed(BuildableObject hub)
    {
        string hubId = GetHubId(hub);
        hubSubscriptions.Unsubscribe(hub);
        if (IsReplacedFacility(hub)) return;
        string reason =
            $"{hub?.BuildingData?.objectName ?? "서비스 시설"} 파괴로 영업이 취소되었습니다.";
        string[] sessionIds = Aggregate.ActiveSessions
            .Where(session => string.Equals(
                session.HubId,
                hubId,
                StringComparison.Ordinal))
            .Select(session => session.SessionId)
            .ToArray();
        foreach (string sessionId in sessionIds)
        {
            if (!CancelSession(sessionId, reason))
            {
                UnityEngine.Debug.LogError(
                    "service-session-hub-destroy-cancellation-rejected:"
                    + sessionId);
            }
        }
    }

    private int CountActiveSessions(string hubId) =>
        Aggregate.CountActiveSessions(hubId);

    private ServiceOperationMode ResolveMode(string hubId) =>
        Aggregate.ResolveMode(hubId);

    private static ServiceOperatingState HighestState(
        IReadOnlyList<ServiceHubSnapshot> hubs)
    {
        if (hubs.Any(hub => hub.State == ServiceOperatingState.Automated))
        {
            return ServiceOperatingState.Automated;
        }
        if (hubs.Any(hub => hub.State == ServiceOperatingState.Managed))
        {
            return ServiceOperatingState.Managed;
        }
        return ServiceOperatingState.Direct;
    }

    private static ServiceOperatingState ToOperatingState(
        ServiceOperationMode mode) =>
        mode switch
        {
            ServiceOperationMode.Managed => ServiceOperatingState.Managed,
            ServiceOperationMode.Automated => ServiceOperatingState.Automated,
            _ => ServiceOperatingState.Direct
        };

    private static bool IsOperational(BuildableObject building) =>
        building != null
        && !building.IsGridDestroyed
        && building.BuildingData != null;

    internal static string GetHubId(BuildableObject hub) =>
        hub.RequirePersistentInstanceId().Value;

    private bool TryGetCurrentAbsoluteDay(out int absoluteDay)
    {
        absoluteDay = 0;
        return gameDataProvider.TryGetSessionState(
                   out GameSessionState gameData)
            && gameData?.day != null
            && (absoluteDay = gameData.day.Value) >= 0;
    }

    private static string GetHubDisplayName(
        BuildableObject hub,
        string hubId)
    {
        string displayName = hub?.BuildingData?.objectName?.Trim()
            ?? string.Empty;
        return displayName.Length > 0 ? displayName : hubId;
    }

    private static ServiceModeChangeResult Success(
        ServiceOperationMode previous,
        ServiceOperationMode requested) =>
        new ServiceModeChangeResult
        {
            Succeeded = true,
            PreviousMode = previous,
            RequestedMode = requested,
            Failure = DomainFailure.None
        };

    private static ServiceModeChangeResult Failure(
        ServiceOperationMode previous,
        ServiceOperationMode requested,
        DomainFailure failure) =>
        new ServiceModeChangeResult
        {
            Succeeded = false,
            PreviousMode = previous,
            RequestedMode = requested,
            Failure = failure
        };

}

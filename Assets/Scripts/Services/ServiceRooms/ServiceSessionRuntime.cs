using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using DungeonStory.ServiceRooms;
using VContainer.Unity;

public interface IServiceSessionRuntime :
    IServiceAvailabilityQuery,
    IServiceDemandPolicyRuntime
{
    int Version { get; }
    IReadOnlyList<ServiceSessionSnapshot> ActiveSessions { get; }
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
            ICoreSessionRulesProvider rulesProvider)
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
        }

        public IGameClock Clock { get; }
        public IGameMoneyAccount Money { get; }
        public IPowerInfrastructureQuery Power { get; }
        public IServiceRoomResearchQuery Research { get; }
        public CoreSessionRulesDefinition Rules { get; }
    }

    private readonly IBuildingWorldQuery buildings;
    private readonly IServiceRoomLinkRuntime links;
    private readonly IServiceProcessCatalog processCatalog;
    private readonly IGameClock clock;
    private readonly IGameMoneyAccount money;
    private readonly IPowerInfrastructureQuery power;
    private readonly IServiceRoomResearchQuery research;
    private readonly CoreSessionRulesDefinition rules;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly ICharacterWorldPersistenceIdentityQuery persistentCharacters;
    private readonly ServiceHubSubscriptionRegistry<BuildableObject>
        hubSubscriptions;
    private int projectedRestoreRevision;

    private ServiceSessionAggregate Aggregate => aggregateRootStore.GetOrCreate(
        () => new ServiceSessionAggregate());

    public ServiceSessionRuntime(
        IBuildingWorldQuery buildings,
        IServiceRoomLinkRuntime links,
        IServiceProcessCatalog processCatalog,
        Dependencies dependencies,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        ICharacterWorldPersistenceIdentityQuery persistentCharacters)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.processCatalog = processCatalog
            ?? throw new ArgumentNullException(nameof(processCatalog));
        dependencies = dependencies
            ?? throw new ArgumentNullException(nameof(dependencies));
        clock = dependencies.Clock;
        money = dependencies.Money;
        power = dependencies.Power;
        research = dependencies.Research;
        rules = dependencies.Rules;
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

    public int Version => Aggregate.Version;

    public IReadOnlyList<ServiceSessionSnapshot> ActiveSessions =>
        Aggregate.ActiveSessions;

    public void Tick()
    {
        int publishedRevision = aggregateRootStore.PublishedRestoreRevision;
        if (projectedRestoreRevision == publishedRevision)
        {
            return;
        }

        projectedRestoreRevision = publishedRevision;
        SynchronizeHubSubscriptions();
    }

    public void Dispose() => hubSubscriptions.Clear();

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
                speedMultiplier *= Math.Max(0.01f, support.workSpeedMultiplier);
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
        ServiceOperationMode previous = ResolveMode(GetHubId(hub));
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

        Aggregate.SetMode(GetHubId(hub), mode, out _);
        SubscribeToHub(hub);
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
                StartedAt = clock.Time,
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
            clock.Time,
            out failure);
    }

    public bool TryCompleteSession(
        string sessionId,
        out ServiceSessionSnapshot completed,
        out DomainFailure failure)
    {
        if (!Aggregate.TryComplete(
                sessionId,
                clock.Time,
                out ServiceSessionCompletionTransition transition,
                out failure))
        {
            completed = null;
            return false;
        }

        ServiceSessionEconomicCommand command = transition.EconomicCommand;
        if (command != null)
        {
            money.Add(
                command.Amount,
                new EconomyTransactionContext(
                    EconomyTransactionKind.GuestServiceIncome,
                    command.HubId,
                    command.ActorId,
                    command.CommandId));
        }
        completed = transition.Completed;
        return true;
    }

    public bool CancelSession(string sessionId, string reason)
    {
        return Aggregate.CancelSession(sessionId, reason, clock.Time);
    }

    public ServiceRoomsSaveData Capture()
    {
        ReconcileCaptureReferences();
        return Aggregate.Capture();
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

            Aggregate.CancelSession(
                session.SessionId,
                "save-reference-invalidated",
                clock.Time);
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

        ServiceSessionAggregate restored =
            ServiceSessionAggregate.CreateRestored(
                saveData,
                unchecked(Version + 1));
        return new ServiceRoomsRestoreCandidate(restored);
    }

    public void PublishRestoreCandidate(ServiceRoomsRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        aggregateRootStore.Replace(candidate.Aggregate);
    }

    private void SynchronizeHubSubscriptions()
    {
        hubSubscriptions.Synchronize(
            GetOperationalHubs(null),
            OnHubDestroyed);
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
            .Select(link => link.Support?.GetServiceSupportAbility())
            .Where(support => support != null)
            .Aggregate(
                1f,
                (current, support) =>
                    current * Math.Max(0.01f, support.workSpeedMultiplier));
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
        Aggregate.CancelHubSessions(
            hubId,
            $"{hub?.BuildingData?.objectName ?? "서비스 시설"} 파괴로 영업이 취소되었습니다.",
            clock.Time);
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

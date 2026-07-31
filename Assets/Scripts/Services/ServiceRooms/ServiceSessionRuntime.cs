using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public interface IServiceAvailabilityQuery
{
    ServiceAvailabilitySnapshot GetAvailability(ServiceCategory category);
    bool ShouldAcceptDemand(ServiceCategory category);
    bool ShouldRecordUnservedDemand(
        ServiceCategory category,
        bool demandWasAdvertised);
}

public interface IServiceDemandPolicyRuntime
{
    bool IsAdvertisingEnabled(ServiceCategory category);
    void SetAdvertisingEnabled(ServiceCategory category, bool enabled);
}

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
        out string failureReason);
    bool TrySetStage(
        string sessionId,
        ServiceSessionStage stage,
        out string failureReason);
    bool TryCompleteSession(
        string sessionId,
        out ServiceSessionSnapshot completed,
        out string failureReason);
    bool CancelSession(string sessionId, string reason);
    ServiceRoomsSaveData Capture();
    void Restore(ServiceRoomsSaveData saveData);
}

public sealed class ServiceSessionRuntime : IServiceSessionRuntime
{
    private readonly IBuildingWorldQuery buildings;
    private readonly ICharacterLifetimeQuery characters;
    private readonly IServiceRoomLinkRuntime links;
    private readonly IServiceProcessCatalog processCatalog;
    private readonly IGameClock clock;
    private readonly IGameMoneyRuntime money;
    private readonly IElectricalNetworkRuntime power;
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly Dictionary<string, ServiceOperationMode> modesByHubId =
        new Dictionary<string, ServiceOperationMode>(StringComparer.Ordinal);
    private readonly Dictionary<string, ServiceSessionSnapshot> sessionsById =
        new Dictionary<string, ServiceSessionSnapshot>(StringComparer.Ordinal);
    private readonly HashSet<ServiceCategory> advertisedCategories =
        new HashSet<ServiceCategory>();
    private readonly HashSet<BuildableObject> subscribedHubs =
        new HashSet<BuildableObject>();

    public ServiceSessionRuntime(
        IBuildingWorldQuery buildings,
        ICharacterLifetimeQuery characters,
        IServiceRoomLinkRuntime links,
        IServiceProcessCatalog processCatalog,
        IGameClock clock,
        IGameMoneyRuntime money,
        IElectricalNetworkRuntime power,
        IBlueprintResearchRuntimeProvider researchProvider = null)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.processCatalog = processCatalog
            ?? throw new ArgumentNullException(nameof(processCatalog));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.researchProvider = researchProvider;
    }

    public int Version { get; private set; }

    public IReadOnlyList<ServiceSessionSnapshot> ActiveSessions =>
        sessionsById.Values
            .Where(session => session != null && session.IsActive)
            .OrderBy(session => session.StartedAt)
            .ThenBy(session => session.SessionId, StringComparer.Ordinal)
            .ToArray();

    public bool IsAdvertisingEnabled(ServiceCategory category) =>
        advertisedCategories.Contains(category);

    public void SetAdvertisingEnabled(ServiceCategory category, bool enabled)
    {
        bool changed = enabled
            ? advertisedCategories.Add(category)
            : advertisedCategories.Remove(category);
        if (changed)
        {
            IncrementVersion();
        }
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
            BlockedReason = accepting.Length == 0 && hubs.Length > 0
                ? hubs.Select(snapshot => snapshot.BlockedReason)
                    .FirstOrDefault(reason =>
                        !string.IsNullOrWhiteSpace(reason)) ?? "일시 중단"
                : string.Empty
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
                BlockedReason = "서비스 시설이 없습니다."
            };
        }

        SubscribeToHub(hub);
        string hubId = GetHubId(hub);
        ServiceOperationMode mode = ResolveMode(hubId);
        bool valid = ValidateMode(hub, ability, mode, out string reason);
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
            BlockedReason = reason,
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
            return Failure(previous, mode, "서비스 허브가 아닙니다.");
        }

        if (mode == previous)
        {
            return Success(previous, mode, "이미 선택한 운영 모드입니다.");
        }

        if (!ability.Allows(mode))
        {
            return Failure(previous, mode, "이 시설은 해당 운영 모드를 지원하지 않습니다.");
        }

        if (!ValidateResearch(ability.serviceCategory, mode, out string reason)
            || !ValidateMode(hub, ability, mode, out reason))
        {
            return Failure(previous, mode, reason);
        }

        modesByHubId[GetHubId(hub)] = mode;
        SubscribeToHub(hub);
        IncrementVersion();
        return Success(previous, mode, "신규 손님부터 새 운영 모드를 적용합니다.");
    }

    public ServiceModeChangeResult SwitchToDirect(BuildableObject hub) =>
        SetMode(hub, ServiceOperationMode.Direct);

    public bool TryBeginSession(
        ServiceSessionRequest request,
        out ServiceSessionSnapshot session,
        out string failureReason)
    {
        session = null;
        failureReason = string.Empty;
        BuildableObject hub = request?.Hub;
        BuildingServiceHubAbility ability = hub.GetServiceHubAbility();
        if (!IsOperational(hub) || ability == null)
        {
            failureReason = "서비스가 휴업 중입니다.";
            return false;
        }

        string processId = request?.ProcessId?.Trim() ?? string.Empty;
        if (processId.Length == 0)
        {
            processId = ability.supportedProcessIds?
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))?
                .Trim() ?? string.Empty;
        }
        if (!ability.SupportsProcess(processId)
            || !processCatalog.TryGet(processId, out ServiceProcessSO process)
            || process.ServiceCategory != ability.serviceCategory
            || !string.Equals(
                process.OwnerHubTag,
                ability.ServiceHubTag,
                StringComparison.Ordinal))
        {
            failureReason =
                $"시설이 지원하는 서비스 공정을 찾을 수 없습니다: {processId}";
            return false;
        }

        ServiceHubSnapshot hubState = GetHubSnapshot(hub);
        if (hubState.State == ServiceOperatingState.Suspended)
        {
            failureReason = hubState.BlockedReason;
            return false;
        }

        if (hubState.ActiveSessions >= hubState.Capacity)
        {
            failureReason = "서비스 용량이 가득 찼습니다.";
            return false;
        }

        string actorId = request.Actor?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (actorId.Length > 0
            && sessionsById.Values.Any(candidate =>
                candidate != null
                && candidate.IsActive
                && string.Equals(
                    candidate.ActorId,
                    actorId,
                    StringComparison.Ordinal)))
        {
            failureReason = "이미 진행 중인 서비스 세션이 있습니다.";
            return false;
        }

        if (!process.TryGetContract(
                hubState.Mode,
                out ServiceModeProcessContract modeContract))
        {
            failureReason =
                $"{process.ProcessId} 공정에 {hubState.Mode} 계약이 없습니다.";
            return false;
        }

        ServiceSessionContractSnapshot contract =
            CreateContract(
                hubState,
                ability,
                process,
                modeContract,
                request.IsInternalActor);
        string sessionId = $"service:{Guid.NewGuid():N}";
        session = new ServiceSessionSnapshot
        {
            SessionId = sessionId,
            HubId = hubState.HubId,
            ActorId = actorId,
            ProcessId = processId,
            Category = ability.serviceCategory,
            Stage = FirstStage(contract.activeStages),
            StartedAt = clock.Time,
            StageStartedAt = clock.Time,
            AdvertisedDemand = request.AdvertisedDemand,
            Contract = contract
        };
        sessionsById.Add(sessionId, session);
        IncrementVersion();
        return true;
    }

    public bool TrySetStage(
        string sessionId,
        ServiceSessionStage stage,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetActive(sessionId, out ServiceSessionSnapshot session))
        {
            failureReason = "활성 서비스 세션을 찾을 수 없습니다.";
            return false;
        }

        if (stage == ServiceSessionStage.Completed)
        {
            failureReason = "완료는 서비스 완료 명령으로만 확정할 수 있습니다.";
            return false;
        }

        session.Stage = stage;
        session.StageStartedAt = clock.Time;
        IncrementVersion();
        return true;
    }

    public bool TryCompleteSession(
        string sessionId,
        out ServiceSessionSnapshot completed,
        out string failureReason)
    {
        completed = null;
        failureReason = string.Empty;
        if (!TryGetActive(sessionId, out ServiceSessionSnapshot session))
        {
            failureReason = "활성 서비스 세션을 찾을 수 없습니다.";
            return false;
        }

        if (session.Stage != ServiceSessionStage.Service
            && session.Stage != ServiceSessionStage.Payment
            && session.Stage != ServiceSessionStage.Cleanup)
        {
            failureReason = "서비스 이용이 끝나기 전에는 결제를 확정할 수 없습니다.";
            return false;
        }

        if (session.Contract.paymentRequired
            && !session.PaymentCommitted
            && session.Contract.price > 0)
        {
            money.Add(
                session.Contract.price,
                new EconomyTransactionContext(
                    EconomyTransactionKind.GuestServiceIncome,
                    session.HubId,
                    session.ActorId,
                    "서비스 완료 결제"));
            session.PaymentCommitted = true;
        }

        session.Stage = ServiceSessionStage.Completed;
        session.StageStartedAt = clock.Time;
        completed = session;
        IncrementVersion();
        return true;
    }

    public bool CancelSession(string sessionId, string reason)
    {
        if (!TryGetActive(sessionId, out ServiceSessionSnapshot session))
        {
            return false;
        }

        session.Stage = ServiceSessionStage.Cancelled;
        session.StageStartedAt = clock.Time;
        session.CancellationReason = string.IsNullOrWhiteSpace(reason)
            ? "서비스가 취소되었습니다."
            : reason.Trim();
        IncrementVersion();
        return true;
    }

    public ServiceRoomsSaveData Capture()
    {
        return new ServiceRoomsSaveData
        {
            hubs = modesByHubId
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ServiceHubModeSaveData
                {
                    hubId = pair.Key,
                    mode = pair.Value
                })
                .ToList(),
            advertisedCategories = advertisedCategories
                .OrderBy(category => category)
                .ToList(),
            sessions = ActiveSessions
                .Select(ServiceRoomsSaveData.FromSnapshot)
                .ToList()
        };
    }

    public void Restore(ServiceRoomsSaveData saveData)
    {
        modesByHubId.Clear();
        sessionsById.Clear();
        advertisedCategories.Clear();

        foreach (ServiceHubModeSaveData hub in saveData?.hubs
                     ?? new List<ServiceHubModeSaveData>())
        {
            string hubId = hub?.hubId?.Trim() ?? string.Empty;
            if (hubId.Length > 0)
            {
                modesByHubId[hubId] = hub.mode;
            }
        }

        foreach (ServiceCategory category in saveData?.advertisedCategories
                     ?? new List<ServiceCategory>())
        {
            advertisedCategories.Add(category);
        }

        HashSet<string> validHubIds = new HashSet<string>(
            GetOperationalHubs(null).Select(GetHubId),
            StringComparer.Ordinal);
        HashSet<string> validActorIds = new HashSet<string>(
            (characters.AllCharacters ?? Array.Empty<CharacterActor>())
                .Where(actor => actor != null)
                .Select(actor => actor.Identity?.PersistentId?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        foreach (ServiceSessionSaveData source in saveData?.sessions
                     ?? new List<ServiceSessionSaveData>())
        {
            ServiceSessionSnapshot restored = source?.ToSnapshot();
            if (restored == null
                || !restored.IsActive
                || !validHubIds.Contains(restored.HubId)
                || restored.ActorId.Length > 0
                    && !validActorIds.Contains(restored.ActorId))
            {
                continue;
            }

            sessionsById[restored.SessionId] = restored;
        }

        foreach (BuildableObject hub in GetOperationalHubs(null))
        {
            SubscribeToHub(hub);
        }

        IncrementVersion();
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
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!ability.Allows(mode))
        {
            failureReason = "시설이 선택된 운영 모드를 지원하지 않습니다.";
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
        if (!links.HasFeatures(hub, required, out failureReason))
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
                failureReason = $"{support.BuildingData.objectName}에 전력이 없습니다.";
                return false;
            }
        }

        return true;
    }

    private bool ValidateResearch(
        ServiceCategory category,
        ServiceOperationMode mode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (mode == ServiceOperationMode.Direct)
        {
            return true;
        }

        string required = mode == ServiceOperationMode.Automated
            ? ServiceRoomResearchIds.ServiceAutomation
            : category switch
            {
                ServiceCategory.Lodging =>
                    ServiceRoomResearchIds.HospitalityOperations,
                ServiceCategory.Bathing =>
                    ServiceRoomResearchIds.BathBusiness,
                ServiceCategory.Medical =>
                    ServiceRoomResearchIds.MedicalReception,
                _ => ServiceRoomResearchIds.ServiceFlow
            };
        if (researchProvider != null
            && researchProvider.TryGetRuntime(out BlueprintResearchRuntime runtime)
            && runtime.State.Projects.IsCompleted(
                new ResearchProjectId(required)))
        {
            return true;
        }

        failureReason = $"연구가 필요합니다: {required}";
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
        if (hub != null && subscribedHubs.Add(hub))
        {
            hub.OnBuildingDestroyed += () => OnHubDestroyed(hub);
        }
    }

    private void OnHubDestroyed(BuildableObject hub)
    {
        string hubId = GetHubId(hub);
        foreach (ServiceSessionSnapshot session in sessionsById.Values
                     .Where(session =>
                         session != null
                         && session.IsActive
                         && string.Equals(
                             session.HubId,
                             hubId,
                             StringComparison.Ordinal))
                     .ToArray())
        {
            CancelSession(
                session.SessionId,
                $"{hub?.BuildingData?.objectName ?? "서비스 시설"} 파괴로 영업이 취소되었습니다.");
        }

        IncrementVersion();
    }

    private bool TryGetActive(
        string sessionId,
        out ServiceSessionSnapshot session)
    {
        session = null;
        string normalized = sessionId?.Trim() ?? string.Empty;
        return normalized.Length > 0
            && sessionsById.TryGetValue(normalized, out session)
            && session != null
            && session.IsActive;
    }

    private int CountActiveSessions(string hubId) =>
        sessionsById.Values.Count(session =>
            session != null
            && session.IsActive
            && string.Equals(session.HubId, hubId, StringComparison.Ordinal));

    private ServiceOperationMode ResolveMode(string hubId) =>
        !string.IsNullOrWhiteSpace(hubId)
        && modesByHubId.TryGetValue(hubId, out ServiceOperationMode mode)
            ? mode
            : ServiceOperationMode.Direct;

    private static ServiceSessionStage FirstStage(
        ServiceProcessStageMask stages)
    {
        if ((stages & ServiceProcessStageMask.Reception) != 0)
        {
            return ServiceSessionStage.Reception;
        }
        if ((stages & ServiceProcessStageMask.Waiting) != 0)
        {
            return ServiceSessionStage.Waiting;
        }
        return ServiceSessionStage.Service;
    }

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
        IndustrialInfrastructureIdentity.GetNodeId(hub);

    private static ServiceModeChangeResult Success(
        ServiceOperationMode previous,
        ServiceOperationMode requested,
        string message) =>
        new ServiceModeChangeResult
        {
            Succeeded = true,
            PreviousMode = previous,
            RequestedMode = requested,
            Message = message
        };

    private static ServiceModeChangeResult Failure(
        ServiceOperationMode previous,
        ServiceOperationMode requested,
        string message) =>
        new ServiceModeChangeResult
        {
            Succeeded = false,
            PreviousMode = previous,
            RequestedMode = requested,
            Message = message ?? string.Empty
        };

    private void IncrementVersion()
    {
        unchecked
        {
            Version++;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.ServiceRooms
{
    public sealed class ServiceSessionBeginCommand
    {
        public string HubId { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string ProcessId { get; set; } = string.Empty;
        public ServiceCategory Category { get; set; }
        public int Capacity { get; set; }
        public float StartedAt { get; set; }
        public bool AdvertisedDemand { get; set; }
        public ServiceSessionContractSnapshot Contract { get; set; } = new();
    }

    public sealed class ServiceSessionEconomicCommand
    {
        public string CommandId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string HubId { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public sealed class ServiceSessionCompletionTransition
    {
        public ServiceSessionSnapshot Completed { get; set; }
        public ServiceSessionEconomicCommand EconomicCommand { get; set; }
    }

    /// <summary>
    /// Engine-free authority for service-session identities, state transitions,
    /// restore reconstruction, and one-shot completion economic commands.
    /// </summary>
    public sealed class ServiceSessionAggregate
    {
        private readonly Dictionary<string, ServiceOperationMode> modesByHubId =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, ServiceSessionSnapshot> sessionsById =
            new(StringComparer.Ordinal);
        private readonly HashSet<ServiceCategory> advertisedCategories = new();

        public int Version { get; private set; }

        public IReadOnlyList<ServiceSessionSnapshot> ActiveSessions =>
            sessionsById.Values
                .Where(session => session != null && session.IsActive)
                .OrderBy(session => session.StartedAt)
                .ThenBy(session => session.SessionId, StringComparer.Ordinal)
                .Select(CloneSession)
                .ToArray();

        public bool IsAdvertisingEnabled(ServiceCategory category) =>
            advertisedCategories.Contains(category);

        public bool SetAdvertisingEnabled(ServiceCategory category, bool enabled)
        {
            RequireDefined(category, nameof(category));
            bool changed = enabled
                ? advertisedCategories.Add(category)
                : advertisedCategories.Remove(category);
            if (changed)
            {
                IncrementVersion();
            }
            return changed;
        }

        public ServiceOperationMode ResolveMode(string hubId)
        {
            RequireCanonical(hubId, nameof(hubId));
            return modesByHubId.TryGetValue(hubId, out ServiceOperationMode mode)
                ? mode
                : ServiceOperationMode.Direct;
        }

        public bool SetMode(
            string hubId,
            ServiceOperationMode mode,
            out ServiceOperationMode previous)
        {
            RequireCanonical(hubId, nameof(hubId));
            RequireDefined(mode, nameof(mode));
            previous = ResolveMode(hubId);
            if (previous == mode)
            {
                return false;
            }

            modesByHubId[hubId] = mode;
            IncrementVersion();
            return true;
        }

        public int CountActiveSessions(string hubId)
        {
            RequireCanonical(hubId, nameof(hubId));
            return sessionsById.Values.Count(session =>
                session != null
                && session.IsActive
                && string.Equals(
                    session.HubId,
                    hubId,
                    StringComparison.Ordinal));
        }

        public bool TryBegin(
            ServiceSessionBeginCommand command,
            out ServiceSessionSnapshot session,
            out DomainFailure failure)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            ValidateBeginCommand(command);
            session = null;
            failure = DomainFailure.None;
            if (CountActiveSessions(command.HubId) >= command.Capacity)
            {
                failure = new DomainFailure(FailureCode.ServiceCapacityFull);
                return false;
            }
            if (command.ActorId.Length > 0
                && sessionsById.Values.Any(candidate =>
                    candidate != null
                    && candidate.IsActive
                    && string.Equals(
                        candidate.ActorId,
                        command.ActorId,
                        StringComparison.Ordinal)))
            {
                failure = new DomainFailure(
                    FailureCode.ServiceActorAlreadyActive);
                return false;
            }

            string sessionId = CreateSessionId();
            ServiceSessionSnapshot stored = new()
            {
                SessionId = sessionId,
                HubId = command.HubId,
                ActorId = command.ActorId,
                ProcessId = command.ProcessId,
                Category = command.Category,
                Stage = ServiceSessionPolicy.FirstStage(
                    command.Contract.activeStages),
                StartedAt = command.StartedAt,
                StageStartedAt = command.StartedAt,
                AdvertisedDemand = command.AdvertisedDemand,
                Contract = command.Contract.Clone()
            };
            sessionsById.Add(sessionId, stored);
            IncrementVersion();
            session = CloneSession(stored);
            return true;
        }

        public bool TrySetStage(
            string sessionId,
            ServiceSessionStage stage,
            float changedAt,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            if (!TryGetActive(sessionId, out ServiceSessionSnapshot session))
            {
                failure = new DomainFailure(FailureCode.ServiceSessionMissing);
                return false;
            }
            if (!ServiceSessionPolicy.IsStageAllowed(
                    session.Contract.activeStages,
                    stage))
            {
                failure = new DomainFailure(
                    FailureCode.ServiceStageNotAllowed,
                    stage.ToString());
                return false;
            }

            session.Stage = stage;
            session.StageStartedAt = RequireFiniteNonNegative(
                changedAt,
                nameof(changedAt));
            IncrementVersion();
            return true;
        }

        public bool TryComplete(
            string sessionId,
            float completedAt,
            out ServiceSessionCompletionTransition transition,
            out DomainFailure failure)
        {
            transition = null;
            failure = DomainFailure.None;
            if (!TryGetActive(sessionId, out ServiceSessionSnapshot session))
            {
                failure = new DomainFailure(FailureCode.ServiceSessionMissing);
                return false;
            }
            if (!ServiceSessionPolicy.CanComplete(session.Stage))
            {
                failure = new DomainFailure(FailureCode.ServiceStageIncomplete);
                return false;
            }

            ServiceSessionEconomicCommand economicCommand = null;
            if (session.Contract.paymentRequired
                && !session.PaymentCommitted
                && session.Contract.price > 0)
            {
                session.PaymentCommitted = true;
                economicCommand = new ServiceSessionEconomicCommand
                {
                    CommandId = "service-payment:" + session.SessionId,
                    SessionId = session.SessionId,
                    HubId = session.HubId,
                    ActorId = session.ActorId,
                    Amount = session.Contract.price
                };
            }

            session.Stage = ServiceSessionStage.Completed;
            session.StageStartedAt = RequireFiniteNonNegative(
                completedAt,
                nameof(completedAt));
            IncrementVersion();
            transition = new ServiceSessionCompletionTransition
            {
                Completed = CloneSession(session),
                EconomicCommand = economicCommand
            };
            return true;
        }

        public bool CancelSession(
            string sessionId,
            string reason,
            float cancelledAt)
        {
            if (!TryGetActive(sessionId, out ServiceSessionSnapshot session))
            {
                return false;
            }

            Cancel(session, reason, cancelledAt);
            IncrementVersion();
            return true;
        }

        public int CancelHubSessions(
            string hubId,
            string reason,
            float cancelledAt)
        {
            RequireCanonical(hubId, nameof(hubId));
            ServiceSessionSnapshot[] active = sessionsById.Values
                .Where(session =>
                    session != null
                    && session.IsActive
                    && string.Equals(
                        session.HubId,
                        hubId,
                        StringComparison.Ordinal))
                .ToArray();
            foreach (ServiceSessionSnapshot session in active)
            {
                Cancel(session, reason, cancelledAt);
            }
            if (active.Length > 0)
            {
                IncrementVersion();
            }
            return active.Length;
        }

        public int RemoveHubModesExcept(ISet<string> liveHubIds)
        {
            if (liveHubIds == null)
            {
                throw new ArgumentNullException(nameof(liveHubIds));
            }

            string[] removedHubIds = modesByHubId.Keys
                .Where(hubId => !liveHubIds.Contains(hubId))
                .ToArray();
            foreach (string hubId in removedHubIds)
            {
                modesByHubId.Remove(hubId);
            }
            if (removedHubIds.Length > 0)
            {
                IncrementVersion();
            }
            return removedHubIds.Length;
        }

        public ServiceRoomsSaveData Capture() => new()
        {
            version = ServiceRoomsSaveData.CurrentVersion,
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

        public static ServiceSessionAggregate CreateRestored(
            ServiceRoomsSaveData saveData,
            int version)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }
            if (saveData.version != ServiceRoomsSaveData.CurrentVersion
                || saveData.hubs == null
                || saveData.advertisedCategories == null
                || saveData.sessions == null)
            {
                throw new InvalidOperationException(
                    "Service-room aggregate restore payload is incomplete or incompatible.");
            }

            ServiceSessionAggregate restored = new() { Version = version };
            foreach (ServiceHubModeSaveData hub in saveData.hubs)
            {
                if (hub == null)
                {
                    throw new InvalidOperationException(
                        "Service-room aggregate restore contains a null hub mode.");
                }
                RequireCanonical(hub.hubId, nameof(hub.hubId));
                RequireDefined(hub.mode, nameof(hub.mode));
                if (!restored.modesByHubId.TryAdd(hub.hubId, hub.mode))
                {
                    throw new InvalidOperationException(
                        $"Duplicate service hub ID '{hub.hubId}'.");
                }
            }
            foreach (ServiceCategory category in saveData.advertisedCategories)
            {
                RequireDefined(category, nameof(category));
                if (!restored.advertisedCategories.Add(category))
                {
                    throw new InvalidOperationException(
                        $"Duplicate advertised service category '{category}'.");
                }
            }
            HashSet<string> activeActorIds = new(StringComparer.Ordinal);
            foreach (ServiceSessionSaveData source in saveData.sessions)
            {
                ServiceSessionSnapshot session = RequireRestorableSession(source);
                ServiceOperationMode restoredMode =
                    restored.modesByHubId.TryGetValue(
                        session.HubId,
                        out ServiceOperationMode savedMode)
                        ? savedMode
                        : ServiceOperationMode.Direct;
                if (session.Contract.mode != restoredMode
                    || !ServiceSessionPolicy.IsStageAllowed(
                        session.Contract.activeStages,
                        session.Stage)
                    || session.ActorId.Length > 0
                    && !activeActorIds.Add(session.ActorId))
                {
                    throw new InvalidOperationException(
                        $"Service session '{session.SessionId}' violates its restored mode, stage, or actor ownership.");
                }
                if (!restored.sessionsById.TryAdd(session.SessionId, session))
                {
                    throw new InvalidOperationException(
                        $"Duplicate service session ID '{session.SessionId}'.");
                }
            }
            return restored;
        }

        private bool TryGetActive(
            string sessionId,
            out ServiceSessionSnapshot session)
        {
            session = null;
            return ServiceSessionPolicy.IsCanonicalRequired(sessionId)
                && sessionsById.TryGetValue(sessionId, out session)
                && session != null
                && session.IsActive;
        }

        private static void ValidateBeginCommand(ServiceSessionBeginCommand command)
        {
            RequireCanonical(command.HubId, nameof(command.HubId));
            RequireCanonicalOptional(command.ActorId, nameof(command.ActorId));
            RequireCanonical(command.ProcessId, nameof(command.ProcessId));
            RequireDefined(command.Category, nameof(command.Category));
            if (command.Capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(command.Capacity));
            }
            RequireFiniteNonNegative(command.StartedAt, nameof(command.StartedAt));
            ValidateContract(command.Contract);
        }

        private static ServiceSessionSnapshot RequireRestorableSession(
            ServiceSessionSaveData source)
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Service-room aggregate restore contains a null session.");
            }
            RequireCanonical(source.sessionId, nameof(source.sessionId));
            RequireCanonical(source.hubId, nameof(source.hubId));
            RequireCanonicalOptional(source.actorId, nameof(source.actorId));
            RequireCanonical(source.processId, nameof(source.processId));
            RequireDefined(source.category, nameof(source.category));
            if (source.stage < ServiceSessionStage.Reception
                || source.stage > ServiceSessionStage.Cleanup
                || source.paymentCommitted
                || !string.IsNullOrEmpty(source.cancellationReason))
            {
                throw new InvalidOperationException(
                    $"Service session '{source.sessionId}' is not an active canonical restore record.");
            }
            RequireFiniteNonNegative(source.startedAt, nameof(source.startedAt));
            RequireFiniteNonNegative(
                source.stageStartedAt,
                nameof(source.stageStartedAt));
            if (source.stageStartedAt < source.startedAt)
            {
                throw new InvalidOperationException(
                    $"Service session '{source.sessionId}' starts its stage before the session.");
            }
            ValidateContract(source.contract);
            ServiceSessionSnapshot restored = source.ToSnapshot();
            if (!string.Equals(
                    restored.SessionId,
                    source.sessionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    restored.HubId,
                    source.hubId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    restored.ActorId,
                    source.actorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    restored.ProcessId,
                    source.processId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Service-session restore changed a persistent ID.");
            }
            return restored;
        }

        private static void ValidateContract(ServiceSessionContractSnapshot contract)
        {
            if (contract == null)
            {
                throw new ArgumentNullException(nameof(contract));
            }
            RequireDefined(contract.mode, nameof(contract.mode));
            if (!ServiceSessionPolicy.HasValidStageMask(contract.activeStages)
                || contract.price < 0
                || !IsFiniteNonNegative(contract.receptionSeconds)
                || !IsFiniteNonNegative(contract.waitingSeconds)
                || !IsFinitePositive(contract.serviceSeconds)
                || !IsFiniteNonNegative(contract.paymentSeconds)
                || !IsFiniteNonNegative(contract.cleanupSeconds)
                || float.IsNaN(contract.satisfaction)
                || float.IsInfinity(contract.satisfaction)
                || contract.supportIds == null)
            {
                throw new InvalidOperationException(
                    "Service-session contract is invalid.");
            }
            HashSet<string> supportIds = new(StringComparer.Ordinal);
            foreach (string supportId in contract.supportIds)
            {
                RequireCanonical(supportId, nameof(contract.supportIds));
                if (!supportIds.Add(supportId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate service support ID '{supportId}'.");
                }
            }
        }

        private static ServiceSessionSnapshot CloneSession(
            ServiceSessionSnapshot source) => new()
        {
            SessionId = source.SessionId,
            HubId = source.HubId,
            ActorId = source.ActorId,
            ProcessId = source.ProcessId,
            Category = source.Category,
            Stage = source.Stage,
            StartedAt = source.StartedAt,
            StageStartedAt = source.StageStartedAt,
            AdvertisedDemand = source.AdvertisedDemand,
            PaymentCommitted = source.PaymentCommitted,
            CancellationReason = source.CancellationReason,
            Contract = source.Contract?.Clone() ?? new ServiceSessionContractSnapshot()
        };

        private static void Cancel(
            ServiceSessionSnapshot session,
            string reason,
            float cancelledAt)
        {
            session.Stage = ServiceSessionStage.Cancelled;
            session.StageStartedAt = RequireFiniteNonNegative(
                cancelledAt,
                nameof(cancelledAt));
            session.CancellationReason = string.IsNullOrWhiteSpace(reason)
                ? "service.cancelled"
                : reason.Trim();
        }

        private static string CreateSessionId() =>
            $"service:{Guid.NewGuid():N}";

        private static void RequireCanonical(string value, string parameterName)
        {
            if (!ServiceSessionPolicy.IsCanonicalRequired(value))
            {
                throw new ArgumentException(
                    "A canonical persistent ID is required.",
                    parameterName);
            }
        }

        private static void RequireCanonicalOptional(
            string value,
            string parameterName)
        {
            if (value == null
                || value.Length > 0
                && !ServiceSessionPolicy.IsCanonicalRequired(value))
            {
                throw new ArgumentException(
                    "The optional persistent ID is not canonical.",
                    parameterName);
            }
        }

        private static void RequireDefined<T>(T value, string parameterName)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static float RequireFiniteNonNegative(
            float value,
            string parameterName) => IsFiniteNonNegative(value)
                ? value
                : throw new ArgumentOutOfRangeException(parameterName);

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static bool IsFinitePositive(float value) =>
            IsFiniteNonNegative(value) && value > 0f;

        private void IncrementVersion()
        {
            unchecked
            {
                Version++;
            }
        }
    }

    public static class ServiceSessionPolicy
    {
        private const ServiceProcessStageMask KnownStages =
            ServiceProcessStageMask.Reception
            | ServiceProcessStageMask.Waiting
            | ServiceProcessStageMask.Service
            | ServiceProcessStageMask.Payment
            | ServiceProcessStageMask.Cleanup;

        public static bool IsCanonicalRequired(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);

        public static ServiceSessionStage FirstStage(
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

        public static bool IsStageAllowed(
            ServiceProcessStageMask stages,
            ServiceSessionStage stage)
        {
            ServiceProcessStageMask requested = ToStageMask(stage);
            return requested != ServiceProcessStageMask.None
                && (stages & requested) != 0;
        }

        public static bool CanComplete(ServiceSessionStage stage) =>
            stage == ServiceSessionStage.Service
            || stage == ServiceSessionStage.Payment
            || stage == ServiceSessionStage.Cleanup;

        public static bool HasValidStageMask(ServiceProcessStageMask stages) =>
            stages != ServiceProcessStageMask.None
            && (stages & ~KnownStages) == 0
            && (stages & ServiceProcessStageMask.Service) != 0;

        private static ServiceProcessStageMask ToStageMask(
            ServiceSessionStage stage) => stage switch
            {
                ServiceSessionStage.Reception => ServiceProcessStageMask.Reception,
                ServiceSessionStage.Waiting => ServiceProcessStageMask.Waiting,
                ServiceSessionStage.Service => ServiceProcessStageMask.Service,
                ServiceSessionStage.Payment => ServiceProcessStageMask.Payment,
                ServiceSessionStage.Cleanup => ServiceProcessStageMask.Cleanup,
                _ => ServiceProcessStageMask.None
            };
    }
}

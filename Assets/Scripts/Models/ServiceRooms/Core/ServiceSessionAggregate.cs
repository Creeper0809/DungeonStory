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
        public readonly struct ServiceModeMutationSnapshot
        {
            internal ServiceModeMutationSnapshot(
                string hubId,
                bool hadStoredMode,
                ServiceOperationMode previousMode,
                int version)
            {
                HubId = hubId;
                HadStoredMode = hadStoredMode;
                PreviousMode = previousMode;
                Version = version;
            }

            internal string HubId { get; }
            internal bool HadStoredMode { get; }
            internal ServiceOperationMode PreviousMode { get; }
            internal int Version { get; }
        }

        private readonly Dictionary<string, ServiceOperationMode> modesByHubId =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, ServiceSessionSnapshot> sessionsById =
            new(StringComparer.Ordinal);
        private readonly HashSet<ServiceCategory> advertisedCategories = new();
        private readonly List<ServiceModePendingFactData> pendingModeFacts = new();

        public IReadOnlyList<ServiceModePendingFactData> PendingModeFacts => pendingModeFacts.Select(value => value.Clone()).ToArray();
        public bool HasPendingModeFact(string hubId) => pendingModeFacts.Any(value => value.fact.hubId == hubId);

        public void RecordModeFact(ServiceModePendingFactData pending)
        {
            ServiceModeChangedFact.ValidatePending(new[] { pending });
            if (HasPendingModeFact(pending.fact.hubId) || pendingModeFacts.Count >= ServiceModeChangedFact.MaximumPending
                || pendingModeFacts.Any(value => value.publicationKey == pending.publicationKey)
                || ResolveMode(pending.fact.hubId) != pending.fact.requestedMode)
                throw new InvalidOperationException("service-mode-pending-owner-conflict");
            pendingModeFacts.Add(pending.Clone());
        }

        public void ClearModeFact(string hubId, string publicationKey) =>
            pendingModeFacts.RemoveAll(value => value.fact.hubId == hubId && value.publicationKey == publicationKey);

        public void RotateModeFact(string hubId)
        {
            var pending = pendingModeFacts.FirstOrDefault(value => value.fact.hubId == hubId);
            if (pending == null) return;
            pendingModeFacts.Remove(pending); pendingModeFacts.Add(pending);
        }
        private float timelineTime;
        private float runtimeClockAnchor;
        private float timelineClockAnchor;
        private bool clockNeedsBinding;
        private bool clockBound;

        // The mapping belongs to this root, so transaction rollback restores
        // both the domain data and its clock epoch by restoring the same root.
        public void BindRuntimeClock(float runtimeNow)
        {
            RequireFiniteNonNegative(runtimeNow, nameof(runtimeNow));
            if (!clockNeedsBinding || clockBound)
                throw new InvalidOperationException("Service clock can only bind once on a restored root.");
            runtimeClockAnchor = runtimeNow;
            timelineClockAnchor = timelineTime;
            clockBound = true;
            clockNeedsBinding = false;
        }

        public float GetTimelineTime(float runtimeNow)
        {
            RequireFiniteNonNegative(runtimeNow, nameof(runtimeNow));
            if (clockNeedsBinding)
                throw new InvalidOperationException("Restored service clock has not been bound.");
            if (clockBound && runtimeNow < runtimeClockAnchor)
                throw new InvalidOperationException("Service source clock moved before its restore anchor.");
            float now = clockBound
                ? (float)((double)timelineClockAnchor + ((double)runtimeNow - runtimeClockAnchor))
                : runtimeNow;
            RequireFiniteNonNegative(now, nameof(now));
            if (now < timelineTime)
                throw new InvalidOperationException("Service clock moved before a committed timestamp.");
            return now;
        }

        public int Version { get; private set; }

        public IReadOnlyList<ServiceSessionSnapshot> ActiveSessions =>
            sessionsById.Values
                .Where(session => session != null && session.IsActive)
                .OrderBy(session => session.StartedAt)
                .ThenBy(session => session.SessionId, StringComparer.Ordinal)
                .Select(CloneSession)
                .ToArray();

        public IReadOnlyList<ServiceSessionSnapshot> PendingTerminalSessions =>
            sessionsById.Values
                .Where(session => session != null
                    && !session.IsActive
                    && session.TerminalPhase is >= ServiceSessionTerminalPhase.Prepared
                        and < ServiceSessionTerminalPhase.Acknowledged)
                .OrderBy(session => session.StageStartedAt)
                .ThenBy(session => session.SessionId, StringComparer.Ordinal)
                .Select(CloneSession)
                .ToArray();

        public bool TryGetSession(
            string sessionId,
            out ServiceSessionSnapshot session)
        {
            session = null;
            if (!ServiceSessionPolicy.IsCanonicalRequired(sessionId)
                || !sessionsById.TryGetValue(
                    sessionId,
                    out ServiceSessionSnapshot stored)
                || stored == null)
            {
                return false;
            }

            session = CloneSession(stored);
            return true;
        }

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

            if (HasPendingModeFact(hubId))
                throw new InvalidOperationException("service-mode-previous-observation-pending");
            modesByHubId[hubId] = mode;
            IncrementVersion();
            return true;
        }

        public ServiceModeMutationSnapshot CaptureModeMutationSnapshot(
            string hubId)
        {
            RequireCanonical(hubId, nameof(hubId));
            bool hadStoredMode = modesByHubId.TryGetValue(
                hubId,
                out ServiceOperationMode previousMode);
            return new ServiceModeMutationSnapshot(
                hubId,
                hadStoredMode,
                hadStoredMode
                    ? previousMode
                    : ServiceOperationMode.Direct,
                Version);
        }

        public void RestoreModeMutationSnapshot(
            in ServiceModeMutationSnapshot snapshot)
        {
            RequireCanonical(snapshot.HubId, nameof(snapshot));
            RequireDefined(snapshot.PreviousMode, nameof(snapshot));
            if (snapshot.Version < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshot));
            }

            if (snapshot.HadStoredMode)
            {
                modesByHubId[snapshot.HubId] = snapshot.PreviousMode;
            }
            else
            {
                modesByHubId.Remove(snapshot.HubId);
            }
            Version = snapshot.Version;
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
            timelineTime = Math.Max(timelineTime, command.StartedAt);
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

            RequireTransitionTime(session, changedAt);
            session.Stage = stage;
            session.StageStartedAt = changedAt;
            timelineTime = Math.Max(timelineTime, changedAt);
            IncrementVersion();
            return true;
        }

        public bool TryComplete(
            string sessionId,
            float completedAt,
            out ServiceSessionCompletionTransition transition,
            out DomainFailure failure) => TryComplete(
                sessionId,
                completedAt,
                0,
                out transition,
                out failure);

        public bool TryComplete(
            string sessionId,
            float completedAt,
            int completedAbsoluteDay,
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

            RequireTransitionTime(session, completedAt);
            if (completedAbsoluteDay < 0) throw new ArgumentOutOfRangeException(nameof(completedAbsoluteDay));
            ServiceSessionStage previousStage = session.Stage;
            ServiceSessionEconomicCommand economicCommand = null;
            if (session.Contract.paymentRequired
                && !session.PaymentCommitted
                && session.Contract.price > 0)
            {
                economicCommand = new ServiceSessionEconomicCommand
                {
                    CommandId = ServiceSessionTerminalIdentity.PaymentSourceId(
                        session.SessionId),
                    SessionId = session.SessionId,
                    HubId = session.HubId,
                    ActorId = session.ActorId,
                    Amount = session.Contract.price
                };
            }

            session.Stage = ServiceSessionStage.Completed;
            session.StageStartedAt = completedAt;
            session.ProvidedServiceSeconds = session.Contract.serviceSeconds;
            timelineTime = Math.Max(timelineTime, completedAt);
            BeginTerminal(
                session,
                previousStage,
                completedAbsoluteDay);
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
            float cancelledAt) => CancelSession(
                sessionId,
                reason,
                cancelledAt,
                0);

        public bool CancelSession(
            string sessionId,
            string reason,
            float cancelledAt,
            int cancelledAbsoluteDay)
        {
            if (!TryGetActive(sessionId, out ServiceSessionSnapshot session))
            {
                return false;
            }

            RequireTransitionTime(session, cancelledAt);
            if (cancelledAbsoluteDay < 0) throw new ArgumentOutOfRangeException(nameof(cancelledAbsoluteDay));
            ServiceSessionStage previousStage = session.Stage;
            float providedServiceSeconds = ComputeProvidedServiceSeconds(
                session,
                cancelledAt);
            Cancel(session, reason, cancelledAt);
            session.ProvidedServiceSeconds = providedServiceSeconds;
            timelineTime = Math.Max(timelineTime, cancelledAt);
            BeginTerminal(session, previousStage, cancelledAbsoluteDay);
            IncrementVersion();
            return true;
        }

        public int CancelHubSessions(
            string hubId,
            string reason,
            float cancelledAt) => CancelHubSessions(
                hubId,
                reason,
                cancelledAt,
                0);

        public int CancelHubSessions(
            string hubId,
            string reason,
            float cancelledAt,
            int cancelledAbsoluteDay)
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
            if (cancelledAbsoluteDay < 0) throw new ArgumentOutOfRangeException(nameof(cancelledAbsoluteDay));
            foreach (ServiceSessionSnapshot session in active)
                RequireTransitionTime(session, cancelledAt);
            foreach (ServiceSessionSnapshot session in active)
            {
                ServiceSessionStage previousStage = session.Stage;
                float providedServiceSeconds = ComputeProvidedServiceSeconds(
                    session,
                    cancelledAt);
                Cancel(session, reason, cancelledAt);
                session.ProvidedServiceSeconds = providedServiceSeconds;
                BeginTerminal(session, previousStage, cancelledAbsoluteDay);
            }
            if (active.Length > 0)
            {
                timelineTime = Math.Max(timelineTime, cancelledAt);
                IncrementVersion();
            }
            return active.Length;
        }

        // Binding is a separate, explicit preparation state. No key encoding or
        // receipt semantics belong to this owner; the observer supplies the reference.
        public bool TryBindTerminalObservation(string sessionId, string publicationKey, string hubName)
        {
            if (!IsCanonical(publicationKey) || !IsCanonical(hubName)
                || !sessionsById.TryGetValue(sessionId, out var session)
                || session.IsActive || session.TerminalPhase != ServiceSessionTerminalPhase.Prepared)
                return false;
            if (!string.IsNullOrEmpty(session.TerminalPublicationKey))
                return session.TerminalPublicationKey == publicationKey && session.TerminalHubName == hubName;
            session.TerminalPublicationKey = publicationKey;
            session.TerminalHubName = hubName;
            IncrementVersion();
            return true;
        }

        public bool TryRecordPayment(
            string sessionId,
            string transactionId,
            string transactionSourceId,
            int amount,
            int balanceBefore,
            int balanceAfter)
        {
            if (!sessionsById.TryGetValue(
                    sessionId,
                    out ServiceSessionSnapshot session)
                || session == null
                || session.Stage != ServiceSessionStage.Completed
                || session.TerminalPhase != ServiceSessionTerminalPhase.Prepared
                || string.IsNullOrWhiteSpace(session.TerminalPublicationKey)
                || !session.Contract.paymentRequired
                || session.Contract.price <= 0
                || string.IsNullOrWhiteSpace(transactionId)
                || amount != session.Contract.price
                || !string.Equals(
                    transactionSourceId,
                    ServiceSessionTerminalIdentity.PaymentSourceId(sessionId),
                    StringComparison.Ordinal)
                || balanceBefore < 0
                || balanceAfter != balanceBefore + amount)
            {
                return false;
            }

            session.PaymentCommitted = true;
            session.EconomyTransactionId = transactionId.Trim();
            session.EconomyTransactionSourceId = transactionSourceId;
            session.PaymentAmount = amount;
            session.BalanceBefore = balanceBefore;
            session.BalanceAfter = balanceAfter;
            session.TerminalPhase = ServiceSessionTerminalPhase.PaymentCommitted;
            IncrementVersion();
            return true;
        }

        public bool TryRecordObservationCommit(
            string sessionId,
            string outcomeId,
            string outcomeDigest)
        {
            if (!sessionsById.TryGetValue(
                    sessionId,
                    out ServiceSessionSnapshot session)
                || session == null
                || session.IsActive
                || session.TerminalPhase is < ServiceSessionTerminalPhase.Prepared
                    or >= ServiceSessionTerminalPhase.Acknowledged
                || string.IsNullOrWhiteSpace(session.TerminalPublicationKey)
                || outcomeId == null || outcomeDigest == null
                || outcomeId.Length > 0 && !IsCanonical(outcomeId)
                || outcomeDigest.Length > 0 && !IsCanonical(outcomeDigest)
                || session.TerminalPublicationId.Length > 0 && session.TerminalPublicationId != outcomeId
                || session.TerminalPublicationFingerprint.Length > 0 && session.TerminalPublicationFingerprint != outcomeDigest
                || RequiresPayment(session)
                    && session.TerminalPhase
                        < ServiceSessionTerminalPhase.PaymentCommitted)
            {
                return false;
            }

            session.TerminalPublicationId = outcomeId?.Trim() ?? string.Empty;
            session.TerminalPublicationFingerprint = outcomeDigest?.Trim() ?? string.Empty;
            session.TerminalPhase = ServiceSessionTerminalPhase.ObservationCommitted;
            IncrementVersion();
            return true;
        }

        public bool TryAcknowledgeTerminal(
            string sessionId,
            string outcomeId,
            string outcomeDigest)
        {
            if (!sessionsById.TryGetValue(
                    sessionId,
                    out ServiceSessionSnapshot session)
                || session == null
                || session.TerminalPhase
                    != ServiceSessionTerminalPhase.ObservationCommitted
                || !IsCanonical(outcomeId) || !IsCanonical(outcomeDigest)
                || session.TerminalPublicationId.Length > 0 && session.TerminalPublicationId != outcomeId
                || session.TerminalPublicationFingerprint.Length > 0 && session.TerminalPublicationFingerprint != outcomeDigest)
            {
                return false;
            }

            session.TerminalPublicationId = outcomeId.Trim();
            session.TerminalPublicationFingerprint = outcomeDigest.Trim();
            session.TerminalPhase = ServiceSessionTerminalPhase.Acknowledged;
            IncrementVersion();
            return true;
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

        public ServiceRoomsSaveData Capture() => Capture(timelineTime);

        public ServiceRoomsSaveData Capture(float capturedTimelineTime, ISet<string> retainedCleanupSessions = null)
        {
            RequireFiniteNonNegative(capturedTimelineTime, nameof(capturedTimelineTime));
            if (capturedTimelineTime < timelineTime)
                throw new InvalidOperationException("Service capture clock precedes its committed state.");
            if (retainedCleanupSessions != null)
                foreach (string sessionId in retainedCleanupSessions)
                    if (!sessionsById.TryGetValue(sessionId, out ServiceSessionSnapshot retained)
                        || retained.Stage != ServiceSessionStage.Cancelled)
                        throw new InvalidOperationException("Retained cleanup session is missing or not cancelled.");
            return new()
            {
                version = ServiceRoomsSaveData.CurrentVersion,
                timelineTime = capturedTimelineTime,
                pendingModeFacts = pendingModeFacts.OrderBy(value => value.fact.hubId, StringComparer.Ordinal)
                    .Select(value => value.Clone()).ToList(),
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
                sessions = sessionsById.Values
                    .Where(session => session != null
                        && (session.IsActive
                            || session.TerminalPhase
                                != ServiceSessionTerminalPhase.Acknowledged
                            || retainedCleanupSessions?.Contains(session.SessionId) == true))
                    .OrderBy(session => session.StartedAt)
                    .ThenBy(session => session.SessionId, StringComparer.Ordinal)
                    .Select(CloneSession)
                    .Select(ServiceRoomsSaveData.FromSnapshot)
                    .ToList()
            };
        }

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
                || saveData.sessions == null || saveData.pendingModeFacts == null)
            {
                throw new InvalidOperationException(
                    "Service-room aggregate restore payload is incomplete or incompatible.");
            }

            RequireFiniteNonNegative(saveData.timelineTime, nameof(saveData.timelineTime));
            ServiceSessionAggregate restored = new()
            {
                Version = version,
                timelineTime = saveData.timelineTime,
                clockNeedsBinding = true
            };
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
            ServiceModeChangedFact.ValidatePending(saveData.pendingModeFacts);
            foreach (var pending in saveData.pendingModeFacts)
            {
                // Retired facilities may no longer own a mode. Their already-applied
                // historical fact must survive independently until delivery completes.
                if (restored.modesByHubId.TryGetValue(pending.fact.hubId, out var mode)
                    && mode != pending.fact.requestedMode)
                    throw new InvalidOperationException("service-mode-pending-owner-state-conflict");
                restored.pendingModeFacts.Add(pending.Clone());
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
                ServiceSessionSnapshot session = RequireRestorableSession(
                    source);
                if (session.StageStartedAt > saveData.timelineTime)
                    throw new InvalidOperationException("Service session timestamp exceeds the captured timeline.");
                ServiceOperationMode restoredMode =
                    restored.modesByHubId.TryGetValue(
                        session.HubId,
                        out ServiceOperationMode savedMode)
                        ? savedMode
                        : ServiceOperationMode.Direct;
                if (session.IsActive
                    && (session.Contract.mode != restoredMode
                        || !ServiceSessionPolicy.IsStageAllowed(
                            session.Contract.activeStages,
                            session.Stage))
                    || session.IsActive
                    && session.ActorId.Length > 0
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
            bool active = source.stage is >= ServiceSessionStage.Reception
                and <= ServiceSessionStage.Cleanup;
            if (active)
            {
                if (!HasEmptyTerminalFields(source))
                {
                    throw new InvalidOperationException(
                        $"Active service session '{source.sessionId}' carries terminal state.");
                }
            }
            else if (!HasValidPendingTerminalShape(source))
            {
                throw new InvalidOperationException(
                    $"Terminal service session '{source.sessionId}' has an invalid pending outcome shape.");
            }
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
            ProvidedServiceSeconds = source.ProvidedServiceSeconds,
            AdvertisedDemand = source.AdvertisedDemand,
            PaymentCommitted = source.PaymentCommitted,
            CancellationReason = source.CancellationReason,
            TerminalPhase = source.TerminalPhase,
            PreviousStage = source.PreviousStage,
            TerminalAbsoluteDay = source.TerminalAbsoluteDay,
            TerminalOwnerRevision = source.TerminalOwnerRevision,
            EconomyTransactionId = source.EconomyTransactionId,
            EconomyTransactionSourceId = source.EconomyTransactionSourceId,
            PaymentAmount = source.PaymentAmount,
            BalanceBefore = source.BalanceBefore,
            BalanceAfter = source.BalanceAfter,
            TerminalPublicationKey = source.TerminalPublicationKey,
            TerminalHubName = source.TerminalHubName,
            TerminalPublicationId = source.TerminalPublicationId,
            TerminalPublicationFingerprint = source.TerminalPublicationFingerprint,
            Contract = source.Contract?.Clone() ?? new ServiceSessionContractSnapshot()
        };

        private static void BeginTerminal(
            ServiceSessionSnapshot session,
            ServiceSessionStage previousStage,
            int absoluteDay)
        {
            session.PreviousStage = previousStage;
            session.TerminalAbsoluteDay = Math.Max(0, absoluteDay);
            session.TerminalOwnerRevision =
                ServiceSessionTerminalIdentity.OwnerRevision;
            session.TerminalPublicationKey = string.Empty;
            session.TerminalHubName = string.Empty;
            session.TerminalPhase = ServiceSessionTerminalPhase.Prepared;
            session.EconomyTransactionId = string.Empty;
            session.EconomyTransactionSourceId = string.Empty;
            session.PaymentAmount = 0;
            session.BalanceBefore = 0;
            session.BalanceAfter = 0;
            session.TerminalPublicationId = string.Empty;
            session.TerminalPublicationFingerprint = string.Empty;
        }

        private static bool RequiresPayment(ServiceSessionSnapshot session) =>
            session.Stage == ServiceSessionStage.Completed
            && session.Contract.paymentRequired
            && session.Contract.price > 0;

        private static bool HasEmptyTerminalFields(ServiceSessionSaveData source) =>
            source.terminalPhase == ServiceSessionTerminalPhase.None
            && source.terminalOwnerRevision == 0L
            && source.terminalAbsoluteDay == 0
            && source.providedServiceSeconds == 0f
            && string.IsNullOrEmpty(source.economyTransactionId)
            && string.IsNullOrEmpty(source.economyTransactionSourceId)
            && source.paymentAmount == 0
            && source.balanceBefore == 0
            && source.balanceAfter == 0
            && string.IsNullOrEmpty(source.terminalPublicationKey)
            && string.IsNullOrEmpty(source.terminalHubName)
            && string.IsNullOrEmpty(source.terminalPublicationId)
            && string.IsNullOrEmpty(source.terminalPublicationFingerprint)
            && !source.paymentCommitted
            && string.IsNullOrEmpty(source.cancellationReason);

        private static bool HasValidPendingTerminalShape(
            ServiceSessionSaveData source)
        {
            bool terminalStage = source.stage is ServiceSessionStage.Completed
                or ServiceSessionStage.Cancelled;
            bool previousStage = source.previousStage
                is >= ServiceSessionStage.Reception
                and <= ServiceSessionStage.Cleanup;
            bool bindingShape = IsCanonical(source.terminalPublicationKey) && IsCanonical(source.terminalHubName)
                || source.terminalPhase == ServiceSessionTerminalPhase.Prepared
                    && string.IsNullOrEmpty(source.terminalPublicationKey) && string.IsNullOrEmpty(source.terminalHubName);
            bool requiresPayment = source.stage == ServiceSessionStage.Completed
                && source.contract.paymentRequired
                && source.contract.price > 0;
            bool paymentShape = requiresPayment
                ? source.terminalPhase == ServiceSessionTerminalPhase.Prepared
                    ? !source.paymentCommitted
                        && string.IsNullOrEmpty(source.economyTransactionId)
                        && string.IsNullOrEmpty(
                            source.economyTransactionSourceId)
                        && source.paymentAmount == 0
                        && source.balanceBefore == 0
                        && source.balanceAfter == 0
                    : source.paymentCommitted
                        && !string.IsNullOrWhiteSpace(
                            source.economyTransactionId)
                        && string.Equals(
                            source.economyTransactionSourceId,
                            ServiceSessionTerminalIdentity.PaymentSourceId(
                                source.sessionId),
                            StringComparison.Ordinal)
                        && source.paymentAmount == source.contract.price
                        && source.balanceAfter
                            == source.balanceBefore + source.paymentAmount
                : !source.paymentCommitted
                    && string.IsNullOrEmpty(source.economyTransactionId)
                    && string.IsNullOrEmpty(source.economyTransactionSourceId)
                    && source.paymentAmount == 0
                    && source.balanceBefore == 0
                    && source.balanceAfter == 0;
            bool outcomeShape = source.terminalPhase == ServiceSessionTerminalPhase.Acknowledged
                ? source.stage == ServiceSessionStage.Cancelled
                    && IsCanonical(source.terminalPublicationId) && IsCanonical(source.terminalPublicationFingerprint)
                : source.terminalPhase == ServiceSessionTerminalPhase.ObservationCommitted
                ? (string.IsNullOrEmpty(source.terminalPublicationId)
                        || IsCanonical(source.terminalPublicationId))
                    && (string.IsNullOrEmpty(source.terminalPublicationFingerprint)
                        || IsCanonical(source.terminalPublicationFingerprint))
                : string.IsNullOrEmpty(source.terminalPublicationId)
                    && string.IsNullOrEmpty(source.terminalPublicationFingerprint);
            bool cancellationShape = source.stage
                == ServiceSessionStage.Cancelled
                    ? !string.IsNullOrWhiteSpace(source.cancellationReason)
                    : string.IsNullOrEmpty(source.cancellationReason);
            bool provisionShape = IsFiniteNonNegative(
                    source.providedServiceSeconds)
                && source.providedServiceSeconds
                    <= source.contract.serviceSeconds + 0.001f
                && (source.stage == ServiceSessionStage.Completed
                    ? Approximately(
                        source.providedServiceSeconds,
                        source.contract.serviceSeconds)
                    : source.previousStage is ServiceSessionStage.Reception
                            or ServiceSessionStage.Waiting
                        ? source.providedServiceSeconds == 0f
                        : source.previousStage is ServiceSessionStage.Payment
                            or ServiceSessionStage.Cleanup
                            ? Approximately(
                                source.providedServiceSeconds,
                                source.contract.serviceSeconds)
                            : true);
            return terminalStage
                && previousStage
                && source.stageStartedAt >= source.startedAt
                && source.terminalAbsoluteDay >= 0
                && source.terminalOwnerRevision
                    == ServiceSessionTerminalIdentity.OwnerRevision
                && bindingShape
                && source.terminalPhase is >= ServiceSessionTerminalPhase.Prepared
                    and <= ServiceSessionTerminalPhase.Acknowledged
                && (!requiresPayment
                    || source.terminalPhase
                        != ServiceSessionTerminalPhase.ObservationCommitted
                    || source.paymentCommitted)
                && paymentShape
                && outcomeShape
                && cancellationShape
                && provisionShape;
        }

        private static float ComputeProvidedServiceSeconds(
            ServiceSessionSnapshot session,
            float terminalAt)
        {
            if (session.Stage is ServiceSessionStage.Reception
                or ServiceSessionStage.Waiting)
            {
                return 0f;
            }
            if (session.Stage is ServiceSessionStage.Payment
                or ServiceSessionStage.Cleanup)
            {
                return session.Contract.serviceSeconds;
            }
            return Math.Max(
                0f,
                Math.Min(
                    session.Contract.serviceSeconds,
                    terminalAt - session.StageStartedAt));
        }

        private static bool Approximately(float left, float right) =>
            Math.Abs(left - right) <= 0.001f;

        private static bool IsCanonical(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);

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

        private static void RequireTransitionTime(ServiceSessionSnapshot session, float now)
        {
            RequireFiniteNonNegative(now, nameof(now));
            if (now < session.StageStartedAt || now < session.StartedAt)
                throw new InvalidOperationException("Service transition time precedes its current stage.");
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

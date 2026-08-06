using System;
using System.Collections.Generic;

public sealed class ServiceRoomsSaveSection :
    DungeonStrictJsonSaveSection<
        ServiceRoomsSaveData,
        ServiceRoomsRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "service.rooms";

    private readonly IServiceSessionRuntime runtime;
    private readonly IServiceProcessCatalog processCatalog;

    public ServiceRoomsSaveSection(
        IServiceSessionRuntime runtime,
        IServiceProcessCatalog processCatalog)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.processCatalog = processCatalog
            ?? throw new ArgumentNullException(nameof(processCatalog));
    }

    public override string SectionId => Id;
    public override int SectionVersion => ServiceRoomsSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[]
        {
            ModularFacilityWorldSaveSection.Id,
            CharacterWorldSaveSection.Id
        };

    protected override ServiceRoomsSaveData CapturePayload() =>
        runtime.Capture();

    protected override void NormalizeRestorePayload(
        ServiceRoomsSaveData payload,
        DungeonGameRestoreReport report) =>
        V18WorkProductionCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override void ValidateParsedPayload(ServiceRoomsSaveData payload)
    {
        ValidatePayloadOrThrow(payload);
    }

    protected override ServiceRoomsRestoreCandidate BuildRestoreCandidate(
        ServiceRoomsSaveData payload)
    {
        ValidatePayloadOrThrow(payload);
        return runtime.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        ServiceRoomsRestoreCandidate candidate)
    {
        runtime.PublishRestoreCandidate(candidate);
    }

    private void ValidatePayloadOrThrow(ServiceRoomsSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidatePayload(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Service-rooms restore payload is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    private void ValidatePayload(
        ServiceRoomsSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null
            || payload.hubs == null
            || payload.advertisedCategories == null
            || payload.sessions == null)
        {
            report.AddError("Service-rooms payload or required list is null.");
            return;
        }
        if (payload.version != ServiceRoomsSaveData.CurrentVersion)
        {
            report.AddError(
                $"Service-rooms payload version {payload.version} is unsupported.");
        }

        Dictionary<string, ServiceOperationMode> modes =
            new Dictionary<string, ServiceOperationMode>(StringComparer.Ordinal);
        string previousHubId = null;
        foreach (ServiceHubModeSaveData hub in payload.hubs)
        {
            string hubId = hub?.hubId;
            if (hub == null
                || !IsCanonicalRequired(hubId)
                || !Enum.IsDefined(typeof(ServiceOperationMode), hub.mode)
                || previousHubId != null
                    && string.CompareOrdinal(previousHubId, hubId) >= 0
                || !modes.TryAdd(hubId, hub.mode))
            {
                report.AddError(
                    "Service-rooms hub modes contain a null, invalid, duplicate, or unordered entry.");
                continue;
            }
            previousHubId = hubId;
        }

        int previousCategory = -1;
        foreach (ServiceCategory category in payload.advertisedCategories)
        {
            int raw = (int)category;
            if (!Enum.IsDefined(typeof(ServiceCategory), category)
                || raw <= previousCategory)
            {
                report.AddError(
                    "Service-rooms advertised categories are invalid or unordered.");
                break;
            }
            previousCategory = raw;
        }

        HashSet<string> sessionIds = new HashSet<string>(StringComparer.Ordinal);
        float previousStartedAt = -1f;
        string previousSessionId = null;
        foreach (ServiceSessionSaveData session in payload.sessions)
        {
            ValidateSession(
                session,
                modes,
                sessionIds,
                previousStartedAt,
                previousSessionId,
                report);
            if (session != null)
            {
                previousStartedAt = session.startedAt;
                previousSessionId = session.sessionId;
            }
        }
    }

    private void ValidateSession(
        ServiceSessionSaveData session,
        IReadOnlyDictionary<string, ServiceOperationMode> modes,
        ISet<string> sessionIds,
        float previousStartedAt,
        string previousSessionId,
        DungeonGameRestoreReport report)
    {
        if (session == null
            || !IsCanonicalRequired(session.sessionId)
            || !sessionIds.Add(session.sessionId)
            || !IsCanonicalRequired(session.hubId)
            || !IsCanonicalOptional(session.actorId)
            || !IsCanonicalRequired(session.processId)
            || !Enum.IsDefined(typeof(ServiceCategory), session.category)
            || session.stage < ServiceSessionStage.Reception
            || session.stage > ServiceSessionStage.Cleanup
            || !IsFiniteNonNegative(session.startedAt)
            || !IsFiniteNonNegative(session.stageStartedAt)
            || session.stageStartedAt < session.startedAt
            || session.paymentCommitted
            || !string.IsNullOrEmpty(session.cancellationReason)
            || session.startedAt < previousStartedAt
            || session.startedAt == previousStartedAt
                && previousSessionId != null
                && string.CompareOrdinal(previousSessionId, session.sessionId) >= 0)
        {
            report.AddError(
                "Service-rooms payload contains an invalid or unordered active session.");
            return;
        }

        processCatalog.TryGet(
            session.processId,
            out ServiceProcessSO process);
        ServiceOperationMode mode = modes.TryGetValue(
            session.hubId,
            out ServiceOperationMode savedMode)
                ? savedMode
                : ServiceOperationMode.Direct;
        if (process == null
            || process.ServiceCategory != session.category
            || session.contract == null
            || session.contract.mode != mode
            || !process.TryGetContract(mode, out _))
        {
            report.AddError(
                $"Service session '{session.sessionId}' has invalid authored process or mode data.");
            return;
        }

        ServiceSessionContractSnapshot contract = session.contract;
        const ServiceProcessStageMask knownStages =
            ServiceProcessStageMask.Reception
            | ServiceProcessStageMask.Waiting
            | ServiceProcessStageMask.Service
            | ServiceProcessStageMask.Payment
            | ServiceProcessStageMask.Cleanup;
        if ((contract.activeStages & ~knownStages) != 0
            || (contract.activeStages & ServiceProcessStageMask.Service) == 0
            || (contract.activeStages & ToStageMask(session.stage)) == 0
            || !IsFiniteNonNegative(contract.receptionSeconds)
            || !IsFiniteNonNegative(contract.waitingSeconds)
            || !IsFinitePositive(contract.serviceSeconds)
            || !IsFiniteNonNegative(contract.paymentSeconds)
            || !IsFiniteNonNegative(contract.cleanupSeconds)
            || contract.price < 0
            || float.IsNaN(contract.satisfaction)
            || float.IsInfinity(contract.satisfaction)
            || contract.supportIds == null
            || !HasUniqueCanonicalIds(contract.supportIds))
        {
            report.AddError(
                $"Service session '{session.sessionId}' has an invalid contract snapshot.");
        }
    }

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

    private static bool HasUniqueCanonicalIds(IEnumerable<string> values)
    {
        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (!IsCanonicalRequired(value) || !unique.Add(value))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalOptional(string value) =>
        value != null
        && (value.Length == 0 || IsCanonicalRequired(value));

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool IsFinitePositive(float value) =>
        IsFiniteNonNegative(value) && value > 0f;
}

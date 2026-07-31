using System;
using System.Collections.Generic;

[Serializable]
public sealed class ServiceHubModeSaveData
{
    public string hubId = string.Empty;
    public ServiceOperationMode mode = ServiceOperationMode.Direct;
}

[Serializable]
public sealed class ServiceSessionSaveData
{
    public string sessionId = string.Empty;
    public string hubId = string.Empty;
    public string actorId = string.Empty;
    public string processId = string.Empty;
    public ServiceCategory category;
    public ServiceSessionStage stage;
    public float startedAt;
    public float stageStartedAt;
    public bool advertisedDemand;
    public bool paymentCommitted;
    public string cancellationReason = string.Empty;
    public ServiceSessionContractSnapshot contract =
        new ServiceSessionContractSnapshot();

    public ServiceSessionSnapshot ToSnapshot()
    {
        string normalizedSessionId = sessionId?.Trim() ?? string.Empty;
        string normalizedHubId = hubId?.Trim() ?? string.Empty;
        if (normalizedSessionId.Length == 0 || normalizedHubId.Length == 0)
        {
            return null;
        }

        return new ServiceSessionSnapshot
        {
            SessionId = normalizedSessionId,
            HubId = normalizedHubId,
            ActorId = actorId?.Trim() ?? string.Empty,
            ProcessId = processId?.Trim() ?? string.Empty,
            Category = category,
            Stage = stage,
            StartedAt = startedAt,
            StageStartedAt = stageStartedAt,
            AdvertisedDemand = advertisedDemand,
            PaymentCommitted = paymentCommitted,
            CancellationReason = cancellationReason?.Trim() ?? string.Empty,
            Contract = contract?.Clone() ?? new ServiceSessionContractSnapshot()
        };
    }
}

[Serializable]
public sealed class ServiceRoomsSaveData
{
    public List<ServiceHubModeSaveData> hubs =
        new List<ServiceHubModeSaveData>();
    public List<ServiceCategory> advertisedCategories =
        new List<ServiceCategory>();
    public List<ServiceSessionSaveData> sessions =
        new List<ServiceSessionSaveData>();

    public static ServiceSessionSaveData FromSnapshot(
        ServiceSessionSnapshot source)
    {
        if (source == null)
        {
            return null;
        }

        return new ServiceSessionSaveData
        {
            sessionId = source.SessionId,
            hubId = source.HubId,
            actorId = source.ActorId,
            processId = source.ProcessId,
            category = source.Category,
            stage = source.Stage,
            startedAt = source.StartedAt,
            stageStartedAt = source.StageStartedAt,
            advertisedDemand = source.AdvertisedDemand,
            paymentCommitted = source.PaymentCommitted,
            cancellationReason = source.CancellationReason,
            contract = source.Contract?.Clone()
                ?? new ServiceSessionContractSnapshot()
        };
    }
}

public sealed class ServiceRoomsSaveSection :
    DungeonJsonSaveSection<ServiceRoomsSaveData>
{
    public const string Id = "service.rooms";

    private readonly IServiceSessionRuntime runtime;

    public ServiceRoomsSaveSection(IServiceSessionRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => 1;
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

    protected override void RestorePayload(
        ServiceRoomsSaveData payload,
        DungeonGameRestoreReport report)
    {
        runtime.Restore(payload ?? new ServiceRoomsSaveData());
    }
}

using System;
using System.Collections.Generic;

public enum ServiceCategory
{
    Dining = 0,
    Retail = 1,
    Lodging = 2,
    Bathing = 3,
    Medical = 4
}

public enum ServiceOperationMode
{
    Direct = 0,
    Managed = 1,
    Automated = 2
}

[Flags]
public enum ServiceOperationModeMask
{
    None = 0,
    Direct = 1 << 0,
    Managed = 1 << 1,
    Automated = 1 << 2,
    All = Direct | Managed | Automated
}

public enum ServicePaymentPolicy
{
    Free = 0,
    PayAfterCompletion = 1,
    InternalStaffFree = 2
}

public enum ServiceSupportModifierType
{
    Stage = 0,
    Capacity = 1,
    WorkSpeed = 2,
    Satisfaction = 3,
    Revenue = 4,
    Security = 5,
    Cleanup = 6
}

[Flags]
public enum ServiceProcessStageMask
{
    None = 0,
    Reception = 1 << 0,
    Waiting = 1 << 1,
    Service = 1 << 2,
    Payment = 1 << 3,
    Cleanup = 1 << 4
}

public enum ServiceSessionStage
{
    Reception = 0,
    Waiting = 1,
    Service = 2,
    Payment = 3,
    Cleanup = 4,
    Completed = 5,
    Cancelled = 6
}

public enum ServiceOperatingState
{
    Closed = 0,
    Direct = 1,
    Managed = 2,
    Automated = 3,
    Suspended = 4
}

public sealed class ServiceModeChangeResult
{
    public bool Succeeded { get; set; }
    public ServiceOperationMode PreviousMode { get; set; }
    public ServiceOperationMode RequestedMode { get; set; }
    public DomainFailure Failure { get; set; } = DomainFailure.None;
}

public sealed class ServiceAvailabilitySnapshot
{
    public ServiceCategory Category { get; set; }
    public ServiceOperatingState State { get; set; }
    public int OperationalHubCount { get; set; }
    public int Capacity { get; set; }
    public int ActiveSessions { get; set; }
    public bool AdvertisingEnabled { get; set; }
    public DomainFailure BlockedFailure { get; set; } = DomainFailure.None;
    public bool AcceptsNewDemand =>
        State == ServiceOperatingState.Direct
        || State == ServiceOperatingState.Managed
        || State == ServiceOperatingState.Automated;
}

[Serializable]
public sealed class ServiceSessionContractSnapshot
{
    public ServiceOperationMode mode;
    public ServiceProcessStageMask activeStages;
    public float receptionSeconds;
    public float waitingSeconds;
    public float serviceSeconds = 1f;
    public float paymentSeconds;
    public float cleanupSeconds;
    public int price;
    public float satisfaction;
    public bool paymentRequired = true;
    public bool internalActor;
    public string[] supportIds = Array.Empty<string>();

    public ServiceSessionContractSnapshot Clone() => new()
    {
        mode = mode,
        activeStages = activeStages,
        receptionSeconds = receptionSeconds,
        waitingSeconds = waitingSeconds,
        serviceSeconds = serviceSeconds,
        paymentSeconds = paymentSeconds,
        cleanupSeconds = cleanupSeconds,
        price = price,
        satisfaction = satisfaction,
        paymentRequired = paymentRequired,
        internalActor = internalActor,
        supportIds = supportIds != null
            ? (string[])supportIds.Clone()
            : Array.Empty<string>()
    };
}

public sealed class ServiceSessionSnapshot
{
    public string SessionId { get; set; } = string.Empty;
    public string HubId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ProcessId { get; set; } = string.Empty;
    public ServiceCategory Category { get; set; }
    public ServiceSessionStage Stage { get; set; }
    public float StartedAt { get; set; }
    public float StageStartedAt { get; set; }
    public bool AdvertisedDemand { get; set; }
    public bool PaymentCommitted { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
    public ServiceSessionContractSnapshot Contract { get; set; } = new();
    public bool IsActive =>
        Stage != ServiceSessionStage.Completed
        && Stage != ServiceSessionStage.Cancelled;
}

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
    public ServiceSessionContractSnapshot contract = new();

    public ServiceSessionSnapshot ToSnapshot() => new()
    {
        SessionId = sessionId,
        HubId = hubId,
        ActorId = actorId,
        ProcessId = processId,
        Category = category,
        Stage = stage,
        StartedAt = startedAt,
        StageStartedAt = stageStartedAt,
        AdvertisedDemand = advertisedDemand,
        PaymentCommitted = paymentCommitted,
        CancellationReason = cancellationReason,
        Contract = contract.Clone()
    };
}

[Serializable]
public sealed class ServiceRoomsSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public List<ServiceHubModeSaveData> hubs = new();
    public List<ServiceCategory> advertisedCategories = new();
    public List<ServiceSessionSaveData> sessions = new();

    public static ServiceSessionSaveData FromSnapshot(
        ServiceSessionSnapshot source) => source == null
            ? null
            : new ServiceSessionSaveData
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

public static class ServiceRoomResearchIds
{
    public const string ServiceFlow = "research:service-flow";
    public const string HospitalityOperations =
        "research:hospitality-operations";
    public const string BathBusiness = "research:bath-business";
    public const string MedicalReception = "research:medical-reception";
    public const string ServiceAutomation = "research:service-automation";
}

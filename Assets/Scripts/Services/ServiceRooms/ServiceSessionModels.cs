using System;
using System.Collections.Generic;
using UnityEngine;

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
    public string Message { get; set; } = string.Empty;
}

public sealed class ServiceAvailabilitySnapshot
{
    public ServiceCategory Category { get; set; }
    public ServiceOperatingState State { get; set; }
    public int OperationalHubCount { get; set; }
    public int Capacity { get; set; }
    public int ActiveSessions { get; set; }
    public bool AdvertisingEnabled { get; set; }
    public string BlockedReason { get; set; } = string.Empty;
    public bool AcceptsNewDemand =>
        State == ServiceOperatingState.Direct
        || State == ServiceOperatingState.Managed
        || State == ServiceOperatingState.Automated;
}

public sealed class ServiceHubSnapshot
{
    public string HubId { get; set; } = string.Empty;
    public BuildableObject Hub { get; set; }
    public ServiceCategory Category { get; set; }
    public ServiceOperationMode Mode { get; set; }
    public ServiceOperatingState State { get; set; }
    public int Capacity { get; set; }
    public int ActiveSessions { get; set; }
    public float EstimatedWaitSeconds { get; set; }
    public int ExpectedRevenue { get; set; }
    public float ExpectedSatisfaction { get; set; }
    public string BlockedReason { get; set; } = string.Empty;
    public IReadOnlyList<ServiceSupportLinkSnapshot> Supports { get; set; } =
        Array.Empty<ServiceSupportLinkSnapshot>();
}

public sealed class ServiceSessionRequest
{
    public BuildableObject Hub { get; set; }
    public CharacterActor Actor { get; set; }
    public string ProcessId { get; set; } = string.Empty;
    public bool IsInternalActor { get; set; }
    public bool AdvertisedDemand { get; set; }
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

    public ServiceSessionContractSnapshot Clone()
    {
        return new ServiceSessionContractSnapshot
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
    public ServiceSessionContractSnapshot Contract { get; set; } =
        new ServiceSessionContractSnapshot();

    public bool IsActive =>
        Stage != ServiceSessionStage.Completed
        && Stage != ServiceSessionStage.Cancelled;
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

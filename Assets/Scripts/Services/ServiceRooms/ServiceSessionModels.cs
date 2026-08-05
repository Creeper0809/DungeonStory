using System;
using System.Collections.Generic;

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
    public DomainFailure BlockedFailure { get; set; } = DomainFailure.None;
    public IReadOnlyList<ServiceSupportLinkSnapshot> Supports { get; set; } =
        Array.Empty<ServiceSupportLinkSnapshot>();
}

public sealed class ServiceSessionRequest
{
    public BuildableObject Hub { get; set; }
    public IBuildingCharacterPort Actor { get; set; }
    public string ProcessId { get; set; } = string.Empty;
    public bool IsInternalActor { get; set; }
    public bool AdvertisedDemand { get; set; }
}

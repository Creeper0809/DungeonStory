using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class ServiceModeChangedFactData
{
    public string hubId = string.Empty;
    public string hubName = string.Empty;
    public ServiceOperationMode previousMode;
    public ServiceOperationMode requestedMode;
    public int absoluteDay;

    public ServiceModeChangedFactData Clone() => (ServiceModeChangedFactData)MemberwiseClone();
}

[Serializable]
public sealed class ServiceModePendingFactData
{
    public string publicationKey = string.Empty;
    public ServiceModeChangedFactData fact = new();

    public ServiceModePendingFactData Clone() => new() { publicationKey = publicationKey, fact = fact?.Clone() };
}

/// <summary>A frozen domain transition. Publication references are opaque to the service owner.</summary>
public sealed class ServiceModeChangedFact
{
    public const int MaximumPending = 512;
    private readonly ServiceModeChangedFactData data;

    public ServiceModeChangedFact(ServiceModeChangedFactData source)
    {
        data = source?.Clone() ?? throw new ArgumentNullException(nameof(source));
        if (!DomainFactValueSyntax.IsIdentity(data.hubId)
            || !DomainFactValueSyntax.IsText(data.hubName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || !Enum.IsDefined(typeof(ServiceOperationMode), data.previousMode)
            || !Enum.IsDefined(typeof(ServiceOperationMode), data.requestedMode)
            || data.previousMode == data.requestedMode || data.absoluteDay < 0)
            throw new ArgumentException("service-mode-fact-invalid", nameof(source));
    }

    public ServiceModeChangedFactData Snapshot() => data.Clone();

    public static void ValidatePending(IReadOnlyList<ServiceModePendingFactData> pending)
    {
        if (pending == null || pending.Count > MaximumPending)
            throw new InvalidOperationException("service-mode-pending-list-invalid");
        var hubs = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in pending)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.publicationKey)
                || !keys.Add(entry.publicationKey))
                throw new InvalidOperationException("service-mode-pending-reference-invalid");
            var fact = new ServiceModeChangedFact(entry.fact);
            if (!hubs.Add(fact.data.hubId))
                throw new InvalidOperationException("service-mode-pending-owner-duplicate");
        }
    }
}

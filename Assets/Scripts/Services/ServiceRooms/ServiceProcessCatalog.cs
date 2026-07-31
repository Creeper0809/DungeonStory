using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IServiceProcessCatalog
{
    IReadOnlyList<ServiceProcessSO> All { get; }
    bool TryGet(string processId, out ServiceProcessSO process);
}

public sealed class ResourceServiceProcessCatalog : IServiceProcessCatalog
{
    private readonly IReadOnlyList<ServiceProcessSO> all;
    private readonly IReadOnlyDictionary<string, ServiceProcessSO> byId;

    public ResourceServiceProcessCatalog()
        : this(Resources.LoadAll<ServiceProcessSO>(
            "SO/ServiceRooms/Processes"))
    {
    }

    internal ResourceServiceProcessCatalog(
        IEnumerable<ServiceProcessSO> processes)
    {
        ServiceProcessSO[] values = (processes
                ?? Array.Empty<ServiceProcessSO>())
            .Where(process => process != null && process.IsValid)
            .OrderBy(process => process.ProcessId, StringComparer.Ordinal)
            .ToArray();
        all = values;
        byId = values
            .GroupBy(process => process.ProcessId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<ServiceProcessSO> All => all;

    public bool TryGet(string processId, out ServiceProcessSO process)
    {
        process = null;
        string normalized = processId?.Trim() ?? string.Empty;
        return normalized.Length > 0
            && byId.TryGetValue(normalized, out process);
    }
}

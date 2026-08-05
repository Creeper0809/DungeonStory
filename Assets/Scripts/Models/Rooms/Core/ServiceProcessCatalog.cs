using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

public interface IServiceProcessAuthoredContentPort
{
    IReadOnlyList<ServiceProcessSO> ServiceProcesses { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IServiceProcessCatalog
{
    IReadOnlyList<ServiceProcessSO> All { get; }
    bool TryGet(string processId, out ServiceProcessSO process);
    ServiceProcessSO Require(string processId);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceServiceProcessCatalog : IServiceProcessCatalog
{
    private readonly IReadOnlyList<ServiceProcessSO> all;
    private readonly IReadOnlyDictionary<string, ServiceProcessSO> byId;

    public ResourceServiceProcessCatalog(
        IServiceProcessAuthoredContentPort content)
        : this((content ?? throw new ArgumentNullException(nameof(content)))
            .ServiceProcesses)
    {
    }

    internal ResourceServiceProcessCatalog(
        IEnumerable<ServiceProcessSO> processes)
    {
        ServiceProcessSO[] values = (processes
                ?? throw new ArgumentNullException(nameof(processes)))
            .ToArray();
        List<string> errors = new();
        for (int index = 0; index < values.Length; index++)
        {
            ServiceProcessSO process = values[index];
            if (process == null)
            {
                errors.Add($"Service process reference {index} is missing.");
                continue;
            }

            errors.AddRange(process.ValidateDefinition()
                .Select(error => $"{process.name}: {error}"));
        }
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Service-process catalog is invalid:\n"
                + string.Join("\n", errors));
        }

        all = values
            .OrderBy(process => process.ProcessId, StringComparer.Ordinal)
            .ToArray();
        byId = all.ToDictionary(
            process => process.ProcessId,
            process => process,
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

    public ServiceProcessSO Require(string processId)
    {
        return TryGet(processId, out ServiceProcessSO process)
            ? process
            : throw new KeyNotFoundException(
                $"Unknown service process '{processId}'.");
    }
}

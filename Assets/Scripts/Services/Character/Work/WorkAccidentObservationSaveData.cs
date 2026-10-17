using System;
using System.Collections.Generic;
using System.Globalization;

[Serializable]
public sealed class WorkAccidentObservationSaveData
{
    public WorkAccidentFactSaveData fact;
    public string publicationKey = string.Empty;
    public bool observationCompleted;
    public bool identityNotificationAttempted;
    public bool rollbackBlocked;
    public string rollbackFailure = string.Empty;

    public WorkAccidentObservationSaveData Clone() => new()
    {
        fact = fact?.Clone(), publicationKey = publicationKey,
        observationCompleted = observationCompleted,
        identityNotificationAttempted = identityNotificationAttempted,
        rollbackBlocked = rollbackBlocked, rollbackFailure = rollbackFailure
    };

    public void Validate()
    {
        if (fact == null) throw new InvalidOperationException("work-accident-fact-missing");
        fact.Validate();
        if (string.IsNullOrWhiteSpace(publicationKey) || publicationKey.Length > 1024
            || publicationKey != publicationKey.Trim() || rollbackFailure == null
            || rollbackBlocked != (rollbackFailure.Length > 0)
            || rollbackBlocked && (observationCompleted || identityNotificationAttempted))
            throw new InvalidOperationException("work-accident-observation-state-invalid");
    }

    public static void ValidateAll(DungeonWorkOrderSaveData source)
    {
        if (source?.pendingAccidents == null || source.version != DungeonWorkOrderSaveData.CurrentVersion
            || source.nextOrderSequence < 1 || source.pendingAccidents.Count > 4096)
            throw new InvalidOperationException("work-accident-pending-list-invalid");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var workers = new HashSet<string>(StringComparer.Ordinal);
        string previous = string.Empty;
        foreach (var row in source.pendingAccidents)
        {
            if (row == null) throw new InvalidOperationException("work-accident-row-null");
            row.Validate();
            const string prefix = "work-accident-operation:";
            string id = row.fact.operationId;
            if (!id.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(id.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
                || sequence < 1 || sequence >= source.nextOrderSequence
                || id != prefix + sequence.ToString("D8", CultureInfo.InvariantCulture)
                || string.CompareOrdinal(previous, id) >= 0
                || !keys.Add(row.publicationKey) || !workers.Add(row.fact.workerId))
                throw new InvalidOperationException("work-accident-pending-identity-conflict");
            previous = id;
        }
    }
}

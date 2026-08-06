using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class OffenseReturnBarrier
{
    public int ReturningMembers;
    public bool Sealed;
}

internal sealed class OffenseReturnArrivalAggregateState
{
    internal List<OffenseReturnArrivalState> Arrivals { get; } = new();
    internal Dictionary<string, OffenseReturnBarrier> Barriers { get; } =
        new(StringComparer.Ordinal);
    internal int NextArrivalSequence { get; set; } = 1;
    internal float NextRetryAt { get; set; }

    internal OffenseReturnArrivalAggregateState Clone()
    {
        OffenseReturnArrivalAggregateState clone = new()
        {
            NextArrivalSequence = NextArrivalSequence,
            NextRetryAt = NextRetryAt
        };
        foreach (OffenseReturnArrivalState arrival in Arrivals)
        {
            clone.Arrivals.Add(arrival.Clone());
        }
        foreach (KeyValuePair<string, OffenseReturnBarrier> pair in Barriers)
        {
            clone.Barriers.Add(pair.Key, new OffenseReturnBarrier
            {
                ReturningMembers = pair.Value.ReturningMembers,
                Sealed = pair.Value.Sealed
            });
        }

        return clone;
    }
}

internal static class OffenseReturnArrivalSaveValidation
{
    internal const int MaximumArrivals = 256;
    internal const int MaximumArrivalSize = 10_000;

    public static void Validate(
        DungeonOffenseReturnArrivalSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (payload == null)
        {
            report.AddError("Offense return-arrival payload is null.");
            return;
        }

        if (payload.version !=
            DungeonOffenseReturnArrivalSaveData.CurrentVersion)
        {
            report.AddError(
                $"Unsupported offense return-arrival version {payload.version}; expected {DungeonOffenseReturnArrivalSaveData.CurrentVersion}.");
        }
        if (payload.nextArrivalSequence < 1)
        {
            report.AddError(
                "Offense return-arrival next sequence must be positive.");
        }
        if (payload.arrivals == null)
        {
            report.AddError(
                "Offense return-arrival payload is missing its arrival list.");
            return;
        }
        if (payload.arrivals.Count > MaximumArrivals)
        {
            report.AddError(
                $"Offense return-arrival payload exceeds {MaximumArrivals} records.");
        }

        HashSet<string> arrivalIds = new(StringComparer.Ordinal);
        int highestSequence = 0;
        foreach (OffenseReturnArrivalState arrival in payload.arrivals)
        {
            string arrivalId = arrival?.arrivalId ?? string.Empty;
            if (arrival == null
                || !string.Equals(arrivalId, arrivalId.Trim(),
                    StringComparison.Ordinal)
                || !TryParseArrivalId(arrivalId, out int sequence)
                || !arrivalIds.Add(arrivalId)
                || string.IsNullOrWhiteSpace(arrival.targetId)
                || !string.Equals(arrival.targetId,
                    arrival.targetId.Trim(),
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(arrival.expeditionId)
                || !string.Equals(arrival.expeditionId,
                    arrival.expeditionId.Trim(),
                    StringComparison.Ordinal)
                || !Enum.IsDefined(
                    typeof(OffenseReturnArrivalKind),
                    arrival.kind)
                || !Enum.IsDefined(
                    typeof(OffenseReturnArrivalStage),
                    arrival.stage))
            {
                report.AddError(
                    $"Offense return-arrival contains invalid record '{arrivalId}'.");
                continue;
            }

            highestSequence = Math.Max(highestSequence, sequence);
            if (arrival.requestedAmount < 1
                || arrival.requestedAmount > MaximumArrivalSize
                || arrival.returningMembers < 0
                || !IsFiniteInRange(arrival.escapeRisk, 0f, 100f)
                || arrival.materializedIds == null
                || arrival.escapedIds == null
                || arrival.lastStatus == null)
            {
                report.AddError(
                    $"Offense return-arrival '{arrivalId}' contains invalid state values.");
                continue;
            }

            HashSet<string> materialized = ValidateEntityIds(
                arrival,
                arrival.materializedIds,
                "materialized",
                report);
            HashSet<string> escaped = ValidateEntityIds(
                arrival,
                arrival.escapedIds,
                "escaped",
                report);
            if (materialized.Count > arrival.requestedAmount
                || escaped.Count > materialized.Count
                || escaped.Any(id => !materialized.Contains(id)))
            {
                report.AddError(
                    $"Offense return-arrival '{arrivalId}' has inconsistent entity counts.");
            }
        }

        if (payload.nextArrivalSequence <= highestSequence)
        {
            report.AddError(
                $"Offense return-arrival next sequence {payload.nextArrivalSequence} does not exceed saved sequence {highestSequence}.");
        }
    }

    public static OffenseReturnArrivalAggregateState CreateStrictState(
        DungeonOffenseReturnArrivalSaveData payload,
        float nextRetryAt)
    {
        OffenseReturnArrivalAggregateState restored = new()
        {
            NextArrivalSequence = payload.nextArrivalSequence,
            NextRetryAt = nextRetryAt
        };
        foreach (OffenseReturnArrivalState source in payload.arrivals)
        {
            restored.Arrivals.Add(new OffenseReturnArrivalState
            {
                arrivalId = source.arrivalId,
                expeditionId = source.expeditionId,
                targetId = source.targetId,
                kind = source.kind,
                requestedAmount = source.requestedAmount,
                returnSealed = source.returnSealed,
                returningMembers = source.returningMembers,
                stage = source.stage,
                escapeRisk = source.escapeRisk,
                materializedIds = CanonicalizeEntityIdsForRestore(source,
                    source.materializedIds),
                escapedIds = CanonicalizeEntityIdsForRestore(source,
                    source.escapedIds),
                lastStatus = source.lastStatus
            });
        }
        return restored;
    }

    private static HashSet<string> ValidateEntityIds(
        OffenseReturnArrivalState arrival,
        IReadOnlyList<string> values,
        string label,
        DungeonGameRestoreReport report)
    {
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string raw in values)
        {
            string value = raw ?? string.Empty;
            bool valid = TryResolveEntityId(arrival, value, out string canonical);
            if (!valid
                || !string.Equals(value, value.Trim(),
                    StringComparison.Ordinal)
                || !unique.Add(canonical))
            {
                report.AddError(
                    $"Offense return-arrival '{arrival.arrivalId}' contains invalid or duplicate {label} ID '{value}'.");
            }
        }

        return unique;
    }

    private static List<string> CanonicalizeEntityIdsForRestore(
        OffenseReturnArrivalState arrival,
        IReadOnlyList<string> values)
    {
        List<string> canonical = new(values.Count);
        foreach (string value in values)
        {
            if (!TryResolveEntityId(arrival, value, out string resolved))
            {
                throw new InvalidOperationException(
                    $"Offense return-arrival '{arrival.arrivalId}' contains invalid entity ID '{value}'.");
            }
            canonical.Add(resolved);
        }
        return canonical;
    }

    private static bool TryResolveEntityId(
        OffenseReturnArrivalState arrival,
        string value,
        out string canonical)
    {
        if (arrival.kind != OffenseReturnArrivalKind.Prisoner)
        {
            canonical = value;
            return IsWildlifeId(value);
        }

        string prefix = arrival.arrivalId + ":prisoner:";
        string candidate = value ?? string.Empty;
        // Early V18 persisted the return-arrival suffix without its character scope.
        // Accept only that exact suffix and canonicalize after validation in staging.
        const string characterPrefix = "character:";
        string suffix = candidate.StartsWith(
                characterPrefix,
                StringComparison.Ordinal)
            ? candidate.Substring(characterPrefix.Length)
            : candidate;
        if (!suffix.StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(
                suffix.Substring(prefix.Length),
                out int sequence)
            || sequence <= 0
            || !string.Equals(
                suffix,
                prefix + sequence.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            canonical = string.Empty;
            return false;
        }

        canonical = CharacterId.FromStableSuffix(suffix).Value;
        return string.Equals(candidate, suffix, StringComparison.Ordinal)
            || string.Equals(candidate, canonical, StringComparison.Ordinal);
    }

    private static bool TryParseArrivalId(
        string arrivalId,
        out int sequence)
    {
        const string prefix = "return:";
        sequence = 0;
        return arrivalId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(arrivalId.Substring(prefix.Length), out sequence)
            && sequence > 0;
    }

    private static bool IsWildlifeId(string value)
    {
        const string prefix = "wild:";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(value.Substring(prefix.Length), out int sequence)
            && sequence > 0;
    }

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= minimum
            && value <= maximum;
    }
}

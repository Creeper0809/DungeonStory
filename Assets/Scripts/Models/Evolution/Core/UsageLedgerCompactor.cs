using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public interface IUsageLedgerCompactor
{
    UsageLedgerEvent Record(
        UsageLedger ledger,
        string eventId,
        float amount = 1f,
        string actorId = "",
        string targetId = "",
        IEnumerable<string> sourceTags = null,
        string evidenceId = "",
        HistoricalEvidenceKind historicalEvidenceKind = HistoricalEvidenceKind.None,
        string outcomeId = "",
        int generation = 0,
        int repeatCount = 1);
    CompactedHistorySegment CloseGeneration(UsageLedger ledger, int generation);
    string ComputeHistoryHash(UsageLedger ledger);
}

public sealed class UsageLedgerCompactor : IUsageLedgerCompactor
{
    private const int KeyEventLimit = 8;
    private const int MergeFanout = 8;

    public UsageLedgerEvent Record(
        UsageLedger ledger,
        string eventId,
        float amount = 1f,
        string actorId = "",
        string targetId = "",
        IEnumerable<string> sourceTags = null,
        string evidenceId = "",
        HistoricalEvidenceKind historicalEvidenceKind = HistoricalEvidenceKind.None,
        string outcomeId = "",
        int generation = 0,
        int repeatCount = 1)
    {
        if (ledger == null)
        {
            throw new ArgumentNullException(nameof(ledger));
        }

        string normalizedEventId = eventId?.Trim() ?? string.Empty;
        if (normalizedEventId.Length == 0)
        {
            throw new ArgumentException("Usage event ID cannot be blank.", nameof(eventId));
        }

        long sequence = Math.Max(1L, ledger.nextSequence);
        ledger.nextSequence = sequence + 1L;
        UsageLedgerEvent entry = new UsageLedgerEvent
        {
            evidenceId = string.IsNullOrWhiteSpace(evidenceId)
                ? $"evidence:{sequence.ToString(CultureInfo.InvariantCulture)}"
                : evidenceId.Trim(),
            eventId = normalizedEventId,
            actorId = actorId?.Trim() ?? string.Empty,
            targetId = targetId?.Trim() ?? string.Empty,
            amount = amount,
            historicalEvidenceKind = historicalEvidenceKind,
            outcomeId = outcomeId?.Trim() ?? string.Empty,
            generation = Math.Max(0, generation),
            repeatCount = Math.Max(1, repeatCount),
            sequence = sequence,
            sourceTags = Normalize(sourceTags)
        };

        ledger.currentGenerationEvents ??= new List<UsageLedgerEvent>();
        ledger.currentGenerationEvents.Add(entry);
        int overflow = ledger.currentGenerationEvents.Count - UsageLedger.RawEventCapacity;
        if (overflow > 0)
        {
            ledger.currentGenerationEvents.RemoveRange(0, overflow);
        }

        return entry.Clone();
    }

    public CompactedHistorySegment CloseGeneration(
        UsageLedger ledger,
        int generation)
    {
        if (ledger == null)
        {
            throw new ArgumentNullException(nameof(ledger));
        }

        ledger.currentGenerationEvents ??= new List<UsageLedgerEvent>();
        ledger.compactedSegments ??= new List<CompactedHistorySegment>();
        CompactedHistorySegment segment = BuildSegment(
            level: 0,
            firstGeneration: Math.Max(0, generation),
            lastGeneration: Math.Max(0, generation),
            ledger.currentGenerationEvents,
            Array.Empty<CompactedHistorySegment>());
        ledger.currentGenerationEvents.Clear();
        ledger.compactedSegments.Add(segment);
        MergeHierarchy(ledger);
        return segment.Clone();
    }

    public string ComputeHistoryHash(UsageLedger ledger)
    {
        if (ledger == null)
        {
            return StableEvolutionHash.Compute("empty-ledger");
        }

        StringBuilder canonical = new StringBuilder(1024);
        foreach (CompactedHistorySegment segment in
                 (ledger.compactedSegments ?? new List<CompactedHistorySegment>())
                 .Where(segment => segment != null)
                 .OrderBy(segment => segment.level)
                 .ThenBy(segment => segment.firstGeneration)
                 .ThenBy(segment => segment.historyHash, StringComparer.Ordinal))
        {
            AppendSegment(canonical, segment);
        }

        foreach (UsageLedgerEvent entry in
                 (ledger.currentGenerationEvents ?? new List<UsageLedgerEvent>())
                 .Where(entry => entry != null)
                 .OrderBy(entry => entry.sequence))
        {
            AppendEvent(canonical, entry);
        }

        return StableEvolutionHash.Compute(canonical.ToString());
    }

    private static CompactedHistorySegment BuildSegment(
        int level,
        int firstGeneration,
        int lastGeneration,
        IEnumerable<UsageLedgerEvent> events,
        IEnumerable<CompactedHistorySegment> children)
    {
        List<UsageLedgerEvent> rawEvents = events?
            .Where(entry => entry != null)
            .Select(entry => entry.Clone())
            .ToList() ?? new List<UsageLedgerEvent>();
        List<CompactedHistorySegment> childSegments = children?
            .Where(segment => segment != null)
            .Select(segment => segment.Clone())
            .ToList() ?? new List<CompactedHistorySegment>();

        Dictionary<string, float> metrics = new Dictionary<string, float>(
            StringComparer.Ordinal);
        foreach (UsageLedgerEvent entry in rawEvents)
        {
            metrics.TryGetValue(entry.eventId, out float current);
            metrics[entry.eventId] = current + entry.amount;
        }

        foreach (CompactedHistorySegment child in childSegments)
        {
            foreach (UsageLedgerMetric metric in child.metrics.Where(metric => metric != null))
            {
                metrics.TryGetValue(metric.metricId ?? string.Empty, out float current);
                metrics[metric.metricId ?? string.Empty] = current + metric.value;
            }
        }

        List<UsageLedgerEvent> keyEvents = rawEvents
            .Concat(childSegments.SelectMany(segment => segment.keyEvents))
            .Where(entry => entry != null)
            .OrderByDescending(entry => Math.Abs(entry.amount))
            .ThenBy(entry => entry.sequence)
            .ThenBy(entry => entry.evidenceId, StringComparer.Ordinal)
            .Take(KeyEventLimit)
            .Select(entry => entry.Clone())
            .ToList();
        List<HistoricalEvidenceMetric> historicalEvidence = rawEvents
            .Where(entry => entry.historicalEvidenceKind != HistoricalEvidenceKind.None)
            .GroupBy(entry => entry.historicalEvidenceKind)
            .Select(group => new HistoricalEvidenceMetric
            {
                kind = group.Key,
                strength = group.Sum(entry => Math.Abs(entry.amount)),
                occurrences = group.Sum(entry => Math.Max(1, entry.repeatCount))
            })
            .Concat(childSegments
                .SelectMany(segment => segment.historicalEvidence
                    ?? new List<HistoricalEvidenceMetric>()))
            .GroupBy(entry => entry.kind)
            .Select(group => new HistoricalEvidenceMetric
            {
                kind = group.Key,
                strength = group.Sum(entry => entry.strength),
                occurrences = group.Sum(entry => entry.occurrences)
            })
            .OrderBy(entry => entry.kind)
            .ToList();
        CompactedHistorySegment result = new CompactedHistorySegment
        {
            level = Math.Max(0, level),
            firstGeneration = Math.Max(0, firstGeneration),
            lastGeneration = Math.Max(firstGeneration, lastGeneration),
            eventCount = rawEvents.Count + childSegments.Sum(segment => segment.eventCount),
            totalMagnitude = rawEvents.Sum(entry => Math.Abs(entry.amount))
                + childSegments.Sum(segment => segment.totalMagnitude),
            metrics = metrics
                .Where(pair => pair.Key.Length > 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new UsageLedgerMetric
                {
                    metricId = pair.Key,
                    value = pair.Value
                })
                .ToList(),
            keyEvents = keyEvents,
            historicalEvidence = historicalEvidence,
            participantIds = rawEvents
                .Select(entry => entry.actorId)
                .Concat(rawEvents.Select(entry => entry.targetId))
                .Concat(childSegments.SelectMany(segment => segment.participantIds))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            sourceTags = rawEvents
                .SelectMany(entry => entry.sourceTags)
                .Concat(childSegments.SelectMany(segment => segment.sourceTags))
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList()
        };
        result.historyHash = ComputeSegmentHash(result);
        return result;
    }

    private static void MergeHierarchy(UsageLedger ledger)
    {
        int level = 0;
        while (true)
        {
            List<CompactedHistorySegment> candidates = ledger.compactedSegments
                .Where(segment => segment != null && segment.level == level)
                .OrderBy(segment => segment.firstGeneration)
                .ThenBy(segment => segment.lastGeneration)
                .ThenBy(segment => segment.historyHash, StringComparer.Ordinal)
                .Take(MergeFanout)
                .ToList();
            if (candidates.Count < MergeFanout)
            {
                int highestLevel = ledger.compactedSegments.Count == 0
                    ? 0
                    : ledger.compactedSegments.Max(segment => segment?.level ?? 0);
                if (level >= highestLevel)
                {
                    break;
                }

                level++;
                continue;
            }

            CompactedHistorySegment merged = BuildSegment(
                level + 1,
                candidates.Min(segment => segment.firstGeneration),
                candidates.Max(segment => segment.lastGeneration),
                Array.Empty<UsageLedgerEvent>(),
                candidates);
            foreach (CompactedHistorySegment candidate in candidates)
            {
                ledger.compactedSegments.Remove(candidate);
            }

            ledger.compactedSegments.Add(merged);
        }

        ledger.compactedSegments = ledger.compactedSegments
            .Where(segment => segment != null)
            .OrderBy(segment => segment.level)
            .ThenBy(segment => segment.firstGeneration)
            .ThenBy(segment => segment.historyHash, StringComparer.Ordinal)
            .ToList();
    }

    private static string ComputeSegmentHash(CompactedHistorySegment segment)
    {
        StringBuilder canonical = new StringBuilder(512);
        AppendSegment(canonical, segment, includeHash: false);
        return StableEvolutionHash.Compute(canonical.ToString());
    }

    private static void AppendSegment(
        StringBuilder builder,
        CompactedHistorySegment segment,
        bool includeHash = true)
    {
        builder.Append("S|")
            .Append(segment.level).Append('|')
            .Append(segment.firstGeneration).Append('|')
            .Append(segment.lastGeneration).Append('|')
            .Append(segment.eventCount).Append('|')
            .Append(segment.totalMagnitude.ToString("R", CultureInfo.InvariantCulture))
            .Append('|');
        if (includeHash)
        {
            builder.Append(segment.historyHash ?? string.Empty).Append('|');
        }

        foreach (UsageLedgerMetric metric in segment.metrics
                     .Where(metric => metric != null)
                     .OrderBy(metric => metric.metricId, StringComparer.Ordinal))
        {
            builder.Append("M|")
                .Append(metric.metricId ?? string.Empty).Append('|')
                .Append(metric.value.ToString("R", CultureInfo.InvariantCulture))
                .Append('|');
        }

        foreach (UsageLedgerEvent entry in segment.keyEvents
                     .Where(entry => entry != null)
                     .OrderBy(entry => entry.sequence))
        {
            AppendEvent(builder, entry);
        }

        foreach (HistoricalEvidenceMetric evidence in
                 (segment.historicalEvidence ?? new List<HistoricalEvidenceMetric>())
                 .Where(entry => entry != null)
                 .OrderBy(entry => entry.kind))
        {
            builder.Append("H|")
                .Append((int)evidence.kind).Append('|')
                .Append(evidence.strength.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(evidence.occurrences).Append('|');
        }

        foreach (string participant in segment.participantIds.OrderBy(
                     id => id,
                     StringComparer.Ordinal))
        {
            builder.Append("P|").Append(participant).Append('|');
        }

        foreach (string tag in segment.sourceTags.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            builder.Append("T|").Append(tag).Append('|');
        }
    }

    private static void AppendEvent(StringBuilder builder, UsageLedgerEvent entry)
    {
        builder.Append("E|")
            .Append(entry.sequence).Append('|')
            .Append(entry.evidenceId ?? string.Empty).Append('|')
            .Append(entry.eventId ?? string.Empty).Append('|')
            .Append(entry.actorId ?? string.Empty).Append('|')
            .Append(entry.targetId ?? string.Empty).Append('|')
            .Append(entry.amount.ToString("R", CultureInfo.InvariantCulture))
            .Append('|')
            .Append((int)entry.historicalEvidenceKind).Append('|')
            .Append(entry.outcomeId ?? string.Empty).Append('|')
            .Append(entry.generation).Append('|')
            .Append(entry.repeatCount).Append('|');
        foreach (string tag in entry.sourceTags.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            builder.Append(tag).Append(',');
        }
        builder.Append('|');
    }

    private static List<string> Normalize(IEnumerable<string> values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList() ?? new List<string>();
    }
}

public static class StableEvolutionHash
{
    public static string Compute(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        foreach (byte valueByte in bytes)
        {
            hash ^= valueByte;
            hash *= prime;
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    public static int ToSeed(params string[] values)
    {
        string hash = Compute(string.Join("|", values ?? Array.Empty<string>()));
        uint low = uint.Parse(
            hash.Substring(hash.Length - 8),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
        return unchecked((int)low);
    }
}

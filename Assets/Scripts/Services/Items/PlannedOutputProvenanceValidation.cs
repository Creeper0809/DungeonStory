using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates immutable publication receipts independently of mutable physical
/// custody. Released stacks can split, merge or be consumed; surviving markers
/// do not claim that the original batch is still present in its entirety.
/// </summary>
internal static class PlannedOutputProvenanceValidation
{
    internal static bool RequiresExactCustody(
        PlannedOutputPublicationMetadata header) => !header.Acknowledged
        || header.BatchCommitId.StartsWith(
            "production-output-batch:", StringComparison.Ordinal);

    internal static void ValidateFrozenBatch(
        IReadOnlyList<PlannedOutputPublicationMetadata> entries)
    {
        if (entries == null || entries.Count == 0)
            throw new InvalidOperationException("Planned-output provenance batch is empty.");

        PlannedOutputPublicationMetadata header = entries[0];
        foreach (PlannedOutputPublicationMetadata entry in entries)
        {
            if (entry.BatchCommitId != header.BatchCommitId
                || entry.Acknowledged != header.Acknowledged
                || entry.OutcomeFingerprint != header.OutcomeFingerprint
                || entry.PlannedOutputFingerprint != header.PlannedOutputFingerprint
                || entry.BatchStackCount != header.BatchStackCount
                || entry.BatchQuantity != header.BatchQuantity
                || entry.BatchMassGrams != header.BatchMassGrams
                || entry.StackOrdinal < 0 || entry.StackOrdinal >= entry.LineStackCount
                || entry.LineStackCount > entry.BatchStackCount
                || entry.LineQuantity > entry.BatchQuantity
                || entry.LineMassGrams > entry.BatchMassGrams
                || entry.Quantity > entry.LineQuantity
                || entry.MassGrams > entry.LineMassGrams
                || entry.LineStackCount > entry.LineQuantity
                || entry.BatchStackCount > entry.BatchQuantity)
                Fail(header.BatchCommitId, "conflicting or impossible frozen batch header");
        }

        List<PlannedOutputPublicationMetadata> lineHeaders = new();
        foreach (var line in entries.GroupBy(value => value.OutputLineId,
                     StringComparer.Ordinal))
        {
            PlannedOutputPublicationMetadata lineHeader = line.First();
            lineHeaders.Add(lineHeader);
            if (line.Any(value => value.LineStackCount != lineHeader.LineStackCount
                    || value.LineQuantity != lineHeader.LineQuantity
                    || value.LineMassGrams != lineHeader.LineMassGrams
                    || value.ItemId != lineHeader.ItemId))
                Fail(header.BatchCommitId, "conflicting frozen line header: " + line.Key);

            List<PlannedOutputPublicationMetadata> originals = new();
            foreach (var ordinal in line.GroupBy(value => value.StackOrdinal))
            {
                PlannedOutputPublicationMetadata original = ordinal.First();
                // Split copies must agree on their immutable original receipt.
                if (ordinal.Any(value => value.Quantity != original.Quantity
                        || value.MassGrams != original.MassGrams
                        || value.ComponentSignature != original.ComponentSignature
                        || value.PreparedComponentFingerprint != original.PreparedComponentFingerprint))
                    Fail(header.BatchCommitId, "conflicting copies of frozen stack receipt: " + line.Key);
                originals.Add(original);
            }
            long quantity = originals.Sum(value => (long)value.Quantity);
            long mass = originals.Sum(value => value.MassGrams);
            if (originals.Count > lineHeader.LineStackCount
                || quantity > lineHeader.LineQuantity || mass > lineHeader.LineMassGrams
                || originals.Count == lineHeader.LineStackCount
                    && (quantity != lineHeader.LineQuantity || mass != lineHeader.LineMassGrams))
                Fail(header.BatchCommitId, "inconsistent frozen line totals: " + line.Key);
        }

        long stackCount = lineHeaders.Sum(value => (long)value.LineStackCount);
        long totalQuantity = lineHeaders.Sum(value => (long)value.LineQuantity);
        long totalMass = lineHeaders.Sum(value => value.LineMassGrams);
        if (stackCount > header.BatchStackCount || totalQuantity > header.BatchQuantity
            || totalMass > header.BatchMassGrams
            || stackCount == header.BatchStackCount
                && (totalQuantity != header.BatchQuantity || totalMass != header.BatchMassGrams))
            Fail(header.BatchCommitId, "inconsistent frozen batch totals");
    }

    // Call only after ValidateFrozenBatch. This does not manufacture missing
    // stacks or infer custody from history; active owners still need an exact
    // candidate and must reject a historical-only/missing candidate.
    internal static bool HasExactQuantityTopology(
        IReadOnlyList<(PlannedOutputPublicationMetadata Metadata, int Quantity)> entries)
    {
        PlannedOutputPublicationMetadata header = entries[0].Metadata;
        if (entries.Count != header.BatchStackCount
            || entries.Any(value => value.Quantity != value.Metadata.Quantity)
            || entries.Sum(value => (long)value.Quantity) != header.BatchQuantity)
            return false;
        return entries.GroupBy(value => value.Metadata.OutputLineId, StringComparer.Ordinal)
            .All(line => line.Count() == line.First().Metadata.LineStackCount
                && line.Select(value => value.Metadata.StackOrdinal).OrderBy(value => value)
                    .SequenceEqual(Enumerable.Range(0, line.Count())));
    }

    private static void Fail(string batchId, string reason) =>
        throw new InvalidOperationException(
            "Invalid planned-output provenance: " + batchId + ": " + reason);
}

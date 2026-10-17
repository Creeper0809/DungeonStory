using System;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation.Facts;

internal static class ProductionCompletionParentCausation
{
    internal static GameplayOutcomeCausation Capture(
        DomainFactDelivery parentPublication,
        ProductionBillId billId,
        int cycleSequence)
    {
        if (!billId.IsValid || cycleSequence <= 0
            || parentPublication.Phase is not DomainFactDeliveryPhase.Delivered
                and not DomainFactDeliveryPhase.AcceptedPendingDelivery
            || !IsDigest(parentPublication.Fingerprint)
            || !GameplayDomainFactDeliveryQuery.TryReadKey(
                parentPublication.OperationKey,
                out GameplayResultKey key))
        {
            throw new InvalidOperationException(
                "production-completion-parent-publication-invalid");
        }

        GameplayResultKey expected = new(
            ProductionCombatOutcomeIds.ProductionProducerId,
            new GameplayOperationId(
                "production-cycle:" + billId.Value + ":"
                + cycleSequence.ToString(CultureInfo.InvariantCulture)),
            cycleSequence,
            0);
        if (key != expected)
        {
            throw new InvalidOperationException(
                "production-completion-parent-key-mismatch");
        }

        GameplayOutcomeId parentId = ReadParent(
            parentPublication.PublicationId);
        return new GameplayOutcomeCausation(
            parentId,
            key.OperationId,
            "production-declared-loss-observation");
    }

    private static GameplayOutcomeId ReadParent(string value)
    {
        int separator = value?.LastIndexOf('/') ?? -1;
        if (separator <= 0
            || !long.TryParse(
                value.Substring(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long sequence))
        {
            throw new InvalidOperationException(
                "production-completion-parent-id-invalid");
        }
        GameplayOutcomeId id = new(
            new GameplayOutcomeRunId(value.Substring(0, separator)),
            sequence);
        if (!id.IsValid
            || !string.Equals(id.ToString(), value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-completion-parent-id-noncanonical");
        }
        return id;
    }

    private static bool IsDigest(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}

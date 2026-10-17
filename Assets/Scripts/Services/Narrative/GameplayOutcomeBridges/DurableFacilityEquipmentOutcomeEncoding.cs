using System;
using System.Collections.Generic;

internal static class DurableFacilityEquipmentOutcomeEncoding
{
    private const string ReservedStackId =
        "item-stack:durable-equipment-reservation";
    private const string ReservedItemId =
        "item:durable-equipment-reservation";

    internal static MigratedProducerOutcomeDetails Append(
        in MigratedProducerOutcomeDetails details,
        DurableFacilityEquipmentUseEvidence equipment)
    {
        if (equipment == null)
            return details;
        return Append(
            details,
            equipment.StackId,
            equipment.ItemId,
            equipment.ToCanonicalString());
    }

    internal static MigratedProducerOutcomeDetails Append(
        in MigratedProducerOutcomeDetails details,
        string stackId,
        string itemId,
        string canonicalEvidence)
    {
        if (string.IsNullOrEmpty(stackId)
            && string.IsNullOrEmpty(itemId)
            && string.IsNullOrEmpty(canonicalEvidence))
        {
            return details;
        }
        return AppendRequired(details, stackId, itemId, canonicalEvidence);
    }

    internal static MigratedProducerOutcomeDetails AppendReservationShape(
        in MigratedProducerOutcomeDetails details) => Append(
        details,
        ReservedStackId,
        ReservedItemId,
        "semantics=wear;reservation=shape-only");

    private static MigratedProducerOutcomeDetails AppendRequired(
        in MigratedProducerOutcomeDetails details,
        string stackId,
        string itemId,
        string canonicalEvidence)
    {
        if (!details.IsValid
            || !GameplayOutcomeStableIdSyntax.IsValid(stackId)
            || !GameplayOutcomeStableIdSyntax.IsValid(itemId)
            || !GameplayOutcomeLedger.IsValidBoundedUtf16(
                canonicalEvidence,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
        {
            throw new ArgumentException(
                "Durable equipment outcome evidence is invalid.");
        }

        var subjects = new List<MigratedProducerOutcomeSubject>(
            details.Subjects);
        subjects.Add(new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.ItemStackKind,
            stackId,
            itemId,
            MigratedProducerOutcomeIds.EquipmentRole));

        var facts = new List<GameplayOutcomeFact>(details.Facts)
        {
            new(
                MigratedProducerOutcomeIds.EquipmentDefinitionFact,
                itemId),
            new(
                MigratedProducerOutcomeIds.DetailFact,
                canonicalEvidence)
        };
        if (subjects.Count > 16 || facts.Count > 14)
        {
            throw new InvalidOperationException(
                "Durable equipment outcome evidence exceeds fixed ledger capacity.");
        }

        return new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            details.Metrics,
            facts.ToArray(),
            details.Summary,
            details.Causation);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

internal static class CharacterMedicalSupplyDestinationDrainCrossAggregateJoin
{
    private const string OwnerStablePrefix = "character-medical-order:";
    private const string ParentOperationPrefix =
        "character-medical-supply-drain:";

    internal static void Validate(
        IReadOnlyList<CharacterMedicalOrder> orders,
        IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot> children)
    {
        CharacterMedicalOrder[] ownerOrders = (orders
                ?? Array.Empty<CharacterMedicalOrder>())
            .Where(value => value != null)
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, CharacterMedicalOrder> orderById = ownerOrders
            .ToDictionary(value => value.orderId, StringComparer.Ordinal);
        Dictionary<string, FacilityBufferDestinationCustodyDrainSnapshot>
            childByStep = new(StringComparer.Ordinal);

        foreach (FacilityBufferDestinationCustodyDrainSnapshot child in
                 (children
                     ?? Array.Empty<
                         FacilityBufferDestinationCustodyDrainSnapshot>())
                 .Where(ClaimsCharacterMedicalDomain)
                 .OrderBy(value => value.StepOperationId,
                     StringComparer.Ordinal))
        {
            if (child == null
                || string.IsNullOrEmpty(child.OwnerSubjectId)
                || !orderById.ContainsKey(child.OwnerSubjectId)
                || !string.Equals(
                    child.OwnerStableId,
                    CharacterMedicalSupplyDestinationAuthority
                        .FormatOwnerStableId(child.OwnerSubjectId),
                    StringComparison.Ordinal)
                || !childByStep.TryAdd(child.StepOperationId, child))
            {
                throw new InvalidOperationException(
                    "character-medical-supply-drain-lower-owner-invalid:"
                    + (child?.StepOperationId ?? string.Empty));
            }
        }

        HashSet<string> joinedSteps = new(StringComparer.Ordinal);
        foreach (CharacterMedicalOrder order in ownerOrders)
        {
            foreach (CharacterMedicalSupplyDestinationDrainJoinData upper in
                     (order.treatmentDestinationDrainJoins
                          ?? new List<
                              CharacterMedicalSupplyDestinationDrainJoinData>())
                     .Where(value => value != null)
                     .OrderBy(value => value.destinationSequence))
            {
                string failureReason = string.Empty;
                if (!childByStep.TryGetValue(
                        upper.stepOperationId,
                        out FacilityBufferDestinationCustodyDrainSnapshot child)
                    || !joinedSteps.Add(upper.stepOperationId)
                    || !CharacterMedicalSupplyDestinationDrainJoin.TryValidate(
                        order,
                        upper,
                        child,
                        out failureReason))
                {
                    throw new InvalidOperationException(
                        "character-medical-supply-drain-cross-join-invalid:"
                        + order.orderId + ":" + upper.destinationSequence
                        + ":" + (failureReason ?? string.Empty));
                }
            }
        }

        if (joinedSteps.Count != childByStep.Count)
        {
            string lowerOnly = childByStep.Keys
                .Where(value => !joinedSteps.Contains(value))
                .OrderBy(value => value, StringComparer.Ordinal)
                .FirstOrDefault() ?? string.Empty;
            throw new InvalidOperationException(
                "character-medical-supply-drain-lower-without-upper:"
                + lowerOnly);
        }
    }

    private static bool ClaimsCharacterMedicalDomain(
        FacilityBufferDestinationCustodyDrainSnapshot value)
    {
        if (value == null)
        {
            return false;
        }
        return (value.OwnerStableId ?? string.Empty).StartsWith(
                OwnerStablePrefix,
                StringComparison.Ordinal)
            || (value.ParentOperationId ?? string.Empty).StartsWith(
                ParentOperationPrefix,
                StringComparison.Ordinal)
            || (value.StepOperationId ?? string.Empty).StartsWith(
                ParentOperationPrefix,
                StringComparison.Ordinal);
    }
}

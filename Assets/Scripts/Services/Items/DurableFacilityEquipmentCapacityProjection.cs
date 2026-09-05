using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DefinitionMassDurableFacilityEquipmentCapacityProjector :
    IDurableFacilityEquipmentCapacityProjector
{
    private readonly IPhysicalItemMassQuery mass;

    public DefinitionMassDurableFacilityEquipmentCapacityProjector(
        IPhysicalItemMassQuery mass)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
    }

    public string PolicyKind =>
        DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind;

    public bool TryProjectMaximumMass(
        DurableFacilityEquipmentAssignment assignment,
        out DurableFacilityEquipmentCapacityProjection projection,
        out string failureReason)
    {
        projection = null;
        failureReason = string.Empty;
        if (assignment == null
            || !string.Equals(
                assignment.CapacityPolicyKind,
                PolicyKind,
                StringComparison.Ordinal))
        {
            failureReason = "durable-equipment-definition-mass-assignment-invalid";
            return false;
        }

        try
        {
            long total = 0L;
            List<string> evidence = new();
            foreach (DurableFacilityEquipmentRequirement requirement in
                     assignment.Requirements)
            {
                long unit = mass.GetDefinitionUnitMass(requirement.ItemId).Value;
                if (unit <= 0L)
                {
                    failureReason =
                        "durable-equipment-definition-mass-nonpositive:"
                        + requirement.ItemId.Value;
                    return false;
                }
                total = checked(total + checked(
                    unit * requirement.RequiredQuantity));
                evidence.Add(requirement.RequirementId);
                evidence.Add(requirement.ItemId.Value);
                evidence.Add(requirement.RequiredQuantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
                evidence.Add(unit.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            PhysicalMassGrams maximum = new(total);
            long revision = mass.AuthorityRevision;
            string assignmentFingerprint =
                DurableFacilityEquipmentFingerprint.CreateAssignment(assignment);
            string sourceFingerprint =
                DurableFacilityEquipmentFingerprint.CreateProjectionSource(
                    assignmentFingerprint,
                    PolicyKind,
                    revision,
                    maximum,
                    evidence);
            projection = new DurableFacilityEquipmentCapacityProjection(
                PolicyKind,
                maximum,
                revision,
                sourceFingerprint);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or ArgumentException
            or OverflowException)
        {
            projection = null;
            failureReason = "durable-equipment-definition-mass-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }
}

public sealed class DurableFacilityEquipmentCapacityProjectionRegistry :
    IDurableFacilityEquipmentCapacityProjectionQuery
{
    private readonly IReadOnlyDictionary<string,
        IDurableFacilityEquipmentCapacityProjector> byKind;

    public DurableFacilityEquipmentCapacityProjectionRegistry(
        IEnumerable<IDurableFacilityEquipmentCapacityProjector> projectors)
    {
        IDurableFacilityEquipmentCapacityProjector[] source = (projectors
                ?? throw new ArgumentNullException(nameof(projectors)))
            .OrderBy(value => value?.PolicyKind, StringComparer.Ordinal)
            .ToArray();
        if (source.Length == 0
            || source.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.PolicyKind)
                || !string.Equals(value.PolicyKind, value.PolicyKind.Trim(),
                    StringComparison.Ordinal))
            || source.Select(value => value.PolicyKind)
                .Distinct(StringComparer.Ordinal).Count() != source.Length)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment capacity projectors are missing, duplicate, or non-canonical.");
        }
        byKind = source.ToDictionary(
            value => value.PolicyKind,
            StringComparer.Ordinal);
    }

    public bool TryProjectMaximumMass(
        DurableFacilityEquipmentAssignment assignment,
        out DurableFacilityEquipmentCapacityProjection projection,
        out string failureReason)
    {
        projection = null;
        failureReason = string.Empty;
        if (assignment == null
            || !byKind.TryGetValue(
                assignment.CapacityPolicyKind,
                out IDurableFacilityEquipmentCapacityProjector projector))
        {
            failureReason = "durable-equipment-capacity-policy-unregistered:"
                + (assignment?.CapacityPolicyKind ?? string.Empty);
            return false;
        }
        bool succeeded = projector.TryProjectMaximumMass(
            assignment,
            out projection,
            out failureReason);
        if (succeeded)
        {
            if (projection == null
                || !string.Equals(
                    projection.PolicyKind,
                    assignment.CapacityPolicyKind,
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(failureReason))
            {
                projection = null;
                failureReason =
                    "durable-equipment-capacity-projector-invalid-success:"
                    + assignment.CapacityPolicyKind;
                return false;
            }
            return true;
        }
        if (projection != null || string.IsNullOrWhiteSpace(failureReason))
        {
            projection = null;
            failureReason =
                "durable-equipment-capacity-projector-invalid-failure:"
                + assignment.CapacityPolicyKind;
        }
        return false;
    }
}

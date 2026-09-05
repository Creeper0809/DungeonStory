using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DurableFacilityEquipmentPolicyRegistry :
    IDurableFacilityEquipmentPolicyQuery
{
    private readonly IReadOnlyDictionary<string,
        DurableFacilityEquipmentPolicy> byId;
    private readonly IReadOnlyList<DurableFacilityEquipmentPolicy> ordered;

    public DurableFacilityEquipmentPolicyRegistry(
        IEnumerable<IDurableFacilityEquipmentPolicySource> sources)
    {
        IDurableFacilityEquipmentPolicySource[] sourceArray = (sources
                ?? throw new ArgumentNullException(nameof(sources)))
            .OrderBy(value => value?.SourceId, StringComparer.Ordinal)
            .ToArray();
        if (sourceArray.Length == 0
            || sourceArray.Any(value => value == null
                || !Canonical(value.SourceId)
                || value.Revision <= 0L)
            || sourceArray.Select(value => value.SourceId)
                .Distinct(StringComparer.Ordinal).Count() != sourceArray.Length)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment policy sources are missing, duplicate, or invalid.");
        }

        List<DurableFacilityEquipmentPolicy> policies = new();
        // Revision is a deterministic freshness token, not an arithmetic
        // quantity. Keep the rolling hash modular so adding valid policy
        // sources cannot make container construction fail by overflowing a
        // signed long.
        ulong revision = 17UL;
        foreach (IDurableFacilityEquipmentPolicySource source in sourceArray)
        {
            DurableFacilityEquipmentPolicy[] captured = (source
                    .CapturePolicies()
                    ?? throw new InvalidOperationException(
                        "Durable facility-equipment policy source returned null: "
                        + source.SourceId))
                .OrderBy(value => value?.PolicyId, StringComparer.Ordinal)
                .ToArray();
            if (captured.Length == 0 || captured.Any(value => value == null))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment policy source is empty or invalid: "
                    + source.SourceId);
            }
            revision = unchecked(revision * 31UL + (ulong)source.Revision);
            foreach (DurableFacilityEquipmentPolicy policy in captured)
            {
                revision = unchecked(revision * 31UL + (ulong)policy.Revision);
                policies.Add(policy);
            }
        }

        DurableFacilityEquipmentPolicy[] sorted = policies
            .OrderBy(value => value.PolicyId, StringComparer.Ordinal)
            .ToArray();
        if (sorted.Select(value => value.PolicyId)
                .Distinct(StringComparer.Ordinal).Count() != sorted.Length)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment policy ID is registered more than once.");
        }
        Revision = (long)(revision & 0x7fffffffffffffffUL);
        if (Revision == 0L)
            Revision = 1L;
        ordered = Array.AsReadOnly(sorted);
        byId = sorted.ToDictionary(value => value.PolicyId,
            StringComparer.Ordinal);
    }

    public long Revision { get; }

    public bool TryGetPolicy(
        string policyId,
        out DurableFacilityEquipmentPolicy policy) =>
        byId.TryGetValue(policyId ?? string.Empty, out policy);

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        ordered;

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class DurableFacilityEquipmentUsabilityRegistry :
    IDurableFacilityEquipmentUsabilityQuery
{
    private readonly IReadOnlyDictionary<string,
        IDurableFacilityEquipmentUsabilityPolicy> byKind;

    public DurableFacilityEquipmentUsabilityRegistry(
        IEnumerable<IDurableFacilityEquipmentUsabilityPolicy> policies)
    {
        IDurableFacilityEquipmentUsabilityPolicy[] source = (policies
                ?? throw new ArgumentNullException(nameof(policies)))
            .OrderBy(value => value?.PolicyKind, StringComparer.Ordinal)
            .ToArray();
        if (source.Length == 0
            || source.Any(value => value == null
                || !Canonical(value.PolicyKind))
            || source.Select(value => value.PolicyKind)
                .Distinct(StringComparer.Ordinal).Count() != source.Length)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment usability policies are missing, duplicate, or invalid.");
        }
        byKind = source.ToDictionary(value => value.PolicyKind,
            StringComparer.Ordinal);
    }

    public bool TryEvaluate(
        string policyKind,
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        out DurableFacilityEquipmentUsabilityResult result,
        out string failureReason)
    {
        result = default;
        failureReason = string.Empty;
        if (!byKind.TryGetValue(
                policyKind ?? string.Empty,
                out IDurableFacilityEquipmentUsabilityPolicy policy))
        {
            failureReason = "durable-equipment-usability-policy-unregistered:"
                + (policyKind ?? string.Empty);
            return false;
        }
        try
        {
            result = policy.Evaluate(requirement, subject);
            if (string.IsNullOrWhiteSpace(result.ReasonCode))
            {
                failureReason =
                    "durable-equipment-usability-policy-invalid-result:"
                    + policyKind;
                result = default;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            result = default;
            failureReason = "durable-equipment-usability-policy-failed:"
                + policyKind + ":" + exception.GetType().Name + ":"
                + exception.Message;
            return false;
        }
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class PositiveDurabilityComponentUsabilityPolicy :
    IDurableFacilityEquipmentUsabilityPolicy
{
    public string PolicyKind =>
        DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent;

    public DurableFacilityEquipmentUsabilityResult Evaluate(
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject)
    {
        if (requirement == null || subject == null)
            throw new ArgumentNullException(
                requirement == null ? nameof(requirement) : nameof(subject));
        if (!requirement.ItemId.Equals(subject.ItemId))
        {
            return new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Incompatible,
                "durable-equipment-definition-mismatch");
        }

        DurableFacilityEquipmentComponentSnapshot component = subject.Components
            .SingleOrDefault(value => string.Equals(
                value.ComponentTypeId,
                ItemInstanceComponentIds.Durability,
                StringComparison.Ordinal));
        if (component == null)
        {
            return new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Incompatible,
                "durable-equipment-durability-component-missing");
        }
        DurableFacilityEquipmentComponentValueSnapshot? current =
            FindDecimal(component, "current");
        DurableFacilityEquipmentComponentValueSnapshot? maximum =
            FindDecimal(component, "maximum");
        if (!current.HasValue
            || !maximum.HasValue
            || double.IsNaN(current.Value.DecimalValue)
            || double.IsInfinity(current.Value.DecimalValue)
            || double.IsNaN(maximum.Value.DecimalValue)
            || double.IsInfinity(maximum.Value.DecimalValue)
            || maximum.Value.DecimalValue <= 0d
            || current.Value.DecimalValue > maximum.Value.DecimalValue)
        {
            return new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Incompatible,
                "durable-equipment-durability-component-invalid");
        }
        return current.Value.DecimalValue > 0d
            ? new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Usable,
                "durable-equipment-usable")
            : new DurableFacilityEquipmentUsabilityResult(
                DurableFacilityEquipmentUsabilityDisposition.Exhausted,
                "durable-equipment-exhausted");
    }

    private static DurableFacilityEquipmentComponentValueSnapshot? FindDecimal(
        DurableFacilityEquipmentComponentSnapshot component,
        string key)
    {
        DurableFacilityEquipmentComponentValueSnapshot[] matches =
            component.Values.Where(value => string.Equals(
                    value.Key,
                    key,
                    StringComparison.Ordinal)
                && value.Kind == ItemStateValueKind.Decimal)
                .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}

public sealed class DurableFacilityEquipmentWearRegistry :
    IDurableFacilityEquipmentWearQuery
{
    private readonly IReadOnlyDictionary<string,
        IDurableFacilityEquipmentWearPolicy> byKind;

    public DurableFacilityEquipmentWearRegistry(
        IEnumerable<IDurableFacilityEquipmentWearPolicy> policies)
    {
        IDurableFacilityEquipmentWearPolicy[] source = (policies
                ?? throw new ArgumentNullException(nameof(policies)))
            .OrderBy(value => value?.PolicyKind, StringComparer.Ordinal)
            .ToArray();
        if (source.Length == 0
            || source.Any(value => value == null
                || !Canonical(value.PolicyKind))
            || source.Select(value => value.PolicyKind)
                .Distinct(StringComparer.Ordinal).Count() != source.Length)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment wear policies are missing, duplicate, or invalid.");
        }
        byKind = source.ToDictionary(value => value.PolicyKind,
            StringComparer.Ordinal);
    }

    public bool TryProject(
        string policyKind,
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        double wearAmount,
        out DurableFacilityEquipmentWearProjection projection,
        out string failureReason)
    {
        projection = null;
        failureReason = string.Empty;
        if (!byKind.TryGetValue(
                policyKind ?? string.Empty,
                out IDurableFacilityEquipmentWearPolicy policy))
        {
            failureReason = "durable-equipment-wear-policy-unregistered:"
                + (policyKind ?? string.Empty);
            return false;
        }
        try
        {
            projection = policy.Project(requirement, subject, wearAmount);
            if (projection == null
                || !string.Equals(projection.PolicyKind, policy.PolicyKind,
                    StringComparison.Ordinal))
            {
                projection = null;
                failureReason = "durable-equipment-wear-policy-invalid-result:"
                    + policyKind;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException
                or OverflowException)
        {
            projection = null;
            failureReason = "durable-equipment-wear-policy-failed:"
                + policyKind + ":" + exception.GetType().Name + ":"
                + exception.Message;
            return false;
        }
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class PositiveDurabilityComponentWearPolicy :
    IDurableFacilityEquipmentWearPolicy
{
    public string PolicyKind =>
        DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent;

    public DurableFacilityEquipmentWearProjection Project(
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        double wearAmount)
    {
        if (requirement == null || subject == null)
            throw new ArgumentNullException(
                requirement == null ? nameof(requirement) : nameof(subject));
        if (!requirement.ItemId.Equals(subject.ItemId))
            throw new InvalidOperationException(
                "Durable equipment wear definition does not match the requirement.");
        if (double.IsNaN(wearAmount)
            || double.IsInfinity(wearAmount)
            || wearAmount <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(wearAmount));
        }

        DurableFacilityEquipmentComponentSnapshot component = subject.Components
            .SingleOrDefault(value => string.Equals(
                value.ComponentTypeId,
                ItemInstanceComponentIds.Durability,
                StringComparison.Ordinal));
        if (component == null)
            throw new InvalidOperationException(
                "Durable equipment wear requires one durability component.");
        double current = RequireDecimal(component, "current");
        double maximum = RequireDecimal(component, "maximum");
        if (double.IsNaN(current)
            || double.IsInfinity(current)
            || double.IsNaN(maximum)
            || double.IsInfinity(maximum)
            || current <= 0d
            || maximum <= 0d
            || current > maximum)
        {
            throw new InvalidOperationException(
                "Durable equipment wear component values are invalid.");
        }

        double after = Math.Max(0d, current - wearAmount);
        ItemInstanceComponentSaveData replacement = new()
        {
            componentTypeId = ItemInstanceComponentIds.Durability,
            schemaVersion = component.SchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = "current",
                    kind = ItemStateValueKind.Decimal,
                    decimalValue = after
                },
                new()
                {
                    key = "maximum",
                    kind = ItemStateValueKind.Decimal,
                    decimalValue = maximum
                }
            }
        };
        return new DurableFacilityEquipmentWearProjection(
            PolicyKind,
            replacement,
            after <= 0d,
            current,
            after);
    }

    private static double RequireDecimal(
        DurableFacilityEquipmentComponentSnapshot component,
        string key)
    {
        DurableFacilityEquipmentComponentValueSnapshot[] matches =
            component.Values.Where(value => string.Equals(
                    value.Key,
                    key,
                    StringComparison.Ordinal)
                && value.Kind == ItemStateValueKind.Decimal)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Durable equipment wear component field is missing or duplicate: "
                + key);
        return matches[0].DecimalValue;
    }
}

public static class DurableFacilityEquipmentUseSubjectCapture
{
    public static DurableFacilityEquipmentUseSubject Capture(
        WorldItemStackSnapshot stack)
    {
        if (stack == null)
            throw new ArgumentNullException(nameof(stack));
        DurableFacilityEquipmentComponentSnapshot[] components =
            (stack.Components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Select(CaptureComponent)
            .ToArray();
        return new DurableFacilityEquipmentUseSubject(
            stack.StackId,
            stack.ContentRevision,
            (ItemDefinitionId)stack.ItemId,
            stack.Quantity,
            components);
    }

    private static DurableFacilityEquipmentComponentSnapshot CaptureComponent(
        ItemInstanceComponentSaveData source)
    {
        if (source == null)
            throw new ArgumentException(
                "Durable equipment component source is null.");
        return new DurableFacilityEquipmentComponentSnapshot(
            source.componentTypeId,
            source.schemaVersion,
            (source.values ?? new List<ItemStateValueSaveData>())
            .Where(value => value != null)
            .Select(value =>
                new DurableFacilityEquipmentComponentValueSnapshot(
                    value.key,
                    value.kind,
                    value.stringValue,
                    value.integerValue,
                    value.decimalValue,
                    value.booleanValue)));
    }
}

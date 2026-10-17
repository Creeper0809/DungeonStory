using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public static class DurableFacilityEquipmentPolicyKinds
{
    public const string PositiveDurabilityComponent =
        "component-durability-positive";
}

public sealed class DurableFacilityEquipmentPolicy
{
    private readonly IReadOnlyList<DurableFacilityEquipmentRequirement>
        requirements;

    public DurableFacilityEquipmentPolicy(
        string policyId,
        long revision,
        string logicalOwnerDomain,
        string capacityPolicyKind,
        string usabilityPolicyKind,
        IEnumerable<DurableFacilityEquipmentRequirement> requirements)
    {
        DurableFacilityEquipmentRequirement[] copied = (requirements
                ?? throw new ArgumentNullException(nameof(requirements)))
            .OrderBy(value => value?.RequirementId, StringComparer.Ordinal)
            .ToArray();
        _ = new DurableFacilityEquipmentSlotKey(
            logicalOwnerDomain,
            "policy-validation");
        if (!Canonical(policyId)
            || revision <= 0L
            || !Canonical(capacityPolicyKind)
            || !Canonical(usabilityPolicyKind)
            || copied.Length == 0
            || copied.Any(value => value == null)
            || copied.Select(value => value.RequirementId)
                .Distinct(StringComparer.Ordinal).Count() != copied.Length)
        {
            throw new ArgumentException(
                "Durable facility-equipment policy is invalid.");
        }
        PolicyId = policyId;
        Revision = revision;
        LogicalOwnerDomain = logicalOwnerDomain;
        CapacityPolicyKind = capacityPolicyKind;
        UsabilityPolicyKind = usabilityPolicyKind;
        this.requirements = Array.AsReadOnly(copied);
    }

    public string PolicyId { get; }
    public long Revision { get; }
    public string LogicalOwnerDomain { get; }
    public string CapacityPolicyKind { get; }
    public string UsabilityPolicyKind { get; }
    public IReadOnlyList<DurableFacilityEquipmentRequirement> Requirements =>
        requirements;

    public DurableFacilityEquipmentAssignment CreateAssignment(
        string ownerSubjectId,
        BuildingInstanceId ownerFacilityId,
        Vector2Int dropPosition) => new(
        new DurableFacilityEquipmentSlotKey(
            LogicalOwnerDomain,
            ownerSubjectId),
        PolicyId,
        Revision,
        CapacityPolicyKind,
        UsabilityPolicyKind,
        ownerFacilityId,
        dropPosition,
        requirements);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IDurableFacilityEquipmentPolicySource
{
    string SourceId { get; }
    long Revision { get; }
    IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies();
}

public interface IDurableFacilityEquipmentPolicyQuery
{
    long Revision { get; }

    bool TryGetPolicy(
        string policyId,
        out DurableFacilityEquipmentPolicy policy);

    IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies();
}

public readonly struct DurableFacilityEquipmentComponentValueSnapshot
{
    public DurableFacilityEquipmentComponentValueSnapshot(
        string key,
        ItemStateValueKind kind,
        string stringValue,
        long integerValue,
        double decimalValue,
        bool booleanValue)
    {
        if (!Canonical(key) || !Enum.IsDefined(typeof(ItemStateValueKind), kind))
            throw new ArgumentException(
                "Durable equipment component value is invalid.");
        Key = key;
        Kind = kind;
        StringValue = stringValue ?? string.Empty;
        IntegerValue = integerValue;
        DecimalValue = decimalValue;
        BooleanValue = booleanValue;
    }

    public string Key { get; }
    public ItemStateValueKind Kind { get; }
    public string StringValue { get; }
    public long IntegerValue { get; }
    public double DecimalValue { get; }
    public bool BooleanValue { get; }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class DurableFacilityEquipmentComponentSnapshot
{
    private readonly IReadOnlyList<
        DurableFacilityEquipmentComponentValueSnapshot> values;

    public DurableFacilityEquipmentComponentSnapshot(
        string componentTypeId,
        int schemaVersion,
        IEnumerable<DurableFacilityEquipmentComponentValueSnapshot> values)
    {
        DurableFacilityEquipmentComponentValueSnapshot[] copied = (values
                ?? throw new ArgumentNullException(nameof(values)))
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        if (!Canonical(componentTypeId)
            || schemaVersion <= 0
            || copied.Select(value => value.Key)
                .Distinct(StringComparer.Ordinal).Count() != copied.Length)
        {
            throw new ArgumentException(
                "Durable equipment component snapshot is invalid.");
        }
        ComponentTypeId = componentTypeId;
        SchemaVersion = schemaVersion;
        this.values = Array.AsReadOnly(copied);
    }

    public string ComponentTypeId { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<DurableFacilityEquipmentComponentValueSnapshot>
        Values => values;

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class DurableFacilityEquipmentUseSubject
{
    private readonly IReadOnlyList<DurableFacilityEquipmentComponentSnapshot>
        components;

    public DurableFacilityEquipmentUseSubject(
        string stackId,
        long contentRevision,
        ItemDefinitionId itemId,
        int quantity,
        IEnumerable<DurableFacilityEquipmentComponentSnapshot> components)
    {
        DurableFacilityEquipmentComponentSnapshot[] copied = (components
                ?? throw new ArgumentNullException(nameof(components)))
            .OrderBy(value => value?.ComponentTypeId, StringComparer.Ordinal)
            .ToArray();
        if (!Canonical(stackId)
            || contentRevision < 0L
            || !itemId.IsValid
            || quantity <= 0
            || copied.Any(value => value == null)
            || copied.Select(value => value.ComponentTypeId)
                .Distinct(StringComparer.Ordinal).Count() != copied.Length)
        {
            throw new ArgumentException(
                "Durable equipment use subject is invalid.");
        }
        StackId = stackId;
        ContentRevision = contentRevision;
        ItemId = itemId;
        Quantity = quantity;
        this.components = Array.AsReadOnly(copied);
    }

    public string StackId { get; }
    public long ContentRevision { get; }
    public ItemDefinitionId ItemId { get; }
    public int Quantity { get; }
    public IReadOnlyList<DurableFacilityEquipmentComponentSnapshot>
        Components => components;

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public enum DurableFacilityEquipmentUsabilityDisposition
{
    Usable = 0,
    Exhausted = 1,
    Incompatible = 2
}

public readonly struct DurableFacilityEquipmentUsabilityResult
{
    public DurableFacilityEquipmentUsabilityResult(
        DurableFacilityEquipmentUsabilityDisposition disposition,
        string reasonCode)
    {
        if (!Enum.IsDefined(
                typeof(DurableFacilityEquipmentUsabilityDisposition),
                disposition)
            || string.IsNullOrWhiteSpace(reasonCode)
            || !string.Equals(reasonCode, reasonCode.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Durable equipment usability result is invalid.");
        }
        Disposition = disposition;
        ReasonCode = reasonCode;
    }

    public DurableFacilityEquipmentUsabilityDisposition Disposition { get; }
    public string ReasonCode { get; }
    public bool IsUsable =>
        Disposition == DurableFacilityEquipmentUsabilityDisposition.Usable;
}

public interface IDurableFacilityEquipmentUsabilityPolicy
{
    string PolicyKind { get; }

    DurableFacilityEquipmentUsabilityResult Evaluate(
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject);
}

public interface IDurableFacilityEquipmentUsabilityQuery
{
    bool TryEvaluate(
        string policyKind,
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        out DurableFacilityEquipmentUsabilityResult result,
        out string failureReason);
}

public sealed class DurableFacilityEquipmentWearProjection
{
    public DurableFacilityEquipmentWearProjection(
        string policyKind,
        ItemInstanceComponentSaveData replacementComponent,
        bool exhaustedAfter,
        double currentBefore,
        double currentAfter)
    {
        if (!Canonical(policyKind)
            || replacementComponent == null
            || !Canonical(replacementComponent.componentTypeId)
            || double.IsNaN(currentBefore)
            || double.IsInfinity(currentBefore)
            || double.IsNaN(currentAfter)
            || double.IsInfinity(currentAfter)
            || currentBefore <= 0d
            || currentAfter < 0d
            || currentAfter > currentBefore
            || exhaustedAfter != (currentAfter <= 0d))
        {
            throw new ArgumentException(
                "Durable equipment wear projection is invalid.");
        }
        PolicyKind = policyKind;
        ReplacementComponent = replacementComponent.Clone();
        ExhaustedAfter = exhaustedAfter;
        CurrentBefore = currentBefore;
        CurrentAfter = currentAfter;
    }

    public string PolicyKind { get; }
    public ItemInstanceComponentSaveData ReplacementComponent { get; }
    public bool ExhaustedAfter { get; }
    public double CurrentBefore { get; }
    public double CurrentAfter { get; }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IDurableFacilityEquipmentWearPolicy
{
    string PolicyKind { get; }

    DurableFacilityEquipmentWearProjection Project(
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        double wearAmount);
}

public interface IDurableFacilityEquipmentWearQuery
{
    bool TryProject(
        string policyKind,
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        double wearAmount,
        out DurableFacilityEquipmentWearProjection projection,
        out string failureReason);
}

public sealed class DurableFacilityEquipmentUseContext
{
    public DurableFacilityEquipmentUseContext(
        DurableFacilityEquipmentSlotSnapshot slot,
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject before,
        DurableFacilityEquipmentUseSubject after,
        double wearAmount)
    {
        if (slot == null
            || slot.LifecyclePhase !=
                DurableFacilityEquipmentSlotLifecyclePhase.Active
            || requirement == null
            || before == null
            || after == null
            || !string.Equals(before.StackId, after.StackId,
                StringComparison.Ordinal)
            || !before.ItemId.Equals(after.ItemId)
            || after.ContentRevision <= before.ContentRevision
            || double.IsNaN(wearAmount)
            || double.IsInfinity(wearAmount)
            || wearAmount <= 0d)
        {
            throw new ArgumentException(
                "Durable equipment use context is invalid.");
        }
        Slot = slot;
        Requirement = requirement;
        Before = before;
        After = after;
        WearAmount = wearAmount;
    }

    public DurableFacilityEquipmentSlotSnapshot Slot { get; }
    public DurableFacilityEquipmentRequirement Requirement { get; }
    public DurableFacilityEquipmentUseSubject Before { get; }
    public DurableFacilityEquipmentUseSubject After { get; }
    public double WearAmount { get; }
}

/// <summary>
/// Immutable, owner-facing projection of one committed durable-tool wear.
/// Quantity is retained explicitly so owner outcomes cannot confuse wear with
/// consumable-item removal.
/// </summary>
public sealed class DurableFacilityEquipmentUseEvidence
{
    public DurableFacilityEquipmentUseEvidence(
        DurableFacilityEquipmentUseContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        StackId = context.Before.StackId;
        ItemId = context.Before.ItemId.Value;
        FacilityId = context.Slot.OwnerFacilityId.Value;
        RequirementId = context.Requirement.RequirementId;
        AssignmentSequence = context.Slot.AssignmentSequence;
        RevisionBefore = context.Before.ContentRevision;
        RevisionAfter = context.After.ContentRevision;
        QuantityBefore = context.Before.Quantity;
        QuantityAfter = context.After.Quantity;
        DurabilityBefore = ReadDurability(context.Before, "current");
        DurabilityAfter = ReadDurability(context.After, "current");
        MaximumDurability = ReadDurability(context.Before, "maximum");
        RequestedWear = context.WearAmount;

        if (!Canonical(StackId)
            || !Canonical(ItemId)
            || !Canonical(FacilityId)
            || !Canonical(RequirementId)
            || !string.Equals(
                context.Requirement.ItemId.Value,
                ItemId,
                StringComparison.Ordinal)
            || AssignmentSequence <= 0L
            || RevisionBefore < 0L
            || RevisionAfter <= RevisionBefore
            || QuantityBefore <= 0
            || QuantityAfter != QuantityBefore
            || !double.IsFinite(DurabilityBefore)
            || !double.IsFinite(DurabilityAfter)
            || !double.IsFinite(MaximumDurability)
            || !double.IsFinite(RequestedWear)
            || DurabilityBefore <= 0d
            || MaximumDurability <= 0d
            || DurabilityBefore > MaximumDurability
            || DurabilityAfter < 0d
            || DurabilityAfter > DurabilityBefore
            || RequestedWear <= 0d
            || Math.Abs(DurabilityAfter
                - Math.Max(0d, DurabilityBefore - RequestedWear))
                > 0.0000001d)
        {
            throw new ArgumentException(
                "Durable equipment use evidence is invalid.",
                nameof(context));
        }
    }

    public string StackId { get; }
    public string ItemId { get; }
    public string FacilityId { get; }
    public string RequirementId { get; }
    public long AssignmentSequence { get; }
    public long RevisionBefore { get; }
    public long RevisionAfter { get; }
    public int QuantityBefore { get; }
    public int QuantityAfter { get; }
    public double DurabilityBefore { get; }
    public double DurabilityAfter { get; }
    public double MaximumDurability { get; }
    public double RequestedWear { get; }
    public double Spent => DurabilityBefore - DurabilityAfter;

    public string ToCanonicalString() => string.Concat(
        "semantics=wear;item=", ItemId,
        ";stack=", StackId,
        ";facility=", FacilityId,
        ";requirement=", RequirementId,
        ";slotSequence=", AssignmentSequence.ToString(CultureInfo.InvariantCulture),
        ";revision=", RevisionBefore.ToString(CultureInfo.InvariantCulture),
        "->", RevisionAfter.ToString(CultureInfo.InvariantCulture),
        ";quantity=", QuantityBefore.ToString(CultureInfo.InvariantCulture),
        "->", QuantityAfter.ToString(CultureInfo.InvariantCulture),
        ";durability=", Format(DurabilityBefore),
        "->", Format(DurabilityAfter),
        ";maximum=", Format(MaximumDurability),
        ";requestedWear=", Format(RequestedWear),
        ";spent=", Format(Spent));

    private static double ReadDurability(
        DurableFacilityEquipmentUseSubject subject,
        string key)
    {
        DurableFacilityEquipmentComponentSnapshot component = subject.Components
            .SingleOrDefault(value => string.Equals(
                value.ComponentTypeId,
                ItemInstanceComponentIds.Durability,
                StringComparison.Ordinal));
        DurableFacilityEquipmentComponentValueSnapshot[] values = component
            ?.Values.Where(value => string.Equals(
                value.Key,
                key,
                StringComparison.Ordinal)).ToArray()
            ?? Array.Empty<DurableFacilityEquipmentComponentValueSnapshot>();
        if (values.Length != 1
            || values[0].Kind != ItemStateValueKind.Decimal)
        {
            throw new ArgumentException(
                "Durable equipment use evidence is missing durability " + key + ".");
        }
        return values[0].DecimalValue;
    }

    private static string Format(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IDurableFacilityEquipmentEffectCommit
{
    string EffectKind { get; }

    bool TryPreflight(
        DurableFacilityEquipmentSlotSnapshot slot,
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        double wearAmount,
        out string failureReason);

    bool TryCommit(
        DurableFacilityEquipmentUseContext context,
        out string failureReason);
}

public enum DurableFacilityEquipmentUseStatus
{
    Applied = 0,
    AppliedDrainPending = 1,
    Unavailable = 2,
    Deferred = 3,
    Conflict = 4
}

public readonly struct DurableFacilityEquipmentUseResult
{
    public DurableFacilityEquipmentUseResult(
        DurableFacilityEquipmentUseStatus status,
        DurableFacilityEquipmentSlotSnapshot slot,
        string stackId,
        string failureReason,
        string postCommitFaultCode = "",
        DurableFacilityEquipmentUseContext committedContext = null)
    {
        bool applied = status is DurableFacilityEquipmentUseStatus.Applied
            or DurableFacilityEquipmentUseStatus.AppliedDrainPending;
        bool failed = status is DurableFacilityEquipmentUseStatus.Unavailable
            or DurableFacilityEquipmentUseStatus.Deferred
            or DurableFacilityEquipmentUseStatus.Conflict;
        string reason = failureReason ?? string.Empty;
        string postCommitFault = postCommitFaultCode ?? string.Empty;
        if (!Enum.IsDefined(typeof(DurableFacilityEquipmentUseStatus), status)
            || slot == null
            || (applied && (string.IsNullOrWhiteSpace(stackId)
                || reason.Length != 0))
            || (failed && ((stackId ?? string.Empty).Length != 0
                || !Canonical(reason)))
            || (postCommitFault.Length != 0 && (!applied || !Canonical(postCommitFault)))
            || (committedContext != null
                && (!applied
                    || !string.Equals(
                        committedContext.Before.StackId,
                        stackId,
                        StringComparison.Ordinal)
                    || !committedContext.Slot.Key.Equals(slot.Key)
                    || committedContext.Slot.AssignmentSequence
                        != slot.AssignmentSequence)))
        {
            throw new ArgumentException(
                "Durable equipment use result is incoherent.");
        }
        Status = status;
        Slot = slot;
        StackId = stackId ?? string.Empty;
        FailureReason = reason;
        PostCommitFaultCode = postCommitFault;
        CommittedContext = committedContext;
    }

    public DurableFacilityEquipmentUseStatus Status { get; }
    public DurableFacilityEquipmentSlotSnapshot Slot { get; }
    public string StackId { get; }
    public string FailureReason { get; }
    /// <summary>Wear/effect are committed; retry only finalization, never the gameplay effect.</summary>
    public string PostCommitFaultCode { get; }
    /// <summary>
    /// Exact before/after wear context for a committed use. Failed uses do not
    /// expose a context because their physical mutation was rolled back.
    /// </summary>
    public DurableFacilityEquipmentUseContext CommittedContext { get; }
    public bool Succeeded => Status is DurableFacilityEquipmentUseStatus.Applied
        or DurableFacilityEquipmentUseStatus.AppliedDrainPending;

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IDurableFacilityEquipmentUseCommand
{
    DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
        DurableFacilityEquipmentSlotKey key,
        string requirementId,
        double wearAmount,
        IDurableFacilityEquipmentEffectCommit effect);
}

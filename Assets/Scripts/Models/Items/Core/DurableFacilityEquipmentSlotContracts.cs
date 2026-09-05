using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public readonly struct DurableFacilityEquipmentSlotKey :
    IEquatable<DurableFacilityEquipmentSlotKey>
{
    public DurableFacilityEquipmentSlotKey(
        string logicalOwnerDomain,
        string ownerSubjectId)
    {
        if (!IsCanonicalDomain(logicalOwnerDomain)
            || !IsCanonicalRequired(ownerSubjectId))
        {
            throw new ArgumentException(
                "Durable facility-equipment slot key requires canonical IDs.");
        }
        LogicalOwnerDomain = logicalOwnerDomain;
        OwnerSubjectId = ownerSubjectId;
    }

    public string LogicalOwnerDomain { get; }
    public string OwnerSubjectId { get; }
    public bool IsValid => IsCanonicalDomain(LogicalOwnerDomain)
        && IsCanonicalRequired(OwnerSubjectId);

    public bool Equals(DurableFacilityEquipmentSlotKey other) =>
        string.Equals(LogicalOwnerDomain, other.LogicalOwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(OwnerSubjectId, other.OwnerSubjectId,
            StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is DurableFacilityEquipmentSlotKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(LogicalOwnerDomain ?? string.Empty),
        StringComparer.Ordinal.GetHashCode(OwnerSubjectId ?? string.Empty));

    public override string ToString() =>
        (LogicalOwnerDomain ?? string.Empty) + ":" +
        (OwnerSubjectId ?? string.Empty);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalDomain(string value) =>
        IsCanonicalRequired(value)
        && value.All(character => character is >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '.' or '_' or '-');
}

public sealed class DurableFacilityEquipmentRequirement
{
    public DurableFacilityEquipmentRequirement(
        string requirementId,
        ItemDefinitionId itemId,
        int requiredQuantity)
    {
        if (!IsCanonicalRequired(requirementId)
            || !itemId.IsValid
            || requiredQuantity <= 0)
        {
            throw new ArgumentException(
                "Durable facility-equipment requirement is invalid.");
        }
        RequirementId = requirementId;
        ItemId = itemId;
        RequiredQuantity = requiredQuantity;
    }

    public string RequirementId { get; }
    public ItemDefinitionId ItemId { get; }
    public int RequiredQuantity { get; }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class DurableFacilityEquipmentAssignment
{
    private readonly IReadOnlyList<DurableFacilityEquipmentRequirement>
        requirements;

    public DurableFacilityEquipmentAssignment(
        DurableFacilityEquipmentSlotKey key,
        string policyId,
        long policyRevision,
        string capacityPolicyKind,
        string usabilityPolicyKind,
        BuildingInstanceId ownerFacilityId,
        Vector2Int dropPosition,
        IEnumerable<DurableFacilityEquipmentRequirement> requirements)
    {
        DurableFacilityEquipmentRequirement[] copied = (requirements
                ?? throw new ArgumentNullException(nameof(requirements)))
            .OrderBy(value => value?.RequirementId, StringComparer.Ordinal)
            .ToArray();
        if (!key.IsValid
            || !IsCanonicalRequired(policyId)
            || policyRevision <= 0L
            || !IsCanonicalRequired(capacityPolicyKind)
            || !IsCanonicalRequired(usabilityPolicyKind)
            || !ownerFacilityId.IsValid
            || copied.Length == 0
            || copied.Any(value => value == null)
            || copied.Select(value => value.RequirementId)
                .Distinct(StringComparer.Ordinal).Count() != copied.Length)
        {
            throw new ArgumentException(
                "Durable facility-equipment assignment is invalid.");
        }

        Key = key;
        PolicyId = policyId;
        PolicyRevision = policyRevision;
        CapacityPolicyKind = capacityPolicyKind;
        UsabilityPolicyKind = usabilityPolicyKind;
        OwnerFacilityId = ownerFacilityId;
        DropPosition = dropPosition;
        this.requirements = Array.AsReadOnly(copied);
    }

    public DurableFacilityEquipmentSlotKey Key { get; }
    public string PolicyId { get; }
    public long PolicyRevision { get; }
    public string CapacityPolicyKind { get; }
    public string UsabilityPolicyKind { get; }
    public BuildingInstanceId OwnerFacilityId { get; }
    public Vector2Int DropPosition { get; }
    public IReadOnlyList<DurableFacilityEquipmentRequirement> Requirements =>
        requirements;

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class DurableFacilityEquipmentRequirementStatus
{
    public DurableFacilityEquipmentRequirementStatus(
        DurableFacilityEquipmentRequirement requirement,
        int pendingQuantity,
        int bufferedUsableQuantity)
    {
        if (requirement == null
            || pendingQuantity < 0
            || bufferedUsableQuantity < 0)
        {
            throw new ArgumentException(
                "Durable facility-equipment requirement status is invalid.");
        }
        Requirement = requirement;
        PendingQuantity = pendingQuantity;
        BufferedUsableQuantity = bufferedUsableQuantity;
    }

    public DurableFacilityEquipmentRequirement Requirement { get; }
    public int PendingQuantity { get; }
    public int BufferedUsableQuantity { get; }
    public bool IsReady => BufferedUsableQuantity
        >= Requirement.RequiredQuantity;
}

public sealed class DurableFacilityEquipmentSlotSnapshot
{
    private readonly IReadOnlyList<DurableFacilityEquipmentRequirementStatus>
        requirements;

    public DurableFacilityEquipmentSlotSnapshot(
        DurableFacilityEquipmentAssignment assignment,
        long assignmentSequence,
        string destinationId,
        string ownerOperationId,
        string assignmentFingerprint,
        DurableFacilityEquipmentCapacityProjection capacityProjection,
        IEnumerable<DurableFacilityEquipmentRequirementStatus> requirements,
        DurableFacilityEquipmentSlotLifecyclePhase lifecyclePhase =
            DurableFacilityEquipmentSlotLifecyclePhase.Active,
        string closeReasonCode = "",
        FacilityBufferDestinationCustodyDrainSnapshot drain = null,
        bool authoritiesRevoked = false)
    {
        DurableFacilityEquipmentRequirementStatus[] copied = (requirements
                ?? throw new ArgumentNullException(nameof(requirements)))
            .OrderBy(value => value?.Requirement.RequirementId,
                StringComparer.Ordinal)
            .ToArray();
        if (assignment == null
            || assignmentSequence <= 0L
            || !string.Equals(
                destinationId,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    assignment.Key,
                    assignmentSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                ownerOperationId,
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    assignment.Key,
                    assignmentSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                assignmentFingerprint,
                DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
                StringComparison.Ordinal)
            || capacityProjection == null
            || !string.Equals(
                capacityProjection.PolicyKind,
                assignment.CapacityPolicyKind,
                StringComparison.Ordinal)
            || copied.Length == 0
            || copied.Any(value => value == null)
            || !RequirementsMatch(assignment.Requirements, copied)
            || !LifecycleValid(
                lifecyclePhase,
                closeReasonCode,
                drain,
                authoritiesRevoked))
        {
            throw new ArgumentException(
                "Durable facility-equipment slot snapshot is invalid.");
        }
        Assignment = assignment;
        AssignmentSequence = assignmentSequence;
        DestinationId = destinationId;
        OwnerOperationId = ownerOperationId;
        AssignmentFingerprint = assignmentFingerprint;
        CapacityProjection = capacityProjection;
        this.requirements = Array.AsReadOnly(copied);
        LifecyclePhase = lifecyclePhase;
        CloseReasonCode = closeReasonCode ?? string.Empty;
        Drain = drain;
        AuthoritiesRevoked = authoritiesRevoked;
    }

    public DurableFacilityEquipmentAssignment Assignment { get; }
    public DurableFacilityEquipmentSlotKey Key => Assignment.Key;
    public long AssignmentSequence { get; }
    public string PolicyId => Assignment.PolicyId;
    public long PolicyRevision => Assignment.PolicyRevision;
    public string CapacityPolicyKind => Assignment.CapacityPolicyKind;
    public string UsabilityPolicyKind => Assignment.UsabilityPolicyKind;
    public BuildingInstanceId OwnerFacilityId => Assignment.OwnerFacilityId;
    public Vector2Int DropPosition => Assignment.DropPosition;
    public string DestinationId { get; }
    public string OwnerOperationId { get; }
    public string AssignmentFingerprint { get; }
    public DurableFacilityEquipmentCapacityProjection CapacityProjection { get; }
    public string SourceAuthorityFingerprint =>
        CapacityProjection.SourceAuthorityFingerprint;
    public long SourceAuthorityRevision =>
        CapacityProjection.SourceAuthorityRevision;
    public PhysicalMassGrams Capacity => CapacityProjection.MaximumMass;
    public IReadOnlyList<DurableFacilityEquipmentRequirementStatus>
        Requirements => requirements;
    public DurableFacilityEquipmentSlotLifecyclePhase LifecyclePhase { get; }
    public string CloseReasonCode { get; }
    public FacilityBufferDestinationCustodyDrainSnapshot Drain { get; }
    public bool AuthoritiesRevoked { get; }
    public bool SupplyReady => LifecyclePhase ==
        DurableFacilityEquipmentSlotLifecyclePhase.Active
        && requirements.All(value => value.IsReady);

    private static bool RequirementsMatch(
        IReadOnlyList<DurableFacilityEquipmentRequirement> expected,
        IReadOnlyList<DurableFacilityEquipmentRequirementStatus> actual)
    {
        if (expected.Count != actual.Count
            || actual.Select(value => value.Requirement.RequirementId)
                .Distinct(StringComparer.Ordinal).Count() != actual.Count)
        {
            return false;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            DurableFacilityEquipmentRequirement left = expected[index];
            DurableFacilityEquipmentRequirement right =
                actual[index].Requirement;
            if (!string.Equals(left.RequirementId, right.RequirementId,
                    StringComparison.Ordinal)
                || !left.ItemId.Equals(right.ItemId)
                || left.RequiredQuantity != right.RequiredQuantity)
            {
                return false;
            }
        }
        return true;
    }

    private static bool LifecycleValid(
        DurableFacilityEquipmentSlotLifecyclePhase phase,
        string reason,
        FacilityBufferDestinationCustodyDrainSnapshot drain,
        bool authoritiesRevoked)
    {
        if (!Enum.IsDefined(
                typeof(DurableFacilityEquipmentSlotLifecyclePhase),
                phase))
        {
            return false;
        }
        string value = reason ?? string.Empty;
        if (phase == DurableFacilityEquipmentSlotLifecyclePhase.Active)
            return value.Length == 0 && drain == null && !authoritiesRevoked;
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }
        if (phase == DurableFacilityEquipmentSlotLifecyclePhase.CloseRequested)
            return drain == null && !authoritiesRevoked;
        if (drain == null)
            return false;
        return phase == DurableFacilityEquipmentSlotLifecyclePhase.Draining
            ? !authoritiesRevoked || drain.EffectCommitted
            : authoritiesRevoked && drain.OwnerAcknowledged;
    }
}

public enum DurableFacilityEquipmentSlotLifecyclePhase
{
    Active = 0,
    CloseRequested = 1,
    Draining = 2,
    ClosedAwaitingCheckpointGc = 3
}

public enum DurableFacilityEquipmentSlotStatus
{
    None = 0,
    Applied = 1,
    Replay = 2,
    Deferred = 3,
    Conflict = 4
}

public readonly struct DurableFacilityEquipmentSlotResult
{
    public DurableFacilityEquipmentSlotResult(
        DurableFacilityEquipmentSlotStatus status,
        DurableFacilityEquipmentSlotSnapshot snapshot,
        string failureReason)
    {
        string reason = failureReason ?? string.Empty;
        bool success = status is DurableFacilityEquipmentSlotStatus.Applied
            or DurableFacilityEquipmentSlotStatus.Replay;
        bool failure = status is DurableFacilityEquipmentSlotStatus.Deferred
            or DurableFacilityEquipmentSlotStatus.Conflict;
        if ((success && (snapshot == null || reason.Length != 0))
            || (failure && reason.Length == 0)
            || (!success && !failure))
        {
            throw new ArgumentException(
                "Durable facility-equipment slot result is incoherent.");
        }
        Status = status;
        Snapshot = snapshot;
        FailureReason = reason;
    }

    public DurableFacilityEquipmentSlotStatus Status { get; }
    public DurableFacilityEquipmentSlotSnapshot Snapshot { get; }
    public string FailureReason { get; }
    public bool Succeeded => Snapshot != null && Status is
        DurableFacilityEquipmentSlotStatus.Applied
        or DurableFacilityEquipmentSlotStatus.Replay;
}

public sealed class DurableFacilityEquipmentCapacityProjection
{
    public DurableFacilityEquipmentCapacityProjection(
        string policyKind,
        PhysicalMassGrams maximumMass,
        long sourceAuthorityRevision,
        string sourceAuthorityFingerprint)
    {
        if (!IsCanonicalRequired(policyKind)
            || maximumMass.Value <= 0L
            || sourceAuthorityRevision <= 0L
            || !DurableFacilityEquipmentFingerprint.IsFingerprint(
                sourceAuthorityFingerprint))
        {
            throw new ArgumentException(
                "Durable facility-equipment capacity projection is invalid.");
        }
        PolicyKind = policyKind;
        MaximumMass = maximumMass;
        SourceAuthorityRevision = sourceAuthorityRevision;
        SourceAuthorityFingerprint = sourceAuthorityFingerprint;
    }

    public string PolicyKind { get; }
    public PhysicalMassGrams MaximumMass { get; }
    public long SourceAuthorityRevision { get; }
    public string SourceAuthorityFingerprint { get; }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IDurableFacilityEquipmentCapacityProjector
{
    string PolicyKind { get; }

    bool TryProjectMaximumMass(
        DurableFacilityEquipmentAssignment assignment,
        out DurableFacilityEquipmentCapacityProjection projection,
        out string failureReason);
}

public interface IDurableFacilityEquipmentCapacityProjectionQuery
{
    bool TryProjectMaximumMass(
        DurableFacilityEquipmentAssignment assignment,
        out DurableFacilityEquipmentCapacityProjection projection,
        out string failureReason);
}

public static class DurableFacilityEquipmentFingerprint
{
    public static string CreateAssignment(
        DurableFacilityEquipmentAssignment assignment)
    {
        if (assignment == null)
            throw new ArgumentNullException(nameof(assignment));
        StringBuilder canonical = new();
        Append(canonical, assignment.Key.LogicalOwnerDomain);
        Append(canonical, assignment.Key.OwnerSubjectId);
        Append(canonical, assignment.PolicyId);
        Append(canonical, assignment.PolicyRevision.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, assignment.CapacityPolicyKind);
        Append(canonical, assignment.UsabilityPolicyKind);
        Append(canonical, assignment.OwnerFacilityId.Value);
        Append(canonical, assignment.DropPosition.x.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, assignment.DropPosition.y.ToString(
            CultureInfo.InvariantCulture));
        foreach (DurableFacilityEquipmentRequirement requirement in
                 assignment.Requirements)
        {
            Append(canonical, requirement.RequirementId);
            Append(canonical, requirement.ItemId.Value);
            Append(canonical, requirement.RequiredQuantity.ToString(
                CultureInfo.InvariantCulture));
        }
        return Hash(canonical.ToString());
    }

    public static string CreateProjectionSource(
        string assignmentFingerprint,
        string policyKind,
        long sourceAuthorityRevision,
        PhysicalMassGrams maximumMass,
        IEnumerable<string> evidenceTokens)
    {
        string[] tokens = (evidenceTokens
                ?? throw new ArgumentNullException(nameof(evidenceTokens)))
            .ToArray();
        if (!IsFingerprint(assignmentFingerprint)
            || string.IsNullOrWhiteSpace(policyKind)
            || !string.Equals(policyKind, policyKind.Trim(),
                StringComparison.Ordinal)
            || sourceAuthorityRevision <= 0L
            || maximumMass.Value <= 0L
            || tokens.Any(value => value == null))
        {
            throw new ArgumentException(
                "Durable facility-equipment projection fingerprint input is invalid.");
        }
        StringBuilder canonical = new();
        Append(canonical, assignmentFingerprint);
        Append(canonical, policyKind);
        Append(canonical, sourceAuthorityRevision.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, maximumMass.Value.ToString(
            CultureInfo.InvariantCulture));
        foreach (string token in tokens)
            Append(canonical, token);
        return Hash(canonical.ToString());
    }

    public static bool IsFingerprint(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append(';');
    }

    private static string Hash(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        StringBuilder text = new(digest.Length * 2);
        foreach (byte item in digest)
            text.Append(item.ToString("x2", CultureInfo.InvariantCulture));
        return text.ToString();
    }
}

public interface IDurableFacilityEquipmentSlotCommand
{
    DurableFacilityEquipmentSlotResult TryReconcile(
        DurableFacilityEquipmentAssignment desired);

    DurableFacilityEquipmentSlotResult TryClose(
        DurableFacilityEquipmentSlotKey key,
        string reasonCode);

    DurableFacilityEquipmentSlotResult TryEnsureSupply(
        DurableFacilityEquipmentSlotKey key);

    IReadOnlyList<DurableFacilityEquipmentSlotResult> TryAdvancePending();
}

public interface IDurableFacilityEquipmentSlotQuery
{
    bool TryCapture(
        DurableFacilityEquipmentSlotKey key,
        out DurableFacilityEquipmentSlotSnapshot snapshot);

    IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll();
}

public static class DurableFacilityEquipmentSlotIdentity
{
    public const string AuthorityOwnerDomain = "durable-facility-equipment";
    public const string DefinitionMassPolicyKind = "definition-mass";
    public const string DestinationPrefix =
        "facility-input:exact:durable-equipment:";
    public const string OwnerStableIdPrefix =
        "durable-equipment-slot-owner:";
    public const string DrainParentOperationPrefix =
        "durable-equipment-slot-drain:";

    public static string BuildDestinationId(
        DurableFacilityEquipmentSlotKey key,
        long sequence) => Build(
            DestinationPrefix, key, sequence);

    public static string BuildOwnerOperationId(
        DurableFacilityEquipmentSlotKey key,
        long sequence) => Build(
            "durable-equipment-slot:", key, sequence);

    public static string BuildDrainParentOperationId(
        DurableFacilityEquipmentSlotKey key,
        long sequence) => Build(
            DrainParentOperationPrefix, key, sequence);

    public static string BuildDrainStepOperationId(
        DurableFacilityEquipmentSlotKey key,
        long sequence) => BuildDrainParentOperationId(key, sequence)
            + ":custody";

    public static string BuildOwnerStableId(
        DurableFacilityEquipmentSlotKey key,
        long sequence) => Build(
            OwnerStableIdPrefix, key, sequence);

    private static string Build(
        string prefix,
        DurableFacilityEquipmentSlotKey key,
        long sequence)
    {
        if (!key.IsValid || sequence <= 0L)
            throw new ArgumentException(
                "Durable facility-equipment identity input is invalid.");
        return prefix + key.LogicalOwnerDomain + ":"
            + key.OwnerSubjectId + ":"
            + sequence.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
    }
}

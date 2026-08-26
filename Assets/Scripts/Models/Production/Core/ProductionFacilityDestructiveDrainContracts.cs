using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public readonly struct ProductionFacilityDestructiveDrainOperationId :
    IEquatable<ProductionFacilityDestructiveDrainOperationId>
{
    public const string Prefix = "production-facility-destructive-drain:";
    private readonly string value;

    private ProductionFacilityDestructiveDrainOperationId(string value) =>
        this.value = value;

    public string Value => value ?? string.Empty;
    public bool IsValid => TryParse(Value, out _);

    public static ProductionFacilityDestructiveDrainOperationId FromFacility(
        BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A destructive drain operation requires a valid facility ID.",
                nameof(facilityId));
        return new ProductionFacilityDestructiveDrainOperationId(
            Prefix + facilityId.Value);
    }

    public static bool TryParse(
        string candidate,
        out ProductionFacilityDestructiveDrainOperationId operationId)
    {
        operationId = default;
        if (string.IsNullOrEmpty(candidate)
            || !string.Equals(
                candidate,
                candidate.Trim(),
                StringComparison.Ordinal)
            || !candidate.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string facility = candidate.Substring(Prefix.Length);
        if (!((BuildingInstanceId)facility).IsValid)
            return false;
        operationId = new ProductionFacilityDestructiveDrainOperationId(
            candidate);
        return true;
    }

    public bool Equals(ProductionFacilityDestructiveDrainOperationId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is ProductionFacilityDestructiveDrainOperationId other
        && Equals(other);
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value;
}

public enum ProductionFacilityDestructiveDrainCause
{
    None = 0,
    StructuralIntegrity = 1,
    CombatCover = 2,
    ExplicitDemolition = 3
}

public enum ProductionFacilityDestructiveDrainPhase
{
    None = 0,
    Prepared = 1,
    DrainingParticipants = 2,
    AwaitingEmptyVerification = 3,
    AwaitingAuthorityRevoke = 4,
    AwaitingWorldRemoval = 5,
    WorldRemovedAwaitingCheckpointGc = 6
}

public enum ProductionFacilityDestructiveDrainStepPhase
{
    Planned = 0,
    EffectCommittedAwaitingOwnerAck = 1,
    OwnerAcknowledged = 2
}

public enum ProductionFacilityDestructiveDrainDisposition
{
    Terminalize = 0,
    Transfer = 1
}

public static class ProductionFacilityDestructiveDrainParticipantIds
{
    public const string GenericProductionBills =
        "generic-production-bills";
    public const string CombatEquipmentCrafting =
        "combat-equipment-crafting";
    public const string ApparelWorkOrders =
        "apparel-work-orders";
    public const string CapacityRoutingOutbox =
        "capacity-routing-outbox";
    public const string PhysicalCustodyCarryRecovery =
        "physical-custody-carry-recovery";
}

public static class ProductionFacilityDestructiveDrainOwnerStableIds
{
    public static string GenericBill(string billId) =>
        Build("bill", billId);

    public static string CombatCraftOrder(string orderId) =>
        Build("craft-order", orderId);

    public static string EquipmentRepairOrder(string orderId) =>
        Build("repair-order", orderId);

    public static string ApparelWorkOrder(string orderId) =>
        Build("apparel-order", orderId);

    public static string RoutingBatch(string batchCommitId) =>
        Build("routing-batch", batchCommitId);

    public static string PhysicalDestination(string destinationId) =>
        Build("physical-destination", destinationId);

    private static string Build(string kind, string sourceId)
    {
        if (!ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(kind)
            || !ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                sourceId))
        {
            throw new ArgumentException(
                "Destructive-drain owner source identity is invalid.");
        }
        return kind + ":" + sourceId;
    }
}

public enum ProductionFacilityDestructiveDrainStepStatus
{
    Applied = 0,
    Replay = 1,
    Deferred = 2,
    Conflict = 3
}

public enum ProductionFacilityDestructiveDrainRecoveryAction
{
    ResumeCommit = 0,
    ResumeAcknowledge = 1,
    AlreadyAcknowledged = 2,
    Conflict = 3
}

public readonly struct ProductionFacilityDestructiveDrainPrepareContext
{
    public ProductionFacilityDestructiveDrainPrepareContext(
        ProductionFacilityDestructiveDrainOperationId operationId,
        ProductionFacilityDestructiveDrainCause cause,
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId,
        string durableLifecycleFingerprint)
    {
        if (!operationId.IsValid
            || !facilityId.IsValid
            || !destinationId.IsValid
            || cause == ProductionFacilityDestructiveDrainCause.None
            || !Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainCause),
                cause)
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                durableLifecycleFingerprint))
        {
            throw new ArgumentException(
                "Destructive-drain prepare context is invalid.");
        }
        OperationId = operationId;
        Cause = cause;
        FacilityId = facilityId;
        DestinationId = destinationId;
        DurableLifecycleFingerprint = durableLifecycleFingerprint;
    }

    public ProductionFacilityDestructiveDrainOperationId OperationId { get; }
    public ProductionFacilityDestructiveDrainCause Cause { get; }
    public BuildingInstanceId FacilityId { get; }
    public ProductionOutputDestinationId DestinationId { get; }
    public string DurableLifecycleFingerprint { get; }
}

public readonly struct ProductionFacilityDestructiveDrainOwnerPlan
{
    public ProductionFacilityDestructiveDrainOwnerPlan(
        string ownerStableId,
        ProductionFacilityDestructiveDrainDisposition disposition,
        string targetDestinationId,
        string requestFingerprint)
    {
        bool transfer = disposition ==
            ProductionFacilityDestructiveDrainDisposition.Transfer;
        if (!ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                ownerStableId)
            || !Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainDisposition),
                disposition)
            || transfer !=
                ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                    targetDestinationId)
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                requestFingerprint))
        {
            throw new ArgumentException(
                "Destructive-drain owner plan is invalid.");
        }
        OwnerStableId = ownerStableId;
        Disposition = disposition;
        TargetDestinationId = targetDestinationId ?? string.Empty;
        RequestFingerprint = requestFingerprint;
    }

    public string OwnerStableId { get; }
    public ProductionFacilityDestructiveDrainDisposition Disposition { get; }
    public string TargetDestinationId { get; }
    public string RequestFingerprint { get; }
}

public sealed class ProductionFacilityDestructiveDrainParticipantPlan
{
    public ProductionFacilityDestructiveDrainParticipantPlan(
        string participantId,
        int contractVersion,
        string durableContributionFingerprint,
        string planFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerPlan> owners)
    {
        if (!ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                participantId)
            || contractVersion <= 0
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                durableContributionFingerprint)
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                planFingerprint))
        {
            throw new ArgumentException(
                "Destructive-drain participant plan header is invalid.");
        }
        ParticipantId = participantId;
        ContractVersion = contractVersion;
        DurableContributionFingerprint = durableContributionFingerprint;
        PlanFingerprint = planFingerprint;
        Owners = Array.AsReadOnly((owners
                ?? Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>())
            .OrderBy(value => value.OwnerStableId, StringComparer.Ordinal)
            .ToArray());
        for (int index = 1; index < Owners.Count; index++)
        {
            if (string.Equals(
                    Owners[index - 1].OwnerStableId,
                    Owners[index].OwnerStableId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Destructive-drain participant plan has duplicate owners.");
            }
        }
    }

    public string ParticipantId { get; }
    public int ContractVersion { get; }
    public string DurableContributionFingerprint { get; }
    public string PlanFingerprint { get; }
    public IReadOnlyList<ProductionFacilityDestructiveDrainOwnerPlan> Owners
    { get; }
}

public readonly struct ProductionFacilityDestructiveDrainStepContext
{
    public ProductionFacilityDestructiveDrainStepContext(
        ProductionFacilityDestructiveDrainOperationId operationId,
        BuildingInstanceId facilityId,
        string participantId,
        ProductionFacilityDestructiveDrainOwnerSaveData owner,
        string expectedDurableContributionFingerprint)
    {
        if (!operationId.IsValid
            || !facilityId.IsValid
            || !ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                participantId)
            || owner == null
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                expectedDurableContributionFingerprint))
        {
            throw new ArgumentException(
                "Destructive-drain step context is invalid.");
        }
        OperationId = operationId;
        FacilityId = facilityId;
        ParticipantId = participantId;
        Owner = owner.Clone();
        ExpectedDurableContributionFingerprint =
            expectedDurableContributionFingerprint;
    }

    public ProductionFacilityDestructiveDrainOperationId OperationId { get; }
    public BuildingInstanceId FacilityId { get; }
    public string ParticipantId { get; }
    public ProductionFacilityDestructiveDrainOwnerSaveData Owner { get; }
    public string ExpectedDurableContributionFingerprint { get; }
}

public readonly struct ProductionFacilityDestructiveDrainStepResult
{
    public ProductionFacilityDestructiveDrainStepResult(
        ProductionFacilityDestructiveDrainStepStatus status,
        string commitId,
        string receiptFingerprint,
        string currentDurableContributionFingerprint)
    {
        if (!Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainStepStatus),
                status)
            || (status == ProductionFacilityDestructiveDrainStepStatus.Applied
                || status == ProductionFacilityDestructiveDrainStepStatus.Replay)
                && (!ProductionFacilityDestructiveDrainCanonical
                        .IsCanonicalToken(commitId)
                    || !ProductionFacilityDestructiveDrainCanonical
                        .IsFingerprint(receiptFingerprint))
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                currentDurableContributionFingerprint))
        {
            throw new ArgumentException(
                "Destructive-drain step result is invalid.");
        }
        Status = status;
        CommitId = commitId ?? string.Empty;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
        CurrentDurableContributionFingerprint =
            currentDurableContributionFingerprint;
    }

    public ProductionFacilityDestructiveDrainStepStatus Status { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }
    public string CurrentDurableContributionFingerprint { get; }
}

public readonly struct ProductionFacilityDestructiveDrainRecoveryResult
{
    public ProductionFacilityDestructiveDrainRecoveryResult(
        ProductionFacilityDestructiveDrainRecoveryAction action,
        ProductionFacilityDestructiveDrainStepResult step)
    {
        if (!Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainRecoveryAction),
                action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }
        Action = action;
        Step = step;
    }

    public ProductionFacilityDestructiveDrainRecoveryAction Action { get; }
    public ProductionFacilityDestructiveDrainStepResult Step { get; }
}

public interface IProductionFacilityDestructiveDrainParticipant
{
    string ParticipantId { get; }
    int ContractVersion { get; }
    IReadOnlyList<string> DependsOnParticipantIds { get; }
    ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context);
    ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context);
    ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
        ProductionFacilityDestructiveDrainStepContext context);
    ProductionFacilityDestructiveDrainRecoveryResult Recover(
        ProductionFacilityDestructiveDrainStepContext context);
}

/// <summary>
/// Optional producer-side prepare stage executed only after the journal entry
/// is durable and before any participant effect may begin. Participants whose
/// later effect depends on an immutable source vector use this boundary to
/// persist that vector without creating a producer-only orphan before the
/// journal exists.
/// </summary>
public interface IProductionFacilityDestructiveDrainDurablePrepareParticipant
{
    bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason);
}

public interface IProductionFacilityDestructiveDrainParticipantRegistry
{
    string RegistryFingerprint { get; }
    IReadOnlyList<IProductionFacilityDestructiveDrainParticipant>
        ExecutionOrder { get; }
    bool TryGet(
        string participantId,
        out IProductionFacilityDestructiveDrainParticipant participant);
}

[Serializable]
public sealed class ProductionFacilityDestructiveDrainOwnerSaveData
{
    public string ownerStableId = string.Empty;
    public ProductionFacilityDestructiveDrainDisposition disposition;
    public string targetDestinationId = string.Empty;
    public string stepOperationId = string.Empty;
    public ProductionFacilityDestructiveDrainStepPhase phase;
    public string requestFingerprint = string.Empty;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionFacilityDestructiveDrainOwnerSaveData Clone() => new()
    {
        ownerStableId = ownerStableId,
        disposition = disposition,
        targetDestinationId = targetDestinationId,
        stepOperationId = stepOperationId,
        phase = phase,
        requestFingerprint = requestFingerprint,
        commitId = commitId,
        receiptFingerprint = receiptFingerprint
    };
}

[Serializable]
public sealed class ProductionFacilityDestructiveDrainParticipantSaveData
{
    public string participantId = string.Empty;
    public int contractVersion;
    public string preparedContributionFingerprint = string.Empty;
    public string expectedCurrentContributionFingerprint = string.Empty;
    public string planFingerprint = string.Empty;
    public List<ProductionFacilityDestructiveDrainOwnerSaveData> owners = new();

    public ProductionFacilityDestructiveDrainParticipantSaveData Clone() => new()
    {
        participantId = participantId,
        contractVersion = contractVersion,
        preparedContributionFingerprint = preparedContributionFingerprint,
        expectedCurrentContributionFingerprint =
            expectedCurrentContributionFingerprint,
        planFingerprint = planFingerprint,
        owners = (owners
                ?? new List<ProductionFacilityDestructiveDrainOwnerSaveData>())
            .ConvertAll(value => value?.Clone())
    };
}

[Serializable]
public sealed class ProductionFacilityDestructiveDrainEntrySaveData
{
    public string operationId = string.Empty;
    public string initiatingMutationOperationId = string.Empty;
    public ProductionFacilityDestructiveDrainCause cause;
    public string facilityId = string.Empty;
    public string destinationId = string.Empty;
    public ProductionFacilityDestructiveDrainPhase phase;
    public string preparedLifecycleFingerprint = string.Empty;
    public string expectedCurrentLifecycleFingerprint = string.Empty;
    public long revision;
    public List<ProductionFacilityDestructiveDrainParticipantSaveData>
        participants = new();

    public ProductionFacilityDestructiveDrainEntrySaveData Clone() => new()
    {
        operationId = operationId,
        initiatingMutationOperationId = initiatingMutationOperationId,
        cause = cause,
        facilityId = facilityId,
        destinationId = destinationId,
        phase = phase,
        preparedLifecycleFingerprint = preparedLifecycleFingerprint,
        expectedCurrentLifecycleFingerprint = expectedCurrentLifecycleFingerprint,
        revision = revision,
        participants = (participants
                ?? new List<
                    ProductionFacilityDestructiveDrainParticipantSaveData>())
            .ConvertAll(value => value?.Clone())
    };
}

[Serializable]
public sealed class DungeonProductionFacilityDestructiveDrainSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string registryFingerprint = string.Empty;
    public List<ProductionFacilityDestructiveDrainEntrySaveData> entries = new();
}

public sealed class ProductionFacilityDestructiveDrainRestoreCandidate
{
    public ProductionFacilityDestructiveDrainRestoreCandidate(
        DungeonProductionFacilityDestructiveDrainSaveData payload)
    {
        Payload = payload
            ?? throw new ArgumentNullException(nameof(payload));
    }

    public DungeonProductionFacilityDestructiveDrainSaveData Payload { get; }
}

public interface IProductionFacilityDestructiveDrainPersistence
{
    DungeonProductionFacilityDestructiveDrainSaveData Capture();
    ProductionFacilityDestructiveDrainRestoreCandidate BuildRestore(
        DungeonProductionFacilityDestructiveDrainSaveData payload);
    void Restore(ProductionFacilityDestructiveDrainRestoreCandidate candidate);
}

public interface IProductionFacilityDestructiveDrainJournalQuery
{
    int Version { get; }
    IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData> CaptureOpen();
    bool TryGet(
        ProductionFacilityDestructiveDrainOperationId operationId,
        out ProductionFacilityDestructiveDrainEntrySaveData entry);
}

public readonly struct ProductionFacilityDestructiveDrainOpenOperationSnapshot
{
    public ProductionFacilityDestructiveDrainOpenOperationSnapshot(
        ProductionFacilityDestructiveDrainOperationId operationId,
        BuildingInstanceId facilityId,
        ProductionFacilityDestructiveDrainPhase phase,
        long revision)
    {
        if (!operationId.IsValid
            || !facilityId.IsValid
            || !Enum.IsDefined(typeof(ProductionFacilityDestructiveDrainPhase), phase)
            || phase == ProductionFacilityDestructiveDrainPhase.None
            || revision <= 0L)
        {
            throw new ArgumentException(
                "Destructive-drain open-operation snapshot is invalid.");
        }
        OperationId = operationId;
        FacilityId = facilityId;
        Phase = phase;
        Revision = revision;
    }

    public ProductionFacilityDestructiveDrainOperationId OperationId { get; }
    public BuildingInstanceId FacilityId { get; }
    public ProductionFacilityDestructiveDrainPhase Phase { get; }
    public long Revision { get; }
}

/// <summary>
/// Cycle-free, root-store-only gate for new production ownership. It never
/// resolves participants or mutates the destructive-drain journal.
/// </summary>
public interface IProductionFacilityDestructiveDrainOpenOperationQuery
{
    int Revision { get; }
    bool IsOpen(BuildingInstanceId facilityId);
    bool TryCapture(
        BuildingInstanceId facilityId,
        out ProductionFacilityDestructiveDrainOpenOperationSnapshot snapshot);
}

public interface IProductionFacilityDestructiveDrainJournalCommand
{
    bool TryRequest(
        ProductionFacilityDestructiveDrainCause cause,
        BuildingInstanceId facilityId,
        string initiatingMutationOperationId,
        string preparedLifecycleFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants,
        out ProductionFacilityDestructiveDrainEntrySaveData entry,
        out string failureReason);

    bool TryAdvance(
        ProductionFacilityDestructiveDrainOperationId operationId,
        long expectedRevision,
        ProductionFacilityDestructiveDrainPhase nextPhase,
        string expectedCurrentLifecycleFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants,
        out ProductionFacilityDestructiveDrainEntrySaveData entry,
        out string failureReason);

    bool TryRemoveCheckpointed(
        ProductionFacilityDestructiveDrainOperationId operationId,
        long expectedRevision,
        out string failureReason);
}

/// <summary>
/// Immutable Production-owned projection used only before a destructive-drain
/// journal is created. It intentionally exposes no save DTO or mutation
/// surface; the start gate uses it to prove that a prepared physical batch
/// cannot appear after the participant plan has been frozen.
/// </summary>
public readonly struct ProductionFacilityDestructiveDrainPreparedOutputOwner
{
    public ProductionFacilityDestructiveDrainPreparedOutputOwner(
        ProductionBillId billId,
        BuildingInstanceId facilityId,
        string recipeId,
        int cycleSequence,
        string destinationId,
        ProductionPreparedOutputPhase phase,
        string batchCommitId,
        string outcomeFingerprint)
    {
        if (!billId.IsValid
            || !facilityId.IsValid
            || !ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                recipeId)
            || cycleSequence <= 0
            || !ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                destinationId)
            || !Enum.IsDefined(typeof(ProductionPreparedOutputPhase), phase))
        {
            throw new ArgumentException(
                "A destructive-drain prepared-output owner is invalid.");
        }

        bool unresolved = phase == ProductionPreparedOutputPhase.Unresolved;
        if (unresolved != string.IsNullOrEmpty(batchCommitId)
            || unresolved != string.IsNullOrEmpty(outcomeFingerprint)
            || !unresolved
                && (!ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                        batchCommitId)
                    || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                        outcomeFingerprint)))
        {
            throw new ArgumentException(
                "A destructive-drain prepared-output identity is invalid.");
        }

        BillId = billId;
        FacilityId = facilityId;
        RecipeId = recipeId;
        CycleSequence = cycleSequence;
        DestinationId = destinationId;
        Phase = phase;
        BatchCommitId = batchCommitId ?? string.Empty;
        OutcomeFingerprint = outcomeFingerprint ?? string.Empty;
    }

    public ProductionBillId BillId { get; }
    public BuildingInstanceId FacilityId { get; }
    public string RecipeId { get; }
    public int CycleSequence { get; }
    public string DestinationId { get; }
    public ProductionPreparedOutputPhase Phase { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
}

public interface IProductionFacilityDestructiveDrainPreparedOutputQuery
{
    IReadOnlyList<ProductionFacilityDestructiveDrainPreparedOutputOwner>
        CapturePreparedOutputOwners(BuildingInstanceId facilityId);
}

public static class ProductionFacilityDestructiveDrainCanonical
{
    public static string BuildInitiatingMutationOperationId(
        ProductionFacilityDestructiveDrainCause cause,
        BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A valid facility ID is required.", nameof(facilityId));
        return cause switch
        {
            ProductionFacilityDestructiveDrainCause.StructuralIntegrity =>
                "production-mutation:structural-loss:" + facilityId.Value,
            ProductionFacilityDestructiveDrainCause.CombatCover =>
                "production-mutation:cover-loss:" + facilityId.Value,
            ProductionFacilityDestructiveDrainCause.ExplicitDemolition =>
                "production-mutation:demolition:" + facilityId.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(cause))
        };
    }

    public static string BuildStepOperationId(
        ProductionFacilityDestructiveDrainOperationId parent,
        string participantId,
        string ownerStableId)
    {
        if (!parent.IsValid
            || !IsCanonicalToken(participantId)
            || !IsCanonicalToken(ownerStableId))
        {
            throw new ArgumentException(
                "A destructive drain step requires canonical identities.");
        }
        return parent.Value + ":step:"
            + ComputeFingerprint(participantId + "\n" + ownerStableId);
    }

    public static string ComputeFingerprint(string canonical)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical ?? string.Empty));
        StringBuilder result = new(digest.Length * 2);
        for (int index = 0; index < digest.Length; index++)
        {
            result.Append(digest[index].ToString(
                "x2",
                CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }

    public static bool IsCanonicalToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    public static bool IsFingerprint(string value) =>
        value != null
        && value.Length == 64
        && string.Equals(
            value,
            value.ToLowerInvariant(),
            StringComparison.Ordinal)
        && IsLowerHex(value);

    private static bool IsLowerHex(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!((character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }
        return true;
    }
}

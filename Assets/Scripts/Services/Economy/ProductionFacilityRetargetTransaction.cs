using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionFacilityRetargetRequest
{
    public ProductionFacilityRetargetRequest(
        ProductionFacilityHandle sourceFacility,
        ProductionFacilityMutationKind mutationKind)
    {
        SourceFacility = sourceFacility
            ?? throw new ArgumentNullException(nameof(sourceFacility));
        if (!SourceFacility.InstanceId.IsValid
            || SourceFacility.IsDestroyed
            || !Enum.IsDefined(typeof(ProductionFacilityMutationKind), mutationKind))
        {
            throw new ArgumentException(
                "A retarget request requires a live, stable source facility.");
        }

        MutationKind = mutationKind;
    }

    public ProductionFacilityHandle SourceFacility { get; }
    public BuildingInstanceId SourceFacilityId => SourceFacility.InstanceId;
    public ProductionFacilityMutationKind MutationKind { get; }
}

public sealed class ProductionFacilityRetargetBinding
{
    public ProductionFacilityRetargetBinding(
        BuildingInstanceId sourceFacilityId,
        ProductionFacilityHandle targetFacility)
    {
        if (!sourceFacilityId.IsValid)
            throw new ArgumentException(
                "A retarget binding requires a stable source facility ID.",
                nameof(sourceFacilityId));
        TargetFacility = targetFacility
            ?? throw new ArgumentNullException(nameof(targetFacility));
        if (!TargetFacility.InstanceId.IsValid || TargetFacility.IsDestroyed)
            throw new ArgumentException(
                "A retarget binding requires a live, stable target facility.",
                nameof(targetFacility));

        SourceFacilityId = sourceFacilityId;
    }

    public BuildingInstanceId SourceFacilityId { get; }
    public ProductionFacilityHandle TargetFacility { get; }
    public BuildingInstanceId TargetFacilityId => TargetFacility.InstanceId;
}

public sealed class ProductionFacilityRetargetParticipantPlan
{
    private ProductionFacilityRetargetParticipantPlan(
        string participantId,
        string preparedFingerprint,
        object participantState)
    {
        if (!ProductionFacilityRetargetCanonical.IsToken(participantId)
            || !ProductionFacilityRetargetCanonical.IsFingerprint(
                preparedFingerprint))
        {
            throw new ArgumentException(
                "A retarget participant plan requires canonical identity and fingerprint.");
        }

        ParticipantId = participantId;
        PreparedFingerprint = preparedFingerprint;
        ParticipantState = participantState
            ?? throw new ArgumentNullException(nameof(participantState));
    }

    public string ParticipantId { get; }
    public string PreparedFingerprint { get; }
    public object ParticipantState { get; }

    public static ProductionFacilityRetargetParticipantPlan Create(
        string participantId,
        string preparedFingerprint,
        object participantState) => new(
        participantId,
        preparedFingerprint,
        participantState);
}

public interface IProductionFacilityRetargetParticipant
{
    string ParticipantId { get; }

    bool TryPrepare(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        string operationId,
        out ProductionFacilityRetargetParticipantPlan plan,
        out string failureReason);

    bool TryCommit(
        ProductionFacilityRetargetParticipantPlan plan,
        IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
        out string committedFingerprint,
        out string failureReason);

    bool TryRollback(
        ProductionFacilityRetargetParticipantPlan plan,
        out string rolledBackFingerprint,
        out string failureReason);

    bool TryCaptureCurrentFingerprint(
        ProductionFacilityRetargetParticipantPlan plan,
        out string currentFingerprint,
        out string failureReason);
}

public sealed class ProductionFacilityRetargetParticipantRegistry
{
    private readonly IReadOnlyList<IProductionFacilityRetargetParticipant>
        participants;

    public ProductionFacilityRetargetParticipantRegistry(
        IEnumerable<IProductionFacilityRetargetParticipant> participants)
    {
        IProductionFacilityRetargetParticipant[] ordered = (participants
                ?? throw new ArgumentNullException(nameof(participants)))
            .OrderBy(value => value?.ParticipantId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0)
            throw new InvalidOperationException(
                "Production facility retarget requires at least one real authority participant.");

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (IProductionFacilityRetargetParticipant participant in ordered)
        {
            if (participant == null
                || !ProductionFacilityRetargetCanonical.IsToken(
                    participant.ParticipantId)
                || !ids.Add(participant.ParticipantId))
            {
                throw new InvalidOperationException(
                    "Production facility retarget participant registry is invalid or duplicated.");
            }
        }

        this.participants = Array.AsReadOnly(ordered);
    }

    public IReadOnlyList<IProductionFacilityRetargetParticipant> Participants =>
        participants;
}

public enum ProductionFacilityRetargetTransactionPhase
{
    Prepared = 0,
    Committed = 1,
    RolledBack = 2,
    Completed = 3
}

public sealed class ProductionFacilityRetargetTransactionState
{
    internal ProductionFacilityRetargetTransactionState(
        string operationId,
        IReadOnlyList<ProductionFacilityRetargetRequest> requests,
        IReadOnlyList<ParticipantEntry> participants,
        IReadOnlyList<long> epochs)
    {
        OperationId = operationId;
        Requests = requests;
        Participants = participants;
        Epochs = epochs;
    }

    public string OperationId { get; }
    public IReadOnlyList<ProductionFacilityRetargetRequest> Requests { get; }
    public ProductionFacilityRetargetTransactionPhase Phase { get; internal set; }

    internal IReadOnlyList<ParticipantEntry> Participants { get; }
    internal IReadOnlyList<long> Epochs { get; }
    internal IReadOnlyList<ProductionFacilityRetargetBinding> Bindings
    {
        get;
        set;
    }

    internal sealed class ParticipantEntry
    {
        internal ParticipantEntry(
            IProductionFacilityRetargetParticipant participant,
            ProductionFacilityRetargetParticipantPlan plan)
        {
            Participant = participant;
            Plan = plan;
        }

        internal IProductionFacilityRetargetParticipant Participant { get; }
        internal ProductionFacilityRetargetParticipantPlan Plan { get; }
        internal bool CommitAttempted { get; set; }
        internal bool Committed { get; set; }
        internal string CommittedFingerprint { get; set; } = string.Empty;
    }
}

public interface IProductionFacilityRetargetTransaction
{
    bool TryBegin(
        IReadOnlyList<ProductionFacilityRetargetRequest> requests,
        string operationId,
        out ProductionFacilityRetargetTransactionState transaction,
        out string failureReason);

    bool TryCommit(
        ProductionFacilityRetargetTransactionState transaction,
        IReadOnlyList<ProductionFacilityRetargetBinding> bindings,
        out string failureReason);

    bool TryRollback(
        ProductionFacilityRetargetTransactionState transaction,
        out string failureReason);

    bool TryComplete(
        ProductionFacilityRetargetTransactionState transaction,
        out string failureReason);
}

/// <summary>
/// Coordinates exact production-owner retargeting around an already prepared
/// detached world replacement. Every authority participant prepares before the
/// first mutation, commits in stable order, and proves exact reverse rollback.
/// Epochs remain open until rollback or completion, so new productive custody
/// cannot enter any source facility during the handoff.
/// </summary>
public sealed class ProductionFacilityRetargetTransaction :
    IProductionFacilityRetargetTransaction
{
    private readonly ProductionFacilityRetargetParticipantRegistry registry;
    private readonly IProductionFacilityMutationEpochAuthority epochs;

    public ProductionFacilityRetargetTransaction(
        ProductionFacilityRetargetParticipantRegistry registry,
        IProductionFacilityMutationEpochAuthority epochs)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.epochs = epochs ?? throw new ArgumentNullException(nameof(epochs));
    }

    public bool TryBegin(
        IReadOnlyList<ProductionFacilityRetargetRequest> requests,
        string operationId,
        out ProductionFacilityRetargetTransactionState transaction,
        out string failureReason)
    {
        transaction = null;
        failureReason = string.Empty;
        if (!ProductionFacilityRetargetCanonical.IsToken(operationId)
            || requests == null
            || requests.Count == 0)
        {
            failureReason = "production-facility-retarget-request-invalid";
            return false;
        }

        ProductionFacilityRetargetRequest[] ordered = requests
            .OrderBy(value => value?.SourceFacilityId.Value,
                StringComparer.Ordinal)
            .ToArray();
        HashSet<BuildingInstanceId> sourceIds = new();
        foreach (ProductionFacilityRetargetRequest request in ordered)
        {
            if (request == null
                || request.SourceFacility == null
                || request.SourceFacility.IsDestroyed
                || !request.SourceFacilityId.IsValid
                || !sourceIds.Add(request.SourceFacilityId))
            {
                failureReason =
                    "production-facility-retarget-source-invalid-or-duplicate";
                return false;
            }
        }

        List<long> openedEpochs = new(ordered.Length);
        bool closingOrClosed = false;
        try
        {
            for (int index = 0; index < ordered.Length; index++)
            {
                if (!epochs.TryBegin(
                        ordered[index].SourceFacilityId,
                        operationId,
                        out long epoch,
                        out failureReason))
                {
                    closingOrClosed = true;
                    CloseOpenedEpochsOrThrow(
                        ordered,
                        openedEpochs,
                        operationId,
                        "begin-rollback");
                    return false;
                }
                openedEpochs.Add(epoch);
            }

            List<ProductionFacilityRetargetTransactionState.ParticipantEntry>
                prepared = new(registry.Participants.Count);
            foreach (IProductionFacilityRetargetParticipant participant in
                     registry.Participants)
            {
                if (!participant.TryPrepare(
                        ordered,
                        operationId,
                        out ProductionFacilityRetargetParticipantPlan plan,
                        out failureReason)
                    || plan == null
                    || !string.Equals(
                        plan.ParticipantId,
                        participant.ParticipantId,
                        StringComparison.Ordinal)
                    || !TryVerifyFingerprint(
                        participant,
                        plan,
                        plan.PreparedFingerprint,
                        out failureReason))
                {
                    closingOrClosed = true;
                    CloseOpenedEpochsOrThrow(
                        ordered,
                        openedEpochs,
                        operationId,
                        "prepare-rollback");
                    failureReason = "production-facility-retarget-prepare-failed:"
                        + participant.ParticipantId + ":" + failureReason;
                    return false;
                }
                prepared.Add(new(
                    participant,
                    plan));
            }

            transaction = new ProductionFacilityRetargetTransactionState(
                operationId,
                Array.AsReadOnly(ordered),
                Array.AsReadOnly(prepared.ToArray()),
                Array.AsReadOnly(openedEpochs.ToArray()));
            return true;
        }
        catch
        {
            if (openedEpochs.Count > 0 && !closingOrClosed)
            {
                closingOrClosed = true;
                CloseOpenedEpochsOrThrow(
                    ordered,
                    openedEpochs,
                    operationId,
                    "exception-rollback");
            }
            throw;
        }
    }

    public bool TryCommit(
        ProductionFacilityRetargetTransactionState transaction,
        IReadOnlyList<ProductionFacilityRetargetBinding> bindings,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequireOpen(transaction, out failureReason)
            || transaction.Phase != ProductionFacilityRetargetTransactionPhase
                .Prepared)
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-facility-retarget-transaction-not-prepared"
                : failureReason;
            return false;
        }
        if (!TryValidateBindings(
                transaction,
                bindings,
                out ProductionFacilityRetargetBinding[] orderedBindings,
                out failureReason))
        {
            return false;
        }

        foreach (ProductionFacilityRetargetTransactionState.ParticipantEntry entry
                 in transaction.Participants)
        {
            entry.CommitAttempted = true;
            bool committed;
            string fingerprint;
            bool verified = false;
            string verifyFailure = string.Empty;
            try
            {
                committed = entry.Participant.TryCommit(
                    entry.Plan,
                    orderedBindings,
                    out fingerprint,
                    out failureReason);
                if (committed
                    && ProductionFacilityRetargetCanonical.IsFingerprint(
                        fingerprint))
                {
                    verified = TryVerifyFingerprint(
                        entry.Participant,
                        entry.Plan,
                        fingerprint,
                        out verifyFailure);
                }
            }
            catch (Exception exception)
            {
                committed = false;
                fingerprint = string.Empty;
                failureReason = exception.GetType().Name + ":" + exception.Message;
            }

            if (!committed
                || !ProductionFacilityRetargetCanonical.IsFingerprint(fingerprint)
                || !verified)
            {
                string original = "production-facility-retarget-commit-failed:"
                    + entry.Participant.ParticipantId + ":"
                    + (string.IsNullOrEmpty(failureReason)
                        ? verifyFailure
                        : failureReason);
                RollbackParticipantsOrThrow(transaction, includeAttempted: true);
                failureReason = original;
                return false;
            }

            entry.Committed = true;
            entry.CommittedFingerprint = fingerprint;
        }

        transaction.Bindings = Array.AsReadOnly(orderedBindings);
        transaction.Phase = ProductionFacilityRetargetTransactionPhase.Committed;
        return true;
    }

    public bool TryRollback(
        ProductionFacilityRetargetTransactionState transaction,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequireOpen(transaction, out failureReason)
            || transaction.Phase is ProductionFacilityRetargetTransactionPhase
                .RolledBack or ProductionFacilityRetargetTransactionPhase.Completed)
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-facility-retarget-transaction-closed"
                : failureReason;
            return false;
        }

        RollbackParticipantsOrThrow(transaction, includeAttempted: true);
        CloseAllEpochsOrThrow(transaction, "rollback");
        transaction.Phase = ProductionFacilityRetargetTransactionPhase.RolledBack;
        return true;
    }

    public bool TryComplete(
        ProductionFacilityRetargetTransactionState transaction,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequireOpen(transaction, out failureReason)
            || transaction.Phase != ProductionFacilityRetargetTransactionPhase
                .Committed)
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-facility-retarget-transaction-not-committed"
                : failureReason;
            return false;
        }

        foreach (ProductionFacilityRetargetTransactionState.ParticipantEntry entry
                 in transaction.Participants)
        {
            if (!entry.Committed
                || !TryVerifyFingerprint(
                    entry.Participant,
                    entry.Plan,
                    entry.CommittedFingerprint,
                    out failureReason))
            {
                failureReason = "production-facility-retarget-completion-drift:"
                    + entry.Participant.ParticipantId + ":" + failureReason;
                return false;
            }
        }

        CloseAllEpochsOrThrow(transaction, "complete");
        transaction.Phase = ProductionFacilityRetargetTransactionPhase.Completed;
        return true;
    }

    private bool TryValidateBindings(
        ProductionFacilityRetargetTransactionState transaction,
        IReadOnlyList<ProductionFacilityRetargetBinding> bindings,
        out ProductionFacilityRetargetBinding[] ordered,
        out string failureReason)
    {
        ordered = Array.Empty<ProductionFacilityRetargetBinding>();
        failureReason = string.Empty;
        if (bindings == null || bindings.Count != transaction.Requests.Count)
        {
            failureReason = "production-facility-retarget-binding-coverage-invalid";
            return false;
        }

        Dictionary<BuildingInstanceId, ProductionFacilityRetargetRequest> requests =
            transaction.Requests.ToDictionary(value => value.SourceFacilityId);
        HashSet<BuildingInstanceId> boundSources = new();
        Dictionary<BuildingInstanceId, object> targetObjects = new();
        ordered = bindings
            .OrderBy(value => value?.SourceFacilityId.Value,
                StringComparer.Ordinal)
            .ToArray();
        foreach (ProductionFacilityRetargetBinding binding in ordered)
        {
            if (binding == null
                || !requests.TryGetValue(
                    binding.SourceFacilityId,
                    out ProductionFacilityRetargetRequest request)
                || !boundSources.Add(binding.SourceFacilityId)
                || binding.TargetFacility == null
                || binding.TargetFacility.IsDestroyed
                || !binding.TargetFacilityId.IsValid
                || !requests.ContainsKey(binding.TargetFacilityId)
                || ReferenceEquals(
                    request.SourceFacility.RuntimeObject,
                    binding.TargetFacility.RuntimeObject))
            {
                failureReason = "production-facility-retarget-binding-invalid";
                ordered = Array.Empty<ProductionFacilityRetargetBinding>();
                return false;
            }
            if (targetObjects.TryGetValue(
                    binding.TargetFacilityId,
                    out object existingTarget)
                && !ReferenceEquals(
                    existingTarget,
                    binding.TargetFacility.RuntimeObject))
            {
                failureReason =
                    "production-facility-retarget-target-split-brain";
                ordered = Array.Empty<ProductionFacilityRetargetBinding>();
                return false;
            }
            targetObjects[binding.TargetFacilityId] =
                binding.TargetFacility.RuntimeObject;
        }
        return true;
    }

    private bool TryRequireOpen(
        ProductionFacilityRetargetTransactionState transaction,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (transaction == null)
        {
            failureReason = "production-facility-retarget-transaction-null";
            return false;
        }
        for (int index = 0; index < transaction.Requests.Count; index++)
        {
            if (!epochs.IsCurrent(
                    transaction.Requests[index].SourceFacilityId,
                    transaction.OperationId,
                    transaction.Epochs[index]))
            {
                failureReason = "production-facility-retarget-epoch-stale:"
                    + transaction.Requests[index].SourceFacilityId.Value;
                return false;
            }
        }
        return true;
    }

    private static bool TryVerifyFingerprint(
        IProductionFacilityRetargetParticipant participant,
        ProductionFacilityRetargetParticipantPlan plan,
        string expected,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!participant.TryCaptureCurrentFingerprint(
                plan,
                out string current,
                out failureReason)
            || !ProductionFacilityRetargetCanonical.IsFingerprint(current)
            || !string.Equals(current, expected, StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-facility-retarget-fingerprint-mismatch"
                : failureReason;
            return false;
        }
        return true;
    }

    private static void RollbackParticipantsOrThrow(
        ProductionFacilityRetargetTransactionState transaction,
        bool includeAttempted)
    {
        for (int index = transaction.Participants.Count - 1; index >= 0; index--)
        {
            ProductionFacilityRetargetTransactionState.ParticipantEntry entry =
                transaction.Participants[index];
            if (!entry.Committed && !(includeAttempted && entry.CommitAttempted))
                continue;
            string verifyFailure = string.Empty;
            if (!entry.Participant.TryRollback(
                    entry.Plan,
                    out string fingerprint,
                    out string rollbackFailure)
                || !string.Equals(
                    fingerprint,
                    entry.Plan.PreparedFingerprint,
                    StringComparison.Ordinal)
                || !TryVerifyFingerprint(
                    entry.Participant,
                    entry.Plan,
                    entry.Plan.PreparedFingerprint,
                    out verifyFailure))
            {
                throw new InvalidOperationException(
                    "Production facility retarget rollback failed: "
                    + entry.Participant.ParticipantId + ":"
                    + (string.IsNullOrEmpty(rollbackFailure)
                        ? verifyFailure
                        : rollbackFailure));
            }
            entry.Committed = false;
            entry.CommitAttempted = false;
            entry.CommittedFingerprint = string.Empty;
        }
    }

    private void CloseAllEpochsOrThrow(
        ProductionFacilityRetargetTransactionState transaction,
        string phase)
    {
        for (int index = 0; index < transaction.Requests.Count; index++)
        {
            if (!epochs.IsCurrent(
                    transaction.Requests[index].SourceFacilityId,
                    transaction.OperationId,
                    transaction.Epochs[index]))
            {
                throw new InvalidOperationException(
                    "Production facility retarget epoch drifted before " + phase
                    + ":" + transaction.Requests[index].SourceFacilityId.Value);
            }
        }
        for (int index = transaction.Requests.Count - 1; index >= 0; index--)
        {
            if (!epochs.TryEnd(
                    transaction.Requests[index].SourceFacilityId,
                    transaction.OperationId,
                    transaction.Epochs[index],
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Production facility retarget epoch close failed during "
                    + phase + ":" + failureReason);
            }
        }
    }

    private void CloseOpenedEpochsOrThrow(
        IReadOnlyList<ProductionFacilityRetargetRequest> ordered,
        IReadOnlyList<long> openedEpochs,
        string operationId,
        string phase)
    {
        for (int index = openedEpochs.Count - 1; index >= 0; index--)
        {
            if (!epochs.TryEnd(
                    ordered[index].SourceFacilityId,
                    operationId,
                    openedEpochs[index],
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Production facility retarget partial epoch rollback failed during "
                    + phase + ":" + failureReason);
            }
        }
    }
}

internal static class ProductionFacilityRetargetCanonical
{
    internal static bool IsToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static bool IsFingerprint(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f'))
                return false;
        }
        return true;
    }
}

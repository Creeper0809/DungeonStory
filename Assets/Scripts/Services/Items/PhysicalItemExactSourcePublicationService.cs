using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class PhysicalItemExactSourcePublicationPlan
{
    private readonly IReadOnlyList<FacilityBufferPlannedOutputSlice> outputs;

    public PhysicalItemExactSourcePublicationPlan(
        string ownerDomain,
        string ownerOperationId,
        Vector2Int dropPosition,
        IReadOnlyList<FacilityBufferPlannedOutputSlice> outputs)
    {
        OwnerDomain = RequireCanonical(ownerDomain, nameof(ownerDomain));
        OwnerOperationId = RequireCanonical(
            ownerOperationId,
            nameof(ownerOperationId));
        DropPosition = dropPosition;
        FacilityBufferPlannedOutputSlice[] copied = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (copied.Length == 0
            || copied.Any(value => value.Subject == null
                || !value.ItemDefinitionId.IsValid
                || value.Quantity <= 0
                || !IsCanonical(value.OutputLineId))
            || copied.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != copied.Length)
        {
            throw new ArgumentException(
                "Exact source outputs must be non-empty, canonical, positive, and uniquely identified.",
                nameof(outputs));
        }

        this.outputs = Array.AsReadOnly(copied);
        DestinationId = ReservedTargetDestinationIdentity.PhysicalSourceBufferPrefix
            + OwnerDomain + ":" + OwnerOperationId;
        AuthorityOwnerId = "physical-source-owner:"
            + OwnerDomain + ":" + OwnerOperationId;
        PublicationOperationId = "physical-source-publication:"
            + OwnerDomain + ":" + OwnerOperationId;
        BatchCommitId = "physical-source-batch:"
            + OwnerDomain + ":" + OwnerOperationId;
        OutcomeFingerprint = CreateOutcomeFingerprint(copied);
    }

    public string OwnerDomain { get; }
    public string OwnerOperationId { get; }
    public Vector2Int DropPosition { get; }
    public string DestinationId { get; }
    public string AuthorityOwnerId { get; }
    public string PublicationOperationId { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public IReadOnlyList<FacilityBufferPlannedOutputSlice> Outputs => outputs;

    private static string RequireCanonical(string value, string parameter)
    {
        if (!IsCanonical(value))
            throw new ArgumentException("A canonical non-empty token is required.", parameter);
        return value;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string CreateOutcomeFingerprint(
        IReadOnlyList<FacilityBufferPlannedOutputSlice> values)
    {
        StringBuilder canonical = new();
        foreach (FacilityBufferPlannedOutputSlice value in values)
        {
            Append(canonical, value.OutputLineId);
            Append(canonical, value.ItemDefinitionId.Value);
            Append(canonical, value.Quantity.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            Append(canonical, value.Subject.ItemInstanceId);
            Append(canonical, value.PreparedComponentFingerprint);
            Append(canonical, value.UniqueBindingCapabilityId);
        }
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(
                sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length)
            .Append(':')
            .Append(safe)
            .Append('|');
    }
}

public readonly struct PhysicalItemExactSourcePublicationTransaction
{
    private readonly IReadOnlyList<FacilityBufferPublishedOutputStackReceipt>
        preparedStacks;

    internal PhysicalItemExactSourcePublicationTransaction(
        string batchCommitId,
        string destinationId,
        IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> preparedStacks)
    {
        BatchCommitId = batchCommitId;
        DestinationId = destinationId;
        FacilityBufferPublishedOutputStackReceipt[] copied = (preparedStacks
                ?? throw new ArgumentNullException(nameof(preparedStacks)))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        this.preparedStacks = Array.AsReadOnly(copied);
    }

    public string BatchCommitId { get; }
    public string DestinationId { get; }
    public IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> PreparedStacks =>
        preparedStacks ?? Array.Empty<FacilityBufferPublishedOutputStackReceipt>();
    public bool IsPrepared => !string.IsNullOrEmpty(BatchCommitId)
        && !string.IsNullOrEmpty(DestinationId)
        && PreparedStacks.Count > 0;

#if UNITY_EDITOR
    public static PhysicalItemExactSourcePublicationTransaction CreateEditorFixture(
        string batchCommitId,
        string destinationId,
        string stackId,
        string outputLineId,
        ItemDefinitionId itemDefinitionId,
        int quantity,
        PhysicalMassGrams mass,
        string itemInstanceId) => new(
        batchCommitId,
        destinationId,
        new[]
        {
            new FacilityBufferPublishedOutputStackReceipt(
                stackId,
                outputLineId,
                itemDefinitionId,
                quantity,
                mass,
                itemInstanceId)
        });
#endif
}

public readonly struct PhysicalItemExactSourcePublicationReceipt
{
    private readonly IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> stacks;

    internal PhysicalItemExactSourcePublicationReceipt(
        FacilityBufferPlannedOutputPublicationReceipt publication,
        long totalMassGrams,
        bool retained)
    {
        BatchCommitId = publication.BatchCommitId;
        DestinationId = publication.DestinationId;
        TotalMassGrams = totalMassGrams;
        FacilityBufferPublishedOutputStackReceipt[] copied = publication.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        stacks = Array.AsReadOnly(copied);
        IsRetained = retained;
    }

    public string BatchCommitId { get; }
    public string DestinationId { get; }
    public long TotalMassGrams { get; }
    public IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> Stacks =>
        stacks ?? Array.Empty<FacilityBufferPublishedOutputStackReceipt>();
    public bool IsRetained { get; }
}

public interface IPhysicalItemExactSourcePublicationService
{
    bool TryPrepare(
        PhysicalItemExactSourcePublicationPlan plan,
        out PhysicalItemExactSourcePublicationTransaction transaction,
        out string failureReason);

    bool TryCommitRetained(
        PhysicalItemExactSourcePublicationTransaction transaction,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason);

    bool TryCommitReleased(
        PhysicalItemExactSourcePublicationTransaction transaction,
        Vector2Int releasePosition,
        string reasonCode,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason);

    bool TryCommitReleased(
        PhysicalItemExactSourcePublicationTransaction transaction,
        FacilityBufferAcknowledgedOutputReleaseTarget target,
        string reasonCode,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason);

    bool TryRollback(
        PhysicalItemExactSourcePublicationTransaction transaction,
        string reasonCode,
        out string failureReason);

    bool TryReleaseRetained(
        PhysicalItemExactSourcePublicationPlan plan,
        Vector2Int releasePosition,
        string reasonCode,
        out int releasedQuantity,
        out string failureReason);

    bool TrySinkRetained(
        PhysicalItemExactSourcePublicationPlan plan,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt disposition,
        out string failureReason);
}

public readonly struct PhysicalItemExactSourceRestoreDescriptor
{
    private readonly IReadOnlyList<string> expectedStackIds;

    public PhysicalItemExactSourceRestoreDescriptor(
        PhysicalItemExactSourcePublicationPlan plan,
        IReadOnlyList<string> expectedStackIds)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        string[] copied = (expectedStackIds
                ?? throw new ArgumentNullException(nameof(expectedStackIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (copied.Length == 0
            || copied.Any(value => !IsCanonical(value))
            || copied.Distinct(StringComparer.Ordinal).Count() != copied.Length)
        {
            throw new ArgumentException(
                "Expected source stack IDs must be non-empty, canonical, and unique.",
                nameof(expectedStackIds));
        }
        this.expectedStackIds = Array.AsReadOnly(copied);
    }

    public PhysicalItemExactSourcePublicationPlan Plan { get; }
    public IReadOnlyList<string> ExpectedStackIds => expectedStackIds
        ?? Array.Empty<string>();

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IPhysicalItemExactSourceRestoreAuthorityCommand
{
    bool TryReplaceRestoreAuthorities(
        IReadOnlyCollection<string> ownedDomains,
        IReadOnlyList<PhysicalItemExactSourceRestoreDescriptor> retained,
        out string failureReason);
}

/// <summary>
/// Publishes domain-created physical item vectors through the same exact gram,
/// atomic planned-output boundary as production. Prepared transactions are
/// deliberately transient and block save capture until committed or rolled back.
/// </summary>
public sealed class PhysicalItemExactSourcePublicationService :
    IPhysicalItemExactSourcePublicationService,
    IPhysicalItemExactSourceRestoreAuthorityCommand,
    IDungeonSaveCaptureGuard
{
    public const long CapacitySchemaRevision = 1L;

    private sealed class Pending
    {
        public PhysicalItemExactSourcePublicationPlan Plan;
        public FacilityBufferPlannedOutputToken Token;
        public FacilityBufferPlannedOutputPublicationReceipt Publication;
        public long TotalMassGrams;
        public bool AdmissionCommitted;
        public bool Acknowledged;
        public bool AcknowledgedReleaseOutput;
        public FacilityBufferAcknowledgedOutputReleaseTarget
            AcknowledgedReleaseTarget;
    }

    private readonly IPhysicalItemMassQuery mass;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferMassAdmissionService admission;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;
    private readonly IFacilityBufferDestinationReleaseService release;
    private readonly IPhysicalItemBatchDispositionService dispositions;
    private readonly IFacilityBufferAcknowledgedOutputRestoreCandidateQuery acknowledged;
    private readonly Dictionary<string, Pending> pending =
        new(StringComparer.Ordinal);

    public PhysicalItemExactSourcePublicationService(
        IPhysicalItemMassQuery mass,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication,
        IFacilityBufferDestinationReleaseService release,
        IPhysicalItemBatchDispositionService dispositions,
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery acknowledged)
    {
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities ?? throw new ArgumentNullException(nameof(capacities));
        this.admission = admission ?? throw new ArgumentNullException(nameof(admission));
        this.publication = publication ?? throw new ArgumentNullException(nameof(publication));
        this.release = release ?? throw new ArgumentNullException(nameof(release));
        this.dispositions = dispositions ?? throw new ArgumentNullException(nameof(dispositions));
        this.acknowledged = acknowledged
            ?? throw new ArgumentNullException(nameof(acknowledged));
    }

    public bool TryPrepare(
        PhysicalItemExactSourcePublicationPlan plan,
        out PhysicalItemExactSourcePublicationTransaction transaction,
        out string failureReason)
    {
        transaction = default;
        failureReason = string.Empty;
        if (plan == null)
        {
            failureReason = "physical-exact-source-plan-missing";
            return false;
        }
        if (pending.ContainsKey(plan.BatchCommitId))
        {
            failureReason = "physical-exact-source-batch-already-prepared";
            return false;
        }

        long totalMass;
        try
        {
            totalMass = plan.Outputs.Aggregate(
                0L,
                (current, slice) => checked(
                    current + mass.GetQuantityMass(
                        slice.ItemDefinitionId,
                        slice.Subject,
                        slice.Quantity).Value));
        }
        catch (Exception exception) when (exception is OverflowException
            or ArgumentException
            or InvalidOperationException)
        {
            failureReason = "physical-exact-source-mass-projection:" + exception.Message;
            return false;
        }

        FacilityBufferDestinationClaim claim = new(
            plan.DestinationId,
            plan.DropPosition,
            plan.OwnerDomain,
            plan.OwnerOperationId,
            plan.AuthorityOwnerId,
            FacilityBufferDestinationAnchorKind.ReservedTarget,
            FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);
        FacilityBufferCapacityProfile profile = new(
            plan.DestinationId,
            plan.DropPosition,
            plan.OwnerDomain,
            plan.OwnerOperationId,
            plan.AuthorityOwnerId,
            new PhysicalMassGrams(totalMass),
            capacityRevision: CapacitySchemaRevision);
        if (!TryAddAuthority(plan.OwnerDomain, claim, profile, out failureReason))
            return false;

        FacilityBufferPlannedOutputRequest request = new(
            plan.PublicationOperationId,
            plan.BatchCommitId,
            plan.OutcomeFingerprint,
            plan.DestinationId,
            plan.DropPosition,
            plan.OwnerDomain,
            plan.OwnerOperationId,
            plan.AuthorityOwnerId,
            expectedCapacityRevision: CapacitySchemaRevision,
            plan.Outputs);
        if (!admission.TryReservePlannedOutput(
                request,
                out FacilityBufferPlannedOutputToken token,
                out _,
                out string reserveFailure))
        {
            RetireAuthorityOrThrow(plan);
            failureReason = "physical-exact-source-reserve:" + reserveFailure;
            return false;
        }
        if (!publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt published,
                out _,
                out string publishFailure))
        {
            ReleaseReservationOrThrow(token, "prepare-publication-failed");
            RetireAuthorityOrThrow(plan);
            failureReason = "physical-exact-source-publish:" + publishFailure;
            return false;
        }

        pending.Add(plan.BatchCommitId, new Pending
        {
            Plan = plan,
            Token = token,
            Publication = published,
            TotalMassGrams = totalMass
        });
        transaction = new PhysicalItemExactSourcePublicationTransaction(
            plan.BatchCommitId,
            plan.DestinationId,
            published.Stacks);
        return true;
    }

    public bool TryCommitRetained(
        PhysicalItemExactSourcePublicationTransaction transaction,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason) => TryCommit(
        transaction,
        releaseOutput: false,
        default,
        out receipt,
        out failureReason);

    public bool TryCommitReleased(
        PhysicalItemExactSourcePublicationTransaction transaction,
        Vector2Int releasePosition,
        string reasonCode,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason)
    {
        if (!IsCanonical(reasonCode))
        {
            receipt = default;
            failureReason = "physical-exact-source-release-reason-invalid";
            return false;
        }
        return TryCommit(
            transaction,
            releaseOutput: true,
            FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned,
            out receipt,
            out failureReason);
    }

    public bool TryCommitReleased(
        PhysicalItemExactSourcePublicationTransaction transaction,
        FacilityBufferAcknowledgedOutputReleaseTarget target,
        string reasonCode,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason)
    {
        if (!IsCanonical(reasonCode) || !target.IsValid)
        {
            receipt = default;
            failureReason = "physical-exact-source-release-target-invalid";
            return false;
        }
        return TryCommit(
            transaction,
            releaseOutput: true,
            target,
            out receipt,
            out failureReason);
    }

    public bool TryRollback(
        PhysicalItemExactSourcePublicationTransaction transaction,
        string reasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsCanonical(reasonCode)
            || !TryResolve(transaction, out Pending state, out failureReason))
            return false;
        if (state.AdmissionCommitted)
        {
            failureReason = "physical-exact-source-committed-cannot-rollback";
            return false;
        }
        if (!publication.TryRollbackPublishedBatch(
                state.Publication,
                out _,
                out string publicationFailure))
        {
            failureReason = "physical-exact-source-rollback-publication:"
                + publicationFailure;
            return false;
        }
        if (!admission.TryReleasePlannedOutput(
                state.Token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out string admissionFailure))
        {
            throw new InvalidOperationException(
                "Exact source physical batch rolled back but admission release failed: "
                + admissionFailure);
        }
        RetireAuthorityOrThrow(state.Plan);
        pending.Remove(state.Plan.BatchCommitId);
        return true;
    }

    public bool TryReleaseRetained(
        PhysicalItemExactSourcePublicationPlan plan,
        Vector2Int releasePosition,
        string reasonCode,
        out int releasedQuantity,
        out string failureReason)
    {
        releasedQuantity = 0;
        failureReason = string.Empty;
        if (plan == null || !IsCanonical(reasonCode))
        {
            failureReason = "physical-exact-source-retained-release-invalid";
            return false;
        }
        if (!release.TryReleaseAtOwnerPosition(
                plan.DestinationId,
                releasePosition,
                reasonCode,
                out releasedQuantity,
                out failureReason))
            return false;
        RetireAuthorityOrThrow(plan);
        return true;
    }

    public bool TrySinkRetained(
        PhysicalItemExactSourcePublicationPlan plan,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt disposition,
        out string failureReason)
    {
        disposition = default;
        failureReason = string.Empty;
        if (plan == null || !IsCanonical(reasonCode))
        {
            failureReason = "physical-exact-source-retained-sink-invalid";
            return false;
        }
        if (!publication.TryCaptureBatch(
                plan.BatchCommitId,
                allowAcknowledged: true,
                out var batch,
                out bool isAcknowledged,
                out _,
                out _)
            || !isAcknowledged
            || batch.Stacks.Count == 0
            || batch.Stacks.Any(value => value.State
                    != WorldItemStackState.FacilityOutputBuffer
                || !string.Equals(
                    value.DestinationId,
                    plan.DestinationId,
                    StringComparison.Ordinal)))
        {
            failureReason = "physical-exact-source-retained-batch-missing-or-drifted";
            return false;
        }
        PhysicalItemTransformInput[] inputs = batch.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => new PhysicalItemTransformInput(
                value.StackId,
                value.Quantity))
            .ToArray();
        if (!dispositions.TryCommit(
                inputs,
                PhysicalItemDispositionKind.Sink,
                plan.BatchCommitId + ":sink",
                reasonCode,
                out disposition,
                out failureReason))
            return false;
        RetireAuthorityOrThrow(plan);
        return true;
    }

    public void ValidateBeforeCapture()
    {
        if (pending.Count == 0)
            return;
        string rows = string.Join(
            ",",
            pending.Keys.OrderBy(value => value, StringComparer.Ordinal));
        throw new InvalidOperationException(
            "Physical exact-source publication has incomplete transactions: " + rows);
    }

    public bool TryReplaceRestoreAuthorities(
        IReadOnlyCollection<string> ownedDomains,
        IReadOnlyList<PhysicalItemExactSourceRestoreDescriptor> retained,
        out string failureReason)
    {
        // Restore replaces the complete owner-domain projection, including an
        // explicitly empty domain, so removed incidents cannot retain authority.
        failureReason = string.Empty;
        string[] domains = (ownedDomains ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        PhysicalItemExactSourceRestoreDescriptor[] descriptors = (retained
                ?? Array.Empty<PhysicalItemExactSourceRestoreDescriptor>())
            .OrderBy(value => value.Plan?.OwnerDomain, StringComparer.Ordinal)
            .ThenBy(value => value.Plan?.OwnerOperationId, StringComparer.Ordinal)
            .ToArray();
        if (domains.Length == 0
            || domains.Any(value => !IsCanonical(value))
            || domains.Distinct(StringComparer.Ordinal).Count() != domains.Length
            || descriptors.Any(value => value.Plan == null
                || !domains.Contains(
                    value.Plan.OwnerDomain,
                    StringComparer.Ordinal))
            || descriptors.Select(value => value.Plan.BatchCommitId)
                .Distinct(StringComparer.Ordinal).Count() != descriptors.Length)
        {
            failureReason = "physical-exact-source-restore-request-invalid";
            return false;
        }
        if (!acknowledged.IsCandidateAvailable)
        {
            failureReason = "physical-exact-source-restore-candidate-unavailable";
            return false;
        }

        Dictionary<string, (FacilityBufferDestinationClaim Claim,
                FacilityBufferCapacityProfile Profile)> authorities =
            new(StringComparer.Ordinal);
        foreach (PhysicalItemExactSourceRestoreDescriptor descriptor in descriptors)
        {
            if (!TryValidateRestoreDescriptor(
                    descriptor,
                    out long totalMassGrams,
                    out failureReason))
                return false;
            PhysicalItemExactSourcePublicationPlan plan = descriptor.Plan;
            authorities.Add(
                plan.DestinationId,
                (new FacilityBufferDestinationClaim(
                        plan.DestinationId,
                        plan.DropPosition,
                        plan.OwnerDomain,
                        plan.OwnerOperationId,
                        plan.AuthorityOwnerId,
                        FacilityBufferDestinationAnchorKind.ReservedTarget,
                        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired),
                    new FacilityBufferCapacityProfile(
                        plan.DestinationId,
                        plan.DropPosition,
                        plan.OwnerDomain,
                        plan.OwnerOperationId,
                        plan.AuthorityOwnerId,
                        new PhysicalMassGrams(totalMassGrams),
                        capacityRevision: CapacitySchemaRevision)));
        }

        foreach (string domain in domains)
        {
            var owned = authorities.Values
                .Where(value => string.Equals(
                    value.Claim.OwnerDomain,
                    domain,
                    StringComparison.Ordinal))
                .OrderBy(value => value.Claim.DestinationId, StringComparer.Ordinal)
                .ToArray();
            if (!lifecycle.TryReplaceOwnedAuthorities(
                    domain,
                    owned.Select(value => value.Claim).ToArray(),
                    owned.Select(value => value.Profile).ToArray(),
                    out failureReason))
            {
                failureReason = "physical-exact-source-restore-authority:"
                    + failureReason;
                return false;
            }
        }
        return true;
    }

    private bool TryCommit(
        PhysicalItemExactSourcePublicationTransaction transaction,
        bool releaseOutput,
        FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget,
        out PhysicalItemExactSourcePublicationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (!TryResolve(transaction, out Pending state, out failureReason))
            return false;
        if (!state.AdmissionCommitted)
        {
            if (!admission.TryCommitPlannedOutput(
                    state.Token,
                    state.Publication,
                    out FacilityBufferPlannedOutputReceipt committed,
                    out _,
                    out failureReason)
                || committed.CommittedMassGrams != state.TotalMassGrams)
            {
                failureReason = "physical-exact-source-admission-commit:"
                    + failureReason;
                return false;
            }
            state.AdmissionCommitted = true;
        }

        if (state.Acknowledged
            && !MatchesAcknowledgementMode(
                state,
                releaseOutput,
                releaseTarget))
        {
            failureReason =
                "physical-exact-source-acknowledgement-retry-mismatch";
            return false;
        }
        if (!state.Acknowledged)
        {
            bool acknowledgedNow = releaseOutput
                ? publication.TryAcknowledgeAndReleasePublishedBatch(
                    state.Publication,
                    releaseTarget,
                    out _,
                    out failureReason)
                : publication.TryAcknowledgePublishedBatch(
                    state.Publication,
                    out _,
                    out failureReason);
            if (!acknowledgedNow)
            {
                failureReason = "physical-exact-source-acknowledgement:"
                    + failureReason;
                return false;
            }
            state.Acknowledged = true;
            state.AcknowledgedReleaseOutput = releaseOutput;
            state.AcknowledgedReleaseTarget = releaseTarget;
        }
        if (releaseOutput
            && !TryRetireAuthority(state.Plan, out string retirementFailure))
        {
            failureReason = "physical-exact-source-authority-retirement:"
                + retirementFailure;
            return false;
        }
        pending.Remove(state.Plan.BatchCommitId);
        receipt = new PhysicalItemExactSourcePublicationReceipt(
            state.Publication,
            state.TotalMassGrams,
            retained: !releaseOutput);
        return true;
    }

    private bool TryResolve(
        PhysicalItemExactSourcePublicationTransaction transaction,
        out Pending state,
        out string failureReason)
    {
        state = null;
        failureReason = string.Empty;
        if (!transaction.IsPrepared
            || !pending.TryGetValue(transaction.BatchCommitId, out state)
            || !string.Equals(
                transaction.DestinationId,
                state.Plan.DestinationId,
                StringComparison.Ordinal))
        {
            failureReason = "physical-exact-source-transaction-missing-or-mismatched";
            return false;
        }
        return true;
    }

    private static bool MatchesAcknowledgementMode(
        Pending state,
        bool releaseOutput,
        FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget)
    {
        if (state.AcknowledgedReleaseOutput != releaseOutput)
            return false;
        if (!releaseOutput)
            return true;
        FacilityBufferAcknowledgedOutputReleaseTarget acknowledgedTarget =
            state.AcknowledgedReleaseTarget;
        return acknowledgedTarget.HasDestination == releaseTarget.HasDestination
            && string.Equals(
                acknowledgedTarget.DestinationId,
                releaseTarget.DestinationId,
                StringComparison.Ordinal)
            && acknowledgedTarget.DestinationPosition
                == releaseTarget.DestinationPosition;
    }

    private bool TryValidateRestoreDescriptor(
        PhysicalItemExactSourceRestoreDescriptor descriptor,
        out long totalMassGrams,
        out string failureReason)
    {
        totalMassGrams = 0L;
        failureReason = string.Empty;
        PhysicalItemExactSourcePublicationPlan plan = descriptor.Plan;
        if (!acknowledged.TryGetBatch(
                plan.BatchCommitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
            || !string.Equals(
                batch.OutcomeFingerprint,
                plan.OutcomeFingerprint,
                StringComparison.Ordinal)
            || batch.Stacks.Count == 0
            || batch.Stacks.Any(value =>
                value.State != WorldItemStackState.FacilityOutputBuffer
                || value.Position != plan.DropPosition
                || !string.Equals(
                    value.DestinationId,
                    plan.DestinationId,
                    StringComparison.Ordinal)))
        {
            failureReason = "physical-exact-source-restore-batch-missing-or-drifted:"
                + plan.BatchCommitId;
            return false;
        }
        string[] actualStackIds = batch.Stacks
            .Select(value => value.StackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actualStackIds.SequenceEqual(
                descriptor.ExpectedStackIds,
                StringComparer.Ordinal))
        {
            failureReason = "physical-exact-source-restore-stack-vector-mismatch:"
                + plan.BatchCommitId;
            return false;
        }
        Dictionary<string, int> expectedLines = plan.Outputs.ToDictionary(
            value => value.OutputLineId,
            value => value.Quantity,
            StringComparer.Ordinal);
        Dictionary<string, int> actualLines = batch.Stacks
            .GroupBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.Quantity),
                StringComparer.Ordinal);
        if (expectedLines.Count != actualLines.Count
            || expectedLines.Any(value => !actualLines.TryGetValue(
                    value.Key,
                    out int actualQuantity)
                || actualQuantity != value.Value))
        {
            failureReason = "physical-exact-source-restore-output-vector-mismatch:"
                + plan.BatchCommitId;
            return false;
        }
        totalMassGrams = batch.TotalMassGrams;
        return totalMassGrams > 0L;
    }

    private bool TryAddAuthority(
        string ownerDomain,
        FacilityBufferDestinationClaim desiredClaim,
        FacilityBufferCapacityProfile desiredProfile,
        out string failureReason)
    {
        FacilityBufferDestinationClaim[] currentClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] currentProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal))
            .ToArray();
        if (currentClaims.Any(value => string.Equals(
                value.DestinationId,
                desiredClaim.DestinationId,
                StringComparison.Ordinal))
            || currentProfiles.Any(value => string.Equals(
                value.DestinationId,
                desiredProfile.DestinationId,
                StringComparison.Ordinal)))
        {
            failureReason = "physical-exact-source-authority-already-exists";
            return false;
        }
        return lifecycle.TryReplaceOwnedAuthorities(
            ownerDomain,
            currentClaims.Append(desiredClaim).ToArray(),
            currentProfiles.Append(desiredProfile).ToArray(),
            out failureReason);
    }

    private void RetireAuthorityOrThrow(PhysicalItemExactSourcePublicationPlan plan)
    {
        if (!TryRetireAuthority(plan, out string failureReason))
        {
            throw new InvalidOperationException(
                "Physical exact-source authority retirement failed: "
                + failureReason);
        }
    }

    private bool TryRetireAuthority(
        PhysicalItemExactSourcePublicationPlan plan,
        out string failureReason)
    {
        FacilityBufferDestinationClaim[] retainedClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => string.Equals(
                    value.OwnerDomain,
                    plan.OwnerDomain,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.DestinationId,
                    plan.DestinationId,
                    StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] retainedProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => string.Equals(
                    value.OwnerDomain,
                    plan.OwnerDomain,
                    StringComparison.Ordinal)
                && !string.Equals(
                    value.DestinationId,
                    plan.DestinationId,
                    StringComparison.Ordinal))
            .ToArray();
        return lifecycle.TryReplaceOwnedAuthorities(
            plan.OwnerDomain,
            retainedClaims,
            retainedProfiles,
            out failureReason);
    }

    private void ReleaseReservationOrThrow(
        FacilityBufferPlannedOutputToken token,
        string context)
    {
        if (!admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Exact source admission release failed after " + context + ": "
                + failureReason);
        }
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

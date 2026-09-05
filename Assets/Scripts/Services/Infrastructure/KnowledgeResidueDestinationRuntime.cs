using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IKnowledgeResidueDestinationRuntime
{
    bool TryEnsure(
        KnowledgeResidueTaskSaveData task,
        BuildableObject facility,
        out string failureReason);

    bool TryReplace(
        IReadOnlyList<KnowledgeResidueTaskSaveData> tasks,
        IReadOnlyList<BuildableObject> facilities,
        out string failureReason);

    bool TryRevoke(
        KnowledgeResidueTaskSaveData task,
        out string failureReason);

    bool TryValidate(
        KnowledgeResidueTaskSaveData task,
        out string failureReason);
}

internal static class KnowledgeResidueDestinationAuthority
{
    internal const string OwnerDomain = "research.knowledge-residue";
    internal const string MemoryResidueItemId = "captivity:memory-residue";
    internal const string SinkReasonCode = "memory-residue-research-consumed";
    internal const long CapacitySchemaRevision = 1L;

    internal static string FormatDestinationId(
        string taskId,
        int assignmentSequence) =>
        ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
        + OwnerDomain + ":" + (taskId ?? string.Empty)
        + $":{assignmentSequence:D8}";

    internal static string FormatOwnerOperationId(string taskId) =>
        "research-knowledge-residue-destination:"
        + (taskId ?? string.Empty);

    internal static string FormatSinkOperationId(string taskId) =>
        "research-knowledge-residue-sink:"
        + (taskId ?? string.Empty);
}

/// <summary>
/// Projects each queued knowledge task into the common exact-gram
/// FacilityBuffer claim/profile authority. Task state stores the projection so
/// current-format restore can reject stale item-mass or facility identity.
/// </summary>
public sealed class KnowledgeResidueDestinationRuntime :
    IKnowledgeResidueDestinationRuntime
{
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery claims;
    private readonly IFacilityBufferMassCapacityAuthorityQuery capacities;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;

    public KnowledgeResidueDestinationRuntime(
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferDestinationClaimAuthorityQuery claims,
        IFacilityBufferMassCapacityAuthorityQuery capacities,
        IFacilityBufferDestinationLifecycleCommand lifecycle)
    {
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    [GameplayInternalOnly(
        "Publishes one exact memory-residue task destination before delivery.",
        "KnowledgeResidueProcessingRuntime only")]
    public bool TryEnsure(
        KnowledgeResidueTaskSaveData task,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (task == null || facility == null)
        {
            failureReason = "knowledge-residue-authority-owner-missing";
            return false;
        }

        string facilityInstanceId;
        try
        {
            facilityInstanceId = facility.RequirePersistentInstanceId().Value;
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                           or ArgumentException)
        {
            failureReason = "knowledge-residue-authority-facility-invalid:"
                + exception.Message;
            return false;
        }

        if (HasStoredProjection(task))
        {
            return TryValidateProjection(
                    task,
                    facilityInstanceId,
                    out failureReason)
                && TryFindOwnedPair(task, out _, out _, out failureReason);
        }

        TaskProjectionSnapshot previous = new(task);
        try
        {
            if (!TryProject(
                    task,
                    facilityInstanceId,
                    out long capacity,
                    out string fingerprint,
                    out failureReason))
            {
                return false;
            }

            task.facilityId = facility.id;
            task.facilityX = facility.centerPos.x;
            task.facilityY = facility.centerPos.y;
            task.facilityInstanceId = facilityInstanceId;
            task.inputCapacityGrams = capacity;
            task.massAuthorityRevision = massQuery.AuthorityRevision;
            task.inputCapacityFingerprint = fingerprint;

            if (!TryCaptureOwnedPairs(
                    out List<FacilityBufferDestinationClaim> ownedClaims,
                    out List<FacilityBufferCapacityProfile> ownedProfiles,
                    out failureReason))
            {
                return false;
            }

            FacilityBufferDestinationClaim claim = CreateClaim(task);
            FacilityBufferCapacityProfile profile = CreateProfile(task);
            if (ownedClaims.Any(value => string.Equals(
                    value.DestinationId,
                    claim.DestinationId,
                    StringComparison.Ordinal)))
            {
                failureReason =
                    "knowledge-residue-authority-destination-duplicate:"
                    + claim.DestinationId;
                return false;
            }

            ownedClaims.Add(claim);
            ownedProfiles.Add(profile);
            if (lifecycle.TryReplaceOwnedAuthorities(
                    KnowledgeResidueDestinationAuthority.OwnerDomain,
                    ownedClaims,
                    ownedProfiles,
                    out failureReason))
            {
                return true;
            }
            failureReason = "knowledge-residue-authority-publish-failed:"
                + failureReason;
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "knowledge-residue-authority-projection-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(failureReason))
            {
                previous.Restore(task);
            }
        }
    }

    public bool TryReplace(
        IReadOnlyList<KnowledgeResidueTaskSaveData> tasks,
        IReadOnlyList<BuildableObject> facilities,
        out string failureReason)
    {
        failureReason = string.Empty;
        BuildableObject[] candidates = (facilities
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null && !value.isDestroy)
            .ToArray();
        List<FacilityBufferDestinationClaim> desiredClaims = new();
        List<FacilityBufferCapacityProfile> desiredProfiles = new();
        foreach (KnowledgeResidueTaskSaveData task in
                 (tasks ?? Array.Empty<KnowledgeResidueTaskSaveData>())
                     .Where(value => value != null
                         && HasStoredProjection(value)
                         && value.dispositionPhase ==
                            KnowledgeResidueDispositionPhase.AwaitingInput)
                     .OrderBy(value => value.taskId, StringComparer.Ordinal))
        {
            BuildableObject facility = candidates.SingleOrDefault(value =>
                value.id == task.facilityId
                && value.centerPos.x == task.facilityX
                && value.centerPos.y == task.facilityY
                && string.Equals(
                    value.RequirePersistentInstanceId().Value,
                    task.facilityInstanceId,
                    StringComparison.Ordinal)
                && value.SupportsWork(BuiltInWorkTypeIds.Research));
            if (facility == null
                || !TryValidateProjection(
                    task,
                    task.facilityInstanceId,
                    out failureReason))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "knowledge-residue-authority-restore-facility-missing:"
                        + task.taskId
                    : failureReason;
                return false;
            }
            desiredClaims.Add(CreateClaim(task));
            desiredProfiles.Add(CreateProfile(task));
        }

        return lifecycle.TryReplaceOwnedAuthorities(
            KnowledgeResidueDestinationAuthority.OwnerDomain,
            desiredClaims,
            desiredProfiles,
            out failureReason);
    }

    public bool TryRevoke(
        KnowledgeResidueTaskSaveData task,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (task == null
            || !TryCaptureOwnedPairs(
                out List<FacilityBufferDestinationClaim> ownedClaims,
                out List<FacilityBufferCapacityProfile> ownedProfiles,
                out failureReason))
        {
            return false;
        }

        FacilityBufferDestinationClaim[] matchingClaims = ownedClaims
            .Where(value => string.Equals(
                value.DestinationId,
                task.destinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = ownedProfiles
            .Where(value => string.Equals(
                value.DestinationId,
                task.destinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length == 0 && matchingProfiles.Length == 0)
        {
            return true;
        }
        if (matchingClaims.Length != 1
            || matchingProfiles.Length != 1
            || !PairMatches(task, matchingClaims[0], matchingProfiles[0]))
        {
            failureReason =
                "knowledge-residue-authority-revoke-pair-invalid:"
                + task.taskId;
            return false;
        }

        ownedClaims.Remove(matchingClaims[0]);
        ownedProfiles.Remove(matchingProfiles[0]);
        return lifecycle.TryReplaceOwnedAuthorities(
            KnowledgeResidueDestinationAuthority.OwnerDomain,
            ownedClaims,
            ownedProfiles,
            out failureReason);
    }

    public bool TryValidate(
        KnowledgeResidueTaskSaveData task,
        out string failureReason) =>
        TryValidateProjection(
            task,
            task?.facilityInstanceId,
            out failureReason)
        && TryFindOwnedPair(task, out _, out _, out failureReason);

    private bool TryProject(
        KnowledgeResidueTaskSaveData task,
        string facilityInstanceId,
        out long capacity,
        out string fingerprint,
        out string failureReason)
    {
        capacity = 0L;
        fingerprint = string.Empty;
        failureReason = string.Empty;
        if (task == null
            || !IsCanonicalRequired(task.taskId)
            || !IsCanonicalRequired(facilityInstanceId)
            || !string.Equals(
                task.destinationId,
                KnowledgeResidueDestinationAuthority.FormatDestinationId(
                    task.taskId,
                    task.assignmentSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                task.sinkOperationId,
                KnowledgeResidueDestinationAuthority.FormatSinkOperationId(
                    task.taskId),
                StringComparison.Ordinal)
            || !string.Equals(
                task.sinkReasonCode,
                KnowledgeResidueDestinationAuthority.SinkReasonCode,
                StringComparison.Ordinal)
            || task.assignmentSequence <= 0)
        {
            failureReason = "knowledge-residue-authority-identity-invalid";
            return false;
        }

        capacity = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)KnowledgeResidueDestinationAuthority
                .MemoryResidueItemId).Value;
        if (capacity <= 0L)
        {
            failureReason = "knowledge-residue-authority-mass-not-positive";
            return false;
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("knowledge-residue-input-capacity-v1");
        digest.Append(task.taskId);
        digest.Append(facilityInstanceId);
        digest.Append(task.destinationId);
        digest.Append(task.sinkOperationId);
        digest.Append(task.sinkReasonCode);
        digest.Append(KnowledgeResidueDestinationAuthority.MemoryResidueItemId);
        digest.Append(massQuery.AuthorityRevision);
        digest.Append(capacity);
        fingerprint = digest.ComputeSha256();
        return true;
    }

    private bool TryValidateProjection(
        KnowledgeResidueTaskSaveData task,
        string facilityInstanceId,
        out string failureReason)
    {
        if (!TryProject(
                task,
                facilityInstanceId,
                out long capacity,
                out string fingerprint,
                out failureReason))
        {
            return false;
        }
        if (task.facilityId <= 0
            || task.inputCapacityGrams != capacity
            || task.massAuthorityRevision != massQuery.AuthorityRevision
            || !string.Equals(
                task.inputCapacityFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "knowledge-residue-authority-stored-projection-invalid:"
                + task.taskId;
            return false;
        }
        return true;
    }

    private bool TryFindOwnedPair(
        KnowledgeResidueTaskSaveData task,
        out FacilityBufferDestinationClaim claim,
        out FacilityBufferCapacityProfile profile,
        out string failureReason)
    {
        claim = null;
        profile = null;
        failureReason = string.Empty;
        if (task == null)
        {
            failureReason = "knowledge-residue-authority-task-missing";
            return false;
        }
        FacilityBufferDestinationClaim[] matchingClaims = claims
            .CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                task.destinationId,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] matchingProfiles = capacities
            .CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.DestinationId,
                task.destinationId,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length != 1 || matchingProfiles.Length != 1)
        {
            failureReason = "knowledge-residue-authority-pair-cardinality:"
                + matchingClaims.Length + ":" + matchingProfiles.Length;
            return false;
        }
        claim = matchingClaims[0];
        profile = matchingProfiles[0];
        if (!PairMatches(task, claim, profile))
        {
            claim = null;
            profile = null;
            failureReason = "knowledge-residue-authority-pair-mismatch:"
                + task.taskId;
            return false;
        }
        return true;
    }

    private bool TryCaptureOwnedPairs(
        out List<FacilityBufferDestinationClaim> ownedClaims,
        out List<FacilityBufferCapacityProfile> ownedProfiles,
        out string failureReason)
    {
        ownedClaims = claims.CaptureAuthorityClaims()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                KnowledgeResidueDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        ownedProfiles = capacities.CaptureAuthorityProfiles()
            .Where(value => value != null && string.Equals(
                value.OwnerDomain,
                KnowledgeResidueDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal))
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToList();
        failureReason = string.Empty;
        if (ownedClaims.Count != ownedProfiles.Count
            || !ownedClaims.Select(value => value.DestinationId)
                .SequenceEqual(
                    ownedProfiles.Select(value => value.DestinationId),
                    StringComparer.Ordinal))
        {
            failureReason =
                "knowledge-residue-authority-owner-set-mismatch";
            return false;
        }
        return true;
    }

    private static FacilityBufferDestinationClaim CreateClaim(
        KnowledgeResidueTaskSaveData task) => new(
        task.destinationId,
        new Vector2Int(task.facilityX, task.facilityY),
        KnowledgeResidueDestinationAuthority.OwnerDomain,
        KnowledgeResidueDestinationAuthority.FormatOwnerOperationId(
            task.taskId),
        task.facilityInstanceId,
        FacilityBufferDestinationAnchorKind.LiveFacility,
        FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);

    private static FacilityBufferCapacityProfile CreateProfile(
        KnowledgeResidueTaskSaveData task) => new(
        task.destinationId,
        new Vector2Int(task.facilityX, task.facilityY),
        KnowledgeResidueDestinationAuthority.OwnerDomain,
        KnowledgeResidueDestinationAuthority.FormatOwnerOperationId(
            task.taskId),
        task.facilityInstanceId,
        new PhysicalMassGrams(task.inputCapacityGrams),
        KnowledgeResidueDestinationAuthority.CapacitySchemaRevision);

    private static bool PairMatches(
        KnowledgeResidueTaskSaveData task,
        FacilityBufferDestinationClaim claim,
        FacilityBufferCapacityProfile profile) =>
        task != null
        && claim != null
        && profile != null
        && claim.DropPosition == new Vector2Int(task.facilityX, task.facilityY)
        && profile.DropPosition == claim.DropPosition
        && string.Equals(claim.DestinationId, task.destinationId,
            StringComparison.Ordinal)
        && string.Equals(profile.DestinationId, task.destinationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerDomain,
            KnowledgeResidueDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerDomain,
            KnowledgeResidueDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerOperationId,
            KnowledgeResidueDestinationAuthority.FormatOwnerOperationId(
                task.taskId),
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerOperationId,
            claim.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(claim.OwnerFacilityId, task.facilityInstanceId,
            StringComparison.Ordinal)
        && string.Equals(profile.OwnerFacilityId, task.facilityInstanceId,
            StringComparison.Ordinal)
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.AdmissionPolicy
            == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
        && profile.MaxMassGrams == task.inputCapacityGrams
        && profile.CapacityRevision
            == KnowledgeResidueDestinationAuthority.CapacitySchemaRevision;

    private static bool HasStoredProjection(
        KnowledgeResidueTaskSaveData task) => task != null
        && (!string.IsNullOrEmpty(task.facilityInstanceId)
            || task.inputCapacityGrams != 0L
            || task.massAuthorityRevision != 0L
            || !string.IsNullOrEmpty(task.inputCapacityFingerprint));

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private readonly struct TaskProjectionSnapshot
    {
        internal TaskProjectionSnapshot(KnowledgeResidueTaskSaveData task)
        {
            FacilityId = task.facilityId;
            FacilityX = task.facilityX;
            FacilityY = task.facilityY;
            AssignmentSequence = task.assignmentSequence;
            FacilityInstanceId = task.facilityInstanceId;
            Capacity = task.inputCapacityGrams;
            Revision = task.massAuthorityRevision;
            Fingerprint = task.inputCapacityFingerprint;
        }

        private int FacilityId { get; }
        private int FacilityX { get; }
        private int FacilityY { get; }
        private int AssignmentSequence { get; }
        private string FacilityInstanceId { get; }
        private long Capacity { get; }
        private long Revision { get; }
        private string Fingerprint { get; }

        internal void Restore(KnowledgeResidueTaskSaveData task)
        {
            task.facilityId = FacilityId;
            task.facilityX = FacilityX;
            task.facilityY = FacilityY;
            task.assignmentSequence = AssignmentSequence;
            task.facilityInstanceId = FacilityInstanceId;
            task.inputCapacityGrams = Capacity;
            task.massAuthorityRevision = Revision;
            task.inputCapacityFingerprint = Fingerprint;
        }
    }
}

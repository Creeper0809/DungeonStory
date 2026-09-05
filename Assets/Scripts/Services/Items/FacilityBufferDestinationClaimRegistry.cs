using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum FacilityBufferDestinationAnchorKind
{
    LiveFacility = 0,
    ReservedTarget = 1,
    LiveBuilding = 2
}

public enum FacilityBufferDestinationAdmissionPolicy
{
    CountCompatible = 0,
    ExactGramRequired = 1
}

public static class ReservedTargetDestinationIdentity
{
    public const string PhysicalSourceBufferPrefix = "physical-source-buffer:";
    public const string ExactFacilityInputPrefix =
        ExactFacilityInputDestinationIdentity.Prefix;
    public const string ProductionInputPrefix = "production:";
    public const string ProductionStockSensorPrefix = "production-sensor:";
    public const string ExpeditionPrefix = "expedition:";
    public const string EquipmentRepairPrefix = "equipment-repair:";
    public const string SurgeryMaterialsPrefix = "surgery-materials:";
    public const string ResearchArchivePrefix = "research-archive:";
    public const string PowerFuelPrefix = "power:";
    public const string QualityRejectedMarketDestination =
        "sale:quality-rejected";

    public static bool RequiresExactClaim(string destinationId) =>
        !string.IsNullOrWhiteSpace(destinationId)
        && (destinationId.StartsWith(
                PhysicalSourceBufferPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                ExactFacilityInputPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                ProductionInputPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(ExpeditionPrefix, StringComparison.Ordinal)
            || destinationId.StartsWith(
                ProductionStockSensorPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                EquipmentRepairPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                SurgeryMaterialsPrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                ResearchArchivePrefix,
                StringComparison.Ordinal)
            || destinationId.StartsWith(
                PowerFuelPrefix,
                StringComparison.Ordinal)
            || string.Equals(
                destinationId,
                QualityRejectedMarketDestination,
                StringComparison.Ordinal));
}

public enum FacilityBufferDestinationClaimFailureCode
{
    None = 0,
    InvalidClaim = 1,
    InvalidDestinationId = 2,
    InvalidOwnerDomain = 3,
    InvalidOwnerOperationId = 4,
    InvalidOwnerFacilityId = 5,
    InvalidAnchorKind = 6,
    DestinationConflict = 7,
    ClaimNotFound = 8,
    ClaimMismatch = 9,
    RestoreMutationAfterPublish = 10
}

/// <summary>
/// Immutable ownership evidence for one non-construction facility-buffer
/// destination. Strings are stable identifiers and compare ordinally; callers
/// must provide their canonical spelling rather than relying on normalization.
/// </summary>
public sealed class FacilityBufferDestinationClaim
{
    public FacilityBufferDestinationClaim(
        string destinationId,
        Vector2Int dropPosition,
        string ownerDomain,
        string ownerOperationId,
        string ownerFacilityId,
        FacilityBufferDestinationAnchorKind anchorKind,
        FacilityBufferDestinationAdmissionPolicy admissionPolicy =
            FacilityBufferDestinationAdmissionPolicy.CountCompatible)
    {
        DestinationId = destinationId;
        DropPosition = dropPosition;
        OwnerDomain = ownerDomain;
        OwnerOperationId = ownerOperationId;
        OwnerFacilityId = ownerFacilityId;
        AnchorKind = anchorKind;
        AdmissionPolicy = admissionPolicy;
    }

    public string DestinationId { get; }
    public Vector2Int DropPosition { get; }
    public string OwnerDomain { get; }
    public string OwnerOperationId { get; }
    public string OwnerFacilityId { get; }
    public FacilityBufferDestinationAnchorKind AnchorKind { get; }
    public FacilityBufferDestinationAdmissionPolicy AdmissionPolicy { get; }
}

public interface IFacilityBufferDestinationClaimQuery
{
    long Revision { get; }

    bool TryGetClaim(
        string destinationId,
        Vector2Int dropPosition,
        out FacilityBufferDestinationClaim claim);

    IReadOnlyList<FacilityBufferDestinationClaim> CaptureClaims();
}

/// <summary>
/// Internal authoring view used while a dungeon restore transaction is
/// staging. Ordinary gameplay queries continue to observe only live claims.
/// </summary>
public interface IFacilityBufferDestinationClaimAuthorityQuery
{
    bool TryGetAuthorityClaim(
        string destinationId,
        Vector2Int dropPosition,
        out FacilityBufferDestinationClaim claim);

    IReadOnlyList<FacilityBufferDestinationClaim> CaptureAuthorityClaims();
}

public interface IFacilityBufferDestinationClaimCommand
{
    bool TryClaim(
        FacilityBufferDestinationClaim claim,
        out FacilityBufferDestinationClaimFailureCode failureCode,
        out string failureReason);

    bool TryRevoke(
        FacilityBufferDestinationClaim expectedClaim,
        out FacilityBufferDestinationClaimFailureCode failureCode,
        out string failureReason);

    bool TryReplaceOwnedClaims(
        string ownerDomain,
        IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
        out FacilityBufferDestinationClaimFailureCode failureCode,
        out string failureReason);
}

/// <summary>
/// Single mutable authority for non-construction facility-buffer destination
/// ownership. Restore-time producers write an isolated candidate; publication
/// swaps that candidate into the live query atomically and reversibly.
/// </summary>
public sealed class FacilityBufferDestinationClaimRegistry :
    IFacilityBufferDestinationClaimQuery,
    IFacilityBufferDestinationClaimAuthorityQuery,
    IFacilityBufferDestinationClaimCommand,
    IDungeonPreStageRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "220.world.facility-buffer-destinations";

    private Dictionary<string, FacilityBufferDestinationClaim> live =
        CreateClaimMap();
    private Dictionary<string, FacilityBufferDestinationClaim> candidate;
    private Dictionary<string, FacilityBufferDestinationClaim> previousLive;
    private long revision;
    private long preparedPublishRevision;
    private long preparedRollbackRevision;
    private bool restoreActive;
    private bool published;

    public string ParticipantId => RestoreParticipantId;
    public long Revision => revision;

    public bool TryGetClaim(
        string destinationId,
        Vector2Int dropPosition,
        out FacilityBufferDestinationClaim claim)
    {
        claim = null;
        if (!IsCanonicalRequiredId(destinationId)
            || !live.TryGetValue(
                destinationId,
                out FacilityBufferDestinationClaim found)
            || found.DropPosition != dropPosition)
        {
            return false;
        }

        claim = found;
        return true;
    }

    public IReadOnlyList<FacilityBufferDestinationClaim> CaptureClaims() =>
        live.Values
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();

    public bool TryGetAuthorityClaim(
        string destinationId,
        Vector2Int dropPosition,
        out FacilityBufferDestinationClaim claim)
    {
        claim = null;
        IReadOnlyDictionary<string, FacilityBufferDestinationClaim> authority =
            GetAuthorityView();
        if (!IsCanonicalRequiredId(destinationId)
            || !authority.TryGetValue(
                destinationId,
                out FacilityBufferDestinationClaim found)
            || found.DropPosition != dropPosition)
        {
            return false;
        }

        claim = found;
        return true;
    }

    public IReadOnlyList<FacilityBufferDestinationClaim>
        CaptureAuthorityClaims() => GetAuthorityView().Values
        .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
        .ToArray();

    [GameplayInternalOnly(
        "Facility domains publish exact destination ownership before haul planning or restore rebind.",
        "Facility-buffer destination producer adapters")]
    public bool TryClaim(
        FacilityBufferDestinationClaim claim,
        out FacilityBufferDestinationClaimFailureCode failureCode,
        out string failureReason)
    {
        if (!TryValidateClaim(claim, out failureCode, out failureReason))
            return false;

        if (!TryGetMutationTarget(
                out Dictionary<string, FacilityBufferDestinationClaim> target,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        if (target.TryGetValue(
                claim.DestinationId,
                out FacilityBufferDestinationClaim existing))
        {
            if (ClaimsMatch(existing, claim))
            {
                failureCode = FacilityBufferDestinationClaimFailureCode.None;
                failureReason = string.Empty;
                return true;
            }

            failureCode =
                FacilityBufferDestinationClaimFailureCode.DestinationConflict;
            failureReason =
                $"Facility-buffer destination '{claim.DestinationId}' is already "
                + "claimed with different ownership evidence.";
            return false;
        }

        target.Add(claim.DestinationId, claim);
        if (!restoreActive)
            AdvanceRevision();
        failureCode = FacilityBufferDestinationClaimFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    [GameplayInternalOnly(
        "Only the owning facility domain may retire its exact destination evidence.",
        "Facility-buffer destination producer adapters")]
    public bool TryRevoke(
        FacilityBufferDestinationClaim expectedClaim,
        out FacilityBufferDestinationClaimFailureCode failureCode,
        out string failureReason)
    {
        if (!TryValidateClaim(
                expectedClaim,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        if (!TryGetMutationTarget(
                out Dictionary<string, FacilityBufferDestinationClaim> target,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        if (!target.TryGetValue(
                expectedClaim.DestinationId,
                out FacilityBufferDestinationClaim existing))
        {
            failureCode = FacilityBufferDestinationClaimFailureCode.ClaimNotFound;
            failureReason =
                $"Facility-buffer destination '{expectedClaim.DestinationId}' "
                + "has no claim to revoke.";
            return false;
        }
        if (!ClaimsMatch(existing, expectedClaim))
        {
            failureCode = FacilityBufferDestinationClaimFailureCode.ClaimMismatch;
            failureReason =
                $"Facility-buffer destination '{expectedClaim.DestinationId}' "
                + "does not match the expected ownership evidence.";
            return false;
        }

        target.Remove(expectedClaim.DestinationId);
        if (!restoreActive)
            AdvanceRevision();
        failureCode = FacilityBufferDestinationClaimFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    [GameplayInternalOnly(
        "Restore-capable destination owners atomically replace only their own claim set.",
        "Facility-buffer destination producer save sections")]
    public bool TryReplaceOwnedClaims(
        string ownerDomain,
        IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
        out FacilityBufferDestinationClaimFailureCode failureCode,
        out string failureReason)
    {
        if (!IsCanonicalRequiredId(ownerDomain))
        {
            failureCode =
                FacilityBufferDestinationClaimFailureCode.InvalidOwnerDomain;
            failureReason =
                "Facility-buffer owner domain must be a non-empty canonical id.";
            return false;
        }
        if (!TryGetMutationTarget(
                out Dictionary<string, FacilityBufferDestinationClaim> target,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        FacilityBufferDestinationClaim[] desired =
            (desiredClaims ?? Array.Empty<FacilityBufferDestinationClaim>())
            .ToArray();
        Dictionary<string, FacilityBufferDestinationClaim> replacements =
            CreateClaimMap();
        foreach (FacilityBufferDestinationClaim claim in desired)
        {
            if (!TryValidateClaim(claim, out failureCode, out failureReason))
                return false;
            if (!string.Equals(
                    claim.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
            {
                failureCode =
                    FacilityBufferDestinationClaimFailureCode.InvalidOwnerDomain;
                failureReason =
                    $"Facility-buffer claim '{claim.DestinationId}' belongs to "
                    + $"'{claim.OwnerDomain}', not '{ownerDomain}'.";
                return false;
            }
            if (!replacements.TryAdd(claim.DestinationId, claim))
            {
                failureCode =
                    FacilityBufferDestinationClaimFailureCode.DestinationConflict;
                failureReason =
                    $"Facility-buffer destination '{claim.DestinationId}' is duplicated in the replacement set.";
                return false;
            }
        }

        if (!restoreActive
            && OwnedClaimSetMatches(target, ownerDomain, replacements))
        {
            failureCode = FacilityBufferDestinationClaimFailureCode.None;
            failureReason = string.Empty;
            return true;
        }

        Dictionary<string, FacilityBufferDestinationClaim> next =
            CopyClaims(target);
        foreach (string destinationId in next.Values
                     .Where(value => value != null
                         && string.Equals(
                             value.OwnerDomain,
                             ownerDomain,
                             StringComparison.Ordinal))
                     .Select(value => value.DestinationId)
                     .ToArray())
        {
            next.Remove(destinationId);
        }
        foreach (KeyValuePair<string, FacilityBufferDestinationClaim> pair in
                 replacements)
        {
            if (next.TryGetValue(
                    pair.Key,
                    out FacilityBufferDestinationClaim foreign)
                && !ClaimsMatch(foreign, pair.Value))
            {
                failureCode =
                    FacilityBufferDestinationClaimFailureCode.DestinationConflict;
                failureReason =
                    $"Facility-buffer destination '{pair.Key}' is owned by another domain.";
                return false;
            }
            next[pair.Key] = pair.Value;
        }

        if (restoreActive)
            candidate = next;
        else
        {
            live = next;
            AdvanceRevision();
        }
        failureCode = FacilityBufferDestinationClaimFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    public void BeginRestoreCandidate()
    {
        if (restoreActive)
        {
            throw new InvalidOperationException(
                "Facility-buffer destination restore is already active.");
        }

        // Preflight every allocation and checked revision before exposing the
        // candidate. Publish and rollback are aggregate-restore callbacks and
        // must only swap references/values after Begin succeeds.
        Dictionary<string, FacilityBufferDestinationClaim> preparedPrevious =
            CopyClaims(live);
        Dictionary<string, FacilityBufferDestinationClaim> preparedCandidate =
            CreateClaimMap();
        long nextPublishRevision = checked(revision + 1L);
        long nextRollbackRevision = checked(nextPublishRevision + 1L);

        previousLive = preparedPrevious;
        candidate = preparedCandidate;
        preparedPublishRevision = nextPublishRevision;
        preparedRollbackRevision = nextRollbackRevision;
        restoreActive = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        live = candidate;
        candidate = null;
        published = true;
        revision = preparedPublishRevision;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreActive)
            return;

        if (published && previousLive != null)
        {
            live = previousLive;
            revision = preparedRollbackRevision;
        }
        ResetRestoreState();
    }

    public void CompleteRestoreCandidate()
    {
        ResetRestoreState();
    }

    public void DiscardRestoreCandidate()
    {
        if (published)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        ResetRestoreState();
    }

    private static bool TryValidateClaim(
        FacilityBufferDestinationClaim claim,
        out FacilityBufferDestinationClaimFailureCode failureCode,
        out string failureReason)
    {
        if (claim == null)
        {
            failureCode = FacilityBufferDestinationClaimFailureCode.InvalidClaim;
            failureReason = "Facility-buffer destination claim is null.";
            return false;
        }
        if (!IsCanonicalRequiredId(claim.DestinationId))
        {
            failureCode =
                FacilityBufferDestinationClaimFailureCode.InvalidDestinationId;
            failureReason =
                "Facility-buffer destination id must be a non-empty canonical id.";
            return false;
        }
        if (!IsCanonicalRequiredId(claim.OwnerDomain))
        {
            failureCode =
                FacilityBufferDestinationClaimFailureCode.InvalidOwnerDomain;
            failureReason =
                "Facility-buffer owner domain must be a non-empty canonical id.";
            return false;
        }
        if (!IsCanonicalRequiredId(claim.OwnerOperationId))
        {
            failureCode =
                FacilityBufferDestinationClaimFailureCode.InvalidOwnerOperationId;
            failureReason =
                "Facility-buffer owner operation id must be a non-empty canonical id.";
            return false;
        }
        if (claim.OwnerFacilityId != null
            && !IsCanonicalRequiredId(claim.OwnerFacilityId))
        {
            failureCode =
                FacilityBufferDestinationClaimFailureCode.InvalidOwnerFacilityId;
            failureReason =
                "Optional facility id must be null or a non-empty canonical id.";
            return false;
        }
        if ((claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
                || claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveBuilding)
            && !IsCanonicalRequiredId(claim.OwnerFacilityId))
        {
            failureCode =
                FacilityBufferDestinationClaimFailureCode.InvalidOwnerFacilityId;
            failureReason =
                "Live-building claims require an exact owner building id.";
            return false;
        }
        if (!Enum.IsDefined(
                typeof(FacilityBufferDestinationAnchorKind),
                claim.AnchorKind))
        {
            failureCode =
                FacilityBufferDestinationClaimFailureCode.InvalidAnchorKind;
            failureReason =
                $"Unsupported facility-buffer anchor kind '{claim.AnchorKind}'.";
            return false;
        }
        if (!Enum.IsDefined(
                typeof(FacilityBufferDestinationAdmissionPolicy),
                claim.AdmissionPolicy))
        {
            failureCode =
                FacilityBufferDestinationClaimFailureCode.InvalidClaim;
            failureReason =
                $"Unsupported facility-buffer admission policy "
                + $"'{claim.AdmissionPolicy}'.";
            return false;
        }

        failureCode = FacilityBufferDestinationClaimFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    private static bool ClaimsMatch(
        FacilityBufferDestinationClaim left,
        FacilityBufferDestinationClaim right) =>
        left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && left.AnchorKind == right.AnchorKind
        && left.AdmissionPolicy == right.AdmissionPolicy
        && string.Equals(
            left.DestinationId,
            right.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.OwnerDomain,
            right.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            left.OwnerOperationId,
            right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.OwnerFacilityId,
            right.OwnerFacilityId,
            StringComparison.Ordinal);

    private static bool OwnedClaimSetMatches(
        IReadOnlyDictionary<string, FacilityBufferDestinationClaim> current,
        string ownerDomain,
        IReadOnlyDictionary<string, FacilityBufferDestinationClaim> desired)
    {
        if (current == null || desired == null)
            return false;
        int ownedCount = 0;
        foreach (FacilityBufferDestinationClaim claim in current.Values)
        {
            if (claim != null && string.Equals(
                    claim.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
            {
                ownedCount++;
            }
        }
        if (ownedCount != desired.Count)
            return false;
        foreach (KeyValuePair<string, FacilityBufferDestinationClaim> pair in desired)
        {
            if (!current.TryGetValue(
                    pair.Key,
                    out FacilityBufferDestinationClaim candidate)
                || !ClaimsMatch(candidate, pair.Value))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsCanonicalRequiredId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static Dictionary<string, FacilityBufferDestinationClaim>
        CreateClaimMap() =>
        new(StringComparer.Ordinal);

    private static Dictionary<string, FacilityBufferDestinationClaim> CopyClaims(
        IReadOnlyDictionary<string, FacilityBufferDestinationClaim> source)
    {
        Dictionary<string, FacilityBufferDestinationClaim> copy = CreateClaimMap();
        if (source == null)
            return copy;
        foreach (KeyValuePair<string, FacilityBufferDestinationClaim> pair in source)
            copy.Add(pair.Key, pair.Value);
        return copy;
    }

    private IReadOnlyDictionary<string, FacilityBufferDestinationClaim>
        GetAuthorityView() =>
        restoreActive && !published && candidate != null
            ? candidate
            : live;

    private void AdvanceRevision()
    {
        revision = checked(revision + 1L);
    }

    private bool TryGetMutationTarget(
        out Dictionary<string, FacilityBufferDestinationClaim> target,
        out FacilityBufferDestinationClaimFailureCode failureCode,
        out string failureReason)
    {
        target = restoreActive ? candidate : live;
        if (!restoreActive || (!published && candidate != null))
        {
            failureCode = FacilityBufferDestinationClaimFailureCode.None;
            failureReason = string.Empty;
            return true;
        }

        target = null;
        failureCode =
            FacilityBufferDestinationClaimFailureCode.RestoreMutationAfterPublish;
        failureReason =
            "Facility-buffer destination claims cannot mutate after restore publication.";
        return false;
    }

    private void ResetRestoreState()
    {
        candidate = null;
        previousLive = null;
        preparedPublishRevision = 0L;
        preparedRollbackRevision = 0L;
        restoreActive = false;
        published = false;
    }
}

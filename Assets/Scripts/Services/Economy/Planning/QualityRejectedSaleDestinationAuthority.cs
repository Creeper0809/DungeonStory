using System;
using UnityEngine;
using VContainer.Unity;

public interface IQualityRejectedSaleDestinationAuthority
{
    bool TryEnsureTarget(
        out FacilityBufferAcknowledgedOutputReleaseTarget target,
        out string failureReason);
}

/// <summary>
/// Owns the one reserved physical market dropoff used by quality-rejected
/// outputs. The target is derived from the live world drop zone and its claim is
/// republished after restore before gameplay resumes.
/// </summary>
public sealed class QualityRejectedSaleDestinationAuthority :
    IQualityRejectedSaleDestinationAuthority,
    IInitializable,
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "220.world.quality-rejected-sale-destination";
    public const string OwnerDomain = "economy.quality-rejected-sale";
    public const string OwnerOperationId =
        "economy.quality-rejected-sale:market-destination";

    private readonly IWorldDropZoneQuery dropZones;
    private readonly IFacilityBufferDestinationClaimCommand claims;

    public QualityRejectedSaleDestinationAuthority(
        IWorldDropZoneQuery dropZones,
        IFacilityBufferDestinationClaimCommand claims)
    {
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        this.claims = claims
            ?? throw new ArgumentNullException(nameof(claims));
    }

    public string ParticipantId => RestoreParticipantId;

    public void Initialize()
    {
        // A run without a delivery drop zone can still initialize. The first
        // actual sale request remains fail-loud through TryEnsureTarget.
        TryEnsureTarget(out _, out _);
    }

    public void BeginRestoreCandidate()
    {
        // The claim registry candidate already exists because its participant
        // id sorts immediately before this owner. Missing world drop zones are
        // tolerated only when no restored market release consumes this claim;
        // that release validates the candidate and fails the whole RestoreAll.
        TryEnsureTarget(out _, out _);
    }

    public void PublishRestoreCandidate() { }
    public void RollbackPublishedRestoreCandidate() { }
    public void CompleteRestoreCandidate() { }
    public void DiscardRestoreCandidate() { }

    public bool TryEnsureTarget(
        out FacilityBufferAcknowledgedOutputReleaseTarget target,
        out string failureReason)
    {
        target = default;
        failureReason = string.Empty;
        if (!dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            failureReason = "quality-rejected-market-dropoff-missing";
            return false;
        }
        FacilityBufferDestinationClaim claim = new(
            QualityRejectedOutputRules.MarketDestinationId,
            dropoff,
            OwnerDomain,
            OwnerOperationId,
            null,
            FacilityBufferDestinationAnchorKind.ReservedTarget);
        if (!claims.TryReplaceOwnedClaims(
                OwnerDomain,
                new[] { claim },
                out FacilityBufferDestinationClaimFailureCode code,
                out string claimFailure))
        {
            failureReason = "quality-rejected-market-claim:"
                + code + ":" + claimFailure;
            return false;
        }
        target = new FacilityBufferAcknowledgedOutputReleaseTarget(
            QualityRejectedOutputRules.MarketDestinationId,
            dropoff);
        return true;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public enum CharacterMedicalSupplyDestinationDrainAdvanceStatus
{
    Closed = 1,
    Deferred = 2,
    Conflict = 3
}

public readonly struct CharacterMedicalSupplyDestinationDrainAdvanceResult
{
    public CharacterMedicalSupplyDestinationDrainAdvanceResult(
        CharacterMedicalSupplyDestinationDrainAdvanceStatus status,
        string closedFacilityId,
        string failureReason)
    {
        Status = status;
        ClosedFacilityId = closedFacilityId ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public CharacterMedicalSupplyDestinationDrainAdvanceStatus Status { get; }
    public string ClosedFacilityId { get; }
    public string FailureReason { get; }
    public bool IsClosed =>
        Status == CharacterMedicalSupplyDestinationDrainAdvanceStatus.Closed;
}

public interface ICharacterMedicalSupplyDestinationDrainRuntime
{
    CharacterMedicalSupplyDestinationDrainAdvanceResult TryBeginOrResume(
        CharacterMedicalOrder order,
        CharacterMedicalOrderState targetState,
        CharacterMedicalStatusCode targetStatusCode,
        IReadOnlyList<string> targetStatusParameters);

    CharacterMedicalSupplyDestinationDrainAdvanceResult TryResume(
        CharacterMedicalOrder order);
}

/// <summary>
/// Character Medical owner adapter over the Items-owned destination-custody
/// capability. One order may close several destination lifetimes; only the
/// sequence-scoped join is stored here while Items retains all physical state.
/// </summary>
internal sealed class CharacterMedicalSupplyDestinationDrainRuntime :
    ICharacterMedicalSupplyDestinationDrainRuntime
{
    private readonly IFacilityBufferDestinationCustodyDrainService drains;
    private readonly IFacilityBufferDestinationClaimQuery claims;
    private readonly ICharacterMedicalSupplyDestinationRuntime destinations;

    internal CharacterMedicalSupplyDestinationDrainRuntime(
        IFacilityBufferDestinationCustodyDrainService drains,
        IFacilityBufferDestinationClaimQuery claims,
        ICharacterMedicalSupplyDestinationRuntime destinations)
    {
        this.drains = drains ?? throw new ArgumentNullException(nameof(drains));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        if (!this.drains.RequiresImmediateRecoveryBeforeGameplayTick)
        {
            throw new ArgumentException(
                "Character medical custody requires immediate pre-gameplay recovery.",
                nameof(drains));
        }
    }

    [GameplayInternalOnly(
        "Begins or resumes a sequence-scoped Character Medical supply destination drain before the owner changes facility or closes.",
        "CharacterMedicalRuntime lifecycle only")]
    public CharacterMedicalSupplyDestinationDrainAdvanceResult
        TryBeginOrResume(
            CharacterMedicalOrder order,
            CharacterMedicalOrderState targetState,
            CharacterMedicalStatusCode targetStatusCode,
            IReadOnlyList<string> targetStatusParameters)
    {
        if (order == null
            || !IsAllowedTarget(targetState)
            || targetStatusCode is CharacterMedicalStatusCode.Unknown
                or CharacterMedicalStatusCode.MaterialDestinationDraining)
        {
            return Conflict(
                "character-medical-supply-drain-order-or-target-invalid");
        }

        CharacterMedicalSupplyDestinationDrainJoinData active =
            FindActiveJoin(order, out string cardinalityFailure);
        if (!string.IsNullOrEmpty(cardinalityFailure))
        {
            return Conflict(cardinalityFailure);
        }
        if (active != null)
        {
            if (!TryValidateActiveOwnerState(
                    order,
                    active,
                    out string activeFailure))
            {
                return Conflict(activeFailure);
            }
            if (!TryMergeTarget(
                    active,
                    targetState,
                    targetStatusCode,
                    targetStatusParameters,
                    out string mergeFailure))
            {
                return Conflict(mergeFailure);
            }
            return Advance(order, active);
        }

        if (string.IsNullOrEmpty(order.treatmentMaterialDestinationId))
        {
            string closedFacilityId = order.treatmentFacilityId;
            ClearCurrentAuthority(order);
            ApplyTarget(order, targetState, targetStatusCode,
                targetStatusParameters);
            return Closed(closedFacilityId);
        }
        if (!destinations.TryValidate(order, out string validationFailure))
        {
            return Conflict(
                "character-medical-supply-drain-authority-invalid:"
                + validationFailure);
        }
        if (!TryPreflightNewJoin(
                order,
                targetState,
                targetStatusCode,
                targetStatusParameters,
                out string preflightFailure))
        {
            return Conflict(preflightFailure);
        }

        FacilityBufferDestinationClaim[] matchingClaims = claims.CaptureClaims()
            .Where(value => value != null
                && string.Equals(
                    value.DestinationId,
                    order.treatmentMaterialDestinationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matchingClaims.Length != 1
            || !ClaimMatches(order, matchingClaims[0]))
        {
            return Conflict(
                "character-medical-supply-drain-claim-cardinality-or-identity:"
                + order.orderId);
        }

        FacilityBufferDestinationCustodyDrainDescriptor descriptor = new(
            CharacterMedicalSupplyDestinationAuthority.FormatParentOperationId(
                order.orderId,
                order.treatmentDestinationSequence),
            CharacterMedicalSupplyDestinationAuthority.FormatStepOperationId(
                order.orderId,
                order.treatmentDestinationSequence),
            CharacterMedicalSupplyDestinationAuthority.FormatOwnerStableId(
                order.orderId),
            order.orderId,
            order.treatmentFacilityId,
            order.treatmentMaterialDestinationId,
            matchingClaims[0].DropPosition,
            order.treatmentCapacityFingerprint);
        FacilityBufferDestinationCustodyDrainResult prepared =
            drains.TryPrepare(descriptor);
        if (!prepared.Succeeded || prepared.Snapshot == null)
        {
            return FromChildFailure(prepared);
        }

        active = PersistPreparedJoin(
            order,
            targetState,
            targetStatusCode,
            targetStatusParameters,
            prepared.Snapshot);
        return Advance(order, active);
    }

    [GameplayInternalOnly(
        "Resumes the one active Character Medical supply destination drain before ordinary medical AI ticks.",
        "CharacterMedicalRuntime lifecycle and restore recovery only")]
    public CharacterMedicalSupplyDestinationDrainAdvanceResult TryResume(
        CharacterMedicalOrder order)
    {
        CharacterMedicalSupplyDestinationDrainJoinData active =
            FindActiveJoin(order, out string failureReason);
        if (!string.IsNullOrEmpty(failureReason))
        {
            return Conflict(failureReason);
        }
        return active == null
            ? Conflict("character-medical-supply-drain-active-join-missing:"
                       + (order?.orderId ?? string.Empty))
            : Advance(order, active);
    }

    private CharacterMedicalSupplyDestinationDrainAdvanceResult Advance(
        CharacterMedicalOrder order,
        CharacterMedicalSupplyDestinationDrainJoinData upper)
    {
        if (!TryValidateActiveOwnerState(
                order,
                upper,
                out string activeFailure))
        {
            return Conflict(activeFailure);
        }
        if (!drains.TryCapture(
                upper.stepOperationId,
                out FacilityBufferDestinationCustodyDrainSnapshot child))
        {
            return Conflict(
                "character-medical-supply-drain-child-missing:"
                + order.orderId);
        }

        HashSet<string> progress = new(StringComparer.Ordinal);
        while (true)
        {
            if (child.EffectCommitted
                && upper.phase ==
                    CharacterMedicalSupplyDestinationDrainPhase.Prepared)
            {
                upper.commitId = child.CommitId;
                upper.receiptFingerprint = child.ReceiptFingerprint;
                upper.phase = CharacterMedicalSupplyDestinationDrainPhase
                    .EffectCommittedAwaitingOwnerAck;
            }
            if (child.OwnerAcknowledged
                && upper.phase ==
                    CharacterMedicalSupplyDestinationDrainPhase
                        .EffectCommittedAwaitingOwnerAck)
            {
                upper.phase = CharacterMedicalSupplyDestinationDrainPhase
                    .OwnerAcknowledgedAwaitingClosure;
            }
            if (!CharacterMedicalSupplyDestinationDrainJoin.TryValidate(
                    order,
                    upper,
                    child,
                    out string joinFailure))
            {
                return Conflict(joinFailure);
            }

            if (child.Phase < FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck)
            {
                string cursor = CreateProgressCursor(child);
                if (!progress.Add(cursor))
                {
                    return Conflict(
                        "character-medical-supply-drain-child-made-no-progress:"
                        + order.orderId);
                }
                FacilityBufferDestinationCustodyDrainResult advanced =
                    drains.TryAdvance(
                        upper.stepOperationId,
                        upper.requestFingerprint);
                if (!advanced.Succeeded || advanced.Snapshot == null)
                {
                    return FromChildFailure(advanced);
                }
                child = advanced.Snapshot;
                continue;
            }

            if (child.Phase == FacilityBufferDestinationCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck)
            {
                FacilityBufferDestinationCustodyDrainResult acknowledged =
                    drains.TryAcknowledge(
                        upper.stepOperationId,
                        upper.receiptFingerprint);
                if (!acknowledged.Succeeded || acknowledged.Snapshot == null)
                {
                    return FromChildFailure(acknowledged);
                }
                child = acknowledged.Snapshot;
                upper.phase = CharacterMedicalSupplyDestinationDrainPhase
                    .OwnerAcknowledgedAwaitingClosure;
            }

            if (!CharacterMedicalSupplyDestinationDrainJoin.TryValidate(
                    order,
                    upper,
                    child,
                    out joinFailure))
            {
                return Conflict(joinFailure);
            }
            if (!child.OwnerAcknowledged)
            {
                return Deferred(
                    "character-medical-supply-drain-child-not-acknowledged:"
                    + order.orderId);
            }
            if ((CharacterMedicalSupplyCommitPhase)
                    order.treatmentSupplyCommitPhase
                != CharacterMedicalSupplyCommitPhase.None)
            {
                return Deferred(
                    "character-medical-supply-drain-sink-recovery-pending:"
                    + order.orderId);
            }
            if (!destinations.TryRevoke(order, out string revokeFailure))
            {
                return Deferred(
                    "character-medical-supply-drain-authority-revoke-deferred:"
                    + revokeFailure);
            }

            string closedFacilityId = upper.ownerFacilityId;
            ClearCurrentAuthority(order);
            upper.phase = CharacterMedicalSupplyDestinationDrainPhase
                .ClosedAwaitingCheckpointGc;
            ApplyTarget(
                order,
                upper.targetState,
                upper.targetStatusCode,
                upper.targetStatusParameters);
            return Closed(closedFacilityId);
        }
    }

    private static CharacterMedicalSupplyDestinationDrainJoinData
        PersistPreparedJoin(
            CharacterMedicalOrder order,
            CharacterMedicalOrderState targetState,
            CharacterMedicalStatusCode targetStatusCode,
            IReadOnlyList<string> targetStatusParameters,
            FacilityBufferDestinationCustodyDrainSnapshot child)
    {
        CharacterMedicalSupplyDestinationDrainJoinData join = new()
        {
            destinationSequence = order.treatmentDestinationSequence,
            phase = CharacterMedicalSupplyDestinationDrainPhase.Prepared,
            targetState = targetState,
            targetStatusCode = targetStatusCode,
            targetStatusParameters = (targetStatusParameters
                    ?? Array.Empty<string>())
                .Select(value => value ?? string.Empty)
                .ToList(),
            parentOperationId = child.ParentOperationId,
            stepOperationId = child.StepOperationId,
            ownerFacilityId = order.treatmentFacilityId,
            sourceDestinationId = order.treatmentMaterialDestinationId,
            sourceBufferCapacityGrams = order.treatmentBufferCapacityGrams,
            sourceMassAuthorityRevision = order.treatmentMassAuthorityRevision,
            sourceCapacityFingerprint = order.treatmentCapacityFingerprint,
            requestFingerprint = child.RequestFingerprint,
            inputQuantity = child.InputQuantity,
            inputMassGrams = child.InputMassGrams,
            ownerX = child.OwnerGridX,
            ownerY = child.OwnerGridY
        };
        order.treatmentDestinationDrainJoins.Add(join);
        order.treatmentDestinationDrainJoins.Sort(
            (left, right) => left.destinationSequence.CompareTo(
                right.destinationSequence));
        order.state = CharacterMedicalOrderState.MaterialDestinationDraining;
        order.SetStatus(CharacterMedicalStatusCode.MaterialDestinationDraining);
        return join;
    }

    private static CharacterMedicalSupplyDestinationDrainJoinData FindActiveJoin(
        CharacterMedicalOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order?.treatmentDestinationDrainJoins == null)
        {
            failureReason =
                "character-medical-supply-drain-join-collection-missing";
            return null;
        }
        CharacterMedicalSupplyDestinationDrainJoinData[] active = order
            .treatmentDestinationDrainJoins
            .Where(value => value != null
                && value.phase != CharacterMedicalSupplyDestinationDrainPhase
                    .ClosedAwaitingCheckpointGc)
            .ToArray();
        if (active.Length > 1)
        {
            failureReason =
                "character-medical-supply-drain-active-cardinality:"
                + order.orderId;
            return null;
        }
        return active.SingleOrDefault();
    }

    private static bool TryMergeTarget(
        CharacterMedicalSupplyDestinationDrainJoinData active,
        CharacterMedicalOrderState requestedState,
        CharacterMedicalStatusCode requestedStatus,
        IReadOnlyList<string> requestedParameters,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (active.targetState == requestedState)
        {
            active.targetState = requestedState;
            active.targetStatusCode = requestedStatus;
            active.targetStatusParameters = (requestedParameters
                    ?? Array.Empty<string>())
                .Select(value => value ?? string.Empty)
                .ToList();
            return true;
        }
        bool currentTerminal = IsTerminal(active.targetState);
        bool requestedTerminal = IsTerminal(requestedState);
        if (currentTerminal && !requestedTerminal)
        {
            active.targetState = requestedState;
            active.targetStatusCode = requestedStatus;
            active.targetStatusParameters = (requestedParameters
                    ?? Array.Empty<string>())
                .Select(value => value ?? string.Empty)
                .ToList();
            return true;
        }
        if (!currentTerminal && requestedTerminal)
        {
            active.targetState = requestedState;
            active.targetStatusCode = requestedStatus;
            active.targetStatusParameters = (requestedParameters
                    ?? Array.Empty<string>())
                .Select(value => value ?? string.Empty)
                .ToList();
            return true;
        }
        if (currentTerminal && requestedTerminal)
        {
            if (requestedState == CharacterMedicalOrderState.Cancelled)
            {
                active.targetState = requestedState;
                active.targetStatusCode = requestedStatus;
                active.targetStatusParameters = (requestedParameters
                        ?? Array.Empty<string>())
                    .Select(value => value ?? string.Empty)
                    .ToList();
            }
            return true;
        }

        failureReason =
            "character-medical-supply-drain-target-conflict:"
            + active.destinationSequence.ToString(CultureInfo.InvariantCulture);
        return false;
    }

    private static bool TryPreflightNewJoin(
        CharacterMedicalOrder order,
        CharacterMedicalOrderState targetState,
        CharacterMedicalStatusCode targetStatusCode,
        IReadOnlyList<string> targetStatusParameters,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order?.treatmentDestinationDrainJoins == null
            || order.treatmentDestinationDrainJoins.Count >=
                CharacterMedicalSupplyDestinationDrainValidation
                    .MaximumJoinsPerOrder
            || order.treatmentDestinationDrainJoins.Any(value => value == null
                || value.phase !=
                    CharacterMedicalSupplyDestinationDrainPhase
                        .ClosedAwaitingCheckpointGc
                || value.destinationSequence ==
                    order.treatmentDestinationSequence)
            || !IsAllowedTarget(targetState)
            || targetStatusCode is CharacterMedicalStatusCode.Unknown
                or CharacterMedicalStatusCode.MaterialDestinationDraining
            || targetStatusParameters == null
            || targetStatusParameters.Count > 4
            || targetStatusParameters.Any(value => value == null
                || value.Length > 128
                || !string.Equals(value, value.Trim(),
                    StringComparison.Ordinal)))
        {
            failureReason =
                "character-medical-supply-drain-new-join-preflight-failed:"
                + (order?.orderId ?? string.Empty);
            return false;
        }
        return true;
    }

    private static bool TryValidateActiveOwnerState(
        CharacterMedicalOrder order,
        CharacterMedicalSupplyDestinationDrainJoinData upper,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || upper == null
            || order.state !=
                CharacterMedicalOrderState.MaterialDestinationDraining
            || order.treatmentDestinationSequence !=
                upper.destinationSequence
            || !string.Equals(order.treatmentFacilityId,
                upper.ownerFacilityId, StringComparison.Ordinal)
            || !string.Equals(order.treatmentMaterialDestinationId,
                upper.sourceDestinationId, StringComparison.Ordinal)
            || order.treatmentBufferCapacityGrams !=
                upper.sourceBufferCapacityGrams
            || order.treatmentMassAuthorityRevision !=
                upper.sourceMassAuthorityRevision
            || !string.Equals(order.treatmentCapacityFingerprint,
                upper.sourceCapacityFingerprint, StringComparison.Ordinal))
        {
            failureReason =
                "character-medical-supply-drain-active-owner-drift:"
                + (order?.orderId ?? string.Empty);
            return false;
        }
        return true;
    }

    private static bool ClaimMatches(
        CharacterMedicalOrder order,
        FacilityBufferDestinationClaim claim) =>
        claim != null
        && string.Equals(
            claim.OwnerDomain,
            CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerOperationId,
            CharacterMedicalSupplyDestinationAuthority.FormatOwnerOperationId(
                order.orderId,
                order.treatmentDestinationSequence),
            StringComparison.Ordinal)
        && string.Equals(
            claim.OwnerFacilityId,
            order.treatmentFacilityId,
            StringComparison.Ordinal)
        && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
        && claim.AdmissionPolicy ==
            FacilityBufferDestinationAdmissionPolicy.ExactGramRequired;

    private static void ClearCurrentAuthority(CharacterMedicalOrder order)
    {
        order.treatmentFacilityId = string.Empty;
        order.treatmentMaterialDestinationId = string.Empty;
        order.treatmentDestinationSequence = 0;
        order.treatmentBufferCapacityGrams = 0L;
        order.treatmentMassAuthorityRevision = 0L;
        order.treatmentCapacityFingerprint = string.Empty;
        order.treatmentSupply = CharacterMedicalSupplyKind.None;
        order.treatmentSupplyConsumed = false;
        order.treatmentSupplyDeliveryRequested = false;
        order.treatmentItemId = string.Empty;
        order.treatmentPotency = 1f;
        order.treatmentInfectionReduction = 0f;
        order.treatmentPainReduction = 0f;
    }

    private static void ApplyTarget(
        CharacterMedicalOrder order,
        CharacterMedicalOrderState state,
        CharacterMedicalStatusCode status,
        IReadOnlyList<string> parameters)
    {
        order.state = state;
        order.SetStatus(
            status,
            (parameters ?? Array.Empty<string>()).ToArray());
    }

    private static bool IsAllowedTarget(CharacterMedicalOrderState value) =>
        value is CharacterMedicalOrderState.AwaitingStabilization
            or CharacterMedicalOrderState.AwaitingRescue
            or CharacterMedicalOrderState.AwaitingBed
            or CharacterMedicalOrderState.Completed
            or CharacterMedicalOrderState.Cancelled;

    private static bool IsTerminal(CharacterMedicalOrderState value) =>
        value is CharacterMedicalOrderState.Completed
            or CharacterMedicalOrderState.Cancelled;

    private static string CreateProgressCursor(
        FacilityBufferDestinationCustodyDrainSnapshot value) =>
        ((int)value.Phase).ToString(CultureInfo.InvariantCulture)
        + ":" + value.CompletedActorCount.ToString(CultureInfo.InvariantCulture)
        + ":" + value.ReleasedOperationCount.ToString(
            CultureInfo.InvariantCulture);

    private static CharacterMedicalSupplyDestinationDrainAdvanceResult
        FromChildFailure(FacilityBufferDestinationCustodyDrainResult child) =>
        child.Status == FacilityBufferDestinationCustodyDrainStatus.Deferred
            ? Deferred(child.FailureReason)
            : Conflict(child.FailureReason);

    private static CharacterMedicalSupplyDestinationDrainAdvanceResult Closed(
        string facilityId) => new(
        CharacterMedicalSupplyDestinationDrainAdvanceStatus.Closed,
        facilityId,
        string.Empty);

    private static CharacterMedicalSupplyDestinationDrainAdvanceResult Deferred(
        string failureReason) => new(
        CharacterMedicalSupplyDestinationDrainAdvanceStatus.Deferred,
        string.Empty,
        failureReason);

    private static CharacterMedicalSupplyDestinationDrainAdvanceResult Conflict(
        string failureReason) => new(
        CharacterMedicalSupplyDestinationDrainAdvanceStatus.Conflict,
        string.Empty,
        failureReason);
}

internal static class CharacterMedicalSupplyDestinationDrainJoin
{
    internal static bool TryValidate(
        CharacterMedicalOrder order,
        CharacterMedicalSupplyDestinationDrainJoinData upper,
        FacilityBufferDestinationCustodyDrainSnapshot child,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || !CharacterMedicalSupplyDestinationDrainValidation.TryValidateJoin(
                order,
                upper,
                out failureReason)
            || child == null)
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "character-medical-supply-drain-join-missing"
                : failureReason;
            return false;
        }

        if (!string.Equals(child.ParentOperationId, upper.parentOperationId,
                StringComparison.Ordinal)
            || !string.Equals(child.StepOperationId, upper.stepOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerStableId,
                CharacterMedicalSupplyDestinationAuthority.FormatOwnerStableId(
                    order.orderId),
                StringComparison.Ordinal)
            || !string.Equals(child.OwnerSubjectId, order.orderId,
                StringComparison.Ordinal)
            || !string.Equals(child.OwnerFacilityId, upper.ownerFacilityId,
                StringComparison.Ordinal)
            || !string.Equals(child.SourceDestinationId,
                upper.sourceDestinationId, StringComparison.Ordinal)
            || !string.Equals(child.SourceAuthorityFingerprint,
                upper.sourceCapacityFingerprint, StringComparison.Ordinal)
            || !string.Equals(child.RequestFingerprint,
                upper.requestFingerprint, StringComparison.Ordinal)
            || child.OwnerGridX != upper.ownerX
            || child.OwnerGridY != upper.ownerY
            || child.InputQuantity != upper.inputQuantity
            || child.InputMassGrams != upper.inputMassGrams
            || child.ReleasedQuantity > child.InputQuantity
            || child.ReleasedMassGrams > child.InputMassGrams
            || child.CompletedActorCount < 0
            || child.CompletedActorCount > child.SourceActorCount
            || child.ReleasedOperationCount < 0
            || child.ReleasedOperationCount > child.SourceOperationCount)
        {
            failureReason =
                "character-medical-supply-drain-child-join-mismatch:"
                + order.orderId + ":" + upper.destinationSequence;
            return false;
        }

        bool upperEffectCommitted = upper.phase is
            CharacterMedicalSupplyDestinationDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or CharacterMedicalSupplyDestinationDrainPhase
                .OwnerAcknowledgedAwaitingClosure
            or CharacterMedicalSupplyDestinationDrainPhase
                .ClosedAwaitingCheckpointGc;
        if (child.EffectCommitted != upperEffectCommitted
            || upperEffectCommitted
            && (!string.Equals(child.CommitId, upper.commitId,
                    StringComparison.Ordinal)
                || !string.Equals(child.ReceiptFingerprint,
                    upper.receiptFingerprint, StringComparison.Ordinal)
                || child.ReleasedQuantity != child.InputQuantity
                || child.ReleasedMassGrams != child.InputMassGrams))
        {
            failureReason =
                "character-medical-supply-drain-effect-join-mismatch:"
                + order.orderId + ":" + upper.destinationSequence;
            return false;
        }

        bool upperAcknowledged = upper.phase is
            CharacterMedicalSupplyDestinationDrainPhase
                .OwnerAcknowledgedAwaitingClosure
            or CharacterMedicalSupplyDestinationDrainPhase
                .ClosedAwaitingCheckpointGc;
        if (child.OwnerAcknowledged != upperAcknowledged)
        {
            failureReason =
                "character-medical-supply-drain-ack-join-mismatch:"
                + order.orderId + ":" + upper.destinationSequence;
            return false;
        }
        return true;
    }
}

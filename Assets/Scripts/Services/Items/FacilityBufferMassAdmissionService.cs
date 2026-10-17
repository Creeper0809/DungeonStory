using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Reads exact physical occupancy and lot mass from the same repository state
/// used by delivery retargeting. Callers cannot author occupancy totals.
/// </summary>
public sealed class FacilityBufferPhysicalOccupancyQuery :
    IFacilityBufferCustodyOwnedPhysicalOccupancyQuery
{
    private readonly WorldItemRepository repository;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IRestoreHaulDeliveryIntentCandidateQuery restoreHaulIntents;

    public FacilityBufferPhysicalOccupancyQuery(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IItemQuantityReservationService quantityReservations,
        DungeonRuntimeAggregateRootStore aggregateRootStore = null,
        IRestoreHaulDeliveryIntentCandidateQuery restoreHaulIntents = null)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.quantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
        this.aggregateRootStore = aggregateRootStore;
        this.restoreHaulIntents = restoreHaulIntents;
    }

    public long MassAuthorityRevision => massQuery.AuthorityRevision;

    public FacilityBufferPhysicalOccupancySnapshot Capture(string destinationId)
    {
        string destination = destinationId ?? string.Empty;
        if (!IsCanonicalRequired(destination))
            throw new ArgumentException("A canonical destination is required.");

        long nonCarried = 0L;
        foreach (WorldItemStackRecord record in repository.Records
                     .Where(record => record != null
                         && record.quantity > 0
                         && record.state != WorldItemStackState.Carried
                         && string.Equals(
                             record.destinationId,
                             destination,
                             StringComparison.Ordinal))
                     .OrderBy(record => record.stackId, StringComparer.Ordinal))
        {
            nonCarried = checked(nonCarried + GetMass(record, record.quantity));
        }

        long carried = 0L;
        foreach (HaulDeliveryIntentSaveData intent in HaulDeliveryIntentAuthorityView
                     .Capture(
                         aggregateRootStore,
                         restoreHaulIntents,
                         repository.HaulDeliveryIntents)
                     .Where(intent => intent != null
                         && string.Equals(
                             intent.destinationId,
                             destination,
                             StringComparison.Ordinal))
                     .OrderBy(intent => intent.operationId, StringComparer.Ordinal))
        {
            foreach (HaulDeliveryItemCommitmentSaveData commitment in
                     (intent.commitments
                         ?? new List<HaulDeliveryItemCommitmentSaveData>())
                     .Where(value => value != null)
                     .OrderBy(value => value.carriedStackId, StringComparer.Ordinal))
            {
                if (!repository.RecordsById.TryGetValue(
                        commitment.carriedStackId,
                        out WorldItemStackRecord record)
                    || record == null)
                {
                    // A successful facility deposit may aggregate the carried
                    // identity before AbilityHaul retires its intent later in
                    // the same synchronous action. The physical destination
                    // total above is already authoritative in that window.
                    continue;
                }
                if (record.state != WorldItemStackState.Carried)
                {
                    if (record.state == WorldItemStackState.FacilityBuffer
                        && string.Equals(
                            record.destinationId,
                            destination,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Committed carried lot '{commitment.carriedStackId}' has invalid state for facility-buffer '{destination}'.");
                }
                if (record.quantity != commitment.quantity
                    || !string.Equals(
                        record.destinationId,
                        intent.ownerCharacterId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        record.itemId,
                        commitment.itemId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Committed carried lot '{commitment.carriedStackId}' is inconsistent with facility-buffer '{destination}'.");
                }
                carried = checked(carried + GetMass(record, commitment.quantity));
            }
        }

        return new FacilityBufferPhysicalOccupancySnapshot(
            nonCarried,
            carried);
    }

    public bool TryCaptureExactLot(
        IReadOnlyList<FacilityBufferMassLotSlice> slices,
        out FacilityBufferExactLotSnapshot lot,
        out string failureReason)
    {
        lot = default;
        failureReason = string.Empty;
        FacilityBufferMassLotSlice[] requested =
            (slices ?? Array.Empty<FacilityBufferMassLotSlice>()).ToArray();
        if (requested.Length == 0
            || requested.Any(slice => !IsCanonicalRequired(slice.StackId)
                || slice.Quantity <= 0
                || slice.ExpectedReservationRevision < 0L)
            || requested.Select(slice => slice.StackId)
                .Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            failureReason = "facility-buffer-exact-lot-invalid";
            return false;
        }

        long massGrams = 0L;
        List<string> fingerprintParts = new(requested.Length);
        foreach (FacilityBufferMassLotSlice slice in requested
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            if (!repository.RecordsById.TryGetValue(
                    slice.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.reservationRevision != slice.ExpectedReservationRevision
                || slice.Quantity > quantityReservations.GetAvailableQuantity(
                    (ItemStackId)slice.StackId))
            {
                failureReason =
                    "facility-buffer-exact-lot-changed:" + slice.StackId;
                return false;
            }
            long sliceMass = GetMass(record, slice.Quantity);
            massGrams = checked(massGrams + sliceMass);
            fingerprintParts.Add(
                slice.StackId + ":"
                + slice.ExpectedReservationRevision.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                + ":"
                + slice.Quantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                + ":"
                + sliceMass.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        if (massGrams <= 0L)
        {
            failureReason = "facility-buffer-exact-lot-mass-invalid";
            return false;
        }

        lot = new FacilityBufferExactLotSnapshot(
            string.Join("|", fingerprintParts),
            new PhysicalMassGrams(massGrams));
        return true;
    }

    public bool TryCaptureCustodyOwnedExactLot(
        IReadOnlyList<FacilityBufferMassLotSlice> slices,
        string expectedDestinationId,
        string expectedRouteOperationId,
        string expectedPhysicalReceiptFingerprint,
        long expectedMassAuthorityRevision,
        out FacilityBufferExactLotSnapshot lot,
        out string failureReason)
    {
        lot = default;
        failureReason = string.Empty;
        FacilityBufferMassLotSlice[] requested =
            (slices ?? Array.Empty<FacilityBufferMassLotSlice>()).ToArray();
        if (!IsCanonicalRequired(expectedDestinationId)
            || !IsCanonicalRequired(expectedRouteOperationId)
            || !IsCanonicalRequired(expectedPhysicalReceiptFingerprint)
            || expectedMassAuthorityRevision <= 0L
            || expectedMassAuthorityRevision != massQuery.AuthorityRevision
            || requested.Length == 0
            || requested.Any(slice => !IsCanonicalRequired(slice.StackId)
                || slice.Quantity <= 0
                || slice.ExpectedReservationRevision < 0L
                || !IsCanonicalRequired(
                    slice.ExpectedCustodyComponentFingerprint)
                || slice.ExpectedCustodyMassGrams <= 0L)
            || requested.Select(slice => slice.StackId)
                .Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            failureReason = "facility-buffer-custody-lot-invalid-or-stale";
            return false;
        }

        long totalMass = 0L;
        List<string> fingerprintParts = new(requested.Length);
        foreach (FacilityBufferMassLotSlice slice in requested
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            if (!repository.RecordsById.TryGetValue(
                    slice.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.state != WorldItemStackState.Loose
                || record.quantity != slice.Quantity
                || record.reservationRevision != slice.ExpectedReservationRevision
                || !FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                || custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
                || custody.Quantity != slice.Quantity
                || !string.Equals(
                    custody.CurrentSourceStackId,
                    record.stackId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    custody.RouteOperationId,
                    expectedRouteOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    custody.PhysicalReceiptFingerprint,
                    expectedPhysicalReceiptFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    custody.ComponentFingerprint,
                    slice.ExpectedCustodyComponentFingerprint,
                    StringComparison.Ordinal)
                || slice.ExpectedCustodyMassGrams <= 0L
                || custody.MassGrams != slice.ExpectedCustodyMassGrams)
            {
                failureReason =
                    "facility-buffer-custody-lot-changed:" + slice.StackId;
                return false;
            }

            long exactMass;
            try
            {
                exactMass = GetMass(record, slice.Quantity);
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                failureReason = "facility-buffer-custody-mass-invalid:"
                    + exception.Message;
                return false;
            }
            if (exactMass <= 0L || exactMass != custody.MassGrams)
            {
                failureReason =
                    "facility-buffer-custody-mass-changed:" + slice.StackId;
                return false;
            }
            totalMass = checked(totalMass + exactMass);
            fingerprintParts.Add(
                slice.StackId + ":"
                + slice.ExpectedReservationRevision.ToString(
                    CultureInfo.InvariantCulture) + ":"
                + custody.SourceOffsetQuantity.ToString(
                    CultureInfo.InvariantCulture) + ":"
                + slice.Quantity.ToString(CultureInfo.InvariantCulture) + ":"
                + exactMass.ToString(CultureInfo.InvariantCulture) + ":"
                + custody.PhysicalReceiptFingerprint);
        }
        if (massQuery.AuthorityRevision != expectedMassAuthorityRevision
            || totalMass <= 0L)
        {
            failureReason = "facility-buffer-custody-mass-authority-stale";
            return false;
        }

        lot = new FacilityBufferExactLotSnapshot(
            string.Join("|", fingerprintParts),
            new PhysicalMassGrams(totalMass));
        return true;
    }

    private long GetMass(WorldItemStackRecord record, int quantity)
    {
        ItemDefinitionId itemId = (ItemDefinitionId)record.itemId;
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            itemId,
            record.itemInstanceId,
            record.components);
        return massQuery.GetQuantityMass(itemId, subject, quantity).Value;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>
/// Transient synchronous admission authority for existing-lot retargeting and
/// not-yet-physical output publication. Both token kinds spend the same destination
/// ledger. Once physical publication succeeds, repository occupancy replaces the
/// reservation, so neither token payload is save authority.
/// </summary>
public sealed class FacilityBufferMassAdmissionService :
    IFacilityBufferMassAdmissionService,
    IReversibleFacilityBufferPlannedOutputAdmissionService,
    IFacilityBufferPlannedOutputProjectionQuery,
    IFacilityBufferMassCapacityAuthorityQuery,
    IDungeonPreStageRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "221.world.facility-buffer-mass-capacity";
    private const int MaximumTerminalTokenCount = 2_048;
    private sealed class TokenState
    {
        internal FacilityBufferMassAdmissionToken Token;
        internal FacilityBufferMassAdmissionTokenStatus Status;
        internal FacilityBufferMassAdmissionReleaseReason ReleaseReason;
        internal FacilityBufferMassAdmissionReceipt Receipt;
    }

    private sealed class PlannedOutputTokenState
    {
        internal FacilityBufferPlannedOutputToken Token;
        internal FacilityBufferMassAdmissionTokenStatus Status;
        internal FacilityBufferMassAdmissionReleaseReason ReleaseReason;
        internal FacilityBufferPlannedOutputPublicationReceipt Publication;
        internal FacilityBufferPlannedOutputReceipt Receipt;
    }

    private readonly IFacilityBufferDestinationClaimAuthorityQuery destinationClaims;
    private readonly IFacilityBufferPhysicalOccupancyQuery physicalOccupancy;
    private readonly IPhysicalItemMassQuery plannedOutputMassQuery;
    private readonly IFacilityBufferDestinationAdmissionFenceQuery
        admissionFences;
    private Dictionary<string, FacilityBufferCapacityProfile> profiles =
        CreateProfileMap();
    private Dictionary<string, FacilityBufferCapacityProfile> candidateProfiles;
    private Dictionary<string, FacilityBufferCapacityProfile> previousProfiles;
    private readonly Dictionary<string, TokenState> tokens =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlannedOutputTokenState> plannedOutputTokens =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> reservedByDestination =
        new(StringComparer.Ordinal);
    private readonly Queue<string> terminalTokenOrder = new();
    private readonly Queue<string> plannedTerminalTokenOrder = new();
    private long revision;
    private long nextTokenSequence = 1L;
    private long preparedPublishRevision;
    private long preparedRollbackRevision;
    private bool restoreActive;
    private bool restorePublished;

    public FacilityBufferMassAdmissionService(
        IFacilityBufferDestinationClaimAuthorityQuery destinationClaims,
        IFacilityBufferPhysicalOccupancyQuery physicalOccupancy,
        IPhysicalItemMassQuery plannedOutputMassQuery = null,
        IProductionFacilityDestructiveDrainOpenOperationQuery
            openDestructiveDrains = null)
        : this(
            destinationClaims,
            physicalOccupancy,
            plannedOutputMassQuery,
            openDestructiveDrains == null
                ? null
                : new FacilityBufferDestinationAdmissionFenceQuery(new[]
                {
                    new ProductionFacilityDestructiveDrainAdmissionFenceSource(
                        openDestructiveDrains)
                }))
    {
    }

    [VContainer.Inject]
    public FacilityBufferMassAdmissionService(
        IFacilityBufferDestinationClaimAuthorityQuery destinationClaims,
        IFacilityBufferPhysicalOccupancyQuery physicalOccupancy,
        IPhysicalItemMassQuery plannedOutputMassQuery,
        IFacilityBufferDestinationAdmissionFenceQuery admissionFences)
    {
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        this.physicalOccupancy = physicalOccupancy
            ?? throw new ArgumentNullException(nameof(physicalOccupancy));
        this.plannedOutputMassQuery = plannedOutputMassQuery;
        this.admissionFences = admissionFences;
    }

    public string ParticipantId => RestoreParticipantId;
    public long Revision => revision;

    public IReadOnlyList<FacilityBufferCapacityProfile> CaptureProfiles() =>
        profiles.Values
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<FacilityBufferCapacityProfile>
        CaptureAuthorityProfiles() => GetAuthorityView().Values
        .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
        .ToArray();

    public bool TryValidateExactDestinationClaim(
        string destinationId,
        Vector2Int dropPosition,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!destinationClaims.TryGetAuthorityClaim(
                destinationId,
                dropPosition,
                out FacilityBufferDestinationClaim claim)
            || claim == null)
        {
            failureReason =
                "facility-buffer-exact-destination-claim-missing";
            return false;
        }
        return true;
    }

    public bool TryGetCapacity(
        string destinationId,
        Vector2Int dropPosition,
        out FacilityBufferMassCapacitySnapshot snapshot)
    {
        snapshot = default;
        if (!profiles.TryGetValue(
                destinationId ?? string.Empty,
                out FacilityBufferCapacityProfile profile)
            || profile.DropPosition != dropPosition)
        {
            return false;
        }

        snapshot = new FacilityBufferMassCapacitySnapshot(
            profile,
            reservedByDestination.GetValueOrDefault(profile.DestinationId, 0L),
            physicalOccupancy is IFacilityBufferCustodyOwnedPhysicalOccupancyQuery
                custodyOccupancy
                ? custodyOccupancy.MassAuthorityRevision
                : plannedOutputMassQuery?.AuthorityRevision ?? 0L);
        return true;
    }

    public bool TryGetCapacityAuthorityFingerprint(
        string destinationId,
        Vector2Int dropPosition,
        out string fingerprint)
    {
        fingerprint = string.Empty;
        if (!TryGetCapacity(destinationId, dropPosition, out var snapshot)
            || snapshot.Profile == null)
        {
            return false;
        }
        FacilityBufferCapacityProfile profile = snapshot.Profile;
        string payload = string.Join("|",
            "facility-buffer-capacity-v1",
            profile.DestinationId,
            profile.DropPosition.x.ToString(CultureInfo.InvariantCulture),
            profile.DropPosition.y.ToString(CultureInfo.InvariantCulture),
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.MaxMassGrams.ToString(CultureInfo.InvariantCulture),
            profile.CapacityRevision.ToString(CultureInfo.InvariantCulture),
            revision.ToString(CultureInfo.InvariantCulture),
            snapshot.MassAuthorityRevision.ToString(
                CultureInfo.InvariantCulture));
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        fingerprint = hex.ToString();
        return true;
    }

    public bool TryGetReceipt(
        string tokenId,
        out FacilityBufferMassAdmissionReceipt receipt)
    {
        receipt = default;
        return tokens.TryGetValue(tokenId ?? string.Empty, out TokenState state)
            && state.Status == FacilityBufferMassAdmissionTokenStatus.Routed
            && (receipt = state.Receipt).CommittedMassGrams > 0L;
    }

    public bool TryGetPlannedOutputReceipt(
        string tokenId,
        out FacilityBufferPlannedOutputReceipt receipt)
    {
        receipt = default;
        return plannedOutputTokens.TryGetValue(
                tokenId ?? string.Empty,
                out PlannedOutputTokenState state)
            && state.Status == FacilityBufferMassAdmissionTokenStatus.Routed
            && (receipt = state.Receipt).CommittedMassGrams > 0L;
    }

    public bool TryGetPlannedOutputToken(
        string tokenId,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionTokenStatus status)
    {
        token = default;
        status = FacilityBufferMassAdmissionTokenStatus.Released;
        if (!plannedOutputTokens.TryGetValue(
                tokenId ?? string.Empty,
                out PlannedOutputTokenState state))
        {
            return false;
        }
        token = state.Token;
        status = state.Status;
        return !string.IsNullOrEmpty(token.TokenId);
    }

    public bool TryValidatePlannedOutputReservation(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!TryValidatePlannedOutputPublicationToken(
                token,
                out bool publicationCommitted,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (!publicationCommitted)
            return true;
        return FacilityBufferMassAdmissionRules.Fail(
            FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
            $"Facility-buffer planned-output token '{token.TokenId}' is already committed.",
            out failureCode,
            out failureReason);
    }

    public bool TryValidatePlannedOutputPublicationToken(
        FacilityBufferPlannedOutputToken token,
        out bool publicationCommitted,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        publicationCommitted = false;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!plannedOutputTokens.TryGetValue(
                token.TokenId ?? string.Empty,
                out PlannedOutputTokenState state))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer planned-output token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Routed)
        {
            if (!FacilityBufferMassAdmissionRules.PlannedOutputTokenMatches(state.Token, token))
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                    $"Facility-buffer planned-output token '{token.TokenId}' replay mismatched.",
                    out failureCode,
                    out failureReason);
            }
            publicationCommitted = true;
            return true;
        }
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer planned-output token '{token.TokenId}' is not reserved.",
                out failureCode,
                out failureReason);
        }
        if (!FacilityBufferMassAdmissionRules.PlannedOutputTokenMatches(state.Token, token)
            || plannedOutputMassQuery == null
            || token.CapacityAuthorityRevision != revision
            || token.MassAuthorityRevision != plannedOutputMassQuery.AuthorityRevision
            || !profiles.TryGetValue(
                token.Request.DestinationId,
                out FacilityBufferCapacityProfile profile)
            || !FacilityBufferMassAdmissionRules.ProfileMatchesPlannedOutputRequest(profile, token.Request))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' is stale or mismatched.",
                out failureCode,
                out failureReason);
        }
        return TryValidateProfile(
            profile,
            profile.OwnerDomain,
            out failureCode,
            out failureReason);
    }

    public bool TryReplaceOwnedProfiles(
        string ownerDomain,
        IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!FacilityBufferMassAdmissionRules.IsCanonicalRequired(ownerDomain))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidProfile,
                "Facility-buffer capacity owner domain is invalid.",
                out failureCode,
                out failureReason);
        }
        if (!TryGetMutationTarget(
                out Dictionary<string, FacilityBufferCapacityProfile> target,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        FacilityBufferCapacityProfile[] desired =
            (desiredProfiles ?? Array.Empty<FacilityBufferCapacityProfile>())
            .ToArray();
        Dictionary<string, FacilityBufferCapacityProfile> replacements =
            new(StringComparer.Ordinal);
        foreach (FacilityBufferCapacityProfile profile in desired)
        {
            if (!TryValidateProfile(
                    profile,
                    ownerDomain,
                    out failureCode,
                    out failureReason))
            {
                return false;
            }
            if (!replacements.TryAdd(profile.DestinationId, profile))
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                    $"Duplicate facility-buffer capacity '{profile.DestinationId}'.",
                    out failureCode,
                    out failureReason);
            }
        }

        if (!restoreActive
            && FacilityBufferMassAdmissionRules.OwnedProfileSetMatches(target, ownerDomain, replacements))
        {
            return true;
        }

        string[] retiring = target.Values
            .Where(value => string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal))
            .Select(value => value.DestinationId)
            .Where(destinationId => !replacements.ContainsKey(destinationId))
            .ToArray();
        if (retiring.Any(destinationId =>
                reservedByDestination.GetValueOrDefault(destinationId, 0L) > 0L
                || physicalOccupancy.Capture(destinationId).TotalMassGrams > 0L))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                "A facility-buffer capacity with a live admission cannot be retired.",
                out failureCode,
                out failureReason);
        }

        Dictionary<string, FacilityBufferCapacityProfile> next =
            new(target, StringComparer.Ordinal);
        foreach (string destinationId in next.Values
                     .Where(value => string.Equals(
                         value.OwnerDomain,
                         ownerDomain,
                         StringComparison.Ordinal))
                     .Select(value => value.DestinationId)
                     .ToArray())
        {
            next.Remove(destinationId);
        }
        foreach (KeyValuePair<string, FacilityBufferCapacityProfile> pair in
                 replacements)
        {
            if (next.TryGetValue(pair.Key, out FacilityBufferCapacityProfile foreign)
                && !string.Equals(
                    foreign.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                    $"Facility-buffer capacity '{pair.Key}' belongs to another domain.",
                    out failureCode,
                    out failureReason);
            }
            long occupied = checked(
                physicalOccupancy.Capture(pair.Key).TotalMassGrams
                + reservedByDestination.GetValueOrDefault(pair.Key, 0L));
            if (target.TryGetValue(
                    pair.Key,
                    out FacilityBufferCapacityProfile existing)
                && !ProfileOwnershipMatches(existing, pair.Value)
                && occupied > 0L)
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                    $"Facility-buffer capacity '{pair.Key}' cannot change ownership while {occupied}g remains.",
                    out failureCode,
                    out failureReason);
            }
            if (occupied > pair.Value.MaxMassGrams)
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
                    $"Facility-buffer capacity '{pair.Key}' cannot shrink below {occupied}g.",
                    out failureCode,
                    out failureReason);
            }
            next[pair.Key] = pair.Value;
        }

        if (restoreActive)
            candidateProfiles = next;
        else
        {
            profiles = next;
            revision = checked(revision + 1L);
        }
        return true;
    }

    public bool TryReserveExactLot(
        FacilityBufferMassAdmissionRequest request,
        out FacilityBufferMassAdmissionToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
        => TryReserveExactLotCore(
            request,
            null,
            out token,
            out failureCode,
            out failureReason);

    public bool TryReserveCustodyOwnedExactLot(
        FacilityBufferCustodyOwnedAdmissionRequest request,
        out FacilityBufferMassAdmissionToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!FacilityBufferMassAdmissionRules.IsCanonicalRequired(request.ExpectedRouteOperationId)
            || !FacilityBufferMassAdmissionRules.IsCanonicalRequired(request.ExpectedPhysicalReceiptFingerprint)
            || request.ExpectedMassAuthorityRevision <= 0L
            || physicalOccupancy
                is not IFacilityBufferCustodyOwnedPhysicalOccupancyQuery)
        {
            token = default;
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer custody-owned admission request is invalid.",
                out failureCode,
                out failureReason);
        }
        return TryReserveExactLotCore(
            request.Admission,
            request,
            out token,
            out failureCode,
            out failureReason);
    }

    private bool TryReserveExactLotCore(
        FacilityBufferMassAdmissionRequest request,
        FacilityBufferCustodyOwnedAdmissionRequest? custodyOwned,
        out FacilityBufferMassAdmissionToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        token = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!FacilityBufferMassAdmissionRules.TryValidateRequest(request, out failureCode, out failureReason))
            return false;
        if (!profiles.TryGetValue(
                request.DestinationId,
                out FacilityBufferCapacityProfile profile))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileMissing,
                $"Facility-buffer capacity '{request.DestinationId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidateProfile(
                profile,
                profile.OwnerDomain,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (IsOwnerMutationFenceOpen(profile, out string mutationOperationId))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode
                    .OwnerMutationFenceOpen,
                "Facility-buffer owner is fenced by mutation operation '"
                + mutationOperationId + "'.",
                out failureCode,
                out failureReason);
        }
        if (!ProfileMatchesRequest(profile, request))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                $"Facility-buffer capacity '{request.DestinationId}' changed before admission.",
                out failureCode,
                out failureReason);
        }
        FacilityBufferExactLotSnapshot exactLot;
        string lotFailure;
        bool captured = custodyOwned.HasValue
            ? ((IFacilityBufferCustodyOwnedPhysicalOccupancyQuery)physicalOccupancy)
                .TryCaptureCustodyOwnedExactLot(
                    request.ExactLotSlices,
                    request.DestinationId,
                    custodyOwned.Value.ExpectedRouteOperationId,
                    custodyOwned.Value.ExpectedPhysicalReceiptFingerprint,
                    custodyOwned.Value.ExpectedMassAuthorityRevision,
                    out exactLot,
                    out lotFailure)
            : physicalOccupancy.TryCaptureExactLot(
                request.ExactLotSlices,
                out exactLot,
                out lotFailure);
        if (!captured)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                lotFailure,
                out failureCode,
                out failureReason);
        }
        if (HasOperationId(request.TransferOperationId))
        {
            TokenState existing = tokens.Values.SingleOrDefault(value =>
                string.Equals(
                    value.Token.Request.TransferOperationId,
                    request.TransferOperationId,
                    StringComparison.Ordinal));
            if (custodyOwned.HasValue
                && existing != null
                && existing.Status == FacilityBufferMassAdmissionTokenStatus.Reserved
                && FacilityBufferMassAdmissionRules.AdmissionRequestsMatch(existing.Token.Request, request)
                && string.Equals(
                    existing.Token.ExactLot.Fingerprint,
                    exactLot.Fingerprint,
                    StringComparison.Ordinal)
                && existing.Token.ReservedMassGrams == exactLot.Mass.Value)
            {
                token = existing.Token;
                return true;
            }
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer transfer '{request.TransferOperationId}' is duplicated or conflicts.",
                out failureCode,
                out failureReason);
        }
        FacilityBufferPhysicalOccupancySnapshot physical =
            physicalOccupancy.Capture(request.DestinationId);
        long alreadyReserved = reservedByDestination.GetValueOrDefault(
            request.DestinationId,
            0L);
        long occupied;
        try
        {
            occupied = checked(
                physical.NonCarriedMassGrams
                + physical.CommittedCarriedMassGrams
                + alreadyReserved);
        }
        catch (OverflowException)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer observed occupancy overflowed.",
                out failureCode,
                out failureReason);
        }
        if (occupied > profile.MaxMassGrams
            || exactLot.Mass.Value > profile.MaxMassGrams - occupied)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
                $"Facility-buffer '{request.DestinationId}' cannot admit "
                + $"{exactLot.Mass.Value}g at {occupied}/{profile.MaxMassGrams}g.",
                out failureCode,
                out failureReason);
        }

        string tokenId = $"facility-buffer-admission:{nextTokenSequence:D12}";
        nextTokenSequence = checked(nextTokenSequence + 1L);
        token = new FacilityBufferMassAdmissionToken(
            tokenId,
            request,
            exactLot,
            revision);
        tokens.Add(tokenId, new TokenState
        {
            Token = token,
            Status = FacilityBufferMassAdmissionTokenStatus.Reserved
        });
        reservedByDestination[request.DestinationId] = checked(
            alreadyReserved + exactLot.Mass.Value);
        return true;
    }

    public bool TryCommitRouted(
        FacilityBufferMassAdmissionToken token,
        string exactLotFingerprint,
        long routedMassGrams,
        out FacilityBufferMassAdmissionReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!tokens.TryGetValue(token.TokenId ?? string.Empty, out TokenState state))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer admission token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Routed)
        {
            receipt = state.Receipt;
            if (receipt.CommittedMassGrams == routedMassGrams
                && string.Equals(
                    receipt.ExactLotFingerprint,
                    exactLotFingerprint,
                    StringComparison.Ordinal))
            {
                return true;
            }
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer admission token '{token.TokenId}' replay conflicts.",
                out failureCode,
                out failureReason);
        }
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer admission token '{token.TokenId}' is not reserved.",
                out failureCode,
                out failureReason);
        }
        if (!TokenMatches(state.Token, token)
            || routedMassGrams != token.ReservedMassGrams
            || !string.Equals(
                token.ExactLot.Fingerprint,
                exactLotFingerprint,
                StringComparison.Ordinal))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer admission token '{token.TokenId}' commit mismatched.",
                out failureCode,
                out failureReason);
        }

        RemoveReservedMass(token.Request.DestinationId, token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Routed;
        state.Receipt = new FacilityBufferMassAdmissionReceipt(token);
        receipt = state.Receipt;
        TrackTerminalToken(token.TokenId);
        return true;
    }

    public bool TryRelease(
        FacilityBufferMassAdmissionToken token,
        FacilityBufferMassAdmissionReleaseReason reason,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!Enum.IsDefined(typeof(FacilityBufferMassAdmissionReleaseReason), reason)
            || !tokens.TryGetValue(token.TokenId ?? string.Empty, out TokenState state))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer admission token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Released)
            return state.ReleaseReason == reason;
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved
            || !TokenMatches(state.Token, token))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer admission token '{token.TokenId}' cannot be released.",
                out failureCode,
                out failureReason);
        }

        RemoveReservedMass(token.Request.DestinationId, token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Released;
        state.ReleaseReason = reason;
        TrackTerminalToken(token.TokenId);
        return true;
    }

    public bool TryRollbackRouted(
        FacilityBufferMassAdmissionToken token,
        FacilityBufferMassAdmissionReceipt expectedReceipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!tokens.TryGetValue(token.TokenId ?? string.Empty, out TokenState state)
            || state.Status != FacilityBufferMassAdmissionTokenStatus.Routed)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer routed token '{token.TokenId}' cannot roll back.",
                out failureCode,
                out failureReason);
        }
        if (!TokenMatches(state.Token, token)
            || expectedReceipt.TokenId != state.Receipt.TokenId
            || expectedReceipt.CommittedMassGrams
                != state.Receipt.CommittedMassGrams
            || !string.Equals(
                expectedReceipt.ExactLotFingerprint,
                state.Receipt.ExactLotFingerprint,
                StringComparison.Ordinal))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer routed token '{token.TokenId}' rollback mismatched.",
                out failureCode,
                out failureReason);
        }
        tokens.Remove(token.TokenId);
        return true;
    }

    public bool TryReservePlannedOutput(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        token = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (plannedOutputMassQuery == null)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer planned output requires the physical-mass authority.",
                out failureCode,
                out failureReason);
        }
        if (!FacilityBufferMassAdmissionRules.TryValidatePlannedOutputRequest(
                request,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (!profiles.TryGetValue(
                request.DestinationId,
                out FacilityBufferCapacityProfile profile))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileMissing,
                $"Facility-buffer capacity '{request.DestinationId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidateProfile(
                profile,
                profile.OwnerDomain,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (IsOwnerMutationFenceOpen(profile, out string mutationOperationId))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode
                    .OwnerMutationFenceOpen,
                "Facility-buffer owner is fenced by mutation operation '"
                + mutationOperationId + "'.",
                out failureCode,
                out failureReason);
        }
        if (!FacilityBufferMassAdmissionRules.ProfileMatchesPlannedOutputRequest(profile, request))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                $"Facility-buffer capacity '{request.DestinationId}' changed before planned output admission.",
                out failureCode,
                out failureReason);
        }
        long massAuthorityRevision = plannedOutputMassQuery.AuthorityRevision;
        if (!TryCapturePlannedOutput(
                request,
                massAuthorityRevision,
                out FacilityBufferPlannedOutputSnapshot planned,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        if (HasOperationId(request.PublicationOperationId)
            && !CanRetryRolledBackPlannedOutput(request.PublicationOperationId, planned.Fingerprint))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer operation '{request.PublicationOperationId}' is duplicated or conflicts with its rolled-back request.",
                out failureCode,
                out failureReason);
        }

        FacilityBufferPhysicalOccupancySnapshot physical =
            physicalOccupancy.Capture(request.DestinationId);
        long alreadyReserved = reservedByDestination.GetValueOrDefault(
            request.DestinationId,
            0L);
        long occupied;
        try
        {
            occupied = checked(
                physical.NonCarriedMassGrams
                + physical.CommittedCarriedMassGrams
                + alreadyReserved);
        }
        catch (OverflowException)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer observed occupancy overflowed.",
                out failureCode,
                out failureReason);
        }
        if (occupied > profile.MaxMassGrams
            || planned.TotalMassGrams > profile.MaxMassGrams - occupied)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
                $"Facility-buffer '{request.DestinationId}' cannot admit planned output "
                + $"{planned.TotalMassGrams}g at {occupied}/{profile.MaxMassGrams}g.",
                out failureCode,
                out failureReason);
        }

        string tokenId =
            $"facility-buffer-planned-output-admission:{nextTokenSequence:D12}";
        nextTokenSequence = checked(nextTokenSequence + 1L);
        token = new FacilityBufferPlannedOutputToken(
            tokenId,
            request,
            planned,
            revision,
            massAuthorityRevision);
        plannedOutputTokens.Add(tokenId, new PlannedOutputTokenState
        {
            Token = token,
            Status = FacilityBufferMassAdmissionTokenStatus.Reserved
        });
        reservedByDestination[request.DestinationId] = checked(
            alreadyReserved + planned.TotalMassGrams);
        return true;
    }

    public bool TryCommitPlannedOutput(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferPlannedOutputReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!plannedOutputTokens.TryGetValue(
                token.TokenId ?? string.Empty,
                out PlannedOutputTokenState state))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer planned-output token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Routed)
        {
            receipt = state.Receipt;
            if (FacilityBufferMassAdmissionRules.PlannedOutputTokenMatches(state.Token, token)
                && FacilityBufferMassAdmissionRules.PublicationReceiptsMatch(state.Publication, publication))
            {
                return true;
            }
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' replay conflicts.",
                out failureCode,
                out failureReason);
        }
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer planned-output token '{token.TokenId}' is not reserved.",
                out failureCode,
                out failureReason);
        }
        if (!FacilityBufferMassAdmissionRules.PlannedOutputTokenMatches(state.Token, token))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' commit mismatched.",
                out failureCode,
                out failureReason);
        }
        if (plannedOutputMassQuery == null
            || token.MassAuthorityRevision
                != plannedOutputMassQuery.AuthorityRevision
            || !profiles.TryGetValue(
                token.Request.DestinationId,
                out FacilityBufferCapacityProfile profile))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' is stale.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidateProfile(
                profile,
                profile.OwnerDomain,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (!FacilityBufferMassAdmissionRules.ProfileMatchesPlannedOutputRequest(profile, token.Request))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' profile changed.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidatePlannedOutputPublication(
                token,
                publication,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        RemoveReservedMass(
            token.Request.DestinationId,
            token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Routed;
        state.Publication = publication;
        state.Receipt = new FacilityBufferPlannedOutputReceipt(
            token,
            publication);
        receipt = state.Receipt;
        TrackPlannedTerminalToken(token.TokenId);
        return true;
    }

    public bool TryCommitPlannedOutputReversible(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out IReversibleFacilityBufferPlannedOutputAdmission transaction,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        transaction = null;
        if (!TryCommitPlannedOutput(
                token,
                publication,
                out FacilityBufferPlannedOutputReceipt receipt,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        transaction = new ReversiblePlannedOutputAdmission(
            this,
            token,
            publication,
            receipt);
        return true;
    }

    private bool TryRollbackCommittedPlannedOutput(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        FacilityBufferPlannedOutputReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!plannedOutputTokens.TryGetValue(
                token.TokenId ?? string.Empty,
                out PlannedOutputTokenState state)
            || state.Status != FacilityBufferMassAdmissionTokenStatus.Routed
            || !FacilityBufferMassAdmissionRules.PlannedOutputTokenMatches(state.Token, token)
            || !FacilityBufferMassAdmissionRules.PublicationReceiptsMatch(state.Publication, publication)
            || state.Receipt.CommittedMassGrams != receipt.CommittedMassGrams
            || state.Receipt.PublishedQuantity != receipt.PublishedQuantity
            || !string.Equals(
                state.Receipt.BatchCommitId,
                receipt.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                state.Receipt.PlannedOutputFingerprint,
                receipt.PlannedOutputFingerprint,
                StringComparison.Ordinal))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' reversible commit changed.",
                out failureCode,
                out failureReason);
        }
        long current = reservedByDestination.GetValueOrDefault(
            token.Request.DestinationId,
            0L);
        reservedByDestination[token.Request.DestinationId] = checked(
            current + token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Reserved;
        state.Publication = default;
        state.Receipt = default;
        return true;
    }

    public bool TryReleasePlannedOutput(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferMassAdmissionReleaseReason reason,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!Enum.IsDefined(typeof(FacilityBufferMassAdmissionReleaseReason), reason)
            || !plannedOutputTokens.TryGetValue(
                token.TokenId ?? string.Empty,
                out PlannedOutputTokenState state))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer planned-output token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Released)
            return state.ReleaseReason == reason;
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved
            || !FacilityBufferMassAdmissionRules.PlannedOutputTokenMatches(state.Token, token))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer planned-output token '{token.TokenId}' cannot be released.",
                out failureCode,
                out failureReason);
        }

        RemoveReservedMass(
            token.Request.DestinationId,
            token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Released;
        state.ReleaseReason = reason;
        TrackPlannedTerminalToken(token.TokenId);
        return true;
    }

    public void BeginRestoreCandidate()
    {
        if (restoreActive)
        {
            throw new InvalidOperationException(
                "Facility-buffer mass-capacity restore is already active.");
        }
        if (reservedByDestination.Count != 0
            || tokens.Values.Any(value =>
                value.Status == FacilityBufferMassAdmissionTokenStatus.Reserved)
            || plannedOutputTokens.Values.Any(value =>
                value.Status == FacilityBufferMassAdmissionTokenStatus.Reserved))
        {
            throw new InvalidOperationException(
                "Facility-buffer mass-capacity restore cannot begin with an active admission token.");
        }

        // Prepare every allocation and checked value before exposing an active
        // restore candidate. Publish and rollback are transaction callbacks;
        // once Begin succeeds neither callback may discover arithmetic or map
        // construction failures while the aggregate restore is being swapped.
        Dictionary<string, FacilityBufferCapacityProfile> preparedPrevious =
            CopyProfiles(profiles);
        Dictionary<string, FacilityBufferCapacityProfile> preparedCandidate =
            CreateProfileMap();
        long nextPublishRevision = checked(revision + 1L);
        long nextRollbackRevision = checked(nextPublishRevision + 1L);

        previousProfiles = preparedPrevious;
        candidateProfiles = preparedCandidate;
        preparedPublishRevision = nextPublishRevision;
        preparedRollbackRevision = nextRollbackRevision;
        restoreActive = true;
        restorePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        profiles = candidateProfiles;
        candidateProfiles = null;
        restorePublished = true;
        revision = preparedPublishRevision;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreActive)
            return;
        if (restorePublished && previousProfiles != null)
        {
            profiles = previousProfiles;
            revision = preparedRollbackRevision;
        }
        ResetRestoreState();
    }

    public void CompleteRestoreCandidate()
    {
        if (restorePublished)
        {
            tokens.Clear();
            terminalTokenOrder.Clear();
            plannedOutputTokens.Clear();
            plannedTerminalTokenOrder.Clear();
            reservedByDestination.Clear();
        }
        ResetRestoreState();
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublished)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        ResetRestoreState();
    }

    private bool TryCapturePlannedOutput(
        FacilityBufferPlannedOutputRequest request,
        long massAuthorityRevision,
        out FacilityBufferPlannedOutputSnapshot planned,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        planned = default;
        try
        {
            List<FacilityBufferPlannedOutputSliceSnapshot> snapshots = new();
            int totalQuantity = 0;
            long totalMassGrams = 0L;
            foreach (FacilityBufferPlannedOutputSlice slice in request.Slices
                         .OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
            {
                IReadOnlyList<ItemInstanceComponentSaveData> runtimeComponents =
                    slice.MaterializeRuntimeComponents();
                PhysicalItemMassSubject reconstructed =
                    PhysicalItemMassSubjectAdapter.Create(
                        plannedOutputMassQuery,
                        slice.ItemDefinitionId,
                        slice.Subject.ItemInstanceId,
                        runtimeComponents);
                if (!PlannedSubjectsMatch(slice.Subject, reconstructed))
                {
                    return FacilityBufferMassAdmissionRules.Fail(
                        FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                        $"Facility-buffer planned-output line '{slice.OutputLineId}' component subject mismatched.",
                        out failureCode,
                        out failureReason);
                }
                PhysicalMassGrams exactMass = plannedOutputMassQuery.GetQuantityMass(
                    slice.ItemDefinitionId,
                    reconstructed,
                    slice.Quantity);
                totalQuantity = checked(totalQuantity + slice.Quantity);
                totalMassGrams = checked(totalMassGrams + exactMass.Value);
                snapshots.Add(new FacilityBufferPlannedOutputSliceSnapshot(
                    slice,
                    exactMass));
            }
            if (totalQuantity <= 0 || totalMassGrams <= 0L)
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                    "Facility-buffer planned output has no positive physical mass.",
                    out failureCode,
                    out failureReason);
            }
            if (plannedOutputMassQuery.AuthorityRevision
                != massAuthorityRevision)
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                    "Facility-buffer planned-output mass authority changed during projection.",
                    out failureCode,
                    out failureReason);
            }

            string fingerprint = FacilityBufferMassAdmissionRules.BuildPlannedOutputFingerprint(
                request,
                snapshots,
                totalQuantity,
                totalMassGrams,
                massAuthorityRevision);
            planned = new FacilityBufferPlannedOutputSnapshot(
                fingerprint,
                snapshots,
                totalQuantity,
                new PhysicalMassGrams(totalMassGrams));
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer planned-output mass projection failed: "
                + exception.Message,
                out failureCode,
                out failureReason);
        }
    }

    private bool TryValidatePlannedOutputPublication(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!string.Equals(
                publication.AdmissionTokenId,
                token.TokenId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.BatchCommitId,
                token.Request.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.OutcomeFingerprint,
                token.Request.OutcomeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.DestinationId,
                token.Request.DestinationId,
                StringComparison.Ordinal)
            || publication.DropPosition != token.Request.DropPosition
            || !string.Equals(
                publication.OwnerDomain,
                token.Request.ExpectedOwnerDomain,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.OwnerOperationId,
                token.Request.ExpectedOwnerOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.OwnerFacilityId,
                token.Request.ExpectedOwnerFacilityId,
                StringComparison.Ordinal)
            || publication.CapacityRevision
                != token.Request.ExpectedCapacityRevision
            || !string.Equals(
                publication.PlannedOutputFingerprint,
                token.PlannedOutput.Fingerprint,
                StringComparison.Ordinal))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' publication ownership mismatched.",
                out failureCode,
                out failureReason);
        }

        FacilityBufferPublishedOutputStackReceipt[] published =
            publication.Stacks.ToArray();
        if (published.Length == 0
            || published.Any(stack => !FacilityBufferMassAdmissionRules.IsCanonicalRequired(stack.StackId)
                || !FacilityBufferMassAdmissionRules.IsCanonicalRequired(stack.OutputLineId)
                || !stack.ItemDefinitionId.IsValid
                || stack.Quantity <= 0
                || stack.MassGrams <= 0L)
            || published.Select(stack => stack.StackId)
                .Distinct(StringComparer.Ordinal).Count() != published.Length)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' publication receipt is invalid.",
                out failureCode,
                out failureReason);
        }

        Dictionary<string, FacilityBufferPlannedOutputSliceSnapshot> expected =
            token.PlannedOutput.Slices.ToDictionary(
                slice => slice.OutputLineId,
                StringComparer.Ordinal);
        if (published.Select(stack => stack.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != expected.Count
            || published.Any(stack => !expected.ContainsKey(stack.OutputLineId)))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' publication lines mismatched.",
                out failureCode,
                out failureReason);
        }

        try
        {
            int totalQuantity = 0;
            long totalMassGrams = 0L;
            HashSet<string> publishedItemInstanceIds = new(
                StringComparer.Ordinal);
            foreach (IGrouping<string, FacilityBufferPublishedOutputStackReceipt> group in
                     published.GroupBy(
                         stack => stack.OutputLineId,
                         StringComparer.Ordinal))
            {
                FacilityBufferPlannedOutputSliceSnapshot expectedLine =
                    expected[group.Key];
                int lineQuantity = 0;
                long lineMassGrams = 0L;
                foreach (FacilityBufferPublishedOutputStackReceipt stack in group)
                {
                    if (!stack.ItemDefinitionId.Equals(
                            expectedLine.ItemDefinitionId))
                    {
                        return FacilityBufferMassAdmissionRules.Fail(
                            FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                            $"Facility-buffer planned-output line '{group.Key}' item mismatched.",
                            out failureCode,
                            out failureReason);
                    }
                    string expectedInstanceId =
                        expectedLine.Source.Subject.ItemInstanceId
                        ?? string.Empty;
                    string publishedInstanceId = stack.ItemInstanceId
                        ?? string.Empty;
                    bool exactInstance = string.Equals(
                        publishedInstanceId,
                        expectedInstanceId,
                        StringComparison.Ordinal);
                    bool publicationAllocatedGenericInstance =
                        expectedInstanceId.Length == 0
                        && expectedLine.Source.Subject.Kind
                            == PhysicalItemMassSubjectKind.GenericDefinition
                        && stack.Quantity == 1
                        && publishedInstanceId.Length > 0
                        && ((ItemInstanceId)publishedInstanceId).IsValid
                        && string.Equals(
                            ((ItemInstanceId)publishedInstanceId).Value,
                            publishedInstanceId,
                            StringComparison.Ordinal);
                    if (!exactInstance && !publicationAllocatedGenericInstance)
                    {
                        return FacilityBufferMassAdmissionRules.Fail(
                            FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                            $"Facility-buffer planned-output stack '{stack.StackId}' item instance mismatched.",
                            out failureCode,
                            out failureReason);
                    }
                    if (publishedInstanceId.Length > 0
                        && !publishedItemInstanceIds.Add(publishedInstanceId))
                    {
                        return FacilityBufferMassAdmissionRules.Fail(
                            FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                            $"Facility-buffer planned-output item instance '{publishedInstanceId}' is duplicated.",
                            out failureCode,
                            out failureReason);
                    }
                    long exactStackMass = plannedOutputMassQuery.GetQuantityMass(
                        expectedLine.ItemDefinitionId,
                        expectedLine.Source.Subject,
                        stack.Quantity).Value;
                    if (stack.MassGrams != exactStackMass)
                    {
                        return FacilityBufferMassAdmissionRules.Fail(
                            FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                            $"Facility-buffer planned-output stack '{stack.StackId}' mass mismatched.",
                            out failureCode,
                            out failureReason);
                    }
                    lineQuantity = checked(lineQuantity + stack.Quantity);
                    lineMassGrams = checked(lineMassGrams + stack.MassGrams);
                }
                if (lineQuantity != expectedLine.Quantity
                    || lineMassGrams != expectedLine.ExactMassGrams)
                {
                    return FacilityBufferMassAdmissionRules.Fail(
                        FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                        $"Facility-buffer planned-output line '{group.Key}' totals mismatched.",
                        out failureCode,
                        out failureReason);
                }
                totalQuantity = checked(totalQuantity + lineQuantity);
                totalMassGrams = checked(totalMassGrams + lineMassGrams);
            }
            if (totalQuantity != token.PlannedOutput.TotalQuantity
                || totalMassGrams != token.PlannedOutput.TotalMassGrams)
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                    $"Facility-buffer planned-output token '{token.TokenId}' batch totals mismatched.",
                    out failureCode,
                    out failureReason);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                "Facility-buffer planned-output publication validation failed: "
                + exception.Message,
                out failureCode,
                out failureReason);
        }

        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    private bool TryValidateProfile(
        FacilityBufferCapacityProfile profile,
        string expectedOwnerDomain,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (profile == null
            || !FacilityBufferMassAdmissionRules.IsCanonicalRequired(profile.DestinationId)
            || !FacilityBufferMassAdmissionRules.IsCanonicalRequired(profile.OwnerDomain)
            || !FacilityBufferMassAdmissionRules.IsCanonicalRequired(profile.OwnerOperationId)
            || profile.OwnerFacilityId != null
                && !FacilityBufferMassAdmissionRules.IsCanonicalRequired(profile.OwnerFacilityId)
            || profile.MaxMassGrams <= 0L
            || profile.CapacityRevision <= 0L
            || !string.Equals(
                profile.OwnerDomain,
                expectedOwnerDomain,
                StringComparison.Ordinal))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidProfile,
                "Facility-buffer capacity profile is invalid.",
                out failureCode,
                out failureReason);
        }
        if (!destinationClaims.TryGetAuthorityClaim(
                profile.DestinationId,
                profile.DropPosition,
                out FacilityBufferDestinationClaim claim)
            || claim.AnchorKind != FacilityBufferDestinationAnchorKind.ReservedTarget
                && !FacilityBufferMassAdmissionRules.IsCanonicalRequired(profile.OwnerFacilityId)
            || !string.Equals(
                claim.OwnerDomain,
                profile.OwnerDomain,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.OwnerOperationId,
                profile.OwnerOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.OwnerFacilityId,
                profile.OwnerFacilityId,
                StringComparison.Ordinal))
        {
            return FacilityBufferMassAdmissionRules.Fail(
                FacilityBufferMassAdmissionFailureCode.ClaimMissingOrMismatched,
                $"Facility-buffer capacity '{profile.DestinationId}' has no exact matching claim.",
                out failureCode,
                out failureReason);
        }
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    private static bool ProfileMatchesRequest(
        FacilityBufferCapacityProfile profile,
        FacilityBufferMassAdmissionRequest request) =>
        profile.DropPosition == request.DropPosition
        && profile.CapacityRevision == request.ExpectedCapacityRevision
        && string.Equals(
            profile.OwnerDomain,
            request.ExpectedOwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            request.ExpectedOwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            request.ExpectedOwnerFacilityId,
            StringComparison.Ordinal);

    private static bool ProfileOwnershipMatches(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && string.Equals(left.OwnerDomain, right.OwnerDomain, StringComparison.Ordinal)
        && string.Equals(
            left.OwnerOperationId,
            right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.OwnerFacilityId,
            right.OwnerFacilityId,
            StringComparison.Ordinal)
        && string.Equals(
            left.AuthorityDigest,
            right.AuthorityDigest,
            StringComparison.Ordinal);

    private static bool TokenMatches(
        FacilityBufferMassAdmissionToken left,
        FacilityBufferMassAdmissionToken right) =>
        left.ProfileRevision == right.ProfileRevision
        && left.ReservedMassGrams == right.ReservedMassGrams
        && string.Equals(left.TokenId, right.TokenId, StringComparison.Ordinal)
        && string.Equals(
            left.Request.TransferOperationId,
            right.Request.TransferOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.ExactLot.Fingerprint,
            right.ExactLot.Fingerprint,
            StringComparison.Ordinal);

    private bool HasOperationId(string operationId) => tokens.Values.Any(value =>
            string.Equals(
                value.Token.Request.TransferOperationId,
                operationId,
                StringComparison.Ordinal))
        || plannedOutputTokens.Values.Any(value => string.Equals(
            value.Token.Request.PublicationOperationId,
            operationId,
            StringComparison.Ordinal));

    private bool CanRetryRolledBackPlannedOutput(string operationId, string plannedFingerprint)
    {
        // A prepare rollback has no published effect to replay. Keep its token
        // history/idempotent release, but admit the exact same frozen request
        // with a new transient token. Active/routed/cancelled/conflicting and
        // source-transfer operations remain non-reusable.
        if (tokens.Values.Any(value => string.Equals(value.Token.Request.TransferOperationId,
                operationId, StringComparison.Ordinal)))
            return false;
        bool found = false;
        foreach (PlannedOutputTokenState state in plannedOutputTokens.Values)
        {
            if (!string.Equals(state.Token.Request.PublicationOperationId,
                    operationId, StringComparison.Ordinal))
                continue;
            found = true;
            if (state.Status != FacilityBufferMassAdmissionTokenStatus.Released
                || state.ReleaseReason != FacilityBufferMassAdmissionReleaseReason.TransactionRollback
                || !string.Equals(state.Token.PlannedOutput.Fingerprint,
                    plannedFingerprint, StringComparison.Ordinal))
                return false;
        }
        return found;
    }

    private static bool PlannedSubjectsMatch(
        PhysicalItemMassSubject left,
        PhysicalItemMassSubject right) => left != null
        && right != null
        && left.ItemId.Equals(right.ItemId)
        && string.Equals(left.ItemInstanceId, right.ItemInstanceId, StringComparison.Ordinal)
        && left.Kind == right.Kind
        && string.Equals(
            left.ComponentFingerprint,
            right.ComponentFingerprint,
            StringComparison.Ordinal);

    private void TrackTerminalToken(string tokenId)
    {
        terminalTokenOrder.Enqueue(tokenId);
        while (terminalTokenOrder.Count > MaximumTerminalTokenCount)
        {
            string oldest = terminalTokenOrder.Dequeue();
            if (tokens.TryGetValue(oldest, out TokenState state)
                && state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
            {
                tokens.Remove(oldest);
            }
        }
    }

    private void TrackPlannedTerminalToken(string tokenId)
    {
        plannedTerminalTokenOrder.Enqueue(tokenId);
        while (plannedTerminalTokenOrder.Count > MaximumTerminalTokenCount)
        {
            string oldest = plannedTerminalTokenOrder.Dequeue();
            if (plannedOutputTokens.TryGetValue(
                    oldest,
                    out PlannedOutputTokenState state)
                && state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
            {
                plannedOutputTokens.Remove(oldest);
            }
        }
    }

    private sealed class ReversiblePlannedOutputAdmission :
        IReversibleFacilityBufferPlannedOutputAdmission
    {
        private readonly FacilityBufferMassAdmissionService owner;
        private readonly FacilityBufferPlannedOutputToken token;
        private readonly FacilityBufferPlannedOutputPublicationReceipt
            publication;
        private bool terminal;

        internal ReversiblePlannedOutputAdmission(
            FacilityBufferMassAdmissionService owner,
            FacilityBufferPlannedOutputToken token,
            FacilityBufferPlannedOutputPublicationReceipt publication,
            FacilityBufferPlannedOutputReceipt receipt)
        {
            this.owner = owner;
            this.token = token;
            this.publication = publication;
            Receipt = receipt;
        }

        public FacilityBufferPlannedOutputReceipt Receipt { get; }

        public bool TryRollback(
            out FacilityBufferMassAdmissionFailureCode failureCode,
            out string failureReason)
        {
            if (terminal)
            {
                return FacilityBufferMassAdmissionRules.Fail(
                    FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                    "Reversible planned-output admission is terminal.",
                    out failureCode,
                    out failureReason);
            }
            bool rolledBack = owner.TryRollbackCommittedPlannedOutput(
                token,
                publication,
                Receipt,
                out failureCode,
                out failureReason);
            if (rolledBack)
                terminal = true;
            return rolledBack;
        }

        public bool TryAcknowledge(out string failureReason)
        {
            if (terminal)
            {
                failureReason =
                    "Reversible planned-output admission is terminal.";
                return false;
            }
            terminal = true;
            failureReason = string.Empty;
            return true;
        }
    }

    private bool TryGetMutationTarget(
        out Dictionary<string, FacilityBufferCapacityProfile> target,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        target = restoreActive ? candidateProfiles : profiles;
        if (!restoreActive || (!restorePublished && candidateProfiles != null))
        {
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }

        target = null;
        return FacilityBufferMassAdmissionRules.Fail(
            FacilityBufferMassAdmissionFailureCode.RestoreMutationAfterPublish,
            "Facility-buffer mass-capacity profiles cannot mutate after restore publication.",
            out failureCode,
            out failureReason);
    }

    private IReadOnlyDictionary<string, FacilityBufferCapacityProfile>
        GetAuthorityView() =>
        restoreActive && !restorePublished && candidateProfiles != null
            ? candidateProfiles
            : profiles;

    private static Dictionary<string, FacilityBufferCapacityProfile>
        CreateProfileMap() => new(StringComparer.Ordinal);

    private static Dictionary<string, FacilityBufferCapacityProfile> CopyProfiles(
        IReadOnlyDictionary<string, FacilityBufferCapacityProfile> source) =>
        source == null
            ? CreateProfileMap()
            : new Dictionary<string, FacilityBufferCapacityProfile>(
                source,
                StringComparer.Ordinal);

    private void ResetRestoreState()
    {
        candidateProfiles = null;
        previousProfiles = null;
        preparedPublishRevision = 0L;
        preparedRollbackRevision = 0L;
        restoreActive = false;
        restorePublished = false;
    }

    private void RemoveReservedMass(string destinationId, long massGrams)
    {
        long current = reservedByDestination.GetValueOrDefault(destinationId, 0L);
        if (current < massGrams)
        {
            throw new InvalidOperationException(
                $"Facility-buffer reserved mass underflow for '{destinationId}'.");
        }
        long next = current - massGrams;
        if (next == 0L)
            reservedByDestination.Remove(destinationId);
        else
            reservedByDestination[destinationId] = next;
    }

    private bool IsOwnerMutationFenceOpen(
        FacilityBufferCapacityProfile profile,
        out string operationId)
    {
        operationId = string.Empty;
        if (admissionFences == null || profile == null)
        {
            return false;
        }

        FacilityBufferDestinationAdmissionFenceSubject subject = new(
            profile.DestinationId,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId);
        if (!admissionFences.TryCaptureOpenFence(
                subject,
                out FacilityBufferDestinationAdmissionFenceSnapshot pending))
        {
            return false;
        }
        operationId = pending.OperationId;
        return true;
    }

    public bool TryProjectPlannedOutput(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputSnapshot planned,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        planned = default;
        long revision = plannedOutputMassQuery.AuthorityRevision;
        return FacilityBufferMassAdmissionRules.TryValidatePlannedOutputRequest(
                request,
                out failureCode,
                out failureReason)
            && TryCapturePlannedOutput(
                request,
                revision,
                out planned,
                out failureCode,
                out failureReason);
    }

}

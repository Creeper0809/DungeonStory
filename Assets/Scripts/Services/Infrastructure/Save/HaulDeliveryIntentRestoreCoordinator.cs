using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Rebinds saved, pickup-committed haul deliveries after the character
/// candidate is published and the physical stack/lease candidate is staged,
/// but before character publication activates AI. Any identity, quantity,
/// signature, destination or lease mismatch aborts the restore; no substitute
/// stack, destination or autonomous haul plan is selected.
/// </summary>
public sealed class HaulDeliveryIntentRestoreCoordinator :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId = "225.world.haul-delivery-intents";

    private readonly ICharacterHaulDeliveryRestoreQuery characters;
    private readonly IItemQuantityReservationService reservations;
    private readonly IItemQuantityReservationPersistence reservationPersistence;
    private readonly IWorkOrderQuery workOrders;
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly IHaulDeliveryIntentCommand commands;
    private readonly WorldItemRepository repository;
    private readonly WorldItemWarehouseService warehouseService;
    private IReadOnlyList<HaulDeliveryIntentSaveData> previousState;
    private readonly List<AbilityHaul> rebound = new();
    private readonly List<HaulDeliveryIntentSaveData> rebuiltWarehouseAdmissions = new();
    private bool active;
    private bool published;
    private bool registryReplaced;

    public HaulDeliveryIntentRestoreCoordinator(
        ICharacterHaulDeliveryRestoreQuery characters,
        IItemQuantityReservationService reservations,
        IItemQuantityReservationPersistence reservationPersistence,
        IWorkOrderQuery workOrders,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        WorldItemRepository repository,
        WorldItemWarehouseService warehouseService)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
        this.reservationPersistence = reservationPersistence
            ?? throw new ArgumentNullException(nameof(reservationPersistence));
        this.workOrders = workOrders ?? throw new ArgumentNullException(nameof(workOrders));
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.warehouseService = warehouseService
            ?? throw new ArgumentNullException(nameof(warehouseService));
        commands = repository.HaulDeliveryIntents;
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
            throw new InvalidOperationException("Haul delivery restore is already active.");
        previousState = commands.CaptureRuntimeState();
        rebound.Clear();
        rebuiltWarehouseAdmissions.Clear();
        active = true;
        published = false;
        registryReplaced = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
            throw new InvalidOperationException("Haul delivery restore is not ready to publish.");

        CharacterHaulDeliveryRestoreBinding[] bindings = characters
            .GetPublishedHaulDeliveryRestoreBindings()
            .Where(binding => binding.Intent?.HasCommittedPickup == true)
            .ToArray();
        HashSet<string> bindingOperationIds = bindings
            .Select(binding => binding.Intent.operationId?.Trim() ?? string.Empty)
            .Where(operationId => operationId.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> restoredHaulingLeaseOwners = reservationPersistence
            .CaptureReservationIntents()
            .Where(intent => intent != null
                && intent.reservationHints != null
                && intent.reservationHints.Any(hint => hint != null
                    && hint.purpose == ItemReservationPurpose.Hauling))
            .Select(intent => intent.ownerOperationId?.Trim() ?? string.Empty)
            .Where(operationId => operationId.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (bindingOperationIds.Count != bindings.Length
            || !bindingOperationIds.SetEquals(restoredHaulingLeaseOwners))
        {
            throw new InvalidOperationException(
                "Restored hauling leases and character delivery intents are not a one-to-one authority set: "
                + $"bindings=[{string.Join(",", bindingOperationIds.OrderBy(value => value, StringComparer.Ordinal))}]; "
                + $"leases=[{string.Join(",", restoredHaulingLeaseOwners.OrderBy(value => value, StringComparer.Ordinal))}].");
        }

        commands.ReplaceRuntimeState(Array.Empty<HaulDeliveryIntentSaveData>());
        registryReplaced = true;
        foreach (CharacterHaulDeliveryRestoreBinding binding in bindings)
        {
            HaulDeliveryIntentSaveData intent = binding.Intent;
            if (binding.Actor == null
                || !reservations.TryGetLeasesByOwner(
                    intent.operationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                || leases.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Haul delivery '{intent.operationId}' has no restored actor or lease.");
            }
            IReadOnlyList<ItemQuantityLease> revalidatedLeases =
                RevalidateExactLeases(intent, leases);
            if (!warehouseService.TryRebuildRestoredHaulAdmissions(
                    intent,
                    out string admissionFailure))
            {
                throw new InvalidOperationException(
                    $"Haul delivery '{intent.operationId}' destination admission rebind failed: "
                    + admissionFailure);
            }
            if (intent.warehouseAdmissions?.Count > 0)
            {
                rebuiltWarehouseAdmissions.Add(intent);
            }
            if (!commands.TryRestoreCommitted(intent, out string registryFailure))
            {
                throw new InvalidOperationException(registryFailure);
            }

            AbilityHaul haul = AbilityHaul.Ensure(binding.Actor);
            string rebindFailure = haul == null
                ? "Restored actor has no AbilityHaul."
                : string.Empty;
            if (haul == null
                || !haul.TryRebindRestoredDeliveryIntent(
                    intent,
                    revalidatedLeases,
                    workOrders,
                    destinationClaims,
                    out rebindFailure))
            {
                throw new InvalidOperationException(
                    $"Haul delivery '{intent.operationId}' rebind failed: {rebindFailure}");
            }
            rebound.Add(haul);
        }
        published = true;
    }

    private IReadOnlyList<ItemQuantityLease> RevalidateExactLeases(
        HaulDeliveryIntentSaveData intent,
        IReadOnlyList<ItemQuantityLease> ownerLeases)
    {
        string operationId = intent.operationId?.Trim() ?? string.Empty;
        string ownerCharacterId = intent.ownerCharacterId?.Trim() ?? string.Empty;
        if (!HaulDeliveryOperationIdentity.TryParse(
                operationId,
                ownerCharacterId,
                out long sequence)
            || sequence <= 0
            || sequence >= repository.NextHaulOperationSequence)
        {
            throw new InvalidOperationException(
                $"Haul delivery '{operationId}' is outside the saved deterministic operation sequence.");
        }
        string expectedCohort =
            $"haul:{intent.destinationKind}:{intent.destinationId?.Trim()}";
        HaulDeliveryItemCommitmentSaveData[] commitments = intent.commitments
            .Where(value => value != null)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        if (commitments.Length == 0 || ownerLeases.Count != commitments.Length)
        {
            throw new InvalidOperationException(
                $"Haul delivery '{operationId}' lease count does not match its physical commitments.");
        }

        List<ItemQuantityLease> exact = new(commitments.Length);
        HashSet<string> matchedLeaseIds = new(StringComparer.Ordinal);
        foreach (HaulDeliveryItemCommitmentSaveData commitment in commitments)
        {
            ItemQuantityLease candidate = ownerLeases.SingleOrDefault(lease =>
                IsExactLease(
                    lease,
                    operationId,
                    ownerCharacterId,
                    expectedCohort,
                    commitment));
            DomainFailure failure = DomainFailure.None;
            if (candidate == null
                || !matchedLeaseIds.Add(candidate.leaseId)
                || !reservations.Revalidate(
                    candidate.leaseId,
                    out ItemQuantityLease revalidated,
                    out failure)
                || !IsExactLease(
                    revalidated,
                    operationId,
                    ownerCharacterId,
                    expectedCohort,
                    commitment))
            {
                throw new InvalidOperationException(
                    $"Haul delivery '{operationId}' lease rebind mismatch for "
                    + $"'{commitment.carriedStackId}': {failure}.");
            }
            exact.Add(revalidated);
        }

        if (matchedLeaseIds.Count != ownerLeases.Count)
        {
            throw new InvalidOperationException(
                $"Haul delivery '{operationId}' has an unrelated restored lease.");
        }
        return exact;
    }

    private static bool IsExactLease(
        ItemQuantityLease lease,
        string operationId,
        string ownerCharacterId,
        string expectedCohort,
        HaulDeliveryItemCommitmentSaveData commitment)
    {
        return lease != null
            && lease.purpose == ItemReservationPurpose.Hauling
            && lease.remainingQuantity == commitment.quantity
            && lease.slices != null
            && lease.slices.Count == 1
            && string.Equals(
                lease.ownerOperationId?.Trim(),
                operationId,
                StringComparison.Ordinal)
            && string.Equals(
                lease.ownerCharacterId?.Trim(),
                ownerCharacterId,
                StringComparison.Ordinal)
            && string.Equals(
                lease.aggregationCohortId?.Trim(),
                expectedCohort,
                StringComparison.Ordinal)
            && lease.slices[0] != null
            && lease.slices[0].quantity == commitment.quantity
            && string.Equals(
                lease.slices[0].stackId?.Trim(),
                commitment.carriedStackId?.Trim(),
                StringComparison.Ordinal)
            && string.Equals(
                lease.slices[0].expectedStackSignature?.Trim(),
                commitment.expectedStackSignature?.Trim(),
                StringComparison.Ordinal);
    }

    public void RollbackPublishedRestoreCandidate()
    {
        for (int index = rebound.Count - 1; index >= 0; index--)
            rebound[index]?.ClearRestoredDeliveryIntentBinding();
        for (int index = rebuiltWarehouseAdmissions.Count - 1; index >= 0; index--)
        {
            warehouseService.ReleaseHaulAdmissions(
                rebuiltWarehouseAdmissions[index],
                WarehouseMassAdmissionReleaseReason.RestoreRollback);
        }
        commands.ReplaceRuntimeState(
            previousState ?? Array.Empty<HaulDeliveryIntentSaveData>());
        Reset();
    }

    public void CompleteRestoreCandidate()
    {
        // Binding is already complete while restored actors are inactive.
        // Completion must remain non-failing and must not wake or replan AI.
        Reset();
    }

    public void DiscardRestoreCandidate()
    {
        // Publish can fail after replacing the registry or rebinding an earlier
        // actor but before the participant is marked published. That partial
        // state still owns cleanup and must be rolled back exactly once.
        if (published || registryReplaced || rebound.Count > 0)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        Reset();
    }

    private void Reset()
    {
        previousState = null;
        rebound.Clear();
        rebuiltWarehouseAdmissions.Clear();
        active = false;
        published = false;
        registryReplaced = false;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Current-format cross-section join for a pickup-committed warehouse haul.
/// The saved token id is an ephemeral runtime handle and may be reissued on
/// restore; every physical and authored field around it remains exact.
/// </summary>
internal static class ExactWarehouseHaulAdmissionJoin
{
    internal static void RetainCommittedAdmissions(
        HaulDeliveryIntentSaveData intent)
    {
        if (intent?.warehouseAdmissions == null || intent.commitments == null)
            return;
        HaulDeliveryItemCommitmentSaveData[] commitments = intent.commitments
            .Where(value => value != null)
            .ToArray();
        intent.warehouseAdmissions = intent.warehouseAdmissions
            .Where(admission => admission != null
                && commitments.Any(commitment =>
                    string.Equals(
                        commitment.sourceStackId,
                        admission.sourceStackId,
                        StringComparison.Ordinal)
                    && string.Equals(commitment.itemId, admission.itemId,
                        StringComparison.Ordinal)
                    && commitment.quantity == admission.quantity
                    && string.Equals(
                        commitment.expectedStackSignature,
                        admission.lotFingerprint,
                        StringComparison.Ordinal)))
            .OrderBy(value => value.ownerAdmissionOperationId,
                StringComparer.Ordinal)
            .ToList();
    }

    internal static bool TryValidateCurrentAuthorityProvenance(
        WarehouseHaulAdmissionSaveData admission,
        long currentCatalogRevision,
        out string failureReason)
    {
        if (admission == null
            || currentCatalogRevision <= 0L
            || admission.catalogRevision != currentCatalogRevision
            || admission.sourceRevision <= 0L)
        {
            failureReason = "warehouse-admission-authority-revision-mismatch";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    internal static bool TryValidateSavedIntent(
        HaulDeliveryIntentSaveData intent,
        IReadOnlyList<CharacterCarriedItemSaveData> carriedItems,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (intent == null
            || intent.commitments == null
            || intent.warehouseAdmissions == null)
        {
            failureReason = "warehouse-admission-intent-missing";
            return false;
        }

        CharacterCarriedItemSaveData[] carried = (carriedItems
                ?? Array.Empty<CharacterCarriedItemSaveData>())
            .Where(value => value != null && value.quantity > 0)
            .ToArray();
        WarehouseHaulAdmissionSaveData[] admissions = intent.warehouseAdmissions
            .Where(value => value != null)
            .ToArray();
        bool hasExactWarehouseCustody = carried.Any(IsExactWarehouseCustody);
        if (admissions.Length == 0)
        {
            if (hasExactWarehouseCustody)
            {
                failureReason = "exact-warehouse-admission-missing";
                return false;
            }
            return true;
        }

        if (intent.destinationKind != WorldItemHaulDestinationKind.Warehouse
            || intent.commitments.Count != admissions.Length
            || carried.Length != admissions.Length)
        {
            failureReason = "warehouse-admission-cardinality-mismatch";
            return false;
        }

        string destination = intent.destinationId ?? string.Empty;
        HashSet<string> tokenIds = new(StringComparer.Ordinal);
        HashSet<string> operationIds = new(StringComparer.Ordinal);
        HashSet<string> sourceStackIds = new(StringComparer.Ordinal);
        HashSet<string> matchedCommitments = new(StringComparer.Ordinal);
        string admissionOperationPrefix =
            (intent.operationId ?? string.Empty) + ":warehouse-admission:";
        foreach (WarehouseHaulAdmissionSaveData admission in admissions)
        {
            if (!IsCanonicalRequired(admission.tokenId)
                || !IsCanonicalRequired(admission.ownerAdmissionOperationId)
                || !IsCanonicalRequired(admission.warehouseId)
                || !IsCanonicalRequired(admission.sourceStackId)
                || !IsCanonicalRequired(admission.itemId)
                || !IsCanonicalRequired(admission.lotFingerprint)
                || !HasCanonicalAdmissionOrdinal(
                    admission.ownerAdmissionOperationId,
                    admissionOperationPrefix)
                || !tokenIds.Add(admission.tokenId)
                || !operationIds.Add(admission.ownerAdmissionOperationId)
                || !sourceStackIds.Add(admission.sourceStackId)
                || admission.quantity <= 0
                || admission.reservedMassGrams <= 0L
                || admission.catalogRevision <= 0L
                || admission.sourceRevision <= 0L
                || !string.Equals(
                    destination,
                    WarehouseStorageIdentity.DestinationPrefix
                    + admission.warehouseId,
                    StringComparison.Ordinal))
            {
                failureReason = "warehouse-admission-provenance-invalid";
                return false;
            }

            HaulDeliveryItemCommitmentSaveData[] commitmentMatches =
                intent.commitments
                    .Where(value => value != null
                        && string.Equals(
                            value.sourceStackId,
                            admission.sourceStackId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.itemId,
                            admission.itemId,
                            StringComparison.Ordinal)
                        && value.quantity == admission.quantity
                        && string.Equals(
                            value.expectedStackSignature,
                            admission.lotFingerprint,
                            StringComparison.Ordinal))
                    .ToArray();
            if (commitmentMatches.Length != 1
                || !matchedCommitments.Add(
                    commitmentMatches[0].carriedStackId ?? string.Empty))
            {
                failureReason = "warehouse-admission-commitment-mismatch";
                return false;
            }

            HaulDeliveryItemCommitmentSaveData commitment = commitmentMatches[0];
            CharacterCarriedItemSaveData[] carriedMatches = carried
                .Where(value => string.Equals(
                    value.carriedStackId,
                    commitment.carriedStackId,
                    StringComparison.Ordinal))
                .ToArray();
            if (carriedMatches.Length != 1)
            {
                failureReason = "warehouse-admission-carried-lot-mismatch";
                return false;
            }

            CharacterCarriedItemSaveData physical = carriedMatches[0];
            string signature = ItemReservationSignature.Create(
                physical.itemId,
                physical.components);
            if (physical.quantity != admission.quantity
                || !string.Equals(
                    physical.sourceStackId,
                    admission.sourceStackId,
                    StringComparison.Ordinal)
                || !string.Equals(physical.itemId, admission.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    physical.itemInstanceId ?? string.Empty,
                    admission.itemInstanceId ?? string.Empty,
                    StringComparison.Ordinal)
                || !string.Equals(signature, admission.lotFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = "warehouse-admission-physical-lot-mismatch";
                return false;
            }

            if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    physical.components)
                && (!FacilityOutputExactRouteCustodyCodec.TryRead(
                        physical.components,
                        out FacilityOutputExactRouteCustodyMetadata custody)
                    || custody.Phase !=
                        FacilityOutputExactRouteCustodyPhase.Routable
                    || custody.Quantity != admission.quantity
                    || custody.MassGrams != admission.reservedMassGrams
                    || !string.Equals(custody.ItemId, admission.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        custody.CurrentSourceStackId,
                        physical.carriedStackId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        custody.CurrentTargetDestinationId,
                        destination,
                        StringComparison.Ordinal)))
            {
                failureReason = "exact-warehouse-admission-custody-mismatch";
                return false;
            }
        }

        if (matchedCommitments.Count != intent.commitments.Count)
        {
            failureReason = "warehouse-admission-unmatched-commitment";
            return false;
        }
        return true;
    }

    private static bool IsExactWarehouseCustody(
        CharacterCarriedItemSaveData carried) =>
        carried != null
        && FacilityOutputExactRouteCustodyCodec.TryRead(
            carried.components,
            out FacilityOutputExactRouteCustodyMetadata custody)
        && custody.Phase == FacilityOutputExactRouteCustodyPhase.Routable
        && custody.CurrentTargetDestinationId.StartsWith(
            WarehouseStorageIdentity.DestinationPrefix,
            StringComparison.Ordinal);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool HasCanonicalAdmissionOrdinal(
        string operationId,
        string prefix) =>
        IsCanonicalRequired(prefix)
        && operationId?.Length == prefix.Length + 2
        && operationId.StartsWith(prefix, StringComparison.Ordinal)
        && operationId[prefix.Length] is >= '0' and <= '9'
        && operationId[prefix.Length + 1] is >= '0' and <= '9';
}

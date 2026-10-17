using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static class FacilityBufferMassAdmissionRules
{
    internal static bool TryValidatePlannedOutputRequest(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        FacilityBufferPlannedOutputSlice[] slices = request.Slices.ToArray();
        bool productionCapacitySource = string.Equals(
            request.ExpectedOwnerDomain,
            "economy.production-output",
            StringComparison.Ordinal);
        if (!IsCanonicalRequired(request.PublicationOperationId)
            || !IsCanonicalRequired(request.BatchCommitId)
            || !IsCanonicalRequired(request.OutcomeFingerprint)
            || !IsCanonicalRequired(request.DestinationId)
            || !IsCanonicalRequired(request.ExpectedOwnerDomain)
            || !IsCanonicalRequired(request.ExpectedOwnerOperationId)
            || !IsCanonicalRequired(request.ExpectedOwnerFacilityId)
            || request.ExpectedCapacityRevision <= 0L
            || productionCapacitySource
                && (!IsLowercaseSha256(request.CapacitySourceDigest)
                    || request.ExpectedMinimumCapacityGrams <= 0L
                    || request.CapacityAuthorityDigest.Length != 0
                        && !IsLowercaseSha256(
                            request.CapacityAuthorityDigest))
            || !productionCapacitySource
                && (request.CapacitySourceDigest.Length != 0
                    || request.ExpectedMinimumCapacityGrams != 0L
                    || request.CapacityAuthorityDigest.Length != 0)
            || slices.Length == 0
            || slices.Any(slice =>
                !IsCanonicalRequired(slice.OutputLineId)
                || slice.Subject == null
                || !slice.ItemDefinitionId.IsValid
                || slice.Quantity <= 0
                || (slice.PreparedComponentFingerprint ?? string.Empty).Length > 0
                    && !IsLowercaseSha256(
                        slice.PreparedComponentFingerprint)
                || (slice.UniqueBindingCapabilityId ?? string.Empty).Length > 0
                    && !IsCanonicalRequired(slice.UniqueBindingCapabilityId))
            || slices.Select(slice => slice.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != slices.Length)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer planned-output request is invalid.",
                out failureCode,
                out failureReason);
        }

        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        foreach (char character in value)
        {
            if (!(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f'))
                return false;
        }
        return true;
    }

    internal static bool TryValidateRequest(
        FacilityBufferMassAdmissionRequest request,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!IsCanonicalRequired(request.TransferOperationId)
            || !IsCanonicalRequired(request.DestinationId)
            || !IsCanonicalRequired(request.ExpectedOwnerDomain)
            || !IsCanonicalRequired(request.ExpectedOwnerOperationId)
            || request.ExpectedOwnerFacilityId != null
                && !IsCanonicalRequired(request.ExpectedOwnerFacilityId)
            || request.ExpectedCapacityRevision <= 0L
            || request.ExactLotSlices == null
            || request.ExactLotSlices.Count == 0)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer mass admission request is invalid.",
                out failureCode,
                out failureReason);
        }
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    internal static bool ProfileMatchesPlannedOutputRequest(
        FacilityBufferCapacityProfile profile,
        FacilityBufferPlannedOutputRequest request) =>
        profile.DropPosition == request.DropPosition
        && profile.CapacityRevision == request.ExpectedCapacityRevision
        && profile.MaxMassGrams >= request.ExpectedMinimumCapacityGrams
        && string.Equals(
            profile.AuthorityDigest,
            request.CapacityAuthorityDigest,
            StringComparison.Ordinal)
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

    private static bool ProfilesMatch(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && left.MaxMassGrams == right.MaxMassGrams
        && left.CapacityRevision == right.CapacityRevision
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
            StringComparison.Ordinal)
        && string.Equals(
            left.AuthorityDigest,
            right.AuthorityDigest,
            StringComparison.Ordinal);

    internal static bool OwnedProfileSetMatches(
        IReadOnlyDictionary<string, FacilityBufferCapacityProfile> current,
        string ownerDomain,
        IReadOnlyDictionary<string, FacilityBufferCapacityProfile> desired)
    {
        if (current == null || desired == null)
            return false;
        int ownedCount = 0;
        foreach (FacilityBufferCapacityProfile profile in current.Values)
        {
            if (profile != null && string.Equals(
                    profile.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
            {
                ownedCount++;
            }
        }
        if (ownedCount != desired.Count)
            return false;
        foreach (KeyValuePair<string, FacilityBufferCapacityProfile> pair in desired)
        {
            if (!current.TryGetValue(
                    pair.Key,
                    out FacilityBufferCapacityProfile candidate)
                || !ProfilesMatch(candidate, pair.Value))
            {
                return false;
            }
        }
        return true;
    }

    internal static bool AdmissionRequestsMatch(
        FacilityBufferMassAdmissionRequest left,
        FacilityBufferMassAdmissionRequest right) =>
        string.Equals(left.TransferOperationId, right.TransferOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && left.DropPosition == right.DropPosition
        && left.ExpectedCapacityRevision == right.ExpectedCapacityRevision
        && string.Equals(left.ExpectedOwnerDomain, right.ExpectedOwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(left.ExpectedOwnerOperationId,
            right.ExpectedOwnerOperationId, StringComparison.Ordinal)
        && string.Equals(left.ExpectedOwnerFacilityId,
            right.ExpectedOwnerFacilityId, StringComparison.Ordinal)
        && left.ExactLotSlices.Count == right.ExactLotSlices.Count
        && left.ExactLotSlices.OrderBy(value => value.StackId,
                StringComparer.Ordinal)
            .Zip(right.ExactLotSlices.OrderBy(value => value.StackId,
                    StringComparer.Ordinal),
                (a, b) => a.StackId == b.StackId
                    && a.Quantity == b.Quantity
                    && a.ExpectedReservationRevision
                        == b.ExpectedReservationRevision
                    && a.ExpectedCustodyMassGrams
                        == b.ExpectedCustodyMassGrams
                    && string.Equals(a.ExpectedCustodyComponentFingerprint,
                        b.ExpectedCustodyComponentFingerprint,
                        StringComparison.Ordinal))
            .All(value => value);

    internal static bool PlannedOutputTokenMatches(
        FacilityBufferPlannedOutputToken left,
        FacilityBufferPlannedOutputToken right) =>
        left.CapacityAuthorityRevision == right.CapacityAuthorityRevision
        && left.MassAuthorityRevision == right.MassAuthorityRevision
        && left.ReservedMassGrams == right.ReservedMassGrams
        && string.Equals(left.TokenId, right.TokenId, StringComparison.Ordinal)
        && string.Equals(
            left.Request.PublicationOperationId,
            right.Request.PublicationOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.Request.BatchCommitId,
            right.Request.BatchCommitId,
            StringComparison.Ordinal)
        && string.Equals(
            left.Request.OutcomeFingerprint,
            right.Request.OutcomeFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            left.Request.DestinationId,
            right.Request.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.PlannedOutput.Fingerprint,
            right.PlannedOutput.Fingerprint,
            StringComparison.Ordinal);

    internal static bool PublicationReceiptsMatch(
        FacilityBufferPlannedOutputPublicationReceipt left,
        FacilityBufferPlannedOutputPublicationReceipt right)
    {
        if (!string.Equals(
                left.AdmissionTokenId,
                right.AdmissionTokenId,
                StringComparison.Ordinal)
            || !string.Equals(
                left.BatchCommitId,
                right.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                left.OutcomeFingerprint,
                right.OutcomeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                left.DestinationId,
                right.DestinationId,
                StringComparison.Ordinal)
            || left.DropPosition != right.DropPosition
            || !string.Equals(
                left.OwnerDomain,
                right.OwnerDomain,
                StringComparison.Ordinal)
            || !string.Equals(
                left.OwnerOperationId,
                right.OwnerOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                left.OwnerFacilityId,
                right.OwnerFacilityId,
                StringComparison.Ordinal)
            || left.CapacityRevision != right.CapacityRevision
            || !string.Equals(
                left.PlannedOutputFingerprint,
                right.PlannedOutputFingerprint,
                StringComparison.Ordinal)
            || left.Stacks.Count != right.Stacks.Count)
        {
            return false;
        }

        FacilityBufferPublishedOutputStackReceipt[] leftStacks = left.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferPublishedOutputStackReceipt[] rightStacks = right.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < leftStacks.Length; index++)
        {
            FacilityBufferPublishedOutputStackReceipt a = leftStacks[index];
            FacilityBufferPublishedOutputStackReceipt b = rightStacks[index];
            if (!string.Equals(a.StackId, b.StackId, StringComparison.Ordinal)
                || !string.Equals(
                    a.OutputLineId,
                    b.OutputLineId,
                    StringComparison.Ordinal)
                || !a.ItemDefinitionId.Equals(b.ItemDefinitionId)
                || a.Quantity != b.Quantity
                || a.MassGrams != b.MassGrams
                || !string.Equals(
                    a.ItemInstanceId,
                    b.ItemInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal static string BuildPlannedOutputFingerprint(
        FacilityBufferPlannedOutputRequest request,
        IReadOnlyList<FacilityBufferPlannedOutputSliceSnapshot> slices,
        int totalQuantity,
        long totalMassGrams,
        long massAuthorityRevision)
    {
        StringBuilder canonical = new();
        AppendFingerprintValue(canonical, "facility-buffer-planned-output-v5");
        AppendFingerprintValue(canonical, request.PublicationOperationId);
        AppendFingerprintValue(canonical, request.BatchCommitId);
        AppendFingerprintValue(canonical, request.OutcomeFingerprint);
        AppendFingerprintValue(canonical, request.DestinationId);
        AppendFingerprintValue(canonical, request.DropPosition.x.ToString(
            CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, request.DropPosition.y.ToString(
            CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, request.ExpectedOwnerDomain);
        AppendFingerprintValue(canonical, request.ExpectedOwnerOperationId);
        AppendFingerprintValue(canonical, request.ExpectedOwnerFacilityId);
        AppendFingerprintValue(canonical, request.ExpectedCapacityRevision.ToString(
            CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, request.CapacitySourceDigest);
        AppendFingerprintValue(canonical, request.CapacityAuthorityDigest);
        AppendFingerprintValue(canonical, request.ExpectedMinimumCapacityGrams
            .ToString(CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, massAuthorityRevision.ToString(
            CultureInfo.InvariantCulture));

        foreach (FacilityBufferPlannedOutputSliceSnapshot slice in slices
                     .OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
        {
            PhysicalItemMassSubject subject = slice.Source.Subject;
            AppendFingerprintValue(canonical, slice.OutputLineId);
            AppendFingerprintValue(canonical, slice.ItemDefinitionId.Value);
            AppendFingerprintValue(canonical, ((int)subject.Kind).ToString(
                CultureInfo.InvariantCulture));
            AppendFingerprintValue(canonical, subject.ItemInstanceId);
            AppendFingerprintValue(canonical, subject.ComponentFingerprint);
            AppendFingerprintValue(
                canonical,
                slice.Source.PreparedComponentFingerprint);
            AppendFingerprintValue(
                canonical,
                slice.Source.UniqueBindingCapabilityId);
            AppendFingerprintValue(canonical, slice.Quantity.ToString(
                CultureInfo.InvariantCulture));
            AppendFingerprintValue(canonical, slice.ExactMassGrams.ToString(
                CultureInfo.InvariantCulture));
            foreach (PhysicalItemComponentSnapshot component in subject.Components
                         .OrderBy(value => value.ComponentTypeId, StringComparer.Ordinal)
                         .ThenBy(value => value.SchemaVersion)
                         .ThenBy(value => value.Fingerprint, StringComparer.Ordinal))
            {
                AppendFingerprintValue(canonical, component.ComponentTypeId);
                AppendFingerprintValue(canonical, component.SchemaVersion.ToString(
                    CultureInfo.InvariantCulture));
                AppendFingerprintValue(canonical, component.CanonicalPayload);
                AppendFingerprintValue(canonical, component.Fingerprint);
                AppendFingerprintValue(
                    canonical,
                    component.PreparedUnitMass.HasValue
                        ? component.PreparedUnitMass.Value.Value.ToString(
                            CultureInfo.InvariantCulture)
                        : string.Empty);
                foreach (PhysicalItemMassContribution contribution in
                         component.MassContributions
                             .OrderBy(value => value.ItemId.Value, StringComparer.Ordinal)
                             .ThenBy(value => value.Quantity))
                {
                    AppendFingerprintValue(canonical, contribution.ItemId.Value);
                    AppendFingerprintValue(canonical, contribution.Quantity.ToString(
                        CultureInfo.InvariantCulture));
                }
            }
            foreach (FacilityBufferPlannedOutputComponentSnapshot component in
                     slice.Source.RuntimeComponents
                         .OrderBy(value => value.ComponentTypeId, StringComparer.Ordinal)
                         .ThenBy(value => value.SchemaVersion)
                         .ThenBy(value => value.CanonicalFingerprint, StringComparer.Ordinal))
            {
                AppendFingerprintValue(canonical, component.ComponentTypeId);
                AppendFingerprintValue(canonical, component.SchemaVersion.ToString(
                    CultureInfo.InvariantCulture));
                AppendFingerprintValue(canonical, component.AffectsStacking ? "1" : "0");
                AppendFingerprintValue(canonical, component.CanonicalFingerprint);
            }
        }
        AppendFingerprintValue(canonical, totalQuantity.ToString(
            CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, totalMassGrams.ToString(
            CultureInfo.InvariantCulture));

        using SHA256 sha256 = SHA256.Create();
        byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(
            canonical.ToString()));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    private static void AppendFingerprintValue(
        StringBuilder target,
        string value)
    {
        string canonical = value ?? string.Empty;
        target.Append(canonical.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(canonical);
        target.Append('|');
    }

    internal static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static bool Fail(
        FacilityBufferMassAdmissionFailureCode code,
        string reason,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = code;
        failureReason = reason ?? string.Empty;
        return false;
    }
}

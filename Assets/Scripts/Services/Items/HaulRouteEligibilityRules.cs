using System;
using System.Collections.Generic;
using System.Linq;

internal static class HaulRouteEligibilityRules
{
    internal static bool IsExactWarehouseCustody(WorldItemStackRecord stack) =>
        stack != null
        && FacilityOutputExactRouteCustodyCodec.TryRead(
            stack.components,
            out FacilityOutputExactRouteCustodyMetadata custody)
        && custody.Phase == FacilityOutputExactRouteCustodyPhase.Routable
        && TryParseWarehouseId(custody.CurrentTargetDestinationId, out _);

    internal static bool RequiresExactWarehouseAdmissionsBeforeLease(
        IEnumerable<WorldItemStackRecord> stacks)
    {
        WorldItemStackRecord[] exact = (stacks
                ?? Array.Empty<WorldItemStackRecord>())
            .ToArray();
        return exact.Length > 0 && exact.All(IsExactWarehouseCustody);
    }

    internal static bool IsExactRouteDeliveryCandidate(
        WorldItemStackRecord stack,
        IFacilityOutputExactRouteOutboxQuery query,
        out FacilityOutputExactRouteFailure failure,
        IPreparedOutputExactDestinationAdmissionParticipant
            destinationAdmission = null)
    {
        failure = FacilityOutputExactRouteFailure.None;
        if (stack == null
            || !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                stack.components))
        {
            // Ordinary hauling deliberately has no dependency on the prepared
            // output lifecycle.
            return true;
        }
        if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                stack.components,
                out FacilityOutputExactRouteCustodyMetadata custody))
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.ComponentMismatch,
                "custody metadata is invalid",
                out failure);
        }
        if (custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
            || custody.CurrentDeliveryRevision < 0L
            || string.IsNullOrEmpty(
                custody.CurrentDeliveryRevisionFingerprint))
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.PhaseMismatch,
                "current delivery revision is not confirmed",
                out failure);
        }
        if (string.IsNullOrEmpty(custody.CurrentTargetDestinationId))
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.PendingRouteMissing,
                "warehouse selection is still pending",
                out failure);
        }
        if (!stack.hasDestinationPosition
            || !string.Equals(
                stack.destinationId,
                custody.CurrentTargetDestinationId,
                StringComparison.Ordinal)
            || stack.destinationPosition != custody.CurrentTargetPosition)
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                "physical target does not match the custody overlay",
                out failure);
        }
        if (query == null)
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.PendingRouteMissing,
                "current delivery query is unavailable",
                out failure);
        }

        FacilityOutputExactRoutePendingSnapshot[] matches =
            (query.CapturePendingRoutes()
                ?? Array.Empty<FacilityOutputExactRoutePendingSnapshot>())
            .Where(value => value?.Receipt != null
                && string.Equals(
                    value.Receipt.RouteOperationId,
                    custody.RouteOperationId,
                    StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            return FailExactRouteCandidate(
                matches.Length == 0
                    ? FacilityOutputExactRouteFailureCode.PendingRouteMissing
                    : FacilityOutputExactRouteFailureCode.OperationConflict,
                matches.Length == 0
                    ? "current route is absent from the outbox"
                    : "current route is duplicated in the outbox",
                out failure);
        }

        FacilityOutputExactRoutePendingSnapshot route = matches[0];
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery =
            route.DeliveryRevision;
        if (route.Phase != FacilityOutputExactRoutePhase.Routable)
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.PhaseMismatch,
                "outbox route is not routable",
                out failure);
        }
        if (delivery == null
            || !string.Equals(
                route.Receipt.PhysicalReceiptFingerprint,
                custody.PhysicalReceiptFingerprint,
                StringComparison.Ordinal)
            || delivery.Revision != custody.CurrentDeliveryRevision
            || !string.Equals(
                delivery.RevisionFingerprint,
                custody.CurrentDeliveryRevisionFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                delivery.RerouteOperationId,
                custody.CurrentDeliveryRerouteOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                delivery.TargetDestinationId,
                custody.CurrentTargetDestinationId,
                StringComparison.Ordinal)
            || delivery.TargetPositionX != custody.CurrentTargetPosition.x
            || delivery.TargetPositionY != custody.CurrentTargetPosition.y
            || !string.Equals(
                delivery.TargetAuthorityFingerprint,
                custody.CurrentTargetAuthorityFingerprint,
                StringComparison.Ordinal))
        {
            return FailExactRouteCandidate(
                FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                "outbox and custody delivery overlays differ",
                out failure);
        }

        if (TryParseWarehouseId(
                custody.CurrentTargetDestinationId,
                out _))
        {
            PreparedOutputExactDestinationAuthoritySnapshot authority = default;
            PreparedOutputExactDestinationAdmissionFailureCode authorityFailure =
                PreparedOutputExactDestinationAdmissionFailureCode.None;
            string authorityReason = string.Empty;
            if (custody.CurrentDeliveryRevision <= 0L
                || string.IsNullOrEmpty(
                    custody.CurrentTargetAuthorityFingerprint)
                || destinationAdmission == null
                || !destinationAdmission.TryCaptureTargetAuthority(
                    PreparedOutputExactDestinationTargetKind.Warehouse,
                    custody.CurrentTargetDestinationId,
                    custody.CurrentTargetPosition,
                    out authority,
                    out authorityFailure,
                    out authorityReason)
                || authority.Kind !=
                    PreparedOutputExactDestinationTargetKind.Warehouse
                || authority.Position != custody.CurrentTargetPosition
                || !string.Equals(
                    authority.DestinationId,
                    custody.CurrentTargetDestinationId,
                    StringComparison.Ordinal)
                || string.IsNullOrEmpty(authority.Fingerprint)
                || !string.Equals(
                    authority.Fingerprint,
                    authority.Fingerprint.Trim(),
                    StringComparison.Ordinal)
                || authority.CapacityRevision <= 0L
                || authority.MassAuthorityRevision <= 0L
                || authority.MaxMassGrams <= 0L
                || authority.ReservedMassGrams < 0L
                || authority.ReservedMassGrams > authority.MaxMassGrams)
            {
                return FailExactRouteCandidate(
                    FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                    "warehouse target authority is stale:"
                        + authorityFailure + ":" + authorityReason,
                    out failure);
            }

            // Warehouse admission reserve/release advances the live capacity
            // authority revision. Consequently, a route-minted target fingerprint
            // is a structural provenance token, not a stable view of the mutable
            // admission ledger. Candidate discovery still requires the exact route,
            // custody, destination id, position, and a canonical positive live
            // authority. Current capacity/revision is fenced atomically when the
            // admission token is reserved and again immediately before pickup.
        }
        return true;
    }

    private static bool FailExactRouteCandidate(
        FacilityOutputExactRouteFailureCode code,
        string reason,
        out FacilityOutputExactRouteFailure failure)
    {
        failure = new FacilityOutputExactRouteFailure(code, reason);
        return false;
    }

    internal static bool CanShareOpportunisticRoute(
        WorldItemStackRecord first,
        WorldItemStackRecord second)
    {
        bool firstHasCustody = first != null
            && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                first.components);
        bool secondHasCustody = second != null
            && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                second.components);
        if (!firstHasCustody && !secondHasCustody)
        {
            return true;
        }
        if (!firstHasCustody
            || !secondHasCustody
            || !FacilityOutputExactRouteCustodyCodec.TryRead(
                first.components,
                out FacilityOutputExactRouteCustodyMetadata firstCustody)
            || !FacilityOutputExactRouteCustodyCodec.TryRead(
                second.components,
                out FacilityOutputExactRouteCustodyMetadata secondCustody))
        {
            return false;
        }
        return string.Equals(
            firstCustody.RouteOperationId,
            secondCustody.RouteOperationId,
            StringComparison.Ordinal);
    }

    internal static bool TryParseWarehouseId(
        string destinationId,
        out string warehouseId)
    {
        warehouseId = string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (!destination.StartsWith(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }
        warehouseId = destination.Substring(
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix.Length);
        return warehouseId.Length > 0;
    }
}

using System;
using System.Collections.Generic;

public sealed partial class WildlifeCaptureRuntime
{
    [GameplayEntryPoint("WildlifeInfoPanel independent-haul role selection")]
    public bool TryAssignHaul(string wildlifeId, out string failureReason)
    {
        failureReason = string.Empty;
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.TryGet(id, out CapturedWildlifeState state))
        {
            failureReason = "우리에서 관리 중인 동물을 찾을 수 없습니다.";
            return false;
        }
        if (!TryReadRole(state, out CapturedWildlifeRoleId roleId)
            || !roleId.Equals(CapturedWildlifeRoleIds.None))
        {
            failureReason = "다른 역할을 먼저 해제해야 합니다.";
            return false;
        }
        if (!state.isTamed
            || state.escaped
            || state.transportState != CapturedWildlifeTransportState.Penned
            || !string.IsNullOrEmpty(state.assignedShowOrderId))
        {
            failureReason = "길들여져 우리에 등록된 동물만 독립 운반을 맡을 수 있습니다.";
            return false;
        }
        if (!speciesCatalog.TryGetSpecies(
                state.speciesId,
                out WildlifeSpeciesDefinition species)
            || species.HaulRoleProfile == null)
        {
            failureReason = "이 종은 독립 운반 역할을 지원하지 않습니다.";
            return false;
        }

        state.capabilityState = CapturedWildlifeCapabilityStateCodec.CreateHaul(
            CapturedWildlifeHaulPhase.Idle,
            clock.Time + species.HaulRoleProfile.DecisionIntervalSeconds);
        state.lastCareStatus = "독립 운반 역할 배정";
        return true;
    }

    [GameplayEntryPoint("WildlifeInfoPanel independent-haul role selection")]
    public bool TryClearHaul(string wildlifeId, out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetHaul(wildlifeId, out WildlifeHaulAssignmentSnapshot current)
            || !stateSession.TryGet(current.WildlifeId, out CapturedWildlifeState state))
        {
            failureReason = "독립 운반 역할이 배정되지 않았습니다.";
            return false;
        }

        if (current.Phase == CapturedWildlifeHaulPhase.Reserved)
        {
            wildlifeHaulItems.ReleaseUnpicked(current);
        }
        if (current.Phase is CapturedWildlifeHaulPhase.CargoOwned
                or CapturedWildlifeHaulPhase.ReleasePending)
        {
            if (current.Phase == CapturedWildlifeHaulPhase.CargoOwned)
            {
                WildlifeHaulAssignmentSnapshot pending = CopyWithPhase(
                    current,
                    CapturedWildlifeHaulPhase.ReleasePending);
                state.capabilityState = ToState(pending);
            }
            state.lastCareStatus = "독립 운반 해제 대기 · 보유 화물 배송 중";
            return true;
        }

        state.capabilityState = CapturedWildlifeCapabilityStateCodec.CreateNone();
        state.lastCareStatus = "독립 운반 역할 해제";
        state.nextCareAt = Math.Min(state.nextCareAt, clock.Time);
        return true;
    }

    public bool TryGetHaul(
        string wildlifeId,
        out WildlifeHaulAssignmentSnapshot assignment)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (stateSession.TryGet(id, out CapturedWildlifeState state)
            && CapturedWildlifeCapabilityStateCodec.TryReadHaul(
                state.capabilityState,
                out CapturedWildlifeHaulPayloadSnapshot decoded,
                out _))
        {
            assignment = WildlifeHaulAssignmentSnapshot.FromPayload(
                decoded,
                state.wildlifeId,
                state.penPosition);
            return true;
        }
        assignment = default;
        return false;
    }

    public void CopyHaulAssignments(
        List<WildlifeHaulAssignmentSnapshot> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
        destination.Clear();
        foreach (CapturedWildlifeState state in stateSession.Values)
        {
            if (CapturedWildlifeCapabilityStateCodec.TryRead(
                    state.capabilityState,
                    out CapturedWildlifeRoleId roleId,
                    out string failureReason))
            {
                if (roleId.Equals(CapturedWildlifeRoleIds.Haul))
                {
                    if (!TryGetHaul(state.wildlifeId, out var assignment))
                    {
                        throw new InvalidOperationException(
                            $"Captured wildlife '{state.wildlifeId}' haul payload could not be decoded.");
                    }
                    destination.Add(assignment);
                }
                continue;
            }
            throw new InvalidOperationException(
                $"Captured wildlife '{state.wildlifeId}' has invalid role state: {failureReason}");
        }
    }

    bool IWildlifeHaulRoleStateCommand.TryReplaceHaul(
        string wildlifeId,
        CapturedWildlifeHaulPhase expectedPhase,
        string expectedOperationId,
        WildlifeHaulAssignmentSnapshot replacement,
        string status)
    {
        if (!TryGetHaul(wildlifeId, out WildlifeHaulAssignmentSnapshot current)
            || current.Phase != expectedPhase
            || !string.Equals(current.OperationId, expectedOperationId ?? string.Empty,
                StringComparison.Ordinal)
            || !string.Equals(replacement.WildlifeId, current.WildlifeId,
                StringComparison.Ordinal)
            || !stateSession.TryGet(current.WildlifeId, out CapturedWildlifeState state))
        {
            return false;
        }
        state.capabilityState = ToState(replacement);
        state.lastCareStatus = status ?? string.Empty;
        return true;
    }

    void IWildlifeHaulRoleStateCommand.ClearRecoveredHaul(
        string wildlifeId,
        string status)
    {
        if (TryGetHaul(wildlifeId, out _)
            && stateSession.TryGet(wildlifeId, out CapturedWildlifeState state))
        {
            state.capabilityState = CapturedWildlifeCapabilityStateCodec.CreateNone();
            state.lastCareStatus = status ?? string.Empty;
            state.nextCareAt = Math.Min(state.nextCareAt, clock.Time);
        }
    }

    void IWildlifeHaulRoleStateCommand.RetireUnavailableHaul(
        string wildlifeId)
    {
        if (!TryGetHaul(wildlifeId, out _)
            || !stateSession.Remove(wildlifeId, out _))
        {
            return;
        }
        carriedParents.Remove(wildlifeId);
        nextPenReturnPathAt.Remove(wildlifeId);
        doorSubjects.SetCapturedWildlife(wildlifeId, false);
    }

    private static CapturedWildlifeCapabilityState ToState(
        WildlifeHaulAssignmentSnapshot value) =>
        CapturedWildlifeCapabilityStateCodec.CreateHaul(
            value.Phase,
            value.NextDecisionAt,
            value.OperationId,
            value.LeaseId,
            value.SourceStackId,
            value.CargoStackId,
            value.ItemId,
            value.ExpectedStackSignature,
            value.Quantity,
            value.PickupStandPosition,
            value.DestinationKind switch
            {
                WorldItemHaulDestinationKind.Warehouse =>
                    CapturedWildlifeHaulDestinationKind.Warehouse,
                WorldItemHaulDestinationKind.FacilityBuffer =>
                    CapturedWildlifeHaulDestinationKind.FacilityBuffer,
                _ => throw new InvalidOperationException(
                    $"Unknown wildlife haul destination '{value.DestinationKind}'.")
            },
            value.DestinationId,
            value.DeliveryPosition,
            value.DropPosition);

    private static WildlifeHaulAssignmentSnapshot CopyWithPhase(
        WildlifeHaulAssignmentSnapshot value,
        CapturedWildlifeHaulPhase phase) => new(
            value.WildlifeId,
            phase,
            value.NextDecisionAt,
            value.OperationId,
            value.LeaseId,
            value.SourceStackId,
            value.CargoStackId,
            value.ItemId,
            value.ExpectedStackSignature,
            value.Quantity,
            value.PickupStandPosition,
            value.DestinationKind,
            value.DestinationId,
            value.DeliveryPosition,
            value.DropPosition,
            value.PenPosition);
}

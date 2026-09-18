using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public sealed partial class WildlifeCaptureRuntime
{
    [GameplayEntryPoint(
        "WildlifeInfoPanel companion-owner selection and WIM029 live scenario")]
    public bool TryAssignCompanion(
        string wildlifeId,
        CharacterId ownerId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.TryGet(id, out CapturedWildlifeState state))
        {
            failureReason = "우리에서 관리 중인 동물을 찾을 수 없습니다.";
            return false;
        }
        if (!TryReadRole(state, out CapturedWildlifeRoleId currentRole)
            || (!currentRole.Equals(CapturedWildlifeRoleIds.None)
                && !currentRole.Equals(CapturedWildlifeRoleIds.Companion)))
        {
            failureReason = "현재 동물 역할 상태를 동행 역할로 바꿀 수 없습니다.";
            return false;
        }
        if (!CanAssignCompanion(state, ownerId, out _, out failureReason))
        {
            return false;
        }

        WildlifeCompanionRoleProfile profile =
            RequireCompanionProfile(state.speciesId);
        state.capabilityState =
            CapturedWildlifeCapabilityStateCodec.CreateCompanion(
                ownerId,
                clock.Time + profile.AttackCooldownSeconds);
        state.lastCareStatus = "동행 역할 배정 · 초기 공격 준비 중";
        return true;
    }

    [GameplayEntryPoint(
        "WildlifeInfoPanel companion-owner selection and WIM029 live scenario")]
    public bool TryClearCompanion(
        string wildlifeId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.TryGet(id, out CapturedWildlifeState state))
        {
            failureReason = "우리에서 관리 중인 동물을 찾을 수 없습니다.";
            return false;
        }

        if (!TryReadRole(state, out CapturedWildlifeRoleId roleId))
        {
            failureReason = "동물 역할 상태가 유효하지 않습니다.";
            return false;
        }
        if (!roleId.Equals(CapturedWildlifeRoleIds.Companion))
        {
            failureReason = "동행 역할이 배정되지 않았습니다.";
            return false;
        }

        ClearCompanionRole(state, "동행 역할 해제");
        return true;
    }

    public bool TryGetCompanion(
        string wildlifeId,
        out WildlifeCompanionAssignmentSnapshot assignment)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (stateSession.TryGet(id, out CapturedWildlifeState state)
            && CapturedWildlifeCapabilityStateCodec.TryReadCompanion(
                state.capabilityState,
                out CharacterId ownerId,
                out float nextAttackAt,
                out _))
        {
            assignment = new WildlifeCompanionAssignmentSnapshot(
                state.wildlifeId,
                ownerId,
                nextAttackAt,
                state.penPosition);
            return true;
        }

        assignment = default;
        return false;
    }

    public void CopyCompanionAssignments(
        List<WildlifeCompanionAssignmentSnapshot> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        foreach (CapturedWildlifeState state in stateSession.Values)
        {
            if (!CapturedWildlifeCapabilityStateCodec.TryRead(
                    state.capabilityState,
                    out CapturedWildlifeRoleId roleId,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    $"Captured wildlife '{state.wildlifeId}' has invalid role state: {failureReason}");
            }
            if (!roleId.Equals(CapturedWildlifeRoleIds.Companion))
            {
                continue;
            }
            if (!TryGetCompanion(state.wildlifeId, out var assignment))
            {
                throw new InvalidOperationException(
                    $"Captured wildlife '{state.wildlifeId}' companion payload could not be decoded.");
            }
            else
            {
                destination.Add(assignment);
            }
        }
    }

    public bool TryGetCompanionOwner(
        WildlifeActor wildlife,
        out CharacterActor owner)
    {
        owner = null;
        if (wildlife == null
            || !TryGetCompanion(wildlife.WildlifeId, out var assignment))
        {
            return false;
        }

        owner = FindOwner(assignment.OwnerId);
        return owner != null && !owner.IsDead;
    }

    bool IWildlifeCompanionRoleStateCommand.TryArmAttackCooldown(
        string wildlifeId,
        CharacterId expectedOwnerId,
        float expectedReadyAt,
        float nextAttackAt,
        out WildlifeCompanionAssignmentSnapshot assignment)
    {
        assignment = default;
        if (!TryGetCompanion(wildlifeId, out var current)
            || !current.OwnerId.Equals(expectedOwnerId)
            || Math.Abs(current.NextAttackAt - expectedReadyAt) > 0.0001f
            || clock.Time + 0.0001f < current.NextAttackAt
            || float.IsNaN(nextAttackAt)
            || float.IsInfinity(nextAttackAt)
            || nextAttackAt < clock.Time)
        {
            return false;
        }

        if (!stateSession.TryGet(wildlifeId, out CapturedWildlifeState state))
        {
            return false;
        }
        state.capabilityState =
            CapturedWildlifeCapabilityStateCodec.CreateCompanion(
                expectedOwnerId,
                nextAttackAt);
        assignment = new WildlifeCompanionAssignmentSnapshot(
            wildlifeId,
            expectedOwnerId,
            nextAttackAt,
            state.penPosition);
        return true;
    }

    void IWildlifeCompanionRoleStateCommand.ClearInvalidCompanion(
        string wildlifeId,
        string reason)
    {
        if (stateSession.TryGet(wildlifeId, out CapturedWildlifeState state))
        {
            ClearCompanionRole(state, reason);
        }
    }

    private bool CanAssignCompanion(
        CapturedWildlifeState state,
        CharacterId ownerId,
        out CharacterActor owner,
        out string failureReason)
    {
        owner = FindOwner(ownerId);
        failureReason = string.Empty;
        if (!ownerId.IsValid || owner == null || !IsEligibleOwner(owner))
        {
            failureReason = "활동 중인 정착지 직원을 동행 주인으로 선택해야 합니다.";
            return false;
        }
        if (!state.isTamed
            || state.escaped
            || state.transportState != CapturedWildlifeTransportState.Penned
            || !string.IsNullOrEmpty(state.assignedShowOrderId))
        {
            failureReason = "길들여져 우리에 등록된 동물만 동행할 수 있습니다.";
            return false;
        }
        if (!speciesCatalog.TryGetSpecies(
                state.speciesId,
                out WildlifeSpeciesDefinition species)
            || species.CompanionRoleProfile == null)
        {
            failureReason = "이 종은 동행 역할을 지원하지 않습니다.";
            return false;
        }

        int assigned = stateSession.Values.Count(candidate =>
            !string.Equals(
                candidate.wildlifeId,
                state.wildlifeId,
                StringComparison.Ordinal)
            && CapturedWildlifeCapabilityStateCodec.TryReadCompanion(
                candidate.capabilityState,
                out CharacterId candidateOwner,
                out _,
                out _)
            && candidateOwner.Equals(ownerId));
        if (assigned >= species.CompanionRoleProfile.MaximumCompanionsPerOwner)
        {
            failureReason = "직원 한 명에게는 동행 동물 한 마리만 배정할 수 있습니다.";
            return false;
        }
        return true;
    }

    private CharacterActor FindOwner(CharacterId ownerId) =>
        ownerId.IsValid
            ? world.Characters.FirstOrDefault(actor =>
                actor != null
                && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
                && id.Equals(ownerId))
            : null;

    internal static bool IsEligibleOwner(CharacterActor owner) =>
        owner != null
        && !owner.IsDead
        && owner.characterType == CharacterType.NPC
        && owner.CurrentLifecycleState == CharacterLifecycleState.Active
        && CharacterWorkRoleUtility.TryGetWork(owner, out _);

    private WildlifeCompanionRoleProfile RequireCompanionProfile(
        string speciesId)
    {
        if (speciesCatalog.TryGetSpecies(
                speciesId,
                out WildlifeSpeciesDefinition species)
            && species.CompanionRoleProfile != null)
        {
            return species.CompanionRoleProfile;
        }
        throw new InvalidOperationException(
            $"Species '{speciesId}' has no companion-role profile.");
    }

    private static bool TryReadRole(
        CapturedWildlifeState state,
        out CapturedWildlifeRoleId roleId) =>
        CapturedWildlifeCapabilityStateCodec.TryRead(
            state.capabilityState,
            out roleId,
            out _);

    private void ClearCompanionRole(
        CapturedWildlifeState state,
        string reason)
    {
        state.capabilityState =
            CapturedWildlifeCapabilityStateCodec.CreateNone();
        state.lastCareStatus = reason ?? string.Empty;
        state.nextCareAt = Math.Min(state.nextCareAt, clock.Time);
    }
}

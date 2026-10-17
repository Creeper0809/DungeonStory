using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeCompanionRoleRuntime : ITickable
{
    private const float CareThreshold = 0.45f;

    private readonly IWildlifeCompanionRoleStateCommand roles;
    private readonly WildlifeWorldServices world;
    private readonly WildlifeCombatServices combat;
    private readonly WildlifeExecutionServices execution;
    private readonly ICharacterCombatCommandRuntime commands;
    private readonly IDefenseEngagementStore engagements;
    private readonly ICombatAffiliationService affiliation;
    private readonly CombatCommandResultApplier resultApplier;
    private readonly List<WildlifeCompanionAssignmentSnapshot> assignments =
        new();
    private readonly Dictionary<string, float> nextDecisionAt =
        new(StringComparer.Ordinal);

    public WildlifeCompanionRoleRuntime(
        IWildlifeCompanionRoleStateCommand roles,
        WildlifeWorldServices world,
        WildlifeCombatServices combat,
        WildlifeExecutionServices execution,
        ICharacterCombatCommandRuntime commands,
        IDefenseEngagementStore engagements,
        ICombatAffiliationService affiliation,
        CombatCommandResultApplier resultApplier)
    {
        this.roles = roles ?? throw new ArgumentNullException(nameof(roles));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
        this.execution = execution
            ?? throw new ArgumentNullException(nameof(execution));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.engagements = engagements
            ?? throw new ArgumentNullException(nameof(engagements));
        this.affiliation = affiliation
            ?? throw new ArgumentNullException(nameof(affiliation));
        this.resultApplier = resultApplier
            ?? throw new ArgumentNullException(nameof(resultApplier));
    }

    public void Tick()
    {
        IGameClock clock = execution.Clock;
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        roles.CopyCompanionAssignments(assignments);
        HashSet<string> activeIds = assignments
            .Select(item => item.WildlifeId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string stale in nextDecisionAt.Keys
                     .Where(id => !activeIds.Contains(id))
                     .ToArray())
        {
            nextDecisionAt.Remove(stale);
        }

        foreach (WildlifeCompanionAssignmentSnapshot assignment in assignments)
        {
            TickCompanion(assignment, clock.Time);
        }
    }

    private void TickCompanion(
        WildlifeCompanionAssignmentSnapshot assignment,
        float now)
    {
        WildlifeActor actor = world.WorldRegistry.Wildlife.FirstOrDefault(item =>
            item != null
            && string.Equals(
                item.WildlifeId,
                assignment.WildlifeId,
                StringComparison.Ordinal));
        if (actor == null || !actor.IsAlive || actor.State != WildlifeState.Captured)
        {
            roles.ClearInvalidCompanion(
                assignment.WildlifeId,
                "동행 개체가 없어 역할 해제");
            return;
        }
        if (!world.Species.TryGetSpecies(
                actor.SpeciesId,
                out WildlifeSpeciesDefinition species)
            || species.CompanionRoleProfile == null)
        {
            throw new InvalidOperationException(
                $"Assigned companion species '{actor.SpeciesId}' has no companion profile.");
        }

        CharacterActor owner = FindOwner(assignment.OwnerId);
        if (owner == null || owner.IsDead)
        {
            RecordPathStatus(
                actor,
                ReturnToPen(actor, assignment, now),
                "우리 복귀");
            roles.ClearInvalidCompanion(
                assignment.WildlifeId,
                "동행 주인 소실로 역할 해제");
            return;
        }
        if (owner.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            if (!BeginDecision(
                    actor.WildlifeId,
                    now,
                    species.CompanionRoleProfile.DecisionIntervalSeconds))
            {
                return;
            }
            RecordPathStatus(
                actor,
                ReturnToPen(actor, assignment, now),
                "우리 복귀");
            return;
        }
        if (!WildlifeCaptureRuntime.IsEligibleOwner(owner))
        {
            RecordPathStatus(
                actor,
                ReturnToPen(actor, assignment, now),
                "우리 복귀");
            roles.ClearInvalidCompanion(
                assignment.WildlifeId,
                "동행 주인이 정착지를 떠나 역할 해제");
            return;
        }
        DefenseEngagement ownerEngagement = FindActiveOwnerEngagement(owner);
        bool ownerCombatControlled = ownerEngagement != null
            || commands.IsInCombatStance(owner);
        if (owner.IsAiPaused() && !ownerCombatControlled)
        {
            return;
        }

        WildlifeCompanionRoleProfile profile = species.CompanionRoleProfile;
        if (!BeginDecision(
                actor.WildlifeId,
                now,
                profile.DecisionIntervalSeconds))
        {
            return;
        }

        if (actor.Hunger >= CareThreshold || actor.Thirst >= CareThreshold)
        {
            RecordPathStatus(
                actor,
                ReturnToPen(actor, assignment, now),
                "돌봄 복귀");
            return;
        }

        CombatParticipantRef target = ResolveOwnerTarget(
            owner,
            ownerEngagement,
            profile);
        if (target.IsValid)
        {
            TickCombat(actor, assignment, target, profile, now);
            return;
        }

        int ownerDistance = Manhattan(actor.GridPosition, owner.GetNowXY());
        if (ownerDistance > profile.ResumeFollowDistanceCells)
        {
            RecordPathStatus(
                actor,
                TryMoveNear(
                    actor,
                    owner.GetNowXY(),
                    profile.CloseRadiusCells,
                    now),
                "주인 추종");
        }
    }

    private CombatParticipantRef ResolveOwnerTarget(
        CharacterActor owner,
        DefenseEngagement ownerEngagement,
        WildlifeCompanionRoleProfile profile)
    {
        CombatParticipantRef target = default;
        if (commands.TryGetCommand(owner, out CharacterCombatCommand command)
            && command.type == CombatCommandType.Attack)
        {
            target = FindParticipant(command.targetId);
        }

        if (!target.IsValid)
        {
            if (ownerEngagement?.IntruderActor != null)
            {
                target = new CombatParticipantRef(
                    ownerEngagement.IntruderActor);
            }
        }

        return target.IsValid
            && !target.IsDead
            && (!target.IsCharacter
                || !combat.BodyHealthQuery.GetSnapshot(target.Character).Downed)
            && Manhattan(owner.GetNowXY(), target.GridPosition)
                <= profile.OwnerTargetLeashCells
            && affiliation.GetRelationship(
                new CombatParticipantRef(owner),
                target) == CombatRelationship.Hostile
                ? target
                : default;
    }

    private DefenseEngagement FindActiveOwnerEngagement(
        CharacterActor owner) =>
        engagements.Engagements.FirstOrDefault(item =>
            item != null
            && item.IsActive
            && (item.LeadGuard == owner
                || item.ReserveGuard == owner
                || item.RangedGuard == owner
                || item.SecondaryRangedGuard == owner));

    private void TickCombat(
        WildlifeActor actor,
        WildlifeCompanionAssignmentSnapshot assignment,
        CombatParticipantRef target,
        WildlifeCompanionRoleProfile profile,
        float now)
    {
        CombatParticipantRef attacker = new(actor);
        if (affiliation.GetRelationship(attacker, target)
            != CombatRelationship.Hostile)
        {
            return;
        }

        int distance = Manhattan(actor.GridPosition, target.GridPosition);
        bool inMeleePosition = actor.GridPosition.y == target.GridPosition.y
            && Math.Abs(actor.GridPosition.x - target.GridPosition.x)
                == profile.MeleeRangeCells;
        if (!inMeleePosition)
        {
            RecordPathStatus(
                actor,
                TryMoveToMelee(
                    actor,
                    target.GridPosition,
                    now),
                "교전 위치");
            return;
        }
        if (now + 0.0001f < assignment.NextAttackAt)
        {
            return;
        }

        float nextAttackAt = now + profile.AttackCooldownSeconds;
        if (!roles.TryArmAttackCooldown(
                assignment.WildlifeId,
                assignment.OwnerId,
                assignment.NextAttackAt,
                nextAttackAt,
                out _))
        {
            return;
        }

        CombatWeaponSnapshot weapon = CreateCompanionWeapon(profile);
        CharacterBodyHealthSnapshot targetBody = target.IsCharacter
            ? combat.BodyHealthQuery.GetSnapshot(target.Character)
            : default;
        CombatAttackResult result = combat.Resolution.Resolve(
            new CombatAttackRequest(
                "wildlife-companion:"
                    + assignment.WildlifeId
                    + ":"
                    + nextAttackAt.ToString("R", CultureInfo.InvariantCulture),
                assignment.WildlifeId,
                target.Id,
                CombatRuntimeStatFactory.Create(actor),
                GetCombatStats(target),
                weapon,
                distance,
                CombatFireMode.Aimed,
                default,
                defenderDowned: target.IsCharacter && targetBody.Downed,
                defenderMeleeLocked: true,
                defenderSuppression: target.IsCharacter
                    ? targetBody.Suppression
                    : 0f,
                defenderArmor: target.IsCharacter
                    ? combat.Equipment.GetArmor(target.Id)
                    : null,
                defenderShield: target.IsCharacter
                    ? combat.Equipment.GetShield(target.Id)
                    : default));
        resultApplier.Apply(
            target,
            result,
            null,
            actor.DisplayName,
            CombatDamageType.Slash);
        resultApplier.ApplyArmorDurabilityDamage(result);
    }

    private static GridPathRequestStatus ReturnToPen(
        WildlifeActor actor,
        WildlifeCompanionAssignmentSnapshot assignment,
        float now)
    {
        return actor.RequestManagedCaptivePath(assignment.PenPosition, now);
    }

    private GridPathRequestStatus TryMoveNear(
        WildlifeActor actor,
        Vector2Int anchor,
        int radius,
        float now)
    {
        if (!world.Grid.TryGetGrid(out Grid grid))
        {
            return GridPathRequestStatus.Unreachable;
        }

        Vector2Int[] candidates = EnumerateNearby(anchor, radius)
            .Where(cell => IsValidCompanionTargetCell(grid, cell))
            .OrderBy(cell => Manhattan(actor.GridPosition, cell))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .ToArray();
        return RequestFirstReachablePath(actor, candidates, now);
    }

    private GridPathRequestStatus TryMoveToMelee(
        WildlifeActor actor,
        Vector2Int target,
        float now)
    {
        if (!world.Grid.TryGetGrid(out Grid grid))
        {
            return GridPathRequestStatus.Unreachable;
        }

        Vector2Int[] candidates = new[]
            {
                target + Vector2Int.left,
                target + Vector2Int.right
            }
            .Where(cell => IsValidCompanionTargetCell(grid, cell))
            .OrderBy(cell => Manhattan(actor.GridPosition, cell))
            .ThenBy(cell => cell.x)
            .ToArray();
        return RequestFirstReachablePath(actor, candidates, now);
    }

    private static GridPathRequestStatus RequestFirstReachablePath(
        WildlifeActor actor,
        IReadOnlyList<Vector2Int> candidates,
        float now)
    {
        for (int index = 0; index < candidates.Count; index++)
        {
            GridPathRequestStatus status = actor.RequestManagedCaptivePath(
                candidates[index],
                now);
            if (status != GridPathRequestStatus.Unreachable)
            {
                return status;
            }
        }

        return GridPathRequestStatus.Unreachable;
    }

    private static void RecordPathStatus(
        WildlifeActor actor,
        GridPathRequestStatus status,
        string purpose)
    {
        string reason = status switch
        {
            GridPathRequestStatus.Pending => $"동행 경로 대기 · {purpose}",
            GridPathRequestStatus.Reachable => $"동행 이동 · {purpose}",
            _ => $"동행 경로 차단 · {purpose}"
        };
        actor.SetIntent(WildlifeIntent.Wander, reason);
    }

    private static IEnumerable<Vector2Int> EnumerateNearby(
        Vector2Int anchor,
        int radius)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int distance = Math.Abs(x) + Math.Abs(y);
                if (distance > 0 && distance <= radius)
                {
                    yield return anchor + new Vector2Int(x, y);
                }
            }
        }
    }

    private static bool IsValidCompanionTargetCell(
        Grid grid,
        Vector2Int cell)
    {
        GridCell gridCell = grid?.GetGridCell(cell);
        return gridCell != null
            && grid.IsWalkable(cell)
            && !gridCell.HasOccupantInLayer(GridLayer.Wildlife);
    }

    private CharacterActor FindOwner(CharacterId ownerId) =>
        world.WorldRegistry.Characters.FirstOrDefault(actor =>
            actor != null
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            && id.Equals(ownerId));

    private CombatParticipantRef FindParticipant(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return default;
        }

        CharacterActor character = world.WorldRegistry.Characters.FirstOrDefault(actor =>
            actor != null
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            && string.Equals(characterId.Value, id, StringComparison.Ordinal));
        if (character != null)
        {
            return new CombatParticipantRef(character);
        }

        WildlifeActor wildlife = world.WorldRegistry.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(actor.WildlifeId, id, StringComparison.Ordinal));
        return wildlife != null ? new CombatParticipantRef(wildlife) : default;
    }

    private CombatStatSnapshot GetCombatStats(CombatParticipantRef target) =>
        target.IsCharacter
            ? CombatRuntimeStatFactory.Create(
                target.Character,
                combat.BodyHealthQuery.GetSnapshot(target.Character),
                combat.Performance)
            : CombatRuntimeStatFactory.Create(target.Wildlife);

    private bool BeginDecision(
        string wildlifeId,
        float now,
        float interval)
    {
        if (nextDecisionAt.TryGetValue(wildlifeId, out float next)
            && now + 0.0001f < next)
        {
            return false;
        }
        nextDecisionAt[wildlifeId] = now + interval;
        return true;
    }

    private static CombatWeaponSnapshot CreateCompanionWeapon(
        WildlifeCompanionRoleProfile profile) =>
        new(
            "combat:wildlife-companion",
            string.Empty,
            CombatEquipmentKind.MeleeWeapon,
            new MeleeStrikeVerb
            {
                attackTime = profile.AttackCooldownSeconds,
                baseDamage = profile.BaseAttackDamage,
                penetration = profile.BaseAttackDamage * 0.2f,
                damageType = CombatDamageType.Slash,
                tracking = 0.08f
            },
            new[]
            {
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Contact,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                }
            },
            profile.MeleeRangeCells,
            CombatEquipmentQuality.Normal,
            string.Empty,
            0,
            0,
            0f,
            false,
            false,
            false);

    private static int Manhattan(Vector2Int a, Vector2Int b) =>
        Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
}

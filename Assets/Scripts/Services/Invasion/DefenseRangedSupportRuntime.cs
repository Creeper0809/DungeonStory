using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using static DefenseRangedSupportAccess;
using UnityEngine;

internal sealed class DefenseRangedSupportRuntime
{
    private readonly IDefenseCombatExecutor combatExecutor;
    private readonly ICombatLineOfSightService lineOfSight;
    private readonly ICombatAmmoResupplyRuntime ammoResupply;
    private readonly IGameClock clock;
    private readonly DefenseRangedPositionPlanner positionPlanner;
    private readonly ICharacterPerformanceQuery performance;

    public DefenseRangedSupportRuntime(
        DefenseEngagementWorldServices world,
        DefenseEngagementCombatServices combat,
        DefenseRangedPositionPlanner positionPlanner)
    {
        clock = (world ?? throw new ArgumentNullException(nameof(world))).Clock;
        DefenseEngagementCombatServices requiredCombat = combat
            ?? throw new ArgumentNullException(nameof(combat));
        combatExecutor = requiredCombat.Executor;
        lineOfSight = requiredCombat.LineOfSight;
        ammoResupply = requiredCombat.AmmoResupply;
        performance = requiredCombat.Performance;
        this.positionPlanner = positionPlanner
            ?? throw new ArgumentNullException(nameof(positionPlanner));
    }

    public void TryFill(
        Grid grid,
        DefenseEngagement engagement,
        Func<IEnumerable<CharacterActor>> eligibleGuards,
        Func<CharacterActor, Vector2Int, bool> isReserved,
        Action<CharacterActor, string> prepareGuard,
        Action<DefenseEngagement, string, bool> releaseGuard)
    {
        if (grid == null
            || engagement == null
            || (engagement.RangedGuard != null
                && engagement.SecondaryRangedGuard != null)
            || engagement.IntruderActor == null
            || engagement.IntruderActor.IsDead)
        {
            return;
        }

        bool secondary = engagement.RangedGuard != null;
        foreach (CharacterActor candidate in eligibleGuards()
            .Where(combatExecutor.HasActiveRangedWeapon)
            .Where(candidate => candidate != engagement.RangedGuard
                && candidate != engagement.SecondaryRangedGuard)
            .OrderByDescending(candidate => performance.Evaluate(
                candidate,
                "performance:combat:ranged-hit").Value)
            .ThenBy(candidate => Manhattan(
                candidate.GetNowXY(),
                engagement.IntruderActor.GetNowXY())))
        {
            if (!positionPlanner.TryFind(
                grid,
                engagement,
                candidate,
                isReserved,
                out Vector2Int cell,
                out Queue<GridMoveStep> path))
            {
                continue;
            }

            SetRangedGuard(engagement, secondary, candidate);
            SetRangedCell(engagement, secondary, cell);
            SetRangedArrived(engagement, secondary, candidate.GetNowXY() == cell);
            SetNextRangedReplanAt(engagement, secondary, clock.Time + 0.75f);
            prepareGuard(candidate, "엄폐 사격 위치로 이동");
            if (GetRangedArrived(engagement, secondary))
            {
                SetStatus(candidate, "엄폐 사격 준비");
            }
            else
            {
                StartMovement(
                    grid,
                    engagement,
                    candidate,
                    cell,
                    path,
                    secondary,
                    releaseGuard);
            }

            return;
        }
    }

    public void StartMovement(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor guard,
        Vector2Int target,
        Queue<GridMoveStep> initialPath,
        bool secondary,
        Action<DefenseEngagement, string, bool> releaseGuard)
    {
        if (guard == null || guard.IsDead)
        {
            return;
        }

        Coroutine movement = guard.StartCoroutine(RunMovement(
            grid,
            engagement,
            guard,
            target,
            initialPath,
            secondary,
            releaseGuard));
        SetRangedMovement(engagement, secondary, movement);
    }

    public void Tick(
        Grid grid,
        DefenseEngagement engagement,
        bool secondary,
        Func<CharacterActor, Vector2Int, bool> isReserved,
        Action<DefenseEngagement, string, bool> releaseGuard,
        Action<DefenseEngagement> resolveIntruder)
    {
        CharacterActor guard = GetRangedGuard(engagement, secondary);
        CharacterActor intruder = engagement?.IntruderActor;
        if (grid == null || guard == null || intruder == null)
        {
            return;
        }

        if (guard.IsDead)
        {
            releaseGuard(engagement, "원거리 경비 쓰러짐", secondary);
            return;
        }

        if (!GetRangedArrived(engagement, secondary) || intruder.IsDead)
        {
            return;
        }

        if (!combatExecutor.TryGetActiveRangedWeapon(guard, out CombatWeaponSnapshot weapon))
        {
            releaseGuard(engagement, "사용 가능한 원거리 무기 없음", secondary);
            return;
        }

        Vector2Int guardCell = guard.GetNowXY();
        Vector2Int intruderCell = intruder.GetNowXY();
        int distance = Manhattan(guardCell, intruderCell);
        CombatLineOfSightResult sight = lineOfSight.Evaluate(
            grid,
            guardCell,
            intruderCell,
            GetPersistentId(guard),
            GetPersistentId(intruder));
        if (distance < 2 || distance > weapon.MaximumRange || !sight.HasLineOfSight)
        {
            if (clock.Time >= GetNextRangedReplanAt(engagement, secondary)
                && positionPlanner.TryFind(
                    grid,
                    engagement,
                    guard,
                    isReserved,
                    out Vector2Int nextCell,
                    out Queue<GridMoveStep> path)
                && nextCell != guardCell)
            {
                SetRangedCell(engagement, secondary, nextCell);
                SetRangedArrived(engagement, secondary, false);
                SetNextRangedReplanAt(engagement, secondary, clock.Time + 0.75f);
                SetStatus(guard, "사선 재확보");
                StartMovement(
                    grid,
                    engagement,
                    guard,
                    nextCell,
                    path,
                    secondary,
                    releaseGuard);
            }

            return;
        }

        CharacterCombatLoadoutProfile profile = combatExecutor.GetActiveProfile(guard);
        if (profile?.holdFire == true)
        {
            SetStatus(guard, "사격 중지");
            return;
        }

        if (sight.FriendlyFireRisk)
        {
            SetStatus(guard, "아군 사선 대기");
            return;
        }

        if (weapon.RequiresAmmo && weapon.LoadedAmmo <= 0)
        {
            HandleEmptyWeapon(engagement, guard, weapon, secondary, releaseGuard);
            return;
        }

        if (clock.Time < GetNextRangedAttackAt(engagement, secondary))
        {
            return;
        }

        CombatFireMode mode = combatExecutor.ResolveSupportedFireMode(
            weapon,
            profile?.fireMode ?? CombatFireMode.Aimed);
        DefenseCombatExecutionResult result = combatExecutor.ExecuteRanged(
            grid,
            engagement,
            guard,
            intruder,
            weapon,
            mode,
            sight,
            distance);
        SetStatus(guard, result.StatusText);
        if (result.DefenderDefeated)
        {
            resolveIntruder(engagement);
            return;
        }

        SetNextRangedAttackAt(
            engagement,
            secondary,
            clock.Time + combatExecutor.GetAttackInterval(guard, weapon, mode));
    }

    private IEnumerator RunMovement(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor guard,
        Vector2Int target,
        Queue<GridMoveStep> initialPath,
        bool secondary,
        Action<DefenseEngagement, string, bool> releaseGuard)
    {
        AbilityMove move = guard.GetAbility<AbilityMove>();
        if (move == null)
        {
            releaseGuard(engagement, "원거리 경비 이동 능력 없음", secondary);
            yield break;
        }

        Queue<GridMoveStep> path = initialPath;
        for (int attempt = 0; attempt < 3 && guard != null && !guard.IsDead; attempt++)
        {
            if (guard.GetNowXY() == target)
            {
                break;
            }

            path ??= grid.GetMovePathTo(guard.GetNowXY(), target);
            if (path == null || path.Count == 0)
            {
                break;
            }

            yield return move.MoveByPath(path);
            path = null;
        }

        bool arrived = guard != null && !guard.IsDead && guard.GetNowXY() == target;
        SetRangedMovement(engagement, secondary, null);
        SetRangedArrived(engagement, secondary, arrived);
        if (arrived)
        {
            SetStatus(guard, "엄폐 사격 준비");
        }
        else
        {
            releaseGuard(engagement, "원거리 사격 위치 경로 막힘", secondary);
        }
    }

    private void HandleEmptyWeapon(
        DefenseEngagement engagement,
        CharacterActor guard,
        CombatWeaponSnapshot weapon,
        bool secondary,
        Action<DefenseEngagement, string, bool> releaseGuard)
    {
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(guard);
        if (combatExecutor.TryReload(
            guard,
            weapon,
            inventory,
            out float reloadDuration))
        {
            SetNextRangedAttackAt(engagement, secondary, clock.Time + reloadDuration);
            DefenseCombatPresentation.Ensure(guard)?.PlayReload(weapon, reloadDuration);
            SetStatus(guard, "재장전 중");
            return;
        }

        if (combatExecutor.TrySwitchFallbackWeapon(guard, out CombatWeaponSnapshot fallback))
        {
            if (fallback.IsRanged)
            {
                SetStatus(guard, "장전된 백업 무기로 교체");
            }
            else
            {
                releaseGuard(engagement, "근접 백업 무기로 교체", secondary);
            }

            return;
        }

        releaseGuard(engagement, "탄약 재보급", secondary);
        ammoResupply.TryRequestAmmoResupply(guard, out _);
    }

    private static void SetStatus(CharacterActor actor, string status)
    {
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(status, true);
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId ?? string.Empty;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;
public sealed class DefenseEngagementRuntime :
    IDefenseEngagementRuntime,
    IInitializable,
    ITickable,
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("DefenseEngagementRuntime.Tick");

    private readonly IStaffWorkforceQueryService workforceQuery;
    private readonly IGridSystemProvider gridProvider;
    private readonly IDefenseResponsePolicyRuntime policyRuntime;
    private readonly IInvasionIntruderContext invasionContext;
    private readonly InvasionDirectorRuntime director;
    private readonly IInvasionOwnerEvacuationService ownerEvacuation;
    private readonly IDefenseCombatExecutor combatExecutor;
    private readonly ICombatAmmoResupplyRuntime ammoResupply;
    private readonly IDefenseTacticalCoordinator tacticalCoordinator;
    private readonly IDefenseEngagementStore engagementStore;
    private readonly IGameEventBus gameEventBus;
    private readonly DefenseGuardControlRuntime guardControl;
    private readonly IGameClock gameClock;
    private readonly DefenseInterceptPlanner interceptPlanner = new DefenseInterceptPlanner();
    private readonly DefenseRangedPositionPlanner rangedPositionPlanner;
    private readonly DefenseRangedSupportRuntime rangedSupport;
    private readonly DefenseEngagementPersistence persistence;
    private readonly DefenseEngagementCombatRuntime combatRuntime;
    private IDisposable downedSubscription, deathSubscription, breachSubscription, resolvedSubscription;
    public DefenseEngagementRuntime(
        DefenseEngagementWorldServices world,
        DefenseEngagementCombatServices combat)
    {
        DefenseEngagementWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        DefenseEngagementCombatServices requiredCombat = combat
            ?? throw new ArgumentNullException(nameof(combat));
        workforceQuery = requiredWorld.Workforce;
        gridProvider = requiredWorld.Grid;
        invasionContext = requiredWorld.Invasion;
        director = requiredWorld.Director;
        ownerEvacuation = requiredWorld.OwnerEvacuation;
        gameEventBus = requiredWorld.Events;
        gameClock = requiredWorld.Clock;
        policyRuntime = requiredCombat.Policy;
        combatExecutor = requiredCombat.Executor;
        ammoResupply = requiredCombat.AmmoResupply;
        tacticalCoordinator = requiredCombat.Tactics;
        engagementStore = requiredCombat.Store;
        guardControl = new DefenseGuardControlRuntime();
        rangedPositionPlanner = new DefenseRangedPositionPlanner(requiredCombat);
        rangedSupport = new DefenseRangedSupportRuntime(
            requiredWorld,
            requiredCombat,
            rangedPositionPlanner);
        persistence = new DefenseEngagementPersistence(requiredWorld, requiredCombat);
        combatRuntime = new DefenseEngagementCombatRuntime(
            requiredWorld,
            requiredCombat,
            guardControl);
    }

    public IInvasionOwnerEvacuationService OwnerEvacuation => ownerEvacuation;
    public IDefenseResponsePolicyRuntime PolicyRuntime => policyRuntime;
    public IReadOnlyList<DefenseEngagement> ActiveEngagements => engagementStore.Engagements;

    public string BuildDebugSummary()
    {
        List<string> lines = new List<string>
        {
            $"engagements={engagementStore.Engagements.Count}"
        };
        foreach (CharacterActor actor in workforceQuery.FindActiveWorkers())
        {
            if (actor == null)
            {
                continue;
            }

            bool hasWork = CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work);
            WorkPriorityLevel guardPriority = hasWork
                ? work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard)
                : WorkPriorityLevel.Off;
            DefenseResponsePolicyData policy = policyRuntime.GetPolicy(actor);
            lines.Add(
                $"{actor.Identity?.DisplayName ?? actor.name}:owner={actor.IsOwner},work={hasWork}," +
                $"offDuty={(hasWork && work.IsOffDuty)},guard={guardPriority},assigned={IsGuardAssigned(actor)}," +
                $"hp={GetHealthRatio(actor):0.00},auto={policy?.autoRespond ?? false}");
        }

        return string.Join(" | ", lines);
    }

    public void Initialize()
    {
        resolvedSubscription = gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
        breachSubscription = gameEventBus.Subscribe<InvasionDungeonBreachedEvent>(OnDungeonBreached);
        downedSubscription = gameEventBus.Subscribe<CharacterBodyHealthDownedEvent>(
            gameEvent => NotifyActorDowned(gameEvent.Actor));
        deathSubscription = gameEventBus.Subscribe<CharacterDeathEvent>(OnCharacterDeath);
    }

    public void Dispose()
    {
        resolvedSubscription?.Dispose();
        breachSubscription?.Dispose();
        downedSubscription?.Dispose();
        deathSubscription?.Dispose();
        breachSubscription = null;
        downedSubscription = null;
        deathSubscription = null;
        resolvedSubscription = null;
        foreach (DefenseEngagement engagement in engagementStore.Engagements.ToArray())
        {
            CompleteEngagement(engagement, releaseIntruder: false);
        }

        ReleaseOrphanedDefenseGuards(releaseAll: true);
        engagementStore.ClearAll();
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        if (!gridProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        IReadOnlyList<DefenseEngagement> engagements =
            engagementStore.Engagements;
        for (int index = engagements.Count - 1; index >= 0; index--)
        {
            TickEngagement(grid, engagements[index]);
        }

        if (director == null)
        {
            return;
        }

        IReadOnlyList<InvasionIntruderRuntime> intruders =
            director.ActiveIntruders;
        for (int index = intruders.Count - 1; index >= 0; index--)
        {
            InvasionIntruderRuntime intruder = intruders[index];
            if (intruder == null
                || intruder.State == InvasionIntruderState.Finished
                || intruder.IntruderActor == null
                || intruder.IntruderActor.IsDead
                || !intruder.HasBreachedDungeonInterior
                || TryGetEngagement(intruder, out _))
            {
                continue;
            }

            if (!TryDispatchForIntruder(grid, intruder))
            {
                TryStartOwnerDefenseWhenReady(grid, intruder);
            }
        }
    }

    private void OnDungeonBreached(InvasionDungeonBreachedEvent eventType)
    {
        InvasionIntruderRuntime intruder = eventType.intruderRuntime;
        if (intruder == null
            || !intruder.HasBreachedDungeonInterior
            || !gridProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        TryDispatchForIntruder(grid, intruder);
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        foreach (DefenseEngagement engagement in engagementStore.Engagements
            .Where(item => item == null
                || item.Intruder == null
                || item.Intruder.State == InvasionIntruderState.Finished
                || item.IntruderActor == null
                || item.IntruderActor.IsDead)
            .ToArray())
        {
            CompleteEngagement(engagement, releaseIntruder: false);
        }

        ReleaseOrphanedDefenseGuards(releaseAll: false);
    }

    private void OnCharacterDeath(CharacterDeathEvent eventType)
    {
        foreach (DefenseEngagement engagement in engagementStore.Engagements.ToArray())
        {
            if (HasCharacterId(engagement.IntruderActor, eventType.CharacterId))
            {
                ResolveIntruderDefeated(engagement);
            }
            else if (HasCharacterId(engagement.LeadGuard, eventType.CharacterId))
            {
                if (engagement.IsOwnerFinalDefense)
                {
                    ResolveOwnerDefeated(engagement);
                }
                else
                {
                    HandleLeadLost(engagement, "선두 경비 쓰러짐");
                }
            }
            else if (HasCharacterId(engagement.ReserveGuard, eventType.CharacterId))
            {
                ReleaseGuard(engagement.ReserveGuard, engagement.ReserveMovement, true);
                engagement.ReserveGuard = null;
                engagement.ReserveMovement = null;
                engagement.ReserveArrived = false;
                engagement.StatusText = "예비 경비 쓰러짐";
            }
            else if (HasCharacterId(engagement.RangedGuard, eventType.CharacterId))
            {
                ReleaseRangedGuard(engagement, "원거리 경비 쓰러짐", secondary: false);
            }
            else if (HasCharacterId(
                engagement.SecondaryRangedGuard,
                eventType.CharacterId))
            {
                ReleaseRangedGuard(engagement, "원거리 경비 쓰러짐", secondary: true);
            }
        }
    }

    private static bool HasCharacterId(
        CharacterActor actor,
        CharacterId expected) =>
        CharacterPersistentIdentity.TryGet(actor, out CharacterId actual)
        && actual.Equals(expected);

    public void NotifyActorDowned(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        foreach (DefenseEngagement engagement in engagementStore.Engagements.ToArray())
        {
            if (engagement.IntruderActor == actor)
            {
                ResolveIntruderDefeated(engagement);
            }
            else if (engagement.LeadGuard == actor)
            {
                if (engagement.IsOwnerFinalDefense)
                {
                    ResolveOwnerDefeated(engagement);
                }
                else
                {
                    HandleLeadLost(engagement, "선두 경비 쓰러짐");
                }
            }
            else if (engagement.ReserveGuard == actor)
            {
                ReleaseGuard(engagement.ReserveGuard, engagement.ReserveMovement, true);
                engagement.ReserveGuard = null;
                engagement.ReserveMovement = null;
                engagement.ReserveArrived = false;
                engagement.StatusText = "예비 경비 쓰러짐";
            }
            else if (engagement.RangedGuard == actor)
            {
                ReleaseRangedGuard(engagement, "원거리 경비 쓰러짐", secondary: false);
            }
            else if (engagement.SecondaryRangedGuard == actor)
            {
                ReleaseRangedGuard(engagement, "원거리 경비 쓰러짐", secondary: true);
            }
        }
    }

    public bool TryGetEngagement(
        InvasionIntruderRuntime intruder,
        out DefenseEngagement engagement)
    {
        engagement = engagementStore.Engagements.FirstOrDefault(item => item != null
            && item.IsActive
            && item.Intruder == intruder);
        return engagement != null;
    }

    public bool TryGetActorDefenseStatus(
        CharacterActor actor,
        out DefenseEngagement engagement,
        out string role,
        out string status)
    {
        engagement = null;
        role = string.Empty;
        status = string.Empty;
        if (actor == null)
        {
            return false;
        }

        engagement = engagementStore.Engagements.FirstOrDefault(item => item != null
            && item.IsActive
            && (item.LeadGuard == actor
                || item.ReserveGuard == actor
                || item.RangedGuard == actor
                || item.SecondaryRangedGuard == actor
                || item.IntruderActor == actor));
        if (engagement == null)
        {
            return false;
        }

        role = engagement.LeadGuard == actor
            ? engagement.IsOwnerFinalDefense ? "최종 방어자" : "선두 경비"
            : engagement.ReserveGuard == actor
                ? "예비 경비"
                : engagement.RangedGuard == actor
                    ? "원거리 경비"
                    : engagement.SecondaryRangedGuard == actor
                        ? "원거리 경비 2"
                : "침입자";
        status = engagement.StatusText;
        return true;
    }

    public bool IsCellReservedForOther(CharacterActor actor, Vector2Int cell)
    {
        // Evacuation is a critical movement and starts before the defense line is formed.
        // It must not be stranded by reservations created a frame later for responding guards.
        if (actor != null
            && actor == ownerEvacuation.Owner
            && ownerEvacuation.IsEvacuating
            && !ownerEvacuation.HasReachedTarget)
        {
            return false;
        }

        if (tacticalCoordinator.IsReservedForOther(GetPersistentId(actor), cell))
        {
            return true;
        }

        foreach (DefenseEngagement engagement in engagementStore.Engagements)
        {
            if (engagement == null || !engagement.IsActive)
            {
                continue;
            }

            if (cell == engagement.IntruderStopCell
                && actor != engagement.IntruderActor)
            {
                return true;
            }

            if (cell == engagement.GuardCell
                && actor != engagement.LeadGuard
                && !(engagement.State == DefenseEngagementState.Switching
                    && actor == engagement.ReserveGuard))
            {
                return true;
            }

            if (engagement.HasReserveCell
                && cell == engagement.ReserveCell
                && actor != engagement.ReserveGuard
                && !(engagement.State == DefenseEngagementState.Switching
                    && actor == engagement.LeadGuard))
            {
                return true;
            }

            if (cell == engagement.RangedCell
                && actor != engagement.RangedGuard
                && engagement.RangedGuard != null)
            {
                return true;
            }

            if (cell == engagement.SecondaryRangedCell
                && actor != engagement.SecondaryRangedGuard
                && engagement.SecondaryRangedGuard != null)
            {
                return true;
            }
        }

        return false;
    }

    public bool ShouldHoldIntruder(InvasionIntruderRuntime intruder)
    {
        if (!TryGetEngagement(intruder, out DefenseEngagement engagement))
        {
            return false;
        }

        if (engagement.State == DefenseEngagementState.Engaged
            || engagement.State == DefenseEngagementState.Switching)
        {
            return true;
        }

        if (engagement.LeadArrived
            && engagement.IntruderActor != null
            && engagement.IntruderActor.GetNowXY() == engagement.IntruderStopCell)
        {
            BeginEngagement(engagement);
            return engagement.State == DefenseEngagementState.Engaged;
        }

        return false;
    }

    public bool CanIntruderAdvanceTo(InvasionIntruderRuntime intruder, Vector2Int nextCell)
    {
        if (intruder == null)
        {
            return false;
        }

        foreach (DefenseEngagement other in engagementStore.Engagements)
        {
            if (other == null || !other.IsActive || other.Intruder == intruder)
            {
                continue;
            }

            if (nextCell == other.IntruderStopCell
                || nextCell == other.GuardCell
                || (other.HasReserveCell && nextCell == other.ReserveCell))
            {
                return false;
            }
        }

        if (!TryGetEngagement(intruder, out DefenseEngagement engagement)
            && intruder.HasBreachedDungeonInterior
            && ownerEvacuation.IsEvacuating
            && ownerEvacuation.HasReachedTarget
            && ownerEvacuation.Owner != null
            && nextCell == ownerEvacuation.Owner.GetNowXY()
            && TryBeginOwnerFinalDefense(intruder, ownerEvacuation.Owner))
        {
            return false;
        }

        if (engagement == null)
        {
            return true;
        }

        Vector2Int current = intruder.IntruderActor.GetNowXY();
        if (engagement.State == DefenseEngagementState.Engaged
            || engagement.State == DefenseEngagementState.Switching)
        {
            return false;
        }

        if (current == engagement.IntruderStopCell && engagement.LeadArrived)
        {
            BeginEngagement(engagement);
            return false;
        }

        if (current == engagement.IntruderStopCell || nextCell == engagement.GuardCell)
        {
            CollapseFront(engagement, "경비가 늦어 저지 지점을 놓침");
            return true;
        }

        return true;
    }

    public bool TryAssignManual(
        CharacterActor defender,
        InvasionIntruderRuntime intruder,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (defender == null || defender.IsDead || intruder == null || intruder.IntruderActor == null)
        {
            failureReason = "유효한 경비와 침입자가 필요합니다.";
            return false;
        }

        if (!intruder.HasBreachedDungeonInterior)
        {
            failureReason = "침입자가 아직 외부에 있습니다. 던전 내부에 진입한 뒤 저지할 수 있습니다.";
            return false;
        }

        if (defender.IsOwner)
        {
            failureReason = "사장은 경비 전선이 무너진 뒤에만 싸웁니다.";
            return false;
        }

        if (!gridProvider.TryGetGrid(out Grid grid)
            || !invasionContext.TryGetOwner(out CharacterActor owner))
        {
            failureReason = "저지 경로를 계산할 수 없습니다.";
            return false;
        }

        if (TryGetEngagement(intruder, out DefenseEngagement current))
        {
            if (current.LeadGuard == defender || current.ReserveGuard == defender)
            {
                return true;
            }

            if (current.ReserveGuard == null
                && TryAssignReserve(grid, current, defender, forced: true))
            {
                return true;
            }

            failureReason = "이 침입자의 선두와 예비 경비가 이미 정해졌습니다.";
            return false;
        }

        if (!interceptPlanner.TryCreatePlan(
                grid,
                intruder,
                defender,
                owner.GetNowXY(),
                interceptPlanner.BuildUnavailableCells(engagementStore.Engagements),
                out DefenseInterceptPlan plan))
        {
            failureReason = "침입자보다 먼저 도착할 안전한 저지 칸이 없습니다.";
            return false;
        }

        CreateEngagement(grid, intruder, defender, plan, forced: true);
        return true;
    }

    public bool TryBeginOwnerFinalDefense(InvasionIntruderRuntime intruder, CharacterActor owner)
    {
        if (intruder == null
            || owner == null
            || owner.IsDead
            || !intruder.HasBreachedDungeonInterior
            || TryGetEngagement(intruder, out _)
            || HasCombatCapableGuard()
            || !ownerEvacuation.HasReachedTarget
            || !gridProvider.TryGetGrid(out Grid grid)
            || !interceptPlanner.TryCreateOwnerFinalPlan(
                grid,
                intruder,
                owner,
                interceptPlanner.BuildUnavailableCells(engagementStore.Engagements),
                out DefenseInterceptPlan plan))
        {
            return false;
        }

        DefenseEngagement engagement = CreateEngagement(
            grid,
            intruder,
            owner,
            plan,
            forced: true,
            ownerFinalDefense: true);
        engagement.LeadArrived = true;
        engagement.StatusText = "최종 방어 대기";
        return true;
    }

    public void NotifyIntruderFinished(InvasionIntruderRuntime intruder)
    {
        if (TryGetEngagement(intruder, out DefenseEngagement engagement))
        {
            CompleteEngagement(engagement, releaseIntruder: false);
        }
    }

    public DefenseEngagementSaveSnapshot Capture()
    {
        return persistence.Capture();
    }

    private void TickEngagement(Grid grid, DefenseEngagement engagement)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        if (engagement.Intruder == null
            || engagement.IntruderActor == null
            || engagement.Intruder.State == InvasionIntruderState.Finished)
        {
            CompleteEngagement(engagement, releaseIntruder: false);
            return;
        }

        if (engagement.IntruderActor.IsDead)
        {
            ResolveIntruderDefeated(engagement);
            return;
        }

        if (engagement.LeadGuard == null || engagement.LeadGuard.IsDead)
        {
            HandleLeadLost(engagement, "선두 경비 부재");
            return;
        }

        if (!engagement.LeadArrived
            && engagement.LeadGuard.GetNowXY() == engagement.GuardCell)
        {
            engagement.LeadArrived = true;
            engagement.LeadMovement = null;
            engagement.StatusText = "저지 위치 도착";
        }

        if (engagement.ReserveGuard != null
            && !engagement.ReserveArrived
            && engagement.ReserveGuard.GetNowXY() == engagement.ReserveCell)
        {
            engagement.ReserveArrived = true;
            engagement.ReserveMovement = null;
            engagement.StatusText = "예비 경비 교대 대기";
        }

        if (engagement.RangedGuard != null
            && !engagement.RangedArrived
            && engagement.RangedGuard.GetNowXY() == engagement.RangedCell)
        {
            engagement.RangedArrived = true;
            engagement.RangedMovement = null;
            SetActorDefenseStatus(engagement.RangedGuard, "엄폐 사격 준비", combatActive: true);
        }

        if (engagement.SecondaryRangedGuard != null
            && !engagement.SecondaryRangedArrived
            && engagement.SecondaryRangedGuard.GetNowXY()
                == engagement.SecondaryRangedCell)
        {
            engagement.SecondaryRangedArrived = true;
            engagement.SecondaryRangedMovement = null;
            SetActorDefenseStatus(
                engagement.SecondaryRangedGuard,
                "엄폐 사격 준비",
                combatActive: true);
        }

        if (!engagement.IsOwnerFinalDefense)
        {
            TryFillRangedSupport(grid, engagement);
        }
        TickRangedSupport(grid, engagement, secondary: false);
        TickRangedSupport(grid, engagement, secondary: true);

        if (engagement.State == DefenseEngagementState.InterceptPlanned
            || engagement.State == DefenseEngagementState.Dispatching
            || engagement.State == DefenseEngagementState.ReserveWaiting)
        {
            if (engagement.LeadArrived
                && engagement.IntruderActor.GetNowXY() == engagement.IntruderStopCell)
            {
                BeginEngagement(engagement);
            }

            return;
        }

        if (engagement.State != DefenseEngagementState.Engaged)
        {
            return;
        }

        if (ShouldRetreat(engagement.LeadGuard))
        {
            if (engagement.ReserveGuard != null && engagement.ReserveArrived)
            {
                BeginGuardSwitch(engagement);
                return;
            }

            DefenseResponsePolicyData policy = policyRuntime.GetPolicy(engagement.LeadGuard);
            if (policy != null && !policy.holdWithoutReplacement)
            {
                MarkRetreated(engagement.LeadGuard);
                CollapseFront(engagement, "후퇴 정책에 따라 전선 이탈");
                return;
            }
        }

        if (engagement.ReserveGuard == null && !engagement.IsOwnerFinalDefense)
        {
            TryFillReserve(grid, engagement);
        }

        TickCombatExchange(engagement);
    }

    private bool TryDispatchForIntruder(Grid grid, InvasionIntruderRuntime intruder)
    {
        if (grid == null
            || intruder == null
            || intruder.IntruderActor == null
            || !intruder.HasBreachedDungeonInterior
            || !invasionContext.TryGetOwner(out CharacterActor owner))
        {
            return false;
        }

        foreach (CharacterActor candidate in GetEligibleGuards()
            .OrderBy(candidate =>
                combatExecutor.HasActiveRangedWeapon(candidate) ? 1 : 0)
            .ThenBy(candidate => candidate.GetNowXY().y != intruder.IntruderActor.GetNowXY().y)
            .ThenBy(candidate => Manhattan(candidate.GetNowXY(), intruder.IntruderActor.GetNowXY())))
        {
            if (!interceptPlanner.TryCreatePlan(
                    grid,
                    intruder,
                    candidate,
                    owner.GetNowXY(),
                    interceptPlanner.BuildUnavailableCells(engagementStore.Engagements),
                    out DefenseInterceptPlan plan))
            {
                continue;
            }

            CreateEngagement(grid, intruder, candidate, plan, forced: false);
            return true;
        }

        return false;
    }

    private DefenseEngagement CreateEngagement(
        Grid grid,
        InvasionIntruderRuntime intruder,
        CharacterActor lead,
        DefenseInterceptPlan plan,
        bool forced,
        bool ownerFinalDefense = false)
    {
        DefenseEngagement engagement = new DefenseEngagement
        {
            Id = engagementStore.AllocateId(),
            Intruder = intruder,
            LeadGuard = lead,
            State = DefenseEngagementState.Dispatching,
            IntruderStopCell = plan.IntruderStopCell,
            GuardCell = plan.GuardCell,
            ReserveCell = plan.ReserveCell,
            HasReserveCell = plan.ReserveCell != plan.GuardCell,
            IsOwnerFinalDefense = ownerFinalDefense,
            Forced = forced,
            StatusText = ownerFinalDefense ? "사장 최종 방어 준비" : "저지하러 이동"
        };
        engagementStore.Add(engagement);
        PrepareGuard(lead, engagement.StatusText);
        StartGuardMovement(grid, engagement, lead, plan.GuardCell, reserve: false, plan.LeadPath);
        intruder.SetEngagementState(false);
        if (!ownerFinalDefense)
        {
            TryFillReserve(grid, engagement);
            TryFillRangedSupport(grid, engagement);
        }

        return engagement;
    }

    private void TryFillReserve(Grid grid, DefenseEngagement engagement)
    {
        if (engagement == null
            || engagement.ReserveGuard != null
            || !engagement.HasReserveCell)
        {
            return;
        }

        CharacterActor reserve = GetEligibleGuards()
            .Where(candidate => candidate != engagement.LeadGuard)
            .OrderBy(candidate =>
                combatExecutor.HasActiveRangedWeapon(candidate) ? 1 : 0)
            .ThenBy(candidate => Manhattan(candidate.GetNowXY(), engagement.ReserveCell))
            .FirstOrDefault(candidate => grid.GetMovePathTo(
                candidate.GetNowXY(),
                engagement.ReserveCell).Count > 0
                || candidate.GetNowXY() == engagement.ReserveCell);
        if (reserve != null)
        {
            TryAssignReserve(grid, engagement, reserve, forced: false);
        }
    }

    private bool TryAssignReserve(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor reserve,
        bool forced)
    {
        if (grid == null
            || engagement == null
            || reserve == null
            || reserve.IsDead
            || engagement.ReserveGuard != null
            || !engagement.HasReserveCell
            || IsGuardAssigned(reserve))
        {
            return false;
        }

        Queue<GridMoveStep> path = grid.GetMovePathTo(
            reserve.GetNowXY(),
            engagement.ReserveCell);
        if (reserve.GetNowXY() != engagement.ReserveCell && (path == null || path.Count == 0))
        {
            return false;
        }

        engagement.ReserveGuard = reserve;
        engagement.ReserveArrived = reserve.GetNowXY() == engagement.ReserveCell;
        engagement.Forced |= forced;
        PrepareGuard(reserve, "교대 준비 위치로 이동");
        SetActorDefenseStatus(reserve, "교대 위치로 이동", combatActive: false);
        if (!engagement.ReserveArrived)
        {
            StartGuardMovement(grid, engagement, reserve, engagement.ReserveCell, reserve: true, path);
        }

        return true;
    }

    private void TryFillRangedSupport(Grid grid, DefenseEngagement engagement)
    {
        rangedSupport.TryFill(
            grid, engagement, GetEligibleGuards, IsCellReservedForOther,
            PrepareGuard, ReleaseRangedGuard);
    }

    private void StartRangedMovement(
        Grid grid, DefenseEngagement engagement, CharacterActor guard,
        Vector2Int target, Queue<GridMoveStep> initialPath = null,
        bool secondary = false)
    {
        rangedSupport.StartMovement(
            grid, engagement, guard, target, initialPath, secondary,
            ReleaseRangedGuard);
    }

    private void TickRangedSupport(
        Grid grid, DefenseEngagement engagement, bool secondary)
    {
        rangedSupport.Tick(
            grid, engagement, secondary, IsCellReservedForOther,
            ReleaseRangedGuard, ResolveIntruderDefeated);
    }

    private void StartGuardMovement(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor guard,
        Vector2Int target,
        bool reserve,
        Queue<GridMoveStep> initialPath = null)
    {
        if (guard == null || guard.IsDead)
        {
            return;
        }

        Coroutine routine = guard.StartCoroutine(RunGuardMovement(
            grid,
            engagement,
            guard,
            target,
            reserve,
            initialPath));
        if (reserve)
        {
            engagement.ReserveMovement = routine;
        }
        else
        {
            engagement.LeadMovement = routine;
        }
    }

    private IEnumerator RunGuardMovement(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor guard,
        Vector2Int target,
        bool reserve,
        Queue<GridMoveStep> initialPath)
    {
        AbilityMove move = guard.GetAbility<AbilityMove>();
        if (move == null)
        {
            CollapseFront(engagement, "경비 이동 능력 없음");
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
        if (reserve)
        {
            engagement.ReserveMovement = null;
            engagement.ReserveArrived = arrived;
            engagement.StatusText = arrived ? "교대 대기" : "예비 경비 경로 막힘";
            if (!arrived)
            {
                ReleaseGuard(guard, null, true);
                engagement.ReserveGuard = null;
            }
            else
            {
                SetActorDefenseStatus(guard, "교대 대기", combatActive: false);
            }
        }
        else
        {
            engagement.LeadMovement = null;
            engagement.LeadArrived = arrived;
            engagement.State = arrived
                ? DefenseEngagementState.InterceptPlanned
                : DefenseEngagementState.FrontCollapsed;
            engagement.StatusText = arrived ? "저지 예정" : "저지 경로 막힘";
            if (!arrived)
            {
                CollapseFront(engagement, engagement.StatusText);
            }
            else
            {
                SetActorDefenseStatus(guard, "저지 예정", combatActive: false);
            }
        }
    }

    private void BeginEngagement(DefenseEngagement engagement)
    {
        combatRuntime.Begin(engagement);
    }

    private void TickCombatExchange(DefenseEngagement engagement)
    {
        combatRuntime.TickExchange(engagement, StartGuardMovement);
    }

    private void BeginGuardSwitch(DefenseEngagement engagement)
    {
        combatRuntime.BeginGuardSwitch(engagement);
    }

    private void HandleLeadLost(DefenseEngagement engagement, string reason)
    {
        combatRuntime.HandleLeadLost(engagement, reason, StartGuardMovement);
    }

    private void CollapseFront(DefenseEngagement engagement, string reason)
    {
        combatRuntime.CollapseFront(engagement, reason);
    }

    private void ResolveOwnerDefeated(DefenseEngagement engagement)
    {
        combatRuntime.ResolveOwnerDefeated(engagement);
    }

    private void ResolveIntruderDefeated(DefenseEngagement engagement)
    {
        combatRuntime.ResolveIntruderDefeated(engagement);
    }

    private void CompleteEngagement(DefenseEngagement engagement, bool releaseIntruder)
    {
        combatRuntime.Complete(engagement, releaseIntruder);
    }

    private void ReleaseRangedGuard(
        DefenseEngagement engagement, string reason, bool secondary)
    {
        combatRuntime.ReleaseRangedGuard(engagement, reason, secondary);
    }

    private void PrepareGuard(CharacterActor guard, string activity)
    {
        guardControl.Prepare(guard, activity);
    }

    private void ReleaseGuard(CharacterActor guard, Coroutine movement, bool resumeAi)
    {
        guardControl.Release(guard, movement, resumeAi);
    }

    private void ReleaseOrphanedDefenseGuards(bool releaseAll)
    {
        guardControl.ReleaseOrphans(releaseAll, IsGuardAssigned);
    }

    private IEnumerable<CharacterActor> GetEligibleGuards()
    {
        foreach (CharacterActor actor in workforceQuery.FindActiveWorkers())
        {
            if (actor == null
                || actor.IsDead
                || actor.IsOwner
                || ammoResupply.IsResupplying(actor)
                || IsGuardAssigned(actor)
                || !CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
                || work.IsOffDuty
                || work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard) == WorkPriorityLevel.Off)
            {
                continue;
            }

            DefenseResponsePolicyData policy = policyRuntime.GetPolicy(actor);
            if (policy == null || !policy.autoRespond)
            {
                continue;
            }

            float healthRatio = GetHealthRatio(actor);
            string actorId = GetPersistentId(actor);
            float requiredHealth = engagementStore.HasRetreated(actorId)
                ? policy.rejoinHealthRatio
                : policy.minimumDispatchHealthRatio;
            if (healthRatio + 0.0001f < requiredHealth)
            {
                continue;
            }

            engagementStore.ClearRetreated(actorId);
            yield return actor;
        }
    }

    private bool HasCombatCapableGuard()
    {
        return workforceQuery.FindActiveWorkers().Any(actor =>
        {
            if (actor == null
                || actor.IsDead
                || actor.IsOwner
                || !CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
                || work.IsOffDuty
                || work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard) == WorkPriorityLevel.Off)
            {
                return false;
            }

            if (IsGuardAssigned(actor))
            {
                return true;
            }

            DefenseResponsePolicyData policy = policyRuntime.GetPolicy(actor);
            float minimumHealth = engagementStore.HasRetreated(GetPersistentId(actor))
                ? policy?.rejoinHealthRatio ?? 1f
                : policy?.minimumDispatchHealthRatio ?? 1f;
            return policy != null
                && policy.autoRespond
                && GetHealthRatio(actor) + 0.0001f >= minimumHealth;
        });
    }

    private bool IsGuardAssigned(CharacterActor actor)
    {
        return actor != null && engagementStore.Engagements.Any(
            engagement => engagement != null
            && engagement.IsActive
            && (engagement.LeadGuard == actor
                || engagement.ReserveGuard == actor
                || engagement.RangedGuard == actor
                || engagement.SecondaryRangedGuard == actor));
    }

    private bool ShouldRetreat(CharacterActor guard)
    {
        if (guard == null || guard.IsDead || guard.IsOwner)
        {
            return false;
        }

        DefenseResponsePolicyData policy = policyRuntime.GetPolicy(guard);
        return policy != null
            && policy.retreatHealthRatio > 0f
            && GetHealthRatio(guard) <= policy.retreatHealthRatio;
    }

    private void MarkRetreated(CharacterActor guard)
    {
        combatRuntime.MarkRetreated(guard);
    }

    private void TryStartOwnerDefenseWhenReady(Grid grid, InvasionIntruderRuntime intruder)
    {
        if (intruder == null
            || !intruder.HasBreachedDungeonInterior
            || !ownerEvacuation.IsEvacuating
            || !ownerEvacuation.HasReachedTarget
            || ownerEvacuation.Owner == null)
        {
            return;
        }

        TryBeginOwnerFinalDefense(intruder, ownerEvacuation.Owner);
    }

    public void PrepareRestoreCandidate(
        DefenseEngagementSaveSnapshot snapshot,
        DungeonGameRestoreReport report)
    {
        persistence.PrepareRestoreCandidate(snapshot, report);
    }

    public void PublishRestoreCandidate()
    {
        persistence.PublishRestoreCandidate();
    }

    public void RollbackPublishedRestoreCandidate()
    {
        persistence.RollbackPublishedRestoreCandidate();
    }

    public void RetirePreviousRestoreProjection()
    {
        persistence.RetirePreviousRestoreProjection(CompleteEngagement);
    }

    public void ActivateRestoreProjection()
    {
        persistence.ActivateRestoreProjection(
            PrepareGuard,
            StartGuardMovement,
            StartRangedMovement);
    }

    public void CompleteRestoreCandidate()
    {
        RetirePreviousRestoreProjection();
        ActivateRestoreProjection();
    }

    public void DiscardRestoreCandidate()
    {
        persistence.DiscardRestoreCandidate();
    }

    private static float GetHealthRatio(CharacterActor actor)
    {
        return actor != null ? Mathf.Clamp01(actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth)) : 0f;
    }

    private static void SetActorDefenseStatus(
        CharacterActor actor,
        string status,
        bool combatActive)
    {
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(status, combatActive);
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor != null ? actor.Identity?.PersistentId ?? string.Empty : string.Empty;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}

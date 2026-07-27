using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using static DefenseRangedSupportAccess;
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
    private readonly IInvasionDirectorRuntimeProvider directorProvider;
    private readonly IInvasionOwnerEvacuationService ownerEvacuation;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly ICombatLineOfSightService lineOfSight;
    private readonly ICombatCoverQuery coverQuery;
    private readonly IDefenseCombatExecutor combatExecutor;
    private readonly ICombatAmmoResupplyRuntime ammoResupply;
    private readonly IDefenseTacticalCoordinator tacticalCoordinator;
    private readonly IDefenseEngagementStore engagementStore;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly IGameEventBus gameEventBus;
    private readonly Dictionary<string, bool> guardPauseStateBeforeDefense =
        new Dictionary<string, bool>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterActor> defenseControlledGuards =
        new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
    private readonly IGameClock gameClock;
    private readonly DefenseInterceptPlanner interceptPlanner = new DefenseInterceptPlanner();
    private IDisposable downedSubscription, deathSubscription, breachSubscription, resolvedSubscription;
    public DefenseEngagementRuntime(
        IStaffWorkforceQueryService workforceQuery,
        IGridSystemProvider gridProvider,
        IDefenseResponsePolicyRuntime policyRuntime,
        IInvasionIntruderContext invasionContext,
        IInvasionDirectorRuntimeProvider directorProvider,
        IInvasionOwnerEvacuationService ownerEvacuation,
        ICharacterWorldQuery characterWorld,
        ICombatLineOfSightService lineOfSight,
        ICombatCoverQuery coverQuery,
        IDefenseCombatExecutor combatExecutor,
        ICombatAmmoResupplyRuntime ammoResupply,
        IDefenseTacticalCoordinator tacticalCoordinator,
        IDefenseEngagementStore engagementStore,
        IGridPathSearchBroker pathSearchBroker,
        IGameEventBus gameEventBus,
        IGameClock gameClock)
    {
        this.workforceQuery = workforceQuery ?? throw new ArgumentNullException(nameof(workforceQuery));
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.policyRuntime = policyRuntime ?? throw new ArgumentNullException(nameof(policyRuntime));
        this.invasionContext = invasionContext ?? throw new ArgumentNullException(nameof(invasionContext));
        this.directorProvider = directorProvider ?? throw new ArgumentNullException(nameof(directorProvider));
        this.ownerEvacuation = ownerEvacuation ?? throw new ArgumentNullException(nameof(ownerEvacuation));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.lineOfSight = lineOfSight ?? throw new ArgumentNullException(nameof(lineOfSight));
        this.coverQuery = coverQuery ?? throw new ArgumentNullException(nameof(coverQuery));
        this.combatExecutor = combatExecutor
            ?? throw new ArgumentNullException(nameof(combatExecutor));
        this.ammoResupply = ammoResupply ?? throw new ArgumentNullException(nameof(ammoResupply));
        this.tacticalCoordinator = tacticalCoordinator
            ?? throw new ArgumentNullException(nameof(tacticalCoordinator));
        this.engagementStore = engagementStore
            ?? throw new ArgumentNullException(nameof(engagementStore));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public IInvasionOwnerEvacuationService OwnerEvacuation => ownerEvacuation;
    public IDefenseResponsePolicyRuntime PolicyRuntime => policyRuntime;
    public IReadOnlyList<DefenseEngagement> ActiveEngagements =>
        engagementStore.Engagements;

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
        downedSubscription = gameEventBus.Subscribe<CharacterBodyHealthRuntime.CharacterDownedEvent>(
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

        if (!directorProvider.TryGetRuntime(out InvasionDirectorRuntime director))
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
        CharacterActor dead = eventType.Actor;
        if (dead == null)
        {
            return;
        }

        foreach (DefenseEngagement engagement in engagementStore.Engagements.ToArray())
        {
            if (engagement.IntruderActor == dead)
            {
                ResolveIntruderDefeated(engagement);
            }
            else if (engagement.LeadGuard == dead)
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
            else if (engagement.ReserveGuard == dead)
            {
                ReleaseGuard(engagement.ReserveGuard, engagement.ReserveMovement, true);
                engagement.ReserveGuard = null;
                engagement.ReserveMovement = null;
                engagement.ReserveArrived = false;
                engagement.StatusText = "예비 경비 쓰러짐";
            }
            else if (engagement.RangedGuard == dead)
            {
                ReleaseRangedGuard(engagement, "원거리 경비 쓰러짐", secondary: false);
            }
            else if (engagement.SecondaryRangedGuard == dead)
            {
                ReleaseRangedGuard(engagement, "원거리 경비 쓰러짐", secondary: true);
            }
        }
    }

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
                BuildUnavailableCells(),
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
                BuildUnavailableCells(),
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
        return new DefenseEngagementSaveSnapshot
        {
            engagements = engagementStore.Engagements
                .Where(engagement => engagement != null && engagement.IsActive)
                .Select(engagement => new DefenseEngagementSaveData
                {
                    id = engagement.Id,
                    intruderId = GetPersistentId(engagement.IntruderActor),
                    leadGuardId = GetPersistentId(engagement.LeadGuard),
                    reserveGuardId = GetPersistentId(engagement.ReserveGuard),
                    rangedGuardId = GetPersistentId(engagement.RangedGuard),
                    secondaryRangedGuardId =
                        GetPersistentId(engagement.SecondaryRangedGuard),
                    state = engagement.State,
                    intruderStopX = engagement.IntruderStopCell.x,
                    intruderStopY = engagement.IntruderStopCell.y,
                    guardX = engagement.GuardCell.x,
                    guardY = engagement.GuardCell.y,
                    reserveX = engagement.ReserveCell.x,
                    reserveY = engagement.ReserveCell.y,
                    rangedX = engagement.RangedCell.x,
                    rangedY = engagement.RangedCell.y,
                    secondaryRangedX = engagement.SecondaryRangedCell.x,
                    secondaryRangedY = engagement.SecondaryRangedCell.y,
                    hasReserveCell = engagement.HasReserveCell,
                    ownerFinalDefense = engagement.IsOwnerFinalDefense,
                    forced = engagement.Forced,
                    guardAttackRemaining = Mathf.Max(0f, engagement.NextGuardAttackAt - gameClock.Time),
                    intruderAttackRemaining = Mathf.Max(0f, engagement.NextIntruderAttackAt - gameClock.Time),
                    rangedAttackRemaining = Mathf.Max(0f, engagement.NextRangedAttackAt - gameClock.Time),
                    secondaryRangedAttackRemaining = Mathf.Max(
                        0f,
                        engagement.NextSecondaryRangedAttackAt - gameClock.Time),
                    exchangeCount = engagement.ExchangeCount
                })
                .ToList()
        };
    }

    public void Restore(DefenseEngagementSaveSnapshot snapshot, IList<string> warnings)
    {
        foreach (DefenseEngagement engagement in engagementStore.Engagements.ToArray())
        {
            CompleteEngagement(engagement, releaseIntruder: false);
        }

        engagementStore.ClearEngagements();
        if (snapshot == null || !gridProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        foreach (DefenseEngagementSaveData source in snapshot.engagements
            ?? new List<DefenseEngagementSaveData>())
        {
            InvasionIntruderRuntime intruder = FindIntruder(source?.intruderId);
            CharacterActor lead = FindCharacter(source?.leadGuardId);
            CharacterActor reserve = FindCharacter(source?.reserveGuardId);
            CharacterActor ranged = FindCharacter(source?.rangedGuardId);
            CharacterActor secondaryRanged =
                FindCharacter(source?.secondaryRangedGuardId);
            if (source == null || intruder == null || lead == null)
            {
                warnings?.Add("대상 또는 경비가 사라진 교전 예약을 해제했습니다.");
                continue;
            }

            if (!intruder.HasBreachedDungeonInterior)
            {
                warnings?.Add("던전 밖 침입자에게 저장되어 있던 경비 저지 예약을 해제했습니다.");
                continue;
            }

            Vector2Int stopCell = new Vector2Int(source.intruderStopX, source.intruderStopY);
            Vector2Int guardCell = new Vector2Int(source.guardX, source.guardY);
            Vector2Int reserveCell = new Vector2Int(source.reserveX, source.reserveY);
            Vector2Int rangedCell = new Vector2Int(source.rangedX, source.rangedY);
            Vector2Int secondaryRangedCell = new Vector2Int(
                source.secondaryRangedX,
                source.secondaryRangedY);
            if (!grid.IsValidGridPos(stopCell)
                || !grid.IsValidGridPos(guardCell)
                || grid.GetGridCell(stopCell)?.AreaType != GridCellAreaType.DungeonInterior
                || grid.GetGridCell(guardCell)?.AreaType != GridCellAreaType.DungeonInterior
                || stopCell == guardCell
                || Mathf.Abs(stopCell.x - guardCell.x) + Mathf.Abs(stopCell.y - guardCell.y) != 1)
            {
                warnings?.Add("무효한 교전 칸을 해제하고 저지 지점을 다시 계산합니다.");
                continue;
            }

            DefenseEngagement engagement = new DefenseEngagement
            {
                Id = string.IsNullOrWhiteSpace(source.id)
                    ? engagementStore.AllocateId()
                    : source.id,
                Intruder = intruder,
                LeadGuard = lead,
                ReserveGuard = reserve,
                RangedGuard = ranged,
                SecondaryRangedGuard = secondaryRanged,
                State = source.state == DefenseEngagementState.Completed
                    ? DefenseEngagementState.InterceptPlanned
                    : source.state,
                IntruderStopCell = stopCell,
                GuardCell = guardCell,
                ReserveCell = reserveCell,
                RangedCell = rangedCell,
                SecondaryRangedCell = secondaryRangedCell,
                HasReserveCell = source.hasReserveCell,
                IsOwnerFinalDefense = source.ownerFinalDefense,
                Forced = source.forced,
                NextGuardAttackAt = gameClock.Time + Mathf.Max(0f, source.guardAttackRemaining),
                NextIntruderAttackAt = gameClock.Time + Mathf.Max(0f, source.intruderAttackRemaining),
                NextRangedAttackAt = gameClock.Time + Mathf.Max(0f, source.rangedAttackRemaining),
                NextRangedReplanAt = gameClock.Time + 0.25f,
                NextSecondaryRangedAttackAt = gameClock.Time
                    + Mathf.Max(0f, source.secondaryRangedAttackRemaining),
                NextSecondaryRangedReplanAt = gameClock.Time + 0.25f,
                ExchangeCount = Mathf.Max(0, source.exchangeCount),
                LeadArrived = lead.GetNowXY() == guardCell,
                ReserveArrived = reserve != null && reserve.GetNowXY() == reserveCell,
                RangedArrived = ranged != null && ranged.GetNowXY() == rangedCell,
                SecondaryRangedArrived = secondaryRanged != null
                    && secondaryRanged.GetNowXY() == secondaryRangedCell,
                StatusText = "저장된 교전 복원"
            };
            engagementStore.Add(engagement);
            PrepareGuard(lead, "저지 위치 복원");
            if (reserve != null)
            {
                PrepareGuard(reserve, "교대 위치 복원");
            }
            if (ranged != null)
            {
                PrepareGuard(ranged, "원거리 위치 복원");
            }
            if (secondaryRanged != null)
            {
                PrepareGuard(secondaryRanged, "두 번째 원거리 위치 복원");
            }

            if (!engagement.LeadArrived)
            {
                StartGuardMovement(grid, engagement, lead, guardCell, reserve: false);
            }

            if (reserve != null && !engagement.ReserveArrived)
            {
                StartGuardMovement(grid, engagement, reserve, reserveCell, reserve: true);
            }
            if (ranged != null && !engagement.RangedArrived)
            {
                StartRangedMovement(
                    grid,
                    engagement,
                    ranged,
                    rangedCell,
                    secondary: false);
            }
            if (secondaryRanged != null
                && !engagement.SecondaryRangedArrived)
            {
                StartRangedMovement(
                    grid,
                    engagement,
                    secondaryRanged,
                    secondaryRangedCell,
                    secondary: true);
            }

            if (engagement.State == DefenseEngagementState.Engaged
                && engagement.LeadArrived
                && intruder.IntruderActor.GetNowXY() == stopCell)
            {
                SetCombatPresentation(engagement, true);
                intruder.SetEngagementState(true, stopCell);
            }
            else
            {
                engagement.State = DefenseEngagementState.InterceptPlanned;
                intruder.SetEngagementState(false);
            }
        }
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
                    BuildUnavailableCells(),
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
        foreach (CharacterActor candidate in GetEligibleGuards()
            .Where(combatExecutor.HasActiveRangedWeapon)
            .Where(candidate =>
                candidate != engagement.RangedGuard
                && candidate != engagement.SecondaryRangedGuard)
            .OrderByDescending(candidate => candidate.GetCharacterStat(CharacterStatType.Shooting))
            .ThenBy(candidate => Manhattan(candidate.GetNowXY(), engagement.IntruderActor.GetNowXY())))
        {
            if (!TryFindRangedPosition(
                    grid,
                    engagement,
                    candidate,
                    out Vector2Int cell,
                    out Queue<GridMoveStep> path))
            {
                continue;
            }

            SetRangedGuard(engagement, secondary, candidate);
            SetRangedCell(engagement, secondary, cell);
            SetRangedArrived(
                engagement,
                secondary,
                candidate.GetNowXY() == cell);
            SetNextRangedReplanAt(
                engagement,
                secondary,
                gameClock.Time + 0.75f);
            PrepareGuard(candidate, "엄폐 사격 위치로 이동");
            if (GetRangedArrived(engagement, secondary))
            {
                SetActorDefenseStatus(candidate, "엄폐 사격 준비", combatActive: true);
            }
            else
            {
                StartRangedMovement(
                    grid,
                    engagement,
                    candidate,
                    cell,
                    path,
                    secondary);
            }

            return;
        }
    }

    private bool TryFindRangedPosition(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor guard,
        out Vector2Int bestCell,
        out Queue<GridMoveStep> bestPath)
    {
        bestCell = default;
        bestPath = null;
        if (grid == null
            || engagement?.IntruderActor == null
            || guard == null
            || !combatExecutor.TryGetActiveRangedWeapon(
                guard,
                out CombatWeaponSnapshot weapon))
        {
            return false;
        }

        string guardId = GetPersistentId(guard);
        string intruderId = GetPersistentId(engagement.IntruderActor);
        Vector2Int intruderCell = engagement.IntruderActor.GetNowXY();
        Vector2Int guardCell = guard.GetNowXY();
        List<(Vector2Int Cell, float Score)> candidates =
            new List<(Vector2Int Cell, float Score)>();
        int maxRange = Mathf.Max(2, weapon.MaximumRange);
        for (int y = intruderCell.y - maxRange;
            y <= intruderCell.y + maxRange;
            y++)
        {
            for (int x = intruderCell.x - maxRange;
                x <= intruderCell.x + maxRange;
                x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                GridCell gridCell = grid.GetGridCell(cell);
                int distance = Manhattan(cell, intruderCell);
                if (gridCell == null
                    || gridCell.AreaType != GridCellAreaType.DungeonInterior
                    || distance < 2
                    || distance > weapon.MaximumRange
                    || IsCellReservedForOther(guard, cell))
                {
                    continue;
                }

                CombatLineOfSightResult sight = lineOfSight.Evaluate(
                    grid,
                    cell,
                    intruderCell,
                    guardId,
                    intruderId);
                if (!sight.HasLineOfSight || sight.FriendlyFireRisk)
                {
                    continue;
                }

                CombatRangeBand band = CombatRangeRules.GetBand(distance);
                float rangeScore = weapon.GetAccuracyMultiplier(band) * 2.2f
                    + weapon.GetDamageMultiplier(band);
                CombatCoverSnapshot cover =
                    coverQuery.GetCover(grid, intruderCell, cell);
                float coverScore = cover.Height != CombatCoverHeight.None
                    ? cover.BaseBlockChance
                        * cover.GetDirectionalMultiplier()
                        * 4f
                    : 0f;
                float travelPenalty =
                    Manhattan(guardCell, cell)
                    * 0.04f;
                float crowdPenalty = cell == engagement.GuardCell
                    || cell == engagement.ReserveCell
                        ? 4f
                        : 0f;
                float score =
                    rangeScore + coverScore - travelPenalty - crowdPenalty;
                candidates.Add((cell, score));
            }
        }

        candidates.Sort((left, right) =>
            right.Score.CompareTo(left.Score));
        float bestScore = float.NegativeInfinity;
        for (int index = 0; index < candidates.Count; index++)
        {
            (Vector2Int Cell, float Score) candidate = candidates[index];
            if (candidate.Cell == guardCell)
            {
                bestCell = candidate.Cell;
                bestScore = candidate.Score;
                bestPath = new Queue<GridMoveStep>();
                break;
            }

            Queue<GridMoveStep> candidatePath =
                pathSearchBroker.GetMovePathTo(
                    grid,
                    guardCell,
                    candidate.Cell);
            if (candidatePath == null)
            {
                // Exact pathfinding is frame-sliced by the broker.
                return false;
            }

            if (candidatePath.Count == 0)
            {
                continue;
            }

            bestCell = candidate.Cell;
            bestScore = candidate.Score;
            bestPath = candidatePath;
            break;
        }

        if (float.IsNegativeInfinity(bestScore) || bestPath == null)
        {
            return false;
        }

        Vector2Int resolvedBestCell = bestCell;
        return (guardCell == resolvedBestCell || bestPath.Count > 0)
            && tacticalCoordinator.TryReserve(
                guardId,
                intruderId,
                resolvedBestCell,
                CombatPositionReservationKind.Ranged,
                bestScore,
                out _);
    }

    private void StartRangedMovement(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor guard,
        Vector2Int target,
        Queue<GridMoveStep> initialPath = null,
        bool secondary = false)
    {
        if (guard == null || guard.IsDead)
        {
            return;
        }

        Coroutine movement = guard.StartCoroutine(RunRangedMovement(
            grid,
            engagement,
            guard,
            target,
            initialPath,
            secondary));
        SetRangedMovement(engagement, secondary, movement);
    }

    private IEnumerator RunRangedMovement(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor guard,
        Vector2Int target,
        Queue<GridMoveStep> initialPath,
        bool secondary)
    {
        AbilityMove move = guard.GetAbility<AbilityMove>();
        if (move == null)
        {
            ReleaseRangedGuard(
                engagement,
                "원거리 경비 이동 능력 없음",
                secondary);
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
            SetActorDefenseStatus(guard, "엄폐 사격 준비", combatActive: true);
        }
        else
        {
            ReleaseRangedGuard(
                engagement,
                "원거리 사격 위치 경로 막힘",
                secondary);
        }
    }

    private void TickRangedSupport(
        Grid grid,
        DefenseEngagement engagement,
        bool secondary)
    {
        CharacterActor guard = GetRangedGuard(engagement, secondary);
        CharacterActor intruder = engagement?.IntruderActor;
        if (grid == null || guard == null || intruder == null)
        {
            return;
        }

        if (guard.IsDead)
        {
            ReleaseRangedGuard(
                engagement,
                "원거리 경비 쓰러짐",
                secondary);
            return;
        }

        if (!GetRangedArrived(engagement, secondary) || intruder.IsDead)
        {
            return;
        }

        if (!combatExecutor.TryGetActiveRangedWeapon(
                guard,
                out CombatWeaponSnapshot weapon))
        {
            ReleaseRangedGuard(
                engagement,
                "사용 가능한 원거리 무기 없음",
                secondary);
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
            if (gameClock.Time >= GetNextRangedReplanAt(engagement, secondary)
                && TryFindRangedPosition(
                    grid,
                    engagement,
                    guard,
                    out Vector2Int nextCell,
                    out Queue<GridMoveStep> path)
                && nextCell != guardCell)
            {
                SetRangedCell(engagement, secondary, nextCell);
                SetRangedArrived(engagement, secondary, false);
                SetNextRangedReplanAt(
                    engagement,
                    secondary,
                    gameClock.Time + 0.75f);
                SetActorDefenseStatus(guard, "사선 재확보", combatActive: true);
                StartRangedMovement(
                    grid,
                    engagement,
                    guard,
                    nextCell,
                    path,
                    secondary);
            }

            return;
        }

        CharacterCombatLoadoutProfile profile =
            combatExecutor.GetActiveProfile(guard);
        if (profile?.holdFire == true)
        {
            SetActorDefenseStatus(guard, "사격 중지", combatActive: true);
            return;
        }

        if (sight.FriendlyFireRisk)
        {
            SetActorDefenseStatus(guard, "아군 사선 대기", combatActive: true);
            return;
        }

        if (weapon.RequiresAmmo && weapon.LoadedAmmo <= 0)
        {
            CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(guard);
            if (combatExecutor.TryReload(
                    guard,
                    weapon,
                    inventory,
                    out float reloadDuration))
            {
                SetNextRangedAttackAt(
                    engagement,
                    secondary,
                    gameClock.Time + reloadDuration);
                DefenseCombatPresentation.Ensure(guard)?.PlayReload(weapon, reloadDuration);
                SetActorDefenseStatus(guard, "재장전 중", combatActive: true);
            }
            else
            {
                if (combatExecutor.TrySwitchFallbackWeapon(
                        guard,
                        out CombatWeaponSnapshot fallback))
                {
                    if (fallback.IsRanged)
                    {
                        SetActorDefenseStatus(guard, "장전된 백업 무기로 교체", combatActive: true);
                    }
                    else
                    {
                        ReleaseRangedGuard(
                            engagement,
                            "근접 백업 무기로 교체",
                            secondary);
                    }
                }
                else
                {
                    ReleaseRangedGuard(
                        engagement,
                        "탄약 재보급",
                        secondary);
                    ammoResupply.TryRequestAmmoResupply(guard, out _);
                }
            }

            return;
        }

        if (gameClock.Time < GetNextRangedAttackAt(engagement, secondary))
        {
            return;
        }

        CombatFireMode mode = combatExecutor.ResolveSupportedFireMode(
            weapon,
            profile?.fireMode ?? CombatFireMode.Aimed);
        DefenseCombatExecutionResult rangedResult =
            combatExecutor.ExecuteRanged(
                grid,
                engagement,
                guard,
                intruder,
                weapon,
                mode,
                sight,
                distance);
        SetActorDefenseStatus(
            guard,
            rangedResult.StatusText,
            combatActive: true);
        if (rangedResult.DefenderDefeated)
        {
            ResolveIntruderDefeated(engagement);
            return;
        }

        SetNextRangedAttackAt(
            engagement,
            secondary,
            gameClock.Time + combatExecutor.GetAttackInterval(
                guard,
                weapon,
                mode));
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
        if (engagement == null
            || engagement.State == DefenseEngagementState.Engaged
            || engagement.LeadGuard == null
            || engagement.LeadGuard.IsDead
            || engagement.IntruderActor == null
            || engagement.IntruderActor.IsDead
            || engagement.LeadGuard.GetNowXY() != engagement.GuardCell
            || engagement.IntruderActor.GetNowXY() != engagement.IntruderStopCell)
        {
            return;
        }

        engagement.State = DefenseEngagementState.Engaged;
        engagement.StatusText = engagement.IsOwnerFinalDefense ? "사장 최종 교전" : "교전 중";
        engagement.NextGuardAttackAt = gameClock.Time
            + combatExecutor.GetAttackInterval(engagement.LeadGuard, 1f);
        engagement.NextIntruderAttackAt = gameClock.Time
            + combatExecutor.GetAttackInterval(
            engagement.IntruderActor,
            engagement.Intruder.AttackSpeedMultiplier);
        FaceOpponents(engagement.LeadGuard, engagement.IntruderActor);
        SetCombatPresentation(engagement, true);
        SetActorDefenseStatus(engagement.ReserveGuard, "대기", combatActive: false);
        engagement.Intruder.SetEngagementState(true, engagement.IntruderStopCell);
        engagement.LeadGuard.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Started,
            $"{engagement.IntruderActor.Identity?.DisplayName ?? "침입자"} 저지",
            actionId: "defense:engagement",
            targetId: GetPersistentId(engagement.IntruderActor),
            targetName: engagement.IntruderActor.Identity?.DisplayName ?? engagement.IntruderActor.name,
            sentiment: -0.1f,
            bubbleEligible: true));
        TriggerPassives(engagement.LeadGuard, CharacterSkillTrigger.BattleStarted, engagement, engagement.IntruderActor, "guard");
        TriggerPassives(engagement.IntruderActor, CharacterSkillTrigger.BattleStarted, engagement, engagement.LeadGuard, "intruder");
    }

    private void TickCombatExchange(DefenseEngagement engagement)
    {
        if (gameClock.Time >= engagement.NextGuardAttackAt)
        {
            DefenseCombatExecutionResult guardAttack =
                combatExecutor.ExecuteMelee(
                    engagement,
                    engagement.LeadGuard,
                    engagement.IntruderActor,
                    1f,
                    attackerIsGuard: true);
            if (guardAttack.DefenderDefeated
                || engagement.IntruderActor == null
                || engagement.IntruderActor.IsDead)
            {
                ResolveIntruderDefeated(engagement);
                return;
            }

            engagement.NextGuardAttackAt = gameClock.Time
                + combatExecutor.GetAttackInterval(
                    engagement.LeadGuard,
                    1f);
        }

        if (gameClock.Time >= engagement.NextIntruderAttackAt)
        {
            DefenseCombatExecutionResult intruderAttack =
                combatExecutor.ExecuteMelee(
                    engagement,
                    engagement.IntruderActor,
                    engagement.LeadGuard,
                    engagement.Intruder.MeleeDamageMultiplier,
                    attackerIsGuard: false);
            if (intruderAttack.DefenderDefeated
                || engagement.LeadGuard == null
                || engagement.LeadGuard.IsDead)
            {
                HandleLeadLost(engagement, "선두 경비 쓰러짐");
                return;
            }

            engagement.NextIntruderAttackAt = gameClock.Time
                + combatExecutor.GetAttackInterval(
                    engagement.IntruderActor,
                    engagement.Intruder.AttackSpeedMultiplier);
        }
    }

    private void BeginGuardSwitch(DefenseEngagement engagement)
    {
        if (engagement == null
            || engagement.State == DefenseEngagementState.Switching
            || engagement.LeadGuard == null
            || engagement.ReserveGuard == null
            || !engagement.ReserveArrived)
        {
            return;
        }

        engagement.State = DefenseEngagementState.Switching;
        engagement.StatusText = "경비 교대 중";
        SetActorDefenseStatus(engagement.LeadGuard, string.Empty, combatActive: true);
        SetActorDefenseStatus(engagement.ReserveGuard, "교대", combatActive: true);
        engagement.LeadGuard.StartCoroutine(RunGuardSwitch(engagement));
    }

    private IEnumerator RunGuardSwitch(DefenseEngagement engagement)
    {
        CharacterActor oldLead = engagement.LeadGuard;
        CharacterActor newLead = engagement.ReserveGuard;
        Vector3 oldStart = oldLead.transform.position;
        Vector3 newStart = newLead.transform.position;
        Vector3 oldEnd = newStart;
        Vector3 newEnd = oldStart;
        float elapsed = 0f;
        const float duration = 0.28f;
        while (elapsed < duration
            && oldLead != null
            && !oldLead.IsDead
            && newLead != null
            && !newLead.IsDead)
        {
            float t = elapsed / duration;
            oldLead.transform.position = Vector3.Lerp(oldStart, oldEnd, t);
            newLead.transform.position = Vector3.Lerp(newStart, newEnd, t);
            elapsed += gameClock.DeltaTime;
            yield return null;
        }

        if (newLead == null || newLead.IsDead)
        {
            CollapseFront(engagement, "교대 경비 쓰러짐");
            yield break;
        }

        oldLead.transform.position = oldEnd;
        newLead.transform.position = newEnd;
        SetActorCombatPresentation(oldLead, false);
        MarkRetreated(oldLead);
        ReleaseGuard(oldLead, null, true);
        DefenseCombatPresentation.Ensure(oldLead)?.ShowTemporaryStatus("후퇴 중", 1.5f);
        engagement.LeadGuard = newLead;
        engagement.ReserveGuard = null;
        engagement.LeadArrived = true;
        engagement.ReserveArrived = false;
        engagement.ReserveMovement = null;
        engagement.State = DefenseEngagementState.Engaged;
        engagement.StatusText = "교대 완료 · 교전 중";
        engagement.NextGuardAttackAt = gameClock.Time + 0.15f;
        FaceOpponents(newLead, engagement.IntruderActor);
        SetCombatPresentation(engagement, true);
    }

    private void HandleLeadLost(DefenseEngagement engagement, string reason)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        SetActorCombatPresentation(engagement.LeadGuard, false);
        ReleaseGuard(engagement.LeadGuard, engagement.LeadMovement, true);
        if (engagement.ReserveGuard != null
            && !engagement.ReserveGuard.IsDead
            && engagement.ReserveArrived)
        {
            CharacterActor promoted = engagement.ReserveGuard;
            engagement.LeadGuard = promoted;
            engagement.ReserveGuard = null;
            engagement.ReserveArrived = false;
            engagement.ReserveMovement = null;
            engagement.State = DefenseEngagementState.Switching;
            engagement.StatusText = "예비 경비가 전선을 인계 중";
            SetActorDefenseStatus(promoted, "전선 인계 중", combatActive: true);
            StartGuardMovement(
                gridProvider.Grid,
                engagement,
                promoted,
                engagement.GuardCell,
                reserve: false);
            return;
        }

        CollapseFront(engagement, reason);
    }

    private void CollapseFront(DefenseEngagement engagement, string reason)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        engagement.State = DefenseEngagementState.FrontCollapsed;
        engagement.StatusText = reason;
        engagement.Intruder?.SetFrontBrokenState();
        CharacterActor releasedLead = engagement.LeadGuard;
        CharacterActor releasedReserve = engagement.ReserveGuard;
        SetCombatPresentation(engagement, false);
        ReleaseGuard(engagement.LeadGuard, engagement.LeadMovement, true);
        ReleaseGuard(engagement.ReserveGuard, engagement.ReserveMovement, true);
        if (releasedLead != null && !releasedLead.IsDead)
        {
            DefenseCombatPresentation.Ensure(releasedLead)?.ShowTemporaryStatus("후퇴 중", 1.5f);
        }
        if (releasedReserve != null && !releasedReserve.IsDead)
        {
            DefenseCombatPresentation.Ensure(releasedReserve)?.ShowTemporaryStatus("후퇴 중", 1.5f);
        }
        engagement.LeadGuard = null;
        engagement.ReserveGuard = null;
        engagement.State = DefenseEngagementState.Completed;
        engagementStore.Remove(engagement);
    }

    private void ResolveOwnerDefeated(DefenseEngagement engagement)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        CharacterActor owner = engagement.LeadGuard;
        InvasionIntruderRuntime intruder = engagement.Intruder;
        CompleteEngagement(engagement, releaseIntruder: false);
        intruder?.ResolveDefenseFailed(owner);
    }

    private void ResolveIntruderDefeated(DefenseEngagement engagement)
    {
        if (engagement == null || !engagement.IsActive)
        {
            return;
        }

        CharacterActor victor = engagement.LeadGuard;
        if (victor != null && !victor.IsDead)
        {
            TriggerPassives(victor, CharacterSkillTrigger.EnemyDefeated, engagement, engagement.IntruderActor, "victory");
            TriggerPassives(victor, CharacterSkillTrigger.BattleCompleted, engagement, engagement.IntruderActor, "complete");
            victor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Completed,
                "침입자 저지 완료",
                actionId: "defense:engagement",
                targetName: engagement.IntruderActor?.Identity?.DisplayName ?? "침입자",
                value: engagement.ExchangeCount,
                sentiment: 0.7f,
                bubbleEligible: true));
        }

        InvasionIntruderRuntime intruder = engagement.Intruder;
        CompleteEngagement(engagement, releaseIntruder: false);
        intruder?.ResolveSuppressedBy(victor);
    }

    private void CompleteEngagement(DefenseEngagement engagement, bool releaseIntruder)
    {
        if (engagement == null)
        {
            return;
        }

        engagement.State = DefenseEngagementState.Completed;
        SetCombatPresentation(engagement, false);
        SetActorCombatPresentation(engagement.RangedGuard, false);
        SetActorCombatPresentation(engagement.SecondaryRangedGuard, false);
        ReleaseGuard(engagement.LeadGuard, engagement.LeadMovement, true);
        ReleaseGuard(engagement.ReserveGuard, engagement.ReserveMovement, true);
        ReleaseGuard(engagement.RangedGuard, engagement.RangedMovement, true);
        ReleaseGuard(
            engagement.SecondaryRangedGuard,
            engagement.SecondaryRangedMovement,
            true);
        tacticalCoordinator.Release(GetPersistentId(engagement.RangedGuard));
        tacticalCoordinator.Release(GetPersistentId(engagement.SecondaryRangedGuard));
        if (releaseIntruder)
        {
            engagement.Intruder?.SetEngagementState(false);
        }

        engagementStore.Remove(engagement);
    }

    private void ReleaseRangedGuard(
        DefenseEngagement engagement,
        string reason,
        bool secondary)
    {
        if (engagement == null)
        {
            return;
        }

        CharacterActor guard = GetRangedGuard(engagement, secondary);
        tacticalCoordinator.Release(GetPersistentId(guard));
        SetActorCombatPresentation(guard, false);
        ReleaseGuard(guard, GetRangedMovement(engagement, secondary), true);
        SetRangedGuard(engagement, secondary, null);
        SetRangedMovement(engagement, secondary, null);
        SetRangedArrived(engagement, secondary, false);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            engagement.StatusText = reason;
        }
    }

    private void PrepareGuard(CharacterActor guard, string activity)
    {
        if (guard == null || guard.IsDead)
        {
            return;
        }

        guard.GetAbility<AbilityWork>()?.ReleaseAssignedWorkTarget();
        guard.GetAbility<AbilityMove>()?.CancelActiveMovement();
        guard.Brain?.RequestImmediateReplan(clearFailures: false);
        if (!guard.IsOwner)
        {
            string guardId = GetPersistentId(guard);
            if (!guardPauseStateBeforeDefense.ContainsKey(guardId))
            {
                guardPauseStateBeforeDefense[guardId] = guard.IsAiPaused();
            }

            defenseControlledGuards[guardId] = guard;
        }

        guard.SetAiPaused(true);
        SetActorDefenseStatus(guard, activity, combatActive: false);
        guard.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Started,
            activity,
            actionId: "defense:dispatch",
            sentiment: -0.05f));
    }

    private void ReleaseGuard(CharacterActor guard, Coroutine movement, bool resumeAi)
    {
        if (guard == null)
        {
            return;
        }

        if (movement != null)
        {
            guard.StopCoroutine(movement);
        }

        guard.GetAbility<AbilityMove>()?.CancelActiveMovement();
        if (resumeAi && !guard.IsDead && !guard.IsOwner)
        {
            string guardId = GetPersistentId(guard);
            bool previousPause = guardPauseStateBeforeDefense.TryGetValue(
                guardId,
                out bool storedPause)
                && storedPause;
            guardPauseStateBeforeDefense.Remove(guardId);
            defenseControlledGuards.Remove(guardId);
            guard.SetAiPaused(previousPause);
            guard.Brain?.RequestImmediateReplan(clearFailures: false);
        }
    }

    private void ReleaseOrphanedDefenseGuards(bool releaseAll)
    {
        foreach (KeyValuePair<string, CharacterActor> pair in defenseControlledGuards
                     .ToArray())
        {
            CharacterActor guard = pair.Value;
            if (!releaseAll && guard != null && IsGuardAssigned(guard))
            {
                continue;
            }

            bool previousPause = guardPauseStateBeforeDefense.TryGetValue(
                pair.Key,
                out bool storedPause)
                && storedPause;
            guardPauseStateBeforeDefense.Remove(pair.Key);
            defenseControlledGuards.Remove(pair.Key);
            if (guard == null || guard.IsDead || guard.IsOwner)
            {
                continue;
            }

            guard.GetAbility<AbilityMove>()?.CancelActiveMovement();
            guard.SetAiPaused(previousPause);
            guard.Brain?.RequestImmediateReplan(clearFailures: false);
        }
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
        string id = GetPersistentId(guard);
        if (!string.IsNullOrWhiteSpace(id))
        {
            engagementStore.MarkRetreated(id);
        }
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

    private HashSet<Vector2Int> BuildUnavailableCells()
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        foreach (DefenseEngagement engagement in engagementStore.Engagements)
        {
            if (engagement == null || !engagement.IsActive)
            {
                continue;
            }

            cells.Add(engagement.IntruderStopCell);
            cells.Add(engagement.GuardCell);
            if (engagement.HasReserveCell)
            {
                cells.Add(engagement.ReserveCell);
            }
            if (engagement.RangedGuard != null)
            {
                cells.Add(engagement.RangedCell);
            }
            if (engagement.SecondaryRangedGuard != null)
            {
                cells.Add(engagement.SecondaryRangedCell);
            }
        }

        return cells;
    }

    private InvasionIntruderRuntime FindIntruder(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId)
            || !directorProvider.TryGetRuntime(out InvasionDirectorRuntime director))
        {
            return null;
        }

        return director.ActiveIntruders.FirstOrDefault(intruder => intruder != null
            && string.Equals(GetPersistentId(intruder.IntruderActor), persistentId, StringComparison.Ordinal));
    }

    private CharacterActor FindCharacter(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return null;
        }

        return CharacterActorCollection.DistinctByGameObject(
                characterWorld.Characters)
            .FirstOrDefault(actor => actor != null
                && !actor.IsDead
                && string.Equals(GetPersistentId(actor), persistentId, StringComparison.Ordinal));
    }

    private static void TriggerPassives(
        CharacterActor actor,
        CharacterSkillTrigger trigger,
        DefenseEngagement engagement,
        CharacterActor target,
        string suffix,
        int serial = 0)
    {
        if (actor == null || engagement == null)
        {
            return;
        }

        CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
            actor,
            trigger,
            $"{engagement.Id}:{suffix}:{serial}",
            targetActor: target));
    }

    private static void FaceOpponents(CharacterActor first, CharacterActor second)
    {
        if (first == null || second == null)
        {
            return;
        }

        first.Flip(second.transform.position.x > first.transform.position.x
            ? CharacterFacing.RIGHT
            : CharacterFacing.LEFT);
        second.Flip(first.transform.position.x > second.transform.position.x
            ? CharacterFacing.RIGHT
            : CharacterFacing.LEFT);
    }

    private static void SetCombatPresentation(DefenseEngagement engagement, bool engaged)
    {
        if (engagement == null)
        {
            return;
        }

        if (engaged)
        {
            SetActorDefenseStatus(engagement.LeadGuard, "교전", combatActive: true);
            SetActorDefenseStatus(engagement.IntruderActor, string.Empty, combatActive: true);
            ApplyCombatNameplateOffsets(engagement.LeadGuard, engagement.IntruderActor);
        }
        else
        {
            SetActorCombatPresentation(engagement.LeadGuard, false);
            SetActorCombatPresentation(engagement.IntruderActor, false);
        }
        if (!engaged)
        {
            SetActorCombatPresentation(engagement.ReserveGuard, false);
        }
    }

    private static void SetActorDefenseStatus(
        CharacterActor actor,
        string status,
        bool combatActive)
    {
        if (actor == null)
        {
            return;
        }

        DefenseCombatPresentation.Ensure(actor)?.SetStatus(status, combatActive);
    }

    private static void SetActorCombatPresentation(CharacterActor actor, bool engaged)
    {
        DefenseCombatPresentation.Ensure(actor)?.SetEngaged(engaged);
    }

    private static void ApplyCombatNameplateOffsets(CharacterActor first, CharacterActor second)
    {
        if (first == null || second == null)
        {
            return;
        }

        float firstDirection = Mathf.Sign(first.transform.position.x - second.transform.position.x);
        if (Mathf.Approximately(firstDirection, 0f))
        {
            firstDirection = 1f;
        }

        WorldCharacterNameplate.Ensure(first)?.SetCombatHorizontalOffset(firstDirection * 0.24f);
        WorldCharacterNameplate.Ensure(second)?.SetCombatHorizontalOffset(-firstDirection * 0.24f);
    }

    private static float GetHealthRatio(CharacterActor actor)
    {
        return actor != null ? Mathf.Clamp01(actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth)) : 0f;
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

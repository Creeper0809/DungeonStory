using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal interface IInvasionIntruderExecutionHost
{
    InvasionIntruderRuntime Runtime { get; }
    CharacterActor Actor { get; }
    AbilityMove Move { get; }
    InvasionIntruderSettings Settings { get; }
    InvasionIntruderState State { get; set; }
    bool Resolved { get; set; }
    float RallyRemainingSeconds { get; set; }
    IGameClock Clock { get; }
    IGameEventBus GameEventBus { get; }
    IInvasionIntruderContext Context { get; }
    IDefenseEngagementRuntime DefenseEngagement { get; }
    BuildableObject PriorityTarget { get; set; }
    bool HasFinalDefenseTarget { get; set; }
    Vector2Int FinalDefenseTarget { get; }
    float Elapsed { get; set; }
    bool HasBreachedDungeonInterior { get; set; }
    bool BreachEventRaised { get; set; }
    float NextDamageTime { get; set; }
    IDefenseStatusRuntimeService DefenseStatusRuntimeService { get; }
    ITreasuryDefenseRuntime TreasuryDefenseRuntime { get; }
    string RuntimeId { get; }
    InvasionThreatSnapshot ThreatSnapshot { get; }
    bool IsBoss { get; }
    IDefenseRaidAwarenessRuntime RaidAwareness { get; }
    int CommittedAwarenessVersion { get; }
    float RouteCommitmentUntil { get; }
    IDefenseFacilityNetworkRuntime FacilityNetwork { get; }
    IDefenseBreachPlanner BreachPlanner { get; }
    IBuildingStructuralIntegrityRuntime StructuralIntegrity { get; }
    bool NoBreachableExitAlerted { get; set; }
    BuildableObject BreachTarget { get; set; }
    Vector2Int BreachAttackCell { get; set; }
    float TrappedSince { get; set; }
    float NextStructureAttackAt { get; set; }
    bool EnragedBreach { get; set; }
    float RestoredStructureAttackDelay { get; set; }
    float RestoredTrappedSeconds { get; set; }
    bool RestoredEnragedBreach { get; set; }
    float MeleeDamageMultiplier { get; }
    float AttackSpeedMultiplier { get; }
    ICharacterPerformanceQuery Performance { get; }

    Queue<GridMoveStep> CreateNextPath(
        Grid grid,
        Vector2Int ownerPosition,
        out bool direct,
        out BuildableObject priorityTarget);
    bool TryDamageNearbyFacility(Grid grid, BuildableObject preferredTarget);
    void MarkDungeonBreached(Grid grid, Vector2Int cellPosition);
    void ClearBreachState();
    void Finish();
}

internal sealed class InvasionIntruderExecutionCoordinator
{
    private readonly IInvasionIntruderExecutionHost host;

    public InvasionIntruderExecutionCoordinator(IInvasionIntruderExecutionHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void ApplyFinalCombat(CharacterActor owner)
    {
        if (owner == null || host.Actor == null || host.Settings == null)
        {
            return;
        }

        host.State = InvasionIntruderState.FinalCombat;
        host.GameEventBus.Publish(new InvasionFinalCombatStartedEvent(host.Actor, owner));
        owner.ApplyDamage(host.Settings.finalCombatDamage, "침입자 최종 교전");
        host.Resolved = true;
        host.GameEventBus.Publish(new InvasionResolvedEvent(!owner.IsDead, owner.IsDead ? 5f : 2f));
    }

    public void ClearBreachState()
    {
        host.BreachPlanner?.ReleaseReservation(host.RuntimeId);
        host.BreachTarget = null;
        host.BreachAttackCell = default;
        host.TrappedSince = 0f;
        host.NextStructureAttackAt = 0f;
        host.EnragedBreach = false;
        host.RaidAwareness?.SetBreachTarget(
            InvasionIntruderCombatRules.ResolveRaidId(
                host.Settings,
                host.RuntimeId),
            null,
            string.Empty);
    }

    public void MarkDungeonBreached(Grid grid, Vector2Int cellPosition)
    {
        if (host.HasBreachedDungeonInterior
            || grid == null
            || !grid.IsValidGridPos(cellPosition))
        {
            return;
        }

        GridCell cell = grid.GetGridCell(cellPosition);
        if (cell == null || cell.AreaType != GridCellAreaType.DungeonInterior)
        {
            return;
        }

        host.HasBreachedDungeonInterior = true;
        if (host.BreachEventRaised)
        {
            return;
        }

        host.BreachEventRaised = true;
        host.GameEventBus.Publish(new InvasionDungeonBreachedEvent(
            host.Runtime,
            host.Actor,
            host.ThreatSnapshot));
        host.GameEventBus.RaiseAlert(
            "던전 내부 침입",
            "침입자가 내부에 진입했습니다. 당직 경비가 저지하러 이동합니다.",
            EventAlertImportance.High,
            "방어");
    }

    public IEnumerator Run(
        Vector3 entryDoorPosition,
        Vector2Int entryGridPosition,
        bool includeRally)
    {
        if (includeRally && host.RallyRemainingSeconds > 0f)
        {
            host.State = InvasionIntruderState.Rallying;
            while (host.RallyRemainingSeconds > 0f
                && host.Actor != null
                && !host.Actor.IsDead)
            {
                host.RallyRemainingSeconds = Mathf.Max(
                    0f,
                    host.RallyRemainingSeconds - host.Clock.DeltaTime);
                yield return null;
            }

            if (host.Actor == null || host.Actor.IsDead)
            {
                ResolveIntruderDefeated();
                yield break;
            }

            host.GameEventBus.RaiseAlert(
                "침입 개시",
                "집결을 마친 침입자들이 던전 입구로 접근합니다.",
                EventAlertImportance.High,
                "침입");
        }

        host.State = InvasionIntruderState.Entering;
        yield return host.Move.Move2PosBySpeed(entryDoorPosition);

        host.Context.TryGetGrid(out Grid grid);
        if (grid != null && grid.IsValidGridPos(entryGridPosition))
        {
            yield return host.Move.Move2PosBySpeed(grid.GetWorldPos(entryGridPosition));
            host.MarkDungeonBreached(grid, entryGridPosition);
        }

        host.Actor.SetLifecycleState(CharacterLifecycleState.Active);
        yield return RunInside();
    }

    public IEnumerator RunInside()
    {
        IInvasionIntruderContext context = host.Context;
        Grid grid;
        while (host.State != InvasionIntruderState.Finished
               && host.Actor != null
               && !host.Actor.IsDead)
        {
            if (host.Settings.retreatHealthRatio > 0f
                && host.Actor.CurrentHealth
                    <= host.Actor.MaxHealth * host.Settings.retreatHealthRatio)
            {
                ResolveRetreated();
                yield break;
            }

            context.TryGetGrid(out grid);
            context.TryGetOwner(out CharacterActor owner);
            if (grid == null || owner == null || owner.IsDead)
            {
                host.Finish();
                yield break;
            }

            host.MarkDungeonBreached(grid, host.Actor.GetNowXY());

            if (host.DefenseEngagement?.ShouldHoldIntruder(host.Runtime) ?? false)
            {
                host.PriorityTarget = null;
                yield return null;
                continue;
            }

            if (host.HasFinalDefenseTarget
                && host.Actor.GetNowXY() == host.FinalDefenseTarget)
            {
                host.HasFinalDefenseTarget = false;
            }

            if (!host.HasFinalDefenseTarget
                && InvasionIntruderPlanner.IsAtOwner(grid, host.Actor, owner))
            {
                if (host.DefenseEngagement != null)
                {
                    host.DefenseEngagement.TryBeginOwnerFinalDefense(host.Runtime, owner);
                    yield return null;
                    continue;
                }

                yield return FinalCombat(owner);
                yield break;
            }

            host.Elapsed += Mathf.Max(0.01f, host.Settings.repathIntervalSeconds);
            Queue<GridMoveStep> path = host.CreateNextPath(
                grid,
                host.HasFinalDefenseTarget
                    ? host.FinalDefenseTarget
                    : owner.GetNowXY(),
                out bool direct,
                out BuildableObject priorityTarget);
            host.PriorityTarget = priorityTarget;
            host.State = priorityTarget != null
                ? InvasionIntruderState.MovingToFacility
                : direct
                    ? InvasionIntruderState.MovingToOwner
                    : InvasionIntruderState.Searching;

            if (host.HasBreachedDungeonInterior
                && !(host.DefenseEngagement?.ShouldHoldIntruder(host.Runtime) ?? false)
                && host.Clock.Time >= host.NextDamageTime)
            {
                host.TryDamageNearbyFacility(grid, host.PriorityTarget);
                host.NextDamageTime = host.Clock.Time
                    + host.Settings.facilityDamageIntervalSeconds;
            }

            if (path.Count == 0)
            {
                if (host.HasFinalDefenseTarget)
                {
                    host.HasFinalDefenseTarget = false;
                    yield return null;
                    continue;
                }

                yield return ExecuteBreach(grid, owner.GetNowXY());
                if (host.BreachTarget != null
                    || host.State == InvasionIntruderState.Breaching)
                {
                    continue;
                }

                InvasionIntruderCombatRules.TickDefenseStatuses(
                    host.Actor,
                    host.Settings.repathIntervalSeconds,
                    host.DefenseStatusRuntimeService);
                yield return new WaitForSeconds(host.Settings.repathIntervalSeconds);
                continue;
            }

            yield return MovePathWithDefense(grid, path);
            if (host.Actor == null || host.Actor.IsDead)
            {
                ResolveIntruderDefeated();
                yield break;
            }

            if (host.HasBreachedDungeonInterior
                && !(host.DefenseEngagement?.ShouldHoldIntruder(host.Runtime) ?? false)
                && host.PriorityTarget != null
                && host.Clock.Time >= host.NextDamageTime)
            {
                host.TryDamageNearbyFacility(grid, host.PriorityTarget);
                host.NextDamageTime = host.Clock.Time
                    + host.Settings.facilityDamageIntervalSeconds;
            }

            yield return null;
        }

        if (host.Actor != null
            && host.Actor.IsDead
            && host.State != InvasionIntruderState.Finished)
        {
            ResolveIntruderDefeated();
        }
    }

    private IEnumerator MovePathWithDefense(Grid grid, Queue<GridMoveStep> path)
    {
        if (path == null)
        {
            yield break;
        }

        while (path.Count > 0 && host.Actor != null && !host.Actor.IsDead)
        {
            GridMoveStep step = path.Dequeue();
            if (!step.IsValid)
            {
                continue;
            }

            if (!(host.DefenseEngagement?.CanIntruderAdvanceTo(host.Runtime, step.To) ?? true))
            {
                yield break;
            }

            yield return host.Move.MoveByStep(step);
            if (host.Move.LastGridMoveWasBlocked)
            {
                yield break;
            }

            host.MarkDungeonBreached(grid, step.To);
            string raidId = InvasionIntruderCombatRules.ResolveRaidId(
                host.Settings,
                host.RuntimeId);
            host.FacilityNetwork?.DetectIntruder(
                raidId,
                step.To,
                host.RaidAwareness);
            InvasionIntruderDefenseObservationSceneAdapter.Observe(
                host.RaidAwareness,
                raidId,
                grid,
                step.To);

            List<DefenseActivationReport> reports = DefenseFacilityResolver.TriggerAt(
                grid,
                host.Actor,
                step.To,
                DefenseTriggerTiming.OnEnter,
                host.DefenseStatusRuntimeService,
                host.TreasuryDefenseRuntime,
                host.RuntimeId,
                host.ThreatSnapshot.threat,
                host.IsBoss);
            foreach (DefenseActivationReport report in reports)
            {
                host.RaidAwareness?.RecordTriggeredFacility(raidId, report);
            }

            InvasionIntruderCombatRules.TickDefenseStatuses(
                host.Actor,
                host.Settings.repathIntervalSeconds,
                host.DefenseStatusRuntimeService);

            if (host.Actor.IsDead)
            {
                yield break;
            }

            if (host.DefenseEngagement?.ShouldHoldIntruder(host.Runtime) ?? false)
            {
                yield break;
            }

            DefenseRaidAwarenessSnapshot currentAwareness =
                host.RaidAwareness?.GetSnapshot(raidId);
            if (currentAwareness != null
                && currentAwareness.Version != host.CommittedAwarenessVersion
                && host.Clock.Time >= host.RouteCommitmentUntil)
            {
                yield break;
            }

            float delay = reports.Count > 0
                ? reports.Max(report => report.MovementDelaySeconds)
                : 0f;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
        }
    }

    private IEnumerator ExecuteBreach(Grid grid, Vector2Int destination)
    {
        if (host.BreachPlanner == null
            || host.StructuralIntegrity == null
            || host.Actor == null)
        {
            yield break;
        }

        string raidId = InvasionIntruderCombatRules.ResolveRaidId(
            host.Settings,
            host.RuntimeId);
        DefenseRaidAwarenessSnapshot awareness =
            host.RaidAwareness?.GetSnapshot(raidId);
        if (!host.BreachPlanner.TryPlan(
                host.RuntimeId,
                grid,
                host.Actor.GetNowXY(),
                destination,
                host.Actor.PathSearchBroker,
                host.StructuralIntegrity,
                awareness?.KnownRisks,
                host.Settings.riskTolerance,
                InvasionIntruderCombatRules.EstimateStructureDamage(
                    host.Actor,
                    host.Settings,
                    host.MeleeDamageMultiplier,
                    host.Performance),
                out DefenseBreachPlan plan))
        {
            if (!host.NoBreachableExitAlerted)
            {
                host.NoBreachableExitAlerted = true;
                host.GameEventBus.RaiseAlert(
                    "파괴 가능한 탈출구 없음",
                    "침입자가 목표로 이어지는 문·벽을 찾지 못해 가장 안전한 도달 지점으로 이동합니다.",
                    EventAlertImportance.High,
                    "방어");
            }

            yield return MoveToSafestReachableCell(grid, destination, awareness);
            yield break;
        }

        host.NoBreachableExitAlerted = false;
        host.BreachTarget = plan.Target;
        host.BreachAttackCell = plan.AttackCell;
        float breachStartTime = host.Clock.Time;
        host.TrappedSince = breachStartTime - host.RestoredTrappedSeconds;
        host.NextStructureAttackAt = breachStartTime
            + host.RestoredStructureAttackDelay;
        host.EnragedBreach = host.RestoredEnragedBreach
            || host.RestoredTrappedSeconds >= 3f;
        host.RestoredStructureAttackDelay = 0f;
        host.RestoredTrappedSeconds = 0f;
        host.RestoredEnragedBreach = false;
        host.State = InvasionIntruderState.Breaching;
        host.RaidAwareness?.SetBreachTarget(
            raidId,
            host.BreachTarget,
            "정상 경로 단절 · 구조물 돌파");
        host.RaidAwareness?.SetExpectedPath(
            raidId,
            plan.VirtualPath,
            "가상 돌파 경로 선택");

        if (plan.ApproachPath.Count > 0)
        {
            yield return MovePathWithDefense(grid, plan.ApproachPath);
        }

        while (host.BreachTarget != null
               && !host.BreachTarget.isDestroy
               && host.Actor != null
               && !host.Actor.IsDead)
        {
            Queue<GridMoveStep> openedPath =
                host.Actor.PathSearchBroker.GetMovePathTo(
                    grid,
                    host.Actor.GetNowXY(),
                    destination,
                    GridPathSearchPriority.Urgent);
            if (openedPath != null && openedPath.Count > 0)
            {
                host.RaidAwareness?.SetExpectedPath(
                    raidId,
                    openedPath.Select(step => step.To),
                    "문 개방 또는 구조물 제거로 우회로 열림");
                break;
            }

            if (host.Actor.GetNowXY() != host.BreachAttackCell)
            {
                break;
            }

            float now = host.Clock.Time;
            host.EnragedBreach = now - host.TrappedSince >= 3f;
            if (now >= host.NextStructureAttackAt
                && host.Clock.DeltaTime > 0f)
            {
                string targetName =
                    host.BreachTarget.BuildingData?.objectName
                    ?? host.BreachTarget.name;
                if (host.StructuralIntegrity.TryGet(
                        host.BreachTarget,
                        out BuildingStructuralIntegritySnapshot snapshot))
                {
                    float damage = InvasionIntruderCombatRules.CalculateStructuralDamage(
                        host.Actor,
                        host.Settings,
                        host.MeleeDamageMultiplier,
                        snapshot.Toughness,
                        host.EnragedBreach,
                        host.Performance);
                    BuildingStructuralDamageResult result =
                        host.StructuralIntegrity.ApplyDamage(
                            host.BreachTarget,
                            damage);
                    if (result.Applied)
                    {
                        host.Actor.AddActivity(
                            CharacterActivityEvent.Facility(
                                CharacterActivityKinds.Combat,
                                CharacterActivityOutcomes.Damaged,
                                $"{targetName} 돌파 {result.Damage:0.#}",
                                host.BreachTarget,
                                actionId: "invasion:breach-structure",
                                reasonCode: host.EnragedBreach
                                    ? "enraged-breach"
                                    : "breach",
                                value: result.Damage,
                                bubbleEligible: true));
                    }
                }

                float interval = Mathf.Max(
                    0.1f,
                    host.Settings.structureAttackIntervalSeconds)
                    / host.AttackSpeedMultiplier;
                if (host.EnragedBreach)
                {
                    interval *= 0.65f;
                }

                host.NextStructureAttackAt = now + interval;
            }

            yield return null;
        }

        host.ClearBreachState();
        if (host.Actor != null && !host.Actor.IsDead)
        {
            host.State = InvasionIntruderState.Searching;
        }
    }

    private IEnumerator MoveToSafestReachableCell(
        Grid grid,
        Vector2Int destination,
        DefenseRaidAwarenessSnapshot awareness)
    {
        if (host.Actor?.PathSearchBroker == null
            || !host.Actor.PathSearchBroker.TryGetSearch(
                grid,
                host.Actor.GetNowXY(),
                out GridPathSearchResult search,
                GridPathSearchPriority.Urgent))
        {
            yield break;
        }

        Vector2Int start = host.Actor.GetNowXY();
        Vector2Int[] candidates = search.GetReachablePositions()
            .Where(position => position != start)
            .OrderBy(position => awareness != null
                && awareness.KnownRisks.TryGetValue(position, out float risk)
                    ? risk
                    : 0f)
            .ThenBy(position =>
                Mathf.Abs(position.x - destination.x)
                + Mathf.Abs(position.y - destination.y))
            .ThenBy(position => position.y)
            .ThenBy(position => position.x)
            .ToArray();
        if (candidates.Length == 0)
        {
            yield break;
        }

        Vector2Int safest = candidates[0];
        Queue<GridMoveStep> path = search.GetMovePathTo(safest);
        yield return MovePathWithDefense(grid, path);
    }

    private IEnumerator FinalCombat(CharacterActor owner)
    {
        host.State = InvasionIntruderState.FinalCombat;
        if (host.Settings.finalCombatWindupSeconds > 0f)
        {
            yield return new WaitForSeconds(
                host.Settings.finalCombatWindupSeconds);
        }

        ApplyFinalCombat(owner);
        host.Finish();
    }

    private void ResolveIntruderDefeated()
    {
        if (!host.Resolved)
        {
            host.Resolved = true;
            host.GameEventBus.Publish(new InvasionResolvedEvent(true, 1f));
        }

        host.Finish();
    }

    private void ResolveRetreated()
    {
        if (host.Resolved)
        {
            host.Finish();
            return;
        }

        host.Resolved = true;
        host.State = InvasionIntruderState.Finished;
        host.GameEventBus.RaiseAlert(
            "침입자 철수",
            "부상당한 침입자가 목표를 포기하고 물러났습니다.",
            EventAlertImportance.Low,
            "침입");
        host.GameEventBus.Publish(new InvasionResolvedEvent(true, 0.5f));
        host.Finish();
    }
}

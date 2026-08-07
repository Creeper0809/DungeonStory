using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal delegate bool CaptiveTriggerCommand(
    string captiveId,
    string trigger,
    out string failureReason);

internal sealed class CaptivityEscapeRuntime : ICaptivityEscapeRuntime
{
    private readonly CaptivityActorAccess actors;
    private readonly CaptivityActorRuntimeLookup actorRuntime;
    private readonly IGridSystemProvider gridProvider;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly IDoorAccessCommandService doorAccessCommands;
    private readonly IDoorAccessSubjectRegistry doorSubjectRegistry;
    private readonly IGameClock gameClock;
    private readonly IGameEventBus gameEventBus;
    private readonly CaptiveTriggerCommand triggerBetrayal;

    public CaptivityEscapeRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        CaptivityWorldContext world,
        CaptivitySessionContext session,
        CaptiveTriggerCommand triggerBetrayal)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime
            ?? throw new ArgumentNullException(nameof(actorRuntime));
        world = world ?? throw new ArgumentNullException(nameof(world));
        session = session ?? throw new ArgumentNullException(nameof(session));
        gridProvider = world.GridProvider;
        pathSearchBroker = world.PathSearchBroker;
        doorAccessCommands = world.DoorAccessCommands;
        doorSubjectRegistry = world.DoorSubjectRegistry;
        gameClock = session.GameClock;
        gameEventBus = session.GameEventBus;
        this.triggerBetrayal = triggerBetrayal ?? throw new ArgumentNullException(nameof(triggerBetrayal));
    }

    public void HandleInvasionStarted()
    {
        foreach (CaptiveState state in actors.States.Where(candidate =>
                     candidate != null
                     && candidate.IsActive
                     && candidate.falseCompliance).ToArray())
        {
            if (state.status is CaptivityStatus.Labor
                or CaptivityStatus.Performer)
            {
                triggerBetrayal(state.captiveId, "침공의 혼란", out _);
            }
            else if (state.status == CaptivityStatus.Confined)
            {
                TryBeginEscapeAttempt(state, "침공 중 훔친 열쇠", out _);
            }
        }
    }

    public bool TryBeginEscapeAttempt(
        CaptiveState state,
        string trigger,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterActor actor = state != null
            ? actorRuntime.Find(state.captiveId)
            : null;
        if (state == null
            || actor == null
            || !state.IsActive
            || state.status is CaptivityStatus.Escorting
                or CaptivityStatus.Interaction
                or CaptivityStatus.Performer
                or CaptivityStatus.EscapeAttempt)
        {
            failureReason = "현재 상태에서는 탈출을 시도할 수 없습니다.";
            return false;
        }

        if (!gridProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "탈출 경로를 계산할 그리드가 없습니다.";
            return false;
        }

        GridTraversalContext context = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(actor),
            DoorAccessOverrideKind.CaptiveEscape,
            GridMovementIntent.EscapeHazard);
        if (!pathSearchBroker.TryGetSearch(
                grid,
                actor.GetNowXY(),
                out GridPathSearchResult search,
                GridPathSearchPriority.Urgent,
                context))
        {
            failureReason = "탈출 경로 탐색 예산을 확보하지 못했습니다.";
            return false;
        }

        Vector2Int destination = grid.GetCells()
            .Where(cell =>
                cell != null
                && cell.AreaType == GridCellAreaType.ExteriorPath
                && cell.IsWalkableArea
                && grid.IsWalkable(cell.Position)
                && search.ContainsPosition(cell.Position))
            .OrderBy(cell => Manhattan(actor.GetNowXY(), cell.Position))
            .Select(cell => cell.Position)
            .FirstOrDefault();
        if (!search.ContainsPosition(destination)
            || grid.GetGridCell(destination)?.AreaType
                != GridCellAreaType.ExteriorPath)
        {
            failureReason = "외부까지 이어지는 탈출 경로가 없습니다.";
            return false;
        }

        state.status = CaptivityStatus.EscapeAttempt;
        state.escapeDestination = destination;
        state.betrayalTrigger = string.IsNullOrWhiteSpace(trigger)
            ? "탈출 시도"
            : trigger.Trim();
        state.restrained = false;
        state.lastResult = state.betrayalTrigger;
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(true);
        AbilityCaptiveEscape ability =
            CaptivityAbilityAdapterFactory.EnsureEscape(
                actor,
                this,
                gameClock);
        if (ability == null)
        {
            FailEscape(
                state.captiveId,
                actor,
                "탈출 행동을 시작할 수 없습니다.");
            failureReason = state.lastResult;
            return false;
        }

        ability.StartEscape(state.captiveId);
        return true;
    }

    public bool TryGetEscapeState(
        string captiveId,
        CharacterActor actor,
        out Vector2Int destination,
        out string failureReason)
    {
        CaptiveState state = actors.FindState(captiveId);
        destination = state?.escapeDestination ?? default;
        failureReason = string.Empty;
        if (state == null
            || actor == null
            || state.status != CaptivityStatus.EscapeAttempt
            || !string.Equals(
                state.captiveId,
                CaptivityActorAccess.RequireCharacterId(
                    actor?.Identity?.PersistentId),
                StringComparison.Ordinal))
        {
            failureReason = "유효한 탈출 시도가 아닙니다.";
            return false;
        }

        return true;
    }

    public IDisposable BeginEscapePass(CharacterActor actor, string captiveId)
    {
        DoorAccessSubjectRef subject = new DoorAccessSubjectRef(
            CaptivityActorAccess.RequireCharacterId(
                actor?.Identity?.PersistentId),
            DoorAccessGroup.Captive,
            character: actor);
        return doorAccessCommands.BeginTemporaryOverride(
            subject,
            DoorAccessOverrideKind.CaptiveEscape,
            $"captive-escape:{captiveId?.Trim() ?? string.Empty}");
    }

    public void CompleteEscape(string captiveId, CharacterActor actor)
    {
        CaptiveState state = actors.FindState(captiveId);
        if (state == null || actor == null)
        {
            return;
        }

        state.status = CaptivityStatus.Escaped;
        state.restrained = false;
        state.lastResult = string.IsNullOrWhiteSpace(state.betrayalTrigger)
            ? "감방에서 탈출"
            : state.betrayalTrigger;
        state.retaliationPressure = ClampStat(
            state.retaliationPressure + 15f + state.grudge * 0.2f);
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(false);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        gameEventBus.Publish(new CaptiveEscapedEvent(
            state.captiveId,
            state.lastResult,
            betrayal: false));
        actor.GetAbility<AbilityMove>()?.StartSystemExitDungeon();
    }

    public void FailEscape(
        string captiveId,
        CharacterActor actor,
        string reason)
    {
        CaptiveState state = actors.FindState(captiveId);
        if (state == null)
        {
            return;
        }

        state.status = CaptivityStatus.Confined;
        state.failedEscapeAttempts++;
        state.grudge = ClampStat(state.grudge + 6f);
        state.escapeRisk = ClampStat(state.escapeRisk + 8f);
        state.restrained = true;
        state.lastResult = string.IsNullOrWhiteSpace(reason)
            ? "탈출 실패 후 재구속"
            : $"{reason} · 재구속";
        if (actor != null)
        {
            actor.characterType = CharacterType.Intruder;
            actor.SetAiPaused(true);
            actor.SetLifecycleState(CharacterLifecycleState.Downed);
        }
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

    private static float ClampStat(float value)
    {
        return Mathf.Clamp(value, 0f, 100f);
    }
}

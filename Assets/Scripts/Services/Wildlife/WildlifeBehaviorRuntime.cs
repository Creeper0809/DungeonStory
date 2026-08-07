using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class WildlifeBehaviorRuntime
{
    public delegate bool ArrivalSpawner(
        string speciesId,
        Vector2Int preferredPosition,
        out WildlifeActor actor,
        out string message);

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWildlifeEcosystemRuntime ecosystemRuntime;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWildlifeCarcassService carcassService;
    private readonly IRandomStream randomStream;
    private readonly List<WildlifeActor> wildlife;
    private readonly Dictionary<string, float> nextBehaviorTickByWildlifeId;
    private readonly List<WildlifeFoodRaidOrderSaveData> foodRaidOrders;
    private readonly ArrivalSpawner spawnArrival;
    private WorldItemStackSnapshot[] cachedItemStacks =
        Array.Empty<WorldItemStackSnapshot>();
    private int cachedItemStackVersion = -1;

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public WildlifeBehaviorRuntime(
        WildlifeWorldServices world,
        WildlifeCombatServices combat,
        WildlifeExecutionServices execution,
        List<WildlifeActor> wildlife,
        Dictionary<string, float> nextBehaviorTicks,
        List<WildlifeFoodRaidOrderSaveData> foodRaidOrders,
        ArrivalSpawner spawnArrival)
    {
        WildlifeWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        gridSystemProvider = requiredWorld.Grid;
        ecosystemRuntime = requiredWorld.Ecosystem;
        pathSearchBroker = requiredWorld.PathSearch;
        worldRegistry = requiredWorld.WorldRegistry;
        itemStackRuntime = requiredWorld.Items;
        carcassService = (combat ?? throw new ArgumentNullException(nameof(combat))).Carcasses;
        randomStream = (execution ?? throw new ArgumentNullException(nameof(execution)))
            .RandomStreams.Get("wildlife.runtime");
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        nextBehaviorTickByWildlifeId = nextBehaviorTicks
            ?? throw new ArgumentNullException(nameof(nextBehaviorTicks));
        this.foodRaidOrders = foodRaidOrders
            ?? throw new ArgumentNullException(nameof(foodRaidOrders));
        this.spawnArrival = spawnArrival
            ?? throw new ArgumentNullException(nameof(spawnArrival));
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetReachableFoodRaidTargets()
    {
        if (itemStackRuntime == null
            || !gridSystemProvider.TryGetGrid(out Grid grid)
            || !TryFindFoodRaidEntry(grid, out Vector2Int entry))
        {
            return Array.Empty<WorldItemStackSnapshot>();
        }

        return itemStackRuntime.GetAllStacks()
            .Where(stack => IsLooseRaidFood(stack)
                && IsReachableFoodRaidTarget(grid, entry, stack.Position))
            .OrderBy(stack => Manhattan(entry, stack.Position))
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<WildlifeFoodRaidOrderSnapshot> GetFoodRaidOrders()
    {
        return foodRaidOrders.Select(ToFoodRaidSnapshot).ToArray();
    }

    public bool TryBeginFoodRaid(
        string raidId,
        int wolfCount,
        out IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders,
        out string failureReason)
    {
        orders = Array.Empty<WildlifeFoodRaidOrderSnapshot>();
        string normalizedRaidId = raidId?.Trim() ?? string.Empty;
        if (normalizedRaidId.Length == 0)
        {
            failureReason = "습격 ID가 필요합니다.";
            return false;
        }

        if (foodRaidOrders.Any(order =>
                order != null
                && !IsFoodRaidTerminal(order.state)))
        {
            failureReason = "이미 진행 중인 식량 습격이 있습니다.";
            return false;
        }

        IReadOnlyList<WorldItemStackSnapshot> targets =
            GetReachableFoodRaidTargets();
        if (!gridSystemProvider.TryGetGrid(out Grid grid)
            || !TryFindFoodRaidEntry(grid, out Vector2Int entry))
        {
            failureReason = "외부 진입로에 유효한 늑대 출현 지점이 없습니다.";
            return false;
        }

        foodRaidOrders.Clear();
        int requested = Mathf.Max(1, wolfCount);
        string lastSpawnFailure = string.Empty;
        for (int index = 0; index < requested; index++)
        {
            if (!spawnArrival(
                    "shadow_wolf",
                    entry,
                    out WildlifeActor wolf,
                    out lastSpawnFailure))
            {
                continue;
            }

            WorldItemStackSnapshot target = targets.Count > 0
                ? targets[index % targets.Count]
                : null;
            foodRaidOrders.Add(new WildlifeFoodRaidOrderSaveData
            {
                raidId = normalizedRaidId,
                wildlifeId = wolf.WildlifeId,
                targetStackId = target?.StackId ?? string.Empty,
                state = WildlifeFoodRaidOrderState.Approaching,
                stolenQuantity = 0,
                outcomeReason = string.Empty
            });
            wolf.SetIntent(
                WildlifeIntent.Forage,
                target != null
                    ? $"노출 식량 {target.DisplayName}을 노리는 중"
                    : "노출 식량을 찾는 중");
        }

        orders = GetFoodRaidOrders();
        if (orders.Count == 0)
        {
            failureReason = string.IsNullOrWhiteSpace(lastSpawnFailure)
                ? "습격 늑대를 출현시키지 못했습니다."
                : lastSpawnFailure;
            return false;
        }

        failureReason = string.Empty;
        return true;
    }
    public bool TickFoodRaid(WildlifeActor actor, Grid grid, float now)
    {
        WildlifeFoodRaidOrderSaveData order = foodRaidOrders.FirstOrDefault(
            candidate => candidate != null
                && candidate.state == WildlifeFoodRaidOrderState.Approaching
                && string.Equals(
                    candidate.wildlifeId,
                    actor?.WildlifeId,
                    StringComparison.Ordinal));
        if (order == null)
        {
            return false;
        }

        if (actor == null || !actor.IsAlive)
        {
            order.state = WildlifeFoodRaidOrderState.Cancelled;
            order.outcomeReason =
                "습격 개체가 처치되어 도난이 취소되었습니다.";
            return true;
        }

        WorldItemStackSnapshot target = itemStackRuntime?.GetAllStacks()
            .FirstOrDefault(stack => IsLooseRaidFood(stack)
                && string.Equals(
                    stack.StackId,
                    order.targetStackId,
                    StringComparison.Ordinal));
        if (target == null)
        {
            target = FindReachableFoodRaidTarget(actor, grid, now);
            if (target == null)
            {
                order.state = WildlifeFoodRaidOrderState.Failed;
                order.outcomeReason =
                    "도달 가능한 노출 식량이 없어 아무것도 훔치지 못했습니다.";
                actor.MarkLeaving();
                return true;
            }

            order.targetStackId = target.StackId;
        }

        actor.SetIntent(
            WildlifeIntent.Forage,
            $"노출 식량 {target.DisplayName}을 노리는 중");
        if (actor.GridPosition == target.Position)
        {
            if (itemStackRuntime.TryConsumeStackQuantity(
                    target.StackId,
                    1,
                    out WorldItemStackSnapshot consumed))
            {
                order.stolenQuantity = consumed?.Quantity ?? 0;
                order.state = WildlifeFoodRaidOrderState.Stolen;
                order.outcomeReason =
                    order.stolenQuantity > 0
                        ? "늑대가 식량에 도달해 1개를 훔쳤습니다."
                        : "식량이 먼저 사라져 아무것도 훔치지 못했습니다.";
            }
            else
            {
                order.state = WildlifeFoodRaidOrderState.Failed;
                order.outcomeReason =
                    "식량이 먼저 사라져 아무것도 훔치지 못했습니다.";
            }

            actor.MarkLeaving();
            return true;
        }

        if (!actor.CanRepath(now))
        {
            return true;
        }

        if (actor.TrySetPath(target.Position, now))
        {
            return true;
        }

        WorldItemStackSnapshot replacement =
            FindReachableFoodRaidTarget(actor, grid, now);
        if (replacement == null
            || !actor.TrySetPath(replacement.Position, now))
        {
            order.state = WildlifeFoodRaidOrderState.Failed;
            order.outcomeReason =
                "문 또는 지형에 막혀 도달 가능한 노출 식량이 없습니다.";
            actor.MarkLeaving();
            return true;
        }

        order.targetStackId = replacement.StackId;
        return true;
    }

    private WorldItemStackSnapshot FindReachableFoodRaidTarget(
        WildlifeActor actor,
        Grid grid,
        float now)
    {
        if (actor == null || grid == null || itemStackRuntime == null)
        {
            return null;
        }

        foreach (WorldItemStackSnapshot candidate in itemStackRuntime
                     .GetAllStacks()
                     .Where(IsLooseRaidFood)
                     .OrderBy(stack =>
                         Manhattan(actor.GridPosition, stack.Position))
                     .ThenBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            if (candidate.Position == actor.GridPosition)
            {
                return candidate;
            }

            Queue<GridMoveStep> path = pathSearchBroker?.GetMovePathTo(
                grid,
                actor.GridPosition,
                candidate.Position,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForWildlife(actor.WildlifeId));
            path ??= grid.GetMovePathTo(
                actor.GridPosition,
                candidate.Position);
            if (path != null && path.Count > 0)
            {
                return candidate;
            }
        }

        return null;
    }

    public void CancelFoodRaidForActor(
        string wildlifeId,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(wildlifeId))
        {
            return;
        }

        foreach (WildlifeFoodRaidOrderSaveData order in foodRaidOrders)
        {
            if (order == null
                || IsFoodRaidTerminal(order.state)
                || !string.Equals(
                    order.wildlifeId,
                    wildlifeId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            order.state = WildlifeFoodRaidOrderState.Cancelled;
            order.outcomeReason = reason ?? string.Empty;
        }
    }

    public void CompleteLeavingFoodRaidForActor(string wildlifeId)
    {
        WildlifeFoodRaidOrderSaveData order = foodRaidOrders.FirstOrDefault(
            candidate => candidate != null
                && candidate.state == WildlifeFoodRaidOrderState.Leaving
                && string.Equals(
                    candidate.wildlifeId,
                    wildlifeId,
                    StringComparison.Ordinal));
        if (order != null)
        {
            order.state = order.stolenQuantity > 0
                ? WildlifeFoodRaidOrderState.Stolen
                : WildlifeFoodRaidOrderState.Failed;
        }
    }

    public void TickBehavior(WildlifeActor actor, Grid grid, float now)
    {
        if (actor == null || !actor.IsAlive || !actor.CanRepath(now))
        {
            return;
        }

        Vector2Int target = actor.State switch
        {
            WildlifeState.Fleeing => ChooseFleePosition(actor, grid),
            WildlifeState.Hunted => ChooseHuntedMovePosition(actor, grid),
            WildlifeState.Retaliating => ChooseHuntedMovePosition(actor, grid),
            WildlifeState.PredatorStalking => ChooseEcologyOrPredatorPosition(actor, grid),
            WildlifeState.Leaving => ChooseLeavingPosition(actor, grid),
            _ => ChooseEcologyOrWanderPosition(actor, grid)
        };

        actor.TrySetPath(target, now);
    }

    private Vector2Int ChooseEcologyOrPredatorPosition(WildlifeActor actor, Grid grid)
    {
        if (ecosystemRuntime != null
            && ecosystemRuntime.TryChooseEcologyTarget(
                actor,
                grid,
                wildlife,
                GetCachedItemStacks(),
                out Vector2Int target,
                out WildlifeIntent intent,
                out string reason))
        {
            actor.SetIntent(intent, reason);
            if (intent == WildlifeIntent.LeaveMap)
            {
                actor.MarkLeaving();
            }
            else if (intent != WildlifeIntent.HuntPrey)
            {
                actor.SetGrazing();
            }

            return target;
        }

        return ChoosePredatorPosition(actor, grid);
    }

    private Vector2Int ChooseLeavingPosition(WildlifeActor actor, Grid grid)
    {
        int exitX = actor.GridPosition.x < grid.width * 0.5f ? 0 : grid.width - 1;
        Vector2Int target = new Vector2Int(exitX, actor.GridPosition.y);
        if (CanWildlifeRoamTargetAt(grid, target, actor.CanEnterDungeon))
        {
            actor.SetIntent(WildlifeIntent.LeaveMap, "먹이와 물을 찾아 지역을 떠남");
            return target;
        }

        return ChooseReachablePosition(actor, grid, minDistance: 4, maxDistance: 10, preferAwayFrom: actor.TerritoryCenter);
    }

    private Vector2Int ChooseEcologyOrWanderPosition(WildlifeActor actor, Grid grid)
    {
        if (ecosystemRuntime != null
            && ecosystemRuntime.TryChooseEcologyTarget(
                actor,
                grid,
                wildlife,
                GetCachedItemStacks(),
                out Vector2Int target,
                out WildlifeIntent intent,
                out string reason))
        {
            actor.SetIntent(intent, reason);
            switch (intent)
            {
                case WildlifeIntent.Forage:
                case WildlifeIntent.Drink:
                    actor.SetGrazing();
                    break;
                case WildlifeIntent.HuntPrey:
                    actor.SetPredatorStalking();
                    break;
                case WildlifeIntent.LeaveMap:
                    actor.MarkLeaving();
                    break;
                default:
                    actor.SetIdle();
                    break;
            }

            return target;
        }

        return ChooseWanderPosition(actor, grid);
    }

    private Vector2Int ChooseWanderPosition(WildlifeActor actor, Grid grid)
    {
        if (actor.Fear >= 4f || (actor.HasLastThreatPosition && actor.LastThreatAge < 12f))
        {
            actor.SetIntent(WildlifeIntent.Flee, "위협을 피해 도망");
            return ChooseFleePosition(actor, grid);
        }

        if (actor.Species != null
            && actor.Species.IsPredator
            && (actor.Hunger >= 0.55f || randomStream.Chance(actor.Species.Aggression * 0.18f)))
        {
            actor.SetIntent(WildlifeIntent.HuntPrey, "먹잇감을 찾는 중");
            actor.SetPredatorStalking();
            return ChoosePredatorPosition(actor, grid);
        }

        actor.SetIntent(WildlifeIntent.Wander, "영역 안을 배회");
        actor.SetGrazing();
        return ChooseReachablePosition(actor, grid, minDistance: 2, maxDistance: 6, preferAwayFrom: null);
    }

    private Vector2Int ChooseHuntedMovePosition(WildlifeActor actor, Grid grid)
    {
        CharacterActor hunter = FindCharacterByPersistentId(actor.ReservedByPersistentId);
        if (hunter != null)
        {
            return ChooseReachablePosition(actor, grid, minDistance: 3, maxDistance: 8, preferAwayFrom: hunter.GetNowXY());
        }

        return ChooseFleePosition(actor, grid);
    }

    private Vector2Int ChooseFleePosition(WildlifeActor actor, Grid grid)
    {
        CharacterActor nearest = FindNearestWorker(actor.GridPosition);
        Vector2Int? awayFrom = actor.HasLastThreatPosition && actor.LastThreatAge < 20f
            ? actor.LastThreatPosition
            : nearest != null ? nearest.GetNowXY() : null;
        return ChooseReachablePosition(actor, grid, minDistance: 4, maxDistance: 10, preferAwayFrom: awayFrom);
    }

    private Vector2Int ChoosePredatorPosition(WildlifeActor actor, Grid grid)
    {
        CharacterActor target = FindBestPredatorTarget(actor);
        if (target == null)
        {
            return ChooseReachablePosition(actor, grid, minDistance: 2, maxDistance: 6, preferAwayFrom: null);
        }

        return target.GetNowXY();
    }

    private Vector2Int ChooseReachablePosition(
        WildlifeActor actor,
        Grid grid,
        int minDistance,
        int maxDistance,
        Vector2Int? preferAwayFrom)
    {
        Vector2Int origin = actor.GridPosition;
        Vector2Int best = origin;
        float bestScore = float.NegativeInfinity;
        int samples = 0;
        int clampedMin = Mathf.Max(1, minDistance);
        int clampedMax = Mathf.Max(clampedMin, maxDistance);
        for (int distance = clampedMin; distance <= clampedMax; distance++)
        {
            for (int direction = -1; direction <= 1; direction += 2)
            {
                Vector2Int candidate = new Vector2Int(origin.x + direction * distance, origin.y);
                if (!CanWildlifeRoamTargetAt(grid, candidate, actor.CanEnterDungeon))
                {
                    continue;
                }

                float score = ScoreWildlifeMovePosition(actor, grid, candidate, preferAwayFrom);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }

                samples++;
            }
        }

        if (samples <= 0)
        {
            return origin;
        }

        Vector2Int selected = best;
        float accumulatedWeight = 0f;
        const float viableScoreRange = 5f;
        for (int distance = clampedMin; distance <= clampedMax; distance++)
        {
            for (int direction = -1; direction <= 1; direction += 2)
            {
                Vector2Int candidate = new Vector2Int(origin.x + direction * distance, origin.y);
                if (!CanWildlifeRoamTargetAt(grid, candidate, actor.CanEnterDungeon))
                {
                    continue;
                }

                float score = ScoreWildlifeMovePosition(actor, grid, candidate, preferAwayFrom);
                if (score < bestScore - viableScoreRange)
                {
                    continue;
                }

                float weight = Mathf.Exp((score - bestScore) * 0.55f);
                accumulatedWeight += weight;
                if (randomStream.NextFloat() * accumulatedWeight <= weight)
                {
                    selected = candidate;
                }
            }
        }

        return selected;
    }

    private float ScoreWildlifeMovePosition(
        WildlifeActor actor,
        Grid grid,
        Vector2Int position,
        Vector2Int? preferAwayFrom)
    {
        float score = 0f;
        GridCell cell = grid.GetGridCell(position);
        GridCellAreaType areaType = cell != null ? cell.AreaType : GridCellAreaType.BlockedExterior;
        if (preferAwayFrom.HasValue)
        {
            Vector2Int threat = preferAwayFrom.Value;
            int distanceFromThreat = Mathf.Abs(position.x - threat.x) + Mathf.Abs(position.y - threat.y);
            score += distanceFromThreat * 4f;
        }
        else
        {
            int territoryDistance = Mathf.Abs(position.x - actor.TerritoryCenter.x)
                + Mathf.Abs(position.y - actor.TerritoryCenter.y);
            int herdDistance = Mathf.Abs(position.x - actor.HerdAnchorPosition.x)
                + Mathf.Abs(position.y - actor.HerdAnchorPosition.y);
            score += Mathf.Clamp(12f - territoryDistance, -8f, 12f);
            score += Mathf.Clamp(7f - herdDistance, -4f, 7f);
            score += actor.Hunger * (areaType == GridCellAreaType.ExteriorPath ? 4f : 1f);

            int direction = Mathf.RoundToInt(Mathf.Sign(position.x - actor.GridPosition.x));
            if (direction != 0 && actor.LastHorizontalDirection != 0)
            {
                score += direction == actor.LastHorizontalDirection ? 2.6f : -2.1f;
            }
        }

        if (areaType == GridCellAreaType.Entrance)
        {
            score -= actor.CanEnterDungeon ? 1.5f : 7f;
        }
        else if (areaType == GridCellAreaType.DropZone)
        {
            score -= 2f;
        }
        else if (areaType == GridCellAreaType.DungeonInterior && !actor.CanEnterDungeon)
        {
            score -= 30f;
        }

        score -= CountNearbyCharacters(position, 3) * (actor.Species != null && actor.Species.IsPredator ? 0.8f : 2.6f);
        return score;
    }
    private bool TryFindFoodRaidEntry(
        Grid grid,
        out Vector2Int entryPosition)
    {
        entryPosition = default;
        if (grid == null)
        {
            return false;
        }

        GridCell entrance = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.Entrance)
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (entrance != null
            && TryFindNearestInitialSpawnCell(
                grid,
                entrance.Position,
                out entryPosition))
        {
            return true;
        }

        GridCell exterior = grid.GetCells()
            .FirstOrDefault(cell => IsInitialWildlifeSpawnCell(grid, cell));
        if (exterior == null)
        {
            return false;
        }

        entryPosition = exterior.Position;
        return true;
    }

    private bool TryFindNearestInitialSpawnCell(
        Grid grid,
        Vector2Int origin,
        out Vector2Int position)
    {
        position = default;
        if (grid == null)
        {
            return false;
        }

        int maxRadius = Mathf.Max(grid.width, grid.height);
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != radius)
                    {
                        continue;
                    }

                    Vector2Int candidate = origin + new Vector2Int(dx, dy);
                    if (grid.IsValidGridPos(candidate)
                        && IsInitialWildlifeSpawnCell(
                            grid,
                            grid.GetGridCell(candidate)))
                    {
                        position = candidate;
                        return true;
                    }
                }
            }
        }

        GridCell fallback = grid.GetCells()
            .FirstOrDefault(cell => IsInitialWildlifeSpawnCell(grid, cell));
        if (fallback == null)
        {
            return false;
        }

        position = fallback.Position;
        return true;
    }

    private static bool IsInitialWildlifeSpawnCell(Grid grid, GridCell cell)
    {
        return cell != null
            && grid != null
            && cell.AreaType == GridCellAreaType.ExteriorPath
            && grid.IsWalkable(cell.Position)
            && IsOutdoorSurfaceCell(grid, cell)
            && !cell.HasOccupantInLayer(GridLayer.Wildlife);
    }

    private static bool IsReachableFoodRaidTarget(
        Grid grid,
        Vector2Int entry,
        Vector2Int target)
    {
        if (grid == null || !grid.IsValidGridPos(target))
        {
            return false;
        }

        return entry == target
            || grid.GetMovePathTo(entry, target)?.Count > 0;
    }

    private static bool IsLooseRaidFood(WorldItemStackSnapshot stack)
    {
        return stack != null
            && IsRaidFoodEligible(
                stack.State,
                stack.StockCategory,
                stack.Quantity);
    }

    public static bool IsRaidFoodEligible(
        WorldItemStackState state,
        StockCategory category,
        int quantity)
    {
        return state == WorldItemStackState.Loose
            && category == StockCategory.Food
            && quantity > 0;
    }

    private static bool IsFoodRaidTerminal(
        WildlifeFoodRaidOrderState state)
    {
        return state == WildlifeFoodRaidOrderState.Stolen
            || state == WildlifeFoodRaidOrderState.Cancelled
            || state == WildlifeFoodRaidOrderState.Failed;
    }

    public static WildlifeFoodRaidOrderSaveData CloneFoodRaidOrder(
        WildlifeFoodRaidOrderSaveData source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new WildlifeFoodRaidOrderSaveData
        {
            raidId = source.raidId,
            wildlifeId = source.wildlifeId,
            targetStackId = source.targetStackId,
            state = source.state,
            stolenQuantity = source.stolenQuantity,
            outcomeReason = source.outcomeReason
        };
    }

    private static WildlifeFoodRaidOrderSnapshot ToFoodRaidSnapshot(
        WildlifeFoodRaidOrderSaveData source)
    {
        WildlifeFoodRaidOrderSaveData value =
            source ?? new WildlifeFoodRaidOrderSaveData();
        return new WildlifeFoodRaidOrderSnapshot(
            value.raidId,
            value.wildlifeId,
            value.targetStackId,
            value.state,
            value.stolenQuantity,
            value.outcomeReason);
    }
    private bool CanWildlifeRoamTargetAt(Grid grid, Vector2Int position, bool canEnterDungeon)
    {
        GridCell cell = grid?.GetGridCell(position);
        if (cell == null
            || cell.AreaType == GridCellAreaType.DropZone
            || cell.AreaType == GridCellAreaType.Entrance)
        {
            return false;
        }

        return CanSpawnAt(grid, position, canEnterDungeon);
    }

    public bool CanSpawnAt(Grid grid, Vector2Int position, bool canEnterDungeon)
    {
        GridCell cell = grid?.GetGridCell(position);
        if (cell == null || !grid.IsWalkable(position) || cell.HasOccupantInLayer(GridLayer.Wildlife))
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.BlockedExterior)
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath
            && !IsOutdoorSurfaceCell(grid, cell))
        {
            return false;
        }

        return canEnterDungeon || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    public static bool IsOutdoorSurfaceCell(Grid grid, GridCell cell)
    {
        if (grid == null || cell == null || cell.AreaType != GridCellAreaType.ExteriorPath)
        {
            return false;
        }

        if (cell.Position.y > 0)
        {
            return false;
        }

        Vector2Int belowPosition = new Vector2Int(cell.Position.x, cell.Position.y - 1);
        GridCell below = grid.GetGridCell(belowPosition);
        return below == null || below.AreaType == GridCellAreaType.BlockedExterior;
    }
    private CharacterActor FindCharacterByPersistentId(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return null;
        }

        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor != null
                && actor.Identity != null
                && string.Equals(actor.Identity.PersistentId, persistentId, StringComparison.Ordinal))
            {
                return actor;
            }
        }

        return null;
    }

    private CharacterActor FindNearestWorker(Vector2Int position)
    {
        CharacterActor best = null;
        int bestDistance = int.MaxValue;
        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor == null || actor.IsDead || !CharacterWorkRoleUtility.TryGetWork(actor, out _))
            {
                continue;
            }

            Vector2Int actorPosition = actor.GetNowXY();
            int distance = Mathf.Abs(actorPosition.x - position.x) + Mathf.Abs(actorPosition.y - position.y);
            if (best != null && distance >= bestDistance)
            {
                continue;
            }

            best = actor;
            bestDistance = distance;
        }

        return best;
    }

    private CharacterActor FindBestPredatorTarget(WildlifeActor predator)
    {
        if (predator == null)
        {
            return null;
        }

        CharacterActor best = null;
        float bestScore = float.MinValue;
        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            Vector2Int actorPosition = actor.GetNowXY();
            int distance = Mathf.Abs(actorPosition.x - predator.GridPosition.x)
                + Mathf.Abs(actorPosition.y - predator.GridPosition.y);
            if (distance > 10)
            {
                continue;
            }

            float healthWeakness = actor.MaxHealth > 0
                ? Mathf.Clamp01(1f - actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth))
                : 0f;
            float workerPenalty = CharacterWorkRoleUtility.TryGetWork(actor, out _) ? 0.2f : 0f;
            float score = healthWeakness * 5f
                + Mathf.Clamp(10f - distance, 0f, 10f) * 0.45f
                + predator.Hunger * 3f
                - workerPenalty;
            if (best == null || score > bestScore)
            {
                best = actor;
                bestScore = score;
            }
        }

        return best;
    }

    public bool TryResolvePredatorWildlifeContact(WildlifeActor predator)
    {
        if (predator == null
            || !predator.IsAlive
            || predator.Species == null
            || predator.Species.Diet != WildlifeDietType.Carnivore
            || predator.Hunger < 0.45f)
        {
            return false;
        }

        WildlifeActor prey = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < wildlife.Count; i++)
        {
            WildlifeActor candidate = wildlife[i];
            if (candidate == null
                || candidate == predator
                || !candidate.IsAlive
                || candidate.Species == null
                || candidate.Species.Diet == WildlifeDietType.Carnivore
                || !IsAdjacentCell(predator.GridPosition, candidate.GridPosition))
            {
                continue;
            }

            float weakness = candidate.MaxHealth > 0
                ? 1f - (candidate.CurrentHealth / (float)candidate.MaxHealth)
                : 0f;
            float score = weakness * 5f
                + Mathf.Clamp(predator.MaxHealth - candidate.MaxHealth, -8f, 12f)
                - (candidate.IsDangerous ? 6f : 0f);
            if (prey == null || score > bestScore)
            {
                prey = candidate;
                bestScore = score;
            }
        }

        if (prey == null)
        {
            return false;
        }

        int damage = Mathf.Max(
            1,
            Mathf.RoundToInt((predator.RetaliationDamage * 0.75f) + (predator.MaxHealth * 0.12f)));
        prey.RegisterThreat(predator.GridPosition, 0.65f);
        prey.ApplyDamage(damage, null);
        predator.SetIntent(WildlifeIntent.HuntPrey, prey.IsAlive ? "먹잇감을 몰아붙이는 중" : "먹잇감을 쓰러뜨림");
        predator.ChangeHunger(-0.18f);
        if (prey.IsAlive)
        {
            return true;
        }

        CancelFoodRaidForActor(
            prey.WildlifeId,
            "습격 늑대가 처치되어 도난이 취소되었습니다.");
        ecosystemRuntime?.NotifyWildlifeKilled(prey, byHunt: false);
        carcassService?.SpawnCarcass(prey);
        wildlife.Remove(prey);
        nextBehaviorTickByWildlifeId.Remove(prey.WildlifeId);
        UnityEngine.Object.Destroy(prey.gameObject);
        predator.ChangeHunger(-0.45f);
        return true;
    }

    private static bool IsAdjacentCell(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) <= 1
            && a != b;
    }

    private IReadOnlyList<WorldItemStackSnapshot> GetCachedItemStacks()
    {
        IWorldItemStackRuntime runtime = itemStackRuntime;
        if (runtime == null)
        {
            cachedItemStackVersion = -1;
            cachedItemStacks = Array.Empty<WorldItemStackSnapshot>();
            return cachedItemStacks;
        }

        if (cachedItemStackVersion == runtime.ItemStackVersion)
        {
            return cachedItemStacks;
        }

        cachedItemStackVersion = runtime.ItemStackVersion;
        cachedItemStacks = runtime.GetAllStacks()
            .Where(stack => stack != null)
            .ToArray();
        return cachedItemStacks;
    }

    private int CountNearbyCharacters(Vector2Int position, int radius)
    {
        int count = 0;
        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            Vector2Int actorPosition = actor.GetNowXY();
            int distance = Mathf.Abs(actorPosition.x - position.x) + Mathf.Abs(actorPosition.y - position.y);
            if (distance <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private float NextRange(float minInclusive, float maxInclusive)
    {
        return Mathf.Lerp(minInclusive, maxInclusive, randomStream.NextFloat());
    }
}

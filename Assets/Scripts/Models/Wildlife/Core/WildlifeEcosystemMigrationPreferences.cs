using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WildlifeEcosystemRuntime
{
    private const float MigrationSignalDistancePenalty = 0.35f;

    private static readonly IReadOnlyDictionary<
        WildlifeMigrationCapability,
        IWildlifeMigrationPreferenceStrategy> MigrationPreferenceStrategies =
        CreateMigrationPreferenceStrategies();

    private bool TryFindMigrationPreferenceTarget(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid,
        IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
        out Vector2Int target,
        out string reason)
    {
        target = actor?.GridPosition ?? Vector2Int.zero;
        reason = string.Empty;
        WildlifeMigrationProfile profile = actor?.Species?.MigrationProfile
            ?? throw new InvalidOperationException(
                $"Wildlife '{actor?.WildlifeId ?? "<missing>"}' has no migration profile.");
        if (!MigrationPreferenceStrategies.TryGetValue(
                profile.Capability,
                out IWildlifeMigrationPreferenceStrategy strategy))
        {
            throw new InvalidOperationException(
                $"Wildlife migration capability '{profile.Capability}' has no registered strategy.");
        }

        return strategy.TryChoose(
            this,
            actor,
            grid,
            itemStacks,
            profile,
            out target,
            out reason);
    }

    private static IReadOnlyDictionary<
        WildlifeMigrationCapability,
        IWildlifeMigrationPreferenceStrategy>
        CreateMigrationPreferenceStrategies()
    {
        IWildlifeMigrationPreferenceStrategy[] strategies =
        {
            new GeneralHabitatMigrationPreferenceStrategy(),
            new LoreOnlyMigrationPreferenceStrategy(),
            new FoulWaterMigrationPreferenceStrategy(),
            new TemperatureBandMigrationPreferenceStrategy(),
            new CarcassMigrationPreferenceStrategy()
        };
        Dictionary<
            WildlifeMigrationCapability,
            IWildlifeMigrationPreferenceStrategy> byCapability = new();
        foreach (IWildlifeMigrationPreferenceStrategy strategy in strategies)
        {
            if (!byCapability.TryAdd(strategy.Capability, strategy))
            {
                throw new InvalidOperationException(
                    $"Duplicate wildlife migration strategy '{strategy.Capability}'.");
            }
        }

        return byCapability;
    }

    private bool TryResolveSignalAccessTarget(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid,
        Vector2Int signalOrigin,
        int detectionRadius,
        out Vector2Int target)
    {
        target = actor.GridPosition;
        if (!grid.IsValidGridPos(signalOrigin)
            || !IsWithinDetection(actor.GridPosition, signalOrigin, detectionRadius))
        {
            return false;
        }

        if (signalOrigin == actor.GridPosition)
        {
            return IsWithinTerritory(actor, actor.GridPosition);
        }

        return TryFindSurfaceNear(grid, actor, signalOrigin, out target)
            && IsWithinDetection(actor.GridPosition, target, detectionRadius)
            && IsWithinTerritory(actor, target);
    }

    private bool IsActorAtWaterSource(
        IWildlifeAnimalPort actor,
        string sourceId)
    {
        if (actor == null || string.IsNullOrWhiteSpace(sourceId))
        {
            return false;
        }

        foreach (WildlifeHabitatPatch patch in patches)
        {
            if (patch != null
                && string.Equals(
                    patch.LinkedWaterSourceId,
                    sourceId,
                    StringComparison.Ordinal)
                && patch.Contains(actor.GridPosition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanUsePreferenceTarget(
        IWildlifeGridPort grid,
        IWildlifeAnimalPort actor,
        Vector2Int target)
    {
        if (target == actor.GridPosition)
        {
            return grid != null && grid.IsValidGridPos(target);
        }

        return CanAnimalUseCell(grid, actor, target);
    }

    private static bool IsWithinDetection(
        Vector2Int origin,
        Vector2Int target,
        int radius) =>
        Manhattan(origin, target) <= radius;

    private static bool IsWithinTerritory(
        IWildlifeAnimalPort actor,
        Vector2Int target) =>
        actor?.Species != null
        && Manhattan(actor.TerritoryCenter, target)
            <= Mathf.CeilToInt(actor.Species.TerritoryRadius);

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    private static bool IsBetterMigrationCandidate(
        float score,
        Vector2Int target,
        bool found,
        float bestScore,
        Vector2Int bestTarget)
    {
        if (!found || score > bestScore)
        {
            return true;
        }
        if (!Mathf.Approximately(score, bestScore))
        {
            return false;
        }

        return target.x < bestTarget.x
            || target.x == bestTarget.x && target.y < bestTarget.y;
    }

    private interface IWildlifeMigrationPreferenceStrategy
    {
        WildlifeMigrationCapability Capability { get; }

        bool TryChoose(
            WildlifeEcosystemRuntime runtime,
            IWildlifeAnimalPort actor,
            IWildlifeGridPort grid,
            IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
            WildlifeMigrationProfile profile,
            out Vector2Int target,
            out string reason);
    }

    private sealed class GeneralHabitatMigrationPreferenceStrategy :
        IWildlifeMigrationPreferenceStrategy
    {
        public WildlifeMigrationCapability Capability =>
            WildlifeMigrationCapability.GeneralHabitat;

        public bool TryChoose(
            WildlifeEcosystemRuntime runtime,
            IWildlifeAnimalPort actor,
            IWildlifeGridPort grid,
            IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
            WildlifeMigrationProfile profile,
            out Vector2Int target,
            out string reason)
        {
            target = actor.GridPosition;
            reason = "일반 서식 이동 정책";
            return false;
        }
    }

    private sealed class LoreOnlyMigrationPreferenceStrategy :
        IWildlifeMigrationPreferenceStrategy
    {
        public WildlifeMigrationCapability Capability =>
            WildlifeMigrationCapability.LoreOnly;

        public bool TryChoose(
            WildlifeEcosystemRuntime runtime,
            IWildlifeAnimalPort actor,
            IWildlifeGridPort grid,
            IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
            WildlifeMigrationProfile profile,
            out Vector2Int target,
            out string reason)
        {
            target = actor.GridPosition;
            reason = "설정 전용 이동 양식 — 일반 서식 이동 정책";
            return false;
        }
    }

    private sealed class FoulWaterMigrationPreferenceStrategy :
        IWildlifeMigrationPreferenceStrategy
    {
        public WildlifeMigrationCapability Capability =>
            WildlifeMigrationCapability.FoulWater;

        public bool TryChoose(
            WildlifeEcosystemRuntime runtime,
            IWildlifeAnimalPort actor,
            IWildlifeGridPort grid,
            IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
            WildlifeMigrationProfile profile,
            out Vector2Int target,
            out string reason)
        {
            target = actor.GridPosition;
            reason = "감지 범위 안에 선호하는 오염 수원이 없음";
            bool found = false;
            float bestScore = float.NegativeInfinity;
            Vector2Int bestTarget = target;
            IReadOnlyList<WildlifeWaterSourceSnapshot> sources =
                runtime.world.GetWaterSources()
                ?? Array.Empty<WildlifeWaterSourceSnapshot>();
            foreach (WildlifeWaterSourceSnapshot source in sources)
            {
                float remainingRatio = source.Capacity > 0f
                    ? source.Remaining / source.Capacity
                    : 0f;
                if (!source.Foul
                    || remainingRatio < profile.MinimumWaterRemainingRatio
                    || !IsWithinDetection(
                        actor.GridPosition,
                        source.Position,
                        profile.DetectionRadiusCells))
                {
                    continue;
                }

                Vector2Int candidate;
                if (runtime.IsActorAtWaterSource(actor, source.SourceId))
                {
                    candidate = actor.GridPosition;
                }
                else if (!runtime.TryResolveSignalAccessTarget(
                             actor,
                             grid,
                             source.Position,
                             profile.DetectionRadiusCells,
                             out candidate))
                {
                    continue;
                }

                if (!IsWithinDetection(
                        actor.GridPosition,
                        candidate,
                        profile.DetectionRadiusCells)
                    || !IsWithinTerritory(actor, candidate))
                {
                    continue;
                }

                float score = profile.SignalScore
                    - Manhattan(actor.GridPosition, candidate)
                    * MigrationSignalDistancePenalty;
                if (!IsBetterMigrationCandidate(
                        score,
                        candidate,
                        found,
                        bestScore,
                        bestTarget))
                {
                    continue;
                }

                found = true;
                bestScore = score;
                bestTarget = candidate;
            }

            if (!found)
            {
                return false;
            }

            target = bestTarget;
            reason = "오염된 수로를 따라 이동";
            return true;
        }
    }

    private sealed class TemperatureBandMigrationPreferenceStrategy :
        IWildlifeMigrationPreferenceStrategy
    {
        public WildlifeMigrationCapability Capability =>
            WildlifeMigrationCapability.TemperatureBand;

        public bool TryChoose(
            WildlifeEcosystemRuntime runtime,
            IWildlifeAnimalPort actor,
            IWildlifeGridPort grid,
            IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
            WildlifeMigrationProfile profile,
            out Vector2Int target,
            out string reason)
        {
            target = actor.GridPosition;
            reason = "감지 범위 안에 적정 온도 지점이 없음";
            bool found = false;
            float bestScore = float.NegativeInfinity;
            Vector2Int bestTarget = target;
            int radius = profile.DetectionRadiusCells;
            for (int distance = 0; distance <= radius; distance++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    int dyMagnitude = distance - Mathf.Abs(dx);
                    Evaluate(actor.GridPosition + new Vector2Int(dx, dyMagnitude));
                    if (dyMagnitude != 0)
                    {
                        Evaluate(actor.GridPosition + new Vector2Int(dx, -dyMagnitude));
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            target = bestTarget;
            reason = "적정 온도 구역으로 이동";
            return true;

            void Evaluate(Vector2Int candidate)
            {
                if (!CanUsePreferenceTarget(grid, actor, candidate)
                    || !IsWithinTerritory(actor, candidate)
                    || !runtime.world.TryGetTemperatureC(
                        candidate,
                        out float temperatureC)
                    || temperatureC < profile.MinimumTemperatureC
                    || temperatureC > profile.MaximumTemperatureC)
                {
                    return;
                }

                float score = profile.SignalScore
                    - Manhattan(actor.GridPosition, candidate)
                    * MigrationSignalDistancePenalty;
                if (!IsBetterMigrationCandidate(
                        score,
                        candidate,
                        found,
                        bestScore,
                        bestTarget))
                {
                    return;
                }

                found = true;
                bestScore = score;
                bestTarget = candidate;
            }
        }
    }

    private sealed class CarcassMigrationPreferenceStrategy :
        IWildlifeMigrationPreferenceStrategy
    {
        public WildlifeMigrationCapability Capability =>
            WildlifeMigrationCapability.Carcass;

        public bool TryChoose(
            WildlifeEcosystemRuntime runtime,
            IWildlifeAnimalPort actor,
            IWildlifeGridPort grid,
            IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
            WildlifeMigrationProfile profile,
            out Vector2Int target,
            out string reason)
        {
            target = actor.GridPosition;
            reason = "감지 범위 안에 이용 가능한 사체가 없음";
            bool found = false;
            float bestScore = float.NegativeInfinity;
            Vector2Int bestTarget = target;
            foreach (WildlifeCarcassStackSnapshot stack in
                     itemStacks ?? Array.Empty<WildlifeCarcassStackSnapshot>())
            {
                if (stack.Quantity < profile.MinimumCarcassQuantity
                    || stack.Forbidden
                    || !WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                        stack.ItemId,
                        out _)
                    || !IsWithinDetection(
                        actor.GridPosition,
                        stack.Position,
                        profile.DetectionRadiusCells)
                    || !CanUsePreferenceTarget(grid, actor, stack.Position)
                    || !IsWithinTerritory(actor, stack.Position))
                {
                    continue;
                }

                float score = profile.SignalScore
                    - Manhattan(actor.GridPosition, stack.Position)
                    * MigrationSignalDistancePenalty;
                if (!IsBetterMigrationCandidate(
                        score,
                        stack.Position,
                        found,
                        bestScore,
                        bestTarget))
                {
                    continue;
                }

                found = true;
                bestScore = score;
                bestTarget = stack.Position;
            }

            if (!found)
            {
                return false;
            }

            target = bestTarget;
            reason = "전장의 사체 냄새를 따라 이동";
            return true;
        }
    }
}

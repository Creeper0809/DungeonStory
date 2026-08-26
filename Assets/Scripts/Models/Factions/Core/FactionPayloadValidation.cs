using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Factions
{

public static class FactionPayloadValidation
{
    private const string RoutePrefix = "faction-route:";

    public static IReadOnlyList<string> Validate(
        DungeonFactionSaveData data,
        IReadOnlyList<FactionDefinitionSnapshot> definitions,
        Func<string, bool> itemExists)
    {
        if (itemExists == null)
        {
            throw new ArgumentNullException(nameof(itemExists));
        }
        ValidationErrors report = new();
        if (data == null)
        {
            report.AddError("Faction payload is null.");
            return report.Errors;
        }
        if (definitions == null)
        {
            report.AddError("Faction validation has no authored definition catalog.");
            return report.Errors;
        }
        if (data.version != DungeonFactionSaveData.CurrentVersion)
        {
            report.AddError(
                $"Faction payload version {data.version} is unsupported.");
        }
        if (data.currentDay < 1
            || data.routeSequence < 0
            || data.goodwillOperationSequence < 0)
        {
            report.AddError("Faction payload has an invalid day or route sequence.");
        }

        ValidateFactions(data, definitions, report);
        ValidateRoutes(data, definitions, itemExists, report);
        return report.Errors;
    }

    public static int RouteSequenceOf(string routeId)
    {
        return TryParseRouteId(routeId, out int sequence)
            ? sequence
            : int.MaxValue;
    }

    public static IReadOnlyList<string> CanonicalizeReinforcementActorIdsForRestore(
        FactionRouteState route)
    {
        // Early V18 wrote the stable route suffix directly. Resolve only that exact
        // grammar into the detached candidate; current captures stay character-scoped.
        if (route == null
            || !TryParseRouteId(route.routeId, out int sequence)
            || route.reinforcementActorIds == null)
        {
            throw new InvalidOperationException(
                "Faction reinforcement restore requires a valid route and actor list.");
        }

        List<string> canonical = new(route.reinforcementActorIds.Count);
        for (int index = 0; index < route.reinforcementActorIds.Count; index++)
        {
            if (!TryResolveReinforcementActorId(
                    sequence,
                    index + 1,
                    route.reinforcementActorIds[index],
                    out string actorId))
            {
                throw new InvalidOperationException(
                    $"Faction route '{route.routeId}' contains invalid reinforcement actor ID '{route.reinforcementActorIds[index]}'.");
            }
            canonical.Add(actorId);
        }
        return canonical;
    }

    private static void ValidateFactions(
        DungeonFactionSaveData data,
        IReadOnlyList<FactionDefinitionSnapshot> definitions,
        ValidationErrors report)
    {
        if (data.factions == null)
        {
            report.AddError("Faction payload has no faction list.");
            return;
        }
        if (data.factions.Count != definitions.Count)
        {
            report.AddError(
                "Faction payload does not contain every authored faction exactly once.");
        }

        int count = Math.Min(data.factions.Count, definitions.Count);
        for (int index = 0; index < count; index++)
        {
            DungeonFactionState faction = data.factions[index];
            string expectedId = definitions[index]?.StableId ?? string.Empty;
            string factionId = faction?.factionId ?? string.Empty;
            if (faction == null
                || expectedId.Length == 0
                || !string.Equals(factionId, expectedId, StringComparison.Ordinal)
                || !IsCanonical(factionId))
            {
                report.AddError(
                    $"Faction payload entry {index} does not match the authored canonical faction order.");
                continue;
            }

            if (faction.trust < -100
                || faction.trust > 100
                || faction.betrayalScars < 0
                || faction.negotiationBlockedUntilDay < 0
                || faction.lastBetrayalLootValue < 0
                || faction.restitutionRequiredValue < 0
                || faction.unpaidContractCount < 0
                || faction.reinforcementDeaths < 0
                || faction.equipmentLosses < 0)
            {
                report.AddError(
                    $"Faction '{factionId}' has an invalid trust or nonnegative counter.");
            }

            ValidateRestitutionTransfer(faction, report);
            ValidateGoodwillTransfer(
                faction,
                data.goodwillOperationSequence,
                report);
        }
    }

    private static void ValidateGoodwillTransfer(
        DungeonFactionState faction,
        int globalSequence,
        ValidationErrors report)
    {
        string operationId = faction.goodwillTransferOperationId
            ?? string.Empty;
        string commitId = faction.goodwillTransferCommitId ?? string.Empty;
        IReadOnlyList<string> sourceStackIds =
            faction.goodwillTransferSourceStackIds;
        if (operationId.Length == 0)
        {
            if (commitId.Length > 0
                || sourceStackIds == null
                || sourceStackIds.Count != 0
                || faction.goodwillTransferSequence != 0
                || faction.goodwillTransferQuantity != 0
                || faction.goodwillTransferMassGrams != 0L
                || faction.goodwillTransferredPhysicalValue != 0
                || faction.goodwillCampaignRapportTarget != 0
                || faction.goodwillTransferCompleted)
            {
                report.AddError(
                    $"Faction '{faction.factionId}' has partial goodwill transfer provenance.");
            }
            return;
        }

        string expectedOperation =
            $"faction-goodwill:{faction.factionId}:"
            + $"{faction.goodwillTransferSequence:D8}";
        bool sourcesCanonical = sourceStackIds != null
            && sourceStackIds.Count > 0
            && sourceStackIds.All(IsCanonical)
            && sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == sourceStackIds.Count
            && sourceStackIds.SequenceEqual(
                sourceStackIds.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);
        string expectedCommit =
            $"physical-batch-disposition:1:{operationId}:"
            + $"{faction.goodwillTransferQuantity}:"
            + faction.goodwillTransferMassGrams;
        if (faction.goodwillTransferSequence <= 0
            || faction.goodwillTransferSequence > globalSequence
            || !string.Equals(
                operationId,
                expectedOperation,
                StringComparison.Ordinal)
            || !IsCanonical(commitId)
            || !string.Equals(
                commitId,
                expectedCommit,
                StringComparison.Ordinal)
            || !sourcesCanonical
            || faction.goodwillTransferQuantity <= 0
            || faction.goodwillTransferMassGrams <= 0L
            || faction.goodwillTransferredPhysicalValue < 50
            || faction.goodwillCampaignRapportTarget is < -100 or > 100
            || faction.goodwillTransferCompleted && !faction.discovered)
        {
            report.AddError(
                $"Faction '{faction.factionId}' has invalid goodwill transfer provenance.");
        }
    }

    private static void ValidateRestitutionTransfer(
        DungeonFactionState faction,
        ValidationErrors report)
    {
        string operationId = faction.restitutionTransferOperationId
            ?? string.Empty;
        string commitId = faction.restitutionTransferCommitId ?? string.Empty;
        IReadOnlyList<string> sourceStackIds =
            faction.restitutionTransferSourceStackIds;
        bool hasOperation = operationId.Length > 0;
        if (!hasOperation)
        {
            if (commitId.Length > 0
                || sourceStackIds == null
                || sourceStackIds.Count != 0
                || faction.restitutionTransferQuantity != 0
                || faction.restitutionTransferMassGrams != 0L
                || faction.restitutionTransferredPhysicalValue != 0
                || faction.restitutionCampaignGrievanceTarget != 0
                || faction.restitutionTransferCompleted)
            {
                report.AddError(
                    $"Faction '{faction.factionId}' has partial restitution transfer provenance.");
            }
            return;
        }

        string expectedOperation =
            $"faction-restitution:{faction.factionId}:scar:{faction.betrayalScars:D8}";
        bool sourcesCanonical = sourceStackIds != null
            && sourceStackIds.Count > 0
            && sourceStackIds.All(IsCanonical)
            && sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == sourceStackIds.Count
            && sourceStackIds.SequenceEqual(
                sourceStackIds.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);
        string expectedCommit =
            $"physical-batch-disposition:1:{operationId}:"
            + $"{faction.restitutionTransferQuantity}:"
            + faction.restitutionTransferMassGrams;
        if (faction.betrayalScars <= 0
            || !string.Equals(operationId, expectedOperation,
                StringComparison.Ordinal)
            || !IsCanonical(commitId)
            || !string.Equals(commitId, expectedCommit,
                StringComparison.Ordinal)
            || !sourcesCanonical
            || faction.restitutionTransferQuantity <= 0
            || faction.restitutionTransferMassGrams <= 0L
            || faction.restitutionTransferredPhysicalValue <= 0
            || faction.restitutionCampaignGrievanceTarget is < 0 or > 100
            || faction.restitutionTransferCompleted && !faction.restitutionPaid)
        {
            report.AddError(
                $"Faction '{faction.factionId}' has invalid restitution transfer provenance.");
        }
    }

    private static void ValidateRoutes(
        DungeonFactionSaveData data,
        IReadOnlyList<FactionDefinitionSnapshot> definitions,
        Func<string, bool> itemExists,
        ValidationErrors report)
    {
        if (data.routes == null)
        {
            report.AddError("Faction payload has no route list.");
            return;
        }

        HashSet<string> factionIds = new(StringComparer.Ordinal);
        foreach (FactionDefinitionSnapshot definition in definitions)
        {
            if (definition != null && definition.StableId.Length > 0)
            {
                factionIds.Add(definition.StableId);
            }
        }

        int previousSequence = 0;
        for (int index = 0; index < data.routes.Count; index++)
        {
            FactionRouteState route = data.routes[index];
            string routeId = route?.routeId ?? string.Empty;
            if (route == null
                || !TryParseRouteId(routeId, out int sequence)
                || sequence <= previousSequence
                || sequence > data.routeSequence)
            {
                report.AddError(
                    "Faction payload contains a null, duplicate, unordered, or invalid route ID.");
                continue;
            }
            previousSequence = sequence;
            ValidateRoute(
                route,
                sequence,
                data.currentDay,
                factionIds,
                itemExists,
                report);
        }

        if (data.routes.Count != data.routeSequence
            || (data.routes.Count > 0 && previousSequence != data.routeSequence))
        {
            report.AddError(
                "Faction route sequence does not exactly match the persisted route set.");
        }
    }

    private static void ValidateRoute(
        FactionRouteState route,
        int sequence,
        int currentDay,
        HashSet<string> factionIds,
        Func<string, bool> itemExists,
        ValidationErrors report)
    {
        if (!IsCanonical(route.factionId)
            || !factionIds.Contains(route.factionId))
        {
            report.AddError(
                $"Faction route '{route.routeId}' references an unknown faction.");
        }
        if (!Enum.IsDefined(typeof(FactionRouteKind), route.kind)
            || !Enum.IsDefined(typeof(FactionRouteStatus), route.status))
        {
            report.AddError(
                $"Faction route '{route.routeId}' has an invalid enum value.");
        }
        if (route.path == null || route.path.Count == 0)
        {
            report.AddError(
                $"Faction route '{route.routeId}' has no path.");
        }
        else
        {
            for (int index = 0; index < route.path.Count; index++)
            {
                if (route.path[index] == null)
                {
                    report.AddError(
                        $"Faction route '{route.routeId}' has a null path coordinate.");
                }
            }
            if (route.pathIndex < 0 || route.pathIndex >= route.path.Count)
            {
                report.AddError(
                    $"Faction route '{route.routeId}' has an invalid path index.");
            }
        }
        if (!IsFinite(route.segmentProgress)
            || route.segmentProgress < 0f
            || route.segmentProgress >= 1f
            || !IsFinite(route.delaySeconds)
            || route.delaySeconds < 0f
            || route.strength < 0
            || route.strength > 100)
        {
            report.AddError(
                $"Faction route '{route.routeId}' has invalid travel state.");
        }
        if (route.createdDay < 1
            || route.createdDay > currentDay
            || route.estimatedArrivalDay < route.createdDay)
        {
            report.AddError(
                $"Faction route '{route.routeId}' has invalid day bounds.");
        }

        ValidateReinforcementActors(route, sequence, report);
        ValidateCargo(route, itemExists, report);
    }

    private static void ValidateReinforcementActors(
        FactionRouteState route,
        int sequence,
        ValidationErrors report)
    {
        if (route.reinforcementActorIds == null)
        {
            report.AddError(
                $"Faction route '{route.routeId}' has no reinforcement-actor list.");
            return;
        }
        if (route.actorsSpawned != (route.reinforcementActorIds.Count > 0))
        {
            report.AddError(
                $"Faction route '{route.routeId}' has inconsistent actor-spawn state.");
        }
        for (int index = 0; index < route.reinforcementActorIds.Count; index++)
        {
            if (!TryResolveReinforcementActorId(
                    sequence,
                    index + 1,
                    route.reinforcementActorIds[index],
                    out _))
            {
                report.AddError(
                    $"Faction route '{route.routeId}' has a non-canonical reinforcement actor ID.");
            }
        }
    }

    private static bool TryResolveReinforcementActorId(
        int routeSequence,
        int actorSequence,
        string value,
        out string canonical)
    {
        string stableSuffix =
            $"{RoutePrefix}{routeSequence}:ally:{actorSequence}";
        canonical = CharacterId.FromStableSuffix(stableSuffix).Value;
        return string.Equals(value, canonical, StringComparison.Ordinal)
            || string.Equals(value, stableSuffix, StringComparison.Ordinal);
    }

    private static void ValidateCargo(
        FactionRouteState route,
        Func<string, bool> itemExists,
        ValidationErrors report)
    {
        if (route.cargo == null)
        {
            report.AddError($"Faction route '{route.routeId}' has no cargo list.");
            return;
        }
        foreach (FactionCargoLine line in route.cargo)
        {
            string itemId = line?.itemId ?? string.Empty;
            if (line == null
                || line.amount <= 0
                || !IsCanonical(itemId)
                || !itemExists(itemId))
            {
                report.AddError(
                    $"Faction route '{route.routeId}' has null, nonpositive, or unknown cargo.");
            }
        }
    }

    private static bool TryParseRouteId(string routeId, out int sequence)
    {
        sequence = 0;
        return IsCanonical(routeId)
            && routeId.StartsWith(RoutePrefix, StringComparison.Ordinal)
            && int.TryParse(routeId.Substring(RoutePrefix.Length), out sequence)
            && sequence > 0
            && string.Equals(
                routeId,
                RoutePrefix + sequence,
                StringComparison.Ordinal);
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private sealed class ValidationErrors
    {
        private readonly List<string> errors = new();

        public IReadOnlyList<string> Errors => errors;

        public void AddError(string error) => errors.Add(error);
    }
}
}

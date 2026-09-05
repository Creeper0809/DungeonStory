using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
            || data.routeSettlementOperationSequence < 0
            || data.goodwillOperationSequence < 0
            || data.allianceBenefitBalanceMilliEwu < 0
            || data.allianceBenefitRefillRemainder < 0
            || data.allianceBenefitLastRefillDay < 1
            || data.allianceBenefitLastRefillDay > data.currentDay
            || !IsSha256(data.allianceBenefitAuthorityDigest))
        {
            report.AddError(
                "Faction payload has an invalid sequence or alliance-benefit budget state.");
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
        HashSet<int> settlementSequences = new();
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
                data.routeSettlementOperationSequence,
                data.allianceBenefitAuthorityDigest,
                settlementSequences,
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
        int globalSettlementSequence,
        string globalAllianceBenefitAuthorityDigest,
        HashSet<int> settlementSequences,
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
        ValidateCargoDelivery(route, report);
        ValidateSettlement(
            route,
            globalSettlementSequence,
            globalAllianceBenefitAuthorityDigest,
            settlementSequences,
            report);
    }

    private static void ValidateSettlement(
        FactionRouteState route,
        int globalSequence,
        string globalAllianceBenefitAuthorityDigest,
        HashSet<int> seenSequences,
        ValidationErrors report)
    {
        FactionRouteSettlementReceipt receipt = route.settlement;
        if (receipt == null)
        {
            report.AddError(
                $"Faction route '{route.routeId}' has no settlement receipt.");
            return;
        }

        bool economicRoute = route.kind is FactionRouteKind.TradeCaravan
            or FactionRouteKind.SupplyCaravan;
        if (!economicRoute)
        {
            if (receipt.state != FactionRouteSettlementState.NotApplicable
                || HasSettlementPayload(receipt))
            {
                report.AddError(
                    $"Faction route '{route.routeId}' has settlement data for a non-economic route.");
            }
            return;
        }

        if (receipt.operationSequence <= 0
            || receipt.operationSequence > globalSequence
            || !seenSequences.Add(receipt.operationSequence)
            || !IsCanonical(receipt.capabilityId)
            || receipt.capabilityVersion <= 0
            || receipt.cargoAuthoredGold <= 0
            || !IsSha256(receipt.sourceDigest)
            || !IsSha256(receipt.quoteDigest)
            || !TryCalculateSettlementDigests(
                route.factionId,
                route.kind,
                receipt.capabilityId,
                receipt.capabilityVersion,
                receipt.quoteLines,
                receipt.paymentGold,
                out int frozenCargoGold,
                out string frozenSourceDigest,
                out string frozenQuoteDigest)
            || frozenCargoGold != receipt.cargoAuthoredGold
            || !string.Equals(
                frozenSourceDigest,
                receipt.sourceDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                frozenQuoteDigest,
                receipt.quoteDigest,
                StringComparison.Ordinal)
            || !QuoteLinesMatchCargo(receipt.quoteLines, route.cargo))
        {
            report.AddError(
                $"Faction route '{route.routeId}' has invalid quote settlement provenance.");
            return;
        }

        if (route.kind == FactionRouteKind.TradeCaravan)
        {
            string expectedSource =
                $"faction-route-settlement:{receipt.operationSequence:D8}";
            if (receipt.state != FactionRouteSettlementState.Paid
                || receipt.paymentGold <= 0
                || receipt.paymentGold != receipt.cargoAuthoredGold
                || !IsCanonical(receipt.transactionId)
                || !string.Equals(
                    receipt.transactionSourceId,
                    expectedSource,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.transactionTargetId,
                    route.factionId,
                    StringComparison.Ordinal)
                || receipt.balanceBefore < receipt.paymentGold
                || receipt.balanceAfter
                    != receipt.balanceBefore - receipt.paymentGold
                || !string.IsNullOrEmpty(
                    receipt.allianceBenefitAuthorityDigest)
                || !string.IsNullOrEmpty(
                    receipt.allianceBenefitReservationId)
                || receipt.allianceBenefitDebitMilliEwu != 0
                || receipt.allianceBenefitBalanceBeforeMilliEwu != 0
                || receipt.allianceBenefitBalanceAfterMilliEwu != 0)
            {
                report.AddError(
                    $"Faction route '{route.routeId}' has an invalid paid settlement receipt.");
            }
            return;
        }

        string expectedReservation =
            $"faction-alliance-benefit:{receipt.operationSequence:D8}";
        if (receipt.state
                != FactionRouteSettlementState.AllianceBenefitDebited
            || receipt.paymentGold != 0
            || !string.IsNullOrEmpty(receipt.transactionId)
            || !string.IsNullOrEmpty(receipt.transactionSourceId)
            || !string.IsNullOrEmpty(receipt.transactionTargetId)
            || receipt.balanceBefore != 0
            || receipt.balanceAfter != 0
            || !IsSha256(receipt.allianceBenefitAuthorityDigest)
            || !string.Equals(
                receipt.allianceBenefitAuthorityDigest,
                globalAllianceBenefitAuthorityDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.allianceBenefitReservationId,
                expectedReservation,
                StringComparison.Ordinal)
            || receipt.allianceBenefitDebitMilliEwu <= 0
            || receipt.allianceBenefitBalanceBeforeMilliEwu
                < receipt.allianceBenefitDebitMilliEwu
            || receipt.allianceBenefitBalanceAfterMilliEwu
                != receipt.allianceBenefitBalanceBeforeMilliEwu
                    - receipt.allianceBenefitDebitMilliEwu)
        {
            report.AddError(
                $"Faction route '{route.routeId}' has an invalid alliance-benefit settlement receipt.");
        }
    }

    private static bool HasSettlementPayload(
        FactionRouteSettlementReceipt receipt) =>
        receipt.operationSequence != 0
        || receipt.capabilityVersion != 0
        || receipt.cargoAuthoredGold != 0
        || receipt.paymentGold != 0
        || (receipt.quoteLines?.Count ?? 0) != 0
        || receipt.balanceBefore != 0
        || receipt.balanceAfter != 0
        || receipt.allianceBenefitDebitMilliEwu != 0
        || receipt.allianceBenefitBalanceBeforeMilliEwu != 0
        || receipt.allianceBenefitBalanceAfterMilliEwu != 0
        || !string.IsNullOrEmpty(receipt.capabilityId)
        || !string.IsNullOrEmpty(receipt.sourceDigest)
        || !string.IsNullOrEmpty(receipt.quoteDigest)
        || !string.IsNullOrEmpty(receipt.transactionId)
        || !string.IsNullOrEmpty(receipt.transactionSourceId)
        || !string.IsNullOrEmpty(receipt.transactionTargetId)
        || !string.IsNullOrEmpty(receipt.allianceBenefitAuthorityDigest)
        || !string.IsNullOrEmpty(receipt.allianceBenefitReservationId);

    private static bool IsSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    public static bool TryCalculateSettlementDigests(
        string factionId,
        FactionRouteKind routeKind,
        string capabilityId,
        int capabilityVersion,
        IReadOnlyList<FactionRouteQuoteLineReceipt> quoteLines,
        int paymentGold,
        out int cargoAuthoredGold,
        out string sourceDigest,
        out string quoteDigest)
    {
        cargoAuthoredGold = 0;
        sourceDigest = string.Empty;
        quoteDigest = string.Empty;
        if (!IsCanonical(factionId)
            || !IsCanonical(capabilityId)
            || capabilityVersion <= 0
            || quoteLines == null
            || quoteLines.Count == 0
            || paymentGold < 0)
            return false;

        string previousId = string.Empty;
        StringBuilder source = new();
        try
        {
            for (int index = 0; index < quoteLines.Count; index++)
            {
                FactionRouteQuoteLineReceipt line = quoteLines[index];
                if (line == null
                    || !IsCanonical(line.itemId)
                    || line.amount <= 0
                    || line.unitPriceGold <= 0
                    || (index > 0 && string.Compare(
                        previousId,
                        line.itemId,
                        StringComparison.Ordinal) >= 0))
                    return false;
                if (index > 0)
                    source.Append('\n');
                source.Append(line.itemId)
                    .Append('|')
                    .Append(line.amount.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(line.unitPriceGold.ToString(
                        CultureInfo.InvariantCulture));
                cargoAuthoredGold = checked(cargoAuthoredGold
                    + checked(line.amount * line.unitPriceGold));
                previousId = line.itemId;
            }
        }
        catch (OverflowException)
        {
            cargoAuthoredGold = 0;
            return false;
        }

        sourceDigest = Sha256(source.ToString());
        string quoteCanonical = string.Join("|", new[]
        {
            capabilityId,
            capabilityVersion.ToString(CultureInfo.InvariantCulture),
            factionId,
            ((int)routeKind).ToString(CultureInfo.InvariantCulture),
            cargoAuthoredGold.ToString(CultureInfo.InvariantCulture),
            paymentGold.ToString(CultureInfo.InvariantCulture),
            sourceDigest
        });
        quoteDigest = Sha256(quoteCanonical);
        return true;
    }

    private static bool QuoteLinesMatchCargo(
        IReadOnlyList<FactionRouteQuoteLineReceipt> quoteLines,
        IReadOnlyList<FactionCargoLine> cargo)
    {
        if (quoteLines == null || cargo == null || quoteLines.Count != cargo.Count)
            return false;
        FactionCargoLine[] orderedCargo = cargo
            .Where(value => value != null)
            .OrderBy(value => value.itemId, StringComparer.Ordinal)
            .ToArray();
        if (orderedCargo.Length != cargo.Count)
            return false;
        for (int index = 0; index < quoteLines.Count; index++)
        {
            if (!string.Equals(
                    quoteLines[index].itemId,
                    orderedCargo[index].itemId,
                    StringComparison.Ordinal)
                || quoteLines[index].amount != orderedCargo[index].amount)
                return false;
        }
        return true;
    }

    private static string Sha256(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(value ?? string.Empty));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte part in digest)
            result.Append(part.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
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

    private static void ValidateCargoDelivery(
        FactionRouteState route,
        ValidationErrors report)
    {
        FactionRouteCargoDeliveryReceipt receipt = route.cargoDelivery;
        if (receipt == null)
        {
            report.AddError(
                $"Faction route '{route.routeId}' has no cargo-delivery receipt.");
            return;
        }
        bool hasCargo = (route.cargo?.Count ?? 0) > 0;
        bool hasPayload = !string.IsNullOrEmpty(receipt.batchCommitId)
            || !string.IsNullOrEmpty(receipt.destinationId)
            || !string.IsNullOrEmpty(receipt.outcomeFingerprint)
            || receipt.totalMassGrams != 0
            || (receipt.stacks?.Count ?? 0) != 0;
        if (!hasCargo)
        {
            if (receipt.state != FactionRouteCargoDeliveryState.NotApplicable
                || hasPayload)
            {
                report.AddError(
                    $"Faction route '{route.routeId}' has delivery state without cargo.");
            }
            return;
        }
        if (receipt.state == FactionRouteCargoDeliveryState.Publishing)
        {
            report.AddError(
                $"Faction route '{route.routeId}' captured a transient cargo publication.");
            return;
        }
        if (receipt.state == FactionRouteCargoDeliveryState.Ready)
        {
            if (hasPayload)
            {
                report.AddError(
                    $"Faction route '{route.routeId}' has terminal cargo data while ready.");
            }
            return;
        }
        if (receipt.state != FactionRouteCargoDeliveryState.Delivered
            || route.status != FactionRouteStatus.Arrived
            || receipt.stacks == null
            || receipt.stacks.Count != route.cargo.Count
            || receipt.totalMassGrams <= 0
            || !string.Equals(
                receipt.batchCommitId,
                "physical-source-batch:faction.route-cargo:" + route.routeId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.destinationId,
                "physical-source-buffer:faction.route-cargo:" + route.routeId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.outcomeFingerprint,
                CalculateCargoOutcomeFingerprint(route),
                StringComparison.Ordinal))
        {
            report.AddError(
                $"Faction route '{route.routeId}' has invalid terminal cargo provenance.");
            return;
        }

        long totalMass = 0L;
        HashSet<string> stackIds = new(StringComparer.Ordinal);
        for (int index = 0; index < route.cargo.Count; index++)
        {
            FactionCargoLine line = route.cargo[index];
            ProductionDomainPublishedStackSaveData stack =
                receipt.stacks[index];
            string expectedLine =
                $"cargo:{index:D4}:{line?.itemId ?? string.Empty}";
            if (stack == null
                || !string.Equals(
                    stack.outputLineId,
                    expectedLine,
                    StringComparison.Ordinal)
                || !string.Equals(
                    stack.itemId,
                    line?.itemId,
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(stack.itemInstanceId)
                || stack.quantity != ResolveCargoDeliveryQuantity(
                    line?.amount ?? 0,
                    route.strength)
                || stack.massGrams <= 0
                || !IsCanonical(stack.stackId)
                || !stackIds.Add(stack.stackId))
            {
                report.AddError(
                    $"Faction route '{route.routeId}' has a mismatched cargo stack receipt.");
                return;
            }
            try
            {
                totalMass = checked(totalMass + stack.massGrams);
            }
            catch (OverflowException)
            {
                report.AddError(
                    $"Faction route '{route.routeId}' cargo mass overflowed.");
                return;
            }
        }
        if (totalMass != receipt.totalMassGrams)
        {
            report.AddError(
                $"Faction route '{route.routeId}' cargo mass receipt does not conserve its stack sum.");
        }
    }

    private static int ResolveCargoDeliveryQuantity(int amount, int strength)
    {
        if (amount <= 0 || strength < 0)
        {
            return 0;
        }
        long scaled = checked((long)amount * strength);
        long whole = scaled / 100L;
        long remainder = scaled % 100L;
        if (remainder > 50L
            || (remainder == 50L && (whole & 1L) != 0L))
        {
            whole = checked(whole + 1L);
        }
        return Math.Max(1, checked((int)whole));
    }

    private static string CalculateCargoOutcomeFingerprint(
        FactionRouteState route)
    {
        StringBuilder canonical = new();
        for (int index = 0; index < route.cargo.Count; index++)
        {
            FactionCargoLine line = route.cargo[index];
            AppendLengthToken(
                canonical,
                $"cargo:{index:D4}:{line.itemId}");
            AppendLengthToken(canonical, line.itemId);
            AppendLengthToken(
                canonical,
                ResolveCargoDeliveryQuantity(line.amount, route.strength)
                    .ToString(CultureInfo.InvariantCulture));
            AppendLengthToken(canonical, string.Empty);
            AppendLengthToken(canonical, string.Empty);
            AppendLengthToken(canonical, string.Empty);
        }
        return Sha256(canonical.ToString());
    }

    private static void AppendLengthToken(
        StringBuilder builder,
        string value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length)
            .Append(':')
            .Append(safe)
            .Append('|');
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

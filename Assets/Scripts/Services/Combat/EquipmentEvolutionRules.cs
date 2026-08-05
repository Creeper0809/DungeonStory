using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class EquipmentEvolutionRules
{
    internal static Dictionary<string, int> BuildRequirements(
        EvolutionReforgeOrder order)
    {
        Dictionary<string, int> result =
            new Dictionary<string, int>(StringComparer.Ordinal);
        AddRequirement(
            result,
            order.primaryMaterialItemId,
            order.primaryMaterialAmount);
        AddRequirement(result, order.catalystItemId, 1);
        AddRequirement(result, order.bindingItemId, order.bindingAmount);
        AddRequirement(
            result,
            order.stabilizerItemId,
            order.stabilizerAmount);
        return result;
    }

    internal static void AddRequirement(
        IDictionary<string, int> destination,
        string itemId,
        int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return;
        }

        string normalized = itemId.Trim();
        destination.TryGetValue(normalized, out int current);
        destination[normalized] = current + amount;
    }

    internal static EquipmentEvolutionDirection InferDirection(
        CompactedHistorySegment segment)
    {
        string source = string.Join(
            "|",
            segment.metrics
                .OrderByDescending(metric => Mathf.Abs(metric.value))
                .Select(metric => metric.metricId)
                .Concat(segment.sourceTags))
            .ToLowerInvariant();
        if (ContainsAny(source, "boss", "kill", "execution"))
        {
            return EquipmentEvolutionDirection.Execution;
        }
        if (ContainsAny(source, "absorb", "block", "armor", "shield"))
        {
            return EquipmentEvolutionDirection.Protection;
        }
        if (ContainsAny(source, "downed", "survive", "recovery"))
        {
            return EquipmentEvolutionDirection.Survival;
        }
        if (ContainsAny(source, "intercept", "guard", "defense"))
        {
            return EquipmentEvolutionDirection.Interception;
        }
        if (ContainsAny(source, "long", "medium", "ranged", "shoot"))
        {
            return EquipmentEvolutionDirection.Ranged;
        }
        if (ContainsAny(source, "accuracy", "hit"))
        {
            return EquipmentEvolutionDirection.Accuracy;
        }
        if (ContainsAny(source, "melee", "contact", "near"))
        {
            return EquipmentEvolutionDirection.Melee;
        }
        return EquipmentEvolutionDirection.Balanced;
    }

    internal static EquipmentEvolutionDirection InferDirectionFromOpenLedger(
        UsageLedger ledger)
    {
        CompactedHistorySegment synthetic = new CompactedHistorySegment
        {
            metrics = (ledger?.currentGenerationEvents
                    ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null)
                .GroupBy(entry => entry.eventId, StringComparer.Ordinal)
                .Select(group => new UsageLedgerMetric
                {
                    metricId = group.Key,
                    value = group.Sum(entry => entry.amount)
                })
                .ToList(),
            sourceTags = (ledger?.currentGenerationEvents
                    ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null)
                .SelectMany(entry => entry.sourceTags)
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
        return InferDirection(synthetic);
    }

    internal static string ResolveModuleId(
        EquipmentEvolutionDirection direction,
        string catalystFamily)
    {
        return direction switch
        {
            EquipmentEvolutionDirection.Melee
                => "equipment:force",
            EquipmentEvolutionDirection.Execution => "equipment:execution",
            EquipmentEvolutionDirection.Ranged => "equipment:cadence",
            EquipmentEvolutionDirection.Accuracy => "equipment:precision",
            EquipmentEvolutionDirection.Interception => "equipment:control",
            EquipmentEvolutionDirection.Protection
                or EquipmentEvolutionDirection.Survival => "equipment:durability",
            _ => catalystFamily?.IndexOf(
                    "defense",
                    StringComparison.OrdinalIgnoreCase) >= 0
                ? "equipment:durability"
                : catalystFamily?.IndexOf(
                    "survival",
                    StringComparison.OrdinalIgnoreCase) >= 0
                    ? "equipment:durability"
                    : "equipment:force"
        };
    }

    public static float GetCatalystFamilyPotencyScale(string catalystFamily)
    {
        string normalized =
            catalystFamily?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("arcane"))
        {
            return 1.1f;
        }

        if (normalized.Contains("offense"))
        {
            return 1.07f;
        }

        if (normalized.Contains("defense"))
        {
            return 1.04f;
        }

        if (normalized.Contains("authority"))
        {
            return 1.02f;
        }

        if (normalized.Contains("survival"))
        {
            return 0.98f;
        }

        return 1f;
    }

    internal static void AddAttunement(
        EquipmentEvolutionState state,
        string equipmentInstanceId,
        string ownerPersistentId,
        int points)
    {
        AttunementRecord record = state.attunements.FirstOrDefault(entry =>
            entry != null
            && string.Equals(
                entry.ownerPersistentId,
                ownerPersistentId,
                StringComparison.Ordinal));
        if (record == null)
        {
            record = new AttunementRecord
            {
                ownerPersistentId = ownerPersistentId,
                startedGeneration = state.generation
            };
            state.attunements.Add(record);
        }

        int previousTier = record.attainedTier;
        record.affinityScore = Mathf.Max(0, record.affinityScore + points);
        int nextTier = record.affinityScore >= 250
            ? 3
            : record.affinityScore >= 100
                ? 2
                : record.affinityScore >= 30
                    ? 1
                    : 0;
        for (int tier = previousTier + 1; tier <= nextTier; tier++)
        {
            CreateAttunementHistoryNode(
                state,
                record,
                equipmentInstanceId,
                tier);
        }

        record.attainedTier = nextTier;
    }

    internal static void CreateAttunementHistoryNode(
        EquipmentEvolutionState state,
        AttunementRecord record,
        string equipmentInstanceId,
        int tier)
    {
        string historyHash = StableEvolutionHash.Compute(string.Join(
            "|",
            equipmentInstanceId,
            record.ownerPersistentId,
            tier.ToString(),
            state.usageLedger != null
                ? string.Join(
                    ",",
                    state.usageLedger.currentGenerationEvents
                        .Where(entry => entry != null)
                        .OrderBy(entry => entry.sequence)
                        .Select(entry =>
                            $"{entry.evidenceId}:{entry.eventId}:{entry.amount:R}"))
                : string.Empty));
        string parentNodeId = record.historyNodeIds.LastOrDefault()
            ?? string.Empty;
        EquipmentEvolutionDirection direction =
            InferDirectionFromOpenLedger(state.usageLedger);
        string effectId = ResolveModuleId(direction, string.Empty);
        string historyNodeHash = StableEvolutionHash.Compute(
            equipmentInstanceId
            + "|"
            + record.ownerPersistentId
            + "|"
            + tier
            + "|"
            + historyHash);
        string nodeId = $"equipment-history:{historyNodeHash}";
        EvolutionNode node = new EvolutionNode
        {
            nodeId = nodeId,
            parentNodeId = parentNodeId,
            effectId = effectId,
            burdenEffectId = string.Empty,
            generation = state.generation,
            active = true,
            historical = true,
            playerVisible = false,
            displayName = string.Empty,
            description = string.Empty,
            potencyMultiplier = tier switch
            {
                1 => 0.35f,
                2 => 0.55f,
                _ => 0.8f
            },
            activationRule = new EvolutionModuleActivationRule()
        };
        EvolutionNarrativeRequestSnapshot request =
            EvolutionNarrativeRequestFactory.Create(
                EvolutionNarrativeTargetKind.Equipment,
                equipmentInstanceId,
                node,
                historyHash,
                state.usageLedger,
                state.ResonanceBudget);
        node.evidenceIds = new List<string>(request.evidenceIds);
        state.evolutionNodes.Add(node);
        state.narrativeRequests ??=
            new List<EvolutionNarrativeRequestSnapshot>();
        state.narrativeRequests.Add(request);
        record.historyNodeIds.Add(nodeId);
        if (string.IsNullOrWhiteSpace(record.rootNodeId))
        {
            record.rootNodeId = nodeId;
        }

        state.activeHistoricalNodeIds ??= new List<string>();
        if (state.activeHistoricalNodeIds.Count < state.ResonanceBudget)
        {
            state.activeHistoricalNodeIds.Add(nodeId);
        }
    }

    internal static void DisableNodeAndDescendants(
        IReadOnlyList<EvolutionNode> nodes,
        ISet<string> activeIds,
        string rootNodeId)
    {
        Queue<string> queue = new Queue<string>();
        queue.Enqueue(rootNodeId);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            activeIds.Remove(current);
            foreach (EvolutionNode child in nodes.Where(node =>
                         node != null
                         && string.Equals(
                             node.parentNodeId,
                             current,
                             StringComparison.Ordinal)))
            {
                queue.Enqueue(child.nodeId);
            }
        }
    }


    private static bool ContainsAny(string source, params string[] values)
    {
        return values.Any(value =>
            source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}

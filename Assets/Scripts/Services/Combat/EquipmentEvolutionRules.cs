using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EquipmentEvolutionRules
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
        return ScoreDirections(segment)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .FirstOrDefault();
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
                .ToList(),
            historicalEvidence = (ledger?.currentGenerationEvents
                    ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null
                    && entry.historicalEvidenceKind != HistoricalEvidenceKind.None)
                .GroupBy(entry => entry.historicalEvidenceKind)
                .Select(group => new HistoricalEvidenceMetric
                {
                    kind = group.Key,
                    strength = group.Sum(entry => Mathf.Abs(entry.amount)),
                    occurrences = group.Sum(entry => Mathf.Max(1, entry.repeatCount))
                })
                .ToList()
        };
        return InferDirection(synthetic);
    }

    public static List<string> BuildLegalHistoricalEffectCandidates(
        UsageLedger ledger)
    {
        CompactedHistorySegment synthetic = new CompactedHistorySegment
        {
            metrics = (ledger?.currentGenerationEvents ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null)
                .GroupBy(entry => entry.eventId, StringComparer.Ordinal)
                .Select(group => new UsageLedgerMetric
                {
                    metricId = group.Key,
                    value = group.Sum(entry => entry.amount)
                })
                .ToList(),
            sourceTags = (ledger?.currentGenerationEvents ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null)
                .SelectMany(entry => entry.sourceTags)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            historicalEvidence = (ledger?.currentGenerationEvents ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null
                    && entry.historicalEvidenceKind != HistoricalEvidenceKind.None)
                .GroupBy(entry => entry.historicalEvidenceKind)
                .Select(group => new HistoricalEvidenceMetric
                {
                    kind = group.Key,
                    strength = group.Sum(entry => Mathf.Abs(entry.amount)),
                    occurrences = group.Sum(entry => Mathf.Max(1, entry.repeatCount))
                })
                .ToList()
        };
        List<string> candidates = ScoreDirections(synthetic)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => ResolveModuleId(pair.Key, string.Empty))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();
        if (candidates.Count == 0)
        {
            candidates.Add(ResolveModuleId(EquipmentEvolutionDirection.Balanced, string.Empty));
        }
        return candidates;
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
        List<string> legalCandidates = BuildLegalHistoricalEffectCandidates(
            state.usageLedger);
        string effectId = legalCandidates[0];
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
            mechanicallyUnlocked = true,
            narrativeReady = false,
            uiVisible = true,
            playerVisible = true,
            displayName = BuildTemporaryHistoryName(effectId, tier),
            description = "기록 해석 중 · 효과는 이미 적용되고 있습니다.",
            potencyMultiplier = tier switch
            {
                1 => 0.35f,
                2 => 0.55f,
                _ => 0.8f
            },
            activationRule = new EvolutionModuleActivationRule(),
            legalCandidateEffectIds = legalCandidates,
            selectedCandidateIndex = legalCandidates.Count == 1 ? 0 : -1
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
        request.legalCandidateEffectIds = new List<string>(legalCandidates);
        request.selectedCandidateIndex = node.selectedCandidateIndex;
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


    private static Dictionary<EquipmentEvolutionDirection, float> ScoreDirections(
        CompactedHistorySegment segment)
    {
        Dictionary<EquipmentEvolutionDirection, float> scores =
            new Dictionary<EquipmentEvolutionDirection, float>
            {
                [EquipmentEvolutionDirection.Balanced] = 0.01f
            };
        foreach (HistoricalEvidenceMetric evidence in segment?.historicalEvidence
                     ?? new List<HistoricalEvidenceMetric>())
        {
            EquipmentEvolutionDirection direction = evidence.kind switch
            {
                HistoricalEvidenceKind.BossExecution => EquipmentEvolutionDirection.Execution,
                HistoricalEvidenceKind.ProtectedOwner => EquipmentEvolutionDirection.Protection,
                HistoricalEvidenceKind.InterceptedFatalHit => EquipmentEvolutionDirection.Interception,
                HistoricalEvidenceKind.SurvivedNearDeath => EquipmentEvolutionDirection.Survival,
                HistoricalEvidenceKind.RepeatedLongRangeHit => EquipmentEvolutionDirection.Ranged,
                HistoricalEvidenceKind.ArmorBroken => EquipmentEvolutionDirection.Protection,
                HistoricalEvidenceKind.CapturedEnemy => EquipmentEvolutionDirection.Interception,
                _ => EquipmentEvolutionDirection.Balanced
            };
            scores.TryGetValue(direction, out float current);
            scores[direction] = current
                + Mathf.Max(0.01f, evidence.strength)
                + Mathf.Max(0, evidence.occurrences) * 0.25f;
        }

        foreach (UsageLedgerMetric metric in segment?.metrics
                     ?? new List<UsageLedgerMetric>())
        {
            EquipmentEvolutionDirection direction = metric.metricId switch
            {
                "combat:block" => EquipmentEvolutionDirection.Protection,
                "combat:absorb" => EquipmentEvolutionDirection.Protection,
                "combat:hit" when segment.sourceTags.Contains("ranged", StringComparer.Ordinal)
                    => EquipmentEvolutionDirection.Ranged,
                "combat:hit" when segment.sourceTags.Contains("melee", StringComparer.Ordinal)
                    => EquipmentEvolutionDirection.Melee,
                "combat:hit" => EquipmentEvolutionDirection.Accuracy,
                _ => EquipmentEvolutionDirection.Balanced
            };
            scores.TryGetValue(direction, out float current);
            scores[direction] = current + Mathf.Abs(metric.value) * 0.1f;
        }
        return scores;
    }

    private static string BuildTemporaryHistoryName(string effectId, int tier)
    {
        string stem = effectId switch
        {
            "equipment:execution" => "결전의 흔적",
            "equipment:durability" => "버텨 낸 흔적",
            "equipment:control" => "가로막은 흔적",
            "equipment:cadence" => "먼 거리의 흔적",
            "equipment:precision" => "빗나가지 않은 흔적",
            _ => "쌓여 온 흔적"
        };
        return $"{stem} {Mathf.Max(1, tier)}단계";
    }
}

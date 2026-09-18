using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// The definition used for a newly generated equipment formula. Callers must
/// construct this from the immutable catalog definition, never from a pilot
/// identifier or a separately inferred equipment kind.
/// </summary>
internal sealed class EquipmentFormulaTargetContext
{
    public EquipmentFormulaTargetContext(CombatEquipmentDefinitionSO definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        DefinitionId = definition.EquipmentId;
        if (string.IsNullOrWhiteSpace(DefinitionId))
        {
            throw new ArgumentException(
                "Equipment formula target definitions require a canonical ID.",
                nameof(definition));
        }

        Kind = definition.Kind;
        if (!Enum.IsDefined(typeof(CombatEquipmentKind), Kind)
            || !MatchesDefinitionKind(definition, Kind))
        {
            throw new ArgumentException(
                "Equipment formula target kind must come from its concrete catalog definition.",
                nameof(definition));
        }
    }

    public CombatEquipmentDefinitionSO Definition { get; }
    public string DefinitionId { get; }
    public CombatEquipmentKind Kind { get; }

    private static bool MatchesDefinitionKind(
        CombatEquipmentDefinitionSO definition,
        CombatEquipmentKind kind)
    {
        return kind switch
        {
            CombatEquipmentKind.MeleeWeapon
                or CombatEquipmentKind.RangedWeapon
                or CombatEquipmentKind.RecoverableThrowingWeapon
                => definition is CombatWeaponSO,
            CombatEquipmentKind.Armor => definition is CombatArmorSO,
            CombatEquipmentKind.Shield => definition is CombatShieldSO,
            _ => false
        };
    }
}

internal readonly struct EquipmentAttunementAdvanceResult
{
    public EquipmentAttunementAdvanceResult(
        int blockedTier,
        string nodeGenerationFailureReason)
    {
        BlockedTier = Mathf.Max(0, blockedTier);
        NodeGenerationFailureReason = nodeGenerationFailureReason?.Trim()
            ?? string.Empty;
    }

    public int BlockedTier { get; }
    public string NodeGenerationFailureReason { get; }
    public bool NodeGenerationBlocked => NodeGenerationFailureReason.Length > 0;
}

public static class EquipmentEvolutionRules
{
    public const int ModuleSelectionFormulaVersion = 2;
    public const int DrawbackModuleSelectionFormulaVersion = 3;
    public const int CurrentFormulaVersion = 4;
    private const string SelectedModuleNumericallyInfeasibleError =
        "Selected equipment module is numerically infeasible.";
    private static readonly IEvolutionModuleRegistry EvolutionModules =
        new EvolutionModuleRegistry();
    private static readonly NarrativeFormulaGenerationCostContext FormulaCostContext =
        new NarrativeFormulaGenerationCostContext(
            triggerFrequencyUnits: 0,
            guaranteedProc: false,
            targetCount: 1);

    private sealed class SelectedModuleFormulaResolution
    {
        public SelectedModuleFormulaResolution(
            NarrativeFormulaCapabilityDescriptor descriptor,
            EvolutionModuleDefinition pairedModule,
            NarrativeFormulaStrength strength,
            bool hasNegativeEvidence,
            NarrativeFormulaCandidate winner,
            NarrativeFormulaSelectedModuleResolution selectedDrawbackResolution)
        {
            Descriptor = descriptor;
            PairedModule = pairedModule;
            Strength = strength;
            HasNegativeEvidence = hasNegativeEvidence;
            Winner = winner;
            SelectedDrawbackResolution = selectedDrawbackResolution;
        }

        public NarrativeFormulaCapabilityDescriptor Descriptor { get; }
        public EvolutionModuleDefinition PairedModule { get; }
        public NarrativeFormulaStrength Strength { get; }
        public bool HasNegativeEvidence { get; }
        public NarrativeFormulaCandidate Winner { get; }
        public NarrativeFormulaSelectedModuleResolution SelectedDrawbackResolution { get; }
    }

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

    internal static EquipmentAttunementAdvanceResult AddAttunement(
        EquipmentEvolutionState state,
        EquipmentFormulaTargetContext targetContext,
        string targetResolutionFailureReason,
        string equipmentInstanceId,
        string ownerPersistentId,
        int points)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        AttunementRecord record = state.attunements.FirstOrDefault(entry =>
            entry != null
            && string.Equals(
                entry.ownerPersistentId,
                ownerPersistentId,
                StringComparison.Ordinal));
        bool isNewRecord = record == null;
        if (record == null)
        {
            record = new AttunementRecord
            {
                ownerPersistentId = ownerPersistentId,
                startedGeneration = state.generation
            };
        }

        int previousTier = record.attainedTier;
        int nextAffinityScore = Mathf.Max(0, record.affinityScore + points);
        int nextTier = nextAffinityScore >= 250
            ? 3
            : nextAffinityScore >= 100
                ? 2
                : nextAffinityScore >= 30
                    ? 1
                    : 0;
        string nodeGenerationFailureReason = string.Empty;
        if (nextTier > previousTier)
        {
            nodeGenerationFailureReason = targetResolutionFailureReason?.Trim()
                ?? string.Empty;
            if (nodeGenerationFailureReason.Length == 0 && targetContext == null)
            {
                nodeGenerationFailureReason =
                    "Equipment formula generation requires a concrete catalog target.";
            }
            else if (nodeGenerationFailureReason.Length == 0)
            {
                TryGetRuntimeApplicablePositiveOfferFailure(
                    EquipmentEvolutionFormulaCatalogSO.LoadRequired(),
                    targetContext,
                    out nodeGenerationFailureReason);
            }
        }

        if (isNewRecord)
        {
            state.attunements.Add(record);
        }
        record.affinityScore = nextAffinityScore;
        if (nodeGenerationFailureReason.Length == 0)
        {
            for (int tier = previousTier + 1; tier <= nextTier; tier++)
            {
                CreateAttunementHistoryNode(
                    state,
                    record,
                    targetContext,
                    equipmentInstanceId,
                    tier);
            }

            record.attainedTier = nextTier;
        }
        return new EquipmentAttunementAdvanceResult(
            nodeGenerationFailureReason.Length == 0 ? 0 : nextTier,
            nodeGenerationFailureReason);
    }

    internal static void CreateAttunementHistoryNode(
        EquipmentEvolutionState state,
        AttunementRecord record,
        EquipmentFormulaTargetContext targetContext,
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
        string parentNodeId = (state.presentationRequests
                ?? new List<EquipmentEvolutionPresentationRequest>())
            .Where(value => value != null
                && string.Equals(value.attunementOwnerPersistentId,
                    record.ownerPersistentId, StringComparison.Ordinal))
            .Select(value => value.nodeId)
            .LastOrDefault()
            ?? record.historyNodeIds.LastOrDefault()
            ?? string.Empty;
        string historyNodeHash = StableEvolutionHash.Compute(
            equipmentInstanceId
            + "|"
            + record.ownerPersistentId
            + "|"
            + tier
            + "|"
            + historyHash);
        string nodeId = $"equipment-history:{historyNodeHash}";
        EquipmentEvolutionFormulaCatalogSO catalog =
            EquipmentEvolutionFormulaCatalogSO.LoadRequired();
        EquipmentEvolutionDirection direction = InferDirectionFromOpenLedger(
            state.usageLedger);
        EvolutionNode node = BuildFormulaNode(
            state,
            catalog,
            targetContext,
            equipmentInstanceId,
            nodeId,
            parentNodeId,
            historyHash,
            direction,
            string.Empty,
            state.generation,
            historical: true,
            manifestationPosition: $"attunement:{record.ownerPersistentId}:{tier}");
        EquipmentEvolutionPresentationRequest request = BuildPresentationRequest(
            node,
            equipmentInstanceId,
            historyHash,
            record.ownerPersistentId,
            string.Empty,
            state.generation,
            0f);
        state.evolutionNodes.Add(node);
        state.presentationRequests ??= new List<EquipmentEvolutionPresentationRequest>();
        state.presentationRequests.Add(request);
    }

    internal static void RecordFormulaEvidence(
        EquipmentEvolutionState state,
        UsageLedgerEvent recorded)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (recorded == null) throw new ArgumentNullException(nameof(recorded));
        EquipmentEvolutionFormulaCatalogSO catalog =
            EquipmentEvolutionFormulaCatalogSO.LoadRequired();
        string evidenceId = RequireCanonical(recorded.evidenceId, "equipment evidenceId");
        state.formulaEvidence ??= new List<EquipmentEvolutionFormulaEvidenceRecord>();
        if (state.formulaEvidence.Any(value => value != null
            && string.Equals(value.evidenceId, evidenceId, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Duplicate equipment evidence '{evidenceId}'.");
        int repeatCount = Mathf.Max(1, recorded.repeatCount);
        string actor = recorded.actorId?.Trim() ?? string.Empty;
        string target = recorded.targetId?.Trim() ?? string.Empty;
        state.formulaEvidence.Add(new EquipmentEvolutionFormulaEvidenceRecord
        {
            evidenceId = evidenceId,
            eventGroupKey = RequireCanonical(recorded.eventId, "equipment eventId"),
            actionKey = recorded.eventId,
            relationshipKey = actor.Length > 0 && target.Length > 0
                ? actor + ">" + target
                : actor.Length > 0 ? actor : target,
            domainKey = "equipment",
            attainedMilestoneCount = catalog.milestoneThresholds.Count(value => repeatCount >= value),
            importancePoints = recorded.historicalEvidenceKind == HistoricalEvidenceKind.None
                ? catalog.ordinaryImportance
                : catalog.historicalImportance,
            influenceUseCount = 0,
            originalEvent = recorded.Clone()
        });
    }

    /// <summary>
    /// New formula generation is legal only when every authored effect that
    /// will actually be applied reaches a runtime consumer for this concrete
    /// equipment definition. Historical nodes retain their frozen offers and
    /// do not pass through this generation-time predicate.
    /// </summary>
    internal static bool HasRuntimeApplicablePositiveOffer(
        EquipmentEvolutionFormulaCatalogSO catalog,
        EquipmentFormulaTargetContext targetContext)
    {
        return GetRuntimeApplicablePositiveModuleIds(catalog, targetContext).Count > 0;
    }

    internal static IReadOnlyList<string> GetRuntimeApplicablePositiveModuleIds(
        EquipmentEvolutionFormulaCatalogSO catalog,
        EquipmentFormulaTargetContext targetContext)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (targetContext == null) throw new ArgumentNullException(nameof(targetContext));
        return catalog.capabilities
            .Where(value => value != null)
            .Select(value => RequireEvolutionModule(value.capabilityId))
            .Where(value => value.IsPositiveModule
                && IsRuntimeApplicable(value, targetContext))
            .Select(value => value.ModuleId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public static int CountRuntimeApplicablePositiveModuleOffers(
        EquipmentEvolutionFormulaCatalogSO catalog,
        CombatEquipmentDefinitionSO definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        return GetRuntimeApplicablePositiveModuleIds(
            catalog,
            new EquipmentFormulaTargetContext(definition)).Count;
    }

    internal static bool TryGetRuntimeApplicablePositiveOfferFailure(
        EquipmentEvolutionFormulaCatalogSO catalog,
        EquipmentFormulaTargetContext targetContext,
        out string failureReason)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (targetContext == null) throw new ArgumentNullException(nameof(targetContext));
        if (HasRuntimeApplicablePositiveOffer(catalog, targetContext))
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = "Equipment formula generation has no runtime-applicable positive offer for "
            + $"definition '{targetContext.DefinitionId}' ({targetContext.Kind}).";
        return false;
    }

    internal static void RequireRuntimeApplicablePositiveOffer(
        EquipmentEvolutionFormulaCatalogSO catalog,
        EquipmentFormulaTargetContext targetContext)
    {
        if (!TryGetRuntimeApplicablePositiveOfferFailure(
                catalog, targetContext, out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
    }

    internal static EvolutionNode BuildFormulaNode(
        EquipmentEvolutionState state,
        EquipmentEvolutionFormulaCatalogSO catalog,
        EquipmentFormulaTargetContext targetContext,
        string equipmentInstanceId,
        string nodeId,
        string parentNodeId,
        string historyHash,
        EquipmentEvolutionDirection direction,
        string catalystFamily,
        int generation,
        bool historical,
        string manifestationPosition,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings = null)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (targetContext == null) throw new ArgumentNullException(nameof(targetContext));
        RequireRuntimeApplicablePositiveOffer(catalog, targetContext);
        NarrativeFormulaStrengthPolicy policy = catalog.RequirePolicy();
        string catalogSha256 = catalog.formulaPolicy.RequireCatalogSha256();
        EquipmentEvolutionFormulaEvidenceRecord[] sourceEvidence =
            (state.formulaEvidence ?? new List<EquipmentEvolutionFormulaEvidenceRecord>())
            .Where(value => value != null)
            .OrderBy(value => value.evidenceId, StringComparer.Ordinal)
            .ToArray();
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings = (outcomeBindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal).ToArray();
        if (sourceEvidence.Length == 0 && exactBindings.Length == 0)
            throw new InvalidOperationException("Equipment formula generation requires original evidence.");
        NarrativeFormulaEvidence[] evidence = sourceEvidence.Select(value =>
            new NarrativeFormulaEvidence(
                value.evidenceId,
                value.eventGroupKey,
                value.actionKey,
                value.relationshipKey,
                value.domainKey,
                value.attainedMilestoneCount,
                value.importancePoints,
                value.influenceUseCount))
            .Concat(exactBindings.Select(value =>
                GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(value, policy)))
            .ToArray();
        NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(policy, evidence);
        int budget = strength.Budget;
        if (budget < 0) throw new InvalidOperationException("Equipment formula budget is invalid.");
        if (policy.FormulaVersion >= ModuleSelectionFormulaVersion)
        {
            List<EquipmentEvolutionModuleOfferState> offers =
                GetRuntimeApplicablePositiveModuleIds(catalog, targetContext)
                .Select(RequireEvolutionModule)
                .Select(value => new EquipmentEvolutionModuleOfferState
                {
                    moduleId = value.ModuleId,
                    polarity = EvolutionModuleOfferPolarity.Positive,
                    semanticDescription = DescribeModule(value, catalog)
                }).ToList();
            if (policy.FormulaVersion >= DrawbackModuleSelectionFormulaVersion)
            {
                bool creditReachable = catalog.formulaPolicy.RequireDrawbackPolicy()
                    .Resolve(budget, new NarrativeFormulaDrawbackOption(
                        "equipment:drawback-probe", 1,
                        NarrativeFormulaDrawbackSelectionKind.Automatic,
                        true, true, false, false, true)).AcceptedCredit > 0;
                if (creditReachable)
                {
                    foreach (EvolutionModuleDefinition module in EvolutionModules.All
                                 .Where(value => value.ModuleId.StartsWith(
                                         "equipment:", StringComparison.Ordinal)
                                     && value.BurdenKind ==
                                         EvolutionModuleBurdenKind.OptionalDrawback
                                     && IsRuntimeApplicable(value, targetContext)
                                      && (sourceEvidence.Any(evidence => evidence.originalEvent != null
                                          && value.MatchesNegativeEvidence(
                                              evidence.originalEvent.eventId,
                                              evidence.originalEvent.outcomeId,
                                              evidence.originalEvent.historicalEvidenceKind.ToString()))
                                           || exactBindings.Any(binding =>
                                               GameplayOutcomeEvidenceFormulaProjection.IsNegative(binding)))))
                    {
                        EquipmentEvolutionModuleOfferState drawbackOffer = new()
                        {
                            moduleId = module.ModuleId,
                            polarity = EvolutionModuleOfferPolarity.Drawback,
                            semanticDescription = DescribeModule(module, catalog)
                        };
                        // V3 pending selections are frozen authority. New V4-and-later
                        // offers must prove every contract-valid positive pairing resolves
                        // with this drawback's supporting evidence before it is exposed.
                        if (policy.FormulaVersion > DrawbackModuleSelectionFormulaVersion
                            && !HasFeasibleOptionalDrawbackPair(
                                state,
                                catalog,
                                policy.FormulaVersion,
                                catalogSha256,
                                nodeId,
                                equipmentInstanceId,
                                offers,
                                drawbackOffer,
                                sourceEvidence.Where(evidence => evidence.originalEvent != null
                                    && module.MatchesNegativeEvidence(
                                        evidence.originalEvent.eventId,
                                        evidence.originalEvent.outcomeId,
                                        evidence.originalEvent.historicalEvidenceKind.ToString()))
                                    .Select(evidence => evidence.evidenceId)
                                    .Concat(exactBindings
                                        .Where(GameplayOutcomeEvidenceFormulaProjection.IsNegative)
                                        .Select(value => value.publicFactId)),
                                exactBindings))
                            continue;
                        offers.Add(drawbackOffer);
                    }
                }
            }
            offers = offers.OrderBy(value => value.moduleId, StringComparer.Ordinal).ToList();
            if (offers.Count == 0)
                throw new InvalidOperationException(
                    "Equipment formula generation has no runtime-applicable positive offer for "
                    + $"definition '{targetContext.DefinitionId}' ({targetContext.Kind}).");
            string selectionHash = ComputeSha256(string.Join("|",
                RequireCanonical(equipmentInstanceId, "equipment instance ID"),
                RequireCanonical(manifestationPosition, "equipment manifestation position"),
                policy.FormulaVersion, catalogSha256,
                string.Join(",", offers.Select(value =>
                    value.moduleId + ":" + (int)value.polarity)),
                string.Join(",", evidence.Select(value => value.EvidenceId))));
            string selectionId = "presentation:equipment:" + selectionHash;
        return new EvolutionNode
        {
                nodeId = RequireCanonical(nodeId, "equipment node ID"),
                parentNodeId = parentNodeId?.Trim() ?? string.Empty,
                generation = Mathf.Max(0, generation),
                active = false,
                historical = historical,
                mechanicallyUnlocked = false,
                narrativeReady = false,
                uiVisible = false,
                playerVisible = false,
                formulaVersion = policy.FormulaVersion,
                formulaCatalogSha256 = catalogSha256,
                formulaBudget = budget,
                evidenceIds = evidence.Select(value => value.EvidenceId)
                    .OrderBy(value => value, StringComparer.Ordinal).ToList(),
                gameplayOutcomeEvidence = exactBindings
                    .Select(value => value.Clone()).ToList(),
                presentationId = selectionId,
                moduleSelectionId = selectionId,
                moduleSelectionOffers = offers,
                presentationState = EquipmentEvolutionPresentationState.ModuleSelectionPending,
                activationRule = new EvolutionModuleActivationRule()
            };
        }
        string capabilityId = ResolveModuleId(direction, catalystFamily);
        EquipmentEvolutionFormulaCapabilityDefinition capability =
            catalog.RequireCapability(capabilityId);
        NarrativeFormulaCapabilityDescriptor descriptor = capability.ToRuntime();
        EvolutionModuleDefinition pairedModule = new EvolutionModuleRegistry().All.Single(value =>
            string.Equals(value.ModuleId, capabilityId, StringComparison.Ordinal));
        if (pairedModule.Burdens.Count == 0)
            throw new InvalidOperationException(
                $"Equipment formula capability '{capabilityId}' has no inseparable runtime burden.");
        bool hasNegativeEvidence = HasNegativeEvidence(sourceEvidence)
            || exactBindings.Any(GameplayOutcomeEvidenceFormulaProjection.IsNegative);
        NarrativeFormulaDrawbackOption drawback = new(
            "equipment-burden:" + capabilityId,
            Math.Max(1, pairedModule.RiskWeight),
            NarrativeFormulaDrawbackSelectionKind.Automatic,
            reachable: true,
            mandatory: true,
            separatelyRemovable: false,
            cancelsSelectedBenefit: false,
            hasNegativeNarrativeEvidence: hasNegativeEvidence);
        double novelty = (state.evolutionNodes ?? new List<EvolutionNode>())
            .Where(value => value != null
                && value.formulaVersion > 0
                && value.presentationState == EquipmentEvolutionPresentationState.Ready)
            .SelectMany(value => value.formulaCapabilities
                ?? new List<EquipmentEvolutionFormulaCapabilityEnvelope>())
            .Any(value => value != null && string.Equals(
                value.capabilityId, capabilityId, StringComparison.Ordinal)) ? 0d : 1d;
        IReadOnlyList<NarrativeFormulaCandidate> candidates = NarrativeFormulaCore.Optimize(
            descriptor,
            FormulaCostContext,
            budget,
            descriptor.NarrativeAffinity,
            novelty,
            evidence.Select(value => value.EvidenceId),
            maximumResults: 3,
            drawbackPolicy: catalog.formulaPolicy.RequireDrawbackPolicy(),
            drawbackOption: drawback);
        if (candidates.Count == 0)
            throw new InvalidOperationException("Equipment formula has no legal candidate within budget.");
        NarrativeFormulaCandidate winner = NarrativeFormulaCore.ChooseDeterministicWinner(
            candidates,
            RequireCanonical(equipmentInstanceId, "equipment instance ID"),
            RequireCanonical(manifestationPosition, "equipment manifestation position"),
            policy.FormulaVersion,
            catalogSha256);
        EquipmentEvolutionFormulaCapabilityEnvelope[] envelopes = winner.Allocations
            .Select(allocation => new EquipmentEvolutionFormulaCapabilityEnvelope
            {
                capabilityId = allocation.Descriptor.CapabilityId,
                formatterId = allocation.Descriptor.FormatterId,
                applicatorId = allocation.Descriptor.ApplicatorId,
                parameters = allocation.Parameters.Select(parameter =>
                    new EquipmentEvolutionFormulaParameterEnvelope
                    {
                        parameterId = parameter.ParameterId,
                        units = parameter.Units
                    }).ToList()
            }).ToArray();
        string presentationId = "presentation:equipment:" + ComputeSha256(
            string.Join("|",
                equipmentInstanceId,
                manifestationPosition,
                policy.FormulaVersion,
                catalogSha256,
                winner.CanonicalSignature));
        EvolutionNode node = new EvolutionNode
        {
            nodeId = RequireCanonical(nodeId, "equipment node ID"),
            parentNodeId = parentNodeId?.Trim() ?? string.Empty,
            generation = Mathf.Max(0, generation),
            active = false,
            historical = historical,
            mechanicallyUnlocked = false,
            narrativeReady = false,
            uiVisible = false,
            playerVisible = false,
            formulaVersion = policy.FormulaVersion,
            formulaCatalogSha256 = catalogSha256,
            formulaCapabilities = envelopes.ToList(),
            formulaBudget = budget,
            calculatedCost = winner.CalculatedCost,
            positiveCost = winner.PositiveCost,
            drawbackCredit = winner.DrawbackCredit,
            drawbackId = winner.DrawbackId,
            drawbackEvidenceQualified = hasNegativeEvidence,
            evidenceIds = winner.EvidenceIds.ToList(),
            gameplayOutcomeEvidence = exactBindings
                .Select(value => value.Clone()).ToList(),
            presentationId = presentationId,
            presentationState = EquipmentEvolutionPresentationState.PresentationPending,
            activationRule = new EvolutionModuleActivationRule()
        };
        node.mechanicalDescription = FormatMechanicalDescription(node, catalog);
        ValidateFormulaNode(node, catalog);
        return node;
    }

    private static string ComputeSha256(string value)
    {
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }

    internal static EquipmentEvolutionPresentationRequest BuildPresentationRequest(
        EvolutionNode node,
        string equipmentInstanceId,
        string historyHash,
        string attunementOwnerPersistentId,
        string reforgeOrderId,
        int targetGeneration,
        float masteryCost) => new()
    {
        presentationId = node?.presentationId ?? string.Empty,
        nodeId = node?.nodeId ?? string.Empty,
        targetPersistentId = equipmentInstanceId?.Trim() ?? string.Empty,
        historyHash = historyHash ?? string.Empty,
        attunementOwnerPersistentId = attunementOwnerPersistentId?.Trim() ?? string.Empty,
        reforgeOrderId = reforgeOrderId?.Trim() ?? string.Empty,
        targetGeneration = Mathf.Max(0, targetGeneration),
        masteryCost = Mathf.Max(0f, masteryCost),
        state = node?.presentationState
            ?? EquipmentEvolutionPresentationState.PresentationPending,
        evidenceIds = node?.evidenceIds?.OrderBy(value => value, StringComparer.Ordinal).ToList()
            ?? new List<string>(),
        gameplayOutcomeEvidence = node?.gameplayOutcomeEvidence?
            .Where(value => value != null).Select(value => value.Clone())
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal).ToList()
            ?? new List<GameplayOutcomeEvidenceBindingSnapshot>()
    };

    private static bool HasFeasibleOptionalDrawbackPair(
        EquipmentEvolutionState state,
        EquipmentEvolutionFormulaCatalogSO catalog,
        int formulaVersion,
        string catalogSha256,
        string nodeId,
        string equipmentInstanceId,
        IEnumerable<EquipmentEvolutionModuleOfferState> positiveOffers,
        EquipmentEvolutionModuleOfferState drawbackOffer,
        IEnumerable<string> supportingEvidenceIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings)
    {
        if (drawbackOffer == null
            || drawbackOffer.polarity != EvolutionModuleOfferPolarity.Drawback)
            throw new ArgumentException("An optional drawback offer is required.", nameof(drawbackOffer));
        string[] supporting = (supportingEvidenceIds
                ?? throw new ArgumentNullException(nameof(supportingEvidenceIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (supporting.Length == 0)
            return false;
        EvolutionNode prospective = new()
        {
            nodeId = RequireCanonical(nodeId, "equipment node ID"),
            formulaCatalogSha256 = catalogSha256,
            formulaVersion = formulaVersion,
            moduleSelectionId = "prospective:equipment:optional-drawback",
            evidenceIds = (state?.formulaEvidence
                    ?? throw new ArgumentNullException(nameof(state)))
                .Where(value => value != null)
                .Select(value => value.evidenceId)
                .Concat((outcomeBindings
                        ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
                    .Where(value => value != null)
                    .Select(value => value.publicFactId))
                .OrderBy(value => value, StringComparer.Ordinal).ToList(),
            gameplayOutcomeEvidence = (outcomeBindings
                    ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
                .Where(value => value != null)
                .Select(value => value.Clone()).ToList(),
            moduleSelectionOffers = (positiveOffers ?? throw new ArgumentNullException(nameof(positiveOffers)))
                .Where(value => value != null
                    && value.polarity == EvolutionModuleOfferPolarity.Positive)
                .Concat(new[] { drawbackOffer })
                .Select(value => value.Clone())
                .OrderBy(value => value.moduleId, StringComparer.Ordinal).ToList()
        };
        NarrativeFormulaModuleSelectionRequest request =
            BuildModuleSelectionRequest(prospective, catalog);
        int selectablePairCount = 0;
        foreach (NarrativeFormulaModuleOffer positive in request.Offers.Where(value =>
                     value.Polarity == NarrativeFormulaModulePolarity.Positive))
        {
            NarrativeFormulaModuleSelectionChoice choice = new(
                request.SelectionId,
                new[] { positive.ModuleId },
                new[] { drawbackOffer.moduleId },
                supporting);
            if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                    request, choice, out NarrativeFormulaValidatedModuleSelection selected,
                    out _))
                continue;
            selectablePairCount++;
            try
            {
                ResolveSelectedModuleFormula(
                    prospective, state, catalog, equipmentInstanceId, selected);
            }
            catch (InvalidOperationException error) when (string.Equals(
                error.Message, SelectedModuleNumericallyInfeasibleError,
                StringComparison.Ordinal))
            {
                return false;
            }
        }
        return selectablePairCount > 0;
    }

    private static bool HasNegativeEvidence(
        IEnumerable<EquipmentEvolutionFormulaEvidenceRecord> sourceEvidence) =>
        sourceEvidence.Any(value => value?.originalEvent != null
            && (NarrativeFormulaNegativeEvidence.Matches(
                    value.originalEvent.eventId, value.originalEvent.outcomeId)
                || value.originalEvent.historicalEvidenceKind is
                    HistoricalEvidenceKind.SurvivedNearDeath
                    or HistoricalEvidenceKind.ArmorBroken
                    or HistoricalEvidenceKind.OwnerDeathWitnessed
                    or HistoricalEvidenceKind.UsedDuringPlague));

    internal static NarrativeFormulaModuleSelectionRequest BuildModuleSelectionRequest(
        EvolutionNode node,
        EquipmentEvolutionFormulaCatalogSO catalog)
    {
        if (node == null || catalog == null
            || node.formulaVersion < ModuleSelectionFormulaVersion
            || string.IsNullOrWhiteSpace(node.moduleSelectionId))
            throw new InvalidOperationException("Equipment module selection is not pending.");
        EquipmentEvolutionModuleOfferState[] persisted = (node.moduleSelectionOffers
                ?? new List<EquipmentEvolutionModuleOfferState>())
            .Where(value => value != null)
            .OrderBy(value => value.moduleId, StringComparer.Ordinal).ToArray();
        List<NarrativeFormulaModuleOffer> offers = new();
        foreach (EquipmentEvolutionModuleOfferState offer in persisted)
        {
            NarrativeFormulaCapabilityDescriptor descriptor;
            string semantic;
            IEnumerable<string> requiredGroups = Array.Empty<string>();
            if (offer.polarity == EvolutionModuleOfferPolarity.Positive)
            {
                EquipmentEvolutionFormulaCapabilityDefinition definition =
                    catalog.RequireCapability(offer.moduleId);
                EvolutionModuleDefinition module = RequireEvolutionModule(offer.moduleId);
                if (!module.IsPositiveModule)
                    throw new InvalidOperationException(
                        $"Equipment positive offer '{offer.moduleId}' is an optional drawback.");
                descriptor = definition.ToRuntime();
                semantic = DescribeModule(module, catalog);
            }
            else
            {
                if (node.formulaVersion < DrawbackModuleSelectionFormulaVersion)
                    throw new InvalidOperationException(
                        "Legacy equipment selection cannot contain independent drawbacks.");
                EvolutionModuleDefinition module = RequireEvolutionModule(offer.moduleId);
                descriptor = EvolutionModuleFormulaDescriptor.ForOptionalDrawback(
                    module,
                    "equipment-drawback",
                    GetCatalogForbiddenSynergies(module, catalog));
                semantic = DescribeModule(module, catalog);
                requiredGroups = new[] { "equipment-primary" };
            }
            EvolutionModuleDefinition authoredModule = RequireEvolutionModule(offer.moduleId);
            string legacySemantic = DescribeLegacyModule(authoredModule, catalog);
            if (!string.Equals(semantic, offer.semanticDescription, StringComparison.Ordinal)
                && !string.Equals(
                    legacySemantic,
                    offer.semanticDescription,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Equipment module offer '{offer.moduleId}' changed after submission.");
            offers.Add(new NarrativeFormulaModuleOffer(
                offer.moduleId,
                EvolutionModuleFormulaDescriptor.ToNarrativePolarity(offer.polarity),
                semantic,
                new NarrativeFormulaCapabilityInput(
                    descriptor, descriptor.NarrativeAffinity, 1d),
                offer.polarity == EvolutionModuleOfferPolarity.Drawback ? 1 : 0,
                requiredCompanionGroups: requiredGroups));
        }
        return new NarrativeFormulaModuleSelectionRequest(
            node.moduleSelectionId,
            offers,
            node.evidenceIds,
            maximumPositiveModules: 1,
            maximumDrawbackModules: node.formulaVersion >= DrawbackModuleSelectionFormulaVersion
                && offers.Any(value => value.Polarity == NarrativeFormulaModulePolarity.Drawback)
                ? 1 : 0);
    }

    private static SelectedModuleFormulaResolution ResolveSelectedModuleFormula(
        EvolutionNode pending,
        EquipmentEvolutionState state,
        EquipmentEvolutionFormulaCatalogSO catalog,
        string equipmentInstanceId,
        NarrativeFormulaValidatedModuleSelection selected)
    {
        NarrativeFormulaModuleOffer selectedOffer = selected.PositiveModules.Single();
        EquipmentEvolutionFormulaCapabilityDefinition capability =
            catalog.RequireCapability(selectedOffer.ModuleId);
        NarrativeFormulaCapabilityDescriptor descriptor = capability.ToRuntime();
        HashSet<string> exactIds = (pending.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        EquipmentEvolutionFormulaEvidenceRecord[] sourceEvidence = selected.EvidenceFactIds
            .Where(id => !exactIds.Contains(id))
            .Select(id => state.formulaEvidence.SingleOrDefault(value => value != null
                && string.Equals(value.evidenceId, id, StringComparison.Ordinal)))
            .ToArray();
        if (sourceEvidence.Any(value => value == null))
            throw new InvalidOperationException("Equipment module selection lost authoritative evidence.");
        GameplayOutcomeEvidenceBindingSnapshot[] selectedExact =
            (pending.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null
                && selected.EvidenceFactIds.Contains(value.publicFactId, StringComparer.Ordinal))
            .ToArray();
        if (selectedExact.Length != exactIds.Count(id =>
                selected.EvidenceFactIds.Contains(id, StringComparer.Ordinal)))
            throw new InvalidOperationException("Equipment selection lost exact outcome evidence.");
        bool hasNegativeEvidence = HasNegativeEvidence(sourceEvidence)
            || selectedExact.Any(GameplayOutcomeEvidenceFormulaProjection.IsNegative);
        EvolutionModuleDefinition pairedModule =
            RequireEvolutionModule(selectedOffer.ModuleId);
        double novelty = (state.evolutionNodes ?? new List<EvolutionNode>())
            .Where(value => value != null && value.formulaVersion > 0
                && value.presentationState == EquipmentEvolutionPresentationState.Ready)
            .SelectMany(value => value.formulaCapabilities
                ?? new List<EquipmentEvolutionFormulaCapabilityEnvelope>())
            .Any(value => value != null && string.Equals(
                value.capabilityId, selectedOffer.ModuleId, StringComparison.Ordinal)) ? 0d : 1d;
        NarrativeFormulaEvidence[] evidence = sourceEvidence.Select(value =>
            new NarrativeFormulaEvidence(
                value.evidenceId, value.eventGroupKey, value.actionKey,
                value.relationshipKey, value.domainKey, value.attainedMilestoneCount,
                value.importancePoints, value.influenceUseCount))
            .Concat(selectedExact.Select(value =>
                GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(
                    value, catalog.RequirePolicy())))
            .ToArray();
        NarrativeFormulaStrength selectedStrength = NarrativeFormulaCore.CalculateStrength(
            catalog.RequirePolicy(), evidence);
        IReadOnlyList<NarrativeFormulaCandidate> candidates;
        NarrativeFormulaSelectedModuleResolution selectedResolution = null;
        if (selected.DrawbackModules.Count > 0)
        {
            IReadOnlyList<NarrativeFormulaSelectedModuleResolution> resolutions =
                NarrativeFormulaCore.OptimizeSelectedModules(
                    selected,
                    FormulaCostContext,
                    selectedStrength.Budget,
                    catalog.formulaPolicy.RequireDrawbackPolicy(),
                    NarrativeFormulaDrawbackSelectionKind.Automatic,
                    hasNegativeEvidence,
                    maximumResults: 3);
            candidates = resolutions.Select(value => value.Positive).ToArray();
            if (candidates.Count > 0)
            {
                NarrativeFormulaCandidate picked = NarrativeFormulaCore.ChooseDeterministicWinner(
                    candidates,
                    RequireCanonical(equipmentInstanceId, "equipment instance ID"),
                    pending.nodeId,
                    pending.formulaVersion,
                    pending.formulaCatalogSha256);
                selectedResolution = resolutions.Single(value => string.Equals(
                    value.Positive.CanonicalSignature, picked.CanonicalSignature,
                    StringComparison.Ordinal));
            }
        }
        else
        {
            NarrativeFormulaDrawbackOption inseparable =
                pending.formulaVersion < DrawbackModuleSelectionFormulaVersion
                    || pairedModule.BurdenKind == EvolutionModuleBurdenKind.InseparableRisk
                    ? new NarrativeFormulaDrawbackOption(
                        "equipment-burden:" + selectedOffer.ModuleId,
                        Math.Max(1, pairedModule.RiskWeight),
                        NarrativeFormulaDrawbackSelectionKind.Automatic,
                        true, true, false, false, hasNegativeEvidence)
                    : null;
            candidates = NarrativeFormulaCore.Optimize(
                descriptor,
                FormulaCostContext,
                selectedStrength.Budget,
                descriptor.NarrativeAffinity,
                novelty,
                evidence.Select(value => value.EvidenceId),
                maximumResults: 3,
                drawbackPolicy: inseparable == null
                    ? null : catalog.formulaPolicy.RequireDrawbackPolicy(),
                drawbackOption: inseparable);
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException(SelectedModuleNumericallyInfeasibleError);
        NarrativeFormulaCandidate winner = selectedResolution?.Positive
            ?? NarrativeFormulaCore.ChooseDeterministicWinner(
                candidates,
                RequireCanonical(equipmentInstanceId, "equipment instance ID"),
                pending.nodeId,
                pending.formulaVersion,
                pending.formulaCatalogSha256);
        return new SelectedModuleFormulaResolution(
            descriptor,
            pairedModule,
            selectedStrength,
            hasNegativeEvidence,
            winner,
            selectedResolution);
    }

    internal static EvolutionNode FreezeSelectedModule(
        EvolutionNode pending,
        EquipmentEvolutionState state,
        EquipmentEvolutionFormulaCatalogSO catalog,
        string equipmentInstanceId,
        NarrativeFormulaModuleSelectionChoice choice)
    {
        NarrativeFormulaModuleSelectionRequest request =
            BuildModuleSelectionRequest(pending, catalog);
        if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                request, choice, out NarrativeFormulaValidatedModuleSelection selected,
                out string selectionError))
            throw new InvalidOperationException(selectionError);
        SelectedModuleFormulaResolution resolution = ResolveSelectedModuleFormula(
            pending, state, catalog, equipmentInstanceId, selected);
        EvolutionNode node = pending.Clone();
        node.formulaBudget = resolution.Strength.Budget;
        node.formulaCapabilities = resolution.Winner.Allocations.Select(allocation =>
            new EquipmentEvolutionFormulaCapabilityEnvelope
            {
                capabilityId = allocation.Descriptor.CapabilityId,
                formatterId = allocation.Descriptor.FormatterId,
                applicatorId = allocation.Descriptor.ApplicatorId,
                parameters = allocation.Parameters.Select(parameter =>
                    new EquipmentEvolutionFormulaParameterEnvelope
                    {
                        parameterId = parameter.ParameterId,
                        units = parameter.Units
                    }).ToList()
            }).ToList();
        node.calculatedCost = resolution.Winner.CalculatedCost;
        node.positiveCost = resolution.Winner.PositiveCost;
        node.drawbackCredit = resolution.Winner.DrawbackCredit;
        node.drawbackId = resolution.Winner.DrawbackId;
        node.drawbackEvidenceQualified = selected.DrawbackModules.Count > 0
            || (pending.formulaVersion < DrawbackModuleSelectionFormulaVersion
                || resolution.PairedModule.BurdenKind == EvolutionModuleBurdenKind.InseparableRisk)
                && resolution.HasNegativeEvidence;
        node.burdenEffectId = selected.DrawbackModules.SingleOrDefault()?.ModuleId
            ?? string.Empty;
        node.burdenPotencyMultiplier = resolution.SelectedDrawbackResolution?.DrawbackSeverity == null
            ? 0f
            : ResolveMagnitude(
                resolution.SelectedDrawbackResolution.DrawbackSeverity,
                resolution.SelectedDrawbackResolution.DrawbackSeverity.Allocations.Single().Descriptor);
        node.evidenceIds = resolution.Winner.EvidenceIds.OrderBy(value => value, StringComparer.Ordinal).ToList();
        node.gameplayOutcomeEvidence = GameplayOutcomeEvidenceFormulaProjection.Select(
            pending.gameplayOutcomeEvidence,
            node.evidenceIds);
        node.mechanicalDescription = FormatMechanicalDescription(node, catalog);
        return node;
    }

    [GameplayInternalOnly(
        "The controlled review export replays an authored equipment module choice through the runtime formula authority without publishing runtime state.",
        "FormulaPresentationPilotExporter")]
    public static EvolutionNode FreezeSelectedModuleForExport(
        EvolutionNode pending,
        EquipmentEvolutionState state,
        EquipmentEvolutionFormulaCatalogSO catalog,
        string equipmentInstanceId,
        NarrativeFormulaModuleSelectionChoice choice) => FreezeSelectedModule(
            pending, state, catalog, equipmentInstanceId, choice);

    internal static void ValidateFormulaNode(
        EvolutionNode node,
        EquipmentEvolutionFormulaCatalogSO catalog)
    {
        if (node == null || catalog == null)
            throw new InvalidOperationException("Equipment formula validation requires a node and catalog.");
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings =
            (node.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .ToArray();
        if (exactBindings.Length != (node.gameplayOutcomeEvidence?.Count ?? 0)
            || exactBindings.Select(value => value.publicFactId)
                .Distinct(StringComparer.Ordinal).Count() != exactBindings.Length
            || exactBindings.Any(value =>
                !GameplayOutcomeEvidenceBindingAuthority.TryValidate(value, out _))
            || exactBindings.Any(value => !(node.evidenceIds
                    ?? new List<string>()).Contains(
                    value.publicFactId,
                    StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Equipment formula node has invalid exact-outcome evidence bindings.");
        NarrativeFormulaStrengthPolicy policy = catalog.RequirePolicy();
        if (node.presentationState == EquipmentEvolutionPresentationState.ModuleSelectionPending)
        {
            if (node.formulaVersion < ModuleSelectionFormulaVersion
                || node.formulaVersion != policy.FormulaVersion
                || !string.Equals(node.formulaCatalogSha256,
                    catalog.formulaPolicy.RequireCatalogSha256(), StringComparison.Ordinal)
                || !IsCanonical(node.nodeId) || !IsOptionalCanonical(node.parentNodeId)
                || !IsPresentationId(node.presentationId)
                || !string.Equals(node.moduleSelectionId, node.presentationId, StringComparison.Ordinal)
                || node.formulaBudget < policy.BaseBudget
                || node.formulaBudget > checked(policy.BaseBudget + policy.PowerScale)
                || node.formulaCapabilities == null || node.formulaCapabilities.Count != 0
                || node.calculatedCost != 0 || node.positiveCost != 0
                || node.drawbackCredit != 0 || !string.IsNullOrEmpty(node.drawbackId)
                || !string.IsNullOrEmpty(node.mechanicalDescription)
                || node.moduleSelectionOffers == null || node.moduleSelectionOffers.Count == 0
                || node.moduleSelectionOffers.Select(value => value?.moduleId)
                    .Distinct(StringComparer.Ordinal).Count() != node.moduleSelectionOffers.Count)
                throw new InvalidOperationException("Equipment unresolved module-selection node is invalid.");
            BuildModuleSelectionRequest(node, catalog);
            return;
        }
        if (!IsCanonical(node.nodeId)
            || !IsOptionalCanonical(node.parentNodeId)
            || node.formulaVersion > policy.FormulaVersion
            || node.formulaVersion >= DrawbackModuleSelectionFormulaVersion
                && node.formulaVersion != policy.FormulaVersion
            || node.formulaVersion >= DrawbackModuleSelectionFormulaVersion
                && !string.Equals(node.formulaCatalogSha256,
                    catalog.formulaPolicy.RequireCatalogSha256(), StringComparison.Ordinal)
            || !IsPresentationId(node.presentationId)
            || node.formulaCapabilities == null
            || node.formulaCapabilities.Count < 1
            || node.formulaCapabilities.Count > 3
            || node.formulaCapabilities.Any(value => value == null)
            || node.formulaCapabilities.Select(value => value?.capabilityId)
                .Distinct(StringComparer.Ordinal).Count() != node.formulaCapabilities.Count
            || node.evidenceIds == null
            || node.evidenceIds.Count == 0
            || node.evidenceIds.Any(id => !IsCanonical(id))
            || node.evidenceIds.Distinct(StringComparer.Ordinal).Count() != node.evidenceIds.Count
            || !string.IsNullOrEmpty(node.effectId)
            || node.formulaVersion < DrawbackModuleSelectionFormulaVersion
                && !string.IsNullOrEmpty(node.burdenEffectId)
            || (node.legalCandidateEffectIds?.Count ?? 0) != 0
            || node.selectedCandidateIndex != -1)
            throw new InvalidOperationException("Equipment formula node identity or frozen evidence is invalid.");
        List<NarrativeFormulaCapabilityAllocation> allocations = new();
        foreach (EquipmentEvolutionFormulaCapabilityEnvelope envelope in node.formulaCapabilities)
        {
            EquipmentEvolutionFormulaCapabilityDefinition definition =
                catalog.RequireCapability(envelope.capabilityId);
            NarrativeFormulaCapabilityDescriptor descriptor = definition.ToRuntime();
            if (!string.Equals(envelope.formatterId, descriptor.FormatterId, StringComparison.Ordinal)
                || !string.Equals(envelope.applicatorId, descriptor.ApplicatorId, StringComparison.Ordinal))
                throw new InvalidOperationException("Equipment formula formatter/applicator changed after freezing.");
            allocations.Add(new NarrativeFormulaCapabilityAllocation(
                descriptor,
                (envelope.parameters ?? new List<EquipmentEvolutionFormulaParameterEnvelope>())
                .Select(value => new NarrativeFormulaParameterValue(value.parameterId, value.units))));
        }
        int calculated = NarrativeFormulaCore.CalculateCost(allocations, FormulaCostContext);
        int maximumBudget = checked(policy.BaseBudget + policy.PowerScale);
        if (node.formulaBudget < policy.BaseBudget
            || node.formulaBudget > maximumBudget
            || calculated != node.positiveCost
            || node.calculatedCost != node.positiveCost - node.drawbackCredit
            || node.calculatedCost > node.formulaBudget)
            throw new InvalidOperationException("Equipment formula calculated cost changed after freezing.");
        EquipmentEvolutionFormulaCapabilityDefinition primary = catalog.RequireCapability(
            node.formulaCapabilities[0].capabilityId);
        EvolutionModuleDefinition module = new EvolutionModuleRegistry().All.Single(value =>
            string.Equals(value.ModuleId, primary.capabilityId, StringComparison.Ordinal));
        NarrativeFormulaDrawbackResolution expectedDrawback;
        if (node.formulaVersion < DrawbackModuleSelectionFormulaVersion)
        {
            expectedDrawback = catalog.formulaPolicy.RequireDrawbackPolicy().Resolve(
                node.formulaBudget,
                new NarrativeFormulaDrawbackOption(
                    "equipment-burden:" + primary.capabilityId,
                    Math.Max(1, module.RiskWeight),
                    NarrativeFormulaDrawbackSelectionKind.Automatic,
                    true, true, false, false, node.drawbackEvidenceQualified));
        }
        else if (!string.IsNullOrEmpty(node.burdenEffectId))
        {
            EvolutionModuleDefinition optional = RequireEvolutionModule(node.burdenEffectId);
            if (optional.BurdenKind != EvolutionModuleBurdenKind.OptionalDrawback
                || optional.ForbiddenSynergyModuleIds.Contains(
                    module.ModuleId, StringComparer.Ordinal)
                || node.burdenPotencyMultiplier < 1f
                || node.burdenPotencyMultiplier > optional.MaximumDrawbackSeverity
                || Math.Abs(node.burdenPotencyMultiplier
                    - Math.Round(node.burdenPotencyMultiplier)) > 0.001d
                || !node.drawbackEvidenceQualified)
                throw new InvalidOperationException(
                    "Equipment optional drawback severity is invalid.");
            string drawbackId = "selected-drawbacks:"
                + NarrativeInferenceHash.ComputeSha256Utf8(optional.ModuleId)
                    .Substring("sha256:".Length);
            expectedDrawback = catalog.formulaPolicy.RequireDrawbackPolicy().Resolve(
                node.formulaBudget,
                new NarrativeFormulaDrawbackOption(
                    drawbackId,
                    (int)Math.Round(node.burdenPotencyMultiplier),
                    NarrativeFormulaDrawbackSelectionKind.Automatic,
                    true, true, false, false, true));
        }
        else if (module.BurdenKind == EvolutionModuleBurdenKind.InseparableRisk)
        {
            expectedDrawback = catalog.formulaPolicy.RequireDrawbackPolicy().Resolve(
                node.formulaBudget,
                new NarrativeFormulaDrawbackOption(
                    "equipment-burden:" + primary.capabilityId,
                    Math.Max(1, module.RiskWeight),
                    NarrativeFormulaDrawbackSelectionKind.Automatic,
                    true, true, false, false, node.drawbackEvidenceQualified));
        }
        else
        {
            expectedDrawback = NarrativeFormulaDrawbackResolution.None;
            if (node.burdenPotencyMultiplier != 0f)
                throw new InvalidOperationException(
                    "Equipment burden-free allocation has stray drawback potency.");
        }
        if (!string.Equals(node.drawbackId, expectedDrawback.DrawbackId, StringComparison.Ordinal)
            || node.drawbackCredit != expectedDrawback.AcceptedCredit)
            throw new InvalidOperationException("Equipment formula drawback credit changed after freezing.");
        string description = FormatMechanicalDescription(node, catalog);
        if (!string.Equals(description, node.mechanicalDescription, StringComparison.Ordinal))
            throw new InvalidOperationException("Equipment formula mechanical description changed after freezing.");
    }

    internal static void ValidateFormulaState(EquipmentEvolutionState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (state.generation < 0
            || float.IsNaN(state.mastery)
            || float.IsInfinity(state.mastery)
            || state.mastery < 0f)
            throw new InvalidOperationException("Equipment formula owner progression is invalid.");
        state.evolutionNodes ??= new List<EvolutionNode>();
        state.formulaEvidence ??= new List<EquipmentEvolutionFormulaEvidenceRecord>();
        state.presentationRequests ??= new List<EquipmentEvolutionPresentationRequest>();
        if (state.formulaEvidence.Any(value => value == null
                || !IsCanonical(value.evidenceId)
                || !IsCanonical(value.eventGroupKey)
                || !IsCanonical(value.actionKey)
                || !IsCanonical(value.domainKey)
                || value.relationshipKey == null
                || !string.Equals(value.relationshipKey, value.relationshipKey.Trim(), StringComparison.Ordinal)
                || value.originalEvent == null
                || !string.Equals(value.evidenceId, value.originalEvent.evidenceId, StringComparison.Ordinal)
                || !string.Equals(value.eventGroupKey, value.originalEvent.eventId, StringComparison.Ordinal)
                || !string.Equals(value.actionKey, value.originalEvent.eventId, StringComparison.Ordinal)
                || !string.Equals(value.domainKey, "equipment", StringComparison.Ordinal)
                || !IsCanonical(value.originalEvent.eventId)
                || float.IsNaN(value.originalEvent.amount)
                || float.IsInfinity(value.originalEvent.amount)
                || value.originalEvent.generation < 0
                || value.originalEvent.repeatCount < 1
                || value.originalEvent.sequence < 1L
                || value.attainedMilestoneCount < 0
                || value.influenceUseCount < 0
                || !NarrativeFormulaGuard.IsFiniteNonNegative(value.importancePoints))
            || state.formulaEvidence.Select(value => value.evidenceId)
                .Distinct(StringComparer.Ordinal).Count() != state.formulaEvidence.Count)
            throw new InvalidOperationException("Equipment formula evidence ledger is invalid.");
        if (state.presentationRequests.Any(value => value == null
                || !IsPresentationId(value.presentationId)
                || !IsCanonical(value.nodeId)
                || !IsCanonical(value.targetPersistentId)
                || !IsCanonical(value.historyHash)
                || value.attunementOwnerPersistentId == null
                || value.reforgeOrderId == null
                || !IsOptionalCanonical(value.attunementOwnerPersistentId)
                || !IsOptionalCanonical(value.reforgeOrderId)
                || string.IsNullOrEmpty(value.attunementOwnerPersistentId)
                    == string.IsNullOrEmpty(value.reforgeOrderId)
                || value.targetGeneration < 0
                || value.masteryCost < 0f
                || float.IsNaN(value.masteryCost)
                || float.IsInfinity(value.masteryCost)
                || value.evidenceIds == null
                || value.evidenceIds.Count == 0
                || value.evidenceIds.Any(id => !IsCanonical(id))
                || value.evidenceIds.Distinct(StringComparer.Ordinal).Count()
                    != value.evidenceIds.Count
                || !value.evidenceIds.SequenceEqual(
                    value.evidenceIds.OrderBy(id => id, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                || value.failureCount < 0 || value.failureCount > 5
                || value.state is not EquipmentEvolutionPresentationState.PresentationPending
                    and not EquipmentEvolutionPresentationState.ModuleSelectionPending
                    and not EquipmentEvolutionPresentationState.AwaitingNarrativeRetry
                || (value.state is EquipmentEvolutionPresentationState.PresentationPending
                    or EquipmentEvolutionPresentationState.ModuleSelectionPending)
                    && value.failureCount >= 5
                || value.state == EquipmentEvolutionPresentationState.AwaitingNarrativeRetry
                    && value.failureCount != 5)
            || state.presentationRequests.Select(value => value.presentationId)
                .Distinct(StringComparer.Ordinal).Count() != state.presentationRequests.Count)
            throw new InvalidOperationException("Equipment presentation request ledger is invalid.");
        if (state.evolutionNodes.Any(value => value != null && value.formulaVersion < 0))
            throw new InvalidOperationException("Equipment formula version cannot be negative.");
        EvolutionNode[] formulaNodes = state.evolutionNodes
            .Where(value => value != null && value.formulaVersion > 0)
            .ToArray();
        if (formulaNodes.Select(value => value.nodeId).Distinct(StringComparer.Ordinal).Count()
                != formulaNodes.Length
            || formulaNodes.Select(value => value.presentationId)
                .Distinct(StringComparer.Ordinal).Count() != formulaNodes.Length
            || formulaNodes.Any(value => value.generation < 0
                || value.formulaBudget < 0
                || value.calculatedCost > value.formulaBudget
                || !value.evidenceIds.SequenceEqual(
                    value.evidenceIds.OrderBy(id => id, StringComparer.Ordinal),
                    StringComparer.Ordinal)))
            throw new InvalidOperationException("Equipment formula node identity or budget is invalid.");
        if (state.presentationRequests.Any(request => state.evolutionNodes.All(node => node == null
                || node.formulaVersion <= 0
                || !string.Equals(node.nodeId, request.nodeId, StringComparison.Ordinal))))
            throw new InvalidOperationException("Equipment presentation request has no frozen node.");
        foreach (EvolutionNode node in formulaNodes)
        {
            EquipmentEvolutionFormulaCatalogSO catalog =
                EquipmentEvolutionFormulaCatalogSO.LoadForPersistedNode(node.formulaVersion);
            ValidateFormulaNode(node, catalog);
            if (!Enum.IsDefined(typeof(EquipmentEvolutionPresentationState), node.presentationState)
                || node.presentationState == EquipmentEvolutionPresentationState.Legacy
                || node.presentationFailureCount < 0
                || node.presentationFailureCount > 5)
                throw new InvalidOperationException("Equipment formula presentation lifecycle is invalid.");
            EquipmentEvolutionPresentationRequest request = state.presentationRequests
                .SingleOrDefault(value => string.Equals(
                    value.presentationId, node.presentationId, StringComparison.Ordinal));
            bool pending = node.presentationState is
                EquipmentEvolutionPresentationState.PresentationPending
                or EquipmentEvolutionPresentationState.ModuleSelectionPending
                or EquipmentEvolutionPresentationState.AwaitingNarrativeRetry;
            if (pending != (request != null)
                || request != null && (request.state != node.presentationState
                    || request.failureCount != node.presentationFailureCount
                    || node.historical != !string.IsNullOrEmpty(request.attunementOwnerPersistentId)
                    || node.generation != request.targetGeneration
                    || node.historical && request.masteryCost != 0f
                    || !node.historical && (request.masteryCost <= 0f
                        || request.targetGeneration != state.generation + 1
                        || state.mastery + 0.001f < request.masteryCost)
                    || !string.Equals(request.nodeId, node.nodeId, StringComparison.Ordinal)
                    || !request.evidenceIds.OrderBy(value => value, StringComparer.Ordinal)
                        .SequenceEqual(node.evidenceIds.OrderBy(value => value, StringComparer.Ordinal),
                            StringComparer.Ordinal)))
                throw new InvalidOperationException("Equipment formula node and presentation request diverged.");
            if (node.presentationState == EquipmentEvolutionPresentationState.Ready)
            {
                if (!node.active || !node.mechanicallyUnlocked || !node.narrativeReady
                    || !node.uiVisible || !node.playerVisible
                    || string.IsNullOrWhiteSpace(node.displayName)
                    || string.IsNullOrWhiteSpace(node.narrativeFlavor)
                    || !string.Equals(node.displayName, node.displayName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(node.narrativeFlavor, node.narrativeFlavor.Trim(), StringComparison.Ordinal)
                    || !string.Equals(node.description, node.narrativeFlavor, StringComparison.Ordinal)
                    || node.displayName.Length > 32
                    || node.narrativeFlavor.Length > 180
                    || ContainsMechanicalNumber(node.displayName)
                    || ContainsMechanicalNumber(node.narrativeFlavor))
                    throw new InvalidOperationException("Committed equipment formula presentation is incomplete.");
            }
            else if (node.active || node.mechanicallyUnlocked || node.narrativeReady
                     || node.uiVisible || node.playerVisible)
            {
                throw new InvalidOperationException("Pending equipment formula mechanics became visible or active.");
            }
        }
    }

    internal static string FormatMechanicalDescription(
        EvolutionNode node,
        EquipmentEvolutionFormulaCatalogSO catalog)
    {
        string benefits = string.Join(" / ", (node?.formulaCapabilities
                ?? new List<EquipmentEvolutionFormulaCapabilityEnvelope>())
            .OrderBy(value => value.capabilityId, StringComparer.Ordinal)
            .Select(envelope =>
            {
                EquipmentEvolutionFormulaCapabilityDefinition definition =
                    catalog.RequireCapability(envelope.capabilityId);
                NarrativeFormulaQuantizedRange range = definition.ToRuntime()
                    .RequireRange(NarrativeFormulaParameterIds.Magnitude);
                EquipmentEvolutionFormulaParameterEnvelope magnitude =
                    envelope.parameters.Single(value => string.Equals(
                        value.parameterId, NarrativeFormulaParameterIds.Magnitude,
                        StringComparison.Ordinal));
                decimal percent = range.ToDecimal(magnitude.units) * 100m;
                string verb = definition.modifierKind ==
                    EquipmentEvolutionFormulaModifierKind.MultiplierReduction
                    ? "감소" : "증가";
                return $"{definition.displayName}: {definition.statId} {percent:0.##}% {verb}";
            }));
        EvolutionModuleDefinition primary = node?.formulaCapabilities?.Count > 0
            ? RequireEvolutionModule(node.formulaCapabilities[0].capabilityId)
            : null;
        string paired = primary?.BurdenKind switch
        {
            EvolutionModuleBurdenKind.OperatingCost => " / 운영비: "
                + FormatModifiers(primary.Burdens, 1f),
            EvolutionModuleBurdenKind.InseparableRisk => " / 불가분 위험: "
                + FormatModifiers(primary.Burdens, 1f),
            _ => " / 추가 부담 없음"
        };
        string drawback = string.IsNullOrEmpty(node?.burdenEffectId)
            ? string.Empty
            : " / 선택 단점: " + RequireEvolutionModule(node.burdenEffectId).DisplayName
                + " (강도 "
                + node.burdenPotencyMultiplier.ToString("0.##", CultureInfo.InvariantCulture)
                + ", 예산 +" + node.drawbackCredit.ToString(CultureInfo.InvariantCulture)
                + ")";
        return benefits + paired + drawback + " / 비용: "
            + node.positiveCost.ToString(CultureInfo.InvariantCulture) + " - "
            + node.drawbackCredit.ToString(CultureInfo.InvariantCulture) + " = "
            + node.calculatedCost.ToString(CultureInfo.InvariantCulture);
    }

    private static EvolutionModuleDefinition RequireEvolutionModule(string moduleId)
    {
        if (EvolutionModules.TryGet(moduleId, out EvolutionModuleDefinition module))
            return module;
        throw new KeyNotFoundException(
            $"Unknown equipment evolution module '{moduleId ?? string.Empty}'.");
    }

    private static bool IsRuntimeApplicable(
        EvolutionModuleDefinition module,
        EquipmentFormulaTargetContext targetContext)
    {
        if (module == null || targetContext == null)
        {
            return false;
        }

        if (module.IsPositiveModule
            && (module.Benefits.Count == 0
                || module.Benefits.Any(effect =>
                    !HasRuntimeConsumer(effect, targetContext))))
        {
            return false;
        }

        return module.BurdenKind is not (
                EvolutionModuleBurdenKind.OperatingCost
                or EvolutionModuleBurdenKind.InseparableRisk
                or EvolutionModuleBurdenKind.OptionalDrawback)
            || module.Burdens.All(effect => HasRuntimeConsumer(effect, targetContext));
    }

    // This is deliberately a stat/kind table rather than a module allow-list:
    // the runtime, not a module ID, decides whether an applied effect has a
    // consumer on the concrete equipment kind.
    private static bool HasRuntimeConsumer(
        EvolutionEffectModifier effect,
        EquipmentFormulaTargetContext targetContext)
    {
        string statId = effect?.statId?.Trim() ?? string.Empty;
        return statId switch
        {
            "combat.damage" or "combat.accuracy" or "combat.penetration"
                => targetContext.Kind is CombatEquipmentKind.MeleeWeapon
                    or CombatEquipmentKind.RangedWeapon
                    or CombatEquipmentKind.RecoverableThrowingWeapon,
            "combat.reload" => targetContext.Kind == CombatEquipmentKind.RangedWeapon,
            "combat.defense" or "combat.durability"
                => targetContext.Kind is CombatEquipmentKind.Armor
                    or CombatEquipmentKind.Shield,
            "combat.value" => true,
            _ => false
        };
    }

    private static IReadOnlyList<string> GetCatalogForbiddenSynergies(
        EvolutionModuleDefinition module,
        EquipmentEvolutionFormulaCatalogSO catalog)
    {
        if (module == null || catalog == null)
            throw new ArgumentNullException(module == null ? nameof(module) : nameof(catalog));
        HashSet<string> catalogModuleIds = catalog.RequireCatalogModuleIds()
            .ToHashSet(StringComparer.Ordinal);
        return module.ForbiddenSynergyModuleIds
            .Where(catalogModuleIds.Contains)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string DescribeModule(
        EvolutionModuleDefinition module,
        EquipmentEvolutionFormulaCatalogSO catalog)
    {
        string benefits = DescribeModifierSemantics(module.Benefits, beneficial: true);
        string burdens = DescribeModifierSemantics(module.Burdens, beneficial: false);
        string description = module.BurdenKind switch
        {
            EvolutionModuleBurdenKind.None =>
                module.DisplayName + ": " + benefits + "; 추가 부담 없음",
            EvolutionModuleBurdenKind.OperatingCost =>
                module.DisplayName + ": " + benefits + "; 운영비/요구량 부담 " + burdens
                    + " (예산 보너스 없음)",
            EvolutionModuleBurdenKind.InseparableRisk =>
                module.DisplayName + ": " + benefits + "; 효과와 분리할 수 없는 위험 "
                    + burdens,
            EvolutionModuleBurdenKind.OptionalDrawback =>
                module.DisplayName + ": 선택 시 " + burdens
                    + "; 부정 원장 근거가 있는 경우에만 예산 보너스",
            _ => throw new InvalidOperationException("Unknown evolution burden kind.")
        };
        IReadOnlyList<string> forbiddenSynergies = GetCatalogForbiddenSynergies(module, catalog);
        if (ContainsMechanicalNumber(description))
            throw new InvalidOperationException(
                "Equipment module-selection semantics must not expose numeric mechanics.");
        return forbiddenSynergies.Count == 0
            ? description
            : description + "; 함께 선택 금지 이로운 기능="
                + string.Join(",", forbiddenSynergies);
    }

    private static string DescribeLegacyModule(
        EvolutionModuleDefinition module,
        EquipmentEvolutionFormulaCatalogSO catalog)
    {
        if (module == null || catalog == null)
            throw new ArgumentNullException(module == null ? nameof(module) : nameof(catalog));
        string description = module.BurdenKind switch
        {
            EvolutionModuleBurdenKind.None =>
                module.DisplayName + ": " + FormatModifiers(module.Benefits, 1f)
                    + "; 추가 부담 없음",
            EvolutionModuleBurdenKind.OperatingCost =>
                module.DisplayName + ": " + FormatModifiers(module.Benefits, 1f)
                    + "; 운영비/요구량 " + FormatModifiers(module.Burdens, 1f)
                    + " (예산 보너스 없음)",
            EvolutionModuleBurdenKind.InseparableRisk =>
                module.DisplayName + ": " + FormatModifiers(module.Benefits, 1f)
                    + "; 효과와 분리할 수 없는 위험 "
                    + FormatModifiers(module.Burdens, 1f),
            EvolutionModuleBurdenKind.OptionalDrawback =>
                module.DisplayName + ": 선택 시 " + FormatModifiers(module.Burdens, 1f)
                    + "; 부정 원장 근거가 있는 경우에만 예산 보너스",
            _ => throw new InvalidOperationException("Unknown evolution burden kind.")
        };
        IReadOnlyList<string> forbiddenSynergies =
            GetCatalogForbiddenSynergies(module, catalog);
        return forbiddenSynergies.Count == 0
            ? description
            : description + "; 함께 선택 금지 이로운 기능="
                + string.Join(",", forbiddenSynergies);
    }

    private static string DescribeModifierSemantics(
        IReadOnlyList<EvolutionEffectModifier> modifiers,
        bool beneficial)
    {
        EvolutionEffectModifier[] values = (modifiers
                ?? Array.Empty<EvolutionEffectModifier>())
            .Where(value => value != null)
            .OrderBy(value => value.statId, StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0)
            return beneficial ? "이로운 변화" : "불리한 변화";
        string suffix = beneficial ? " 강화" : " 부담";
        return string.Join(", ", values.Select(value =>
            EquipmentStatSemanticLabel(value.statId) + suffix));
    }

    private static string EquipmentStatSemanticLabel(string statId) => statId switch
    {
        "combat.damage" => "공격력",
        "combat.accuracy" => "명중 성능",
        "combat.penetration" => "관통 성능",
        "combat.reload" => "재정비 속도",
        "combat.defense" => "방어 성능",
        "combat.durability" => "내구",
        "combat.value" => "장비 가치",
        _ => throw new InvalidOperationException(
            "Equipment module-selection semantics have no label for stat '"
            + (statId ?? string.Empty) + "'.")
    };

    private static string FormatModifiers(
        IReadOnlyList<EvolutionEffectModifier> modifiers,
        float potency) => string.Join(", ", (modifiers
            ?? Array.Empty<EvolutionEffectModifier>()).Select(value =>
            value.statId + (value.additive != 0f
                ? " " + (value.additive * potency).ToString("+0.####;-0.####;0",
                    CultureInfo.InvariantCulture)
                : " ×" + (1f + (value.multiplier - 1f) * potency)
                    .ToString("0.####", CultureInfo.InvariantCulture))));

    private static float ResolveMagnitude(
        NarrativeFormulaCandidate candidate,
        NarrativeFormulaCapabilityDescriptor descriptor)
    {
        NarrativeFormulaParameterValue magnitude = candidate.Allocations.Single()
            .Parameters.Single(value => string.Equals(
                value.ParameterId, NarrativeFormulaParameterIds.Magnitude,
                StringComparison.Ordinal));
        return (float)descriptor.RequireRange(NarrativeFormulaParameterIds.Magnitude)
            .ToDecimal(magnitude.Units);
    }

    private static string RequireCanonical(string value, string label)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new InvalidOperationException($"{label} must be canonical.");
        return canonical;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsOptionalCanonical(string value) =>
        value != null && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsPresentationId(string value)
    {
        const string prefix = "presentation:equipment:";
        if (!IsCanonical(value) || !value.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        string suffix = value.Substring(prefix.Length);
        return suffix.Length == 64
            && string.Equals(suffix, suffix.ToLowerInvariant(), StringComparison.Ordinal)
            && suffix.All(Uri.IsHexDigit);
    }

    private static bool ContainsMechanicalNumber(string value) =>
        (value ?? string.Empty).Any(character =>
            char.IsDigit(character) || character is '%' or '％');

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

}

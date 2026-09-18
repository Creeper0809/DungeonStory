using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class CharacterSkillFormulaGeneration
{
    public const int ModuleSelectionFormulaVersion = 2;
    public const int DrawbackModuleSelectionFormulaVersion = 3;
    public const int RuntimeStatDrawbackFormulaVersion = 4;
    public const int MathematicalScalingFormulaVersion = 5;
    public const int EffectiveBoundsFormulaVersion = 6;
    private sealed class FrozenOption
    {
        public CharacterSkillCandidateRule Rule;
        public IReadOnlyList<CharacterSkillModuleRule> Modules;
        public NarrativeFormulaCandidate Formula;
        public int DrawbackCooldownTurns;
        public int DrawbackCooldownDays;
        public CharacterSkillDrawbackEffectEnvelope DrawbackEffect;
        public bool DrawbackEvidenceQualified;
    }

    private sealed class ResolvedSelectedDrawback
    {
        public NarrativeFormulaDrawbackOption Option;
        public int CooldownTurns;
        public int CooldownDays;
        public CharacterSkillDrawbackEffectEnvelope Effect;
    }

    public static string BuildEvidenceId(CharacterNarrativeFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        return "skill-evidence:" + NarrativeInferenceHash.ComputeSha256Utf8(string.Join("\n",
            fact.domain.ToString(),
            fact.factId?.Trim() ?? string.Empty,
            fact.subjectId?.Trim() ?? string.Empty));
    }

    internal static int CalculateNarrativeBudget(
        CharacterProgression progression,
        CharacterSkillSystemSettingsSO settings,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeEvidence = null)
    {
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        NarrativeFormulaEvidence[] evidence = BuildEvidence(
            progression.NarrativeLedger, outcomeEvidence, policy);
        if (evidence.Length == 0)
            throw new InvalidOperationException(
                "New CharacterSkill formula generation requires at least one meaningful narrative evidence fact.");
        return NarrativeFormulaCore.CalculateStrength(
            policy,
            evidence).Budget;
    }

    public static void InitializeModuleSelection(
        CharacterProgression progression,
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeEvidence = null)
    {
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        if (draft == null) throw new ArgumentNullException(nameof(draft));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        if (policy.FormulaVersion < ModuleSelectionFormulaVersion)
            throw new InvalidOperationException("The authored formula policy does not enable module selection.");
        CharacterNarrativeFact[] facts = MeaningfulFacts(progression.NarrativeLedger);
        draft.outcomeEvidenceBindings = (outcomeEvidence
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToList();
        NarrativeFormulaEvidence[] evidence = BuildEvidence(
            progression.NarrativeLedger,
            draft.outcomeEvidenceBindings,
            policy);
        if (evidence.Length == 0)
            throw new InvalidOperationException("Module selection requires narrative evidence.");
        NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(policy, evidence);
        draft.formulaVersion = policy.FormulaVersion;
        draft.formulaCatalogSha256 = settings.formulaPolicy.RequireCatalogSha256();
        draft.formulaBudget = strength.Budget;
        draft.presentationFailureCount = 0;
        draft.nextPresentationIndex = 0;
        draft.candidates = new List<CharacterSkillInstance>();
        draft.frozenMechanics = new List<CharacterSkillInstance>();
        draft.moduleSelectionOffers = new List<CharacterSkillModuleOfferState>();
        string[] evidenceIds = evidence.Select(value => value.EvidenceId)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        CharacterNarrativeFact[] negativeFacts = facts.Where(value =>
                NarrativeFormulaNegativeEvidence.Matches(value.outcome, value.factId))
            .ToArray();
        HashSet<string> negativeEvidenceIds = negativeFacts.Select(BuildEvidenceId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in
                 draft.outcomeEvidenceBindings.Where(
                     GameplayOutcomeEvidenceFormulaProjection.IsNegative))
            negativeEvidenceIds.Add(binding.publicFactId);
        foreach (CharacterSkillCandidateRule rule in draft.rules ?? new List<CharacterSkillCandidateRule>())
        {
            if (rule == null) throw new InvalidOperationException("Formula skill draft has a null rule.");
            rule.budget = strength.Budget;
            rule.allowedVariantIds = new List<string>();
            string[] positiveIds = RequireLegalModules(draft, rule, settings)
                .Select(value => value.id).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (positiveIds.Length == 0)
                throw new InvalidOperationException($"Rule '{rule.ruleId}' has no individually legal formula modules.");
            string[] drawbackIds = policy.FormulaVersion >= DrawbackModuleSelectionFormulaVersion
                ? RequireLegalDrawbacks(draft, rule, settings, negativeFacts, strength.Budget)
                    .Select(value => value.DrawbackId)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            string[] qualifiedNegativeIds = negativeFacts
                .Where(fact => drawbackIds.Any(drawbackId =>
                    settings.FindDrawback(drawbackId)?.domainAffinities.Contains(fact.domain) == true))
                .Select(BuildEvidenceId)
                .Where(negativeEvidenceIds.Contains)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            qualifiedNegativeIds = qualifiedNegativeIds
                .Concat(draft.outcomeEvidenceBindings
                    .Where(GameplayOutcomeEvidenceFormulaProjection.IsNegative)
                    .Select(value => value.publicFactId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string selectionHash = NarrativeInferenceHash.ComputeSha256Utf8(string.Join("\n",
                draft.requestKey, rule.ruleId, policy.FormulaVersion.ToString(CultureInfo.InvariantCulture),
                draft.formulaCatalogSha256, string.Join(",", positiveIds),
                string.Join(",", drawbackIds), string.Join(",", evidenceIds),
                string.Join(",", qualifiedNegativeIds)))
                .Substring("sha256:".Length);
            draft.moduleSelectionOffers.Add(new CharacterSkillModuleOfferState
            {
                selectionId = "selection:skill:" + selectionHash,
                ruleId = rule.ruleId,
                positiveModuleIds = positiveIds.ToList(),
                drawbackModuleIds = drawbackIds.ToList(),
                evidenceFactIds = evidenceIds.ToList(),
                qualifiedNegativeEvidenceFactIds = qualifiedNegativeIds.ToList(),
                maximumPositiveModules = Math.Min(3, positiveIds.Length),
                maximumDrawbackModules = drawbackIds.Length > 0 ? 1 : 0
            });
        }
        int required = draft.kind == CharacterSkillKind.Active ? 3 : 1;
        if (draft.moduleSelectionOffers.Count != required)
            throw new InvalidOperationException($"CharacterSkill requires exactly {required} selection offers.");
        draft.presentationState = CharacterSkillPresentationState.PresentationPending;
    }

    public static NarrativeFormulaModuleSelectionRequest BuildModuleSelectionRequest(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings)
    {
        if (draft == null || settings == null) throw new ArgumentNullException(nameof(draft));
        if (draft.nextPresentationIndex < 0
            || draft.nextPresentationIndex >= (draft.moduleSelectionOffers?.Count ?? 0)
            || draft.nextPresentationIndex >= (draft.rules?.Count ?? 0))
            throw new InvalidOperationException("CharacterSkill has no pending module-selection offer.");
        CharacterSkillModuleOfferState state = draft.moduleSelectionOffers[draft.nextPresentationIndex];
        CharacterSkillCandidateRule rule = draft.rules[draft.nextPresentationIndex];
        Dictionary<string, CharacterSkillModuleRule> legal = RequireLegalModules(draft, rule, settings)
            .ToDictionary(value => value.id, StringComparer.Ordinal);
        List<NarrativeFormulaModuleOffer> offers = new();
        foreach (string moduleId in state.positiveModuleIds)
        {
            if (!legal.TryGetValue(moduleId, out CharacterSkillModuleRule module))
                throw new InvalidOperationException($"Stored offered module '{moduleId}' is no longer legal.");
            NarrativeFormulaCapabilityDescriptor descriptor = settings.RequireFormulaDescriptor(module);
            offers.Add(new NarrativeFormulaModuleOffer(
                module.id,
                NarrativeFormulaModulePolarity.Positive,
                module.displayName + ": "
                    + CharacterSkillPresentationSemantics.DescribeCapability(
                        descriptor),
                new NarrativeFormulaCapabilityInput(descriptor, descriptor.NarrativeAffinity, 1d),
                0));
        }
        foreach (string drawbackId in state.drawbackModuleIds ?? new List<string>())
        {
            CharacterSkillDrawbackCapabilityDefinition drawback =
                settings.FindDrawback(drawbackId)
                ?? throw new InvalidOperationException(
                    $"Stored offered drawback '{drawbackId}' is no longer authored.");
            if (!drawback.AppliesTo(draft.kind, rule.trigger))
            {
                throw new InvalidOperationException(
                    $"Stored offered drawback '{drawbackId}' is no longer legal for the rule.");
            }
            NarrativeFormulaCapabilityDescriptor descriptor =
                drawback.RequireFormulaDescriptor();
            string forbidden = descriptor.ForbiddenSynergies.Count == 0
                ? string.Empty
                : "; 함께 선택 금지 이로운 기능="
                    + string.Join(",", descriptor.ForbiddenSynergies);
            offers.Add(new NarrativeFormulaModuleOffer(
                drawback.DrawbackId,
                NarrativeFormulaModulePolarity.Drawback,
                drawback.DisplayName + ": " + drawback.Description + forbidden,
                new NarrativeFormulaCapabilityInput(
                    descriptor, descriptor.NarrativeAffinity, 1d),
                drawback.MaximumCredit));
        }
        return new NarrativeFormulaModuleSelectionRequest(
            state.selectionId, offers, state.evidenceFactIds,
            state.maximumPositiveModules, state.maximumDrawbackModules);
    }

    public static CharacterSkillInstance ResolveModuleSelection(
        CharacterSkillDraft draft,
        NarrativeFormulaModuleSelectionChoice choice,
        CharacterSkillSystemSettingsSO settings)
    {
        NarrativeFormulaModuleSelectionRequest request = BuildModuleSelectionRequest(draft, settings);
        if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                request, choice, out NarrativeFormulaValidatedModuleSelection selected, out string error))
            throw new InvalidOperationException(error);
        string primaryModuleId = choice.PositiveModuleIds[0];
        if (draft.kind == CharacterSkillKind.Active
            && (draft.candidates ?? new List<CharacterSkillInstance>()).Any(value => value != null
                && string.Equals(value.combinationId, primaryModuleId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Active skill candidates require distinct primary modules.");
        CharacterSkillCandidateRule rule = draft.rules[draft.nextPresentationIndex];
        NarrativeFormulaGenerationCostContext authored =
            settings.formulaPolicy.RequireGenerationCostContext(rule.trigger, rule.target);
        NarrativeFormulaGenerationCostContext context = new(
            authored.TriggerFrequencyUnits, authored.GuaranteedProc,
            CharacterSkillAreaRules.ResolveCostTargetCount(rule.effectArea, rule.areaSize, authored.TargetCount));
        ResolvedSelectedDrawback drawback = ResolveSelectedDrawback(
            draft, state: draft.moduleSelectionOffers[draft.nextPresentationIndex],
            selected, settings);
        IReadOnlyList<NarrativeFormulaCandidate> candidates = NarrativeFormulaCore.OptimizeExactComposition(
            selected.PositiveModules.Select(value => value.Capability), context, draft.formulaBudget,
            selected.EvidenceFactIds, maximumResults: 3,
            drawbackPolicy: drawback?.Option == null
                ? null
                : settings.formulaPolicy.RequireDrawbackPolicy(),
            drawbackOption: drawback?.Option);
        if (candidates.Count == 0)
            throw new InvalidOperationException("The selected module set has no legal numeric allocation within budget.");
        string ownerId = draft.requestKey?.Trim() ?? string.Empty;
        if (ownerId.Length == 0) throw new InvalidOperationException("Formula skill selection requires a stable request key.");
        NarrativeFormulaCandidate winner = NarrativeFormulaCore.ChooseDeterministicWinner(
            NarrativeFormulaCore.RetainNearBest(candidates, draft.formulaBudget), ownerId,
            $"{draft.kind}:{draft.unlockLevel}:{draft.nextPresentationIndex}",
            draft.formulaVersion, draft.formulaCatalogSha256);
        Dictionary<string, CharacterSkillModuleRule> byCapability = selected.PositiveModules
            .ToDictionary(value => value.Capability.Descriptor.CapabilityId,
                value => settings.FindModule(value.ModuleId), StringComparer.Ordinal);
        FrozenOption option = new FrozenOption
        {
            Rule = rule,
            Formula = winner,
            Modules = winner.Allocations.Select(value => byCapability[value.Descriptor.CapabilityId]).ToArray(),
            DrawbackCooldownTurns = drawback?.CooldownTurns ?? 0,
            DrawbackCooldownDays = drawback?.CooldownDays ?? 0,
            DrawbackEffect = drawback?.Effect?.Clone(),
            DrawbackEvidenceQualified = drawback != null
        };
        CharacterSkillInstance skill = CreateFrozenSkill(
            draft, option, draft.nextPresentationIndex, draft.formulaCatalogSha256, draft.formulaVersion);
        skill.combinationId = primaryModuleId;
        skill.evidenceBindings = GameplayOutcomeEvidenceFormulaProjection.Select(
            draft.outcomeEvidenceBindings,
            skill.evidenceIds);
        return skill;
    }

    private static CharacterSkillModuleRule[] RequireLegalModules(
        CharacterSkillDraft draft,
        CharacterSkillCandidateRule rule,
        CharacterSkillSystemSettingsSO settings)
    {
        return (rule.allowedModuleIds ?? new List<string>()).Select(settings.FindModule)
            .Where(module => module != null
                && module.Allows(draft.kind, rule.trigger, rule.target)
                && CharacterSkillValidation.IsTargetCompatible(module, rule.target)
                && !CharacterSkillValidation.WouldSelfTrigger(module, rule.trigger)
                && CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                    draft.kind,
                    rule,
                    module)
                && (!(module is CharacterManagementSkillModuleRule)
                    || CharacterSkillRuntimeEffects.IsManagementModuleReachable(draft.kind, rule.trigger, module))
                && (draft.kind != CharacterSkillKind.Ultimate
                    || (rule.ultimateDomain == CharacterUltimateDomain.Management)
                        == (module is CharacterManagementSkillModuleRule)))
            .Distinct().ToArray();
    }

    private static CharacterSkillDrawbackCapabilityDefinition[] RequireLegalDrawbacks(
        CharacterSkillDraft draft,
        CharacterSkillCandidateRule rule,
        CharacterSkillSystemSettingsSO settings,
        IReadOnlyCollection<CharacterNarrativeFact> negativeFacts,
        int budget)
    {
        if (negativeFacts == null || negativeFacts.Count == 0)
            return Array.Empty<CharacterSkillDrawbackCapabilityDefinition>();
        NarrativeFormulaDrawbackSelectionKind selectionKind =
            draft.kind == CharacterSkillKind.Active
                ? NarrativeFormulaDrawbackSelectionKind.PlayerChoice
                : NarrativeFormulaDrawbackSelectionKind.Automatic;
        NarrativeFormulaDrawbackCreditPolicy policy =
            settings.formulaPolicy.RequireDrawbackPolicy();
        return settings.RequireDrawbackCatalog().Where(value =>
            value.Allows(draft.kind, rule.trigger, negativeFacts)
            && policy.Resolve(budget, new NarrativeFormulaDrawbackOption(
                value.DrawbackId,
                value.MaximumCredit,
                selectionKind,
                reachable: true,
                mandatory: true,
                separatelyRemovable: false,
                cancelsSelectedBenefit: false,
                hasNegativeNarrativeEvidence: true)).AcceptedCredit > 0)
            .ToArray();
    }

    private static ResolvedSelectedDrawback ResolveSelectedDrawback(
        CharacterSkillDraft draft,
        CharacterSkillModuleOfferState state,
        NarrativeFormulaValidatedModuleSelection selected,
        CharacterSkillSystemSettingsSO settings)
    {
        if (selected.DrawbackModules.Count == 0) return null;
        if (selected.DrawbackModules.Count != 1)
            throw new InvalidOperationException(
                "CharacterSkill supports at most one selected drawback.");
        HashSet<string> qualified = (state.qualifiedNegativeEvidenceFactIds
                ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
        if (!selected.EvidenceFactIds.Any(qualified.Contains))
            throw new InvalidOperationException(
                "A selected CharacterSkill drawback must cite a qualified negative evidence fact.");
        CharacterSkillDrawbackCapabilityDefinition definition =
            settings.FindDrawback(selected.DrawbackModules[0].ModuleId)
            ?? throw new InvalidOperationException(
                "The selected CharacterSkill drawback is no longer authored.");
        NarrativeFormulaCapabilityDescriptor drawbackDescriptor =
            definition.RequireFormulaDescriptor();
        NarrativeFormulaCapabilityDescriptor conflictingPositive =
            selected.PositiveModules.Select(value => value.Capability.Descriptor)
                .FirstOrDefault(value =>
                    drawbackDescriptor.ForbiddenSynergies.Contains(
                        value.CapabilityId, StringComparer.Ordinal)
                    || value.ForbiddenSynergies.Contains(
                        drawbackDescriptor.CapabilityId, StringComparer.Ordinal));
        if (conflictingPositive != null)
            throw new InvalidOperationException(
                $"CharacterSkill benefit '{conflictingPositive.CapabilityId}' and drawback "
                + $"'{definition.DrawbackId}' modify the same outcome axis.");
        NarrativeFormulaDrawbackSelectionKind selectionKind =
            draft.kind == CharacterSkillKind.Active
                ? NarrativeFormulaDrawbackSelectionKind.PlayerChoice
                : NarrativeFormulaDrawbackSelectionKind.Automatic;
        NarrativeFormulaDrawbackCreditPolicy policy =
            settings.formulaPolicy.RequireDrawbackPolicy();
        int severityBudget = policy.Resolve(
            draft.formulaBudget,
            new NarrativeFormulaDrawbackOption(
                definition.DrawbackId,
                definition.MaximumCredit,
                selectionKind,
                reachable: true,
                mandatory: true,
                separatelyRemovable: false,
                cancelsSelectedBenefit: false,
                hasNegativeNarrativeEvidence: true)).AcceptedCredit;
        if (severityBudget <= 0)
            throw new InvalidOperationException(
                "The selected CharacterSkill drawback cannot earn credit at this narrative budget.");
        NarrativeFormulaCandidate severity = NarrativeFormulaCore.OptimizeExactComposition(
                selected.DrawbackModules.Select(value => value.Capability),
                new NarrativeFormulaGenerationCostContext(0, false, 1),
                Math.Min(severityBudget, definition.MaximumCredit),
                selected.EvidenceFactIds,
                maximumResults: 1)
            .SingleOrDefault();
        if (severity == null || severity.PositiveCost <= 0)
            throw new InvalidOperationException(
                "The selected CharacterSkill drawback has no legal severity allocation.");
        NarrativeFormulaCapabilityAllocation allocation = severity.Allocations.Single();
        long units = allocation.Parameters.Single(value => string.Equals(
            value.ParameterId, NarrativeFormulaParameterIds.Magnitude,
            StringComparison.Ordinal)).Units;
        if (units < 1 || units > definition.MaximumCredit)
            throw new InvalidOperationException(
                "The selected CharacterSkill drawback severity is out of range.");
        int amount = checked((int)units);
        return new ResolvedSelectedDrawback
        {
            Option = new NarrativeFormulaDrawbackOption(
                definition.BuildResolvedDrawbackId(amount),
                severity.PositiveCost,
                selectionKind,
                reachable: true,
                mandatory: true,
                separatelyRemovable: false,
                cancelsSelectedBenefit: false,
                hasNegativeNarrativeEvidence: true),
            CooldownTurns = definition.applicationKind
                == CharacterSkillDrawbackApplicationKind.CombatCooldownTurns ? amount : 0,
            CooldownDays = definition.applicationKind
                == CharacterSkillDrawbackApplicationKind.WorkCooldownDays ? amount : 0,
            Effect = definition.applicationKind
                == CharacterSkillDrawbackApplicationKind.EquippedStat
                ? new CharacterSkillDrawbackEffectEnvelope
                {
                    drawbackModuleId = definition.DrawbackId,
                    effectId = definition.effectDefinition.EffectId,
                    targetId = definition.effectDefinition.TargetId,
                    operation = definition.effectDefinition.Operation,
                    value = definition.BuildResolvedStatValue(amount),
                    severityUnits = amount,
                    displayName = definition.DisplayName
                }
                : null
        };
    }

    public static List<CharacterSkillInstance> FreezeMechanics(
        CharacterProgression progression,
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings)
    {
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        if (draft == null) throw new ArgumentNullException(nameof(draft));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        string ownerId = progression.Actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (ownerId.Length == 0)
            throw new InvalidOperationException("Formula skill generation requires a persistent owner ID.");

        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        string catalogSha256 = settings.formulaPolicy.RequireCatalogSha256();
        NarrativeFormulaEvidence[] evidence = BuildEvidence(progression.NarrativeLedger);
        if (evidence.Length == 0)
            throw new InvalidOperationException(
                "New CharacterSkill formula generation requires at least one meaningful narrative evidence fact.");
        NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(policy, evidence);
        bool hasNegativeEvidence = (progression.NarrativeLedger?.Facts
            ?? Array.Empty<CharacterNarrativeFact>()).Any(value => value != null
                && NarrativeFormulaNegativeEvidence.Matches(value.outcome, value.factId));
        string[] evidenceIds = evidence.Select(value => value.EvidenceId).ToArray();
        HashSet<string> existingCapabilities = CollectCommittedCapabilities(progression, settings);

        draft.formulaVersion = policy.FormulaVersion;
        draft.formulaCatalogSha256 = catalogSha256;
        draft.formulaBudget = strength.Budget;
        draft.presentationFailureCount = 0;
        draft.nextPresentationIndex = 0;
        draft.candidates = new List<CharacterSkillInstance>();

        List<FrozenOption> options = new List<FrozenOption>();
        CharacterSkillCandidateRule[] rules = (draft.rules ?? new List<CharacterSkillCandidateRule>()).ToArray();
        for (int ruleIndex = 0; ruleIndex < rules.Length; ruleIndex++)
        {
            CharacterSkillCandidateRule rule = rules[ruleIndex];
            if (rule == null) throw new InvalidOperationException("Formula skill draft has a null mechanical rule.");
            rule.budget = strength.Budget;
            rule.allowedVariantIds = new List<string>();
            CharacterSkillModuleRule[] legalModules = (rule.allowedModuleIds ?? new List<string>())
                .Select(settings.FindModule)
                .Where(module => module != null
                    && module.Allows(draft.kind, rule.trigger, rule.target)
                    && CharacterSkillValidation.IsTargetCompatible(module, rule.target)
                    && !CharacterSkillValidation.WouldSelfTrigger(module, rule.trigger)
                    && CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                        draft.kind,
                        rule,
                        module)
                    && (!(module is CharacterManagementSkillModuleRule)
                        || CharacterSkillRuntimeEffects.IsManagementModuleReachable(draft.kind, rule.trigger, module))
                    && (draft.kind != CharacterSkillKind.Ultimate
                        || (rule.ultimateDomain == CharacterUltimateDomain.Management)
                            == (module is CharacterManagementSkillModuleRule)))
                .Distinct()
                .ToArray();
            if (legalModules.Length == 0) continue;
            Dictionary<string, CharacterSkillModuleRule> modulesByCapability = legalModules.ToDictionary(
                module => settings.RequireFormulaDescriptor(module).CapabilityId,
                StringComparer.Ordinal);
            NarrativeFormulaCapabilityInput[] inputs = legalModules.Select(module =>
            {
                NarrativeFormulaCapabilityDescriptor descriptor = settings.RequireFormulaDescriptor(module);
                return new NarrativeFormulaCapabilityInput(
                    descriptor,
                    descriptor.NarrativeAffinity + CountAffinityMatches(descriptor, evidence),
                    existingCapabilities.Contains(descriptor.CapabilityId) ? 0d : 1d);
            }).ToArray();
            CharacterSkillAreaRules.RequireValid(
                rule.targetingMode, rule.effectArea, rule.areaSize);
            NarrativeFormulaGenerationCostContext authoredContext =
                settings.formulaPolicy.RequireGenerationCostContext(rule.trigger, rule.target);
            NarrativeFormulaGenerationCostContext context = new(
                authoredContext.TriggerFrequencyUnits,
                authoredContext.GuaranteedProc,
                CharacterSkillAreaRules.ResolveCostTargetCount(
                    rule.effectArea,
                    rule.areaSize,
                    authoredContext.TargetCount));
            NarrativeFormulaDrawbackOption drawback = BuildDrawbackOption(
                draft.kind, rule, ruleIndex, hasNegativeEvidence,
                out int cooldownTurns, out int cooldownDays);
            foreach (CharacterSkillModuleRule primaryModule in legalModules)
            {
                string primaryCapability = settings.RequireFormulaDescriptor(primaryModule).CapabilityId;
                foreach (NarrativeFormulaCandidate candidate in NarrativeFormulaCore.OptimizeCompositions(
                    inputs,
                    context,
                    strength.Budget,
                    primaryCapability,
                    evidenceIds,
                    maximumCapabilityCount: 3,
                    maximumResults: 3,
                    drawbackPolicy: drawback == null
                        ? null : settings.formulaPolicy.RequireDrawbackPolicy(),
                    drawbackOption: drawback))
                {
                    options.Add(new FrozenOption
                    {
                        Rule = rule,
                        Modules = candidate.Allocations.Select(allocation =>
                            modulesByCapability[allocation.Descriptor.CapabilityId]).ToArray(),
                        Formula = candidate,
                        DrawbackCooldownTurns = cooldownTurns,
                        DrawbackCooldownDays = cooldownDays,
                        DrawbackEvidenceQualified = hasNegativeEvidence
                    });
                }
            }
        }

        if (options.Count == 0)
            throw new InvalidOperationException("No registered CharacterSkill formula capability produced a legal maximal state.");

        List<FrozenOption> selected;
        if (draft.kind == CharacterSkillKind.Active)
        {
            selected = SelectDistinctActive(options, draft.rules, strength.Budget);
        }
        else
        {
            IReadOnlyList<NarrativeFormulaCandidate> ranked = NarrativeFormulaCore.RetainNearBest(
                options.Select(value => value.Formula), strength.Budget);
            NarrativeFormulaCandidate winner = NarrativeFormulaCore.ChooseDeterministicWinner(
                ranked,
                ownerId,
                $"{draft.kind}:{draft.unlockLevel}",
                policy.FormulaVersion,
                catalogSha256);
            selected = new List<FrozenOption>
            {
                options.Single(value => ReferenceEquals(value.Formula, winner))
            };
        }

        List<CharacterSkillInstance> frozen = selected.Select((option, index) =>
            CreateFrozenSkill(draft, option, index, catalogSha256, policy.FormulaVersion)).ToList();
        if (draft.kind == CharacterSkillKind.Active
            && (frozen.Count != 3
                || frozen.Select(value => value.formulaCapabilities[0].capabilityId)
                    .Distinct(StringComparer.Ordinal).Count() != 3))
            throw new InvalidOperationException("Active formula generation must freeze exactly three distinct core effects.");
        if (draft.kind != CharacterSkillKind.Active && frozen.Count != 1)
            throw new InvalidOperationException("Automatic formula generation must freeze exactly one deterministic winner.");
        draft.frozenMechanics = frozen.Select(value => value.Clone()).ToList();
        draft.presentationState = CharacterSkillPresentationState.PresentationPending;
        return frozen;
    }

    public static void ValidateRestoredFormulaSkill(
        CharacterSkillInstance skill,
        CharacterSkillSystemSettingsSO settings)
    {
        if (skill == null) throw new ArgumentNullException(nameof(skill));
        if (skill.formulaVersion == 0) return;
        CharacterSkillFormulaAuthority authority =
            CharacterSkillFormulaCompatibility.RequireAuthority(skill, settings);
        if (skill.calculatedCost < 0
            || skill.evidenceIds == null
            || skill.evidenceIds.Count == 0
            || skill.evidenceIds.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value.Trim(), value, StringComparison.Ordinal))
            || skill.evidenceIds.Distinct(StringComparer.Ordinal).Count() != skill.evidenceIds.Count
            || skill.formulaCapabilities == null
            || skill.formulaCapabilities.Count == 0)
            throw new InvalidOperationException($"Formula skill '{skill.id}' has a stale or incomplete generated envelope.");
        GameplayOutcomeEvidenceBindingSnapshot[] bindings = (skill.evidenceBindings
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null).ToArray();
        if (bindings.Length != (skill.evidenceBindings?.Count ?? 0)
            || bindings.Select(value => value.publicFactId)
                .Distinct(StringComparer.Ordinal).Count() != bindings.Length
            || bindings.Any(value =>
                !GameplayOutcomeEvidenceBindingAuthority.TryValidate(value, out _)
                || !skill.evidenceIds.Contains(value.publicFactId, StringComparer.Ordinal)))
            throw new InvalidOperationException(
                $"Formula skill '{skill.id}' has invalid exact-outcome evidence bindings.");
        CharacterSkillModuleSelection[] selections = (skill.modules
                ?? new List<CharacterSkillModuleSelection>())
            .Where(value => value != null).ToArray();
        if (selections.Length != skill.formulaCapabilities.Count
            || selections.Any(value => string.IsNullOrWhiteSpace(value.moduleId)
                || !string.IsNullOrEmpty(value.variantId))
            || selections.Select(value => value.moduleId).Distinct(StringComparer.Ordinal).Count()
                != selections.Length)
            throw new InvalidOperationException(
                $"Formula skill '{skill.id}' must bind each generated capability to one variant-free authored module.");
        if (skill.formulaCapabilities.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.capabilityId))
            || skill.formulaCapabilities.Select(value => value.capabilityId)
                .Distinct(StringComparer.Ordinal).Count() != skill.formulaCapabilities.Count)
            throw new InvalidOperationException(
                $"Formula skill '{skill.id}' has null or duplicate capability envelopes.");
        List<NarrativeFormulaCapabilityAllocation> allocations =
            new List<NarrativeFormulaCapabilityAllocation>();
        foreach (CharacterSkillFormulaCapabilityEnvelope envelope in skill.formulaCapabilities)
        {
            CharacterSkillModuleRule module = selections.Select(value => settings.FindModule(value.moduleId))
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        CharacterSkillModuleCapabilityRegistry.Require(value).CapabilityId,
                        envelope?.capabilityId,
                        StringComparison.Ordinal));
            if (module == null)
                throw new InvalidOperationException(
                    $"Formula skill '{skill.id}' has no authored module for capability '{envelope.capabilityId}'.");
            if (skill.formulaVersion >= EffectiveBoundsFormulaVersion
                && !CharacterSkillFormulaRuntimeContextPolicy.ConsumesAllAppliedAxes(
                    skill.kind,
                    skill.trigger,
                    skill.ultimateDomain,
                    module))
            {
                throw new InvalidOperationException(
                    $"Formula skill '{skill.id}' uses capability '{envelope.capabilityId}' "
                    + "in a runtime context that does not consume all applied axes.");
            }
            NarrativeFormulaCapabilityDescriptor descriptor =
                authority.RequireDescriptor(envelope.capabilityId);
            if (!string.Equals(envelope.formatterId, descriptor.FormatterId, StringComparison.Ordinal)
                || !string.Equals(envelope.applicatorId, descriptor.ApplicatorId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Formula skill '{skill.id}' formatter/applicator is stale.");
            Dictionary<string, long> values = (envelope.parameters ?? new List<CharacterSkillFormulaParameter>())
                .ToDictionary(value => value.parameterId, value => value.units, StringComparer.Ordinal);
            if (values.Count != NarrativeFormulaParameterIds.Required.Count)
                throw new InvalidOperationException($"Formula skill '{skill.id}' has unexpected parameter keys.");
            List<NarrativeFormulaParameterValue> parameters =
                new List<NarrativeFormulaParameterValue>();
            foreach (string parameterId in NarrativeFormulaParameterIds.Required)
            {
                NarrativeFormulaQuantizedRange range = descriptor.RequireRange(parameterId);
                if (!values.TryGetValue(parameterId, out long units))
                    throw new InvalidOperationException($"Formula skill '{skill.id}' omits '{parameterId}'.");
                range.RequireContains(units);
                parameters.Add(new NarrativeFormulaParameterValue(parameterId, units));
            }
            allocations.Add(new NarrativeFormulaCapabilityAllocation(descriptor, parameters));
        }
        CharacterSkillAreaRules.RequireValid(
            skill.targetingMode, skill.effectArea, skill.areaSize);
        NarrativeFormulaGenerationCostContext authoredContext =
            authority.RequireGenerationCostContext(skill.trigger, skill.target);
        int recomputedCost = NarrativeFormulaCore.CalculateCost(
            allocations,
            new NarrativeFormulaGenerationCostContext(
                authoredContext.TriggerFrequencyUnits,
                authoredContext.GuaranteedProc,
                CharacterSkillAreaRules.ResolveCostTargetCount(
                    skill.effectArea,
                    skill.areaSize,
                    authoredContext.TargetCount)));
        if (recomputedCost != skill.positiveCost
            || skill.calculatedCost != skill.positiveCost - skill.drawbackCredit
            || skill.narrativeBudget < 0
            || skill.calculatedCost > skill.narrativeBudget)
            throw new InvalidOperationException($"Formula skill '{skill.id}' calculated cost does not match its envelope.");
        ValidateStoredDrawback(
            skill,
            settings,
            authority);
    }

    public static float RequireParameter(
        CharacterSkillInstance skill,
        string capabilityId,
        string parameterId,
        CharacterSkillSystemSettingsSO settings)
    {
        if (skill == null || skill.formulaVersion <= 0)
            throw new InvalidOperationException("Generated formula parameters require formulaVersion > 0.");
        ValidateRestoredFormulaSkill(skill, settings);
        CharacterSkillFormulaAuthority authority =
            CharacterSkillFormulaCompatibility.RequireAuthority(skill, settings);
        CharacterSkillFormulaCapabilityEnvelope envelope = skill.formulaCapabilities.SingleOrDefault(value => value != null
            && string.Equals(value.capabilityId, capabilityId, StringComparison.Ordinal));
        if (envelope == null) return 0f;
        NarrativeFormulaQuantizedRange range = authority.RequireDescriptor(capabilityId)
            .RequireRange(parameterId);
        CharacterSkillFormulaParameter parameter = envelope.parameters.Single(value => value != null
            && string.Equals(value.parameterId, parameterId, StringComparison.Ordinal));
        return (float)range.ToDecimal(parameter.units);
    }

    private static NarrativeFormulaEvidence[] BuildEvidence(
        CharacterNarrativeLedger ledger,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeEvidence = null,
        NarrativeFormulaStrengthPolicy policy = null)
    {
        CharacterNarrativeFact[] facts = MeaningfulFacts(ledger);
        IEnumerable<NarrativeFormulaEvidence> legacy = facts.Select(fact => new NarrativeFormulaEvidence(
            BuildEvidenceId(fact),
            RequireFactKey(fact.eventGroupKey, fact, "eventGroupKey"),
            RequireFactKey(fact.actionKey, fact, "actionKey"),
            fact.relationshipKey?.Trim() ?? string.Empty,
            fact.domain.ToString(),
            fact.milestoneCount,
            fact.importancePoints,
            fact.influenceUseCount));
        IEnumerable<NarrativeFormulaEvidence> exact = (outcomeEvidence
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(
                value,
                policy ?? throw new InvalidOperationException(
                    "Outcome evidence requires a formula strength policy.")));
        return legacy.Concat(exact)
            .OrderBy(value => value.EvidenceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CharacterNarrativeFact[] MeaningfulFacts(
        CharacterNarrativeLedger ledger) =>
        (ledger?.Facts ?? Array.Empty<CharacterNarrativeFact>())
        .Where(value => value != null && value.milestoneCount > 0)
        .OrderBy(BuildEvidenceId, StringComparer.Ordinal)
        .ToArray();

    private static string RequireFactKey(string value, CharacterNarrativeFact fact, string field)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new InvalidOperationException($"Narrative fact '{fact.factId}' lacks canonical {field} formula metadata.");
        return canonical;
    }

    private static double CountAffinityMatches(
        NarrativeFormulaCapabilityDescriptor descriptor,
        IEnumerable<NarrativeFormulaEvidence> evidence)
    {
        HashSet<string> keys = new HashSet<string>(evidence.SelectMany(value => new[]
        {
            value.EventGroupKey, value.ActionKey, value.RelationshipKey, value.DomainKey
        }).Where(value => value.Length > 0), StringComparer.Ordinal);
        return descriptor.AffinityKeys.Count(keys.Contains);
    }

    private static HashSet<string> CollectCommittedCapabilities(
        CharacterProgression progression,
        CharacterSkillSystemSettingsSO settings)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<CharacterSkillInstance> skills = progression.ActiveSkills
            .Concat(progression.PassiveSkills)
            .Concat(progression.OwnerFixedSkills)
            .Concat(progression.Ultimate == null ? Array.Empty<CharacterSkillInstance>() : new[] { progression.Ultimate });
        foreach (CharacterSkillInstance skill in skills.Where(value => value != null))
        {
            if (skill.formulaVersion > 0)
            {
                foreach (CharacterSkillFormulaCapabilityEnvelope value in skill.formulaCapabilities ?? new List<CharacterSkillFormulaCapabilityEnvelope>())
                    if (value != null && !string.IsNullOrWhiteSpace(value.capabilityId)) result.Add(value.capabilityId);
            }
            else
            {
                foreach (CharacterSkillModuleSelection value in skill.modules ?? new List<CharacterSkillModuleSelection>())
                {
                    CharacterSkillModuleRule module = settings.FindModule(value?.moduleId);
                    if (module != null) result.Add(CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId);
                }
            }
        }
        return result;
    }

    private static List<FrozenOption> SelectDistinctActive(
        IEnumerable<FrozenOption> options,
        IEnumerable<CharacterSkillCandidateRule> rules,
        int budget)
    {
        CharacterSkillCandidateRule[] orderedRules =
            (rules ?? Array.Empty<CharacterSkillCandidateRule>()).ToArray();
        FrozenOption[][] candidatesByRule = orderedRules.Select(rule => options
                .Where(value => ReferenceEquals(value.Rule, rule))
                .GroupBy(value => value.Formula.Descriptor.CapabilityId,
                    StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(value =>
                        NarrativeFormulaCore.NormalizedScore(value.Formula, budget))
                    .ThenBy(value => value.Formula.CanonicalSignature,
                        StringComparer.Ordinal)
                    .First())
                .OrderBy(value => value.Formula.Descriptor.CapabilityId,
                    StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        if (orderedRules.Length != 3
            || candidatesByRule.Any(values => values.Length == 0))
            throw new InvalidOperationException(
                "Active formula rules cannot supply three registered core effects.");

        List<FrozenOption> current = new List<FrozenOption>(orderedRules.Length);
        HashSet<string> usedCapabilities = new HashSet<string>(StringComparer.Ordinal);
        List<FrozenOption> best = null;
        double bestScore = double.NegativeInfinity;
        string bestSignature = string.Empty;

        void Search(int ruleIndex, double score)
        {
            if (ruleIndex >= candidatesByRule.Length)
            {
                string signature = string.Join("\n", current.Select(value =>
                    value.Formula.CanonicalSignature));
                if (score > bestScore + 0.000000001d
                    || (Math.Abs(score - bestScore) <= 0.000000001d
                        && (best == null || string.CompareOrdinal(
                            signature, bestSignature) < 0)))
                {
                    bestScore = score;
                    bestSignature = signature;
                    best = current.ToList();
                }
                return;
            }

            foreach (FrozenOption candidate in candidatesByRule[ruleIndex])
            {
                string capability = candidate.Formula.Descriptor.CapabilityId;
                if (!usedCapabilities.Add(capability)) continue;
                current.Add(candidate);
                Search(
                    ruleIndex + 1,
                    score + NarrativeFormulaCore.NormalizedScore(
                        candidate.Formula,
                        budget));
                current.RemoveAt(current.Count - 1);
                usedCapabilities.Remove(capability);
            }
        }

        Search(0, 0d);
        if (best == null)
            throw new InvalidOperationException(
                "Active formula rules cannot supply three distinct registered core effects.");
        return best.OrderByDescending(value =>
                NarrativeFormulaCore.NormalizedScore(value.Formula, budget))
            .ThenBy(value => value.Formula.CanonicalSignature, StringComparer.Ordinal)
            .ToList();
    }

    private static CharacterSkillInstance CreateFrozenSkill(
        CharacterSkillDraft draft,
        FrozenOption option,
        int index,
        string catalogSha256,
        int formulaVersion)
    {
        string signatureHash = NarrativeInferenceHash.ComputeSha256Utf8(option.Formula.CanonicalSignature);
        string presentationHash = NarrativeInferenceHash.ComputeSha256Utf8(
            draft.requestKey + "\n" + index.ToString(CultureInfo.InvariantCulture) + "\n" + signatureHash)
            .Substring("sha256:".Length);
        string presentationId = "presentation:skill:" + presentationHash;
        List<CharacterSkillFormulaCapabilityEnvelope> envelopes = option.Formula.Allocations.Select(allocation =>
            new CharacterSkillFormulaCapabilityEnvelope
            {
                capabilityId = allocation.Descriptor.CapabilityId,
                formatterId = allocation.Descriptor.FormatterId,
                applicatorId = allocation.Descriptor.ApplicatorId,
                parameters = allocation.Parameters.Select(value => new CharacterSkillFormulaParameter
                {
                    parameterId = value.ParameterId,
                    units = value.Units
                }).ToList()
            }).ToList();
        List<CharacterSkillModuleSelection> modules = option.Modules.Select(module =>
            new CharacterSkillModuleSelection { moduleId = module.id, variantId = string.Empty }).ToList();
        CharacterSkillFormationRules.Resolve(option.Rule.target, modules,
            out OffenseFormationMask usableFrom, out OffenseFormationMask targetPositions);
        string mechanics = CharacterSkillFormulaPresentation.FormatMechanicalDescription(
            option.Modules,
            option.Rule,
            option.Formula);
        if (option.DrawbackEffect != null)
            mechanics += "; 장착 부담: "
                + FormatDrawbackEffect(option.DrawbackEffect);
        return new CharacterSkillInstance
        {
            id = draft.requestKey + ":formula:" + signatureHash,
            ruleId = option.Rule.ruleId,
            combinationId = string.Empty,
            kind = draft.kind,
            rarity = option.Rule.rarity,
            trigger = option.Rule.trigger,
            target = option.Rule.target,
            targetingMode = option.Rule.targetingMode,
            effectArea = option.Rule.effectArea,
            areaSize = option.Rule.areaSize,
            ultimateDomain = option.Rule.ultimateDomain,
            cooldownTurns = checked(option.Rule.cooldownTurns + option.DrawbackCooldownTurns),
            manualDurationHours = option.Rule.manualDurationHours,
            manualCooldownDays = checked(option.Rule.manualCooldownDays
                + option.DrawbackCooldownDays),
            usableFrom = usableFrom,
            targetPositions = targetPositions,
            modules = modules,
            requestKey = draft.requestKey,
            formulaVersion = formulaVersion,
            formulaCatalogSha256 = catalogSha256,
            calculatedCost = option.Formula.CalculatedCost,
            positiveCost = option.Formula.PositiveCost,
            drawbackCredit = option.Formula.DrawbackCredit,
            drawbackId = option.Formula.DrawbackId,
            drawbackEffect = option.DrawbackEffect?.Clone(),
            narrativeBudget = draft.formulaBudget,
            drawbackEvidenceQualified = option.DrawbackEvidenceQualified,
            evidenceIds = option.Formula.EvidenceIds.ToList(),
            formulaCapabilities = envelopes,
            presentationId = presentationId,
            mechanicalDescription = mechanics
        };
    }

    private static NarrativeFormulaDrawbackOption BuildDrawbackOption(
        CharacterSkillKind kind,
        CharacterSkillCandidateRule rule,
        int ruleIndex,
        bool hasNegativeEvidence,
        out int cooldownTurns,
        out int cooldownDays)
    {
        cooldownTurns = 0;
        cooldownDays = 0;
        if (kind == CharacterSkillKind.Active
            && rule?.trigger == CharacterSkillTrigger.ManualWork)
        {
            cooldownDays = 2;
        }
        else if (kind == CharacterSkillKind.Active && ruleIndex > 0)
            cooldownTurns = Math.Min(2, ruleIndex);
        else if (kind == CharacterSkillKind.Ultimate && hasNegativeEvidence)
            cooldownTurns = 1;
        if (cooldownTurns == 0 && cooldownDays == 0) return null;
        NarrativeFormulaDrawbackSelectionKind selectionKind = kind == CharacterSkillKind.Active
            ? NarrativeFormulaDrawbackSelectionKind.PlayerChoice
            : NarrativeFormulaDrawbackSelectionKind.Automatic;
        return new NarrativeFormulaDrawbackOption(
            cooldownDays > 0
                ? "character-skill:work-cooldown:+"
                    + cooldownDays.ToString(CultureInfo.InvariantCulture) + "d"
                : "character-skill:cooldown:+"
                    + cooldownTurns.ToString(CultureInfo.InvariantCulture),
            cooldownDays > 0 ? cooldownDays : cooldownTurns,
            selectionKind,
            reachable: true,
            mandatory: true,
            separatelyRemovable: false,
            cancelsSelectedBenefit: false,
            hasNegativeNarrativeEvidence: hasNegativeEvidence);
    }

    private static void ValidateStoredDrawback(
        CharacterSkillInstance skill,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillFormulaAuthority authority)
    {
        bool hasStatEffect = skill.drawbackEffect != null
            && !CharacterSkillDrawbackEffectEnvelope.IsExactSerializedAbsence(
                skill.drawbackEffect);
        if (string.IsNullOrEmpty(skill.drawbackId))
        {
            if (skill.drawbackCredit != 0 || hasStatEffect)
                throw new InvalidOperationException(
                    $"Formula skill '{skill.id}' has credit without a drawback.");
            return;
        }
        if (hasStatEffect)
        {
            CharacterSkillDrawbackEffectEnvelope effect = skill.drawbackEffect;
            int statAmount = effect.severityUnits;
            if (authority.UsesCurrentStatDrawbacks)
            {
                CharacterSkillDrawbackCapabilityDefinition definition =
                    settings.FindDrawback(effect.drawbackModuleId)
                    ?? throw new InvalidOperationException(
                        $"Formula skill '{skill.id}' references an unknown stat drawback.");
                float expectedValue = definition.BuildResolvedStatValue(statAmount);
                if (definition.applicationKind
                        != CharacterSkillDrawbackApplicationKind.EquippedStat
                    || !string.Equals(skill.drawbackId,
                        definition.BuildResolvedDrawbackId(statAmount),
                        StringComparison.Ordinal)
                    || !string.Equals(effect.effectId,
                        definition.effectDefinition.EffectId,
                        StringComparison.Ordinal)
                    || !string.Equals(effect.targetId,
                        definition.effectDefinition.TargetId,
                        StringComparison.Ordinal)
                    || effect.operation != definition.effectDefinition.Operation
                    || float.IsNaN(effect.value)
                    || float.IsInfinity(effect.value)
                    || Math.Abs(effect.value - expectedValue) > 0.000001f
                    || !string.Equals(effect.displayName,
                        definition.DisplayName,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Formula skill '{skill.id}' has a stale or invalid stat drawback envelope.");
            }
            else if (!CharacterSkillFormulaCompatibility.TryValidateArchivedStatDrawback(
                         authority, skill, out string archivedError))
            {
                throw new InvalidOperationException(
                    $"Formula skill '{skill.id}' has a stale or invalid stat drawback envelope: {archivedError}.");
            }
            NarrativeFormulaDrawbackResolution statExpected = authority.DrawbackPolicy.Resolve(
                skill.narrativeBudget,
                new NarrativeFormulaDrawbackOption(
                    skill.drawbackId,
                    statAmount,
                    skill.kind == CharacterSkillKind.Active
                        ? NarrativeFormulaDrawbackSelectionKind.PlayerChoice
                        : NarrativeFormulaDrawbackSelectionKind.Automatic,
                    true, true, false, false, skill.drawbackEvidenceQualified));
            if (statExpected.AcceptedCredit != skill.drawbackCredit)
                throw new InvalidOperationException(
                    $"Formula skill '{skill.id}' has stale stat drawback credit.");
            return;
        }
        const string turnPrefix = "character-skill:cooldown:+";
        const string dayPrefix = "character-skill:work-cooldown:+";
        bool dayCooldown = skill.drawbackId.StartsWith(dayPrefix, StringComparison.Ordinal)
            && skill.drawbackId.EndsWith("d", StringComparison.Ordinal);
        string numeric = dayCooldown
            ? skill.drawbackId.Substring(
                dayPrefix.Length,
                skill.drawbackId.Length - dayPrefix.Length - 1)
            : skill.drawbackId.StartsWith(turnPrefix, StringComparison.Ordinal)
                ? skill.drawbackId.Substring(turnPrefix.Length)
                : string.Empty;
        if (!int.TryParse(numeric,
                NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
            || amount < 1 || amount > 2
            || (dayCooldown
                ? skill.trigger != CharacterSkillTrigger.ManualWork
                    || skill.manualCooldownDays < amount
                    || skill.manualDurationHours < 1
                : skill.cooldownTurns < amount))
            throw new InvalidOperationException($"Formula skill '{skill.id}' has an invalid cooldown drawback.");
        NarrativeFormulaDrawbackResolution expected = authority.DrawbackPolicy.Resolve(
            skill.narrativeBudget,
            new NarrativeFormulaDrawbackOption(
                skill.drawbackId,
                amount,
                skill.kind == CharacterSkillKind.Active
                    ? NarrativeFormulaDrawbackSelectionKind.PlayerChoice
                    : NarrativeFormulaDrawbackSelectionKind.Automatic,
                true, true, false, false, skill.drawbackEvidenceQualified));
        if (expected.AcceptedCredit != skill.drawbackCredit)
            throw new InvalidOperationException($"Formula skill '{skill.id}' has stale drawback credit.");
    }

    public static string FormatDrawbackEffect(
        CharacterSkillDrawbackEffectEnvelope effect)
    {
        if (effect == null) return string.Empty;
        string target = AcquiredTraitBurdenTargetCatalog.DisplayName(effect.targetId);
        return effect.operation switch
        {
            GameplayEffectOperation.Multiply => effect.value >= 1f
                ? $"{target} ×{effect.value.ToString("0.###", CultureInfo.InvariantCulture)}"
                : $"{target} ×{effect.value.ToString("0.###", CultureInfo.InvariantCulture)}",
            GameplayEffectOperation.AddPercent =>
                $"{target} {(effect.value >= 0f ? "+" : string.Empty)}{(effect.value * 100f).ToString("0.#", CultureInfo.InvariantCulture)}%",
            GameplayEffectOperation.AddFlat =>
                $"{target} {(effect.value >= 0f ? "+" : string.Empty)}{effect.value.ToString("0.###", CultureInfo.InvariantCulture)}",
            _ => $"{target} {effect.operation} {effect.value.ToString("0.###", CultureInfo.InvariantCulture)}"
        } + " (기술 장착 중)";
    }
}

public static class CharacterSkillFormulaPresentation
{
    public static string FormatMechanicalDescription(
        IReadOnlyList<CharacterSkillModuleRule> modules,
        CharacterSkillCandidateRule rule,
        NarrativeFormulaCandidate candidate)
    {
        if (modules == null || rule == null || candidate == null)
            throw new ArgumentNullException(nameof(modules));
        if (modules.Count != candidate.Allocations.Count)
            throw new InvalidOperationException("Formula presentation requires one module per capability allocation.");
        List<string> effects = new List<string>();
        for (int index = 0; index < candidate.Allocations.Count; index++)
        {
            NarrativeFormulaCapabilityAllocation allocation = candidate.Allocations[index];
            if (!string.Equals(allocation.Descriptor.FormatterId, allocation.Descriptor.CapabilityId, StringComparison.Ordinal))
                throw new InvalidOperationException("CharacterSkill formula formatter is not registered for its capability.");
            string parameters = string.Join(", ", allocation.Parameters.Select(value =>
                value.ParameterId + "=" + allocation.Descriptor.RequireRange(value.ParameterId).Format(value.Units)));
            effects.Add(modules[index].displayName + ": " + parameters);
        }
        string drawback = candidate.DrawbackId.Length == 0
            ? string.Empty
            : "; drawback=" + candidate.DrawbackId
                + "; drawbackCredit=" + candidate.DrawbackCredit;
        return string.Join(" + ", effects)
            + $"; trigger={rule.trigger}; target={rule.target}; targeting={rule.targetingMode}"
            + $"; area={rule.effectArea}; areaSize={rule.areaSize}"
            + (rule.trigger == CharacterSkillTrigger.ManualWork
                ? $"; durationHours={rule.manualDurationHours}"
                : string.Empty)
            + $"; positiveCost={candidate.PositiveCost}"
            + drawback + $"; netCost={candidate.CalculatedCost}";
    }
}

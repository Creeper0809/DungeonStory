using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static class CharacterAcquiredTraitEvidenceProjection
{
    public const string ProjectedFactIdPrefix = "public-fact:sha256:";

    public static string Project(CharacterNarrativeFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        return NarrativePublicContextFactory.BuildProjectedFactId(
            fact.domain.ToString(),
            fact.factId,
            fact.subjectId);
    }

    public static bool TryResolve(
        CharacterNarrativeLedger ledger,
        string evidenceFactId,
        out CharacterNarrativeFact[] facts,
        out string error)
    {
        facts = Array.Empty<CharacterNarrativeFact>();
        string evidence = evidenceFactId?.Trim() ?? string.Empty;
        if (ledger == null || evidence.Length == 0)
        {
            error = "Acquired-trait evidence projection requires a ledger and canonical ID.";
            return false;
        }

        CharacterNarrativeFact[] meaningful = ledger.Facts
            .Where(value => value != null && value.milestoneCount > 0)
            .ToArray();
        if (evidence.StartsWith(ProjectedFactIdPrefix, StringComparison.Ordinal))
        {
            facts = meaningful.Where(value => string.Equals(
                    Project(value),
                    evidence,
                    StringComparison.Ordinal))
                .ToArray();
            if (facts.Length > 1)
            {
                facts = Array.Empty<CharacterNarrativeFact>();
                error = $"Projected acquired-trait evidence '{evidence}' resolves to multiple source tuples.";
                return false;
            }
        }
        else
        {
            facts = meaningful.Where(value => string.Equals(
                    value.factId?.Trim(),
                    evidence,
                    StringComparison.Ordinal))
                .ToArray();
            if (facts.Select(Project).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                facts = Array.Empty<CharacterNarrativeFact>();
                error = $"Legacy acquired-trait evidence '{evidence}' is ambiguous across public fact tuples.";
                return false;
            }
        }

        if (facts.Length == 0)
        {
            error = $"Acquired-trait evidence fact '{evidence}' is not a meaningful fact in the authoritative ledger.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

/// <summary>Formula-v1+ mechanical selection. This deliberately performs no LLM call,
/// state publication, or ledger mutation; the aggregate command owns those atomic effects.</summary>
public static class CharacterAcquiredTraitFormulaGeneration
{
    public const int ModuleSelectionFormulaVersion = 3;

    private sealed class FormulaChoice
    {
        public CharacterAcquiredTraitModuleSO Benefit;
        public NarrativeFormulaCandidate BenefitCandidate;
        public CharacterAcquiredTraitDrawbackCapabilityDefinition Drawback;
        public NarrativeFormulaCandidate DrawbackCandidate;
    }

    public static NarrativeFormulaModuleSelectionRequest PrepareModuleSelection(
        CharacterProgression progression,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> selectedEvidenceIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings = null)
    {
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        string targetId = progression.Actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (targetId.Length == 0) throw new InvalidOperationException("Acquired-trait selection requires a persistent target.");
        CharacterAcquiredTraitModuleSO[] modules = (source ?? throw new ArgumentNullException(nameof(source)))
            .Where(value => value != null).OrderBy(value => value.ModuleId, StringComparer.Ordinal).ToArray();
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        NarrativeFormulaStrengthPolicy formulaPolicy = settings.RequireFormulaPolicy();
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings = (outcomeBindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToArray();
        NarrativeFormulaEvidence[] evidence = BuildEvidence(
            progression.NarrativeLedger, selectedEvidenceIds, exactBindings, formulaPolicy);
        NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(
            formulaPolicy, evidence);
        NarrativeFormulaGenerationCostContext generationContext =
            settings.FormulaPolicy.RequireGenerationContext();
        HashSet<string> selectedEvidence = evidence.Select(value => value.EvidenceId).ToHashSet(StringComparer.Ordinal);
        CharacterNarrativeFact[] facts = progression.NarrativeLedger.Facts.Where(value => value != null
            && selectedEvidence.Contains(CharacterAcquiredTraitEvidenceProjection.Project(value))).ToArray();
        HashSet<CharacterNarrativeDomain> exactDomains = ResolveExactDomains(exactBindings);
        CharacterAcquiredTraitInstanceState[] active = progression.CaptureAcquiredTraitState()
            .CaptureActiveInstances().ToArray();
        Dictionary<string, CharacterAcquiredTraitModuleSO> byId = modules.ToDictionary(value => value.ModuleId, StringComparer.Ordinal);
        HashSet<string> occupied = active.SelectMany(value => value.moduleIds ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeConflicts = active.SelectMany(value => value.moduleIds ?? new List<string>())
            .Where(byId.ContainsKey).SelectMany(value => byId[value].ConflictGroups).ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeBindings = active.SelectMany(value => value.moduleIds ?? new List<string>())
            .Where(byId.ContainsKey).SelectMany(value => byId[value].Effects).Where(value => value != null)
            .Select(value => value.bindingId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeDrawbacks = active.SelectMany(value => value.drawbackCapabilityIds ?? new List<string>())
            .ToHashSet(StringComparer.Ordinal);
        List<NarrativeFormulaModuleOffer> offers = modules.Where(value =>
                !occupied.Contains(value.ModuleId)
                && !value.ConflictGroups.Any(activeConflicts.Contains)
                && !value.Effects.Any(effect => effect != null && activeBindings.Contains(effect.bindingId))
                && value.DomainAffinities.Any(domain =>
                    facts.Any(fact => fact.domain == domain) || exactDomains.Contains(domain))
                && CalculateAffinity(value.RequireFormulaDescriptor(), evidence) > 0d
                && value.RequireFormulaDescriptor().CalculateContextCost(
                    generationContext, 1) <= strength.Budget)
            .Select(value => new NarrativeFormulaModuleOffer(
                value.ModuleId,
                NarrativeFormulaModulePolarity.Positive,
                FormatModuleOfferDescription(value),
                new NarrativeFormulaCapabilityInput(value.RequireFormulaDescriptor(),
                    CalculateAffinity(value.RequireFormulaDescriptor(), evidence), 1d),
                value.RequireFormulaDescriptor().CalculateContextCost(
                    generationContext, 1)))
            .ToList();
        offers.AddRange(settings.DrawbackCapabilities.Where(value => value != null
                && !activeDrawbacks.Contains(value.DrawbackId)
                && (HasQualifiedNegativeEvidence(value, facts)
                    || exactBindings.Any(GameplayOutcomeEvidenceFormulaProjection.IsNegative)))
            .Select(value => new NarrativeFormulaModuleOffer(
                value.DrawbackId,
                NarrativeFormulaModulePolarity.Drawback,
                FormatDrawbackOfferDescription(value),
                new NarrativeFormulaCapabilityInput(value.RequireFormulaDescriptor(),
                    CalculateAffinity(value.RequireFormulaDescriptor(), evidence), 1d),
                value.RequireFormulaDescriptor().CalculateContextCost(
                    generationContext, 1))));
        if (offers.All(value => value.Polarity != NarrativeFormulaModulePolarity.Positive))
            throw new InvalidOperationException("No authored acquired-trait benefit is individually legal.");
        string catalog = settings.FormulaPolicy.RequireCatalogSha256();
        string selectionId = "selection:trait:"
            + NarrativeInferenceHash.ComputeSha256Utf8(string.Join("\n",
                RequireCanonical(requestId, nameof(requestId)),
                RequireCanonical(requestKey, nameof(requestKey)), targetId,
                manifestationMilestone.ToString(CultureInfo.InvariantCulture), catalog,
                string.Join(",", offers.Select(value => value.ModuleId))))
                .Substring("sha256:".Length);
        return new NarrativeFormulaModuleSelectionRequest(
            selectionId, offers, evidence.Select(value => value.EvidenceId),
            maximumPositiveModules: 1, maximumDrawbackModules: 1);
    }

    public static CharacterAcquiredTraitPendingRequestState PreparePendingModuleSelection(
        CharacterProgression progression,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> selectedEvidenceIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        if (policy.FormulaVersion < ModuleSelectionFormulaVersion)
            throw new InvalidOperationException("Acquired-trait module selection requires formulaVersion 3 or later.");
        NarrativeFormulaModuleSelectionRequest selection = PrepareModuleSelection(
            progression, requestId, requestKey, manifestationMilestone,
            settings, source, selectedEvidenceIds, outcomeBindings);
        NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(
            policy, BuildEvidence(progression.NarrativeLedger, selection.EvidenceFactIds,
                outcomeBindings, policy));
        string targetId = progression.Actor.Identity.PersistentId;
        string packetHash = NarrativeInferenceHash.ComputeSha256Utf8(string.Join("\n",
            RequireCanonical(requestId, nameof(requestId)),
            RequireCanonical(requestKey, nameof(requestKey)), targetId,
            manifestationMilestone.ToString(CultureInfo.InvariantCulture),
            policy.FormulaVersion.ToString(CultureInfo.InvariantCulture),
            settings.FormulaPolicy.RequireCatalogSha256(), selection.SelectionId,
            string.Join(",", selection.Offers.Select(value => value.ModuleId)),
            string.Join(",", selection.EvidenceFactIds)));
        return new CharacterAcquiredTraitPendingRequestState
        {
            targetPersistentId = targetId,
            requestId = requestId,
            requestKey = requestKey,
            candidatePacketHash = packetHash,
            submissionAuditId = "trait-module-selection-submission:"
                + packetHash.Substring("sha256:".Length),
            manifestationMilestone = manifestationMilestone,
            evidenceFactIds = selection.EvidenceFactIds.ToList(),
            evidenceBindings = GameplayOutcomeEvidenceFormulaProjection.Select(
                outcomeBindings, selection.EvidenceFactIds),
            formulaVersion = policy.FormulaVersion,
            formulaCatalogSha256 = settings.FormulaPolicy.RequireCatalogSha256(),
            narrativeBudget = strength.Budget,
            moduleSelectionId = selection.SelectionId,
            offeredBenefitModuleIds = selection.Offers
                .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Positive)
                .Select(value => value.ModuleId).OrderBy(value => value, StringComparer.Ordinal).ToList(),
            offeredDrawbackModuleIds = selection.Offers
                .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Drawback)
                .Select(value => value.ModuleId).OrderBy(value => value, StringComparer.Ordinal).ToList(),
            moduleOffers = selection.Offers.Select(value => new CharacterAcquiredTraitModuleOfferState
            {
                moduleId = value.ModuleId,
                polarity = value.Polarity,
                semanticDescription = value.SemanticDescription
            }).ToList(),
            presentationState = CharacterAcquiredTraitPresentationState.ModuleSelectionPending,
            presentationFailureCount = 0
        };
    }

    public static CharacterAcquiredTraitPendingRequestState FreezeSelected(
        CharacterProgression progression,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> selectedEvidenceIds,
        NarrativeFormulaModuleSelectionChoice choice,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings = null)
    {
        NarrativeFormulaModuleSelectionRequest request = PrepareModuleSelection(
            progression, requestId, requestKey, manifestationMilestone,
            settings, source, selectedEvidenceIds, outcomeBindings);
        if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                request, choice, out NarrativeFormulaValidatedModuleSelection selected, out string error))
            throw new InvalidOperationException(error);
        CharacterAcquiredTraitModuleSO[] modules = (source ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .Where(value => value != null).ToArray();
        CharacterAcquiredTraitModuleSO benefit = modules.Single(value => string.Equals(
            value.ModuleId, selected.PositiveModules[0].ModuleId, StringComparison.Ordinal));
        CharacterAcquiredTraitDrawbackCapabilityDefinition drawback = selected.DrawbackModules.Count == 0
            ? null
            : settings.DrawbackCapabilities.Single(value => value != null && string.Equals(
                value.DrawbackId, selected.DrawbackModules[0].ModuleId, StringComparison.Ordinal));
        if (drawback != null && ConflictsOrCancels(benefit, drawback))
            throw new InvalidOperationException("The selected acquired-trait benefit and drawback conflict or cancel the same effect binding.");
        return FreezeCore(
            progression.Actor.Identity.PersistentId,
            progression.NarrativeLedger,
            progression.CaptureAcquiredTraitState(),
            requestId, requestKey, manifestationMilestone, settings, modules,
            selected.EvidenceFactIds,
            benefit.ModuleId,
            drawback?.DrawbackId,
            outcomeBindings);
    }

    public static CharacterAcquiredTraitPendingRequestState Freeze(
        CharacterProgression progression,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> selectedEvidenceIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings = null)
    {
        if (progression == null) throw new ArgumentNullException(nameof(progression));
        string targetId = progression.Actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (targetId.Length == 0) throw new InvalidOperationException("Formula acquired-trait generation requires a persistent target.");
        return FreezeCore(
            targetId,
            progression.NarrativeLedger,
            progression.CaptureAcquiredTraitState(),
            requestId,
            requestKey,
            manifestationMilestone,
            settings,
            source,
            selectedEvidenceIds,
            outcomeBindings: outcomeBindings);
    }

    [GameplayInternalOnly(
        "The controlled export replays the same acquired-trait authority without publishing runtime state.",
        "FormulaPresentationPilotExporter")]
    public static CharacterAcquiredTraitPendingRequestState FreezeForExport(
        string targetPersistentId,
        CharacterNarrativeLedger ledger,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> selectedEvidenceIds) => FreezeCore(
            RequireCanonical(targetPersistentId, nameof(targetPersistentId)),
            ledger ?? throw new ArgumentNullException(nameof(ledger)),
            new CharacterAcquiredTraitAggregateState(),
            requestId,
            requestKey,
            manifestationMilestone,
            settings,
            source,
            selectedEvidenceIds);

    [GameplayInternalOnly(
        "The controlled review export replays an already-authored module choice through the runtime formula authority without publishing runtime state.",
        "FormulaPresentationPilotExporter")]
    public static CharacterAcquiredTraitPendingRequestState FreezeSelectedForExport(
        string targetPersistentId,
        CharacterNarrativeLedger ledger,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> selectedEvidenceIds,
        string selectedBenefitModuleId,
        string selectedDrawbackCapabilityId = null) => FreezeCore(
            RequireCanonical(targetPersistentId, nameof(targetPersistentId)),
            ledger ?? throw new ArgumentNullException(nameof(ledger)),
            new CharacterAcquiredTraitAggregateState(),
            requestId,
            requestKey,
            manifestationMilestone,
            settings,
            source,
            selectedEvidenceIds,
            RequireCanonical(selectedBenefitModuleId, nameof(selectedBenefitModuleId)),
            string.IsNullOrWhiteSpace(selectedDrawbackCapabilityId)
                ? null
                : RequireCanonical(selectedDrawbackCapabilityId,
                    nameof(selectedDrawbackCapabilityId)));

    private static CharacterAcquiredTraitPendingRequestState FreezeCore(
        string targetId,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitAggregateState aggregateState,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> selectedEvidenceIds,
        string selectedBenefitModuleId = null,
        string selectedDrawbackCapabilityId = null,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (!settings.TryGetGate(manifestationMilestone, out _))
            throw new InvalidOperationException("Formula acquired-trait manifestation milestone is not authored.");
        string canonicalRequestId = RequireCanonical(requestId, nameof(requestId));
        string canonicalRequestKey = RequireCanonical(requestKey, nameof(requestKey));
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        string catalogSha256 = settings.FormulaPolicy.RequireCatalogSha256();
        NarrativeFormulaEvidence[] evidence = BuildEvidence(
            ledger,
            selectedEvidenceIds,
            outcomeBindings,
            policy);
        NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(policy, evidence);
        HashSet<string> selectedEvidence = evidence.Select(value => value.EvidenceId)
            .ToHashSet(StringComparer.Ordinal);
        CharacterNarrativeFact[] selectedFacts = (ledger?.Facts
                ?? Array.Empty<CharacterNarrativeFact>())
            .Where(value => value != null && selectedEvidence.Contains(
                CharacterAcquiredTraitEvidenceProjection.Project(value)))
            .ToArray();
        bool hasNegativeEvidence = selectedFacts.Any(value =>
            NarrativeFormulaNegativeEvidence.Matches(value.outcome, value.factId))
            || (outcomeBindings ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
                .Any(GameplayOutcomeEvidenceFormulaProjection.IsNegative);
        CharacterAcquiredTraitModuleSO[] modules = (source ?? throw new ArgumentNullException(nameof(source)))
            .Where(value => value != null).OrderBy(value => value.ModuleId, StringComparer.Ordinal).ToArray();
        if (modules.Length == 0) throw new InvalidOperationException("Formula acquired-trait generation has no authored modules.");
        Dictionary<string, CharacterAcquiredTraitModuleSO> byId = modules
            .ToDictionary(value => value.ModuleId, StringComparer.Ordinal);
        CharacterAcquiredTraitInstanceState[] active = (aggregateState
                ?? new CharacterAcquiredTraitAggregateState()).CaptureActiveInstances()
            .ToArray();
        HashSet<string> occupiedModules = active
            .SelectMany(value => value.moduleIds ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeConflicts = active.SelectMany(value => value.moduleIds)
            .Where(byId.ContainsKey)
            .SelectMany(value => byId[value].ConflictGroups)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeBindings = active.SelectMany(value => value.moduleIds)
            .Where(byId.ContainsKey)
            .SelectMany(value => byId[value].Effects)
            .Where(value => value != null)
            .Select(value => value.bindingId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> activeDrawbackIds = active
            .SelectMany(value => value.drawbackCapabilityIds ?? new List<string>())
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, CharacterAcquiredTraitDrawbackCapabilityDefinition> drawbacksById =
            settings.DrawbackCapabilities.Where(value => value != null)
                .ToDictionary(value => value.DrawbackId, StringComparer.Ordinal);
        HashSet<string> activeDrawbackConflicts = activeDrawbackIds
            .Where(drawbacksById.ContainsKey)
            .SelectMany(value => drawbacksById[value].ConflictGroups)
            .ToHashSet(StringComparer.Ordinal);
        List<FormulaChoice> choices = new();
        foreach (CharacterAcquiredTraitModuleSO module in modules.Where(value =>
                     (selectedBenefitModuleId == null || string.Equals(
                         value.ModuleId, selectedBenefitModuleId, StringComparison.Ordinal))
                     && !occupiedModules.Contains(value.ModuleId)
                     && !value.ConflictGroups.Any(activeConflicts.Contains)
                     && !value.Effects.Any(effect => effect != null && activeBindings.Contains(effect.bindingId))))
        {
            NarrativeFormulaCapabilityDescriptor descriptor = module.RequireFormulaDescriptor();
            double benefitAffinity = CalculateAffinity(descriptor, evidence);
            if (selectedDrawbackCapabilityId == null)
            {
                foreach (NarrativeFormulaCandidate candidate in NarrativeFormulaCore.Optimize(
                    descriptor, settings.FormulaPolicy.RequireGenerationContext(), strength.Budget,
                    benefitAffinity, 1d, evidence.Select(value => value.EvidenceId),
                    maximumResults: 3))
                    choices.Add(new FormulaChoice
                    {
                        Benefit = module,
                        BenefitCandidate = candidate
                    });
            }

            if (policy.FormulaVersion < 2
                || (selectedBenefitModuleId != null
                    && selectedDrawbackCapabilityId == null)) continue;
            foreach (CharacterAcquiredTraitDrawbackCapabilityDefinition drawback in
                     settings.DrawbackCapabilities.Where(value => value != null
                         && (selectedDrawbackCapabilityId == null || string.Equals(
                             value.DrawbackId, selectedDrawbackCapabilityId, StringComparison.Ordinal))
                         && !activeDrawbackIds.Contains(value.DrawbackId)
                         && !value.ConflictGroups.Any(activeDrawbackConflicts.Contains)
                         && HasQualifiedNegativeEvidence(value, selectedFacts)
                         && !ConflictsOrCancels(module, value)))
            {
                NarrativeFormulaDrawbackCreditPolicy creditPolicy =
                    settings.FormulaPolicy.RequireDrawbackPolicy();
                NarrativeFormulaDrawbackOption maximumOption = new(
                    drawback.DrawbackId,
                    drawback.MaximumCredit,
                    NarrativeFormulaDrawbackSelectionKind.Automatic,
                    reachable: true,
                    mandatory: true,
                    separatelyRemovable: false,
                    cancelsSelectedBenefit: false,
                    hasNegativeNarrativeEvidence: true);
                int severityBudget = creditPolicy.Resolve(strength.Budget, maximumOption)
                    .AcceptedCredit;
                if (severityBudget <= 0) continue;
                NarrativeFormulaCapabilityDescriptor drawbackDescriptor =
                    drawback.RequireFormulaDescriptor();
                NarrativeFormulaCandidate severity = NarrativeFormulaCore.Optimize(
                        drawbackDescriptor,
                        new NarrativeFormulaGenerationCostContext(0, false, 1),
                        severityBudget,
                        CalculateAffinity(drawbackDescriptor, evidence),
                        1d,
                        evidence.Select(value => value.EvidenceId),
                        maximumResults: 1)
                    .SingleOrDefault();
                if (severity == null || severity.PositiveCost <= 0) continue;
                NarrativeFormulaDrawbackOption option = new(
                    drawback.DrawbackId,
                    severity.PositiveCost,
                    NarrativeFormulaDrawbackSelectionKind.Automatic,
                    reachable: true,
                    mandatory: true,
                    separatelyRemovable: false,
                    cancelsSelectedBenefit: false,
                    hasNegativeNarrativeEvidence: true);
                double combinedAffinity = benefitAffinity
                    + 0.25d * CalculateAffinity(drawbackDescriptor, evidence);
                foreach (NarrativeFormulaCandidate candidate in NarrativeFormulaCore.Optimize(
                             descriptor,
                             settings.FormulaPolicy.RequireGenerationContext(),
                             strength.Budget,
                             combinedAffinity,
                             1d,
                             evidence.Select(value => value.EvidenceId),
                             maximumResults: 3,
                             drawbackPolicy: creditPolicy,
                             drawbackOption: option))
                    choices.Add(new FormulaChoice
                    {
                        Benefit = module,
                        BenefitCandidate = candidate,
                        Drawback = drawback,
                        DrawbackCandidate = severity
                    });
            }
        }
        if (choices.Count == 0) throw new InvalidOperationException("No authored acquired-trait formula capability fits the current evidence budget.");
        IReadOnlyList<NarrativeFormulaCandidate> retained = NarrativeFormulaCore.RetainNearBest(
            choices.Select(value => value.BenefitCandidate), strength.Budget);
        NarrativeFormulaCandidate winner = NarrativeFormulaCore.ChooseDeterministicWinner(
            retained, targetId, "acquired-trait:" + manifestationMilestone.ToString(CultureInfo.InvariantCulture),
            policy.FormulaVersion, catalogSha256);
        FormulaChoice selected = choices.Single(value =>
            ReferenceEquals(value.BenefitCandidate, winner));
        string selectionSignature = winner.CanonicalSignature
            + (selected.DrawbackCandidate == null ? string.Empty
                : "\nseverity=" + selected.DrawbackCandidate.CanonicalSignature);
        string signatureHash = NarrativeInferenceHash.ComputeSha256Utf8(string.Join("\n",
                targetId,
                manifestationMilestone.ToString(CultureInfo.InvariantCulture),
                policy.FormulaVersion.ToString(CultureInfo.InvariantCulture),
                catalogSha256,
                selectionSignature))
            .Substring("sha256:".Length);
        string packetHash = NarrativeInferenceHash.ComputeSha256Utf8(string.Join("\n", canonicalRequestId,
            canonicalRequestKey, targetId, manifestationMilestone.ToString(CultureInfo.InvariantCulture), signatureHash));
        List<CharacterAcquiredTraitEffectOverride> effectOverrides =
            FreezeEffectOverrides(selected.Benefit, winner, selected.Drawback,
                selected.DrawbackCandidate).ToList();
        List<CharacterAcquiredTraitFormulaCapabilityEnvelope> capabilities = winner.Allocations
            .Concat(selected.DrawbackCandidate?.Allocations
                ?? Array.Empty<NarrativeFormulaCapabilityAllocation>())
            .Select(ToEnvelope).ToList();
        return new CharacterAcquiredTraitPendingRequestState
        {
            targetPersistentId = targetId,
            requestId = canonicalRequestId,
            requestKey = canonicalRequestKey,
            candidatePacketHash = packetHash,
            submissionAuditId = "trait-formula-submission:" + signatureHash,
            manifestationMilestone = manifestationMilestone,
            evidenceFactIds = winner.EvidenceIds.OrderBy(value => value, StringComparer.Ordinal).ToList(),
            evidenceBindings = GameplayOutcomeEvidenceFormulaProjection.Select(
                outcomeBindings, winner.EvidenceIds),
            formulaVersion = policy.FormulaVersion,
            formulaCatalogSha256 = catalogSha256,
            calculatedCost = winner.CalculatedCost,
            positiveCost = winner.PositiveCost,
            drawbackCredit = winner.DrawbackCredit,
            drawbackId = winner.DrawbackId,
            narrativeBudget = strength.Budget,
            drawbackEvidenceQualified = selected.Drawback == null
                ? hasNegativeEvidence
                : HasQualifiedNegativeEvidence(selected.Drawback, selectedFacts),
            benefitModuleIds = new List<string> { selected.Benefit.ModuleId },
            drawbackCapabilityIds = selected.Drawback == null
                ? new List<string>()
                : new List<string> { selected.Drawback.DrawbackId },
            formulaCapabilities = capabilities,
            effectOverrides = effectOverrides,
            presentationId = "presentation:trait:" + signatureHash,
            mechanicalDescription = FormatMechanicalDescription(
                selected.Benefit, winner, selected.Drawback,
                selected.DrawbackCandidate, effectOverrides),
            presentationState = CharacterAcquiredTraitPresentationState.PresentationPending,
            presentationFailureCount = 0
        };
    }

    private static NarrativeFormulaEvidence[] BuildEvidence(
        CharacterNarrativeLedger ledger,
        IEnumerable<string> selectedEvidenceIds,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings = null,
        NarrativeFormulaStrengthPolicy policy = null)
    {
        HashSet<string> selected = (selectedEvidenceIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0)
            throw new InvalidOperationException("Formula acquired-trait generation requires selected public evidence.");
        IEnumerable<NarrativeFormulaEvidence> local = (ledger?.Facts ?? Array.Empty<CharacterNarrativeFact>())
        .Where(value => value != null && value.milestoneCount > 0)
        .Where(value => selected.Contains(CharacterAcquiredTraitEvidenceProjection.Project(value)))
        .Select(value => new NarrativeFormulaEvidence(
            CharacterAcquiredTraitEvidenceProjection.Project(value),
            RequireFactFormulaKey(value.eventGroupKey, value, "eventGroupKey"),
            RequireFactFormulaKey(value.actionKey, value, "actionKey"),
            value.relationshipKey?.Trim() ?? string.Empty, value.domain.ToString(),
            value.milestoneCount, value.importancePoints, value.influenceUseCount))
        .OrderBy(value => value.EvidenceId, StringComparer.Ordinal);
        IEnumerable<NarrativeFormulaEvidence> exact = (outcomeBindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null && selected.Contains(value.publicFactId))
            .Select(value => GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(
                value, policy ?? throw new InvalidOperationException(
                    "Exact acquired-trait evidence requires a formula policy.")));
        NarrativeFormulaEvidence[] result = local.Concat(exact)
            .OrderBy(value => value.EvidenceId, StringComparer.Ordinal).ToArray();
        if (result.Length != selected.Count)
            throw new InvalidOperationException("Formula acquired-trait evidence does not resolve to an exact selected public set.");
        return result;
    }

    private static HashSet<CharacterNarrativeDomain> ResolveExactDomains(
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        HashSet<CharacterNarrativeDomain> result = new();
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in bindings
                     ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
        {
            foreach (string tag in binding?.semanticTags ?? new List<string>())
            {
                if (Enum.TryParse(tag, ignoreCase: true, out CharacterNarrativeDomain domain)
                    && Enum.IsDefined(typeof(CharacterNarrativeDomain), domain))
                    result.Add(domain);
            }
        }
        return result;
    }

    private static CharacterAcquiredTraitFormulaCapabilityEnvelope ToEnvelope(NarrativeFormulaCapabilityAllocation allocation) => new()
    {
        capabilityId = allocation.Descriptor.CapabilityId,
        formatterId = allocation.Descriptor.FormatterId,
        applicatorId = allocation.Descriptor.ApplicatorId,
        parameters = allocation.Parameters.Select(value => new CharacterAcquiredTraitFormulaParameter
        { parameterId = value.ParameterId, units = value.Units }).ToList()
    };

    private static IEnumerable<CharacterAcquiredTraitEffectOverride> FreezeEffectOverrides(
        CharacterAcquiredTraitModuleSO module,
        NarrativeFormulaCandidate candidate,
        CharacterAcquiredTraitDrawbackCapabilityDefinition drawback,
        NarrativeFormulaCandidate drawbackCandidate)
    {
        float benefitScale = ResolveMagnitude(candidate);
        IEnumerable<CharacterAcquiredTraitEffectOverride> benefit = module.Effects
            .Where(value => value != null
                && !module.DrawbackBindingIds.Contains(value.bindingId,
                    StringComparer.Ordinal))
            .OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .Select(value => new CharacterAcquiredTraitEffectOverride
            {
                bindingId = value.bindingId,
                value = ScaleEffectValue(value, benefitScale)
            });
        if (drawback == null || drawbackCandidate == null) return benefit;
        float drawbackScale = ResolveMagnitude(drawbackCandidate);
        return benefit.Concat(drawback.Effects.Where(value => value != null)
            .OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .Select(value => new CharacterAcquiredTraitEffectOverride
            {
                bindingId = value.bindingId,
                value = ScaleEffectValue(value, drawbackScale)
            }));
    }

    private static float ResolveMagnitude(NarrativeFormulaCandidate candidate)
    {
        NarrativeFormulaParameterValue magnitude = candidate.Parameters.Single(value =>
            string.Equals(value.ParameterId, NarrativeFormulaParameterIds.Magnitude,
                StringComparison.Ordinal));
        return (float)candidate.Descriptor.RequireRange(
            NarrativeFormulaParameterIds.Magnitude).ToDecimal(magnitude.Units);
    }

    private static float ScaleEffectValue(GameplayEffectBinding binding, float potencyScale)
    {
        if (binding?.definition == null
            || float.IsNaN(binding.value) || float.IsInfinity(binding.value)
            || float.IsNaN(potencyScale) || float.IsInfinity(potencyScale)
            || potencyScale < 1f)
            throw new InvalidOperationException("Acquired-trait formula effect scaling input is invalid.");
        double scaled = binding.definition.Operation == GameplayEffectOperation.Multiply
            ? 1d + (binding.value - 1d) * potencyScale
            : binding.value * potencyScale;
        if (double.IsNaN(scaled) || double.IsInfinity(scaled)
            || scaled < float.MinValue || scaled > float.MaxValue)
            throw new OverflowException("Acquired-trait formula effect scaling overflowed.");
        return (float)scaled;
    }

    private static string FormatMechanicalDescription(
        CharacterAcquiredTraitModuleSO module,
        NarrativeFormulaCandidate candidate,
        CharacterAcquiredTraitDrawbackCapabilityDefinition drawback,
        NarrativeFormulaCandidate drawbackCandidate,
        IReadOnlyList<CharacterAcquiredTraitEffectOverride> overrides)
    {
        float potencyScale = ResolveMagnitude(candidate);
        string scalar = string.Join(", ", overrides.OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .Select(value =>
            {
                GameplayEffectBinding binding = module.Effects
                    .Concat(drawback?.Effects ?? Array.Empty<GameplayEffectBinding>())
                    .Single(item => item != null && string.Equals(
                        item.bindingId, value.bindingId, StringComparison.Ordinal));
                string condition = binding.condition == null
                    ? string.Empty : " if " + binding.condition.ConditionId;
                return binding.definition.TargetId + " " + binding.definition.Operation + "="
                    + value.value.ToString("0.####", CultureInfo.InvariantCulture) + condition;
            }));
        string reactions = string.Join(" ", module.SpecialReactions
            .Where(value => value != null)
            .OrderBy(value => value.ReactionId, StringComparer.Ordinal)
            .Select(value => CharacterAcquiredTraitSpecialReactionMath
                .FormatMechanicalDescription(
                    value,
                    CharacterAcquiredTraitSpecialReactionMath.ResolveActionValue(
                        value,
                        potencyScale))));
        return module.DisplayName + ": "
        + scalar
        + (scalar.Length > 0 && reactions.Length > 0 ? "; " : string.Empty)
        + reactions
        + (drawback == null ? string.Empty
            : "; drawback=" + drawback.DisplayName
                + "(" + drawback.DrawbackId + ")"
                + "; drawbackCredit=" + candidate.DrawbackCredit.ToString(CultureInfo.InvariantCulture))
        + "; potencyScale=" + candidate.Descriptor.RequireRange(NarrativeFormulaParameterIds.Magnitude)
            .Format(candidate.Parameters.Single(value => string.Equals(
                value.ParameterId, NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal)).Units)
        + "; positiveCost=" + candidate.PositiveCost.ToString(CultureInfo.InvariantCulture)
        + "; netCost=" + candidate.CalculatedCost.ToString(CultureInfo.InvariantCulture)
        + (drawbackCandidate == null ? string.Empty
            : "; drawbackSeverity=" + drawbackCandidate.Descriptor
                .RequireRange(NarrativeFormulaParameterIds.Magnitude)
                .Format(drawbackCandidate.Parameters.Single(value => string.Equals(
                    value.ParameterId, NarrativeFormulaParameterIds.Magnitude,
                    StringComparison.Ordinal)).Units));
    }

    public static string FormatModuleOfferDescription(
        CharacterAcquiredTraitModuleSO module)
    {
        if (module == null) throw new ArgumentNullException(nameof(module));
        string description = module.DisplayName + ": " + module.Description
            + FormatReactionOfferSuffix(module);
        if (ContainsMechanicalNumber(description))
            throw new InvalidOperationException(
                "Acquired-trait module-selection semantics must not expose numeric mechanics.");
        IReadOnlyList<string> forbidden = module.RequireFormulaDescriptor()
            .ForbiddenSynergies;
        return forbidden.Count == 0
            ? description
            : description + "; 함께 선택 금지 이로운 기능="
                + string.Join(",", forbidden);
    }

    private static string FormatDrawbackOfferDescription(
        CharacterAcquiredTraitDrawbackCapabilityDefinition drawback)
    {
        if (drawback == null) throw new ArgumentNullException(nameof(drawback));
        string description = drawback.DisplayName + ": " + drawback.Description;
        if (ContainsMechanicalNumber(description))
            throw new InvalidOperationException(
                "Acquired-trait drawback-selection semantics must not expose numeric mechanics.");
        IReadOnlyList<string> forbidden = drawback.RequireFormulaDescriptor()
            .ForbiddenSynergies;
        return forbidden.Count == 0
            ? description
            : description + "; 함께 선택 금지 이로운 기능="
                + string.Join(",", forbidden);
    }

    private static string FormatReactionOfferSuffix(
        CharacterAcquiredTraitModuleSO module)
    {
        string[] descriptions = module.SpecialReactions
            .Where(value => value != null)
            .OrderBy(value => value.ReactionId, StringComparer.Ordinal)
            .Select(FormatReactionOfferDescription)
            .ToArray();
        return descriptions.Length == 0
            ? string.Empty
            : " 특수 반응: " + string.Join(" ", descriptions);
    }

    private static string FormatReactionOfferDescription(
        CharacterAcquiredTraitSpecialReactionDefinition reaction)
    {
        if (reaction == null) throw new ArgumentNullException(nameof(reaction));
        string triggerText = reaction.Trigger switch
        {
            CharacterAcquiredTraitReactionTrigger.WorkCompleted =>
                reaction.Condition == CharacterAcquiredTraitReactionCondition.ProductCreated
                    ? "완성품이 있는 작업을 마치면"
                    : "작업을 마치면",
            CharacterAcquiredTraitReactionTrigger.CharacterInjured =>
                reaction.Condition == CharacterAcquiredTraitReactionCondition.WorkAccident
                    ? "작업 사고로 다치면"
                    : "부상을 입으면",
            CharacterAcquiredTraitReactionTrigger.ExpeditionOutcome =>
                reaction.Condition == CharacterAcquiredTraitReactionCondition.ExpeditionSucceeded
                    ? "원정에 성공하면"
                    : "원정이 끝나면",
            CharacterAcquiredTraitReactionTrigger.ApologyCompleted =>
                reaction.Condition == CharacterAcquiredTraitReactionCondition.RestitutionProvided
                    ? "배상을 포함한 사과를 받으면"
                    : "사과를 받으면",
            _ => throw new ArgumentOutOfRangeException(nameof(reaction))
        };
        string actionText = reaction.Action switch
        {
            CharacterAcquiredTraitReactionAction.GrantExperience => "경험을 쌓습니다",
            CharacterAcquiredTraitReactionAction.ApplyMoodImpulse => "기분이 좋아집니다",
            _ => throw new ArgumentOutOfRangeException(nameof(reaction))
        };
        bool limited = reaction.MaximumTriggersPerDay > 0
            || reaction.CooldownDays > 0
            || reaction.MaximumLifetimeTriggers > 0;
        return triggerText + " " + actionText + "."
            + (limited ? " 발동 횟수나 재사용 간격에는 제한이 있습니다." : string.Empty);
    }

    private static bool ContainsMechanicalNumber(string value) =>
        (value ?? string.Empty).Any(character => character is >= '0' and <= '9'
            or >= '０' and <= '９' or '%' or '％');

    private static double CalculateAffinity(
        NarrativeFormulaCapabilityDescriptor descriptor,
        IEnumerable<NarrativeFormulaEvidence> evidence) =>
        descriptor.NarrativeAffinity + descriptor.AffinityKeys.Count(key =>
            evidence.Any(item => string.Equals(item.DomainKey, key, StringComparison.Ordinal)
                || string.Equals(item.EventGroupKey, key, StringComparison.Ordinal)
                || string.Equals(item.ActionKey, key, StringComparison.Ordinal)));

    private static bool HasQualifiedNegativeEvidence(
        CharacterAcquiredTraitDrawbackCapabilityDefinition drawback,
        IEnumerable<CharacterNarrativeFact> selectedFacts) =>
        selectedFacts.Any(value => value != null
            && drawback.DomainAffinities.Contains(value.domain)
            && NarrativeFormulaNegativeEvidence.Matches(value.outcome, value.factId));

    private static bool ConflictsOrCancels(
        CharacterAcquiredTraitModuleSO benefit,
        CharacterAcquiredTraitDrawbackCapabilityDefinition drawback)
    {
        if (benefit.ConflictGroups.Intersect(
                drawback.ConflictGroups, StringComparer.Ordinal).Any())
            return true;
        GameplayEffectBinding[] benefits = benefit.Effects.Where(value => value != null
            && !benefit.DrawbackBindingIds.Contains(value.bindingId,
                StringComparer.Ordinal)).ToArray();
        return benefits.Any(left => drawback.Effects.Any(right => right != null
            && left.definition != null && right.definition != null
            && string.Equals(left.definition.TargetId, right.definition.TargetId,
                StringComparison.Ordinal)
            && string.Equals(left.condition?.ConditionId ?? string.Empty,
                right.condition?.ConditionId ?? string.Empty, StringComparison.Ordinal)));
    }

    private static string RequireFactFormulaKey(string value, CharacterNarrativeFact fact, string field)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new InvalidOperationException($"Narrative fact '{fact.factId}' lacks canonical {field} formula metadata.");
        return canonical;
    }
    private static string RequireCanonical(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException("A canonical non-empty value is required.", name);
        return canonical;
    }
}

[Serializable]
public sealed class CharacterAcquiredTraitFormulaPresentationDto : ILlmJsonPayload
{
    public string presentationId = string.Empty;
    public string displayName = string.Empty;
    public string narrativeFlavor = string.Empty;

    public bool Validate(out string error)
    {
        const string prefix = "presentation:trait:";
        error = string.Empty;
        if (presentationId == null || presentationId.Length != prefix.Length + 64
            || !presentationId.StartsWith(prefix, StringComparison.Ordinal)
            || presentationId.Skip(prefix.Length).Any(value =>
                !((value >= '0' && value <= '9')
                  || (value >= 'a' && value <= 'f'))))
        {
            error = "Acquired-trait presentationId must be canonical presentation:trait:<sha256>.";
            return false;
        }
        if (!IsText(displayName, 32) || !IsText(narrativeFlavor, 180))
        {
            error = "Acquired-trait presentation fields are missing, non-canonical, or exceed their limit.";
            return false;
        }
        if ((displayName + narrativeFlavor).Any(value => value is >= '0' and <= '9'
                or >= '０' and <= '９' or '%' or '％'))
        {
            error = "Acquired-trait presentation cannot restate mechanical numbers or percentages.";
            return false;
        }
        return true;
    }

    private static bool IsText(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && value.Length <= maximumLength;
}

public static class CharacterAcquiredTraitFormulaPresentation
{
    public static bool TryValidate(
        CharacterAcquiredTraitPendingRequestState pending,
        string responseJson,
        out CharacterAcquiredTraitFormulaPresentationDto presentation,
        out string error)
    {
        presentation = null;
        error = string.Empty;
        if (pending == null || pending.formulaVersion < 1
            || pending.presentationState != CharacterAcquiredTraitPresentationState.PresentationPending)
        {
            error = "Acquired-trait formula presentation is not pending.";
            return false;
        }
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                "AcquiredTrait", responseJson, out _, out error)
            || !LlmJsonResponseParser.TryParse(responseJson, out CharacterAcquiredTraitFormulaPresentationDto parsed, out error))
            return false;
        if (!string.Equals(parsed.presentationId, pending.presentationId, StringComparison.Ordinal))
        {
            error = "Acquired-trait presentationId does not match frozen mechanics.";
            return false;
        }
        presentation = parsed;
        return true;
    }
}

[Serializable]
public sealed class CharacterAcquiredTraitModuleSelectionResponseDto : ILlmJsonPayload
{
    public string selectionId = string.Empty;
    public List<string> positiveModuleIds = new();
    public List<string> drawbackModuleIds = new();
    public List<string> evidenceFactIds = new();
    public string displayName = string.Empty;
    public string narrativeFlavor = string.Empty;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(selectionId)
            || positiveModuleIds == null || drawbackModuleIds == null
            || evidenceFactIds == null || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(narrativeFlavor))
        {
            error = "The six acquired-trait module-selection fields are required.";
            return false;
        }
        if (!string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal)
            || displayName.Length > 32
            || !string.Equals(narrativeFlavor, narrativeFlavor.Trim(), StringComparison.Ordinal)
            || narrativeFlavor.Length > 180
            || (displayName + narrativeFlavor).Any(value => value is >= '0' and <= '9'
                or >= '０' and <= '９' or '%' or '％'))
        {
            error = "Acquired-trait presentation text is non-canonical, too long, or contains mechanical numbers.";
            return false;
        }
        return true;
    }
}

public static class CharacterAcquiredTraitModuleSelectionResponse
{
    public static bool TryValidate(
        CharacterAcquiredTraitPendingRequestState pending,
        NarrativeFormulaModuleSelectionRequest request,
        string responseJson,
        out CharacterAcquiredTraitModuleSelectionResponseDto response,
        out NarrativeFormulaValidatedModuleSelection selection,
        out string error)
    {
        response = null;
        selection = null;
        if (pending == null || request == null
            || pending.presentationState != CharacterAcquiredTraitPresentationState.ModuleSelectionPending
            || !string.Equals(pending.moduleSelectionId, request.SelectionId, StringComparison.Ordinal))
        {
            error = "Acquired-trait module selection is not pending or its offer is stale.";
            return false;
        }
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                responseJson, out _, out error)
            || !LlmJsonResponseParser.TryParse(responseJson,
                out CharacterAcquiredTraitModuleSelectionResponseDto parsed, out error))
            return false;
        NarrativeFormulaModuleSelectionChoice choice = new(
            parsed.selectionId, parsed.positiveModuleIds,
            parsed.drawbackModuleIds, parsed.evidenceFactIds);
        if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                request, choice, out selection, out error))
            return false;
        response = parsed;
        return true;
    }
}

public static class CharacterAcquiredTraitRequestPacketAuthority
{
    public static bool TryBuild(
        CharacterAcquiredTraitSubmissionCommand command,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitAggregateState state,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        out CharacterAcquiredTraitRequestPacketDto packet,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        packet = null;
        if (command == null)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidCommand;
            error = "Acquired-trait submission command is missing.";
            return false;
        }
        return TryBuild(
            command.TargetPersistentId,
            command.RequestId,
            command.RequestKey,
            command.ManifestationMilestone,
            command.EvidenceFactIds,
            ledger,
            state,
            settings,
            moduleDefinitions,
            out packet,
            out issueCode,
            out error);
    }

    public static bool TryValidate(
        CharacterAcquiredTraitRequestPacketDto packet,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitAggregateState state,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        if (packet == null)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            error = "Acquired-trait request packet is missing.";
            return false;
        }

        CharacterAcquiredTraitModuleSO[] modules = (moduleDefinitions
                ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .ToArray();
        if (!TryRequireDefinitions(
                settings,
                modules,
                out Dictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
                out issueCode,
                out error))
        {
            return false;
        }
        if (!TryResolveActiveConstraints(
                state,
                ledger,
                settings,
                modules,
                modulesById,
                out ActiveTraitConstraints activeConstraints,
                out issueCode,
                out error))
        {
            return false;
        }
        if (!TryRequireCanonicalIdentifier(packet.settingsId, "settingsId", out error)
            || !TryRequireCanonicalIdentifier(
                packet.targetPersistentId,
                "targetPersistentId",
                out error)
            || !TryRequireCanonicalIdentifier(packet.requestId, "requestId", out error)
            || !TryRequireCanonicalIdentifier(packet.requestKey, "requestKey", out error))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            return false;
        }
        if (!settings.TryGetGate(
                packet.manifestationMilestone,
                out CharacterAcquiredTraitManifestationGateDefinition gate))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidMilestone;
            error = $"Acquired-trait request milestone {packet.manifestationMilestone} is not authored.";
            return false;
        }
        if (!string.Equals(packet.settingsId, settings.SettingsId, StringComparison.Ordinal)
            || !string.Equals(packet.rarity, gate.Rarity.ToString(), StringComparison.Ordinal)
            || packet.budget != gate.Budget
            || packet.maximumActiveTraits != settings.MaximumActiveTraits)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            error = "Acquired-trait request packet does not match current settings authority.";
            return false;
        }

        if (!TryResolveEvidenceDomains(
                packet.evidenceFactIds,
                ledger,
                packet.manifestationMilestone,
                out string[] evidence,
                out string[] eligibleDomains,
                out issueCode,
                out error))
        {
            return false;
        }
        if (!SequenceEqual(packet.evidenceFactIds, evidence)
            || !SequenceEqual(packet.eligibleDomains, eligibleDomains))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            error = "Acquired-trait packet evidence or eligible domains are non-canonical.";
            return false;
        }

        if (packet.modules == null || packet.modules.Any(value => value == null))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            error = "Acquired-trait packet module catalog is missing or contains null.";
            return false;
        }
        if (packet.modules.GroupBy(value => value.moduleId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.DuplicateModule;
            error = "Acquired-trait packet module catalog contains a duplicate module ID.";
            return false;
        }
        foreach (CharacterAcquiredTraitModulePacketDto modulePacket in packet.modules)
        {
            if (!modulesById.TryGetValue(
                    modulePacket.moduleId ?? string.Empty,
                    out CharacterAcquiredTraitModuleSO module))
            {
                issueCode = CharacterAcquiredTraitInferenceIssueCode.UnknownModule;
                error = $"Acquired-trait packet references unknown module '{modulePacket.moduleId ?? string.Empty}'.";
                return false;
            }
            if (!TryAllowAgainstActive(
                    module,
                    activeConstraints,
                    out issueCode,
                    out error))
            {
                return false;
            }
            if (!ModuleMatches(modulePacket, module))
            {
                issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
                error = $"Acquired-trait packet module '{module.ModuleId}' differs from authored content.";
                return false;
            }
        }

        if (packet.combinationOptions == null
            || packet.combinationOptions.Count == 0
            || packet.combinationOptions.Any(value => value == null))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidCombination;
            error = "Acquired-trait packet requires at least one non-null legal combination.";
            return false;
        }
        if (packet.combinationOptions
            .GroupBy(value => value.combinationId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.DuplicateCombination;
            error = "Acquired-trait packet contains a duplicate combination ID.";
            return false;
        }

        HashSet<string> eligibleDomainSet = eligibleDomains.ToHashSet(StringComparer.Ordinal);
        HashSet<string> combinationShapes = new(StringComparer.Ordinal);
        foreach (CharacterAcquiredTraitCombinationPacketDto option
                 in packet.combinationOptions)
        {
            if (!TryValidateOption(
                    option,
                    gate,
                    modulesById,
                    eligibleDomainSet,
                    activeConstraints,
                    out issueCode,
                    out error))
            {
                return false;
            }
            if (!combinationShapes.Add(string.Join("|", option.moduleIds)))
            {
                issueCode = CharacterAcquiredTraitInferenceIssueCode.DuplicateCombination;
                error = "Acquired-trait packet repeats the same mechanical module combination.";
                return false;
            }
        }

        string packetHash = ComputeHash(packet);
        if (!string.Equals(
                packet.candidatePacketHash,
                packetHash,
                StringComparison.Ordinal))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            error = "Acquired-trait packet hash does not match its canonical contents.";
            return false;
        }

        if (!TryBuild(
                packet.targetPersistentId,
                packet.requestId,
                packet.requestKey,
                packet.manifestationMilestone,
                packet.evidenceFactIds,
                ledger,
                state,
                settings,
                modules,
                out CharacterAcquiredTraitRequestPacketDto expected,
                out issueCode,
                out error))
        {
            return false;
        }
        if (!string.Equals(
                expected.candidatePacketHash,
                packet.candidatePacketHash,
                StringComparison.Ordinal))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            error = "Acquired-trait packet is not the complete legal packet from current C# authority.";
            return false;
        }

        issueCode = CharacterAcquiredTraitInferenceIssueCode.None;
        error = string.Empty;
        return true;
    }

    public static string ComputeHash(CharacterAcquiredTraitRequestPacketDto packet)
    {
        if (packet == null)
            throw new ArgumentNullException(nameof(packet));

        StringBuilder builder = new();
        Append(builder, packet.settingsId);
        Append(builder, packet.targetPersistentId);
        Append(builder, packet.requestId);
        Append(builder, packet.requestKey);
        Append(builder, packet.manifestationMilestone);
        Append(builder, packet.rarity);
        Append(builder, packet.budget);
        Append(builder, packet.maximumActiveTraits);
        Append(builder, packet.eligibleDomains);
        foreach (CharacterAcquiredTraitModulePacketDto module
                 in packet.modules ?? new List<CharacterAcquiredTraitModulePacketDto>())
        {
            Append(builder, module?.moduleId);
            Append(builder, module?.displayName);
            Append(builder, module?.description);
            Append(builder, module?.cost ?? int.MinValue);
            Append(builder, module?.domainAffinities);
            Append(builder, module?.conflictGroups);
        }
        foreach (CharacterAcquiredTraitCombinationPacketDto option
                 in packet.combinationOptions
                    ?? new List<CharacterAcquiredTraitCombinationPacketDto>())
        {
            Append(builder, option?.combinationId);
            Append(builder, option?.moduleIds);
            Append(builder, option?.totalCost ?? int.MinValue);
            Append(builder, option?.domainAffinities);
            Append(builder, option?.conflictGroups);
        }
        Append(builder, packet.evidenceFactIds);
        return NarrativeInferenceHash.ComputeSha256Utf8(builder.ToString());
    }

    private static bool TryBuild(
        string targetPersistentId,
        string requestId,
        string requestKey,
        int manifestationMilestone,
        IEnumerable<string> evidenceFactIds,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitAggregateState state,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        out CharacterAcquiredTraitRequestPacketDto packet,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        packet = null;
        if (!TryRequireCanonicalIdentifier(
                targetPersistentId,
                "targetPersistentId",
                out error)
            || !TryRequireCanonicalIdentifier(requestId, "requestId", out error)
            || !TryRequireCanonicalIdentifier(requestKey, "requestKey", out error))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidCommand;
            return false;
        }

        CharacterAcquiredTraitModuleSO[] modules = (moduleDefinitions
                ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .ToArray();
        if (!TryRequireDefinitions(
                settings,
                modules,
                out Dictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
                out issueCode,
                out error))
        {
            return false;
        }
        if (!TryResolveActiveConstraints(
                state,
                ledger,
                settings,
                modules,
                modulesById,
                out ActiveTraitConstraints activeConstraints,
                out issueCode,
                out error))
        {
            return false;
        }
        if (!settings.TryGetGate(
                manifestationMilestone,
                out CharacterAcquiredTraitManifestationGateDefinition gate))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidMilestone;
            error = $"Acquired-trait milestone {manifestationMilestone} is not authored.";
            return false;
        }
        if (!TryResolveEvidenceDomains(
                evidenceFactIds,
                ledger,
                manifestationMilestone,
                out string[] evidence,
                out string[] eligibleDomains,
                out issueCode,
                out error))
        {
            return false;
        }

        HashSet<string> domainSet = eligibleDomains.ToHashSet(StringComparer.Ordinal);
        CharacterAcquiredTraitModuleSO[] domainEligibleModules = modules
            .Where(value => value != null
                && value.DomainAffinities.Any(domain =>
                    domainSet.Contains(domain.ToString())))
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        if (domainEligibleModules.Length == 0)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.NoEligibleDomain;
            error = "No acquired-trait module is affiliated with the submitted evidence domains.";
            return false;
        }
        CharacterAcquiredTraitModuleSO[] eligibleModules = domainEligibleModules
            .Where(value => IsAllowedAgainstActive(value, activeConstraints))
            .ToArray();
        if (eligibleModules.Length == 0)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.ActiveConflict;
            error = "All domain-eligible acquired-trait modules conflict with active acquired traits.";
            return false;
        }

        List<CharacterAcquiredTraitCombinationPacketDto> combinations = new();
        for (int size = CharacterAcquiredTraitCombinationIdentity.MinimumModuleCount;
             size <= CharacterAcquiredTraitCombinationIdentity.MaximumModuleCount;
             size++)
        {
            BuildCombinations(
                eligibleModules,
                gate,
                domainSet,
                size,
                0,
                new List<CharacterAcquiredTraitModuleSO>(size),
                combinations);
        }
        combinations = combinations
            .OrderBy(value => value.moduleIds.Count)
            .ThenBy(value => string.Join("|", value.moduleIds), StringComparer.Ordinal)
            .ToList();
        if (combinations.Count == 0)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidCombination;
            error = "Current acquired-trait budget, conflicts, and domains produce no legal combination.";
            return false;
        }

        packet = new CharacterAcquiredTraitRequestPacketDto
        {
            settingsId = settings.SettingsId,
            targetPersistentId = targetPersistentId,
            requestId = requestId,
            requestKey = requestKey,
            manifestationMilestone = manifestationMilestone,
            rarity = gate.Rarity.ToString(),
            budget = gate.Budget,
            maximumActiveTraits = settings.MaximumActiveTraits,
            eligibleDomains = eligibleDomains.ToList(),
            modules = eligibleModules.Select(ToPacket).ToList(),
            combinationOptions = combinations,
            evidenceFactIds = evidence.ToList()
        };
        packet.candidatePacketHash = ComputeHash(packet);
        issueCode = CharacterAcquiredTraitInferenceIssueCode.None;
        error = string.Empty;
        return true;
    }

    private static void BuildCombinations(
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        CharacterAcquiredTraitManifestationGateDefinition gate,
        ISet<string> eligibleDomains,
        int requiredCount,
        int nextIndex,
        List<CharacterAcquiredTraitModuleSO> selected,
        ICollection<CharacterAcquiredTraitCombinationPacketDto> output)
    {
        if (selected.Count == requiredCount)
        {
            if (selected.Sum(value => (long)value.Cost) > gate.Budget
                || HasConflict(selected)
                || selected.Any(value => !value.DomainAffinities.Any(domain =>
                    eligibleDomains.Contains(domain.ToString()))))
            {
                return;
            }

            string[] moduleIds = selected
                .Select(value => value.ModuleId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            output.Add(new CharacterAcquiredTraitCombinationPacketDto
            {
                combinationId = CharacterAcquiredTraitCombinationIdentity.Build(moduleIds),
                moduleIds = moduleIds.ToList(),
                totalCost = selected.Sum(value => value.Cost),
                domainAffinities = selected
                    .SelectMany(value => value.DomainAffinities)
                    .Select(value => value.ToString())
                    .Where(eligibleDomains.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList(),
                conflictGroups = selected
                    .SelectMany(value => value.ConflictGroups)
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList()
            });
            return;
        }

        int remaining = requiredCount - selected.Count;
        for (int index = nextIndex; index <= modules.Count - remaining; index++)
        {
            selected.Add(modules[index]);
            BuildCombinations(
                modules,
                gate,
                eligibleDomains,
                requiredCount,
                index + 1,
                selected,
                output);
            selected.RemoveAt(selected.Count - 1);
        }
    }

    private static bool TryValidateOption(
        CharacterAcquiredTraitCombinationPacketDto option,
        CharacterAcquiredTraitManifestationGateDefinition gate,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
        ISet<string> eligibleDomains,
        ActiveTraitConstraints activeConstraints,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        if (option.moduleIds == null
            || option.moduleIds.Count
                is < CharacterAcquiredTraitCombinationIdentity.MinimumModuleCount
                or > CharacterAcquiredTraitCombinationIdentity.MaximumModuleCount)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidCombination;
            error = "Acquired-trait combination must contain one to three modules.";
            return false;
        }
        if (option.moduleIds.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidCombination;
            error = "Acquired-trait combination contains an empty or padded module ID.";
            return false;
        }
        if (option.moduleIds.Distinct(StringComparer.Ordinal).Count()
            != option.moduleIds.Count)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.DuplicateModule;
            error = "Acquired-trait combination contains a duplicate module.";
            return false;
        }
        if (!option.moduleIds.SequenceEqual(
                option.moduleIds.OrderBy(value => value, StringComparer.Ordinal)))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidCombination;
            error = "Acquired-trait combination module IDs are not canonical.";
            return false;
        }

        List<CharacterAcquiredTraitModuleSO> selected = new(option.moduleIds.Count);
        foreach (string moduleId in option.moduleIds)
        {
            if (!modulesById.TryGetValue(moduleId, out CharacterAcquiredTraitModuleSO module))
            {
                issueCode = CharacterAcquiredTraitInferenceIssueCode.UnknownModule;
                error = $"Acquired-trait combination references unknown module '{moduleId}'.";
                return false;
            }
            selected.Add(module);
        }
        foreach (CharacterAcquiredTraitModuleSO module in selected)
        {
            if (!TryAllowAgainstActive(
                    module,
                    activeConstraints,
                    out issueCode,
                    out error))
            {
                return false;
            }
        }
        string expectedId = CharacterAcquiredTraitCombinationIdentity.Build(option.moduleIds);
        if (!string.Equals(option.combinationId, expectedId, StringComparison.Ordinal))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidCombination;
            error = $"Acquired-trait combination '{option.combinationId ?? string.Empty}' has a non-canonical ID.";
            return false;
        }

        long totalCost = selected.Sum(value => (long)value.Cost);
        if (totalCost > gate.Budget)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.BudgetExceeded;
            error = $"Acquired-trait combination '{option.combinationId}' costs {totalCost}, above budget {gate.Budget}.";
            return false;
        }
        if (totalCost != option.totalCost)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            error = $"Acquired-trait combination '{option.combinationId}' has a forged total cost.";
            return false;
        }
        if (HasConflict(selected))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.Conflict;
            error = $"Acquired-trait combination '{option.combinationId}' violates a conflict group or effect binding identity.";
            return false;
        }
        if (selected.Any(value => !value.DomainAffinities.Any(domain =>
                eligibleDomains.Contains(domain.ToString()))))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.DomainMismatch;
            error = $"Acquired-trait combination '{option.combinationId}' contains a module outside the evidence domains.";
            return false;
        }

        string[] expectedDomains = selected
            .SelectMany(value => value.DomainAffinities)
            .Select(value => value.ToString())
            .Where(eligibleDomains.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedConflicts = selected
            .SelectMany(value => value.ConflictGroups)
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!SequenceEqual(option.domainAffinities, expectedDomains)
            || !SequenceEqual(option.conflictGroups, expectedConflicts))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.PacketMismatch;
            error = $"Acquired-trait combination '{option.combinationId}' has forged domain or conflict metadata.";
            return false;
        }

        issueCode = CharacterAcquiredTraitInferenceIssueCode.None;
        error = string.Empty;
        return true;
    }

    private static bool TryResolveEvidenceDomains(
        IEnumerable<string> evidenceFactIds,
        CharacterNarrativeLedger ledger,
        int manifestationMilestone,
        out string[] evidence,
        out string[] eligibleDomains,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        evidence = (evidenceFactIds ?? Array.Empty<string>()).ToArray();
        eligibleDomains = Array.Empty<string>();
        if (!CharacterAcquiredTraitExperienceScore.TryCalculate(
                ledger,
                out int experienceScore,
                out error))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidEvidence;
            return false;
        }
        if (manifestationMilestone <= 0)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidMilestone;
            error = "Acquired-trait milestone must be positive.";
            return false;
        }
        if (experienceScore < manifestationMilestone)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.MilestoneNotReached;
            error = $"Acquired-trait experience score {experienceScore} has not reached milestone {manifestationMilestone}.";
            return false;
        }
        if (evidence.Length == 0
            || evidence.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidEvidence;
            error = "Acquired-trait request requires non-empty canonical evidence fact IDs.";
            return false;
        }
        if (evidence.Distinct(StringComparer.Ordinal).Count() != evidence.Length)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.DuplicateEvidence;
            error = "Acquired-trait request evidence contains a duplicate fact ID.";
            return false;
        }
        evidence = evidence.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        List<CharacterNarrativeFact> resolvedFacts = new();
        foreach (string evidenceId in evidence)
        {
            if (!CharacterAcquiredTraitEvidenceProjection.TryResolve(
                    ledger,
                    evidenceId,
                    out CharacterNarrativeFact[] resolved,
                    out error))
            {
                issueCode = CharacterAcquiredTraitInferenceIssueCode.EvidenceForgery;
                return false;
            }
            resolvedFacts.AddRange(resolved);
        }
        CharacterNarrativeFact[] facts = resolvedFacts
            .Where(value => value != null && value.milestoneCount > 0)
            .GroupBy(
                CharacterAcquiredTraitEvidenceProjection.Project,
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (facts.Any(value => !Enum.IsDefined(typeof(CharacterNarrativeDomain), value.domain)))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.NoEligibleDomain;
            error = "Acquired-trait evidence contains an unauthored narrative domain.";
            return false;
        }
        eligibleDomains = facts
            .Select(value => value.domain.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (eligibleDomains.Length == 0)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.NoEligibleDomain;
            error = "Acquired-trait evidence has no eligible narrative domain.";
            return false;
        }

        issueCode = CharacterAcquiredTraitInferenceIssueCode.None;
        error = string.Empty;
        return true;
    }

    private static bool TryRequireDefinitions(
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyCollection<CharacterAcquiredTraitModuleSO> modules,
        out Dictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        modulesById = null;
        if (settings == null)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidSettings;
            error = "Acquired-trait settings authority is missing.";
            return false;
        }
        IReadOnlyList<string> settingErrors = settings.ValidateDefinition();
        if (settingErrors.Count > 0)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidSettings;
            error = string.Join(" | ", settingErrors);
            return false;
        }
        if (modules == null || modules.Count == 0 || modules.Any(value => value == null))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidModuleDefinition;
            error = "Acquired-trait module authority is empty or contains null.";
            return false;
        }
        if (modules.GroupBy(value => value.ModuleId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.DuplicateModule;
            error = "Acquired-trait module authority contains a duplicate module ID.";
            return false;
        }
        foreach (CharacterAcquiredTraitModuleSO module in modules)
        {
            IReadOnlyList<string> moduleErrors = module.ValidateDefinition();
            if (moduleErrors.Count > 0)
            {
                issueCode = CharacterAcquiredTraitInferenceIssueCode.InvalidModuleDefinition;
                error = string.Join(" | ", moduleErrors);
                return false;
            }
        }
        modulesById = modules.ToDictionary(value => value.ModuleId, StringComparer.Ordinal);
        issueCode = CharacterAcquiredTraitInferenceIssueCode.None;
        error = string.Empty;
        return true;
    }

    private static CharacterAcquiredTraitModulePacketDto ToPacket(
        CharacterAcquiredTraitModuleSO module) => new()
    {
        moduleId = module.ModuleId,
        displayName = module.DisplayName,
        description = module.Description,
        cost = module.Cost,
        domainAffinities = module.DomainAffinities
            .Select(value => value.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList(),
        conflictGroups = module.ConflictGroups
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList()
    };

    private static bool ModuleMatches(
        CharacterAcquiredTraitModulePacketDto packet,
        CharacterAcquiredTraitModuleSO module)
    {
        CharacterAcquiredTraitModulePacketDto expected = ToPacket(module);
        return string.Equals(packet.moduleId, expected.moduleId, StringComparison.Ordinal)
            && string.Equals(packet.displayName, expected.displayName, StringComparison.Ordinal)
            && string.Equals(packet.description, expected.description, StringComparison.Ordinal)
            && packet.cost == expected.cost
            && SequenceEqual(packet.domainAffinities, expected.domainAffinities)
            && SequenceEqual(packet.conflictGroups, expected.conflictGroups);
    }

    private static bool HasConflict(
        IEnumerable<CharacterAcquiredTraitModuleSO> selected)
    {
        CharacterAcquiredTraitModuleSO[] modules = selected.ToArray();
        bool conflictGroup = modules
            .SelectMany(value => value.ConflictGroups)
            .Select(value => value.Trim())
            .GroupBy(value => value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        bool effectBinding = modules
            .SelectMany(value => value.Effects)
            .Where(value => value != null)
            .Select(value => value.bindingId?.Trim() ?? string.Empty)
            .GroupBy(value => value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        return conflictGroup || effectBinding;
    }

    private sealed class ActiveTraitConstraints
    {
        public HashSet<string> ModuleIds { get; } = new(StringComparer.Ordinal);
        public HashSet<string> ConflictGroups { get; } = new(StringComparer.Ordinal);
        public HashSet<string> EffectBindingIds { get; } = new(StringComparer.Ordinal);
    }

    private static bool TryResolveActiveConstraints(
        CharacterAcquiredTraitAggregateState state,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyCollection<CharacterAcquiredTraitModuleSO> modules,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
        out ActiveTraitConstraints constraints,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        constraints = null;
        if (state == null)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.StateValidationFailed;
            error = "Acquired-trait packet authority requires current aggregate state.";
            return false;
        }
        IReadOnlyList<CharacterAcquiredTraitValidationIssue> stateIssues =
            CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(
                state,
                ledger,
                settings,
                modules);
        if (stateIssues.Count > 0)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.StateValidationFailed;
            error = "Acquired-trait packet authority rejected current state: "
                + string.Join(" | ", stateIssues);
            return false;
        }

        ActiveTraitConstraints resolved = new();
        foreach (CharacterAcquiredTraitInstanceState instance
                 in state.instances.Where(value => value != null && !value.erased))
        {
            foreach (string moduleId in instance.moduleIds)
            {
                if (!modulesById.TryGetValue(
                        moduleId,
                        out CharacterAcquiredTraitModuleSO module))
                {
                    issueCode = CharacterAcquiredTraitInferenceIssueCode.StateValidationFailed;
                    error = $"Active acquired-trait instance '{instance.instanceId}' references unknown module '{moduleId}'.";
                    return false;
                }
                if (!resolved.ModuleIds.Add(moduleId))
                {
                    issueCode = CharacterAcquiredTraitInferenceIssueCode.StateValidationFailed;
                    error = $"Active acquired traits already repeat module '{moduleId}'.";
                    return false;
                }
                foreach (string conflictGroup in module.ConflictGroups)
                {
                    if (!resolved.ConflictGroups.Add(conflictGroup))
                    {
                        issueCode = CharacterAcquiredTraitInferenceIssueCode.StateValidationFailed;
                        error = $"Active acquired traits already conflict on group '{conflictGroup}'.";
                        return false;
                    }
                }
                foreach (GameplayEffectBinding effect in module.Effects)
                {
                    string bindingId = effect.bindingId.Trim();
                    if (!resolved.EffectBindingIds.Add(bindingId))
                    {
                        issueCode = CharacterAcquiredTraitInferenceIssueCode.StateValidationFailed;
                        error = $"Active acquired traits already conflict on effect binding '{bindingId}'.";
                        return false;
                    }
                }
            }
        }

        constraints = resolved;
        issueCode = CharacterAcquiredTraitInferenceIssueCode.None;
        error = string.Empty;
        return true;
    }

    private static bool IsAllowedAgainstActive(
        CharacterAcquiredTraitModuleSO module,
        ActiveTraitConstraints constraints) =>
        !constraints.ModuleIds.Contains(module.ModuleId)
        && !module.ConflictGroups.Any(constraints.ConflictGroups.Contains)
        && !module.Effects.Any(value =>
            constraints.EffectBindingIds.Contains(value.bindingId.Trim()));

    private static bool TryAllowAgainstActive(
        CharacterAcquiredTraitModuleSO module,
        ActiveTraitConstraints constraints,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        if (constraints.ModuleIds.Contains(module.ModuleId))
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.ActiveModuleAlreadyPresent;
            error = $"Acquired-trait module '{module.ModuleId}' is already present in an active acquired trait.";
            return false;
        }
        string conflictGroup = module.ConflictGroups
            .FirstOrDefault(constraints.ConflictGroups.Contains);
        if (conflictGroup != null)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.ActiveConflict;
            error = $"Acquired-trait module '{module.ModuleId}' conflicts with active group '{conflictGroup}'.";
            return false;
        }
        string bindingId = module.Effects
            .Select(value => value.bindingId.Trim())
            .FirstOrDefault(constraints.EffectBindingIds.Contains);
        if (bindingId != null)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.ActiveConflict;
            error = $"Acquired-trait module '{module.ModuleId}' conflicts with active effect binding '{bindingId}'.";
            return false;
        }
        issueCode = CharacterAcquiredTraitInferenceIssueCode.None;
        error = string.Empty;
        return true;
    }

    private static bool TryRequireCanonicalIdentifier(
        string value,
        string name,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            error = $"Acquired-trait {name} is missing or non-canonical.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool SequenceEqual(
        IEnumerable<string> actual,
        IEnumerable<string> expected) =>
        actual != null
        && expected != null
        && actual.SequenceEqual(expected, StringComparer.Ordinal);

    private static void Append(StringBuilder builder, int value) =>
        Append(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, IEnumerable<string> values)
    {
        string[] materialized = (values ?? Array.Empty<string>()).ToArray();
        Append(builder, materialized.Length);
        foreach (string value in materialized)
            Append(builder, value);
    }

    private static void Append(StringBuilder builder, string value)
    {
        string normalized = value ?? string.Empty;
        builder.Append(normalized.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(normalized);
        builder.Append(';');
    }
}

public static class CharacterAcquiredTraitResponseAuthority
{
    public static bool TryValidate(
        CharacterAcquiredTraitRequestPacketDto packet,
        string responseJson,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitAggregateState state,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions,
        out CharacterAcquiredTraitResponseDto response,
        out CharacterAcquiredTraitCombinationPacketDto selected,
        out CharacterAcquiredTraitInferenceIssueCode issueCode,
        out string error)
    {
        response = null;
        selected = null;
        if (!CharacterAcquiredTraitRequestPacketAuthority.TryValidate(
                packet,
                ledger,
                state,
                settings,
                moduleDefinitions,
                out issueCode,
                out error))
        {
            return false;
        }
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTraitLegacyV2.Id,
                responseJson,
                out _,
                out string contractError))
        {
            issueCode = ClassifyContractError(contractError);
            error = contractError;
            return false;
        }
        if (!LlmJsonResponseParser.TryParse(
                LocalLlmRequestProfiles.AcquiredTraitLegacyV2.Id,
                responseJson,
                out CharacterAcquiredTraitResponseDto parsedResponse,
                out error))
        {
            issueCode = error.IndexOf("duplicate fact ID", StringComparison.OrdinalIgnoreCase) >= 0
                ? CharacterAcquiredTraitInferenceIssueCode.DuplicateEvidence
                : error.IndexOf("narrative fields", StringComparison.OrdinalIgnoreCase) >= 0
                    || error.IndexOf("combinationId", StringComparison.OrdinalIgnoreCase) >= 0
                    ? CharacterAcquiredTraitInferenceIssueCode.InvalidNarrative
                    : CharacterAcquiredTraitInferenceIssueCode.ResponseSchemaInvalid;
            return false;
        }
        selected = packet.combinationOptions.SingleOrDefault(value => value != null
            && string.Equals(
                value.combinationId,
                parsedResponse.combinationId,
                StringComparison.Ordinal));
        if (selected == null)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.UnknownCombination;
            error = $"Acquired-trait response selected unknown combination '{parsedResponse.combinationId}'.";
            return false;
        }

        HashSet<string> allowedEvidence = packet.evidenceFactIds
            .ToHashSet(StringComparer.Ordinal);
        string forged = parsedResponse.evidenceFactIds
            .FirstOrDefault(value => !allowedEvidence.Contains(value));
        if (forged != null)
        {
            issueCode = CharacterAcquiredTraitInferenceIssueCode.EvidenceForgery;
            error = $"Acquired-trait response forged evidence fact '{forged}'.";
            return false;
        }

        response = parsedResponse;
        issueCode = CharacterAcquiredTraitInferenceIssueCode.None;
        error = string.Empty;
        return true;
    }

    private static CharacterAcquiredTraitInferenceIssueCode ClassifyContractError(
        string error)
    {
        if (!string.IsNullOrWhiteSpace(error)
            && error.IndexOf("extra=[", StringComparison.Ordinal) >= 0
            && error.IndexOf("extra=[]", StringComparison.Ordinal) < 0)
        {
            return CharacterAcquiredTraitInferenceIssueCode.ExtraResponseKey;
        }
        if (!string.IsNullOrWhiteSpace(error)
            && error.IndexOf("missing=[", StringComparison.Ordinal) >= 0
            && error.IndexOf("missing=[]", StringComparison.Ordinal) < 0)
        {
            return CharacterAcquiredTraitInferenceIssueCode.MissingResponseKey;
        }
        return CharacterAcquiredTraitInferenceIssueCode.ResponseSchemaInvalid;
    }
}

public sealed class CharacterAcquiredTraitInferenceService
{
    private const string SubmissionAuditPrefix = "acquired-trait-audit:submission:";
    private const string CompletionAuditPrefix = "acquired-trait-audit:completion:";
    private const string InstancePrefix = "acquired-trait-instance:";

    private readonly CharacterAcquiredTraitSettingsSO settings;
    private readonly CharacterAcquiredTraitModuleSO[] modules;

    public CharacterAcquiredTraitInferenceService(
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions)
    {
        this.settings = settings;
        modules = (moduleDefinitions ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .ToArray();
    }

    [GameplayInternalOnly(
        "The live acquired-trait producer submits one explicitly chosen milestone.",
        "V25 acquired-trait inference producer")]
    public CharacterAcquiredTraitInferenceCommandResult SubmitMilestone(
        CharacterProgression progression,
        CharacterAcquiredTraitSubmissionCommand command)
    {
        CharacterAcquiredTraitAggregateState current = progression?
            .CaptureAcquiredTraitState();
        if (command == null)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.InvalidCommand,
                string.Empty,
                string.Empty,
                string.Empty,
                current?.revision ?? -1,
                0,
                current?.revision ?? -1,
                default,
                "Acquired-trait submission command is missing.",
                null);
        }
        if (progression == null)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.InvalidCommand,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                -1,
                command.Timestamp,
                "Acquired-trait submission requires a character progression authority.",
                null);
        }
        if (!command.Timestamp.IsValid)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.InvalidTimestamp,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                "Acquired-trait submission timestamp is invalid.",
                null);
        }
        if (!TargetMatches(progression, command.TargetPersistentId, out string targetError))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.InvalidCommand,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                targetError,
                null);
        }
        if (current.revision != command.ExpectedRevision)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.StaleRevision,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                $"Acquired-trait submission expected revision {command.ExpectedRevision}, current revision is {current.revision}.",
                null);
        }
        if (current.revision == int.MaxValue)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.StaleRevision,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                "Acquired-trait revision is exhausted.",
                null);
        }
        if (current.HasProcessedMilestone(command.ManifestationMilestone))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.MilestoneAlreadyProcessed,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                $"Acquired-trait milestone {command.ManifestationMilestone} is already processed.",
                null);
        }
        if (current.instances.Any(value => value != null
                && (string.Equals(value.originatingRequestId, command.RequestId,
                        StringComparison.Ordinal)
                    || string.Equals(value.originatingRequestKey, command.RequestKey,
                        StringComparison.Ordinal))))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.DuplicateRequest,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                "Acquired-trait request ID or request key was already completed.",
                null);
        }
        if (current.pendingRequests.Any(value => value != null))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                current.pendingRequests.Any(value => value != null
                        && (string.Equals(value.requestId, command.RequestId,
                                StringComparison.Ordinal)
                            || string.Equals(value.requestKey, command.RequestKey,
                                StringComparison.Ordinal)))
                    ? CharacterAcquiredTraitInferenceIssueCode.DuplicateRequest
                    : CharacterAcquiredTraitInferenceIssueCode.PendingRequestInFlight,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                "Exactly one acquired-trait milestone request may be pending at a time.",
                null);
        }
        string unprojectedEvidence = command.EvidenceFactIds.FirstOrDefault(value =>
            string.IsNullOrWhiteSpace(value)
            || !value.StartsWith(
                CharacterAcquiredTraitEvidenceProjection.ProjectedFactIdPrefix,
                StringComparison.Ordinal));
        if (unprojectedEvidence != null)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.InvalidEvidence,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                "New acquired-trait submissions require collision-proof projected evidence IDs.",
                null);
        }
        // New manifestations are formula-only. Legacy packet/combination mechanics remain
        // load-and-complete support for formulaVersion=0 saves and are never a new source.
        CharacterAcquiredTraitPendingRequestState frozen;
        try
        {
            frozen = settings.RequireFormulaPolicy().FormulaVersion
                    >= CharacterAcquiredTraitFormulaGeneration.ModuleSelectionFormulaVersion
                ? CharacterAcquiredTraitFormulaGeneration.PreparePendingModuleSelection(
                    progression,
                    command.RequestId,
                    command.RequestKey,
                    command.ManifestationMilestone,
                    settings,
                    modules,
                    command.EvidenceFactIds,
                    command.EvidenceBindings)
                : CharacterAcquiredTraitFormulaGeneration.Freeze(
                    progression,
                    command.RequestId,
                    command.RequestKey,
                    command.ManifestationMilestone,
                    settings,
                    modules,
                    command.EvidenceFactIds,
                    command.EvidenceBindings);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.InvalidSettings,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                exception.Message,
                null);
        }
        int formulaExperienceScore = CharacterAcquiredTraitExperienceScore.Require(
            progression.NarrativeLedger);
        int formulaNextEligibleMilestone = settings.ManifestationGates
            .Where(value => value != null
                && value.MeaningfulRecordMilestone <= formulaExperienceScore
                && !current.HasProcessedMilestone(value.MeaningfulRecordMilestone))
            .Select(value => value.MeaningfulRecordMilestone)
            .OrderBy(value => value)
            .FirstOrDefault();
        if (formulaNextEligibleMilestone != command.ManifestationMilestone)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.OutOfOrderMilestone,
                command.RequestKey,
                frozen.candidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                "Formula acquired-trait milestone is not the next eligible unprocessed milestone.",
                null);
        }
        if (current.ActiveCount >= settings.MaximumActiveTraits)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.ActiveCapacityExceeded,
                command.RequestKey,
                frozen.candidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                "Acquired-trait active capacity is already full.",
                null);
        }
        int formulaNextRevision = checked(command.ExpectedRevision + 1);
        frozen.registeredRevision = formulaNextRevision;
        frozen.submissionAuditId = BuildAuditId(
            SubmissionAuditPrefix,
            command.RequestId,
            command.RequestKey,
            frozen.candidatePacketHash,
            formulaNextRevision,
            string.Empty);
        CharacterAcquiredTraitAggregateState formulaCandidate = current.Clone();
        formulaCandidate.revision = formulaNextRevision;
        formulaCandidate.pendingRequests.Add(frozen);
        CharacterAcquiredTraitRequestPacketDto formulaPacket = new()
        {
            settingsId = settings.SettingsId,
            targetPersistentId = frozen.targetPersistentId,
            requestId = frozen.requestId,
            requestKey = frozen.requestKey,
            manifestationMilestone = frozen.manifestationMilestone,
            evidenceFactIds = frozen.evidenceFactIds.ToList(),
            candidatePacketHash = frozen.candidatePacketHash
        };
        if (!progression.TryCommitAcquiredTraitState(
                formulaCandidate,
                command.ExpectedRevision,
                settings,
                modules,
                out IReadOnlyList<CharacterAcquiredTraitValidationIssue> formulaIssues))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.CommitRejected,
                command.RequestKey,
                frozen.candidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                formulaNextRevision,
                current.revision,
                command.Timestamp,
                "Formula acquired-trait pending registration was rejected: "
                    + string.Join(" | ", formulaIssues),
                formulaPacket);
        }
        return Success(
            frozen.submissionAuditId,
            CharacterAcquiredTraitInferenceCommandKind.Submission,
            command.RequestKey,
            frozen.candidatePacketHash,
            string.Empty,
            command.TargetPersistentId,
            command.ExpectedRevision,
            formulaNextRevision,
            formulaNextRevision,
            command.Timestamp,
            formulaPacket);

        if (!CharacterAcquiredTraitRequestPacketAuthority.TryBuild(
                command,
                progression.NarrativeLedger,
                current,
                settings,
                modules,
                out CharacterAcquiredTraitRequestPacketDto packet,
                out CharacterAcquiredTraitInferenceIssueCode buildIssue,
                out string buildError))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                buildIssue,
                command.RequestKey,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                buildError,
                null);
        }
        int experienceScore = CharacterAcquiredTraitExperienceScore.Require(
            progression.NarrativeLedger);
        int nextEligibleMilestone = settings.ManifestationGates
            .Where(value => value != null
                && value.MeaningfulRecordMilestone <= experienceScore
                && !current.HasProcessedMilestone(value.MeaningfulRecordMilestone))
            .Select(value => value.MeaningfulRecordMilestone)
            .OrderBy(value => value)
            .FirstOrDefault();
        if (nextEligibleMilestone != command.ManifestationMilestone)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.OutOfOrderMilestone,
                command.RequestKey,
                packet.candidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                $"Acquired-trait milestone {command.ManifestationMilestone} is not the next eligible unprocessed milestone {nextEligibleMilestone}.",
                packet);
        }
        if (current.ActiveCount >= settings.MaximumActiveTraits)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.ActiveCapacityExceeded,
                command.RequestKey,
                packet.candidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                0,
                current.revision,
                command.Timestamp,
                "Acquired-trait active capacity is already full.",
                packet);
        }

        int nextRevision = checked(command.ExpectedRevision + 1);
        string auditId = BuildAuditId(
            SubmissionAuditPrefix,
            command.RequestId,
            command.RequestKey,
            packet.candidatePacketHash,
            nextRevision,
            string.Empty);
        CharacterAcquiredTraitAggregateState candidate = current.Clone();
        candidate.revision = nextRevision;
        candidate.pendingRequests.Add(new CharacterAcquiredTraitPendingRequestState
        {
            targetPersistentId = command.TargetPersistentId,
            requestId = command.RequestId,
            requestKey = command.RequestKey,
            candidatePacketHash = packet.candidatePacketHash,
            submissionAuditId = auditId,
            manifestationMilestone = command.ManifestationMilestone,
            evidenceFactIds = packet.evidenceFactIds.ToList(),
            registeredRevision = nextRevision
        });
        candidate.pendingRequests = candidate.pendingRequests
            .OrderBy(value => value.manifestationMilestone)
            .ThenBy(value => value.requestId, StringComparer.Ordinal)
            .ToList();
        if (!progression.TryCommitAcquiredTraitState(
                candidate,
                command.ExpectedRevision,
                settings,
                modules,
                out IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Submission,
                CharacterAcquiredTraitInferenceIssueCode.CommitRejected,
                command.RequestKey,
                packet.candidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                nextRevision,
                current.revision,
                command.Timestamp,
                "Acquired-trait pending registration was rejected: "
                    + string.Join(" | ", validationIssues),
                packet);
        }

        return Success(
            auditId,
            CharacterAcquiredTraitInferenceCommandKind.Submission,
            command.RequestKey,
            packet.candidatePacketHash,
            string.Empty,
            command.TargetPersistentId,
            command.ExpectedRevision,
            nextRevision,
            nextRevision,
            command.Timestamp,
            packet);
    }

    [GameplayInternalOnly(
        "The live acquired-trait producer completes an exact persisted pending request.",
        "V25 acquired-trait inference producer callback")]
#if UNITY_EDITOR
    public CharacterAcquiredTraitInferenceCommandResult CompleteMilestone(
        CharacterProgression progression,
        CharacterAcquiredTraitCompletionCommand command,
        CharacterAcquiredTraitRequestPacketDto originalPacket) =>
        throw new InvalidOperationException(
            "Acquired-trait completion requires its mandatory gameplay outcome committer.");
#endif

    public CharacterAcquiredTraitInferenceCommandResult CompleteMilestone(
        CharacterProgression progression,
        CharacterAcquiredTraitCompletionCommand command,
        CharacterAcquiredTraitRequestPacketDto originalPacket,
        IAcquiredTraitInferenceOutcomeCommitter outcomeCommitter)
    {
        if (outcomeCommitter == null)
            throw new ArgumentNullException(nameof(outcomeCommitter));
        CharacterAcquiredTraitAggregateState current = progression?
            .CaptureAcquiredTraitState();
        if (command == null || progression == null || originalPacket == null)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.InvalidCommand,
                command?.RequestKey ?? string.Empty,
                command?.CandidatePacketHash ?? string.Empty,
                command?.TargetPersistentId ?? string.Empty,
                command?.ExpectedRevision ?? -1,
                command?.RegisteredRevision ?? 0,
                current?.revision ?? -1,
                command?.Timestamp ?? default,
                "Acquired-trait completion requires progression, command, and original packet.",
                originalPacket);
        }
        if (!command.Timestamp.IsValid)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.InvalidTimestamp,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                "Acquired-trait completion timestamp is invalid.",
                originalPacket);
        }
        if (!TargetMatches(progression, command.TargetPersistentId, out string targetError))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.StaleCallback,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                targetError,
                originalPacket);
        }
        if (!current.TryGetPendingRequest(
                command.RequestId,
                out CharacterAcquiredTraitPendingRequestState pending))
        {
            bool completed = current.instances.Any(value => value != null
                && (string.Equals(
                        value.originatingRequestId,
                        command.RequestId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        value.originatingRequestKey,
                        command.RequestKey,
                        StringComparison.Ordinal)));
            if (!completed && current.revision != command.ExpectedRevision)
            {
                return Failure(
                    CharacterAcquiredTraitInferenceCommandKind.Completion,
                    CharacterAcquiredTraitInferenceIssueCode.StaleRevision,
                    command.RequestKey,
                    command.CandidatePacketHash,
                    command.TargetPersistentId,
                    command.ExpectedRevision,
                    command.RegisteredRevision,
                    current.revision,
                    command.Timestamp,
                    $"Acquired-trait callback expected revision {command.ExpectedRevision}, current revision is {current.revision}.",
                    originalPacket);
            }
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                completed
                    ? CharacterAcquiredTraitInferenceIssueCode.DuplicateCallback
                    : CharacterAcquiredTraitInferenceIssueCode.MissingPendingRequest,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                completed
                    ? "Acquired-trait callback request is already completed."
                    : "Acquired-trait callback has no matching pending request.",
                originalPacket);
        }
        if (current.revision != command.ExpectedRevision)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.StaleRevision,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                $"Acquired-trait callback expected revision {command.ExpectedRevision}, current revision is {current.revision}.",
                originalPacket);
        }
        if (current.revision == int.MaxValue)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.StaleRevision,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                "Acquired-trait revision is exhausted.",
                originalPacket);
        }
        if (!PendingMatches(pending, command, originalPacket))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.StaleCallback,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                "Acquired-trait callback tokens do not match the persisted pending request.",
                originalPacket);
        }
        if (pending.formulaVersion > 0)
        {
            string displayName;
            string narrativeFlavor;
            if (pending.presentationState == CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
            {
                NarrativeFormulaModuleSelectionRequest selectionRequest;
                try
                {
                    selectionRequest = CharacterAcquiredTraitFormulaGeneration.PrepareModuleSelection(
                        progression, pending.requestId, pending.requestKey,
                        pending.manifestationMilestone, settings, modules,
                        pending.evidenceFactIds);
                }
                catch (Exception exception) when (exception is ArgumentException
                    or InvalidOperationException or KeyNotFoundException or OverflowException)
                {
                    return Failure(
                        CharacterAcquiredTraitInferenceCommandKind.Completion,
                        CharacterAcquiredTraitInferenceIssueCode.InvalidSettings,
                        command.RequestKey, command.CandidatePacketHash,
                        command.TargetPersistentId, command.ExpectedRevision,
                        command.RegisteredRevision, current.revision,
                        command.Timestamp, exception.Message, originalPacket);
                }
                string[] offeredBenefits = selectionRequest.Offers
                    .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Positive)
                    .Select(value => value.ModuleId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] offeredDrawbacks = selectionRequest.Offers
                    .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Drawback)
                    .Select(value => value.ModuleId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (!string.Equals(pending.moduleSelectionId, selectionRequest.SelectionId, StringComparison.Ordinal)
                    || !offeredBenefits.SequenceEqual(pending.offeredBenefitModuleIds, StringComparer.Ordinal)
                    || !offeredDrawbacks.SequenceEqual(pending.offeredDrawbackModuleIds, StringComparer.Ordinal))
                {
                    return Failure(
                        CharacterAcquiredTraitInferenceCommandKind.Completion,
                        CharacterAcquiredTraitInferenceIssueCode.StaleCallback,
                        command.RequestKey, command.CandidatePacketHash,
                        command.TargetPersistentId, command.ExpectedRevision,
                        command.RegisteredRevision, current.revision,
                        command.Timestamp, "Acquired-trait module offers changed after submission.", originalPacket);
                }
                if (!CharacterAcquiredTraitModuleSelectionResponse.TryValidate(
                        pending, selectionRequest, command.ResponseJson,
                        out CharacterAcquiredTraitModuleSelectionResponseDto moduleResponse,
                        out _, out string selectionError))
                {
                    return Failure(
                        CharacterAcquiredTraitInferenceCommandKind.Completion,
                        CharacterAcquiredTraitInferenceIssueCode.InvalidNarrative,
                        command.RequestKey, command.CandidatePacketHash,
                        command.TargetPersistentId, command.ExpectedRevision,
                        command.RegisteredRevision, current.revision,
                        command.Timestamp, selectionError, originalPacket);
                }
                try
                {
                    pending = CharacterAcquiredTraitFormulaGeneration.FreezeSelected(
                        progression, pending.requestId, pending.requestKey,
                        pending.manifestationMilestone, settings, modules,
                        pending.evidenceFactIds,
                        new NarrativeFormulaModuleSelectionChoice(
                            moduleResponse.selectionId, moduleResponse.positiveModuleIds,
                            moduleResponse.drawbackModuleIds, moduleResponse.evidenceFactIds),
                        pending.evidenceBindings);
                }
                catch (Exception exception) when (exception is ArgumentException
                    or InvalidOperationException or KeyNotFoundException or OverflowException)
                {
                    return Failure(
                        CharacterAcquiredTraitInferenceCommandKind.Completion,
                        CharacterAcquiredTraitInferenceIssueCode.InvalidNarrative,
                        command.RequestKey, command.CandidatePacketHash,
                        command.TargetPersistentId, command.ExpectedRevision,
                        command.RegisteredRevision, current.revision,
                        command.Timestamp, exception.Message, originalPacket);
                }
                displayName = moduleResponse.displayName;
                narrativeFlavor = moduleResponse.narrativeFlavor;
            }
            else if (!CharacterAcquiredTraitFormulaPresentation.TryValidate(
                         pending,
                         command.ResponseJson,
                         out CharacterAcquiredTraitFormulaPresentationDto presentation,
                         out string presentationError))
            {
                return Failure(
                    CharacterAcquiredTraitInferenceCommandKind.Completion,
                    CharacterAcquiredTraitInferenceIssueCode.InvalidNarrative,
                    command.RequestKey,
                    command.CandidatePacketHash,
                    command.TargetPersistentId,
                    command.ExpectedRevision,
                    command.RegisteredRevision,
                    current.revision,
                    command.Timestamp,
                    presentationError,
                    originalPacket);
            }
            else
            {
                displayName = presentation.displayName;
                narrativeFlavor = presentation.narrativeFlavor;
            }
            CharacterAcquiredTraitModuleSO selectedModule;
            try
            {
                if (pending.formulaVersion >= 2)
                {
                    string moduleId = pending.benefitModuleIds.Single();
                    selectedModule = modules.Single(value => value != null
                        && string.Equals(value.ModuleId, moduleId,
                            StringComparison.Ordinal));
                }
                else
                {
                    string capabilityId = pending.formulaCapabilities.Single().capabilityId;
                    selectedModule = modules.Single(value => value != null
                        && string.Equals(value.RequireFormulaDescriptor().CapabilityId,
                            capabilityId, StringComparison.Ordinal));
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or ArgumentException or KeyNotFoundException)
            {
                return Failure(
                    CharacterAcquiredTraitInferenceCommandKind.Completion,
                    CharacterAcquiredTraitInferenceIssueCode.InvalidModuleDefinition,
                    command.RequestKey,
                    command.CandidatePacketHash,
                    command.TargetPersistentId,
                    command.ExpectedRevision,
                    command.RegisteredRevision,
                    current.revision,
                    command.Timestamp,
                    exception.Message,
                    originalPacket);
            }
            int formulaCompletionRevision = checked(command.ExpectedRevision + 1);
            string formulaAuditId = BuildAuditId(
                CompletionAuditPrefix,
                command.RequestId,
                command.RequestKey,
                command.CandidatePacketHash,
                formulaCompletionRevision,
                NarrativeInferenceHash.ComputeSha256Utf8(command.ResponseJson));
            CharacterAcquiredTraitAggregateState formulaCandidate = current.Clone();
            formulaCandidate.revision = formulaCompletionRevision;
            formulaCandidate.pendingRequests.RemoveAll(value => value != null
                && string.Equals(value.requestId, command.RequestId, StringComparison.Ordinal));
            formulaCandidate.instances.Add(new CharacterAcquiredTraitInstanceState
            {
                instanceId = InstancePrefix + HashSuffix(command.TargetPersistentId + "|"
                    + pending.manifestationMilestone.ToString(CultureInfo.InvariantCulture)
                    + "|" + command.RequestId),
                combinationId = string.Empty,
                moduleIds = new List<string> { selectedModule.ModuleId },
                displayName = displayName,
                description = pending.mechanicalDescription,
                narrativeReason = narrativeFlavor,
                narrativeFlavor = narrativeFlavor,
                evidenceFactIds = pending.evidenceFactIds.OrderBy(value => value,
                    StringComparer.Ordinal).ToList(),
                evidenceBindings = (pending.evidenceBindings
                        ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
                    .Where(value => value != null)
                    .Select(value => value.Clone()).ToList(),
                manifestationMilestone = pending.manifestationMilestone,
                originatingRequestId = command.RequestId,
                originatingRequestKey = command.RequestKey,
                candidatePacketHash = command.CandidatePacketHash,
                selectionAuditId = formulaAuditId,
                acceptedRevision = formulaCompletionRevision,
                erased = false,
                erasedAt = CharacterAcquiredTraitInstanceState.NotErasedAtAbsoluteHour,
                erasedRevision = 0,
                erasureAuditId = string.Empty,
                formulaVersion = pending.formulaVersion,
                formulaCatalogSha256 = pending.formulaCatalogSha256,
                calculatedCost = pending.calculatedCost,
                positiveCost = pending.positiveCost,
                drawbackCredit = pending.drawbackCredit,
                drawbackId = pending.drawbackId,
                narrativeBudget = pending.narrativeBudget,
                drawbackEvidenceQualified = pending.drawbackEvidenceQualified,
                benefitModuleIds = pending.formulaVersion >= 2
                    ? pending.benefitModuleIds.ToList()
                    : new List<string>(),
                drawbackCapabilityIds = pending.formulaVersion >= 2
                    ? pending.drawbackCapabilityIds.ToList()
                    : new List<string>(),
                formulaCapabilities = pending.formulaCapabilities
                    .Select(value => value.Clone()).ToList(),
                effectOverrides = pending.effectOverrides
                    .Select(value => value.Clone()).ToList(),
                presentationId = pending.presentationId,
                mechanicalDescription = pending.mechanicalDescription
            });
            formulaCandidate.instances = formulaCandidate.instances
                .OrderBy(value => value.manifestationMilestone)
                .ThenBy(value => value.instanceId, StringComparer.Ordinal).ToList();
            formulaCandidate.processedMilestones.Add(pending.manifestationMilestone);
            formulaCandidate.processedMilestones = formulaCandidate.processedMilestones
                .Distinct().OrderBy(value => value).ToList();
            CharacterAcquiredTraitInferenceCommandResult formulaSuccess = Success(
                formulaAuditId,
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                command.RequestKey,
                command.CandidatePacketHash,
                string.Empty,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                formulaCompletionRevision,
                command.Timestamp,
                originalPacket);
            if (!TryPrepareOutcome(
                    formulaSuccess,
                    progression,
                    outcomeCommitter,
                    out PreparedEvolutionOutcome preparedFormulaOutcome,
                    out string formulaOutcomeFailure))
            {
                return Failure(
                    CharacterAcquiredTraitInferenceCommandKind.Completion,
                    CharacterAcquiredTraitInferenceIssueCode.CommitRejected,
                    command.RequestKey,
                    command.CandidatePacketHash,
                    command.TargetPersistentId,
                    command.ExpectedRevision,
                    command.RegisteredRevision,
                    current.revision,
                    command.Timestamp,
                    "Acquired-trait outcome preparation failed: "
                        + formulaOutcomeFailure,
                    originalPacket);
            }
            IReadOnlyList<CharacterAcquiredTraitValidationIssue> formulaIssues;
            try
            {
                if (progression.TryCommitAcquiredTraitStateWithFormulaEvidenceAndOutcome(
                        formulaCandidate,
                        command.ExpectedRevision,
                        settings,
                        modules,
                        pending.evidenceFactIds,
                        pending.evidenceBindings,
                        () => CommitOutcomeOrThrow(
                            outcomeCommitter,
                            preparedFormulaOutcome,
                            formulaCompletionRevision),
                        out formulaIssues))
                {
                    return formulaSuccess;
                }
            }
            catch
            {
                outcomeCommitter.Cancel(preparedFormulaOutcome);
                throw;
            }
            outcomeCommitter.Cancel(preparedFormulaOutcome);
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.CommitRejected,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                "Formula acquired-trait atomic completion was rejected: "
                    + string.Join(" | ", formulaIssues),
                originalPacket);
        }
        if (!CharacterAcquiredTraitResponseAuthority.TryValidate(
                originalPacket,
                command.ResponseJson,
                progression.NarrativeLedger,
                current,
                settings,
                modules,
                out CharacterAcquiredTraitResponseDto response,
                out CharacterAcquiredTraitCombinationPacketDto selected,
                out CharacterAcquiredTraitInferenceIssueCode validationIssue,
                out string validationError))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                validationIssue,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                validationError,
                originalPacket);
        }
        if (current.ActiveCount >= settings.MaximumActiveTraits)
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.ActiveCapacityExceeded,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                "Acquired-trait completion would exceed the active-trait limit.",
                originalPacket);
        }

        int nextRevision = checked(command.ExpectedRevision + 1);
        string auditId = BuildAuditId(
            CompletionAuditPrefix,
            command.RequestId,
            command.RequestKey,
            command.CandidatePacketHash,
            nextRevision,
            NarrativeInferenceHash.ComputeSha256Utf8(command.ResponseJson));
        string instanceId = InstancePrefix + HashSuffix(
            command.TargetPersistentId + "|"
            + pending.manifestationMilestone.ToString(CultureInfo.InvariantCulture)
            + "|" + command.RequestId);
        CharacterAcquiredTraitAggregateState candidate = current.Clone();
        candidate.revision = nextRevision;
        candidate.pendingRequests.RemoveAll(value => value != null
            && string.Equals(value.requestId, command.RequestId, StringComparison.Ordinal));
        candidate.instances.Add(new CharacterAcquiredTraitInstanceState
        {
            instanceId = instanceId,
            combinationId = selected.combinationId,
            moduleIds = selected.moduleIds.ToList(),
            displayName = response.displayName,
            description = response.description,
            narrativeReason = response.narrativeReason,
            evidenceFactIds = response.evidenceFactIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            manifestationMilestone = pending.manifestationMilestone,
            originatingRequestId = command.RequestId,
            originatingRequestKey = command.RequestKey,
            candidatePacketHash = command.CandidatePacketHash,
            selectionAuditId = auditId,
            acceptedRevision = nextRevision,
            erased = false,
            erasedAt = CharacterAcquiredTraitInstanceState.NotErasedAtAbsoluteHour,
            erasedRevision = 0,
            erasureAuditId = string.Empty
        });
        candidate.instances = candidate.instances
            .OrderBy(value => value.manifestationMilestone)
            .ThenBy(value => value.instanceId, StringComparer.Ordinal)
            .ToList();
        candidate.processedMilestones.Add(pending.manifestationMilestone);
        candidate.processedMilestones = candidate.processedMilestones
            .Distinct()
            .OrderBy(value => value)
            .ToList();
        CharacterAcquiredTraitInferenceCommandResult success = Success(
            auditId,
            CharacterAcquiredTraitInferenceCommandKind.Completion,
            command.RequestKey,
            command.CandidatePacketHash,
            selected.combinationId,
            command.TargetPersistentId,
            command.ExpectedRevision,
            command.RegisteredRevision,
            nextRevision,
            command.Timestamp,
            originalPacket);
        if (!TryPrepareOutcome(
                success,
                progression,
                outcomeCommitter,
                out PreparedEvolutionOutcome preparedOutcome,
                out string outcomeFailure))
        {
            return Failure(
                CharacterAcquiredTraitInferenceCommandKind.Completion,
                CharacterAcquiredTraitInferenceIssueCode.CommitRejected,
                command.RequestKey,
                command.CandidatePacketHash,
                command.TargetPersistentId,
                command.ExpectedRevision,
                command.RegisteredRevision,
                current.revision,
                command.Timestamp,
                "Acquired-trait outcome preparation failed: " + outcomeFailure,
                originalPacket);
        }
        IReadOnlyList<CharacterAcquiredTraitValidationIssue> validationIssues;
        try
        {
            if (progression.TryCommitAcquiredTraitStateAndOutcome(
                    candidate,
                    command.ExpectedRevision,
                    settings,
                    modules,
                    () => CommitOutcomeOrThrow(
                        outcomeCommitter,
                        preparedOutcome,
                        nextRevision),
                    out validationIssues))
            {
                return success;
            }
        }
        catch
        {
            outcomeCommitter.Cancel(preparedOutcome);
            throw;
        }
        outcomeCommitter.Cancel(preparedOutcome);
        return Failure(
            CharacterAcquiredTraitInferenceCommandKind.Completion,
            CharacterAcquiredTraitInferenceIssueCode.CommitRejected,
            command.RequestKey,
            command.CandidatePacketHash,
            command.TargetPersistentId,
            command.ExpectedRevision,
            command.RegisteredRevision,
            current.revision,
            command.Timestamp,
            "Acquired-trait completion commit was rejected: "
                + string.Join(" | ", validationIssues),
            originalPacket);
    }

    private static bool TryPrepareOutcome(
        in CharacterAcquiredTraitInferenceCommandResult result,
        CharacterProgression progression,
        IAcquiredTraitInferenceOutcomeCommitter outcomeCommitter,
        out PreparedEvolutionOutcome prepared,
        out string failureReason)
    {
        CharacterAcquiredTraitInferenceAuditRecord audit = result.Audit;
        string operationId = "trait-inference:"
            + audit.TargetPersistentId
            + ":revision:"
            + audit.ResultingRevision.ToString("D8", CultureInfo.InvariantCulture);
        int absoluteDay = ResolveAbsoluteDay(audit.Timestamp);
        AcquiredTraitInferenceOutcomeReceipt receipt = new(
            operationId,
            result,
            ResolveCharacterDisplayName(progression),
            absoluteDay);
        return outcomeCommitter.TryPrepare(
            receipt,
            out prepared,
            out failureReason);
    }

    private static string ResolveCharacterDisplayName(
        CharacterProgression progression)
    {
        string displayName = progression?.Actor?.Identity?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = progression?.GrowthState?.displayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException(
                "Acquired-trait outcome requires the character's immutable display identity.");
        }
        return displayName.Trim();
    }

    private static void CommitOutcomeOrThrow(
        IAcquiredTraitInferenceOutcomeCommitter outcomeCommitter,
        in PreparedEvolutionOutcome prepared,
        int expectedRevision)
    {
        if (!outcomeCommitter.TryCommit(
                prepared,
                expectedRevision,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Acquired-trait mandatory outcome commit failed; the staged state was rolled back: "
                + failureReason);
        }
    }

    private static int ResolveAbsoluteDay(NarrativeInferenceTimestamp timestamp)
    {
        if (timestamp.Authority != NarrativeInferenceTimeAuthority.GameTick
            || timestamp.GameTick < 0L)
        {
            throw new InvalidOperationException(
                "Acquired-trait gameplay outcome requires a game-tick timestamp.");
        }
        long day = timestamp.GameTick / GameCalendarRules.HoursPerDay + 1L;
        return checked((int)day);
    }

    private static bool PendingMatches(
        CharacterAcquiredTraitPendingRequestState pending,
        CharacterAcquiredTraitCompletionCommand command,
        CharacterAcquiredTraitRequestPacketDto packet)
    {
        return pending.evidenceFactIds != null
            && packet.evidenceFactIds != null
            && string.Equals(
                pending.targetPersistentId,
                command.TargetPersistentId,
                StringComparison.Ordinal)
            && string.Equals(pending.requestId, command.RequestId, StringComparison.Ordinal)
            && string.Equals(pending.requestKey, command.RequestKey, StringComparison.Ordinal)
            && string.Equals(
                pending.candidatePacketHash,
                command.CandidatePacketHash,
                StringComparison.Ordinal)
            && pending.registeredRevision == command.RegisteredRevision
            && string.Equals(packet.targetPersistentId, command.TargetPersistentId,
                StringComparison.Ordinal)
            && string.Equals(packet.requestId, command.RequestId, StringComparison.Ordinal)
            && string.Equals(packet.requestKey, command.RequestKey, StringComparison.Ordinal)
            && string.Equals(
                packet.candidatePacketHash,
                command.CandidatePacketHash,
                StringComparison.Ordinal)
            && packet.manifestationMilestone == pending.manifestationMilestone
            && packet.evidenceFactIds.SequenceEqual(
                pending.evidenceFactIds,
                StringComparer.Ordinal);
    }

    private static bool TargetMatches(
        CharacterProgression progression,
        string targetPersistentId,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(targetPersistentId)
            || !string.Equals(
                targetPersistentId,
                targetPersistentId.Trim(),
                StringComparison.Ordinal))
        {
            error = "Acquired-trait target persistent ID is missing or non-canonical.";
            return false;
        }
        string boundId = progression.Actor?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (boundId.Length > 0
            && !string.Equals(boundId, targetPersistentId, StringComparison.Ordinal))
        {
            error = $"Acquired-trait target '{targetPersistentId}' does not match bound character '{boundId}'.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static CharacterAcquiredTraitInferenceCommandResult Success(
        string auditId,
        CharacterAcquiredTraitInferenceCommandKind kind,
        string requestKey,
        string packetHash,
        string selectedCombinationId,
        string targetPersistentId,
        int expectedRevision,
        int registeredRevision,
        int resultingRevision,
        NarrativeInferenceTimestamp timestamp,
        CharacterAcquiredTraitRequestPacketDto packet) => new(
        new CharacterAcquiredTraitInferenceAuditRecord(
            auditId,
            kind,
            CharacterAcquiredTraitInferenceIssueCode.None,
            requestKey,
            packetHash,
            selectedCombinationId,
            targetPersistentId,
            expectedRevision,
            registeredRevision,
            resultingRevision,
            timestamp,
            string.Empty),
        packet);

    private static CharacterAcquiredTraitInferenceCommandResult Failure(
        CharacterAcquiredTraitInferenceCommandKind kind,
        CharacterAcquiredTraitInferenceIssueCode issueCode,
        string requestKey,
        string packetHash,
        string targetPersistentId,
        int expectedRevision,
        int registeredRevision,
        int resultingRevision,
        NarrativeInferenceTimestamp timestamp,
        string error,
        CharacterAcquiredTraitRequestPacketDto packet)
    {
        string auditId = BuildAuditId(
            kind == CharacterAcquiredTraitInferenceCommandKind.Submission
                ? SubmissionAuditPrefix
                : CompletionAuditPrefix,
            targetPersistentId,
            requestKey,
            packetHash,
            resultingRevision,
            issueCode + "|" + (error ?? string.Empty));
        return new CharacterAcquiredTraitInferenceCommandResult(
            new CharacterAcquiredTraitInferenceAuditRecord(
                auditId,
                kind,
                issueCode,
                requestKey,
                packetHash,
                string.Empty,
                targetPersistentId,
                expectedRevision,
                registeredRevision,
                resultingRevision,
                timestamp,
                error),
            packet);
    }

    private static string BuildAuditId(
        string prefix,
        string requestId,
        string requestKey,
        string packetHash,
        int revision,
        string discriminator) => prefix + HashSuffix(
        (requestId ?? string.Empty) + "|"
        + (requestKey ?? string.Empty) + "|"
        + (packetHash ?? string.Empty) + "|"
        + revision.ToString(CultureInfo.InvariantCulture) + "|"
        + (discriminator ?? string.Empty));

    private static string HashSuffix(string value)
    {
        const string prefix = "sha256:";
        string hash = NarrativeInferenceHash.ComputeSha256Utf8(value);
        return hash.StartsWith(prefix, StringComparison.Ordinal)
            ? hash.Substring(prefix.Length)
            : hash;
    }
}

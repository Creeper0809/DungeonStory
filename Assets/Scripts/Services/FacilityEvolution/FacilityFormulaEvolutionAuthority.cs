using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>Facility-owned formula preparation. It performs no material consumption,
/// replacement, effect publication, or influence mutation.</summary>
public static class FacilityFormulaEvolutionAuthority
{
    public const int ModuleSelectionFormulaVersion = 2;
    public const int DrawbackModuleSelectionFormulaVersion = 3;
    // The model response contract exposes at most four evidence IDs. Optional
    // drawbacks require every non-empty selectable subset to resolve, so this
    // caps the prospective check at 2^4 - 1 deterministic freeze attempts.
    private const int MaximumProspectiveOptionalDrawbackEvidenceCount = 4;
    // The facility module registry is immutable built-in authority. Recipe assets
    // name the module explicitly; no recipe ID or LLM output is used to choose it.
    private static readonly IEvolutionModuleRegistry RegisteredModules =
        new EvolutionModuleRegistry();

    public static bool TryPrepare(
        FacilityEvolutionState state,
        FacilityEvolutionRecipeSO recipe,
        out FacilityEvolutionFormulaPresentationPendingSnapshot pending,
        out string failureReason,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings = null)
    {
        pending = null;
        failureReason = string.Empty;
        try
        {
            if (state == null || string.IsNullOrWhiteSpace(state.facilityPersistentId))
                throw new InvalidOperationException("Facility formula requires a persistent facility state.");
            NarrativeFormulaStrengthPolicy policy = recipe.RequireFormulaPolicy();
            if (policy.FormulaVersion >= ModuleSelectionFormulaVersion)
            {
                pending = PrepareModuleSelection(
                    state, recipe, policy, outcomeBindings);
                return true;
            }
            FacilityEvolutionFormulaCapabilityDefinition definition = recipe.RequireFormulaCapability();
            if (!RegisteredModules.TryGet(definition.evolutionModuleId,
                    out EvolutionModuleDefinition module))
                throw new InvalidOperationException(
                    "Facility formula capability names an unregistered evolution module: "
                    + definition.evolutionModuleId);
            if (!IsEligiblePositiveModule(recipe, module, out string eligibilityFailure))
                throw new InvalidOperationException(
                    "Facility formula capability is unavailable for its target: "
                    + eligibilityFailure);
            if (module.Burdens.Count == 0)
                throw new InvalidOperationException(
                    "Facility formula capability requires an inseparable runtime burden: "
                    + definition.evolutionModuleId);
            NarrativeFormulaCapabilityDescriptor descriptor = definition.ToRuntime();
            FacilityFormulaEvidenceRecord[] records = BuildEvidence(state, policy).ToArray();
            GameplayOutcomeEvidenceBindingSnapshot[] exactBindings = NormalizeBindings(
                outcomeBindings);
            if (records.Length == 0 && exactBindings.Length == 0)
                throw new InvalidOperationException("Facility formula requires recorded facility usage evidence.");
            NarrativeFormulaEvidence[] evidence = records.Select(value => new NarrativeFormulaEvidence(
                value.evidenceId, value.eventGroupKey, value.actionKey,
                value.relationshipKey, value.domainKey, value.attainedMilestoneCount,
                value.importancePoints, value.influenceUseCount))
                .Concat(exactBindings.Select(value =>
                    GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(value, policy)))
                .ToArray();
            NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(policy, evidence);
            bool hasNegativeEvidence = records.Any(value => value.originalEvent != null
                && NarrativeFormulaNegativeEvidence.Matches(
                    value.originalEvent.eventId, value.originalEvent.outcomeId))
                || exactBindings.Any(GameplayOutcomeEvidenceFormulaProjection.IsNegative);
            NarrativeFormulaDrawbackOption drawback = new(
                "facility-burden:" + definition.evolutionModuleId,
                Math.Max(1, module.RiskWeight),
                NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                reachable: true,
                mandatory: true,
                separatelyRemovable: false,
                cancelsSelectedBenefit: false,
                hasNegativeNarrativeEvidence: hasNegativeEvidence);
            IReadOnlyList<NarrativeFormulaCandidate> candidates = NarrativeFormulaCore.Optimize(
                descriptor,
                recipe.formulaPolicy.RequireGenerationContext(),
                strength.Budget,
                descriptor.NarrativeAffinity,
                Novelty(state, descriptor.CapabilityId),
                evidence.Select(value => value.EvidenceId),
                maximumResults: 3,
                drawbackPolicy: recipe.formulaPolicy.RequireDrawbackPolicy(),
                drawbackOption: drawback);
            if (candidates.Count == 0)
                throw new InvalidOperationException("No facility formula candidate fits the recorded evidence budget.");
            NarrativeFormulaCandidate winner = NarrativeFormulaCore.ChooseDeterministicWinner(
                candidates,
                state.facilityPersistentId,
                "facility-evolution:" + recipe.EffectiveId + ":" + (state.generation + 1),
                policy.FormulaVersion,
                recipe.formulaPolicy.RequireCatalogSha256());
            string stableHash = NarrativeInferenceHash.ComputeSha256Utf8(string.Join("|",
                state.facilityPersistentId, recipe.EffectiveId, policy.FormulaVersion,
                recipe.formulaPolicy.RequireCatalogSha256(), winner.CanonicalSignature))
                .Substring("sha256:".Length);
            EvolutionNode node = new()
            {
                nodeId = "facility-formula-node:" + stableHash,
                effectId = definition.evolutionModuleId,
                generation = Math.Max(0, state.generation + 1),
                active = false,
                mechanicallyUnlocked = false,
                narrativeReady = false,
                uiVisible = false,
                playerVisible = false,
                formulaVersion = policy.FormulaVersion,
                formulaCatalogSha256 = recipe.formulaPolicy.RequireCatalogSha256(),
                formulaCapabilities = winner.Allocations.Select(ToEnvelope).ToList(),
                calculatedCost = winner.CalculatedCost,
                positiveCost = winner.PositiveCost,
                drawbackCredit = winner.DrawbackCredit,
                drawbackId = winner.DrawbackId,
                drawbackEvidenceQualified = hasNegativeEvidence,
                formulaBudget = strength.Budget,
                evidenceIds = winner.EvidenceIds.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                gameplayOutcomeEvidence = GameplayOutcomeEvidenceFormulaProjection.Select(
                    exactBindings, winner.EvidenceIds),
                presentationId = "presentation:facility:" + stableHash,
                presentationState = EquipmentEvolutionPresentationState.PresentationPending,
                presentationFailureCount = 0,
                potencyMultiplier = ToPotency(winner, descriptor),
                mechanicalDescription = FormatMechanicalDescription(recipe, definition, winner)
            };
            state.formulaEvidence ??= new List<FacilityFormulaEvidenceRecord>();
            foreach (FacilityFormulaEvidenceRecord evidenceRecord in records)
            {
                if (!state.formulaEvidence.Any(value => value != null && string.Equals(
                        value.evidenceId, evidenceRecord.evidenceId, StringComparison.Ordinal)))
                    state.formulaEvidence.Add(evidenceRecord.Clone());
            }
            state.formulaEvidence = state.formulaEvidence.Where(value => value != null)
                .OrderBy(value => value.evidenceId, StringComparer.Ordinal).ToList();
            pending = new FacilityEvolutionFormulaPresentationPendingSnapshot
            {
                recipeId = recipe.EffectiveId,
                sourceFacilityPersistentId = state.facilityPersistentId,
                presentationId = node.presentationId,
                node = node
            };
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            failureReason = exception.Message;
            return false;
        }
    }

    private static FacilityEvolutionFormulaPresentationPendingSnapshot PrepareModuleSelection(
        FacilityEvolutionState state,
        FacilityEvolutionRecipeSO recipe,
        NarrativeFormulaStrengthPolicy policy,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> outcomeBindings)
    {
        FacilityFormulaEvidenceRecord[] records = BuildEvidence(state, policy).ToArray();
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings = NormalizeBindings(
            outcomeBindings);
        if (records.Length == 0 && exactBindings.Length == 0)
            throw new InvalidOperationException(
                "Facility module selection requires recorded facility usage evidence.");
        NarrativeFormulaEvidence[] evidence = records.Select(ToFormulaEvidence)
            .Concat(exactBindings.Select(value =>
                GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(value, policy)))
            .ToArray();
        NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(policy, evidence);
        bool hasNegativeEvidence = records.Any(record => record.originalEvent != null
            && NarrativeFormulaNegativeEvidence.Matches(
                record.originalEvent.eventId, record.originalEvent.outcomeId))
            || exactBindings.Any(GameplayOutcomeEvidenceFormulaProjection.IsNegative);
        List<EquipmentEvolutionModuleOfferState> offers = new();
        List<string> rejectedPositiveReasons = new();
        foreach (FacilityEvolutionFormulaCapabilityDefinition definition in
                 recipe.RequireFormulaCapabilities())
        {
            if (!RegisteredModules.TryGet(definition.evolutionModuleId,
                    out EvolutionModuleDefinition module)
                || module.Benefits.Count == 0 || !module.IsPositiveModule)
                throw new InvalidOperationException(
                    "Facility module selection requires an authored positive module: "
                    + definition.evolutionModuleId);

            if (!IsEligiblePositiveModule(recipe, module, out string eligibilityFailure))
            {
                rejectedPositiveReasons.Add(eligibilityFailure);
                continue;
            }

            offers.Add(new EquipmentEvolutionModuleOfferState
            {
                moduleId = definition.evolutionModuleId,
                polarity = EvolutionModuleOfferPolarity.Positive,
                semanticDescription = DescribeModule(module)
            });
        }
        if (offers.Count == 0)
            throw new InvalidOperationException(
                "Facility module selection has no eligible positive offers: "
                + string.Join("; ", rejectedPositiveReasons
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)));
        if (policy.FormulaVersion >= DrawbackModuleSelectionFormulaVersion)
        {
            bool creditReachable = recipe.formulaPolicy.RequireDrawbackPolicy()
                .Resolve(strength.Budget, new NarrativeFormulaDrawbackOption(
                    "facility:drawback-probe", 1,
                    NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                    true, true, false, false, true)).AcceptedCredit > 0;
            if (creditReachable)
            {
                foreach (EvolutionModuleDefinition module in RegisteredModules.All
                             .Where(value => value.ModuleId.StartsWith(
                                     "facility:", StringComparison.Ordinal)
                                 && value.BurdenKind ==
                                     EvolutionModuleBurdenKind.OptionalDrawback
                                 && IsEligibleOptionalDrawbackModule(recipe, value, out _)
                                 && records.Any(record => record.originalEvent != null
                                     && value.MatchesNegativeEvidence(
                                         record.originalEvent.eventId,
                                         record.originalEvent.outcomeId,
                                         record.originalEvent.narrativeContext?.resultDetail))
                                 && HasResolvableOptionalDrawbackPair(
                                     state,
                                     recipe,
                                     offers,
                                     value,
                                     records)))
                {
                    offers.Add(new EquipmentEvolutionModuleOfferState
                    {
                        moduleId = module.ModuleId,
                        polarity = EvolutionModuleOfferPolarity.Drawback,
                        semanticDescription = DescribeModule(module)
                    });
                }
            }
        }
        offers = offers.OrderBy(value => value.moduleId, StringComparer.Ordinal).ToList();
        string stableHash = NarrativeInferenceHash.ComputeSha256Utf8(string.Join("|",
            state.facilityPersistentId, recipe.EffectiveId, policy.FormulaVersion,
            recipe.formulaPolicy.RequireCatalogSha256(),
            string.Join(",", offers.Select(value =>
                value.moduleId + ":" + (int)value.polarity)),
            string.Join(",", evidence.Select(value => value.EvidenceId))))
            .Substring("sha256:".Length);
        string selectionId = "presentation:facility:" + stableHash;
        EvolutionNode node = new()
        {
            nodeId = "facility-formula-node:" + stableHash,
            generation = Math.Max(0, state.generation + 1),
            active = false,
            mechanicallyUnlocked = false,
            narrativeReady = false,
            uiVisible = false,
            playerVisible = false,
            formulaVersion = policy.FormulaVersion,
            formulaCatalogSha256 = recipe.formulaPolicy.RequireCatalogSha256(),
            formulaBudget = strength.Budget,
            evidenceIds = evidence.Select(value => value.EvidenceId)
                .OrderBy(value => value, StringComparer.Ordinal).ToList(),
            gameplayOutcomeEvidence = exactBindings
                .Select(value => value.Clone()).ToList(),
            presentationId = selectionId,
            moduleSelectionId = selectionId,
            moduleSelectionOffers = offers,
            presentationState = EquipmentEvolutionPresentationState.ModuleSelectionPending,
            presentationFailureCount = 0
        };
        state.formulaEvidence ??= new List<FacilityFormulaEvidenceRecord>();
        foreach (FacilityFormulaEvidenceRecord record in records)
        {
            if (!state.formulaEvidence.Any(value => value != null && string.Equals(
                    value.evidenceId, record.evidenceId, StringComparison.Ordinal)))
                state.formulaEvidence.Add(record.Clone());
        }
        state.formulaEvidence = state.formulaEvidence.Where(value => value != null)
            .OrderBy(value => value.evidenceId, StringComparer.Ordinal).ToList();
        BuildModuleSelectionRequest(node, recipe);
        return new FacilityEvolutionFormulaPresentationPendingSnapshot
        {
            recipeId = recipe.EffectiveId,
            sourceFacilityPersistentId = state.facilityPersistentId,
            presentationId = selectionId,
            node = node
        };
    }

    /// <summary>
    /// Optional drawbacks are prospective v3 offers, so they must be legal for
    /// at least one already-eligible positive module and numerically resolvable
    /// with this exact pending evidence/budget. Every selection-compatible
    /// positive is checked too, so an exposed legal pair cannot reach
    /// resolution only to fail after the player chooses it. Committed nodes and
    /// restore validation never pass through this gate.
    /// </summary>
    private static bool HasResolvableOptionalDrawbackPair(
        FacilityEvolutionState state,
        FacilityEvolutionRecipeSO recipe,
        IEnumerable<EquipmentEvolutionModuleOfferState> positiveOffers,
        EvolutionModuleDefinition drawbackModule,
        IEnumerable<FacilityFormulaEvidenceRecord> sourceEvidence)
    {
        FacilityFormulaEvidenceRecord[] previewEvidence = (sourceEvidence
                ?? Array.Empty<FacilityFormulaEvidenceRecord>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .ToArray();
        string[] evidenceIds = previewEvidence
            .Select(value => value.evidenceId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (evidenceIds.Length == 0
            || evidenceIds.Distinct(StringComparer.Ordinal).Count() != evidenceIds.Length)
        {
            return false;
        }
        if (evidenceIds.Length > MaximumProspectiveOptionalDrawbackEvidenceCount)
            return false;

        FacilityEvolutionState previewState = new()
        {
            facilityPersistentId = state?.facilityPersistentId ?? string.Empty,
            formulaEvidence = previewEvidence.Select(value => value.Clone()).ToList()
        };
        EvolutionNode prospective = new()
        {
            nodeId = "facility-prospective-drawback",
            formulaVersion = DrawbackModuleSelectionFormulaVersion,
            formulaCatalogSha256 = recipe.formulaPolicy.RequireCatalogSha256(),
            evidenceIds = evidenceIds.ToList(),
            presentationId = "facility:prospective-drawback",
            moduleSelectionId = "facility:prospective-drawback",
            moduleSelectionOffers = (positiveOffers
                    ?? Array.Empty<EquipmentEvolutionModuleOfferState>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .Append(new EquipmentEvolutionModuleOfferState
                {
                    moduleId = drawbackModule.ModuleId,
                    polarity = EvolutionModuleOfferPolarity.Drawback,
                    semanticDescription = DescribeModule(drawbackModule)
                })
                .OrderBy(value => value.moduleId, StringComparer.Ordinal)
                .ToList(),
            presentationState = EquipmentEvolutionPresentationState.ModuleSelectionPending
        };
        NarrativeFormulaModuleSelectionRequest request =
            BuildModuleSelectionRequest(prospective, recipe);

        bool hasResolvablePair = false;
        foreach (NarrativeFormulaModuleOffer positiveOffer in request.Offers
                     .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Positive)
                     .OrderBy(value => value.ModuleId, StringComparer.Ordinal))
        {
            bool hasSelectableEvidenceSubset = false;
            foreach (string[] selectedEvidenceIds in
                     EnumerateNonEmptyEvidenceSubsets(request.EvidenceFactIds))
            {
                NarrativeFormulaModuleSelectionChoice choice = new(
                    request.SelectionId,
                    new[] { positiveOffer.ModuleId },
                    new[] { drawbackModule.ModuleId },
                    selectedEvidenceIds);
                if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                        request, choice, out _, out _))
                {
                    continue;
                }

                hasSelectableEvidenceSubset = true;
                try
                {
                    ResolveSelectedModule(previewState, recipe, prospective, choice);
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
            hasResolvablePair |= hasSelectableEvidenceSubset;
        }

        return hasResolvablePair;
    }

    private static IEnumerable<string[]> EnumerateNonEmptyEvidenceSubsets(
        IReadOnlyList<string> evidenceIds)
    {
        if (evidenceIds == null || evidenceIds.Count == 0
            || evidenceIds.Count > MaximumProspectiveOptionalDrawbackEvidenceCount)
            yield break;
        int subsetCount = 1 << evidenceIds.Count;
        for (int mask = 1; mask < subsetCount; mask++)
        {
            List<string> subset = new();
            for (int index = 0; index < evidenceIds.Count; index++)
            {
                if ((mask & (1 << index)) != 0)
                    subset.Add(evidenceIds[index]);
            }
            yield return subset.ToArray();
        }
    }

    public static NarrativeFormulaModuleSelectionRequest BuildModuleSelectionRequest(
        EvolutionNode node,
        FacilityEvolutionRecipeSO recipe)
    {
        if (node == null || recipe == null
            || node.formulaVersion < ModuleSelectionFormulaVersion
            || node.presentationState != EquipmentEvolutionPresentationState.ModuleSelectionPending
            || string.IsNullOrWhiteSpace(node.moduleSelectionId))
            throw new InvalidOperationException("Facility module selection is not pending.");
        EquipmentEvolutionModuleOfferState[] persisted = (node.moduleSelectionOffers
                ?? new List<EquipmentEvolutionModuleOfferState>())
            .Where(value => value != null)
            .OrderBy(value => value.moduleId, StringComparer.Ordinal).ToArray();
        IReadOnlyDictionary<string, string> authoredCapabilityIdsByModule = recipe
            .RequireFormulaCapabilities()
            .ToDictionary(
                value => value.evolutionModuleId,
                value => value.ToRuntime().CapabilityId,
                StringComparer.Ordinal);
        List<NarrativeFormulaModuleOffer> offers = new();
        foreach (EquipmentEvolutionModuleOfferState offer in persisted)
        {
            if (!RegisteredModules.TryGet(offer.moduleId,
                    out EvolutionModuleDefinition module))
                throw new InvalidOperationException(
                    "Facility module offer is no longer registered: " + offer.moduleId);
            NarrativeFormulaCapabilityDescriptor descriptor;
            IEnumerable<string> requiredGroups = Array.Empty<string>();
            if (offer.polarity == EvolutionModuleOfferPolarity.Positive)
            {
                FacilityEvolutionFormulaCapabilityDefinition definition =
                    recipe.RequireFormulaCapabilityForModule(offer.moduleId);
                if (!module.IsPositiveModule)
                    throw new InvalidOperationException(
                        "Facility positive offer became an optional drawback: " + offer.moduleId);
                if (!IsEligiblePositiveModule(recipe, module, out string eligibilityFailure))
                    throw new InvalidOperationException(
                        "Facility pending positive offer is unavailable for its target: "
                        + eligibilityFailure);
                descriptor = definition.ToRuntime();
            }
            else
            {
                if (node.formulaVersion < DrawbackModuleSelectionFormulaVersion)
                    throw new InvalidOperationException(
                        "Legacy facility selection cannot contain independent drawbacks.");
                if (!IsEligibleOptionalDrawbackModule(
                        recipe, module, out string eligibilityFailure))
                    throw new InvalidOperationException(
                        "Facility pending drawback offer is unavailable for its target: "
                        + eligibilityFailure);
                descriptor = EvolutionModuleFormulaDescriptor.ForOptionalDrawback(
                    module,
                    "facility-drawback",
                    module.ForbiddenSynergyModuleIds
                        .Where(authoredCapabilityIdsByModule.ContainsKey)
                        .Select(value => authoredCapabilityIdsByModule[value]));
                requiredGroups = new[] { "facility-module" };
            }
            string semantic = DescribeModule(module);
            if (!string.Equals(semantic, offer.semanticDescription, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Facility module offer changed after submission: " + offer.moduleId);
            offers.Add(new NarrativeFormulaModuleOffer(
                offer.moduleId,
                EvolutionModuleFormulaDescriptor.ToNarrativePolarity(offer.polarity),
                semantic,
                new NarrativeFormulaCapabilityInput(
                    descriptor, descriptor.NarrativeAffinity, 1d),
                minimumFeasibilityCost:
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

    private sealed class FacilityFormulaResolvedModuleSelection
    {
        public NarrativeFormulaValidatedModuleSelection selection;
        public NarrativeFormulaModuleOffer selectedOffer;
        public FacilityEvolutionFormulaCapabilityDefinition definition;
        public EvolutionModuleDefinition module;
        public NarrativeFormulaCapabilityDescriptor descriptor;
        public NarrativeFormulaStrength strength;
        public bool hasNegativeEvidence;
        public NarrativeFormulaSelectedModuleResolution selectedResolution;
        public NarrativeFormulaCandidate winner;
    }

    /// <summary>
    /// Resolves the exact pending selection without changing state. Prospective
    /// drawback eligibility and final response freeze both use this path so an
    /// exposed pair cannot pass a different numeric calculation at preparation.
    /// </summary>
    private static FacilityFormulaResolvedModuleSelection ResolveSelectedModule(
        FacilityEvolutionState state,
        FacilityEvolutionRecipeSO recipe,
        EvolutionNode pending,
        NarrativeFormulaModuleSelectionChoice choice)
    {
        NarrativeFormulaModuleSelectionRequest request =
            BuildModuleSelectionRequest(pending, recipe);
        if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                request, choice, out NarrativeFormulaValidatedModuleSelection selection,
                out string selectionError))
            throw new InvalidOperationException(selectionError);
        NarrativeFormulaModuleOffer selectedOffer = selection.PositiveModules.Single();
        FacilityEvolutionFormulaCapabilityDefinition definition =
            recipe.RequireFormulaCapabilityForModule(selectedOffer.ModuleId);
        if (!RegisteredModules.TryGet(selectedOffer.ModuleId,
                out EvolutionModuleDefinition module))
            throw new InvalidOperationException(
                "Selected facility module is not an eligible authored positive module.");
        if (!IsEligiblePositiveModule(recipe, module, out string selectedEligibilityFailure))
            throw new InvalidOperationException(
                "Selected facility module is unavailable for its target: "
                + selectedEligibilityFailure);
        HashSet<string> exactIds = (pending.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        FacilityFormulaEvidenceRecord[] sourceEvidence = selection.EvidenceFactIds
            .Where(id => !exactIds.Contains(id))
            .Select(id => state.formulaEvidence?.SingleOrDefault(value => value != null
                && string.Equals(value.evidenceId, id, StringComparison.Ordinal)))
            .ToArray();
        if (sourceEvidence.Any(value => value == null))
            throw new InvalidOperationException(
                "Facility module selection lost authoritative evidence.");
        GameplayOutcomeEvidenceBindingSnapshot[] selectedExact =
            (pending.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null
                && selection.EvidenceFactIds.Contains(value.publicFactId,
                    StringComparer.Ordinal))
            .ToArray();
        NarrativeFormulaEvidence[] evidence = sourceEvidence.Select(ToFormulaEvidence)
            .Concat(selectedExact.Select(value =>
                GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(
                    value, recipe.RequireFormulaPolicy())))
            .ToArray();
        NarrativeFormulaStrength strength = NarrativeFormulaCore.CalculateStrength(
            recipe.RequireFormulaPolicy(), evidence);
        bool hasNegativeEvidence = sourceEvidence.Any(value => value.originalEvent != null
            && NarrativeFormulaNegativeEvidence.Matches(
                value.originalEvent.eventId, value.originalEvent.outcomeId))
            || selectedExact.Any(GameplayOutcomeEvidenceFormulaProjection.IsNegative);
        NarrativeFormulaCapabilityDescriptor descriptor = definition.ToRuntime();
        IReadOnlyList<NarrativeFormulaCandidate> candidates;
        NarrativeFormulaSelectedModuleResolution selectedResolution = null;
        if (selection.DrawbackModules.Count > 0)
        {
            IReadOnlyList<NarrativeFormulaSelectedModuleResolution> resolutions =
                NarrativeFormulaCore.OptimizeSelectedModules(
                    selection,
                    recipe.formulaPolicy.RequireGenerationContext(),
                    strength.Budget,
                    recipe.formulaPolicy.RequireDrawbackPolicy(),
                    NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                    hasNegativeEvidence,
                    maximumResults: 3);
            candidates = resolutions.Select(value => value.Positive).ToArray();
            if (candidates.Count > 0)
            {
                NarrativeFormulaCandidate picked = NarrativeFormulaCore.ChooseDeterministicWinner(
                    candidates,
                    state.facilityPersistentId,
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
                || module.BurdenKind == EvolutionModuleBurdenKind.InseparableRisk
                    ? new NarrativeFormulaDrawbackOption(
                        "facility-burden:" + selectedOffer.ModuleId,
                        Math.Max(1, module.RiskWeight),
                        NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                        true, true, false, false, hasNegativeEvidence)
                    : null;
            candidates = NarrativeFormulaCore.Optimize(
                descriptor,
                recipe.formulaPolicy.RequireGenerationContext(),
                strength.Budget,
                descriptor.NarrativeAffinity,
                Novelty(state, descriptor.CapabilityId),
                evidence.Select(value => value.EvidenceId),
                maximumResults: 3,
                drawbackPolicy: inseparable == null
                    ? null : recipe.formulaPolicy.RequireDrawbackPolicy(),
                drawbackOption: inseparable);
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException(
                "Selected facility module is numerically infeasible.");
        NarrativeFormulaCandidate winner = selectedResolution?.Positive
            ?? NarrativeFormulaCore.ChooseDeterministicWinner(
                candidates,
                state.facilityPersistentId,
                pending.nodeId,
                pending.formulaVersion,
                pending.formulaCatalogSha256);
        return new FacilityFormulaResolvedModuleSelection
        {
            selection = selection,
            selectedOffer = selectedOffer,
            definition = definition,
            module = module,
            descriptor = descriptor,
            strength = strength,
            hasNegativeEvidence = hasNegativeEvidence,
            selectedResolution = selectedResolution,
            winner = winner
        };
    }

    public static EvolutionNode FreezeSelectedModule(
        FacilityEvolutionState state,
        FacilityEvolutionRecipeSO recipe,
        EvolutionNode pending,
        NarrativeFormulaModuleSelectionChoice choice)
    {
        FacilityFormulaResolvedModuleSelection resolution =
            ResolveSelectedModule(state, recipe, pending, choice);
        EvolutionNode node = pending.Clone();
        node.effectId = resolution.selectedOffer.ModuleId;
        node.formulaBudget = resolution.strength.Budget;
        node.formulaCapabilities = resolution.winner.Allocations.Select(ToEnvelope).ToList();
        node.calculatedCost = resolution.winner.CalculatedCost;
        node.positiveCost = resolution.winner.PositiveCost;
        node.drawbackCredit = resolution.winner.DrawbackCredit;
        node.drawbackId = resolution.winner.DrawbackId;
        node.drawbackEvidenceQualified = resolution.selection.DrawbackModules.Count > 0
            || (pending.formulaVersion < DrawbackModuleSelectionFormulaVersion
                || resolution.module.BurdenKind == EvolutionModuleBurdenKind.InseparableRisk)
                && resolution.hasNegativeEvidence;
        node.burdenEffectId = resolution.selection.DrawbackModules.SingleOrDefault()?.ModuleId
            ?? string.Empty;
        node.burdenPotencyMultiplier = resolution.selectedResolution?.DrawbackSeverity == null
            ? 0f
            : ToPotency(
                resolution.selectedResolution.DrawbackSeverity,
                resolution.selectedResolution.DrawbackSeverity.Allocations.Single().Descriptor);
        node.evidenceIds = resolution.winner.EvidenceIds
            .OrderBy(value => value, StringComparer.Ordinal).ToList();
        node.gameplayOutcomeEvidence = GameplayOutcomeEvidenceFormulaProjection.Select(
            pending.gameplayOutcomeEvidence,
            node.evidenceIds);
        node.potencyMultiplier = ToPotency(resolution.winner, resolution.descriptor);
        node.mechanicalDescription = FormatMechanicalDescription(
            recipe, resolution.definition, resolution.winner, node.burdenEffectId,
            node.burdenPotencyMultiplier);
        node.presentationState = EquipmentEvolutionPresentationState.PresentationPending;
        return node;
    }

    /// <summary>
    /// Adds an approved formula lineage to the resolved result snapshot before
    /// materials are committed. The same snapshot is retained by the physical
    /// pending receipt, so replacement recovery cannot publish effect/history
    /// without its paired evidence-influence increments.
    /// </summary>
    public static bool TryFinalizeApprovedPresentation(
        FacilityEvolutionStateSnapshot resolved,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionFormulaPresentationPendingSnapshot pending,
        out string failureReason)
    {
        failureReason = string.Empty;
        try
        {
            if (resolved?.instanceEvolution == null || pending?.node == null
                || resolved.evolutionHistory == null || resolved.evolutionHistory.Count == 0)
                throw new InvalidOperationException(
                    "Facility formula result snapshot is incomplete.");

            FacilityEvolutionState state = resolved.instanceEvolution;
            state.evolutionNodes ??= new List<EvolutionNode>();
            state.activeNodeIds ??= new List<string>();
            state.formulaEvidence ??= new List<FacilityFormulaEvidenceRecord>();
            EvolutionNode node = pending.node.Clone();
            ValidatePendingNode(recipe, node);
            if (node.formulaVersion < 1
                || !string.Equals(node.presentationId, pending.presentationId,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(pending.displayName)
                || string.IsNullOrWhiteSpace(pending.narrativeFlavor)
                || ContainsMechanicalNumber(pending.displayName)
                || ContainsMechanicalNumber(pending.narrativeFlavor)
                || state.evolutionNodes.Any(value => value != null
                    && string.Equals(value.nodeId, node.nodeId, StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Facility formula presentation completion does not match its pending node.");

            node.displayName = pending.displayName.Trim();
            node.description = pending.narrativeFlavor.Trim();
            node.narrativeFlavor = pending.narrativeFlavor.Trim();
            node.presentationState = EquipmentEvolutionPresentationState.Ready;
            node.presentationFailureCount = pending.failureCount;
            node.active = true;
            node.mechanicallyUnlocked = true;
            node.narrativeReady = true;
            node.uiVisible = true;
            node.playerVisible = true;

            HashSet<string> exactIds = (node.gameplayOutcomeEvidence
                    ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
                .Where(value => value != null)
                .Select(value => value.publicFactId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string evidenceId in node.evidenceIds ?? new List<string>())
            {
                if (exactIds.Contains(evidenceId))
                    continue;
                FacilityFormulaEvidenceRecord evidence = state.formulaEvidence
                    .SingleOrDefault(value => value != null && string.Equals(
                        value.evidenceId, evidenceId, StringComparison.Ordinal));
                if (evidence == null || evidence.influenceUseCount == int.MaxValue)
                    throw new InvalidOperationException(
                        "Facility formula evidence is missing or exhausted.");
                evidence.influenceUseCount++;
            }

            state.evolutionNodes.Add(node);
            state.activeNodeIds = state.activeNodeIds
                .Append(node.nodeId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            resolved.evolutionHistory[^1].summary = node.narrativeFlavor;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            failureReason = exception.Message;
            return false;
        }
    }

    private static GameplayOutcomeEvidenceBindingSnapshot[] NormalizeBindings(
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        GameplayOutcomeEvidenceBindingSnapshot[] values = (bindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .ToArray();
        if (values.Any(value =>
                !GameplayOutcomeEvidenceBindingAuthority.TryValidate(value, out _))
            || values.Select(value => value.publicFactId)
                .Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException(
                "Facility formula exact-outcome evidence bindings are invalid.");
        return values
            .Select(value => value.Clone())
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidatePendingNode(
        FacilityEvolutionRecipeSO recipe,
        EvolutionNode node)
    {
        if (recipe == null || node == null)
            throw new InvalidOperationException("Facility formula pending authority is missing.");
        NarrativeFormulaStrengthPolicy formula = recipe.RequireFormulaPolicy();
        FacilityEvolutionFormulaCapabilityDefinition capability =
            node.formulaVersion >= ModuleSelectionFormulaVersion
                ? recipe.RequireFormulaCapabilityForModule(node.effectId)
                : recipe.RequireFormulaCapability();
        if (!RegisteredModules.TryGet(capability.evolutionModuleId,
                out EvolutionModuleDefinition module))
            throw new InvalidOperationException(
                "Facility formula pending node has no authored positive module.");
        if (!IsEligiblePositiveModule(recipe, module, out string positiveEligibilityFailure))
            throw new InvalidOperationException(
                "Facility formula pending node is unavailable for its target: "
                + positiveEligibilityFailure);
        NarrativeFormulaDrawbackResolution expectedDrawback;
        if (node.formulaVersion < DrawbackModuleSelectionFormulaVersion)
        {
            expectedDrawback = recipe.formulaPolicy.RequireDrawbackPolicy().Resolve(
                node.formulaBudget,
                new NarrativeFormulaDrawbackOption(
                    "facility-burden:" + capability.evolutionModuleId,
                    Math.Max(1, module.RiskWeight),
                    NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                    true, true, false, false, node.drawbackEvidenceQualified));
        }
        else if (!string.IsNullOrEmpty(node.burdenEffectId))
        {
            if (!RegisteredModules.TryGet(node.burdenEffectId,
                    out EvolutionModuleDefinition optional)
                || optional.BurdenKind != EvolutionModuleBurdenKind.OptionalDrawback
                || !IsEligibleOptionalDrawbackModule(recipe, optional, out _)
                || optional.ForbiddenSynergyModuleIds.Contains(
                    module.ModuleId, StringComparer.Ordinal)
                || node.burdenPotencyMultiplier < 1f
                || node.burdenPotencyMultiplier > optional.MaximumDrawbackSeverity
                || Math.Abs(node.burdenPotencyMultiplier
                    - Math.Round(node.burdenPotencyMultiplier)) > 0.001d
                || !node.drawbackEvidenceQualified)
                throw new InvalidOperationException(
                    "Facility optional drawback severity is invalid.");
            string drawbackId = "selected-drawbacks:"
                + NarrativeInferenceHash.ComputeSha256Utf8(optional.ModuleId)
                    .Substring("sha256:".Length);
            expectedDrawback = recipe.formulaPolicy.RequireDrawbackPolicy().Resolve(
                node.formulaBudget,
                new NarrativeFormulaDrawbackOption(
                    drawbackId,
                    (int)Math.Round(node.burdenPotencyMultiplier),
                    NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                    true, true, false, false, true));
        }
        else if (module.BurdenKind == EvolutionModuleBurdenKind.InseparableRisk)
        {
            expectedDrawback = recipe.formulaPolicy.RequireDrawbackPolicy().Resolve(
                node.formulaBudget,
                new NarrativeFormulaDrawbackOption(
                    "facility-burden:" + capability.evolutionModuleId,
                    Math.Max(1, module.RiskWeight),
                    NarrativeFormulaDrawbackSelectionKind.PlayerChoice,
                    true, true, false, false, node.drawbackEvidenceQualified));
        }
        else
        {
            expectedDrawback = NarrativeFormulaDrawbackResolution.None;
            if (node.burdenPotencyMultiplier != 0f)
                throw new InvalidOperationException(
                    "Facility burden-free allocation has stray drawback potency.");
        }
        if (node.formulaVersion > formula.FormulaVersion
            || node.formulaVersion >= DrawbackModuleSelectionFormulaVersion
                && !string.Equals(node.formulaCatalogSha256,
                    recipe.formulaPolicy.RequireCatalogSha256(), StringComparison.Ordinal)
            || !string.Equals(node.effectId, capability.evolutionModuleId,
                StringComparison.Ordinal)
            || node.formulaBudget < formula.BaseBudget
            || node.positiveCost < 0
            || node.drawbackCredit != expectedDrawback.AcceptedCredit
            || !string.Equals(node.drawbackId, expectedDrawback.DrawbackId,
                StringComparison.Ordinal)
            || node.calculatedCost != node.positiveCost - node.drawbackCredit
            || node.calculatedCost < 0
            || node.calculatedCost > node.formulaBudget)
        {
            throw new InvalidOperationException(
                "Facility formula pending node failed drawback-credit validation.");
        }
    }

    private static bool IsEligiblePositiveModule(
        FacilityEvolutionRecipeSO recipe,
        EvolutionModuleDefinition module,
        out string failureReason)
    {
        return FacilityEvolutionModifierApplicability
            .IsPositiveModuleEligibleForNewGeneration(
                recipe?.resultBuilding,
                module,
                out failureReason);
    }

    private static bool IsEligibleOptionalDrawbackModule(
        FacilityEvolutionRecipeSO recipe,
        EvolutionModuleDefinition module,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (module == null
            || module.BurdenKind != EvolutionModuleBurdenKind.OptionalDrawback
            || module.Burdens.Count == 0)
        {
            failureReason = "module is not an authored optional drawback";
            return false;
        }
        if (recipe?.resultBuilding == null)
        {
            failureReason = "recipe target building definition is missing";
            return false;
        }
        return FacilityEvolutionModifierApplicability.AreAllStatsReachable(
            recipe.resultBuilding,
            module.Burdens,
            "optional burden",
            out failureReason);
    }

    private static IEnumerable<FacilityFormulaEvidenceRecord> BuildEvidence(
        FacilityEvolutionState state,
        NarrativeFormulaStrengthPolicy policy)
    {
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        foreach (UsageLedgerEvent item in state.usageLedger?.currentGenerationEvents
                     ?? new List<UsageLedgerEvent>())
        {
            if (item == null || string.IsNullOrWhiteSpace(item.evidenceId)
                || string.IsNullOrWhiteSpace(item.eventId))
                continue;
            string evidenceId = "facility-fact:" + NarrativeInferenceHash.ComputeSha256Utf8(
                string.Join("|", state.facilityPersistentId, item.evidenceId, item.sequence))
                .Substring("sha256:".Length);
            FacilityFormulaEvidenceRecord previous = state.formulaEvidence?
                .SingleOrDefault(value => value != null && string.Equals(value.evidenceId,
                    evidenceId, StringComparison.Ordinal));
            yield return previous?.Clone() ?? new FacilityFormulaEvidenceRecord
            {
                evidenceId = evidenceId,
                eventGroupKey = item.eventId.Trim(),
                actionKey = string.IsNullOrWhiteSpace(item.outcomeId)
                    ? item.eventId.Trim() : item.outcomeId.Trim(),
                relationshipKey = (item.actorId?.Trim() ?? string.Empty) + ">"
                    + (item.targetId?.Trim() ?? string.Empty),
                domainKey = "facility",
                attainedMilestoneCount = Math.Min(
                    policy.MilestoneWeights.Count,
                    Math.Max(1, item.repeatCount)),
                importancePoints = (float)Math.Clamp(
                    Math.Max(0.25d, Math.Abs((double)item.amount)),
                    policy.MinimumImportance,
                    policy.MaximumImportance),
                influenceUseCount = 0,
                originalEvent = item.Clone()
            };
        }
    }

    private static NarrativeFormulaEvidence ToFormulaEvidence(
        FacilityFormulaEvidenceRecord value) => new(
            value.evidenceId,
            value.eventGroupKey,
            value.actionKey,
            value.relationshipKey,
            value.domainKey,
            value.attainedMilestoneCount,
            value.importancePoints,
            value.influenceUseCount);

    private static string DescribeModule(EvolutionModuleDefinition module)
    {
        string benefits = string.Join(", ", module.Benefits
            .Select(value => value.statId).OrderBy(value => value, StringComparer.Ordinal));
        string burdens = string.Join(", ", module.Burdens
            .Select(value => value.statId).OrderBy(value => value, StringComparer.Ordinal));
        string description = module.BurdenKind switch
        {
            EvolutionModuleBurdenKind.None =>
                module.DisplayName + ": " + benefits + " 강화; 추가 부담 없음",
            EvolutionModuleBurdenKind.OperatingCost =>
                module.DisplayName + ": " + benefits + " 강화; " + burdens
                    + " 운영비/요구량 (예산 보너스 없음)",
            EvolutionModuleBurdenKind.InseparableRisk =>
                module.DisplayName + ": " + benefits + " 강화; " + burdens
                    + " 효과와 분리할 수 없는 위험",
            EvolutionModuleBurdenKind.OptionalDrawback =>
                module.DisplayName + ": 선택 시 " + burdens
                    + " 부담; 부정 원장 근거가 있는 경우에만 예산 보너스",
            _ => throw new InvalidOperationException("Unknown evolution burden kind.")
        };
        return module.ForbiddenSynergyModuleIds.Count == 0
            ? description
            : description + "; 함께 선택 금지 이로운 기능="
                + string.Join(",", module.ForbiddenSynergyModuleIds);
    }

    private static double Novelty(FacilityEvolutionState state, string capabilityId) =>
        (state.evolutionNodes ?? new List<EvolutionNode>()).Any(value => value != null
            && value.formulaVersion > 0 && value.formulaCapabilities != null
            && value.formulaCapabilities.Any(envelope => envelope != null
                && string.Equals(envelope.capabilityId, capabilityId, StringComparison.Ordinal))) ? 0d : 1d;

    private static bool ContainsMechanicalNumber(string value) => (value ?? string.Empty)
        .Any(character => character is >= '0' and <= '9' or >= '０' and <= '９' or '%' or '％');

    private static EquipmentEvolutionFormulaCapabilityEnvelope ToEnvelope(
        NarrativeFormulaCapabilityAllocation allocation) => new()
    {
        capabilityId = allocation.Descriptor.CapabilityId,
        formatterId = allocation.Descriptor.FormatterId,
        applicatorId = allocation.Descriptor.ApplicatorId,
        parameters = allocation.Parameters.Select(value => new EquipmentEvolutionFormulaParameterEnvelope
        {
            parameterId = value.ParameterId,
            units = value.Units
        }).ToList()
    };

    private static float ToPotency(
        NarrativeFormulaCandidate candidate,
        NarrativeFormulaCapabilityDescriptor descriptor)
    {
        NarrativeFormulaParameterValue magnitude = candidate.Parameters.Single(value =>
            string.Equals(value.ParameterId, NarrativeFormulaParameterIds.Magnitude,
                StringComparison.Ordinal));
        return Math.Max(0.01f, (float)descriptor.RequireRange(
            NarrativeFormulaParameterIds.Magnitude).ToDecimal(magnitude.Units));
    }

    private static string FormatMechanicalDescription(
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionFormulaCapabilityDefinition capability,
        NarrativeFormulaCandidate candidate,
        string optionalDrawbackModuleId = "",
        float optionalDrawbackPotency = 0f)
    {
        if (!RegisteredModules.TryGet(capability.evolutionModuleId,
                out EvolutionModuleDefinition module))
            throw new InvalidOperationException(
                "Facility mechanical description cannot resolve its module.");
        string paired = module.BurdenKind switch
        {
            EvolutionModuleBurdenKind.OperatingCost => "; 운영비="
                + FormatModifiers(module.Burdens, 1f),
            EvolutionModuleBurdenKind.InseparableRisk => "; 불가분 위험="
                + FormatModifiers(module.Burdens, 1f),
            _ => "; 추가 부담 없음"
        };
        string optional = string.Empty;
        if (!string.IsNullOrEmpty(optionalDrawbackModuleId))
        {
            if (!RegisteredModules.TryGet(optionalDrawbackModuleId,
                    out EvolutionModuleDefinition drawback)
                || drawback.BurdenKind != EvolutionModuleBurdenKind.OptionalDrawback)
                throw new InvalidOperationException(
                    "Facility mechanical description cannot resolve its optional drawback.");
            optional = "; 선택 단점=" + drawback.DisplayName + " "
                + FormatModifiers(drawback.Burdens, optionalDrawbackPotency);
        }
        return recipe.DisplayName + ": " + capability.evolutionModuleId + " potency="
            + ToPotency(candidate, candidate.Descriptor).ToString("0.####", CultureInfo.InvariantCulture)
            + paired + optional
            + "; positiveCost=" + candidate.PositiveCost.ToString(CultureInfo.InvariantCulture)
            + "; drawbackCredit=" + candidate.DrawbackCredit.ToString(CultureInfo.InvariantCulture)
            + "; netCost=" + candidate.CalculatedCost.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatModifiers(
        IReadOnlyList<EvolutionEffectModifier> modifiers,
        float potency) => string.Join(", ", (modifiers
            ?? Array.Empty<EvolutionEffectModifier>()).Select(value =>
            value.statId + (value.additive != 0f
                ? " " + (value.additive * potency).ToString("+0.####;-0.####;0",
                    CultureInfo.InvariantCulture)
                : " ×" + (1f + (value.multiplier - 1f) * potency)
                    .ToString("0.####", CultureInfo.InvariantCulture))));
}

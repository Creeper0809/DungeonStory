using System;
using System.Collections.Generic;
using System.Linq;

public enum CharacterAcquiredTraitValidationIssueCode
{
    MissingState,
    UnsupportedFormat,
    InvalidRevision,
    InvalidLedger,
    MissingSettings,
    InvalidSettings,
    InvalidModuleDefinition,
    InvalidInstance,
    InvalidPendingRequest,
    InvalidMilestone,
    InvalidEvidence,
    InvalidCombination,
    DomainMismatch,
    BudgetExceeded,
    Conflict,
    ActiveCapacityExceeded,
    DuplicateIdentity,
    NonCanonicalState
}

public readonly struct CharacterAcquiredTraitValidationIssue
{
    public CharacterAcquiredTraitValidationIssue(
        CharacterAcquiredTraitValidationIssueCode code,
        string message)
    {
        Code = code;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException(
                "An acquired-trait validation message is required.",
                nameof(message))
            : message.Trim();
    }

    public CharacterAcquiredTraitValidationIssueCode Code { get; }
    public string Message { get; }
    public override string ToString() => $"{Code}: {Message}";
}

public static class CharacterAcquiredTraitMeaningfulLedgerValidator
{
    private static readonly int[] MilestoneThresholds = { 1, 3, 8, 20, 50 };

    public static IReadOnlyList<int> Thresholds =>
        (int[])MilestoneThresholds.Clone();

    public static int RecomputeMilestoneCount(int factCount)
    {
        if (factCount < 0)
            throw new ArgumentOutOfRangeException(nameof(factCount));
        return MilestoneThresholds.Count(threshold => factCount >= threshold);
    }

    public static bool TryValidate(
        CharacterNarrativeLedger ledger,
        out string failure)
    {
        if (ledger?.facts == null)
        {
            failure = "the meaningful narrative ledger is missing";
            return false;
        }
        for (int index = 0; index < ledger.facts.Count; index++)
        {
            CharacterNarrativeFact fact = ledger.facts[index];
            if (fact == null)
            {
                failure = $"meaningful narrative fact {index} is null";
                return false;
            }
            if (fact.count < 0)
            {
                failure =
                    $"meaningful narrative fact '{fact.factId}' has a negative count";
                return false;
            }
            int expected = RecomputeMilestoneCount(fact.count);
            if (fact.milestoneCount != expected)
            {
                failure =
                    $"meaningful narrative fact '{fact.factId}' milestoneCount "
                    + $"is {fact.milestoneCount}, expected exactly {expected} "
                    + "from thresholds 1,3,8,20,50";
                return false;
            }
        }
        failure = string.Empty;
        return true;
    }

    public static int RequireMeaningfulRecordCount(
        CharacterNarrativeLedger ledger)
    {
        if (!TryValidate(ledger, out string failure))
            throw new InvalidOperationException(failure);
        return checked(ledger.facts.Sum(value => value.milestoneCount));
    }

    public static IReadOnlyCollection<string> RequireMeaningfulFactIds(
        CharacterNarrativeLedger ledger)
    {
        if (!TryValidate(ledger, out string failure))
            throw new InvalidOperationException(failure);
        return ledger.facts
            .Where(value => value.milestoneCount > 0)
            .Select(value => value.factId?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>
/// Derives the acquired-trait gate score without changing the authoritative
/// narrative ledger. Subject-specific rows with the same domain and fact ID
/// contribute to one experience group before the shared 1/3/8/20/50
/// thresholds are applied.
/// </summary>
public static class CharacterAcquiredTraitExperienceScore
{
    public static IReadOnlyList<int> Thresholds =>
        CharacterAcquiredTraitMeaningfulLedgerValidator.Thresholds;

    public static int RecomputeGroupScore(int aggregateCount) =>
        CharacterAcquiredTraitMeaningfulLedgerValidator
            .RecomputeMilestoneCount(aggregateCount);

    public static bool TryCalculate(
        CharacterNarrativeLedger ledger,
        out int score,
        out string failure)
    {
        score = 0;
        if (!CharacterAcquiredTraitMeaningfulLedgerValidator.TryValidate(
                ledger,
                out failure))
        {
            return false;
        }
        if (ledger.facts.Any(value =>
                !Enum.IsDefined(typeof(CharacterNarrativeDomain), value.domain)
                || string.IsNullOrWhiteSpace(value.factId)
                || !string.Equals(
                    value.factId,
                    value.factId.Trim(),
                    StringComparison.Ordinal)))
        {
            failure = "acquired-trait experience facts require a valid domain and canonical fact ID";
            return false;
        }

        long totalScore = 0L;
        foreach (var group in ledger.facts.GroupBy(value => new
                 {
                     value.domain,
                     value.factId
                 }))
        {
            long aggregateCount = group.Sum(value => (long)value.count);
            if (aggregateCount > int.MaxValue)
            {
                failure = $"acquired-trait experience group '{group.Key.domain}:{group.Key.factId}' count exceeds Int32";
                return false;
            }
            totalScore += RecomputeGroupScore((int)aggregateCount);
            if (totalScore > int.MaxValue)
            {
                failure = "acquired-trait experience score exceeds Int32";
                return false;
            }
        }

        score = (int)totalScore;
        failure = string.Empty;
        return true;
    }

    public static int Require(CharacterNarrativeLedger ledger)
    {
        if (!TryCalculate(ledger, out int score, out string failure))
            throw new InvalidOperationException(failure);
        return score;
    }
}

public static class CharacterAcquiredTraitStateValidator
{
    public static IReadOnlyList<CharacterAcquiredTraitValidationIssue>
        ValidatePersistentState(
            CharacterAcquiredTraitAggregateState state,
            CharacterNarrativeLedger ledger)
    {
        List<CharacterAcquiredTraitValidationIssue> issues = new();
        if (state == null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.MissingState,
                "Acquired-trait aggregate state is missing.");
            return issues;
        }
        if (state.formatVersion != CharacterAcquiredTraitAggregateState.LegacyFormatVersion
            && state.formatVersion
            != CharacterAcquiredTraitAggregateState.FormulaFormatVersion
            && state.formatVersion
            != CharacterAcquiredTraitAggregateState.CurrentFormatVersion)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.UnsupportedFormat,
                $"Acquired-trait aggregate format {state.formatVersion} is unsupported.");
        }
        if (state.revision < 0)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidRevision,
                "Acquired-trait aggregate revision is negative.");
        }
        if (!CharacterAcquiredTraitMeaningfulLedgerValidator.TryValidate(
                ledger,
                out string ledgerFailure))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidLedger,
                ledgerFailure);
        }
        if (state.instances == null
            || state.processedMilestones == null
            || state.pendingRequests == null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.MissingState,
                "Acquired-trait aggregate contains a missing required collection.");
            return issues;
        }
        if (state.HasPersistentData && state.revision == 0)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidRevision,
                "Non-empty acquired-trait state requires a positive revision.");
        }
        if (state.instances.Any(value => value == null))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                "Acquired-trait instances contain a null entry.");
        }
        if (state.pendingRequests.Any(value => value == null))
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidPendingRequest,
                "Acquired-trait pending requests contain a null entry.");
        }
        if (state.processedMilestones.Any(value => value <= 0)
            || state.processedMilestones.Distinct().Count()
                != state.processedMilestones.Count)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidMilestone,
                "Processed acquired-trait milestones must be positive and unique.");
        }
        if (!state.processedMilestones.SequenceEqual(
                state.processedMilestones.OrderBy(value => value)))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.NonCanonicalState,
                "Processed acquired-trait milestones are not in ascending order.");
        }

        CharacterAcquiredTraitInstanceState[] instances = state.instances
            .Where(value => value != null)
            .ToArray();
        CharacterAcquiredTraitPendingRequestState[] pending = state.pendingRequests
            .Where(value => value != null)
            .ToArray();
        ValidateUniqueIdentities(instances, pending, issues);

        HashSet<string> meaningfulFactIds = new(StringComparer.Ordinal);
        if (CharacterAcquiredTraitMeaningfulLedgerValidator.TryValidate(
                ledger,
                out _))
        {
            CharacterNarrativeFact[] meaningfulFacts = ledger.Facts
                .Where(value => value != null && value.milestoneCount > 0)
                .ToArray();
            meaningfulFactIds.UnionWith(
                meaningfulFacts.Select(
                    CharacterAcquiredTraitEvidenceProjection.Project));
            foreach (IGrouping<string, CharacterNarrativeFact> legacyGroup in
                     meaningfulFacts.GroupBy(
                         value => value.factId?.Trim() ?? string.Empty,
                         StringComparer.Ordinal))
            {
                if (legacyGroup.Key.Length > 0
                    && legacyGroup.Select(
                            CharacterAcquiredTraitEvidenceProjection.Project)
                        .Distinct(StringComparer.Ordinal)
                        .Count() == 1)
                {
                    meaningfulFactIds.Add(legacyGroup.Key);
                }
            }
        }
        foreach (CharacterAcquiredTraitInstanceState instance in instances)
            ValidateInstance(state, instance, meaningfulFactIds, issues);
        foreach (CharacterAcquiredTraitPendingRequestState request in pending)
            ValidatePendingRequest(state, request, meaningfulFactIds, issues);
        if (state.formatVersion == CharacterAcquiredTraitAggregateState.LegacyFormatVersion
            && (instances.Any(value => value.formulaVersion > 0)
                || pending.Any(value => value.formulaVersion > 0)))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.UnsupportedFormat,
                "Legacy acquired-trait format 1 cannot contain formula-v1 mechanics.");
        }

        int[] instanceMilestones = instances
            .Select(value => value.manifestationMilestone)
            .ToArray();
        if (instanceMilestones.Distinct().Count() != instanceMilestones.Length)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidMilestone,
                "More than one acquired trait originates from the same milestone.");
        }
        if (!state.processedMilestones.SequenceEqual(
                instanceMilestones.OrderBy(value => value)))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidMilestone,
                "Processed milestones must exactly match manifested acquired-trait instances.");
        }
        int[] pendingMilestones = pending
            .Select(value => value.manifestationMilestone)
            .ToArray();
        if (pendingMilestones.Distinct().Count() != pendingMilestones.Length)
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidPendingRequest,
                "More than one pending acquired-trait request targets the same milestone.");
        }
        if (pending.Length > 1)
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidPendingRequest,
                "Exactly one acquired-trait milestone request may be pending at a time.");
        }
        if (pendingMilestones.Intersect(state.processedMilestones).Any())
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidPendingRequest,
                "A processed acquired-trait milestone still has a pending request.");
        }
        return issues;
    }

    public static IReadOnlyList<CharacterAcquiredTraitValidationIssue>
        ValidateWithDefinitions(
            CharacterAcquiredTraitAggregateState state,
            CharacterNarrativeLedger ledger,
            CharacterAcquiredTraitSettingsSO settings,
            IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions)
    {
        List<CharacterAcquiredTraitValidationIssue> issues =
            ValidatePersistentState(state, ledger).ToList();
        if (state == null)
            return issues;
        if (settings == null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.MissingSettings,
                "Acquired-trait validation requires authored settings authority.");
            return issues;
        }
        foreach (string error in settings.ValidateDefinition())
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidSettings,
                error);
        }

        CharacterAcquiredTraitModuleSO[] modules =
            (moduleDefinitions ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .ToArray();
        if (modules.Any(value => value == null))
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidModuleDefinition,
                "Acquired-trait module definitions contain a null entry.");
        }
        CharacterAcquiredTraitModuleSO[] concreteModules = modules
            .Where(value => value != null)
            .ToArray();
        if (concreteModules.GroupBy(value => value.ModuleId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.DuplicateIdentity,
                "Acquired-trait module definitions contain a duplicate module ID.");
        }
        foreach (CharacterAcquiredTraitModuleSO module in concreteModules)
        {
            foreach (string error in module.ValidateDefinition())
            {
                Add(issues,
                    CharacterAcquiredTraitValidationIssueCode.InvalidModuleDefinition,
                    error);
            }
        }
        Dictionary<string, CharacterAcquiredTraitModuleSO> modulesById =
            concreteModules
                .GroupBy(value => value.ModuleId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);

        CharacterAcquiredTraitInstanceState[] instances =
            (state.instances ?? new List<CharacterAcquiredTraitInstanceState>())
            .Where(value => value != null)
            .ToArray();
        CharacterAcquiredTraitPendingRequestState[] pending =
            (state.pendingRequests
                ?? new List<CharacterAcquiredTraitPendingRequestState>())
            .Where(value => value != null)
            .ToArray();
        int experienceScore = 0;
        bool validLedger = CharacterAcquiredTraitExperienceScore.TryCalculate(
                ledger,
                out experienceScore,
                out _);

        if (instances.Count(value => !value.erased) > settings.MaximumActiveTraits)
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.ActiveCapacityExceeded,
                $"Active acquired-trait count exceeds settings limit "
                + $"{settings.MaximumActiveTraits}.");
        }
        foreach (CharacterAcquiredTraitInstanceState instance in instances)
        {
            ValidateManifestationAgainstDefinitions(
                instance,
                experienceScore,
                validLedger,
                ledger,
                settings,
                modulesById,
                issues);
        }
        ValidateActiveCoexistence(instances, settings, modulesById, issues);
        foreach (CharacterAcquiredTraitPendingRequestState request in pending)
        {
            if (!settings.TryGetGate(request.manifestationMilestone, out _))
            {
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidMilestone,
                    $"Pending request '{request.requestId}' references unauthored "
                    + $"milestone {request.manifestationMilestone}.");
            }
            else if (validLedger
                && experienceScore < request.manifestationMilestone)
            {
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidMilestone,
                    $"Pending request '{request.requestId}' milestone "
                    + $"{request.manifestationMilestone} exceeds validated meaningful "
                    + $"acquired-trait experience score {experienceScore}.");
            }
            if (request.presentationState == CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
            {
                ValidatePendingModuleOffers(request, settings, modulesById, issues);
            }
            else
            {
                ValidateFormulaV2CompositionAgainstDefinitions(
                    request.formulaVersion,
                    request.benefitModuleIds,
                    request.drawbackCapabilityIds,
                    request.evidenceFactIds,
                    request.formulaCapabilities,
                    request.effectOverrides,
                    request.positiveCost,
                    request.drawbackCredit,
                    request.narrativeBudget,
                    ledger,
                    settings,
                    modulesById,
                    issues,
                    $"Pending acquired-trait request '{request.requestId}'");
            }
        }
        if (validLedger && pending.Length == 1)
        {
            int nextEligibleMilestone = settings.ManifestationGates
                .Where(value => value != null
                    && value.MeaningfulRecordMilestone <= experienceScore
                    && !(state.processedMilestones ?? new List<int>()).Contains(
                        value.MeaningfulRecordMilestone))
                .Select(value => value.MeaningfulRecordMilestone)
                .OrderBy(value => value)
                .FirstOrDefault();
            if (pending[0].manifestationMilestone != nextEligibleMilestone)
            {
                Add(issues,
                    CharacterAcquiredTraitValidationIssueCode.InvalidMilestone,
                    $"Pending request '{pending[0].requestId}' milestone "
                    + $"{pending[0].manifestationMilestone} is not the next eligible "
                    + $"unprocessed milestone {nextEligibleMilestone}.");
            }
        }
        return issues;
    }

    private static void ValidateInstance(
        CharacterAcquiredTraitAggregateState state,
        CharacterAcquiredTraitInstanceState instance,
        ISet<string> meaningfulFactIds,
        ICollection<CharacterAcquiredTraitValidationIssue> issues)
    {
        if (!IsCanonicalRequired(instance.instanceId)
            || (instance.formulaVersion == 0 && !IsCanonicalRequired(instance.combinationId))
            || (instance.formulaVersion > 0 && !string.IsNullOrEmpty(instance.combinationId))
            || !IsCanonicalRequired(instance.displayName)
            || !IsCanonicalRequired(instance.description)
            || !IsCanonicalRequired(instance.narrativeReason)
            || !IsCanonicalRequired(instance.originatingRequestId)
            || !IsCanonicalRequired(instance.originatingRequestKey)
            || !IsSha256(instance.candidatePacketHash)
            || !IsCanonicalRequired(instance.selectionAuditId)
            || instance.manifestationMilestone <= 0)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                $"Acquired-trait instance '{instance.instanceId}' has invalid identity, "
                + "narrative, request, audit, hash, or milestone data.");
        }
        if (instance.moduleIds == null
            || instance.evidenceFactIds == null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                $"Acquired-trait instance '{instance.instanceId}' has a missing collection.");
            return;
        }
        if (!IsCanonicalSortedUnique(instance.moduleIds)
            || instance.moduleIds.Count
                is < CharacterAcquiredTraitCombinationIdentity.MinimumModuleCount
                or > CharacterAcquiredTraitCombinationIdentity.MaximumModuleCount)
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidCombination,
                $"Acquired-trait instance '{instance.instanceId}' has a non-canonical "
                + "one-to-three module combination.");
        }
        else if (instance.formulaVersion == 0)
        {
            string expected = CharacterAcquiredTraitCombinationIdentity.Build(
                instance.moduleIds);
            if (!string.Equals(
                    expected,
                    instance.combinationId,
                    StringComparison.Ordinal))
            {
                Add(issues,
                    CharacterAcquiredTraitValidationIssueCode.InvalidCombination,
                    $"Acquired-trait instance '{instance.instanceId}' combination ID "
                    + "does not match its canonical module IDs.");
            }
        }
        HashSet<string> exactEvidenceIds = ValidateExactEvidenceBindings(
            instance.evidenceBindings,
            instance.evidenceFactIds,
            issues,
            $"Acquired-trait instance '{instance.instanceId}'");
        if (instance.evidenceFactIds.Count == 0
            || !IsCanonicalSortedUnique(instance.evidenceFactIds)
            || instance.evidenceFactIds.Any(value =>
                !meaningfulFactIds.Contains(value)
                && !exactEvidenceIds.Contains(value)))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                $"Acquired-trait instance '{instance.instanceId}' contains invalid, "
                + "duplicate, non-meaningful, or non-canonical evidence fact IDs.");
        }
        ValidateFormulaCompositionIdentity(
            instance.formulaVersion,
            instance.moduleIds,
            instance.benefitModuleIds,
            instance.drawbackCapabilityIds,
            instance.drawbackId,
            instance.drawbackCredit,
            issues,
            $"Acquired-trait instance '{instance.instanceId}'");
        ValidateFormulaEnvelope(
            instance.formulaVersion,
            instance.formulaCatalogSha256,
            instance.calculatedCost,
            instance.positiveCost,
            instance.drawbackCredit,
            instance.drawbackId,
            instance.narrativeBudget,
            instance.formulaCapabilities,
            instance.effectOverrides,
            instance.presentationId,
            instance.mechanicalDescription,
            instance.displayName,
            instance.narrativeFlavor,
            instance.formulaVersion == 0
                ? CharacterAcquiredTraitPresentationState.None
                : CharacterAcquiredTraitPresentationState.Ready,
            issues,
            $"Acquired-trait instance '{instance.instanceId}'");
        if (instance.acceptedRevision <= 0
            || instance.acceptedRevision > state.revision)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidRevision,
                $"Acquired-trait instance '{instance.instanceId}' has invalid accepted revision.");
        }
        if (instance.erased)
        {
            if (!IsCanonicalRequired(instance.erasureAuditId)
                || instance.erasedAt < 0L)
            {
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                    $"Erased acquired-trait instance '{instance.instanceId}' has invalid "
                    + "erasure time or audit data.");
            }
            if (instance.erasedRevision <= instance.acceptedRevision
                || instance.erasedRevision > state.revision)
            {
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidRevision,
                    $"Erased acquired-trait instance '{instance.instanceId}' has invalid "
                    + "erasure revision data.");
            }
        }
        else if (!string.IsNullOrEmpty(instance.erasureAuditId)
            || instance.erasedAt
                != CharacterAcquiredTraitInstanceState.NotErasedAtAbsoluteHour
            || instance.erasedRevision != 0)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                $"Active acquired-trait instance '{instance.instanceId}' contains erasure data.");
        }
    }

    private static void ValidatePendingRequest(
        CharacterAcquiredTraitAggregateState state,
        CharacterAcquiredTraitPendingRequestState request,
        ISet<string> meaningfulFactIds,
        ICollection<CharacterAcquiredTraitValidationIssue> issues)
    {
        if (!IsCanonicalRequired(request.targetPersistentId)
            || !IsCanonicalRequired(request.requestId)
            || !IsCanonicalRequired(request.requestKey)
            || !IsSha256(request.candidatePacketHash)
            || !IsCanonicalRequired(request.submissionAuditId)
            || request.manifestationMilestone <= 0)
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidPendingRequest,
                $"Pending acquired-trait request '{request.requestId}' has invalid "
                + "target, identity, audit, hash, or milestone data.");
        }
        HashSet<string> exactEvidenceIds = ValidateExactEvidenceBindings(
            request.evidenceBindings,
            request.evidenceFactIds,
            issues,
            $"Pending acquired-trait request '{request.requestId}'");
        if (request.evidenceFactIds == null
            || request.evidenceFactIds.Count == 0
            || !IsCanonicalSortedUnique(request.evidenceFactIds)
            || request.evidenceFactIds.Any(value =>
                !meaningfulFactIds.Contains(value)
                && !exactEvidenceIds.Contains(value)))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                $"Pending acquired-trait request '{request.requestId}' contains "
                + "invalid, duplicate, non-meaningful, or non-canonical evidence fact IDs.");
        }
        if (request.registeredRevision <= 0
            || request.registeredRevision > state.revision)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidRevision,
                $"Pending acquired-trait request '{request.requestId}' has invalid revision.");
        }
        if (request.presentationState == CharacterAcquiredTraitPresentationState.ModuleSelectionPending)
        {
            bool validSelection = request.formulaVersion
                    >= CharacterAcquiredTraitFormulaGeneration.ModuleSelectionFormulaVersion
                && IsCanonicalRequired(request.formulaCatalogSha256)
                && request.formulaCatalogSha256.Length == 64
                && IsCanonicalRequired(request.moduleSelectionId)
                && request.moduleSelectionId.StartsWith("selection:trait:", StringComparison.Ordinal)
                && request.offeredBenefitModuleIds != null
                && request.offeredBenefitModuleIds.Count > 0
                && IsCanonicalSortedUnique(request.offeredBenefitModuleIds)
                && request.offeredDrawbackModuleIds != null
                && IsCanonicalSortedUnique(request.offeredDrawbackModuleIds)
                && !request.offeredBenefitModuleIds.Intersect(
                    request.offeredDrawbackModuleIds, StringComparer.Ordinal).Any()
                && request.moduleOffers != null
                && request.moduleOffers.Count == request.offeredBenefitModuleIds.Count
                    + request.offeredDrawbackModuleIds.Count
                && request.moduleOffers.All(value => value != null
                    && IsCanonicalRequired(value.moduleId)
                    && IsCanonicalRequired(value.semanticDescription)
                    && (value.polarity == NarrativeFormulaModulePolarity.Positive
                        ? request.offeredBenefitModuleIds.Contains(value.moduleId, StringComparer.Ordinal)
                        : value.polarity == NarrativeFormulaModulePolarity.Drawback
                            && request.offeredDrawbackModuleIds.Contains(value.moduleId, StringComparer.Ordinal)))
                && request.moduleOffers.Select(value => value.moduleId)
                    .Distinct(StringComparer.Ordinal).Count() == request.moduleOffers.Count
                && (request.benefitModuleIds?.Count ?? 0) == 0
                && (request.drawbackCapabilityIds?.Count ?? 0) == 0
                && (request.formulaCapabilities?.Count ?? 0) == 0
                && (request.effectOverrides?.Count ?? 0) == 0
                && request.calculatedCost == 0 && request.positiveCost == 0
                && request.drawbackCredit == 0 && string.IsNullOrEmpty(request.drawbackId)
                && request.narrativeBudget >= 0
                && string.IsNullOrEmpty(request.presentationId)
                && string.IsNullOrEmpty(request.mechanicalDescription);
            if (!validSelection)
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidPendingRequest,
                    $"Pending acquired-trait request '{request.requestId}' has an invalid unresolved module offer.");
            return;
        }
        ValidateFormulaCompositionIdentity(
            request.formulaVersion,
            null,
            request.benefitModuleIds,
            request.drawbackCapabilityIds,
            request.drawbackId,
            request.drawbackCredit,
            issues,
            $"Pending acquired-trait request '{request.requestId}'");
        ValidateFormulaEnvelope(
            request.formulaVersion,
            request.formulaCatalogSha256,
            request.calculatedCost,
            request.positiveCost,
            request.drawbackCredit,
            request.drawbackId,
            request.narrativeBudget,
            request.formulaCapabilities,
            request.effectOverrides,
            request.presentationId,
            request.mechanicalDescription,
            string.Empty,
            string.Empty,
            request.presentationState,
            issues,
            $"Pending acquired-trait request '{request.requestId}'");
    }

    private static HashSet<string> ValidateExactEvidenceBindings(
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        IReadOnlyList<string> evidenceFactIds,
        ICollection<CharacterAcquiredTraitValidationIssue> issues,
        string owner)
    {
        GameplayOutcomeEvidenceBindingSnapshot[] values = (bindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .ToArray();
        bool invalid = values.Any(value =>
                !GameplayOutcomeEvidenceBindingAuthority.TryValidate(value, out _))
            || values.Select(value => value.publicFactId)
                .Distinct(StringComparer.Ordinal).Count() != values.Length
            || values.Any(value => !(evidenceFactIds ?? Array.Empty<string>())
                .Contains(value.publicFactId, StringComparer.Ordinal));
        if (invalid)
        {
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                owner + " has invalid exact gameplay-outcome evidence bindings.");
            return new HashSet<string>(StringComparer.Ordinal);
        }
        return values.Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void ValidatePendingModuleOffers(
        CharacterAcquiredTraitPendingRequestState request,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
        ICollection<CharacterAcquiredTraitValidationIssue> issues)
    {
        HashSet<string> drawbackIds = settings.DrawbackCapabilities
            .Where(value => value != null).Select(value => value.DrawbackId)
            .ToHashSet(StringComparer.Ordinal);
        if (request.formulaVersion != settings.RequireFormulaPolicy().FormulaVersion
            || request.offeredBenefitModuleIds.Any(value => !modulesById.ContainsKey(value))
            || request.offeredDrawbackModuleIds.Any(value => !drawbackIds.Contains(value)))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidPendingRequest,
                $"Pending acquired-trait request '{request.requestId}' references stale or unknown module offers.");
        }
    }

    private static void ValidateUniqueIdentities(
        IReadOnlyCollection<CharacterAcquiredTraitInstanceState> instances,
        IReadOnlyCollection<CharacterAcquiredTraitPendingRequestState> pending,
        ICollection<CharacterAcquiredTraitValidationIssue> issues)
    {
        if (instances.GroupBy(value => value.instanceId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1)
            || instances.GroupBy(value => value.selectionAuditId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1)
            || instances.GroupBy(value => value.originatingRequestId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1)
            || instances.GroupBy(value => value.originatingRequestKey, StringComparer.Ordinal)
                .Any(group => group.Count() > 1)
            || pending.GroupBy(value => value.requestId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1)
            || pending.GroupBy(value => value.requestKey, StringComparer.Ordinal)
                .Any(group => group.Count() > 1)
            || pending.GroupBy(value => value.submissionAuditId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.DuplicateIdentity,
                "Acquired-trait state contains duplicate instance, request, or audit identity.");
        }
        HashSet<string> completedRequests = instances
            .Select(value => value.originatingRequestId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> completedRequestKeys = instances
            .Select(value => value.originatingRequestKey)
            .ToHashSet(StringComparer.Ordinal);
        if (pending.Any(value => completedRequests.Contains(value.requestId)
                || completedRequestKeys.Contains(value.requestKey)))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.DuplicateIdentity,
                "A completed acquired-trait request ID or key is still pending.");
        }
    }

    private static void ValidateManifestationAgainstDefinitions(
        CharacterAcquiredTraitInstanceState instance,
        int experienceScore,
        bool validLedger,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
        ICollection<CharacterAcquiredTraitValidationIssue> issues)
    {
        if (!settings.TryGetGate(
                instance.manifestationMilestone,
                out CharacterAcquiredTraitManifestationGateDefinition gate))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidMilestone,
                $"Acquired-trait instance '{instance.instanceId}' references unauthored "
                + $"milestone {instance.manifestationMilestone}.");
            return;
        }
        if (validLedger && experienceScore < instance.manifestationMilestone)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidMilestone,
                $"Acquired-trait instance '{instance.instanceId}' milestone "
                    + $"{instance.manifestationMilestone} exceeds validated meaningful "
                    + $"acquired-trait experience score {experienceScore}.");
        }
        CharacterAcquiredTraitModuleSO[] selected =
            (instance.moduleIds ?? new List<string>())
            .Where(modulesById.ContainsKey)
            .Select(moduleId => modulesById[moduleId])
            .ToArray();
        if (selected.Length != (instance.moduleIds?.Count ?? 0))
        {
            string missing = (instance.moduleIds ?? new List<string>())
                .FirstOrDefault(moduleId => !modulesById.ContainsKey(moduleId));
            Add(issues,
                CharacterAcquiredTraitValidationIssueCode.InvalidModuleDefinition,
                $"Acquired-trait instance '{instance.instanceId}' references unknown "
                + $"module '{missing ?? string.Empty}'.");
            return;
        }
        ValidateReactionStates(instance, selected, issues);
        ValidateFormulaV2CompositionAgainstDefinitions(
            instance.formulaVersion,
            instance.benefitModuleIds,
            instance.drawbackCapabilityIds,
            instance.evidenceFactIds,
            instance.formulaCapabilities,
            instance.effectOverrides,
            instance.positiveCost,
            instance.drawbackCredit,
            instance.narrativeBudget,
            ledger,
            settings,
            modulesById,
            issues,
            $"Acquired-trait instance '{instance.instanceId}'");
        if (validLedger)
        {
            HashSet<string> evidence = (instance.evidenceFactIds
                    ?? new List<string>())
                .ToHashSet(StringComparer.Ordinal);
            HashSet<CharacterNarrativeDomain> evidenceDomains = new();
            HashSet<string> originalEvidenceFactIds = new(StringComparer.Ordinal);
            foreach (string evidenceId in evidence)
            {
                if (CharacterAcquiredTraitEvidenceProjection.TryResolve(
                        ledger,
                        evidenceId,
                        out CharacterNarrativeFact[] facts,
                        out _))
                {
                    originalEvidenceFactIds.UnionWith(facts.Select(
                        value => value.factId?.Trim() ?? string.Empty));
                }
            }
            evidenceDomains.UnionWith(ledger.Facts
                .Where(value => value != null
                    && value.count > 0
                    && originalEvidenceFactIds.Contains(
                        value.factId?.Trim() ?? string.Empty))
                .Select(value => value.domain));
            string mismatchedModule = selected
                .Where(value => !value.DomainAffinities.Any(
                    evidenceDomains.Contains))
                .Select(value => value.ModuleId)
                .FirstOrDefault();
            if (mismatchedModule != null)
            {
                Add(issues,
                    CharacterAcquiredTraitValidationIssueCode.DomainMismatch,
                    $"Acquired-trait instance '{instance.instanceId}' module "
                    + $"'{mismatchedModule}' is outside its evidence domains.");
            }
        }
        long cost = selected.Sum(value => (long)value.Cost);
        if (cost > gate.Budget)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.BudgetExceeded,
                $"Acquired-trait instance '{instance.instanceId}' cost {cost} exceeds "
                + $"milestone {gate.MeaningfulRecordMilestone} budget {gate.Budget}.");
        }
        string duplicateConflict = selected
            .SelectMany(value => value.ConflictGroups
                .Select(group => group?.Trim() ?? string.Empty))
            .Where(value => value.Length > 0)
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateConflict != null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                $"Acquired-trait instance '{instance.instanceId}' combines conflicting "
                + $"group '{duplicateConflict}'.");
        }
        string duplicateBinding = selected
            .SelectMany(value => value.Effects)
            .Where(value => value != null)
            .GroupBy(value => value.bindingId?.Trim() ?? string.Empty,
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateBinding != null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                $"Acquired-trait instance '{instance.instanceId}' combines duplicate "
                + $"effect binding ID '{duplicateBinding}'.");
        }
    }

    private static void ValidateReactionStates(
        CharacterAcquiredTraitInstanceState instance,
        IEnumerable<CharacterAcquiredTraitModuleSO> selectedModules,
        ICollection<CharacterAcquiredTraitValidationIssue> issues)
    {
        CharacterAcquiredTraitReactionState[] states = (instance.reactionStates
                ?? new List<CharacterAcquiredTraitReactionState>())
            .ToArray();
        if (states.Any(value => value == null))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                $"Acquired-trait instance '{instance.instanceId}' contains a null reaction state.");
            return;
        }
        string[] authoredIds = (selectedModules
                ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .Where(value => value != null)
            .SelectMany(value => value.SpecialReactions)
            .Where(value => value != null)
            .Select(value => value.ReactionId)
            .ToArray();
        if (states.GroupBy(value => value.reactionId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1)
            || states.Any(value => !IsCanonicalRequired(value.reactionId)
                || !authoredIds.Contains(value.reactionId, StringComparer.Ordinal)
                || value.lastTriggerAbsoluteDay < -1
                || value.triggersOnLastDay < 0
                || value.totalTriggerCount < 0
                || (value.lastTriggerAbsoluteDay < 0
                    && (value.triggersOnLastDay != 0
                        || value.totalTriggerCount != 0))
                || value.triggersOnLastDay > value.totalTriggerCount))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                $"Acquired-trait instance '{instance.instanceId}' contains invalid, duplicate, or unauthored reaction state.");
        }
    }

    private static void ValidateActiveCoexistence(
        IEnumerable<CharacterAcquiredTraitInstanceState> instances,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
        ICollection<CharacterAcquiredTraitValidationIssue> issues)
    {
        CharacterAcquiredTraitInstanceState[] active = instances
            .Where(value => value != null && !value.erased)
            .ToArray();
        string duplicateCombination = active
            .Where(value => value.formulaVersion == 0
                && !string.IsNullOrEmpty(value.combinationId))
            .GroupBy(value => value.combinationId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateCombination != null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                $"Active acquired traits repeat combination '{duplicateCombination}'.");
        }

        var selected = active
            .SelectMany(instance => (instance.moduleIds ?? new List<string>())
                .Where(modulesById.ContainsKey)
                .Select(moduleId => new
                {
                    instance.instanceId,
                    Module = modulesById[moduleId]
                }))
            .ToArray();
        string duplicateModule = selected
            .GroupBy(value => value.Module.ModuleId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateModule != null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                $"Active acquired traits repeat module '{duplicateModule}'.");
        }
        string duplicateConflict = selected
            .SelectMany(value => value.Module.ConflictGroups.Select(group => new
            {
                value.instanceId,
                Group = group
            }))
            .GroupBy(value => value.Group, StringComparer.Ordinal)
            .Where(group => group.Select(value => value.instanceId)
                .Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateConflict != null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                $"Active acquired traits conflict on group '{duplicateConflict}'.");
        }
        string duplicateBinding = selected
            .SelectMany(value => value.Module.Effects.Select(effect => new
            {
                value.instanceId,
                BindingId = effect.bindingId?.Trim() ?? string.Empty
            }))
            .GroupBy(value => value.BindingId, StringComparer.Ordinal)
            .Where(group => group.Select(value => value.instanceId)
                .Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateBinding != null)
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                $"Active acquired traits conflict on effect binding '{duplicateBinding}'.");
        }

        Dictionary<string, CharacterAcquiredTraitDrawbackCapabilityDefinition> drawbacks =
            (settings?.DrawbackCapabilities
                ?? Array.Empty<CharacterAcquiredTraitDrawbackCapabilityDefinition>())
            .Where(value => value != null)
            .ToDictionary(value => value.DrawbackId, StringComparer.Ordinal);
        var selectedDrawbacks = active.SelectMany(instance =>
                (instance.drawbackCapabilityIds ?? new List<string>())
                .Where(drawbacks.ContainsKey)
                .Select(drawbackId => new
                {
                    instance.instanceId,
                    Drawback = drawbacks[drawbackId]
                }))
            .ToArray();
        string duplicateDrawback = selectedDrawbacks
            .GroupBy(value => value.Drawback.DrawbackId, StringComparer.Ordinal)
            .Where(group => group.Select(value => value.instanceId)
                .Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicateDrawback != null)
            Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                $"Active acquired traits repeat drawback '{duplicateDrawback}'.");
        string drawbackConflict = selectedDrawbacks
            .SelectMany(value => value.Drawback.ConflictGroups.Select(group => new
            {
                value.instanceId,
                Group = group
            }))
            .GroupBy(value => value.Group, StringComparer.Ordinal)
            .Where(group => group.Select(value => value.instanceId)
                .Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (drawbackConflict != null)
            Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                $"Active acquired traits conflict on drawback group '{drawbackConflict}'.");
    }

    private static void ValidateFormulaV2CompositionAgainstDefinitions(
        int formulaVersion,
        IReadOnlyList<string> benefitModuleIds,
        IReadOnlyList<string> drawbackCapabilityIds,
        IReadOnlyList<string> evidenceFactIds,
        IReadOnlyCollection<CharacterAcquiredTraitFormulaCapabilityEnvelope> capabilities,
        IReadOnlyCollection<CharacterAcquiredTraitEffectOverride> overrides,
        int positiveCost,
        int drawbackCredit,
        int narrativeBudget,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
        ICollection<CharacterAcquiredTraitValidationIssue> issues,
        string label)
    {
        if (formulaVersion < 2) return;
        string benefitId = benefitModuleIds?.SingleOrDefault();
        if (benefitId == null || !modulesById.TryGetValue(
                benefitId, out CharacterAcquiredTraitModuleSO benefit))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidModuleDefinition,
                label + " references an unknown formula v2 benefit module.");
            return;
        }
        Dictionary<string, CharacterAcquiredTraitDrawbackCapabilityDefinition> drawbacks =
            settings.DrawbackCapabilities.Where(value => value != null)
                .ToDictionary(value => value.DrawbackId, StringComparer.Ordinal);
        CharacterAcquiredTraitDrawbackCapabilityDefinition drawback = null;
        if ((drawbackCapabilityIds?.Count ?? 0) == 1
            && !drawbacks.TryGetValue(drawbackCapabilityIds[0], out drawback))
        {
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidModuleDefinition,
                label + " references an unknown formula v2 drawback capability.");
            return;
        }
        if (drawback != null)
        {
            CharacterNarrativeFact[] selectedFacts = (ledger?.Facts
                    ?? Array.Empty<CharacterNarrativeFact>())
                .Where(value => value != null
                    && (evidenceFactIds ?? Array.Empty<string>()).Contains(
                        CharacterAcquiredTraitEvidenceProjection.Project(value),
                        StringComparer.Ordinal))
                .ToArray();
            if (!selectedFacts.Any(value => drawback.DomainAffinities.Contains(value.domain)
                    && NarrativeFormulaNegativeEvidence.Matches(value.outcome, value.factId)))
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidEvidence,
                    label + " has no domain-compatible negative evidence for its drawback.");
            if (benefit.ConflictGroups.Intersect(
                    drawback.ConflictGroups, StringComparer.Ordinal).Any())
                Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                    label + " combines conflicting benefit and drawback groups.");
            if (benefit.Effects.Where(value => value != null
                    && !benefit.DrawbackBindingIds.Contains(value.bindingId,
                        StringComparer.Ordinal))
                .Any(left => drawback.Effects.Any(right => right != null
                    && left.definition != null && right.definition != null
                    && string.Equals(left.definition.TargetId,
                        right.definition.TargetId, StringComparison.Ordinal)
                    && string.Equals(left.condition?.ConditionId ?? string.Empty,
                        right.condition?.ConditionId ?? string.Empty,
                        StringComparison.Ordinal))))
                Add(issues, CharacterAcquiredTraitValidationIssueCode.Conflict,
                    label + " directly cancels its benefit with a drawback on the same target and condition.");
        }
        HashSet<string> expectedCapabilityIds = new(StringComparer.Ordinal)
        {
            benefit.RequireFormulaDescriptor().CapabilityId
        };
        if (drawback != null)
            expectedCapabilityIds.Add(drawback.RequireFormulaDescriptor().CapabilityId);
        HashSet<string> actualCapabilityIds = (capabilities
                ?? Array.Empty<CharacterAcquiredTraitFormulaCapabilityEnvelope>())
            .Where(value => value != null)
            .Select(value => value.capabilityId)
            .ToHashSet(StringComparer.Ordinal);
        if (!expectedCapabilityIds.SetEquals(actualCapabilityIds))
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                label + " formula capability envelopes do not match its frozen composition.");
        else
        {
            try
            {
                CharacterAcquiredTraitFormulaCapabilityEnvelope benefitEnvelope =
                    capabilities.Single(value => string.Equals(value.capabilityId,
                        benefit.RequireFormulaDescriptor().CapabilityId,
                        StringComparison.Ordinal));
                NarrativeFormulaCapabilityAllocation benefitAllocation =
                    ToAllocation(benefit.RequireFormulaDescriptor(), benefitEnvelope);
                int expectedPositiveCost = NarrativeFormulaCore.CalculateCost(
                    new[] { benefitAllocation },
                    settings.FormulaPolicy.RequireGenerationContext());
                if (expectedPositiveCost != positiveCost)
                    Add(issues, CharacterAcquiredTraitValidationIssueCode.BudgetExceeded,
                        label + " positive cost does not match its frozen benefit formula.");
                if (drawback != null)
                {
                    CharacterAcquiredTraitFormulaCapabilityEnvelope drawbackEnvelope =
                        capabilities.Single(value => string.Equals(value.capabilityId,
                            drawback.RequireFormulaDescriptor().CapabilityId,
                            StringComparison.Ordinal));
                    NarrativeFormulaCapabilityAllocation drawbackAllocation =
                        ToAllocation(drawback.RequireFormulaDescriptor(), drawbackEnvelope);
                    int requestedCredit = NarrativeFormulaCore.CalculateCost(
                        new[] { drawbackAllocation },
                        new NarrativeFormulaGenerationCostContext(0, false, 1));
                    NarrativeFormulaDrawbackResolution resolution = settings.FormulaPolicy
                        .RequireDrawbackPolicy().Resolve(
                            narrativeBudget,
                            new NarrativeFormulaDrawbackOption(
                                drawback.DrawbackId,
                                requestedCredit,
                                NarrativeFormulaDrawbackSelectionKind.Automatic,
                                reachable: true,
                                mandatory: true,
                                separatelyRemovable: false,
                                cancelsSelectedBenefit: false,
                                hasNegativeNarrativeEvidence: true));
                    if (requestedCredit <= 0
                        || requestedCredit > drawback.MaximumCredit
                        || resolution.AcceptedCredit != drawbackCredit)
                        Add(issues, CharacterAcquiredTraitValidationIssueCode.BudgetExceeded,
                            label + " drawback severity or credit does not match its formula policy.");
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or ArgumentException or OverflowException)
            {
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                    label + " has invalid formula allocations: " + exception.Message);
            }
        }

        HashSet<string> expectedBindings = benefit.Effects.Where(value => value != null
                && !benefit.DrawbackBindingIds.Contains(value.bindingId,
                    StringComparer.Ordinal))
            .Select(value => value.bindingId)
            .Concat(drawback?.Effects.Where(value => value != null)
                .Select(value => value.bindingId) ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> actualBindings = (overrides
                ?? Array.Empty<CharacterAcquiredTraitEffectOverride>())
            .Where(value => value != null)
            .Select(value => value.bindingId)
            .ToHashSet(StringComparer.Ordinal);
        if (!expectedBindings.SetEquals(actualBindings))
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                label + " effect overrides do not match its frozen composition.");
        else if (expectedCapabilityIds.SetEquals(actualCapabilityIds))
        {
            try
            {
                Dictionary<string, CharacterAcquiredTraitEffectOverride> actual =
                    overrides.ToDictionary(value => value.bindingId,
                        StringComparer.Ordinal);
                CharacterAcquiredTraitFormulaCapabilityEnvelope benefitEnvelope =
                    capabilities.Single(value => string.Equals(
                        value.capabilityId,
                        benefit.RequireFormulaDescriptor().CapabilityId,
                        StringComparison.Ordinal));
                float benefitScale = ResolveMagnitude(
                    benefit.RequireFormulaDescriptor(), benefitEnvelope);
                foreach (GameplayEffectBinding binding in benefit.Effects.Where(value =>
                             value != null && !benefit.DrawbackBindingIds.Contains(
                                 value.bindingId, StringComparer.Ordinal)))
                    RequireScaledOverride(binding, benefitScale, actual, label, issues);
                if (drawback != null)
                {
                    CharacterAcquiredTraitFormulaCapabilityEnvelope drawbackEnvelope =
                        capabilities.Single(value => string.Equals(
                            value.capabilityId,
                            drawback.RequireFormulaDescriptor().CapabilityId,
                            StringComparison.Ordinal));
                    float drawbackScale = ResolveMagnitude(
                        drawback.RequireFormulaDescriptor(), drawbackEnvelope);
                    foreach (GameplayEffectBinding binding in drawback.Effects.Where(
                                 value => value != null))
                        RequireScaledOverride(binding, drawbackScale, actual, label, issues);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or ArgumentException or OverflowException)
            {
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                    label + " has invalid formula effect values: " + exception.Message);
            }
        }
    }

    private static NarrativeFormulaCapabilityAllocation ToAllocation(
        NarrativeFormulaCapabilityDescriptor descriptor,
        CharacterAcquiredTraitFormulaCapabilityEnvelope envelope) => new(
        descriptor,
        envelope.parameters.Select(value => new NarrativeFormulaParameterValue(
            value.parameterId, value.units)));

    private static float ResolveMagnitude(
        NarrativeFormulaCapabilityDescriptor descriptor,
        CharacterAcquiredTraitFormulaCapabilityEnvelope envelope)
    {
        CharacterAcquiredTraitFormulaParameter magnitude = envelope.parameters.Single(
            value => string.Equals(value.parameterId,
                NarrativeFormulaParameterIds.Magnitude, StringComparison.Ordinal));
        return (float)descriptor.RequireRange(NarrativeFormulaParameterIds.Magnitude)
            .ToDecimal(magnitude.units);
    }

    private static void RequireScaledOverride(
        GameplayEffectBinding binding,
        float potencyScale,
        IReadOnlyDictionary<string, CharacterAcquiredTraitEffectOverride> overrides,
        string label,
        ICollection<CharacterAcquiredTraitValidationIssue> issues)
    {
        double expected = binding.definition.Operation == GameplayEffectOperation.Multiply
            ? 1d + (binding.value - 1d) * potencyScale
            : binding.value * potencyScale;
        float actual = overrides[binding.bindingId].value;
        double tolerance = Math.Max(1e-6d, Math.Abs(expected) * 1e-6d);
        if (double.IsNaN(expected) || double.IsInfinity(expected)
            || Math.Abs(expected - actual) > tolerance)
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                label + $" effect override '{binding.bindingId}' does not match its formula value.");
    }

    private static bool IsCanonicalSortedUnique(IReadOnlyList<string> values)
    {
        if (values == null || values.Any(value => !IsCanonicalRequired(value)))
            return false;
        return values.Distinct(StringComparer.Ordinal).Count() == values.Count
            && values.SequenceEqual(values.OrderBy(value => value,
                StringComparer.Ordinal));
    }

    private static void ValidateFormulaCompositionIdentity(
        int formulaVersion,
        IReadOnlyList<string> compatibilityModuleIds,
        IReadOnlyList<string> benefitModuleIds,
        IReadOnlyList<string> drawbackCapabilityIds,
        string drawbackId,
        int drawbackCredit,
        ICollection<CharacterAcquiredTraitValidationIssue> issues,
        string label)
    {
        if (formulaVersion < 2)
        {
            if ((benefitModuleIds?.Count ?? 0) != 0
                || (drawbackCapabilityIds?.Count ?? 0) != 0)
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                    label + " mixes formula v2 composition IDs with an older version.");
            return;
        }
        bool validBenefit = benefitModuleIds != null
            && benefitModuleIds.Count == 1
            && IsCanonicalSortedUnique(benefitModuleIds);
        bool validDrawback = drawbackCapabilityIds != null
            && drawbackCapabilityIds.Count <= 1
            && IsCanonicalSortedUnique(drawbackCapabilityIds)
            && (drawbackCapabilityIds.Count == 0
                ? drawbackCredit == 0 && string.IsNullOrEmpty(drawbackId)
                : drawbackCredit > 0 && string.Equals(
                    drawbackCapabilityIds[0], drawbackId,
                    StringComparison.Ordinal));
        bool validCompatibility = compatibilityModuleIds == null
            || (validBenefit && compatibilityModuleIds.SequenceEqual(
                benefitModuleIds, StringComparer.Ordinal));
        if (!validBenefit || !validDrawback || !validCompatibility)
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                label + " has invalid formula v2 benefit/drawback composition IDs.");
    }

    private static void ValidateFormulaEnvelope(
        int formulaVersion,
        string catalogSha256,
        int calculatedCost,
        int positiveCost,
        int drawbackCredit,
        string drawbackId,
        int narrativeBudget,
        IReadOnlyCollection<CharacterAcquiredTraitFormulaCapabilityEnvelope> capabilities,
        IReadOnlyCollection<CharacterAcquiredTraitEffectOverride> overrides,
        string presentationId,
        string mechanicalDescription,
        string displayName,
        string narrativeFlavor,
        CharacterAcquiredTraitPresentationState presentationState,
        ICollection<CharacterAcquiredTraitValidationIssue> issues,
        string label)
    {
        if (formulaVersion == 0)
        {
            if (!string.IsNullOrEmpty(catalogSha256)
                || calculatedCost != 0
                || positiveCost != 0
                || drawbackCredit != 0
                || !string.IsNullOrEmpty(drawbackId)
                || narrativeBudget != 0
                || (capabilities?.Count ?? 0) != 0
                || (overrides?.Count ?? 0) != 0
                || !string.IsNullOrEmpty(presentationId)
                || presentationState != CharacterAcquiredTraitPresentationState.None)
                Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                    label + " mixes legacy formulaVersion=0 data with v1 fields.");
            return;
        }
        bool hasCanonicalHash = catalogSha256?.Length == 64
            && catalogSha256.All(character => Uri.IsHexDigit(character))
            && string.Equals(catalogSha256, catalogSha256.ToLowerInvariant(), StringComparison.Ordinal);
        bool validPendingState = presentationState == CharacterAcquiredTraitPresentationState.PresentationPending
            || presentationState == CharacterAcquiredTraitPresentationState.AwaitingNarrativeRetry;
        bool validDrawback = drawbackCredit == 0
            ? string.IsNullOrEmpty(drawbackId) || IsCanonicalRequired(drawbackId)
            : IsCanonicalRequired(drawbackId);
        if (formulaVersion < 1 || !hasCanonicalHash || calculatedCost < 0
            || positiveCost < 0 || drawbackCredit < 0
            || calculatedCost != positiveCost - drawbackCredit
            || narrativeBudget < 0 || calculatedCost > narrativeBudget
            || !validDrawback
            || capabilities == null || capabilities.Count == 0
            || capabilities.Any(value => value == null
                || !IsCanonicalRequired(value.capabilityId)
                || !string.Equals(value.formatterId, value.capabilityId, StringComparison.Ordinal)
                || !string.Equals(value.applicatorId, value.capabilityId, StringComparison.Ordinal)
                || value.parameters == null
                || value.parameters.Count != NarrativeFormulaParameterIds.Required.Count
                || value.parameters.Any(parameter => parameter == null
                    || !IsCanonicalRequired(parameter.parameterId))
                || value.parameters.Select(parameter => parameter.parameterId)
                    .Distinct(StringComparer.Ordinal).Count() != NarrativeFormulaParameterIds.Required.Count)
            || overrides == null || overrides.Count == 0
            || overrides.Any(value => value == null || !IsCanonicalRequired(value.bindingId)
                || float.IsNaN(value.value) || float.IsInfinity(value.value))
            || overrides.Select(value => value.bindingId).Distinct(StringComparer.Ordinal).Count()
                != overrides.Count
            || !IsCanonicalRequired(presentationId)
            || !presentationId.StartsWith("presentation:trait:", StringComparison.Ordinal)
            || !IsCanonicalRequired(mechanicalDescription)
            || (presentationState != CharacterAcquiredTraitPresentationState.Ready
                && !validPendingState)
            || (presentationState == CharacterAcquiredTraitPresentationState.Ready
                && (!IsCanonicalRequired(displayName)
                    || !IsCanonicalRequired(narrativeFlavor)
                    || ContainsMechanicalNumber(displayName)
                    || ContainsMechanicalNumber(narrativeFlavor))))
            Add(issues, CharacterAcquiredTraitValidationIssueCode.InvalidInstance,
                label + " has an invalid formula envelope or presentation state.");
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool ContainsMechanicalNumber(string value) =>
        (value ?? string.Empty).Any(character => character is >= '0' and <= '9'
            or >= '０' and <= '９' or '%' or '％');

    private static bool IsSha256(string value)
    {
        const string prefix = "sha256:";
        if (value == null
            || !value.StartsWith(prefix, StringComparison.Ordinal)
            || value.Length != prefix.Length + 64)
        {
            return false;
        }
        return value.Skip(prefix.Length).All(character =>
            character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
    }

    private static void Add(
        ICollection<CharacterAcquiredTraitValidationIssue> issues,
        CharacterAcquiredTraitValidationIssueCode code,
        string message) => issues.Add(
        new CharacterAcquiredTraitValidationIssue(code, message));
}

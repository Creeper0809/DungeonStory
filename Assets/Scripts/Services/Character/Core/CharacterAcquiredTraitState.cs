using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CharacterAcquiredTraitPendingRequestState
{
    public string targetPersistentId = string.Empty;
    public string requestId = string.Empty;
    public string requestKey = string.Empty;
    public string candidatePacketHash = string.Empty;
    public string submissionAuditId = string.Empty;
    public int manifestationMilestone;
    public List<string> evidenceFactIds = new();
    public List<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings = new();
    public int registeredRevision;
    public int formulaVersion;
    public string formulaCatalogSha256 = string.Empty;
    public int calculatedCost;
    public int positiveCost;
    public int drawbackCredit;
    public string drawbackId = string.Empty;
    public int narrativeBudget;
    public bool drawbackEvidenceQualified;
    public string moduleSelectionId = string.Empty;
    public List<string> offeredBenefitModuleIds = new();
    public List<string> offeredDrawbackModuleIds = new();
    public List<CharacterAcquiredTraitModuleOfferState> moduleOffers = new();
    public List<string> benefitModuleIds = new();
    public List<string> drawbackCapabilityIds = new();
    public List<CharacterAcquiredTraitFormulaCapabilityEnvelope> formulaCapabilities = new();
    public List<CharacterAcquiredTraitEffectOverride> effectOverrides = new();
    public string presentationId = string.Empty;
    public string mechanicalDescription = string.Empty;
    public CharacterAcquiredTraitPresentationState presentationState;
    public int presentationFailureCount;

    public CharacterAcquiredTraitPendingRequestState Clone()
    {
        CharacterAcquiredTraitPendingRequestState clone =
            (CharacterAcquiredTraitPendingRequestState)MemberwiseClone();
        clone.evidenceFactIds = (evidenceFactIds ?? new List<string>()).ToList();
        clone.evidenceBindings = (evidenceBindings
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToList();
        clone.offeredBenefitModuleIds = (offeredBenefitModuleIds ?? new List<string>()).ToList();
        clone.offeredDrawbackModuleIds = (offeredDrawbackModuleIds ?? new List<string>()).ToList();
        clone.moduleOffers = (moduleOffers ?? new List<CharacterAcquiredTraitModuleOfferState>())
            .Where(value => value != null).Select(value => value.Clone()).ToList();
        clone.benefitModuleIds = (benefitModuleIds ?? new List<string>()).ToList();
        clone.drawbackCapabilityIds = (drawbackCapabilityIds ?? new List<string>()).ToList();
        clone.formulaCapabilities = (formulaCapabilities ?? new List<CharacterAcquiredTraitFormulaCapabilityEnvelope>())
            .Where(value => value != null).Select(value => value.Clone()).ToList();
        clone.effectOverrides = (effectOverrides ?? new List<CharacterAcquiredTraitEffectOverride>())
            .Where(value => value != null).Select(value => value.Clone()).ToList();
        return clone;
    }
}

[Serializable]
public sealed class CharacterAcquiredTraitModuleOfferState
{
    public string moduleId = string.Empty;
    public NarrativeFormulaModulePolarity polarity;
    [TextArea] public string semanticDescription = string.Empty;

    public CharacterAcquiredTraitModuleOfferState Clone() =>
        (CharacterAcquiredTraitModuleOfferState)MemberwiseClone();
}

public enum CharacterAcquiredTraitPresentationState
{
    None = 0,
    PresentationPending = 1,
    Ready = 2,
    AwaitingNarrativeRetry = 3,
    ModuleSelectionPending = 4
}

[Serializable]
public sealed class CharacterAcquiredTraitFormulaParameter
{
    public string parameterId = string.Empty;
    public long units;
    public CharacterAcquiredTraitFormulaParameter Clone() =>
        (CharacterAcquiredTraitFormulaParameter)MemberwiseClone();
}

[Serializable]
public sealed class CharacterAcquiredTraitFormulaCapabilityEnvelope
{
    public string capabilityId = string.Empty;
    public string formatterId = string.Empty;
    public string applicatorId = string.Empty;
    public List<CharacterAcquiredTraitFormulaParameter> parameters = new();
    public CharacterAcquiredTraitFormulaCapabilityEnvelope Clone() => new()
    {
        capabilityId = capabilityId,
        formatterId = formatterId,
        applicatorId = applicatorId,
        parameters = (parameters ?? new List<CharacterAcquiredTraitFormulaParameter>())
            .Where(value => value != null).Select(value => value.Clone()).ToList()
    };
}

[Serializable]
public sealed class CharacterAcquiredTraitEffectOverride
{
    public string bindingId = string.Empty;
    public float value;
    public CharacterAcquiredTraitEffectOverride Clone() =>
        (CharacterAcquiredTraitEffectOverride)MemberwiseClone();
}

[Serializable]
public sealed class CharacterAcquiredTraitReactionState
{
    public string reactionId = string.Empty;
    public int lastTriggerAbsoluteDay = -1;
    public int triggersOnLastDay;
    public int totalTriggerCount;

    public CharacterAcquiredTraitReactionState Clone() =>
        (CharacterAcquiredTraitReactionState)MemberwiseClone();
}

[Serializable]
public sealed class CharacterAcquiredTraitInstanceState
{
    public const long NotErasedAtAbsoluteHour = -1L;

    public string instanceId = string.Empty;
    public string combinationId = string.Empty;
    public List<string> moduleIds = new();
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [TextArea] public string narrativeReason = string.Empty;
    public List<string> evidenceFactIds = new();
    public List<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings = new();
    public int manifestationMilestone;
    public string originatingRequestId = string.Empty;
    public string originatingRequestKey = string.Empty;
    public string candidatePacketHash = string.Empty;
    public string selectionAuditId = string.Empty;
    public int acceptedRevision;
    public bool erased;
    /// <summary>
    /// Persisted <c>IGameCalendar.AbsoluteHour</c> of erasure. The active-state
    /// sentinel is <see cref="NotErasedAtAbsoluteHour"/>; wall-clock UTC and
    /// frame count are intentionally not persistence authorities.
    /// </summary>
    public long erasedAt = NotErasedAtAbsoluteHour;
    public string erasureAuditId = string.Empty;
    public int erasedRevision;
    public int formulaVersion;
    public string formulaCatalogSha256 = string.Empty;
    public int calculatedCost;
    public int positiveCost;
    public int drawbackCredit;
    public string drawbackId = string.Empty;
    public int narrativeBudget;
    public bool drawbackEvidenceQualified;
    public List<string> benefitModuleIds = new();
    public List<string> drawbackCapabilityIds = new();
    public List<CharacterAcquiredTraitFormulaCapabilityEnvelope> formulaCapabilities = new();
    public List<CharacterAcquiredTraitEffectOverride> effectOverrides = new();
    public List<CharacterAcquiredTraitReactionState> reactionStates = new();
    public string presentationId = string.Empty;
    [TextArea] public string mechanicalDescription = string.Empty;
    [TextArea] public string narrativeFlavor = string.Empty;

    public string EffectSourceId => instanceId?.Trim() ?? string.Empty;
    public bool IsActive => !erased;

    public CharacterAcquiredTraitInstanceState Clone()
    {
        CharacterAcquiredTraitInstanceState clone =
            (CharacterAcquiredTraitInstanceState)MemberwiseClone();
        clone.moduleIds = (moduleIds ?? new List<string>()).ToList();
        clone.evidenceFactIds = (evidenceFactIds ?? new List<string>()).ToList();
        clone.evidenceBindings = (evidenceBindings
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null).Select(value => value.Clone()).ToList();
        clone.benefitModuleIds = (benefitModuleIds ?? new List<string>()).ToList();
        clone.drawbackCapabilityIds = (drawbackCapabilityIds ?? new List<string>()).ToList();
        clone.formulaCapabilities = (formulaCapabilities ?? new List<CharacterAcquiredTraitFormulaCapabilityEnvelope>())
            .Where(value => value != null).Select(value => value.Clone()).ToList();
        clone.effectOverrides = (effectOverrides ?? new List<CharacterAcquiredTraitEffectOverride>())
            .Where(value => value != null).Select(value => value.Clone()).ToList();
        clone.reactionStates = (reactionStates
                ?? new List<CharacterAcquiredTraitReactionState>())
            .Where(value => value != null).Select(value => value.Clone()).ToList();
        return clone;
    }
}

[Serializable]
public sealed class CharacterAcquiredTraitAggregateState
{
    public const int LegacyFormatVersion = 1;
    public const int FormulaFormatVersion = 2;
    public const int CurrentFormatVersion = 3;

    public int formatVersion = CurrentFormatVersion;
    public int revision;
    public List<CharacterAcquiredTraitInstanceState> instances = new();
    public List<int> processedMilestones = new();
    public List<CharacterAcquiredTraitPendingRequestState> pendingRequests = new();

    public int ActiveCount => (instances ?? new List<CharacterAcquiredTraitInstanceState>())
        .Count(value => value != null && !value.erased);

    public bool HasPersistentData => revision != 0
        || (instances?.Count ?? 0) != 0
        || (processedMilestones?.Count ?? 0) != 0
        || (pendingRequests?.Count ?? 0) != 0;

    [GameplayInternalOnly(
        "Character progression owns acquired-trait aggregate initialization.",
        "CharacterProgression")]
    internal void EnsureCollections()
    {
        instances ??= new List<CharacterAcquiredTraitInstanceState>();
        processedMilestones ??= new List<int>();
        pendingRequests ??= new List<CharacterAcquiredTraitPendingRequestState>();
    }

    public CharacterAcquiredTraitAggregateState Clone()
    {
        List<CharacterAcquiredTraitInstanceState> sourceInstances = instances
            ?? new List<CharacterAcquiredTraitInstanceState>();
        List<int> sourceMilestones = processedMilestones
            ?? new List<int>();
        List<CharacterAcquiredTraitPendingRequestState> sourcePending =
            pendingRequests
            ?? new List<CharacterAcquiredTraitPendingRequestState>();
        return new CharacterAcquiredTraitAggregateState
        {
            formatVersion = formatVersion,
            revision = revision,
            instances = sourceInstances
                .Select(value => value?.Clone())
                .ToList(),
            processedMilestones = sourceMilestones.ToList(),
            pendingRequests = sourcePending
                .Select(value => value?.Clone())
                .ToList()
        };
    }

    public IReadOnlyList<CharacterAcquiredTraitInstanceState>
        CaptureActiveInstances() => (instances
                ?? new List<CharacterAcquiredTraitInstanceState>())
            .Where(value => value != null && !value.erased)
            .OrderBy(value => value.instanceId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();

    public bool TryGetInstance(
        string instanceId,
        out CharacterAcquiredTraitInstanceState instance)
    {
        string required = instanceId?.Trim() ?? string.Empty;
        CharacterAcquiredTraitInstanceState found = (instances
                ?? new List<CharacterAcquiredTraitInstanceState>())
            .SingleOrDefault(value => value != null
                && string.Equals(value.instanceId, required, StringComparison.Ordinal));
        instance = found?.Clone();
        return instance != null;
    }

    public bool HasProcessedMilestone(int milestone) =>
        (processedMilestones ?? new List<int>()).Contains(milestone);

    public bool TryGetPendingRequest(
        string requestId,
        out CharacterAcquiredTraitPendingRequestState request)
    {
        string required = requestId?.Trim() ?? string.Empty;
        CharacterAcquiredTraitPendingRequestState found = (pendingRequests
                ?? new List<CharacterAcquiredTraitPendingRequestState>())
            .SingleOrDefault(value => value != null
                && string.Equals(value.requestId, required, StringComparison.Ordinal));
        request = found?.Clone();
        return request != null;
    }
}

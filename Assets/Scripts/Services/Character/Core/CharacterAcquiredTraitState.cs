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
    public CharacterNarrativeRequestOwnerBinding ownerBinding;
    // 0 is an older request reconstructed under the existing strict identity
    // check. New live requests freeze their public, request-time context as v1.
    public int publicContextVersion;
    public NarrativePublicContextMaterialSnapshot publicContext;
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
    public List<CharacterNarrativeFact> moduleSelectionLocalFacts = new();
    public string moduleSelectionLocalFactsHash = string.Empty;
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
    internal bool HasUnresolvedModuleSelection => presentationState is
        CharacterAcquiredTraitPresentationState.ModuleSelectionPending
        or CharacterAcquiredTraitPresentationState.ModuleSelectionAwaitingNarrativeRetry;

    public CharacterAcquiredTraitPendingRequestState Clone()
    {
        CharacterAcquiredTraitPendingRequestState clone =
            (CharacterAcquiredTraitPendingRequestState)MemberwiseClone();
        clone.ownerBinding = ownerBinding?.Clone();
        clone.publicContext = publicContext?.Clone();
        clone.moduleSelectionLocalFacts = moduleSelectionLocalFacts?
            .Select(value => value?.Clone()).ToList();
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

    public NarrativePublicContextMaterial RestorePublicContext()
    {
        // Unity's inline serializer can materialize a null reference as an
        // empty object. Only that empty v0 representation is legacy absence.
        if (publicContextVersion == 0 && (publicContext == null
            || string.IsNullOrEmpty(publicContext.semanticHash)
                && string.IsNullOrEmpty(publicContext.profileId) && string.IsNullOrEmpty(publicContext.subjectId)
                && string.IsNullOrEmpty(publicContext.cultureStyleId) && string.IsNullOrEmpty(publicContext.selectionPolicyId)
                && !publicContext.requireCharacterFact && !publicContext.requireMotif
                && publicContext.availableFactCount == 0 && publicContext.omittedFactCount == 0
                && (publicContext.entities?.Count ?? 0) == 0 && (publicContext.publicFacts?.Count ?? 0) == 0
                && (publicContext.events?.Count ?? 0) == 0 && (publicContext.motifs?.Count ?? 0) == 0
                && (publicContext.backgroundFactIds?.Count ?? 0) == 0 && (publicContext.relationshipFactIds?.Count ?? 0) == 0
                && (publicContext.memoryFactIds?.Count ?? 0) == 0 && (publicContext.priorHistoryFactIds?.Count ?? 0) == 0)) return null;
        if (publicContextVersion != 1 || publicContext == null)
            throw new InvalidOperationException("Acquired-trait public context version/snapshot is invalid.");
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Restore(publicContext);
        if (material.ProfileId != LocalLlmRequestProfiles.AcquiredTrait.Id
            || material.SubjectKind != NarrativePublicSubjectKind.Character
            || material.SubjectId != targetPersistentId
            || evidenceFactIds == null || evidenceFactIds.Count == 0
            || evidenceFactIds.Any(id => !material.Events.Any(value => value.FactId == id)))
            throw new InvalidOperationException("Acquired-trait frozen public context does not own its profile, target and evidence.");
        return material;
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
    ModuleSelectionPending = 4,
    // Same unresolved offers, paused after rejection. No effects are selected.
    ModuleSelectionAwaitingNarrativeRetry = 5
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
public sealed class CharacterAcquiredTraitPresentationOriginState
{
    public int version;
    public int registeredRevision;
    public CharacterNarrativeRequestOwnerBinding ownerBinding;
    public NarrativePublicContextMaterialSnapshot publicContext;
    public List<string> evidenceFactIds = new();

    // Unity can materialize an absent inline object. An empty v0 is legacy absence.
    public bool HasData => version != 0 || registeredRevision != 0
        || !string.IsNullOrEmpty(ownerBinding?.persistentId)
        || (ownerBinding?.version ?? 0) != 0
        || (ownerBinding?.lifecycleRevision ?? -1) != -1
        || (ownerBinding?.generationRevision ?? -1) != -1
        || !string.IsNullOrEmpty(publicContext?.semanticHash)
        || !string.IsNullOrEmpty(publicContext?.subjectId)
        || !string.IsNullOrEmpty(publicContext?.profileId)
        || !string.IsNullOrEmpty(publicContext?.cultureStyleId)
        || !string.IsNullOrEmpty(publicContext?.selectionPolicyId)
        || (publicContext?.schemaVersion ?? NarrativePublicContextFactory.SchemaVersion) != NarrativePublicContextFactory.SchemaVersion
        || (publicContext?.subjectKind ?? default) != default
        || publicContext?.requireCharacterFact == true || publicContext?.requireMotif == true
        || (publicContext?.availableFactCount ?? 0) != 0 || (publicContext?.omittedFactCount ?? 0) != 0
        || (publicContext?.entities?.Count ?? 0) != 0
        || (publicContext?.events?.Count ?? 0) != 0
        || (publicContext?.publicFacts?.Count ?? 0) != 0
        || (publicContext?.backgroundFactIds?.Count ?? 0) != 0
        || (publicContext?.relationshipFactIds?.Count ?? 0) != 0
        || (publicContext?.memoryFactIds?.Count ?? 0) != 0
        || (publicContext?.priorHistoryFactIds?.Count ?? 0) != 0
        || (publicContext?.motifs?.Count ?? 0) != 0
        || (evidenceFactIds?.Count ?? 0) != 0;

    public CharacterAcquiredTraitPresentationOriginState Clone() => new()
    {
        version = version,
        registeredRevision = registeredRevision,
        ownerBinding = ownerBinding?.Clone(),
        publicContext = publicContext?.Clone(),
        evidenceFactIds = evidenceFactIds?.ToList()
    };

    public static CharacterAcquiredTraitPresentationOriginState Capture(
        CharacterAcquiredTraitPendingRequestState pending)
    {
        if (pending.publicContextVersion == 0) return null;
        pending.RestorePublicContext();
        if (pending.ownerBinding?.IsWellFormed != true || pending.registeredRevision <= 0)
            throw new InvalidOperationException("Trait presentation origin lacks its request owner or revision.");
        return new CharacterAcquiredTraitPresentationOriginState
        {
            version = 1,
            registeredRevision = pending.registeredRevision,
            ownerBinding = pending.ownerBinding.Clone(),
            publicContext = pending.publicContext.Clone(),
            evidenceFactIds = pending.evidenceFactIds.ToList()
        };
    }

    public NarrativePublicContextMaterial RestorePublicContext(
        CharacterAcquiredTraitInstanceState instance)
    {
        if (version != 1 || ownerBinding?.IsWellFormed != true || publicContext == null
            || registeredRevision <= 0 || registeredRevision >= instance.acceptedRevision
            || evidenceFactIds == null || evidenceFactIds.Count == 0
            || evidenceFactIds.Any(string.IsNullOrWhiteSpace)
            || !evidenceFactIds.SequenceEqual(evidenceFactIds.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidOperationException("Trait presentation origin has invalid version, owner, revision or evidence.");
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Restore(publicContext);
        if (material.ProfileId != LocalLlmRequestProfiles.AcquiredTrait.Id
            || material.SubjectKind != NarrativePublicSubjectKind.Character
            || material.SubjectId != ownerBinding.persistentId
            || evidenceFactIds.Any(id => !material.Events.Any(value => value.FactId == id))
            || instance.evidenceFactIds == null
            || instance.evidenceFactIds.Any(id => !evidenceFactIds.Contains(id, StringComparer.Ordinal)))
            throw new InvalidOperationException("Trait presentation origin does not own its subject and original evidence.");
        NarrativePublicEntity subject = material.Entities.Single(value => value.EntityId == material.SubjectId);
        if (subject.Kind != NarrativePublicEntityKind.Character
            || string.IsNullOrWhiteSpace(subject.DisplayName)
            || subject.DisplayName != subject.DisplayName.Trim()
            || subject.DisplayName.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            throw new InvalidOperationException("Trait presentation origin requires its exact original character name.");
        return material;
    }

    public void RequireRequestIdentity(CharacterAcquiredTraitInstanceState instance,
        CharacterAcquiredTraitSettingsSO settings)
    {
        var material = RestorePublicContext(instance);
        CharacterAcquiredTraitManifestationRuntime.BuildProductionRequestIdentity(settings,
            material.SubjectId, instance.manifestationMilestone, evidenceFactIds,
            material.SemanticHash, ownerBinding, registeredRevision - 1,
            out string requestId, out string requestKey);
        if (requestId != instance.originatingRequestId || requestKey != instance.originatingRequestKey)
            throw new InvalidOperationException("Trait presentation origin does not match its original request identity.");
    }
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
    public CharacterAcquiredTraitPresentationOriginState presentationOrigin;

    public string EffectSourceId => instanceId?.Trim() ?? string.Empty;
    public bool IsActive => !erased;

    public CharacterAcquiredTraitInstanceState Clone()
    {
        CharacterAcquiredTraitInstanceState clone =
            (CharacterAcquiredTraitInstanceState)MemberwiseClone();
        clone.presentationOrigin = presentationOrigin?.Clone();
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
public sealed class CharacterAcquiredTraitRequestInvalidationState
{
    public string requestId = string.Empty;
    public string requestKey = string.Empty;
    public string reason = string.Empty;
    public int registeredRevision;
    public int invalidatedRevision;
    public bool HasData => !string.IsNullOrEmpty(requestId) || !string.IsNullOrEmpty(requestKey)
        || !string.IsNullOrEmpty(reason) || registeredRevision != 0 || invalidatedRevision != 0;
    public CharacterAcquiredTraitRequestInvalidationState Clone() =>
        (CharacterAcquiredTraitRequestInvalidationState)MemberwiseClone();
}

[Serializable]
public sealed class CharacterAcquiredTraitAggregateState
{
    [NonSerialized, Newtonsoft.Json.JsonIgnore]
    internal readonly object ReadIdentity = new();

    public const int LegacyFormatVersion = 1;
    public const int FormulaFormatVersion = 2;
    public const int CurrentFormatVersion = 4;

    public int formatVersion = CurrentFormatVersion;
    public int revision;
    public List<CharacterAcquiredTraitInstanceState> instances = new();
    public List<int> processedMilestones = new();
    public List<CharacterAcquiredTraitPendingRequestState> pendingRequests = new();
    public CharacterAcquiredTraitRequestInvalidationState lastRequestInvalidation;

    public int ActiveCount => (instances ?? new List<CharacterAcquiredTraitInstanceState>())
        .Count(value => value != null && !value.erased);

    public bool HasPersistentData => revision != 0
        || lastRequestInvalidation?.HasData == true
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
            lastRequestInvalidation = lastRequestInvalidation?.Clone(),
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

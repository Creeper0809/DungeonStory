using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>The actual accepted trait, frozen before notification; no recording or scoring policy.</summary>
public readonly struct CharacterTraitInferredFact
{
    private readonly CharacterTraitInferredFactData data;
    public CharacterTraitInferredFact(CharacterTraitInferredFactData data)
    {
        (data ?? throw new ArgumentNullException(nameof(data))).Validate();
        this.data = data.Clone();
    }
    public CharacterTraitInferredFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("trait-inference-fact-uninitialized")).Clone();
}

[Serializable]
public sealed class CharacterTraitInferredFactData
{
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public string auditId = string.Empty;
    public string requestKey = string.Empty;
    public string candidatePacketHash = string.Empty;
    public string selectedCombinationId = string.Empty;
    public int beforeRevision;
    public int registeredRevision;
    public int afterRevision;
    public long gameTick;
    public CharacterAcquiredTraitInstanceState trait;

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(characterId)
            || !DomainFactValueSyntax.IsText(characterDisplayName, 4096, false)
            || !DomainFactValueSyntax.IsIdentity(auditId)
            || !DomainFactValueSyntax.IsIdentity(requestKey)
            || !DomainFactValueSyntax.IsIdentity(candidatePacketHash)
            || !DomainFactValueSyntax.IsText(selectedCombinationId, 4096, true)
            || beforeRevision < 0 || beforeRevision == int.MaxValue || afterRevision != beforeRevision + 1
            || registeredRevision < 1 || registeredRevision > beforeRevision || gameTick < 0
            || trait == null || trait.acceptedRevision != afterRevision || trait.erased
            || trait.selectionAuditId != auditId || trait.originatingRequestKey != requestKey
            || trait.candidatePacketHash != candidatePacketHash
            || !DomainFactValueSyntax.IsIdentity(trait.instanceId)
            || !DomainFactValueSyntax.IsText(trait.displayName, 4096, false)
            || trait.moduleIds == null || trait.moduleIds.Count == 0 || trait.moduleIds.Any(string.IsNullOrWhiteSpace)
            || trait.evidenceFactIds == null || trait.evidenceFactIds.Any(string.IsNullOrWhiteSpace)
            || trait.evidenceFactIds.Distinct(StringComparer.Ordinal).Count() != trait.evidenceFactIds.Count
            || trait.evidenceBindings == null || trait.evidenceBindings.Any(value => value == null)
            || trait.evidenceBindings.Select(value => value.publicFactId).Distinct(StringComparer.Ordinal).Count() != trait.evidenceBindings.Count
            || trait.evidenceBindings.Any(value => !trait.evidenceFactIds.Contains(value.publicFactId, StringComparer.Ordinal))
            || trait.benefitModuleIds == null || trait.drawbackCapabilityIds == null
            || trait.formulaCapabilities == null || trait.formulaCapabilities.Any(value => value?.parameters == null || value.parameters.Any(item => item == null))
            || trait.effectOverrides == null || trait.effectOverrides.Any(value => value == null)
            || trait.reactionStates == null || trait.reactionStates.Any(value => value == null))
            throw new ArgumentException("trait-inference-fact-invalid");
        foreach (var binding in trait.evidenceBindings)
            if (binding.roleIds == null || binding.metricIds == null || binding.metricReferenceIds == null
                || binding.factIds == null || binding.semanticTags == null || binding.participants == null
                || binding.metrics == null || binding.facts == null || binding.provenance == null
                || binding.participants.Any(value => value == null) || binding.metrics.Any(value => value == null)
                || binding.facts.Any(value => value == null) || binding.provenance.Any(value => value == null))
                throw new ArgumentException("trait-inference-evidence-shape-invalid");
    }

    public CharacterTraitInferredFactData Clone()
    {
        Validate(); // Validate before Clone can normalize malformed collections.
        var copy = (CharacterTraitInferredFactData)MemberwiseClone();
        copy.trait = trait.Clone();
        return copy;
    }
}

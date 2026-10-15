using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>A frozen learned skill. Publication identity and experience scoring belong to observers.</summary>
public readonly struct CharacterSkillAcquiredFact
{
    private readonly CharacterSkillAcquiredFactData data;
    public CharacterSkillAcquiredFact(CharacterSkillAcquiredFactData data)
    {
        (data ?? throw new ArgumentNullException(nameof(data))).Validate();
        this.data = data.Clone();
    }
    public CharacterSkillAcquiredFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("skill-acquisition-fact-uninitialized")).Clone();
}

[Serializable]
public sealed class CharacterSkillAcquiredFactData
{
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public long beforeRevision;
    public long afterRevision;
    public int unlockLevel;
    public int absoluteDay;
    public CharacterSkillInstance skill;

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(characterId)
            || !DomainFactValueSyntax.IsText(characterDisplayName, 4096, false)
            || beforeRevision < 0 || beforeRevision == long.MaxValue || afterRevision != beforeRevision + 1
            || unlockLevel < 1 || absoluteDay < 0 || skill == null || skill.formulaVersion <= 0
            || skill.kind is not (CharacterSkillKind.Active or CharacterSkillKind.Passive or CharacterSkillKind.Ultimate)
            || !DomainFactValueSyntax.IsIdentity(skill.id) || !DomainFactValueSyntax.IsIdentity(skill.ruleId)
            || !DomainFactValueSyntax.IsIdentity(skill.presentationId)
            || !DomainFactValueSyntax.IsText(skill.displayName, 4096, false)
            || skill.modules == null || skill.modules.Count == 0 || skill.modules.Any(value => value == null)
            || skill.formulaCapabilities == null || skill.formulaCapabilities.Count == 0
            || skill.formulaCapabilities.Any(value => value?.parameters == null || value.parameters.Any(item => item == null))
            || skill.evidenceIds == null || skill.evidenceIds.Any(string.IsNullOrWhiteSpace)
            || skill.evidenceIds.Distinct(StringComparer.Ordinal).Count() != skill.evidenceIds.Count
            || skill.evidenceBindings == null || skill.evidenceBindings.Any(value => value == null)
            || skill.evidenceBindings.Select(value => value.publicFactId).Distinct(StringComparer.Ordinal).Count()
                != skill.evidenceBindings.Count
            || skill.evidenceBindings.Any(value => !skill.evidenceIds.Contains(value.publicFactId, StringComparer.Ordinal)))
            throw new ArgumentException("skill-acquisition-fact-invalid");
        foreach (var binding in skill.evidenceBindings)
            if (binding.roleIds == null || binding.metricIds == null || binding.metricReferenceIds == null
                || binding.factIds == null || binding.semanticTags == null || binding.participants == null
                || binding.metrics == null || binding.facts == null || binding.provenance == null
                || binding.participants.Any(value => value == null) || binding.metrics.Any(value => value == null)
                || binding.facts.Any(value => value == null) || binding.provenance.Any(value => value == null))
                throw new ArgumentException("skill-acquisition-evidence-shape-invalid");
    }

    public CharacterSkillAcquiredFactData Clone()
    {
        Validate(); // Never let CharacterSkillInstance.Clone normalize malformed evidence.
        var copy = (CharacterSkillAcquiredFactData)MemberwiseClone();
        copy.skill = skill.Clone();
        copy.skill.usableFrom = skill.usableFrom;
        copy.skill.targetPositions = skill.targetPositions;
        if (skill.narrativeTrace != null)
        {
            var trace = skill.narrativeTrace;
            copy.skill.narrativeTrace = new NarrativeGenerationTrace
            {
                schemaId = trace.schemaId, schemaVersion = trace.schemaVersion, schemaHash = trace.schemaHash,
                cultureStyleId = trace.cultureStyleId, usedMotifIds = trace.usedMotifIds?.ToArray(),
                usedCharacterFactIds = trace.usedCharacterFactIds?.ToArray(), verdict = trace.verdict,
                retryCount = trace.retryCount, usedFallback = trace.usedFallback
            };
        }
        return copy;
    }
}

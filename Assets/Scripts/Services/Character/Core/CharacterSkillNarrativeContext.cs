using System;
using System.Collections.Generic;
using VContainer;

/// <summary>The narrative inputs that are authoritative in the request owner's scene.</summary>
public interface ICharacterSkillNarrativeContext
{
    NarrativePublicContextMaterial BuildSkillMaterialWithRequiredFacts(
        CharacterProgression progression, IEnumerable<string> requiredProjectedFactIds);

    IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetOutcomeEvidence(CharacterProgression progression);

    bool TryResolveExactBinding(GameplayOutcomeEvidenceBindingSnapshot binding);
}

public sealed class GameplayCharacterSkillNarrativeContext : ICharacterSkillNarrativeContext, IDisposable
{
    private NarrativeRequestContextBuilder contexts;
    private IGameplayOutcomeNarrativeEvidenceQuery outcomes;
    private bool disposed;

    [Inject]
    public GameplayCharacterSkillNarrativeContext(
        NarrativeRequestContextBuilder contexts, IGameplayOutcomeNarrativeEvidenceQuery outcomes)
        : this(contexts, outcomes, allowMissingFixtureEvidence: false) { }

    internal GameplayCharacterSkillNarrativeContext(
        NarrativeRequestContextBuilder contexts, IGameplayOutcomeNarrativeEvidenceQuery outcomes,
        bool allowMissingFixtureEvidence)
    {
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        this.outcomes = outcomes;
        if (outcomes == null && !allowMissingFixtureEvidence)
            throw new ArgumentNullException(nameof(outcomes));
    }

    public NarrativePublicContextMaterial BuildSkillMaterialWithRequiredFacts(
        CharacterProgression progression, IEnumerable<string> requiredProjectedFactIds)
    {
        RequireActive();
        return contexts.BuildSkillMaterialWithRequiredFacts(progression, requiredProjectedFactIds);
    }

    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetOutcomeEvidence(CharacterProgression progression)
    {
        RequireActive();
        return progression.Actor == null || outcomes == null
            ? Array.Empty<GameplayOutcomeNarrativeEvidenceSource>()
            : outcomes.GetForCharacter(progression.Actor.Identity.PersistentId, 24, 0f, includeCompacted: true);
    }

    public bool TryResolveExactBinding(GameplayOutcomeEvidenceBindingSnapshot binding)
    {
        RequireActive();
        if (outcomes == null)
            throw new InvalidOperationException("CharacterSkill evidence query is required before response commit.");
        return outcomes.TryResolveExactBinding(binding, out _);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        // The container owns these authorities; release our references only.
        contexts = null;
        outcomes = null;
    }

    private void RequireActive()
    {
        if (disposed) throw new ObjectDisposedException(nameof(GameplayCharacterSkillNarrativeContext));
    }
}
